using System.Diagnostics;
using System.Text;
using Api.Config;
using Microsoft.VisualBasic.FileIO;

namespace Api.Services.Access;

public sealed class MdbToolsCliSession : IAccessDataReaderSession
{
    private readonly string _sourceFilePath;
    private readonly AccessImportOptions _options;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _metadataLimiter;
    private readonly Dictionary<string, IReadOnlyList<string>> _columnCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _exactRowCountCache = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<string>? _tableCache;
    private bool _disposed;

    public MdbToolsCliSession(string sourceFilePath, AccessImportOptions options, ILogger logger)
    {
        _sourceFilePath = sourceFilePath;
        _options = options;
        _logger = logger;
        _metadataLimiter = new SemaphoreSlim(Math.Max(1, options.MaxMetadataParallelism));
    }

    public string Mode => "cli";

    public string SourceFilePath => _sourceFilePath;
    public bool SupportsPredicatePushdown => false;

    public async Task<IReadOnlyList<string>> GetTablesAsync(bool includeTemporaryTables = false, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (_tableCache is not null)
            return FilterVisibleTables(_tableCache, includeTemporaryTables);

        await _metadataLimiter.WaitAsync(ct);
        try
        {
            if (_tableCache is not null)
                return FilterVisibleTables(_tableCache, includeTemporaryTables);

            var handle = StartProcess("mdb-tables", $"-1 \"{_sourceFilePath}\"", ct);
            try
            {
                var tables = new List<string>();
                while (!handle.Process.StandardOutput.EndOfStream)
                {
                    ct.ThrowIfCancellationRequested();
                    var line = await handle.Process.StandardOutput.ReadLineAsync();
                    if (!string.IsNullOrWhiteSpace(line))
                        tables.Add(line.Trim());
                }

                await handle.FinishAsync(requireSuccess: true, killIfRunning: false);
                _tableCache = tables;
                return FilterVisibleTables(tables, includeTemporaryTables);
            }
            catch
            {
                await handle.FinishAsync(requireSuccess: false, killIfRunning: true);
                throw;
            }
        }
        finally
        {
            _metadataLimiter.Release();
        }
    }

    public async Task<IReadOnlyList<string>> GetColumnsAsync(string table, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (_columnCache.TryGetValue(table, out var cached))
            return cached;

        await _metadataLimiter.WaitAsync(ct);
        try
        {
            if (_columnCache.TryGetValue(table, out cached))
                return cached;

            var handle = StartProcess("mdb-export", $"\"{_sourceFilePath}\" \"{table}\"", ct);
            try
            {
                using var parser = CreateParser(handle.Process.StandardOutput);
                var header = parser.EndOfData ? [] : (parser.ReadFields() ?? []);
                var columns = header
                    .Select(CleanColumnName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList();

                _columnCache[table] = columns;
                await handle.FinishAsync(requireSuccess: false, killIfRunning: true);
                return columns;
            }
            catch
            {
                await handle.FinishAsync(requireSuccess: false, killIfRunning: true);
                throw;
            }
        }
        finally
        {
            _metadataLimiter.Release();
        }
    }

    public Task<AccessRowCountResult> TryGetExactRowCountAsync(string table, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return Task.FromResult(
            _exactRowCountCache.TryGetValue(table, out var count)
                ? AccessRowCountResult.Exact(count)
                : AccessRowCountResult.Unknown());
    }

    public IAsyncEnumerable<AccessDataRow> ReadRowsAsync(string table, CancellationToken ct = default)
        => ReadRowsAsync(table, query: null, ct);

    public async IAsyncEnumerable<AccessDataRow> ReadRowsAsync(
        string table,
        AccessReadQuery? query,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (query is not null)
        {
            _logger.LogDebug(
                "Access CLI session does not support predicate pushdown. Falling back to full stream and in-memory filtering. TableName: {TableName}.",
                table);
        }
        var handle = StartProcess("mdb-export", $"\"{_sourceFilePath}\" \"{table}\"", ct);
        var completed = false;
        var rowCount = 0;
        var streamSw = Stopwatch.StartNew();
        var firstRowLogged = false;
        var traceArtikli = ShouldTraceArtikli(table);

        _logger.LogInformation(
            "Access CLI row stream started. Step: {Step}. TableName: {TableName}. Mode: {Mode}. SourceFile: {SourceFile}. TraceArtikli: {TraceArtikli}.",
            "cli-row-stream",
            table,
            Mode,
            Path.GetFileName(_sourceFilePath),
            traceArtikli);

        try
        {
            using var parser = CreateParser(handle.Process.StandardOutput);
            if (parser.EndOfData)
            {
                completed = true;
                _columnCache[table] = [];
                _exactRowCountCache[table] = 0;
                await handle.FinishAsync(requireSuccess: true, killIfRunning: false);
                yield break;
            }

            var header = parser.ReadFields() ?? [];
            var columns = header
                .Select(CleanColumnName)
                .ToList();
            _columnCache[table] = columns;
            var schema = new AccessDataSchema(columns);

            _logger.LogInformation(
                "Access CLI row stream header parsed. Step: {Step}. TableName: {TableName}. ColumnCount: {ColumnCount}. ElapsedMs: {ElapsedMs}.",
                "cli-row-stream",
                table,
                columns.Count,
                streamSw.ElapsedMilliseconds);

            while (!parser.EndOfData)
            {
                ct.ThrowIfCancellationRequested();
                var fields = parser.ReadFields();
                if (fields is null)
                    break;

                var values = new object?[columns.Count];
                for (var i = 0; i < columns.Count; i++)
                {
                    var value = i < fields.Length ? fields[i] : null;
                    values[i] = string.IsNullOrEmpty(value) ? null : value;
                }

                rowCount++;
                if (!firstRowLogged)
                {
                    firstRowLogged = true;
                    _logger.LogInformation(
                        "Access CLI row stream emitted first row. Step: {Step}. TableName: {TableName}. ElapsedMs: {ElapsedMs}. ColumnCount: {ColumnCount}.",
                        "cli-row-stream",
                        table,
                        streamSw.ElapsedMilliseconds,
                        columns.Count);
                }

                if (traceArtikli && rowCount % 250 == 0)
                {
                    _logger.LogInformation(
                        "Access CLI row stream progress. Step: {Step}. TableName: {TableName}. RowsRead: {RowsRead}. ElapsedMs: {ElapsedMs}.",
                        "cli-row-stream",
                        table,
                        rowCount,
                        streamSw.ElapsedMilliseconds);
                }

                yield return new AccessDataRow(schema, values);
            }

            completed = true;
            _exactRowCountCache[table] = rowCount;
            await handle.FinishAsync(requireSuccess: true, killIfRunning: false);
            _logger.LogInformation(
                "Access CLI row stream completed. Step: {Step}. TableName: {TableName}. RowsRead: {RowsRead}. DurationMs: {DurationMs}.",
                "cli-row-stream",
                table,
                rowCount,
                streamSw.ElapsedMilliseconds);
        }
        finally
        {
            if (!completed)
                await handle.FinishAsync(requireSuccess: false, killIfRunning: true);
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;

        _disposed = true;
        _metadataLimiter.Dispose();
        return ValueTask.CompletedTask;
    }

    private static TextFieldParser CreateParser(TextReader reader)
    {
        var parser = new TextFieldParser(reader)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");
        return parser;
    }

    internal static IEnumerable<string[]> ReadCsvRecords(TextReader reader)
    {
        using var parser = CreateParser(reader);
        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields is null)
                yield break;

            yield return fields;
        }
    }

    private RunningCliProcess StartProcess(string command, string args, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        var process = new Process { StartInfo = startInfo };
        _logger.LogInformation(
            "Access CLI process starting. Step: {Step}. Command: {Command}. Arguments: {Arguments}.",
            "cli-process",
            command,
            args);
        process.Start();
        return new RunningCliProcess(process, command, _options.CliTimeoutSeconds, _logger, ct);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MdbToolsCliSession));
    }

    private static IReadOnlyList<string> FilterVisibleTables(IEnumerable<string> tables, bool includeTemporaryTables)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filtered = new List<string>();

        foreach (var table in tables)
        {
            if (string.IsNullOrWhiteSpace(table))
                continue;

            if (table.StartsWith("MSys", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!includeTemporaryTables && AccessImportService.Normalize(table).Contains("privremena", StringComparison.Ordinal))
                continue;

            if (seen.Add(table))
                filtered.Add(table);
        }

        return filtered;
    }

    private static string CleanColumnName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim().Trim('"').Trim('\uFEFF');
    }

    private static bool ShouldTraceArtikli(string table)
    {
        if (string.IsNullOrWhiteSpace(table))
            return false;

        var normalized = AccessImportService.Normalize(table);
        return normalized.Contains("artikli", StringComparison.Ordinal)
            || normalized.Contains("artikal", StringComparison.Ordinal)
            || normalized.Contains("tblart", StringComparison.Ordinal);
    }

    private sealed class RunningCliProcess : IDisposable
    {
        private readonly Process _process;
        private readonly string _command;
        private readonly int _timeoutSeconds;
        private readonly ILogger _logger;
        private readonly CancellationToken _callerCancellationToken;
        private readonly CancellationTokenSource _timeoutCts;
        private readonly CancellationTokenSource _linkedCts;
        private readonly CancellationTokenRegistration _killRegistration;
        private readonly Task<string> _stderrTask;
        private bool _finished;
        private bool _disposed;

        public RunningCliProcess(Process process, string command, int timeoutSeconds, ILogger logger, CancellationToken callerCancellationToken)
        {
            _process = process;
            _command = command;
            _timeoutSeconds = Math.Max(1, timeoutSeconds);
            _logger = logger;
            _callerCancellationToken = callerCancellationToken;
            _timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds));
            _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(callerCancellationToken, _timeoutCts.Token);
            _killRegistration = _linkedCts.Token.Register(() => TryKillProcess(process));
            _stderrTask = process.StandardError.ReadToEndAsync();
        }

        public Process Process => _process;

        public async Task FinishAsync(bool requireSuccess, bool killIfRunning)
        {
            if (_finished)
                return;

            _finished = true;

            try
            {
                if (killIfRunning && !_process.HasExited)
                    TryKillProcess(_process);

                if (!_process.HasExited)
                    await _process.WaitForExitAsync();

                var stderr = await _stderrTask;

                if (_callerCancellationToken.IsCancellationRequested)
                    _callerCancellationToken.ThrowIfCancellationRequested();

                if (_timeoutCts.IsCancellationRequested && !_callerCancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning(
                        "Access CLI process timed out. Step: {Step}. Command: {Command}. TimeoutSeconds: {TimeoutSeconds}.",
                        "cli-process",
                        _command,
                        _timeoutSeconds);
                    throw new TimeoutException($"{_command} exceeded the configured timeout of {_timeoutSeconds} seconds.");
                }

                if (requireSuccess && _process.ExitCode != 0)
                {
                    _logger.LogWarning(
                        "Access CLI process failed. Step: {Step}. Command: {Command}. ExitCode: {ExitCode}.",
                        "cli-process",
                        _command,
                        _process.ExitCode);
                    throw new InvalidOperationException($"{_command} failed: {stderr}".Trim());
                }

                _logger.LogInformation(
                    "Access CLI process completed. Step: {Step}. Command: {Command}. ExitCode: {ExitCode}. Timeout: {Timeout}.",
                    "cli-process",
                    _command,
                    _process.ExitCode,
                    false);

                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    _logger.LogDebug(
                        "{Command} stderr for access import process: {Stderr}",
                        _command,
                        stderr.Trim());
                }
            }
            finally
            {
                _killRegistration.Dispose();
                _linkedCts.Dispose();
                _timeoutCts.Dispose();
                _process.Dispose();
            }
        }

        private static void TryKillProcess(Process process)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _killRegistration.Dispose();
            _linkedCts.Dispose();
            _timeoutCts.Dispose();
            _process.Dispose();
        }
    }
}

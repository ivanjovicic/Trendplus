using Api.Config;
using Api.Services.Access;
using global::Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data.Odbc;

namespace Api.Services.DataSources;

public interface IDataSourceProfileCatalog
{
    IReadOnlyList<DataSourceProfileSummary> ListProfiles();
    bool TryGetProfile(string profileName, out NamedDataSourceProfile profile, out string? error);
}

public interface ISourceDataSessionFactory
{
    ISourceDataSession Create(NamedDataSourceProfile profile);
}

public interface IDataSourceDiscoveryService
{
    IReadOnlyList<DataSourceProfileSummary> ListProfiles();
    Task<DataSourceConnectionTestResult> TestConnectionAsync(string profileName, CancellationToken ct = default);
    Task<IReadOnlyList<DataSourceSchemaSummary>> GetSchemasAsync(string profileName, CancellationToken ct = default);
    Task<IReadOnlyList<DataSourceTableSummary>> GetTablesAsync(
        string profileName,
        string? schema = null,
        bool includeTemporaryTables = false,
        CancellationToken ct = default);
    Task<IReadOnlyList<DataSourceColumnSummary>> GetColumnsAsync(string profileName, string table, CancellationToken ct = default);
}

public sealed record DataSourceProfileSummary(
    string Name,
    string Provider,
    string Mode,
    bool Enabled,
    string? DefaultSchema,
    string? Description);

public sealed record NamedDataSourceProfile(
    string Name,
    string Provider,
    string Mode,
    bool Enabled,
    string? ConnectionString,
    string? FilePath,
    string? DefaultSchema,
    string? Description,
    int CommandTimeoutSeconds);

public sealed record DataSourceConnectionTestResult(
    string ProfileName,
    string Provider,
    string Mode,
    bool Ok,
    string Category,
    string Message);

public sealed record DataSourceSchemaSummary(string Name);

public sealed record DataSourceTableSummary(string Identifier, string Schema, string Name);

public sealed record DataSourceColumnSummary(string Name);

public sealed class DataSourceProfileCatalog : IDataSourceProfileCatalog
{
    private readonly IOptionsMonitor<DataSourceOptions> _options;

    public DataSourceProfileCatalog(IOptionsMonitor<DataSourceOptions> options)
    {
        _options = options;
    }

    public IReadOnlyList<DataSourceProfileSummary> ListProfiles()
    {
        return GetProfiles()
            .Where(profile => profile.Enabled)
            .Select(ToSummary)
            .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public bool TryGetProfile(string profileName, out NamedDataSourceProfile profile, out string? error)
    {
        profile = default!;
        error = null;

        if (string.IsNullOrWhiteSpace(profileName))
        {
            error = "Profile name is required.";
            return false;
        }

        var match = GetProfiles()
            .FirstOrDefault(candidate => string.Equals(candidate.Name, profileName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            error = $"Data source profile '{profileName}' was not found.";
            return false;
        }

        if (!match.Enabled)
        {
            error = $"Data source profile '{profileName}' is disabled.";
            return false;
        }

        profile = match;
        return true;
    }

    private IReadOnlyList<NamedDataSourceProfile> GetProfiles()
    {
        var rawProfiles = _options.CurrentValue.Profiles ?? [];
        return rawProfiles
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Name) && !string.IsNullOrWhiteSpace(profile.Provider))
            .Select(profile => new NamedDataSourceProfile(
                profile.Name.Trim(),
                profile.Provider.Trim().ToLowerInvariant(),
                ResolveMode(profile),
                profile.Enabled,
                string.IsNullOrWhiteSpace(profile.ConnectionString) ? null : profile.ConnectionString,
                string.IsNullOrWhiteSpace(profile.FilePath) ? null : profile.FilePath,
                string.IsNullOrWhiteSpace(profile.DefaultSchema) ? null : profile.DefaultSchema.Trim(),
                string.IsNullOrWhiteSpace(profile.Description) ? null : profile.Description.Trim(),
                Math.Clamp(profile.CommandTimeoutSeconds ?? 30, 1, 3600)))
            .ToArray();
    }

    private static string ResolveMode(NamedDataSourceProfileOptions profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.Mode))
            return profile.Mode.Trim().ToLowerInvariant();

        return profile.Provider.Trim().ToLowerInvariant() switch
        {
            "sqlserver" => "sqlclient",
            "access" => OperatingSystem.IsWindows() ? "windows" : "cli",
            _ => "unknown"
        };
    }

    private static DataSourceProfileSummary ToSummary(NamedDataSourceProfile profile)
        => new(
            profile.Name,
            profile.Provider,
            profile.Mode,
            profile.Enabled,
            profile.DefaultSchema,
            profile.Description);
}

public sealed class SourceDataSessionFactory : ISourceDataSessionFactory
{
    private readonly AccessImportOptions _accessOptions;
    private readonly ILoggerFactory _loggerFactory;

    public SourceDataSessionFactory(IOptions<AccessImportOptions> accessOptions, ILoggerFactory loggerFactory)
    {
        _accessOptions = accessOptions.Value;
        _loggerFactory = loggerFactory;
    }

    public ISourceDataSession Create(NamedDataSourceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return profile.Provider switch
        {
            "sqlserver" => CreateSqlServerSession(profile),
            "access" => CreateAccessSession(profile),
            _ => throw new InvalidOperationException($"Unsupported data source provider '{profile.Provider}'.")
        };
    }

    private ISourceDataSession CreateSqlServerSession(NamedDataSourceProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.ConnectionString))
            throw new InvalidOperationException($"Data source profile '{profile.Name}' is missing a connection string.");

        return new SqlServerSourceDataSession(
            profile.ConnectionString,
            _loggerFactory.CreateLogger<SqlServerSourceDataSession>(),
            commandTimeoutSeconds: profile.CommandTimeoutSeconds);
    }

    private ISourceDataSession CreateAccessSession(NamedDataSourceProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.FilePath))
            throw new InvalidOperationException($"Data source profile '{profile.Name}' is missing a file path.");

        var logger = _loggerFactory.CreateLogger<AccessSourceDataSessionAdapter>();
        IAccessDataReaderSession inner = OperatingSystem.IsWindows() && !_accessOptions.EnableMdbSql
            ? new WindowsAccessSession(profile.FilePath, _accessOptions, logger)
            : new MdbToolsCliSession(profile.FilePath, _accessOptions, logger);
        return new AccessSourceDataSessionAdapter(inner);
    }
}

public sealed class DataSourceDiscoveryService : IDataSourceDiscoveryService
{
    private readonly IDataSourceProfileCatalog _catalog;
    private readonly ISourceDataSessionFactory _sessionFactory;
    private readonly DataSourceOptions _options;
    private readonly ILogger<DataSourceDiscoveryService> _logger;

    public DataSourceDiscoveryService(
        IDataSourceProfileCatalog catalog,
        ISourceDataSessionFactory sessionFactory,
        IOptions<DataSourceOptions> options,
        ILogger<DataSourceDiscoveryService> logger)
    {
        _catalog = catalog;
        _sessionFactory = sessionFactory;
        _options = options.Value;
        _logger = logger;
    }

    public IReadOnlyList<DataSourceProfileSummary> ListProfiles() => _catalog.ListProfiles();

    public async Task<DataSourceConnectionTestResult> TestConnectionAsync(string profileName, CancellationToken ct = default)
    {
        var profile = GetProfile(profileName);
        try
        {
            await using var session = _sessionFactory.Create(profile);
            await ExecuteWithTimeoutAsync(profile, DataSourceDiscoveryOperation.ConnectionTest, session.TestConnectionAsync, ct);

            return new DataSourceConnectionTestResult(
                profile.Name,
                profile.Provider,
                profile.Mode,
                Ok: true,
                Category: "ok",
                Message: "Connection test succeeded.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var safeError = MapSafeError(ex);
            _logger.LogWarning(
                ex,
                "Data source connection test failed. Profile={ProfileName} Provider={Provider} Category={Category}.",
                profile.Name,
                profile.Provider,
                safeError.Category);
            return new DataSourceConnectionTestResult(
                profile.Name,
                profile.Provider,
                profile.Mode,
                Ok: false,
                safeError.Category,
                safeError.Message);
        }
    }

    public async Task<IReadOnlyList<DataSourceSchemaSummary>> GetSchemasAsync(string profileName, CancellationToken ct = default)
    {
        var profile = GetProfile(profileName);
        await using var session = _sessionFactory.Create(profile);
        var tables = await ExecuteWithTimeoutAsync(
            profile,
            DataSourceDiscoveryOperation.Discovery,
            token => session.GetTablesAsync(includeTemporaryTables: false, token),
            ct);

        return tables
            .Select(table => ParseTableIdentifier(table, profile.DefaultSchema).Schema)
            .Where(schema => !string.IsNullOrWhiteSpace(schema))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(schema => schema, StringComparer.OrdinalIgnoreCase)
            .Select(schema => new DataSourceSchemaSummary(schema))
            .ToArray();
    }

    public async Task<IReadOnlyList<DataSourceTableSummary>> GetTablesAsync(
        string profileName,
        string? schema = null,
        bool includeTemporaryTables = false,
        CancellationToken ct = default)
    {
        var profile = GetProfile(profileName);
        await using var session = _sessionFactory.Create(profile);
        var tables = await ExecuteWithTimeoutAsync(
            profile,
            DataSourceDiscoveryOperation.Discovery,
            token => session.GetTablesAsync(includeTemporaryTables, token),
            ct);

        return tables
            .Select(table =>
            {
                var parsed = ParseTableIdentifier(table, profile.DefaultSchema);
                return new DataSourceTableSummary(table, parsed.Schema, parsed.Name);
            })
            .Where(table => string.IsNullOrWhiteSpace(schema)
                || string.Equals(table.Schema, schema.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(table => table.Schema, StringComparer.OrdinalIgnoreCase)
            .ThenBy(table => table.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<DataSourceColumnSummary>> GetColumnsAsync(string profileName, string table, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(table))
            throw new ArgumentException("Table identifier is required.", nameof(table));

        var profile = GetProfile(profileName);
        await using var session = _sessionFactory.Create(profile);
        var columns = await ExecuteWithTimeoutAsync(
            profile,
            DataSourceDiscoveryOperation.Discovery,
            token => session.GetColumnsAsync(table, token),
            ct);

        return columns
            .Where(column => !string.IsNullOrWhiteSpace(column))
            .Select(column => new DataSourceColumnSummary(column))
            .ToArray();
    }

    private NamedDataSourceProfile GetProfile(string profileName)
    {
        if (_catalog.TryGetProfile(profileName, out var profile, out var error))
            return profile;

        throw new KeyNotFoundException(error ?? $"Data source profile '{profileName}' was not found.");
    }

    private async Task<T> ExecuteWithTimeoutAsync<T>(
        NamedDataSourceProfile profile,
        DataSourceDiscoveryOperation operation,
        Func<CancellationToken, Task<T>> action,
        CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(GetTimeoutSeconds(operation)));

        try
        {
            return await action(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"{operation} timed out for data source profile '{profile.Name}'.");
        }
    }

    private async Task ExecuteWithTimeoutAsync(
        NamedDataSourceProfile profile,
        DataSourceDiscoveryOperation operation,
        Func<CancellationToken, Task> action,
        CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(GetTimeoutSeconds(operation)));

        try
        {
            await action(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"{operation} timed out for data source profile '{profile.Name}'.");
        }
    }

    private int GetTimeoutSeconds(DataSourceDiscoveryOperation operation)
    {
        return operation switch
        {
            DataSourceDiscoveryOperation.ConnectionTest => Math.Clamp(_options.ConnectionTestTimeoutSeconds, 1, 300),
            _ => Math.Clamp(_options.DiscoveryTimeoutSeconds, 1, 300)
        };
    }

    private static (string Category, string Message) MapSafeError(Exception ex)
    {
        return ex switch
        {
            TimeoutException => ("timeout", "The source did not respond before the configured timeout."),
            InvalidOperationException invalidOp when invalidOp.Message.Contains("Unsupported data source provider", StringComparison.OrdinalIgnoreCase)
                => ("unsupported_provider", "The source provider is not supported by this backend."),
            InvalidOperationException => ("invalid_configuration", "The source profile is incomplete or invalid."),
            ArgumentException => ("invalid_configuration", "The source profile is incomplete or invalid."),
            UnauthorizedAccessException => ("access_denied", "The source could not be opened with the configured permissions."),
            FileNotFoundException => ("source_not_found", "The configured source file or database was not found."),
            DirectoryNotFoundException => ("source_not_found", "The configured source file or database was not found."),
            SqlException sqlEx when sqlEx.Number == -2
                => ("timeout", "The source did not respond before the configured timeout."),
            SqlException sqlEx when sqlEx.Number == 18456
                => ("authentication_failed", "The source rejected the configured credentials."),
            SqlException sqlEx when sqlEx.Number == 4060
                => ("source_not_found", "The configured source file or database was not found."),
            SqlException sqlEx when sqlEx.Number is 2 or 53
                => ("connectivity_failed", "The source could not be reached with the configured network settings."),
            SqlException => ("connectivity_failed", "The source connection failed."),
            OdbcException odbcEx when MessageContains(odbcEx, "timeout")
                => ("timeout", "The source did not respond before the configured timeout."),
            OdbcException odbcEx when MessageContains(odbcEx, "login")
                => ("authentication_failed", "The source rejected the configured credentials."),
            OdbcException => ("connectivity_failed", "The source connection failed."),
            _ => ("connection_failed", "The source connection failed.")
        };
    }

    private static bool MessageContains(Exception ex, string fragment)
        => ex.Message.Contains(fragment, StringComparison.OrdinalIgnoreCase);

    private static (string Schema, string Name) ParseTableIdentifier(string identifier, string? defaultSchema)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return (defaultSchema ?? "default", string.Empty);

        var trimmed = identifier.Trim();
        if (trimmed.StartsWith('['))
        {
            var closingSchema = trimmed.IndexOf("].[", StringComparison.Ordinal);
            if (closingSchema > 0 && trimmed.EndsWith(']'))
            {
                var schema = trimmed[1..closingSchema];
                var name = trimmed[(closingSchema + 3)..^1];
                return (schema, name);
            }
        }

        var parts = trimmed.Split('.', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2
            ? (parts[0], parts[1])
            : (defaultSchema ?? "default", trimmed);
    }

    private enum DataSourceDiscoveryOperation
    {
        ConnectionTest,
        Discovery
    }
}

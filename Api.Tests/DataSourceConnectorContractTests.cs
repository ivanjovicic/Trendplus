using Api.Config;
using Api.Services;
using Api.Services.Access;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Provider-neutral characterization of the current Access source-reader seam.
/// Uses in-memory schema/row-count objects and deterministic static helpers only.
/// </summary>
public sealed class DataSourceConnectorContractTests
{
    [Fact]
    public void SourceSchema_NormalizedAliasLookup_PreservesMissingAsUnknown()
    {
        var schema = new AccessDataSchema(["IDArtikal", "Naziv", "Cena"]);
        var row = new AccessDataRow(schema, [101, "Boot", 12.5m]);

        Assert.True(row.TryGetValue("idartikal", out var idValue));
        Assert.Equal(101, idValue);

        Assert.True(row.TryGetValueNormalized(AccessImportService.Normalize("Naziv"), out var nameValue));
        Assert.Equal("Boot", nameValue);

        Assert.False(row.TryGetValue("missing_column", out var missing));
        Assert.Null(missing);

        Assert.False(row.TryGetValueNormalized("not_a_real_alias", out var missingNormalized));
        Assert.Null(missingNormalized);

        Assert.False(schema.TryGetValue("ghost", Array.Empty<object?>(), out _));
    }

    [Fact]
    public void SourceRowCount_ModePreservesExactSampledAndUnknown()
    {
        var exact = AccessRowCountResult.Exact(42);
        var sampled = AccessRowCountResult.Sampled(7);
        var unknown = AccessRowCountResult.Unknown();

        Assert.Equal("exact", exact.Mode);
        Assert.Equal(42, exact.Count);
        Assert.True(exact.IsExact);

        Assert.Equal("sampled", sampled.Mode);
        Assert.Equal(7, sampled.Count);
        Assert.False(sampled.IsExact);

        Assert.Equal("unknown", unknown.Mode);
        Assert.Equal(0, unknown.Count);
        Assert.False(unknown.IsExact);

        // Unknown with Count=0 must remain distinguishable from exact empty.
        var exactEmpty = AccessRowCountResult.Exact(0);
        Assert.True(exactEmpty.IsExact);
        Assert.NotEqual(exactEmpty.Mode, unknown.Mode);
    }

    [Fact]
    public async Task SourceCapabilities_DistinguishWindowsPushdownFromCliFallback()
    {
        var options = new AccessImportOptions();
        var logger = NullLogger.Instance;

        await using var windows = new WindowsAccessSession(@"C:\trendplus\characterization.accdb", options, logger);
        await using var cli = new MdbToolsCliSession("/tmp/trendplus/characterization.accdb", options, logger);

        Assert.Equal("windows", windows.Mode);
        Assert.True(windows.SupportsPredicatePushdown);

        Assert.Equal("cli", cli.Mode);
        Assert.False(cli.SupportsPredicatePushdown);

        // Consumer tests must read the capability flag, not infer from Mode strings alone.
        IAccessDataReaderSession windowsSession = windows;
        IAccessDataReaderSession cliSession = cli;
        Assert.True(windowsSession.SupportsPredicatePushdown);
        Assert.False(cliSession.SupportsPredicatePushdown);
    }

    [Fact]
    public void IncrementalCursor_CompositeTimestampAndId_RemainsDeterministic()
    {
        var cursorUtc = new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);
        var query = new AccessReadQuery
        {
            CursorMode = "timestamp_then_id",
            CursorTimestampUtc = cursorUtc,
            CursorTieBreakerId = 9001,
            OverlapSeconds = 30,
            TimestampAliases = ["updatedat", "lastmodified"],
            IdAliases = ["id", "idartikal"]
        };

        var (sql, parameters) = WindowsAccessSession.BuildSelectSqlFromColumns(
            columns: ["UpdatedAt", "IDArtikal", "Naziv"],
            quotedTable: "[tblArtikli]",
            query: query);

        Assert.Equal(
            "SELECT * FROM [tblArtikli] WHERE ([UpdatedAt] > ? OR ([UpdatedAt] = ? AND [IDArtikal] > ?)) ORDER BY [UpdatedAt], [IDArtikal]",
            sql);
        Assert.Equal(3, parameters.Count);

        var overlapped = cursorUtc.AddSeconds(-30);
        Assert.Equal(overlapped, parameters[0]);
        Assert.Equal(overlapped, parameters[1]);
        Assert.Equal(9001L, parameters[2]);

        Assert.True(AccessImportService.CanApplyAccessReadPushdown(
            cursorMode: query.CursorMode,
            cursorTimestampUtc: query.CursorTimestampUtc,
            cursorId: query.CursorId,
            cursorTieBreakerId: query.CursorTieBreakerId,
            timestampAliases: query.TimestampAliases,
            idAliases: query.IdAliases,
            sourceColumns: ["UpdatedAt", "IDArtikal", "Naziv"]));
    }

    [Fact]
    public void IncrementalCursor_MissingAlias_FallsBackWithoutFakeCheckpointAdvance()
    {
        var query = new AccessReadQuery
        {
            CursorMode = "id",
            CursorId = 55,
            IdAliases = ["id", "idartikal"]
        };

        var (sql, parameters) = WindowsAccessSession.BuildSelectSqlFromColumns(
            columns: ["Naziv", "Cena"],
            quotedTable: "[tblNoCursor]",
            query: query);

        Assert.Equal("SELECT * FROM [tblNoCursor]", sql);
        Assert.Empty(parameters);

        Assert.False(AccessImportService.CanApplyAccessReadPushdown(
            cursorMode: query.CursorMode,
            cursorTimestampUtc: query.CursorTimestampUtc,
            cursorId: query.CursorId,
            cursorTieBreakerId: query.CursorTieBreakerId,
            timestampAliases: query.TimestampAliases,
            idAliases: query.IdAliases,
            sourceColumns: ["Naziv", "Cena"]));
    }

    [Fact]
    public async Task SourceRowStreaming_RespectsCancellationToken()
    {
        var session = new CancellationAwareProbeSession();
        await using (session.ConfigureAwait(false))
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in session.ReadRowsAsync("tblProbe", cts.Token))
                {
                    Assert.Fail("Cancelled streaming must not yield rows.");
                }
            });
        }
    }

    private sealed class CancellationAwareProbeSession : IAccessDataReaderSession
    {
        public string Mode => "probe";
        public string SourceFilePath => "probe://in-memory";
        public bool SupportsPredicatePushdown => false;

        public Task<IReadOnlyList<string>> GetTablesAsync(bool includeTemporaryTables = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(["tblProbe"]);

        public Task<IReadOnlyList<string>> GetColumnsAsync(string table, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(["Id"]);

        public Task<AccessRowCountResult> TryGetExactRowCountAsync(string table, CancellationToken ct = default)
            => Task.FromResult(AccessRowCountResult.Unknown());

        public async IAsyncEnumerable<AccessDataRow> ReadRowsAsync(
            string table,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            yield break;
        }

        public IAsyncEnumerable<AccessDataRow> ReadRowsAsync(string table, AccessReadQuery? query, CancellationToken ct = default)
            => ReadRowsAsync(table, ct);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

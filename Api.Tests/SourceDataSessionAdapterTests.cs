using Api.Config;
using Api.Services;
using Api.Services.Access;
using Api.Services.DataSources;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Compatibility seam: Access Windows/CLI sessions satisfy ISourceDataSession through the adapter.
/// </summary>
public sealed class SourceDataSessionAdapterTests
{
    [Fact]
    public async Task AccessWindowsAndCli_SatisfyProviderNeutralSession_ViaAdapter()
    {
        var options = new AccessImportOptions();
        var logger = NullLogger.Instance;

        await using var windowsAccess = new WindowsAccessSession(@"C:\trendplus\qdb02.accdb", options, logger);
        await using var cliAccess = new MdbToolsCliSession("/tmp/trendplus/qdb02.accdb", options, logger);

        await using ISourceDataSession windows = new AccessSourceDataSessionAdapter(windowsAccess);
        await using ISourceDataSession cli = new AccessSourceDataSessionAdapter(cliAccess);

        Assert.Equal("access", windows.Provider);
        Assert.Equal("access", cli.Provider);

        Assert.Equal("windows", windows.Mode);
        Assert.Equal("cli", cli.Mode);

        Assert.Equal(@"C:\trendplus\qdb02.accdb", windows.SourceIdentity);
        Assert.Equal("/tmp/trendplus/qdb02.accdb", cli.SourceIdentity);

        Assert.True(windows.Capabilities.PredicatePushdown);
        Assert.False(cli.Capabilities.PredicatePushdown);

        // Capability object must drive behavior checks — not Mode/Provider string switches.
        Assert.True(windows.Capabilities.SchemaDiscovery);
        Assert.True(cli.Capabilities.SchemaDiscovery);
        Assert.False(windows.Capabilities.Cdc);
        Assert.False(cli.Capabilities.Cdc);
    }

    [Fact]
    public void SourceCapabilities_ReplaceProviderNameSwitches()
    {
        var pushdown = new SourceCapabilities(PredicatePushdown: true);
        var fallback = new SourceCapabilities(PredicatePushdown: false);

        Assert.True(CanUsePushdown(pushdown));
        Assert.False(CanUsePushdown(fallback));

        static bool CanUsePushdown(SourceCapabilities capabilities)
            => capabilities.PredicatePushdown;
    }

    [Fact]
    public void QueryAndRowCount_RoundTripBetweenAccessAndSourceContracts()
    {
        var sourceQuery = new SourceReadQuery
        {
            CursorMode = "timestamp_then_id",
            CursorTimestampUtc = new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc),
            CursorTieBreakerId = 42,
            OverlapSeconds = 15,
            TimestampAliases = ["updatedat"],
            IdAliases = ["id"]
        };

        var accessQuery = AccessSourceDataSessionAdapter.ToAccessQuery(sourceQuery);
        var roundTrip = AccessSourceDataSessionAdapter.ToSourceQuery(accessQuery);

        Assert.Equal(sourceQuery.CursorMode, roundTrip.CursorMode);
        Assert.Equal(sourceQuery.CursorTimestampUtc, roundTrip.CursorTimestampUtc);
        Assert.Equal(sourceQuery.CursorTieBreakerId, roundTrip.CursorTieBreakerId);
        Assert.Equal(sourceQuery.OverlapSeconds, roundTrip.OverlapSeconds);
        Assert.Equal(sourceQuery.TimestampAliases, roundTrip.TimestampAliases);
        Assert.Equal(sourceQuery.IdAliases, roundTrip.IdAliases);

        Assert.Equal("exact", AccessSourceDataSessionAdapter.MapRowCount(AccessRowCountResult.Exact(3)).Mode);
        Assert.Equal("sampled", AccessSourceDataSessionAdapter.MapRowCount(AccessRowCountResult.Sampled(9)).Mode);
        Assert.Equal("unknown", AccessSourceDataSessionAdapter.MapRowCount(AccessRowCountResult.Unknown()).Mode);
    }

    [Fact]
    public async Task Adapter_PreservesNormalizedLookupAndCancellation()
    {
        var probe = new ProbeAccessSession();
        await using var session = new AccessSourceDataSessionAdapter(probe);

        await foreach (var row in session.ReadRowsAsync("tblProbe"))
        {
            Assert.True(row.TryGetValue("idartikal", out var id));
            Assert.Equal(7, id);
            Assert.False(row.TryGetValue("missing", out _));
        }

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

    [Fact]
    public async Task Adapter_ExposesAccessSessionForGradualMigration()
    {
        var probe = new ProbeAccessSession();
        await using var adapter = new AccessSourceDataSessionAdapter(probe);

        Assert.Same(probe, adapter.AccessSession);
        Assert.Equal("probe", adapter.AccessSession.Mode);
        Assert.False(adapter.AccessSession.SupportsPredicatePushdown);
        Assert.Equal(adapter.AccessSession.SupportsPredicatePushdown, adapter.Capabilities.PredicatePushdown);
    }

    private sealed class ProbeAccessSession : IAccessDataReaderSession
    {
        public string Mode => "probe";
        public string SourceFilePath => "probe://qdb02";
        public bool SupportsPredicatePushdown => false;

        public Task<IReadOnlyList<string>> GetTablesAsync(bool includeTemporaryTables = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(["tblProbe"]);

        public Task<IReadOnlyList<string>> GetColumnsAsync(string table, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(["IDArtikal", "Naziv"]);

        public Task<AccessRowCountResult> TryGetExactRowCountAsync(string table, CancellationToken ct = default)
            => Task.FromResult(AccessRowCountResult.Exact(1));

        public async IAsyncEnumerable<AccessDataRow> ReadRowsAsync(
            string table,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            var schema = new AccessDataSchema(["IDArtikal", "Naziv"]);
            yield return new AccessDataRow(schema, [7, "Boot"]);
        }

        public IAsyncEnumerable<AccessDataRow> ReadRowsAsync(string table, AccessReadQuery? query, CancellationToken ct = default)
            => ReadRowsAsync(table, ct);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

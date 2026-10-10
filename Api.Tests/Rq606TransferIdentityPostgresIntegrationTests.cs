using System.Reflection;
using System.Runtime.CompilerServices;
using Api.Models;
using Api.Services;
using Api.Services.Access;
using Domain.Model;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
[Trait("Suite", "RQ606")]
public sealed class Rq606TransferIdentityPostgresIntegrationTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public Rq606TransferIdentityPostgresIntegrationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "RQ606 transfer re-import preserves distinct documents and repeated article lines")]
    public async Task TransferImport_UsesSourceDocumentAndLineMultiset_AndIsIdempotent()
    {
        var connectionString = await CreateDatabaseAsync("tp_rq606_transfer");
        if (connectionString is null)
            return;

        await using var db = CreateDb(connectionString);
        await db.Database.MigrateAsync();

        var rows = BuildSyntheticTransferRows();
        var session = new SyntheticMdbSession(rows);
        var service = new AccessImportService(
            db,
            analyticsDb: null!,
            NullLogger<AccessImportService>.Instance);
        SetPrivateField(service, "_activeBatchId", 606L);

        var first = new AccessImportRunResponse { BatchId = 606, Status = "running" };
        await InvokeTransferImportAsync(service, session, first, CancellationToken.None);
        await FlushTransferWritesAsync(service);

        Assert.Equal(4, first.CoverageByTable["prenos_robe"].SourceRows);
        var firstCoverage = first.CoverageByTable["prenos_robe"];
        Assert.True(
            first.PrenosRobeInserted == 8,
            $"source={firstCoverage.SourceRows}; accepted={firstCoverage.AcceptedRows}; inserted={first.PrenosRobeInserted}");
        var firstRows = await db.DnevnikPromena.AsNoTracking()
            .Where(row => row.SourceTableKey == "prenosrobe" && row.SourceRowId != null)
            .OrderBy(row => row.SourceRowId)
            .ThenBy(row => row.TipPromene)
            .ToListAsync();
        Assert.Equal(8, firstRows.Count);
        Assert.All(firstRows, row => Assert.Equal("prenosrobe", row.SourceTableKey));

        Assert.Equal(1, firstRows.Count(row => row.SourceRowId == 1001 && row.TipPromene == TipPromeneConstants.PrenosIzlaz));
        Assert.Equal(1, firstRows.Count(row => row.SourceRowId == 1001 && row.TipPromene == TipPromeneConstants.PrenosUlaz));
        Assert.Equal(1, firstRows.Count(row => row.SourceRowId == 1002 && row.TipPromene == TipPromeneConstants.PrenosIzlaz));
        Assert.Equal(1, firstRows.Count(row => row.SourceRowId == 1002 && row.TipPromene == TipPromeneConstants.PrenosUlaz));
        Assert.Equal(2, firstRows.Count(row => row.SourceRowId == 2001 && row.TipPromene == TipPromeneConstants.PrenosIzlaz));
        Assert.Equal(2, firstRows.Count(row => row.SourceRowId == 2001 && row.TipPromene == TipPromeneConstants.PrenosUlaz));

        var second = new AccessImportRunResponse { BatchId = 606, Status = "running" };
        await InvokeTransferImportAsync(service, session, second, CancellationToken.None);
        await FlushTransferWritesAsync(service);

        Assert.Equal(0, second.PrenosRobeInserted);
        Assert.Equal(8, await db.DnevnikPromena.AsNoTracking().CountAsync(
            row => row.SourceTableKey == "prenosrobe" && row.SourceRowId != null));
    }

    [Fact(DisplayName = "RQ606 transfer import cancellation leaves no pending movement rows")]
    public async Task TransferImport_CancellationBeforeWrite_DoesNotPersistRows()
    {
        var connectionString = await CreateDatabaseAsync("tp_rq606_cancel");
        if (connectionString is null)
            return;

        await using var db = CreateDb(connectionString);
        await db.Database.MigrateAsync();
        var service = new AccessImportService(db, null!, NullLogger<AccessImportService>.Instance);
        var session = new SyntheticMdbSession(BuildSyntheticTransferRows());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => InvokeTransferImportAsync(
            service,
            session,
            new AccessImportRunResponse { BatchId = 606, Status = "running" },
            cancellation.Token));

        Assert.Equal(0, await db.DnevnikPromena.AsNoTracking().CountAsync());
    }

    private async Task<string?> CreateDatabaseAsync(string prefix)
    {
        if (!_fixture.IsAvailable)
            return null;

        return await _fixture.TryCreateDatabaseConnectionStringAsync($"{prefix}_{Guid.NewGuid():N}");
    }

    private static TrendplusDbContext CreateDb(string connectionString)
        => new(new DbContextOptionsBuilder<TrendplusDbContext>().UseNpgsql(connectionString).Options);

    private static IReadOnlyList<AccessDataRow> BuildSyntheticTransferRows()
    {
        var schema = new AccessDataSchema(
        [
            "IDDnevnik", "IDArtikal", "Kolicina", "Datum", "Cena",
            "IDObjekatIz", "IDObjekatUlaz", "BrDokumenta"
        ]);
        var date = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Unspecified);

        return
        [
            // Two different source documents intentionally have equal business values.
            new AccessDataRow(schema, [1001, 7001, 2, date, 100m, 1, 2, "T-A"]),
            new AccessDataRow(schema, [1002, 7001, 2, date, 100m, 3, 4, "T-B"]),
            // The same source document contains two repeated article lines.
            new AccessDataRow(schema, [2001, 7002, 1, date, 50m, 1, 2, "T-C"]),
            new AccessDataRow(schema, [2001, 7002, 1, date, 50m, 1, 2, "T-C"])
        ];
    }

    private static async Task InvokeTransferImportAsync(
        AccessImportService service,
        IAccessDataReaderSession session,
        AccessImportRunResponse result,
        CancellationToken ct)
    {
        var method = typeof(AccessImportService).GetMethod(
            "ImportPrenosRobeAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = (Task?)method!.Invoke(service, [session, "synthetic_prenos", false, result, ct]);
        Assert.NotNull(task);
        await task!;
    }

    private static void SetPrivateField<T>(AccessImportService service, string name, T value)
        => typeof(AccessImportService)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(service, value);

    private static async Task FlushTransferWritesAsync(AccessImportService service)
    {
        var method = typeof(AccessImportService).GetMethod(
            "FlushTrendWritesAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = (Task?)method!.Invoke(service, [true, CancellationToken.None]);
        Assert.NotNull(task);
        await task!;
    }

    private sealed class SyntheticMdbSession(IReadOnlyList<AccessDataRow> rows) : IAccessDataReaderSession
    {
        public string Mode => "synthetic-mdb";
        public string SourceFilePath => "synthetic-rq606.mdb";
        public bool SupportsPredicatePushdown => false;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task<IReadOnlyList<string>> GetTablesAsync(bool includeTemporaryTables = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(["synthetic_prenos"]);

        public Task<IReadOnlyList<string>> GetColumnsAsync(string table, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(rows.Count == 0 ? [] : rows[0].Columns);

        public Task<AccessRowCountResult> TryGetExactRowCountAsync(string table, CancellationToken ct = default)
            => Task.FromResult(AccessRowCountResult.Exact(rows.Count));

        public IAsyncEnumerable<AccessDataRow> ReadRowsAsync(string table, CancellationToken ct = default)
            => ReadRowsCoreAsync(ct);

        public IAsyncEnumerable<AccessDataRow> ReadRowsAsync(string table, AccessReadQuery? query, CancellationToken ct = default)
            => ReadRowsCoreAsync(ct);

        private async IAsyncEnumerable<AccessDataRow> ReadRowsCoreAsync([EnumeratorCancellation] CancellationToken ct)
        {
            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return row;
            }
        }
    }
}

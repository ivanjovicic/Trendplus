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
        await FlushTransferWritesAsync(service, CancellationToken.None);

        Assert.Equal(5, first.CoverageByTable["prenos_robe"].SourceRows);
        var firstCoverage = first.CoverageByTable["prenos_robe"];
        Assert.True(
            first.PrenosRobeInserted == 10,
            $"source={firstCoverage.SourceRows}; accepted={firstCoverage.AcceptedRows}; inserted={first.PrenosRobeInserted}");
        var firstRows = await db.DnevnikPromena.AsNoTracking()
            .Where(row => row.SourceTableKey == "prenosrobe" && row.SourceRowId != null)
            .OrderBy(row => row.SourceRowId)
            .ThenBy(row => row.TipPromene)
            .ToListAsync();
        Assert.Equal(10, firstRows.Count);
        Assert.All(firstRows, row => Assert.Equal("prenosrobe", row.SourceTableKey));

        Assert.Equal(1, firstRows.Count(row => row.SourceRowId == 1001 && row.TipPromene == TipPromeneConstants.PrenosIzlaz));
        Assert.Equal(1, firstRows.Count(row => row.SourceRowId == 1001 && row.TipPromene == TipPromeneConstants.PrenosUlaz));
        Assert.Equal(1, firstRows.Count(row => row.SourceRowId == 1002 && row.TipPromene == TipPromeneConstants.PrenosIzlaz));
        Assert.Equal(1, firstRows.Count(row => row.SourceRowId == 1002 && row.TipPromene == TipPromeneConstants.PrenosUlaz));
        Assert.Equal(2, firstRows.Count(row => row.SourceRowId == 2001 && row.TipPromene == TipPromeneConstants.PrenosIzlaz));
        Assert.Equal(2, firstRows.Count(row => row.SourceRowId == 2001 && row.TipPromene == TipPromeneConstants.PrenosUlaz));
        Assert.Equal(1, firstRows.Count(row => row.SourceRowId == -3001 && row.TipPromene == TipPromeneConstants.PrenosIzlaz));
        Assert.Equal(1, firstRows.Count(row => row.SourceRowId == -3001 && row.TipPromene == TipPromeneConstants.PrenosUlaz));

        var second = new AccessImportRunResponse { BatchId = 606, Status = "running" };
        await InvokeTransferImportAsync(service, session, second, CancellationToken.None);
        await FlushTransferWritesAsync(service, CancellationToken.None);

        Assert.Equal(0, second.PrenosRobeInserted);
        Assert.Equal(10, await db.DnevnikPromena.AsNoTracking().CountAsync(
            row => row.SourceTableKey == "prenosrobe" && row.SourceRowId != null));
    }

    [Fact(DisplayName = "RQ606 negative transfer source ID repairs a missing side without duplicating the pair")]
    public async Task TransferImport_NegativeSourceId_IsStableAndRepairsMissingSide()
    {
        var connectionString = await CreateDatabaseAsync("tp_rq606_negative");

        await using var db = CreateDb(connectionString);
        await db.Database.MigrateAsync();
        var date = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
        db.DnevnikPromena.Add(new DnevnikPromena
        {
            Id = 910001,
            TipPromene = TipPromeneConstants.PrenosIzlaz,
            Datum = date,
            ArtikalId = 7003,
            Kolicina = -2,
            NovaProdajnaCena = 100m,
            Iznos = 200m,
            IDObjekat = 99,
            BrojRacuna = "old-document",
            DataOrigin = "access",
            SourceTableKey = "prenosrobe",
            SourceRowId = -3001,
            SourceBatchId = 605
        });
        await db.SaveChangesAsync();

        var schema = new AccessDataSchema(
        [
            "IDDnevnik", "IDArtikal", "Kolicina", "Datum", "Cena",
            "IDObjekatIz", "IDObjekatUlaz", "BrDokumenta"
        ]);
        var session = new SyntheticMdbSession(
        [
            new AccessDataRow(schema, [-3001, 7003, 2, date, 100m, 5, 6, "T-N"])
        ]);
        var service = new AccessImportService(db, null!, NullLogger<AccessImportService>.Instance);
        SetPrivateField(service, "_activeBatchId", 606L);

        var result = new AccessImportRunResponse { BatchId = 606, Status = "running" };
        await InvokeTransferImportAsync(service, session, result, CancellationToken.None);
        await FlushTransferWritesAsync(service, CancellationToken.None);

        Assert.Equal(1, result.PrenosRobeInserted);
        var pair = await db.DnevnikPromena.AsNoTracking()
            .Where(row => row.SourceTableKey == "prenosrobe" && row.SourceRowId == -3001)
            .ToListAsync();
        Assert.Equal(2, pair.Count);
        Assert.Single(pair, row => row.TipPromene == TipPromeneConstants.PrenosIzlaz && row.IDObjekat == 99);
        Assert.Single(pair, row => row.TipPromene == TipPromeneConstants.PrenosUlaz && row.IDObjekat == 6);

        var second = new AccessImportRunResponse { BatchId = 606, Status = "running" };
        await InvokeTransferImportAsync(service, session, second, CancellationToken.None);
        await FlushTransferWritesAsync(service, CancellationToken.None);
        Assert.Equal(0, second.PrenosRobeInserted);
        Assert.Equal(2, await db.DnevnikPromena.AsNoTracking()
            .CountAsync(row => row.SourceTableKey == "prenosrobe" && row.SourceRowId == -3001));
    }

    [Fact(DisplayName = "RQ606 transfer import cancellation leaves no pending movement rows")]
    public async Task TransferImport_CancellationBeforeWrite_DoesNotPersistRows()
    {
        var connectionString = await CreateDatabaseAsync("tp_rq606_cancel");

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

    [Fact(DisplayName = "RQ606 cancellation after transfer flush rolls back and retry persists exactly once")]
    public async Task TransferImport_CancellationAfterWrite_RollsBackAndRetryIsIdempotent()
    {
        var connectionString = await CreateDatabaseAsync("tp_rq606_rollback");

        await using var db = CreateDb(connectionString);
        await db.Database.MigrateAsync();
        var service = new AccessImportService(db, null!, NullLogger<AccessImportService>.Instance);
        SetPrivateField(service, "_activeBatchId", 606L);
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RetriableDbContextTransaction.ExecuteAsync(
            db,
            async transactionCt =>
            {
                var result = new AccessImportRunResponse { BatchId = 606, Status = "running" };
                await InvokeTransferImportAsync(
                    service,
                    new SyntheticMdbSession(BuildSyntheticTransferRows()),
                    result,
                    transactionCt);
                await FlushTransferWritesAsync(service, transactionCt);
                Assert.Equal(10, result.PrenosRobeInserted);

                // The rows were written through the real EF/PostgreSQL transaction.
                // Cancel only after SaveChanges so rollback is the behavior under test.
                cancellation.Cancel();
                transactionCt.ThrowIfCancellationRequested();
            },
            cancellation.Token));

        await using (var verifyDb = CreateDb(connectionString))
        {
            Assert.Equal(0, await verifyDb.DnevnikPromena.AsNoTracking().CountAsync());
        }

        await using var retryDb = CreateDb(connectionString);
        var retryService = new AccessImportService(retryDb, null!, NullLogger<AccessImportService>.Instance);
        SetPrivateField(retryService, "_activeBatchId", 606L);
        var retryResult = new AccessImportRunResponse { BatchId = 606, Status = "running" };
        await RetriableDbContextTransaction.ExecuteAsync(
            retryDb,
            async transactionCt =>
            {
                await InvokeTransferImportAsync(
                    retryService,
                    new SyntheticMdbSession(BuildSyntheticTransferRows()),
                    retryResult,
                    transactionCt);
                await FlushTransferWritesAsync(retryService, transactionCt);
            });

        Assert.Equal(10, retryResult.PrenosRobeInserted);
        Assert.Equal(10, await retryDb.DnevnikPromena.AsNoTracking()
            .CountAsync(row => row.SourceTableKey == "prenosrobe"));

        var secondRetryResult = new AccessImportRunResponse { BatchId = 606, Status = "running" };
        await RetriableDbContextTransaction.ExecuteAsync(
            retryDb,
            async transactionCt =>
            {
                await InvokeTransferImportAsync(
                    retryService,
                    new SyntheticMdbSession(BuildSyntheticTransferRows()),
                    secondRetryResult,
                    transactionCt);
                await FlushTransferWritesAsync(retryService, transactionCt);
            });

        Assert.Equal(0, secondRetryResult.PrenosRobeInserted);
        Assert.Equal(10, await retryDb.DnevnikPromena.AsNoTracking()
            .CountAsync(row => row.SourceTableKey == "prenosrobe"));
    }

    private async Task<string> CreateDatabaseAsync(string prefix)
    {
        Assert.True(
            _fixture.IsAvailable,
            "RQ606 certification requires a live PostgreSQL Testcontainers fixture; unavailable PostgreSQL must fail the test, not skip it.");

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"{prefix}_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString), "RQ606 certification database creation failed.");
        return connectionString!;
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
            // A negative Access source ID is a valid stable event identity.
            new AccessDataRow(schema, [-3001, 7003, 2, date, 100m, 5, 6, "T-N"]),
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

    private static async Task FlushTransferWritesAsync(AccessImportService service, CancellationToken ct)
    {
        var method = typeof(AccessImportService).GetMethod(
            "FlushTrendWritesAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = (Task?)method!.Invoke(service, [true, ct]);
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

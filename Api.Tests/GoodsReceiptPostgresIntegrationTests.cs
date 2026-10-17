using Api.Services;
using Api.Services.Access;
using Domain.Model;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
[Trait("Suite", "GoodsReceipt")]
public sealed class GoodsReceiptPostgresIntegrationTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public GoodsReceiptPostgresIntegrationTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [PostgresRequiredFact(DisplayName = "Goods receipt commits mixed lines once and stores exact movement evidence")]
    public async Task MixedReceipt_IsAtomic_Idempotent_AndPreservesMasterPrices()
    {
        var databases = await CreateMigratedDatabasesAsync();
        await SeedAsync(databases);
        await using var operational = CreateOperational(databases.Operational);
        await using var analytics = CreateAnalytics(databases.Analytics);
        var service = CreateService(operational, analytics);
        var request = Request(Guid.NewGuid(), "FA-MIXED", [
            ExistingLine(-7, 2, 13.50m),
            ExistingLine(-7, 1, 13.50m),
            NewLine("Novi model 42", 1, 25m)
        ]) with { ConfirmDuplicateLines = true };

        var first = await service.CreateAsync(request, "pg-test-admin", CancellationToken.None);
        var replay = await service.CreateAsync(request, "pg-test-admin", CancellationToken.None);

        Assert.False(first.Replayed);
        Assert.True(replay.Replayed);
        Assert.Equal(first.Receipt.Id, replay.Receipt.Id);
        Assert.Equal(3, first.Receipt.LineCount);
        Assert.Equal(4, first.Receipt.TotalQuantity);
        Assert.Equal(65.50m, first.Receipt.PurchaseTotal);

        Assert.Equal(7, await operational.Database.SqlQueryRaw<int>("SELECT \"Kolicina\" AS \"Value\" FROM \"Artikli\" WHERE \"Id\" = -7").SingleAsync());
        Assert.Equal(100m, await operational.Database.SqlQueryRaw<decimal>("SELECT \"NabavnaCena\" AS \"Value\" FROM \"Artikli\" WHERE \"Id\" = -7").SingleAsync());
        Assert.Equal(200m, await operational.Database.SqlQueryRaw<decimal>("SELECT \"ProdajnaCena\" AS \"Value\" FROM \"Artikli\" WHERE \"Id\" = -7").SingleAsync());
        var createdId = await operational.Database.SqlQueryRaw<int>("SELECT \"Id\" AS \"Value\" FROM \"Artikli\" WHERE \"Naziv\" = 'Novi model 42'").SingleAsync();
        Assert.Equal(1, await operational.Database.SqlQueryRaw<int>($"SELECT \"Kolicina\" AS \"Value\" FROM \"Artikli\" WHERE \"Id\" = {createdId}").SingleAsync());
        Assert.Equal(-4, await operational.Database.SqlQueryRaw<int>($"SELECT \"IDObjekat\" AS \"Value\" FROM \"Artikli\" WHERE \"Id\" = {createdId}").SingleAsync());
        Assert.Equal("app", await operational.Database.SqlQueryRaw<string>($"SELECT \"DataOrigin\" AS \"Value\" FROM \"Artikli\" WHERE \"Id\" = {createdId}").SingleAsync());
        Assert.Equal(0, await operational.Database.SqlQueryRaw<long>($"SELECT COUNT(*) AS \"Value\" FROM \"Artikli\" WHERE \"Id\" = {createdId} AND (\"NabavnaCena\" IS NOT NULL OR \"ProdajnaCena\" IS NOT NULL)").SingleAsync());

        Assert.Equal(3, await operational.DnevnikPromena.AsNoTracking()
            .CountAsync(x => x.DataOrigin == "app" && x.TipPromene == TipPromeneConstants.UlazRobe));
        var stockOverlay = await AccessArticleStockOverlay.LoadAsync(operational, CancellationToken.None);
        Assert.Equal(3, stockOverlay[-7]);
        Assert.Equal(1, stockOverlay[createdId]);
        Assert.Equal(7, AccessArticleStockOverlay.Apply(4, stockOverlay[-7], previousQuantity: 7));
        Assert.Equal(7, AccessArticleStockOverlay.Apply(null, stockOverlay[-7], previousQuantity: 7));
        Assert.Equal(3, await operational.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS \"Value\" FROM \"GoodsReceiptLines\"").SingleAsync());
        Assert.Equal(3, await operational.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS \"Value\" FROM \"DnevnikPromena\" WHERE \"GoodsReceiptLineId\" IS NOT NULL").SingleAsync());

        var details = await service.GetAsync(first.Receipt.Id, CancellationToken.None);
        Assert.NotNull(details);
        Assert.Equal(3, details!.Lines.Count);
        Assert.Equal(2, details.Lines.Count(line => line.ArticleId == -7 && line.PurchaseUnitPrice == 13.50m));
        var newArticleLine = details.Lines.Single(line => line.ArticleName == "Novi model 42");
        Assert.Equal(25m, newArticleLine.PurchaseUnitPrice);
        Assert.Equal(50m, newArticleLine.SalesUnitPrice);
        Assert.All(details.Lines, line => Assert.NotNull(line.MovementId));
    }

    [PostgresRequiredFact(DisplayName = "A backdated receipt movement is durably projected once and can be retried")]
    public async Task BackdatedMovement_IsProjectedIdempotently_AndMarksOutboxAfterAnalyticsWrite()
    {
        var databases = await CreateMigratedDatabasesAsync();
        await SeedAsync(databases);
        await using var operational = CreateOperational(databases.Operational);
        await using var analytics = CreateAnalytics(databases.Analytics);
        var request = Request(Guid.NewGuid(), "FA-BACKDATED", [ExistingLine(-7, 2, 13.50m)]) with
        {
            ReceivedDate = new DateOnly(2024, 1, 2)
        };
        var created = await CreateService(operational, analytics).CreateAsync(request, "pg-test-admin", CancellationToken.None);
        Assert.Equal(0, await analytics.InventoryMovementFacts.CountAsync());

        var projected = await Workers.GoodsReceiptMovementProjector.ProjectPendingAsync(operational, analytics, CancellationToken.None);
        Assert.Equal(1, projected);

        var fact = await analytics.InventoryMovementFacts.AsNoTracking().SingleAsync();
        Assert.Equal(created.Receipt.Lines[0].MovementId, fact.SourceId);
        Assert.Equal("app", fact.DataOrigin);
        Assert.Equal(TipPromeneConstants.UlazRobe, fact.TipPromene);
        Assert.Equal(2, fact.Kolicina);
        Assert.Equal(-4, fact.StoreId);
        Assert.Equal(-3, fact.DobavljacId);
        Assert.Equal(27m, fact.Iznos);
        Assert.Equal(new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), fact.Datum);
        Assert.Equal(1, await operational.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS \"Value\" FROM \"GoodsReceiptLines\" WHERE \"AnalyticsProjectedAtUtc\" IS NOT NULL").SingleAsync());

        // Simulate a crash after analytics upsert and before an operational marker persisted.
        await operational.Database.ExecuteSqlRawAsync("UPDATE \"GoodsReceiptLines\" SET \"AnalyticsProjectedAtUtc\" = NULL");
        Assert.Equal(1, await Workers.GoodsReceiptMovementProjector.ProjectPendingAsync(operational, analytics, CancellationToken.None));
        Assert.Equal(1, await analytics.InventoryMovementFacts.CountAsync());
    }

    [PostgresRequiredFact(DisplayName = "Concurrent identical request keys do not double post stock")]
    public async Task ConcurrentSameRequestKey_CommitsExactlyOnce()
    {
        var databases = await CreateMigratedDatabasesAsync();
        await SeedAsync(databases);
        var request = Request(Guid.NewGuid(), "FA-IDEMPOTENT", [ExistingLine(-7, 3, 10m)]);

        async Task<GoodsReceiptCreateResult> CreateOneAsync()
        {
            await using var operational = CreateOperational(databases.Operational);
            await using var analytics = CreateAnalytics(databases.Analytics);
            return await CreateService(operational, analytics).CreateAsync(request, "pg-test-admin", CancellationToken.None);
        }

        var documentLockKey = string.Join('|', request.SupplierId, request.StoreId,
            request.DocumentType.Trim().ToLowerInvariant(), request.DocumentNumber.Trim().ToUpperInvariant());
        await using var gate = new NpgsqlConnection(databases.Operational);
        await gate.OpenAsync();
        await using (var lockCommand = new NpgsqlCommand(
            "SELECT pg_advisory_lock(hashtextextended(@documentKey, 0))", gate))
        {
            lockCommand.Parameters.AddWithValue("documentKey", documentLockKey);
            await lockCommand.ExecuteScalarAsync();
        }

        var firstTask = CreateOneAsync();
        var secondTask = CreateOneAsync();
        var advisoryWaiters = 0L;
        try
        {
            for (var attempt = 0; attempt < 100 && advisoryWaiters < 2; attempt++)
            {
                await using var waitersCommand = new NpgsqlCommand("""
                    SELECT COUNT(*) FROM pg_stat_activity
                    WHERE datname = current_database() AND wait_event_type = 'Lock'
                      AND wait_event = 'advisory' AND query LIKE '%pg_advisory_xact_lock%'
                    """, gate);
                advisoryWaiters = (long)(await waitersCommand.ExecuteScalarAsync())!;
                if (advisoryWaiters < 2) await Task.Delay(50);
            }
        }
        finally
        {
            await using var unlockCommand = new NpgsqlCommand(
                "SELECT pg_advisory_unlock(hashtextextended(@documentKey, 0))", gate);
            unlockCommand.Parameters.AddWithValue("documentKey", documentLockKey);
            await unlockCommand.ExecuteScalarAsync();
        }

        var results = await Task.WhenAll(firstTask, secondTask);
        Assert.Equal(2, advisoryWaiters);

        Assert.Equal(results[0].Receipt.Id, results[1].Receipt.Id);
        Assert.Equal(1, results.Count(x => !x.Replayed));
        await using var check = CreateOperational(databases.Operational);
        Assert.Equal(7, await check.Artikli.Where(x => x.Id == -7).Select(x => x.Kolicina).SingleAsync());
        Assert.Equal(1, await check.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS \"Value\" FROM \"DnevnikPromena\" WHERE \"GoodsReceiptLineId\" IS NOT NULL").SingleAsync());
        Assert.Equal(1, await check.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS \"Value\" FROM \"GoodsReceipts\"").SingleAsync());
    }

    [PostgresRequiredFact(DisplayName = "Concurrent same-document receipts require explicit partial-delivery confirmation")]
    public async Task ConcurrentSameDocument_DoesNotBypassDuplicateWarning()
    {
        var databases = await CreateMigratedDatabasesAsync();
        await SeedAsync(databases);

        async Task<GoodsReceiptCreateResult> CreateOneAsync(Guid key)
        {
            await using var operational = CreateOperational(databases.Operational);
            await using var analytics = CreateAnalytics(databases.Analytics);
            var request = Request(key, "FA-CONCURRENT-DUP", [ExistingLine(-7, 4, 10m)]);
            return await CreateService(operational, analytics).CreateAsync(request, "pg-test-admin", CancellationToken.None);
        }

        var firstKey = Guid.NewGuid();
        var secondKey = Guid.NewGuid();
        var results = await Task.WhenAll(CreateOneAsync(firstKey), CreateOneAsync(secondKey));
        Assert.Single(results, x => !x.RequiresSimilarConfirmation);
        var warning = Assert.Single(results, x => x.RequiresSimilarConfirmation);
        Assert.NotNull(warning.SimilarReceiptId);
        await using var check = CreateOperational(databases.Operational);
        Assert.Equal(8, await check.Artikli.Where(x => x.Id == -7).Select(x => x.Kolicina).SingleAsync());
        Assert.Equal(1, await check.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS \"Value\" FROM \"GoodsReceipts\"").SingleAsync());

        var warningKey = results[0].RequiresSimilarConfirmation ? firstKey : secondKey;
        var retried = await CreateOneWithConfirmationAsync(databases, warningKey);
        Assert.False(retried.RequiresSimilarConfirmation);
        await using var final = CreateOperational(databases.Operational);
        Assert.Equal(12, await final.Artikli.Where(x => x.Id == -7).Select(x => x.Kolicina).SingleAsync());
        Assert.Equal(2, await final.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS \"Value\" FROM \"GoodsReceipts\"").SingleAsync());
    }

    [PostgresRequiredFact(DisplayName = "Concurrent separate receipts serialize stock updates and allow partial same-invoice deliveries")]
    public async Task DifferentDocumentsPreserveConcurrentQuantity_AndPartialInvoiceNeedsAcknowledgement()
    {
        var databases = await CreateMigratedDatabasesAsync();
        await SeedAsync(databases);

        async Task<GoodsReceiptCreateResult> CreateOneAsync(string number)
        {
            await using var operational = CreateOperational(databases.Operational);
            await using var analytics = CreateAnalytics(databases.Analytics);
            var request = Request(Guid.NewGuid(), number, [ExistingLine(-7, 4, 10m)]);
            return await CreateService(operational, analytics).CreateAsync(request, "pg-test-admin", CancellationToken.None);
        }

        var concurrent = await Task.WhenAll(CreateOneAsync("FA-A"), CreateOneAsync("FA-B"));
        Assert.NotEqual(concurrent[0].Receipt.Id, concurrent[1].Receipt.Id);
        await using (var check = CreateOperational(databases.Operational))
            Assert.Equal(12, await check.Artikli.Where(x => x.Id == -7).Select(x => x.Kolicina).SingleAsync());

        var partialKey = Guid.NewGuid();
        var partial = Request(partialKey, "FA-PARTIAL", [ExistingLine(-7, 1, 10m)]);
        await using var partialDb = CreateOperational(databases.Operational);
        await using var partialAnalytics = CreateAnalytics(databases.Analytics);
        var partialService = CreateService(partialDb, partialAnalytics);
        var first = await partialService.CreateAsync(partial, "pg-test-admin", CancellationToken.None);
        var secondRequest = Request(Guid.NewGuid(), "FA-PARTIAL", [ExistingLine(-7, 2, 10m)]);
        var warning = await partialService.CreateAsync(secondRequest, "pg-test-admin", CancellationToken.None);
        Assert.True(warning.RequiresSimilarConfirmation);
        Assert.Equal(first.Receipt.Id, warning.SimilarReceiptId);
        Assert.Equal(13, await partialDb.Artikli.Where(x => x.Id == -7).Select(x => x.Kolicina).SingleAsync());
        var second = await partialService.CreateAsync(secondRequest with { ConfirmSimilarDocument = true }, "pg-test-admin", CancellationToken.None);
        Assert.False(second.RequiresSimilarConfirmation);
        Assert.Equal(15, await partialDb.Artikli.Where(x => x.Id == -7).Select(x => x.Kolicina).SingleAsync());
    }

    [PostgresRequiredFact(DisplayName = "A movement failure on line two rolls back header, lines and stock")]
    public async Task FailureAfterFirstMovement_RollsBackEntireReceipt()
    {
        var databases = await CreateMigratedDatabasesAsync();
        await SeedAsync(databases);
        await using (var connection = new NpgsqlConnection(databases.Operational))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("""
                CREATE FUNCTION fail_second_receipt_movement() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."BrojRacuna" = 'FA-ROLLBACK' AND EXISTS (
                        SELECT 1 FROM "GoodsReceiptLines" WHERE "Id" = NEW."GoodsReceiptLineId" AND "LineNumber" = 2) THEN
                        RAISE EXCEPTION 'injected line two failure' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER fail_second_receipt_movement_trigger BEFORE INSERT ON "DnevnikPromena"
                FOR EACH ROW EXECUTE FUNCTION fail_second_receipt_movement();
                """, connection);
            await command.ExecuteNonQueryAsync();
        }

        await using var operational = CreateOperational(databases.Operational);
        await using var analytics = CreateAnalytics(databases.Analytics);
        var request = Request(Guid.NewGuid(), "FA-ROLLBACK", [
            ExistingLine(-7, 2, 10m), ExistingLine(-7, 3, 10m), ExistingLine(-7, 4, 10m)
        ]) with { ConfirmDuplicateLines = true };

        await Assert.ThrowsAsync<PostgresException>(() =>
            CreateService(operational, analytics).CreateAsync(request, "pg-test-admin", CancellationToken.None));

        Assert.Equal(4, await operational.Artikli.Where(x => x.Id == -7).Select(x => x.Kolicina).SingleAsync());
        Assert.Equal(0, await operational.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS \"Value\" FROM \"GoodsReceipts\"").SingleAsync());
        Assert.Equal(0, await operational.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS \"Value\" FROM \"GoodsReceiptLines\"").SingleAsync());
        Assert.Equal(0, await operational.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS \"Value\" FROM \"DnevnikPromena\" WHERE \"GoodsReceiptLineId\" IS NOT NULL").SingleAsync());
    }

    [PostgresRequiredFact(DisplayName = "Unknown supplier, store, article and invalid quantities fail without writes")]
    public async Task InvalidReferencesAndQuantityAreRejectedWithoutPartialState()
    {
        var databases = await CreateMigratedDatabasesAsync();
        await SeedAsync(databases);
        await using var operational = CreateOperational(databases.Operational);
        await using var analytics = CreateAnalytics(databases.Analytics);
        var service = CreateService(operational, analytics);

        await Assert.ThrowsAsync<GoodsReceiptException>(() => service.CreateAsync(
            Request(Guid.NewGuid(), "FA-BAD-QTY", [ExistingLine(-7, -1, 10m)]), "test", CancellationToken.None));
        await Assert.ThrowsAsync<GoodsReceiptException>(() => service.CreateAsync(
            Request(Guid.NewGuid(), "FA-BAD-ARTICLE", [ExistingLine(-999, 1, 10m)]), "test", CancellationToken.None));
        await Assert.ThrowsAsync<GoodsReceiptException>(() => service.CreateAsync(
            Request(Guid.NewGuid(), "FA-BAD-SUPPLIER", [ExistingLine(-7, 1, 10m)]) with { SupplierId = -999 }, "test", CancellationToken.None));
        await Assert.ThrowsAsync<GoodsReceiptException>(() => service.CreateAsync(
            Request(Guid.NewGuid(), "FA-BAD-STORE", [ExistingLine(-7, 1, 10m)]) with { StoreId = -999 }, "test", CancellationToken.None));
        await Assert.ThrowsAsync<GoodsReceiptException>(() => service.CreateAsync(
            Request(Guid.NewGuid(), "FA-DUPLICATE-CODE", [NewLine("Duplicate shoe", 1, 10m) with { ArticleCode = "ACCESS-7" }]), "test", CancellationToken.None));
        await Assert.ThrowsAsync<GoodsReceiptException>(() => service.CreateAsync(
            Request(Guid.NewGuid(), "FA-MISSING-CLASSIFICATION", [NewLine("Unclassified shoe", 1, 10m) with { ShoeTypeId = null }]), "test", CancellationToken.None));

        Assert.Equal(4, await operational.Artikli.Where(x => x.Id == -7).Select(x => x.Kolicina).SingleAsync());
        Assert.Equal(0, await operational.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS \"Value\" FROM \"GoodsReceipts\"").SingleAsync());
        Assert.Equal(1, await operational.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS \"Value\" FROM \"Artikli\"").SingleAsync());
    }

    [PostgresRequiredFact(DisplayName = "Access article source ID collision preserves the web-owned target row")]
    public async Task AccessImport_SourceIdCollisionWithAppArticle_PreservesExistingArticle()
    {
        var databases = await CreateMigratedDatabasesAsync();
        await SeedAsync(databases);
        await using var operational = CreateOperational(databases.Operational);
        await using var analytics = CreateAnalytics(databases.Analytics);
        await operational.Database.ExecuteSqlRawAsync("""
            INSERT INTO "Artikli" ("Id", "PLU", "Naziv", "Kolicina", "IDObjekat", "UpdatedAt", "DataOrigin")
            VALUES (7, 'WEB-7', 'Web receipt article', 6, -4, NOW(), 'app')
            """);

        var schema = new AccessDataSchema(["ID", "Naziv", "PLU", "IDDobavljac", "IDTipObuce", "IDObjekat", "IDSezona", "Kolicina"]);
        var row = new AccessDataRow(schema, [7, "Imported collision", "ACCESS-7", -3, -11, -4, -12, 99]);
        var service = new AccessImportService(operational, analytics, NullLogger<AccessImportService>.Instance);
        var method = typeof(AccessImportService).GetMethod("ImportArtikliAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var session = new SingleRowAccessDataReaderSession(row);
        var result = new Api.Models.AccessImportRunResponse { BatchId = 321 };
        await using var transaction = await operational.Database.BeginTransactionAsync();
        var importTask = (Task)method!.Invoke(service, [session, "artikli", true, result, CancellationToken.None])!;

        await importTask;
        await operational.SaveChangesAsync();

        var article = await operational.Artikli.AsNoTracking().SingleAsync(x => x.Id == 7);
        Assert.Equal("Web receipt article", article.Naziv);
        Assert.Equal(6, article.Kolicina);
        Assert.Equal("app", article.DataOrigin);
        var imported = await operational.Artikli.AsNoTracking().SingleAsync(x => x.SourceRowId == 7);
        Assert.NotEqual(7, imported.Id);
        Assert.Equal("Imported collision", imported.Naziv);
        Assert.Equal(7L, imported.SourceRowId);
        Assert.Equal("synthetic-test-source", imported.SourceNamespace);

        await transaction.RollbackAsync();
    }

    [PostgresRequiredFact(DisplayName = "An Access stock snapshot serializes with a receipt so neither quantity update is lost")]
    public async Task AccessImportSnapshot_AndReceipt_DoNotLoseConcurrentStockUpdate()
    {
        var databases = await CreateMigratedDatabasesAsync();
        await SeedAsync(databases);
        await using (var connection = new NpgsqlConnection(databases.Operational))
        {
            await connection.OpenAsync();
            await using var seed = new NpgsqlCommand("""
                INSERT INTO "Artikli" ("Id", "PLU", "Naziv", "IDDobavljac", "Kolicina", "IDObjekat", "UpdatedAt", "DataOrigin")
                VALUES (7, 'ACCESS-7-POSITIVE', 'Access shoe', -3, 4, -4, NOW(), 'access')
                """, connection);
            await seed.ExecuteNonQueryAsync();
        }

        await using var accessDb = CreateOperational(databases.Operational);
        await using var accessAnalytics = CreateAnalytics(databases.Analytics);
        await using var accessTransaction = await accessDb.Database.BeginTransactionAsync();
        var row = new AccessDataRow(
            new AccessDataSchema(["ID", "Naziv", "PLU", "IDDobavljac", "Kolicina", "IDObjekat"]),
            [7, "Access shoe", "ACCESS-7-POSITIVE", -3, 4, -4]);
        var importService = new AccessImportService(accessDb, accessAnalytics, NullLogger<AccessImportService>.Instance);
        var importMethod = typeof(AccessImportService).GetMethod("ImportArtikliAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(importMethod);
        var import = (Task)importMethod!.Invoke(importService,
            [new SingleRowAccessDataReaderSession(row), "artikli", true, new Api.Models.AccessImportRunResponse { BatchId = 322 }, CancellationToken.None])!;
        await import;

        async Task<GoodsReceiptCreateResult> CreateReceiptAsync()
        {
            await using var receiptDb = CreateOperational(databases.Operational);
            await using var receiptAnalytics = CreateAnalytics(databases.Analytics);
            return await CreateService(receiptDb, receiptAnalytics).CreateAsync(
                Request(Guid.NewGuid(), "FA-ACCESS-RACE", [ExistingLine(7, 2, 10m)]), "pg-test-admin", CancellationToken.None);
        }

        var receiptTask = CreateReceiptAsync();
        await using var observer = new NpgsqlConnection(databases.Operational);
        await observer.OpenAsync();
        var lockWaiters = 0L;
        for (var attempt = 0; attempt < 100 && lockWaiters == 0; attempt++)
        {
            await using var waiters = new NpgsqlCommand("""
                SELECT COUNT(*) FROM pg_stat_activity
                WHERE datname = current_database() AND wait_event_type = 'Lock'
                  AND query LIKE '%FROM "Artikli" WHERE "Id"%FOR UPDATE%'
                """, observer);
            lockWaiters = (long)(await waiters.ExecuteScalarAsync())!;
            if (lockWaiters == 0) await Task.Delay(50);
        }
        Assert.True(lockWaiters > 0, "The receipt must wait on the row lock held by the Access stock snapshot.");

        await accessDb.SaveChangesAsync();
        await accessTransaction.CommitAsync();
        var receipt = await receiptTask.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("confirmed", receipt.Receipt.Status);

        await using var verify = CreateOperational(databases.Operational);
        Assert.Equal(6, await verify.Artikli.Where(x => x.Id == 7).Select(x => x.Kolicina).SingleAsync());
    }

    [Fact(DisplayName = "Access import does not repair PostgreSQL target sequences")]
    public void AccessImport_DoesNotRepairTargetSequences()
    {
        var repair = typeof(AccessImportService).GetMethod("ResetTrendplusSequencesAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.Null(repair);
    }

    [PostgresRequiredFact(DisplayName = "Full storno reverses stock once, retains audit, and projects negative movements")]
    public async Task ReverseReceipt_IsAtomic_IdempotentAndProjectsNegativeMovement()
    {
        var databases = await CreateMigratedDatabasesAsync();
        await SeedAsync(databases);
        await using var operational = CreateOperational(databases.Operational);
        await using var analytics = CreateAnalytics(databases.Analytics);
        var service = CreateService(operational, analytics);

        var created = await service.CreateAsync(
            Request(Guid.NewGuid(), "FA-REVERSAL", [ExistingLine(-7, 2, 13.50m)]),
            "stock-clerk-1", CancellationToken.None);
        var request = new GoodsReceiptReverseRequest(Guid.NewGuid(), "Pogrešno evidentirana količina");
        var reversed = await service.ReverseAsync(created.Receipt.Id, request, "stock-clerk-1", CancellationToken.None);
        var retry = await service.ReverseAsync(created.Receipt.Id, request, "stock-clerk-1", CancellationToken.None);

        Assert.Equal("reversed", reversed.Status);
        Assert.False(reversed.Replayed);
        Assert.True(retry.Replayed);
        Assert.Equal("stock-clerk-1", reversed.ReversedBy);
        Assert.Equal(4, await operational.Artikli.Where(a => a.Id == -7).Select(a => a.Kolicina).SingleAsync());
        Assert.Equal("reversed", (await service.GetAsync(created.Receipt.Id, CancellationToken.None))!.Status);

        var delta = await AccessArticleStockOverlay.LoadAsync(operational, CancellationToken.None);
        Assert.Equal(0, delta[-7]);
        var negative = await operational.DnevnikPromena.AsNoTracking()
            .SingleAsync(x => x.DataOrigin == "app" && x.Kolicina == -2);
        Assert.Equal(-27m, negative.Iznos);

        Assert.Equal(2, await Workers.GoodsReceiptMovementProjector.ProjectPendingAsync(operational, analytics, CancellationToken.None));
        Assert.Equal(2, await analytics.InventoryMovementFacts.CountAsync());
        Assert.Equal(0, await analytics.InventoryMovementFacts.SumAsync(x => x.Kolicina!.Value));
        Assert.Equal(0, await Workers.GoodsReceiptMovementProjector.ProjectPendingAsync(operational, analytics, CancellationToken.None));
        Assert.Equal(1, await operational.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS \"Value\" FROM \"GoodsReceiptLines\" WHERE \"ReversalProjectedAtUtc\" IS NOT NULL").SingleAsync());
    }

    [PostgresRequiredFact(DisplayName = "Storno fails closed when received stock has already been consumed")]
    public async Task ReverseReceipt_InsufficientStock_RollsBackWithoutAuditMutation()
    {
        var databases = await CreateMigratedDatabasesAsync();
        await SeedAsync(databases);
        await using var operational = CreateOperational(databases.Operational);
        await using var analytics = CreateAnalytics(databases.Analytics);
        var service = CreateService(operational, analytics);
        var created = await service.CreateAsync(
            Request(Guid.NewGuid(), "FA-SOLD-AFTER-RECEIPT", [ExistingLine(-7, 2, 10m)]),
            "stock-clerk-1", CancellationToken.None);

        await operational.Database.ExecuteSqlRawAsync(
            "UPDATE \"Artikli\" SET \"Kolicina\" = 1 WHERE \"Id\" = -7");
        var error = await Assert.ThrowsAsync<GoodsReceiptException>(() => service.ReverseAsync(
            created.Receipt.Id, new GoodsReceiptReverseRequest(Guid.NewGuid(), "Ovaj dokument je pogrešan"),
            "stock-clerk-1", CancellationToken.None));
        Assert.Equal("reversal_stock_insufficient", error.Code);
        Assert.Equal(1, await operational.Artikli.Where(a => a.Id == -7).Select(a => a.Kolicina).SingleAsync());
        Assert.Equal("confirmed", (await service.GetAsync(created.Receipt.Id, CancellationToken.None))!.Status);
        Assert.Equal(0, await operational.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS \"Value\" FROM \"GoodsReceiptLines\" WHERE \"ReversalMovementId\" IS NOT NULL").SingleAsync());
    }

    [PostgresRequiredFact(DisplayName = "Two storno requests cannot subtract stock twice")]
    public async Task ReverseReceipt_ConcurrentRequests_AppliesOnlyOnce()
    {
        var databases = await CreateMigratedDatabasesAsync();
        await SeedAsync(databases);
        long id;
        await using (var op = CreateOperational(databases.Operational))
        await using (var analytics = CreateAnalytics(databases.Analytics))
        {
            id = (await CreateService(op, analytics).CreateAsync(
                Request(Guid.NewGuid(), "FA-STORNO-RACE", [ExistingLine(-7, 1, 10m)]),
                "stock-clerk-1", CancellationToken.None)).Receipt.Id;
        }
        var request = new GoodsReceiptReverseRequest(Guid.NewGuid(), "Zamenjen dobavljački dokument");
        async Task<GoodsReceiptReverseResponse> Reverse()
        {
            await using var op = CreateOperational(databases.Operational);
            await using var an = CreateAnalytics(databases.Analytics);
            return await CreateService(op, an).ReverseAsync(id, request, "stock-clerk-1", CancellationToken.None);
        }

        var results = await Task.WhenAll(Reverse(), Reverse());
        Assert.Single(results.Where(x => !x.Replayed));
        Assert.Single(results.Where(x => x.Replayed));
        await using var verify = CreateOperational(databases.Operational);
        Assert.Equal(4, await verify.Artikli.Where(a => a.Id == -7).Select(a => a.Kolicina).SingleAsync());
    }

    private async Task<(string Operational, string Analytics)> CreateMigratedDatabasesAsync()
    {
        Assert.True(_fixture.IsAvailable, "Goods receipt acceptance requires disposable PostgreSQL; unavailable Testcontainers must fail.");
        var suffix = Guid.NewGuid().ToString("N");
        var operational = await _fixture.TryCreateDatabaseConnectionStringAsync($"tp_gr_op_{suffix}");
        var analytics = await _fixture.TryCreateDatabaseConnectionStringAsync($"tp_gr_an_{suffix}");
        Assert.False(string.IsNullOrWhiteSpace(operational));
        Assert.False(string.IsNullOrWhiteSpace(analytics));
        await using (var operationalDb = CreateOperational(operational!))
            await operationalDb.Database.MigrateAsync();
        await using (var analyticsDb = CreateAnalytics(analytics!))
            await analyticsDb.Database.MigrateAsync();
        return (operational!, analytics!);
    }

    private static async Task SeedAsync((string Operational, string Analytics) databases)
    {
        await using (var operational = CreateOperational(databases.Operational))
        {
            await operational.Database.ExecuteSqlRawAsync("""
                INSERT INTO "Dobavljaci" ("Id", "Naziv", "DataOrigin") VALUES (-3, 'Test supplier', 'existing');
                INSERT INTO "TipoviObuce" ("Id", "Naziv", "DataOrigin") VALUES (-11, 'Čizme', 'existing');
                INSERT INTO "Sezone" ("Id", "Naziv", "DatumOd", "DatumDo", "DataOrigin")
                    VALUES (-12, 'Jesen 2026', '2026-08-01T00:00:00Z', '2026-12-31T00:00:00Z', 'existing');
                INSERT INTO "Artikli" ("Id", "PLU", "Naziv", "IDDobavljac", "NabavnaCena", "ProdajnaCena", "Kolicina", "IDObjekat", "UpdatedAt", "DataOrigin")
                    VALUES (-7, 'ACCESS-7', 'Čizma 42', -3, 100, 200, 4, -4, NOW(), 'access');
                """);
        }
        await using (var analytics = CreateAnalytics(databases.Analytics))
        {
            await analytics.Database.ExecuteSqlRawAsync("""
                INSERT INTO "StoresDim" ("StoreId", "StoreName") VALUES (-4, 'Test store');
                """);
        }
    }

    private static GoodsReceiptService CreateService(TrendplusDbContext operational, AnalyticsDbContext analytics)
        => new(operational, analytics, NullLogger<GoodsReceiptService>.Instance);

    private static TrendplusDbContext CreateOperational(string connectionString)
        => new(new DbContextOptionsBuilder<TrendplusDbContext>().UseNpgsql(connectionString).Options);

    private static AnalyticsDbContext CreateAnalytics(string connectionString)
        => new(new DbContextOptionsBuilder<AnalyticsDbContext>().UseNpgsql(connectionString).Options);

    private static async Task<long> ReadArticleSequenceValueAsync(TrendplusDbContext operational)
    {
        var connection = (NpgsqlConnection)operational.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var lookup = new NpgsqlCommand("""SELECT pg_get_serial_sequence('"Artikli"', 'Id')""", connection);
            var sequence = (string)(await lookup.ExecuteScalarAsync())!;
            await using var read = new NpgsqlCommand($"SELECT last_value FROM {sequence}", connection);
            return (long)(await read.ExecuteScalarAsync())!;
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static GoodsReceiptRequest Request(Guid key, string number, IReadOnlyList<GoodsReceiptLineRequest> lines)
        => new(key, -3, -4, "invoice", number, new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 16), "RSD", null, false, false, lines);

    private static GoodsReceiptLineRequest ExistingLine(int articleId, int quantity, decimal cost)
        => new(articleId, null, null, quantity, cost, 200m, null, null, null);

    private static GoodsReceiptLineRequest NewLine(string name, int quantity, decimal cost)
        => new(null, "NEW-42", name, quantity, cost, 50m, -11, -12, "New receipt line");

    private static async Task<GoodsReceiptCreateResult> CreateOneWithConfirmationAsync((string Operational, string Analytics) databases, Guid key)
    {
        await using var operational = CreateOperational(databases.Operational);
        await using var analytics = CreateAnalytics(databases.Analytics);
        var request = Request(key, "FA-CONCURRENT-DUP", [ExistingLine(-7, 4, 10m)]) with { ConfirmSimilarDocument = true };
        return await CreateService(operational, analytics).CreateAsync(request, "pg-test-admin", CancellationToken.None);
    }

    private sealed class SingleRowAccessDataReaderSession(AccessDataRow row) : IAccessDataReaderSession
    {
        public string Mode => "test";
        public string SourceFilePath => "test.accdb";
        public bool SupportsPredicatePushdown => false;
        public Task<IReadOnlyList<string>> GetTablesAsync(bool includeTemporaryTables = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(["artikli"]);
        public Task<IReadOnlyList<string>> GetColumnsAsync(string table, CancellationToken ct = default)
            => Task.FromResult(row.Columns);
        public Task<AccessRowCountResult> TryGetExactRowCountAsync(string table, CancellationToken ct = default)
            => Task.FromResult(AccessRowCountResult.Exact(1));
        public async IAsyncEnumerable<AccessDataRow> ReadRowsAsync(string table, [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Yield();
            yield return row;
        }
        public IAsyncEnumerable<AccessDataRow> ReadRowsAsync(string table, AccessReadQuery? query, CancellationToken ct = default)
            => ReadRowsAsync(table, ct);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

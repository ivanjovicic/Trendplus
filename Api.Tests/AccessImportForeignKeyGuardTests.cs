using System.Reflection;
using Api.Config;
using Npgsql;
using Api.Services.Access;
using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public sealed class AccessImportForeignKeyGuardTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public AccessImportForeignKeyGuardTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresRequiredFact]
    public async Task FlushTrendWritesAsync_SkipsPendingProdajaStavke_WhenParentMissingAndSkipInvalidForeignKeysEnabled()
    {
        var database = await CreateDatabaseAsync();
        if (database.Db is null)
            return;

        await using var db = database.Db;
        var service = CreateService(db, skipInvalidForeignKeys: true);

        db.ProdajaStavke.Add(new ProdajaStavka
        {
            Id = 1,
            IdProdaja = 999,
            IdArtikal = 10,
            Kolicina = 1,
            Cena = 100m
        });
        SetPendingTrendWrites(service, 1);

        await InvokeFlushTrendWritesAsync(service);

        Assert.Equal(0, await db.ProdajaStavke.AsNoTracking().CountAsync());
    }

    [PostgresRequiredFact]
    public async Task FlushTrendWritesAsync_ThrowsClearImporterError_WhenParentMissingAndSkipInvalidForeignKeysDisabled()
    {
        var database = await CreateDatabaseAsync();
        if (database.Db is null)
            return;

        await using var db = database.Db;
        var service = CreateService(db, skipInvalidForeignKeys: false);

        db.ProdajaStavke.Add(new ProdajaStavka
        {
            Id = 1,
            IdProdaja = 999,
            IdArtikal = 10,
            Kolicina = 1,
            Cena = 100m
        });
        SetPendingTrendWrites(service, 1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeFlushTrendWritesAsync(service));

        Assert.Contains("prodaja_stavke FK validation failed before flush", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await db.ProdajaStavke.AsNoTracking().CountAsync());
    }

    [PostgresRequiredFact]
    public async Task FlushTrendWritesAsync_SavesPendingProdajaHeaderAndLineTogether()
    {
        var database = await CreateDatabaseAsync();
        if (database.Db is null)
            return;

        await using var db = database.Db;
        var service = CreateService(db, skipInvalidForeignKeys: true);
        db.ChangeTracker.AutoDetectChangesEnabled = false;

        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
        {
            Id = 7,
            BrojRacuna = "R-7",
            DatumProdaje = DateTime.UtcNow,
            IDObjekat = 1,
            DataOrigin = "access"
        });
        db.ProdajaStavke.Add(new ProdajaStavka
        {
            Id = 1,
            IdProdaja = 7,
            IdArtikal = 10,
            Kolicina = 1,
            Cena = 100m
        });
        SetPendingTrendWrites(service, 2);

        await InvokeFlushTrendWritesAsync(service);

        Assert.Equal(1, await db.ProdajaZaglavlja.AsNoTracking().CountAsync());
        Assert.Equal(1, await db.ProdajaStavke.AsNoTracking().CountAsync());
    }

    [PostgresRequiredFact]
    public async Task DirectAccessSaleImport_MapsSourceIdToGeneratedTargetAndPreservesPosCollision()
    {
        var database = await CreateDatabaseAsync();
        if (database.Db is null)
            return;

        await using var db = database.Db;
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE \"Artikli\" (\"Id\" integer PRIMARY KEY, \"IDDobavljac\" integer, \"IDTipObuce\" integer);");
        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
        {
            Id = 7,
            BrojRacuna = "POS-7",
            DatumProdaje = DateTime.UtcNow,
            DataOrigin = "existing"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var source = new SyntheticAccessSession(new Dictionary<string, IReadOnlyList<AccessDataRow>>(StringComparer.OrdinalIgnoreCase)
        {
            ["sales_headers"] = Rows(
                ["ID", "BrojRacuna", "DatumProdaje", "IDObjekat"],
                [7, "ACCESS-7", DateTime.UtcNow, 2]),
            ["sales_lines"] = Rows(
                ["ID", "IDProdaja", "IDArtikal", "Kolicina", "Cena"],
                [11, 7, 100, 2, 99m],
                [12, 7, 100, 2, 99m])
        });
        var service = CreateService(db, skipInvalidForeignKeys: false);
        var result = new Api.Models.AccessImportRunResponse { BatchId = 7001, Status = "running" };

        await InvokeSaleImportAsync(service, "ImportProdajaAsync", [source, "sales_headers", false, result, CancellationToken.None]);
        var accessHeader = await db.ProdajaZaglavlja.AsNoTracking()
            .SingleAsync(header => header.DataOrigin == "access" && header.SourceRowId == 7);
        Assert.NotEqual(7, accessHeader.Id);

        await InvokeSaleImportAsync(service, "ImportProdajaStavkeAsync", [source, "sales_lines", "sales_headers", false, result, CancellationToken.None]);
        await InvokeFlushTrendWritesAsync(service);

        var headers = await db.ProdajaZaglavlja.AsNoTracking().OrderBy(header => header.Id).ToListAsync();
        Assert.Equal(2, headers.Count);
        Assert.Equal("POS-7", headers.Single(header => header.Id == 7).BrojRacuna);
        var persistedLines = await db.ProdajaStavke.AsNoTracking().OrderBy(line => line.SourceRowId).ToListAsync();
        Assert.Equal(2, persistedLines.Count);
        Assert.All(persistedLines, line => Assert.Equal(accessHeader.Id, line.IdProdaja));
        Assert.All(persistedLines, line => Assert.Equal(100, line.IdArtikal));
        Assert.Equal(new long?[] { 11, 12 }, persistedLines.Select(line => line.SourceRowId).ToArray());

        await InvokeSaleImportAsync(service, "ImportProdajaStavkeAsync", [source, "sales_lines", "sales_headers", false, result, CancellationToken.None]);
        Assert.Equal(2, await db.ProdajaStavke.AsNoTracking().CountAsync());

        var changedSource = new SyntheticAccessSession(new Dictionary<string, IReadOnlyList<AccessDataRow>>(StringComparer.OrdinalIgnoreCase)
        {
            ["sales_headers"] = Rows(
                ["ID", "BrojRacuna", "DatumProdaje", "IDObjekat"],
                [7, "ACCESS-7", DateTime.UtcNow, 2]),
            ["sales_lines"] = Rows(
                ["ID", "IDProdaja", "IDArtikal", "Kolicina", "Cena"],
                [11, 7, 100, 3, 99m],
                [12, 7, 100, 2, 99m])
        });
        await InvokeSaleImportAsync(service, "ImportProdajaStavkeAsync", [changedSource, "sales_lines", "sales_headers", true, result, CancellationToken.None]);
        persistedLines = await db.ProdajaStavke.AsNoTracking().OrderBy(line => line.SourceRowId).ToListAsync();
        Assert.Equal(new[] { 3, 2 }, persistedLines.Select(line => line.Kolicina).ToArray());

        var analyticsSaleIds = (HashSet<int>)typeof(AccessImportService)
            .GetField("_analyticsDeltaSaleIds", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(service)!;
        Assert.Contains(accessHeader.Id, analyticsSaleIds);
        Assert.DoesNotContain(7, analyticsSaleIds);
    }

    [PostgresRequiredFact]
    public async Task LineTableDerivedImport_UsesGeneratedHeaderAndPreservesDuplicateLegacyLinesAcrossReimport()
    {
        var database = await CreateDatabaseAsync();
        if (database.Db is null)
            return;

        await using var db = database.Db;
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE \"Artikli\" (\"Id\" integer PRIMARY KEY, \"IDDobavljac\" integer, \"IDTipObuce\" integer);");
        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
        {
            Id = 7,
            BrojRacuna = "POS-7",
            DatumProdaje = DateTime.UtcNow,
            DataOrigin = "existing"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var sameLineRows = Rows(
            ["IDDnevnik", "IDArtikal", "Kolicina", "ProdajnaCena", "Datum", "IDObjekat", "BrojRacuna"],
            [7, 100, 2, 99m, DateTime.UtcNow.Date, 2, "A-7"],
            [7, 100, 2, 99m, DateTime.UtcNow.Date, 2, "A-7"]);
        var source = new SyntheticAccessSession(new Dictionary<string, IReadOnlyList<AccessDataRow>>(StringComparer.OrdinalIgnoreCase)
        {
            ["sales_lines"] = sameLineRows
        });
        var service = CreateService(db, skipInvalidForeignKeys: false);
        var result = new Api.Models.AccessImportRunResponse { BatchId = 7002, Status = "running" };

        await InvokeSaleImportAsync(service, "ImportProdajaFromLineTableAsync", [source, "sales_lines", false, result, CancellationToken.None]);
        var accessHeader = await db.ProdajaZaglavlja.AsNoTracking()
            .SingleAsync(header => header.DataOrigin == "access" && header.SourceRowId == 7);
        Assert.NotEqual(7, accessHeader.Id);
        var firstLines = await db.ProdajaStavke.AsNoTracking().Where(line => line.IdProdaja == accessHeader.Id).ToListAsync();
        Assert.Equal(2, firstLines.Count);
        Assert.All(firstLines, line => Assert.Null(line.SourceRowId));
        Assert.Equal(1, firstLines.Select(line => line.SourceHash).Distinct().Count());

        var reversedRows = new SyntheticAccessSession(new Dictionary<string, IReadOnlyList<AccessDataRow>>(StringComparer.OrdinalIgnoreCase)
        {
            ["sales_lines"] = sameLineRows.Reverse().ToList()
        });
        await InvokeSaleImportAsync(service, "ImportProdajaFromLineTableAsync", [reversedRows, "sales_lines", false, result, CancellationToken.None]);
        Assert.Equal(2, await db.ProdajaStavke.AsNoTracking().CountAsync(line => line.IdProdaja == accessHeader.Id));

        var changedRows = new SyntheticAccessSession(new Dictionary<string, IReadOnlyList<AccessDataRow>>(StringComparer.OrdinalIgnoreCase)
        {
            ["sales_lines"] = Rows(
                ["IDDnevnik", "IDArtikal", "Kolicina", "ProdajnaCena", "Datum", "IDObjekat", "BrojRacuna"],
                [7, 100, 3, 99m, DateTime.UtcNow.Date, 2, "A-7"],
                [7, 100, 2, 99m, DateTime.UtcNow.Date, 2, "A-7"])
        });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokeSaleImportAsync(service, "ImportProdajaFromLineTableAsync", [changedRows, "sales_lines", false, result, CancellationToken.None]));
        Assert.StartsWith("ambiguous_legacy_mapping:", ex.Message, StringComparison.Ordinal);
        Assert.Equal(2, await db.ProdajaStavke.AsNoTracking().CountAsync(line => line.IdProdaja == accessHeader.Id));
    }

    [PostgresRequiredFact]
    public async Task DnevnikSynthesis_UsesJournalSourceIdentityAndPropagatesGeneratedSaleId()
    {
        var database = await CreateDatabaseAsync();
        if (database.Db is null)
            return;

        await using var db = database.Db;
        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
        {
            Id = 7,
            BrojRacuna = "POS-7",
            DatumProdaje = DateTime.UtcNow,
            DataOrigin = "existing"
        });
        db.DnevnikPromena.Add(new DnevnikPromena
        {
            Id = 51,
            TipPromene = TipPromeneConstants.Prodaja,
            Datum = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc),
            Iznos = 198m,
            BrojRacuna = "ACCESS-DN-7",
            ArtikalId = 100,
            Kolicina = 2,
            IDObjekat = 2,
            DataOrigin = "access",
            SourceTableKey = "dnevnikpromena",
            SourceRowId = 7,
            SourceHash = "source-row-7"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = CreateService(db, skipInvalidForeignKeys: false);
        var result = new Api.Models.AccessImportRunResponse { BatchId = 7003, Status = "running" };
        await InvokeSaleImportAsync(service, "SynthesizeProdajaFromDnevnikAsync", [false, result, CancellationToken.None]);

        var accessHeader = await db.ProdajaZaglavlja.AsNoTracking()
            .SingleAsync(header => header.DataOrigin == "access" && header.SourceRowId == 7);
        Assert.NotEqual(7, accessHeader.Id);
        var accessLine = await db.ProdajaStavke.AsNoTracking().SingleAsync();
        Assert.Equal(accessHeader.Id, accessLine.IdProdaja);
        Assert.Equal(7, accessLine.SourceRowId);
        Assert.Equal(2, accessLine.Kolicina);

        var analyticsSaleIds = (HashSet<int>)typeof(AccessImportService)
            .GetField("_analyticsDeltaSaleIds", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(service)!;
        Assert.Contains(accessHeader.Id, analyticsSaleIds);
        Assert.DoesNotContain(7, analyticsSaleIds);

        // A newly added, earlier journal line can change the synthetic receipt's
        // anchor source ID. The existing line may not be reassigned to that header.
        db.DnevnikPromena.Add(new DnevnikPromena
        {
            Id = 52,
            TipPromene = TipPromeneConstants.Prodaja,
            Datum = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc),
            Iznos = 99m,
            BrojRacuna = "ACCESS-DN-7",
            ArtikalId = 101,
            Kolicina = 1,
            IDObjekat = 2,
            DataOrigin = "access",
            SourceTableKey = "dnevnikpromena",
            SourceRowId = 6,
            SourceHash = "source-row-6"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var changedAnchor = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokeSaleImportAsync(service, "SynthesizeProdajaFromDnevnikAsync", [false, result, CancellationToken.None]));
        Assert.StartsWith("duplicate_sale_line_identity:", changedAnchor.Message, StringComparison.Ordinal);
        Assert.Equal(accessHeader.Id, (await db.ProdajaStavke.AsNoTracking().SingleAsync()).IdProdaja);
    }

    [PostgresRequiredFact]
    public async Task DirectSaleLineReimport_RejectsChangedParentEvenWithoutOverwrite()
    {
        var database = await CreateDatabaseAsync();
        if (database.Db is null)
            return;

        await using var db = database.Db;
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE \"Artikli\" (\"Id\" integer PRIMARY KEY, \"IDDobavljac\" integer, \"IDTipObuce\" integer);");
        var date = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        var headerRows = Rows(["ID", "BrojRacuna", "DatumProdaje"],
            [7, "A-7", date], [8, "A-8", date]);
        var headers = new SyntheticAccessSession(new Dictionary<string, IReadOnlyList<AccessDataRow>>(StringComparer.OrdinalIgnoreCase)
        {
            ["headers"] = headerRows
        });
        var service = CreateService(db, skipInvalidForeignKeys: false);
        var result = new Api.Models.AccessImportRunResponse { BatchId = 7010, Status = "running" };
        await InvokeSaleImportAsync(service, "ImportProdajaAsync", [headers, "headers", false, result, CancellationToken.None]);

        SyntheticAccessSession Lines(int parentId) => new(new Dictionary<string, IReadOnlyList<AccessDataRow>>(StringComparer.OrdinalIgnoreCase)
        {
            ["headers"] = headerRows,
            ["lines"] = Rows(["ID", "IDProdaja", "IDArtikal", "Kolicina", "Cena"], [44, parentId, 100, 1, 99m])
        });
        await InvokeSaleImportAsync(service, "ImportProdajaStavkeAsync", [Lines(7), "lines", "headers", false, result, CancellationToken.None]);
        var original = await db.ProdajaStavke.AsNoTracking().SingleAsync(line => line.SourceRowId == 44);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokeSaleImportAsync(service, "ImportProdajaStavkeAsync", [Lines(8), "lines", "headers", false, result, CancellationToken.None]));
        Assert.StartsWith("duplicate_sale_line_identity:", ex.Message, StringComparison.Ordinal);
        Assert.Equal(original.IdProdaja, (await db.ProdajaStavke.AsNoTracking().SingleAsync(line => line.SourceRowId == 44)).IdProdaja);
        Assert.Equal(1, await db.ProdajaStavke.AsNoTracking().CountAsync());
    }

    [PostgresRequiredFact]
    public async Task LineTableReimport_RejectsChangedParentEvenWithoutOverwrite()
    {
        var database = await CreateDatabaseAsync();
        if (database.Db is null)
            return;

        await using var db = database.Db;
        var date = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        static SyntheticAccessSession Lines(int parentId, DateTime date) => new(new Dictionary<string, IReadOnlyList<AccessDataRow>>(StringComparer.OrdinalIgnoreCase)
        {
            ["lines"] = Rows(
                ["ID", "IDDnevnik", "IDArtikal", "Kolicina", "ProdajnaCena", "Datum", "IDObjekat", "BrojRacuna"],
                [44, parentId, 100, 1, 99m, date, 2, $"A-{parentId}"])
        });
        var service = CreateService(db, skipInvalidForeignKeys: false);
        var result = new Api.Models.AccessImportRunResponse { BatchId = 7011, Status = "running" };
        await InvokeSaleImportAsync(service, "ImportProdajaFromLineTableAsync", [Lines(7, date), "lines", false, result, CancellationToken.None]);
        var original = await db.ProdajaStavke.AsNoTracking().SingleAsync(line => line.SourceRowId == 44);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokeSaleImportAsync(service, "ImportProdajaFromLineTableAsync", [Lines(8, date), "lines", false, result, CancellationToken.None]));
        Assert.StartsWith("duplicate_sale_line_identity:", ex.Message, StringComparison.Ordinal);
        Assert.Equal(original.IdProdaja, (await db.ProdajaStavke.AsNoTracking().SingleAsync(line => line.SourceRowId == 44)).IdProdaja);
        Assert.Equal(1, await db.ProdajaStavke.AsNoTracking().CountAsync());
    }

    [PostgresRequiredFact]
    public async Task DirectSaleLinesWithoutSourceRowId_UseExactMultisetReimportAndRejectChangedSnapshot()
    {
        var database = await CreateDatabaseAsync();
        if (database.Db is null)
            return;

        await using var db = database.Db;
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE \"Artikli\" (\"Id\" integer PRIMARY KEY, \"IDDobavljac\" integer, \"IDTipObuce\" integer);");
        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
        {
            Id = 19,
            BrojRacuna = "ACCESS-19",
            DatumProdaje = DateTime.UtcNow,
            DataOrigin = "access",
            SourceTableKey = "prodajazaglavlje",
            SourceRowId = 7
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        static SyntheticAccessSession Session(int secondQuantity) => new(new Dictionary<string, IReadOnlyList<AccessDataRow>>(StringComparer.OrdinalIgnoreCase)
        {
            ["sales_headers"] = Rows(["ID"], [7]),
            ["sales_lines"] = Rows(
                ["IDProdaja", "IDArtikal", "Kolicina", "Cena"],
                [7, 100, 2, 99m],
                [7, 100, secondQuantity, 99m])
        });

        var service = CreateService(db, skipInvalidForeignKeys: false);
        var result = new Api.Models.AccessImportRunResponse { BatchId = 7004, Status = "running" };
        await InvokeSaleImportAsync(service, "ImportProdajaStavkeAsync", [Session(2), "sales_lines", "sales_headers", false, result, CancellationToken.None]);
        var firstLines = await db.ProdajaStavke.AsNoTracking().Where(line => line.IdProdaja == 19).ToListAsync();
        Assert.Equal(2, firstLines.Count);
        Assert.All(firstLines, line => Assert.Null(line.SourceRowId));
        Assert.All(firstLines, line => Assert.False(string.IsNullOrWhiteSpace(line.SourceHash)));

        await InvokeSaleImportAsync(service, "ImportProdajaStavkeAsync", [Session(2), "sales_lines", "sales_headers", false, result, CancellationToken.None]);
        Assert.Equal(2, await db.ProdajaStavke.AsNoTracking().CountAsync(line => line.IdProdaja == 19));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokeSaleImportAsync(service, "ImportProdajaStavkeAsync", [Session(3), "sales_lines", "sales_headers", false, result, CancellationToken.None]));
        Assert.StartsWith("ambiguous_legacy_mapping:", ex.Message, StringComparison.Ordinal);
        Assert.Equal(2, await db.ProdajaStavke.AsNoTracking().CountAsync(line => line.IdProdaja == 19));
    }

    [PostgresRequiredFact]
    public async Task FlushTrendWritesAsync_ConvertsSaleHeaderPrimaryKeyCollisionToStableImportFailure()
    {
        var database = await CreateDatabaseAsync();
        if (database.Db is null)
            return;

        await using var db = database.Db;
        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
        {
            Id = 7,
            BrojRacuna = "POS-7",
            DatumProdaje = DateTime.UtcNow,
            IDObjekat = 1,
            DataOrigin = "existing"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = CreateService(db, skipInvalidForeignKeys: false);
        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
        {
            Id = 7,
            BrojRacuna = "ACCESS-7",
            DatumProdaje = DateTime.UtcNow,
            IDObjekat = 2,
            DataOrigin = "access",
            SourceTableKey = "prodaja_zaglavlje",
            SourceRowId = 7
        });
        SetPendingTrendWrites(service, 1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeFlushTrendWritesAsync(service));

        Assert.StartsWith("duplicate_sale_header_identity:", ex.Message, StringComparison.Ordinal);
        Assert.Contains("target_primary_key_collision (SQLSTATE 23505)", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("POS-7", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ACCESS-7", ex.Message, StringComparison.Ordinal);

        await using var verifyDb = CreateDbContext(database.ConnectionString);
        var persisted = await verifyDb.ProdajaZaglavlja.AsNoTracking().SingleAsync();
        Assert.Equal("POS-7", persisted.BrojRacuna);
        Assert.Equal("existing", persisted.DataOrigin);
    }

    [PostgresRequiredFact]
    public async Task FlushTrendWritesAsync_ConvertsSaleLinePrimaryKeyCollisionToStableImportFailure()
    {
        var database = await CreateDatabaseAsync();
        if (database.Db is null)
            return;

        await using var db = database.Db;
        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
        {
            Id = 7,
            BrojRacuna = "POS-7",
            DatumProdaje = DateTime.UtcNow,
            IDObjekat = 1,
            DataOrigin = "existing"
        });
        db.ProdajaStavke.Add(new ProdajaStavka
        {
            Id = 11,
            IdProdaja = 7,
            IdArtikal = 100,
            Kolicina = 2,
            Cena = 99m
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = CreateService(db, skipInvalidForeignKeys: false);
        db.ProdajaStavke.Add(new ProdajaStavka
        {
            Id = 11,
            IdProdaja = 7,
            IdArtikal = 200,
            Kolicina = 1,
            Cena = 123m,
            SourceTableKey = "prodaja_stavke",
            SourceRowId = 11
        });
        SetPendingTrendWrites(service, 1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeFlushTrendWritesAsync(service));

        Assert.StartsWith("duplicate_sale_line_identity:", ex.Message, StringComparison.Ordinal);
        Assert.Contains("target_primary_key_collision (SQLSTATE 23505)", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("primary_key_collision:", ex.Message, StringComparison.Ordinal);
        await using var verifyDb = CreateDbContext(database.ConnectionString);
        var persisted = await verifyDb.ProdajaStavke.AsNoTracking().SingleAsync();
        Assert.Equal(100, persisted.IdArtikal);
        Assert.Equal(2, persisted.Kolicina);
        Assert.Equal(99m, persisted.Cena);
    }


    [PostgresRequiredFact]
    public async Task FlushTrendWritesAsync_GeneratedSaleLineId_DoesNotCollideWithUnrelatedPosId()
    {
        var database = await CreateDatabaseAsync();
        if (database.Db is null)
            return;

        await using var db = database.Db;
        db.ProdajaZaglavlja.AddRange(
            new ProdajaZaglavlje
            {
                Id = 7,
                BrojRacuna = "POS-7",
                DatumProdaje = DateTime.UtcNow,
                DataOrigin = "existing"
            },
            new ProdajaZaglavlje
            {
                Id = 8,
                BrojRacuna = "ACCESS-8",
                DatumProdaje = DateTime.UtcNow,
                DataOrigin = "access",
                SourceTableKey = "prodaja_zaglavlje",
                SourceRowId = 8
            });
        db.ProdajaStavke.Add(new ProdajaStavka
        {
            Id = 11,
            IdProdaja = 7,
            IdArtikal = 100,
            Kolicina = 2,
            Cena = 99m
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = CreateService(db, skipInvalidForeignKeys: false);
        var importedLine = new ProdajaStavka
        {
            IdProdaja = 8,
            IdArtikal = 200,
            Kolicina = 1,
            Cena = 123m,
            SourceTableKey = "prodaja_stavke",
            SourceRowId = 11
        };
        db.ProdajaStavke.Add(importedLine);
        SetPendingTrendWrites(service, 1);

        await InvokeFlushTrendWritesAsync(service);

        Assert.True(importedLine.Id > 0);
        Assert.NotEqual(11, importedLine.Id);
        await using var verifyDb = CreateDbContext(database.ConnectionString);
        var persisted = await verifyDb.ProdajaStavke.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(2, persisted.Count);
        Assert.Contains(persisted, x => x.Id == 11 && x.IdProdaja == 7 &&
            x.IdArtikal == 100 && x.Kolicina == 2 && x.Cena == 99m && x.SourceRowId == null);
        Assert.Contains(persisted, x => x.Id == importedLine.Id && x.IdProdaja == 8 &&
            x.IdArtikal == 200 && x.Kolicina == 1 && x.Cena == 123m &&
            x.SourceTableKey == "prodaja_stavke" && x.SourceRowId == 11);
    }

    [PostgresRequiredFact]
    public async Task FlushTrendWritesAsync_RejectsDuplicateSaleLineSourceIdentity()
    {
        var database = await CreateDatabaseAsync();
        if (database.Db is null)
            return;

        await using var db = database.Db;
        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
        {
            Id = 8,
            BrojRacuna = "ACCESS-8",
            DatumProdaje = DateTime.UtcNow,
            DataOrigin = "access"
        });
        db.ProdajaStavke.Add(new ProdajaStavka
        {
            IdProdaja = 8,
            IdArtikal = 100,
            Kolicina = 1,
            Cena = 99m,
            SourceTableKey = "prodaja_stavke",
            SourceRowId = 100
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = CreateService(db, skipInvalidForeignKeys: false);
        db.ProdajaStavke.Add(new ProdajaStavka
        {
            IdProdaja = 8,
            IdArtikal = 200,
            Kolicina = 1,
            Cena = 123m,
            SourceTableKey = "prodaja_stavke",
            SourceRowId = 100
        });
        SetPendingTrendWrites(service, 1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeFlushTrendWritesAsync(service));
        Assert.StartsWith("duplicate_sale_line_identity:", ex.Message, StringComparison.Ordinal);
        Assert.Contains("access_source_lineage_collision (SQLSTATE 23505)", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("target_primary_key_collision", ex.Message, StringComparison.Ordinal);

        await using var verifyDb = CreateDbContext(database.ConnectionString);
        var persisted = await verifyDb.ProdajaStavke.AsNoTracking().SingleAsync();
        Assert.Equal(100, persisted.IdArtikal);
        Assert.Equal(99m, persisted.Cena);
    }

    [PostgresRequiredFact]
    public async Task ResetTrendplusSequencesAsync_NeverLowersSequenceAndCoversExplicitImportedIds()
    {
        var database = await CreateDatabaseAsync();
        if (database.Db is null)
            return;

        await using var db = database.Db;
        foreach (var createSql in new[]
        {
            "CREATE TABLE IF NOT EXISTS \"Artikli\" (\"Id\" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY);",
            "CREATE TABLE IF NOT EXISTS \"Dobavljaci\" (\"Id\" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY);",
            "CREATE TABLE IF NOT EXISTS \"Sezone\" (\"Id\" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY);",
            "CREATE TABLE IF NOT EXISTS \"TipoviObuce\" (\"Id\" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY);",
            "CREATE TABLE IF NOT EXISTS \"povracaj_zaglavlje\" (\"id\" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY);",
            "CREATE TABLE IF NOT EXISTS \"povracaj_stavke\" (\"id\" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY);"
        })
        {
            await db.Database.ExecuteSqlRawAsync(createSql);
        }

        await db.Database.ExecuteSqlRawAsync("INSERT INTO \"Dobavljaci\" (\"Id\") VALUES (50);");
        var seededHeaderSequence = await db.Database.SqlQueryRaw<long>("SELECT setval(pg_get_serial_sequence('prodaja_zaglavlje', 'id'), 100, true) AS \"Value\"").SingleAsync();
        var seededLineSequence = await db.Database.SqlQueryRaw<long>("SELECT setval(pg_get_serial_sequence('prodaja_stavke', 'id'), 80, true) AS \"Value\"").SingleAsync();
        Assert.Equal(100, seededHeaderSequence);
        Assert.Equal(80, seededLineSequence);

        var service = CreateService(db, skipInvalidForeignKeys: false);
        var reset = typeof(AccessImportService).GetMethod("ResetTrendplusSequencesAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)reset.Invoke(service, [CancellationToken.None])!;

        var nextHeaderId = await db.Database.SqlQueryRaw<long>("SELECT nextval(pg_get_serial_sequence('prodaja_zaglavlje', 'id')) AS \"Value\"").SingleAsync();
        var nextLineId = await db.Database.SqlQueryRaw<long>("SELECT nextval(pg_get_serial_sequence('prodaja_stavke', 'id')) AS \"Value\"").SingleAsync();
        var nextSupplierId = await db.Database.SqlQueryRaw<long>("SELECT nextval(pg_get_serial_sequence('\"Dobavljaci\"', 'Id')) AS \"Value\"").SingleAsync();

        Assert.Equal(101, nextHeaderId);
        Assert.Equal(81, nextLineId);
        Assert.Equal(51, nextSupplierId);
    }

    [PostgresRequiredFact]
    public async Task CursorRepositoryUpdateRollsBackWithItsTrendplusTransaction()
    {
        var database = await CreateDatabaseAsync();
        if (database.Db is null)
            return;

        await using var db = database.Db;
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "AccessImportCursors" (
                "TableKey" character varying(128) PRIMARY KEY,
                "CursorMode" character varying(32) NOT NULL,
                "CursorTimestampUtc" timestamp with time zone,
                "CursorId" bigint,
                "CursorTieBreakerId" bigint,
                "LastRowsRead" integer NOT NULL DEFAULT 0,
                "LastRowsMerged" integer NOT NULL DEFAULT 0,
                "LastLagSeconds" integer,
                "LastSuccessfulBatchId" bigint,
                "LastError" character varying(2000),
                "LastRunCompletedAtUtc" timestamp with time zone,
                "UpdatedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW()
            );
            INSERT INTO "AccessImportCursors" ("TableKey", "CursorMode", "CursorId") VALUES ('sales', 'id', 7);
            """);

        var repository = new AccessImportCursorRepository(db);
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await repository.CommitCursorAsync(
                "sales",
                DateTime.UtcNow,
                20,
                30,
                rowsRead: 10,
                rowsMerged: 10,
                lagSeconds: 0,
                lastSuccessfulBatchId: 9001,
                CancellationToken.None);
            await transaction.RollbackAsync();
        }

        var persistedCursor = await db.Database.SqlQueryRaw<long>(
            "SELECT \"CursorId\" AS \"Value\" FROM \"AccessImportCursors\" WHERE \"TableKey\" = 'sales'").SingleAsync();
        Assert.Equal(7, persistedCursor);
    }

    [PostgresRequiredFact]
    public async Task FlushTrendWritesAsync_DistinguishesSourceLineageCollisionFromPrimaryKeyCollision()
    {
        var database = await CreateDatabaseAsync();
        if (database.Db is null)
            return;

        await using var db = database.Db;
        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
        {
            Id = 7,
            BrojRacuna = "REDACT-ME-A",
            DatumProdaje = DateTime.UtcNow,
            DataOrigin = "access",
            SourceTableKey = "prodaja_zaglavlje",
            SourceRowId = 77
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = CreateService(db, skipInvalidForeignKeys: false);
        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
        {
            Id = 8,
            BrojRacuna = "REDACT-ME-B",
            DatumProdaje = DateTime.UtcNow,
            DataOrigin = "access",
            SourceTableKey = "prodaja_zaglavlje",
            SourceRowId = 77
        });
        SetPendingTrendWrites(service, 1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeFlushTrendWritesAsync(service));
        Assert.Contains("access_source_lineage_collision (SQLSTATE 23505)", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("target_primary_key_collision", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("REDACT-ME", ex.Message, StringComparison.Ordinal);

        await using var verifyDb = CreateDbContext(database.ConnectionString);
        var persisted = await verifyDb.ProdajaZaglavlja.AsNoTracking().SingleAsync();
        Assert.Equal(7, persisted.Id);
        Assert.Equal(77, persisted.SourceRowId);
    }

    [PostgresRequiredFact]
    public async Task FlushTrendWritesAsync_DoesNotMisclassifyAnUnknownUniqueIndexAsPk()
    {
        var database = await CreateDatabaseAsync();
        if (database.Db is null)
            return;

        await using var db = database.Db;
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX ux_test_sale_number_store ON prodaja_zaglavlje (broj_racuna, id_objekat);");

        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
        {
            Id = 7,
            BrojRacuna = "DO-NOT-LEAK",
            DatumProdaje = DateTime.UtcNow,
            IDObjekat = 1,
            DataOrigin = "existing"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = CreateService(db, skipInvalidForeignKeys: false);
        db.ProdajaZaglavlja.Add(new ProdajaZaglavlje
        {
            Id = 8,
            BrojRacuna = "DO-NOT-LEAK",
            DatumProdaje = DateTime.UtcNow,
            IDObjekat = 1,
            DataOrigin = "access"
        });
        SetPendingTrendWrites(service, 1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeFlushTrendWritesAsync(service));
        Assert.Contains("unclassified_unique_constraint (SQLSTATE 23505)", ex.Message, StringComparison.Ordinal);
        Assert.StartsWith("duplicate_sale_header_identity:", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("target_primary_key_collision", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("DO-NOT-LEAK", ex.Message, StringComparison.Ordinal);

        await using var verifyDb = CreateDbContext(database.ConnectionString);
        Assert.Equal(7, (await verifyDb.ProdajaZaglavlja.AsNoTracking().SingleAsync()).Id);
    }

    [PostgresRequiredFact]
    public async Task MixedPopulationSqlOracle_DoesNotRebindHistoricalReceipts()
    {
        var database = await CreateDatabaseAsync();
        if (database.Db is null)
            return;

        await using var db = database.Db;
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE \"Artikli\" (\"Id\" integer PRIMARY KEY, \"IDDobavljac\" integer, \"IDTipObuce\" integer);");
        var soldAt = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        db.ProdajaZaglavlja.AddRange(
            new ProdajaZaglavlje
            {
                Id = 7,
                BrojRacuna = "POS-7",
                DatumProdaje = soldAt,
                IDObjekat = 1,
                DataOrigin = "existing"
            },
            new ProdajaZaglavlje
            {
                Id = 40,
                BrojRacuna = "LEGACY-40",
                DatumProdaje = soldAt,
                IDObjekat = 1,
                DataOrigin = "access",
                SourceTableKey = "prodajazaglavlje",
                SourceRowId = null
            });
        db.ProdajaStavke.AddRange(
            new ProdajaStavka
            {
                Id = 11,
                IdProdaja = 7,
                IdArtikal = 100,
                Kolicina = 1,
                Cena = 50m
            },
            new ProdajaStavka
            {
                Id = 41,
                IdProdaja = 40,
                IdArtikal = 100,
                Kolicina = 1,
                Cena = 10m,
                SourceTableKey = null,
                SourceRowId = null
            });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var source = new SyntheticAccessSession(new Dictionary<string, IReadOnlyList<AccessDataRow>>(StringComparer.OrdinalIgnoreCase)
        {
            ["sales_headers"] = Rows(
                ["ID", "BrojRacuna", "DatumProdaje", "IDObjekat"],
                [7, "ACCESS-7", soldAt, 2]),
            ["sales_lines"] = Rows(
                ["ID", "IDProdaja", "IDArtikal", "Kolicina", "Cena"],
                [11, 7, 100, 2, 99m],
                [12, 7, 100, 2, 99m])
        });
        var service = CreateService(db, skipInvalidForeignKeys: false);
        var result = new Api.Models.AccessImportRunResponse { BatchId = 7023, Status = "running" };
        await InvokeSaleImportAsync(service, "ImportProdajaAsync", [source, "sales_headers", false, result, CancellationToken.None]);
        await InvokeSaleImportAsync(service, "ImportProdajaStavkeAsync", [source, "sales_lines", "sales_headers", false, result, CancellationToken.None]);
        await InvokeSaleImportAsync(service, "ImportProdajaAsync", [source, "sales_headers", false, result, CancellationToken.None]);
        await InvokeSaleImportAsync(service, "ImportProdajaStavkeAsync", [source, "sales_lines", "sales_headers", false, result, CancellationToken.None]);

        await ExecuteTriageScriptAsync(database.ConnectionString);

        var headers = await db.ProdajaZaglavlja.AsNoTracking().ToListAsync();
        var pos = headers.Single(header => header.Id == 7);
        Assert.Equal("POS-7", pos.BrojRacuna);
        Assert.Equal("existing", pos.DataOrigin);
        var legacy = headers.Single(header => header.Id == 40);
        Assert.Equal("LEGACY-40", legacy.BrojRacuna);
        Assert.Null(legacy.SourceRowId);
        var imported = headers.Single(header => header.DataOrigin == "access" && header.SourceRowId == 7);
        Assert.NotEqual(7, imported.Id);
        Assert.NotEqual(40, imported.Id);
        Assert.Equal(3, headers.Count);

        var lines = await db.ProdajaStavke.AsNoTracking().ToListAsync();
        Assert.Equal(4, lines.Count);
        Assert.Equal(7, lines.Single(line => line.Id == 11).IdProdaja);
        Assert.Equal(50m, lines.Single(line => line.Id == 11).Cena);
        Assert.Equal(40, lines.Single(line => line.Id == 41).IdProdaja);
        var importedLines = lines.Where(line => line.SourceRowId is 11 or 12).OrderBy(line => line.SourceRowId).ToList();
        Assert.Equal(2, importedLines.Count);
        Assert.All(importedLines, line => Assert.Equal(imported.Id, line.IdProdaja));
        Assert.All(importedLines, line => Assert.NotEqual(11, line.Id));

        Assert.Equal(0, await ScalarAsync<long>(db, """
            SELECT COUNT(*)::bigint AS "Value"
            FROM (
                SELECT source_table_key, source_row_id
                FROM prodaja_zaglavlje
                WHERE data_origin = 'access'
                  AND source_table_key IS NOT NULL
                  AND source_row_id IS NOT NULL
                GROUP BY source_table_key, source_row_id
                HAVING COUNT(*) > 1
            ) AS duplicate_source
            """));
        Assert.Equal(1, await ScalarAsync<long>(db, """
            SELECT COUNT(*)::bigint AS "Value"
            FROM prodaja_zaglavlje
            WHERE data_origin = 'access'
              AND (source_row_id IS NULL OR source_table_key IS NULL)
            """));
        Assert.Equal(0, await ScalarAsync<long>(db, """
            SELECT COUNT(*)::bigint AS "Value"
            FROM prodaja_stavke AS s
            LEFT JOIN prodaja_zaglavlje AS h ON h.id = s.id_prodaja
            WHERE h.id IS NULL
            """));
        Assert.Equal(imported.Id, await ScalarAsync<int>(db, """
            SELECT a.id AS "Value"
            FROM prodaja_zaglavlje AS a
            JOIN prodaja_zaglavlje AS t
              ON t.id = a.source_row_id
             AND t.id <> a.id
            WHERE a.data_origin = 'access'
              AND a.source_row_id = 7
              AND t.data_origin = 'existing'
            """));

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "SalesFacts" (
                "SaleId" integer PRIMARY KEY,
                "BrojRacuna" text NOT NULL,
                "SaleTimestampUtc" timestamp with time zone NOT NULL,
                "StoreId" integer NOT NULL,
                "PaymentType" text NOT NULL,
                "TotalAmount" numeric(18,2) NOT NULL,
                "TotalUnits" integer NOT NULL,
                "TotalLines" integer NOT NULL,
                "DataOrigin" text NOT NULL
            );
            CREATE TABLE "SalesLineFacts" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "SaleId" integer NOT NULL,
                "ProductId" integer NOT NULL,
                "Qty" integer NOT NULL,
                "UnitPrice" numeric(18,2) NOT NULL,
                "LineTotal" numeric(18,2) NOT NULL,
                "NabavnaCena" numeric(18,2) NULL,
                "DataOrigin" text NOT NULL,
                "SourceTableKey" text NULL,
                "SourceLineId" bigint NULL
            );
            INSERT INTO "SalesFacts" (
                "SaleId","BrojRacuna","SaleTimestampUtc","StoreId","PaymentType","TotalAmount","TotalUnits","TotalLines","DataOrigin")
            SELECT
                h.id,
                COALESCE(h.broj_racuna, ''),
                h.datum_prodaje,
                COALESCE(h.id_objekat, 1),
                COALESCE(h.nacin_placanja, ''),
                COALESCE(SUM(s.kolicina * s.cena), 0),
                COALESCE(SUM(s.kolicina), 0)::integer,
                COUNT(s.id)::integer,
                h.data_origin
            FROM prodaja_zaglavlje AS h
            LEFT JOIN prodaja_stavke AS s ON s.id_prodaja = h.id
            GROUP BY h.id, h.broj_racuna, h.datum_prodaje, h.id_objekat, h.nacin_placanja, h.data_origin;
            """);

        var mappedFacts = AccessImportService.MapSalesLineFacts(lines.Select(line =>
            new AccessImportService.SaleLineSourceSyncRow(
                line.Id,
                line.IdProdaja,
                line.IdArtikal,
                line.Kolicina,
                line.Cena,
                line.NabavnaCena,
                line.SourceTableKey,
                line.SourceRowId)));
        var saleIds = lines.Select(line => line.IdProdaja).Distinct().ToArray();
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            for (var attempt = 0; attempt < 2; attempt++)
            {
                await using var transaction = await connection.BeginTransactionAsync();
                await AccessImportService.ReplaceSalesLineFactsBulkAsync(
                    connection,
                    transaction,
                    saleIds,
                    mappedFacts,
                    NullLogger<AccessImportService>.Instance,
                    CancellationToken.None);
                await transaction.CommitAsync();
            }
        }

        Assert.Equal(4, await ScalarAsync<long>(db, """SELECT COUNT(*)::bigint AS "Value" FROM "SalesLineFacts" """));
        Assert.Equal(396m, await ScalarAsync<decimal>(db,
            $"SELECT \"TotalAmount\" AS \"Value\" FROM \"SalesFacts\" WHERE \"SaleId\" = {imported.Id}"));
        Assert.Equal(50m, await ScalarAsync<decimal>(db, """SELECT "TotalAmount" AS "Value" FROM "SalesFacts" WHERE "SaleId" = 7"""));
        Assert.Equal(10m, await ScalarAsync<decimal>(db, """SELECT "TotalAmount" AS "Value" FROM "SalesFacts" WHERE "SaleId" = 40"""));
        Assert.Equal("existing", await ScalarAsync<string>(db, """SELECT "DataOrigin" AS "Value" FROM "SalesFacts" WHERE "SaleId" = 7"""));
        Assert.Equal(imported.Id, await ScalarAsync<int>(db, """
            SELECT "SaleId" AS "Value" FROM "SalesLineFacts"
            WHERE "SourceLineId" = 11 AND "SourceTableKey" = 'prodajastavke'
            """));
        Assert.Equal(0, await ScalarAsync<long>(db, """
            SELECT COUNT(*)::bigint AS "Value"
            FROM "SalesLineFacts"
            WHERE "SaleId" = 7 AND "SourceTableKey" = 'prodajastavke'
            """));
        Assert.Equal(396m, await ScalarAsync<decimal>(db,
            $"SELECT COALESCE(SUM(\"LineTotal\"), 0) AS \"Value\" FROM \"SalesLineFacts\" WHERE \"SaleId\" = {imported.Id}"));
    }

    private async Task<(TrendplusDbContext? Db, string ConnectionString)> CreateDatabaseAsync()
    {
        if (!_fixture.IsAvailable)
            return (null, string.Empty);

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"tp_access_fk_{Guid.NewGuid():N}");
        if (string.IsNullOrWhiteSpace(connectionString))
            return (null, string.Empty);

        var db = CreateDbContext(connectionString);
        await BootstrapSalesSchemaAsync(db);
        return (db, connectionString);
    }

    private static TrendplusDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;
        return new TrendplusDbContext(options);
    }

    private static AccessImportService CreateService(TrendplusDbContext db, bool skipInvalidForeignKeys)
        => new(
            trendDb: db,
            analyticsDb: null!,
            logger: NullLogger<AccessImportService>.Instance,
            options: Options.Create(new AccessImportOptions
            {
                SkipInvalidForeignKeys = skipInvalidForeignKeys,
                DbSaveBatchSize = 1
            }));

    private static void SetPendingTrendWrites(AccessImportService service, int value)
    {
        var field = typeof(AccessImportService).GetField(
            "_pendingTrendWrites",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(service, value);
    }

    private static async Task InvokeFlushTrendWritesAsync(AccessImportService service)
    {
        var method = typeof(AccessImportService).GetMethod(
            "FlushTrendWritesAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = (Task)method!.Invoke(service, new object[] { true, CancellationToken.None })!;
        await task;
    }

    private static async Task InvokeSaleImportAsync(AccessImportService service, string methodName, object?[] arguments)
    {
        var method = typeof(AccessImportService).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = (Task?)method!.Invoke(service, arguments);
        Assert.NotNull(task);
        await task!;
    }

    private static IReadOnlyList<AccessDataRow> Rows(string[] columns, params object?[][] rows)
    {
        var schema = new AccessDataSchema(columns);
        return rows.Select(values => new AccessDataRow(schema, values)).ToList();
    }

    private sealed class SyntheticAccessSession(IReadOnlyDictionary<string, IReadOnlyList<AccessDataRow>> tableRows) : IAccessDataReaderSession
    {
        public string Mode => "synthetic-postgres-integration";
        public string SourceFilePath => "synthetic-access-source.mdb";
        public bool SupportsPredicatePushdown => false;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task<IReadOnlyList<string>> GetTablesAsync(bool includeTemporaryTables = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(tableRows.Keys.ToList());

        public Task<IReadOnlyList<string>> GetColumnsAsync(string table, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(tableRows.TryGetValue(table, out var rows) && rows.Count > 0 ? rows[0].Columns : []);

        public Task<AccessRowCountResult> TryGetExactRowCountAsync(string table, CancellationToken ct = default)
            => Task.FromResult(AccessRowCountResult.Exact(tableRows.TryGetValue(table, out var rows) ? rows.Count : 0));

        public IAsyncEnumerable<AccessDataRow> ReadRowsAsync(string table, CancellationToken ct = default)
            => ReadRowsCoreAsync(table, ct);

        public IAsyncEnumerable<AccessDataRow> ReadRowsAsync(string table, AccessReadQuery? query, CancellationToken ct = default)
            => ReadRowsCoreAsync(table, ct);

        private async IAsyncEnumerable<AccessDataRow> ReadRowsCoreAsync(
            string table,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            if (!tableRows.TryGetValue(table, out var rows))
                yield break;

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return row;
            }
        }
    }

    private static Task BootstrapSalesSchemaAsync(TrendplusDbContext db)
        => db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS prodaja_zaglavlje (
                id integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                broj_racuna character varying(100),
                datum_prodaje timestamp with time zone NOT NULL,
                nacin_placanja character varying(100),
                id_objekat integer,
                korisnik_ime character varying(200),
                data_origin character varying(32) NOT NULL DEFAULT 'existing',
                source_timestamp_basis character varying(64),
                source_table_key character varying(128),
                source_row_id bigint,
                source_updated_at_utc timestamp with time zone,
                source_hash character varying(128),
                source_batch_id bigint,
                attribution_basis character varying(64) NOT NULL DEFAULT 'unknown'
            );

            CREATE TABLE IF NOT EXISTS prodaja_stavke (
                id integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                id_prodaja integer NOT NULL,
                id_artikal integer NOT NULL,
                kolicina integer NOT NULL,
                cena numeric(18,2) NOT NULL,
                nabavna_cena numeric(18,2),
                purchase_cost_provenance character varying(64),
                supplier_id_at_sale integer,
                shoe_type_id_at_sale integer,
                source_table_key character varying(128),
                source_row_id bigint,
                source_updated_at_utc timestamp with time zone,
                source_hash character varying(128),
                source_batch_id bigint,
                attribution_basis character varying(64) NOT NULL DEFAULT 'unknown',
                CONSTRAINT "FK_prodaja_stavke_prodaja_zaglavlje_id_prodaja"
                    FOREIGN KEY (id_prodaja) REFERENCES prodaja_zaglavlje(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS "DnevnikPromena" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "TipPromene" character varying(100) NOT NULL,
                "Datum" timestamp with time zone NOT NULL,
                "Iznos" numeric(18,2) NOT NULL DEFAULT 0,
                "BrojRacuna" character varying(100),
                "DobavljacId" integer,
                "ArtikalId" integer,
                "StaraProdajnaCena" numeric(18,2),
                "NovaProdajnaCena" numeric(18,2),
                "Kolicina" integer,
                "IDObjekat" integer,
                "RedniBroj" integer,
                "Komentar" character varying(500),
                "KorisnikIme" character varying(200),
                "DataOrigin" character varying(32) NOT NULL DEFAULT 'existing',
                "SourceTableKey" character varying(128),
                "SourceRowId" bigint,
                "SourceUpdatedAtUtc" timestamp with time zone,
                "SourceHash" character varying(128),
                "SourceBatchId" bigint
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ux_prodaja_zaglavlje_access_source_row
                ON prodaja_zaglavlje (data_origin, source_table_key, source_row_id)
                WHERE data_origin = 'access' AND source_table_key IS NOT NULL AND source_row_id IS NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS ux_prodaja_stavke_source_row
                ON prodaja_stavke (source_table_key, source_row_id)
                WHERE source_table_key IS NOT NULL AND source_row_id IS NOT NULL;
            """);

    private static async Task<T> ScalarAsync<T>(TrendplusDbContext db, string sql)
        => await db.Database.SqlQueryRaw<T>(sql).SingleAsync();

    private static async Task ExecuteTriageScriptAsync(string connectionString)
    {
        var script = FindTriageScript();
        var executable = string.Join(
            "\n",
            script.Split('\n').Where(line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal)));
        foreach (var forbidden in new[] { "INSERT", "UPDATE", "DELETE", "setval", "DROP ", "ALTER ", "TRUNCATE" })
        {
            if (executable.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Triage script contains forbidden statement '{forbidden}'.");
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(script, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var resultSets = 0;
        do
        {
            while (await reader.ReadAsync())
            {
            }

            resultSets++;
        }
        while (await reader.NextResultAsync());

        if (resultSets < 4)
            throw new InvalidOperationException($"Triage script returned {resultSets} result sets.");
    }

    private static string FindTriageScript()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "tools", "access-sale-header-identity-triage.sql");
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
        }

        throw new FileNotFoundException("tools/access-sale-header-identity-triage.sql was not found from the test output directory.");
    }
}

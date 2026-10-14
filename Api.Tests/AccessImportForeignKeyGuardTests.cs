using System.Reflection;
using Api.Config;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
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
        Assert.StartsWith("duplicate_source_line_identity:", ex.Message, StringComparison.Ordinal);

        await using var verifyDb = CreateDbContext(database.ConnectionString);
        var persisted = await verifyDb.ProdajaStavke.AsNoTracking().SingleAsync();
        Assert.Equal(100, persisted.IdArtikal);
        Assert.Equal(99m, persisted.Cena);
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
            CREATE UNIQUE INDEX IF NOT EXISTS ux_prodaja_zaglavlje_access_source_row
                ON prodaja_zaglavlje (data_origin, source_table_key, source_row_id)
                WHERE data_origin = 'access' AND source_table_key IS NOT NULL AND source_row_id IS NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS ux_prodaja_stavke_source_row
                ON prodaja_stavke (source_table_key, source_row_id)
                WHERE source_table_key IS NOT NULL AND source_row_id IS NOT NULL;
            """);
}

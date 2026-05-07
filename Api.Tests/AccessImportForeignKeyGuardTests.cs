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

    [Fact]
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

    [Fact]
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

    [Fact]
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
                id integer PRIMARY KEY,
                broj_racuna character varying(100),
                datum_prodaje timestamp with time zone NOT NULL,
                nacin_placanja character varying(100),
                id_objekat integer,
                korisnik_ime character varying(200),
                data_origin character varying(32) NOT NULL DEFAULT 'existing',
                source_table_key character varying(128),
                source_row_id bigint,
                source_updated_at_utc timestamp with time zone,
                source_hash character varying(128),
                source_batch_id bigint
            );

            CREATE TABLE IF NOT EXISTS prodaja_stavke (
                id integer PRIMARY KEY,
                id_prodaja integer NOT NULL,
                id_artikal integer NOT NULL,
                kolicina integer NOT NULL,
                cena numeric(18,2) NOT NULL,
                nabavna_cena numeric(18,2),
                source_table_key character varying(128),
                source_row_id bigint,
                source_updated_at_utc timestamp with time zone,
                source_hash character varying(128),
                source_batch_id bigint,
                CONSTRAINT "FK_prodaja_stavke_prodaja_zaglavlje_id_prodaja"
                    FOREIGN KEY (id_prodaja) REFERENCES prodaja_zaglavlje(id) ON DELETE CASCADE
            );
            """);
}

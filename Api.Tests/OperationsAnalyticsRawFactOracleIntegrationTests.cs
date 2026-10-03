using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Trendplus2.Tests;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class OperationsAnalyticsRawFactOracleIntegrationTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _postgres;

    public OperationsAnalyticsRawFactOracleIntegrationTests(PostgresContainerFixture postgres)
    {
        _postgres = postgres;
    }

    [OperationsIntegrationFact(DisplayName = "Runtime integrity oracle follows sale-header scope, receipt exclusions and bucket grain")]
    public async Task SharedFixture_OracleUsesHeaderOriginAndPreservesSupplierAndShoeTypeBuckets()
    {
        Assert.True(_postgres.IsAvailable, "The runtime oracle proof requires a disposable PostgreSQL Testcontainer.");
        var connectionString = await _postgres.TryCreateDatabaseConnectionStringAsync($"rq562_oracle_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await SeedSharedFixtureAsync(connectionString!);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var fromUtc = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = fromUtc.AddDays(1);
        var importedFilters = new OperationsAnalyticsRawFactOracle.Filters(fromUtc, toUtc, null, "imported");
        var existingFilters = importedFilters with { DataScope = "existing" };

        var imported = await OperationsAnalyticsRawFactOracle.QueryTotalsAsync(connection, importedFilters);
        var existing = await OperationsAnalyticsRawFactOracle.QueryTotalsAsync(connection, existingFilters);
        Assert.Equal(1, imported.SaleLineCount);
        Assert.Equal(200m, imported.TotalRevenue);
        Assert.Equal(1, existing.SaleLineCount);
        Assert.Equal(390m, existing.TotalRevenue);

        var julyFilters = new OperationsAnalyticsRawFactOracle.Filters(
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 8, 0, 0, 0, DateTimeKind.Utc),
            null,
            "all");
        var july = await OperationsAnalyticsRawFactOracle.QueryTotalsAsync(connection, julyFilters);
        Assert.Equal(4, july.SaleLineCount);
        Assert.Equal(540m, july.TotalRevenue);

        var septemberFilters = new OperationsAnalyticsRawFactOracle.Filters(
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc),
            null,
            "all");
        var september = await OperationsAnalyticsRawFactOracle.QueryTotalsAsync(connection, septemberFilters);
        Assert.Equal(9, september.SaleLineCount);
        Assert.Equal(590m, september.TotalRevenue);

        var supplierBuckets = await OperationsAnalyticsRawFactOracle.QuerySupplierBucketsAsync(connection, importedFilters);
        var shoeTypeBuckets = await OperationsAnalyticsRawFactOracle.QueryShoeTypeBucketsAsync(connection, importedFilters);
        var colorBuckets = await OperationsAnalyticsRawFactOracle.QueryColorBucketsAsync(connection, importedFilters);
        var dailyBuckets = await OperationsAnalyticsRawFactOracle.QueryDailyBucketsAsync(connection, importedFilters);
        Assert.Equal(imported.TotalUnits, supplierBuckets.Sum(bucket => bucket.Units));
        Assert.Equal(imported.TotalRevenue, supplierBuckets.Sum(bucket => bucket.Revenue));
        Assert.Equal(imported.TotalUnits, shoeTypeBuckets.Sum(bucket => bucket.Units));
        Assert.Equal(imported.TotalRevenue, shoeTypeBuckets.Sum(bucket => bucket.Revenue));
        Assert.Equal(imported.TotalUnits, colorBuckets.Sum(bucket => bucket.Units));
        Assert.Equal(imported.TotalRevenue, colorBuckets.Sum(bucket => bucket.Revenue));
        Assert.Contains(colorBuckets, bucket => bucket.DimensionKey == "CRNA");
        var daily = Assert.Single(dailyBuckets);
        Assert.Equal(1, daily.StoreId);
        Assert.Equal(10, daily.HourOfDay);
        Assert.Equal("access", daily.DataOrigin);
        Assert.Equal(2, daily.Units);
        Assert.Equal(200m, daily.Revenue);

        await SeedSignedUnknownAndNegativeBucketRowsAsync(connection);
        var sentinelWindow = new OperationsAnalyticsRawFactOracle.Filters(
            new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 12, 0, 0, 0, DateTimeKind.Utc),
            null,
            "all");
        var sentinelTotals = await OperationsAnalyticsRawFactOracle.QueryTotalsAsync(connection, sentinelWindow);
        Assert.Equal(2, sentinelTotals.SaleLineCount);
        Assert.Equal(0, sentinelTotals.TotalUnits);
        Assert.Equal(0m, sentinelTotals.TotalRevenue);
        var sentinelBuckets = await OperationsAnalyticsRawFactOracle.QuerySupplierBucketsAsync(connection, sentinelWindow);
        var sentinelShoeTypeBuckets = await OperationsAnalyticsRawFactOracle.QueryShoeTypeBucketsAsync(connection, sentinelWindow);
        Assert.Equal(2, sentinelBuckets.Count);
        Assert.Contains(sentinelBuckets, bucket => bucket.DimensionId == -1 && bucket.Units == 1 && bucket.Revenue == 100m);
        Assert.Contains(sentinelBuckets, bucket => bucket.DimensionId is null && bucket.Units == -1 && bucket.Revenue == -100m);
        Assert.Contains(sentinelShoeTypeBuckets, bucket => bucket.DimensionId == -1 && bucket.Units == 1);
        Assert.Contains(sentinelShoeTypeBuckets, bucket => bucket.DimensionId is null && bucket.Units == -1);
    }

    private static async Task SeedSharedFixtureAsync(string connectionString)
    {
        await using (var db = new TrendplusDbContext(
                         new DbContextOptionsBuilder<TrendplusDbContext>().UseNpgsql(connectionString).Options))
            await db.Database.MigrateAsync();
        await using (var analyticsDb = new AnalyticsDbContext(
                         new DbContextOptionsBuilder<AnalyticsDbContext>().UseNpgsql(connectionString).Options))
            await analyticsDb.Database.MigrateAsync();

        var fixturePath = FindRepositoryFile("Api.Tests", "Fixtures", "operations-analytics-all-routes-seed.sql");
        var sql = await File.ReadAllTextAsync(fixturePath);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SeedSignedUnknownAndNegativeBucketRowsAsync(NpgsqlConnection connection)
    {
        const string sql = """
            INSERT INTO "Dobavljaci" ("Id", "Naziv", "DataOrigin")
            OVERRIDING SYSTEM VALUE VALUES (-1, 'RQ562 negative supplier', 'existing');
            INSERT INTO "TipoviObuce" ("Id", "Naziv", "DataOrigin")
            OVERRIDING SYSTEM VALUE VALUES (-1, 'RQ562 negative type', 'existing');
            INSERT INTO "Artikli"
              ("PLU", "Naziv", "NabavnaCena", "NabavnaCenaDin", "PrvaProdajnaCena", "ProdajnaCena",
               "IDDobavljac", "IDTipObuce", "UpdatedAt", "Kolicina", "MinimalnaKolicina", "IDObjekat",
               "IDSezona", "Kategorija", "Pol", "Velicina", "Boja", "DataOrigin")
            VALUES
              ('RQ562-NEGATIVE', 'Negative supplier id fixture', 50, 50, 100, 100, -1, -1,
               '2026-09-11T00:00:00Z', 0, 0, 1, 1, 'Obuca', 'Unisex', '42', 'Crna', 'existing'),
              ('RQ562-UNKNOWN', 'Unknown supplier id fixture', 50, 50, 100, 100, NULL, NULL,
               '2026-09-11T00:00:00Z', 0, 0, 1, 1, 'Obuca', 'Unisex', '41', 'Crna', 'existing');
            INSERT INTO prodaja_zaglavlje
              (id, broj_racuna, datum_prodaje, id_objekat, korisnik_ime, data_origin)
            VALUES
              (24, 'RQ562-NEGATIVE', '2026-09-11T08:00:00Z', 1, 'rq562', 'existing'),
              (25, 'RQ562-UNKNOWN', '2026-09-11T09:00:00Z', 1, 'rq562', 'existing'),
              (26, 'RQ562-UPPER-BOUNDARY', '2026-09-12T00:00:00Z', 1, 'rq562', 'existing');
            INSERT INTO prodaja_stavke
              (id, id_prodaja, id_artikal, kolicina, cena, nabavna_cena, supplier_id_at_sale, shoe_type_id_at_sale, attribution_basis)
            SELECT 24, 24, a."Id", 1, 100, 50, -1, -1, 'sale_snapshot'
              FROM "Artikli" a WHERE a."PLU" = 'RQ562-NEGATIVE';
            INSERT INTO prodaja_stavke
              (id, id_prodaja, id_artikal, kolicina, cena, nabavna_cena, supplier_id_at_sale, shoe_type_id_at_sale, attribution_basis)
            SELECT 25, 25, a."Id", -1, 100, 50, NULL, NULL, 'sale_snapshot'
              FROM "Artikli" a WHERE a."PLU" = 'RQ562-UNKNOWN';
            INSERT INTO prodaja_stavke
              (id, id_prodaja, id_artikal, kolicina, cena, nabavna_cena, supplier_id_at_sale, shoe_type_id_at_sale, attribution_basis)
            VALUES (26, 26, 1, 99, 100, 50, 1, 1, 'sale_snapshot');
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository fixture: {Path.Combine(segments)}");
    }
}

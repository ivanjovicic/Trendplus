using System.Text.Json;
using Domain.Model;
using Infrastructure.DbContexts;
using Infrastructure.Services.Caching;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
[Trait("Suite", "RQ607")]
public sealed class Rq607InventoryStatusPostgresIntegrationTests : IClassFixture<Rq607PostgresFixture>
{
    private static readonly DateTime FromUtc = new(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ToUtc = new(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc);
    private readonly Rq607PostgresFixture _fixture;

    public Rq607InventoryStatusPostgresIntegrationTests(Rq607PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "RQ607 InventoryStatus maps Access origin and matches independent PostgreSQL oracle")]
    public async Task InventoryStatus_UsesScopeOriginSnapshotAndIsolatedCache()
    {
        Assert.True(_fixture.IsAvailable, "RQ607 certification requires PostgreSQL; unavailable infrastructure must fail, not skip.");
        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"tp_rq607_inventory_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString), "RQ607 could not create an isolated PostgreSQL database.");

        await using (var trendDb = new TrendplusDbContext(
                         new DbContextOptionsBuilder<TrendplusDbContext>().UseNpgsql(connectionString).Options))
        await using (var analyticsDb = new AnalyticsDbContext(
                         new DbContextOptionsBuilder<AnalyticsDbContext>().UseNpgsql(connectionString).Options))
        {
            await trendDb.Database.MigrateAsync();
            await analyticsDb.Database.MigrateAsync();
            await using (var setup = new NpgsqlConnection(connectionString))
            {
                await setup.OpenAsync();
                await using var command = new NpgsqlCommand("""
                    ALTER TABLE "Artikli" ADD COLUMN IF NOT EXISTS "Kategorija" text;
                    ALTER TABLE "Artikli" ADD COLUMN IF NOT EXISTS "Pol" text;
                    ALTER TABLE "Artikli" ADD COLUMN IF NOT EXISTS "Velicina" text;
                    ALTER TABLE "Artikli" ADD COLUMN IF NOT EXISTS "Boja" text;
                    ALTER TABLE "Artikli" ADD COLUMN IF NOT EXISTS "Materijal" character varying(100);
                    ALTER TABLE "Artikli" ADD COLUMN IF NOT EXISTS "MinimalnaKolicina" integer;
                    ALTER TABLE "Artikli" ADD COLUMN IF NOT EXISTS "ImagePath" character varying(500);
                    ALTER TABLE "Artikli" ADD COLUMN IF NOT EXISTS "SourceTableKey" character varying(128);
                    ALTER TABLE "Artikli" ADD COLUMN IF NOT EXISTS "SourceRowId" bigint;
                    ALTER TABLE "Artikli" ADD COLUMN IF NOT EXISTS "SourceUpdatedAtUtc" timestamp with time zone;
                    ALTER TABLE "Artikli" ADD COLUMN IF NOT EXISTS "SourceHash" character varying(128);
                    ALTER TABLE "Artikli" ADD COLUMN IF NOT EXISTS "SourceBatchId" bigint;
                    ALTER TABLE "Artikli" ALTER COLUMN "DataOrigin" DROP NOT NULL;
                    ALTER TABLE "ProductsDim" ADD COLUMN IF NOT EXISTS "DataOrigin" character varying(32) DEFAULT 'existing';
                    ALTER TABLE "ProductsDim" ALTER COLUMN "DataOrigin" DROP NOT NULL;
                    """, setup);
                await command.ExecuteNonQueryAsync();
            }
            Seed(trendDb, analyticsDb);
            await trendDb.SaveChangesAsync();
            await analyticsDb.SaveChangesAsync();
        }

        var cache = new RecordingAnalyticsCacheService();
        await using var factory = new Rq605EndpointFactory(connectionString!, cache);
        using var client = factory.CreateClient();
        await using var oracle = new NpgsqlConnection(connectionString);
        await oracle.OpenAsync();

        foreach (var scope in new[] { "imported", "existing", "all" })
        {
            var expected = await ReadOracleAsync(oracle, scope, null, null);
            var actual = await GetJsonAsync(client, Url(scope));
            AssertInventory(expected, actual);
        }

        var importedStoreSupplier = await GetJsonAsync(
            client,
            Url("imported", storeId: 2, supplierId: 6702));
        AssertInventory(await ReadOracleAsync(oracle, "imported", 2, 6702), importedStoreSupplier);
        Assert.Equal(2, importedStoreSupplier.GetProperty("totalSkuCount").GetInt32());
        Assert.Equal(0, importedStoreSupplier.GetProperty("totalOnHand").GetInt32());
        Assert.Equal(0, importedStoreSupplier.GetProperty("outOfStockCount").GetInt32());

        var trueZero = await GetJsonAsync(client, Url("imported", storeId: 1, supplierId: 6701));
        Assert.Equal(2, trueZero.GetProperty("totalSkuCount").GetInt32());
        Assert.Equal(5, trueZero.GetProperty("totalOnHand").GetInt32());
        Assert.Equal(1, trueZero.GetProperty("outOfStockCount").GetInt32());
        Assert.NotEqual("no_inventory_data", trueZero.GetProperty("meta").GetProperty("emptyReason").GetString());

        var empty = await GetJsonAsync(client, Url("imported", storeId: 999, supplierId: 999));
        Assert.Equal(0, empty.GetProperty("totalSkuCount").GetInt32());
        Assert.Equal("no_inventory_data", empty.GetProperty("meta").GetProperty("emptyReason").GetString());

        var factoriesBeforeHit = cache.FactoryCallsForScope("imported");
        var importedAgain = await GetJsonAsync(client, Url("IMPORTED"));
        Assert.Equal(
            (await ReadOracleAsync(oracle, "imported", null, null)).SkuCount,
            importedAgain.GetProperty("totalSkuCount").GetInt32());
        Assert.True(cache.GetCountForScope("imported") >= 2);
        Assert.Equal(factoriesBeforeHit, cache.FactoryCallsForScope("imported"));
        Assert.Equal(1, cache.FactoryCallsForScope("existing"));
        Assert.Equal(1, cache.FactoryCallsForScope("all"));
        Assert.True(cache.Keys.Count(key => key.Contains("scope:imported", StringComparison.Ordinal)) >= 2);
        Assert.Contains(cache.Keys, key => key.Contains("store:2:supplier:6702:scope:imported", StringComparison.Ordinal));
        Assert.NotEqual(
            AnalyticsCacheKeys.Inventory(2, FromUtc, ToUtc, 1, 6701, "imported"),
            AnalyticsCacheKeys.Inventory(2, FromUtc, ToUtc, 1, 6701, "existing"));
    }

    private static void Seed(TrendplusDbContext trendDb, AnalyticsDbContext analyticsDb)
    {
        trendDb.Artikli.AddRange(
            Article(607001, "RQ607-SAME-SKU", 1, 6701, 5, 10, "access"),
            Article(607002, "RQ607-ZERO", 1, 6701, 0, 0, "access"),
            Article(607003, "RQ607-SAME-SKU", 2, 6702, null, 0, "access"),
            Article(607004, "RQ607-NEGATIVE", 2, 6702, -4, 0, "access"),
            Article(607005, "RQ607-EXISTING", 1, 6701, 3, 1, "existing"),
            Article(607006, "RQ607-EXISTING-ZERO", 2, 6702, 0, 1, "existing"),
            Article(607007, "RQ607-NULL-ORIGIN", 1, 6701, 6, 0, null),
            Article(607008, "RQ607-BLANK-ORIGIN", 2, 6702, 2, 0, ""),
            Article(607009, "RQ607-DEMO", 3, 6703, 100, 0, "demo"),
            Article(607010, "RQ607-OUTSIDE-PERIOD", 1, 6701, 50, 0, "access", FromUtc.AddDays(-1)));

        analyticsDb.ProductsDim.AddRange(
            Snapshot(607001, 5, "access"),
            Snapshot(607002, 0, "access"),
            Snapshot(607003, null, "access"),
            Snapshot(607004, -4, "access"),
            Snapshot(607005, 3, "existing"),
            Snapshot(607006, 0, "existing"),
            Snapshot(607007, 6, null),
            Snapshot(607008, 2, ""),
            Snapshot(607009, 100, "demo"),
            Snapshot(607010, 50, "access", FromUtc.AddDays(-1)),
            Snapshot(607099, 8, "access"));
    }

    private static Artikli Article(
        int id,
        string plu,
        int storeId,
        int supplierId,
        int? quantity,
        int minimum,
        string? origin,
        DateTime? updatedAt = null)
        => new()
        {
            Id = id,
            PLU = plu,
            Naziv = plu,
            IDObjekat = storeId,
            IDDobavljac = supplierId,
            Kolicina = quantity,
            MinimalnaKolicina = minimum,
            DataOrigin = origin!,
            UpdatedAt = updatedAt ?? FromUtc.AddHours(2)
        };

    private static ProductsDim Snapshot(
        int productId,
        int? quantity,
        string? origin,
        DateTime? timestamp = null)
        => new()
        {
            ProductId = productId,
            ProductName = $"RQ607-{productId}",
            Kolicina = quantity,
            Timestamp = timestamp ?? FromUtc.AddHours(2),
            DataOrigin = origin!
        };

    private static string Url(string scope, int? storeId = null, int? supplierId = null)
        => $"/api/analytics/cached/inventory/status?lowStockThreshold=2&fromDate={FromUtc:O}&toDate={ToUtc:O}&dataScope={scope}"
           + (storeId.HasValue ? $"&storeId={storeId.Value}" : string.Empty)
           + (supplierId.HasValue ? $"&supplierId={supplierId.Value}" : string.Empty);

    private static async Task<InventoryOracle> ReadOracleAsync(
        NpgsqlConnection connection,
        string scope,
        int? storeId,
        int? supplierId)
    {
        const string sql = """
            WITH scoped_products AS (
                SELECT p."Kolicina"
                FROM "ProductsDim" p
                WHERE p."Timestamp" >= @fromUtc
                  AND p."Timestamp" <= @toUtc
                  AND (
                        @scope = 'all'
                     OR (@scope = 'imported' AND p."DataOrigin" = 'access')
                     OR (@scope = 'existing' AND (p."DataOrigin" = 'existing' OR p."DataOrigin" IS NULL OR p."DataOrigin" = ''))
                  )
                  AND (@storeId IS NULL OR p."ProductId" IN (
                      SELECT a."Id" FROM "Artikli" a
                      WHERE a."IDObjekat" = @storeId
                        AND (@supplierId IS NULL OR a."IDDobavljac" = @supplierId)
                  ))
                  AND (@supplierId IS NULL OR p."ProductId" IN (
                      SELECT a."Id" FROM "Artikli" a WHERE a."IDDobavljac" = @supplierId
                  ))
            )
            SELECT COUNT(*)::int,
                   COALESCE(SUM(CASE WHEN "Kolicina" > 0 THEN "Kolicina" ELSE 0 END), 0)::int,
                   COUNT(*) FILTER (WHERE "Kolicina" = 0)::int
            FROM scoped_products;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("fromUtc", FromUtc);
        command.Parameters.AddWithValue("toUtc", ToUtc);
        command.Parameters.AddWithValue("scope", scope.Trim().ToLowerInvariant());
        command.Parameters.Add("storeId", NpgsqlDbType.Integer).Value = (object?)storeId ?? DBNull.Value;
        command.Parameters.Add("supplierId", NpgsqlDbType.Integer).Value = (object?)supplierId ?? DBNull.Value;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        var skuCount = reader.GetInt32(0);
        var onHand = reader.GetInt32(1);
        var outOfStock = reader.GetInt32(2);
        await reader.CloseAsync();

        const string lowStockSql = """
            SELECT COUNT(*)::int
            FROM "Artikli" a
            WHERE a."UpdatedAt" >= @fromUtc
              AND a."UpdatedAt" <= @toUtc
              AND (@storeId IS NULL OR a."IDObjekat" = @storeId)
              AND (@supplierId IS NULL OR a."IDDobavljac" = @supplierId)
              AND (
                    @scope = 'all'
                 OR (@scope = 'imported' AND a."DataOrigin" = 'access')
                 OR (@scope = 'existing' AND (a."DataOrigin" = 'existing' OR a."DataOrigin" IS NULL OR a."DataOrigin" = ''))
              )
              AND a."Kolicina" IS NOT NULL
              AND a."Kolicina" > 0
              AND (
                    (a."MinimalnaKolicina" > 0 AND a."Kolicina" <= a."MinimalnaKolicina")
                 OR ((a."MinimalnaKolicina" IS NULL OR a."MinimalnaKolicina" <= 0) AND a."Kolicina" <= 2)
              );
            """;
        await using var lowStockCommand = new NpgsqlCommand(lowStockSql, connection);
        lowStockCommand.Parameters.AddWithValue("fromUtc", FromUtc);
        lowStockCommand.Parameters.AddWithValue("toUtc", ToUtc);
        lowStockCommand.Parameters.AddWithValue("scope", scope.Trim().ToLowerInvariant());
        lowStockCommand.Parameters.Add("storeId", NpgsqlDbType.Integer).Value = (object?)storeId ?? DBNull.Value;
        lowStockCommand.Parameters.Add("supplierId", NpgsqlDbType.Integer).Value = (object?)supplierId ?? DBNull.Value;
        var lowStock = (int)(await lowStockCommand.ExecuteScalarAsync() ?? 0);
        return new InventoryOracle(skuCount, onHand, lowStock, outOfStock);
    }

    private static void AssertInventory(InventoryOracle expected, JsonElement actual)
    {
        Assert.Equal(expected.SkuCount, actual.GetProperty("totalSkuCount").GetInt32());
        Assert.Equal(expected.OnHand, actual.GetProperty("totalOnHand").GetInt32());
        Assert.Equal(expected.LowStock, actual.GetProperty("lowStockCount").GetInt32());
        Assert.Equal(expected.OutOfStock, actual.GetProperty("outOfStockCount").GetInt32());
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{url} returned {(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private sealed record InventoryOracle(int SkuCount, int OnHand, int LowStock, int OutOfStock);
}

/// <summary>
/// RQ607 uses the existing local PostgreSQL service and creates a disposable database on it.
/// This avoids a silent Testcontainers skip while keeping the real trendplus database untouched.
/// </summary>
public sealed class Rq607PostgresFixture : IAsyncLifetime
{
    private readonly List<string> _createdDatabases = [];
    private readonly string _configuredConnectionString =
        Environment.GetEnvironmentVariable("TRENDPLUS_RQ607_POSTGRES_CONNECTION")
        ?? "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";

    public bool IsAvailable { get; private set; }

    public string AdminConnectionString
    {
        get
        {
            var builder = new NpgsqlConnectionStringBuilder(_configuredConnectionString)
            {
                Database = "postgres"
            };
            return builder.ConnectionString;
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(AdminConnectionString);
            await connection.OpenAsync();
            IsAvailable = true;
        }
        catch
        {
            IsAvailable = false;
        }
    }

    public async Task<string?> TryCreateDatabaseConnectionStringAsync(string databaseName)
    {
        if (!IsAvailable)
            return null;

        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\";", connection))
        {
            await create.ExecuteNonQueryAsync();
        }

        _createdDatabases.Add(databaseName);
        var builder = new NpgsqlConnectionStringBuilder(AdminConnectionString)
        {
            Database = databaseName
        };
        return builder.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        if (!IsAvailable)
            return;

        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();
        foreach (var databaseName in _createdDatabases)
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE);", connection);
            await drop.ExecuteNonQueryAsync();
        }
    }
}

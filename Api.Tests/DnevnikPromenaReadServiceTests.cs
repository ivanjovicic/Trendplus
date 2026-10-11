using Api.Models;
using Api.Services;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit.Abstractions;
using Xunit;

namespace Api.Tests;

[Collection(IntelligencePostgresCollection.Name)]
public sealed class DnevnikPromenaReadServiceTests
{
    private static readonly DateTime Store11Earlier = new(2026, 10, 11, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Store22Later = new(2026, 10, 11, 10, 0, 0, DateTimeKind.Utc);

    private readonly IntelligencePostgresFixture _fixture;
    private readonly ITestOutputHelper _output;

    public DnevnikPromenaReadServiceTests(IntelligencePostgresFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsTrendplusDetail_WhenRecordExists()
    {
        await using var databases = await DnevnikPromenaTestDatabases.CreateAsync(_fixture);
        ReportDatabaseIdentity(databases);
        await SeedTrendArticleAsync(databases.TrendDb, 617001, "Test Artikal Detail", 11);
        await SeedTrendMovementAsync(
            databases.TrendDb, 617101, "Nivelacija", Store11Earlier, 617001, "DET-617", 4350m,
            "existing", quantity: 3, oldPrice: 1200m, newPrice: 1450m, comment: "Detalj test", user: "rq617-tester");

        var service = CreateService(databases);
        var detail = await service.GetByIdAsync(617101);

        Assert.NotNull(detail);
        Assert.Equal(617101, detail!.Id);
        Assert.Equal(617101, detail.SourceId);
        Assert.Equal("Test Artikal Detail", detail.NazivArtikla);
        Assert.Equal(3, detail.Kolicina);
        Assert.Equal(1200m, detail.StaraCena);
        Assert.Equal(1450m, detail.NovaCena);
        Assert.Equal("DET-617", detail.BrojRacuna);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersPaginatesAndKeepsBothStoreRowsInScope()
    {
        await using var databases = await DnevnikPromenaTestDatabases.CreateAsync(_fixture);
        ReportDatabaseIdentity(databases);
        await SeedTrendArticleAsync(databases.TrendDb, 617011, "Article store 11", 11);
        await SeedTrendArticleAsync(databases.TrendDb, 617022, "Article store 22", 22);
        await SeedTrendMovementAsync(databases.TrendDb, 617201, "Prodaja", Store11Earlier, 617011, "STORE-11", 1200m, "existing");
        await SeedTrendMovementAsync(databases.TrendDb, 617202, "Prodaja", Store22Later, 617022, "STORE-22", 1800m, "access");
        await SeedTrendMovementAsync(databases.TrendDb, 617203, "Nivelacija", Store22Later.AddHours(1), 617022, "OTHER-23", 2400m, "existing");

        var service = CreateService(databases);
        var pageOne = await service.GetPagedAsync(new DnevnikPromenaListQuery
        {
            PageNumber = 1,
            PageSize = 1,
            BrojRacuna = "STORE-",
            SortBy = "datum",
            SortDir = "desc",
            DataScope = "all"
        });
        var pageTwo = await service.GetPagedAsync(new DnevnikPromenaListQuery
        {
            PageNumber = 2,
            PageSize = 1,
            BrojRacuna = "STORE-",
            SortBy = "datum",
            SortDir = "desc",
            DataScope = "all"
        });
        var imported = await service.GetPagedAsync(new DnevnikPromenaListQuery
        {
            BrojRacuna = "STORE-",
            DataScope = "imported"
        });
        var store11 = await service.GetPagedAsync(new DnevnikPromenaListQuery
        {
            ArtikalId = 617011,
            DataScope = "all"
        });
        var store22 = await service.GetPagedAsync(new DnevnikPromenaListQuery
        {
            ArtikalId = 617022,
            DataScope = "all"
        });

        Assert.Equal(2, pageOne.TotalCount);
        Assert.Equal(617202, Assert.Single(pageOne.Items).Id);
        Assert.Equal("Article store 22", pageOne.Items[0].ArtikalNaziv);
        Assert.Equal(2, pageTwo.TotalCount);
        Assert.Equal(617201, Assert.Single(pageTwo.Items).Id);
        Assert.Equal("Article store 11", pageTwo.Items[0].ArtikalNaziv);
        Assert.Equal(617202, Assert.Single(imported.Items).Id);
        Assert.Equal(617201, Assert.Single(store11.Items).Id);
        Assert.Equal(2, store22.TotalCount);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsAnalyticsFallback_WhenTrendplusRecordIsMissing()
    {
        await using var databases = await DnevnikPromenaTestDatabases.CreateAsync(_fixture);
        ReportDatabaseIdentity(databases);
        await databases.AnalyticsDb.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ProductsDim" ("ProductId", "ProductName", "Category", "SubCategory", "Brand", "IsActive", "Timestamp")
            VALUES ({617301}, {"Fallback Product"}, {"Test"}, {"Test"}, {"Trendplus"}, {true}, {Store11Earlier});
            """);
        await databases.AnalyticsDb.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "InventoryMovementFacts"
                ("SourceId", "TipPromene", "Datum", "ArtikalId", "Kolicina", "NovaProdajnaCena", "Iznos", "BrojDokumenta", "KorisnikIme", "DataOrigin")
            VALUES ({617302}, {"Unos robe"}, {Store11Earlier}, {617301}, {7}, {1999m}, {13993m}, {"FB-617"}, {"fallback-user"}, {"access"});
            """);

        var detail = await CreateService(databases).GetByIdAsync(617302);

        Assert.NotNull(detail);
        Assert.Equal(617302, detail!.Id);
        Assert.Equal(617302, detail.SourceId);
        Assert.Equal("Fallback Product", detail.NazivArtikla);
        Assert.Equal("FB-617", detail.BrojRacuna);
        Assert.Equal("fallback-user", detail.KorisnikIme);
        Assert.Null(detail.Komentar);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNeitherDatabaseContainsRecord()
    {
        await using var databases = await DnevnikPromenaTestDatabases.CreateAsync(_fixture);
        ReportDatabaseIdentity(databases);

        var detail = await CreateService(databases).GetByIdAsync(617401);

        Assert.Null(detail);
    }

    [Fact]
    public async Task GetPagedAsync_PropagatesDatabaseFailure_WhenRequiredTableIsMissing()
    {
        await using var databases = await DnevnikPromenaTestDatabases.CreateAsync(_fixture);
        ReportDatabaseIdentity(databases);
        await databases.TrendDb.Database.ExecuteSqlRawAsync("DROP TABLE \"DnevnikPromena\";");

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            CreateService(databases).GetPagedAsync(new DnevnikPromenaListQuery()));

        Assert.Equal(PostgresErrorCodes.UndefinedTable, exception.SqlState);
    }

    [Fact]
    public async Task ConfiguredBusinessNamedDatabasesAreRejectedBeforeAnyDml()
    {
        var safetyAdminDatabase = await _fixture.CreateDatabaseAsync("rq617_safety_admin");
        var businessDatabaseName = $"trendplus_rq617_{Guid.NewGuid():N}";
        var adminConnectionString = new NpgsqlConnectionStringBuilder(safetyAdminDatabase.ConnectionString)
        {
            Database = "postgres",
            Pooling = false
        }.ConnectionString;
        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{businessDatabaseName}\";", admin);
            await create.ExecuteNonQueryAsync();
        }

        var businessConnectionString = new NpgsqlConnectionStringBuilder(safetyAdminDatabase.ConnectionString)
        {
            Database = businessDatabaseName,
            Pooling = false
        }.ConnectionString;
        _output.WriteLine(
            $"RQ617 safety admin database: {safetyAdminDatabase.Name}; business-looking database created inside disposable container: {businessDatabaseName}; fixture registration: rejected.");
        await using (var businessDatabase = new NpgsqlConnection(businessConnectionString))
        {
            await businessDatabase.OpenAsync();
            await using var seed = new NpgsqlCommand("""
                CREATE TABLE public.rq617_dml_audit (operation text NOT NULL);
                CREATE TABLE public."DnevnikPromena" (id integer PRIMARY KEY, marker integer NOT NULL);
                INSERT INTO public."DnevnikPromena"(id, marker) VALUES (1, 617);
                CREATE FUNCTION public.rq617_record_dml() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    INSERT INTO public.rq617_dml_audit(operation) VALUES (TG_OP);
                    RETURN COALESCE(NEW, OLD);
                END;
                $$;
                CREATE TRIGGER rq617_dml_probe
                    AFTER INSERT OR UPDATE OR DELETE ON public."DnevnikPromena"
                    FOR EACH ROW EXECUTE FUNCTION public.rq617_record_dml();
                """, businessDatabase);
            await seed.ExecuteNonQueryAsync();
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = businessConnectionString,
                ["ConnectionStrings:AnalyticsConnection"] = businessConnectionString
            })
            .Build();

        var configuredTrend = new IntelligenceTestDatabase(
            businessDatabaseName,
            configuration.GetConnectionString("DefaultConnection")!);
        var configuredAnalytics = new IntelligenceTestDatabase(
            businessDatabaseName,
            configuration.GetConnectionString("AnalyticsConnection")!);
        var trendException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _fixture.OpenConnectionAsync(configuredTrend));
        var analyticsException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _fixture.OpenConnectionAsync(configuredAnalytics));
        Assert.Contains("not issued by the disposable", trendException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not issued by the disposable", analyticsException.Message, StringComparison.OrdinalIgnoreCase);

        await using var verify = new NpgsqlConnection(businessConnectionString);
        await verify.OpenAsync();
        Assert.Equal(617, await ReadScalarAsync<int>(verify, "SELECT marker FROM public.\"DnevnikPromena\" WHERE id = 1;"));
        Assert.Equal(0, await ReadScalarAsync<int>(verify, "SELECT count(*) FROM public.rq617_dml_audit;"));
    }

    private static DnevnikPromenaReadService CreateService(DnevnikPromenaTestDatabases databases)
    {
        return new DnevnikPromenaReadService(
            databases.TrendDb,
            databases.AnalyticsDb,
            NullLogger<DnevnikPromenaReadService>.Instance);
    }

    private void ReportDatabaseIdentity(DnevnikPromenaTestDatabases databases)
    {
        _output.WriteLine(
            $"RQ617 disposable database pair: {databases.TrendDatabaseName} + {databases.AnalyticsDatabaseName}; both are migrated and fixture-issued.");
    }

    private sealed class DnevnikPromenaTestDatabases : IAsyncDisposable
    {
        private DnevnikPromenaTestDatabases(
            IntelligenceTestDatabase trendDatabase,
            TrendplusDbContext trendDb,
            IntelligenceTestDatabase analyticsDatabase,
            AnalyticsDbContext analyticsDb)
        {
            TrendDatabaseName = trendDatabase.Name;
            TrendConnectionString = trendDatabase.ConnectionString;
            TrendDb = trendDb;
            AnalyticsDatabaseName = analyticsDatabase.Name;
            AnalyticsConnectionString = analyticsDatabase.ConnectionString;
            AnalyticsDb = analyticsDb;
        }

        public string TrendDatabaseName { get; }
        public string TrendConnectionString { get; }
        public TrendplusDbContext TrendDb { get; }
        public string AnalyticsDatabaseName { get; }
        public string AnalyticsConnectionString { get; }
        public AnalyticsDbContext AnalyticsDb { get; }

        public static async Task<DnevnikPromenaTestDatabases> CreateAsync(IntelligencePostgresFixture fixture)
        {
            var trendDatabase = await fixture.CreateDatabaseAsync("rq617_trend");
            var analyticsDatabase = await fixture.CreateDatabaseAsync("rq617_analytics");
            var trendDb = new TrendplusDbContext(
                new DbContextOptionsBuilder<TrendplusDbContext>()
                    .UseNpgsql(trendDatabase.ConnectionString)
                    .Options);
            var analyticsDb = new AnalyticsDbContext(
                new DbContextOptionsBuilder<AnalyticsDbContext>()
                    .UseNpgsql(analyticsDatabase.ConnectionString)
                    .Options);

            try
            {
                await trendDb.Database.MigrateAsync();
                await analyticsDb.Database.MigrateAsync();
                await VerifyMigratedSchemaAsync(trendDb, analyticsDb, trendDatabase.Name, analyticsDatabase.Name);
                return new DnevnikPromenaTestDatabases(trendDatabase, trendDb, analyticsDatabase, analyticsDb);
            }
            catch
            {
                await trendDb.DisposeAsync();
                await analyticsDb.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await TrendDb.DisposeAsync();
            await AnalyticsDb.DisposeAsync();
        }

        private static async Task VerifyMigratedSchemaAsync(
            TrendplusDbContext trendDb,
            AnalyticsDbContext analyticsDb,
            string trendDatabaseName,
            string analyticsDatabaseName)
        {
            var trendMigrations = await trendDb.Database.GetAppliedMigrationsAsync();
            var analyticsMigrations = await analyticsDb.Database.GetAppliedMigrationsAsync();
            if (!trendMigrations.Any() || !analyticsMigrations.Any())
            {
                throw new InvalidOperationException("RQ617 fixture databases must have their EF migrations applied.");
            }

            if (!string.Equals(await CurrentDatabaseAsync(trendDb), trendDatabaseName, StringComparison.Ordinal)
                || !string.Equals(await CurrentDatabaseAsync(analyticsDb), analyticsDatabaseName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("RQ617 contexts are not connected to their fixture-issued database identities.");
            }

            await RequireRelationAsync(trendDb, "public.\"Artikli\"");
            await RequireRelationAsync(trendDb, "public.\"DnevnikPromena\"");
            await RequireRelationAsync(analyticsDb, "public.\"ProductsDim\"");
            await RequireRelationAsync(analyticsDb, "public.\"InventoryMovementFacts\"");
        }

        private static async Task<string> CurrentDatabaseAsync(DbContext context)
        {
            var connection = context.Database.GetDbConnection();
            await context.Database.OpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT current_database();";
            return (string)(await command.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Could not read PostgreSQL database identity."));
        }

        private static async Task RequireRelationAsync(DbContext context, string relation)
        {
            var connection = context.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT to_regclass(@relation) IS NOT NULL;";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "relation";
            parameter.Value = relation;
            command.Parameters.Add(parameter);
            if ((bool)(await command.ExecuteScalarAsync()
                ?? throw new InvalidOperationException($"Could not verify migrated relation {relation}.")) is false)
            {
                throw new InvalidOperationException($"RQ617 fixture migration did not create required relation {relation}.");
            }
        }
    }

    private static async Task SeedTrendArticleAsync(TrendplusDbContext db, int id, string name, int storeId)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Artikli" ("Id", "Naziv", "UpdatedAt", "IDObjekat", "DataOrigin")
            VALUES ({id}, {name}, {Store11Earlier}, {storeId}, {"existing"});
            """);
    }

    private static async Task SeedTrendMovementAsync(
        TrendplusDbContext db,
        int id,
        string type,
        DateTime date,
        int articleId,
        string receipt,
        decimal amount,
        string origin,
        int? quantity = null,
        decimal? oldPrice = null,
        decimal? newPrice = null,
        string? comment = null,
        string? user = null)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "DnevnikPromena"
                ("Id", "TipPromene", "Datum", "ArtikalId", "Kolicina", "StaraProdajnaCena", "NovaProdajnaCena", "Iznos", "BrojRacuna", "Komentar", "KorisnikIme", "DataOrigin")
            VALUES ({id}, {type}, {date}, {articleId}, {quantity}, {oldPrice}, {newPrice}, {amount}, {receipt}, {comment}, {user}, {origin});
            """);
    }

    private static async Task<T> ReadScalarAsync<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException($"Expected a scalar result for '{sql}'.");
        return (T)Convert.ChangeType(value, typeof(T));
    }
}

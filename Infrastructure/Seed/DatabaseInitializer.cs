using Infrastructure.DbContexts;
using Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure.Seed;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabasesAsync(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger logger)
    {
        logger.LogInformation("=== DATABASE INITIALIZATION START ===");

        try
        {
            // 1. Initialize Trendplus DB
            await InitializeTrendplusDbAsync(services, configuration, logger);

            // 2. Initialize Analytics DB
            await InitializeAnalyticsDbAsync(services, configuration, logger);

            logger.LogInformation("=== DATABASE INITIALIZATION COMPLETE ===");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database initialization failed");
            throw;
        }
    }

    private static async Task InitializeTrendplusDbAsync(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger logger)
    {
        logger.LogInformation("Initializing Trendplus database...");

        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();

        // WORKAROUND: Mark problematic migration as applied before running EF migrations
        // This is needed because columns were already added via manual SQL scripts
        try
        {
            var connection = context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                VALUES ('20260112000000_AddArtikliKategorije', '8.0.0')
                ON CONFLICT (""MigrationId"") DO NOTHING;
            ";
            
            await command.ExecuteNonQueryAsync();
            logger.LogInformation("? Marked migration 20260112000000_AddArtikliKategorije as applied");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to mark migration as applied (table might not exist yet)");
        }

        // Run EF migrations
        await context.Database.MigrateAsync();
        logger.LogInformation("? Trendplus DB migrations applied");

        // Check if we need to seed data
        if (!await context.Artikli.AnyAsync())
        {
            logger.LogInformation("No Artikli found, running seed script...");
            await ExecuteSqlFileAsync(
                configuration.GetConnectionString("DefaultConnection")!,
                "Database/Migrations/005_CreateArtikliAndTestData.sql",
                logger);
        }
        else
        {
            logger.LogInformation("? Trendplus DB already has data");
        }
    }

    private static async Task InitializeAnalyticsDbAsync(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger logger)
    {
        logger.LogInformation("Initializing Analytics database...");

        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        var trendDb = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();

        // Run EF migrations
        await context.Database.MigrateAsync();
        logger.LogInformation("? Analytics DB migrations applied");

        // Check if we need to create tables
        if (!await TableExistsAsync(
            configuration.GetConnectionString("AnalyticsConnection")!,
            "SalesFacts",
            logger))
        {
            logger.LogInformation("SalesFacts table not found, creating...");
            await ExecuteSqlFileAsync(
                configuration.GetConnectionString("AnalyticsConnection")!,
                "Database/Analytics/001_CreateSalesFactTables.sql",
                logger);
        }

        if (!await TableExistsAsync(
            configuration.GetConnectionString("AnalyticsConnection")!,
            "ProductsDim",
            logger))
        {
            logger.LogInformation("ProductsDim table not found, creating...");
            await ExecuteSqlFileAsync(
                configuration.GetConnectionString("AnalyticsConnection")!,
                "Database/Analytics/002_AddVelicinaBojaToProductsDim.sql",
                logger);
        }

        if (!await TableExistsAsync(
            configuration.GetConnectionString("AnalyticsConnection")!,
            "PerformanceLogs",
            logger))
        {
            logger.LogInformation("PerformanceLogs table not found, creating...");
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""PerformanceLogs"" (
                    ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                    ""Timestamp"" timestamp with time zone NOT NULL,
                    ""RequestType"" character varying(200) NOT NULL,
                    ""RequestName"" character varying(500) NOT NULL,
                    ""DurationMs"" integer NOT NULL,
                    ""RequestData"" character varying(4000),
                    ""ResponseData"" character varying(4000),
                    ""ExceptionMessage"" character varying(2000),
                    ""IsSuccess"" boolean NOT NULL
                );

                CREATE INDEX IF NOT EXISTS ""IX_PerformanceLogs_Timestamp"" ON ""PerformanceLogs"" (""Timestamp"");
                CREATE INDEX IF NOT EXISTS ""IX_PerformanceLogs_DurationMs"" ON ""PerformanceLogs"" (""DurationMs"");
                CREATE INDEX IF NOT EXISTS ""IX_PerformanceLogs_RequestName"" ON ""PerformanceLogs"" (""RequestName"");
            ");
        }

        logger.LogInformation("? Analytics DB initialized");

        // Backfill historical sales into analytics facts (idempotent).
        await BackfillSalesFactsAsync(trendDb, context, logger);
    }

    private static async Task BackfillSalesFactsAsync(
        TrendplusDbContext trendDb,
        AnalyticsDbContext analyticsDb,
        ILogger logger)
    {
        try
        {
            var startedAt = DateTime.UtcNow;
            var existingSaleIds = await analyticsDb.SalesFacts
                .AsNoTracking()
                .Select(x => x.SaleId)
                .ToListAsync();

            var existingSet = new HashSet<int>(existingSaleIds);

            var sales = await trendDb.ProdajaZaglavlja
                .Include(p => p.Stavke)
                .AsNoTracking()
                .ToListAsync();

            logger.LogInformation(
                "SalesFacts backfill check: sourceSales={SourceSales}, existingFacts={ExistingFacts}",
                sales.Count,
                existingSaleIds.Count);

            if (sales.Count == 0)
            {
                logger.LogInformation("No source sales found for SalesFacts backfill.");
                return;
            }

            var factsToInsert = new List<SalesFact>();
            var linesToInsert = new List<SalesLineFact>();

            foreach (var sale in sales)
            {
                if (existingSet.Contains(sale.Id))
                    continue;

                var totalAmount = sale.Stavke.Sum(s => s.Kolicina * s.Cena);
                var totalUnits = sale.Stavke.Sum(s => s.Kolicina);

                factsToInsert.Add(new SalesFact
                {
                    SaleId = sale.Id,
                    BrojRacuna = sale.BrojRacuna ?? string.Empty,
                    SaleTimestampUtc = DateTime.SpecifyKind(sale.DatumProdaje, DateTimeKind.Utc),
                    StoreId = sale.IDObjekat ?? 1,
                    PaymentType = sale.NacinPlacanja ?? string.Empty,
                    TotalAmount = totalAmount,
                    TotalUnits = totalUnits,
                    TotalLines = sale.Stavke.Count
                });

                foreach (var line in sale.Stavke)
                {
                    linesToInsert.Add(new SalesLineFact
                    {
                        SaleId = sale.Id,
                        ProductId = line.IdArtikal,
                        Qty = line.Kolicina,
                        UnitPrice = line.Cena,
                        LineTotal = line.Kolicina * line.Cena
                    });
                }
            }

            if (factsToInsert.Count == 0)
            {
                logger.LogInformation(
                    "SalesFacts backfill skipped: analytics already synced. sourceSales={SourceSales}, existingFacts={ExistingFacts}, durationMs={DurationMs}",
                    sales.Count,
                    existingSaleIds.Count,
                    (DateTime.UtcNow - startedAt).TotalMilliseconds);
                return;
            }

            await analyticsDb.SalesFacts.AddRangeAsync(factsToInsert);
            await analyticsDb.SalesLineFacts.AddRangeAsync(linesToInsert);
            await analyticsDb.SaveChangesAsync();

            logger.LogInformation(
                "SalesFacts backfill completed: insertedSales={InsertedSales}, insertedLines={InsertedLines}, skippedSales={SkippedSales}, totalSourceSales={TotalSourceSales}, existingBefore={ExistingBefore}, totalFactsAfter={TotalFactsAfter}, durationMs={DurationMs}",
                factsToInsert.Count,
                linesToInsert.Count,
                sales.Count - factsToInsert.Count,
                sales.Count,
                existingSaleIds.Count,
                existingSaleIds.Count + factsToInsert.Count,
                (DateTime.UtcNow - startedAt).TotalMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to backfill SalesFacts during startup.");
        }
    }

    private static async Task<bool> TableExistsAsync(
        string connectionString,
        string tableName,
        ILogger logger)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT EXISTS (
                    SELECT 1 
                    FROM information_schema.tables 
                    WHERE table_schema = 'public' 
                    AND table_name = @tableName
                );";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("tableName", tableName);

            var result = await command.ExecuteScalarAsync();
            return result is bool exists && exists;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error checking if table {TableName} exists", tableName);
            return false;
        }
    }

    private static async Task ExecuteSqlFileAsync(
        string connectionString,
        string sqlFilePath,
        ILogger logger)
    {
        try
        {
            // Check if file exists
            if (!File.Exists(sqlFilePath))
            {
                logger.LogWarning("SQL file not found: {FilePath}", sqlFilePath);
                return;
            }

            // Read SQL file
            var sql = await File.ReadAllTextAsync(sqlFilePath);

            // Execute SQL
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand(sql, connection);
            command.CommandTimeout = 300; // 5 minutes

            await command.ExecuteNonQueryAsync();

            logger.LogInformation("? Executed SQL file: {FilePath}", sqlFilePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute SQL file: {FilePath}", sqlFilePath);
            throw;
        }
    }
}

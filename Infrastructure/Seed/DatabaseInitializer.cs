using Infrastructure.DbContexts;
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
            logger.LogInformation("✅ Marked migration 20260112000000_AddArtikliKategorije as applied");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to mark migration as applied (table might not exist yet)");
        }

        // Run EF migrations
        await context.Database.MigrateAsync();
        logger.LogInformation("✅ Trendplus DB migrations applied");

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
            logger.LogInformation("✅ Trendplus DB already has data");
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

        // Run EF migrations
        await context.Database.MigrateAsync();
        logger.LogInformation("✅ Analytics DB migrations applied");

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

        logger.LogInformation("✅ Analytics DB initialized");
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

            logger.LogInformation("✅ Executed SQL file: {FilePath}", sqlFilePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute SQL file: {FilePath}", sqlFilePath);
            throw;
        }
    }
}

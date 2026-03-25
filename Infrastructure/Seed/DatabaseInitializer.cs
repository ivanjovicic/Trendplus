using Infrastructure.DbContexts;
using Infrastructure.Analytics;
using Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Security.Cryptography;

namespace Infrastructure.Seed;

public static class DatabaseInitializer
{
    private const long AdvisoryLockKey = 987654321L;
    private const long SupplierDecisionHubBuildLockKey = 987654322L;
    private const long AnalyticsIntelligenceBuildLockKey = 987654323L;
    private const int AdvisoryLockCommandTimeoutSeconds = 10;
    private const int AdvisoryLockRetryDelaySeconds = 2;
    private const int AdvisoryLockMaxWaitSeconds = 120;
    private const int BootstrapCommandTimeoutSeconds = 600;
    private const int StartupSqlLockTimeoutSeconds = 15;

    public static async Task InitializeDatabasesAsync(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger logger)
    {
        logger.LogInformation("=== DATABASE INITIALIZATION START ===");

        var defaultConnection = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(defaultConnection))
        {
            logger.LogCritical("Connection string 'DefaultConnection' is missing or empty.");
            throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");
        }

        await using var connection = new NpgsqlConnection(defaultConnection);
        await connection.OpenAsync();

        // Global advisory lock da samo jedna instanca radi init (ali ne blokira druge konekcije)
        var lockAcquired = await TryAcquireAdvisoryLockAsync(
            connection,
            logger,
            AdvisoryLockKey,
            TimeSpan.FromSeconds(AdvisoryLockMaxWaitSeconds),
            TimeSpan.FromSeconds(AdvisoryLockRetryDelaySeconds));

        if (!lockAcquired)
        {
            logger.LogWarning(
                "Skipping database initialization because advisory startup lock {Key} is still held by another instance.",
                AdvisoryLockKey);
            return;
        }

        var trendplusInitialized = false;
        var analyticsInitialized = false;

        try
        {
            try
            {
                await InitializeTrendplusDbAsync(services, configuration, logger);
                trendplusInitialized = true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Trendplus DB initialization failed.");
            }

            try
            {
                await InitializeAnalyticsDbAsync(services, configuration, logger);
                analyticsInitialized = true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Analytics DB initialization failed.");
            }

            if (!trendplusInitialized || !analyticsInitialized)
            {
                var failFast = configuration.GetValue<bool>("DatabaseInitialization:FailFast");
                if (failFast)
                {
                    logger.LogCritical(
                        "Database initialization failed in strict mode. Trendplus={TrendplusOk}, Analytics={AnalyticsOk}",
                        trendplusInitialized, analyticsInitialized);

                    throw new InvalidOperationException("Database initialization failed.");
                }

                logger.LogWarning(
                    "Database initialization completed with errors (non-strict mode). Trendplus={TrendplusOk}, Analytics={AnalyticsOk}",
                    trendplusInitialized, analyticsInitialized);
            }
            else
            {
                logger.LogInformation("=== DATABASE INITIALIZATION COMPLETED SUCCESSFULLY ===");
            }
        }
        finally
        {
            await using var unlockCmd = new NpgsqlCommand("SELECT pg_advisory_unlock(@key);", connection);
            unlockCmd.CommandTimeout = AdvisoryLockCommandTimeoutSeconds;
            unlockCmd.Parameters.AddWithValue("key", AdvisoryLockKey);
            await unlockCmd.ExecuteNonQueryAsync();
            logger.LogInformation("Released advisory startup lock with key {Key}.", AdvisoryLockKey);
        }
    }

    private static string? ResolveSqlFilePath(string sqlFilePath)
    {
        if (Path.IsPathRooted(sqlFilePath) && File.Exists(sqlFilePath))
        {
            return Path.GetFullPath(sqlFilePath);
        }

        var relative = sqlFilePath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        var repoCandidate = ResolveSqlFilePathFromRepositoryRoot(relative);
        if (repoCandidate != null)
        {
            return repoCandidate;
        }

        var currentDirectoryCandidate = Path.GetFullPath(relative, Directory.GetCurrentDirectory());
        if (File.Exists(currentDirectoryCandidate))
        {
            return currentDirectoryCandidate;
        }

        var appBaseCandidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relative));
        if (File.Exists(appBaseCandidate))
        {
            return appBaseCandidate;
        }

        return null;
    }

    private static string? ResolveSqlFilePathFromRepositoryRoot(string relativeSqlPath)
    {
        foreach (var startPath in GetRepositoryProbeRoots())
        {
            var repositoryRoot = FindRepositoryRoot(startPath);
            if (repositoryRoot == null)
            {
                continue;
            }

            var candidate = Path.GetFullPath(Path.Combine(repositoryRoot, relativeSqlPath));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetRepositoryProbeRoots()
    {
        yield return AppContext.BaseDirectory;

        var currentDirectory = Directory.GetCurrentDirectory();
        if (!string.Equals(currentDirectory, AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase))
        {
            yield return currentDirectory;
        }
    }

    private static string? FindRepositoryRoot(string startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath))
        {
            return null;
        }

        var directory = new DirectoryInfo(Path.GetFullPath(startPath));
        if (!directory.Exists)
        {
            directory = directory.Parent;
        }

        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Trendplus2.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static async Task<bool> TryAcquireAdvisoryLockAsync(
        NpgsqlConnection connection,
        ILogger logger,
        long key,
        TimeSpan maxWait,
        TimeSpan retryDelay)
    {
        var startedAt = DateTime.UtcNow;

        while (DateTime.UtcNow - startedAt < maxWait)
        {
            await using var lockCmd = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key);", connection);
            lockCmd.CommandTimeout = AdvisoryLockCommandTimeoutSeconds;
            lockCmd.Parameters.AddWithValue("key", key);

            var acquired = (bool?)await lockCmd.ExecuteScalarAsync() ?? false;
            if (acquired)
            {
                logger.LogInformation("Acquired advisory startup lock with key {Key}.", key);
                return true;
            }

            logger.LogInformation(
                "Advisory startup lock {Key} is held by another instance. Retrying in {DelaySeconds}s.",
                key,
                retryDelay.TotalSeconds);

            await Task.Delay(retryDelay);
        }

        return false;
    }

    private static async Task<bool> TryAcquireSingleRunAdvisoryLockAsync(
        NpgsqlConnection connection,
        long key)
    {
        await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key);", connection);
        command.CommandTimeout = AdvisoryLockCommandTimeoutSeconds;
        command.Parameters.AddWithValue("key", key);
        return (bool?)await command.ExecuteScalarAsync() ?? false;
    }

    private static async Task ReleaseSingleRunAdvisoryLockAsync(
        NpgsqlConnection connection,
        long key)
    {
        await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(@key);", connection);
        command.CommandTimeout = AdvisoryLockCommandTimeoutSeconds;
        command.Parameters.AddWithValue("key", key);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> AreSupplierDecisionHubCachesReadyAsync(string connectionString)
    {
        const string sql = """
            SELECT
                to_regclass('public.mv_supplier_markdown_dependency_cache') IS NOT NULL
                AND to_regclass('public.mv_supplier_decision_score_cache') IS NOT NULL
                AND to_regclass('public.mv_supplier_recommendations_cache') IS NOT NULL;
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = AdvisoryLockCommandTimeoutSeconds;
        return (bool?)await command.ExecuteScalarAsync() ?? false;
    }

    private static async Task<bool> AreSupplierDecisionHubCoreViewsReadyAsync(string connectionString)
    {
        const string sql = """
            SELECT
                to_regclass('public.vw_supplier_fullprice_signals') IS NOT NULL
                AND to_regclass('public.vw_supplier_markdown_dependency') IS NOT NULL
                AND to_regclass('public.vw_supplier_decision_score') IS NOT NULL
                AND to_regclass('public.vw_supplier_recommendations') IS NOT NULL;
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = AdvisoryLockCommandTimeoutSeconds;
        return (bool?)await command.ExecuteScalarAsync() ?? false;
    }

    private static async Task<bool> AreVendorSalesNivelacijaViewReadyAsync(string connectionString)
    {
        if (!await AreRelationsReadyAsync(connectionString, "public.vw_vendor_sales_nivelacija"))
        {
            return false;
        }

        return await RelationHasColumnAsync(connectionString, "vw_vendor_sales_nivelacija", "price_event_id")
            && await RelationHasColumnAsync(connectionString, "vw_vendor_sales_nivelacija", "old_price")
            && await RelationHasColumnAsync(connectionString, "vw_vendor_sales_nivelacija", "new_price")
            && await RelationHasColumnAsync(connectionString, "vw_vendor_sales_nivelacija", "coverage_pre30")
            && await RelationHasColumnAsync(connectionString, "vw_vendor_sales_nivelacija", "coverage_post30");
    }

    private static async Task<bool> RelationHasColumnAsync(
        string connectionString,
        string relationName,
        string columnName)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = ANY (current_schemas(FALSE))
                  AND table_name = @relationName
                  AND column_name = @columnName
            );
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = AdvisoryLockCommandTimeoutSeconds;
        command.Parameters.AddWithValue("relationName", relationName);
        command.Parameters.AddWithValue("columnName", columnName);
        return (bool?)await command.ExecuteScalarAsync() ?? false;
    }

    private static async Task<bool> AreRelationsReadyAsync(string connectionString, params string[] relationNames)
    {
        if (relationNames.Length == 0)
        {
            return true;
        }

        const string sql = """
            SELECT COALESCE(BOOL_AND(to_regclass(relation_name) IS NOT NULL), TRUE)
            FROM unnest(@relationNames) AS relation_name;
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = AdvisoryLockCommandTimeoutSeconds;
        command.Parameters.AddWithValue("relationNames", relationNames);
        return (bool?)await command.ExecuteScalarAsync() ?? false;
    }

    private static bool AreSameDatabase(string firstConnectionString, string secondConnectionString)
    {
        var first = new NpgsqlConnectionStringBuilder(firstConnectionString);
        var second = new NpgsqlConnectionStringBuilder(secondConnectionString);

        return string.Equals(first.Host, second.Host, StringComparison.OrdinalIgnoreCase)
            && first.Port == second.Port
            && string.Equals(first.Database, second.Database, StringComparison.OrdinalIgnoreCase)
            && string.Equals(first.Username, second.Username, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task InitializeTrendplusDbAsync(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger logger)
    {
        logger.LogInformation("Initializing Trendplus database...");

        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();

        var connectionString = GetValidatedConnectionString(configuration, "DefaultConnection", logger);

        // FIRST: Ensure core tables exist (self-heal before EF migrations)
        // This allows workers to function even if EF migrations fail
        await EnsureTrendplusCoreSchemaAsync(connectionString, logger);
        await EnsureTrendplusAggregationTablesAsync(connectionString, logger);
        await EnsureTrendplusOutboxSchemaAsync(connectionString, logger);
        await EnsureTrendplusDocumentSchemaAsync(connectionString, logger);

        // Ensure migrations history table exists
        await ExecuteSqlCommandAsync(connectionString, @"
            CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
                ""MigrationId"" character varying(150) NOT NULL PRIMARY KEY,
                ""ProductVersion"" character varying(32) NOT NULL
            );
        ", logger);

        await EnsureTrendplusMigrationHistorySeededAsync(connectionString, logger);

        // Apply EF migrations
        try
        {
            await context.Database.MigrateAsync();
            logger.LogInformation("✔ Trendplus DB migrations applied.");
        }
        catch (PostgresException pgEx) when (pgEx.SqlState == "42P07")
        {
            // Duplicate-object errors (relation already exists) may occur when
            // core schema was bootstrapped earlier. Log a concise warning and continue.
            logger.LogWarning("Trendplus DB migrations encountered duplicate-relation error (42P07): {Message}", pgEx.MessageText);
        }
        catch (PostgresException pgEx)
        {
            // Other Postgres errors: log details but continue (initializer is tolerant by design).
            logger.LogWarning(pgEx, "Trendplus DB migrations failed with Postgres error: SqlState={SqlState}", pgEx.SqlState);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Trendplus DB migrations failed; core schema was already self-healed.");
        }

        // Execute additional SQL files
        var sqlFiles = new[]
        {
            "Database/Migrations/017_CreateNightlyAnalyticsMaterializedViews.sql",
            "Database/Migrations/019_AddAnalyticsDashboardIndexes.sql",
            "Database/Migrations/012_AddAccessImportSupport.sql",
            "Database/Migrations/013_AddVendorSalesNivelacijaViews.sql",
            "Database/Migrations/014_FixNivelacijaViewsFromDnevnik.sql",
            // 014 analytics creates vw_vendor_sales_nivelacija (analytics-native version).
            // It must run before 016 so any CASCADE self-heal leaves 016 free to
            // recreate vw_nivelacija_did and related downstream objects on top of
            // the final vw_vendor_sales_nivelacija contract.
            "Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql",
            "Database/Migrations/016_AnalyticsNivelacijaEnhancements.sql",
            // 018_AddSupplierDecisionHubViews.sql is intentionally excluded here.
            // It creates expensive materialized views (can take 10-30 min on first run)
            // and is fired asynchronously below so it does not block startup.
            "Database/Migrations/005_CreateArtikliAndTestData.sql"
        };

        if (!await AreVendorSalesNivelacijaViewReadyAsync(connectionString))
        {
            logger.LogInformation(
                "vw_vendor_sales_nivelacija is missing required supplier-decision columns. Forcing re-execution of analytics nivelacija SQL before 018.");
            await DeleteAppliedStartupSqlHistoryAsync(connectionString, "Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql");
            await DeleteAppliedStartupSqlHistoryAsync(connectionString, "Database/Migrations/016_AnalyticsNivelacijaEnhancements.sql");
        }

        foreach (var sqlFile in sqlFiles)
        {
            if (string.Equals(sqlFile, "Database/Migrations/013_AddVendorSalesNivelacijaViews.sql", StringComparison.Ordinal))
            {
                // 013 contains DDL + backfill + view rebuilds. Running it in one transaction
                // holds relation locks longer than necessary and can hit 55P03 during startup.
                // Execute it batch-by-batch so each section commits independently.
                await ExecuteSqlFileAsync(connectionString, sqlFile, logger, useTransaction: false);
                continue;
            }

            await ExecuteSqlFileAsync(connectionString, sqlFile, logger);
        }

        await ExecuteSqlFileAsync(
            connectionString,
            "Database/Migrations/020_AddRuntimeScoringSearchIndexes.sql",
            logger,
            commandTimeoutSeconds: 0,
            useTransaction: false);

        await ExecuteSqlFileAsync(
            connectionString,
            "Database/Migrations/025_AddTrendplusPerformanceIndexes.sql",
            logger,
            commandTimeoutSeconds: 0,
            useTransaction: false);

        await ExecuteSqlFileAsync(
            connectionString,
            "Database/Migrations/026_CreatePerformanceExplainTemplates.sql",
            logger);

        // Fire-and-forget: startup now builds only the core supplier decision views from 018.
        // The heavy supplier materialized caches are intentionally deferred so API startup can
        // complete promptly and endpoints can fall back to live views on first run.
        // Each statement auto-commits so partial progress is preserved if the process is killed.
        var bg018ConnectionString = connectionString;
        var bg018Logger = logger;

        var runFullOnStartup = configuration.GetValue<bool>("DatabaseInitialization:RunFullSupplierDecisionHubOnStartup");

        if (runFullOnStartup)
        {
            // Execute the 018 build synchronously during startup (blocking). This can increase
            // startup time significantly; enable only when desired via configuration.
            await using var lockConnection = new NpgsqlConnection(bg018ConnectionString);
            try
            {
                await lockConnection.OpenAsync();

                if (!await TryAcquireSingleRunAdvisoryLockAsync(lockConnection, SupplierDecisionHubBuildLockKey))
                {
                    bg018Logger.LogInformation("[BG] Skipping 018_AddSupplierDecisionHubViews.sql because another instance is already building supplier decision hub views.");
                }
                else
                {
                    var coreViewsReady = await AreSupplierDecisionHubCoreViewsReadyAsync(bg018ConnectionString);
                    var cachesReady = coreViewsReady && await AreSupplierDecisionHubCachesReadyAsync(bg018ConnectionString);

                    if (!coreViewsReady)
                    {
                        bg018Logger.LogInformation("[BG] Supplier decision hub core views are missing. Forcing re-execution of the core-view batch...");
                        await DeleteAppliedStartupSqlHistoryAsync(
                            bg018ConnectionString,
                            "Database/Migrations/018_AddSupplierDecisionHubViews.sql#core-views");

                        bg018Logger.LogInformation("[BG] Starting execution of 018_AddSupplierDecisionHubViews.sql core views (startup-safe mode)...");
                        await ExecuteSqlFileAsync(
                            bg018ConnectionString,
                            "Database/Migrations/018_AddSupplierDecisionHubViews.sql",
                            bg018Logger,
                            commandTimeoutSeconds: 0,
                            useTransaction: false,
                            maxBatchCount: 1,
                            historyIdentifier: "Database/Migrations/018_AddSupplierDecisionHubViews.sql#core-views");
                        bg018Logger.LogInformation("[BG] 018_AddSupplierDecisionHubViews.sql core views completed successfully.");
                    }

                    if (!cachesReady)
                    {
                        bg018Logger.LogInformation("[BG] Supplier decision hub materialized caches are missing. Forcing re-execution of the cache batches...");
                        await DeleteAppliedStartupSqlHistoryAsync(
                            bg018ConnectionString,
                            "Database/Migrations/018_AddSupplierDecisionHubViews.sql#full-build");

                        bg018Logger.LogInformation("[BG] Starting materialized cache build for 018_AddSupplierDecisionHubViews.sql (startup)...");
                        await ExecuteSqlFileAsync(
                            bg018ConnectionString,
                            "Database/Migrations/018_AddSupplierDecisionHubViews.sql",
                            bg018Logger,
                            commandTimeoutSeconds: 0,
                            useTransaction: false,
                            startBatchNumber: 2,
                            historyIdentifier: "Database/Migrations/018_AddSupplierDecisionHubViews.sql#full-build");
                        bg018Logger.LogInformation("[BG] 018_AddSupplierDecisionHubViews.sql materialized caches completed successfully.");
                    }
                    else
                    {
                        bg018Logger.LogInformation("[BG] Skipping 018_AddSupplierDecisionHubViews.sql because supplier decision hub materialized views already exist.");
                    }
                }
            }
            catch (Exception ex)
            {
                bg018Logger.LogWarning(ex, "[BG] 018 supplier decision hub build failed when running on startup. Views may be unavailable until next startup.");
            }
            finally
            {
                if (lockConnection.State == System.Data.ConnectionState.Open)
                {
                    await ReleaseSingleRunAdvisoryLockAsync(lockConnection, SupplierDecisionHubBuildLockKey);
                }
            }
        }
        else
        {
            _ = Task.Run(async () =>
            {
                await using var lockConnection = new NpgsqlConnection(bg018ConnectionString);
                try
                {
                    await lockConnection.OpenAsync();

                    if (!await TryAcquireSingleRunAdvisoryLockAsync(lockConnection, SupplierDecisionHubBuildLockKey))
                    {
                        bg018Logger.LogInformation("[BG] Skipping 018_AddSupplierDecisionHubViews.sql because another instance is already building supplier decision hub views.");
                        return;
                    }

                    var coreViewsReady = await AreSupplierDecisionHubCoreViewsReadyAsync(bg018ConnectionString);
                    var cachesReady = coreViewsReady && await AreSupplierDecisionHubCachesReadyAsync(bg018ConnectionString);
                    if (!coreViewsReady)
                    {
                        bg018Logger.LogInformation("[BG] Supplier decision hub core views are missing. Forcing re-execution of the core-view batch...");
                        await DeleteAppliedStartupSqlHistoryAsync(
                            bg018ConnectionString,
                            "Database/Migrations/018_AddSupplierDecisionHubViews.sql#core-views");

                        bg018Logger.LogInformation("[BG] Starting async execution of 018_AddSupplierDecisionHubViews.sql core views (startup-safe mode)...");
                        await ExecuteSqlFileAsync(
                            bg018ConnectionString,
                            "Database/Migrations/018_AddSupplierDecisionHubViews.sql",
                            bg018Logger,
                            commandTimeoutSeconds: 0,
                            useTransaction: false,
                            maxBatchCount: 1,
                            historyIdentifier: "Database/Migrations/018_AddSupplierDecisionHubViews.sql#core-views");
                        bg018Logger.LogInformation("[BG] 018_AddSupplierDecisionHubViews.sql core views completed successfully.");
                    }

                    if (!cachesReady)
                    {
                        bg018Logger.LogInformation("[BG] Supplier decision hub materialized caches are missing. Forcing re-execution of the cache batches...");
                        await DeleteAppliedStartupSqlHistoryAsync(
                            bg018ConnectionString,
                            "Database/Migrations/018_AddSupplierDecisionHubViews.sql#full-build");

                        bg018Logger.LogInformation("[BG] Starting background materialized cache build for 018_AddSupplierDecisionHubViews.sql...");
                        await ExecuteSqlFileAsync(
                            bg018ConnectionString,
                            "Database/Migrations/018_AddSupplierDecisionHubViews.sql",
                            bg018Logger,
                            commandTimeoutSeconds: 0,
                            useTransaction: false,
                            startBatchNumber: 2,
                            historyIdentifier: "Database/Migrations/018_AddSupplierDecisionHubViews.sql#full-build");
                        bg018Logger.LogInformation("[BG] 018_AddSupplierDecisionHubViews.sql materialized caches completed successfully.");
                    }
                    else
                    {
                        bg018Logger.LogInformation("[BG] Skipping 018_AddSupplierDecisionHubViews.sql because supplier decision hub materialized views already exist.");
                    }
                }
                catch (Exception ex)
                {
                    bg018Logger.LogWarning(ex, "[BG] 018 supplier decision hub core-view build failed. Views may be unavailable until next startup.");
                }
                finally
                {
                    if (lockConnection.State == System.Data.ConnectionState.Open)
                    {
                        await ReleaseSingleRunAdvisoryLockAsync(lockConnection, SupplierDecisionHubBuildLockKey);
                    }
                }
            });
        }

        // Check if data seeding is required
        if (!await context.Artikli.AnyAsync())
        {
            logger.LogInformation("No Artikli found, running seed script...");
            await ExecuteSqlFileAsync(connectionString, "Database/Migrations/005_CreateArtikliAndTestData.sql", logger);
        }
        else
        {
            logger.LogInformation("✔ Trendplus DB already has Artikli data.");
        }
    }

    private static string GetValidatedConnectionString(IConfiguration configuration, string key, ILogger logger)
    {
        var connectionString = configuration.GetConnectionString(key);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogError("Connection string '{Key}' is missing or empty.", key);
            throw new InvalidOperationException($"Connection string '{key}' is required.");
        }
        return connectionString;
    }

    private static async Task ExecuteSqlCommandAsync(string connectionString, string sql, ILogger logger)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand(sql, connection);
            command.CommandTimeout = 300; // 5 minutes
            await command.ExecuteNonQueryAsync();

            logger.LogInformation("✔ Executed SQL command successfully.");
        }
        catch (PostgresException pgEx)
        {
            logger.LogError(pgEx,
                "Postgres error while executing SQL command. SqlState={SqlState}, Detail={Detail}",
                pgEx.SqlState, pgEx.Detail);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute SQL command.");
            throw;
        }
    }

    private static async Task<bool> IsTrendplusCoreSchemaReadyAsync(string connectionString)
    {
        const string sql = @"
            SELECT
                to_regclass('public.""Artikli""') IS NOT NULL
                AND EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'Artikli' AND column_name = 'UpdatedAt'
                )
                AND EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'Artikli' AND column_name = 'DataOrigin'
                )
                AND to_regclass('public.prodaja_zaglavlje') IS NOT NULL
                AND EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'prodaja_zaglavlje' AND column_name = 'data_origin'
                )
                AND to_regclass('public.prodaja_stavke') IS NOT NULL
                AND EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'prodaja_stavke' AND column_name = 'nabavna_cena'
                )
                AND to_regclass('public.""DnevnikPromena""') IS NOT NULL
                AND EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'DnevnikPromena' AND column_name = 'DataOrigin'
                )
                AND EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'DnevnikPromena' AND column_name = 'Kolicina'
                )
                AND to_regclass('public.""DataImportBatches""') IS NOT NULL
                AND EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'DataImportBatches' AND column_name = 'DataOrigin'
                )
                AND to_regclass('public.""ErrorRecords""') IS NOT NULL
                AND to_regclass('public.""AccessImportLog""') IS NOT NULL
                AND to_regclass('public.povracaj_zaglavlje') IS NOT NULL
                AND to_regclass('public.povracaj_stavke') IS NOT NULL;
        ";

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = AdvisoryLockCommandTimeoutSeconds;

        return (bool?)await command.ExecuteScalarAsync() ?? false;
    }

    private static async Task EnsureTrendplusMigrationHistorySeededAsync(string connectionString, ILogger logger)
    {
        var migrationIds = new[]
        {
            "20251224163406_InitialPostgreSQL",
            "20251228104227_AddCorrelationIdToErrorRecord",
            "20251229125016_AddArtikliUpdatedAt",
            "20251229143702_AddLevelToErrorRecord",
            "20251229145610_AddPriceChangeFieldsToDnevnikPromena",
            "20260102113822_AddSezoneTable",
            "20260105110118_AddOutboxMessages",
            "20260109125509_AddProdajaTables",
            "20260109133240_FixProdajaColumnMapping",
            "20260110134237_MigrateSalesToDnevnikPromena",
            "20260110150211_AddPovracajTables",
            "20260112000000_AddArtikliKategorije",
            "20260125111606_AddImagePathToArtikli",
            "20260223131749_AddDataOriginToImportEntities",
            "20260223143834_AddKolicinaToDnevnikPromena",
            "20260223162737_AddMaterijalToArtikli",
            "20260224180000_AddIDObjektRedniBrojToDnevnikPromena",
            "20260225100001_AddKorisnikImeNabavnaCenaToProdaja"
        };

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var migrationId in migrationIds)
        {
            const string sql = @"
                INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                VALUES (@migrationId, '8.0.0')
                ON CONFLICT (""MigrationId"") DO NOTHING;
            ";

            await using var command = new NpgsqlCommand(sql, connection);
            command.CommandTimeout = AdvisoryLockCommandTimeoutSeconds;
            command.Parameters.AddWithValue("migrationId", migrationId);
            await command.ExecuteNonQueryAsync();
        }

        logger.LogInformation("Ensured Trendplus EF migration history is aligned with bootstrap schema.");
    }

    private static async Task EnsureTrendplusCoreSchemaAsync(
        string connectionString,
        ILogger logger)
    {
        await ExecuteSqlCommandAsync(connectionString, @"
            CREATE TABLE IF NOT EXISTS ""CreatedIds"" (
                ""Id"" integer NOT NULL
            );
        ", logger);

        // Legacy compatibility self-heal:
        // some existing databases have these tables without DataOrigin.
        await EnsureImportReferenceDataOriginColumnsAsync(connectionString, logger);

        if (await IsTrendplusCoreSchemaReadyAsync(connectionString))
        {
            logger.LogInformation("Trendplus core schema already present. Skipping bootstrap.");
            return;
        }

        const string sql = @"
            -- Create Artikli table if it doesn't exist (idempotent bootstrap)
            CREATE TABLE IF NOT EXISTS ""Artikli"" (
                ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""PLU"" character varying(100),
                ""Naziv"" character varying(500) NOT NULL DEFAULT '',
                ""Komentar"" text,
                ""NabavnaCena"" numeric(18,2),
                ""NabavnaCenaDin"" numeric(18,2),
                ""PrvaProdajnaCena"" numeric(18,2),
                ""ProdajnaCena"" numeric(18,2),
                ""IDDobavljac"" integer,
                ""IDTipObuce"" integer,
                ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                ""Kolicina"" integer,
                ""MinimalnaKolicina"" integer,
                ""IDObjekat"" integer,
                ""IDSezona"" integer,
                ""Kategorija"" text,
                ""Pol"" text,
                ""Velicina"" text,
                ""Boja"" text,
                ""Materijal"" character varying(100),
                ""ImagePath"" character varying(500),
                ""DataOrigin"" character varying(32) NOT NULL DEFAULT 'existing'
            );

            CREATE TABLE IF NOT EXISTS ""CreatedIds"" (
                ""Id"" integer NOT NULL
            );

            -- BOOTSTRAP_BATCH_BREAK

            -- Core Artikli columns used by workers/services (idempotent).
            ALTER TABLE IF EXISTS ""Artikli"" ADD COLUMN IF NOT EXISTS ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW();
            ALTER TABLE IF EXISTS ""Artikli"" ADD COLUMN IF NOT EXISTS ""Kolicina"" integer;
            ALTER TABLE IF EXISTS ""Artikli"" ADD COLUMN IF NOT EXISTS ""MinimalnaKolicina"" integer;
            ALTER TABLE IF EXISTS ""Artikli"" ADD COLUMN IF NOT EXISTS ""IDObjekat"" integer;
            ALTER TABLE IF EXISTS ""Artikli"" ADD COLUMN IF NOT EXISTS ""IDSezona"" integer;
            ALTER TABLE IF EXISTS ""Artikli"" ADD COLUMN IF NOT EXISTS ""Kategorija"" text;
            ALTER TABLE IF EXISTS ""Artikli"" ADD COLUMN IF NOT EXISTS ""Pol"" text;
            ALTER TABLE IF EXISTS ""Artikli"" ADD COLUMN IF NOT EXISTS ""Velicina"" text;
            ALTER TABLE IF EXISTS ""Artikli"" ADD COLUMN IF NOT EXISTS ""Boja"" text;
            ALTER TABLE IF EXISTS ""Artikli"" ADD COLUMN IF NOT EXISTS ""Materijal"" character varying(100);
            ALTER TABLE IF EXISTS ""Artikli"" ADD COLUMN IF NOT EXISTS ""ImagePath"" character varying(500);
            ALTER TABLE IF EXISTS ""Artikli"" ADD COLUMN IF NOT EXISTS ""DataOrigin"" character varying(32) NOT NULL DEFAULT 'existing';

            -- BOOTSTRAP_BATCH_BREAK

            CREATE INDEX IF NOT EXISTS ""IX_Artikli_ImagePath"" ON ""Artikli"" (""ImagePath"");
            CREATE INDEX IF NOT EXISTS ""IX_Artikli_UpdatedAt"" ON ""Artikli"" (""UpdatedAt"" DESC);

            -- BOOTSTRAP_BATCH_BREAK
            -- Create prodaja_zaglavlje table if it doesn't exist (idempotent bootstrap)
            CREATE TABLE IF NOT EXISTS prodaja_zaglavlje (
                id              integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                broj_racuna     character varying(100),
                datum_prodaje   timestamp with time zone NOT NULL,
                nacin_placanja  character varying(100),
                id_objekat      integer,
                korisnik_ime    character varying(200),
                data_origin     character varying(32) NOT NULL DEFAULT 'existing'
            );

            -- Create prodaja_stavke table if it doesn't exist (idempotent bootstrap)
            CREATE TABLE IF NOT EXISTS prodaja_stavke (
                id              integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                id_prodaja      integer NOT NULL REFERENCES prodaja_zaglavlje(id) ON DELETE CASCADE,
                id_artikal      integer NOT NULL,
                kolicina        integer NOT NULL,
                cena            decimal(18,2) NOT NULL,
                nabavna_cena    decimal(18,2)
            );
            CREATE INDEX IF NOT EXISTS IX_prodaja_stavke_id_prodaja ON prodaja_stavke (id_prodaja);

            -- Prodaja operational columns (idempotent)
            ALTER TABLE IF EXISTS prodaja_zaglavlje ADD COLUMN IF NOT EXISTS korisnik_ime character varying(200);
            ALTER TABLE IF EXISTS prodaja_stavke    ADD COLUMN IF NOT EXISTS nabavna_cena decimal(18,2);

            -- BOOTSTRAP_BATCH_BREAK
            -- Create DnevnikPromena table if it doesn't exist (idempotent bootstrap)
            CREATE TABLE IF NOT EXISTS ""DnevnikPromena"" (
                ""Id""                integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""TipPromene""        character varying(100) NOT NULL,
                ""Datum""             timestamp with time zone NOT NULL,
                ""Iznos""             decimal(18,2) NOT NULL,
                ""BrojRacuna""        character varying(100),
                ""DobavljacId""       integer,
                ""ArtikalId""         integer,
                ""StaraProdajnaCena"" decimal(18,2),
                ""NovaProdajnaCena""  decimal(18,2),
                ""Kolicina""          integer,
                ""IDObjekat""         integer,
                ""RedniBroj""         integer,
                ""Komentar""          character varying(500),
                ""KorisnikIme""       character varying(200),
                ""DataOrigin""        character varying(32) NOT NULL DEFAULT 'existing'
            );

            -- DnevnikPromena operational columns used by SyncWorker and import pipeline
            ALTER TABLE IF EXISTS ""DnevnikPromena"" ADD COLUMN IF NOT EXISTS ""IDObjekat"" integer;
            ALTER TABLE IF EXISTS ""DnevnikPromena"" ADD COLUMN IF NOT EXISTS ""RedniBroj"" integer;
            CREATE INDEX IF NOT EXISTS ""IX_DnevnikPromena_DataOrigin"" ON ""DnevnikPromena"" (""DataOrigin"");
            CREATE INDEX IF NOT EXISTS ""IX_DnevnikPromena_IDObjekat_Datum"" ON ""DnevnikPromena"" (""IDObjekat"", ""Datum"");

            -- BOOTSTRAP_BATCH_BREAK
            -- Create DataImportBatches table if it doesn't exist (idempotent bootstrap)
            CREATE TABLE IF NOT EXISTS ""DataImportBatches"" (
                ""Id""              bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""SourceSystem""    character varying(64)   NOT NULL,
                ""SourceFileName""  character varying(300)  NOT NULL,
                ""StartedAtUtc""    timestamp with time zone NOT NULL,
                ""CompletedAtUtc""  timestamp with time zone,
                ""LastHeartbeatUtc"" timestamp with time zone,
                ""Status""          character varying(32)   NOT NULL,
                ""CurrentStep""     character varying(64),
                ""CurrentTable""    character varying(300),
                ""SummaryJson""     text,
                ""ErrorMessage""    character varying(4000),
                ""DurationSeconds"" integer,
                ""TotalImported""   integer NOT NULL DEFAULT 0,
                ""TotalUpdated""    integer NOT NULL DEFAULT 0,
                ""TotalErrors""     integer NOT NULL DEFAULT 0,
                ""DataOrigin""      character varying(32) NOT NULL DEFAULT 'access'
            );
            CREATE INDEX IF NOT EXISTS ""IX_DataImportBatches_StartedAtUtc"" ON ""DataImportBatches"" (""StartedAtUtc"");
            CREATE INDEX IF NOT EXISTS ""IX_DataImportBatches_Status"" ON ""DataImportBatches"" (""Status"");

            -- Access import batch compatibility columns (migration 015, idempotent)
            ALTER TABLE IF EXISTS ""DataImportBatches"" ADD COLUMN IF NOT EXISTS ""DurationSeconds"" integer;
            ALTER TABLE IF EXISTS ""DataImportBatches"" ADD COLUMN IF NOT EXISTS ""TotalImported"" integer NOT NULL DEFAULT 0;
            ALTER TABLE IF EXISTS ""DataImportBatches"" ADD COLUMN IF NOT EXISTS ""TotalUpdated"" integer NOT NULL DEFAULT 0;
            ALTER TABLE IF EXISTS ""DataImportBatches"" ADD COLUMN IF NOT EXISTS ""TotalErrors"" integer NOT NULL DEFAULT 0;
            ALTER TABLE IF EXISTS ""DataImportBatches"" ADD COLUMN IF NOT EXISTS ""DataOrigin"" character varying(32) NOT NULL DEFAULT 'access';
            ALTER TABLE IF EXISTS ""DataImportBatches"" ADD COLUMN IF NOT EXISTS ""LastHeartbeatUtc"" timestamp with time zone;
            ALTER TABLE IF EXISTS ""DataImportBatches"" ADD COLUMN IF NOT EXISTS ""CurrentStep"" character varying(64);
            ALTER TABLE IF EXISTS ""DataImportBatches"" ADD COLUMN IF NOT EXISTS ""CurrentTable"" character varying(300);
            CREATE INDEX IF NOT EXISTS ""IX_DataImportBatches_LastHeartbeatUtc"" ON ""DataImportBatches"" (""LastHeartbeatUtc"");

            -- TipoviObuce (idempotent bootstrap)
            CREATE TABLE IF NOT EXISTS ""TipoviObuce"" (
                ""Id""          integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""Naziv""       text NOT NULL DEFAULT '',
                ""DataOrigin""  character varying(32) NOT NULL DEFAULT 'existing'
            );

            -- Dobavljaci (idempotent bootstrap)
            CREATE TABLE IF NOT EXISTS ""Dobavljaci"" (
                ""Id""          integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""Naziv""       text,
                ""Adresa""      text,
                ""Telefon""     text,
                ""Napomena""    text,
                ""DataOrigin""  character varying(32) NOT NULL DEFAULT 'existing'
            );

            -- Sezone (idempotent bootstrap)
            CREATE TABLE IF NOT EXISTS ""Sezone"" (
                ""Id""          integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""Naziv""       character varying(100) NOT NULL,
                ""DatumOd""     timestamp with time zone NOT NULL,
                ""DatumDo""     timestamp with time zone NOT NULL,
                ""DataOrigin""  character varying(32) NOT NULL DEFAULT 'existing'
            );

            -- BOOTSTRAP_BATCH_BREAK
            -- ErrorRecords (idempotent bootstrap)
            CREATE TABLE IF NOT EXISTS ""ErrorRecords"" (
                ""Id""              integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""Timestamp""       timestamp with time zone NOT NULL,
                ""Level""           text NOT NULL DEFAULT 'Error',
                ""Message""         character varying(2000) NOT NULL DEFAULT '',
                ""ExceptionType""   character varying(500) NOT NULL DEFAULT '',
                ""StackTrace""      character varying(4000),
                ""Path""            character varying(1000),
                ""UserName""        character varying(200),
                ""ClientApp""       character varying(1000),
                ""CorrelationId""   text NOT NULL DEFAULT ''
            );

            -- ProductImages (idempotent bootstrap)
            CREATE TABLE IF NOT EXISTS ""ProductImages"" (
                ""Id""          uuid PRIMARY KEY,
                ""ProductId""   integer NOT NULL,
                ""FileName""    character varying(500) NOT NULL,
                ""CreatedAt""   timestamp with time zone NOT NULL,
                ""IsPrimary""   boolean NOT NULL DEFAULT false
            );
            CREATE INDEX IF NOT EXISTS ""IX_ProductImages_ProductId"" ON ""ProductImages"" (""ProductId"");
            CREATE INDEX IF NOT EXISTS ""IX_ProductImages_CreatedAt"" ON ""ProductImages"" (""CreatedAt"");

            -- CrossPlatformProducts (idempotent bootstrap)
            CREATE TABLE IF NOT EXISTS ""CrossPlatformProducts"" (
                ""Id""              integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""Brand""           text NOT NULL DEFAULT '',
                ""NormalizedName""  text NOT NULL DEFAULT '',
                ""ZalandoUrl""      text NOT NULL DEFAULT '',
                ""DeichmannUrl""    text NOT NULL DEFAULT '',
                ""PriceZalando""    numeric NOT NULL DEFAULT 0,
                ""PriceDeichmann""  numeric NOT NULL DEFAULT 0,
                ""CreatedAt""       timestamp with time zone NOT NULL,
                ""UpdatedAt""       timestamp with time zone NOT NULL
            );

            -- BOOTSTRAP_BATCH_BREAK
            -- povracaj_zaglavlje (idempotent bootstrap)
            CREATE TABLE IF NOT EXISTS povracaj_zaglavlje (
                id                 integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                broj_zapisnika     character varying(100) NOT NULL,
                datum_povracaja    timestamp with time zone NOT NULL,
                id_dobavljac       integer NOT NULL,
                razlog_povracaja   text,
                status             character varying(50) NOT NULL DEFAULT 'Kreiran',
                ukupan_iznos       numeric(18,2),
                komentar           text,
                kreirao_korisnik   character varying(200),
                odobrio_korisnik   character varying(200),
                datum_kreiranja    timestamp with time zone NOT NULL,
                datum_odobrenja    timestamp with time zone,
                data_origin        character varying(32) NOT NULL DEFAULT 'existing'
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_povracaj_zaglavlje_broj_zapisnika"" ON povracaj_zaglavlje (broj_zapisnika);
            CREATE INDEX IF NOT EXISTS ""IX_povracaj_zaglavlje_id_dobavljac"" ON povracaj_zaglavlje (id_dobavljac);
            CREATE INDEX IF NOT EXISTS ""IX_povracaj_zaglavlje_datum_povracaja"" ON povracaj_zaglavlje (datum_povracaja);

            -- povracaj_stavke (idempotent bootstrap)
            CREATE TABLE IF NOT EXISTS povracaj_stavke (
                id              integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                id_povracaj     integer NOT NULL REFERENCES povracaj_zaglavlje(id) ON DELETE CASCADE,
                id_artikal      integer NOT NULL,
                kolicina        integer NOT NULL,
                cena            numeric(18,2) NOT NULL,
                razlog          text,
                stanje_artikla  character varying(100)
            );
            CREATE INDEX IF NOT EXISTS ""IX_povracaj_stavke_id_artikal"" ON povracaj_stavke (id_artikal);

            -- AccessImportLog (idempotent bootstrap)
            CREATE TABLE IF NOT EXISTS ""AccessImportLog"" (
                ""Id""            bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""BatchId""       bigint NOT NULL,
                ""TableName""     character varying(128) NOT NULL,
                ""RowIndex""      integer NOT NULL DEFAULT 0,
                ""Severity""      character varying(16) NOT NULL DEFAULT 'info',
                ""Message""       character varying(2000) NOT NULL,
                ""SourceRowJson"" text,
                ""CreatedAtUtc""  timestamp with time zone NOT NULL DEFAULT NOW()
            );
            CREATE INDEX IF NOT EXISTS ""IX_AccessImportLog_BatchId"" ON ""AccessImportLog"" (""BatchId"");
            CREATE INDEX IF NOT EXISTS ""IX_AccessImportLog_Severity"" ON ""AccessImportLog"" (""Severity"");
            CREATE INDEX IF NOT EXISTS ""IX_AccessImportLog_BatchId_TableName"" ON ""AccessImportLog"" (""BatchId"", ""TableName"");
        ";

        var batches = sql
            .Split("-- BOOTSTRAP_BATCH_BREAK", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var i = 0; i < batches.Length; i++)
        {
            logger.LogInformation(
                "Executing Trendplus core bootstrap batch {BatchNumber}/{BatchCount}.",
                i + 1,
                batches.Length);

            await ExecuteBootstrapBatchAsync(
                connectionString,
                logger,
                $"trendplus-core-{i + 1}",
                batches[i],
                BootstrapCommandTimeoutSeconds);

        }

        // Re-run after bootstrap so old and fresh schemas end up aligned.
        await EnsureImportReferenceDataOriginColumnsAsync(connectionString, logger);

        logger.LogInformation("✔ Ensured Trendplus core schema for Artikli/DnevnikPromena/Prodaja columns.");
    }

    private static async Task EnsureImportReferenceDataOriginColumnsAsync(
        string connectionString,
        ILogger logger)
    {
        const string sql = @"
            ALTER TABLE IF EXISTS ""Dobavljaci""
                ADD COLUMN IF NOT EXISTS ""DataOrigin"" character varying(32) NOT NULL DEFAULT 'existing';

            ALTER TABLE IF EXISTS ""Sezone""
                ADD COLUMN IF NOT EXISTS ""DataOrigin"" character varying(32) NOT NULL DEFAULT 'existing';

            ALTER TABLE IF EXISTS ""TipoviObuce""
                ADD COLUMN IF NOT EXISTS ""DataOrigin"" character varying(32) NOT NULL DEFAULT 'existing';

            ALTER TABLE IF EXISTS povracaj_zaglavlje
                ADD COLUMN IF NOT EXISTS data_origin character varying(32) NOT NULL DEFAULT 'existing';

            -- DnevnikPromena compatibility columns (older DBs can miss post-2025 fields)
            ALTER TABLE IF EXISTS ""DnevnikPromena"" ADD COLUMN IF NOT EXISTS ""BrojRacuna"" character varying(100);
            ALTER TABLE IF EXISTS ""DnevnikPromena"" ADD COLUMN IF NOT EXISTS ""DobavljacId"" integer;
            ALTER TABLE IF EXISTS ""DnevnikPromena"" ADD COLUMN IF NOT EXISTS ""ArtikalId"" integer;
            ALTER TABLE IF EXISTS ""DnevnikPromena"" ADD COLUMN IF NOT EXISTS ""StaraProdajnaCena"" decimal(18,2);
            ALTER TABLE IF EXISTS ""DnevnikPromena"" ADD COLUMN IF NOT EXISTS ""NovaProdajnaCena"" decimal(18,2);
            ALTER TABLE IF EXISTS ""DnevnikPromena"" ADD COLUMN IF NOT EXISTS ""Kolicina"" integer;
            ALTER TABLE IF EXISTS ""DnevnikPromena"" ADD COLUMN IF NOT EXISTS ""IDObjekat"" integer;
            ALTER TABLE IF EXISTS ""DnevnikPromena"" ADD COLUMN IF NOT EXISTS ""RedniBroj"" integer;
            ALTER TABLE IF EXISTS ""DnevnikPromena"" ADD COLUMN IF NOT EXISTS ""Komentar"" character varying(500);
            ALTER TABLE IF EXISTS ""DnevnikPromena"" ADD COLUMN IF NOT EXISTS ""KorisnikIme"" character varying(200);
            ALTER TABLE IF EXISTS ""DnevnikPromena"" ADD COLUMN IF NOT EXISTS ""DataOrigin"" character varying(32) NOT NULL DEFAULT 'existing';

            -- Access import batch compatibility columns (added after initial 012 schema)
            ALTER TABLE IF EXISTS ""DataImportBatches"" ADD COLUMN IF NOT EXISTS ""DurationSeconds"" integer;
            ALTER TABLE IF EXISTS ""DataImportBatches"" ADD COLUMN IF NOT EXISTS ""TotalImported"" integer NOT NULL DEFAULT 0;
            ALTER TABLE IF EXISTS ""DataImportBatches"" ADD COLUMN IF NOT EXISTS ""TotalUpdated"" integer NOT NULL DEFAULT 0;
            ALTER TABLE IF EXISTS ""DataImportBatches"" ADD COLUMN IF NOT EXISTS ""TotalErrors"" integer NOT NULL DEFAULT 0;
            ALTER TABLE IF EXISTS ""DataImportBatches"" ADD COLUMN IF NOT EXISTS ""DataOrigin"" character varying(32) NOT NULL DEFAULT 'access';
            ALTER TABLE IF EXISTS ""DataImportBatches"" ADD COLUMN IF NOT EXISTS ""LastHeartbeatUtc"" timestamp with time zone;
            ALTER TABLE IF EXISTS ""DataImportBatches"" ADD COLUMN IF NOT EXISTS ""CurrentStep"" character varying(64);
            ALTER TABLE IF EXISTS ""DataImportBatches"" ADD COLUMN IF NOT EXISTS ""CurrentTable"" character varying(300);
            CREATE INDEX IF NOT EXISTS ""IX_DataImportBatches_LastHeartbeatUtc"" ON ""DataImportBatches"" (""LastHeartbeatUtc"");

            CREATE INDEX IF NOT EXISTS ""IX_Dobavljaci_DataOrigin"" ON ""Dobavljaci"" (""DataOrigin"");
            CREATE INDEX IF NOT EXISTS ""IX_Sezone_DataOrigin"" ON ""Sezone"" (""DataOrigin"");
            CREATE INDEX IF NOT EXISTS ""IX_TipoviObuce_DataOrigin"" ON ""TipoviObuce"" (""DataOrigin"");
            CREATE INDEX IF NOT EXISTS ""IX_povracaj_zaglavlje_data_origin"" ON povracaj_zaglavlje (data_origin);
            CREATE INDEX IF NOT EXISTS ""IX_DnevnikPromena_DataOrigin"" ON ""DnevnikPromena"" (""DataOrigin"");
            CREATE INDEX IF NOT EXISTS ""IX_DnevnikPromena_IDObjekat_Datum"" ON ""DnevnikPromena"" (""IDObjekat"", ""Datum"");
            CREATE INDEX IF NOT EXISTS ""IX_DataImportBatches_StartedAtUtc"" ON ""DataImportBatches"" (""StartedAtUtc"");
            CREATE INDEX IF NOT EXISTS ""IX_DataImportBatches_LastHeartbeatUtc"" ON ""DataImportBatches"" (""LastHeartbeatUtc"");
            CREATE INDEX IF NOT EXISTS ""IX_DataImportBatches_Status"" ON ""DataImportBatches"" (""Status"");
        ";

        await ExecuteSqlCommandAsync(connectionString, sql, logger);
        logger.LogInformation("Ensured import compatibility columns for reference/master tables and batch history.");
    }

    private static async Task EnsureTrendplusAggregationTablesAsync(
        string connectionString,
        ILogger logger)
    {
        const string sql = @"
            -- Source table indexes used by analytics aggregation worker
            DO $$
            BEGIN
                IF to_regclass('public.prodaja_zaglavlje') IS NOT NULL
                   AND EXISTS (
                       SELECT 1 FROM information_schema.columns
                       WHERE table_schema = 'public' AND table_name = 'prodaja_zaglavlje' AND column_name = 'datum_prodaje'
                   )
                THEN
                    EXECUTE 'CREATE INDEX IF NOT EXISTS idx_prodaja_zaglavlje_datum_prodaje ON prodaja_zaglavlje (datum_prodaje DESC)';
                END IF;
            END $$;

            DO $$
            BEGIN
                IF to_regclass('public.prodaja_stavke') IS NOT NULL
                   AND EXISTS (
                       SELECT 1 FROM information_schema.columns
                       WHERE table_schema = 'public' AND table_name = 'prodaja_stavke' AND column_name = 'id_prodaja'
                   )
                THEN
                    EXECUTE 'CREATE INDEX IF NOT EXISTS idx_prodaja_stavke_id_prodaja ON prodaja_stavke (id_prodaja)';
                END IF;
            END $$;

            DO $$
            BEGIN
                IF to_regclass('public.prodaja_stavke') IS NOT NULL
                   AND EXISTS (
                       SELECT 1 FROM information_schema.columns
                       WHERE table_schema = 'public' AND table_name = 'prodaja_stavke' AND column_name = 'id_artikal'
                   )
                THEN
                    EXECUTE 'CREATE INDEX IF NOT EXISTS idx_prodaja_stavke_id_artikal ON prodaja_stavke (id_artikal)';
                END IF;
            END $$;

            -- Pre-aggregated analytics cache tables
            CREATE TABLE IF NOT EXISTS ""AnalyticsDailySummary"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""Date"" DATE NOT NULL UNIQUE,
                ""TotalRevenue"" DECIMAL(18,2) NOT NULL DEFAULT 0,
                ""TotalTransactions"" INT NOT NULL DEFAULT 0,
                ""TotalUnits"" INT NOT NULL DEFAULT 0,
                ""AvgBasketValue"" DECIMAL(18,2) NOT NULL DEFAULT 0,
                ""AvgItemPrice"" DECIMAL(18,2) NOT NULL DEFAULT 0,
                ""BasketStdDev"" DECIMAL(18,2) NOT NULL DEFAULT 0,
                ""ItemPriceStdDev"" DECIMAL(18,2) NOT NULL DEFAULT 0,
                ""EffectiveTransactionCount"" DECIMAL(18,2) NOT NULL DEFAULT 0,
                ""DataConfidence"" DECIMAL(18,2) NOT NULL DEFAULT 0,
                ""UpdatedAt"" TIMESTAMP NOT NULL DEFAULT NOW()
            );
            ALTER TABLE IF EXISTS ""AnalyticsDailySummary"" ADD COLUMN IF NOT EXISTS ""BasketStdDev"" DECIMAL(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE IF EXISTS ""AnalyticsDailySummary"" ADD COLUMN IF NOT EXISTS ""ItemPriceStdDev"" DECIMAL(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE IF EXISTS ""AnalyticsDailySummary"" ADD COLUMN IF NOT EXISTS ""EffectiveTransactionCount"" DECIMAL(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE IF EXISTS ""AnalyticsDailySummary"" ADD COLUMN IF NOT EXISTS ""DataConfidence"" DECIMAL(18,2) NOT NULL DEFAULT 0;
            CREATE INDEX IF NOT EXISTS idx_daily_summary_date ON ""AnalyticsDailySummary"" (""Date"" DESC);

            CREATE TABLE IF NOT EXISTS ""AnalyticsCategorySummary"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""Date"" DATE NOT NULL,
                ""Kategorija"" VARCHAR(100) NOT NULL,
                ""TotalRevenue"" DECIMAL(18,2) NOT NULL DEFAULT 0,
                ""TotalUnits"" INT NOT NULL DEFAULT 0,
                ""TransactionCount"" INT NOT NULL DEFAULT 0,
                ""UpdatedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
                UNIQUE(""Date"", ""Kategorija"")
            );
            CREATE INDEX IF NOT EXISTS idx_category_summary_date ON ""AnalyticsCategorySummary"" (""Date"" DESC);

            CREATE TABLE IF NOT EXISTS ""AnalyticsSupplierSummary"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""Date"" DATE NOT NULL,
                ""DobavljacId"" INT,
                ""DobavljacNaziv"" VARCHAR(200),
                ""TotalRevenue"" DECIMAL(18,2) NOT NULL DEFAULT 0,
                ""TotalUnits"" INT NOT NULL DEFAULT 0,
                ""TransactionCount"" INT NOT NULL DEFAULT 0,
                ""UpdatedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
                UNIQUE(""Date"", ""DobavljacId"")
            );
            CREATE INDEX IF NOT EXISTS idx_supplier_summary_date ON ""AnalyticsSupplierSummary"" (""Date"" DESC);

            CREATE TABLE IF NOT EXISTS ""AnalyticsGenderSummary"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""Date"" DATE NOT NULL,
                ""Pol"" VARCHAR(50) NOT NULL,
                ""TotalRevenue"" DECIMAL(18,2) NOT NULL DEFAULT 0,
                ""TotalUnits"" INT NOT NULL DEFAULT 0,
                ""UpdatedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
                UNIQUE(""Date"", ""Pol"")
            );
            CREATE INDEX IF NOT EXISTS idx_gender_summary_date ON ""AnalyticsGenderSummary"" (""Date"" DESC);

            CREATE TABLE IF NOT EXISTS ""AnalyticsTopProducts"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""Date"" DATE NOT NULL,
                ""ProductId"" INT NOT NULL,
                ""ProductName"" VARCHAR(300),
                ""TotalRevenue"" DECIMAL(18,2) NOT NULL DEFAULT 0,
                ""TotalUnits"" INT NOT NULL DEFAULT 0,
                ""Rank"" INT NOT NULL DEFAULT 0,
                ""UpdatedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
                UNIQUE(""Date"", ""ProductId"")
            );
            CREATE INDEX IF NOT EXISTS idx_top_products_date ON ""AnalyticsTopProducts"" (""Date"" DESC);
            CREATE INDEX IF NOT EXISTS idx_top_products_rank ON ""AnalyticsTopProducts"" (""Date"", ""Rank"");
        ";

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = BootstrapCommandTimeoutSeconds;
        await command.ExecuteNonQueryAsync();

        logger.LogInformation("✔ Ensured Trendplus analytics aggregation tables/indexes.");
    }

    // Outbox table koja ti trenutno fali (42P01: relation "OutboxMessages" does not exist)
    private static async Task EnsureTrendplusOutboxSchemaAsync(
        string connectionString,
        ILogger logger)
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS ""OutboxMessages"" (
                ""Id"" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""CorrelationId"" character varying(100) NULL,
                ""EventType"" character varying(200) NOT NULL,
                ""Payload"" text NOT NULL,
                ""IsProcessed"" boolean NOT NULL DEFAULT false,
                ""RetryCount"" integer NOT NULL DEFAULT 0,
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""ProcessedAt"" timestamp with time zone NULL,
                ""ErrorMessage"" character varying(2000) NULL
            );

            -- Fix existing installations where CorrelationId was created as uuid
            DO $fix_outbox$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'OutboxMessages'
                      AND column_name = 'CorrelationId'
                      AND data_type = 'uuid'
                ) THEN
                    ALTER TABLE ""OutboxMessages""
                        ALTER COLUMN ""CorrelationId"" TYPE character varying(100)
                        USING ""CorrelationId""::text;
                END IF;
            END $fix_outbox$;

            CREATE INDEX IF NOT EXISTS ""IX_OutboxMessages_IsProcessed_RetryCount_CreatedAt""
                ON ""OutboxMessages"" (""IsProcessed"", ""RetryCount"", ""CreatedAt"");
            CREATE INDEX IF NOT EXISTS ""IX_OutboxMessages_CreatedAt""
                ON ""OutboxMessages"" (""CreatedAt"");
            CREATE INDEX IF NOT EXISTS ""IX_OutboxMessages_IsProcessed""
                ON ""OutboxMessages"" (""IsProcessed"");
        ";

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = BootstrapCommandTimeoutSeconds;
        await command.ExecuteNonQueryAsync();

        logger.LogInformation("✔ Ensured OutboxMessages table exists.");
    }

    private static async Task EnsureTrendplusDocumentSchemaAsync(
        string connectionString,
        ILogger logger)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS "DocumentTemplates" (
                "Id" uuid PRIMARY KEY,
                "Name" character varying(200) NOT NULL,
                "Version" integer NOT NULL,
                "Type" character varying(100) NOT NULL,
                "Locale" character varying(16) NOT NULL,
                "Content" text NOT NULL,
                "HeaderContent" text NULL,
                "FooterContent" text NULL,
                "IsActive" boolean NOT NULL DEFAULT true,
                "CreatedByUserId" character varying(200) NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW()
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_DocumentTemplates_Name_Version"
                ON "DocumentTemplates" ("Name", "Version");
            CREATE INDEX IF NOT EXISTS "IX_DocumentTemplates_Type_IsActive"
                ON "DocumentTemplates" ("Type", "IsActive");

            CREATE TABLE IF NOT EXISTS "Documents" (
                "Id" uuid PRIMARY KEY,
                "BatchId" uuid NULL,
                "TemplateId" uuid NULL,
                "TemplateVersion" integer NOT NULL DEFAULT 0,
                "TemplateName" character varying(200) NOT NULL,
                "DocumentType" character varying(100) NOT NULL,
                "TableKey" character varying(200) NOT NULL,
                "TableTitle" character varying(300) NOT NULL,
                "Format" character varying(32) NOT NULL,
                "Orientation" character varying(32) NOT NULL,
                "Status" character varying(32) NOT NULL,
                "RequestedByUserId" character varying(200) NOT NULL,
                "RequestedByUserName" character varying(200) NOT NULL,
                "RequestedByRoles" character varying(1000) NULL,
                "Locale" character varying(16) NULL,
                "IncludeFiltersAndMetadata" boolean NOT NULL DEFAULT true,
                "IsPreview" boolean NOT NULL DEFAULT false,
                "IsAsync" boolean NOT NULL DEFAULT false,
                "RowCount" integer NOT NULL DEFAULT 0,
                "FiltersJson" text NULL,
                "MetadataJson" text NULL,
                "RequestJson" text NOT NULL,
                "MimeType" character varying(150) NULL,
                "FileName" character varying(260) NULL,
                "StoragePath" character varying(500) NULL,
                "FileUrl" character varying(1000) NULL,
                "SizeBytes" bigint NULL,
                "Sha256" character varying(128) NULL,
                "ErrorMessage" character varying(4000) NULL,
                "RetryCount" integer NOT NULL DEFAULT 0,
                "CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW(),
                "UpdatedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW(),
                "StartedAtUtc" timestamp with time zone NULL,
                "CompletedAtUtc" timestamp with time zone NULL,
                "NextAttemptAtUtc" timestamp with time zone NULL,
                "ExpiresAtUtc" timestamp with time zone NULL
            );

            CREATE INDEX IF NOT EXISTS "IX_Documents_Status" ON "Documents" ("Status");
            CREATE INDEX IF NOT EXISTS "IX_Documents_CreatedAtUtc" ON "Documents" ("CreatedAtUtc");
            CREATE INDEX IF NOT EXISTS "IX_Documents_Status_NextAttemptAtUtc"
                ON "Documents" ("Status", "NextAttemptAtUtc");
            CREATE INDEX IF NOT EXISTS "IX_Documents_BatchId" ON "Documents" ("BatchId");
            CREATE INDEX IF NOT EXISTS "IX_Documents_RequestedByUserId" ON "Documents" ("RequestedByUserId");

            CREATE TABLE IF NOT EXISTS "DocumentAudits" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "DocumentId" uuid NOT NULL,
                "Action" character varying(64) NOT NULL,
                "UserId" character varying(200) NOT NULL,
                "UserName" character varying(200) NOT NULL,
                "Roles" character varying(1000) NULL,
                "IpAddress" character varying(128) NULL,
                "UserAgent" character varying(1024) NULL,
                "DetailsJson" text NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW()
            );

            CREATE INDEX IF NOT EXISTS "IX_DocumentAudits_DocumentId_CreatedAtUtc"
                ON "DocumentAudits" ("DocumentId", "CreatedAtUtc");

            INSERT INTO "DocumentTemplates"
                ("Id", "Name", "Version", "Type", "Locale", "Content", "HeaderContent", "FooterContent", "IsActive", "CreatedByUserId", "CreatedAtUtc")
            VALUES
                (
                    '65f367aa-4206-4b7e-b7d2-7d8ef7351111',
                    'analytics-table-default',
                    1,
                    'analytics-table-report',
                    'sr-RS',
                    '<!DOCTYPE html><html><head><meta charset="utf-8" /><title>{{title}}</title><style>{{styles}}</style></head><body class="doc {{orientation}}"><div class="sheet"><header>{{header}}</header><section class="meta"><div><strong>Izvestaj:</strong> {{title}}</div><div><strong>Generisano:</strong> {{generated_at}}</div><div><strong>Korisnik:</strong> {{requested_by}}</div></section><section class="filters"><h3>Filteri</h3>{{filters}}</section><section class="metadata"><h3>Metapodaci</h3>{{metadata}}</section><section class="table-section">{{table}}</section><footer>{{footer}}</footer></div></body></html>',
                    '<div class="doc-header"><h1>{{title}}</h1><p>Trendplus Analytics Export</p></div>',
                    '<div class="doc-footer"><span>Template v{{template_version}}</span><span>{{table_key}}</span></div>',
                    true,
                    'system',
                    NOW()
                ),
                (
                    '65f367aa-4206-4b7e-b7d2-7d8ef7352222',
                    'executive-summary-default',
                    1,
                    'executive-summary',
                    'sr-RS',
                    '<!DOCTYPE html><html><head><meta charset="utf-8" /><title>{{title}}</title><style>{{styles}}</style></head><body class="doc portrait"><div class="sheet"><header>{{header}}</header><section class="table-section">{{table}}</section><footer>{{footer}}</footer></div></body></html>',
                    '<div class="doc-header"><h1>{{title}}</h1></div>',
                    '<div class="doc-footer">Trendplus Executive Summary</div>',
                    true,
                    'system',
                    NOW()
                ),
                (
                    '65f367aa-4206-4b7e-b7d2-7d8ef7353333',
                    'receipt-default',
                    1,
                    'receipt',
                    'sr-RS',
                    '<!DOCTYPE html><html><head><meta charset="utf-8" /><title>{{title}}</title><style>{{styles}}</style></head><body class="doc portrait thermal"><div class="sheet">{{table}}</div></body></html>',
                    NULL,
                    NULL,
                    true,
                    'system',
                    NOW()
                ),
                (
                    '65f367aa-4206-4b7e-b7d2-7d8ef7354444',
                    'label-default',
                    1,
                    'label',
                    'sr-RS',
                    '<!DOCTYPE html><html><head><meta charset="utf-8" /><title>{{title}}</title><style>{{styles}}</style></head><body class="doc portrait label"><div class="sheet">{{table}}</div></body></html>',
                    NULL,
                    NULL,
                    true,
                    'system',
                    NOW()
                )
            ON CONFLICT ("Name", "Version") DO NOTHING;
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = BootstrapCommandTimeoutSeconds;
        await command.ExecuteNonQueryAsync();

        logger.LogInformation("Ensured document export tables and default templates exist.");
    }

    private static async Task ExecuteBootstrapBatchAsync(
        string connectionString,
        ILogger logger,
        string batchName,
        string sql,
        int timeoutSeconds)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = timeoutSeconds;
        await command.ExecuteNonQueryAsync();

        stopwatch.Stop();
        logger.LogInformation("Bootstrap batch {BatchName} completed in {ElapsedMs}ms.", batchName, stopwatch.ElapsedMilliseconds);
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
        var connectionString = GetValidatedConnectionString(configuration, "AnalyticsConnection", logger);
        var defaultConnectionString = GetValidatedConnectionString(configuration, "DefaultConnection", logger);
        var unifiedDb = AreSameDatabase(defaultConnectionString, connectionString);

        try
        {
            await context.Database.MigrateAsync();
            logger.LogInformation("? Analytics DB migrations applied.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Analytics DB migrations failed; continuing with core analytics table self-heal.");
        }

        await EnsureCoreAnalyticsDimensionTablesAsync(connectionString, logger);

        // Check if we need to create tables
        if (!await TableExistsAsync(connectionString, "SalesFacts", logger))
        {
            logger.LogInformation("SalesFacts table not found, creating...");
            await ExecuteSqlFileAsync(connectionString, "Database/Analytics/001_CreateSalesFactTables.sql", logger);
        }

        if (!await TableExistsAsync(connectionString, "items", logger))
        {
            logger.LogInformation("Scraper scoring tables not found, creating...");
            await ExecuteSqlFileAsync(connectionString, "Database/Analytics/004_AddScraperScoringTables.sql", logger);
        }

        if (!await TableExistsAsync(connectionString, "PerformanceLogs", logger))
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

        // External shopping/trends tables (idempotent SQL scripts)
        if (!await TableExistsAsync(connectionString, "amazon_shoe_products", logger))
        {
            logger.LogInformation("amazon_shoe_products table not found, creating...");
            await ExecuteSqlFileAsync(connectionString, "Database/Analytics/006_AddAmazonShoesTable.sql", logger);
        }

        if (!await TableExistsAsync(connectionString, "ebay_shoe_products", logger))
        {
            logger.LogInformation("ebay_shoe_products table not found, creating...");
            await ExecuteSqlFileAsync(connectionString, "Database/Analytics/007_AddEbayShoesTable.sql", logger);
        }

        if (!await TableExistsAsync(connectionString, "google_shopping_products", logger))
        {
            logger.LogInformation("google_shopping_products table not found, creating...");
            await ExecuteSqlFileAsync(connectionString, "Database/Analytics/010_AddGoogleShoppingTable.sql", logger);
        }

        // Access-import origin support patch (idempotent)
        await ExecuteSqlFileAsync(connectionString, "Database/Analytics/011_AddDataOriginColumns.sql", logger);

        // 013 creates compatibility views that shadow trendplus operational tables ("Artikli", "Dobavljaci", etc.)
        // and expects DataOrigin to already exist on analytics fact/dimension tables.
        // Run 011 first so legacy analytics DBs are patched before those views are created.
        if (!unifiedDb)
        {
            await ExecuteSqlFileAsync(connectionString, "Database/Analytics/013_AddSupplierDecisionCompatibilitySchema.sql", logger);
        }
        else
        {
            logger.LogInformation("Skipping 013_AddSupplierDecisionCompatibilitySchema.sql: analytics and trendplus share the same database (compatibility views not needed).");
        }

        // Open product training schema (stored in analytics DB by default)
        if (!await TableExistsAsync(connectionString, "dataset", logger))
        {
            logger.LogInformation("open_product_training schema not found, creating base tables...");
            await ExecuteSqlFileAsync(connectionString, "Database/OpenProductTraining/001_create_schema.sql", logger);
        }

        if (!await TableExistsAsync(connectionString, "product_split", logger))
        {
            logger.LogInformation("open_product_training split table not found, applying split enum/table patch...");
            await ExecuteSqlFileAsync(connectionString, "Database/OpenProductTraining/002_fix_enum.sql", logger);
        }

        // Views are idempotent (CREATE OR REPLACE) and can be safely re-applied on startup.
        await ExecuteSqlFileAsync(connectionString, "Database/OpenProductTraining/003_add_ml_export_views.sql", logger);

        // Open Product Training 2.0 schema extensions + feature-store views (idempotent).
        await ExecuteSqlFileAsync(connectionString, "Database/OpenProductTraining/004_open_training_2_0.sql", logger);

        await ExecuteSqlFileAsync(connectionString, "Database/OpenProductTraining/005_open_training_2_0_views.sql", logger);

        await ExecuteSqlFileAsync(
            connectionString,
            "Database/Analytics/016_AddScraperScoringSearchIndexes.sql",
            logger,
            commandTimeoutSeconds: 0,
            useTransaction: false);

        await ExecuteSqlFileAsync(
            connectionString,
            "Database/Analytics/017_AddAnalyticsPerformanceIndexes.sql",
            logger,
            commandTimeoutSeconds: 0,
            useTransaction: false);

        await ExecuteSqlFileAsync(connectionString, "Database/OpenProductTraining/006_runtime_priors_materialized.sql", logger);

        await ExecuteSqlFileAsync(connectionString, "Database/OpenProductTraining/007_model_version_runtime_tuning.sql", logger);

        await EnsureOpenProductTrainingDatasetsAsync(connectionString, configuration, logger);

        // Backward-compatible schema patch for older eBay table script versions.
        await context.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE IF EXISTS ebay_shoe_products
                ADD COLUMN IF NOT EXISTS ""Gender"" TEXT;

            ALTER TABLE IF EXISTS ebay_shoe_products
                ADD COLUMN IF NOT EXISTS ""TrendScore"" REAL NOT NULL DEFAULT 0;
        ");

        logger.LogInformation("? Analytics DB initialized");

        // Backfill historical sales / returns before deriving supplier-decision analytics views.
        await BackfillSalesFactsAsync(trendDb, context, logger);
        await BackfillReturnFactsAsync(trendDb, context, logger);

        await ExecuteSqlFileAsync(connectionString, "Database/Analytics/003_AddGlobalTrendsTables.sql", logger);
        await ExecuteSqlFileAsync(connectionString, "Database/Migrations/017_CreateNightlyAnalyticsMaterializedViews.sql", logger);
        // 014 creates nivelacija views (DROP ... CASCADE + CREATE OR REPLACE VIEW).
        // When analytics and trendplus share the same DB the 018 background task is already
        // running DDL on the same relations, causing a 55P03 lock timeout.
        // In that case defer 014 to the start of the 018 bg task where it runs before
        // any heavy MV work (the bg task handles the unified case explicitly).
        if (!unifiedDb)
        {
            await ExecuteSqlFileAsync(connectionString, "Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql", logger);
        }
        await ExecuteSqlFileAsync(connectionString, "Database/Migrations/016_AnalyticsNivelacijaEnhancements.sql", logger);
        await ExecuteSqlFileAsync(connectionString, "Database/Analytics/Intelligence/020_create_intelligence_schema.sql", logger);
        // 018_AddSupplierDecisionHubViews.sql is NOT run on the analytics DB — it references
        // trendplus-specific tables and is executed asynchronously on the trendplus DB.
        // 015_AddSupplierMlRanking.sql is also deferred: it depends on vw_supplier_fullprice_signals
        // which 018 creates. Both are run sequentially in the 018 background task in
        // InitializeTrendplusDbAsync (only when analytics and trendplus share the same DB).

        var analyticsIntelligenceScripts = new (string SqlFilePath, string[] RequiredRelations)[]
        {
            (
                "Database/Analytics/Intelligence/021_product_demand_signals_v1.sql",
                ["analytics_intel.vw_product_demand_signals_v1", "analytics_intel.mv_product_demand_signals_v1_cache"]
            ),
            (
                "Database/Analytics/Intelligence/022_inventory_risk_signals_v1.sql",
                ["analytics_intel.vw_inventory_risk_signals_v1", "analytics_intel.mv_inventory_risk_signals_v1_cache"]
            ),
            (
                "Database/Analytics/Intelligence/023_price_intelligence_v1.sql",
                ["analytics_intel.vw_price_intelligence_v1", "analytics_intel.mv_price_intelligence_v1_cache"]
            ),
            (
                "Database/Analytics/Intelligence/024_trend_momentum_v1.sql",
                ["analytics_intel.vw_trend_momentum_v1", "analytics_intel.mv_trend_momentum_v1_cache"]
            )
        };

        var bgAnalyticsIntelligenceConnectionString = connectionString;
        var bgAnalyticsIntelligenceLogger = logger;
        _ = Task.Run(async () =>
        {
            await using var lockConnection = new NpgsqlConnection(bgAnalyticsIntelligenceConnectionString);
            try
            {
                await lockConnection.OpenAsync();

                if (!await TryAcquireSingleRunAdvisoryLockAsync(lockConnection, AnalyticsIntelligenceBuildLockKey))
                {
                    bgAnalyticsIntelligenceLogger.LogInformation("[BG] Skipping analytics intelligence SQL build because another instance already holds the build lock.");
                    return;
                }

                foreach (var script in analyticsIntelligenceScripts)
                {
                    var relationsReady = await AreRelationsReadyAsync(
                        bgAnalyticsIntelligenceConnectionString,
                        script.RequiredRelations);

                    if (!relationsReady)
                    {
                        bgAnalyticsIntelligenceLogger.LogInformation(
                            "[BG] Analytics intelligence relations missing for {SqlFile}. Forcing re-execution.",
                            script.SqlFilePath);
                        await DeleteAppliedStartupSqlHistoryAsync(
                            bgAnalyticsIntelligenceConnectionString,
                            script.SqlFilePath);
                    }

                    await ExecuteSqlFileAsync(
                        bgAnalyticsIntelligenceConnectionString,
                        script.SqlFilePath,
                        bgAnalyticsIntelligenceLogger,
                        commandTimeoutSeconds: 0,
                        useTransaction: false);
                }

                bgAnalyticsIntelligenceLogger.LogInformation("[BG] Analytics intelligence SQL build completed successfully.");
            }
            catch (Exception ex)
            {
                bgAnalyticsIntelligenceLogger.LogWarning(ex, "[BG] Analytics intelligence SQL build failed. Intelligence views may be partially unavailable until next startup.");
            }
            finally
            {
                if (lockConnection.State == System.Data.ConnectionState.Open)
                {
                    await ReleaseSingleRunAdvisoryLockAsync(lockConnection, AnalyticsIntelligenceBuildLockKey);
                }
            }
        });
    }

    private static async Task EnsureCoreAnalyticsDimensionTablesAsync(
        string connectionString,
        ILogger logger)
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS ""ProductsDim"" (
                ""ProductKey"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""ProductId"" integer NOT NULL,
                ""ProductName"" text NOT NULL DEFAULT '',
                ""Category"" text NOT NULL DEFAULT '',
                ""SubCategory"" text NOT NULL DEFAULT '',
                ""Brand"" text NOT NULL DEFAULT '',
                ""Velicina"" character varying(50),
                ""Boja"" character varying(100),
                ""Materijal"" character varying(100),
                ""FootwearTypeId"" integer,
                ""SupplierId"" integer,
                ""SeasonId"" integer,
                ""PurchasePrice"" numeric,
                ""PurchasePriceRsd"" numeric,
                ""FirstSalePrice"" numeric,
                ""SalePrice"" numeric,
                ""IsActive"" boolean NOT NULL DEFAULT true,
                ""Timestamp"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""Kolicina"" integer,
                ""DataOrigin"" character varying(32) NOT NULL DEFAULT 'existing'
            );

            CREATE INDEX IF NOT EXISTS ""IX_ProductsDim_ProductId"" ON ""ProductsDim"" (""ProductId"");
            CREATE INDEX IF NOT EXISTS ""IX_ProductsDim_Timestamp"" ON ""ProductsDim"" (""Timestamp"");
            CREATE INDEX IF NOT EXISTS ""IX_ProductsDim_Velicina"" ON ""ProductsDim"" (""Velicina"");
            CREATE INDEX IF NOT EXISTS ""IX_ProductsDim_Boja"" ON ""ProductsDim"" (""Boja"");

            CREATE TABLE IF NOT EXISTS ""StoresDim"" (
                ""StoreKey"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""StoreId"" integer NOT NULL,
                ""StoreName"" text NOT NULL DEFAULT '',
                ""City"" text,
                ""Region"" text
            );

            -- Extend existing tables with new analytics columns
            ALTER TABLE IF EXISTS ""ProductsDim"" ADD COLUMN IF NOT EXISTS ""PLU"" character varying(100);
            ALTER TABLE IF EXISTS ""ProductsDim"" ADD COLUMN IF NOT EXISTS ""MinimalnaKolicina"" integer;
            ALTER TABLE IF EXISTS ""StoresDim"" ADD COLUMN IF NOT EXISTS ""Telefon"" character varying(50);
            ALTER TABLE IF EXISTS ""StoresDim"" ADD COLUMN IF NOT EXISTS ""Menedzer"" character varying(200);
            ALTER TABLE IF EXISTS ""StoresDim"" ADD COLUMN IF NOT EXISTS ""DataOrigin"" character varying(32) NOT NULL DEFAULT 'existing';

            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_StoresDim_StoreId"" ON ""StoresDim"" (""StoreId"");
            CREATE INDEX IF NOT EXISTS ""IX_StoresDim_DataOrigin"" ON ""StoresDim"" (""DataOrigin"");

            ALTER TABLE IF EXISTS ""SalesLineFacts"" ADD COLUMN IF NOT EXISTS ""NabavnaCena"" numeric(18,2);

            -- Supplier dimension
            CREATE TABLE IF NOT EXISTS ""SuppliersDim"" (
                ""SupplierKey"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""SupplierId"" integer NOT NULL,
                ""Naziv"" character varying(300) NOT NULL DEFAULT '',
                ""Adresa"" character varying(500),
                ""Telefon"" character varying(50),
                ""Napomena"" character varying(1000),
                ""DataOrigin"" character varying(32) NOT NULL DEFAULT 'existing',
                ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT NOW()
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_SuppliersDim_SupplierId"" ON ""SuppliersDim"" (""SupplierId"");

            -- Season dimension
            CREATE TABLE IF NOT EXISTS ""SeasonsDim"" (
                ""SeasonKey"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""SeasonId"" integer NOT NULL,
                ""Naziv"" character varying(200) NOT NULL DEFAULT '',
                ""DatumOd"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""DatumDo"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ""DataOrigin"" character varying(32) NOT NULL DEFAULT 'existing',
                ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT NOW()
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_SeasonsDim_SeasonId"" ON ""SeasonsDim"" (""SeasonId"");

            -- Footwear type dimension
            CREATE TABLE IF NOT EXISTS ""FootwearTypesDim"" (
                ""TypeKey"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""TypeId"" integer NOT NULL,
                ""Naziv"" character varying(200) NOT NULL DEFAULT '',
                ""DataOrigin"" character varying(32) NOT NULL DEFAULT 'existing',
                ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT NOW()
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_FootwearTypesDim_TypeId"" ON ""FootwearTypesDim"" (""TypeId"");

            -- Inventory movement fact table
            CREATE TABLE IF NOT EXISTS ""InventoryMovementFacts"" (
                ""Id"" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""SourceId"" integer NOT NULL,
                ""TipPromene"" character varying(100) NOT NULL DEFAULT '',
                ""Datum"" timestamp with time zone NOT NULL,
                ""ArtikalId"" integer,
                ""Kolicina"" integer,
                ""StaraProdajnaCena"" numeric(18,2),
                ""NovaProdajnaCena"" numeric(18,2),
                ""Iznos"" numeric(18,2) NOT NULL DEFAULT 0,
                ""StoreId"" integer,
                ""DobavljacId"" integer,
                ""BrojDokumenta"" character varying(100),
                ""KorisnikIme"" character varying(200),
                ""DataOrigin"" character varying(32) NOT NULL DEFAULT 'existing'
            );
            CREATE INDEX IF NOT EXISTS ""IX_InventoryMovementFacts_Datum"" ON ""InventoryMovementFacts"" (""Datum"" DESC);
            CREATE INDEX IF NOT EXISTS ""IX_InventoryMovementFacts_ArtikalId"" ON ""InventoryMovementFacts"" (""ArtikalId"");
            CREATE INDEX IF NOT EXISTS ""IX_InventoryMovementFacts_TipPromene"" ON ""InventoryMovementFacts"" (""TipPromene"");
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_InventoryMovementFacts_SourceId"" ON ""InventoryMovementFacts"" (""SourceId"", ""DataOrigin"");

            -- Return facts (created here to ensure availability regardless of 013 schema file execution)
            CREATE TABLE IF NOT EXISTS ""ReturnFacts"" (
                ""Id""                   bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""SourceLineId""         integer NOT NULL,
                ""ReturnId""             integer NOT NULL,
                ""ProductId""            integer NOT NULL,
                ""SupplierId""           integer NOT NULL,
                ""Qty""                  integer NOT NULL,
                ""UnitCost""             numeric(18,2) NOT NULL DEFAULT 0,
                ""LineAmount""           numeric(18,2) NOT NULL DEFAULT 0,
                ""ReturnTimestampUtc""   timestamp with time zone NOT NULL,
                ""Status""              character varying(100) NOT NULL DEFAULT '',
                ""HeaderReason""         character varying(500),
                ""LineReason""            character varying(500),
                ""ItemCondition""        character varying(200),
                ""BrojZapisnika""        character varying(100),
                ""DataOrigin""           character varying(32) NOT NULL DEFAULT 'existing'
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ReturnFacts_SourceLineId"" ON ""ReturnFacts"" (""SourceLineId"");
            CREATE INDEX IF NOT EXISTS ""IX_ReturnFacts_ReturnId"" ON ""ReturnFacts"" (""ReturnId"");
            CREATE INDEX IF NOT EXISTS ""IX_ReturnFacts_ProductId"" ON ""ReturnFacts"" (""ProductId"");
            CREATE INDEX IF NOT EXISTS ""IX_ReturnFacts_SupplierId"" ON ""ReturnFacts"" (""SupplierId"");
            CREATE INDEX IF NOT EXISTS ""IX_ReturnFacts_ReturnTimestampUtc"" ON ""ReturnFacts"" (""ReturnTimestampUtc"");
            CREATE INDEX IF NOT EXISTS ""IX_ReturnFacts_SupplierId_ReturnTimestampUtc"" ON ""ReturnFacts"" (""SupplierId"", ""ReturnTimestampUtc"");
        ";

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = BootstrapCommandTimeoutSeconds;
        await command.ExecuteNonQueryAsync();

        logger.LogInformation("✔ Ensured core analytics dimension tables (ProductsDim, StoresDim, etc.).");
    }

    private static async Task BackfillSalesFactsAsync(
        TrendplusDbContext trendDb,
        AnalyticsDbContext analyticsDb,
        ILogger logger)
    {
        var lastId = await analyticsDb.SalesFacts
            .OrderByDescending(x => x.SaleId)
            .Select(x => x.SaleId)
            .FirstOrDefaultAsync();

        const int batchSize = 500;

        while (true)
        {
            var batch = await trendDb.ProdajaZaglavlja
                .Where(x => x.Id > lastId)
                .OrderBy(x => x.Id)
                .Take(batchSize)
                .Include(x => x.Stavke)
                .AsNoTracking()
                .ToListAsync();

            if (batch.Count == 0)
                break;

            foreach (var sale in batch)
            {
                analyticsDb.SalesFacts.Add(new SalesFact
                {
                    SaleId = sale.Id,
                    BrojRacuna = sale.BrojRacuna ?? string.Empty,
                    SaleTimestampUtc = DateTime.SpecifyKind(sale.DatumProdaje, DateTimeKind.Utc),
                    StoreId = sale.IDObjekat ?? 0,
                    PaymentType = sale.NacinPlacanja ?? string.Empty,
                    TotalAmount = sale.Stavke.Sum(s => s.Kolicina * s.Cena),
                    TotalUnits = sale.Stavke.Sum(s => s.Kolicina),
                    TotalLines = sale.Stavke.Count,
                    DataOrigin = sale.DataOrigin
                });

                foreach (var line in sale.Stavke)
                {
                    analyticsDb.SalesLineFacts.Add(new SalesLineFact
                    {
                        SaleId = sale.Id,
                        ProductId = line.IdArtikal,
                        Qty = line.Kolicina,
                        UnitPrice = line.Cena,
                        LineTotal = line.Kolicina * line.Cena,
                        NabavnaCena = line.NabavnaCena,
                        DataOrigin = sale.DataOrigin
                    });
                }
            }

            await analyticsDb.SaveChangesAsync();

            lastId = batch.Last().Id;
        }

        logger.LogInformation("✔ SalesFacts incremental backfill complete.");
    }

    private static async Task BackfillReturnFactsAsync(
        TrendplusDbContext trendDb,
        AnalyticsDbContext analyticsDb,
        ILogger logger)
    {
        var trendConnectionString = trendDb.Database.GetConnectionString()
            ?? trendDb.Database.GetDbConnection().ConnectionString;
        var hasPovracajDataOrigin = !string.IsNullOrWhiteSpace(trendConnectionString)
            && await RelationHasColumnAsync(trendConnectionString, "povracaj_zaglavlje", "data_origin");

        if (!hasPovracajDataOrigin)
        {
            logger.LogWarning("povracaj_zaglavlje.data_origin is missing; ReturnFacts backfill will use compatibility mode with DataOrigin='existing'.");
        }

        var lastSourceLineId = await analyticsDb.ReturnFacts
            .OrderByDescending(x => x.SourceLineId)
            .Select(x => x.SourceLineId)
            .FirstOrDefaultAsync();

        const int batchSize = 500;

        while (true)
        {
            if (hasPovracajDataOrigin)
            {
                var batch = await trendDb.PovracajStavke
                    .Where(x => x.Id > lastSourceLineId)
                    .OrderBy(x => x.Id)
                    .Take(batchSize)
                    .Include(x => x.Povracaj)
                    .AsNoTracking()
                    .ToListAsync();

                if (batch.Count == 0)
                    break;

                foreach (var line in batch)
                {
                    if (line.Povracaj is null)
                        continue;

                    analyticsDb.ReturnFacts.Add(new ReturnFact
                    {
                        SourceLineId = line.Id,
                        ReturnId = line.IdPovracaj,
                        ProductId = line.IdArtikal,
                        SupplierId = line.Povracaj.IDDobavljac,
                        Qty = line.Kolicina,
                        UnitCost = line.Cena,
                        LineAmount = line.Kolicina * line.Cena,
                        ReturnTimestampUtc = DateTime.SpecifyKind(line.Povracaj.DatumPovracaja, DateTimeKind.Utc),
                        Status = ReturnFactStatusMapper.Normalize(line.Povracaj.Status),
                        HeaderReason = line.Povracaj.RazlogPovracaja,
                        LineReason = line.Razlog,
                        ItemCondition = line.StanjeArtikla,
                        BrojZapisnika = line.Povracaj.BrojZapisnika,
                        DataOrigin = line.Povracaj.DataOrigin
                    });
                }

                await analyticsDb.SaveChangesAsync();

                lastSourceLineId = batch.Last().Id;
                continue;
            }

            var compatibilityBatch = await trendDb.PovracajStavke
                .Where(x => x.Id > lastSourceLineId)
                .OrderBy(x => x.Id)
                .Take(batchSize)
                .AsNoTracking()
                .Select(x => new ReturnBackfillCompatibilityRow(
                    x.Id,
                    x.IdPovracaj,
                    x.IdArtikal,
                    x.Kolicina,
                    x.Cena,
                    x.Razlog,
                    x.StanjeArtikla,
                    x.Povracaj.IDDobavljac,
                    x.Povracaj.DatumPovracaja,
                    x.Povracaj.Status,
                    x.Povracaj.RazlogPovracaja,
                    x.Povracaj.BrojZapisnika))
                .ToListAsync();

            if (compatibilityBatch.Count == 0)
                break;

            foreach (var line in compatibilityBatch)
            {
                analyticsDb.ReturnFacts.Add(new ReturnFact
                {
                    SourceLineId = line.SourceLineId,
                    ReturnId = line.ReturnId,
                    ProductId = line.ProductId,
                    SupplierId = line.SupplierId,
                    Qty = line.Qty,
                    UnitCost = line.UnitCost,
                    LineAmount = line.Qty * line.UnitCost,
                    ReturnTimestampUtc = DateTime.SpecifyKind(line.ReturnTimestampUtc, DateTimeKind.Utc),
                    Status = ReturnFactStatusMapper.Normalize(line.Status),
                    HeaderReason = line.HeaderReason,
                    LineReason = line.LineReason,
                    ItemCondition = line.ItemCondition,
                    BrojZapisnika = line.BrojZapisnika,
                    DataOrigin = "existing"
                });
            }

            await analyticsDb.SaveChangesAsync();

            lastSourceLineId = compatibilityBatch.Last().SourceLineId;
        }

        logger.LogInformation("âœ” ReturnFacts incremental backfill complete.");
    }

    private sealed record ReturnBackfillCompatibilityRow(
        int SourceLineId,
        int ReturnId,
        int ProductId,
        int Qty,
        decimal UnitCost,
        string? LineReason,
        string? ItemCondition,
        int SupplierId,
        DateTime ReturnTimestampUtc,
        string? Status,
        string? HeaderReason,
        string BrojZapisnika);

    private static string NormalizeSqlScriptIdentifier(string sqlFilePath) =>
        sqlFilePath.Replace('\\', '/');

    private static string ComputeSqlScriptHash(string sql)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(sql);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string[] SplitSqlBatches(string sql)
    {
        var batches = sql
            .Split("-- SQL_BATCH_BREAK", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return batches.Length == 0 ? [sql] : batches;
    }

    private static async Task EnsureStartupSqlHistoryTableAsync(string connectionString)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS "__StartupSqlScriptHistory" (
                "ScriptPath" character varying(512) PRIMARY KEY,
                "ScriptHash" character varying(64) NOT NULL,
                "AppliedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW(),
                "DurationMs" bigint NULL
            );
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = AdvisoryLockCommandTimeoutSeconds;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> GetAppliedStartupSqlHashAsync(string connectionString, string scriptPath)
    {
        const string sql = """
            SELECT "ScriptHash"
            FROM "__StartupSqlScriptHistory"
            WHERE "ScriptPath" = @scriptPath;
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = AdvisoryLockCommandTimeoutSeconds;
        command.Parameters.AddWithValue("scriptPath", scriptPath);
        return (string?)await command.ExecuteScalarAsync();
    }

    private static async Task DeleteAppliedStartupSqlHistoryAsync(string connectionString, string scriptPath)
    {
        const string sql = """
            DELETE FROM "__StartupSqlScriptHistory"
            WHERE "ScriptPath" = @scriptPath;
            """;

        await EnsureStartupSqlHistoryTableAsync(connectionString);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = AdvisoryLockCommandTimeoutSeconds;
        command.Parameters.AddWithValue("scriptPath", NormalizeSqlScriptIdentifier(scriptPath));
        await command.ExecuteNonQueryAsync();
    }

    private static async Task RecordAppliedStartupSqlAsync(
        string connectionString,
        string scriptPath,
        string scriptHash,
        long durationMs)
    {
        const string sql = """
            INSERT INTO "__StartupSqlScriptHistory" ("ScriptPath", "ScriptHash", "AppliedAtUtc", "DurationMs")
            VALUES (@scriptPath, @scriptHash, NOW(), @durationMs)
            ON CONFLICT ("ScriptPath") DO UPDATE
            SET "ScriptHash" = EXCLUDED."ScriptHash",
                "AppliedAtUtc" = EXCLUDED."AppliedAtUtc",
                "DurationMs" = EXCLUDED."DurationMs";
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = AdvisoryLockCommandTimeoutSeconds;
        command.Parameters.AddWithValue("scriptPath", scriptPath);
        command.Parameters.AddWithValue("scriptHash", scriptHash);
        command.Parameters.AddWithValue("durationMs", durationMs);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteSqlBatchesWithoutTransactionAsync(
        NpgsqlConnection connection,
        string scriptIdentifier,
        IReadOnlyList<string> batches,
        int commandTimeoutSeconds,
        ILogger logger)
    {
        const int maxAttempts = 5;
        for (var i = 0; i < batches.Count; i++)
        {
            var attempt = 0;
            Exception? lastEx = null;

            if (batches.Count > 1)
            {
                logger.LogInformation(
                    "Executing startup SQL file {FilePath} batch {BatchNumber}/{BatchCount}.",
                    scriptIdentifier,
                    i + 1,
                    batches.Count);
            }

            while (attempt < maxAttempts)
            {
                attempt++;
                try
                {
                    // Apply a per-session lock timeout to avoid waiting indefinitely for locks.
                    // On retries, we increase the lock timeout.
                    var effectiveLockTimeout = StartupSqlLockTimeoutSeconds * (int)Math.Max(1, Math.Pow(2, attempt - 1));
                    await using (var lockTimeoutCommand = new NpgsqlCommand(
                        $"SET LOCAL lock_timeout = '{effectiveLockTimeout}s'; SET LOCAL statement_timeout = '0';",
                        connection))
                    {
                        lockTimeoutCommand.CommandTimeout = AdvisoryLockCommandTimeoutSeconds;
                        await lockTimeoutCommand.ExecuteNonQueryAsync();
                    }

                    await using var command = new NpgsqlCommand(batches[i], connection);
                    command.CommandTimeout = commandTimeoutSeconds;
                    await command.ExecuteNonQueryAsync();
                    // success
                    lastEx = null;
                    break;
                }
                catch (Npgsql.PostgresException pex) when (pex.SqlState == "55P03")
                {
                    // Lock timeout — log and retry with backoff
                    lastEx = pex;
                    logger.LogWarning(pex, "Lock timeout (55P03) executing batch {Batch} of {Script}. Attempt {Attempt}/{MaxAttempts}", i + 1, scriptIdentifier, attempt, maxAttempts);
                }
                catch (Npgsql.NpgsqlException nex)
                {
                    // Transient network or lower-level error — capture and decide to retry
                    lastEx = nex;
                    logger.LogWarning(nex, "Transient error executing batch {Batch} of {Script}. Attempt {Attempt}/{MaxAttempts}", i + 1, scriptIdentifier, attempt, maxAttempts);
                }
                catch (Exception ex)
                {
                    // Non-transient — rethrow
                    logger.LogError(ex, "Non-retriable error executing SQL batch {Batch} of {Script}", i + 1, scriptIdentifier);
                    throw;
                }

                if (lastEx != null && attempt < maxAttempts)
                {
                    var delayMs = (int)(Math.Pow(2, attempt - 1) * 1000);
                    logger.LogInformation("Waiting {Delay}ms before retrying batch {Batch}", delayMs, i + 1);
                    await Task.Delay(delayMs);
                }
            }

            if (lastEx != null)
            {
                logger.LogError(lastEx, "Failed to execute batch {Batch} of {Script} after {Attempts} attempts", i + 1, scriptIdentifier, maxAttempts);
                throw lastEx;
            }
        }
    }

    private static async Task ExecuteSqlFileAsync(
        string connectionString,
        string sqlFilePath,
        ILogger logger,
        int commandTimeoutSeconds = 300,
        bool useTransaction = true,
        int startBatchNumber = 1,
        int? maxBatchCount = null,
        string? historyIdentifier = null)
    {
        var resolvedPath = ResolveSqlFilePath(sqlFilePath);
        if (resolvedPath == null)
        {
            logger.LogWarning("SQL file not found: {FilePath}", sqlFilePath);
            return;
        }

        var scriptDisplayIdentifier = NormalizeSqlScriptIdentifier(sqlFilePath);
        var scriptHistoryIdentifier = NormalizeSqlScriptIdentifier(historyIdentifier ?? sqlFilePath);
        var sql = await File.ReadAllTextAsync(resolvedPath);
        var batches = SplitSqlBatches(sql);
        var normalizedStartBatchNumber = Math.Clamp(startBatchNumber, 1, batches.Length);
        var selectedBatches = batches.Skip(normalizedStartBatchNumber - 1).ToArray();
        var effectiveBatchCount = maxBatchCount.HasValue
            ? Math.Clamp(maxBatchCount.Value, 1, selectedBatches.Length)
            : selectedBatches.Length;
        var effectiveBatches = (!useTransaction && effectiveBatchCount < selectedBatches.Length)
            ? selectedBatches.Take(effectiveBatchCount).ToArray()
            : selectedBatches;
        var sqlForExecution = (!useTransaction && effectiveBatchCount < selectedBatches.Length)
            ? string.Join(Environment.NewLine + "-- SQL_BATCH_BREAK" + Environment.NewLine, effectiveBatches)
            : string.Join(Environment.NewLine + "-- SQL_BATCH_BREAK" + Environment.NewLine, effectiveBatches);
        var scriptHash = ComputeSqlScriptHash(sqlForExecution);

        await EnsureStartupSqlHistoryTableAsync(connectionString);

        var appliedHash = await GetAppliedStartupSqlHashAsync(connectionString, scriptHistoryIdentifier);
        if (string.Equals(appliedHash, scriptHash, StringComparison.Ordinal))
        {
            logger.LogInformation("Skipping startup SQL file {FilePath}; same hash already applied.", scriptDisplayIdentifier);
            return;
        }

        logger.LogInformation(
            appliedHash == null
                ? "Executing startup SQL file {FilePath}."
                : "Re-executing startup SQL file {FilePath} because the file hash changed.",
            scriptDisplayIdentifier);
        logger.LogInformation("Resolved startup SQL file {FilePath} to {ResolvedPath}.", scriptDisplayIdentifier, resolvedPath);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        NpgsqlTransaction? tx = null;
        try
        {
            if (!useTransaction)
            {
                await ExecuteSqlBatchesWithoutTransactionAsync(connection, scriptDisplayIdentifier, effectiveBatches, commandTimeoutSeconds, logger);
            }
            else
            {
                // Retry transactional execution on lock-timeout (55P03). Some DDL batches can
                // contend with other sessions; retrying with an increased lock_timeout helps
                // survive transient contention during startup.
                const int maxAttempts = 3;
                Exception? lastEx = null;

                for (var attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        tx = await connection.BeginTransactionAsync();

                        // Increase lock_timeout on subsequent attempts (exponential).
                        var lockTimeoutSeconds = StartupSqlLockTimeoutSeconds * (int)Math.Pow(2, attempt - 1);
                        await using var lockTimeoutCommand = new NpgsqlCommand(
                            $"SET LOCAL lock_timeout = '{lockTimeoutSeconds}s';",
                            connection,
                            tx);
                        lockTimeoutCommand.CommandTimeout = AdvisoryLockCommandTimeoutSeconds;
                        await lockTimeoutCommand.ExecuteNonQueryAsync();

                        await using var command = new NpgsqlCommand(sqlForExecution, connection, tx);
                        command.CommandTimeout = commandTimeoutSeconds;
                        await command.ExecuteNonQueryAsync();
                        await tx.CommitAsync();

                        // Success; clear last exception and break
                        lastEx = null;
                        break;
                    }
                    catch (PostgresException pgEx) when (pgEx.SqlState == "55P03")
                    {
                        // Lock timeout — rollback and retry with backoff
                        lastEx = pgEx;
                        await TryRollbackTransactionAsync(tx, logger, scriptDisplayIdentifier);
                        logger.LogWarning(pgEx, "Lock timeout executing startup SQL file {FilePath}. Attempt {Attempt}/{MaxAttempts}", scriptDisplayIdentifier, attempt, maxAttempts);
                    }
                    catch (PostgresException pgEx) when (pgEx.SqlState == "42P07" || (pgEx.Detail != null && pgEx.Detail.Contains("already exists", StringComparison.OrdinalIgnoreCase)))
                    {
                        // Non-fatal duplicate-object error during transactional execution — rollback and continue.
                        await TryRollbackTransactionAsync(tx, logger, scriptDisplayIdentifier);
                        logger.LogWarning(pgEx,
                            "Non-fatal Postgres error while executing SQL file {FilePath} (object already exists). SqlState={SqlState}, Detail={Detail}",
                            scriptDisplayIdentifier, pgEx.SqlState, pgEx.Detail);
                        return;
                    }
                    catch (Exception ex)
                    {
                        await TryRollbackTransactionAsync(tx, logger, scriptDisplayIdentifier);
                        logger.LogError(ex, "Failed executing startup SQL file {FilePath} on attempt {Attempt}.", scriptDisplayIdentifier, attempt);
                        throw;
                    }

                    if (attempt < maxAttempts)
                    {
                        var delayMs = (int)(Math.Pow(2, attempt - 1) * 1000);
                        logger.LogInformation("Waiting {Delay}ms before retrying transactional execution of {FilePath}", delayMs, scriptDisplayIdentifier);
                        await Task.Delay(delayMs);
                    }
                }

                if (lastEx != null)
                {
                    logger.LogError(lastEx, "Failed to execute startup SQL file {FilePath} after {Attempts} attempts", scriptDisplayIdentifier, maxAttempts);
                    throw lastEx;
                }
            }

            stopwatch.Stop();
            logger.LogInformation("Completed startup SQL file {FilePath} in {ElapsedMs}ms.", scriptDisplayIdentifier, stopwatch.ElapsedMilliseconds);
            await RecordAppliedStartupSqlAsync(connectionString, scriptHistoryIdentifier, scriptHash, stopwatch.ElapsedMilliseconds);
        }
        catch (PostgresException pgEx)
        {
            await TryRollbackTransactionAsync(tx, logger, scriptDisplayIdentifier);

            if (pgEx.SqlState == "55P03")
            {
                logger.LogWarning(
                    pgEx,
                    "Skipping startup SQL file {FilePath} because a required relation lock was not acquired within {LockTimeoutSeconds}s.",
                    scriptDisplayIdentifier,
                    StartupSqlLockTimeoutSeconds);
                return;
            }

            // Treat 'relation already exists' errors as non-fatal during initialization
            // so the initializer can continue even if a prior run created the same objects.
            if (pgEx.SqlState == "42P07" || (pgEx.Detail != null && pgEx.Detail.Contains("already exists", StringComparison.OrdinalIgnoreCase)))
            {
                logger.LogWarning(pgEx,
                    "Non-fatal Postgres error while executing SQL file {FilePath} (object already exists). SqlState={SqlState}, Detail={Detail}",
                    scriptDisplayIdentifier, pgEx.SqlState, pgEx.Detail);
                return;
            }

            logger.LogError(pgEx,
                "Postgres error while executing SQL file {FilePath}. SqlState={SqlState}, Detail={Detail}",
                scriptDisplayIdentifier, pgEx.SqlState, pgEx.Detail);
            throw;
        }
        catch (Exception ex)
        {
            await TryRollbackTransactionAsync(tx, logger, scriptDisplayIdentifier);
            logger.LogError(ex, "Failed to execute SQL file: {FilePath}", scriptDisplayIdentifier);
            throw;
        }
        finally
        {
            if (tx != null) await tx.DisposeAsync();
        }
    }

    private static async Task TryRollbackTransactionAsync(
        NpgsqlTransaction? tx,
        ILogger logger,
        string scriptDisplayIdentifier)
    {
        if (tx is null)
        {
            return;
        }

        try
        {
            await tx.RollbackAsync();
        }
        catch (InvalidOperationException ex)
        {
            logger.LogDebug(
                ex,
                "Skipping rollback for startup SQL file {FilePath} because the transaction has already completed.",
                scriptDisplayIdentifier);
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

            const string sql = @"
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
            logger.LogError(ex, "Failed to check table existence for {TableName}. Initialization cannot continue safely.", tableName);
            throw;
        }
    }

    private static async Task EnsureOpenProductTrainingDatasetsAsync(
        string connectionString,
        IConfiguration configuration,
        ILogger logger)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        const string checkSql = @"SELECT to_regclass('public.dataset') IS NOT NULL;";
        await using (var checkCmd = new NpgsqlCommand(checkSql, connection))
        {
            var exists = (bool?)await checkCmd.ExecuteScalarAsync();
            if (exists != true)
            {
                logger.LogInformation("Dataset table does not exist. Skipping dataset seed.");
                return;
            }
        }

        // Ensure UNIQUE constraint on name so ON CONFLICT works
        await using (var idxCmd = new NpgsqlCommand(
            @"CREATE UNIQUE INDEX IF NOT EXISTS uq_dataset_name ON dataset (name);", connection))
        {
            await idxCmd.ExecuteNonQueryAsync();
        }

        var defaultDatasets = new[]
        {
            "kaggle_shoe_dataset",
            "amazon_clothing_shoes",
            "ebay_shoes",
            "google_shopping_shoes"
        };

        foreach (var name in defaultDatasets)
        {
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO dataset (name, source_type, description, license)
                VALUES (@name, @type, @desc, 'Unknown')
                ON CONFLICT (name) DO NOTHING;", connection);

            cmd.Parameters.AddWithValue("name", name);
            cmd.Parameters.AddWithValue("type", InferSourceType(name));
            cmd.Parameters.AddWithValue("desc", $"Open product training dataset: {name}");

            await cmd.ExecuteNonQueryAsync();
        }

        logger.LogInformation("✔ Ensured open_product_training dataset seed.");
    }

    private static string InferSourceType(string datasetName)
    {
        if (datasetName.Contains("kaggle", StringComparison.OrdinalIgnoreCase))
            return "kaggle";
        if (datasetName.Contains("amazon", StringComparison.OrdinalIgnoreCase))
            return "amazon";
        if (datasetName.Contains("ebay", StringComparison.OrdinalIgnoreCase))
            return "ebay";
        if (datasetName.Contains("google", StringComparison.OrdinalIgnoreCase))
            return "google";

        return "custom";
    }
}

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

        var trendplusInitialized = false;
        var analyticsInitialized = false;

        try
        {
            // 1. Initialize Trendplus DB
            await InitializeTrendplusDbAsync(services, configuration, logger);
            trendplusInitialized = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Trendplus DB initialization failed");
        }

        try
        {
            // 2. Initialize Analytics DB
            await InitializeAnalyticsDbAsync(services, configuration, logger);
            analyticsInitialized = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Analytics DB initialization failed");
        }

        if (trendplusInitialized || analyticsInitialized)
        {
            logger.LogInformation(
                "=== DATABASE INITIALIZATION COMPLETE (trendplus={TrendplusOk}, analytics={AnalyticsOk}) ===",
                trendplusInitialized,
                analyticsInitialized);
        }
        else
        {
            logger.LogError("=== DATABASE INITIALIZATION FAILED (no database initialized) ===");
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

        // Run EF migrations. If this fails, continue with minimal schema self-heal.
        try
        {
            await context.Database.MigrateAsync();
            logger.LogInformation("? Trendplus DB migrations applied");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Trendplus DB migrations failed; continuing with core schema self-heal.");
        }

        await EnsureTrendplusCoreSchemaAsync(
            configuration.GetConnectionString("DefaultConnection")!,
            logger);

        // Ensure analytics aggregation support tables/indexes in Trendplus DB (idempotent).
        await EnsureTrendplusAggregationTablesAsync(
            configuration.GetConnectionString("DefaultConnection")!,
            logger);

        // Nightly analytics materialized views (daily facts + rolling + momentum) (idempotent)
        // Keep this early so it still gets applied even if later nivelacija scripts fail.
        await ExecuteSqlFileAsync(
            configuration.GetConnectionString("DefaultConnection")!,
            "Database/Migrations/017_CreateNightlyAnalyticsMaterializedViews.sql",
            logger);

        // Access-import support patch (idempotent)
        await ExecuteSqlFileAsync(
            configuration.GetConnectionString("DefaultConnection")!,
            "Database/Migrations/012_AddAccessImportSupport.sql",
            logger);

        // Pre/Post nivelacija reporting views (idempotent)
        await ExecuteSqlFileAsync(
            configuration.GetConnectionString("DefaultConnection")!,
            "Database/Migrations/013_AddVendorSalesNivelacijaViews.sql",
            logger);

        // Fix nivelacija views to read from DnevnikPromena directly
        // and switch from ILIKE to exact IN-list for index use (idempotent)
        await ExecuteSqlFileAsync(
            configuration.GetConnectionString("DefaultConnection")!,
            "Database/Migrations/014_FixNivelacijaViewsFromDnevnik.sql",
            logger);

        // Additional nivelacija analytics views (rolling, momentum, OOS, DiD)
        // used by pre/post analytics metrics (idempotent)
        await ExecuteSqlFileAsync(
            configuration.GetConnectionString("DefaultConnection")!,
            "Database/Migrations/016_AnalyticsNivelacijaEnhancements.sql",
            logger);

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
        command.CommandTimeout = 120;
        await command.ExecuteNonQueryAsync();

        logger.LogInformation("? Ensured Trendplus analytics aggregation tables/indexes");
    }

    private static async Task EnsureTrendplusCoreSchemaAsync(
        string connectionString,
        ILogger logger)
    {
        const string sql = @"
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

            CREATE INDEX IF NOT EXISTS ""IX_Artikli_ImagePath"" ON ""Artikli"" (""ImagePath"");

            -- DnevnikPromena operational columns used by SyncWorker and import pipeline
            ALTER TABLE IF EXISTS ""DnevnikPromena"" ADD COLUMN IF NOT EXISTS ""IDObjekat"" integer;
            ALTER TABLE IF EXISTS ""DnevnikPromena"" ADD COLUMN IF NOT EXISTS ""RedniBroj"" integer;
            CREATE INDEX IF NOT EXISTS ""IX_DnevnikPromena_IDObjekat_Datum"" ON ""DnevnikPromena"" (""IDObjekat"", ""Datum"");

            -- Prodaja operational columns
            ALTER TABLE IF EXISTS prodaja_zaglavlje ADD COLUMN IF NOT EXISTS ""korisnik_ime"" character varying(200);
            ALTER TABLE IF EXISTS prodaja_stavke    ADD COLUMN IF NOT EXISTS ""nabavna_cena"" decimal(18,2);

            -- Access import batch compatibility columns (migration 015)
            ALTER TABLE IF EXISTS ""DataImportBatches"" ADD COLUMN IF NOT EXISTS ""DurationSeconds"" integer;
            ALTER TABLE IF EXISTS ""DataImportBatches"" ADD COLUMN IF NOT EXISTS ""TotalImported"" integer NOT NULL DEFAULT 0;
            ALTER TABLE IF EXISTS ""DataImportBatches"" ADD COLUMN IF NOT EXISTS ""TotalUpdated"" integer NOT NULL DEFAULT 0;
            ALTER TABLE IF EXISTS ""DataImportBatches"" ADD COLUMN IF NOT EXISTS ""TotalErrors"" integer NOT NULL DEFAULT 0;
            ALTER TABLE IF EXISTS ""DataImportBatches"" ADD COLUMN IF NOT EXISTS ""DataOrigin"" character varying(32) NOT NULL DEFAULT 'access';
        ";

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = 120;
        await command.ExecuteNonQueryAsync();

        logger.LogInformation("? Ensured core Trendplus schema for Artikli/DnevnikPromena/Prodaja columns");
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

        // Run EF migrations. If this fails (e.g. optional extension not available),
        // continue with core table self-heal so workers can still function.
        try
        {
            await context.Database.MigrateAsync();
            logger.LogInformation("? Analytics DB migrations applied");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Analytics DB migrations failed; continuing with core analytics table self-heal.");
        }

        // Self-heal core analytics dimensions used by workers. This covers scenarios where
        // migration history is out of sync and quoted mixed-case tables are missing.
        await EnsureCoreAnalyticsDimensionTablesAsync(
            configuration.GetConnectionString("AnalyticsConnection")!,
            logger);

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
            "items",
            logger))
        {
            logger.LogInformation("Scraper scoring tables not found, creating...");
            await ExecuteSqlFileAsync(
                configuration.GetConnectionString("AnalyticsConnection")!,
                "Database/Analytics/004_AddScraperScoringTables.sql",
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

        // External shopping/trends tables (idempotent SQL scripts)
        if (!await TableExistsAsync(
            configuration.GetConnectionString("AnalyticsConnection")!,
            "amazon_shoe_products",
            logger))
        {
            logger.LogInformation("amazon_shoe_products table not found, creating...");
            await ExecuteSqlFileAsync(
                configuration.GetConnectionString("AnalyticsConnection")!,
                "Database/Analytics/006_AddAmazonShoesTable.sql",
                logger);
        }

        if (!await TableExistsAsync(
            configuration.GetConnectionString("AnalyticsConnection")!,
            "ebay_shoe_products",
            logger))
        {
            logger.LogInformation("ebay_shoe_products table not found, creating...");
            await ExecuteSqlFileAsync(
                configuration.GetConnectionString("AnalyticsConnection")!,
                "Database/Analytics/007_AddEbayShoesTable.sql",
                logger);
        }

        if (!await TableExistsAsync(
            configuration.GetConnectionString("AnalyticsConnection")!,
            "google_shopping_products",
            logger))
        {
            logger.LogInformation("google_shopping_products table not found, creating...");
            await ExecuteSqlFileAsync(
                configuration.GetConnectionString("AnalyticsConnection")!,
                "Database/Analytics/010_AddGoogleShoppingTable.sql",
                logger);
        }

        // Access-import origin support patch (idempotent)
        await ExecuteSqlFileAsync(
            configuration.GetConnectionString("AnalyticsConnection")!,
            "Database/Analytics/011_AddDataOriginColumns.sql",
            logger);

        // Open product training schema (stored in analytics DB by default)
        if (!await TableExistsAsync(
            configuration.GetConnectionString("AnalyticsConnection")!,
            "dataset",
            logger))
        {
            logger.LogInformation("open_product_training schema not found, creating base tables...");
            await ExecuteSqlFileAsync(
                configuration.GetConnectionString("AnalyticsConnection")!,
                "Database/OpenProductTraining/001_create_schema.sql",
                logger);
        }

        if (!await TableExistsAsync(
            configuration.GetConnectionString("AnalyticsConnection")!,
            "product_split",
            logger))
        {
            logger.LogInformation("open_product_training split table not found, applying split enum/table patch...");
            await ExecuteSqlFileAsync(
                configuration.GetConnectionString("AnalyticsConnection")!,
                "Database/OpenProductTraining/002_fix_enum.sql",
                logger);
        }

        // Views are idempotent (CREATE OR REPLACE) and can be safely re-applied on startup.
        await ExecuteSqlFileAsync(
            configuration.GetConnectionString("AnalyticsConnection")!,
            "Database/OpenProductTraining/003_add_ml_export_views.sql",
            logger);

        // Open Product Training 2.0 schema extensions + feature-store views (idempotent).
        await ExecuteSqlFileAsync(
            configuration.GetConnectionString("AnalyticsConnection")!,
            "Database/OpenProductTraining/004_open_training_2_0.sql",
            logger);

        await ExecuteSqlFileAsync(
            configuration.GetConnectionString("AnalyticsConnection")!,
            "Database/OpenProductTraining/005_open_training_2_0_views.sql",
            logger);

        await ExecuteSqlFileAsync(
            configuration.GetConnectionString("AnalyticsConnection")!,
            "Database/OpenProductTraining/006_runtime_priors_materialized.sql",
            logger);

        await EnsureOpenProductTrainingDatasetsAsync(
            configuration.GetConnectionString("AnalyticsConnection")!,
            configuration,
            logger);

        // Backward-compatible schema patch for older eBay table script versions.
        await context.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE IF EXISTS ebay_shoe_products
                ADD COLUMN IF NOT EXISTS ""Gender"" TEXT;

            ALTER TABLE IF EXISTS ebay_shoe_products
                ADD COLUMN IF NOT EXISTS ""TrendScore"" REAL NOT NULL DEFAULT 0;
        ");

        logger.LogInformation("? Analytics DB initialized");

        // Backfill historical sales into analytics facts (idempotent).
        await BackfillSalesFactsAsync(trendDb, context, logger);
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
        ";

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = 120;
        await command.ExecuteNonQueryAsync();

        logger.LogInformation("? Ensured core analytics dimension tables (ProductsDim, StoresDim)");
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

    private static async Task EnsureOpenProductTrainingDatasetsAsync(
        string connectionString,
        IConfiguration configuration,
        ILogger logger)
    {
        try
        {
            if (!await TableExistsAsync(connectionString, "dataset", logger))
            {
                logger.LogInformation("Skipping open_product_training dataset seed because table dataset does not exist.");
                return;
            }

            var configuredDatasets = configuration
                .GetSection("OpenProductTraining:DefaultDatasets")
                .Get<string[]>()?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var datasetNames = (configuredDatasets is { Length: > 0 }
                ? configuredDatasets
                : new[] { "kaggle_shoe_dataset", "amazon_clothing_shoes", "ebay_shoes", "google_shopping_shoes" });

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var selectCmd = new NpgsqlCommand("SELECT name FROM dataset;", connection))
            await using (var reader = await selectCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var name = reader.GetString(0);
                    existingNames.Add(name);
                }
            }

            var inserted = 0;
            foreach (var name in datasetNames)
            {
                if (existingNames.Contains(name))
                    continue;

                await using var insertCmd = new NpgsqlCommand(@"
                    INSERT INTO dataset (name, source_type, description, license)
                    VALUES (@name, @source_type, @description, @license);", connection);

                insertCmd.Parameters.AddWithValue("name", name);
                insertCmd.Parameters.AddWithValue("source_type", InferSourceType(name));
                insertCmd.Parameters.AddWithValue("description", GetDefaultDescription(name));
                insertCmd.Parameters.AddWithValue("license", "Unknown");

                inserted += await insertCmd.ExecuteNonQueryAsync();
                existingNames.Add(name);
            }

            if (inserted > 0)
            {
                logger.LogInformation(
                    "Seeded {InsertedCount} dataset rows in open_product_training.dataset.",
                    inserted);
            }
            else
            {
                logger.LogInformation(
                    "open_product_training.dataset already contains configured dataset names.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to seed open_product_training.dataset.");
        }
    }

    private static string InferSourceType(string datasetName)
    {
        if (datasetName.Contains("amazon", StringComparison.OrdinalIgnoreCase))
            return "amazon";
        if (datasetName.Contains("ebay", StringComparison.OrdinalIgnoreCase))
            return "ebay";
        if (datasetName.Contains("google", StringComparison.OrdinalIgnoreCase) ||
            datasetName.Contains("shopping", StringComparison.OrdinalIgnoreCase))
            return "google";
        if (datasetName.Contains("kaggle", StringComparison.OrdinalIgnoreCase))
            return "kaggle";
        if (datasetName.Contains("zappos", StringComparison.OrdinalIgnoreCase))
            return "zappos";
        return "custom";
    }

    private static string GetDefaultDescription(string datasetName)
    {
        if (datasetName.Equals("kaggle_shoe_dataset", StringComparison.OrdinalIgnoreCase))
            return "Kaggle shoe dataset used for open product training.";
        if (datasetName.Equals("amazon_clothing_shoes", StringComparison.OrdinalIgnoreCase))
            return "Amazon clothing/shoes metadata dataset used for training.";
        if (datasetName.Equals("ebay_shoes", StringComparison.OrdinalIgnoreCase))
            return "eBay shoes dataset used for open product training.";
        if (datasetName.Equals("google_shopping_shoes", StringComparison.OrdinalIgnoreCase))
            return "Google Shopping shoes dataset used for open product training.";
        return $"Open product training dataset: {datasetName}";
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
            var resolvedPath = sqlFilePath;
            if (!File.Exists(resolvedPath))
            {
                var relative = sqlFilePath
                    .Replace('\\', Path.DirectorySeparatorChar)
                    .Replace('/', Path.DirectorySeparatorChar);
                var candidate = Path.Combine(AppContext.BaseDirectory, relative);
                if (File.Exists(candidate))
                {
                    resolvedPath = candidate;
                }
            }

            // Check if file exists
            if (!File.Exists(resolvedPath))
            {
                logger.LogWarning("SQL file not found: {FilePath}", sqlFilePath);
                return;
            }

            // Read SQL file
            var sql = await File.ReadAllTextAsync(resolvedPath);

            // Execute SQL
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand(sql, connection);
            command.CommandTimeout = 300; // 5 minutes

            await command.ExecuteNonQueryAsync();

            logger.LogInformation("? Executed SQL file: {FilePath}", resolvedPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute SQL file: {FilePath}", sqlFilePath);
            throw;
        }
    }
}

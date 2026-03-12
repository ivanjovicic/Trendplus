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
    private const long AdvisoryLockKey = 987654321L;

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
        await using var lockCmd = new NpgsqlCommand("SELECT pg_advisory_lock(@key);", connection);
        lockCmd.Parameters.AddWithValue("key", AdvisoryLockKey);
        await lockCmd.ExecuteNonQueryAsync();
        logger.LogInformation("Acquired advisory startup lock with key {Key}.", AdvisoryLockKey);

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
            unlockCmd.Parameters.AddWithValue("key", AdvisoryLockKey);
            await unlockCmd.ExecuteNonQueryAsync();
            logger.LogInformation("Released advisory startup lock with key {Key}.", AdvisoryLockKey);
        }
    }

    private static string? ResolveSqlFilePath(string sqlFilePath)
    {
        if (File.Exists(sqlFilePath))
            return sqlFilePath;

        var relative = sqlFilePath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        var candidate = Path.Combine(AppContext.BaseDirectory, relative);

        if (File.Exists(candidate))
            return candidate;

        return null;
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

        // Ensure migrations history table exists
        await ExecuteSqlCommandAsync(connectionString, @"
            CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
                ""MigrationId"" character varying(150) NOT NULL PRIMARY KEY,
                ""ProductVersion"" character varying(32) NOT NULL
            );
        ", logger);

        // Mark problematic migration as applied
        await ExecuteSqlCommandAsync(connectionString, @"
            INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
            VALUES ('20260112000000_AddArtikliKategorije', '8.0.0')
            ON CONFLICT (""MigrationId"") DO NOTHING;
        ", logger);

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
            "Database/Migrations/016_AnalyticsNivelacijaEnhancements.sql",
            "Database/Migrations/018_AddSupplierDecisionHubViews.sql",
            "Database/Migrations/005_CreateArtikliAndTestData.sql"
        };

        foreach (var sqlFile in sqlFiles)
        {
            await ExecuteSqlFileAsync(connectionString, sqlFile, logger);
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

    private static async Task EnsureTrendplusCoreSchemaAsync(
        string connectionString,
        ILogger logger)
    {
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

            -- Create DataImportBatches table if it doesn't exist (idempotent bootstrap)
            CREATE TABLE IF NOT EXISTS ""DataImportBatches"" (
                ""Id""              bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""SourceSystem""    character varying(64)   NOT NULL,
                ""SourceFileName""  character varying(300)  NOT NULL,
                ""StartedAtUtc""    timestamp with time zone NOT NULL,
                ""CompletedAtUtc""  timestamp with time zone,
                ""Status""          character varying(32)   NOT NULL,
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

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = 120;
        await command.ExecuteNonQueryAsync();

        logger.LogInformation("✔ Ensured Trendplus core schema for Artikli/DnevnikPromena/Prodaja columns.");
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
        command.CommandTimeout = 60;
        await command.ExecuteNonQueryAsync();

        logger.LogInformation("✔ Ensured OutboxMessages table exists.");
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

        await ExecuteSqlFileAsync(connectionString, "Database/Analytics/013_AddSupplierDecisionCompatibilitySchema.sql", logger);

        // Access-import origin support patch (idempotent)
        await ExecuteSqlFileAsync(connectionString, "Database/Analytics/011_AddDataOriginColumns.sql", logger);

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
        await ExecuteSqlFileAsync(connectionString, "Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql", logger);
        await ExecuteSqlFileAsync(connectionString, "Database/Migrations/016_AnalyticsNivelacijaEnhancements.sql", logger);
        await ExecuteSqlFileAsync(connectionString, "Database/Migrations/018_AddSupplierDecisionHubViews.sql", logger);
        await ExecuteSqlFileAsync(connectionString, "Database/Analytics/015_AddSupplierMlRanking.sql", logger);
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
        var lastSourceLineId = await analyticsDb.ReturnFacts
            .OrderByDescending(x => x.SourceLineId)
            .Select(x => x.SourceLineId)
            .FirstOrDefaultAsync();

        const int batchSize = 500;

        while (true)
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
                    Status = line.Povracaj.Status ?? string.Empty,
                    HeaderReason = line.Povracaj.RazlogPovracaja,
                    LineReason = line.Razlog,
                    ItemCondition = line.StanjeArtikla,
                    BrojZapisnika = line.Povracaj.BrojZapisnika,
                    DataOrigin = line.Povracaj.DataOrigin
                });
            }

            await analyticsDb.SaveChangesAsync();

            lastSourceLineId = batch.Last().Id;
        }

        logger.LogInformation("âœ” ReturnFacts incremental backfill complete.");
    }

    private static async Task ExecuteSqlFileAsync(
        string connectionString,
        string sqlFilePath,
        ILogger logger)
    {
        var resolvedPath = ResolveSqlFilePath(sqlFilePath);
        if (resolvedPath == null)
        {
            logger.LogWarning("SQL file not found: {FilePath}", sqlFilePath);
            return;
        }

        var sql = await File.ReadAllTextAsync(resolvedPath);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var tx = await connection.BeginTransactionAsync();
        try
        {
            await using var command = new NpgsqlCommand(sql, connection, tx);
            command.CommandTimeout = 300;
            await command.ExecuteNonQueryAsync();
            await tx.CommitAsync();
        }
        catch (PostgresException pgEx)
        {
            await tx.RollbackAsync();

            // Treat 'relation already exists' errors as non-fatal during initialization
            // so the initializer can continue even if a prior run created the same objects.
            if (pgEx.SqlState == "42P07" || (pgEx.Detail != null && pgEx.Detail.Contains("already exists", StringComparison.OrdinalIgnoreCase)))
            {
                logger.LogWarning(pgEx,
                    "Non-fatal Postgres error while executing SQL file {FilePath} (object already exists). SqlState={SqlState}, Detail={Detail}",
                    resolvedPath, pgEx.SqlState, pgEx.Detail);
                return;
            }

            logger.LogError(pgEx,
                "Postgres error while executing SQL file {FilePath}. SqlState={SqlState}, Detail={Detail}",
                resolvedPath, pgEx.SqlState, pgEx.Detail);
            throw;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            logger.LogError(ex, "Failed to execute SQL file: {FilePath}", resolvedPath);
            throw;
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

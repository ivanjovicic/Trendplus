using Application.Artikli.Commands.CreateArtikal;
using Application.Artikli.Commands.UpdateArtikal;
using Application.Artikli.Common.Interfaces;
using Application.Artikli.Queries.GetArtikal;
using Application.Artikli.Queries.VratiArtikle;
using Application.Common.Interfaces;
using Application.Dobavljaci.Queries;
using Application.Performance.Queries;
using Application.Povracaj.Commands;
using Application.Prodaja.Commands.ProdajArtikle;
using Application.Prodaja.Queries;
using Application.TipObuce.Commands;
using Application.TipObuce.Queries;
using Api.Models;
using Api.Services;
using Domain.Model;
using Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Infrastructure.DbContexts;
using Application.Analytics.Queries.GetTopProducts;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Domain.Model.TrendShoes;
using Application.TrendShoes;
using System.Globalization;
using Serilog.Context;

namespace Trendplus2.Endpoints;

public static class AllEndpoints
{
    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    private static readonly string[] SeasonalFallbackImageUrls =
    {
        "https://images.unsplash.com/photo-1460353581641-37baddab0fa2?w=1200&auto=format&fit=crop",
        "https://images.unsplash.com/photo-1543163521-1bf539c55dd2?w=1200&auto=format&fit=crop",
        "https://images.unsplash.com/photo-1595950653106-6c9ebd614d3a?w=1200&auto=format&fit=crop",
        "https://images.unsplash.com/photo-1579338559194-a162d19bf842?w=1200&auto=format&fit=crop",
        "https://images.unsplash.com/photo-1525966222134-fcfa99b8ae77?w=1200&auto=format&fit=crop"
    };

    public static void MapAllEndpoints(this WebApplication app)
    {
        // ============ HEALTH & MONITORING ============
        
        // Main health check
        app.MapGet("/health", (IMessageBroker messageBroker, WorkerHealthService workerHealth) =>
        {
            var rabbitMq = messageBroker as RabbitMqMessageBroker;
            var workersHealth = workerHealth.GetHealthSummary();
            
            return Results.Ok(new
            {
                Status = "Backend je živ",
                RabbitMq = new
                {
                    Enabled = messageBroker.IsEnabled,
                    CircuitOpen = rabbitMq?.IsCircuitOpen ?? false
                },
                Workers = workersHealth,
                Timestamp = DateTime.UtcNow
            });
        })
        .WithName("HealthCheck")
        .WithTags("System");

        // Worker health status
        app.MapGet("/api/workers/health", (WorkerHealthService workerHealth, WorkerRuntimeControlService workerControl, IHostEnvironment env) =>
        {
            var summary = workerHealth.GetHealthSummary();
            return Results.Ok(new
            {
                TotalWorkers = summary.TotalWorkers,
                HealthyWorkers = summary.HealthyWorkers,
                RunningWorkers = summary.RunningWorkers,
                ErrorWorkers = summary.ErrorWorkers,
                StoppedWorkers = summary.StaleWorkers,
                StaleWorkers = summary.StaleWorkers,
                HasCriticalIssues = summary.HasCriticalIssues,
                Workers = summary.Workers,
                WorkersEnabled = workerControl.IsEnabled,
                RuntimeToggleAllowed = workerControl.IsRuntimeToggleAllowed,
                Environment = env.EnvironmentName,
                LastSwitchAtUtc = workerControl.LastChangedUtc,
                LastSwitchBy = workerControl.LastChangedBy
            });
        })
        .WithName("WorkerHealthCheck")
        .WithTags("System")
        .RequireRateLimiting("fixed");

        app.MapGet("/api/workers/control", (WorkerRuntimeControlService workerControl, IHostEnvironment env) =>
        {
            return Results.Ok(workerControl.GetState(env.EnvironmentName));
        })
        .WithName("WorkerControlState")
        .WithTags("System")
        .RequireRateLimiting("fixed");

        app.MapPost("/api/workers/control/enable", (WorkerRuntimeControlService workerControl, IHostEnvironment env) =>
        {
            if (!workerControl.IsRuntimeToggleAllowed)
            {
                return Results.BadRequest(new
                {
                    Enabled = workerControl.IsEnabled,
                    Changed = false,
                    Message = "U ovoj okolini runtime ukljucivanje workera je zakljucano."
                });
            }

            var changed = workerControl.SetEnabled(true, $"api:{env.EnvironmentName}");
            return Results.Ok(new
            {
                Enabled = true,
                Changed = changed,
                Message = changed ? "Workeri su ukljuceni." : "Workeri su vec ukljuceni.",
                LastSwitchAtUtc = workerControl.LastChangedUtc,
                LastSwitchBy = workerControl.LastChangedBy
            });
        })
        .WithName("EnableWorkers")
        .WithTags("System")
        .RequireRateLimiting("strict");

        app.MapPost("/api/workers/control/disable", (WorkerRuntimeControlService workerControl, IHostEnvironment env) =>
        {
            var changed = workerControl.SetEnabled(false, $"api:{env.EnvironmentName}");
            return Results.Ok(new
            {
                Enabled = false,
                Changed = changed,
                Message = changed ? "Workeri su iskljuceni." : "Workeri su vec iskljuceni.",
                RuntimeToggleAllowed = workerControl.IsRuntimeToggleAllowed,
                LastSwitchAtUtc = workerControl.LastChangedUtc,
                LastSwitchBy = workerControl.LastChangedBy
            });
        })
        .WithName("DisableWorkers")
        .WithTags("System")
        .RequireRateLimiting("strict");

        // Circuit Breaker Status
        app.MapGet("/api/circuit-breaker/status", (IMessageBroker broker) =>
        {
            if (broker is RabbitMqMessageBroker rmq)
            {
                return Results.Ok(new
                {
                    isOpen = rmq.IsCircuitOpen,
                    isEnabled = rmq.IsEnabled,
                    status = rmq.IsCircuitOpen ? "Open" : "Closed"
                });
            }
            return Results.Ok(new { status = "N/A" });
        })
        .WithName("CircuitBreakerStatus")
        .WithTags("System");

        // ============ ERRORS ============
        
        app.MapGet("/errors", async (IErrorStore store) =>
        {
            var errors = await store.GetAllAsync();
            return Results.Ok(errors);
        })
        .WithName("GetErrors")
        .WithTags("System");

        // ============ LOGS ============
        
        app.MapGet("/api/logs", async (
            IErrorStore store,
            ILogger<Program> logger,
            int pageNumber = 1,
            int pageSize = 50,
            string? level = null,
            DateTime? fromDate = null,
            DateTime? toDate = null) =>
        {
            try
            {
                // Convert dates to UTC if they have Unspecified kind
                if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                    fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

                if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                    toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

                var errors = await store.GetAllAsync();
                var filtered = errors.AsEnumerable();

                if (fromDate.HasValue)
                    filtered = filtered.Where(e => e.Timestamp >= fromDate.Value);

                if (toDate.HasValue)
                    filtered = filtered.Where(e => e.Timestamp <= toDate.Value);

                if (!string.IsNullOrWhiteSpace(level))
                {
                    var lvl = level.Trim();
                    filtered = filtered.Where(e => string.Equals(e.Level, lvl, StringComparison.OrdinalIgnoreCase));
                }

                var total = filtered.Count();

                var paged = filtered
                    .OrderByDescending(e => e.Timestamp)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(e => new
                    {
                        timestamp = e.Timestamp.ToString("o"),
                        level = string.IsNullOrWhiteSpace(e.Level) ? "Error" : e.Level,
                        message = e.Message,
                        exception = !string.IsNullOrEmpty(e.StackTrace)
                            ? $"{e.ExceptionType}\n{e.StackTrace}"
                            : null,
                        properties = new
                        {
                            path = e.Path,
                            userName = e.UserName,
                            clientApp = e.ClientApp,
                            correlationId = e.CorrelationId
                        }
                    })
                    .ToList();

                return Results.Ok(new
                {
                    logs = paged,
                    totalCount = total,
                    pageNumber,
                    pageSize
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fetch logs");
                return Results.Problem(
                    detail: "Unable to fetch logs. Please run migrations: dotnet ef database update",
                    statusCode: 500,
                    title: "Database Error"
                );
            }
        })
        .WithName("GetLogs")
        .WithTags("System")
        .RequireRateLimiting("db-heavy");

        // ============ PERFORMANCE ============
        
        app.MapGet("/api/performance", async (
            IMediator mediator,
            ILogger<Program> logger,
            int topCount = 20,
            int minDurationMs = 1000,
            DateTime? fromDate = null,
            DateTime? toDate = null) =>
        {
            logger.LogInformation("Performance stats request: top={TopCount}, min={MinDuration}ms", topCount, minDurationMs);

            // Convert dates to UTC if they have Unspecified kind
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

            var query = new GetPerformanceStatsQuery(topCount, minDurationMs, fromDate, toDate);
            var result = await mediator.Send(query);
            return Results.Ok(result);
        })
        .WithName("GetPerformance")
        .WithTags("System")
        .RequireRateLimiting("db-heavy");

        // ============ ADMIN - RUN ANALYTICS OPTIMIZATION ============
        
        app.MapPost("/api/admin/run-analytics-optimization", async (
            ITrendplusDbContext db,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            try
            {
                logger.LogInformation("🚀 Starting analytics optimization migration...");
                
                var connectionString = db.Database.GetConnectionString();
                if (string.IsNullOrEmpty(connectionString))
                {
                    return Results.Problem("No connection string available", statusCode: 500);
                }

                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(ct);

                var results = new List<string>();

                // PART 1: Create Indexes
                var indexSql = @"
                    -- Index on ProdajaZaglavlja.DatumProdaje
                    CREATE INDEX IF NOT EXISTS idx_prodaja_datum ON ""ProdajaZaglavlja"" (""DatumProdaje"" DESC);
                    
                    -- Index on ProdajaStavke for JOIN operations
                    CREATE INDEX IF NOT EXISTS idx_prodaja_stavke_prodaja ON ""ProdajaStavke"" (""IdProdaja"");
                    CREATE INDEX IF NOT EXISTS idx_prodaja_stavke_artikal ON ""ProdajaStavke"" (""IdArtikal"");
                    
                    -- Index on Artikli for category/supplier grouping
                    CREATE INDEX IF NOT EXISTS idx_artikli_kategorija ON ""Artikli"" (""Kategorija"");
                    CREATE INDEX IF NOT EXISTS idx_artikli_dobavljac ON ""Artikli"" (""IDDobavljac"");
                    CREATE INDEX IF NOT EXISTS idx_artikli_pol ON ""Artikli"" (""Pol"");
                ";
                
                await using (var cmd = new NpgsqlCommand(indexSql, connection))
                {
                    await cmd.ExecuteNonQueryAsync(ct);
                    results.Add("✅ Indexes created");
                }

                // PART 2: Create Pre-aggregated Tables
                var tablesSql = @"
                    -- Daily Sales Summary
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

                    -- Category Summary
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

                    -- Supplier Summary
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

                    -- Gender Summary
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

                    -- Top Products Summary
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
                ";

                await using (var cmd = new NpgsqlCommand(tablesSql, connection))
                {
                    await cmd.ExecuteNonQueryAsync(ct);
                    results.Add("✅ Pre-aggregated tables created");
                }

                // PART 3: Initial data population for last 30 days
                var today = DateTime.UtcNow.Date;
                var populatedDays = 0;

                for (int i = 0; i < 30; i++)
                {
                    var date = today.AddDays(-i);
                    
                    // Refresh daily summary
                    var dailySql = @"
                        INSERT INTO ""AnalyticsDailySummary"" (""Date"", ""TotalRevenue"", ""TotalTransactions"", ""TotalUnits"", ""AvgBasketValue"", ""AvgItemPrice"", ""UpdatedAt"")
                        SELECT 
                            @date::DATE,
                            COALESCE(SUM(ps.""Kolicina"" * ps.""Cena""), 0),
                            COUNT(DISTINCT p.""Id""),
                            COALESCE(SUM(ps.""Kolicina""), 0),
                            CASE WHEN COUNT(DISTINCT p.""Id"") > 0 
                                THEN COALESCE(SUM(ps.""Kolicina"" * ps.""Cena""), 0) / COUNT(DISTINCT p.""Id"")
                                ELSE 0 
                            END,
                            CASE WHEN COALESCE(SUM(ps.""Kolicina""), 0) > 0 
                                THEN COALESCE(SUM(ps.""Kolicina"" * ps.""Cena""), 0) / SUM(ps.""Kolicina"")
                                ELSE 0 
                            END,
                            NOW()
                        FROM ""ProdajaZaglavlja"" p
                        JOIN ""ProdajaStavke"" ps ON p.""Id"" = ps.""IdProdaja""
                        WHERE DATE(p.""DatumProdaje"") = @date::DATE
                        ON CONFLICT (""Date"") DO UPDATE SET
                            ""TotalRevenue"" = EXCLUDED.""TotalRevenue"",
                            ""TotalTransactions"" = EXCLUDED.""TotalTransactions"",
                            ""TotalUnits"" = EXCLUDED.""TotalUnits"",
                            ""AvgBasketValue"" = EXCLUDED.""AvgBasketValue"",
                            ""AvgItemPrice"" = EXCLUDED.""AvgItemPrice"",
                            ""UpdatedAt"" = NOW();
                    ";

                    await using (var cmd = new NpgsqlCommand(dailySql, connection))
                    {
                        cmd.Parameters.AddWithValue("date", date);
                        await cmd.ExecuteNonQueryAsync(ct);
                    }

                    populatedDays++;
                }

                results.Add($"✅ Populated {populatedDays} days of analytics data");

                // Get stats
                var statsResults = new Dictionary<string, int>();

                await using (var cmd = new NpgsqlCommand(@"SELECT COUNT(*) FROM ""AnalyticsDailySummary""", connection))
                {
                    statsResults["dailySummaryCount"] = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
                }

                logger.LogInformation("✅ Analytics optimization completed successfully");

                return Results.Ok(new
                {
                    success = true,
                    message = "Analytics optimization completed",
                    results,
                    stats = statsResults
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Failed to run analytics optimization");
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 500,
                    title: "Failed to run analytics optimization"
                );
            }
        });

        // ============ ADMIN - RUN ANALYTICS OPTIMIZATION ============
        
        app.MapPost("/api/analytics/optimize", async (AnalyticsDbContext analyticsDb) =>
        {
            try
            {
                await analyticsDb.Database.ExecuteSqlRawAsync(@"
                    REFRESH MATERIALIZED VIEW CONCURRENTLY ProductsSummaryMV;
                    REFRESH MATERIALIZED VIEW CONCURRENTLY SalesSummaryMV;
                ");

                return Results.Ok(new { message = "Materialized views refreshed" });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to refresh: {ex.Message}");
            }
        })
        .WithName("OptimizeAnalytics")
        .WithTags("Analytics");

        // ============ ADMIN - INIT SCORING TABLES (fixes /api/dashboard/latest 500) ============

        app.MapPost("/api/admin/init-scoring-tables", async (
            AnalyticsDbContext analyticsDb,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            try
            {
                logger.LogInformation("🚀 Initializing scoring tables on analytics DB...");

                var connectionString = analyticsDb.Database.GetConnectionString();
                if (string.IsNullOrEmpty(connectionString))
                    return Results.Problem("No analytics connection string available", statusCode: 500);

                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(ct);

                var results = new List<string>();

                // 1. items – canonical product registry
                const string itemsSql = """
                    CREATE TABLE IF NOT EXISTS items (
                        item_id      BIGSERIAL PRIMARY KEY,
                        canonical_key TEXT NOT NULL UNIQUE,
                        brand         TEXT,
                        name          TEXT,
                        category      TEXT,
                        gender        TEXT,
                        created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                        updated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
                    );
                    CREATE INDEX IF NOT EXISTS ix_items_canonical_key ON items (canonical_key);
                    CREATE INDEX IF NOT EXISTS ix_items_category      ON items (category);
                    """;
                await using (var cmd = new NpgsqlCommand(itemsSql, connection))
                    await cmd.ExecuteNonQueryAsync(ct);
                results.Add("✅ items table ready");

                // 2. runs – one row per scraper execution
                const string runsSql = """
                    CREATE TABLE IF NOT EXISTS runs (
                        run_id      BIGSERIAL PRIMARY KEY,
                        started_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                        finished_at TIMESTAMPTZ,
                        status      TEXT NOT NULL DEFAULT 'running'
                                        CHECK (status IN ('running','completed','failed')),
                        total_items INT,
                        notes       TEXT
                    );
                    CREATE INDEX IF NOT EXISTS ix_runs_started_at ON runs (started_at DESC);
                    CREATE INDEX IF NOT EXISTS ix_runs_status      ON runs (status);
                    """;
                await using (var cmd = new NpgsqlCommand(runsSql, connection))
                    await cmd.ExecuteNonQueryAsync(ct);
                results.Add("✅ runs table ready");

                // 3. item_run_stats – one row per item per run (scoring result)
                const string statsSql = """
                    CREATE TABLE IF NOT EXISTS item_run_stats (
                        id                   BIGSERIAL PRIMARY KEY,
                        run_id               BIGINT NOT NULL REFERENCES runs(run_id) ON DELETE CASCADE,
                        item_id              BIGINT NOT NULL REFERENCES items(item_id) ON DELETE CASCADE,
                        rank                 INT,
                        final_score          NUMERIC(10,6),
                        base_score           NUMERIC(10,6),
                        momentum_raw         NUMERIC(10,6),
                        momentum_normalized  NUMERIC(10,6),
                        appearance_count     INT NOT NULL DEFAULT 1,
                        source_count         INT NOT NULL DEFAULT 1,
                        market_count         INT NOT NULL DEFAULT 1,
                        score_components     JSONB,
                        markets              JSONB,
                        sources              JSONB,
                        min_price            NUMERIC(12,2),
                        max_price            NUMERIC(12,2),
                        prev_final_score     NUMERIC(10,6),
                        total_run_appearances INT NOT NULL DEFAULT 1,
                        UNIQUE (run_id, item_id)
                    );
                    CREATE INDEX IF NOT EXISTS ix_irs_run_id   ON item_run_stats (run_id);
                    CREATE INDEX IF NOT EXISTS ix_irs_item_id  ON item_run_stats (item_id);
                    CREATE INDEX IF NOT EXISTS ix_irs_rank     ON item_run_stats (run_id, rank);
                    """;
                await using (var cmd = new NpgsqlCommand(statsSql, connection))
                    await cmd.ExecuteNonQueryAsync(ct);
                results.Add("✅ item_run_stats table ready");

                // 4. item_appearances – raw scraper hit per run
                const string appearancesSql = """
                    CREATE TABLE IF NOT EXISTS item_appearances (
                        id         BIGSERIAL PRIMARY KEY,
                        run_id     BIGINT NOT NULL REFERENCES runs(run_id) ON DELETE CASCADE,
                        item_id    BIGINT NOT NULL REFERENCES items(item_id) ON DELETE CASCADE,
                        source     TEXT,
                        market     TEXT,
                        price      NUMERIC(12,2),
                        position   INT,
                        scraped_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                        raw_data   JSONB
                    );
                    CREATE INDEX IF NOT EXISTS ix_ia_run_id  ON item_appearances (run_id);
                    CREATE INDEX IF NOT EXISTS ix_ia_item_id ON item_appearances (item_id);
                    """;
                await using (var cmd = new NpgsqlCommand(appearancesSql, connection))
                    await cmd.ExecuteNonQueryAsync(ct);
                results.Add("✅ item_appearances table ready");

                // 5. v_item_run_stats – enriched view used by Python dashboard endpoint
                const string viewSql = """
                    CREATE OR REPLACE VIEW v_item_run_stats AS
                    SELECT
                        irs.run_id,
                        irs.rank,
                        irs.item_id,
                        i.brand,
                        i.name,
                        i.category,
                        NULL::text                AS image_url,
                        irs.final_score,
                        irs.base_score,
                        irs.momentum_raw,
                        irs.momentum_normalized,
                        irs.appearance_count,
                        irs.source_count,
                        irs.market_count,
                        irs.score_components,
                        irs.markets,
                        irs.sources,
                        irs.min_price,
                        irs.max_price,
                        irs.prev_final_score,
                        irs.total_run_appearances,
                        i.canonical_key
                    FROM item_run_stats irs
                    JOIN items i ON i.item_id = irs.item_id;
                    """;
                await using (var cmd = new NpgsqlCommand(viewSql, connection))
                    await cmd.ExecuteNonQueryAsync(ct);
                results.Add("✅ v_item_run_stats view ready");

                logger.LogInformation("✅ Scoring tables initialized successfully");
                return Results.Ok(new { success = true, message = "Scoring tables initialized", results });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Failed to init scoring tables");
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Failed to init scoring tables");
            }
        })
        .WithName("InitScoringTables")
        .WithTags("Analytics", "Admin");

        app.MapGet("/api/analytics/health", async (IAnalyticsDbContext db, ILogger<Program> logger) =>
        {
            try
            {
                var salesCount = await db.SalesFacts.CountAsync();
                var linesCount = await db.SalesLineFacts.CountAsync();
                var productsCount = await db.ProductsDim.CountAsync();

                return Results.Ok(new
                {
                    status = "OK",
                    tables = new
                    {
                        salesFacts = salesCount,
                        salesLineFacts = linesCount,
                        productsDim = productsCount
                    },
                    message = "Analytics database connection successful"
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Analytics health check failed");
                return Results.Problem(detail: $"{ex.GetType().Name}: {ex.Message}", statusCode: 500, title: "Analytics database error");
            }
        });

        app.MapGet("/api/analytics/vendor-sales-nivelacija/options", async (
            TrendplusDbContext trendplusDb,
            int? vendorId = null,
            string? category = null,
            int take = 200,
            CancellationToken ct = default) =>
        {
            try
            {
                var connectionString = trendplusDb.Database.GetConnectionString();
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    return Results.Problem(
                        title: "Missing database connection",
                        detail: "Trendplus connection string is missing.",
                        statusCode: 500);
                }

                take = Math.Clamp(take, 10, 1000);

                var options = new List<VendorSalesNivelacijaOptionDto>();

                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(ct);

                var hasOldPrice = await RelationHasColumnAsync(connection, "vw_vendor_sales_nivelacija", "old_price", ct);
                var hasNewPrice = await RelationHasColumnAsync(connection, "vw_vendor_sales_nivelacija", "new_price", ct);
                var hasPriceColumns = hasOldPrice && hasNewPrice;

                var sql = hasPriceColumns
                    ? """
                        WITH ranked AS (
                            SELECT
                                event_date::date AS event_date,
                                vendor_id,
                                article_id,
                                old_price,
                                new_price,
                                pre_qty,
                                pre_revenue,
                                post_qty,
                                post_revenue,
                                ROW_NUMBER() OVER (
                                    PARTITION BY
                                        event_date::date,
                                        COALESCE(vendor_id, -1),
                                        article_id,
                                        old_price,
                                        new_price
                                    ORDER BY price_event_id DESC
                                ) AS rn
                            FROM "vw_vendor_sales_nivelacija"
                            WHERE (@vendorId IS NULL OR vendor_id = @vendorId::int)
                              AND (@category IS NULL OR category ILIKE @categoryPattern::text)
                        )
                        SELECT
                            event_date,
                            COUNT(*)::INT AS events_count,
                            COUNT(DISTINCT vendor_id)::INT AS vendors_count,
                            COUNT(DISTINCT article_id)::INT AS articles_count,
                            COUNT(*) FILTER (
                                WHERE pre_qty <> 0
                                   OR post_qty <> 0
                                   OR pre_revenue <> 0
                                   OR post_revenue <> 0
                            )::INT AS active_articles_count
                        FROM ranked
                        WHERE rn = 1
                        GROUP BY event_date
                        ORDER BY event_date DESC
                        LIMIT @take;
                        """
                    : """
                        WITH ranked AS (
                            SELECT
                                event_date::date AS event_date,
                                vendor_id,
                                article_id,
                                pre_qty,
                                pre_revenue,
                                post_qty,
                                post_revenue,
                                ROW_NUMBER() OVER (
                                    PARTITION BY
                                        event_date::date,
                                        COALESCE(vendor_id, -1),
                                        article_id
                                    ORDER BY price_event_id DESC
                                ) AS rn
                            FROM "vw_vendor_sales_nivelacija"
                            WHERE (@vendorId IS NULL OR vendor_id = @vendorId::int)
                              AND (@category IS NULL OR category ILIKE @categoryPattern::text)
                        )
                        SELECT
                            event_date,
                            COUNT(*)::INT AS events_count,
                            COUNT(DISTINCT vendor_id)::INT AS vendors_count,
                            COUNT(DISTINCT article_id)::INT AS articles_count,
                            COUNT(*) FILTER (
                                WHERE pre_qty <> 0
                                   OR post_qty <> 0
                                   OR pre_revenue <> 0
                                   OR post_revenue <> 0
                            )::INT AS active_articles_count
                        FROM ranked
                        WHERE rn = 1
                        GROUP BY event_date
                        ORDER BY event_date DESC
                        LIMIT @take::int;
                        """;

                await using var command = new NpgsqlCommand(sql, connection);

                var vendorIdParam = new NpgsqlParameter("vendorId", NpgsqlTypes.NpgsqlDbType.Integer)
                {
                    Value = (object?)vendorId ?? DBNull.Value
                };
                command.Parameters.Add(vendorIdParam);

                var categoryParam = new NpgsqlParameter("category", NpgsqlTypes.NpgsqlDbType.Text)
                {
                    Value = (object?)category ?? DBNull.Value
                };
                command.Parameters.Add(categoryParam);

                var categoryPatternParam = new NpgsqlParameter("categoryPattern", NpgsqlTypes.NpgsqlDbType.Text)
                {
                    Value = string.IsNullOrWhiteSpace(category)
                        ? DBNull.Value
                        : $"%{category.Trim()}%"
                };
                command.Parameters.Add(categoryPatternParam);

                var takeParam = new NpgsqlParameter("take", NpgsqlTypes.NpgsqlDbType.Integer)
                {
                    Value = take
                };
                command.Parameters.Add(takeParam);

                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    options.Add(new VendorSalesNivelacijaOptionDto
                    {
                        EventDate = DateTime.SpecifyKind(reader.GetDateTime(0), DateTimeKind.Utc),
                        EventsCount = reader.GetInt32(1),
                        VendorsCount = reader.GetInt32(2),
                        ArticlesCount = reader.GetInt32(3),
                        ActiveArticlesCount = reader.GetInt32(4),
                        HasSalesWindow = reader.GetInt32(4) > 0,
                        Label = $"{reader.GetDateTime(0):dd.MM.yyyy} - aktivni {reader.GetInt32(4)}/{reader.GetInt32(3)} artikala / {reader.GetInt32(2)} dobavljaca"
                    });
                }

                return Results.Ok(options);
            }
            catch (PostgresException ex) when (ex.SqlState == "42P01" || ex.SqlState == "42703")
            {
                return Results.Problem(
                    title: "Nivelacija view schema mismatch",
                    detail: "Run DB migration scripts 013_AddVendorSalesNivelacijaViews.sql, 014_FixNivelacijaViewsFromDnevnik.sql, and 016_AnalyticsNivelacijaEnhancements.sql, then restart the backend.",
                    statusCode: 500);
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    title: "Failed to load nivelacija options",
                    detail: ex.Message,
                    statusCode: 500);
            }
        })
        .WithName("GetVendorSalesNivelacijaOptions")
        .WithTags("Analytics")
        .RequireRateLimiting("analytics");

        app.MapGet("/api/analytics/supplier-sales-stats", async (
            TrendplusDbContext db,
            int? sezonaId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? storeId = null,
            CancellationToken ct = default) =>
        {
            try
            {
                static DateTime? NormalizeUtc(DateTime? value)
                {
                    if (!value.HasValue) return null;
                    var date = value.Value;
                    return date.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(date, DateTimeKind.Utc)
                        : date.ToUniversalTime();
                }

                DateTime? fromUtc = NormalizeUtc(fromDate);
                DateTime? toUtc = NormalizeUtc(toDate);

                if (sezonaId.HasValue)
                {
                    var sezona = await db.Sezone.AsNoTracking()
                        .Where(s => s.Id == sezonaId.Value)
                        .Select(s => new { s.DatumOd, s.DatumDo, s.Naziv })
                        .FirstOrDefaultAsync(ct);

                    if (sezona is null)
                    {
                        return Results.NotFound(new { message = $"Sezona {sezonaId.Value} nije pronađena." });
                    }

                    fromUtc = DateTime.SpecifyKind(sezona.DatumOd.Date, DateTimeKind.Utc);
                    toUtc = DateTime.SpecifyKind(sezona.DatumDo.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
                }

                if (!fromUtc.HasValue && !toUtc.HasValue)
                {
                    var todayUtc = DateTime.UtcNow.Date;
                    fromUtc = todayUtc.AddDays(-89);
                    toUtc = todayUtc.AddDays(1).AddTicks(-1);
                }

                if (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value > toUtc.Value)
                {
                    return Results.BadRequest(new
                    {
                        message = "Neispravan period: fromDate mora biti manji ili jednak toDate.",
                        fromDate = fromUtc.Value,
                        toDate = toUtc.Value
                    });
                }

                var nivelacije = await db.DnevnikPromena.AsNoTracking()
                    .Where(d =>
                        (d.TipPromene == TipPromeneConstants.Nivelacija || d.TipPromene == TipPromeneConstants.NivelacijaCena) &&
                        d.ArtikalId.HasValue &&
                        (!fromUtc.HasValue || d.Datum >= fromUtc.Value) &&
                        (!toUtc.HasValue || d.Datum <= toUtc.Value))
                    .Select(d => new
                    {
                        ArtikalId = d.ArtikalId!.Value,
                        DatumNivelacije = d.Datum,
                        d.StaraProdajnaCena,
                        d.NovaProdajnaCena
                    })
                    .ToListAsync(ct);

                var prvaNivelacijaPoArtiklu = nivelacije
                    .GroupBy(n => n.ArtikalId)
                    .ToDictionary(g => g.Key, g => g.Min(x => x.DatumNivelacije));

                var stavke = await (
                    from ps in db.ProdajaStavke.AsNoTracking()
                    join pz in db.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals pz.Id
                    join a in db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                    join d in db.Dobavljaci.AsNoTracking() on a.IDDobavljac equals d.Id into dj
                    from d in dj.DefaultIfEmpty()
                    where (!fromUtc.HasValue || pz.DatumProdaje >= fromUtc.Value)
                       && (!toUtc.HasValue || pz.DatumProdaje <= toUtc.Value)
                       && (!storeId.HasValue || pz.IDObjekat == storeId.Value)
                    select new
                    {
                        DobavljacId = d != null ? d.Id : (int?)null,
                        DobavljacNaziv = d != null ? d.Naziv : "Nepoznato",
                        ArtikalId = a.Id,
                        ps.Kolicina,
                        ps.Cena,
                        Prihod = ps.Kolicina * ps.Cena,
                        NabavnaCena = ps.NabavnaCena ?? a.NabavnaCena,
                        DatumProdaje = pz.DatumProdaje
                    })
                    .ToListAsync(ct);

                var suppliers = stavke
                    .GroupBy(s => new { s.DobavljacId, s.DobavljacNaziv })
                    .Select(g =>
                    {
                        decimal preNivRevenue = 0m;
                        decimal postNivRevenue = 0m;
                        decimal totalRevenue = 0m;
                        int preNivQty = 0;
                        int postNivQty = 0;
                        int totalQty = 0;
                        decimal totalCost = 0m;
                        decimal revenueWithCost = 0m;

                        var articleIds = new HashSet<int>();
                        var articleIdsWithNivelacija = new HashSet<int>();

                        foreach (var s in g)
                        {
                            totalRevenue += s.Prihod;
                            totalQty += s.Kolicina;
                            articleIds.Add(s.ArtikalId);

                            if (s.NabavnaCena.HasValue)
                            {
                                totalCost += s.Kolicina * s.NabavnaCena.Value;
                                revenueWithCost += s.Prihod;
                            }

                            if (!prvaNivelacijaPoArtiklu.TryGetValue(s.ArtikalId, out var nivDatum))
                            {
                                continue;
                            }

                            articleIdsWithNivelacija.Add(s.ArtikalId);

                            if (s.DatumProdaje < nivDatum)
                            {
                                preNivRevenue += s.Prihod;
                                preNivQty += s.Kolicina;
                            }
                            else
                            {
                                postNivRevenue += s.Prihod;
                                postNivQty += s.Kolicina;
                            }
                        }

                        var marginPct = revenueWithCost > 0m
                            ? (double)((revenueWithCost - totalCost) / revenueWithCost * 100m)
                            : 0d;

                        return new
                        {
                            dobavljacId = g.Key.DobavljacId,
                            dobavljacNaziv = string.IsNullOrWhiteSpace(g.Key.DobavljacNaziv) ? "Nepoznato" : g.Key.DobavljacNaziv,
                            preNivelacijePromet = Math.Round(preNivRevenue, 2),
                            preNivelacijeKolicina = preNivQty,
                            posleNivelacijePromet = Math.Round(postNivRevenue, 2),
                            posleNivelacijeKolicina = postNivQty,
                            ukupanPromet = Math.Round(totalRevenue, 2),
                            ukupnaKolicina = totalQty,
                            brojArtikalaSaNivelacijom = articleIdsWithNivelacija.Count,
                            brojArtikalaUkupno = articleIds.Count,
                            marginPct = Math.Round(marginPct, 2),
                            promenaPrometa = preNivRevenue > 0m
                                ? Math.Round((double)((postNivRevenue - preNivRevenue) / preNivRevenue * 100m), 2)
                                : (double?)null,
                            promenaKolicine = preNivQty > 0
                                ? Math.Round((postNivQty - preNivQty) / (double)preNivQty * 100d, 2)
                                : (double?)null
                        };
                    })
                    .OrderByDescending(x => x.ukupanPromet)
                    .ToList();

                var sumPreRevenue = suppliers.Sum(r => r.preNivelacijePromet);
                var sumPostRevenue = suppliers.Sum(r => r.posleNivelacijePromet);

                var totals = new
                {
                    ukupanPromet = suppliers.Sum(r => r.ukupanPromet),
                    prePromet = sumPreRevenue,
                    poslePromet = sumPostRevenue,
                    ukupnaKolicina = suppliers.Sum(r => r.ukupnaKolicina),
                    preKolicina = suppliers.Sum(r => r.preNivelacijeKolicina),
                    posleKolicina = suppliers.Sum(r => r.posleNivelacijeKolicina),
                    brojDobavljaca = suppliers.Count,
                    promenaPrometaPct = sumPreRevenue > 0m
                        ? Math.Round((double)((sumPostRevenue - sumPreRevenue) / sumPreRevenue * 100m), 2)
                        : (double?)null
                };

                var sezone = (await db.Sezone.AsNoTracking()
                    .OrderByDescending(s => s.DatumOd)
                    .Select(s => new
                    {
                        s.Id,
                        s.Naziv,
                        s.DatumOd,
                        s.DatumDo
                    })
                    .ToListAsync(ct))
                    .Select(s => new
                    {
                        id = s.Id,
                        naziv = s.Naziv,
                        datumOd = DateTime.SpecifyKind(s.DatumOd.Date, DateTimeKind.Utc),
                        datumDo = DateTime.SpecifyKind(s.DatumDo.Date, DateTimeKind.Utc)
                    })
                    .ToList();

                return Results.Ok(new
                {
                    generatedAt = DateTime.UtcNow,
                    fromDate = fromUtc,
                    toDate = toUtc,
                    sezonaId,
                    storeId,
                    suppliers,
                    totals,
                    sezone
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    title: "Greška pri učitavanju statistike prodaje po dobavljačima",
                    detail: ex.Message,
                    statusCode: 500);
            }
        })
        .WithName("GetSupplierSalesStats")
        .WithTags("Analytics")
        .RequireRateLimiting("analytics");

        app.MapGet("/api/analytics/shoe-type-sales-stats", async (
            TrendplusDbContext db,
            int? sezonaId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? storeId = null,
            CancellationToken ct = default) =>
        {
            try
            {
                static DateTime? NormalizeUtc(DateTime? value)
                {
                    if (!value.HasValue) return null;
                    var date = value.Value;
                    return date.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(date, DateTimeKind.Utc)
                        : date.ToUniversalTime();
                }

                DateTime? fromUtc = NormalizeUtc(fromDate);
                DateTime? toUtc = NormalizeUtc(toDate);

                if (sezonaId.HasValue)
                {
                    var sezona = await db.Sezone.AsNoTracking()
                        .Where(s => s.Id == sezonaId.Value)
                        .Select(s => new { s.DatumOd, s.DatumDo, s.Naziv })
                        .FirstOrDefaultAsync(ct);

                    if (sezona is null)
                    {
                        return Results.NotFound(new { message = $"Sezona {sezonaId.Value} nije pronađena." });
                    }

                    fromUtc = DateTime.SpecifyKind(sezona.DatumOd.Date, DateTimeKind.Utc);
                    toUtc = DateTime.SpecifyKind(sezona.DatumDo.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
                }

                if (!fromUtc.HasValue && !toUtc.HasValue)
                {
                    var todayUtc = DateTime.UtcNow.Date;
                    fromUtc = todayUtc.AddDays(-89);
                    toUtc = todayUtc.AddDays(1).AddTicks(-1);
                }

                if (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value > toUtc.Value)
                {
                    return Results.BadRequest(new
                    {
                        message = "Neispravan period: fromDate mora biti manji ili jednak toDate.",
                        fromDate = fromUtc.Value,
                        toDate = toUtc.Value
                    });
                }

                var nivelacije = await db.DnevnikPromena.AsNoTracking()
                    .Where(d =>
                        (d.TipPromene == TipPromeneConstants.Nivelacija || d.TipPromene == TipPromeneConstants.NivelacijaCena) &&
                        d.ArtikalId.HasValue &&
                        (!fromUtc.HasValue || d.Datum >= fromUtc.Value) &&
                        (!toUtc.HasValue || d.Datum <= toUtc.Value))
                    .Select(d => new
                    {
                        ArtikalId = d.ArtikalId!.Value,
                        DatumNivelacije = d.Datum,
                        d.StaraProdajnaCena,
                        d.NovaProdajnaCena
                    })
                    .ToListAsync(ct);

                var prvaNivelacijaPoArtiklu = nivelacije
                    .GroupBy(n => n.ArtikalId)
                    .ToDictionary(g => g.Key, g => g.Min(x => x.DatumNivelacije));

                var stavke = await (
                    from ps in db.ProdajaStavke.AsNoTracking()
                    join pz in db.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals pz.Id
                    join a in db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                    join t in db.TipoviObuce.AsNoTracking() on a.IDTipObuce equals t.Id into tj
                    from t in tj.DefaultIfEmpty()
                    where (!fromUtc.HasValue || pz.DatumProdaje >= fromUtc.Value)
                       && (!toUtc.HasValue || pz.DatumProdaje <= toUtc.Value)
                       && (!storeId.HasValue || pz.IDObjekat == storeId.Value)
                    select new
                    {
                        TipObuceId = t != null ? t.Id : (int?)null,
                        TipObuceNaziv = t != null ? t.Naziv : "Nepoznato",
                        ArtikalId = a.Id,
                        ps.Kolicina,
                        ps.Cena,
                        Prihod = ps.Kolicina * ps.Cena,
                        NabavnaCena = ps.NabavnaCena ?? a.NabavnaCena,
                        DatumProdaje = pz.DatumProdaje
                    })
                    .ToListAsync(ct);

                var shoeTypes = stavke
                    .GroupBy(s => new { s.TipObuceId, s.TipObuceNaziv })
                    .Select(g =>
                    {
                        decimal preNivRevenue = 0m;
                        decimal postNivRevenue = 0m;
                        decimal totalRevenue = 0m;
                        int preNivQty = 0;
                        int postNivQty = 0;
                        int totalQty = 0;
                        decimal totalCost = 0m;
                        decimal revenueWithCost = 0m;

                        var articleIds = new HashSet<int>();
                        var articleIdsWithNivelacija = new HashSet<int>();

                        foreach (var s in g)
                        {
                            totalRevenue += s.Prihod;
                            totalQty += s.Kolicina;
                            articleIds.Add(s.ArtikalId);

                            if (s.NabavnaCena.HasValue)
                            {
                                totalCost += s.Kolicina * s.NabavnaCena.Value;
                                revenueWithCost += s.Prihod;
                            }

                            if (!prvaNivelacijaPoArtiklu.TryGetValue(s.ArtikalId, out var nivDatum))
                            {
                                continue;
                            }

                            articleIdsWithNivelacija.Add(s.ArtikalId);

                            if (s.DatumProdaje < nivDatum)
                            {
                                preNivRevenue += s.Prihod;
                                preNivQty += s.Kolicina;
                            }
                            else
                            {
                                postNivRevenue += s.Prihod;
                                postNivQty += s.Kolicina;
                            }
                        }

                        var marginPct = revenueWithCost > 0m
                            ? (double)((revenueWithCost - totalCost) / revenueWithCost * 100m)
                            : 0d;

                        return new
                        {
                            tipObuceId = g.Key.TipObuceId,
                            tipObuceNaziv = string.IsNullOrWhiteSpace(g.Key.TipObuceNaziv) ? "Nepoznato" : g.Key.TipObuceNaziv,
                            preNivelacijePromet = Math.Round(preNivRevenue, 2),
                            preNivelacijeKolicina = preNivQty,
                            posleNivelacijePromet = Math.Round(postNivRevenue, 2),
                            posleNivelacijeKolicina = postNivQty,
                            ukupanPromet = Math.Round(totalRevenue, 2),
                            ukupnaKolicina = totalQty,
                            brojArtikalaSaNivelacijom = articleIdsWithNivelacija.Count,
                            brojArtikalaUkupno = articleIds.Count,
                            marginPct = Math.Round(marginPct, 2),
                            promenaPrometa = preNivRevenue > 0m
                                ? Math.Round((double)((postNivRevenue - preNivRevenue) / preNivRevenue * 100m), 2)
                                : (double?)null,
                            promenaKolicine = preNivQty > 0
                                ? Math.Round((postNivQty - preNivQty) / (double)preNivQty * 100d, 2)
                                : (double?)null
                        };
                    })
                    .OrderByDescending(x => x.ukupanPromet)
                    .ToList();

                var sumPreRevenue = shoeTypes.Sum(r => r.preNivelacijePromet);
                var sumPostRevenue = shoeTypes.Sum(r => r.posleNivelacijePromet);

                var totals = new
                {
                    ukupanPromet = shoeTypes.Sum(r => r.ukupanPromet),
                    prePromet = sumPreRevenue,
                    poslePromet = sumPostRevenue,
                    ukupnaKolicina = shoeTypes.Sum(r => r.ukupnaKolicina),
                    preKolicina = shoeTypes.Sum(r => r.preNivelacijeKolicina),
                    posleKolicina = shoeTypes.Sum(r => r.posleNivelacijeKolicina),
                    brojTipovaObuce = shoeTypes.Count,
                    promenaPrometaPct = sumPreRevenue > 0m
                        ? Math.Round((double)((sumPostRevenue - sumPreRevenue) / sumPreRevenue * 100m), 2)
                        : (double?)null
                };

                var sezone = (await db.Sezone.AsNoTracking()
                    .OrderByDescending(s => s.DatumOd)
                    .Select(s => new
                    {
                        s.Id,
                        s.Naziv,
                        s.DatumOd,
                        s.DatumDo
                    })
                    .ToListAsync(ct))
                    .Select(s => new
                    {
                        id = s.Id,
                        naziv = s.Naziv,
                        datumOd = DateTime.SpecifyKind(s.DatumOd.Date, DateTimeKind.Utc),
                        datumDo = DateTime.SpecifyKind(s.DatumDo.Date, DateTimeKind.Utc)
                    })
                    .ToList();

                return Results.Ok(new
                {
                    generatedAt = DateTime.UtcNow,
                    fromDate = fromUtc,
                    toDate = toUtc,
                    sezonaId,
                    storeId,
                    shoeTypes,
                    totals,
                    sezone
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    title: "Greska pri ucitavanju statistike prodaje po tipu obuce",
                    detail: ex.Message,
                    statusCode: 500);
            }
        })
        .WithName("GetShoeTypeSalesStats")
        .WithTags("Analytics")
        .RequireRateLimiting("analytics");

        app.MapGet("/api/analytics/color-sales-stats", async (
            TrendplusDbContext db,
            int? sezonaId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? storeId = null,
            CancellationToken ct = default) =>
        {
            try
            {
                static DateTime? NormalizeUtc(DateTime? value)
                {
                    if (!value.HasValue) return null;
                    var date = value.Value;
                    return date.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(date, DateTimeKind.Utc)
                        : date.ToUniversalTime();
                }

                static string NormalizeColor(string? value) =>
                    string.IsNullOrWhiteSpace(value) ? "Nepoznato" : value.Trim();

                DateTime? fromUtc = NormalizeUtc(fromDate);
                DateTime? toUtc = NormalizeUtc(toDate);

                if (sezonaId.HasValue)
                {
                    var sezona = await db.Sezone.AsNoTracking()
                        .Where(s => s.Id == sezonaId.Value)
                        .Select(s => new { s.DatumOd, s.DatumDo, s.Naziv })
                        .FirstOrDefaultAsync(ct);

                    if (sezona is null)
                    {
                        return Results.NotFound(new { message = $"Sezona {sezonaId.Value} nije pronađena." });
                    }

                    fromUtc = DateTime.SpecifyKind(sezona.DatumOd.Date, DateTimeKind.Utc);
                    toUtc = DateTime.SpecifyKind(sezona.DatumDo.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
                }

                if (!fromUtc.HasValue && !toUtc.HasValue)
                {
                    var todayUtc = DateTime.UtcNow.Date;
                    fromUtc = todayUtc.AddDays(-89);
                    toUtc = todayUtc.AddDays(1).AddTicks(-1);
                }

                if (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value > toUtc.Value)
                {
                    return Results.BadRequest(new
                    {
                        message = "Neispravan period: fromDate mora biti manji ili jednak toDate.",
                        fromDate = fromUtc.Value,
                        toDate = toUtc.Value
                    });
                }

                var nivelacije = await db.DnevnikPromena.AsNoTracking()
                    .Where(d =>
                        (d.TipPromene == TipPromeneConstants.Nivelacija || d.TipPromene == TipPromeneConstants.NivelacijaCena) &&
                        d.ArtikalId.HasValue &&
                        (!fromUtc.HasValue || d.Datum >= fromUtc.Value) &&
                        (!toUtc.HasValue || d.Datum <= toUtc.Value))
                    .Select(d => new
                    {
                        ArtikalId = d.ArtikalId!.Value,
                        DatumNivelacije = d.Datum,
                        d.StaraProdajnaCena,
                        d.NovaProdajnaCena
                    })
                    .ToListAsync(ct);

                var prvaNivelacijaPoArtiklu = nivelacije
                    .GroupBy(n => n.ArtikalId)
                    .ToDictionary(g => g.Key, g => g.Min(x => x.DatumNivelacije));

                var stavke = await (
                    from ps in db.ProdajaStavke.AsNoTracking()
                    join pz in db.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals pz.Id
                    join a in db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                    where (!fromUtc.HasValue || pz.DatumProdaje >= fromUtc.Value)
                       && (!toUtc.HasValue || pz.DatumProdaje <= toUtc.Value)
                       && (!storeId.HasValue || pz.IDObjekat == storeId.Value)
                    select new
                    {
                        Boja = a.Boja,
                        ArtikalId = a.Id,
                        ps.Kolicina,
                        ps.Cena,
                        Prihod = ps.Kolicina * ps.Cena,
                        NabavnaCena = ps.NabavnaCena ?? a.NabavnaCena,
                        DatumProdaje = pz.DatumProdaje
                    })
                    .ToListAsync(ct);

                var colors = stavke
                    .GroupBy(s => NormalizeColor(s.Boja))
                    .Select(g =>
                    {
                        decimal preNivRevenue = 0m;
                        decimal postNivRevenue = 0m;
                        decimal totalRevenue = 0m;
                        int preNivQty = 0;
                        int postNivQty = 0;
                        int totalQty = 0;
                        decimal totalCost = 0m;
                        decimal revenueWithCost = 0m;

                        var articleIds = new HashSet<int>();
                        var articleIdsWithNivelacija = new HashSet<int>();

                        foreach (var s in g)
                        {
                            totalRevenue += s.Prihod;
                            totalQty += s.Kolicina;
                            articleIds.Add(s.ArtikalId);

                            if (s.NabavnaCena.HasValue)
                            {
                                totalCost += s.Kolicina * s.NabavnaCena.Value;
                                revenueWithCost += s.Prihod;
                            }

                            if (!prvaNivelacijaPoArtiklu.TryGetValue(s.ArtikalId, out var nivDatum))
                            {
                                continue;
                            }

                            articleIdsWithNivelacija.Add(s.ArtikalId);

                            if (s.DatumProdaje < nivDatum)
                            {
                                preNivRevenue += s.Prihod;
                                preNivQty += s.Kolicina;
                            }
                            else
                            {
                                postNivRevenue += s.Prihod;
                                postNivQty += s.Kolicina;
                            }
                        }

                        var marginPct = revenueWithCost > 0m
                            ? (double)((revenueWithCost - totalCost) / revenueWithCost * 100m)
                            : 0d;

                        return new
                        {
                            boja = g.Key,
                            preNivelacijePromet = Math.Round(preNivRevenue, 2),
                            preNivelacijeKolicina = preNivQty,
                            posleNivelacijePromet = Math.Round(postNivRevenue, 2),
                            posleNivelacijeKolicina = postNivQty,
                            ukupanPromet = Math.Round(totalRevenue, 2),
                            ukupnaKolicina = totalQty,
                            brojArtikalaSaNivelacijom = articleIdsWithNivelacija.Count,
                            brojArtikalaUkupno = articleIds.Count,
                            marginPct = Math.Round(marginPct, 2),
                            promenaPrometa = preNivRevenue > 0m
                                ? Math.Round((double)((postNivRevenue - preNivRevenue) / preNivRevenue * 100m), 2)
                                : (double?)null,
                            promenaKolicine = preNivQty > 0
                                ? Math.Round((postNivQty - preNivQty) / (double)preNivQty * 100d, 2)
                                : (double?)null
                        };
                    })
                    .OrderByDescending(x => x.ukupanPromet)
                    .ToList();

                var sumPreRevenue = colors.Sum(r => r.preNivelacijePromet);
                var sumPostRevenue = colors.Sum(r => r.posleNivelacijePromet);

                var totals = new
                {
                    ukupanPromet = colors.Sum(r => r.ukupanPromet),
                    prePromet = sumPreRevenue,
                    poslePromet = sumPostRevenue,
                    ukupnaKolicina = colors.Sum(r => r.ukupnaKolicina),
                    preKolicina = colors.Sum(r => r.preNivelacijeKolicina),
                    posleKolicina = colors.Sum(r => r.posleNivelacijeKolicina),
                    brojBoja = colors.Count,
                    promenaPrometaPct = sumPreRevenue > 0m
                        ? Math.Round((double)((sumPostRevenue - sumPreRevenue) / sumPreRevenue * 100m), 2)
                        : (double?)null
                };

                var sezone = (await db.Sezone.AsNoTracking()
                    .OrderByDescending(s => s.DatumOd)
                    .Select(s => new
                    {
                        s.Id,
                        s.Naziv,
                        s.DatumOd,
                        s.DatumDo
                    })
                    .ToListAsync(ct))
                    .Select(s => new
                    {
                        id = s.Id,
                        naziv = s.Naziv,
                        datumOd = DateTime.SpecifyKind(s.DatumOd.Date, DateTimeKind.Utc),
                        datumDo = DateTime.SpecifyKind(s.DatumDo.Date, DateTimeKind.Utc)
                    })
                    .ToList();

                return Results.Ok(new
                {
                    generatedAt = DateTime.UtcNow,
                    fromDate = fromUtc,
                    toDate = toUtc,
                    sezonaId,
                    storeId,
                    colors,
                    totals,
                    sezone
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    title: "Greska pri ucitavanju statistike prodaje po boji artikla",
                    detail: ex.Message,
                    statusCode: 500);
            }
        })
        .WithName("GetColorSalesStats")
        .WithTags("Analytics")
        .RequireRateLimiting("analytics");

        app.MapGet("/api/analytics/vendor-sales-nivelacija", async (
            TrendplusDbContext trendplusDb,
            int? vendorId = null,
            DateTime? eventDate = null,
            DateTime? from = null,
            DateTime? to = null,
            string? category = null,
            bool includeInactive = false,
            CancellationToken ct = default) =>
        {
            try
            {
                var connectionString = trendplusDb.Database.GetConnectionString();
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    return Results.Problem(
                        title: "Missing database connection",
                        detail: "Trendplus connection string is missing.",
                        statusCode: 500);
                }

                var eventDateOnly = eventDate?.Date;
                var fromDateOnly = from?.ToUniversalTime().Date;
                var toDateOnly = to?.ToUniversalTime().Date;

                if (fromDateOnly.HasValue && toDateOnly.HasValue && fromDateOnly.Value > toDateOnly.Value)
                {
                    return Results.BadRequest(new
                    {
                        message = "Invalid date range: from must be <= to",
                        from = fromDateOnly.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        to = toDateOnly.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    });
                }

                var categoryTrimmed = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
                var categoryPattern = string.IsNullOrWhiteSpace(categoryTrimmed) ? null : $"%{categoryTrimmed}%";

                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(ct);

                var hasOldPrice = await RelationHasColumnAsync(connection, "vw_vendor_sales_nivelacija", "old_price", ct);
                var hasNewPrice = await RelationHasColumnAsync(connection, "vw_vendor_sales_nivelacija", "new_price", ct);
                var hasPriceColumns = hasOldPrice && hasNewPrice;

                var hasRevenuePercent = await RelationHasColumnAsync(connection, "vw_vendor_sales_nivelacija", "change_percent_revenue", ct);
                var changePercentExpr = hasRevenuePercent ? "change_percent_revenue" : "change_percent_qty";

                const string rawCountSql = """
                    SELECT COUNT(*)::INT
                    FROM "vw_vendor_sales_nivelacija"
                    WHERE (@vendorId IS NULL OR vendor_id = @vendorId::int)
                      AND (@eventDate IS NULL OR event_date::date = @eventDate::date)
                      AND (@fromDate IS NULL OR event_date::date >= @fromDate::date)
                      AND (@toDate IS NULL OR event_date::date <= @toDate::date)
                      AND (@category IS NULL OR category ILIKE @categoryPattern::text);
                    """;

                var rawRows = 0;
                await using (var cmd = new NpgsqlCommand(rawCountSql, connection))
                {
                    cmd.Parameters.Add(new NpgsqlParameter("vendorId", NpgsqlTypes.NpgsqlDbType.Integer)
                    {
                        Value = (object?)vendorId ?? DBNull.Value
                    });

                    cmd.Parameters.Add(new NpgsqlParameter("eventDate", NpgsqlTypes.NpgsqlDbType.Date)
                    {
                        Value = (object?)eventDateOnly ?? DBNull.Value
                    });

                    cmd.Parameters.Add(new NpgsqlParameter("fromDate", NpgsqlTypes.NpgsqlDbType.Date)
                    {
                        Value = (object?)fromDateOnly ?? DBNull.Value
                    });

                    cmd.Parameters.Add(new NpgsqlParameter("toDate", NpgsqlTypes.NpgsqlDbType.Date)
                    {
                        Value = (object?)toDateOnly ?? DBNull.Value
                    });

                    cmd.Parameters.Add(new NpgsqlParameter("category", NpgsqlTypes.NpgsqlDbType.Text)
                    {
                        Value = (object?)categoryTrimmed ?? DBNull.Value
                    });

                    cmd.Parameters.Add(new NpgsqlParameter("categoryPattern", NpgsqlTypes.NpgsqlDbType.Text)
                    {
                        Value = (object?)categoryPattern ?? DBNull.Value
                    });

                    rawRows = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
                }

                var categories = new List<string>();
                const string categoriesSql = """
                    SELECT DISTINCT COALESCE(NULLIF(category, ''), 'N/A') AS category
                    FROM "vw_vendor_sales_nivelacija"
                    WHERE (@vendorId IS NULL OR vendor_id = @vendorId)
                      AND (@eventDate IS NULL OR event_date::date = @eventDate)
                      AND (@fromDate IS NULL OR event_date::date >= @fromDate)
                      AND (@toDate IS NULL OR event_date::date <= @toDate)
                    ORDER BY COALESCE(NULLIF(category, ''), 'N/A');
                    """;

                await using (var cmd = new NpgsqlCommand(categoriesSql, connection))
                {
                    cmd.Parameters.Add(new NpgsqlParameter("vendorId", NpgsqlTypes.NpgsqlDbType.Integer)
                    {
                        Value = (object?)vendorId ?? DBNull.Value
                    });

                    cmd.Parameters.Add(new NpgsqlParameter("eventDate", NpgsqlTypes.NpgsqlDbType.Date)
                    {
                        Value = (object?)eventDateOnly ?? DBNull.Value
                    });

                    cmd.Parameters.Add(new NpgsqlParameter("fromDate", NpgsqlTypes.NpgsqlDbType.Date)
                    {
                        Value = (object?)fromDateOnly ?? DBNull.Value
                    });

                    cmd.Parameters.Add(new NpgsqlParameter("toDate", NpgsqlTypes.NpgsqlDbType.Date)
                    {
                        Value = (object?)toDateOnly ?? DBNull.Value
                    });

                    await using var reader = await cmd.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct))
                    {
                        var value = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                        if (!string.IsNullOrWhiteSpace(value))
                            categories.Add(value);
                    }
                }

                var rowsSql = hasPriceColumns
                    ? $"""
                        WITH ranked AS (
                            SELECT
                                price_event_id,
                                event_date::date AS event_date,
                                vendor_id,
                                COALESCE(vendor_name, 'N/A') AS vendor_name,
                                article_id,
                                COALESCE(NULLIF(sku, ''), article_id::text) AS sku,
                                COALESCE(article_name, '') AS article_name,
                                COALESCE(NULLIF(category, ''), 'N/A') AS category,
                                old_price,
                                new_price,
                                COALESCE(pre_qty, 0)::numeric AS pre_qty,
                                COALESCE(pre_revenue, 0)::numeric AS pre_revenue,
                                COALESCE(post_qty, 0)::numeric AS post_qty,
                                COALESCE(post_revenue, 0)::numeric AS post_revenue,
                                COALESCE(change_qty, 0)::numeric AS change_qty,
                                COALESCE(change_revenue, 0)::numeric AS change_revenue,
                                COALESCE({changePercentExpr}, 0)::numeric AS change_percent,
                                ROW_NUMBER() OVER (
                                    PARTITION BY
                                        event_date::date,
                                        COALESCE(vendor_id, -1),
                                        article_id,
                                        old_price,
                                        new_price
                                    ORDER BY price_event_id DESC
                                ) AS rn
                            FROM "vw_vendor_sales_nivelacija"
                            WHERE (@vendorId IS NULL OR vendor_id = @vendorId::int)
                              AND (@eventDate IS NULL OR event_date::date = @eventDate::date)
                              AND (@fromDate IS NULL OR event_date::date >= @fromDate::date)
                              AND (@toDate IS NULL OR event_date::date <= @toDate::date)
                              AND (@category IS NULL OR category ILIKE @categoryPattern::text)
                        )
                        SELECT
                            price_event_id,
                            event_date,
                            vendor_id,
                            vendor_name,
                            article_id,
                            sku,
                            article_name,
                            category,
                            old_price,
                            new_price,
                            pre_qty,
                            pre_revenue,
                            post_qty,
                            post_revenue,
                            change_qty,
                            change_revenue,
                            change_percent
                        FROM ranked
                        WHERE rn = 1
                        ORDER BY vendor_name, ABS(change_revenue) DESC, article_name;
                        """
                    : $"""
                        WITH ranked AS (
                            SELECT
                                price_event_id,
                                event_date::date AS event_date,
                                vendor_id,
                                COALESCE(vendor_name, 'N/A') AS vendor_name,
                                article_id,
                                COALESCE(NULLIF(sku, ''), article_id::text) AS sku,
                                COALESCE(article_name, '') AS article_name,
                                COALESCE(NULLIF(category, ''), 'N/A') AS category,
                                NULL::numeric AS old_price,
                                NULL::numeric AS new_price,
                                COALESCE(pre_qty, 0)::numeric AS pre_qty,
                                COALESCE(pre_revenue, 0)::numeric AS pre_revenue,
                                COALESCE(post_qty, 0)::numeric AS post_qty,
                                COALESCE(post_revenue, 0)::numeric AS post_revenue,
                                COALESCE(change_qty, 0)::numeric AS change_qty,
                                COALESCE(change_revenue, 0)::numeric AS change_revenue,
                                COALESCE({changePercentExpr}, 0)::numeric AS change_percent,
                                ROW_NUMBER() OVER (
                                    PARTITION BY
                                        event_date::date,
                                        COALESCE(vendor_id, -1),
                                        article_id
                                    ORDER BY price_event_id DESC
                                ) AS rn
                            FROM "vw_vendor_sales_nivelacija"
                            WHERE (@vendorId IS NULL OR vendor_id = @vendorId::int)
                              AND (@eventDate IS NULL OR event_date::date = @eventDate::date)
                              AND (@fromDate IS NULL OR event_date::date >= @fromDate::date)
                              AND (@toDate IS NULL OR event_date::date <= @toDate::date)
                              AND (@category IS NULL OR category ILIKE @categoryPattern::text)
                        )
                        SELECT
                            price_event_id,
                            event_date,
                            vendor_id,
                            vendor_name,
                            article_id,
                            sku,
                            article_name,
                            category,
                            old_price,
                            new_price,
                            pre_qty,
                            pre_revenue,
                            post_qty,
                            post_revenue,
                            change_qty,
                            change_revenue,
                            change_percent
                        FROM ranked
                        WHERE rn = 1
                        ORDER BY vendor_name, ABS(change_revenue) DESC, article_name;
                        """;

                var dedupRows = new List<VendorSalesNivelacijaArticleStatDto>();

                var inactiveRows = 0;
                var unchangedPriceRows = 0;

                await using (var cmd = new NpgsqlCommand(rowsSql, connection))
                {
                    cmd.Parameters.Add(new NpgsqlParameter("vendorId", NpgsqlTypes.NpgsqlDbType.Integer)
                    {
                        Value = (object?)vendorId ?? DBNull.Value
                    });

                    cmd.Parameters.Add(new NpgsqlParameter("eventDate", NpgsqlTypes.NpgsqlDbType.Date)
                    {
                        Value = (object?)eventDateOnly ?? DBNull.Value
                    });

                    cmd.Parameters.Add(new NpgsqlParameter("fromDate", NpgsqlTypes.NpgsqlDbType.Date)
                    {
                        Value = (object?)fromDateOnly ?? DBNull.Value
                    });

                    cmd.Parameters.Add(new NpgsqlParameter("toDate", NpgsqlTypes.NpgsqlDbType.Date)
                    {
                        Value = (object?)toDateOnly ?? DBNull.Value
                    });

                    cmd.Parameters.Add(new NpgsqlParameter("category", NpgsqlTypes.NpgsqlDbType.Text)
                    {
                        Value = (object?)categoryTrimmed ?? DBNull.Value
                    });

                    cmd.Parameters.Add(new NpgsqlParameter("categoryPattern", NpgsqlTypes.NpgsqlDbType.Text)
                    {
                        Value = (object?)categoryPattern ?? DBNull.Value
                    });

                    await using var reader = await cmd.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct))
                    {
                        _ = reader.GetInt64(0); // price_event_id (not returned)
                        var evDate = DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc);
                        var vId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
                        var vName = reader.IsDBNull(3) ? "N/A" : reader.GetString(3);
                        _ = reader.GetInt32(4); // article_id (used by metrics queries, not returned)
                        var sku = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);
                        var articleName = reader.IsDBNull(6) ? string.Empty : reader.GetString(6);
                        var cat = reader.IsDBNull(7) ? "N/A" : reader.GetString(7);
                        var oldPrice = reader.IsDBNull(8) ? (decimal?)null : reader.GetDecimal(8);
                        var newPrice = reader.IsDBNull(9) ? (decimal?)null : reader.GetDecimal(9);

                        var preQty = reader.IsDBNull(10) ? 0 : (int)reader.GetDecimal(10);
                        var preRevenue = reader.IsDBNull(11) ? 0m : reader.GetDecimal(11);
                        var postQty = reader.IsDBNull(12) ? 0 : (int)reader.GetDecimal(12);
                        var postRevenue = reader.IsDBNull(13) ? 0m : reader.GetDecimal(13);
                        var changeQty = reader.IsDBNull(14) ? 0 : (int)reader.GetDecimal(14);
                        var changeRevenue = reader.IsDBNull(15) ? 0m : reader.GetDecimal(15);
                        var changePercentRevenue = reader.IsDBNull(16) ? 0m : reader.GetDecimal(16);

                        var hasSalesWindow =
                            preQty != 0 || postQty != 0 || preRevenue != 0m || postRevenue != 0m;

                        if (!hasSalesWindow)
                            inactiveRows++;

                        var priceChanged = oldPrice.HasValue && newPrice.HasValue && oldPrice.Value != newPrice.Value;
                        if (oldPrice.HasValue && newPrice.HasValue && oldPrice.Value == newPrice.Value)
                            unchangedPriceRows++;

                        decimal? priceChangePercent = null;
                        if (priceChanged && oldPrice.GetValueOrDefault() != 0m)
                        {
                            priceChangePercent = Math.Round(((newPrice!.Value - oldPrice!.Value) / oldPrice.Value) * 100m, 2);
                        }

                        var dto = new VendorSalesNivelacijaArticleStatDto
                        {
                            EventDate = evDate,
                            VendorId = vId,
                            VendorName = vName,
                            Sku = sku,
                            ArticleName = articleName,
                            Category = string.IsNullOrWhiteSpace(cat) ? "N/A" : cat,
                            OldPrice = oldPrice,
                            NewPrice = newPrice,
                            PreQty = preQty,
                            PreRevenue = preRevenue,
                            PostQty = postQty,
                            PostRevenue = postRevenue,
                            ChangeQty = changeQty,
                            ChangeRevenue = changeRevenue,
                            ChangePercent = changePercentRevenue,
                            HasSalesWindow = hasSalesWindow,
                            PriceChanged = priceChanged,
                            PriceChangePercent = priceChangePercent
                        };

                        dedupRows.Add(dto);
                    }
                }

                var deduplicatedRows = dedupRows.Count;

                // Analyze rows: remove unchanged-price rows, and optionally remove inactive rows.
                var analyzed = dedupRows
                    .Where(x => !(x.OldPrice.HasValue && x.NewPrice.HasValue && x.OldPrice.Value == x.NewPrice.Value))
                    .Where(x => includeInactive || x.HasSalesWindow)
                    .ToList();

                var analyzedRows = analyzed.Count;

                var analyzedSharePercent = deduplicatedRows == 0
                    ? 0m
                    : Math.Round(((decimal)analyzedRows / deduplicatedRows) * 100m, 2);

                // Advanced metrics (best-effort, non-fatal).
                var globalWarnings = new List<string>();

                try
                {
                    var warning = await MapRollingAndMomentumToNivelacijaArticlesAsync(
                        analyzed,
                        connection,
                        eventDateOnly,
                        ct);
                    if (!string.IsNullOrWhiteSpace(warning))
                        globalWarnings.Add(warning);
                }
                catch
                {
                    globalWarnings.Add("Metrics mapping failed");
                }

                try
                {
                    var warning = await MapOosAndDidToNivelacijaArticlesAsync(analyzed, connection, ct);
                    if (!string.IsNullOrWhiteSpace(warning))
                        globalWarnings.Add(warning);
                }
                catch
                {
                    globalWarnings.Add("OOS/DiD mapping failed");
                }

                MapElasticityAndLostSalesToNivelacijaArticles(analyzed);
                MapMetricReasons(analyzed, globalWarnings);

                // Totals
                var totalPreQty = analyzed.Sum(x => x.PreQty);
                var totalPostQty = analyzed.Sum(x => x.PostQty);
                var totalPreRevenue = analyzed.Sum(x => x.PreRevenue);
                var totalPostRevenue = analyzed.Sum(x => x.PostRevenue);
                var totalChangeQty = analyzed.Sum(x => x.ChangeQty);
                var totalChangeRevenue = analyzed.Sum(x => x.ChangeRevenue);

                static decimal Pct(decimal pre, decimal post)
                {
                    if (pre == 0m) return post > 0m ? 100m : 0m;
                    return Math.Round(((post - pre) / pre) * 100m, 2);
                }

                var vendorsCount = analyzed.Select(x => x.VendorId).Distinct().Count();
                var articlesCount = analyzed.Select(x => x.Sku).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.Ordinal).Count();
                var activeArticlesCount = analyzed.Count(x => x.HasSalesWindow);

                var avgRevenuePerArticlePre = activeArticlesCount == 0 ? 0m : Math.Round(totalPreRevenue / activeArticlesCount, 2);
                var avgRevenuePerArticlePost = activeArticlesCount == 0 ? 0m : Math.Round(totalPostRevenue / activeArticlesCount, 2);

                var avgPriceChangePercent = analyzed
                    .Where(x => x.PriceChangePercent.HasValue)
                    .Select(x => x.PriceChangePercent!.Value)
                    .DefaultIfEmpty()
                    .Average();

                var totals = new VendorSalesNivelacijaTotalsDto
                {
                    PreQty = totalPreQty,
                    PreRevenue = totalPreRevenue,
                    PostQty = totalPostQty,
                    PostRevenue = totalPostRevenue,
                    ChangeQty = totalChangeQty,
                    ChangeRevenue = totalChangeRevenue,
                    ChangePercent = Pct(totalPreRevenue, totalPostRevenue),
                    VendorsCount = vendorsCount,
                    ArticlesCount = articlesCount,
                    ActiveArticlesCount = activeArticlesCount,
                    AvgRevenuePerArticlePre = avgRevenuePerArticlePre,
                    AvgRevenuePerArticlePost = avgRevenuePerArticlePost,
                    AvgPriceChangePercent = Math.Round(avgPriceChangePercent, 2)
                };

                // Vendor stats
                var vendorStats = analyzed
                    .GroupBy(x => new { x.VendorId, x.VendorName })
                    .Select(g =>
                    {
                        var preRev = g.Sum(x => x.PreRevenue);
                        var postRev = g.Sum(x => x.PostRevenue);
                        var increased = g.Count(x => x.PriceChangePercent.HasValue && x.PriceChangePercent.Value > 0m);
                        var decreased = g.Count(x => x.PriceChangePercent.HasValue && x.PriceChangePercent.Value < 0m);
                        return new VendorSalesNivelacijaVendorStatDto
                        {
                            VendorId = g.Key.VendorId,
                            VendorName = g.Key.VendorName,
                            PreQty = g.Sum(x => x.PreQty),
                            PreRevenue = preRev,
                            PostQty = g.Sum(x => x.PostQty),
                            PostRevenue = postRev,
                            ChangeQty = g.Sum(x => x.ChangeQty),
                            ChangeRevenue = g.Sum(x => x.ChangeRevenue),
                            ChangePercent = Pct(preRev, postRev),
                            ArticleCount = g.Select(x => x.Sku).Distinct(StringComparer.Ordinal).Count(),
                            ActiveArticlesCount = g.Count(x => x.HasSalesWindow),
                            IncreasedPriceArticlesCount = increased,
                            DecreasedPriceArticlesCount = decreased
                        };
                    })
                    .OrderByDescending(x => Math.Abs(x.ChangeRevenue))
                    .ToList();

                // Category stats (top 50)
                var categoryStats = analyzed
                    .GroupBy(x => x.Category ?? "N/A")
                    .Select(g =>
                    {
                        var preRev = g.Sum(x => x.PreRevenue);
                        var postRev = g.Sum(x => x.PostRevenue);
                        return new VendorSalesNivelacijaCategoryStatDto
                        {
                            Category = g.Key,
                            ArticlesCount = g.Select(x => x.Sku).Distinct(StringComparer.Ordinal).Count(),
                            VendorsCount = g.Select(x => x.VendorId).Distinct().Count(),
                            PreQty = g.Sum(x => x.PreQty),
                            PreRevenue = preRev,
                            PostQty = g.Sum(x => x.PostQty),
                            PostRevenue = postRev,
                            ChangeQty = g.Sum(x => x.ChangeQty),
                            ChangeRevenue = g.Sum(x => x.ChangeRevenue),
                            ChangePercent = Pct(preRev, postRev)
                        };
                    })
                    .OrderByDescending(x => Math.Abs(x.ChangeRevenue))
                    .ToList();

                // Price direction stats
                string SegmentFor(VendorSalesNivelacijaArticleStatDto x)
                {
                    if (!x.PriceChangePercent.HasValue) return "Cena N/A";
                    if (x.PriceChangePercent.Value > 0m) return "Cena ↑";
                    if (x.PriceChangePercent.Value < 0m) return "Cena ↓";
                    return "Cena =";
                }

                var priceDirectionStats = analyzed
                    .GroupBy(SegmentFor)
                    .Select(g =>
                    {
                        var preRev = g.Sum(x => x.PreRevenue);
                        var postRev = g.Sum(x => x.PostRevenue);
                        var avgPct = g.Where(x => x.PriceChangePercent.HasValue).Select(x => x.PriceChangePercent!.Value).DefaultIfEmpty().Average();
                        return new VendorSalesNivelacijaPriceDirectionStatDto
                        {
                            Segment = g.Key,
                            ArticlesCount = g.Select(x => x.Sku).Distinct(StringComparer.Ordinal).Count(),
                            VendorsCount = g.Select(x => x.VendorId).Distinct().Count(),
                            AvgPriceChangePercent = Math.Round(avgPct, 2),
                            ChangeRevenue = g.Sum(x => x.ChangeRevenue),
                            ChangePercent = Pct(preRev, postRev)
                        };
                    })
                    .OrderByDescending(x => x.ArticlesCount)
                    .ToList();

                // Insights (minimal, stable)
                var insights = new List<VendorSalesNivelacijaInsightDto>();
                if (totals.ArticlesCount > 0)
                {
                    insights.Add(new VendorSalesNivelacijaInsightDto
                    {
                        Title = "Ukupan obuhvat",
                        Value = $"{totals.VendorsCount} dobavljaca / {totals.ArticlesCount} artikala",
                        Details = $"Aktivni artikli: {totals.ActiveArticlesCount}.",
                        Tone = "neutral"
                    });

                    var bestVendor = vendorStats.OrderByDescending(x => x.ChangeRevenue).FirstOrDefault();
                    if (bestVendor != null)
                    {
                        insights.Add(new VendorSalesNivelacijaInsightDto
                        {
                            Title = "Najveci rast (dobavljac)",
                            Value = $"{bestVendor.VendorName}",
                            Details = $"{bestVendor.ChangeRevenue:N2} RSD ({bestVendor.ChangePercent:N2}%)",
                            Tone = bestVendor.ChangeRevenue >= 0 ? "positive" : "warning"
                        });
                    }

                    var worstVendor = vendorStats.OrderBy(x => x.ChangeRevenue).FirstOrDefault();
                    if (worstVendor != null && worstVendor.ChangeRevenue < 0)
                    {
                        insights.Add(new VendorSalesNivelacijaInsightDto
                        {
                            Title = "Najveci pad (dobavljac)",
                            Value = $"{worstVendor.VendorName}",
                            Details = $"{worstVendor.ChangeRevenue:N2} RSD ({worstVendor.ChangePercent:N2}%)",
                            Tone = "negative"
                        });
                    }
                }

                static decimal? AverageOrNull(IEnumerable<decimal?> values)
                {
                    decimal sum = 0m;
                    var count = 0;
                    foreach (var value in values)
                    {
                        if (!value.HasValue) continue;
                        sum += value.Value;
                        count++;
                    }

                    return count == 0 ? null : sum / count;
                }

                var avgMomentumRevenue = AverageOrNull(analyzed.Select(x => x.MomentumRevenue));
                var avgElasticity = AverageOrNull(analyzed.Select(x => x.PriceElasticity));
                var avgDidRevenue = AverageOrNull(analyzed.Select(x => x.DidRevenue));
                var avgLostSalesOos = AverageOrNull(analyzed.Select(x => x.LostSalesOOS));
                var avgOosRate = AverageOrNull(analyzed.Select(x => x.OOSRate));

                var response = new VendorSalesNivelacijaResponseDto
                {
                    GeneratedAt = DateTime.UtcNow,
                    WindowDays = 30,
                    VendorId = vendorId,
                    EventDate = eventDateOnly,
                    From = from,
                    To = to,
                    Category = categoryTrimmed,
                    IncludeInactive = includeInactive,
                    Categories = categories,
                    VendorStats = vendorStats,
                    ArticleStats = analyzed,
                    Totals = totals,
                    DataQuality = new VendorSalesNivelacijaDataQualityDto
                    {
                        RawRows = rawRows,
                        DeduplicatedRows = deduplicatedRows,
                        DuplicateRowsRemoved = Math.Max(0, rawRows - deduplicatedRows),
                        InactiveRows = inactiveRows,
                        UnchangedPriceRows = unchangedPriceRows,
                        AnalyzedRows = analyzedRows,
                        AnalyzedSharePercent = analyzedSharePercent
                    },
                    CategoryStats = categoryStats,
                    PriceDirectionStats = priceDirectionStats,
                    Insights = insights,
                    AvgMomentumRevenue = avgMomentumRevenue,
                    AvgElasticity = avgElasticity,
                    AvgDidRevenue = avgDidRevenue,
                    AvgLostSalesOOS = avgLostSalesOos,
                    OOSRate = avgOosRate,
                    MetricsStatus = globalWarnings.Count == 0 ? null : string.Join("; ", globalWarnings.Distinct(StringComparer.Ordinal))
                };

                return Results.Ok(response);
            }
            catch (PostgresException ex) when (ex.SqlState == "42P01" || ex.SqlState == "42703")
            {
                return Results.Problem(
                    title: "Nivelacija view schema mismatch",
                    detail: "Run DB migration scripts 013_AddVendorSalesNivelacijaViews.sql, 014_FixNivelacijaViewsFromDnevnik.sql, and 016_AnalyticsNivelacijaEnhancements.sql, then restart the backend.",
                    statusCode: 500);
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    title: "Failed to load pre/post nivelacija analytics",
                    detail: ex.Message,
                    statusCode: 500);
            }
        })
        .WithName("GetVendorSalesNivelacija")
        .WithTags("Analytics")
        .RequireRateLimiting("analytics");

        // Non-cached aliases -> cached routes (keeps existing frontend paths working).
        app.MapGet("/api/analytics/sales/summary", (HttpContext ctx) =>
            Results.Redirect($"/api/analytics/cached/sales/summary{ctx.Request.QueryString}", permanent: false));
        app.MapGet("/api/analytics/sales/top-products", (HttpContext ctx) =>
            Results.Redirect($"/api/analytics/cached/sales/top-products{ctx.Request.QueryString}", permanent: false));
        app.MapGet("/api/analytics/inventory/status", (HttpContext ctx) =>
            Results.Redirect($"/api/analytics/cached/inventory/status{ctx.Request.QueryString}", permanent: false));
        app.MapGet("/api/analytics/sales/daily", (HttpContext ctx) =>
            Results.Redirect($"/api/analytics/cached/sales/daily{ctx.Request.QueryString}", permanent: false));
        app.MapGet("/api/analytics/sales/by-category", (HttpContext ctx) =>
            Results.Redirect($"/api/analytics/cached/sales/by-category{ctx.Request.QueryString}", permanent: false));
        app.MapGet("/api/analytics/sales/by-gender", (HttpContext ctx) =>
            Results.Redirect($"/api/analytics/cached/sales/by-gender{ctx.Request.QueryString}", permanent: false));
        app.MapGet("/api/analytics/sales/by-supplier", (HttpContext ctx) =>
            Results.Redirect($"/api/analytics/cached/sales/by-supplier{ctx.Request.QueryString}", permanent: false));
        app.MapGet("/api/analytics/quick-insights", (HttpContext ctx) =>
            Results.Redirect($"/api/analytics/cached/quick-insights{ctx.Request.QueryString}", permanent: false));
        app.MapGet("/api/analytics/sales/transaction-stats", (HttpContext ctx) =>
            Results.Redirect($"/api/analytics/cached/sales/transaction-stats{ctx.Request.QueryString}", permanent: false));
        app.MapGet("/api/analytics/sales/by-payment", (HttpContext ctx) =>
            Results.Redirect($"/api/analytics/cached/sales/by-payment{ctx.Request.QueryString}", permanent: false));
        app.MapGet("/api/analytics/sales/by-weekday", (HttpContext ctx) =>
            Results.Redirect($"/api/analytics/cached/sales/by-weekday{ctx.Request.QueryString}", permanent: false));
        app.MapGet("/api/analytics/sales/by-hour", (HttpContext ctx) =>
            Results.Redirect($"/api/analytics/cached/sales/by-hour{ctx.Request.QueryString}", permanent: false));
        app.MapGet("/api/analytics/reorder-suggestions", (HttpContext ctx) =>
            Results.Redirect($"/api/analytics/cached/reorder-suggestions{ctx.Request.QueryString}", permanent: false));
        app.MapGet("/api/analytics/sales/category-trends", (HttpContext ctx) =>
            Results.Redirect($"/api/analytics/cached/sales/category-trends{ctx.Request.QueryString}", permanent: false));
        app.MapGet("/api/analytics/dashboard/advanced", (HttpContext ctx) =>
            Results.Redirect($"/api/analytics/cached/dashboard/advanced{ctx.Request.QueryString}", permanent: false));
        app.MapGet("/api/analytics/dashboard/bootstrap", (HttpContext ctx) =>
            Results.Redirect($"/api/analytics/cached/dashboard/bootstrap{ctx.Request.QueryString}", permanent: false));
        app.MapGet("/api/analytics/sales/top-products-advanced", (HttpContext ctx) =>
            Results.Redirect($"/api/analytics/cached/sales/top-products-advanced{ctx.Request.QueryString}", permanent: false));
        app.MapGet("/api/analytics/filters/suppliers", (HttpContext ctx) =>
            Results.Redirect($"/api/analytics/cached/filters/suppliers{ctx.Request.QueryString}", permanent: false));
        app.MapGet("/api/analytics/filters/stores", (HttpContext ctx) =>
            Results.Redirect($"/api/analytics/cached/filters/stores{ctx.Request.QueryString}", permanent: false));
        app.MapGet("/api/analytics/validation/completeness", (HttpContext ctx) =>
            Results.Redirect($"/api/analytics/cached/validation/completeness{ctx.Request.QueryString}", permanent: false));
        app.MapGet("/api/analytics/validation/freshness", (HttpContext ctx) =>
            Results.Redirect($"/api/analytics/cached/validation/freshness{ctx.Request.QueryString}", permanent: false));
        app.MapGet("/api/analytics/validation/lost-sales", (HttpContext ctx) =>
            Results.Redirect($"/api/analytics/cached/validation/lost-sales{ctx.Request.QueryString}", permanent: false));
        app.MapGet("/api/analytics/validation/negative-qty", (HttpContext ctx) =>
            Results.Redirect($"/api/analytics/cached/validation/negative-qty{ctx.Request.QueryString}", permanent: false));

        app.MapGet("/api/analytics/sales/comparison", async (
            IAnalyticsDbContext db,
            IMemoryCache cache,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken ct = default) =>
        {
            try
            {
                if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                    fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);
                if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                    toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

                var fromKey = fromDate?.ToUniversalTime().ToString("O") ?? "null";
                var toKey = toDate?.ToUniversalTime().ToString("O") ?? "null";
                var cacheKey = $"analytics_comparison_{fromKey}_{toKey}";

                if (cache.TryGetValue(cacheKey, out object? cachedComparison) && cachedComparison is not null)
                {
                    return Results.Ok(cachedComparison);
                }

                var currentQuery = db.SalesFacts.AsNoTracking();
                if (fromDate.HasValue)
                    currentQuery = currentQuery.Where(s => s.SaleTimestampUtc >= fromDate.Value);
                if (toDate.HasValue)
                    currentQuery = currentQuery.Where(s => s.SaleTimestampUtc <= toDate.Value);

                var current = await currentQuery
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        totalRevenue = g.Sum(s => s.TotalAmount),
                        totalTransactions = g.Count(),
                        totalUnits = g.Sum(s => s.TotalUnits)
                    })
                    .FirstOrDefaultAsync(ct);

                if (fromDate.HasValue && toDate.HasValue)
                {
                    var duration = (toDate.Value - fromDate.Value).TotalDays;
                    var prevFrom = fromDate.Value.AddDays(-duration);
                    var prevTo = fromDate.Value;

                    var previous = await db.SalesFacts.AsNoTracking()
                        .Where(s => s.SaleTimestampUtc >= prevFrom && s.SaleTimestampUtc < prevTo)
                        .GroupBy(_ => 1)
                        .Select(g => new
                        {
                            totalRevenue = g.Sum(s => s.TotalAmount),
                            totalTransactions = g.Count(),
                            totalUnits = g.Sum(s => s.TotalUnits)
                        })
                        .FirstOrDefaultAsync(ct);

                    if (current != null && previous != null)
                    {
                        static decimal Pct(decimal oldValue, decimal newValue) =>
                            oldValue == 0 ? (newValue > 0 ? 100 : 0) : ((newValue - oldValue) / oldValue) * 100;

                        var response = new
                        {
                            current,
                            previous,
                            change = new
                            {
                                revenue = Pct(previous.totalRevenue, current.totalRevenue),
                                transactions = Pct(previous.totalTransactions, current.totalTransactions),
                                units = Pct(previous.totalUnits, current.totalUnits)
                            }
                        };

                        cache.Set(cacheKey, response, TimeSpan.FromMinutes(1));
                        return Results.Ok(response);
                    }
                }

                var defaultResponse = new { current, previous = (object?)null, change = (object?)null };
                cache.Set(cacheKey, defaultResponse, TimeSpan.FromMinutes(1));
                return Results.Ok(defaultResponse);
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greska pri poredjenju perioda");
            }
        })
        .RequireRateLimiting("analytics");

        app.MapGet("/api/analytics/alerts", async (ITrendplusDbContext db, IMemoryCache cache, CancellationToken ct = default) =>
        {
            try
            {
                const string cacheKey = "analytics_alerts";
                if (cache.TryGetValue(cacheKey, out object? cachedAlerts) && cachedAlerts is not null)
                {
                    return Results.Ok(cachedAlerts);
                }

                var alerts = new List<object>();

                var outOfStock = await db.Artikli
                    .Where(a => a.Kolicina == 0)
                    .Select(a => new { a.Naziv })
                    .ToListAsync(ct);

                if (outOfStock.Count > 0)
                {
                    alerts.Add(new
                    {
                        type = "error",
                        icon = "red",
                        title = $"{outOfStock.Count} proizvoda bez zaliha",
                        message = string.Join(", ", outOfStock.Take(3).Select(a => a.Naziv)) + (outOfStock.Count > 3 ? "..." : "")
                    });
                }

                var lowStock = await db.Artikli
                    .Where(a => a.Kolicina > 0 && a.Kolicina <= a.MinimalnaKolicina)
                    .Select(a => new { a.Naziv, a.Kolicina })
                    .ToListAsync(ct);

                if (lowStock.Count > 0)
                {
                    alerts.Add(new
                    {
                        type = "warning",
                        icon = "yellow",
                        title = $"{lowStock.Count} proizvoda ispod minimalne kolicine",
                        message = string.Join(", ", lowStock.Take(3).Select(a => $"{a.Naziv} ({a.Kolicina})")) + (lowStock.Count > 3 ? "..." : "")
                    });
                }

                cache.Set(cacheKey, alerts, TimeSpan.FromMinutes(2));
                return Results.Ok(alerts);
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greska pri ucitavanju obavestenja");
            }
        })
        .RequireRateLimiting("analytics");

        // ============ GLOBAL TRENDS API ============
        
        // Get social media trends for category
        app.MapGet("/api/global-trends/social", async (
            IHttpClientFactory httpClientFactory,
            ILogger<Program> logger,
            string category = "Patike") =>
        {
            try {
                logger.LogInformation("📊 Fetching social trends for category: '{Category}'", category);
                
                var pythonServiceUrl = "http://localhost:8000";
                var httpClient = httpClientFactory.CreateClient("default");
                
                // Call Python trends API - NO FALLBACK
                try
                {
                    var response = await httpClient.GetAsync($"{pythonServiceUrl}/trends/social?category={category}");
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        logger.LogError("Python service returned {StatusCode}", response.StatusCode);
                        return Results.Problem(
                            detail: $"Python trends service returned HTTP {response.StatusCode}. Make sure Python service is running at {pythonServiceUrl}",
                            statusCode: 503,
                            title: "Trends Service Unavailable"
                        );
                    }
                    
                    var data = await response.Content.ReadFromJsonAsync<object>();
                    logger.LogInformation("✅ Received real data from Python service");
                    
                    return Results.Ok(data);
                }
                catch (TaskCanceledException ex)
                {
                    logger.LogError(ex, "❌ Python service timeout (2s)");
                    return Results.Problem(
                        detail: $"Python service at {pythonServiceUrl} did not respond within 2 seconds.\n\n" +
                                "To start Python service:\n" +
                                "1. Open new terminal\n" +
                                "2. cd Python\n" +
                                "3. start_api.bat\n\n" +
                                "Or run: start-app.bat to start all services",
                        statusCode: 503,
                        title: "Python Trends Service Timeout"
                    );
                }
                catch (HttpRequestException ex)
                {
                    logger.LogError(ex, "❌ Cannot connect to Python service at {Url}", pythonServiceUrl);
                    return Results.Problem(
                        detail: $"Cannot connect to Python service at {pythonServiceUrl}.\n\n" +
                                "Python service is NOT running!\n\n" +
                                "To start it:\n" +
                                "1. Open new terminal\n" +
                                "2. cd Python\n" +
                                "3. start_api.bat\n\n" +
                                "Or run: start-app.bat to start all services\n\n" +
                                $"Error: {ex.Message}",
                        statusCode: 503,
                        title: "Python Service Not Running"
                    );
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Unexpected error fetching trends for category '{Category}'", category);
                return Results.Problem(
                    detail: $"Unexpected error: {ex.Message}",
                    statusCode: 500,
                    title: "Internal Server Error"
                );
            }
        })
        .WithName("GetSocialTrends")
        .WithTags("GlobalTrends");

        // Run EU market scrapers
        app.MapPost("/api/global-trends/scrape", async (
            IHttpClientFactory httpClientFactory,
            ILogger<Program> logger,
            string? category = null,
            int zalandoPages = 3,
            int deichmannPages = 2,
            CancellationToken ct = default) =>
        {
            try
            {
                logger.LogInformation("🔍 Running EU market scrapers (category={Category})", category);

                var pythonServiceUrl = "http://localhost:8000";

                // Build payload to send to Python scraper service
                var payload = new
                {
                    zalando_pages = zalandoPages,
                    deichmann_pages = deichmannPages,
                    category = category ?? "Patike"
                };

                // Create HttpClient with extended timeout for long-running scrapers
                var httpClient = httpClientFactory.CreateClient("default");
                try
                {
                    httpClient.Timeout = TimeSpan.FromMinutes(5);
                }
                catch
                {
                    // Some HttpClient implementations may throw on setting Timeout; ignore and rely on default
                }

                var response = await httpClient.PostAsJsonAsync($"{pythonServiceUrl}/scrapers/run", payload, ct);

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogError("Python scraper service returned {StatusCode}", response.StatusCode);
                    return Results.Problem(
                        detail: $"Python scraper service returned HTTP {response.StatusCode}. Make sure Python service is running at {pythonServiceUrl}",
                        statusCode: 503,
                        title: "Scraper Service Unavailable"
                    );
                }

                var data = await response.Content.ReadFromJsonAsync<object>(cancellationToken: ct);
                logger.LogInformation("✅ Scrapers completed successfully (category={Category})", category);

                return Results.Ok(data);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(ex, "❌ Python scraper service timed out");
                return Results.Problem(
                    detail: $"Python scraper service did not complete within the allotted time. You can increase timeout in backend or run scrapers manually. Error: {ex.Message}",
                    statusCode: 504,
                    title: "Scraper Service Timeout"
                );
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "❌ Cannot connect to Python scraper service");
                return Results.Problem(
                    detail: $"Cannot connect to Python scraper service. Please start it with: cd Python && start_api.bat\nError: {ex.Message}",
                    statusCode: 503,
                    title: "Scraper Service Not Running"
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Failed to run scrapers");
                return Results.Problem("Failed to run scrapers: " + ex.Message);
            }
        })
        .WithName("RunEUScraper")
        .WithTags("GlobalTrends");

        // ===== Zalando ad-hoc scraper proxy =====
        app.MapPost("/api/scrapers/zalando", async (
            HttpContext httpContext,
            IHttpClientFactory httpClientFactory,
            ILogger<Program> logger,
            System.Text.Json.JsonElement filters,
            CancellationToken ct) =>
        {
            var requestId = httpContext.Request.Headers["X-Request-ID"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(requestId))
            {
                requestId = Guid.NewGuid().ToString("N");
            }

            using var __ = LogContext.PushProperty("RequestId", requestId);
            try
            {
                var client = httpClientFactory.CreateClient("scraper");

                var json = filters.GetRawText();
                var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/scrapers/zalando")
                {
                    Content = content
                };
                requestMessage.Headers.TryAddWithoutValidation("X-Request-ID", requestId);

                // Use extended timeout for scraping operations
                client.Timeout = TimeSpan.FromMinutes(5);

                logger.LogInformation("Proxying Zalando scraper request. RequestId={RequestId}", requestId);
                var resp = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, ct);
                logger.LogInformation("Zalando scraper response received. RequestId={RequestId}, StatusCode={StatusCode}", requestId, (int)resp.StatusCode);

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync(ct);
                    logger.LogWarning("Zalando scraper returned {Status}: {Body}", resp.StatusCode, body);
                    return Results.Problem(detail: $"Scraper service returned {resp.StatusCode}: {body}", statusCode: 502);
                }

                var stream = await resp.Content.ReadAsStreamAsync(ct);
                httpContext.Response.Headers["X-Request-ID"] = requestId;
                httpContext.Response.RegisterForDispose(resp);
                httpContext.Response.RegisterForDispose(stream);
                return Results.Stream(stream, "application/json");
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "Failed to call Zalando scraper service");
                return Results.Problem(detail: ex.Message, statusCode: 503);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(ex, "Zalando scraper timed out");
                return Results.Problem(detail: "Scraper service timed out", statusCode: 504);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error proxying Zalando scraper");
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        })
        .WithName("ProxyZalandoScraper")
        .WithTags("External");

        // ===== Deichmann ad-hoc scraper proxy =====
        app.MapPost("/api/scrapers/deichmann", async (
            HttpContext httpContext,
            IHttpClientFactory httpClientFactory,
            ILogger<Program> logger,
            System.Text.Json.JsonElement filters,
            CancellationToken ct) =>
        {
            var requestId = httpContext.Request.Headers["X-Request-ID"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(requestId))
            {
                requestId = Guid.NewGuid().ToString("N");
            }

            using var __ = LogContext.PushProperty("RequestId", requestId);
            try
            {
                var client = httpClientFactory.CreateClient("scraper");

                var json = filters.GetRawText();
                var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/scrapers/deichmann")
                {
                    Content = content
                };
                requestMessage.Headers.TryAddWithoutValidation("X-Request-ID", requestId);

                // Use extended timeout for scraping operations
                client.Timeout = TimeSpan.FromMinutes(5);

                logger.LogInformation("Proxying Deichmann scraper request. RequestId={RequestId}", requestId);
                var resp = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, ct);
                logger.LogInformation("Deichmann scraper response received. RequestId={RequestId}, StatusCode={StatusCode}", requestId, (int)resp.StatusCode);

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync(ct);
                    logger.LogWarning("Deichmann scraper returned {Status}: {Body}", resp.StatusCode, body);
                    return Results.Problem(detail: $"Scraper service returned {resp.StatusCode}: {body}", statusCode: 502);
                }

                var stream = await resp.Content.ReadAsStreamAsync(ct);
                httpContext.Response.Headers["X-Request-ID"] = requestId;
                httpContext.Response.RegisterForDispose(resp);
                httpContext.Response.RegisterForDispose(stream);
                return Results.Stream(stream, "application/json");
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "Failed to call Deichmann scraper service");
                return Results.Problem(detail: ex.Message, statusCode: 503);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(ex, "Deichmann scraper timed out");
                return Results.Problem(detail: "Scraper service timed out", statusCode: 504);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error proxying Deichmann scraper");
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        })
        .WithName("ProxyDeichmannScraper")
        .WithTags("External");

        // ===== AboutYou ad-hoc scraper proxy =====
        app.MapPost("/api/scrapers/aboutyou", async (
            HttpContext httpContext,
            IHttpClientFactory httpClientFactory,
            ILogger<Program> logger,
            System.Text.Json.JsonElement filters,
            CancellationToken ct) =>
        {
            var requestId = httpContext.Request.Headers["X-Request-ID"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(requestId))
            {
                requestId = Guid.NewGuid().ToString("N");
            }

            using var __ = LogContext.PushProperty("RequestId", requestId);
            try
            {
                var client = httpClientFactory.CreateClient("scraper");

                var json = filters.GetRawText();
                var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/scrapers/aboutyou")
                {
                    Content = content
                };
                requestMessage.Headers.TryAddWithoutValidation("X-Request-ID", requestId);

                // Use extended timeout for scraping operations
                client.Timeout = TimeSpan.FromMinutes(5);

                logger.LogInformation("Proxying AboutYou scraper request. RequestId={RequestId}", requestId);
                var resp = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, ct);
                logger.LogInformation("AboutYou scraper response received. RequestId={RequestId}, StatusCode={StatusCode}", requestId, (int)resp.StatusCode);

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync(ct);
                    logger.LogWarning("AboutYou scraper returned {Status}: {Body}", resp.StatusCode, body);
                    return Results.Problem(detail: $"Scraper service returned {resp.StatusCode}: {body}", statusCode: 502);
                }

                var stream = await resp.Content.ReadAsStreamAsync(ct);
                httpContext.Response.Headers["X-Request-ID"] = requestId;
                httpContext.Response.RegisterForDispose(resp);
                httpContext.Response.RegisterForDispose(stream);
                return Results.Stream(stream, "application/json");
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "Failed to call AboutYou scraper service");
                return Results.Problem(detail: ex.Message, statusCode: 503);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(ex, "AboutYou scraper timed out");
                return Results.Problem(detail: "Scraper service timed out", statusCode: 504);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error proxying AboutYou scraper");
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        })
        .WithName("ProxyAboutYouScraper")
        .WithTags("External");

        // ===== Humanic ad-hoc scraper proxy =====
        app.MapPost("/api/scrapers/humanic", async (
            HttpContext httpContext,
            IHttpClientFactory httpClientFactory,
            ILogger<Program> logger,
            System.Text.Json.JsonElement filters,
            CancellationToken ct) =>
        {
            var requestId = httpContext.Request.Headers["X-Request-ID"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(requestId))
            {
                requestId = Guid.NewGuid().ToString("N");
            }

            using var __ = LogContext.PushProperty("RequestId", requestId);
            try
            {
                var client = httpClientFactory.CreateClient("scraper");

                var json = filters.GetRawText();
                var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/scrapers/humanic")
                {
                    Content = content
                };
                requestMessage.Headers.TryAddWithoutValidation("X-Request-ID", requestId);

                // Use extended timeout for scraping operations
                client.Timeout = TimeSpan.FromMinutes(5);

                logger.LogInformation("Proxying Humanic scraper request. RequestId={RequestId}", requestId);
                var resp = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, ct);
                logger.LogInformation("Humanic scraper response received. RequestId={RequestId}, StatusCode={StatusCode}", requestId, (int)resp.StatusCode);

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync(ct);
                    logger.LogWarning("Humanic scraper returned {Status}: {Body}", resp.StatusCode, body);
                    return Results.Problem(detail: $"Scraper service returned {resp.StatusCode}: {body}", statusCode: 502);
                }

                var stream = await resp.Content.ReadAsStreamAsync(ct);
                httpContext.Response.Headers["X-Request-ID"] = requestId;
                httpContext.Response.RegisterForDispose(resp);
                httpContext.Response.RegisterForDispose(stream);
                return Results.Stream(stream, "application/json");
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "Failed to call Humanic scraper service");
                return Results.Problem(detail: ex.Message, statusCode: 503);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(ex, "Humanic scraper timed out");
                return Results.Problem(detail: "Scraper service timed out", statusCode: 504);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error proxying Humanic scraper");
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        })
        .WithName("ProxyHumanicScraper")
        .WithTags("External");

        // ============ UPLOAD IMAGE ============
        
        app.MapPost("/api/upload-image", async (
            HttpContext context,
            ITrendplusDbContext db,
            IEmbeddingService embeddingService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            try
            {
                // Check if request contains a file
                if (!context.Request.HasFormContentType || context.Request.Form.Files.Count == 0)
                {
                    return Results.BadRequest(new { message = "Image missing" });
                }

                var image = context.Request.Form.Files["image"];
                if (image == null || image.Length == 0)
                {
                    return Results.BadRequest(new { message = "Image missing or empty" });
                }

                // Get optional productId from query or form
                var productIdStr = context.Request.Query["productId"].FirstOrDefault() 
                                  ?? context.Request.Form["productId"].FirstOrDefault();
                
                int? productId = null;
                if (!string.IsNullOrWhiteSpace(productIdStr) && int.TryParse(productIdStr, out var pid))
                {
                    productId = pid;
                }

                // Validate file type
                var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
                if (!AllowedImageExtensions.Contains(extension))
                {
                    return Results.BadRequest(new { message = "Invalid file type. Allowed: jpg, jpeg, png, gif, webp" });
                }

                // Validate file size (max 10MB)
                if (image.Length > 10 * 1024 * 1024)
                {
                    return Results.BadRequest(new { message = "File too large. Maximum size is 10MB" });
                }

                // Create uploads directory if it doesn't exist
                var uploadsDir = Path.Combine("wwwroot", "product-images");
                Directory.CreateDirectory(uploadsDir);

                // Generate unique filename
                var fileName = $"{Guid.NewGuid()}{extension}";
                var fullPath = Path.Combine(uploadsDir, fileName);

                // Save file
                using (var fs = new FileStream(fullPath, FileMode.Create))
                {
                    await image.CopyToAsync(fs, ct);
                }

                logger.LogInformation("Image uploaded successfully: {FileName} (Size: {Size} bytes)", fileName, image.Length);

                // TODO: Generate embedding vector when Python service is deployed
                // For now, skip embedding generation to avoid database mapping errors
                
                // Update product in database if productId is provided
                if (productId.HasValue)
                {
                    var artikal = await db.Artikli.FindAsync(new object[] { productId.Value }, ct);
                    if (artikal != null)
                    {
                        // Delete old image if exists
                        if (!string.IsNullOrEmpty(artikal.ImagePath))
                        {
                            var oldImagePath = Path.Combine(uploadsDir, artikal.ImagePath);
                            if (File.Exists(oldImagePath))
                            {
                                try
                                {
                                    File.Delete(oldImagePath);
                                    logger.LogInformation("Deleted old image: {OldImage}", artikal.ImagePath);
                                }
                                catch (Exception ex)
                                {
                                    logger.LogWarning(ex, "Failed to delete old image: {OldImage}", artikal.ImagePath);
                                }
                            }
                        }

                        // Save new image path to Artikli table
                        artikal.ImagePath = fileName;
                        
                        await db.SaveChangesAsync(ct);
                        
                        logger.LogInformation("Image {FileName} associated with product ID {ProductId}", fileName, productId.Value);
                    }
                    else
                    {
                        logger.LogWarning("Product ID {ProductId} not found", productId.Value);
                        return Results.NotFound(new { message = $"Product with ID {productId} not found" });
                    }
                }

                return Results.Ok(new 
                { 
                    success = true,
                    fileName = fileName,
                    imageUrl = $"/product-images/{fileName}",
                    productId = productId,
                    hasEmbedding = false, // Will be true when Python service is integrated
                    message = "Image uploaded successfully"
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error uploading image");
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 500,
                    title: "Error uploading image"
                );
            }
        })
        .DisableAntiforgery()
        .Accepts<IFormFile>("multipart/form-data")
        .WithName("UploadProductImage")
        .WithTags("Upload");

        // ============ GET PRODUCT IMAGE ============
        
        app.MapGet("/product-images/{fileName}", (string fileName) =>
        {
            var imagePath = Path.Combine("wwwroot", "product-images", fileName);
            
            if (!File.Exists(imagePath))
            {
                return Results.NotFound(new { message = "Image not found" });
            }

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var contentType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };

            var fileStream = File.OpenRead(imagePath);
            return Results.File(fileStream, contentType);
        })
        .WithName("GetProductImage")
        .WithTags("Upload");

        // ============ DELETE PRODUCT IMAGE ============
        
        app.MapDelete("/api/product-images/{productId:int}", async (
            int productId,
            ITrendplusDbContext db,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            try
            {
                var artikal = await db.Artikli.FindAsync(new object[] { productId }, ct);
                if (artikal == null)
                {
                    return Results.NotFound(new { message = "Product not found" });
                }

                if (string.IsNullOrEmpty(artikal.ImagePath))
                {
                    return Results.Ok(new { message = "No image to delete" });
                }

                // Delete physical file
                var uploadsDir = Path.Combine("wwwroot", "product-images");
                var imagePath = Path.Combine(uploadsDir, artikal.ImagePath);
                
                if (File.Exists(imagePath))
                {
                    File.Delete(imagePath);
                    logger.LogInformation("Deleted image file: {ImagePath}", artikal.ImagePath);
                }

                // Remove from database
                artikal.ImagePath = null;
                
                // Also delete from ProductImages table
                var productImages = await db.ProductImages
                    .Where(pi => pi.ProductId == productId)
                    .ToListAsync(ct);
                    
                db.ProductImages.RemoveRange(productImages);
                await db.SaveChangesAsync(ct);

                return Results.Ok(new 
                { 
                    success = true,
                    message = "Image deleted successfully" 
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting image for product {ProductId}", productId);
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 500,
                    title: "Error deleting image"
                );
            }
        })
        .WithName("DeleteProductImage")
        .WithTags("Upload");

        // ============ SIMILARITY SEARCH ============
        
        app.MapPost("/api/search-similar-images", async (
            HttpContext context,
            ITrendplusDbContext db,
            IEmbeddingService embeddingService,
            ILogger<Program> logger,
            float threshold = 0.8f,
            int limit = 10,
            CancellationToken ct = default) =>
        {
            try
            {
                if (!context.Request.HasFormContentType || context.Request.Form.Files.Count == 0)
                {
                    return Results.BadRequest(new { message = "Image missing" });
                }

                var image = context.Request.Form.Files["image"];
                if (image == null || image.Length == 0)
                {
                    return Results.BadRequest(new { message = "Image missing or empty" });
                }

                // Save temp file
                var tempDir = Path.Combine(Path.GetTempPath(), "trendplus-search");
                Directory.CreateDirectory(tempDir);
                
                var tempFileName = $"{Guid.NewGuid()}{Path.GetExtension(image.FileName)}";
                var tempPath = Path.Combine(tempDir, tempFileName);

                using (var fs = new FileStream(tempPath, FileMode.Create))
                {
                    await image.CopyToAsync(fs, ct);
                }

                try
                {
                    // Generate embedding for query image
                    var queryEmbedding = await embeddingService.GetEmbeddingAsync(tempPath, ct);
                    logger.LogInformation("Generated query embedding with {Dimensions} dimensions", queryEmbedding.Length);

                    // Find similar products
                    var similarProducts = await embeddingService.FindSimilarProductsAsync(
                        queryEmbedding, 
                        threshold, 
                        limit, 
                        ct);

                    logger.LogInformation("Found {Count} similar products", similarProducts.Count);

                    return Results.Ok(new
                    {
                        success = true,
                        queryImageSize = image.Length,
                        threshold,
                        limit,
                        results = similarProducts.Select(sp => new
                        {
                            sp.ProductId,
                            sp.ProductName,
                            sp.Similarity,
                            imageUrl = $"/product-images/{sp.ImageFileName}"
                        }).ToList()
                    });
                }
                finally
                {
                    // Cleanup temp file
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error searching similar images");
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 500,
                    title: "Error searching similar images"
                );
            }
        })
        .DisableAntiforgery()
        .Accepts<IFormFile>("multipart/form-data")
        .WithName("SearchSimilarImages")
        .WithTags("Upload", "AI");

        // ===== Merged endpoints from Trendplus2/Endpoints/AllEndpoints.cs START =====

        // ADMIN: Sync Analytics DB (from Trendplus endpoints)
        app.MapPost("/api/admin/sync-analytics-db", async (
            ITrendplusDbContext trendDb,
            IAnalyticsDbContext analyticsDb,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            try
            {
                logger.LogInformation("🚀 Starting Analytics DB synchronization...");

                var sales = await trendDb.ProdajaZaglavlja
                    .Include(p => p.Stavke)
                    .AsNoTracking()
                    .ToListAsync(ct);

                if (sales.Count == 0)
                {
                    return Results.Ok(new { message = "No sales to sync" });
                }

                logger.LogInformation("Found {SalesCount} sales to sync.", sales.Count);

                var existingSaleIds = await analyticsDb.SalesFacts
                    .Select(s => s.SaleId)
                    .ToListAsync(ct);

                var existingSaleIdsSet = new HashSet<int>(existingSaleIds);

                var newSalesFacts = new List<SalesFact>();
                var newSalesLineFacts = new List<SalesLineFact>();

                int syncedCount = 0;

                foreach (var sale in sales)
                {
                    if (existingSaleIdsSet.Contains(sale.Id))
                        continue;

                    decimal totalAmount = sale.Stavke.Sum(s => s.Kolicina * s.Cena);
                    int totalUnits = sale.Stavke.Sum(s => s.Kolicina);
                    int totalLines = sale.Stavke.Count;

                    var fact = new SalesFact
                    {
                        SaleId = sale.Id,
                        BrojRacuna = sale.BrojRacuna ?? "N/A",
                        SaleTimestampUtc = DateTime.SpecifyKind(sale.DatumProdaje, DateTimeKind.Utc),
                        StoreId = sale.IDObjekat ?? 1,
                        PaymentType = sale.NacinPlacanja ?? "Unknown",
                        TotalAmount = totalAmount,
                        TotalUnits = totalUnits,
                        TotalLines = totalLines
                    };

                    newSalesFacts.Add(fact);

                    foreach (var line in sale.Stavke)
                    {
                        newSalesLineFacts.Add(new SalesLineFact
                        {
                            SaleId = sale.Id,
                            ProductId = line.IdArtikal,
                            Qty = line.Kolicina,
                            UnitPrice = line.Cena,
                            LineTotal = line.Kolicina * line.Cena
                        });
                    }

                    syncedCount++;
                }

                if (newSalesFacts.Count > 0)
                {
                    await analyticsDb.SalesFacts.AddRangeAsync(newSalesFacts, ct);
                    await analyticsDb.SalesLineFacts.AddRangeAsync(newSalesLineFacts, ct);
                    await analyticsDb.SaveChangesAsync(ct);
                }

                logger.LogInformation("✅ Synced {SyncedCount} new sales to Analytics DB.", syncedCount);

                return Results.Ok(new { success = true, message = $"Synced {syncedCount} new sales (Total sales in source: {sales.Count})" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Failed to sync Analytics DB");
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Failed to sync Analytics DB");
            }
        })
        .RequireRateLimiting("strict");

        // ============ DNEVNIK PROMENA ============
        app.MapGet("/api/dnevnik-promena/tipovi", async (ITrendplusDbContext db, CancellationToken ct) =>
        {
            var tipovi = await db.DnevnikPromena
                .Select(x => x.TipPromene)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(ct);

            return Results.Ok(tipovi);
        })
        .RequireRateLimiting("fixed");

        app.MapGet("/api/dnevnik-promena", async (
            IDnevnikPromenaReadService readService,
            ILogger<Program> logger,
            int pageNumber = 1,
            int pageSize = 50,
            string? tipPromene = null,
            int? artikalId = null,
            string? naziv = null,
            string? brojRacuna = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string sortBy = "datum",
            string sortDir = "desc",
            string dataScope = "all",
            CancellationToken ct = default) =>
        {
            try
            {
                logger.LogInformation(
                    "Fetching DnevnikPromena list page {PageNumber} size {PageSize} sort {SortBy}/{SortDir}",
                    pageNumber,
                    pageSize,
                    sortBy,
                    sortDir);

                if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                    fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

                if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                    toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

                var response = await readService.GetPagedAsync(new DnevnikPromenaListQuery
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TipPromene = tipPromene,
                    ArtikalId = artikalId,
                    Naziv = naziv,
                    BrojRacuna = brojRacuna,
                    FromDate = fromDate,
                    ToDate = toDate,
                    SortBy = sortBy,
                    SortDir = sortDir,
                    DataScope = dataScope
                }, ct);

                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Greška pri učitavanju dnevnika promena");
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri učitavanju dnevnika promena");
            }
        })
        .RequireRateLimiting("db-heavy");

        app.MapGet("/api/dnevnik-promena/{id:int}", async (
            int id,
            IDnevnikPromenaReadService detailService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            logger.LogInformation("Fetching DnevnikPromena detail for {Id}", id);

            var detail = await detailService.GetByIdAsync(id, ct);
            if (detail is null)
            {
                logger.LogWarning("DnevnikPromena detail not found for {Id}", id);
                return Results.NotFound(new { message = $"DnevnikPromena sa Id={id} nije pronađen." });
            }

            logger.LogInformation("DnevnikPromena detail fetched for {Id}", id);
            return Results.Ok(detail);
        })
        .RequireRateLimiting("fixed");

        // ============ ARTIKLI ============
        app.MapGet("/artikli", async (IMediator mediator, CancellationToken ct) =>
        {
            var query = new GetArtikliQuery();
            var result = await mediator.Send(query, ct);
            return Results.Ok(result);
        })
        .RequireRateLimiting("fixed");

        app.MapGet("/api/artikli", async (
            ITrendplusDbContext db,
            IMemoryCache cache,
            ILogger<Program> logger,
            int pageNumber = 1,
            int pageSize = 50,
            string? naziv = null,
            int? sezonaId = null,
            int? dobavljacId = null,
            decimal? minCena = null,
            decimal? maxCena = null,
            decimal? minKolicina = null,
            decimal? maxKolicina = null,
            string sortBy = "naziv",
            string sortDir = "asc",
            string dataScope = "all",
            CancellationToken ct = default) =>
        {
            try
            {
                var normalizedSortBy = (sortBy ?? "naziv").ToLowerInvariant();
                var normalizedSortDir = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";
                var normalizedDataScope = (dataScope ?? "all").Trim().ToLowerInvariant();
                pageNumber = pageNumber < 1 ? 1 : pageNumber;
                pageSize = pageSize < 1 ? 50 : Math.Min(pageSize, 200);

                var responseCacheKey =
                    $"artikli_page_{pageNumber}_{pageSize}_{naziv}_{sezonaId}_{dobavljacId}_{minCena}_{maxCena}_{minKolicina}_{maxKolicina}_{normalizedSortBy}_{normalizedSortDir}_{normalizedDataScope}";

                if (cache.TryGetValue(responseCacheKey, out object? cachedResponse) && cachedResponse is not null)
                    return Results.Ok(cachedResponse);

                var baseQuery = from a in db.Artikli.AsNoTracking()
                                join d in db.Dobavljaci.AsNoTracking() on a.IDDobavljac equals d.Id into dob
                                from dobavljac in dob.DefaultIfEmpty()
                                select new
                                {
                                    a.Id,
                                    a.PLU,
                                    a.Naziv,
                                    a.NabavnaCena,
                                    a.ProdajnaCena,
                                    a.Kolicina,
                                    a.Velicina,
                                    a.Boja,
                                    TipObuceId = a.IDTipObuce,
                                    DobavljacId = a.IDDobavljac,
                                    DobavljacNaziv = dobavljac != null ? dobavljac.Naziv : null,
                                    IdSezona = a.IDSezona,
                                    a.Kategorija,
                                    a.Pol,
                                    a.DataOrigin
                                };

                if (!string.IsNullOrWhiteSpace(naziv))
                    baseQuery = baseQuery.Where(a => a.Naziv.Contains(naziv));

                if (sezonaId.HasValue)
                    baseQuery = baseQuery.Where(a => a.IdSezona == sezonaId.Value);

                if (dobavljacId.HasValue)
                    baseQuery = baseQuery.Where(a => a.DobavljacId == dobavljacId.Value);

                if (minCena.HasValue)
                    baseQuery = baseQuery.Where(a => a.ProdajnaCena >= minCena.Value);

                if (maxCena.HasValue)
                    baseQuery = baseQuery.Where(a => a.ProdajnaCena <= maxCena.Value);

                if (minKolicina.HasValue)
                    baseQuery = baseQuery.Where(a => a.Kolicina >= minKolicina.Value);

                if (maxKolicina.HasValue)
                    baseQuery = baseQuery.Where(a => a.Kolicina <= maxKolicina.Value);

                baseQuery = normalizedDataScope switch
                {
                    "imported" => baseQuery.Where(a => a.DataOrigin == "access"),
                    "existing" => baseQuery.Where(a => a.DataOrigin == "existing" || a.DataOrigin == null || a.DataOrigin == ""),
                    _ => baseQuery
                };

                var filterHash = $"{naziv}_{sezonaId}_{dobavljacId}_{minCena}_{maxCena}_{minKolicina}_{maxKolicina}_{normalizedDataScope}";
                var cacheKey = $"artikli_count_{filterHash}";

                if (!cache.TryGetValue(cacheKey, out int total))
                {
                    total = await baseQuery.CountAsync(ct);
                    cache.Set(cacheKey, total, TimeSpan.FromMinutes(2));
                }

                baseQuery = normalizedSortBy switch
                {
                    "prodajnacena" => normalizedSortDir == "asc" ? baseQuery.OrderBy(a => a.ProdajnaCena) : baseQuery.OrderByDescending(a => a.ProdajnaCena),
                    "nabavnacena"  => normalizedSortDir == "asc" ? baseQuery.OrderBy(a => a.NabavnaCena)  : baseQuery.OrderByDescending(a => a.NabavnaCena),
                    "kolicina"     => normalizedSortDir == "asc" ? baseQuery.OrderBy(a => a.Kolicina)     : baseQuery.OrderByDescending(a => a.Kolicina),
                    "id"           => normalizedSortDir == "asc" ? baseQuery.OrderBy(a => a.Id)           : baseQuery.OrderByDescending(a => a.Id),
                    "dobavljac"    => normalizedSortDir == "asc" ? baseQuery.OrderBy(a => a.DobavljacNaziv) : baseQuery.OrderByDescending(a => a.DobavljacNaziv),
                    _              => normalizedSortDir == "asc" ? baseQuery.OrderBy(a => a.Naziv)        : baseQuery.OrderByDescending(a => a.Naziv)
                };

                var items = await baseQuery.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);
                var response = new { items, totalCount = total, pageNumber, pageSize };
                cache.Set(responseCacheKey, response, TimeSpan.FromSeconds(30));

                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Greška pri učitavanju artikala");
                var rootMessage = ex.GetBaseException().Message;
                return Results.Problem(detail: rootMessage, statusCode: 500, title: "Greška pri učitavanju artikala");
            }
        })
        .RequireRateLimiting("db-heavy");

        app.MapGet("/api/artikli/lookup", async (
            ITrendplusDbContext db,
            string? q = null,
            int take = 50,
            bool includeZeroStock = false,
            string dataScope = "all",
            CancellationToken ct = default) =>
        {
            take = Math.Clamp(take, 1, 200);
            var normalizedDataScope = (dataScope ?? "all").Trim().ToLowerInvariant();

            var query = db.Artikli.AsNoTracking().AsQueryable();

            if (!includeZeroStock)
                query = query.Where(a => (a.Kolicina ?? 0) > 0);

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(a => a.Naziv.Contains(q));

            query = normalizedDataScope switch
            {
                "imported" => query.Where(a => a.DataOrigin == "access"),
                "existing" => query.Where(a => a.DataOrigin == "existing" || a.DataOrigin == null || a.DataOrigin == ""),
                _ => query
            };

            var items = await query
                .OrderBy(a => a.Naziv)
                .Select(a => new
                {
                    id = a.Id,
                    naziv = a.Naziv,
                    cena = a.ProdajnaCena ?? 0m,
                    kolicina = a.Kolicina ?? 0
                })
                .Take(take)
                .ToListAsync(ct);

            return Results.Ok(items);
        })
        .RequireRateLimiting("db-heavy");

        app.MapGet("/artikli/{id:int}", async (int id, IMediator mediator, CancellationToken ct) =>
        {
            try
            {
                var query = new GetArtikalQuery(id);
                var result = await mediator.Send(query, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { message = "Artikal nije pronađen" });
            }
        })
        .RequireRateLimiting("fixed");

        app.MapPost("/artikli", async (CreateArtikalCommand command, IMediator mediator, CancellationToken ct) =>
        {
            try
            {
                var id = await mediator.Send(command, ct);
                return Results.Ok(new { id });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri kreiranju artikla");
            }
        })
        .RequireRateLimiting("writes");

        app.MapPut("/artikli/{id:int}", async (int id, UpdateArtikalCommand command, IMediator mediator, CancellationToken ct) =>
        {
            if (id != command.Id)
                return Results.BadRequest(new { message = "ID ne odgovara" });

            try
            {
                await mediator.Send(command, ct);
                return Results.Ok(new { success = true });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { message = "Artikal nije pronađen" });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri ažuriranju artikla");
            }
        })
        .RequireRateLimiting("writes");

        // ============ SEZONE ============
        app.MapGet("/api/sezone", async (ITrendplusDbContext db, IMemoryCache cache, ILogger<Program> logger, string dataScope = "all", CancellationToken ct = default) =>
        {
            try
            {
                var normalizedDataScope = (dataScope ?? "all").Trim().ToLowerInvariant();
                var cacheKey = $"sezone_all_{normalizedDataScope}";
                if (cache.TryGetValue(cacheKey, out List<Sezona>? cachedSezone) && cachedSezone is not null)
                {
                    return Results.Ok(cachedSezone);
                }

                var query = db.Sezone.AsNoTracking().AsQueryable();
                query = normalizedDataScope switch
                {
                    "imported" => query.Where(s => s.DataOrigin == "access"),
                    "existing" => query.Where(s => s.DataOrigin == "existing" || s.DataOrigin == null || s.DataOrigin == ""),
                    _ => query
                };

                var sezone = await query.OrderBy(s => s.Naziv).ToListAsync(ct);
                cache.Set(cacheKey, sezone, TimeSpan.FromMinutes(10));
                return Results.Ok(sezone);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Greška pri učitavanju sezona");
                var rootMessage = ex.GetBaseException().Message;
                return Results.Problem(detail: rootMessage, statusCode: 500, title: "Greška pri učitavanju sezona");
            }
        })
        .RequireRateLimiting("fixed");

        // ============ TIPOVI OBUCE ============
        app.MapGet("/api/tipovi-obuce", async (
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            try
            {
                var result = await mediator.Send(new GetTipObuceQuery(), ct);
                var ordered = result
                    .OrderBy(t => t.Naziv)
                    .ToList();

                return Results.Ok(ordered);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Greska pri ucitavanju tipova obuce");
                return Results.Problem(detail: ex.GetBaseException().Message, statusCode: 500, title: "Greska pri ucitavanju tipova obuce");
            }
        })
        .RequireRateLimiting("fixed");

        app.MapPost("/api/tipovi-obuce", async (
            CreateTipObuceCommand request,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Naziv))
                {
                    return Results.BadRequest(new { message = "Naziv tipa obuce je obavezan." });
                }

                var normalizedRequest = request with { Naziv = request.Naziv.Trim() };
                var id = await mediator.Send(normalizedRequest, ct);

                logger.LogInformation("Kreiran tip obuce {TipObuceId} sa nazivom {Naziv}", id, normalizedRequest.Naziv);
                return Results.Ok(new { id });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Greska pri kreiranju tipa obuce");
                return Results.Problem(detail: ex.GetBaseException().Message, statusCode: 500, title: "Greska pri kreiranju tipa obuce");
            }
        })
        .RequireRateLimiting("writes");

        // ============ DOBAVLJACI ============
        app.MapGet("/api/dobavljaci", async (ITrendplusDbContext db, string dataScope = "all", CancellationToken ct = default) =>
        {
            var normalizedDataScope = (dataScope ?? "all").Trim().ToLowerInvariant();
            var query = db.Dobavljaci.AsNoTracking().AsQueryable();
            query = normalizedDataScope switch
            {
                "imported" => query.Where(d => d.DataOrigin == "access"),
                "existing" => query.Where(d => d.DataOrigin == "existing" || d.DataOrigin == null || d.DataOrigin == ""),
                _ => query
            };

            var result = await query.OrderBy(d => d.Naziv).ToListAsync(ct);
            return Results.Ok(result);
        })
        .RequireRateLimiting("fixed");

        // ============ NIVELACIJE ============
        app.MapPost("/api/nivelacija", async (
            ITrendplusDbContext db,
            ILogger<Program> logger,
            NivelacijaRequest request,
            CancellationToken ct) =>
        {
            try
            {
                logger.LogInformation("Nivelacija cene za artikal {ArtikalId}", request.ArtikalId);

                var artikal = await db.Artikli.FindAsync(new object[] { request.ArtikalId }, ct);
                if (artikal == null)
                    return Results.NotFound(new { message = "Artikal nije pronađen" });

                var staraCena = artikal.ProdajnaCena;
                artikal.ProdajnaCena = request.NovaProdajnaCena;

                db.DnevnikPromena.Add(new DnevnikPromena
                {
                    TipPromene = TipPromeneConstants.NivelacijaCena,
                    Datum = DateTime.UtcNow,
                    Iznos = 0,
                    ArtikalId = artikal.Id,
                    StaraProdajnaCena = staraCena,
                    NovaProdajnaCena = request.NovaProdajnaCena,
                    Komentar = request.Komentar,
                    KorisnikIme = "System"
                });

                await db.SaveChangesAsync(ct);

                return Results.Ok(new { success = true, message = "Cena uspešno nivelirana" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Greška pri nivelaciji cene");
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri nivelaciji cene");
            }
        })
        .RequireRateLimiting("writes");

        app.MapGet("/api/nivelacije", async (
            ITrendplusDbContext db,
            int pageNumber = 1,
            int pageSize = 50,
            int? artikalId = null,
            string? naziv = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string sortBy = "datum",
            string sortDir = "desc",
            CancellationToken ct = default) =>
        {
            try
            {
                if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                    fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

                if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                    toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

                var query = from dp in db.DnevnikPromena.AsNoTracking()
                            join a in db.Artikli.AsNoTracking() on dp.ArtikalId equals a.Id into artikli
                            from artikal in artikli.DefaultIfEmpty()
                            where dp.TipPromene == TipPromeneConstants.NivelacijaCena
                            select new
                            {
                                dp.Id,
                                dp.Datum,
                                dp.ArtikalId,
                                ArtikalNaziv = artikal != null ? artikal.Naziv : null,
                                dp.StaraProdajnaCena,
                                dp.NovaProdajnaCena,
                                dp.Komentar,
                                dp.KorisnikIme
                            };

                if (artikalId.HasValue)
                    query = query.Where(x => x.ArtikalId == artikalId.Value);

                if (!string.IsNullOrWhiteSpace(naziv))
                    query = query.Where(x => x.ArtikalNaziv != null && x.ArtikalNaziv.Contains(naziv));

                if (fromDate.HasValue)
                    query = query.Where(x => x.Datum >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(x => x.Datum <= toDate.Value);

                query = sortBy.ToLower(CultureInfo.InvariantCulture) switch
                {
                    "naziv" => sortDir == "asc" ? query.OrderBy(x => x.ArtikalNaziv) : query.OrderByDescending(x => x.ArtikalNaziv),
                    "stara_cena" => sortDir == "asc" ? query.OrderBy(x => x.StaraProdajnaCena) : query.OrderByDescending(x => x.StaraProdajnaCena),
                    "nova_cena" => sortDir == "asc" ? query.OrderBy(x => x.NovaProdajnaCena) : query.OrderByDescending(x => x.NovaProdajnaCena),
                    _ => sortDir == "asc" ? query.OrderBy(x => x.Datum) : query.OrderByDescending(x => x.Datum)
                };

                var total = await query.CountAsync(ct);
                var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);

                return Results.Ok(new { items, totalCount = total, pageNumber, pageSize });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri učitavanju nivelacija");
            }
        })
        .RequireRateLimiting("db-heavy");

        // ============ POVRACAJ ROBE ============
        app.MapPost("/api/povracaj", async (
            IMediator mediator,
            ILogger<Program> logger,
            KreirajPovracajCommand command,
            CancellationToken ct) =>
        {
            try
            {
                logger.LogInformation("Creating return note for supplier {SupplierId}", command.IDDobavljac);

                var response = await mediator.Send(command, ct);

                return Results.Ok(new
                {
                    success = true,
                    povracajId = response.PovracajId,
                    brojZapisnika = response.BrojZapisnika,
                    ukupanIznos = response.UkupanIznos,
                    message = $"Zapisnik o povracaju {response.BrojZapisnika} uspesno kreiran"
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating return note");
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greska pri kreiranju povracaja");
            }
        })
        .RequireRateLimiting("writes");

        app.MapGet("/api/povracaj", async (
            ITrendplusDbContext db,
            int pageNumber = 1,
            int pageSize = 50,
            int? dobavljacId = null,
            string? status = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken ct = default) =>
        {
            try
            {
                if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                    fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

                if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                    toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

                var query = from p in db.PovracajZaglavlja.AsNoTracking()
                            join d in db.Dobavljaci.AsNoTracking() on p.IDDobavljac equals d.Id
                            select new { p, d };

                if (dobavljacId.HasValue)
                    query = query.Where(x => x.p.IDDobavljac == dobavljacId.Value);

                if (!string.IsNullOrWhiteSpace(status))
                    query = query.Where(x => x.p.Status == status);

                if (fromDate.HasValue)
                    query = query.Where(x => x.p.DatumPovracaja >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(x => x.p.DatumPovracaja <= toDate.Value);

                var total = await query.CountAsync(ct);

                var items = await query
                    .OrderByDescending(x => x.p.DatumPovracaja)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new
                    {
                        id = x.p.Id,
                        brojZapisnika = x.p.BrojZapisnika,
                        datumPovracaja = x.p.DatumPovracaja,
                        dobavljacId = x.p.IDDobavljac,
                        dobavljacNaziv = x.d.Naziv,
                        razlogPovracaja = x.p.RazlogPovracaja,
                        status = x.p.Status,
                        ukupanIznos = x.p.UkupanIznos,
                        brojStavki = x.p.Stavke.Count,
                        kreatorKorisnik = x.p.KreatorKorisnik
                    })
                    .ToListAsync(ct);

                return Results.Ok(new { items, totalCount = total, pageNumber, pageSize });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greska pri ucitavanju povracaja");
            }
        })
        .RequireRateLimiting("db-heavy");

        app.MapGet("/api/povracaj/{id:int}", async (
            ITrendplusDbContext db,
            int id,
            CancellationToken ct) =>
        {
            try
            {
                var povracaj = await db.PovracajZaglavlja
                    .Include(p => p.Stavke)
                    .FirstOrDefaultAsync(p => p.Id == id, ct);

                if (povracaj == null)
                    return Results.NotFound(new { message = "Povracaj nije pronađen" });

                var dobavljac = await db.Dobavljaci.FindAsync(new object[] { povracaj.IDDobavljac }, ct);

                var artikalIds = povracaj.Stavke.Select(s => s.IdArtikal).ToList();
                var artikli = await db.Artikli
                    .Where(a => artikalIds.Contains(a.Id))
                    .ToDictionaryAsync(a => a.Id, ct);

                return Results.Ok(new
                {
                    id = povracaj.Id,
                    brojZapisnika = povracaj.BrojZapisnika,
                    datumPovracaja = povracaj.DatumPovracaja,
                    dobavljac = new
                    {
                        id = povracaj.IDDobavljac,
                        naziv = dobavljac?.Naziv
                    },
                    razlogPovracaja = povracaj.RazlogPovracaja,
                    status = povracaj.Status,
                    ukupanIznos = povracaj.UkupanIznos,
                    komentar = povracaj.Komentar,
                    kreatorKorisnik = povracaj.KreatorKorisnik,
                    datumKreiranja = povracaj.DatumKreiranja,
                    stavke = povracaj.Stavke.Select(s => new
                    {
                        id = s.Id,
                        artikal = new
                        {
                            id = s.IdArtikal,
                            naziv = artikli.TryGetValue(s.IdArtikal, out var artikal) ? artikal.Naziv : "N/A"
                        },
                        kolicina = s.Kolicina,
                        cena = s.Cena,
                        iznos = s.Kolicina * s.Cena,
                        razlog = s.Razlog,
                        stanjeArtikla = s.StanjeArtikla
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greska pri ucitavanju detalja povracaja");
            }
        })
        .RequireRateLimiting("fixed");

        // ============ PRODAJA ============
        app.MapPost("/api/prodaja", async (IMediator mediator, ILogger<Program> logger, ProdajArtikleCommand command, CancellationToken ct) =>
        {
            try
            {
                logger.LogInformation("Prodaja artikala");
                await mediator.Send(command, ct);
                return Results.Ok(new { success = true, message = "Prodaja uspešno evidentirana" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Greška pri prodaji");
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri prodaji");
            }
        })
        .RequireRateLimiting("writes");

        app.MapGet("/api/prodaja", async (IMediator mediator, DateTime? fromDate = null, DateTime? toDate = null, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) =>
        {
            try
            {
                var query = new GetProdajeQuery(fromDate, toDate, pageNumber, pageSize);
                var result = await mediator.Send(query, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri učitavanju prodaja");
            }
        })
        .RequireRateLimiting("db-heavy");

        // ============ SEASONAL IMAGES (PEXELS) ============
        app.MapGet("/api/trends/seasonal-images", async (
            IServiceProvider serviceProvider,
            IMemoryCache cache,
            ILogger<Program> logger) =>
        {
            try
            {
                const string cacheKey = "seasonal_images_pexels_v2";

                if (!cache.TryGetValue(cacheKey, out List<TrendImageDto>? images))
                {
                    var cfg = serviceProvider.GetRequiredService<IConfiguration>();
                    var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
                    var pexelsApiKey = cfg["Pexels:ApiKey"];

                    images = new List<TrendImageDto>();

                    if (!string.IsNullOrWhiteSpace(pexelsApiKey))
                    {
                        logger.LogInformation("Fetching seasonal images from Pexels API...");

                        var http = httpClientFactory.CreateClient("default");
                        using var request = new HttpRequestMessage(
                            HttpMethod.Get,
                            "https://api.pexels.com/v1/search?query=women%20shoes%20fashion&orientation=landscape&per_page=40");
                        request.Headers.TryAddWithoutValidation("Authorization", pexelsApiKey);

                        using var response = await http.SendAsync(request);
                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync();
                            using var doc = System.Text.Json.JsonDocument.Parse(json);

                            if (doc.RootElement.TryGetProperty("photos", out var photos) &&
                                photos.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                var idx = 1;
                                foreach (var photo in photos.EnumerateArray())
                                {
                                    if (!photo.TryGetProperty("src", out var srcObj))
                                        continue;

                                    var imageUrl = srcObj.TryGetProperty("large2x", out var large2x) ? large2x.GetString() :
                                                   srcObj.TryGetProperty("large", out var large) ? large.GetString() :
                                                   srcObj.TryGetProperty("original", out var original) ? original.GetString() : null;

                                    if (string.IsNullOrWhiteSpace(imageUrl))
                                        continue;

                                    var photographerName = photo.TryGetProperty("photographer", out var p) ? p.GetString() : null;
                                    var photographerUrl = photo.TryGetProperty("photographer_url", out var pu) ? pu.GetString() : null;
                                    var sourceUrl = photo.TryGetProperty("url", out var su) ? su.GetString() : null;

                                    images.Add(new TrendImageDto(
                                        idx++,
                                        imageUrl!,
                                        "pexels",
                                        photographerName,
                                        photographerUrl,
                                        sourceUrl
                                    ));
                                }
                            }
                        }
                        else
                        {
                            logger.LogWarning("Pexels API returned HTTP {StatusCode}", (int)response.StatusCode);
                        }
                    }
                    else
                    {
                        logger.LogWarning("Pexels API key missing. Falling back to static seasonal images.");
                    }

                    if (images.Count == 0)
                    {
                        // Stable fallback if Pexels is unavailable.
                        images.AddRange(SeasonalFallbackImageUrls.Select((url, i) => new TrendImageDto(i + 1, url, "unsplash")));
                    }

                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromHours(1));

                    cache.Set(cacheKey, images, cacheOptions);
                }

                var resultList = images?.ToList() ?? new List<TrendImageDto>();
                var shuffled = resultList.OrderBy(_ => Guid.NewGuid()).Take(20);

                return Results.Ok(shuffled);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Seasonal images FAILED");
                var fallback = new List<TrendImageDto>
                {
                    new TrendImageDto(1, "https://images.unsplash.com/photo-1460353581641-37baddab0fa2?w=1200&auto=format&fit=crop", "unsplash"),
                    new TrendImageDto(2, "https://images.unsplash.com/photo-1543163521-1bf539c55dd2?w=1200&auto=format&fit=crop", "unsplash")
                };
                return Results.Ok(fallback);
            }
        })
        .RequireRateLimiting("external-api");

        // ===== Merged endpoints END =====

        // ============ IMPORT ZALANDO (minimal API) ============
        app.MapPost("/api/products/import-zalando", async (
            ITrendplusDbContext db,
            HttpRequest request,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            try
            {
                // Log request metadata
                try
                {
                    var ua = request.Headers["User-Agent"].FirstOrDefault() ?? "-";
                    var remote = request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "-";
                    logger.LogInformation("ImportZalando called from {Remote} UA={UA}", remote, ua);
                }
                catch { }

                using var doc = await System.Text.Json.JsonDocument.ParseAsync(request.Body, cancellationToken: ct);
                if (doc == null || doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
                {
                    logger.LogWarning("ImportZalando: invalid payload - expected JSON array");
                    return Results.BadRequest(new { message = "Invalid payload - expected JSON array" });
                }

                var root = doc.RootElement;
                logger.LogInformation("ImportZalando: received payload with {Count} elements", root.GetArrayLength());

                var toAdd = new List<Domain.Model.Artikli>();
                var skipped = 0;
                var skippedReasons = new List<string>();

                foreach (var el in root.EnumerateArray())
                {
                    try
                    {
                        string name = string.Empty;
                        if (el.TryGetProperty("Name", out var pn) && pn.ValueKind == System.Text.Json.JsonValueKind.String) name = pn.GetString() ?? string.Empty;
                        else if (el.TryGetProperty("name", out var pnn) && pnn.ValueKind == System.Text.Json.JsonValueKind.String) name = pnn.GetString() ?? string.Empty;

                        string brand = string.Empty;
                        if (el.TryGetProperty("Brand", out var pb) && pb.ValueKind == System.Text.Json.JsonValueKind.String) brand = pb.GetString() ?? string.Empty;
                        else if (el.TryGetProperty("brand", out var pbb) && pbb.ValueKind == System.Text.Json.JsonValueKind.String) brand = pbb.GetString() ?? string.Empty;

                        decimal price = 0;
                        if (el.TryGetProperty("Price", out var pp) && pp.ValueKind != System.Text.Json.JsonValueKind.Null)
                        {
                            if (pp.ValueKind == System.Text.Json.JsonValueKind.Number && pp.TryGetDecimal(out var dec)) price = dec;
                            else if (pp.ValueKind == System.Text.Json.JsonValueKind.String && decimal.TryParse(pp.GetString(), out var d2)) price = d2;
                        }

                        string? imageUrl = null;
                        if (el.TryGetProperty("ImageUrl", out var pi) && pi.ValueKind == System.Text.Json.JsonValueKind.String) imageUrl = pi.GetString();
                        else if (el.TryGetProperty("image_url", out var pi2) && pi2.ValueKind == System.Text.Json.JsonValueKind.String) imageUrl = pi2.GetString();

                        string? url = null;
                        if (el.TryGetProperty("Url", out var pu) && pu.ValueKind == System.Text.Json.JsonValueKind.String) url = pu.GetString();
                        else if (el.TryGetProperty("url", out var pu2) && pu2.ValueKind == System.Text.Json.JsonValueKind.String) url = pu2.GetString();

                        if (string.IsNullOrWhiteSpace(name))
                        {
                            skipped++; skippedReasons.Add("missing name");
                            continue;
                        }

                        var product = new Domain.Model.Artikli
                        {
                            Naziv = name,
                            Kategorija = "zalando",
                            Pol = brand,
                            ProdajnaCena = price,
                            ImagePath = imageUrl,
                            Komentar = url,
                            UpdatedAt = DateTime.UtcNow
                        };

                        toAdd.Add(product);
                    }
                    catch (Exception exInner)
                    {
                        skipped++; skippedReasons.Add(exInner.Message);
                        logger.LogWarning(exInner, "Skipping malformed import item");
                    }
                }

                logger.LogInformation("ImportZalando: parsed {Valid} valid items, {Skipped} skipped", toAdd.Count, skipped);
                if (skippedReasons.Count > 0) logger.LogInformation("ImportZalando skipped reasons sample: {Reasons}", string.Join(";", skippedReasons.Take(5)));

                if (toAdd.Count == 0)
                {
                    logger.LogInformation("ImportZalando: no valid items to import");
                    return Results.Ok(new { count = 0 });
                }

                // Log first 5 items before insert
                logger.LogInformation("ImportZalando: inserting sample items: {Sample}", string.Join(" | ", toAdd.Take(5).Select(t => t.Naziv)));

                await db.Artikli.AddRangeAsync(toAdd, ct);
                await db.SaveChangesAsync(ct);

                // collect inserted ids (if Id is generated)
                var sampleIds = toAdd.Take(5).Select(t => t.Id).ToList();
                var importedNames = toAdd.Select(t => t.Naziv).Take(20).ToList();

                logger.LogInformation("Imported {Count} Zalando products. Sample IDs: {Ids}", toAdd.Count, string.Join(",", sampleIds));
                return Results.Ok(new { count = toAdd.Count, sampleIds, importedNames });
            }
            catch (System.Text.Json.JsonException jex)
            {
                logger.LogError(jex, "ImportZalando: JSON parse error");
                return Results.BadRequest(new { message = "Invalid JSON payload" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error importing Zalando products");
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Import failed");
            }
        })
        .WithName("ImportZalando")
        .WithTags("Import");

        // GET products with optional source filter
        app.MapGet("/api/products", async (ITrendplusDbContext db, string? source, CancellationToken ct) =>
        {
            var query = db.Artikli.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(source))
            {
                if (source.Equals("zalando", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(a => a.Kategorija == "zalando");
                }
                else
                {
                    query = query.Where(a => a.Kategorija == source);
                }
            }

            var items = await query.Select(a => new
            {
                id = a.Id,
                name = a.Naziv,
                brand = a.Pol,
                price = a.ProdajnaCena,
                imageUrl = a.ImagePath,
                url = a.Komentar
            }).ToListAsync(ct);

            return Results.Ok(items);
        })
        .WithName("GetProducts")
        .WithTags("Products");

        // Debug: count of products by source (helps verify imports)
        app.MapGet("/api/products/debug-count", async (ITrendplusDbContext db, string? source, ILogger<Program> logger, CancellationToken ct) =>
        {
            try
            {
                var query = db.Artikli.AsNoTracking().AsQueryable();
                if (!string.IsNullOrWhiteSpace(source))
                {
                    if (source.Equals("zalando", StringComparison.OrdinalIgnoreCase))
                        query = query.Where(a => a.Kategorija == "zalando");
                    else
                        query = query.Where(a => a.Kategorija == source);
                }

                var total = await query.CountAsync(ct);
                logger.LogInformation("Debug count for source {Source}: {Count}", source ?? "all", total);
                return Results.Ok(new { source = source ?? "all", count = total });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get products debug count");
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Debug failed");
            }
        })
        .WithName("GetProductsDebugCount")
        .WithTags("Products", "Debug");

        // Debug: recent imported products (by source)
        app.MapGet("/api/products/recent", async (ITrendplusDbContext db, string? source = null, int limit = 20, CancellationToken ct = default) =>
         {
             var query = db.Artikli.AsNoTracking().AsQueryable();
             if (!string.IsNullOrWhiteSpace(source))
             {
                 if (source.Equals("zalando", StringComparison.OrdinalIgnoreCase))
                     query = query.Where(a => a.Kategorija == "zalando");
                 else
                     query = query.Where(a => a.Kategorija == source);
             }

             var items = await query.OrderByDescending(a => a.UpdatedAt).Take(limit)
                 .Select(a => new { id = a.Id, name = a.Naziv, brand = a.Pol, price = a.ProdajnaCena, imageUrl = a.ImagePath, url = a.Komentar, updatedAt = a.UpdatedAt })
                 .ToListAsync(ct);

             return Results.Ok(items);
         })
         .WithName("GetProductsRecent")
         .WithTags("Products", "Debug");
    }

    private static async Task<string?> MapRollingAndMomentumToNivelacijaArticlesAsync(
        List<VendorSalesNivelacijaArticleStatDto> articles,
        NpgsqlConnection connection,
        DateTime? eventDate,
        CancellationToken ct)
    {
        if (articles.Count == 0) return null;

        static string SkuKey(string? sku) => string.IsNullOrWhiteSpace(sku) ? string.Empty : sku.Trim().ToUpperInvariant();

        var warnings = new List<string>();
        var rollingMap = new Dictionary<string, (decimal? pre, decimal? post)>(StringComparer.Ordinal);
        var momentumMap = new Dictionary<string, decimal?>(StringComparer.Ordinal);

        var skuKeys = articles
            .Select(x => SkuKey(x.Sku))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (eventDate.HasValue)
        {
            try
            {
                const string rollingSql = """
                    SELECT
                        UPPER(TRIM(COALESCE(NULLIF(a."PLU", ''), a."Id"::text))) AS sku_key,
                        AVG(r.ma7_revenue) FILTER (
                            WHERE r.day >= @eventDate - INTERVAL '30 days'
                              AND r.day <  @eventDate
                        ) AS pre,
                        AVG(r.ma7_revenue) FILTER (
                            WHERE r.day >= @eventDate
                              AND r.day <  @eventDate + INTERVAL '30 days'
                        ) AS post
                    FROM vw_sales_rolling_7d r
                    JOIN "Artikli" a ON a."Id" = r.article_id
                    WHERE UPPER(TRIM(COALESCE(NULLIF(a."PLU", ''), a."Id"::text))) = ANY(@skuKeys)
                      AND r.day >= @eventDate - INTERVAL '30 days'
                      AND r.day <  @eventDate + INTERVAL '30 days'
                    GROUP BY UPPER(TRIM(COALESCE(NULLIF(a."PLU", ''), a."Id"::text)));
                    """;
                await using var cmd = new NpgsqlCommand(rollingSql, connection);
                cmd.Parameters.AddWithValue("eventDate", eventDate.Value.Date);
                cmd.Parameters.AddWithValue("skuKeys", skuKeys);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var key = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    rollingMap[key] = (
                        reader.IsDBNull(1) ? null : reader.GetDecimal(1),
                        reader.IsDBNull(2) ? null : reader.GetDecimal(2));
                }
            }
            catch (PostgresException ex) when (ex.SqlState == "42P01" || ex.SqlState == "42703")
            {
                warnings.Add("No rolling data (view missing)");
            }
            catch (Exception)
            {
                warnings.Add("Rolling lookup failed");
            }
        }
        else
        {
            warnings.Add("Rolling pre/post unavailable (no eventDate filter)");
        }

        try
        {
            const string momentumSql = """
                SELECT
                    UPPER(TRIM(COALESCE(sku, ''))) AS sku_key,
                    m.momentum_revenue
                FROM vw_sales_momentum m
                JOIN "Artikli" a ON a."Id" = m.article_id
                WHERE UPPER(TRIM(COALESCE(sku, ''))) = ANY(@skuKeys);
                """;
            await using var cmd = new NpgsqlCommand(momentumSql, connection);
            cmd.Parameters.AddWithValue("skuKeys", skuKeys);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var key = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                if (string.IsNullOrWhiteSpace(key)) continue;
                momentumMap[key] = reader.IsDBNull(1) ? null : reader.GetDecimal(1);
            }
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01" || ex.SqlState == "42703")
        {
            warnings.Add("No momentum data (view missing)");
        }
        catch (Exception)
        {
            warnings.Add("Momentum lookup failed");
        }

        foreach (var row in articles)
        {
            var key = SkuKey(row.Sku);
            if (rollingMap.TryGetValue(key, out var rolling))
            {
                row.Rolling7dPreRevenue = rolling.pre;
                row.Rolling7dPostRevenue = rolling.post;
            }
            if (momentumMap.TryGetValue(key, out var momentum))
            {
                row.MomentumRevenue = momentum;
            }
        }

        return warnings.Count == 0 ? null : string.Join("; ", warnings.Distinct(StringComparer.Ordinal));
    }

    private static async Task<string?> MapOosAndDidToNivelacijaArticlesAsync(
        List<VendorSalesNivelacijaArticleStatDto> articles,
        NpgsqlConnection connection,
        CancellationToken ct)
    {
        if (articles.Count == 0) return null;

        static string SkuKey(string? sku) => string.IsNullOrWhiteSpace(sku) ? string.Empty : sku.Trim().ToUpperInvariant();

        var warnings = new List<string>();
        var oosMap = new Dictionary<string, decimal?>(StringComparer.Ordinal);
        var didMap = new Dictionary<string, (decimal? didRevenue, decimal? didQty)>(StringComparer.Ordinal);

        var skuKeys = articles
            .Select(x => SkuKey(x.Sku))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        try
        {
            const string oosSql = """
                SELECT
                    UPPER(TRIM(COALESCE(sku, ''))) AS sku_key,
                    AVG(is_oos::numeric) AS oos_rate
                FROM vw_stock_red_zone
                WHERE UPPER(TRIM(COALESCE(sku, ''))) = ANY(@skuKeys)
                GROUP BY UPPER(TRIM(COALESCE(sku, '')));
                """;
            await using var cmd = new NpgsqlCommand(oosSql, connection);
            cmd.Parameters.AddWithValue("skuKeys", skuKeys);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var key = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                if (string.IsNullOrWhiteSpace(key)) continue;
                oosMap[key] = reader.IsDBNull(1) ? null : reader.GetDecimal(1);
            }
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01" || ex.SqlState == "42703")
        {
            warnings.Add("No OOS data (view missing)");
        }
        catch (Exception)
        {
            warnings.Add("OOS lookup failed");
        }

        try
        {
            const string didSql = """
                SELECT
                    UPPER(TRIM(COALESCE(sku, ''))) AS sku_key,
                    AVG(did_revenue) AS did_revenue,
                    AVG(did_qty) AS did_qty
                FROM vw_nivelacija_did
                WHERE UPPER(TRIM(COALESCE(sku, ''))) = ANY(@skuKeys)
                GROUP BY UPPER(TRIM(COALESCE(sku, '')));
                """;
            await using var cmd = new NpgsqlCommand(didSql, connection);
            cmd.Parameters.AddWithValue("skuKeys", skuKeys);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var key = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                if (string.IsNullOrWhiteSpace(key)) continue;
                didMap[key] = (
                    reader.IsDBNull(1) ? null : reader.GetDecimal(1),
                    reader.IsDBNull(2) ? null : reader.GetDecimal(2));
            }
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01" || ex.SqlState == "42703")
        {
            warnings.Add("No DiD data (view missing)");
        }
        catch (Exception)
        {
            warnings.Add("DiD lookup failed");
        }

        foreach (var row in articles)
        {
            var key = SkuKey(row.Sku);
            if (oosMap.TryGetValue(key, out var oosRate))
            {
                row.OOSRate = oosRate;
            }
            if (didMap.TryGetValue(key, out var did))
            {
                row.DidRevenue = did.didRevenue;
                row.DidQty = did.didQty;
            }
        }

        return warnings.Count == 0 ? null : string.Join("; ", warnings.Distinct(StringComparer.Ordinal));
    }

    private static void MapElasticityAndLostSalesToNivelacijaArticles(List<VendorSalesNivelacijaArticleStatDto> articles)
    {
        foreach (var row in articles)
        {
            if (row.PriceChangePercent.HasValue && row.PreQty > 0)
            {
                var pricePct = row.PriceChangePercent.Value / 100m;
                if (pricePct != 0m)
                {
                    var qtyPct = ((decimal)row.PostQty - row.PreQty) / row.PreQty;
                    row.PriceElasticity = Math.Round(qtyPct / pricePct, 4);
                }
            }

            if (!row.OOSRate.HasValue) continue;

            var oos = row.OOSRate.Value;
            if (oos < 0m) oos = 0m;
            if (oos > 1m) oos = 1m;
            if (oos >= 1m) continue;

            // Lost sales proxy from realized post revenue under OOS pressure.
            row.LostSalesOOS = Math.Round((row.PostRevenue * oos) / (1m - oos), 2);
        }
    }

    private static void MapMetricReasons(
        List<VendorSalesNivelacijaArticleStatDto> articles,
        IReadOnlyCollection<string> globalWarnings)
    {
        foreach (var row in articles)
        {
            var reasons = new List<string>();
            if (globalWarnings.Count > 0)
                reasons.AddRange(globalWarnings);

            if (!row.Rolling7dPreRevenue.HasValue && !row.Rolling7dPostRevenue.HasValue)
                reasons.Add("No rolling data");
            if (!row.MomentumRevenue.HasValue)
                reasons.Add("No momentum data");
            if (!row.OOSRate.HasValue)
                reasons.Add("No OOS data");
            if (!row.DidRevenue.HasValue && !row.DidQty.HasValue)
                reasons.Add("No DiD data");
            if (!row.PriceElasticity.HasValue)
                reasons.Add("No elasticity data");
            if (!row.LostSalesOOS.HasValue)
                reasons.Add("No lost sales data");

            row.MetricReason = reasons.Count == 0
                ? null
                : string.Join("; ", reasons.Distinct(StringComparer.Ordinal));
        }
    }

    private static async Task<bool> RelationHasColumnAsync(
        NpgsqlConnection connection,
        string relationName,
        string columnName,
        CancellationToken ct)
    {
        const string sql = """
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = @rel
              AND column_name = @col
            LIMIT 1;
            """;

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("rel", relationName);
        cmd.Parameters.AddWithValue("col", columnName);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is not null;
    }
}

// Fix NivelacijaRequest DTO (use Komentar)
public record NivelacijaRequest(int ArtikalId, decimal NovaProdajnaCena, string? Komentar);

// DTO for import
public record ProductDto(string Name, string Brand, decimal Price, string? ImageUrl, string? Url);

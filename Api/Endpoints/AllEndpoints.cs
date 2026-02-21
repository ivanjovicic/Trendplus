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
using Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Infrastructure.DbContexts;
using Application.Analytics.Queries.GetTopProducts;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Domain.Model;
using Domain.Model.TrendShoes;
using Application.TrendShoes;

namespace Trendplus2.Endpoints;

public static class AllEndpoints
{
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
        app.MapGet("/api/workers/health", (WorkerHealthService workerHealth) =>
        {
            var summary = workerHealth.GetHealthSummary();
            return Results.Ok(summary);
        })
        .WithName("WorkerHealthCheck")
        .WithTags("System");

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
        .WithTags("System");

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
        .WithTags("System");

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
                        ""UpdatedAt"" TIMESTAMP NOT NULL DEFAULT NOW()
                    );
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
                    statsResults["dailySummaryCount"] = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
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

        // ===== Deichmann ad-hoc scraper proxy =====
        app.MapPost("/api/scrapers/deichmann", async (
            IHttpClientFactory httpClientFactory,
            ILogger<Program> logger,
            System.Text.Json.JsonElement filters,
            CancellationToken ct) =>
        {
            try
            {
                var client = httpClientFactory.CreateClient("scraper");

                var json = filters.GetRawText();
                var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

                // Use extended timeout for scraping operations
                client.Timeout = TimeSpan.FromMinutes(5);

                var resp = await client.PostAsync("/scrapers/deichmann", content, ct);

                var body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    logger.LogWarning("Deichmann scraper returned {Status}: {Body}", resp.StatusCode, body);
                    return Results.Problem(detail: $"Scraper service returned {resp.StatusCode}: {body}", statusCode: 502);
                }

                // Try to deserialize JSON response and return it as-is
                try
                {
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<object>(body);
                    return Results.Ok(parsed);
                }
                catch (Exception)
                {
                    // If not JSON, return raw body
                    return Results.Ok(new { raw = body });
                }
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
            IHttpClientFactory httpClientFactory,
            ILogger<Program> logger,
            System.Text.Json.JsonElement filters,
            CancellationToken ct) =>
        {
            try
            {
                var client = httpClientFactory.CreateClient("scraper");

                var json = filters.GetRawText();
                var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

                // Use extended timeout for scraping operations
                client.Timeout = TimeSpan.FromMinutes(5);

                var resp = await client.PostAsync("/scrapers/aboutyou", content, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    logger.LogWarning("AboutYou scraper returned {Status}: {Body}", resp.StatusCode, body);
                    return Results.Problem(detail: $"Scraper service returned {resp.StatusCode}: {body}", statusCode: 502);
                }

                try
                {
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<object>(body);
                    return Results.Ok(parsed);
                }
                catch (Exception)
                {
                    return Results.Ok(new { raw = body });
                }
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
            IHttpClientFactory httpClientFactory,
            ILogger<Program> logger,
            System.Text.Json.JsonElement filters,
            CancellationToken ct) =>
        {
            try
            {
                var client = httpClientFactory.CreateClient("scraper");

                var json = filters.GetRawText();
                var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

                // Use extended timeout for scraping operations
                client.Timeout = TimeSpan.FromMinutes(5);

                var resp = await client.PostAsync("/scrapers/humanic", content, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    logger.LogWarning("Humanic scraper returned {Status}: {Body}", resp.StatusCode, body);
                    return Results.Problem(detail: $"Scraper service returned {resp.StatusCode}: {body}", statusCode: 502);
                }

                try
                {
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<object>(body);
                    return Results.Ok(parsed);
                }
                catch (Exception)
                {
                    return Results.Ok(new { raw = body });
                }
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
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
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

                if (!sales.Any())
                {
                    return Results.Ok(new { message = "No sales to sync" });
                }

                logger.LogInformation($"Found {sales.Count} sales to sync.");

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

                if (newSalesFacts.Any())
                {
                    await analyticsDb.SalesFacts.AddRangeAsync(newSalesFacts, ct);
                    await analyticsDb.SalesLineFacts.AddRangeAsync(newSalesLineFacts, ct);
                    await analyticsDb.SaveChangesAsync(ct);
                }

                logger.LogInformation($"✅ Synced {syncedCount} new sales to Analytics DB.");

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
            ITrendplusDbContext db,
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
                            join d in db.Dobavljaci.AsNoTracking() on dp.DobavljacId equals d.Id into dobavljaci
                            from dobavljac in dobavljaci.DefaultIfEmpty()
                            select new
                            {
                                dp.Id,
                                dp.TipPromene,
                                dp.Datum,
                                dp.Iznos,
                                dp.BrojRacuna,
                                dp.ArtikalId,
                                ArtikalNaziv = artikal != null ? artikal.Naziv : null,
                                dp.DobavljacId,
                                DobavljacNaziv = dobavljac != null ? dobavljac.Naziv : null,
                                dp.StaraProdajnaCena,
                                dp.NovaProdajnaCena,
                                dp.Komentar,
                                dp.KorisnikIme
                            };

                if (!string.IsNullOrWhiteSpace(tipPromene))
                    query = query.Where(x => x.TipPromene == tipPromene);

                if (artikalId.HasValue)
                    query = query.Where(x => x.ArtikalId == artikalId.Value);

                if (!string.IsNullOrWhiteSpace(naziv))
                    query = query.Where(x => x.ArtikalNaziv != null && x.ArtikalNaziv.Contains(naziv));

                if (!string.IsNullOrWhiteSpace(brojRacuna))
                    query = query.Where(x => x.BrojRacuna != null && x.BrojRacuna.Contains(brojRacuna));

                if (fromDate.HasValue)
                    query = query.Where(x => x.Datum >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(x => x.Datum <= toDate.Value);

                query = sortBy.ToLower() switch
                {
                    "tippromene" => sortDir == "asc" ? query.OrderBy(x => x.TipPromene) : query.OrderByDescending(x => x.TipPromene),
                    "iznos" => sortDir == "asc" ? query.OrderBy(x => x.Iznos) : query.OrderByDescending(x => x.Iznos),
                    "naziv" => sortDir == "asc" ? query.OrderBy(x => x.ArtikalNaziv) : query.OrderByDescending(x => x.ArtikalNaziv),
                    _ => sortDir == "asc" ? query.OrderBy(x => x.Datum) : query.OrderByDescending(x => x.Datum)
                };

                var total = await query.CountAsync(ct);
                var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);

                return Results.Ok(new { items, totalCount = total, pageNumber, pageSize });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri učitavanju dnevnika promena");
            }
        })
        .RequireRateLimiting("db-heavy");

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
            decimal? minCena = null,
            decimal? maxCena = null,
            decimal? minKolicina = null,
            decimal? maxKolicina = null,
            string sortBy = "naziv",
            string sortDir = "asc",
            CancellationToken ct = default) =>
        {
            try
            {
                var normalizedSortBy = (sortBy ?? "naziv").ToLowerInvariant();
                var normalizedSortDir = (sortDir ?? "asc").ToLowerInvariant() == "desc" ? "desc" : "asc";
                pageNumber = pageNumber < 1 ? 1 : pageNumber;
                pageSize = pageSize < 1 ? 50 : Math.Min(pageSize, 200);

                var responseCacheKey =
                    $"artikli_page_{pageNumber}_{pageSize}_{naziv}_{sezonaId}_{minCena}_{maxCena}_{minKolicina}_{maxKolicina}_{normalizedSortBy}_{normalizedSortDir}";

                if (cache.TryGetValue(responseCacheKey, out object? cachedResponse) && cachedResponse is not null)
                {
                    return Results.Ok(cachedResponse);
                }

                var query = db.Artikli.AsNoTracking().AsQueryable();

                if (!string.IsNullOrWhiteSpace(naziv))
                    query = query.Where(a => a.Naziv.Contains(naziv));

                if (sezonaId.HasValue)
                    query = query.Where(a => a.IDSezona == sezonaId.Value);

                if (minCena.HasValue)
                    query = query.Where(a => a.ProdajnaCena >= minCena.Value);

                if (maxCena.HasValue)
                    query = query.Where(a => a.ProdajnaCena <= maxCena.Value);

                if (minKolicina.HasValue)
                    query = query.Where(a => a.Kolicina >= minKolicina.Value);

                if (maxKolicina.HasValue)
                    query = query.Where(a => a.Kolicina <= maxKolicina.Value);

                var filterHash = $"{naziv}_{sezonaId}_{minCena}_{maxCena}_{minKolicina}_{maxKolicina}";
                var cacheKey = $"artikli_count_{filterHash}";
                
                if (!cache.TryGetValue(cacheKey, out int total))
                {
                    total = await query.CountAsync(ct);
                    cache.Set(cacheKey, total, TimeSpan.FromMinutes(2));
                }

                query = normalizedSortBy switch
                {
                    "prodajnacena" => normalizedSortDir == "asc" ? query.OrderBy(a => a.ProdajnaCena) : query.OrderByDescending(a => a.ProdajnaCena),
                    "nabavnacena" => normalizedSortDir == "asc" ? query.OrderBy(a => a.NabavnaCena) : query.OrderByDescending(a => a.NabavnaCena),
                    "kolicina" => normalizedSortDir == "asc" ? query.OrderBy(a => a.Kolicina) : query.OrderByDescending(a => a.Kolicina),
                    "id" => normalizedSortDir == "asc" ? query.OrderBy(a => a.Id) : query.OrderByDescending(a => a.Id),
                    _ => normalizedSortDir == "asc" ? query.OrderBy(a => a.Naziv) : query.OrderByDescending(a => a.Naziv)
                };

                var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);
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
        app.MapGet("/api/sezone", async (ITrendplusDbContext db, IMemoryCache cache, ILogger<Program> logger, CancellationToken ct) =>
        {
            try
            {
                const string cacheKey = "sezone_all";
                if (cache.TryGetValue(cacheKey, out List<Sezona>? cachedSezone) && cachedSezone is not null)
                {
                    return Results.Ok(cachedSezone);
                }

                var sezone = await db.Sezone.AsNoTracking().OrderBy(s => s.Naziv).ToListAsync(ct);
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

        // ============ DOBAVLJACI ============
        app.MapGet("/api/dobavljaci", async (IMediator mediator, CancellationToken ct) =>
        {
            var query = new GetDobavljacQuery();
            var result = await mediator.Send(query, ct);
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

                db.DnevnikPromena.Add(new Domain.Model.DnevnikPromena
                {
                    TipPromene = "Nivelacija cena",
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
                            where dp.TipPromene == "Nivelacija cena"
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

                query = sortBy.ToLower() switch
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
                    return Results.NotFound(new { message = "Povracaj nije pronadjen" });

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
                            naziv = artikli.ContainsKey(s.IdArtikal) ? artikli[s.IdArtikal].Naziv : "N/A"
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
                        var fallback = new[]
                        {
                            "https://images.unsplash.com/photo-1460353581641-37baddab0fa2?w=1200&auto=format&fit=crop",
                            "https://images.unsplash.com/photo-1543163521-1bf539c55dd2?w=1200&auto=format&fit=crop",
                            "https://images.unsplash.com/photo-1595950653106-6c9ebd614d3a?w=1200&auto=format&fit=crop",
                            "https://images.unsplash.com/photo-1579338559194-a162d19bf842?w=1200&auto=format&fit=crop",
                            "https://images.unsplash.com/photo-1525966222134-fcfa99b8ae77?w=1200&auto=format&fit=crop"
                        };

                        images.AddRange(fallback.Select((url, i) => new TrendImageDto(i + 1, url, "unsplash")));
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
                if (skippedReasons.Any()) logger.LogInformation("ImportZalando skipped reasons sample: {Reasons}", string.Join(";", skippedReasons.Take(5)));

                if (!toAdd.Any())
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
}

// Fix NivelacijaRequest DTO (use Komentar)
public record NivelacijaRequest(int ArtikalId, decimal NovaProdajnaCena, string? Komentar);

// DTO for import
public record ProductDto(string Name, string Brand, decimal Price, string? ImageUrl, string? Url);

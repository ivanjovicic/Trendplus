using Application.Artikli.Commands.CreateArtikal;
using Application.Artikli.Commands.UpdateArtikal;
using Application.Artikli.Common.Interfaces;
using Application.Artikli.Queries.GetArtikal;
using Application.Artikli.Queries.VratiArtikle;
using Application.Common.Interfaces;
using Application.Dobavljaci.Queries;
using Application.Performance.Queries;
using Application.Prodaja.Commands.ProdajArtikle;
using Application.Prodaja.Queries;
using Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Infrastructure.DbContexts;
using Application.Analytics.Queries.GetTopProducts;

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
            HttpClient httpClient,
            ILogger<Program> logger) =>
        {
            try
            {
                logger.LogInformation("🔍 Running EU market scrapers");
                
                var pythonServiceUrl = "http://localhost:8000";
                
                // Call Python scraper API - NO FALLBACK
                var response = await httpClient.PostAsync($"{pythonServiceUrl}/scrapers/run", null);
                
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogError("Python scraper service returned {StatusCode}", response.StatusCode);
                    return Results.Problem(
                        detail: $"Python scraper service returned HTTP {response.StatusCode}. Make sure Python service is running at {pythonServiceUrl}",
                        statusCode: 503,
                        title: "Scraper Service Unavailable"
                    );
                }
                
                var data = await response.Content.ReadFromJsonAsync<object>();
                logger.LogInformation("✅ Scrapers completed successfully");
                
                return Results.Ok(data);
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
                return Results.Problem("Failed to run scrapers");
            }
        })
        .WithName("RunEUScraper")
        .WithTags("GlobalTrends");

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
    }
}

// DTO for nivelacija endpoint
public record NivelacijaRequest(int ArtikalId, decimal NovaProdajnaCena, string? Kamerar);

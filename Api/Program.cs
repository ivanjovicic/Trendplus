using Application.Artikli.Commands.CreateArtikal;
using Application.Artikli.Commands.UpdateArtikal;
using Application.Artikli.Common.Interfaces;
using Application.Behaviors;
using Application.Common.Interfaces;
using Application.Dobavljaci.Queries;
using Application.Performance.Queries;
using Application.Povracaj.Commands;
using Application.Prodaja.Commands.ProdajArtikle;
using Application.Prodaja.Queries;
using Domain.Model;
using FluentValidation;
using Infrastructure.DbContexts;
using Infrastructure.Middleware;
using Infrastructure.Repository;
using Infrastructure.Resilience;
using Infrastructure.Services;
using Infrastructure.Services.Documents;
using Infrastructure.Services.Caching;
using MediatR;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using System.Globalization;
using Trendplus2;
using Trendplus2.Dtos;
using Trendplus2.Endpoints;
using Application.Analytics.Queries.GetInventoryStatus;
using Application.Analytics.Queries.GetSalesSummary;
using Application.Analytics.Queries.GetTopProducts;
using Microsoft.OpenApi.Models;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Api.Services;
using Api.Endpoints;
using Api.Services.Access;
using Api.Services.Startup;
using Api.Config;
using System.Threading.RateLimiting;
using Application.Documents.Interfaces;
using Infrastructure.Configuration;
using Infrastructure.Services.Email;
using Polly;
using Polly.Extensions.Http;

try
{
    Console.WriteLine("Starting application...");
    
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.Configure<HostOptions>(options =>
    {
        options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
    });

    Console.WriteLine("Builder created successfully");

    // Serilog bootstrap
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .CreateLogger();

    Console.WriteLine("Serilog configured");

    builder.Host.UseSerilog();

    builder.Configuration
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
        .AddEnvironmentVariables();

    Console.WriteLine("Configuration loaded");

    // Workers default policy:
    // - Development: enabled
    // - Production/other: disabled
    // Explicit Workers:Enabled overrides this default.
    var workersEnabledFromConfig = builder.Configuration.GetValue<bool?>("Workers:Enabled");
    var workersEnabled = workersEnabledFromConfig ?? builder.Environment.IsDevelopment();
    var workersRuntimeToggleAllowedFromConfig = builder.Configuration.GetValue<bool?>("Workers:AllowRuntimeToggle");
    var workersRuntimeToggleAllowed = workersRuntimeToggleAllowedFromConfig ?? builder.Environment.IsDevelopment();

    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

    Console.WriteLine($"Configured to listen on port {port}");

    // Options binding
    builder.Services.Configure<SerpApiOptions>(builder.Configuration.GetSection(SerpApiOptions.Section));
    builder.Services.Configure<EbayOptions>(builder.Configuration.GetSection(EbayOptions.Section));
    builder.Services.Configure<GoogleShoppingOptions>(builder.Configuration.GetSection(GoogleShoppingOptions.Section));
    builder.Services.Configure<RuntimeScoringOptions>(builder.Configuration.GetSection(RuntimeScoringOptions.Section));
    builder.Services.Configure<AccessImportOptions>(builder.Configuration.GetSection(AccessImportOptions.Section));
    builder.Services.Configure<Infrastructure.Configuration.NightlyAnalyticsRefreshOptions>(
        builder.Configuration.GetSection(Infrastructure.Configuration.NightlyAnalyticsRefreshOptions.Section));
    builder.Services.Configure<Infrastructure.Configuration.OpenTrainingModelTrainingOptions>(
        builder.Configuration.GetSection(Infrastructure.Configuration.OpenTrainingModelTrainingOptions.Section));
    builder.Services.Configure<Infrastructure.Configuration.TrendIngestionOptions>(
        builder.Configuration.GetSection(Infrastructure.Configuration.TrendIngestionOptions.Section));
    builder.Services.Configure<DocumentExportOptions>(builder.Configuration.GetSection(DocumentExportOptions.Section));
    builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.Section));
    var documentOptions = builder.Configuration.GetSection(DocumentExportOptions.Section).Get<DocumentExportOptions>() ?? new DocumentExportOptions();
    var resolvedDocumentSigningKey = documentOptions.ResolveSigningKey();
    if (builder.Environment.IsProduction() && string.IsNullOrWhiteSpace(resolvedDocumentSigningKey))
    {
        throw new InvalidOperationException("Documents signing key is required in production. Configure Documents:SigningKey or DOCUMENT_SIGNING_KEY.");
    }

    var dbCommandTimeoutSeconds =
        builder.Configuration.GetValue<int?>("Database:CommandTimeoutSeconds")
        ?? 300;

    // DbContext
    builder.Services.AddDbContextFactory<TrendplusDbContext>(options =>
        options.UseNpgsql(
                builder.Configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.CommandTimeout(dbCommandTimeoutSeconds))
               .EnableSensitiveDataLogging(builder.Environment.IsDevelopment()));

    builder.Services.AddDbContext<TrendplusDbContext>(options =>
        options.UseNpgsql(
                builder.Configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.CommandTimeout(dbCommandTimeoutSeconds))
               .EnableSensitiveDataLogging(builder.Environment.IsDevelopment()));

    builder.Services.AddScoped<ITrendplusDbContext>(sp =>
        sp.GetRequiredService<TrendplusDbContext>());

    builder.Services.AddDbContext<AnalyticsDbContext>(options =>
        options.UseNpgsql(
                builder.Configuration.GetConnectionString("AnalyticsConnection"),
                npgsql => npgsql.CommandTimeout(dbCommandTimeoutSeconds))
               .EnableSensitiveDataLogging(builder.Environment.IsDevelopment()));

    builder.Services.AddScoped<IAnalyticsDbContext>(sp =>
        sp.GetRequiredService<AnalyticsDbContext>());

    var openProductTrainingConnection =
        builder.Configuration.GetConnectionString("OpenProductTrainingConnection")
        ?? builder.Configuration.GetConnectionString("AnalyticsConnection");

    builder.Services.AddDbContext<OpenProductTrainingDbContext>(options =>
        options.UseNpgsql(
                openProductTrainingConnection,
                o =>
                {
                    o.UseVector();
                    o.CommandTimeout(dbCommandTimeoutSeconds);
                })
               .EnableSensitiveDataLogging(builder.Environment.IsDevelopment()));

    Console.WriteLine("DbContext registered");

    // FluentValidation - auto-register all validators
    builder.Services.AddValidatorsFromAssemblyContaining<CreateArtikalCommandValidator>();
    builder.Services.AddValidatorsFromAssemblyContaining<Api.Validators.StartTrainingRunRequestValidator>();

    // Memory Cache - required by GetArtikliQueryHandler and other caching services
    builder.Services.AddMemoryCache();
    builder.Services.AddHttpContextAccessor();

    // MediatR Pipeline Behaviors (order matters!)
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceLoggingBehavior<,>));

    // Services
    builder.Services.AddScoped<IErrorStore, DbErrorStore>();
    builder.Services.AddScoped<IProdajaRepository, ProdajaRepository>();
builder.Services.AddScoped<IOutboxService, OutboxService>();
builder.Services.AddScoped<IDnevnikPromenaReadService, DnevnikPromenaReadService>();
builder.Services.AddScoped<IAnalyticsDetailReadService, AnalyticsDetailReadService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
    builder.Services.AddScoped<IDocumentQueueStore, DocumentQueueStore>();
    builder.Services.AddScoped<IDocumentAuditService, DocumentAuditService>();
    builder.Services.AddScoped<IDocumentAccessControlService, DocumentAccessControlService>();
    builder.Services.AddScoped<IDocumentTemplateRenderer, DocumentTemplateRenderer>();
    builder.Services.AddScoped<IDocumentStorage, LocalDocumentStorage>();
    builder.Services.AddScoped<IDocumentRenderer, CsvDocumentRenderer>();
    builder.Services.AddScoped<IDocumentRenderer, XlsxDocumentRenderer>();
    builder.Services.AddScoped<IDocumentRenderer, PdfDocumentRenderer>();
    builder.Services.AddScoped<IDocumentRenderer, HtmlDocumentRenderer>();
    builder.Services.AddSingleton<IDocumentDownloadTokenService, DocumentDownloadTokenService>();
    builder.Services.AddScoped<IDocumentUserContextAccessor, DocumentUserContextAccessor>();
    builder.Services.AddScoped<IEmailService, SmtpEmailService>();
    builder.Services.AddScoped<IInventoryReportScheduleService, Infrastructure.Services.Inventory.InventoryReportScheduleService>();
    builder.Services.AddScoped<IInventoryActionDecisionService, Infrastructure.Services.Inventory.InventoryActionDecisionService>();
    builder.Services.AddScoped<Infrastructure.Services.Inventory.InventoryReportDeliveryService>();
    builder.Services.AddSingleton<WorkerHealthService>(); // Worker health monitoring
    builder.Services.AddSingleton(sp =>
        new WorkerRuntimeControlService(
            workersEnabled,
            workersRuntimeToggleAllowed,
            workersEnabledFromConfig.HasValue
                ? "config"
                : (builder.Environment.IsDevelopment() ? "development-default" : "production-default")));
    
    // Embedding service for AI-powered image search
    var pythonServiceUrl = builder.Configuration["EmbeddingService:BaseUrl"] ?? "http://localhost:8000";
    var useMockEmbedding = builder.Configuration.GetValue<bool>("EmbeddingService:UseMock", true);
    
    if (useMockEmbedding)
    {
        Console.WriteLine("⚠️ Using MOCK embedding service (no AI)");
        builder.Services.AddScoped<IEmbeddingService, MockEmbeddingService>();
    }
    else
    {
        Console.WriteLine($"✅ Using Python embedding service at: {pythonServiceUrl}");
        builder.Services.AddHttpClient<IEmbeddingService, PythonEmbeddingService>(client =>
        {
            client.BaseAddress = new Uri(pythonServiceUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
    }

    // RabbitMQ
    builder.Services.Configure<Infrastructure.Configuration.RabbitMqSettings>(
        builder.Configuration.GetSection("RabbitMq"));
    builder.Services.AddSingleton<IMessageBroker, RabbitMqMessageBroker>();

    // HttpClient for external APIs with reasonable timeout
    builder.Services.AddHttpClient("default", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30); // Enough time for Python service
    });
    
    // Named HttpClient for scraper service (Python), configured from appsettings or env
    var scraperBase = builder.Configuration["ScraperService:BaseUrl"] ?? "http://localhost:8000";
    builder.Services.AddHttpClient("scraper", client =>
    {
        client.BaseAddress = new Uri(scraperBase);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddPolicyHandler((sp, request) =>
    {
        var logger = sp.GetRequiredService<ILogger<Program>>();
        IAsyncPolicy<HttpResponseMessage> retry = HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => (int)r.StatusCode == 429)
            .WaitAndRetryAsync(
                retryCount: 2,
                sleepDurationProvider: retryAttempt => retryAttempt == 1
                    ? TimeSpan.FromSeconds(1)
                    : TimeSpan.FromSeconds(3),
                onRetry: (outcome, delay, attempt, _) =>
                {
                    logger.LogWarning(
                        "Scraper retry {Attempt} in {DelaySeconds}s for {Method} {Uri}. Reason={Reason}",
                        attempt,
                        delay.TotalSeconds,
                        request.Method,
                        request.RequestUri,
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                });

        return retry;
    })
    .AddPolicyHandler((sp, request) =>
    {
        var logger = sp.GetRequiredService<ILogger<Program>>();
        IAsyncPolicy<HttpResponseMessage> breaker = HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => (int)r.StatusCode == 429)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (_, breakDelay) =>
                {
                    logger.LogError(
                        "Scraper circuit OPEN for {BreakDelaySeconds}s on {Method} {Uri}",
                        breakDelay.TotalSeconds,
                        request.Method,
                        request.RequestUri);
                },
                onReset: () =>
                {
                    logger.LogInformation(
                        "Scraper circuit RESET for {Method} {Uri}",
                        request.Method,
                        request.RequestUri);
                });

        return breaker;
    });

    // Optional Python model endpoint for runtime scoring evaluate (/api/v1/scoring/evaluate)
    var pythonModelBase = builder.Configuration["RuntimeScoring:PythonModelBaseUrl"] ?? "http://localhost:8000";
    var pythonModelTimeout = builder.Configuration.GetValue<int?>("RuntimeScoring:PythonTimeoutSeconds") ?? 20;
    builder.Services.AddHttpClient("PythonModel", client =>
    {
        client.BaseAddress = new Uri(pythonModelBase);
        client.Timeout = TimeSpan.FromSeconds(Math.Max(5, pythonModelTimeout));
    });

    // Named HttpClient for the consolidated Python API (/generate-trends on port 8000 by default)
    var trendEngineBase = builder.Configuration["TrendIngestion:PythonApiBaseUrl"] ?? "http://localhost:8000";
    var trendEngineTimeout = builder.Configuration.GetValue<int?>("TrendIngestion:PythonCallTimeoutSeconds") ?? 300;
    builder.Services.AddHttpClient("TrendEngine", client =>
    {
        client.BaseAddress = new Uri(trendEngineBase);
        client.Timeout = TimeSpan.FromSeconds(Math.Max(60, trendEngineTimeout));
    });

    // Image providers for trends carousel are optional and resolved dynamically in endpoints

    // Background workers are always registered, but execution is controlled at runtime
    // via WorkerRuntimeControlService (so we can enable/disable without restart).
    builder.Services.AddHostedService<Workers.SyncWorker>();
    builder.Services.AddHostedService<Workers.OutboxProcessorWorker>();
    builder.Services.AddHostedService<Workers.AnalyticsAggregationWorker>();
    builder.Services.AddHostedService<Workers.NightlyAnalyticsRefreshWorker>();
    builder.Services.AddHostedService<Workers.OpenTrainingModelTrainingWorker>();
    builder.Services.AddHostedService<Workers.TrendIngestionWorker>();
    builder.Services.AddHostedService<Workers.DocumentGenerationWorker>();
    builder.Services.AddHostedService<Workers.InventoryReportSchedulerWorker>();
    builder.Services.AddHostedService<AccessImportBackgroundWorker>();
    builder.Services.AddHostedService<DeferredStartupTasksHostedService>();
    // builder.Services.AddHostedService<Workers.DatabaseKeepAliveWorker>();
    Console.WriteLine($"Background workers startup state: {(workersEnabled ? "ENABLED" : "DISABLED")}");
    Console.WriteLine($"Background workers runtime toggle: {(workersRuntimeToggleAllowed ? "ALLOWED" : "LOCKED")}");

    builder.Services.AddControllers();
    builder.Services.ConfigureHttpJsonOptions(opts =>
    {
        opts.SerializerOptions.PropertyNameCaseInsensitive = true;
    });

    builder.Services.AddEndpointsApiExplorer();
    
    // ===== IMPROVED SWAGGER CONFIGURATION =====
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Trendplus API",
            Version = "v1",
            Description = "Trendplus Inventory & Sales Management System API",
            Contact = new OpenApiContact
            {
                Name = "Trendplus Support",
                Email = "support@trendplus.com"
            }
        });

        // Add XML comments if available
        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            c.IncludeXmlComments(xmlPath);
        }

        // Handle nullable reference types
        c.SupportNonNullableReferenceTypes();
        
        // Custom schema IDs to avoid conflicts
        c.CustomSchemaIds(type => type.FullName?.Replace("+", "."));
    });

    builder.Services.AddMediatR(typeof(CreateArtikalHandler).Assembly);

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy
               .WithOrigins(
                   "http://localhost:5173",
                   "http://localhost:5174",
                   "http://localhost:8080",
                   "https://trendplus.vercel.app"
               )
               .AllowAnyHeader()
               .AllowAnyMethod()
               .AllowCredentials();
        });
    });

    // Cache wiring:
    // - Development default: In-Memory only (unless explicitly enabled via Caching:UseRedis=true)
    // - Production default: Hybrid (In-Memory + Redis)
    //
    // NOTE: Some scoped clients (e.g. CommonMatchesClient) depend on IDistributedCache.
    // Ensure a safe default is always available even when Redis is disabled.
    builder.Services.AddDistributedMemoryCache();
    var useRedisCache = builder.Configuration.GetValue<bool?>("Caching:UseRedis")
        ?? !builder.Environment.IsDevelopment();

    if (useRedisCache)
    {
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
            options.Configuration = $"{redisConnection},abortConnect=false,connectTimeout=500,syncTimeout=500,asyncTimeout=500,connectRetry=1";
            options.InstanceName = "trendplus:";
        });
        builder.Services.AddSingleton<IAnalyticsCacheService, HybridCacheService>();
    }
    else
    {
        Console.WriteLine("Caching: Redis disabled (using In-Memory cache only).");
        builder.Services.AddSingleton<IAnalyticsCacheService, InMemoryCacheService>();
    }

    // Register Api.Services.CommonMatchesClient (implementation in Api project)
    builder.Services.AddScoped<ICommonMatchesClient, CommonMatchesClient>();
    builder.Services.AddScoped<IScraperScoringQueryService, ScraperScoringQueryService>();
    builder.Services.AddScoped<IPopularityAndDealScoringService, PopularityAndDealScoringService>();
    builder.Services.AddScoped<IOpenProductTrainingSignalProvider, OpenProductTrainingSignalProvider>();
    builder.Services.AddScoped<IOpenProductTrainingSyncService, OpenProductTrainingSyncService>();
    builder.Services.AddSingleton<ISellProbabilityRsOnnxScorer, SellProbabilityRsOnnxScorer>();
    builder.Services.AddSingleton<IEnterpriseScoringModelProvider, EnterpriseScoringModelProvider>();
    builder.Services.AddScoped<IRuntimeScoringEngine, RuntimeScoringEngine>();
    builder.Services.AddScoped<IAccessImportService, AccessImportService>();
    builder.Services.AddScoped<IAccessImportJobQueue, AccessImportJobQueue>();
    builder.Services.AddScoped<IAccessImportCursorRepository, AccessImportCursorRepository>();
    builder.Services.AddScoped<IBatchLogService, BatchLogService>();
    builder.Services.AddScoped<ITransferService, TransferService>();
    builder.Services.AddScoped<IPreNivelacijaScoringService, PreNivelacijaScoringService>();
    builder.Services.AddScoped<IShopifyImportService, ShopifyImportService>();

    // Named HttpClient for Shopify public storefront API
    builder.Services.AddHttpClient("Shopify", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    });

    // Typed HttpClient services for external product APIs
    var serpTimeout = builder.Configuration.GetValue<int>("SerpApi:TimeoutSeconds", 20);
    builder.Services.AddHttpClient<AmazonShoesService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(serpTimeout);
    });
    builder.Services.AddHttpClient<GoogleShoppingService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(serpTimeout);
    });
    var ebayTimeout = builder.Configuration.GetValue<int>("Ebay:TimeoutSeconds", 20);
    builder.Services.AddHttpClient<EbayBrowseService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(ebayTimeout);
    });

    // API rate limiter policies used by RequireRateLimiting(...) in endpoints
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        static string GetClientPartitionKey(HttpContext httpContext) =>
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "global";

        static FixedWindowRateLimiterOptions CreateWindowPolicy(int permitLimit, int windowSeconds) =>
            new()
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            };

        options.AddPolicy("api-v1", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: GetClientPartitionKey(httpContext),
                factory: _ => CreateWindowPolicy(120, 60)));

        options.AddPolicy("writes", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: GetClientPartitionKey(httpContext),
                factory: _ => CreateWindowPolicy(40, 60)));

        options.AddPolicy("fixed", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: GetClientPartitionKey(httpContext),
                factory: _ => CreateWindowPolicy(120, 60)));

        options.AddPolicy("strict", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: GetClientPartitionKey(httpContext),
                factory: _ => CreateWindowPolicy(20, 60)));

        options.AddPolicy("db-heavy", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: GetClientPartitionKey(httpContext),
                factory: _ => CreateWindowPolicy(60, 60)));

        options.AddPolicy("analytics", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: GetClientPartitionKey(httpContext),
                factory: _ => CreateWindowPolicy(90, 60)));

        options.AddPolicy("external-api", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: GetClientPartitionKey(httpContext),
                factory: _ => CreateWindowPolicy(30, 60)));
    });

    var app = builder.Build();

    // ================= MIDDLEWARE PIPELINE =================

    // 1. Global exception handler (first in pipeline)
    app.UseMiddleware<GlobalExceptionMiddleware>();

    // 2. Serilog request logging
    app.UseSerilogRequestLogging(opts =>
    {
        opts.EnrichDiagnosticContext = (diag, http) =>
        {
            diag.Set("RequestHost", http.Request.Host.Value);
            diag.Set("RequestScheme", http.Request.Scheme);
            diag.Set("UserAgent", http.Request.Headers.UserAgent.ToString());
            diag.Set("RequestPath", http.Request.Path);
            diag.Set("CorrelationId", http.Response.Headers["X-Correlation-ID"].ToString());
        };
    });

    // 3. Static files & routing
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseCors("AllowFrontend");
    app.UseRateLimiter();

    if (app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    // ===== SWAGGER UI =====
    app.UseSwagger(c =>
    {
        c.SerializeAsV2 = false; // Use OpenAPI 3.0
    });
    
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Trendplus API v1");
        c.RoutePrefix = "swagger"; // Serve Swagger UI at /swagger
        c.DocumentTitle = "Trendplus API Documentation";
        c.DisplayRequestDuration();
    });

    app.UseAuthorization();

    // Removed duplicate minimal API proxy for /api/release/{gender} because Api.Controllers.ReleaseController already defines this endpoint.
    // Use the existing ReleaseController implementation instead.

    // ================= ENDPOINTS =================

    // Map controllers and other minimal endpoints
    app.MapControllers();
    // Map all other endpoints from AllEndpoints.cs
    app.MapAllEndpoints();
    app.MapCachedAnalyticsEndpoints();
    app.MapInventoryEndpoints();
    app.MapAnalyticsIntelligenceEndpoints();
    app.MapInsightStudioEndpoints();
    app.MapInsightStudioV2Endpoints();
    app.MapPreNivelacijaPriorityEndpoints();
    app.MapSupplierDecisionHubEndpoints();
    app.MapScoringEndpoints();
    app.MapOpenProductTrainingEndpoints();
    app.MapShopifyEndpoints();
    app.MapAccessImportEndpoints();
    app.MapRedisEndpoints();
    app.MapOutboxEndpoints();
    app.MapAnalyticsTableEndpoints();
    app.MapDataQualityEndpoints();
    app.MapDocumentEndpoints();
    // Transfer endpoints
    app.MapTransferEndpoints();
    
    Console.WriteLine("All endpoints mapped");
    Console.WriteLine($"Swagger UI available at: http://localhost:{port}/swagger");
    Console.WriteLine($"Starting web host on port {port}...");
    
    app.Run();
    
    Console.WriteLine("Application stopped gracefully");
}
catch (HostAbortedException ex)
{
    // Expected when EF Core tools resolve the application service provider at design time.
    Console.WriteLine("Application host aborted by design-time tooling.");
    Console.WriteLine($"Info: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"=== APPLICATION STARTUP FAILED ===");
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"Type: {ex.GetType().Name}");
    Console.WriteLine($"StackTrace:");
    Console.WriteLine(ex.StackTrace);
    
    if (ex.InnerException != null)
    {
        Console.WriteLine($"\nInner Exception: {ex.InnerException.Message}");
        Console.WriteLine(ex.InnerException.StackTrace);
    }
    
    Log.Fatal(ex, "Application terminated unexpectedly");
    
    Environment.Exit(1);
}
finally
{
    Log.CloseAndFlush();
}


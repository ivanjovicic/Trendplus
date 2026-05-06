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
using Application.Config;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Api.Services;
using Api.Endpoints;
using Api.Services.Access;
using Api.Middleware;
using Api.Services.Startup;
using Api.Config;
using Npgsql;
using System.Threading.RateLimiting;
using Application.Documents.Interfaces;
using Infrastructure.Configuration;
using Infrastructure.Services.Email;
using Infrastructure.Services.Storage;
using Polly;
using Polly.Extensions.Http;
using System.Diagnostics;

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
        .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.local.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();

    Console.WriteLine("Configuration loaded");

    // Runtime process selector:
    // - PROCESS_TYPE=web|worker (canonical)
    // - WORKER_PROCESS=true (compatibility alias when PROCESS_TYPE is missing)
    // - default: web
    var processType = WorkerRuntimeConfig.ResolveProcessType(builder.Configuration, out var processTypeSource);
    var isWorkerProcess = processType == ProcessType.Worker;
    if (processTypeSource == "PROCESS_TYPE_INVALID")
    {
        Console.WriteLine("Invalid PROCESS_TYPE value. Falling back to web process mode.");
    }

    // Worker runtime switch policy:
    // - Explicit Workers:Enabled always wins (safety toggle).
    // - Otherwise: enabled for worker process and development; disabled for production web process.
    var workersEnabledFromConfig = builder.Configuration.GetValue<bool?>("Workers:Enabled");
    var workersEnabled = WorkerRuntimeConfig.ResolveWorkersEnabled(
        workersEnabledFromConfig,
        processType,
        builder.Environment.IsDevelopment());
    var workersRuntimeToggleAllowedFromConfig = builder.Configuration.GetValue<bool?>("Workers:AllowRuntimeToggle");
    var workersRuntimeToggleAllowed = workersRuntimeToggleAllowedFromConfig ?? builder.Environment.IsDevelopment();
    var workersEnabledSource = WorkerRuntimeConfig.ResolveWorkersEnabledSource(
        workersEnabledFromConfig,
        processType,
        builder.Environment.IsDevelopment());

    Console.WriteLine($"Process type: {processType.ToString().ToLowerInvariant()} (source: {processTypeSource})");
    if (processTypeSource == "WORKER_PROCESS")
    {
        Console.WriteLine("WORKER_PROCESS alias was used because PROCESS_TYPE was not set.");
    }
    if (isWorkerProcess && workersEnabledFromConfig == false)
    {
        Console.WriteLine("WARNING: Worker process is configured with Workers:Enabled=false. Workers will stay paused until enabled.");
    }
    else if (!isWorkerProcess && workersEnabledFromConfig == true)
    {
        Console.WriteLine("Workers:Enabled=true is set, but this process resolved to web mode so worker hosted services will not be registered.");
    }

    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = 25 * 1024 * 1024;
        options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(20);
        options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
        options.Limits.MinRequestBodyDataRate = null;
    });

    Console.WriteLine($"Configured to listen on port {port}");

    // Options binding
    builder.Services.Configure<SerpApiOptions>(builder.Configuration.GetSection(SerpApiOptions.Section));
    builder.Services.Configure<EbayOptions>(builder.Configuration.GetSection(EbayOptions.Section));
    builder.Services.Configure<GoogleShoppingOptions>(builder.Configuration.GetSection(GoogleShoppingOptions.Section));
    builder.Services.Configure<RuntimeScoringOptions>(builder.Configuration.GetSection(RuntimeScoringOptions.Section));
    builder.Services.Configure<AccessImportOptions>(builder.Configuration.GetSection(AccessImportOptions.Section));
    builder.Services.Configure<Infrastructure.Configuration.AnalyticsDataQualityHealthOptions>(
        builder.Configuration.GetSection(Infrastructure.Configuration.AnalyticsDataQualityHealthOptions.Section));
    builder.Services.Configure<Infrastructure.Configuration.NightlyAnalyticsRefreshOptions>(
        builder.Configuration.GetSection(Infrastructure.Configuration.NightlyAnalyticsRefreshOptions.Section));
    builder.Services.Configure<Infrastructure.Configuration.OpenTrainingModelTrainingOptions>(
        builder.Configuration.GetSection(Infrastructure.Configuration.OpenTrainingModelTrainingOptions.Section));
    builder.Services.Configure<Infrastructure.Configuration.TrendIngestionOptions>(
        builder.Configuration.GetSection(Infrastructure.Configuration.TrendIngestionOptions.Section));
    builder.Services.Configure<DocumentExportOptions>(builder.Configuration.GetSection(DocumentExportOptions.Section));
    builder.Services.Configure<PerformanceLoggingOptions>(builder.Configuration.GetSection(PerformanceLoggingOptions.Section));
    builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.Section));
    builder.Services.Configure<Infrastructure.Configuration.AnalyticsSnapshotOptions>(
        builder.Configuration.GetSection(Infrastructure.Configuration.AnalyticsSnapshotOptions.Section));

    builder.Services.AddFileStorage(builder.Configuration);
    var fileStorageProvider = FileStorageServiceCollectionExtensions.ResolveProviderName(
        builder.Configuration[$"{StorageOptions.Section}:Provider"]);
    Console.WriteLine($"File storage provider: {fileStorageProvider}");

    var documentOptions = builder.Configuration.GetSection(DocumentExportOptions.Section).Get<DocumentExportOptions>() ?? new DocumentExportOptions();
    var resolvedDocumentSigningKey = documentOptions.ResolveSigningKey();
    if (builder.Environment.IsProduction() && string.IsNullOrWhiteSpace(resolvedDocumentSigningKey))
    {
        throw new InvalidOperationException("Documents signing key is required in production. Configure Documents:SigningKey or DOCUMENT_SIGNING_KEY.");
    }

    var dbCommandTimeoutSeconds =
        builder.Configuration.GetValue<int?>("Database:CommandTimeoutSeconds")
        ?? 300;
    var dbOpenTimeoutSeconds =
        builder.Configuration.GetValue<int?>("Database:Npgsql:OpenTimeoutSeconds")
        ?? 15;
    var dbKeepAliveSeconds =
        builder.Configuration.GetValue<int?>("Database:Npgsql:KeepAliveSeconds")
        ?? 30;
    var dbMaxPoolSize =
        builder.Configuration.GetValue<int?>("Database:Npgsql:MaxPoolSize");
    var dbMinPoolSize =
        builder.Configuration.GetValue<int?>("Database:Npgsql:MinPoolSize");
    var dbConnectionIdleLifetimeSeconds =
        builder.Configuration.GetValue<int?>("Database:Npgsql:ConnectionIdleLifetimeSeconds");
    var dbConnectionPruningIntervalSeconds =
        builder.Configuration.GetValue<int?>("Database:Npgsql:ConnectionPruningIntervalSeconds");
    var enableEfRetryOnFailure =
        builder.Configuration.GetValue<bool?>("Database:Npgsql:EnableEfRetryOnFailure")
        ?? true;
    var efRetryMaxCount =
        builder.Configuration.GetValue<int?>("Database:Npgsql:EfRetry:MaxRetryCount")
        ?? 3;
    var efRetryMaxDelaySeconds =
        builder.Configuration.GetValue<int?>("Database:Npgsql:EfRetry:MaxRetryDelaySeconds")
        ?? 5;

    static string SummarizeConnection(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return "<missing>";

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var host = string.IsNullOrWhiteSpace(builder.Host) ? "<unknown-host>" : builder.Host;
            var port = builder.Port;
            var database = string.IsNullOrWhiteSpace(builder.Database) ? "<unknown-db>" : builder.Database;
            var username = string.IsNullOrWhiteSpace(builder.Username) ? "<unknown-user>" : builder.Username;
            return $"{host}:{port}/{database} user={username}";
        }
        catch
        {
            return "<unparseable>";
        }
    }

    static bool IsLoopbackConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return false;

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var hosts = (builder.Host ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var host in hosts)
            {
                if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                    host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                    host.Equals("::1", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Ignore parse failures, caller can still proceed with original value.
        }

        return false;
    }

    static string? ApplyNpgsqlTuning(
        string? connectionString,
        int openTimeoutSeconds,
        int commandTimeoutSeconds,
        int keepAliveSeconds,
        int? maxPoolSize,
        int? minPoolSize,
        int? connectionIdleLifetimeSeconds,
        int? connectionPruningIntervalSeconds)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Timeout = Math.Max(1, openTimeoutSeconds),
                CommandTimeout = Math.Max(1, commandTimeoutSeconds),
                KeepAlive = Math.Max(0, keepAliveSeconds)
            };

            if (maxPoolSize.HasValue && maxPoolSize.Value > 0)
                builder.MaxPoolSize = maxPoolSize.Value;
            if (minPoolSize.HasValue && minPoolSize.Value >= 0)
                builder.MinPoolSize = minPoolSize.Value;
            if (connectionIdleLifetimeSeconds.HasValue && connectionIdleLifetimeSeconds.Value > 0)
                builder.ConnectionIdleLifetime = connectionIdleLifetimeSeconds.Value;
            if (connectionPruningIntervalSeconds.HasValue && connectionPruningIntervalSeconds.Value > 0)
                builder.ConnectionPruningInterval = connectionPruningIntervalSeconds.Value;

            return builder.ConnectionString;
        }
        catch
        {
            return connectionString;
        }
    }

    var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
    var analyticsConnection = builder.Configuration.GetConnectionString("AnalyticsConnection");
    var tunedDefaultConnection = ApplyNpgsqlTuning(
        defaultConnection,
        dbOpenTimeoutSeconds,
        dbCommandTimeoutSeconds,
        dbKeepAliveSeconds,
        dbMaxPoolSize,
        dbMinPoolSize,
        dbConnectionIdleLifetimeSeconds,
        dbConnectionPruningIntervalSeconds);
    var tunedAnalyticsConnection = ApplyNpgsqlTuning(
        analyticsConnection,
        dbOpenTimeoutSeconds,
        dbCommandTimeoutSeconds,
        dbKeepAliveSeconds,
        dbMaxPoolSize,
        dbMinPoolSize,
        dbConnectionIdleLifetimeSeconds,
        dbConnectionPruningIntervalSeconds);

    Console.WriteLine($"DefaultConnection target: {SummarizeConnection(defaultConnection)}");
    Console.WriteLine($"AnalyticsConnection target: {SummarizeConnection(analyticsConnection)}");
    Console.WriteLine(
        $"Npgsql tuning: OpenTimeout={dbOpenTimeoutSeconds}s CommandTimeout={dbCommandTimeoutSeconds}s KeepAlive={dbKeepAliveSeconds}s MaxPoolSize={(dbMaxPoolSize?.ToString() ?? "default")} MinPoolSize={(dbMinPoolSize?.ToString() ?? "default")} IdleLifetime={(dbConnectionIdleLifetimeSeconds?.ToString() ?? "default")}s PruningInterval={(dbConnectionPruningIntervalSeconds?.ToString() ?? "default")}s EfRetryEnabled={enableEfRetryOnFailure} EfRetryMaxCount={efRetryMaxCount} EfRetryMaxDelay={efRetryMaxDelaySeconds}s");

    // DbContext
    builder.Services.AddDbContextFactory<TrendplusDbContext>(options =>
        options.UseNpgsql(
                tunedDefaultConnection,
                npgsql =>
                {
                    npgsql.CommandTimeout(dbCommandTimeoutSeconds);
                    if (enableEfRetryOnFailure)
                    {
                        npgsql.EnableRetryOnFailure(efRetryMaxCount, TimeSpan.FromSeconds(Math.Max(1, efRetryMaxDelaySeconds)), null);
                    }
                })
               .EnableSensitiveDataLogging(builder.Environment.IsDevelopment()));

    builder.Services.AddDbContext<TrendplusDbContext>(options =>
        options.UseNpgsql(
                tunedDefaultConnection,
                npgsql =>
                {
                    npgsql.CommandTimeout(dbCommandTimeoutSeconds);
                    if (enableEfRetryOnFailure)
                    {
                        npgsql.EnableRetryOnFailure(efRetryMaxCount, TimeSpan.FromSeconds(Math.Max(1, efRetryMaxDelaySeconds)), null);
                    }
                })
               .EnableSensitiveDataLogging(builder.Environment.IsDevelopment()));

    builder.Services.AddScoped<ITrendplusDbContext>(sp =>
        sp.GetRequiredService<TrendplusDbContext>());

    builder.Services.AddDbContext<AnalyticsDbContext>(options =>
        options.UseNpgsql(
                tunedAnalyticsConnection,
                npgsql =>
                {
                    npgsql.CommandTimeout(dbCommandTimeoutSeconds);
                    if (enableEfRetryOnFailure)
                    {
                        npgsql.EnableRetryOnFailure(efRetryMaxCount, TimeSpan.FromSeconds(Math.Max(1, efRetryMaxDelaySeconds)), null);
                    }
                })
               .EnableSensitiveDataLogging(builder.Environment.IsDevelopment()));

    builder.Services.AddScoped<IAnalyticsDbContext>(sp =>
        sp.GetRequiredService<AnalyticsDbContext>());

    var configuredOpenProductTrainingConnection =
        builder.Configuration.GetConnectionString("OpenProductTrainingConnection");
    var openProductTrainingConnection = string.IsNullOrWhiteSpace(configuredOpenProductTrainingConnection)
        ? analyticsConnection
        : configuredOpenProductTrainingConnection;

    // Production safety net:
    // if OpenProductTrainingConnection accidentally points to localhost/loopback in non-dev,
    // fallback to AnalyticsConnection (when available and not loopback).
    if (!builder.Environment.IsDevelopment() &&
        IsLoopbackConnectionString(openProductTrainingConnection) &&
        !string.IsNullOrWhiteSpace(analyticsConnection) &&
        !IsLoopbackConnectionString(analyticsConnection))
    {
        Console.WriteLine(
            "WARNING: OpenProductTrainingConnection points to loopback host in non-development environment. " +
            "Falling back to AnalyticsConnection.");
        openProductTrainingConnection = analyticsConnection;
    }
    var tunedOpenProductTrainingConnection = ApplyNpgsqlTuning(
        openProductTrainingConnection,
        dbOpenTimeoutSeconds,
        dbCommandTimeoutSeconds,
        dbKeepAliveSeconds,
        dbMaxPoolSize,
        dbMinPoolSize,
        dbConnectionIdleLifetimeSeconds,
        dbConnectionPruningIntervalSeconds);

    Console.WriteLine($"OpenProductTrainingConnection target: {SummarizeConnection(openProductTrainingConnection)}");

    builder.Services.AddDbContext<OpenProductTrainingDbContext>(options =>
        options.UseNpgsql(
                tunedOpenProductTrainingConnection,
                o =>
                {
                    o.UseVector();
                    o.CommandTimeout(dbCommandTimeoutSeconds);
                    if (enableEfRetryOnFailure)
                    {
                        o.EnableRetryOnFailure(efRetryMaxCount, TimeSpan.FromSeconds(Math.Max(1, efRetryMaxDelaySeconds)), null);
                    }
                })
               .EnableSensitiveDataLogging(builder.Environment.IsDevelopment()));

    Console.WriteLine("DbContext registered");

    // FluentValidation - auto-register all validators
    builder.Services.AddValidatorsFromAssemblyContaining<CreateArtikalCommandValidator>();
    builder.Services.AddValidatorsFromAssemblyContaining<Api.Validators.StartTrainingRunRequestValidator>();

    // Memory Cache - required by GetArtikliQueryHandler and other caching services
    builder.Services.AddMemoryCache();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddSingleton<StartupReadinessState>();
    builder.Services.AddHostedService<ReadinessWarmupHostedService>();
    if (!isWorkerProcess)
    {
        builder.Services.AddHostedService<AnalyticsConnectionDiagnosticsHostedService>();
        builder.Services.AddHostedService<AnalyticsCachePrewarmHostedService>();
    }
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });

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
builder.Services.AddScoped<IDailySalesStatsService, DailySalesStatsService>();
builder.Services.AddScoped<AnalyticsDataQualityHealthService>();
builder.Services.AddScoped<AnalyticsDataQualityHistoryService>();
builder.Services.AddScoped<Api.Services.AnalyticsCostSnapshotService>();
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
            workersEnabledSource));
    
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
        client.Timeout = TimeSpan.FromSeconds(60); // Increased from 30s to 60s for cold start scenarios
    });
    
    // Named HttpClient for scraper service (Python), configured from appsettings or env
    var scraperBase = builder.Configuration["ScraperService:BaseUrl"] ?? "http://localhost:8000";
    builder.Services.AddHttpClient("scraper", client =>
    {
        client.BaseAddress = new Uri(scraperBase);
        client.Timeout = TimeSpan.FromSeconds(60); // Increased from 30s to 60s for cold start
    })
    .AddPolicyHandler((sp, request) =>
    {
        var logger = sp.GetRequiredService<ILogger<Program>>();
        IAsyncPolicy<HttpResponseMessage> retry = HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => (int)r.StatusCode == 429 || (int)r.StatusCode == 503) // Also handle service unavailable (cold start)
            .WaitAndRetryAsync(
                retryCount: 4,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential: 2s, 4s, 8s, 16s
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
        client.Timeout = TimeSpan.FromSeconds(Math.Max(30, pythonModelTimeout)); // Increased from 5s minimum to 30s for cold start
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

    // P0 runtime gating: web processes must not register critical worker hosted services.
    WorkerRuntimeConfig.RegisterWorkerHostedServices(builder.Services, isWorkerProcess);
    Console.WriteLine($"Background workers startup state: {(workersEnabled ? "ENABLED" : "DISABLED")}");
    Console.WriteLine($"Background workers runtime toggle: {(workersRuntimeToggleAllowed ? "ALLOWED" : "LOCKED")}");
    Console.WriteLine($"Worker hosted services registered: {(isWorkerProcess ? "YES" : "NO")}");

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
    builder.Services.AddScoped<INivelacijaRepairService, NivelacijaRepairService>();
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
    var allowedHealthOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "http://localhost:5173",
        "http://localhost:5174",
        "http://localhost:8080",
        "https://trendplus.vercel.app"
    };

    static void ApplyHealthCorsHeaders(HttpContext context, ISet<string> allowedOrigins)
    {
        if (!context.Request.Headers.TryGetValue("Origin", out var originValues))
            return;

        var origin = originValues.ToString();
        if (string.IsNullOrWhiteSpace(origin) || !allowedOrigins.Contains(origin))
            return;

        context.Response.Headers["Access-Control-Allow-Origin"] = origin;
        context.Response.Headers["Vary"] = "Origin";
        context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
        context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization, X-Requested-With";
        context.Response.Headers["Access-Control-Allow-Methods"] = "GET, OPTIONS";
    }

    async Task<IResult> CheckDatabaseHealthAsync(CancellationToken ct)
    {
        static async Task<(bool Ok, long ElapsedMs, string? Error)> ProbeAsync(string name, string? connectionString, CancellationToken requestToken)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return (false, 0, $"{name} connection string is missing");
            }

            var sw = Stopwatch.StartNew();

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(requestToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

                var csb = new NpgsqlConnectionStringBuilder(connectionString)
                {
                    Timeout = 5,
                    CommandTimeout = 5
                };

                await using var connection = new NpgsqlConnection(csb.ConnectionString);
                await connection.OpenAsync(timeoutCts.Token);

                await using var command = new NpgsqlCommand("SELECT 1;", connection)
                {
                    CommandTimeout = 5
                };

                await command.ExecuteScalarAsync(timeoutCts.Token);
                sw.Stop();
                return (true, sw.ElapsedMilliseconds, null);
            }
            catch (OperationCanceledException) when (requestToken.IsCancellationRequested)
            {
                sw.Stop();
                return (false, sw.ElapsedMilliseconds, "request_aborted");
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                return (false, sw.ElapsedMilliseconds, "timeout");
            }
            catch (Exception ex)
            {
                sw.Stop();
                return (false, sw.ElapsedMilliseconds, ex.GetBaseException().Message);
            }
        }

        var defaultDb = await ProbeAsync("default", defaultConnection, ct);
        var analyticsDb = await ProbeAsync("analytics", analyticsConnection, ct);
        var ok = defaultDb.Ok && analyticsDb.Ok;

        if (ok)
        {
            app.Services.GetRequiredService<StartupReadinessState>().MarkReady();
        }

        var payload = new
        {
            status = ok ? "healthy" : "unhealthy",
            checks = new
            {
                defaultDb = new { ok = defaultDb.Ok, elapsedMs = defaultDb.ElapsedMs, error = defaultDb.Error },
                analyticsDb = new { ok = analyticsDb.Ok, elapsedMs = analyticsDb.ElapsedMs, error = analyticsDb.Error }
            }
        };

        return ok ? Results.Ok(payload) : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    // --- Optional guarded automatic migrations ---
    // Applies migrations automatically in Development or when the configuration
    // flag `Database:AutoMigrate` is set to true. This is safe for local/dev
    // environments but guarded to avoid accidental production schema changes.
    if (builder.Configuration.GetValue<bool>("Database:AutoMigrate") || app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
            Console.WriteLine("Auto-migrate enabled - applying EF Core migrations...");
            db.Database.Migrate();
            Console.WriteLine("Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetService<ILogger<Program>>();
            logger?.LogError(ex, "Auto-migrate failed");
            // In non-development environments prefer failing fast so deployment/job runner
            // becomes aware of migration problems. In development we swallow to avoid
            // blocking iterative work.
            if (!app.Environment.IsDevelopment())
                throw;
        }
    }

    // ================= MIDDLEWARE PIPELINE =================

    app.UseForwardedHeaders();
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/health") || path.StartsWithSegments("/ready"))
        {
            ApplyHealthCorsHeaders(context, allowedHealthOrigins);

            if (HttpMethods.IsOptions(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }
        }

        await next(context);
    });

    // 1. Global exception handler (first in pipeline)
    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseMiddleware<SqlLoggingRequestContextMiddleware>();
    app.UseMiddleware<RequestPerformanceLoggingMiddleware>();

    app.Use(async (context, next) =>
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/health") ||
            path.StartsWithSegments("/ready") ||
            path.StartsWithSegments("/swagger"))
        {
            await next(context);
            return;
        }

        var readiness = context.RequestServices.GetRequiredService<StartupReadinessState>();
        if (!readiness.IsReady)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";
            context.Response.Headers.RetryAfter = "5";
            await context.Response.WriteAsJsonAsync(new
            {
                status = "starting",
                reason = readiness.Reason,
                retryAfterSeconds = 5
            }, context.RequestAborted);
            return;
        }

        await next(context);
    });

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
    app.MapGet("/health", CheckDatabaseHealthAsync).AllowAnonymous();
    app.MapGet("/ready", (StartupReadinessState readiness) =>
    {
        var payload = new
        {
            ready = readiness.IsReady,
            reason = readiness.Reason,
            startedAtUtc = readiness.StartedAtUtc,
            readyAtUtc = readiness.ReadyAtUtc
        };

        return readiness.IsReady
            ? Results.Ok(payload)
            : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
    }).AllowAnonymous();

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
    app.MapAdminRepairEndpoints();
    app.MapRedisEndpoints();
    app.MapOutboxEndpoints();
    app.MapAnalyticsTableEndpoints();
    app.MapDataQualityEndpoints();
    app.MapDailySalesStatsEndpoints();
    app.MapAnalyticsSnapshotEndpoints();
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

// Make Program class public for WebApplicationFactory in tests
public partial class Program { }

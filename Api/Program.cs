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
using Api.Services.DataSources;
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
    var accessImportWorkerEnabled = builder.Configuration.GetValue<bool?>("AccessImport:WorkerEnabled")
        ?? new AccessImportOptions().WorkerEnabled;
    var accessImportRegisterInWebProcess = builder.Configuration.GetValue<bool?>("AccessImport:RegisterWorkerInWebProcess");
    var registerAccessImportWorkerInWebProcess = WorkerRuntimeConfig.ResolveAccessImportWorkerInWebProcess(
        accessImportRegisterInWebProcess,
        accessImportWorkerEnabled,
        workersEnabledFromConfig == false,
        processType);
    var effectiveWorkersEnabled = workersEnabled || registerAccessImportWorkerInWebProcess;
    var effectiveWorkersRuntimeToggleAllowed = workersRuntimeToggleAllowed || registerAccessImportWorkerInWebProcess;
    var effectiveWorkersEnabledSource = registerAccessImportWorkerInWebProcess && !workersEnabled
        ? "access-import-web-default"
        : workersEnabledSource;

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
        Console.WriteLine("Workers:Enabled=true is set, but this process resolved to web mode. Only web-safe hosted services may be registered.");
    }
    Console.WriteLine(
        $"Access import worker config: WorkerEnabled={accessImportWorkerEnabled} RegisterWorkerInWebProcess={(accessImportRegisterInWebProcess ?? true)} WillRegisterInWeb={registerAccessImportWorkerInWebProcess}");
    var processStartTimeUtc = Process.GetCurrentProcess().StartTime.ToUniversalTime();

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
    builder.Services.Configure<DataSourceConnectorOptions>(builder.Configuration.GetSection(DataSourceConnectorOptions.SectionName));
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
    builder.Services.Configure<Api.Services.Analytics.DecisionPulseOptions>(
        builder.Configuration.GetSection(Api.Services.Analytics.DecisionPulseOptions.Section));
    builder.Services.Configure<Infrastructure.Configuration.AnalyticsSnapshotOptions>(
        builder.Configuration.GetSection(Infrastructure.Configuration.AnalyticsSnapshotOptions.Section));

    builder.Services.AddSingleton<ISourceSessionFactory, SourceSessionFactory>();
    builder.Services.AddSingleton<NamedSourceDiscoveryService>();
    builder.Services.AddSingleton<SourceMappingPreviewService>();
    builder.Services.AddSingleton<SourceCheckpointSyncEngine>();
    builder.Services.AddScoped<ISourceSyncStore, EfSourceSyncStore>();
    builder.Services.AddScoped<SourceCheckpointSyncService>();
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
    var configuredAnalyticsConnection = builder.Configuration.GetConnectionString("AnalyticsConnection");
    var analyticsConnectionResolution = AnalyticsConnectionResolver.ResolveDetailed(
        builder.Configuration,
        builder.Environment.IsDevelopment(),
        warning => Console.WriteLine($"WARNING: {warning}"));
    var analyticsConnection = analyticsConnectionResolution.ConnectionString;
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

    Console.WriteLine($"DefaultConnection target: {AnalyticsConnectionResolver.SummarizeConnection(defaultConnection)}");
    Console.WriteLine($"AnalyticsConnection configured target: {AnalyticsConnectionResolver.SummarizeConnection(configuredAnalyticsConnection)}");
    Console.WriteLine($"AnalyticsConnection resolved target: {AnalyticsConnectionResolver.SummarizeConnection(analyticsConnection)}");
    Console.WriteLine($"AnalyticsConnection source: {analyticsConnectionResolution.Source} UsedFallback={analyticsConnectionResolution.UsedFallback}");
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
        AnalyticsConnectionResolver.IsLoopbackConnectionString(openProductTrainingConnection) &&
        !string.IsNullOrWhiteSpace(analyticsConnection) &&
        !AnalyticsConnectionResolver.IsLoopbackConnectionString(analyticsConnection))
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

    Console.WriteLine($"OpenProductTrainingConnection target: {AnalyticsConnectionResolver.SummarizeConnection(openProductTrainingConnection)}");

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
        builder.Services.AddHostedService<WorkerRuntimeSettingsSchemaBootstrapHostedService>();
        builder.Services.AddHostedService<SupplierDecisionSchemaRepairHostedService>();
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
builder.Services.AddScoped<IInventoryForecastSnapshotMaterializerService, Infrastructure.Services.Inventory.InventoryForecastSnapshotMaterializerService>();
builder.Services.AddScoped<AnalyticsRefreshRunRecorder>();
builder.Services.AddScoped<Api.Services.AnalyticsCostSnapshotService>();
builder.Services.AddScoped<Infrastructure.Services.Analytics.AnalyticsActionItemService>();
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
    builder.Services.AddScoped<Api.Services.Analytics.DecisionPulseService>();
    builder.Services.AddScoped<IInventoryReportScheduleService, Infrastructure.Services.Inventory.InventoryReportScheduleService>();
    builder.Services.AddScoped<IInventoryActionDecisionService, Infrastructure.Services.Inventory.InventoryActionDecisionService>();
    builder.Services.AddScoped<Infrastructure.Services.Inventory.InventoryReportDeliveryService>();
    builder.Services.AddSingleton<WorkerHealthService>(); // Worker health monitoring
    builder.Services.AddSingleton(sp =>
        new WorkerRuntimeControlService(
            effectiveWorkersEnabled,
            effectiveWorkersRuntimeToggleAllowed,
            effectiveWorkersEnabledSource));
    builder.Services.AddScoped<WorkerConfigurationService>(); // Per-worker runtime settings
    builder.Services.AddSingleton<WorkerRuntimePolicyService>();
    builder.Services.AddScoped<WorkerRegistryService>();
    builder.Services.AddScoped<AnalyticsRefreshStatusService>();
    builder.Services.AddSingleton<BackendRoutingPreferenceService>();
    
    // Embedding service for AI-powered image search
    var embeddingServiceSettings = EmbeddingServiceRuntimePolicy.Resolve(
        builder.Configuration,
        builder.Environment.EnvironmentName);

    if (embeddingServiceSettings.Mode == EmbeddingServiceRuntimeMode.Mock)
    {
        Console.WriteLine("⚠️ Using MOCK embedding service (no AI)");
        builder.Services.AddScoped<IEmbeddingService, MockEmbeddingService>();
    }
    else if (embeddingServiceSettings.Mode == EmbeddingServiceRuntimeMode.Disabled)
    {
        Console.WriteLine("⛔ Embedding service DISABLED (quarantined; no mock vectors)");
        builder.Services.AddScoped<IEmbeddingService, DisabledEmbeddingService>();
    }
    else
    {
        Console.WriteLine($"✅ Using Python embedding service at: {embeddingServiceSettings.BaseAddress}");
        builder.Services.AddHttpClient<IEmbeddingService, PythonEmbeddingService>(client =>
        {
            client.BaseAddress = embeddingServiceSettings.BaseAddress!;
            client.Timeout = embeddingServiceSettings.Timeout;
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

    // P0 runtime gating: web processes must not register the full critical worker fleet.
    WorkerRuntimeConfig.RegisterWorkerHostedServices(builder.Services, isWorkerProcess);
    if (registerAccessImportWorkerInWebProcess)
    {
        builder.Services.AddHostedService<AccessImportBackgroundWorker>();
    }
    // Register only explicitly web-safe workers.
    // Heavy analytics refresh workers must stay worker-process-only.
    var nightlyRegisteredInWeb = false;
    if (!isWorkerProcess && workersEnabled)
    {
        foreach (var def in WorkerRegistryCatalog.Definitions
            .Where(d => d.RegistersInWebProcess && !d.RequiresWebAccessImportFlag))
        {
            WorkerRuntimeConfig.RegisterWebEligibleWorker(builder.Services, def.WorkerName);
            nightlyRegisteredInWeb = true;
        }
    }
    Console.WriteLine($"Background workers startup state: {(effectiveWorkersEnabled ? "ENABLED" : "DISABLED")}");
    Console.WriteLine($"Background workers runtime toggle: {(effectiveWorkersRuntimeToggleAllowed ? "ALLOWED" : "LOCKED")}");
    Console.WriteLine($"Worker hosted services registered: {(isWorkerProcess ? "YES" : registerAccessImportWorkerInWebProcess ? "ACCESS_IMPORT_ONLY" : nightlyRegisteredInWeb ? "WEB_ELIGIBLE" : "NO")}");

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

    var corsAllowedOrigins = CorsOriginsResolver.Resolve(builder.Configuration, builder.Environment);
    builder.Services.Configure<CorsOriginsOptions>(builder.Configuration.GetSection(CorsOriginsOptions.SectionName));
    builder.Services.Configure<SwaggerExposureOptions>(builder.Configuration.GetSection(SwaggerExposureOptions.SectionName));
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy
               .WithOrigins(corsAllowedOrigins)
               .AllowAnyHeader()
               .AllowAnyMethod()
               .AllowCredentials();
        });
    });

    // Cache wiring:
    // AnalyticsCache:Provider = Redis | Memory | Disabled
    // Legacy fallback: Caching:UseRedis (for backward compatibility)
    builder.Services.AddDistributedMemoryCache();
    var configuredCacheProvider = builder.Configuration["AnalyticsCache:Provider"];
    var resolvedCacheProvider = string.IsNullOrWhiteSpace(configuredCacheProvider)
        ? ((builder.Configuration.GetValue<bool?>("Caching:UseRedis") ?? !builder.Environment.IsDevelopment()) ? "redis" : "memory")
        : configuredCacheProvider.Trim().ToLowerInvariant();

    if (resolvedCacheProvider == "redis")
    {
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
            options.Configuration = $"{redisConnection},abortConnect=false,connectTimeout=500,syncTimeout=500,asyncTimeout=500,connectRetry=1";
            options.InstanceName = "trendplus:";
        });
        builder.Services.AddSingleton<IAnalyticsCacheService, HybridCacheService>();
        Console.WriteLine("Analytics cache provider: Redis/Hybrid");
    }
    else if (resolvedCacheProvider == "disabled")
    {
        builder.Services.AddSingleton<IAnalyticsCacheService, DisabledAnalyticsCacheService>();
        Console.WriteLine("Analytics cache provider: Disabled");
    }
    else
    {
        builder.Services.AddSingleton<IAnalyticsCacheService, InMemoryCacheService>();
        Console.WriteLine("Analytics cache provider: Memory");
        if (builder.Environment.IsProduction())
        {
            Console.WriteLine("WARNING: Analytics cache provider is Memory in production. Multi-instance deployments can serve inconsistent cache data.");
        }
    }

    builder.Services.AddSingleton<AnalyticsCacheAdminService>();

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
    using (var scope = app.Services.CreateScope())
    {
        var cacheAdmin = scope.ServiceProvider.GetRequiredService<AnalyticsCacheAdminService>();
        var (cacheMode, isDistributed) = cacheAdmin.ResolveCacheMode();
        app.Logger.LogInformation(
            "Analytics cache mode resolved at startup. Mode={CacheMode} IsDistributed={IsDistributed}",
            cacheMode,
            isDistributed);

        if (app.Environment.IsProduction() && string.Equals(cacheMode, "in-memory", StringComparison.OrdinalIgnoreCase))
        {
            app.Logger.LogWarning(
                "Analytics cache je in-memory. U multi-instance okruženju podaci mogu biti nekonzistentni između instanci.");
        }
    }

    var allowedHealthOrigins = CorsOriginsResolver.ToSet(corsAllowedOrigins);

    static string ResolveProviderName(HttpContext? context)
    {
        var explicitProvider = Environment.GetEnvironmentVariable("BACKEND_PROVIDER");
        if (!string.IsNullOrWhiteSpace(explicitProvider))
        {
            return explicitProvider.Trim().ToLowerInvariant() switch
            {
                "fly" or "fly.io" => "fly",
                "render" => "render",
                _ => "render"
            };
        }

        var host = context?.Request.Host.Host ?? string.Empty;
        if (host.Contains("fly.dev", StringComparison.OrdinalIgnoreCase) ||
            host.Contains("fly.io", StringComparison.OrdinalIgnoreCase))
        {
            return "fly";
        }

        return "render";
    }

    string ResolveRuntimeCommitSha()
    {
        var commitSha = new[]
        {
            "RENDER_GIT_COMMIT",
            "GIT_COMMIT_SHA",
            "SOURCE_VERSION",
            "Build:CommitSha",
            "BUILD_COMMIT_SHA"
        }
        .Select(key => builder.Configuration[key])
        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return string.IsNullOrWhiteSpace(commitSha) ? "unknown" : commitSha.Trim();
    }

    string ResolveRuntimeBuildTimeUtc()
    {
        var rawBuildTime = new[]
        {
            builder.Configuration["BUILD_TIME_UTC"],
            builder.Configuration["Build:TimeUtc"],
            builder.Configuration["Runtime:BuildTimeUtc"]
        }
        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (!string.IsNullOrWhiteSpace(rawBuildTime))
        {
            if (DateTimeOffset.TryParse(rawBuildTime, CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedOffset))
            {
                return parsedOffset.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
            }

            if (DateTime.TryParse(rawBuildTime, CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsedDateTime))
            {
                return DateTime.SpecifyKind(parsedDateTime, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);
            }
        }

        return processStartTimeUtc.ToString("O", CultureInfo.InvariantCulture);
    }

    string ResolveRuntimeProviderName(HttpContext? context)
    {
        var explicitProvider = builder.Configuration["BACKEND_PROVIDER"];
        if (!string.IsNullOrWhiteSpace(explicitProvider))
        {
            return explicitProvider.Trim().ToLowerInvariant() switch
            {
                "local" => "local",
                "render" or "render.com" => "render",
                "unknown" => "unknown",
                _ => "unknown"
            };
        }

        if (builder.Environment.IsDevelopment())
        {
            return "local";
        }

        if (builder.Configuration["RENDER_GIT_COMMIT"] is not null ||
            builder.Configuration["RENDER_SERVICE_NAME"] is not null ||
            builder.Configuration["RENDER_EXTERNAL_URL"] is not null ||
            builder.Configuration["RENDER"] is not null ||
            builder.Configuration["RENDER_SERVICE_ID"] is not null)
        {
            return "render";
        }

        var host = context?.Request.Host.Host ?? string.Empty;
        if (host.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            host.Contains("::1", StringComparison.OrdinalIgnoreCase) ||
            host.Contains("onrender.com", StringComparison.OrdinalIgnoreCase))
        {
            return host.Contains("onrender.com", StringComparison.OrdinalIgnoreCase) ? "render" : "local";
        }

        return "unknown";
    }

    static long? ResolveProbeLatency(long? defaultLatencyMs, long? analyticsLatencyMs)
    {
        if (defaultLatencyMs.HasValue && analyticsLatencyMs.HasValue)
        {
            return Math.Max(defaultLatencyMs.Value, analyticsLatencyMs.Value);
        }

        return defaultLatencyMs ?? analyticsLatencyMs;
    }

    IResult CheckLiveness(HttpContext context)
    {
        var readinessState = app.Services.GetRequiredService<StartupReadinessState>();
        var payload = new
        {
            status = "healthy",
            provider = ResolveProviderName(context),
            ready = readinessState.IsReady,
            timestampUtc = DateTimeOffset.UtcNow
        };

        return Results.Ok(payload);
    }

    async Task<IResult> CheckDatabaseHealthAsync(HttpContext context, CancellationToken ct)
    {
        var logger = context.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("Api.Health.Dependencies");
        var correlationId =
            context.Response.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        var defaultDb = await DbConnectionHelper.TryProbeConnectionStringAsync("default", defaultConnection, ct, logger, correlationId);
        var analyticsDb = await DbConnectionHelper.TryProbeConnectionStringAsync("analytics", analyticsConnection, ct, logger, correlationId);
        var ok = defaultDb.Ok && analyticsDb.Ok;

        var readinessState = app.Services.GetRequiredService<StartupReadinessState>();
        readinessState.ReportProbe(
            new StartupReadinessState.DatabaseProbeState
            {
                Ok = defaultDb.Ok,
                LatencyMs = defaultDb.ElapsedMs,
                Error = defaultDb.Error
            },
            new StartupReadinessState.DatabaseProbeState
            {
                Ok = analyticsDb.Ok,
                LatencyMs = analyticsDb.ElapsedMs,
                Error = analyticsDb.Error
            });

        if (ok)
        {
            readinessState.MarkReady();
        }

        var payload = new
        {
            status = ok ? "healthy" : "unhealthy",
            provider = ResolveProviderName(context),
            ready = ok,
            db = new
            {
                ok,
                latencyMs = ResolveProbeLatency(defaultDb.ElapsedMs, analyticsDb.ElapsedMs)
            },
            timestampUtc = DateTimeOffset.UtcNow,
            retryAfterSeconds = ok ? (int?)null : 5,
            checks = new
            {
                defaultDb = new { ok = defaultDb.Ok, elapsedMs = defaultDb.ElapsedMs, error = defaultDb.Error },
                analyticsDb = new { ok = analyticsDb.Ok, elapsedMs = analyticsDb.ElapsedMs, error = analyticsDb.Error }
            }
        };

        if (!ok)
        {
            context.Response.Headers.RetryAfter = "5";
            return Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(payload);
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
            Console.WriteLine("Auto-migrate enabled - applying EF Core migrations for main DB...");
            db.Database.Migrate();
            Console.WriteLine("Main database migrations applied successfully.");

            // Also apply analytics DB migrations when available to ensure analytics tables exist
            try
            {
                var analyticsDb = scope.ServiceProvider.GetService<Infrastructure.DbContexts.AnalyticsDbContext>();
                if (analyticsDb is not null)
                {
                    Console.WriteLine("Applying EF Core migrations for AnalyticsDb...");
                    analyticsDb.Database.Migrate();
                    Console.WriteLine("Analytics database migrations applied successfully.");
                }
            }
            catch (Exception ex)
            {
                var logger = scope.ServiceProvider.GetService<Microsoft.Extensions.Logging.ILogger<Program>>();
                logger?.LogWarning(ex, "Applying analytics DB migrations failed (continuing)");
            }
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

    // HSTS for Production/Staging behind TLS-terminating proxy (not Development HTTP).
    // Must run after ForwardedHeaders so X-Forwarded-Proto: https is visible as Request.IsHttps.
    if (ProductionEdgePolicy.ShouldUseHsts(app.Environment))
    {
        app.UseHsts();
    }

    app.Use(async (context, next) =>
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/health") || path.StartsWithSegments("/ready"))
        {
            HealthCorsHeaders.Apply(context, allowedHealthOrigins);

            if (HttpMethods.IsOptions(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }
        }

        await next(context);
    });

    // Serve the SPA shell and static assets even while backend dependencies are warming up.
    // API traffic is gated below, but the frontend must be able to render the wake-up state.
    app.UseDefaultFiles();
    app.UseStaticFiles();

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

        var shouldGateRequest =
            path.StartsWithSegments("/api") ||
            path.StartsWithSegments("/artikli") ||
            path.StartsWithSegments("/scrapers") ||
            path.StartsWithSegments("/admin/repair");
        var gateApiTrafficDuringWarmup =
            builder.Configuration.GetValue<bool?>("StartupReadiness:GateApiTraffic") ??
            !app.Environment.IsDevelopment();

        var readiness = context.RequestServices.GetRequiredService<StartupReadinessState>();
        if (gateApiTrafficDuringWarmup && shouldGateRequest && !readiness.IsReady)
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

    // 3. Routing
    app.UseRouting();
    app.UseCors("AllowFrontend");
    app.UseRateLimiter();

    // ===== SWAGGER UI =====
    // Secure default: Development only unless Swagger:Enabled is set explicitly.
    var swaggerEnabled = SwaggerExposurePolicy.IsEnabled(builder.Configuration, app.Environment);
    if (swaggerEnabled)
    {
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
    }
    else
    {
        app.Logger.LogInformation("Swagger UI disabled for environment {EnvironmentName}", app.Environment.EnvironmentName);
    }

    app.UseAuthorization();

    // Removed duplicate minimal API proxy for /api/release/{gender} because Api.Controllers.ReleaseController already defines this endpoint.
    // Use the existing ReleaseController implementation instead.

    // ================= ENDPOINTS =================

    // Map controllers and other minimal endpoints
    app.MapGet("/health", CheckLiveness).AllowAnonymous();
    app.MapGet("/health/dependencies", CheckDatabaseHealthAsync).AllowAnonymous();
    app.MapGet("/ready", (StartupReadinessState readiness, HttpContext context) =>
    {
        var isReady = readiness.IsReady;
        var status = isReady ? "healthy" : (readiness.Reason.Contains("warmup", StringComparison.OrdinalIgnoreCase) || readiness.Reason.Contains("starting", StringComparison.OrdinalIgnoreCase) ? "warming_up" : "degraded");
        var retryAfterSeconds = isReady ? (int?)null : 5;
        if (!isReady)
        {
            context.Response.Headers.RetryAfter = retryAfterSeconds!.Value.ToString(CultureInfo.InvariantCulture);
        }

        var payload = new
        {
            status,
            provider = ResolveProviderName(context),
            ready = isReady,
            db = new
            {
                ok = readiness.DefaultDb.Ok && readiness.AnalyticsDb.Ok,
                latencyMs = ResolveProbeLatency(readiness.DefaultDb.LatencyMs, readiness.AnalyticsDb.LatencyMs)
            },
            timestampUtc = DateTimeOffset.UtcNow,
            retryAfterSeconds,
            reason = readiness.Reason,
            startedAtUtc = readiness.StartedAtUtc,
            readyAtUtc = readiness.ReadyAtUtc,
            lastProbeAtUtc = readiness.LastProbeAtUtc
        };

        return isReady
            ? Results.Ok(payload)
            : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
    }).AllowAnonymous();
    app.MapGet("/api/runtime/version", (HttpContext context) =>
    {
        var payload = new
        {
            service = "trendplus-api",
            environment = builder.Environment.EnvironmentName,
            commitSha = ResolveRuntimeCommitSha(),
            buildTimeUtc = ResolveRuntimeBuildTimeUtc(),
            processType = processType.ToString().ToLowerInvariant(),
            provider = ResolveRuntimeProviderName(context)
        };

        return Results.Ok(payload);
    }).AllowAnonymous();

    app.MapControllers();
    // Map all other endpoints from AllEndpoints.cs
    app.MapAllEndpoints();
    app.MapAnalyticsRefreshStatusEndpoints();
    app.MapCachedAnalyticsEndpoints();
    app.MapDecisionBoardEndpoints();
    app.MapDecisionPulseEndpoints();
    app.MapInventoryEndpoints();
    app.MapAnalyticsActionsEndpoints();
    app.MapAnalyticsIntelligenceEndpoints();
    app.MapInsightStudioEndpoints();
    app.MapInsightStudioV2Endpoints();
    app.MapPreNivelacijaPriorityEndpoints();
    app.MapAnalyticsReportsEndpoints();
    app.MapSupplierDecisionHubEndpoints();
    app.MapScoringEndpoints();
    app.MapOpenProductTrainingEndpoints();
    app.MapShopifyEndpoints();
    app.MapAccessImportEndpoints();
    app.MapDataSourceDiscoveryEndpoints();
    app.MapAdminRepairEndpoints();
    app.MapAdminConfigEndpoints();
    app.MapAdminBackendRoutingEndpoints();
    app.MapRedisEndpoints();
    app.MapOutboxEndpoints();
    app.MapWorkerConfigurationEndpoints();
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

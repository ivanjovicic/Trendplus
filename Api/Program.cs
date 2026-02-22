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
using Infrastructure.Services.Caching;
using Infrastructure.Seed;
using MediatR;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
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
using Microsoft.AspNetCore.ResponseCompression;
using System.Threading.RateLimiting;

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

    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

    Console.WriteLine($"Configured to listen on port {port}");

    // DbContext
    builder.Services.AddDbContext<TrendplusDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), 
            o => o.UseVector()) // ← Enable pgvector support
               .EnableSensitiveDataLogging(builder.Environment.IsDevelopment()));

    builder.Services.AddScoped<ITrendplusDbContext>(sp =>
        sp.GetRequiredService<TrendplusDbContext>());

    builder.Services.AddDbContext<AnalyticsDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("AnalyticsConnection"))
               .EnableSensitiveDataLogging(builder.Environment.IsDevelopment()));

    builder.Services.AddScoped<IAnalyticsDbContext>(sp =>
        sp.GetRequiredService<AnalyticsDbContext>());

    Console.WriteLine("DbContext registered");

    // FluentValidation - auto-register all validators
    builder.Services.AddValidatorsFromAssemblyContaining<CreateArtikalCommandValidator>();

    // Memory Cache - required by GetArtikliQueryHandler and other caching services
    builder.Services.AddMemoryCache();

    // Response compression
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
        options.Providers.Add<BrotliCompressionProvider>();
        options.Providers.Add<GzipCompressionProvider>();
    });

    // MediatR Pipeline Behaviors (order matters!)
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceLoggingBehavior<,>));

    // Services
    builder.Services.AddScoped<IErrorStore, DbErrorStore>();
    builder.Services.AddScoped<IProdajaRepository, ProdajaRepository>();
    builder.Services.AddScoped<IOutboxService, OutboxService>();
    builder.Services.AddSingleton<WorkerHealthService>(); // Worker health monitoring
    
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
    });

    // Image providers for trends carousel are optional and resolved dynamically in endpoints

    // Background Workers (can be disabled in production via Workers:Enabled=false)
    var workersEnabled = builder.Configuration.GetValue<bool>("Workers:Enabled", true);
    if (workersEnabled)
    {
        builder.Services.AddHostedService<Workers.SyncWorker>();
        builder.Services.AddHostedService<Workers.OutboxProcessorWorker>();
        builder.Services.AddHostedService<Workers.AnalyticsAggregationWorker>(); // NEW: Pre-aggregate analytics
        Console.WriteLine("Background workers: ENABLED");
    }
    else
    {
        Console.WriteLine("Background workers: DISABLED");
    }

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

    // add Redis cache
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
        options.Configuration = $"{redisConnection},abortConnect=false,connectTimeout=500,syncTimeout=500,asyncTimeout=500,connectRetry=1";
        options.InstanceName = "trendplus:";
    });
    builder.Services.AddSingleton<IAnalyticsCacheService, HybridCacheService>();

    // Register Api.Services.CommonMatchesClient (implementation in Api project)
    builder.Services.AddScoped<ICommonMatchesClient, CommonMatchesClient>();
    builder.Services.AddScoped<IScraperScoringQueryService, ScraperScoringQueryService>();

    // Amazon Shoes via SerpAPI
    builder.Services.Configure<Api.Config.SerpApiOptions>(
        builder.Configuration.GetSection(Api.Config.SerpApiOptions.Section));
    builder.Services.AddHttpClient<Api.Services.AmazonShoesService>(client =>
    {
        client.BaseAddress = new Uri("https://serpapi.com/");
        client.Timeout = TimeSpan.FromSeconds(
            builder.Configuration.GetValue<int>("SerpApi:TimeoutSeconds", 20));
    });

    // eBay Shoes via Browse API
    builder.Services.Configure<Api.Config.EbayOptions>(
        builder.Configuration.GetSection(Api.Config.EbayOptions.Section));
    builder.Services.AddHttpClient<Api.Services.EbayBrowseService>(client =>
    {
        client.BaseAddress = new Uri("https://api.ebay.com/");
        client.Timeout = TimeSpan.FromSeconds(
            builder.Configuration.GetValue<int>("Ebay:TimeoutSeconds", 20));
    });

    // API v1 rate limiter
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("api-v1", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "global",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 120,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
    });

    var app = builder.Build();

    // ================= DATABASE INITIALIZATION =================
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<Program>>();
        var configuration = services.GetRequiredService<IConfiguration>();

        try
        {
            await DatabaseInitializer.InitializeDatabasesAsync(services, configuration, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while initializing databases");
            // Don't throw - allow app to start even if seeding fails
        }
    }

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

    // 2.1 Response compression
    app.UseResponseCompression();

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
    app.MapScoringEndpoints();
    
    Console.WriteLine("All endpoints mapped");
    Console.WriteLine($"Swagger UI available at: http://localhost:{port}/swagger");
    Console.WriteLine($"Starting web host on port {port}...");
    
    app.Run();
    
    Console.WriteLine("Application stopped gracefully");
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
    
    Console.WriteLine("\nPress any key to exit...");
    Console.ReadKey();
    
    Environment.Exit(1);
}
finally
{
    Log.CloseAndFlush();
}

// Helper function
static decimal CalculatePercentChange(decimal oldValue, decimal newValue)
{
    if (oldValue == 0) return newValue > 0 ? 100 : 0;
    return ((newValue - oldValue) / oldValue) * 100;
}

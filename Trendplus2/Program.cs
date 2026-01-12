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
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
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

    // MediatR Pipeline Behaviors (order matters!)
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceLoggingBehavior<,>));

    // Services
    builder.Services.AddScoped<IErrorStore, DbErrorStore>();
    builder.Services.AddScoped<IProdajaRepository, ProdajaRepository>();
    builder.Services.AddScoped<IOutboxService, OutboxService>();
    builder.Services.AddSingleton<WorkerHealthService>(); // Worker health monitoring

    // RabbitMQ
    builder.Services.Configure<Infrastructure.Configuration.RabbitMqSettings>(
        builder.Configuration.GetSection("RabbitMq"));
    builder.Services.AddSingleton<IMessageBroker, RabbitMqMessageBroker>();

    // Background Workers
    builder.Services.AddHostedService<Workers.SyncWorker>();
    builder.Services.AddHostedService<Workers.OutboxProcessorWorker>();
    builder.Services.AddHostedService<Workers.AnalyticsAggregationWorker>(); // NEW: Pre-aggregate analytics

    builder.Services.AddControllers();
    builder.Services.ConfigureHttpJsonOptions(opts =>
    {
        opts.SerializerOptions.PropertyNameCaseInsensitive = true;
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
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

    // 3. Static files & routing
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseCors("AllowFrontend");

    if (app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseAuthorization();

    // ================= ENDPOINTS =================

    // Circuit Breaker Status
    app.MapCircuitBreakerEndpoints();

    // Health
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
    });

    // Worker Health Status
    app.MapGet("/api/workers/health", (WorkerHealthService workerHealth) =>
    {
        var summary = workerHealth.GetHealthSummary();
        return Results.Ok(summary);
    });

    // Errors
    app.MapGet("/errors", async (IErrorStore store) =>
    {
        var errors = await store.GetAllAsync();
        return Results.Ok(errors);
    });

    // Logs
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
            logger.LogsFetchFailed(ex);
            return Results.Problem(
                detail: "Unable to fetch logs. Please run migrations: dotnet ef database update",
                statusCode: 500,
                title: "Database Error"
            );
        }
    });

    // Performance
    app.MapGet("/api/performance", async (
        IMediator mediator,
        ILogger<Program> logger,
        int topCount = 20,
        int minDurationMs = 1000,
        DateTime? fromDate = null,
        DateTime? toDate = null) =>
    {
        logger.PerformanceRequest(topCount, minDurationMs);

        // Convert dates to UTC if they have Unspecified kind
        if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
            fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

        if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
            toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

        var query = new GetPerformanceStatsQuery(topCount, minDurationMs, fromDate, toDate);
        var result = await mediator.Send(query);
        return Results.Ok(result);
    });

    // Outbox Stats
    app.MapGet("/api/outbox/stats", async (ITrendplusDbContext db) =>
    {
        var total = await db.OutboxMessages.CountAsync();
        var processed = await db.OutboxMessages.CountAsync(m => m.IsProcessed);
        var pending = await db.OutboxMessages.CountAsync(m => !m.IsProcessed && m.RetryCount < 5);
        var failed = await db.OutboxMessages.CountAsync(m => !m.IsProcessed && m.RetryCount >= 5);
        
        var recentMessages = await db.OutboxMessages
            .OrderByDescending(m => m.CreatedAt)
            .Take(10)
            .Select(m => new
            {
                m.Id,
                m.EventType,
                m.CreatedAt,
                m.ProcessedAt,
                m.IsProcessed,
                m.RetryCount,
                m.ErrorMessage,
                m.CorrelationId
            })
            .ToListAsync();

        return Results.Ok(new
        {
            stats = new
            {
                total,
                processed,
                pending,
                failed,
                successRate = total > 0 ? (double)processed / total * 100 : 0
            },
            recentMessages
        });
    });

    // Outbox Messages
    app.MapGet("/api/outbox/messages", async (
        ITrendplusDbContext db,
        int pageNumber = 1,
        int pageSize = 50,
        bool? isProcessed = null,
        string? eventType = null,
        DateTime? fromDate = null,
        DateTime? toDate = null) =>
    {
        var query = db.OutboxMessages.AsQueryable();

        if (isProcessed.HasValue)
            query = query.Where(m => m.IsProcessed == isProcessed.Value);

        if (!string.IsNullOrWhiteSpace(eventType))
            query = query.Where(m => m.EventType.Contains(eventType));

        if (fromDate.HasValue)
            query = query.Where(m => m.CreatedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(m => m.CreatedAt <= toDate.Value);

        var total = await query.CountAsync();

        var messages = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                m.Id,
                m.EventType,
                m.Payload,
                m.CreatedAt,
                m.ProcessedAt,
                m.IsProcessed,
                m.RetryCount,
                m.ErrorMessage,
                m.CorrelationId
            })
            .ToListAsync();

        return Results.Ok(new
        {
            messages,
            totalCount = total,
            pageNumber,
            pageSize
        });
    });

    // Outbox Retry
    app.MapPost("/api/outbox/retry/{id:long}", async (
        long id,
        ITrendplusDbContext db,
        ILogger<Program> logger) =>
    {
        var message = await db.OutboxMessages.FindAsync(id);
        if (message == null)
            return Results.NotFound();

        message.RetryCount = 0;
        message.ErrorMessage = null;
        await db.SaveChangesAsync();

        logger.OutboxRetry(id);
        return Results.Ok(new { success = true });
    });

    // Bulk Retry Failed
    app.MapPost("/api/outbox/retry-all-failed", async (
        ITrendplusDbContext db,
        ILogger<Program> logger) =>
    {
        var failedMessages = await db.OutboxMessages
            .Where(m => !m.IsProcessed && m.RetryCount >= 5)
            .ToListAsync();

        foreach (var message in failedMessages)
        {
            message.RetryCount = 0;
            message.ErrorMessage = null;
        }

        await db.SaveChangesAsync();

        logger.BulkRetry(failedMessages.Count);
        return Results.Ok(new { success = true, count = failedMessages.Count });
    });

    // Purge Processed
    app.MapPost("/api/outbox/purge-processed", async (
        ITrendplusDbContext db,
        ILogger<Program> logger,
        int olderThanDays = 7) =>
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
        
        var messagesToDelete = await db.OutboxMessages
            .Where(m => m.IsProcessed && m.ProcessedAt < cutoffDate)
            .ToListAsync();

        db.OutboxMessages.RemoveRange(messagesToDelete);
        await db.SaveChangesAsync();

        logger.PurgeProcessed(messagesToDelete.Count, olderThanDays);
        return Results.Ok(new { success = true, count = messagesToDelete.Count });
    });

    // Event Type Stats
    app.MapGet("/api/outbox/stats-by-type", async (ITrendplusDbContext db) =>
    {
        var stats = await db.OutboxMessages
            .GroupBy(m => m.EventType)
            .Select(g => new
            {
                eventType = g.Key,
                total = g.Count(),
                processed = g.Count(m => m.IsProcessed),
                pending = g.Count(m => !m.IsProcessed && m.RetryCount < 5),
                failed = g.Count(m => !m.IsProcessed && m.RetryCount >= 5)
            })
            .OrderByDescending(s => s.total)
            .ToListAsync();

        return Results.Ok(stats);
    });

    // Dnevnik Promena - Get distinct tip promene values
    app.MapGet("/api/dnevnik-promena/tipovi", async (ITrendplusDbContext db, CancellationToken ct) =>
    {
        var tipovi = await db.DnevnikPromena
            .Select(x => x.TipPromene)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(ct);

        return Results.Ok(tipovi);
    });

    // ============ povraćaj ROBE ============

    // Kreiranje povraćaja
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
                message = $"Zapisnik o povraćaju {response.BrojZapisnika} uspešno kreiran"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating return note");
            return Results.Problem(
                detail: ex.Message,
                statusCode: 500,
                title: "Greška pri kreiranju povraćaja"
            );
        }
    });

    // Pregled povraćaja
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
            // Convert dates to UTC if they have Unspecified kind
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

            var query = from p in db.PovracajZaglavlja.AsNoTracking()
                        join d in db.Dobavljaci.AsNoTracking() on p.IDDobavljac equals d.Id
                        select new { p, d };

            // Filters
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

            return Results.Ok(new
            {
                items,
                totalCount = total,
                pageNumber,
                pageSize
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: 500,
                title: "Greška pri učitavanju povraćaja"
            );
        }
    });

    // Detalji povraćaja sa stavkama
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
                return Results.NotFound(new { message = "Povraćaj nije pronađen" });

            var dobavljac = await db.Dobavljaci.FindAsync(new object[] { povracaj.IDDobavljac }, ct);

            // Učitaj artikle za stavke
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
            return Results.Problem(
                detail: ex.Message,
                statusCode: 500,
                title: "Greška pri učitavanju detalja povraćaja"
            );
        }
    });

    // ============ Analytics (read model) ============

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
            return Results.Problem(
                detail: $"{ex.GetType().Name}: {ex.Message}",
                statusCode: 500,
                title: "Analytics database error"
            );
        }
    });

    app.MapGet("/api/analytics/sales/summary", async (
        IMediator mediator,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? storeId = null) =>
    {
        if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
            fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

        if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
            toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

        var result = await mediator.Send(new GetSalesSummaryQuery(fromDate, toDate, storeId));
        return Results.Ok(result);
    });

    // NEW: Daily sales for chart
    app.MapGet("/api/analytics/sales/daily", async (
        IAnalyticsDbContext db,
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

            var query = db.SalesFacts.AsNoTracking();

            if (fromDate.HasValue)
                query = query.Where(s => s.SaleTimestampUtc >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(s => s.SaleTimestampUtc <= toDate.Value);

            var dailySales = await query
                .GroupBy(s => s.SaleTimestampUtc.Date)
                .Select(g => new
                {
                    date = g.Key,
                    totalRevenue = g.Sum(s => s.TotalAmount),
                    transactionCount = g.Count(),
                    totalUnits = g.Sum(s => s.TotalUnits)
                })
                .OrderBy(x => x.date)
                .ToListAsync(ct);

            var result = dailySales.Select(x => new
            {
                date = x.date.ToString("yyyy-MM-dd"),
                x.totalRevenue,
                x.transactionCount,
                x.totalUnits
            }).ToList();

            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri učitavanju dnevne prodaje");
        }
    });

    // NEW: Comparison with previous period
    app.MapGet("/api/analytics/sales/comparison", async (
        IAnalyticsDbContext db,
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

            // Current period
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

            // Previous period
            if (fromDate.HasValue && toDate.HasValue)
            {
                var duration = (toDate.Value - fromDate.Value).TotalDays;
                var prevFrom = fromDate.Value.AddDays(-duration);
                var prevTo = fromDate.Value;

                var prevQuery = db.SalesFacts.AsNoTracking()
                    .Where(s => s.SaleTimestampUtc >= prevFrom && s.SaleTimestampUtc < prevTo);

                var previous = await prevQuery
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
                    return Results.Ok(new
                    {
                        current,
                        previous,
                        change = new
                        {
                            revenue = CalculatePercentChange(previous.totalRevenue, current.totalRevenue),
                            transactions = CalculatePercentChange(previous.totalTransactions, current.totalTransactions),
                            units = CalculatePercentChange(previous.totalUnits, current.totalUnits)
                        }
                    });
                }
            }

            return Results.Ok(new { current, previous = (object?)null, change = (object?)null });
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri poređenju perioda");
        }
    });

    app.MapGet("/api/analytics/sales/top-products", async (
        IMediator mediator,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int top = 20,
        int? storeId = null) =>
    {
        if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
            fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

        if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
            toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

        var result = await mediator.Send(new GetTopProductsQuery(fromDate, toDate, top, storeId));
        return Results.Ok(result);
    });

    app.MapGet("/api/analytics/inventory/status", async (
        IMediator mediator,
        int lowStockThreshold = 2) =>
    {
        var result = await mediator.Send(new GetInventoryStatusQuery(lowStockThreshold));
        return Results.Ok(result);
    });

    // NEW: Sales by Category (Pie Chart Data)
    app.MapGet("/api/analytics/sales/by-category", async (
        ITrendplusDbContext db,
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

            var query = from ps in db.ProdajaStavke
                        join p in db.ProdajaZaglavlja on ps.IdProdaja equals p.Id
                        join a in db.Artikli on ps.IdArtikal equals a.Id
                        where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                              (!toDate.HasValue || p.DatumProdaje <= toDate.Value)
                        group ps by new { a.Kategorija, a.Pol } into g
                        select new
                        {
                            kategorija = g.Key.Kategorija ?? "Ostalo",
                            pol = g.Key.Pol ?? "Neodređeno",
                            totalRevenue = g.Sum(x => x.Kolicina * x.Cena),
                            totalUnits = g.Sum(x => x.Kolicina),
                            transactionCount = g.Select(x => x.IdProdaja).Distinct().Count()
                        };

            var result = await query.OrderByDescending(x => x.totalRevenue).ToListAsync(ct);

            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri učitavanju prodaje po kategorijama");
        }
    });

    // NEW: Sales by Gender (Pol)
    app.MapGet("/api/analytics/sales/by-gender", async (
        ITrendplusDbContext db,
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

            var query = from ps in db.ProdajaStavke
                        join p in db.ProdajaZaglavlja on ps.IdProdaja equals p.Id
                        join a in db.Artikli on ps.IdArtikal equals a.Id
                        where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                              (!toDate.HasValue || p.DatumProdaje <= toDate.Value)
                        group ps by a.Pol into g
                        select new
                        {
                            pol = g.Key ?? "Neodređeno",
                            totalRevenue = g.Sum(x => x.Kolicina * x.Cena),
                            totalUnits = g.Sum(x => x.Kolicina)
                        };

            var result = await query.OrderByDescending(x => x.totalRevenue).ToListAsync(ct);

            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri učitavanju prodaje po polu");
        }
    });

    // NEW: Sales by Supplier
    app.MapGet("/api/analytics/sales/by-supplier", async (
        ITrendplusDbContext db,
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

            // Get sales with supplier info through articles
            var query = from ps in db.ProdajaStavke
                        join p in db.ProdajaZaglavlja on ps.IdProdaja equals p.Id
                        join a in db.Artikli on ps.IdArtikal equals a.Id
                        join d in db.Dobavljaci on a.IDDobavljac equals d.Id into dobavljacJoin
                        from d in dobavljacJoin.DefaultIfEmpty()
                        where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                              (!toDate.HasValue || p.DatumProdaje <= toDate.Value)
                        group ps by new { DobavljacId = d != null ? d.Id : (int?)null, DobavljacNaziv = d != null ? d.Naziv : "Nepoznato" } into g
                        select new
                        {
                            dobavljacId = g.Key.DobavljacId,
                            dobavljacNaziv = g.Key.DobavljacNaziv,
                            totalRevenue = g.Sum(x => x.Kolicina * x.Cena),
                            totalUnits = g.Sum(x => x.Kolicina),
                            transactionCount = g.Select(x => x.IdProdaja).Distinct().Count()
                        };

            var result = await query.OrderByDescending(x => x.totalRevenue).ToListAsync(ct);

            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri učitavanju prodaje po dobavljačima");
        }
    });

    // NEW: Transaction stats
    app.MapGet("/api/analytics/sales/transaction-stats", async (
        ITrendplusDbContext db,
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

            var query = db.ProdajaZaglavlja.AsNoTracking().AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(p => p.DatumProdaje >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(p => p.DatumProdaje <= toDate.Value);

            var prodajeIds = await query.Select(p => p.Id).ToListAsync(ct);

            if (prodajeIds.Count == 0)
            {
                return Results.Ok(new
                {
                    avgItemsPerTransaction = 0.0,
                    avgTransactionValue = 0.0m,
                    totalTransactions = 0
                });
            }

            var stavke = await db.ProdajaStavke
                .Where(ps => prodajeIds.Contains(ps.IdProdaja))
                .GroupBy(ps => ps.IdProdaja)
                .Select(g => new
                {
                    IdProdaja = g.Key,
                    ItemCount = g.Count(),
                    TotalValue = g.Sum(x => x.Kolicina * x.Cena)
                })
                .ToListAsync(ct);

            var avgItems = stavke.Any() ? stavke.Average(x => x.ItemCount) : 0.0;
            var avgValue = stavke.Any() ? stavke.Average(x => x.TotalValue) : 0.0m;

            return Results.Ok(new
            {
                avgItemsPerTransaction = avgItems,
                avgTransactionValue = avgValue,
                totalTransactions = prodajeIds.Count
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri učitavanju statistike transakcija");
        }
    });

    // NEW: Sales by payment method
    app.MapGet("/api/analytics/sales/by-payment", async (
        ITrendplusDbContext db,
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

            var query = from p in db.ProdajaZaglavlja
                        where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                              (!toDate.HasValue || p.DatumProdaje <= toDate.Value)
                        group p by p.NacinPlacanja into g
                        select new
                        {
                            nacinPlacanja = g.Key ?? "Nepoznato",
                            transactionCount = g.Count()
                        };

            var prodajeByPayment = await query.ToListAsync(ct);

            var result = new List<object>();
            foreach (var item in prodajeByPayment)
            {
                var prodajeIds = await db.ProdajaZaglavlja
                    .Where(p => p.NacinPlacanja == (item.nacinPlacanja == "Nepoznato" ? null : item.nacinPlacanja))
                    .Where(p => (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                                (!toDate.HasValue || p.DatumProdaje <= toDate.Value))
                    .Select(p => p.Id)
                    .ToListAsync(ct);

                var totalRevenue = await db.ProdajaStavke
                    .Where(ps => prodajeIds.Contains(ps.IdProdaja))
                    .SumAsync(ps => ps.Kolicina * ps.Cena, ct);

                result.Add(new
                {
                    item.nacinPlacanja,
                    totalRevenue,
                    item.transactionCount
                });
            }

            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri učitavanju prodaje po načinu plaćanja");
        }
    });

    // NEW: Sales by weekday
    app.MapGet("/api/analytics/sales/by-weekday", async (
        ITrendplusDbContext db,
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

            var query = db.ProdajaZaglavlja.AsNoTracking().AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(p => p.DatumProdaje >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(p => p.DatumProdaje <= toDate.Value);

            var prodaje = await query.ToListAsync(ct);

            var dayNames = new[] { "Nedelja", "Ponedeljak", "Utorak", "Sreda", "Četvrtak", "Petak", "Subota" };

            var grouped = prodaje
                .GroupBy(p => p.DatumProdaje.DayOfWeek)
                .Select(g => new
                {
                    dayOfWeek = ((int)g.Key).ToString(),
                    dayName = dayNames[(int)g.Key],
                    transactionCount = g.Count(),
                    prodajeIds = g.Select(p => p.Id).ToList()
                })
                .ToList();

            var result = new List<object>();
            foreach (var day in grouped)
            {
                var totalRevenue = await db.ProdajaStavke
                    .Where(ps => day.prodajeIds.Contains(ps.IdProdaja))
                    .SumAsync(ps => ps.Kolicina * ps.Cena, ct);

                result.Add(new
                {
                    day.dayOfWeek,
                    day.dayName,
                    totalRevenue,
                    day.transactionCount
                });
            }

            return Results.Ok(result.OrderBy(x => int.Parse(((dynamic)x).dayOfWeek)));
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri učitavanju prodaje po danima");
        }
    });

    // NEW: Sales by hour
    app.MapGet("/api/analytics/sales/by-hour", async (
        ITrendplusDbContext db,
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

            var query = db.ProdajaZaglavlja.AsNoTracking().AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(p => p.DatumProdaje >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(p => p.DatumProdaje <= toDate.Value);

            var prodaje = await query.ToListAsync(ct);

            var grouped = prodaje
                .GroupBy(p => p.DatumProdaje.Hour)
                .Select(g => new
                {
                    hour = g.Key,
                    transactionCount = g.Count(),
                    prodajeIds = g.Select(p => p.Id).ToList()
                })
                .ToList();

            var result = new List<object>();
            foreach (var hour in grouped)
            {
                var totalRevenue = await db.ProdajaStavke
                    .Where(ps => hour.prodajeIds.Contains(ps.IdProdaja))
                    .SumAsync(ps => ps.Kolicina * ps.Cena, ct);

                result.Add(new
                {
                    hour.hour,
                    totalRevenue,
                    hour.transactionCount
                });
            }

            return Results.Ok(result.OrderBy(x => ((dynamic)x).hour));
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri učitavanju prodaje po satima");
        }
    });

    // NEW: Reorder suggestions
    app.MapGet("/api/analytics/reorder-suggestions", async (
        ITrendplusDbContext db,
        CancellationToken ct = default) =>
    {
        try
        {
            var artikli = await db.Artikli
                .Where(a => a.Kolicina <= a.MinimalnaKolicina || a.Kolicina == 0)
                .OrderBy(a => a.Kolicina)
                .Select(a => new
                {
                    a.Id,
                    a.Naziv,
                    a.Kolicina,
                    a.MinimalnaKolicina,
                    a.Kategorija,
                    a.NabavnaCena
                })
                .ToListAsync(ct);

            return Results.Ok(artikli);
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri učitavanju preporuka za naručivanje");
        }
    });

    // NEW: Quick Insights
    app.MapGet("/api/analytics/quick-insights", async (
        ITrendplusDbContext db,
        IAnalyticsDbContext analyticsDb,
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

            var prodajeQuery = db.ProdajaZaglavlja.AsNoTracking().AsQueryable();
            
            if (fromDate.HasValue)
                prodajeQuery = prodajeQuery.Where(p => p.DatumProdaje >= fromDate.Value);
            
            if (toDate.HasValue)
                prodajeQuery = prodajeQuery.Where(p => p.DatumProdaje <= toDate.Value);

            var prodaje = await prodajeQuery.ToListAsync(ct);

            // Best day
            var bestDay = prodaje
                .GroupBy(p => p.DatumProdaje.DayOfWeek)
                .Select(g => new
                {
                    dayOfWeek = g.Key,
                    dayName = new[] { "Nedelja", "Ponedeljak", "Utorak", "Sreda", "Četvrtak", "Petak", "Subota" }[(int)g.Key],
                    count = g.Count(),
                    prodajeIds = g.Select(p => p.Id).ToList()
                })
                .ToList();

            string? bestDayName = null;
            decimal bestDayRevenue = 0;

            foreach (var day in bestDay)
            {
                var revenue = await db.ProdajaStavke
                    .Where(ps => day.prodajeIds.Contains(ps.IdProdaja))
                    .SumAsync(ps => ps.Kolicina * ps.Cena, ct);
                
                if (revenue > bestDayRevenue)
                {
                    bestDayRevenue = revenue;
                    bestDayName = day.dayName;
                }
            }

            // Top growing product
            var stavke = await db.ProdajaStavke
                .Where(ps => prodaje.Select(p => p.Id).Contains(ps.IdProdaja))
                .GroupBy(ps => ps.IdArtikal)
                .Select(g => new
                {
                    artikalId = g.Key,
                    totalRevenue = g.Sum(x => x.Kolicina * x.Cena)
                })
                .OrderByDescending(x => x.totalRevenue)
                .Take(1)
                .ToListAsync(ct);

            string? topProductName = null;
            if (stavke.Any())
            {
                var artikal = await db.Artikli.FindAsync(new object[] { stavke[0].artikalId }, ct);
                topProductName = artikal?.Naziv;
            }

            // Low stock count
            var lowStockCount = await db.Artikli
                .Where(a => a.Kolicina <= a.MinimalnaKolicina || a.Kolicina == 0)
                .CountAsync(ct);

            return Results.Ok(new
            {
                bestDay = bestDayName,
                bestDayRevenue,
                topProduct = topProductName,
                lowStockAlert = lowStockCount
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri učitavanju brzih uvida");
        }
    });

    // NEW: Alerts
    app.MapGet("/api/analytics/alerts", async (
        ITrendplusDbContext db,
        CancellationToken ct = default) =>
    {
        try
        {
            var alerts = new List<object>();

            // Out of stock
            var outOfStock = await db.Artikli
                .Where(a => a.Kolicina == 0)
                .Select(a => new { a.Id, a.Naziv })
                .ToListAsync(ct);

            if (outOfStock.Any())
            {
                alerts.Add(new
                {
                    type = "error",
                    icon = "🔴",
                    title = $"{outOfStock.Count} proizvoda bez zaliha",
                    message = string.Join(", ", outOfStock.Take(3).Select(a => a.Naziv)) + (outOfStock.Count > 3 ? "..." : ""),
                    count = outOfStock.Count
                });
            }

            // Low stock
            var lowStock = await db.Artikli
                .Where(a => a.Kolicina > 0 && a.Kolicina <= a.MinimalnaKolicina)
                .Select(a => new { a.Id, a.Naziv, a.Kolicina })
                .ToListAsync(ct);

            if (lowStock.Any())
            {
                alerts.Add(new
                {
                    type = "warning",
                    icon = "🟡",
                    title = $"{lowStock.Count} proizvoda ispod minimalne količine",
                    message = string.Join(", ", lowStock.Take(3).Select(a => $"{a.Naziv} ({a.Kolicina})")) + (lowStock.Count > 3 ? "..." : ""),
                    count = lowStock.Count
                });
            }

            return Results.Ok(alerts);
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri učitavanju obaveštenja");
        }
    });

    // NEW: Export Analytics (CSV format for simplicity)
    app.MapGet("/api/analytics/export", async (
        ITrendplusDbContext db,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string format = "csv",
        CancellationToken ct = default) =>
    {
        try
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

            var prodajeQuery = db.ProdajaZaglavlja.AsNoTracking().AsQueryable();
            
            if (fromDate.HasValue)
                prodajeQuery = prodajeQuery.Where(p => p.DatumProdaje >= fromDate.Value);
            
            if (toDate.HasValue)
                prodajeQuery = prodajeQuery.Where(p => p.DatumProdaje <= toDate.Value);

            var prodaje = await prodajeQuery
                .OrderByDescending(p => p.DatumProdaje)
                .Take(1000) // Limit to prevent huge exports
                .ToListAsync(ct);

            var prodajeIds = prodaje.Select(p => p.Id).ToList();

            var stavkeGrouped = await db.ProdajaStavke
                .Where(ps => prodajeIds.Contains(ps.IdProdaja))
                .GroupBy(ps => ps.IdProdaja)
                .Select(g => new
                {
                    IdProdaja = g.Key,
                    TotalRevenue = g.Sum(x => x.Kolicina * x.Cena),
                    TotalItems = g.Sum(x => x.Kolicina)
                })
                .ToDictionaryAsync(x => x.IdProdaja, ct);

            // Generate CSV
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Datum,Broj Računa,Način Plaćanja,Ukupan Iznos,Broj Artikala");

            foreach (var prodaja in prodaje)
            {
                var revenue = stavkeGrouped.ContainsKey(prodaja.Id) ? stavkeGrouped[prodaja.Id].TotalRevenue : 0;
                var items = stavkeGrouped.ContainsKey(prodaja.Id) ? stavkeGrouped[prodaja.Id].TotalItems : 0;

                csv.AppendLine($"{prodaja.DatumProdaje:yyyy-MM-dd HH:mm},{prodaja.BrojRacuna},{prodaja.NacinPlacanja ?? "Nepoznato"},{revenue:F2},{items}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            var fileName = $"analytics_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

            return Results.File(bytes, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri izvozu podataka");
        }
    });

    // NEW: Category trends over time
    app.MapGet("/api/analytics/sales/category-trends", async (
        ITrendplusDbContext db,
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

            var prodajeQuery = db.ProdajaZaglavlja.AsNoTracking().AsQueryable();
            
            if (fromDate.HasValue)
                prodajeQuery = prodajeQuery.Where(p => p.DatumProdaje >= fromDate.Value);
            
            if (toDate.HasValue)
                prodajeQuery = prodajeQuery.Where(p => p.DatumProdaje <= toDate.Value);

            var prodaje = await prodajeQuery.ToListAsync(ct);
            var prodajeIds = prodaje.Select(p => p.Id).ToList();

            var stavke = await db.ProdajaStavke
                .Where(ps => prodajeIds.Contains(ps.IdProdaja))
                .ToListAsync(ct);

            var artikalIds = stavke.Select(s => s.IdArtikal).Distinct().ToList();
            var artikli = await db.Artikli
                .Where(a => artikalIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.Kategorija ?? "Ostalo", ct);

            // Group by date and category
            var grouped = prodaje
                .GroupBy(p => p.DatumProdaje.Date)
                .Select(dateGroup => new
                {
                    date = dateGroup.Key,
                    prodajeIds = dateGroup.Select(p => p.Id).ToList()
                })
                .OrderBy(x => x.date)
                .ToList();

            var result = new List<Dictionary<string, object>>();

            foreach (var dateEntry in grouped)
            {
                var dateStavke = stavke.Where(s => dateEntry.prodajeIds.Contains(s.IdProdaja)).ToList();
                
                var categoryRevenues = dateStavke
                    .GroupBy(s => artikli.ContainsKey(s.IdArtikal) ? artikli[s.IdArtikal] : "Ostalo")
                    .ToDictionary(
                        g => g.Key,
                        g => g.Sum(x => x.Kolicina * x.Cena)
                    );

                var row = new Dictionary<string, object>
                {
                    ["date"] = dateEntry.date.ToString("yyyy-MM-dd")
                };

                foreach (var cat in categoryRevenues)
                {
                    row[cat.Key] = cat.Value;
                }

                result.Add(row);
            }

            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri učitavanju trendova kategorija");
        }
    });

    // SEED DATA ENDPOINT - Only for development!
    app.MapPost("/api/seed-data", async (
        ITrendplusDbContext db,
        ILogger<Program> logger) =>
    {
        try
        {
            logger.LogInformation("Starting seed data generation...");

            // Check if data already exists
            var existingCount = await db.Artikli.CountAsync();
            if (existingCount > 10)
            {
                return Results.BadRequest(new { message = "Baza već ima podatke. Obrišite postojeće podatke pre seed-a." });
            }

            // Seed Dobavljaci
            var nike = new Domain.Model.Dobavljac { Naziv = "Nike", Telefon = "011/123-456", Adresa = "Beograd, Srbija" };
            var adidas = new Domain.Model.Dobavljac { Naziv = "Adidas", Telefon = "011/234-567", Adresa = "Novi Sad, Srbija" };
            var puma = new Domain.Model.Dobavljac { Naziv = "Puma", Telefon = "011/345-678", Adresa = "Niš, Srbija" };
            var reebok = new Domain.Model.Dobavljac { Naziv = "Reebok", Telefon = "011/456-789", Adresa = "Kragujevac, Srbija" };

            if (!await db.Dobavljaci.AnyAsync(d => d.Naziv == "Nike"))
            {
                db.Dobavljaci.AddRange(nike, adidas, puma, reebok);
                await db.SaveChangesAsync();
            }

            // Seed Artikli
            var artikli = new List<Domain.Model.Artikli>
            {
                new() { Naziv = "Nike Air Max 270", NabavnaCena = 8000, ProdajnaCena = 15000, Kolicina = 50, MinimalnaKolicina = 5, Kategorija = "Patike", Pol = "Muško", Boja = "Crna" },
                new() { Naziv = "Nike Air Force 1", NabavnaCena = 7000, ProdajnaCena = 13000, Kolicina = 45, MinimalnaKolicina = 5, Kategorija = "Patike", Pol = "Unisex", Boja = "Bela" },
                new() { Naziv = "Nike Pegasus 39", NabavnaCena = 9000, ProdajnaCena = 16500, Kolicina = 30, MinimalnaKolicina = 5, Kategorija = "Patike", Pol = "Muško", Boja = "Plava" },
                new() { Naziv = "Adidas Superstar", NabavnaCena = 6500, ProdajnaCena = 12000, Kolicina = 60, MinimalnaKolicina = 5, Kategorija = "Patike", Pol = "Unisex", Boja = "Bela" },
                new() { Naziv = "Adidas Ultraboost 22", NabavnaCena = 10000, ProdajnaCena = 18000, Kolicina = 25, MinimalnaKolicina = 5, Kategorija = "Patike", Pol = "Muško", Boja = "Crna" },
                new() { Naziv = "Adidas Stan Smith", NabavnaCena = 5500, ProdajnaCena = 10000, Kolicina = 70, MinimalnaKolicina = 5, Kategorija = "Patike", Pol = "Unisex", Boja = "Zelena" },
                new() { Naziv = "Puma RS-X", NabavnaCena = 7500, ProdajnaCena = 14000, Kolicina = 40, MinimalnaKolicina = 5, Kategorija = "Patike", Pol = "Žensko", Boja = "Roza" },
                new() { Naziv = "Puma Suede Classic", NabavnaCena = 5000, ProdajnaCena = 9000, Kolicina = 55, MinimalnaKolicina = 5, Kategorija = "Patike", Pol = "Unisex", Boja = "Crvena" },
                new() { Naziv = "Puma Cali Sport", NabavnaCena = 6000, ProdajnaCena = 11000, Kolicina = 35, MinimalnaKolicina = 5, Kategorija = "Patike", Pol = "Žensko", Boja = "Bela" },
                new() { Naziv = "Reebok Classic Leather", NabavnaCena = 5500, ProdajnaCena = 10500, Kolicina = 50, MinimalnaKolicina = 5, Kategorija = "Patike", Pol = "Unisex", Boja = "Crna" },
                new() { Naziv = "Reebok Club C 85", NabavnaCena = 6000, ProdajnaCena = 11500, Kolicina = 40, MinimalnaKolicina = 5, Kategorija = "Patike", Pol = "Unisex", Boja = "Bela" },
                new() { Naziv = "Nike React Infinity", NabavnaCena = 11000, ProdajnaCena = 19500, Kolicina = 20, MinimalnaKolicina = 5, Kategorija = "Patike", Pol = "Muško", Boja = "Siva" },
                new() { Naziv = "Adidas NMD R1", NabavnaCena = 8500, ProdajnaCena = 15500, Kolicina = 35, MinimalnaKolicina = 5, Kategorija = "Patike", Pol = "Muško", Boja = "Crna" },
                new() { Naziv = "Puma Future Rider", NabavnaCena = 7000, ProdajnaCena = 13000, Kolicina = 45, MinimalnaKolicina = 5, Kategorija = "Patike", Pol = "Unisex", Boja = "Narandžasta" },
                new() { Naziv = "Nike Blazer Mid", NabavnaCena = 7500, ProdajnaCena = 14000, Kolicina = 40, MinimalnaKolicina = 5, Kategorija = "Cipele", Pol = "Unisex", Boja = "Braon" },
                new() { Naziv = "Adidas Gazelle", NabavnaCena = 6000, ProdajnaCena = 11000, Kolicina = 55, MinimalnaKolicina = 5, Kategorija = "Patike", Pol = "Unisex", Boja = "Plava" },
                new() { Naziv = "Puma Mayze", NabavnaCena = 6500, ProdajnaCena = 12000, Kolicina = 38, MinimalnaKolicina = 5, Kategorija = "Patike", Pol = "Žensko", Boja = "Bela" },
                new() { Naziv = "Nike Cortez", NabavnaCena = 5500, ProdajnaCena = 10000, Kolicina = 60, MinimalnaKolicina = 5, Kategorija = "Patike", Pol = "Unisex", Boja = "Crvena" },
                new() { Naziv = "Adidas Samba", NabavnaCena = 6500, ProdajnaCena = 12500, Kolicina = 50, MinimalnaKolicina = 5, Kategorija = "Cipele", Pol = "Muško", Boja = "Crna" },
                new() { Naziv = "Reebok Nano X3", NabavnaCena = 9500, ProdajnaCena = 17000, Kolicina = 25, MinimalnaKolicina = 5, Kategorija = "Patike", Pol = "Muško", Boja = "Plava" }
            };

            db.Artikli.AddRange(artikli);
            await db.SaveChangesAsync();

            // Seed Prodaje (30 days of sales)
            var random = new Random();
            int counter = 0;

            for (int i = 0; i < 30; i++)
            {
                var randomDate = DateTime.UtcNow.AddDays(-i).AddHours(-random.Next(0, 12));

                // 1-3 sales per day
                for (int j = 0; j < random.Next(1, 4); j++)
                {
                    counter++;

                    var prodaja = new Domain.Model.Prodaja.ProdajaZaglavlje
                    {
                        BrojRacuna = $"SEED-{counter:D6}",
                        DatumProdaje = randomDate,
                        IDObjekat = 1,
                        NacinPlacanja = random.Next(0, 2) == 0 ? "Kes" : "Kartica",
                        Stavke = new List<Domain.Model.Prodaja.ProdajaStavka>()
                    };

                    // Add 1-4 items
                    for (int k = 0; k < random.Next(1, 5); k++)
                    {
                        var randomArtikal = artikli[random.Next(artikli.Count)];
                        var qty = random.Next(1, 4);
                        var cena = randomArtikal.ProdajnaCena ?? 0;

                        prodaja.Stavke.Add(new Domain.Model.Prodaja.ProdajaStavka
                        {
                            IdArtikal = randomArtikal.Id,
                            Kolicina = qty,
                            Cena = cena
                        });

                        // Update stock
                        randomArtikal.Kolicina = (randomArtikal.Kolicina ?? 0) - qty;
                    }

                    // Calculate total - need to sum manually since EF doesn't have Iznos column
                    decimal ukupanIznos = 0;
                    foreach (var stavka in prodaja.Stavke)
                    {
                        ukupanIznos += stavka.Kolicina * stavka.Cena;
                    }

                    db.ProdajaZaglavlja.Add(prodaja);

                    // Log to DnevnikPromena
                    db.DnevnikPromena.Add(new Domain.Model.DnevnikPromena
                    {
                        TipPromene = "Prodaja",
                        Datum = randomDate,
                        Iznos = ukupanIznos,
                        BrojRacuna = prodaja.BrojRacuna,
                        Komentar = "Test prodaja - automatski generisano",
                        KorisnikIme = "System Seed"
                    });
                }
            }

            await db.SaveChangesAsync();

            logger.LogInformation("Seed data generation completed. Created {Count} sales.", counter);

            return Results.Ok(new
            {
                success = true,
                message = $"Test podaci uspešno kreirani! Ukupno {counter} prodaja.",
                stats = new
                {
                    artikli = artikli.Count,
                    prodaje = counter,
                    ukupanPromet = await db.ProdajaZaglavlja
                        .SelectMany(p => p.Stavke)
                        .SumAsync(s => s.Kolicina * s.Cena)
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating seed data");
            return Results.Problem(detail: ex.Message, statusCode: 500, title: "Greška pri generisanju test podataka");
        }
    });

    app.MapAllEndpoints();
    Console.WriteLine("All endpoints mapped");
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

// Helper function - samo jedna verzija!
static decimal CalculatePercentChange(decimal oldValue, decimal newValue)
{
    if (oldValue == 0) return newValue > 0 ? 100 : 0;
    return ((newValue - oldValue) / oldValue) * 100;
}


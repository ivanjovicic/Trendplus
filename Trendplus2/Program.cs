using Application.Artikli.Commands.CreateArtikal;
using Application.Artikli.Commands.UpdateArtikal;
using Application.Artikli.Common.Interfaces;
using Application.Artikli.Queries.GetArtikal;
using Application.Artikli.Queries.VratiArtikle;
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

    // RabbitMQ
    builder.Services.Configure<Infrastructure.Configuration.RabbitMqSettings>(
        builder.Configuration.GetSection("RabbitMq"));
    builder.Services.AddSingleton<IMessageBroker, RabbitMqMessageBroker>();

    // Background Workers
    builder.Services.AddHostedService<Workers.SyncWorker>();
    builder.Services.AddHostedService<Workers.OutboxProcessorWorker>();

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
    app.MapGet("/health", (IMessageBroker messageBroker) =>
    {
        var rabbitMq = messageBroker as RabbitMqMessageBroker;
        return Results.Ok(new
        {
            Status = "Backend je živ",
            RabbitMq = new
            {
                Enabled = messageBroker.IsEnabled,
                CircuitOpen = rabbitMq?.IsCircuitOpen ?? false
            },
            Timestamp = DateTime.UtcNow
        });
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


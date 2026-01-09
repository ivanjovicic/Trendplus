using Application.Artikli.Commands.CreateArtikal;
using Application.Artikli.Commands.UpdateArtikal;
using Application.Artikli.Common.Interfaces;
using Application.Artikli.Queries.GetArtikal;
using Application.Artikli.Queries.VratiArtikle;
using Application.Behaviors;
using Application.Common.Interfaces;
using Application.Dobavljaci.Queries;
using Application.Performance.Queries;
using Application.Prodaja.Commands.ProdajArtikle;
using Application.Prodaja.Queries;
using Application.TipObuce.Queries;
using FluentValidation;
using Infrastructure.DbContexts;
using Infrastructure.Middleware;
using Infrastructure.Repository;
using Infrastructure.Resilience;
using Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Globalization;
using Trendplus2;
using Trendplus2.Dtos;

var builder = WebApplication.CreateBuilder(args);

// Serilog bootstrap
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

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

// Artikli - Create
app.MapPost("/artikli", async (
    Application.Artikli.Commands.CreateArtikal.ClientCreateArtikalDto dto,
    IMediator mediator,
    ILogger<Program> logger) =>
{
    logger.CreateArtikalRequest(System.Text.Json.JsonSerializer.Serialize(dto));

    var cmd = new CreateArtikalCommand(
        dto.Naziv,
        dto.TipObuceId,
        dto.DobavljacId,
        dto.NabavnaCena,
        dto.NabavnaCenaDin,
        dto.PrvaProdajnaCena,
        dto.ProdajnaCena,
        dto.Kolicina,
        dto.Komentar,
        dto.IDObjekat,
        dto.IDSezona
    );

    var id = await mediator.Send(cmd);

    logger.ArtikalCreated(id);

    return Results.Created(
        string.Create(CultureInfo.InvariantCulture, $"/artikli/{id}"),
        new { id }
    );
});

// Artikli - Get By Id
app.MapGet("/artikli/{id:int}", async (int id, IMediator mediator, ILogger<Program> logger) =>
{
    logger.GetArtikalRequest(id);
    try
    {
        var result = await mediator.Send(new GetArtikalQuery(id));
        if (result == null)
            return Results.NotFound(new { error = "Artikal nije prona?en." });
        return Results.Ok(result);
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound(new { error = "Artikal nije prona?en." });
    }
});

// Artikli - List
app.MapGet("/artikli", async (IMediator mediator, ILogger<Program> logger) =>
{
    logger.GetArtikliRequest();
    var result = await mediator.Send(new GetArtikliQuery());
    return Results.Ok(result);
});

// Artikli - Update
app.MapPut("/artikli/{id:int}", async (
    int id,
    Application.Artikli.Commands.UpdateArtikal.UpdateArtikalDto dto,
    IMediator mediator,
    ILogger<Program> logger) =>
{
    logger.UpdateArtikalRequest(id, System.Text.Json.JsonSerializer.Serialize(dto));

    var cmd = new UpdateArtikalCommand(
        id,
        dto.Naziv,
        dto.TipObuceId,
        dto.DobavljacId,
        dto.NabavnaCena,
        dto.NabavnaCenaDin,
        dto.PrvaProdajnaCena,
        dto.ProdajnaCena,
        dto.Kolicina,
        dto.Komentar,
        dto.IDObjekat,
        dto.IDSezona
    );

    try
    {
        await mediator.Send(cmd);
        logger.ArtikalUpdated(id);
        return Results.NoContent();
    }
    catch (InvalidOperationException ex)
    {
        logger.UpdateArtikalFailed(ex, id);
        return Results.NotFound(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        logger.UpdateArtikalError(ex);
        return Results.Problem(detail: ex.Message);
    }
});

// Tipovi Obu?e
app.MapGet("/api/tipovi-obuce", async (IMediator mediator) =>
{
    var result = await mediator.Send(new GetTipObuceQuery());
    return Results.Ok(result);
});

app.MapPost("/api/tipovi-obuce", async (
    Application.TipObuce.Commands.CreateTipObuceCommand cmd,
    IMediator mediator) =>
{
    var id = await mediator.Send(cmd);
    return Results.Created($"/api/tipovi-obuce/{id}", new { id });
});

// Dobavlja?i
app.MapGet("/api/dobavljaci", async (IMediator mediator) =>
{
    var result = await mediator.Send(new GetDobavljacQuery());
    return Results.Ok(result);
});

app.MapPost("/api/dobavljaci", async (CreateDobavljacDto dto, ITrendplusDbContext db) =>
{
    var entity = new Domain.Model.Dobavljac 
    { 
        Naziv = dto.Naziv,
        Adresa = dto.Adresa,
        Telefon = dto.Telefon,
        Napomena = dto.Napomena
    };
    db.Dobavljaci.Add(entity);
    await db.SaveChangesAsync();
    return Results.Created($"/api/dobavljaci/{entity.Id}", new { id = entity.Id });
});

// Sezone
app.MapGet("/api/sezone", async (ITrendplusDbContext db) =>
{
    var result = await db.Sezone.OrderBy(s => s.DatumOd).ToListAsync();
    return Results.Ok(result);
});

app.MapPost("/api/sezone", async (CreateSezonaDto dto, ITrendplusDbContext db) =>
{
    var entity = new Domain.Model.Sezona
    {
        Naziv = dto.Naziv,
        DatumOd = dto.DatumOd,
        DatumDo = dto.DatumDo
    };
    db.Sezone.Add(entity);
    await db.SaveChangesAsync();
    return Results.Created($"/api/sezone/{entity.Id}", new { id = entity.Id });
});

// Prodaja
app.MapPost("/api/prodaja", async (ProdajArtikleCommand command, IMediator mediator, ILogger<Program> logger) =>
{
    logger.ProdajaRequest(System.Text.Json.JsonSerializer.Serialize(command));
    var prodajaId = await mediator.Send(command);
    logger.ProdajaCreated(prodajaId);
    return Results.Ok(prodajaId);
});

// Prodaja - List (Sales History)
app.MapGet("/api/prodaje", async (
    IMediator mediator,
    ILogger<Program> logger,
    int pageNumber = 1,
    int pageSize = 50,
    DateTime? fromDate = null,
    DateTime? toDate = null) =>
{
    logger.LogInformation("Fetching sales history: page={PageNumber}, size={PageSize}, from={FromDate}, to={ToDate}",
        pageNumber, pageSize, fromDate, toDate);

    var query = new GetProdajeQuery(fromDate, toDate, pageNumber, pageSize);
    var result = await mediator.Send(query);
    return Results.Ok(result);
});

// Nivelacija
app.MapPost("/api/nivelacija", async (
    ITrendplusDbContext db,
    ILogger<Program> logger,
    HttpContext http,
    NivelacijaCenaRequest req,
    CancellationToken ct) =>
{
    if (req.ArtikalId <= 0)
        return Results.BadRequest(new { error = "ArtikalId je obavezan." });

    var artikal = await db.Artikli.FirstOrDefaultAsync(a => a.Id == req.ArtikalId, ct);
    if (artikal == null)
        return Results.NotFound(new { error = "Artikal ne postoji." });

    var stara = artikal.ProdajnaCena;
    var nova = req.NovaProdajnaCena;

    if (nova < 0)
        return Results.BadRequest(new { error = "NovaProdajnaCena mora biti >= 0." });

    artikal.ProdajnaCena = nova;

    db.DnevnikPromena.Add(new Domain.Model.DnevnikPromena
    {
        TipPromene = "Nivelacija",
        Datum = DateTime.UtcNow,
        Iznos = 0,
        ArtikalId = artikal.Id,
        StaraProdajnaCena = stara,
        NovaProdajnaCena = nova,
        KorisnikIme = http.User?.Identity?.Name,
        Komentar = string.IsNullOrWhiteSpace(req.Komentar)
            ? $"Nivelacija cene: {stara} -> {nova}"
            : req.Komentar
    });

    await db.SaveChangesAsync(ct);

    logger.NivelacijaCene(artikal.Id, stara, nova);

    return Results.Ok(new { artikalId = artikal.Id, staraCena = stara, novaCena = nova });
});

// Nivelacije - Pregled
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
    var baseQuery = db.DnevnikPromena.AsNoTracking()
        .Where(x => x.TipPromene == "Nivelacija");

    if (artikalId.HasValue)
        baseQuery = baseQuery.Where(x => x.ArtikalId == artikalId.Value);

    if (fromDate.HasValue)
        baseQuery = baseQuery.Where(x => x.Datum >= fromDate.Value);

    if (toDate.HasValue)
        baseQuery = baseQuery.Where(x => x.Datum <= toDate.Value);

    var query = baseQuery
        .GroupJoin(
            db.Artikli.AsNoTracking(),
            p => p.ArtikalId,
            a => (int?)a.Id,
            (p, arts) => new { p, a = arts.FirstOrDefault() });
    
    if (!string.IsNullOrWhiteSpace(naziv))
    {
        var n = naziv.Trim();
        query = query.Where(x => x.a != null && EF.Functions.ILike(x.a.Naziv, $"%{n}%"));
    }

    var total = await query.CountAsync(ct);

    var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

    query = (sortBy?.ToLowerInvariant()) switch
    {
        "datum" => desc ? query.OrderByDescending(x => x.p.Datum) : query.OrderBy(x => x.p.Datum),
        "artikalid" => desc ? query.OrderByDescending(x => x.p.ArtikalId) : query.OrderBy(x => x.p.ArtikalId),
        "stara" => desc ? query.OrderByDescending(x => x.p.StaraProdajnaCena) : query.OrderBy(x => x.p.StaraProdajnaCena),
        "nova" => desc ? query.OrderByDescending(x => x.p.NovaProdajnaCena) : query.OrderBy(x => x.p.NovaProdajnaCena),
        "naziv" => desc ? query.OrderByDescending(x => x.a != null ? x.a.Naziv : "") : query.OrderBy(x => x.a != null ? x.a.Naziv : ""),
        _ => desc ? query.OrderByDescending(x => x.p.Datum) : query.OrderBy(x => x.p.Datum)
    };

    var items = await query
        .Skip((pageNumber - 1) * pageSize)
        .Take(pageSize)
        .Select(x => new
        {
            id = x.p.Id,
            datum = x.p.Datum,
            artikalId = x.p.ArtikalId,
            artikalNaziv = x.a != null ? x.a.Naziv : null,
            staraProdajnaCena = x.p.StaraProdajnaCena,
            novaProdajnaCena = x.p.NovaProdajnaCena,
            komentar = x.p.Komentar,
            korisnikIme = x.p.KorisnikIme
        })
        .ToListAsync(ct);

    return Results.Ok(new
    {
        items,
        totalCount = total,
        pageNumber,
        pageSize,
        sortBy,
        sortDir
    });
});

app.MapControllers();
app.MapFallbackToFile("index.html");

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
    db.Database.Migrate();
}

app.Run();

// DTOs in namespace
namespace Trendplus2.Dtos
{
    internal sealed record CreateDobavljacDto(string Naziv, string? Adresa, string? Telefon, string? Napomena);
    internal sealed record CreateSezonaDto(string Naziv, DateTime DatumOd, DateTime DatumDo);
    internal sealed record NivelacijaCenaRequest(int ArtikalId, decimal NovaProdajnaCena, string? Komentar);
}

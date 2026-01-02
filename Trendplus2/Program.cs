using Application.Artikli.Commands.CreateArtikal;
using Application.Artikli.Commands.UpdateArtikal;
using Application.Artikli.Common.Interfaces;
using Application.Artikli.Queries.GetArtikal;
using Application.Artikli.Queries.VratiArtikle;
using Application.Behaviors;
using Application.Common.Interfaces;
using Application.Dobavljaci.Queries;
using Application.Prodaja.Commands.ProdajArtikle;
using Application.TipObuce.Queries;
using Infrastructure.DbContexts;
using Infrastructure.Middleware;
using Infrastructure.Repository;
using Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Serilog;
using Serilog.Events;
using System.Globalization;
using Application.Performance.Queries;

var builder = WebApplication.CreateBuilder(args);

// Serilog bootstrap iz appsettings.json
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

// DbContext‑ovi – logovanje upita preko Serilog-a
builder.Services.AddDbContext<TrendplusDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .EnableSensitiveDataLogging());

builder.Services.AddScoped<ITrendplusDbContext>(sp =>
    sp.GetRequiredService<TrendplusDbContext>());

builder.Services.AddDbContext<AnalyticsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("AnalyticsConnection"))
           .EnableSensitiveDataLogging());

builder.Services.AddScoped<IAnalyticsDbContext>(sp =>
    sp.GetRequiredService<AnalyticsDbContext>());

builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceLoggingBehavior<,>));
builder.Services.AddScoped<IErrorStore, DbErrorStore>();
builder.Services.AddScoped<IProdajaRepository, ProdajaRepository>();

builder.Services.AddControllers();
builder.Services.AddHostedService<Workers.SyncWorker>();
builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(CreateArtikalHandler).Assembly));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
           .AllowAnyOrigin()
           .AllowAnyHeader()
           .AllowAnyMethod();
    });
});

var app = builder.Build();

// Ensure PerformanceLogs table exists
//using (var scope = app.Services.CreateScope())
//{
//    var analyticsDb = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
//    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
//    try
//    {
//        await Infrastructure.DbContexts.DatabaseMigrationHelper.EnsurePerformanceLogsTableExistsAsync(
//            analyticsDb, 
//            logger
//        );
//    }
//    catch (Exception ex)
//    {
//        logger.LogError(ex, "Failed to initialize PerformanceLogs table. Performance tracking may not work.");
//    }
//}

// Serilog request logging – detaljan log svakog HTTP zahteva
app.UseSerilogRequestLogging(opts =>
{
    // EnrichContext se poziva za svaki request
    opts.EnrichDiagnosticContext = (diag, http) =>
    {
        diag.Set("RequestHost", http.Request.Host.Value);
        diag.Set("RequestScheme", http.Request.Scheme);
        diag.Set("UserAgent", http.Request.Headers.UserAgent.ToString());
        diag.Set("RequestPath", http.Request.Path);
    };
});

// global exception logging u DB (tvoj middleware)
app.UseMiddleware<ExceptionLoggingMiddleware>();

app.UseRouting();
app.UseCors("AllowFrontend");

if (app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();

// ================= ENDPOINTS ==========// Health
app.MapGet("/health", () => Results.Ok("Backend je živ"));

// Errors
app.MapGet("/errors", async (IErrorStore store) =>
{
    var errors = await store.GetAllAsync();
    return Results.Ok(errors);
});

// Logs endpoint – paginisano vraćanje grešaka iz ErrorRecords
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
        {
            filtered = filtered.Where(e => e.Timestamp >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            filtered = filtered.Where(e => e.Timestamp <= toDate.Value);
        }

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
        logger.LogError(ex, "Failed to fetch logs from ErrorRecords table");
        return Results.Problem(
            detail: "Unable to fetch logs. The database table may not exist. Please run migrations: dotnet ef database update",
            statusCode: 500,
            title: "Database Error"
        );
    }
});

// Performance stats endpoint
app.MapGet("/api/performance", async (
    IMediator mediator,
    ILogger<Program> logger,
    int topCount = 20,
    int minDurationMs = 1000,
    DateTime? fromDate = null,
    DateTime? toDate = null) =>
{
    logger.LogInformation("GET /api/performance - TopCount: {TopCount}, MinDuration: {MinDuration}ms", 
        topCount, minDurationMs);

    var query = new GetPerformanceStatsQuery(topCount, minDurationMs, fromDate, toDate);
    var result = await mediator.Send(query);

    return Results.Ok(result);
});

// Diagnostic test-insert
app.MapPost("/diagnostics/test-insert", async (TrendplusDbContext db, ILogger<Program> logger) =>
{
    try
    {
        var test = new Domain.Model.Artikli
        {
            Naziv = "DIAGNOSTIC_TEST",
            ProdajnaCena = 1,
            Kolicina = 0
        };

        db.Artikli.Add(test);
        await db.SaveChangesAsync();
        logger.LogInformation("Diagnostic insert created artikal with Id {Id}", test.Id);
        return Results.Ok(new { test.Id });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Test-insert failed");
        return Results.Problem(detail: ex.Message);
    }
});

// Artikli
app.MapPost("/artikli", async (
    Application.Artikli.Commands.CreateArtikal.ClientCreateArtikalDto dto,
    IMediator mediator,
    ILogger<Program> logger) =>
{
    logger.LogInformation("POST /artikli payload: {@Dto}", dto);

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

    logger.LogInformation("Artikal kreiran sa Id {Id}", id);

    return Results.Created(
        string.Create(
            CultureInfo.InvariantCulture,
            $"/artikli/{id}"
        ),
        new { id }
    );
});

app.MapGet("/artikli/{id:int}", async (int id, IMediator mediator, ILogger<Program> logger) =>
{
    logger.LogInformation("GET /artikli/{Id}", id);
    try
    {
        var result = await mediator.Send(new GetArtikalQuery(id));
        if (result == null)
            return Results.NotFound(new { error = "Artikal nije pronađen." });
        return Results.Ok(result);
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound(new { error = "Artikal nije pronađen." });
    }
});

app.MapGet("/artikli", async (IMediator mediator, ILogger<Program> logger) =>
{
    logger.LogInformation("GET /artikli (lista)");
    var result = await mediator.Send(new GetArtikliQuery());
    return Results.Ok(result);
});

// Tipovi obuća
app.MapGet("/tipovi-obuce", async (IMediator mediator) =>
{
    var result = await mediator.Send(new GetTipObuceQuery());
    return Results.Ok(result);
});

app.MapPost("/tipovi-obuce", async (
    Application.TipObuce.Commands.CreateTipObuceCommand cmd,
    IMediator mediator) =>
{
    var id = await mediator.Send(cmd);
    return Results.Created($"/tipovi-obuce/{id}", new { id });
});

// Dobavljači
app.MapGet("/dobavljaci", async (IMediator mediator) =>
{
    var result = await mediator.Send(new GetDobavljacQuery());
    return Results.Ok(result);
});

app.MapPost("/dobavljaci", async (CreateDobavljacDto dto, ITrendplusDbContext db) =>
{
    var entity = new Domain.Model.Dobavljac { Naziv = dto.Naziv };
    db.Dobavljaci.Add(entity);
    await db.SaveChangesAsync();
    return Results.Created($"/dobavljaci/{entity.Id}", new { id = entity.Id });
});

// Prodaja
app.MapPost("/api/prodaja", async (ProdajArtikleCommand command, IMediator mediator, ILogger<Program> logger) =>
{
    logger.LogInformation("POST /api/prodaja payload: {@Command}", command);
    var prodajaId = await mediator.Send(command);
    logger.LogInformation("Prodaja kreirana sa Id {Id}", prodajaId);
    return Results.Ok(prodajaId);
});

app.MapPut("/artikli/{id:int}", async (
    int id,
    Application.Artikli.Commands.UpdateArtikal.UpdateArtikalDto dto,
    IMediator mediator,
    ILogger<Program> logger) =>
{
    logger.LogInformation("Received PUT /artikli/{Id} DTO: {@Dto}", id, dto);

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
        logger.LogInformation("Artikal {Id} uspešno izmenjen", id);
        return Results.NoContent();
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning(ex, "UpdateArtikal failed for Id {Id}", id);
        return Results.NotFound(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error while handling UpdateArtikalCommand");
        return Results.Problem(detail: ex.Message);
    }
});

// Price leveling (nivelacija) - logs old->new price in DnevnikPromena
app.MapPost("/api/nivelacija", async (
    ITrendplusDbContext db,
    ILogger<Program> logger,
    HttpContext http,
    NivelacijaCenaRequest req,
    CancellationToken ct) =>
{
    if (req.ArtikalId <= 0)
    {
        return Results.BadRequest(new { error = "ArtikalId je obavezan." });
    }

    var artikal = await db.Artikli.FirstOrDefaultAsync(a => a.Id == req.ArtikalId, ct);
    if (artikal == null)
    {
        return Results.NotFound(new { error = "Artikal ne postoji." });
    }

    var stara = artikal.ProdajnaCena;
    var nova = req.NovaProdajnaCena;

    if (nova < 0)
    {
        return Results.BadRequest(new { error = "NovaProdajnaCena mora biti >= 0." });
    }

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

    logger.LogInformation("Nivelacija cene za ArtikalId {Id}: {Old} -> {New}", artikal.Id, stara, nova);

    return Results.Ok(new { artikalId = artikal.Id, staraCena = stara, novaCena = nova });
});

// Nivelacije pregled - filtering/sorting/paging
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

app.Run();

// DTO used by /dobavljaci endpoint
record CreateDobavljacDto(string Naziv);

public sealed record NivelacijaCenaRequest(int ArtikalId, decimal NovaProdajnaCena, string? Komentar);

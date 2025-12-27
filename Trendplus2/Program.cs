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
using Application.Logs.Queries;

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
builder.Services.AddScoped<IErrorStore, DbErrorStore>();
builder.Services.AddScoped<IProdajaRepository, ProdajaRepository>();

builder.Services.AddControllers();
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

// ================= ENDPOINTS =================

// Health
app.MapGet("/health", () => Results.Ok("Backend je živ"));

// Errors
app.MapGet("/errors", async (IErrorStore store) =>
{
    var errors = await store.GetAllAsync();
    return Results.Ok(errors);
});

// Logs endpoint
app.MapGet("/api/logs", async (
    IMediator mediator,
    ILogger<Program> logger,
    int pageNumber = 1,
    int pageSize = 100,
    string? level = null,
    DateTime? fromDate = null,
    DateTime? toDate = null) =>
{
    logger.LogInformation("GET /api/logs - PageNumber: {PageNumber}, PageSize: {PageSize}, Level: {Level}", 
        pageNumber, pageSize, level);

    var query = new GetLogsQuery(pageNumber, pageSize, level, fromDate, toDate);
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
    var result = await mediator.Send(new GetArtikalQuery(id));
    return Results.Ok(result);
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

app.MapControllers();

app.Run();

// DTO used by /dobavljaci endpoint
record CreateDobavljacDto(string Naziv);

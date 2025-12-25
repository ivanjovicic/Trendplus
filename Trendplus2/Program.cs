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
using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Infrastructure.Middleware;
using Infrastructure.Repository;
using Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql; // <- opcionalno, ali može stajati
using System.Data;
using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Design;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";

builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Add services to the container.  ✅ PostgreSQL umesto SQL Server
builder.Services.AddDbContext<TrendplusDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .EnableSensitiveDataLogging()
           .LogTo(Console.WriteLine, LogLevel.Information));
builder.Services.AddScoped<ITrendplusDbContext>(sp =>
    sp.GetRequiredService<TrendplusDbContext>());

builder.Services.AddDbContext<AnalyticsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("AnalyticsConnection"))
           .EnableSensitiveDataLogging()
           .LogTo(Console.WriteLine, LogLevel.Information));
builder.Services.AddScoped<IAnalyticsDbContext>(sp =>
    sp.GetRequiredService<AnalyticsDbContext>());
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

// Register DB-backed error store
builder.Services.AddScoped<IErrorStore, DbErrorStore>();
builder.Services.AddScoped<IProdajaRepository, ProdajaRepository>();

builder.Services.AddControllers();
// Make minimal API JSON binding case-insensitive so DTO model-binding matches client payload
builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.PropertyNameCaseInsensitive = true;
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(CreateArtikalHandler).Assembly));
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        //policy
        //    .WithOrigins(
        //        "http://localhost:8080",          // local dev
        //        "https://trendplus.vercel.app"    // Vercel prod
        //    )
        //    .AllowAnyHeader()
        //    .AllowAnyMethod();
        policy
           .AllowAnyOrigin()
           .AllowAnyHeader()
           .AllowAnyMethod();
    });
});
var app = builder.Build();

// middleware koji loguje exceptione
app.UseMiddleware<ExceptionLoggingMiddleware>();

app.UseRouting();                 // ✅ OBAVEZNO
app.UseCors("AllowFrontend");     // ✅ MORA PRE auth

if (app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Swagger dozvoljen i u Production (Render)
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
    IMediator mediator) =>
{
    var cmd = new CreateArtikalCommand(
        //dto.PLU,
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
    return Results.Created($"/artikli/{id}", new { id });
});

app.MapGet("/artikli/{id:int}", async (int id, IMediator mediator) =>
{
    var result = await mediator.Send(new GetArtikalQuery(id));
    return Results.Ok(result);
});

app.MapGet("/artikli", async (IMediator mediator) =>
{
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
app.MapPost("/api/prodaja", async (ProdajArtikleCommand command, IMediator mediator) =>
{
    var prodajaId = await mediator.Send(command);
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

using Application.Artikli.Commands.CreateArtikal;
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
using System.Data;
using System.IO;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";

builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Add services to the container.
builder.Services.AddDbContext<TrendplusDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
           .EnableSensitiveDataLogging()
           .LogTo(Console.WriteLine, LogLevel.Information));
builder.Services.AddScoped<ITrendplusDbContext>(sp =>
    sp.GetRequiredService<TrendplusDbContext>());

builder.Services.AddDbContext<AnalyticsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AnalyticsConnection"))
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
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Use exception logging middleware early so it catches exceptions from downstream
app.UseMiddleware<ExceptionLoggingMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseHsts();
}
else
{
    // Render already handles HTTPS externally
    // Do NOT call UseHttpsRedirection() in production
}
app.UseSwagger();
app.UseSwaggerUI();
//app.UseHttpsRedirection();

app.UseAuthorization();
app.UseCors();
// Health endpoint
app.MapGet("/health", () => Results.Ok("Backend je živ"));

// Endpoint to return logged errors
app.MapGet("/errors", async (IErrorStore store) =>
{
    var errors = await store.GetAllAsync();
    return Results.Ok(errors);
});

// Diagnostic test-insert endpoint
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
        var changes = await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        var conn = db.Database.GetDbConnection();
        logger.LogInformation("Test insert: changes={Changes}, id={Id}, database={Db}", changes, test.Id, conn?.Database);
        return Results.Ok(new { changes, id = test.Id, database = conn?.Database });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Test-insert failed");
        return Results.Problem(detail: ex.Message);
    }
});

// Use model-binding: framework will deserialize request body into ClientCreateArtikalDto
app.MapPost("/artikli", async (Application.Artikli.Commands.CreateArtikal.ClientCreateArtikalDto dto, IMediator mediator, ILogger<Program> logger) =>
{
    if (dto is null)
    {
        logger.LogWarning("ClientCreateArtikalDto bound as null. Returning 400.");
        return Results.BadRequest(new { error = "Invalid payload. Expected JSON matching client DTO." });
    }

    logger.LogInformation("Received /artikli DTO: {@Dto}", dto);

    // Use the command constructor (match your command's parameter order)
    var cmd = new CreateArtikalCommand(
        dto.PLU,
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

    logger.LogInformation("Mapped CreateArtikalCommand: {@Command}", cmd);

    try
    {
        var id = await mediator.Send(cmd);
        logger.LogInformation("CreateArtikalHandler returned id={Id}", id);
        return Results.Created($"/artikli/{id}", new { id });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error while handling CreateArtikalCommand");
        return Results.Problem(detail: ex.Message);
    }
});

app.MapGet("/artikli/{id:int}", async (int id, IMediator mediator) =>
{
    var result = await mediator.Send(new GetArtikalQuery(id));
    return Results.Ok(result);
});

app.MapGet("/tipovi-obuce", async (IMediator mediator) =>
{
    var result = await mediator.Send(new GetTipObuceQuery());
    return Results.Ok(result);
});

app.MapGet("/dobavljaci", async (IMediator mediator) =>
{
    var result = await mediator.Send(new GetDobavljacQuery());
    return Results.Ok(result);
});

// POST endpoint to create a new TipObuce
app.MapPost("/tipovi-obuce", async (Application.TipObuce.Commands.CreateTipObuceCommand cmd, IMediator mediator) =>
{
    var id = await mediator.Send(cmd);
    return Results.Created($"/tipovi-obuce/{id}", new { id });
});

app.MapPost("/dobavljaci", async (CreateDobavljacDto dto, ITrendplusDbContext db) =>
{
    var entity = new Domain.Model.Dobavljac { Naziv = dto.Naziv };
    db.Dobavljaci.Add(entity);
    await db.SaveChangesAsync(CancellationToken.None);
    return Results.Created($"/dobavljaci/{entity.Id}", new { id = entity.Id });
});

app.MapPost("/api/prodaja", async (
    ProdajArtikleCommand command,
    IMediator mediator) =>
{
    var prodajaId = await mediator.Send(command);
    return Results.Ok(prodajaId);
});

app.MapGet("/api/artikli", async (IMediator mediator) =>
{
    var result = await mediator.Send(new GetArtikliQuery());
    return Results.Ok(result);
});

app.MapControllers();

app.Run();

// DTO used by /dobavljaci endpoint
record CreateDobavljacDto(string Naziv);

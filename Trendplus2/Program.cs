using Application.Artikli.Commands.CreateArtikal;
using Application.Artikli.Common.Interfaces;
using Application.Artikli.Queries.GetArtikal;
using Application.Artikli.Queries.VratiArtikle;
using Application.Common.Interfaces;
using Application.Dobavljaci.Queries;
using Application.Prodaja.Commands.ProdajArtikle;
using Application.TipObuce.Queries;
using Infrastructure.DbContexts;
using Infrastructure.Middleware;
using MediatR;

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

// Tipovi obuće
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

app.MapControllers();

app.Run();

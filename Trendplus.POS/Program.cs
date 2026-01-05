using System.Net.Http.Headers;
using Trendplus.POS.Dtos;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS: dozvoli samo POS UI origin (zameni port svojim)
builder.Services.AddCors(options =>
{
    options.AddPolicy("PosUi", p =>
        p.WithOrigins("http://localhost:5174") // POS React
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials());
});

// Typed HttpClient za Core API
builder.Services.AddHttpClient<ICoreApiClient, CoreApiClient>((sp, http) =>
{
    var baseUrl = builder.Configuration["CoreApi:BaseUrl"]
                  ?? throw new InvalidOperationException("CoreApi:BaseUrl missing");
    http.BaseAddress = new Uri(baseUrl);
    http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddProblemDetails(); // bolji error response

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseCors("PosUi");

app.MapControllers();

app.MapPost("/pos/sale",
async (
    PosSaleRequest dto,
    HttpContext ctx,
    ICoreApiClient core,
    CancellationToken ct
) =>
{
    // Header: X-Terminal-Id
    if (!ctx.Request.Headers.TryGetValue("X-Terminal-Id", out var terminalId) ||
        string.IsNullOrWhiteSpace(terminalId))
    {
        return Results.BadRequest("Missing X-Terminal-Id header.");
    }

    if (dto.Items.Count == 0)
        return Results.BadRequest("Cart is empty.");

    if (dto.Items.Any(i => i.ProductId <= 0 || i.Qty <= 0))
        return Results.BadRequest("Invalid product or quantity.");

    var cmd = new CreateSaleCommand
    {
        TerminalId = terminalId!,
        PaymentType = dto.PaymentType,
        Source = "POS",
        Items = dto.Items.Select(i => new CreateSaleItem
        {
            ProductId = i.ProductId,
            Qty = i.Qty
        }).ToList()
    };

    // Core API: POST /sales
    var result = await core.PostAsync<CreateSaleCommand, CreateSaleResponse>(
        "/sales", cmd, ct);

    return Results.Ok(result);
})
.WithName("CreatePosSale")
.WithTags("POS");


// Health / ping (korisno za kasu)
app.MapGet("/health", () => Results.Ok(new { ok = true }))
   .WithTags("System");

app.Run();


// ---------- CoreApi client ----------
public interface ICoreApiClient
{
    Task<TResponse> PostAsync<TRequest, TResponse>(
        string path, TRequest body, CancellationToken ct);

    Task<TResponse> GetAsync<TResponse>(
        string path, CancellationToken ct);
}
public sealed class CoreApiClient(HttpClient http) : ICoreApiClient
{
    public async Task<TResponse> PostAsync<TRequest, TResponse>(
        string path, TRequest body, CancellationToken ct)
    {
        using var resp = await http.PostAsJsonAsync(path, body, ct);

        if (!resp.IsSuccessStatusCode)
        {
            var text = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Core API error {(int)resp.StatusCode}: {text}");
        }

        return (await resp.Content.ReadFromJsonAsync<TResponse>(cancellationToken: ct))!;
    }

    public async Task<TResponse> GetAsync<TResponse>(
        string path, CancellationToken ct)
    {
        using var resp = await http.GetAsync(path, ct);

        if (!resp.IsSuccessStatusCode)
        {
            var text = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Core API error {(int)resp.StatusCode}: {text}");
        }

        return (await resp.Content.ReadFromJsonAsync<TResponse>(cancellationToken: ct))!;
    }
}

using Infrastructure.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Trendplus2.Endpoints;

namespace Api.Endpoints;

public static class AdminBackendRoutingEndpoints
{
    public sealed class BackendRoutingPreferenceDto
    {
        public string PrimaryProvider { get; set; } = "render";
        public bool FallbackEnabled { get; set; }
        public string? FallbackProvider { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public string UpdatedBy { get; set; } = "startup";
    }

    public sealed class BackendProviderHealthDto
    {
        public string Provider { get; set; } = string.Empty;
        public bool Success { get; set; }
        public int? StatusCode { get; set; }
        public long LatencyMs { get; set; }
        public DateTime CheckedAtUtc { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public static void MapAdminBackendRoutingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/backend-routing")
            .WithName("Admin Backend Routing");

        group.MapGet("/", GetPreference)
            .WithName("GetBackendRoutingPreference")
            .Produces<BackendRoutingPreferenceDto>(StatusCodes.Status200OK);

        group.MapPost("/", UpdatePreference)
            .WithName("UpdateBackendRoutingPreference")
            .Produces<BackendRoutingPreferenceDto>(StatusCodes.Status200OK)
            .Produces<object>(StatusCodes.Status400BadRequest);

        group.MapGet("/ping/{provider}", PingProvider)
            .WithName("PingBackendProvider")
            .Produces<BackendProviderHealthDto>(StatusCodes.Status200OK)
            .Produces<object>(StatusCodes.Status400BadRequest);
    }

    private static Ok<BackendRoutingPreferenceDto> GetPreference(BackendRoutingPreferenceService service)
    {
        var state = service.Get();
        return TypedResults.Ok(ToDto(state));
    }

    private static IResult UpdatePreference(
        [FromBody] BackendRoutingPreferenceUpdate input,
        BackendRoutingPreferenceService service,
        HttpContext httpContext,
        IConfiguration configuration)
    {
        var access = AdminAccessControl.GetDecision(httpContext, configuration);
        if (access is AdminAccessDecision.MissingCredential)
        {
            return Results.Unauthorized();
        }

        if (access is AdminAccessDecision.Forbidden)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var updatedBy = httpContext.Request.Headers.TryGetValue("X-Admin-User", out var headerValue)
            ? headerValue.ToString()
            : "api";

        if (!service.TryUpdate(input, updatedBy, out var updated, out var error))
        {
            return Results.BadRequest(new { message = error ?? "Invalid backend routing preference." });
        }

        return Results.Ok(ToDto(updated));
    }

    private static async Task<IResult> PingProvider(
        string provider,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        var renderUrl = NormalizeUrl(
            configuration["BackendRouting:Providers:RenderUrl"]
            ?? configuration["BackendRouting:RenderUrl"]
            ?? configuration["Render:BaseUrl"]);
        var flyUrl = NormalizeUrl(
            configuration["BackendRouting:Providers:FlyUrl"]
            ?? configuration["BackendRouting:FlyUrl"]
            ?? configuration["Fly:BaseUrl"]);

        var requested = provider.Trim().ToLowerInvariant();
        string? baseUrl = requested switch
        {
            "render" => renderUrl,
            "fly" or "fly.io" => flyUrl,
            _ => null
        };

        if (baseUrl is null)
        {
            return Results.BadRequest(new
            {
                message = "Unknown provider or missing provider URL configuration.",
                provider
            });
        }

        var client = httpClientFactory.CreateClient("default");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/ready");
        var startedAt = DateTime.UtcNow;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var response = await client.SendAsync(request, ct);
            sw.Stop();
            var ok = response.IsSuccessStatusCode;
            return Results.Ok(new BackendProviderHealthDto
            {
                Provider = requested.StartsWith("fly", StringComparison.Ordinal) ? "fly" : "render",
                Success = ok,
                StatusCode = (int)response.StatusCode,
                LatencyMs = sw.ElapsedMilliseconds,
                CheckedAtUtc = startedAt,
                Message = ok ? "Ready" : $"HTTP {(int)response.StatusCode}"
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Results.Ok(new BackendProviderHealthDto
            {
                Provider = requested.StartsWith("fly", StringComparison.Ordinal) ? "fly" : "render",
                Success = false,
                StatusCode = null,
                LatencyMs = sw.ElapsedMilliseconds,
                CheckedAtUtc = startedAt,
                Message = ex.GetBaseException().Message
            });
        }
    }

    private static BackendRoutingPreferenceDto ToDto(BackendRoutingPreference state)
    {
        return new BackendRoutingPreferenceDto
        {
            PrimaryProvider = state.PrimaryProvider == BackendProvider.Render ? "render" : "fly",
            FallbackEnabled = state.FallbackEnabled,
            FallbackProvider = state.FallbackProvider == BackendProvider.Render ? "render" : "fly",
            UpdatedAtUtc = state.UpdatedAtUtc,
            UpdatedBy = state.UpdatedBy
        };
    }

    private static string? NormalizeUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.Trim().TrimEnd('/');
    }
}

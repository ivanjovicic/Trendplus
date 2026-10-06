using System.Net.Http.Json;
using Api.Models;
using Microsoft.AspNetCore.Http;

namespace Api.Services;

internal static class VendorSalesNivelacijaInternalHttpLoader
{
    internal static async Task<VendorSalesNivelacijaResponseDto> GetAsync(
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        string relativePathAndQuery,
        CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("default");
        var requestUri = new Uri(
            $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{relativePathAndQuery}",
            UriKind.Absolute);

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        if (httpContext.Request.Headers.TryGetValue("Authorization", out var authorization))
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization.ToArray());
        }

        if (httpContext.Request.Headers.TryGetValue("X-Correlation-Id", out var correlationId))
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId.ToArray());
        }

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<VendorSalesNivelacijaResponseDto>(cancellationToken: ct);
        if (payload is null)
        {
            throw new InvalidOperationException("Vendor sales nivelacija response was empty.");
        }

        return payload;
    }

    internal static string BuildQueryString(
        int? vendorId,
        DateTime? eventDate,
        DateTime? from,
        DateTime? to,
        string? category,
        bool includeInactive,
        int maxRows,
        int? storeId,
        string dataScope,
        bool includeEnrichment)
    {
        var query = new List<string>();
        if (vendorId.HasValue) query.Add($"vendorId={vendorId.Value}");
        if (eventDate.HasValue) query.Add($"eventDate={FormatDate(eventDate.Value)}");
        if (from.HasValue) query.Add($"from={FormatDate(from.Value)}");
        if (to.HasValue) query.Add($"to={FormatDate(to.Value)}");
        if (!string.IsNullOrWhiteSpace(category)) query.Add($"category={Uri.EscapeDataString(category)}");
        query.Add($"includeInactive={includeInactive.ToString().ToLowerInvariant()}");
        query.Add($"maxRows={maxRows}");
        if (storeId.HasValue) query.Add($"storeId={storeId.Value}");
        query.Add($"dataScope={Uri.EscapeDataString(dataScope)}");
        query.Add($"includeEnrichment={includeEnrichment.ToString().ToLowerInvariant()}");
        return "?" + string.Join("&", query);
    }

    // Round-trip ("O") values can carry a "+hh:mm" offset; an unescaped "+" is decoded as a
    // space by the loopback request's query binder, which fails the internal pair leg with 400.
    private static string FormatDate(DateTime value) =>
        Uri.EscapeDataString(value.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
}

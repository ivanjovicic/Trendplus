using Api.Services.SefXml;
using Microsoft.AspNetCore.Http.Features;

namespace Trendplus2.Endpoints;

/// <summary>
/// Request-scoped preview only: it never persists uploads, changes Access, or posts inventory.
/// </summary>
public static class SefXmlPreviewEndpoints
{
    internal const long DefaultMaxUploadBytes = 5L * 1024 * 1024;
    private const long MultipartOverheadBytes = 64L * 1024;

    public static void MapSefXmlPreviewEndpoints(this WebApplication app)
    {
        app.MapPost("/api/pilot/sef-xml/preview", async (
            HttpRequest request,
            HttpContext httpContext,
            IConfiguration configuration,
            ISefXmlInvoiceParser parser,
            CancellationToken ct) =>
        {
            if (!IsPilotEnabled(configuration))
            {
                return Results.NotFound();
            }

            if (AdminAccessControl.RejectIfUnauthorized(httpContext, configuration) is { } rejected)
            {
                return rejected;
            }

            if (ApplyUploadLimit(httpContext) is { } tooLarge)
            {
                return tooLarge;
            }

            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new { error = "Pošaljite jedan lokalni .xml fajl za read-only pregled." });
            }

            IFormCollection form;
            try
            {
                form = await request.ReadFormAsync(ct);
            }
            catch (InvalidDataException)
            {
                return TooLargeResult(GetMaxUploadBytes(configuration));
            }

            if (form.Files.Count != 1)
            {
                return Results.BadRequest(new { error = "Možete poslati tačno jedan .xml fajl za read-only pregled." });
            }

            var file = form.Files[0];
            var maxUploadBytes = GetMaxUploadBytes(configuration);
            if (file.Length is <= 0)
            {
                return Results.BadRequest(new { error = "XML fajl je prazan." });
            }

            if (file.Length > maxUploadBytes)
            {
                return TooLargeResult(maxUploadBytes);
            }

            if (!string.Equals(Path.GetExtension(file.FileName), ".xml", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { error = "Podržan je samo lokalni .xml fajl za read-only pregled." });
            }

            try
            {
                await using var source = file.OpenReadStream();
                var preview = await parser.ParseAsync(source, ct);
                return Results.Ok(preview);
            }
            catch (SefXmlPreviewException exception)
            {
                return Results.UnprocessableEntity(new
                {
                    code = exception.Code.ToString(),
                    error = exception.SafeMessage,
                    readOnlyPreview = true
                });
            }
            catch (InvalidDataException)
            {
                return TooLargeResult(maxUploadBytes);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return Results.StatusCode(499);
            }
        })
        .WithTags("SEF XML Pilot")
        .WithName("PreviewSefXmlInvoice")
        .RequireRateLimiting("strict")
        .DisableAntiforgery();
    }

    internal static SefXmlInvoiceParseLimits GetParseLimits(IConfiguration configuration)
    {
        var defaults = SefXmlInvoiceParseLimits.Default;
        return defaults with
        {
            MaxDocumentCharacters = GetBoundedInt(configuration, "SefXmlPilot:MaxDocumentCharacters", defaults.MaxDocumentCharacters),
            MaxElements = GetBoundedInt(configuration, "SefXmlPilot:MaxElements", defaults.MaxElements),
            MaxDepth = GetBoundedInt(configuration, "SefXmlPilot:MaxDepth", defaults.MaxDepth),
            MaxInvoiceLines = GetBoundedInt(configuration, "SefXmlPilot:MaxInvoiceLines", defaults.MaxInvoiceLines)
        };
    }

    internal static long GetMaxUploadBytes(IConfiguration configuration)
    {
        var configured = configuration.GetValue<long?>("SefXmlPilot:MaxUploadBytes");
        return configured is > 0
            ? Math.Min(configured.Value, DefaultMaxUploadBytes)
            : DefaultMaxUploadBytes;
    }

    private static bool IsPilotEnabled(IConfiguration configuration) =>
        configuration.GetValue<bool?>("SefXmlPilot:Enabled") == true;

    private static IResult? ApplyUploadLimit(HttpContext httpContext)
    {
        var maxUploadBytes = GetMaxUploadBytes(httpContext.RequestServices.GetRequiredService<IConfiguration>());
        var allowedBodyBytes = maxUploadBytes + MultipartOverheadBytes;
        var feature = httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false })
        {
            feature.MaxRequestBodySize = allowedBodyBytes;
        }

        return httpContext.Request.ContentLength is long contentLength && contentLength > allowedBodyBytes
            ? TooLargeResult(maxUploadBytes)
            : null;
    }

    private static IResult TooLargeResult(long maxUploadBytes) =>
        Results.Json(
            new
            {
                error = $"XML fajl je veći od dozvoljenih {maxUploadBytes / (1024d * 1024d):0.#} MiB za read-only pregled.",
                maxUploadBytes
            },
            statusCode: StatusCodes.Status413PayloadTooLarge);

    private static int GetBoundedInt(IConfiguration configuration, string key, int fallback)
    {
        var configured = configuration.GetValue<int?>(key);
        return configured is > 0 ? Math.Min(configured.Value, fallback) : fallback;
    }
}

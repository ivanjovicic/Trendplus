using Api.Services.SefXml;

namespace Trendplus2.Endpoints;

/// <summary>
/// Reconciles a parser-produced, read-only invoice preview with a bounded catalogue projection.
/// It never writes a receipt, stock, source document, Access record, or master price.
/// </summary>
public static class SefXmlReconciliationEndpoints
{
    public static void MapSefXmlReconciliationEndpoints(this WebApplication app)
    {
        app.MapPost("/api/pilot/sef-xml/reconcile", async (
            SefXmlReconciliationRequest request,
            HttpContext httpContext,
            IConfiguration configuration,
            ISefXmlCatalogReader catalogueReader,
            SefXmlReconciliationService reconciliationService,
            CancellationToken ct) =>
        {
            if (!SefXmlPreviewEndpoints.IsPilotEnabled(configuration))
            {
                return Results.NotFound();
            }

            if (AdminAccessControl.RejectIfUnauthorized(httpContext, configuration) is { } rejected)
            {
                return rejected;
            }

            if (request.Invoice is null || !request.Invoice.IsReadOnlyPreview || request.Invoice.DocumentKind != SefXmlDocumentKind.Invoice)
            {
                return Results.BadRequest(new { error = "Potrebna je read-only UBL Invoice preview vrednost." });
            }

            var limits = SefXmlPreviewEndpoints.GetParseLimits(configuration);
            if (request.Invoice.Lines.Count > limits.MaxInvoiceLines || (request.ManualSelections?.Count ?? 0) > limits.MaxInvoiceLines)
            {
                return Results.BadRequest(new { error = "Preview ili ručni izbori prelaze dozvoljenu granicu pilota." });
            }

            if (!SefXmlCatalogDataScope.TryNormalize(request.DataScope, out var effectiveDataScope))
            {
                return Results.BadRequest(new { error = "Nepodržan opseg kataloga." });
            }

            try
            {
                var catalogue = await catalogueReader.ReadCandidatesAsync(request.Invoice, request.ManualSelections, effectiveDataScope, ct);
                var preview = reconciliationService.Reconcile(request.Invoice, catalogue, request.ManualSelections) with
                {
                    EffectiveDataScope = effectiveDataScope
                };
                return Results.Ok(preview);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return Results.StatusCode(499);
            }
            catch (Exception)
            {
                return Results.Problem(
                    title: "Katalog nije dostupan za read-only pregled.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        })
        .WithTags("SEF XML Pilot")
        .WithName("ReconcileSefXmlInvoice")
        .RequireRateLimiting("strict")
        .DisableAntiforgery();
    }
}

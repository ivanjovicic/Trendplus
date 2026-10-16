using Api.Services;
using Infrastructure.Services.Caching;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Trendplus2.Endpoints;

public static class GoodsReceiptEndpoints
{
    public static void MapGoodsReceiptEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/goods-receipts")
            .WithTags("Goods receipts")
            .RequireRateLimiting("strict");

        group.MapPost("", async (
            [FromBody] GoodsReceiptRequest? request,
            [FromServices] GoodsReceiptService service,
            HttpContext httpContext,
            IConfiguration configuration,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var denial = AdminAccessControl.RejectIfUnauthorized(httpContext, configuration);
            if (denial is not null) return denial;
            if (GoodsReceiptWriteAccess.Reject(httpContext, configuration, out var actor) is { } writeDenial)
                return writeDenial;
            if (request is null) return Results.BadRequest(new { code = "missing_body", message = "Nedostaje prijemni dokument." });

            try
            {
                var result = await service.CreateAsync(request, actor, ct);
                if (result.RequiresSimilarConfirmation)
                    return Results.Conflict(new
                    {
                        code = "similar_document",
                        similarReceiptId = result.SimilarReceiptId,
                        message = $"Postoji prijem {result.SimilarReceiptId} za istog dobavljača, lokaciju i dokument. Ako je ovo druga delimična isporuka, potvrdite da nastavite."
                    });

                if (!result.Replayed)
                {
                    await AnalyticsCacheMutationInvalidation.InvalidateAfterCommitAsync(
                        httpContext.RequestServices, AnalyticsCachePolicy.CoreFamilies, "goods-receipt",
                        loggerFactory.CreateLogger("GoodsReceiptEndpoints"));
                }

                return result.Replayed
                    ? Results.Ok(result.Receipt)
                    : Results.Created($"/api/goods-receipts/{result.Receipt.Id}", result.Receipt);
            }
            catch (GoodsReceiptException ex)
            {
                return Results.Problem(statusCode: ex.StatusCode, title: ex.Code, detail: ex.Message,
                    extensions: new Dictionary<string, object?> { ["code"] = ex.Code });
            }
        }).WithName("CreateGoodsReceipt");

        group.MapGet("", async (
            string? query,
            int? supplierId,
            DateOnly? fromDate,
            DateOnly? toDate,
            int page,
            int pageSize,
            [FromServices] GoodsReceiptService service,
            HttpContext httpContext,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            var denial = AdminAccessControl.RejectIfUnauthorized(httpContext, configuration);
            if (denial is not null) return denial;
            if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
                return Results.BadRequest(new { code = "invalid_date_range", message = "Početni datum mora biti pre završnog." });
            return Results.Ok(await service.SearchAsync(query, supplierId, fromDate, toDate, page, pageSize, ct));
        }).WithName("SearchGoodsReceipts");

        group.MapGet("/articles", async (
            int storeId,
            string? query,
            [FromServices] GoodsReceiptService service,
            HttpContext httpContext,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            var denial = AdminAccessControl.RejectIfUnauthorized(httpContext, configuration);
            if (denial is not null) return denial;
            var results = await service.SearchArticlesAsync(storeId, query, ct);
            return Results.Ok(results);
        }).WithName("SearchGoodsReceiptArticles");

        group.MapGet("/request/{requestKey:guid}", async (
            Guid requestKey,
            [FromServices] GoodsReceiptService service,
            HttpContext httpContext,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            var denial = AdminAccessControl.RejectIfUnauthorized(httpContext, configuration);
            if (denial is not null) return denial;
            var receipt = await service.GetByRequestKeyAsync(requestKey, ct);
            return receipt is null ? Results.NotFound() : Results.Ok(receipt);
        }).WithName("GetGoodsReceiptByRequestKey");

        group.MapGet("/{id:long}", async (
            long id,
            [FromServices] GoodsReceiptService service,
            HttpContext httpContext,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            var denial = AdminAccessControl.RejectIfUnauthorized(httpContext, configuration);
            if (denial is not null) return denial;
            if (id <= 0) return Results.BadRequest(new { code = "invalid_receipt_id", message = "ID prijema nije ispravan." });
            var receipt = await service.GetAsync(id, ct);
            return receipt is null ? Results.NotFound() : Results.Ok(receipt);
        }).WithName("GetGoodsReceipt");
    }
}

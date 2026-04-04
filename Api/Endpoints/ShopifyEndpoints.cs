using Api.Services;
using Api.Endpoints;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Trendplus2.Endpoints;

public static class ShopifyEndpoints
{
    // ── Request DTOs ─────────────────────────────────────────────────────────

    private sealed record ShopifyImportRequestDto(
        string ShopDomain,
        int DatasetId = 0,
        bool AutoCreateDataset = true,
        string? DatasetName = null,
        int MaxPages = 10,
        int PageSize = 250,
        bool NormalizeToTrainingProducts = true);

    private sealed record ShopifyBatchImportRequestDto(
        List<ShopifyImportRequestDto> Stores);

    public static void MapShopifyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/shopify")
            .WithTags("Shopify Integration");

        // ── POST /api/shopify/import ─────────────────────────────────────────
        group.MapPost("/import", async (
            ShopifyImportRequestDto request,
            IShopifyImportService shopifyService,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.ShopDomain))
                return Results.BadRequest(new { error = "ShopDomain je obavezan." });

            try
            {
                var importRequest = new ShopifyImportRequest(
                    ShopDomain: request.ShopDomain,
                    DatasetId: request.DatasetId,
                    AutoCreateDataset: request.AutoCreateDataset,
                    DatasetName: request.DatasetName,
                    MaxPages: request.MaxPages,
                    PageSize: request.PageSize,
                    NormalizeToTrainingProducts: request.NormalizeToTrainingProducts);

                var result = await shopifyService.ImportAsync(importRequest, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                await HandledErrorLogging.PersistHandledExceptionAsync(
                    httpContext,
                    ex,
                    "Shopify import failed",
                    ct);
                return Results.Problem(
                    title: "Shopify import failed",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .RequireRateLimiting("writes")
        .WithName("ShopifyImport")
        .WithDescription("Imports products from a single Shopify store into the training dataset. " +
            "Supports auto-creation of dataset, paginated fetching, normalization to training products.");

        // ── POST /api/shopify/import-batch ───────────────────────────────────
        group.MapPost("/import-batch", async (
            ShopifyBatchImportRequestDto request,
            IShopifyImportService shopifyService,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            if (request.Stores is null || request.Stores.Count == 0)
                return Results.BadRequest(new { error = "Stores lista ne sme biti prazna." });

            if (request.Stores.Count > 20)
                return Results.BadRequest(new { error = "Maksimalno 20 store-ova po batch importu." });

            try
            {
                var importRequests = request.Stores.Select(s => new ShopifyImportRequest(
                    ShopDomain: s.ShopDomain,
                    DatasetId: s.DatasetId,
                    AutoCreateDataset: s.AutoCreateDataset,
                    DatasetName: s.DatasetName,
                    MaxPages: s.MaxPages,
                    PageSize: s.PageSize,
                    NormalizeToTrainingProducts: s.NormalizeToTrainingProducts
                )).ToList();

                var result = await shopifyService.ImportBatchAsync(importRequests, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                await HandledErrorLogging.PersistHandledExceptionAsync(
                    httpContext,
                    ex,
                    "Shopify batch import failed",
                    ct);
                return Results.Problem(
                    title: "Shopify batch import failed",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .RequireRateLimiting("writes")
        .WithName("ShopifyBatchImport")
        .WithDescription("Imports products from multiple Shopify stores in a single request. Max 20 stores per batch.");

        // ── GET /api/shopify/datasets ────────────────────────────────────────
        group.MapGet("/datasets", async (
            OpenProductTrainingDbContext db,
            CancellationToken ct) =>
        {
            var datasets = await db.Datasets
                .AsNoTracking()
                .Where(d => d.SourceType == "shopify")
                .Select(d => new
                {
                    d.Id,
                    d.Name,
                    d.Description,
                    d.CreatedAt,
                    RawProductCount = d.RawProducts.Count(),
                    TrainingProductCount = db.Products.Count(p => p.DatasetId == d.Id)
                })
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync(ct);

            return Results.Ok(new { count = datasets.Count, datasets });
        })
        .WithName("ShopifyDatasets")
        .WithDescription("Lists all Shopify-sourced datasets with product counts.");

        // ── GET /api/shopify/products/{datasetId} ────────────────────────────
        group.MapGet("/products/{datasetId:int}", async (
            int datasetId,
            int page,
            int pageSize,
            string? search,
            OpenProductTrainingDbContext db,
            CancellationToken ct) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = db.Products
                .AsNoTracking()
                .Where(p => p.DatasetId == datasetId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLike = $"%{search.Trim()}%";
                query = query.Where(p =>
                    EF.Functions.ILike(p.Title, searchLike) ||
                    (p.Brand != null && EF.Functions.ILike(p.Brand.Name, searchLike)));
            }

            var totalCount = await query.CountAsync(ct);
            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.Id,
                    p.ExternalId,
                    p.Title,
                    Brand = p.Brand != null ? p.Brand.Name : null,
                    Category = p.Category != null ? p.Category.Name : null,
                    p.Gender,
                    p.ShoeType,
                    p.Price,
                    p.Currency,
                    p.MainImageUrl,
                    p.CreatedAt
                })
                .ToListAsync(ct);

            return Results.Ok(new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                products
            });
        })
        .WithName("ShopifyProducts")
        .WithDescription("Paginated list of normalized Shopify products for a dataset.");

        // ── DELETE /api/shopify/datasets/{datasetId} ─────────────────────────
        group.MapDelete("/datasets/{datasetId:int}", async (
            int datasetId,
            OpenProductTrainingDbContext db,
            ILogger<ShopifyImportService> logger,
            CancellationToken ct) =>
        {
            var dataset = await db.Datasets
                .FirstOrDefaultAsync(d => d.Id == datasetId && d.SourceType == "shopify", ct);

            if (dataset is null)
                return Results.NotFound(new { error = $"Shopify dataset sa ID={datasetId} ne postoji." });

            // Delete dependent rows manually (cascade might not cover all)
            var rawCount = await db.RawProducts.Where(r => r.DatasetId == datasetId).ExecuteDeleteAsync(ct);
            var prodCount = await db.Products.Where(p => p.DatasetId == datasetId).ExecuteDeleteAsync(ct);
            db.Datasets.Remove(dataset);
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Deleted Shopify dataset {Id}: {Raw} raw, {Prod} products", datasetId, rawCount, prodCount);

            return Results.Ok(new
            {
                deletedDatasetId = datasetId,
                deletedRawProducts = rawCount,
                deletedTrainingProducts = prodCount
            });
        })
        .RequireRateLimiting("writes")
        .WithName("ShopifyDeleteDataset")
        .WithDescription("Deletes a Shopify dataset and all associated raw + training products.");
    }
}

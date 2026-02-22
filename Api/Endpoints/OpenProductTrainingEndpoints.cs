using Api.Services;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Trendplus2.Endpoints
{
    public static class OpenProductTrainingEndpoints
    {
        private sealed record RecomputeLabelsRequest(
            string[]? DatasetNames,
            int MinProductsPerGroup = 10);

        private sealed record SyncProductsRequest(
            string[]? DatasetNames);

        public static void MapOpenProductTrainingEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/api/open-training")
                .WithTags("Open Product Training");

            group.MapGet("/stats", async (
                OpenProductTrainingDbContext db,
                CancellationToken ct) =>
            {
                var datasetCount = await db.Datasets.CountAsync(ct);
                var productCount = await db.Products.CountAsync(ct);
                var labelCount = await db.TrainingLabels.CountAsync(ct);
                var splitCount = await db.ProductSplits.CountAsync(ct);

                var popularityCount = await db.TrainingLabels
                    .CountAsync(l => l.LabelType == PopularityAndDealScoringService.PopularityPriorLabelType, ct);
                var dealCount = await db.TrainingLabels
                    .CountAsync(l => l.LabelType == PopularityAndDealScoringService.DealScoreLabelType, ct);

                var splitBreakdown = await db.ProductSplits
                    .GroupBy(s => s.Split)
                    .Select(g => new { split = g.Key, count = g.Count() })
                    .ToListAsync(ct);

                return Results.Ok(new
                {
                    datasetCount,
                    productCount,
                    labelCount,
                    popularityLabelCount = popularityCount,
                    dealLabelCount = dealCount,
                    splitCount,
                    splits = splitBreakdown
                });
            })
            .WithName("GetOpenProductTrainingStats")
            .WithDescription("Returns counts from the open_product_training schema.");

            group.MapGet("/datasets", async (
                OpenProductTrainingDbContext db,
                CancellationToken ct) =>
            {
                var rows = await db.Datasets
                    .AsNoTracking()
                    .OrderByDescending(d => d.CreatedAt)
                    .Select(d => new
                    {
                        d.Id,
                        d.Name,
                        d.SourceType,
                        d.Description,
                        d.License,
                        d.RawLocation,
                        d.CreatedAt,
                        ProductCount = d.Products.Count()
                    })
                    .ToListAsync(ct);

                return Results.Ok(rows);
            })
            .WithName("ListOpenProductTrainingDatasets")
            .WithDescription("Returns all registered datasets with their product counts.");

            group.MapGet("/labels/top", async (
                OpenProductTrainingDbContext db,
                string labelType = PopularityAndDealScoringService.PopularityPriorLabelType,
                int take = 20,
                CancellationToken ct = default) =>
            {
                take = Math.Clamp(take, 1, 100);

                var rows = await db.TrainingLabels
                    .AsNoTracking()
                    .Where(l => l.LabelType == labelType && l.ValueNumeric != null)
                    .OrderByDescending(l => l.ValueNumeric)
                    .Take(take)
                    .Select(l => new
                    {
                        l.ProductId,
                        Title = l.Product.Title,
                        Brand = l.Product.Brand != null ? l.Product.Brand.Name : null,
                        ShoeType = l.Product.ShoeType,
                        Price = l.Product.Price,
                        Currency = l.Product.Currency,
                        ImageUrl = l.Product.MainImageUrl,
                        l.LabelType,
                        Score = l.ValueNumeric,
                        l.CreatedAt
                    })
                    .ToListAsync(ct);

                return Results.Ok(rows);
            })
            .WithName("GetTopOpenProductTrainingLabels")
            .WithDescription("Returns top-N products by label score (popularity_prior or deal_score).");

            group.MapPost("/sync-products", async (
                SyncProductsRequest? request,
                IConfiguration configuration,
                IOpenProductTrainingSyncService syncService,
                CancellationToken ct = default) =>
            {
                var datasetNames = ResolveDatasetNames(request?.DatasetNames, configuration);
                if (datasetNames == null || datasetNames.Length == 0)
                {
                    return Results.BadRequest(new
                    {
                        error = "DatasetNames je obavezan ili mora postojati OpenProductTraining:DefaultDatasets konfiguracija."
                    });
                }

                var syncResult = await syncService.SyncFromAnalyticsAsync(datasetNames, ct);
                return Results.Ok(syncResult);
            })
            .RequireRateLimiting("writes")
            .WithName("SyncOpenProductTrainingProducts")
            .WithDescription("Imports and updates open_product_training.product rows from analytics source tables.");

            group.MapPost("/recompute-labels", async (
                RecomputeLabelsRequest? request,
                IConfiguration configuration,
                IOpenProductTrainingSyncService syncService,
                IPopularityAndDealScoringService scoringService,
                CancellationToken ct = default) =>
            {
                var datasetNames = ResolveDatasetNames(request?.DatasetNames, configuration);
                if (datasetNames == null || datasetNames.Length == 0)
                {
                    return Results.BadRequest(new
                    {
                        error = "DatasetNames je obavezan ili mora postojati OpenProductTraining:DefaultDatasets konfiguracija."
                    });
                }

                var syncResult = await syncService.SyncFromAnalyticsAsync(datasetNames, ct);
                var minProductsPerGroup = request?.MinProductsPerGroup ?? 10;
                var result = await scoringService.ComputeAndPersistAsync(datasetNames, minProductsPerGroup, ct);

                return Results.Ok(new
                {
                    result.DatasetCount,
                    result.CandidateProducts,
                    result.ScoredProducts,
                    result.GroupCount,
                    result.RemovedLabels,
                    result.InsertedLabels,
                    result.ComputedAtUtc,
                    Sync = syncResult
                });
            })
            .RequireRateLimiting("writes")
            .WithName("RecomputeOpenProductTrainingLabels")
            .WithDescription("Syncs source products and recomputes popularity_prior and deal_score labels.");
        }

        private static string[]? ResolveDatasetNames(string[]? requestDatasetNames, IConfiguration configuration)
        {
            var fromRequest = requestDatasetNames
                ?.Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (fromRequest is { Length: > 0 })
            {
                return fromRequest;
            }

            return configuration
                .GetSection("OpenProductTraining:DefaultDatasets")
                .Get<string[]>()?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}

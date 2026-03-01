using Api.Services;
using Infrastructure.DbContexts;
using Domain.Model.OpenProductTraining;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Trendplus2.Endpoints
{
    public static class OpenProductTrainingEndpoints
    {
        private const string DefaultLabelType = "popularity_prior";
        private const string DefaultRateLimitPolicy = "writes";
        private const int MaxTakeLimit = 200;
        private const int DefaultMinProductsPerGroup = 10;

        private sealed record RecomputeLabelsRequest(
            string[]? DatasetNames,
            int MinProductsPerGroup = DefaultMinProductsPerGroup);

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
                string labelType = DefaultLabelType,
                int take = 20,
                string? shoeType = null,
                string? brand = null,
                CancellationToken ct = default) =>
            {
                take = Math.Clamp(take, 1, MaxTakeLimit);

                var query = db.TrainingLabels
                    .AsNoTracking()
                    .Where(l => l.LabelType == labelType && l.ValueNumeric != null);

                query = ApplyFilters(query, shoeType, brand);

                var rows = await query
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
            .WithDescription("Returns top-N products by label score (popularity_prior or deal_score). Optionally filter by shoeType and brand.");

            group.MapGet("/shoe-types", async (
                OpenProductTrainingDbContext db,
                CancellationToken ct) =>
            {
                var rows = await db.Products
                    .AsNoTracking()
                    .Where(p => !string.IsNullOrWhiteSpace(p.ShoeType))
                    .GroupBy(p => p.ShoeType!)
                    .Select(g => new { shoeType = g.Key, productCount = g.Count() })
                    .OrderByDescending(x => x.productCount)
                    .ToListAsync(ct);

                return Results.Ok(rows);
            })
            .WithName("GetOpenProductTrainingShoeTypes")
            .WithDescription("Returns distinct shoe types with product counts from the training dataset.");

            group.MapGet("/brands", async (
                OpenProductTrainingDbContext db,
                string? shoeType = null,
                CancellationToken ct = default) =>
            {
                var query = db.Products.AsNoTracking().Where(p => p.BrandId != null && p.Brand != null);

                if (!string.IsNullOrWhiteSpace(shoeType))
                    query = query.Where(p => p.ShoeType != null && p.ShoeType.ToLower(CultureInfo.InvariantCulture) == shoeType.Trim().ToLower(CultureInfo.InvariantCulture));

                var rows = await query
                    .GroupBy(p => p.Brand!.Name)
                    .Select(g => new { brand = g.Key, productCount = g.Count() })
                    .OrderByDescending(x => x.productCount)
                    .Take(50)
                    .ToListAsync(ct);

                return Results.Ok(rows);
            })
            .WithName("GetOpenProductTrainingBrands")
            .WithDescription("Returns top brands with product counts, optionally filtered by shoe type.");

            group.MapGet("/diagnostics", async (
                OpenProductTrainingDbContext db,
                string labelType = "popularity_prior",
                CancellationToken ct = default) =>
            {
                // Score distribution histogram (10 buckets: 0-10, 10-20, …, 90-100)
                var allScores = await db.TrainingLabels
                    .AsNoTracking()
                    .Where(l => l.LabelType == labelType && l.ValueNumeric != null)
                    .Select(l => l.ValueNumeric!.Value)
                    .ToListAsync(ct);

                var histogram = Enumerable.Range(0, 10).Select(i =>
                {
                    var lo = i * 10m;
                    var hi = lo + 10m;
                    return new
                    {
                        rangeLabel = $"{lo:0}-{hi:0}",
                        lo,
                        hi,
                        count = allScores.Count(s => s >= lo && (i == 9 ? s <= hi : s < hi))
                    };
                }).ToList();

                // Data quality: how many products have rating / reviews / price
                var quality = await db.Products
                    .AsNoTracking()
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        total          = g.Count(),
                        withRating     = g.Count(p => p.AvgRating != null && p.AvgRating > 0),
                        withReviews    = g.Count(p => p.ReviewCount != null && p.ReviewCount > 0),
                        withPrice      = g.Count(p => p.Price != null && p.Price > 0),
                        withBrand      = g.Count(p => p.BrandId != null),
                        withShoeType   = g.Count(p => p.ShoeType != null && p.ShoeType != ""),
                        withRatingAndReviews = g.Count(p =>
                            p.AvgRating != null && p.AvgRating > 0 &&
                            p.ReviewCount != null && p.ReviewCount > 0),
                    })
                    .FirstOrDefaultAsync(ct);

                // Top groups by product count
                var topGroups = await db.Products
                    .AsNoTracking()
                    .Where(p => p.BrandId != null && p.ShoeType != null)
                    .GroupBy(p => new { brand = p.Brand!.Name, shoeType = p.ShoeType! })
                    .Select(g => new
                    {
                        brand       = g.Key.brand,
                        shoeType    = g.Key.shoeType,
                        productCount = g.Count(),
                        withRating  = g.Count(p => p.AvgRating != null && p.AvgRating > 0),
                    })
                    .OrderByDescending(x => x.productCount)
                    .Take(20)
                    .ToListAsync(ct);

                // Score summary stats
                var scoreStats = allScores.Count == 0 ? null : new
                {
                    count  = allScores.Count,
                    min    = Math.Round(allScores.Min(), 1),
                    max    = Math.Round(allScores.Max(), 1),
                    avg    = Math.Round(allScores.Average(), 1),
                    median = Math.Round(Percentile(allScores, 0.5), 1),
                    p25    = Math.Round(Percentile(allScores, 0.25), 1),
                    p75    = Math.Round(Percentile(allScores, 0.75), 1),
                };

                return Results.Ok(new { histogram, quality, topGroups, scoreStats });
            })
            .WithName("GetOpenProductTrainingDiagnostics")
            .WithDescription("Returns score distribution, data quality and top group stats for the training dataset.");

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
            .RequireRateLimiting(DefaultRateLimitPolicy)
            .WithName("SyncOpenProductTrainingProducts")
            .WithDescription("Imports and updates open_product_training.product rows from analytics source tables.");

            group.MapPost("/recompute-labels", async (
                RecomputeLabelsRequest? request,
                IConfiguration configuration,
                IOpenProductTrainingSyncService syncService,
                IPopularityAndDealScoringService scoringService,
                IMemoryCache cache,
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
                var minProductsPerGroup = request?.MinProductsPerGroup ?? DefaultMinProductsPerGroup;
                var result = await scoringService.ComputeAndPersistAsync(datasetNames, minProductsPerGroup, ct);
                cache.Remove(OpenProductTrainingSignalProvider.RuntimeGroupSignalsCacheKey);

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
            .RequireRateLimiting(DefaultRateLimitPolicy)
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
                return fromRequest;

            return configuration
                .GetSection("OpenProductTraining:DefaultDatasets")
                .Get<string[]>()?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static IQueryable<TrainingProduct> ApplyFilters(IQueryable<TrainingProduct> query, string? shoeType, string? brand)
        {
            if (!string.IsNullOrWhiteSpace(shoeType))
            {
                query = query.Where(p => p.ShoeType != null &&
                    p.ShoeType.ToLower(CultureInfo.InvariantCulture) == shoeType.Trim().ToLower(CultureInfo.InvariantCulture));
            }

            if (!string.IsNullOrWhiteSpace(brand))
            {
                query = query.Where(p => p.Brand != null &&
                    p.Brand.Name.ToLower(CultureInfo.InvariantCulture) == brand.Trim().ToLower(CultureInfo.InvariantCulture));
            }

            return query;
        }

        private static IQueryable<TrainingLabel> ApplyFilters(IQueryable<TrainingLabel> query, string? shoeType, string? brand)
        {
            if (!string.IsNullOrWhiteSpace(shoeType))
            {
                query = query.Where(l => l.Product != null && l.Product.ShoeType != null &&
                    l.Product.ShoeType.ToLower(CultureInfo.InvariantCulture) == shoeType.Trim().ToLower(CultureInfo.InvariantCulture));
            }

            if (!string.IsNullOrWhiteSpace(brand))
            {
                query = query.Where(l => l.Product != null && l.Product.Brand != null &&
                    l.Product.Brand.Name.ToLower(CultureInfo.InvariantCulture) == brand.Trim().ToLower(CultureInfo.InvariantCulture));
            }

            return query;
        }

        private static double Percentile(List<decimal> sorted, double p)
        {
            if (sorted.Count == 0) return 0;
            var ordered = sorted.OrderBy(x => x).ToList();
            var idx = p * (ordered.Count - 1);
            var lo  = (int)Math.Floor(idx);
            var hi  = (int)Math.Ceiling(idx);
            if (lo == hi) return (double)ordered[lo];
            return (double)(ordered[lo] + (decimal)(idx - lo) * (ordered[hi] - ordered[lo]));
        }
    }
}

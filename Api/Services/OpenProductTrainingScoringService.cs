using Domain.Model.OpenProductTraining;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Api.Services
{
    public interface IPopularityAndDealScoringService
    {
        Task<PopularityDealComputationResult> ComputeAndPersistAsync(
            IEnumerable<string> datasetNamesForTraining,
            int minProductsPerGroup = 10,
            CancellationToken ct = default);
    }

    public sealed class PopularityDealComputationResult
    {
        public int DatasetCount { get; init; }
        public int CandidateProducts { get; init; }
        public int ScoredProducts { get; init; }
        public int GroupCount { get; init; }
        public int RemovedLabels { get; init; }
        public int InsertedLabels { get; init; }
        public DateTime ComputedAtUtc { get; init; }
    }

    public interface IOpenProductTrainingSignalProvider
    {
        Task<RuntimeScoringSignals> ResolveAsync(
            string? brand,
            string? shoeType,
            decimal? price,
            CancellationToken ct = default);
    }

    public sealed record RuntimeScoringSignals(
        decimal PopularityPriorScore,
        decimal DealScore,
        decimal? TypicalPrice,
        bool HasTrainingSignal);

    public sealed class PopularityAndDealScoringService : IPopularityAndDealScoringService
    {
        public const string PopularityPriorLabelType = "popularity_prior";
        public const string DealScoreLabelType = "deal_score";

        private readonly OpenProductTrainingDbContext _db;
        private readonly ILogger<PopularityAndDealScoringService> _logger;

        public PopularityAndDealScoringService(
            OpenProductTrainingDbContext db,
            ILogger<PopularityAndDealScoringService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<PopularityDealComputationResult> ComputeAndPersistAsync(
            IEnumerable<string> datasetNamesForTraining,
            int minProductsPerGroup = 10,
            CancellationToken ct = default)
        {
            var normalizedDatasetNames = datasetNamesForTraining
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (normalizedDatasetNames.Length == 0)
            {
                _logger.LogWarning("Popularity/deal scoring skipped because no dataset names were provided.");
                return new PopularityDealComputationResult
                {
                    DatasetCount = 0,
                    CandidateProducts = 0,
                    ScoredProducts = 0,
                    GroupCount = 0,
                    RemovedLabels = 0,
                    InsertedLabels = 0,
                    ComputedAtUtc = DateTime.UtcNow
                };
            }

            var datasetRows = await _db.Datasets
                .AsNoTracking()
                .Select(d => new { d.Id, d.Name })
                .ToListAsync(ct);

            var datasetIds = datasetRows
                .Where(d => normalizedDatasetNames.Contains(d.Name, StringComparer.OrdinalIgnoreCase))
                .Select(d => d.Id)
                .Distinct()
                .ToList();

            if (datasetIds.Count == 0)
            {
                _logger.LogWarning(
                    "Popularity/deal scoring skipped because requested datasets were not found. Requested={Datasets}",
                    string.Join(", ", normalizedDatasetNames));
                return new PopularityDealComputationResult
                {
                    DatasetCount = 0,
                    CandidateProducts = 0,
                    ScoredProducts = 0,
                    GroupCount = 0,
                    RemovedLabels = 0,
                    InsertedLabels = 0,
                    ComputedAtUtc = DateTime.UtcNow
                };
            }

            var candidates = await _db.Products
                .AsNoTracking()
                .Where(p =>
                    datasetIds.Contains(p.DatasetId) &&
                    p.BrandId != null &&
                    p.Price != null &&
                    !string.IsNullOrWhiteSpace(p.ShoeType))
                .Select(p => new ProductScoreCandidate
                {
                    ProductId = p.Id,
                    BrandId = p.BrandId!.Value,
                    ShoeType = p.ShoeType!,
                    Price = p.Price!.Value,
                    AvgRating = p.ReviewStats != null && p.ReviewStats.AvgRating != null
                        ? p.ReviewStats.AvgRating
                        : p.AvgRating,
                    ReviewCount = p.ReviewStats != null && p.ReviewStats.RatingCount != null
                        ? p.ReviewStats.RatingCount
                        : p.ReviewCount
                })
                .ToListAsync(ct);

            if (candidates.Count == 0)
            {
                _logger.LogWarning("Popularity/deal scoring found no candidate products for selected datasets.");
                return new PopularityDealComputationResult
                {
                    DatasetCount = datasetIds.Count,
                    CandidateProducts = 0,
                    ScoredProducts = 0,
                    GroupCount = 0,
                    RemovedLabels = 0,
                    InsertedLabels = 0,
                    ComputedAtUtc = DateTime.UtcNow
                };
            }

            minProductsPerGroup = Math.Max(minProductsPerGroup, 1);

            var productsForPopularity = candidates
                .Where(x =>
                    x.AvgRating.HasValue &&
                    x.ReviewCount.HasValue &&
                    x.ReviewCount.Value >= 0)
                .ToList();

            if (productsForPopularity.Count == 0)
            {
                _logger.LogWarning("Popularity/deal scoring found no products with rating/review data.");
                return new PopularityDealComputationResult
                {
                    DatasetCount = datasetIds.Count,
                    CandidateProducts = candidates.Count,
                    ScoredProducts = 0,
                    GroupCount = 0,
                    RemovedLabels = 0,
                    InsertedLabels = 0,
                    ComputedAtUtc = DateTime.UtcNow
                };
            }

            var allLogReviews = productsForPopularity
                .Select(x => Math.Log(1.0 + x.ReviewCount!.Value))
                .ToList();

            var globalMinLog = allLogReviews.Min();
            var globalMaxLog = allLogReviews.Max();
            var globalDenomLog = Math.Max(globalMaxLog - globalMinLog, 1e-9);

            var groupStats = productsForPopularity
                .GroupBy(x => new BrandCategoryKey(
                    x.BrandId,
                    NormalizeKeyPart(x.ShoeType)))
                .Where(g => g.Count() >= minProductsPerGroup)
                .Select(g => new BrandCategoryStats
                {
                    BrandId = g.Key.BrandId,
                    ShoeType = g.Key.ShoeType,
                    AvgRating = g.Average(x => x.AvgRating!.Value),
                    MedianLogReviews = Median(g.Select(x => Math.Log(1.0 + x.ReviewCount!.Value))),
                    MedianPrice = Median(g.Select(x => x.Price)),
                    ProductCount = g.Count()
                })
                .ToList();

            var statsByKey = groupStats.ToDictionary(
                x => new BrandCategoryKey(x.BrandId, x.ShoeType),
                x => x);

            var now = DateTime.UtcNow;
            var labelsToAdd = new List<TrainingLabel>(candidates.Count * 2);

            foreach (var product in candidates)
            {
                var key = new BrandCategoryKey(product.BrandId, NormalizeKeyPart(product.ShoeType));
                if (!statsByKey.TryGetValue(key, out var stats))
                    continue;

                var ratingNorm = Clamp01((double)((stats.AvgRating - 3.0m) / 2.0m));
                var reviewNorm = Clamp01((stats.MedianLogReviews - globalMinLog) / globalDenomLog);

                var popularityPrior = 0.6 * ratingNorm + 0.4 * reviewNorm;
                var popularityPriorScore = Math.Round((decimal)(popularityPrior * 100.0), 2);

                decimal dealScore = 0m;
                if (stats.MedianPrice > 0)
                {
                    var relative = (double)((stats.MedianPrice - product.Price) / stats.MedianPrice);
                    var dealNorm = relative <= 0
                        ? 0
                        : relative >= 0.4
                            ? 1
                            : relative / 0.4;

                    dealScore = Math.Round((decimal)(Clamp01(dealNorm) * 100.0), 2);
                }

                labelsToAdd.Add(new TrainingLabel
                {
                    ProductId = product.ProductId,
                    LabelType = PopularityPriorLabelType,
                    ValueNumeric = popularityPriorScore,
                    CreatedAt = now
                });

                labelsToAdd.Add(new TrainingLabel
                {
                    ProductId = product.ProductId,
                    LabelType = DealScoreLabelType,
                    ValueNumeric = dealScore,
                    CreatedAt = now
                });
            }

            if (labelsToAdd.Count == 0)
            {
                _logger.LogWarning("Popularity/deal scoring produced no labels (group thresholds likely too strict).");
                return new PopularityDealComputationResult
                {
                    DatasetCount = datasetIds.Count,
                    CandidateProducts = candidates.Count,
                    ScoredProducts = 0,
                    GroupCount = statsByKey.Count,
                    RemovedLabels = 0,
                    InsertedLabels = 0,
                    ComputedAtUtc = now
                };
            }

            var targetProductIds = labelsToAdd
                .Select(x => x.ProductId)
                .Distinct()
                .ToList();

            var existing = await _db.TrainingLabels
                .Where(x =>
                    targetProductIds.Contains(x.ProductId) &&
                    (x.LabelType == PopularityPriorLabelType || x.LabelType == DealScoreLabelType))
                .ToListAsync(ct);

            _db.TrainingLabels.RemoveRange(existing);
            await _db.SaveChangesAsync(ct);

            await _db.TrainingLabels.AddRangeAsync(labelsToAdd, ct);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Popularity/deal scoring completed. datasets={Datasets}, candidates={Candidates}, groups={Groups}, removed={Removed}, inserted={Inserted}",
                datasetIds.Count,
                candidates.Count,
                statsByKey.Count,
                existing.Count,
                labelsToAdd.Count);

            return new PopularityDealComputationResult
            {
                DatasetCount = datasetIds.Count,
                CandidateProducts = candidates.Count,
                ScoredProducts = targetProductIds.Count,
                GroupCount = statsByKey.Count,
                RemovedLabels = existing.Count,
                InsertedLabels = labelsToAdd.Count,
                ComputedAtUtc = now
            };
        }

        private static double Median(IEnumerable<double> values)
        {
            var ordered = values.OrderBy(x => x).ToArray();
            if (ordered.Length == 0)
                return 0;

            var mid = ordered.Length / 2;
            return ordered.Length % 2 == 0
                ? (ordered[mid - 1] + ordered[mid]) / 2.0
                : ordered[mid];
        }

        private static decimal Median(IEnumerable<decimal> values)
        {
            var ordered = values.OrderBy(x => x).ToArray();
            if (ordered.Length == 0)
                return 0m;

            var mid = ordered.Length / 2;
            return ordered.Length % 2 == 0
                ? (ordered[mid - 1] + ordered[mid]) / 2m
                : ordered[mid];
        }

        private static double Clamp01(double value)
            => value < 0 ? 0 : (value > 1 ? 1 : value);

        private static string NormalizeKeyPart(string? value)
            => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

        private sealed class ProductScoreCandidate
        {
            public long ProductId { get; init; }
            public int BrandId { get; init; }
            public string ShoeType { get; init; } = string.Empty;
            public decimal Price { get; init; }
            public decimal? AvgRating { get; init; }
            public int? ReviewCount { get; init; }
        }

        private sealed record BrandCategoryKey(int BrandId, string ShoeType);

        private sealed class BrandCategoryStats
        {
            public int BrandId { get; init; }
            public string ShoeType { get; init; } = string.Empty;
            public decimal AvgRating { get; init; }
            public double MedianLogReviews { get; init; }
            public decimal MedianPrice { get; init; }
            public int ProductCount { get; init; }
        }
    }

    public sealed class OpenProductTrainingSignalProvider : IOpenProductTrainingSignalProvider
    {
        private sealed class RuntimeGroupStats
        {
            public decimal TypicalPrice { get; init; }
            public decimal PopularityPriorScore { get; init; }
        }

        private const string CacheKey = "open-product-training:runtime-group-signals:v1";
        private static readonly RuntimeScoringSignals EmptySignals = new(0m, 0m, null, false);

        private readonly OpenProductTrainingDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly ILogger<OpenProductTrainingSignalProvider> _logger;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(15);

        public OpenProductTrainingSignalProvider(
            OpenProductTrainingDbContext db,
            IMemoryCache cache,
            ILogger<OpenProductTrainingSignalProvider> logger)
        {
            _db = db;
            _cache = cache;
            _logger = logger;
        }

        public async Task<RuntimeScoringSignals> ResolveAsync(
            string? brand,
            string? shoeType,
            decimal? price,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(shoeType))
                return EmptySignals;

            var statsMap = await GetOrBuildStatsMapAsync(ct);
            if (statsMap.Count == 0)
                return EmptySignals;

            var typedKey = BuildKey(brand, shoeType);
            if (!statsMap.TryGetValue(typedKey, out var stats))
            {
                var fallbackKey = BuildKey("*", shoeType);
                if (!statsMap.TryGetValue(fallbackKey, out stats))
                    return EmptySignals;
            }

            var popularityPrior = ClampScore(stats.PopularityPriorScore);
            var dealScore = 0m;

            if (price.HasValue && stats.TypicalPrice > 0)
            {
                dealScore = ComputeDealScore(price.Value, stats.TypicalPrice);
            }

            return new RuntimeScoringSignals(popularityPrior, dealScore, stats.TypicalPrice, true);
        }

        private async Task<Dictionary<string, RuntimeGroupStats>> GetOrBuildStatsMapAsync(CancellationToken ct)
        {
            if (_cache.TryGetValue(CacheKey, out Dictionary<string, RuntimeGroupStats>? cached) && cached is not null)
                return cached;

            try
            {
                var rows = await _db.Products
                    .AsNoTracking()
                    .Where(p => p.BrandId != null && p.Price != null && !string.IsNullOrWhiteSpace(p.ShoeType))
                    .Select(p => new
                    {
                        Brand = p.Brand != null ? p.Brand.Name : null,
                        p.ShoeType,
                        Price = p.Price!.Value,
                        PopularityPrior = p.TrainingLabels
                            .Where(l => l.LabelType == PopularityAndDealScoringService.PopularityPriorLabelType && l.ValueNumeric != null)
                            .OrderByDescending(l => l.CreatedAt)
                            .ThenByDescending(l => l.Id)
                            .Select(l => l.ValueNumeric)
                            .FirstOrDefault()
                    })
                    .ToListAsync(ct);

                var map = new Dictionary<string, RuntimeGroupStats>(StringComparer.Ordinal);

                foreach (var group in rows.GroupBy(x => BuildKey(x.Brand, x.ShoeType)))
                {
                    var prices = group.Select(x => x.Price).ToList();
                    if (prices.Count == 0)
                        continue;

                    var popularityValues = group
                        .Where(x => x.PopularityPrior.HasValue)
                        .Select(x => x.PopularityPrior!.Value)
                        .ToList();

                    map[group.Key] = new RuntimeGroupStats
                    {
                        TypicalPrice = Median(prices),
                        PopularityPriorScore = popularityValues.Count == 0 ? 0m : popularityValues.Average()
                    };
                }

                // fallback by shoe type across all brands
                foreach (var group in rows.GroupBy(x => BuildKey("*", x.ShoeType)))
                {
                    if (map.ContainsKey(group.Key))
                        continue;

                    var prices = group.Select(x => x.Price).ToList();
                    if (prices.Count == 0)
                        continue;

                    var popularityValues = group
                        .Where(x => x.PopularityPrior.HasValue)
                        .Select(x => x.PopularityPrior!.Value)
                        .ToList();

                    map[group.Key] = new RuntimeGroupStats
                    {
                        TypicalPrice = Median(prices),
                        PopularityPriorScore = popularityValues.Count == 0 ? 0m : popularityValues.Average()
                    };
                }

                _cache.Set(CacheKey, map, _cacheDuration);
                return map;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to load open-product-training runtime signals. Falling back to base scoring only.");

                return new Dictionary<string, RuntimeGroupStats>(StringComparer.Ordinal);
            }
        }

        private static decimal ComputeDealScore(decimal currentPrice, decimal typicalPrice)
        {
            if (typicalPrice <= 0)
                return 0m;

            var relative = (double)((typicalPrice - currentPrice) / typicalPrice);
            double normalized;

            if (relative <= 0)
            {
                normalized = 0;
            }
            else if (relative >= 0.4)
            {
                normalized = 1;
            }
            else
            {
                normalized = relative / 0.4;
            }

            return Math.Round((decimal)(Clamp01(normalized) * 100.0), 2);
        }

        private static decimal Median(IEnumerable<decimal> values)
        {
            var ordered = values.OrderBy(x => x).ToArray();
            if (ordered.Length == 0)
                return 0m;

            var mid = ordered.Length / 2;
            return ordered.Length % 2 == 0
                ? (ordered[mid - 1] + ordered[mid]) / 2m
                : ordered[mid];
        }

        private static decimal ClampScore(decimal value)
            => value < 0 ? 0 : (value > 100 ? 100 : value);

        private static double Clamp01(double value)
            => value < 0 ? 0 : (value > 1 ? 1 : value);

        private static string BuildKey(string? brand, string? shoeType)
        {
            var normalizedBrand = string.IsNullOrWhiteSpace(brand)
                ? "*"
                : brand.Trim().ToLowerInvariant();
            var normalizedType = string.IsNullOrWhiteSpace(shoeType)
                ? string.Empty
                : shoeType.Trim().ToLowerInvariant();

            return $"{normalizedBrand}|{normalizedType}";
        }
    }
}

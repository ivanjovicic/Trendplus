using Domain.Model.OpenProductTraining;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

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

            var groupStats = productsForPopularity
                .GroupBy(x => new BrandCategoryKey(
                    x.BrandId,
                    NormalizeKeyPart(x.ShoeType)))
                .Where(g => g.Count() >= minProductsPerGroup)
                .Select(g => new BrandCategoryStats
                {
                    BrandId = g.Key.BrandId,
                    ShoeType = g.Key.ShoeType,
                    AvgRating = BayesianAverage(g.Select(x => x.AvgRating!.Value), 3.5m, 10),
                    MedianLogReviews = WinsorizedMedianLog(g.Select(x => x.ReviewCount!.Value)),
                    MedianPrice = Median(g.Select(x => x.Price)),
                    ProductCount = g.Count()
                })
                .ToList();

            var statsByKey = groupStats.ToDictionary(
                x => new BrandCategoryKey(x.BrandId, x.ShoeType),
                x => x);

            var globalMinRating = groupStats.Count > 0 ? groupStats.Min(g => g.AvgRating) : 3.0m;
            var globalMaxRating = groupStats.Count > 0 ? groupStats.Max(g => g.AvgRating) : 5.0m;

            var now = DateTime.UtcNow;
            var labelsToAdd = new List<TrainingLabel>(candidates.Count * 2);

            foreach (var product in candidates)
            {
                var key = new BrandCategoryKey(product.BrandId, NormalizeKeyPart(product.ShoeType));
                if (!statsByKey.TryGetValue(key, out var stats))
                    continue;

                var ratingPercentile = PercentileNormalize(stats.AvgRating, globalMinRating, globalMaxRating);
                var reviewPercentile = PercentileNormalize(stats.MedianLogReviews, globalMinLog, globalMaxLog);

                var rawPopularity = Clamp01(0.65 * ratingPercentile + 0.35 * reviewPercentile);
                var popularityPriorScore = Math.Round(5.0m + (decimal)(rawPopularity * 95.0), 2);

                decimal dealScore = 0m;
                if (stats.MedianPrice > 0)
                {
                    var relative = (double)((stats.MedianPrice - product.Price) / stats.MedianPrice);
                    var dealNorm = relative <= 0
                        ? 0
                        : relative >= 0.30
                            ? 1
                            : relative / 0.30;

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

        private static decimal BayesianAverage(IEnumerable<decimal> ratings, decimal priorMean, int priorWeight)
        {
            var count = ratings.Count();
            if (count == 0) return priorMean;
            var sum = ratings.Sum();
            return (sum + priorMean * priorWeight) / (count + priorWeight);
        }

        private static double WinsorizedMedianLog(IEnumerable<int> reviewCounts)
        {
            var logs = reviewCounts.Select(x => Math.Log(1.0 + x)).OrderBy(x => x).ToList();
            if (logs.Count == 0) return 0;

            var lowerIndex = (int)Math.Floor(logs.Count * 0.05);
            var upperIndex = (int)Math.Ceiling(logs.Count * 0.95) - 1;
            lowerIndex = Math.Clamp(lowerIndex, 0, logs.Count - 1);
            upperIndex = Math.Clamp(upperIndex, 0, logs.Count - 1);

            var lower = logs[lowerIndex];
            var upper = logs[upperIndex];
            var trimmed = logs.Where(x => x >= lower && x <= upper).ToList();
            return trimmed.Count == 0 ? logs[logs.Count / 2] : trimmed.Average();
        }

        private static double PercentileNormalize(decimal value, decimal min, decimal max)
        {
            var denom = max - min;
            if (denom == 0m) return 0.5;
            return Clamp01((double)((value - min) / denom));
        }

        private static double PercentileNormalize(double value, double min, double max)
        {
            var denom = max - min;
            if (Math.Abs(denom) < 1e-9) return 0.5;
            return Clamp01((value - min) / denom);
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
            public int SampleSize { get; init; }
        }

        public const string RuntimeGroupSignalsCacheKey = "open-product-training:runtime-group-signals:v2";
        private const int MinSampleSizeForTrainingSignal = 10;
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
            var typicalPrice = stats.TypicalPrice > 0 ? stats.TypicalPrice : (decimal?)null;
            var dealScore = 0m;

            if (price.HasValue && typicalPrice.HasValue)
            {
                dealScore = ComputeDealScore(price.Value, typicalPrice.Value);
            }

            var hasTrainingSignal = stats.SampleSize >= MinSampleSizeForTrainingSignal && popularityPrior > 0m;
            return new RuntimeScoringSignals(popularityPrior, dealScore, typicalPrice, hasTrainingSignal);
        }

        private async Task<Dictionary<string, RuntimeGroupStats>> GetOrBuildStatsMapAsync(CancellationToken ct)
        {
            if (_cache.TryGetValue(RuntimeGroupSignalsCacheKey, out Dictionary<string, RuntimeGroupStats>? cached) && cached is not null)
                return cached;

            var cs = _db.Database.GetConnectionString();
            if (string.IsNullOrWhiteSpace(cs))
                return new Dictionary<string, RuntimeGroupStats>(StringComparer.Ordinal);

            const string sql = @"
                SELECT
                    brand_key,
                    shoe_type_key,
                    popularity_prior_score,
                    typical_price,
                    sample_size
                FROM vw_brand_shoe_runtime_priors;";

            try
            {
                var map = new Dictionary<string, RuntimeGroupStats>(StringComparer.Ordinal);
                var rows = new List<(string ShoeTypeKey, decimal PopularityPrior, decimal TypicalPrice, int SampleSize)>();

                await using var conn = new NpgsqlConnection(cs);
                await conn.OpenAsync(ct);
                await using var cmd = new NpgsqlCommand(sql, conn);
                await using var r = await cmd.ExecuteReaderAsync(ct);

                while (await r.ReadAsync(ct))
                {
                    var brandKey = r.IsDBNull(0) ? string.Empty : r.GetString(0);
                    var shoeTypeKey = r.IsDBNull(1) ? string.Empty : r.GetString(1);
                    if (string.IsNullOrWhiteSpace(shoeTypeKey))
                        continue;

                    brandKey = brandKey.Trim().ToLowerInvariant();
                    shoeTypeKey = shoeTypeKey.Trim().ToLowerInvariant();

                    var popularityPrior = r.IsDBNull(2) ? 0m : r.GetDecimal(2);
                    var typicalPrice = r.IsDBNull(3) ? 0m : r.GetDecimal(3);
                    var sampleSize = r.IsDBNull(4) ? 0 : r.GetInt32(4);

                    map[$"{brandKey}|{shoeTypeKey}"] = new RuntimeGroupStats
                    {
                        TypicalPrice = typicalPrice,
                        PopularityPriorScore = popularityPrior,
                        SampleSize = sampleSize
                    };

                    rows.Add((shoeTypeKey, popularityPrior, typicalPrice, sampleSize));
                }

                // Fallback by shoe type across all brands.
                foreach (var group in rows.GroupBy(x => x.ShoeTypeKey, StringComparer.Ordinal))
                {
                    var total = group.Sum(x => Math.Max(x.SampleSize, 0));
                    if (total <= 0)
                        continue;

                    var weightedPopularity = group.Sum(x => x.PopularityPrior * x.SampleSize) / total;
                    var typical = WeightedMedian(group.Select(x => (x.TypicalPrice, x.SampleSize)));

                    map[$"*|{group.Key}"] = new RuntimeGroupStats
                    {
                        TypicalPrice = typical,
                        PopularityPriorScore = Math.Round(weightedPopularity, 2),
                        SampleSize = total
                    };
                }

                _cache.Set(RuntimeGroupSignalsCacheKey, map, _cacheDuration);
                return map;
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable || ex.SqlState == PostgresErrorCodes.UndefinedColumn)
            {
                _logger.LogWarning(ex, "Open-product-training runtime priors view missing.");
                return new Dictionary<string, RuntimeGroupStats>(StringComparer.Ordinal);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to load open-product-training runtime signals. Falling back to base scoring only.");

                return new Dictionary<string, RuntimeGroupStats>(StringComparer.Ordinal);
            }
        }

        private static decimal ClampScore(decimal value)
            => value < 0m ? 0m : (value > 100m ? 100m : value);

        private static decimal WeightedMedian(IEnumerable<(decimal Value, int Weight)> values)
        {
            var items = values
                .Where(x => x.Weight > 0 && x.Value > 0)
                .OrderBy(x => x.Value)
                .ToList();

            if (items.Count == 0)
                return 0m;

            var totalWeight = items.Sum(x => x.Weight);
            var threshold = (totalWeight + 1) / 2;
            var cumulative = 0;

            foreach (var item in items)
            {
                cumulative += item.Weight;
                if (cumulative >= threshold)
                    return Math.Round(item.Value, 2, MidpointRounding.AwayFromZero);
            }

            return Math.Round(items[^1].Value, 2, MidpointRounding.AwayFromZero);
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
            else if (relative >= 0.30)   // 30% below typical = full deal (was 40%)
            {
                normalized = 1;
            }
            else
            {
                normalized = relative / 0.30;
            }

            return Math.Round((decimal)(Clamp01(normalized) * 100.0), 2);
        }

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

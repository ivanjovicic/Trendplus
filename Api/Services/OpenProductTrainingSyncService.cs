using Domain.Model;
using Domain.Model.OpenProductTraining;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Api.Services
{
    public interface IOpenProductTrainingSyncService
    {
        Task<OpenProductTrainingSyncResult> SyncFromAnalyticsAsync(
            IEnumerable<string> datasetNames,
            CancellationToken ct = default);
    }

    public sealed class OpenProductTrainingSyncResult
    {
        public int DatasetCount { get; init; }
        public int SourceRowsRead { get; init; }
        public int ProductsInserted { get; init; }
        public int ProductsUpdated { get; init; }
        public int BrandsCreated { get; init; }
        public int CategoriesCreated { get; init; }
        public int UnsupportedDatasets { get; init; }
        public DateTime SyncedAtUtc { get; init; }
    }

    public sealed class OpenProductTrainingSyncService : IOpenProductTrainingSyncService
    {
        private static readonly char[] CategorySeparators = { ' ', '_', '-' };

        private readonly OpenProductTrainingDbContext _trainingDb;
        private readonly AnalyticsDbContext _analyticsDb;
        private readonly ILogger<OpenProductTrainingSyncService> _logger;

        public OpenProductTrainingSyncService(
            OpenProductTrainingDbContext trainingDb,
            AnalyticsDbContext analyticsDb,
            ILogger<OpenProductTrainingSyncService> logger)
        {
            _trainingDb = trainingDb;
            _analyticsDb = analyticsDb;
            _logger = logger;
        }

        public async Task<OpenProductTrainingSyncResult> SyncFromAnalyticsAsync(
            IEnumerable<string> datasetNames,
            CancellationToken ct = default)
        {
            var normalizedNames = datasetNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (normalizedNames.Length == 0)
            {
                return new OpenProductTrainingSyncResult
                {
                    SyncedAtUtc = DateTime.UtcNow
                };
            }

            var datasets = await _trainingDb.Datasets
                .Where(d => normalizedNames.Contains(d.Name))
                .ToListAsync(ct);

            var existingNames = datasets
                .Select(d => d.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missingNames = normalizedNames
                .Where(name => !existingNames.Contains(name))
                .ToArray();

            if (missingNames.Length > 0)
            {
                var now = DateTime.UtcNow;
                var createdDatasets = missingNames
                    .Select(name => new TrainingDataset
                    {
                        Name = name,
                        SourceType = InferDatasetSourceType(name),
                        Description = GetDatasetDescription(name),
                        License = "Unknown",
                        CreatedAt = now
                    })
                    .ToList();

                _trainingDb.Datasets.AddRange(createdDatasets);
                await _trainingDb.SaveChangesAsync(ct);

                datasets.AddRange(createdDatasets);

                _logger.LogInformation(
                    "Open training sync auto-created missing datasets: {Datasets}",
                    string.Join(", ", missingNames));
            }

            if (datasets.Count == 0)
            {
                _logger.LogWarning(
                    "Open training sync skipped because no matching dataset rows were found. Requested={Datasets}",
                    string.Join(", ", normalizedNames));

                return new OpenProductTrainingSyncResult
                {
                    SyncedAtUtc = DateTime.UtcNow
                };
            }

            var brands = await _trainingDb.Brands.ToListAsync(ct);
            var categories = await _trainingDb.Categories.ToListAsync(ct);

            var brandMap = new Dictionary<string, TrainingBrand>(StringComparer.Ordinal);
            var categoryMap = new Dictionary<string, TrainingCategory>(StringComparer.Ordinal);

            foreach (var brand in brands)
            {
                var key = NormalizeLookupKey(brand.Name);
                if (!brandMap.ContainsKey(key))
                {
                    brandMap[key] = brand;
                }
            }

            foreach (var category in categories)
            {
                var key = NormalizeLookupKey(category.Name);
                if (!categoryMap.ContainsKey(key))
                {
                    categoryMap[key] = category;
                }
            }

            var sourceRowsRead = 0;
            var productsInserted = 0;
            var productsUpdated = 0;
            var brandsCreated = 0;
            var categoriesCreated = 0;
            var unsupportedDatasets = 0;

            foreach (var dataset in datasets)
            {
                var sourceType = ResolveSourceType(dataset.SourceType, dataset.Name);
                var sourceRows = await LoadSourceRowsAsync(sourceType, ct);

                if (sourceRows is null)
                {
                    unsupportedDatasets++;
                    continue;
                }

                var preparedRows = sourceRows
                    .Where(x => !string.IsNullOrWhiteSpace(x.ExternalId))
                    .GroupBy(x => x.ExternalId.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.OrderByDescending(x => x.UpdatedAtUtc).First())
                    .ToList();

                sourceRowsRead += preparedRows.Count;

                if (preparedRows.Count == 0)
                {
                    continue;
                }

                var externalIds = preparedRows
                    .Select(x => x.ExternalId.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var existingList = await _trainingDb.Products
                    .Where(p => p.DatasetId == dataset.Id && externalIds.Contains(p.ExternalId))
                    .ToListAsync(ct);

                var existingByExternalId = existingList
                    .ToDictionary(x => x.ExternalId, StringComparer.OrdinalIgnoreCase);

                foreach (var row in preparedRows)
                {
                    var externalId = row.ExternalId.Trim();
                    var shoeType = NormalizeShoeType(row.ShoeType, row.Title);
                    var title = BuildTitle(row.Title, row.Brand, shoeType, externalId);
                    var brandEntity = EnsureBrand(row.Brand, brandMap, ref brandsCreated);
                    var categoryEntity = EnsureCategory(ToDisplayCategory(shoeType), categoryMap, ref categoriesCreated);

                    if (existingByExternalId.TryGetValue(externalId, out var existing))
                    {
                        existing.Brand = brandEntity;
                        existing.Category = categoryEntity;
                        existing.Title = title;
                        existing.Description = row.Description;
                        existing.Gender = NormalizeGender(row.Gender);
                        existing.ShoeType = shoeType;
                        existing.Currency = NormalizeCurrency(row.Currency);
                        existing.Price = NormalizePrice(row.Price);
                        existing.AvgRating = NormalizeRating(row.AvgRating);
                        existing.ReviewCount = NormalizeReviewCount(row.ReviewCount);
                        existing.MainImageUrl = TrimToNull(row.ImageUrl);
                        existing.UpdatedAt = DateTime.UtcNow;
                        if (existing.CreatedAt == default)
                        {
                            existing.CreatedAt = row.UpdatedAtUtc;
                        }

                        productsUpdated++;
                    }
                    else
                    {
                        _trainingDb.Products.Add(new TrainingProduct
                        {
                            DatasetId = dataset.Id,
                            ExternalId = externalId,
                            Brand = brandEntity,
                            Category = categoryEntity,
                            Title = title,
                            Description = row.Description,
                            Gender = NormalizeGender(row.Gender),
                            ShoeType = shoeType,
                            Currency = NormalizeCurrency(row.Currency),
                            Price = NormalizePrice(row.Price),
                            AvgRating = NormalizeRating(row.AvgRating),
                            ReviewCount = NormalizeReviewCount(row.ReviewCount),
                            MainImageUrl = TrimToNull(row.ImageUrl),
                            CreatedAt = row.UpdatedAtUtc,
                            UpdatedAt = DateTime.UtcNow
                        });

                        productsInserted++;
                    }
                }
            }

            await _trainingDb.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Open training sync completed. datasets={Datasets}, sourceRows={SourceRows}, inserted={Inserted}, updated={Updated}, brandsCreated={BrandsCreated}, categoriesCreated={CategoriesCreated}, unsupported={Unsupported}",
                datasets.Count,
                sourceRowsRead,
                productsInserted,
                productsUpdated,
                brandsCreated,
                categoriesCreated,
                unsupportedDatasets);

            return new OpenProductTrainingSyncResult
            {
                DatasetCount = datasets.Count,
                SourceRowsRead = sourceRowsRead,
                ProductsInserted = productsInserted,
                ProductsUpdated = productsUpdated,
                BrandsCreated = brandsCreated,
                CategoriesCreated = categoriesCreated,
                UnsupportedDatasets = unsupportedDatasets,
                SyncedAtUtc = DateTime.UtcNow
            };
        }

        private static string InferDatasetSourceType(string datasetName)
        {
            if (datasetName.Contains("amazon", StringComparison.OrdinalIgnoreCase))
                return "amazon";

            if (datasetName.Contains("ebay", StringComparison.OrdinalIgnoreCase))
                return "ebay";

            if (datasetName.Contains("google", StringComparison.OrdinalIgnoreCase) ||
                datasetName.Contains("shopping", StringComparison.OrdinalIgnoreCase))
                return "google";

            if (datasetName.Contains("eutrend", StringComparison.OrdinalIgnoreCase) ||
                datasetName.Contains("eu_trend", StringComparison.OrdinalIgnoreCase) ||
                datasetName.Contains("zalando", StringComparison.OrdinalIgnoreCase))
                return "eutrend";

            if (datasetName.Contains("kaggle", StringComparison.OrdinalIgnoreCase))
                return "kaggle";

            if (datasetName.Contains("zappos", StringComparison.OrdinalIgnoreCase))
                return "zappos";

            return "custom";
        }

        private static string GetDatasetDescription(string datasetName)
        {
            if (datasetName.Equals("kaggle_shoe_dataset", StringComparison.OrdinalIgnoreCase))
                return "Kaggle shoe dataset used for open product training.";
            if (datasetName.Equals("amazon_clothing_shoes", StringComparison.OrdinalIgnoreCase))
                return "Amazon clothing/shoes metadata dataset used for training.";
            if (datasetName.Equals("ebay_shoes", StringComparison.OrdinalIgnoreCase))
                return "eBay shoes dataset used for open product training.";
            if (datasetName.Equals("google_shopping_shoes", StringComparison.OrdinalIgnoreCase))
                return "Google Shopping shoes dataset used for open product training.";
            if (datasetName.Contains("eutrend", StringComparison.OrdinalIgnoreCase) ||
                datasetName.Contains("eu_trend", StringComparison.OrdinalIgnoreCase) ||
                datasetName.Contains("zalando", StringComparison.OrdinalIgnoreCase))
                return "EU trending products (Zalando scraper) — rank-based popularity signals.";
            return $"Open product training dataset: {datasetName}";
        }

        private async Task<List<SourceProductRow>?> LoadSourceRowsAsync(string sourceType, CancellationToken ct)
        {
            if (sourceType == "amazon")
            {
                return await _analyticsDb.AmazonShoeProducts
                    .AsNoTracking()
                    .Select(x => new SourceProductRow
                    {
                        ExternalId = x.Asin,
                        Title = x.Name,
                        Brand = x.Brand,
                        ShoeType = x.Category,
                        Gender = x.Gender,
                        Price = x.Price,
                        Currency = x.Currency,
                        AvgRating = x.Rating,
                        ReviewCount = x.ReviewCount,
                        ImageUrl = x.ImageUrl,
                        UpdatedAtUtc = ToUtc(x.LastSynced, x.CreatedAt)
                    })
                    .ToListAsync(ct);
            }

            if (sourceType == "ebay")
            {
                return await _analyticsDb.EbayShoeProducts
                    .AsNoTracking()
                    .Select(x => new SourceProductRow
                    {
                        ExternalId = x.EbayItemId,
                        Title = x.Name,
                        Brand = x.Brand,
                        ShoeType = x.Category,
                        Gender = x.Gender,
                        Price = x.Price,
                        Currency = x.Currency,
                        AvgRating = x.Rating,
                        ReviewCount = x.ReviewCount,
                        ImageUrl = x.ImageUrl,
                        UpdatedAtUtc = ToUtc(x.LastSynced, x.CreatedAt)
                    })
                    .ToListAsync(ct);
            }

            if (sourceType == "google")
            {
                // Google Shopping: use Position as synthetic review volume signal
                // (position 1 = most visible = most popular; treat as high review count)
                var raw = await _analyticsDb.GoogleShoppingProducts
                    .AsNoTracking()
                    .Where(x => x.Position > 0)
                    .OrderBy(x => x.Position)
                    .ToListAsync(ct);

                if (raw.Count == 0)
                    return new List<SourceProductRow>();

                var maxPos = raw.Max(x => x.Position);
                return raw.Select(x =>
                {
                    // Invert rank: position 1 = most popular
                    var popNorm = 1.0 - ((double)(x.Position - 1) / Math.Max(maxPos - 1, 1));
                    // If product has real review data, use it; otherwise synthesize from position
                    var syntheticRating  = x.Rating > 0    ? x.Rating    : (float)(3.5 + popNorm * 1.4);
                    var syntheticReviews = x.ReviewCount > 0 ? x.ReviewCount : (int)(50 + popNorm * 4950);
                    return new SourceProductRow
                    {
                        ExternalId   = !string.IsNullOrWhiteSpace(x.ProductId) ? x.ProductId : $"google-{x.Id}",
                        Title        = x.Title,
                        Brand        = x.Brand,
                        ShoeType     = x.Category,
                        Gender       = x.Gender,
                        Price        = x.Price,
                        Currency     = x.Currency,
                        AvgRating    = syntheticRating,
                        ReviewCount  = syntheticReviews,
                        ImageUrl     = x.ImageUrl,
                        UpdatedAtUtc = ToUtc(x.LastSynced, x.CreatedAt)
                    };
                }).ToList();
            }

            if (sourceType == "eutrend")
            {
                // EU Trends (Zalando scraper): rank-based popularity signal.
                // Rank 1 = most trending; convert to synthetic rating + review count.
                var raw = await _analyticsDb.EuTrends
                    .AsNoTracking()
                    .Where(x => x.Brand != null && x.Category != null)
                    .OrderBy(x => x.Rank ?? 999)
                    .ToListAsync(ct);

                if (raw.Count == 0)
                    return new List<SourceProductRow>();

                var maxRank = raw.Select(x => x.Rank ?? 100).DefaultIfEmpty(100).Max();
                return raw.Select(x =>
                {
                    var rank    = x.Rank ?? maxRank;
                    var popNorm = 1.0 - ((double)(rank - 1) / Math.Max(maxRank - 1, 1)); // 0-1, 1=best
                    return new SourceProductRow
                    {
                        ExternalId   = x.Id.ToString(),
                        Title        = x.ProductName,
                        Brand        = x.Brand,
                        ShoeType     = MapEuCategory(x.Category),
                        Price        = x.Price,
                        Currency     = "EUR",
                        // Synthesize review signals from rank
                        AvgRating    = (float)(3.5 + popNorm * 1.5),   // 3.5 – 5.0
                        ReviewCount  = (int)(100 + popNorm * 4900),     // 100 – 5000
                        ImageUrl     = x.ImageUrl,
                        UpdatedAtUtc = x.UpdatedAt
                    };
                }).ToList();
            }

            return null;
        }

        private static string MapEuCategory(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "general";
            var v = raw.Trim().ToLowerInvariant();
            if (v.Contains("sneaker") || v.Contains("trainer") || v.Contains("patik")) return "sneakers";
            if (v.Contains("boot") || v.Contains("cizma"))                             return "boots";
            if (v.Contains("sandal"))                                                  return "sandals";
            if (v.Contains("heel") || v.Contains("pump") || v.Contains("stilt"))       return "heels";
            if (v.Contains("loafer") || v.Contains("mokasin"))                         return "loafers";
            if (v.Contains("slipper") || v.Contains("papuca"))                         return "slippers";
            if (v.Contains("oxford") || v.Contains("derby"))                           return "oxfords";
            if (v.Contains("court") || v.Contains("tenis"))                            return "sneakers";
            return NormalizeLookupKey(raw);
        }

        private static TrainingBrand EnsureBrand(
            string? brandName,
            Dictionary<string, TrainingBrand> map,
            ref int createdCount)
        {
            var canonicalName = string.IsNullOrWhiteSpace(brandName)
                ? "Unknown"
                : brandName.Trim();

            var key = NormalizeLookupKey(canonicalName);
            if (map.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var brand = new TrainingBrand
            {
                Name = canonicalName
            };

            map[key] = brand;
            createdCount++;
            return brand;
        }

        private static TrainingCategory EnsureCategory(
            string categoryName,
            Dictionary<string, TrainingCategory> map,
            ref int createdCount)
        {
            var key = NormalizeLookupKey(categoryName);
            if (map.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var category = new TrainingCategory
            {
                Name = categoryName
            };

            map[key] = category;
            createdCount++;
            return category;
        }

        private static string ResolveSourceType(string? sourceType, string datasetName)
        {
            var normalizedSource = NormalizeLookupKey(sourceType);
            if (normalizedSource is "amazon" or "ebay" or "google" or "eutrend")
                return normalizedSource;

            // kaggle shoe datasets are amazon-format metadata — use the amazon table
            if (normalizedSource == "kaggle")
                return "amazon";

            var name = NormalizeLookupKey(datasetName);
            if (name.Contains("amazon", StringComparison.Ordinal))
                return "amazon";
            if (name.Contains("ebay", StringComparison.Ordinal))
                return "ebay";
            if (name.Contains("eutrend", StringComparison.Ordinal) ||
                name.Contains("eu_trend", StringComparison.Ordinal) ||
                name.Contains("zalando", StringComparison.Ordinal))
                return "eutrend";
            if (name.Contains("google", StringComparison.Ordinal) ||
                name.Contains("shopping", StringComparison.Ordinal))
                return "google";
            if (name.Contains("kaggle", StringComparison.Ordinal))
                return "amazon";

            return normalizedSource;
        }

        private static string NormalizeShoeType(string? rawShoeType, string? title)
        {
            var normalizedRaw = TrimToNull(rawShoeType);
            if (normalizedRaw is not null)
            {
                return NormalizeLookupKey(normalizedRaw);
            }

            var haystack = NormalizeLookupKey(title);
            if (haystack.Contains("sneaker", StringComparison.Ordinal) ||
                haystack.Contains("trainer", StringComparison.Ordinal) ||
                haystack.Contains("running", StringComparison.Ordinal))
            {
                return "sneakers";
            }

            if (haystack.Contains("boot", StringComparison.Ordinal))
            {
                return "boots";
            }

            if (haystack.Contains("heel", StringComparison.Ordinal) ||
                haystack.Contains("pump", StringComparison.Ordinal) ||
                haystack.Contains("stiletto", StringComparison.Ordinal))
            {
                return "heels";
            }

            if (haystack.Contains("sandal", StringComparison.Ordinal))
            {
                return "sandals";
            }

            if (haystack.Contains("loafer", StringComparison.Ordinal))
            {
                return "loafers";
            }

            if (haystack.Contains("mule", StringComparison.Ordinal))
            {
                return "mules";
            }

            if (haystack.Contains("slipper", StringComparison.Ordinal))
            {
                return "slippers";
            }

            if (haystack.Contains("flat", StringComparison.Ordinal) ||
                haystack.Contains("ballerina", StringComparison.Ordinal))
            {
                return "flats";
            }

            return "general";
        }

        private static string ToDisplayCategory(string normalizedShoeType)
        {
            var words = normalizedShoeType
                .Split(CategorySeparators, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1))
                .ToArray();

            if (words.Length == 0)
            {
                return "General";
            }

            return string.Join(' ', words);
        }

        private static string BuildTitle(
            string? title,
            string? brand,
            string shoeType,
            string externalId)
        {
            var trimmedTitle = TrimToNull(title);
            if (trimmedTitle is not null)
            {
                return trimmedTitle;
            }

            var trimmedBrand = TrimToNull(brand) ?? "Unknown";
            return $"{trimmedBrand} {shoeType} ({externalId})";
        }

        private static string? NormalizeGender(string? gender)
        {
            var value = NormalizeLookupKey(gender);
            if (value.Length == 0)
            {
                return null;
            }

            if (value.Contains("women", StringComparison.Ordinal) ||
                value.Contains("female", StringComparison.Ordinal) ||
                value.Contains("frau", StringComparison.Ordinal) ||
                value.Contains("damen", StringComparison.Ordinal) ||
                value.Contains("lady", StringComparison.Ordinal))
            {
                return "women";
            }

            if (value.Contains("men", StringComparison.Ordinal) ||
                value.Contains("male", StringComparison.Ordinal) ||
                value.Contains("herr", StringComparison.Ordinal))
            {
                return "men";
            }

            if (value.Contains("kid", StringComparison.Ordinal) ||
                value.Contains("child", StringComparison.Ordinal) ||
                value.Contains("junior", StringComparison.Ordinal))
            {
                return "kids";
            }

            if (value.Contains("unisex", StringComparison.Ordinal))
            {
                return "unisex";
            }

            return null;
        }

        private static string? NormalizeCurrency(string? currency)
        {
            var value = TrimToNull(currency);
            if (value is null)
            {
                return null;
            }

            var upper = value.ToUpperInvariant();
            return upper.Length <= 10 ? upper : upper[..10];
        }

        private static decimal? NormalizePrice(decimal? price)
        {
            if (!price.HasValue || price.Value <= 0)
            {
                return null;
            }

            return decimal.Round(price.Value, 2, MidpointRounding.AwayFromZero);
        }

        private static decimal? NormalizeRating(float? rating)
        {
            if (!rating.HasValue)
            {
                return null;
            }

            var value = decimal.Round((decimal)rating.Value, 2, MidpointRounding.AwayFromZero);
            if (value < 0)
            {
                return 0;
            }

            if (value > 5)
            {
                return 5;
            }

            return value;
        }

        private static int? NormalizeReviewCount(int? reviewCount)
        {
            if (!reviewCount.HasValue)
            {
                return null;
            }

            return Math.Max(0, reviewCount.Value);
        }

        private static string NormalizeLookupKey(string? value)
            => string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();

        private static string? TrimToNull(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        private static DateTime ToUtc(DateTime preferred, DateTime fallback)
        {
            var value = preferred == default ? fallback : preferred;
            if (value == default)
            {
                return DateTime.UtcNow;
            }

            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }

        private sealed class SourceProductRow
        {
            public string ExternalId { get; init; } = string.Empty;
            public string? Title { get; init; }
            public string? Description { get; init; }
            public string? Brand { get; init; }
            public string? ShoeType { get; init; }
            public string? Gender { get; init; }
            public decimal? Price { get; init; }
            public string? Currency { get; init; }
            public float? AvgRating { get; init; }
            public int? ReviewCount { get; init; }
            public string? ImageUrl { get; init; }
            public DateTime UpdatedAtUtc { get; init; }
        }
    }
}

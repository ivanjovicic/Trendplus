using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Domain.Model.OpenProductTraining;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

// ── Interface ────────────────────────────────────────────────────────────────

public interface IShopifyImportService
{
    /// <summary>Import products from a Shopify store's public products.json endpoint.</summary>
    Task<ShopifyImportResult> ImportAsync(ShopifyImportRequest request, CancellationToken ct = default);

    /// <summary>Import from multiple Shopify stores in a single batch.</summary>
    Task<ShopifyBatchImportResult> ImportBatchAsync(IReadOnlyList<ShopifyImportRequest> requests, CancellationToken ct = default);
}

// ── Request / Response DTOs ──────────────────────────────────────────────────

public sealed record ShopifyImportRequest(
    string ShopDomain,
    int DatasetId,
    bool AutoCreateDataset = true,
    string? DatasetName = null,
    int MaxPages = 10,
    int PageSize = 250,
    bool NormalizeToTrainingProducts = true);

public sealed record ShopifyImportResult(
    string ShopDomain,
    int DatasetId,
    int PagesScanned,
    int TotalProductsSeen,
    int NewRawProducts,
    int SkippedDuplicates,
    int NormalizedProducts,
    int NormalizedBrands,
    int NormalizedCategories,
    double ElapsedMs,
    List<string> Warnings);

public sealed record ShopifyBatchImportResult(
    int TotalStores,
    int SuccessfulStores,
    int FailedStores,
    double TotalElapsedMs,
    List<ShopifyImportResult> Results,
    List<ShopifyBatchError> Errors);

public sealed record ShopifyBatchError(
    string ShopDomain,
    string Error);

// ── Implementation ───────────────────────────────────────────────────────────

public sealed class ShopifyImportService : IShopifyImportService
{
    private const int MaxPagesLimit = 100;
    private const int MaxPageSize = 250; // Shopify max limit per page is 250
    private const string ProductsJsonKey = "products";
    private const int DefaultBatchSize = 100;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OpenProductTrainingDbContext _db;
    private readonly ILogger<ShopifyImportService> _logger;

    public ShopifyImportService(
        IHttpClientFactory httpClientFactory,
        OpenProductTrainingDbContext db,
        ILogger<ShopifyImportService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _db = db;
        _logger = logger;
    }

    public async Task<ShopifyImportResult> ImportAsync(ShopifyImportRequest request, CancellationToken ct = default)
    {
        if (request.MaxPages <= 0 || request.MaxPages > MaxPagesLimit)
            throw new ArgumentOutOfRangeException(nameof(request), $"MaxPages must be between 1 and {MaxPagesLimit}.");

        if (request.PageSize <= 0 || request.PageSize > MaxPageSize)
            throw new ArgumentOutOfRangeException(nameof(request), $"PageSize must be between 1 and {MaxPageSize}.");

        ArgumentException.ThrowIfNullOrWhiteSpace(request.ShopDomain, nameof(request.ShopDomain));

        var sw = Stopwatch.StartNew();
        var warnings = new List<string>();
        var domain = SanitizeDomain(request.ShopDomain);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Shopify import starting for {Domain}, datasetId={DatasetId}", domain, request.DatasetId);
        }

        // ── Resolve or auto-create dataset ──
        var datasetId = request.DatasetId;
        if (datasetId <= 0 && request.AutoCreateDataset)
        {
            datasetId = await ResolveOrCreateDatasetAsync(domain, request.DatasetName, ct);
        }

        if (datasetId <= 0)
        {
            throw new ArgumentException($"DatasetId mora biti > 0 ili uključi AutoCreateDataset. Domain={domain}");
        }

        // ── Paginated fetch ──
        var allProducts = new List<JsonElement>();
        var page = 1;
        string? pageInfo = null;
        var client = _httpClientFactory.CreateClient("Shopify");

        while (page <= request.MaxPages)
        {
            var url = BuildPageUrl(domain, request.PageSize, pageInfo);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Fetching page {Page}: {Url}", page, url);
            }

            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(url, ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                warnings.Add($"Timeout on page {page}. Continuing with fetched products.");
                break;
            }
            catch (HttpRequestException ex)
            {
                warnings.Add($"HTTP error on page {page}: {ex.Message}");
                break;
            }

            if (!response.IsSuccessStatusCode)
            {
                warnings.Add($"HTTP {(int)response.StatusCode} on page {page}.");
                break;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty(ProductsJsonKey, out var products) ||
                products.ValueKind != JsonValueKind.Array)
            {
                warnings.Add($"Unexpected JSON format on page {page}.");
                break;
            }

            var pageProducts = new List<JsonElement>();
            foreach (var p in products.EnumerateArray())
            {
                pageProducts.Add(p.Clone());
            }

            if (pageProducts.Count == 0)
                break;

            allProducts.AddRange(pageProducts);

            // ── Cursor pagination via Link header ──
            pageInfo = ExtractNextPageInfo(response);
            if (pageInfo == null)
                break;

            page++;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Shopify {Domain}: fetched {Count} products across {Pages} pages",
                domain, allProducts.Count, page);
        }

        if (allProducts.Count == 0)
        {
            sw.Stop();
            return new ShopifyImportResult(domain, datasetId, page, 0, 0, 0, 0, 0, 0, sw.Elapsed.TotalMilliseconds, warnings);
        }

        // ── Upsert raw products ──
        var (newRaw, skipped) = await UpsertRawProductsAsync(allProducts, datasetId, ct);

        // ── Normalize to training products ──
        var normalizedProducts = 0;
        var normalizedBrands = 0;
        var normalizedCategories = 0;

        if (request.NormalizeToTrainingProducts && newRaw > 0)
        {
            var normResult = await NormalizeNewProductsAsync(allProducts, datasetId, ct);
            normalizedProducts = normResult.Products;
            normalizedBrands = normResult.Brands;
            normalizedCategories = normResult.Categories;
        }

        sw.Stop();

        _logger.LogInformation(
            "Shopify import complete: {Domain} — {New} novih, {Skip} duplikata, {Norm} normalizovanih za {Ms:F0}ms",
            domain, newRaw, skipped, normalizedProducts, sw.Elapsed.TotalMilliseconds);

        return new ShopifyImportResult(
            domain, datasetId, page, allProducts.Count,
            newRaw, skipped, normalizedProducts, normalizedBrands, normalizedCategories,
            sw.Elapsed.TotalMilliseconds, warnings);
    }

    public async Task<ShopifyBatchImportResult> ImportBatchAsync(
        IReadOnlyList<ShopifyImportRequest> requests, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var results = new List<ShopifyImportResult>();
        var errors = new List<ShopifyBatchError>();

        foreach (var req in requests)
        {
            try
            {
                var result = await ImportAsync(req, ct);
                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Shopify batch import failed for {Domain}", req.ShopDomain);
                errors.Add(new ShopifyBatchError(req.ShopDomain, ex.Message));
            }
        }

        sw.Stop();
        return new ShopifyBatchImportResult(
            requests.Count,
            results.Count,
            errors.Count,
            sw.Elapsed.TotalMilliseconds,
            results,
            errors);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static string SanitizeDomain(string input)
    {
        var domain = input.Trim()
            .Replace("https://", "", StringComparison.OrdinalIgnoreCase)
            .Replace("http://", "", StringComparison.OrdinalIgnoreCase)
            .TrimEnd('/');

        // Append .myshopify.com if no TLD present
        if (!domain.Contains('.'))
            domain += ".myshopify.com";

        return domain.ToLowerInvariant();
    }

    private static string BuildPageUrl(string domain, int pageSize, string? pageInfo)
    {
        var limit = Math.Clamp(pageSize, 1, MaxPageSize);
        if (!string.IsNullOrWhiteSpace(pageInfo))
            return $"https://{domain}/products.json?limit={limit}&page_info={pageInfo}";
        return $"https://{domain}/products.json?limit={limit}";
    }

    private static string? ExtractNextPageInfo(HttpResponseMessage response)
    {
        // Shopify uses Link header with cursor-based pagination:
        // <https://store.myshopify.com/products.json?page_info=XXXXX&limit=250>; rel="next"
        if (!response.Headers.TryGetValues("Link", out var links))
            return null;

        foreach (var link in links)
        {
            var parts = link.Split(',');
            foreach (var part in parts)
            {
                if (!part.Contains("rel=\"next\"", StringComparison.OrdinalIgnoreCase))
                    continue;

                var urlPart = part.Split(';')[0].Trim().Trim('<', '>');
                var uri = new Uri(urlPart);
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                var pi = query["page_info"];
                if (!string.IsNullOrWhiteSpace(pi))
                    return pi;
            }
        }

        return null;
    }

    private async Task<int> ResolveOrCreateDatasetAsync(string domain, string? datasetName, CancellationToken ct)
    {
        var name = string.IsNullOrWhiteSpace(datasetName) ? $"shopify:{domain}" : datasetName.Trim();

        var existing = await _db.Datasets
            .AsNoTracking()
            .Where(d => d.Name == name)
            .Select(d => d.Id)
            .FirstOrDefaultAsync(ct);

        if (existing > 0)
            return existing;

        var dataset = new TrainingDataset
        {
            Name = name,
            SourceType = "shopify",
            Description = $"Auto-imported from Shopify store: {domain}",
            CreatedAt = DateTime.UtcNow
        };

        _db.Datasets.Add(dataset);
        await _db.SaveChangesAsync(ct);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Created new dataset: id={Id}, name={Name}", dataset.Id, dataset.Name);
        }

        return dataset.Id;
    }

    private async Task<(int New, int Skipped)> UpsertRawProductsAsync(
        List<JsonElement> products, int datasetId, CancellationToken ct)
    {
        var incoming = new List<(string ExternalId, string RawPayload)>();
        foreach (var p in products)
        {
            var externalId = ExtractExternalId(p);
            if (string.IsNullOrWhiteSpace(externalId)) continue;
            incoming.Add((externalId, p.GetRawText()));
        }

        if (incoming.Count == 0) return (0, 0);

        var incomingIds = incoming.Select(x => x.ExternalId).Distinct(StringComparer.Ordinal).ToArray();

        // Batch lookup existing
        var existingIds = await _db.RawProducts
            .AsNoTracking()
            .Where(x => x.DatasetId == datasetId && incomingIds.Contains(x.ExternalId))
            .Select(x => x.ExternalId)
            .ToListAsync(ct);

        var existingSet = new HashSet<string>(existingIds, StringComparer.Ordinal);
        var newCount = 0;

        foreach (var item in incoming)
        {
            if (existingSet.Contains(item.ExternalId))
                continue;

            _db.RawProducts.Add(new RawTrainingProduct
            {
                DatasetId = datasetId,
                ExternalId = item.ExternalId,
                RawPayload = item.RawPayload,
                ImportedAt = DateTime.UtcNow
            });

            existingSet.Add(item.ExternalId);
            newCount++;

            // Save in batches
            if (newCount % DefaultBatchSize == 0)
                await _db.SaveChangesAsync(ct);
        }

        if (newCount % DefaultBatchSize != 0)
            await _db.SaveChangesAsync(ct);

        return (newCount, incoming.Count - newCount);
    }

    private async Task<(int Products, int Brands, int Categories)> NormalizeNewProductsAsync(
        List<JsonElement> rawProducts, int datasetId, CancellationToken ct)
    {
        // Pre-load existing brands & categories
        var existingBrands = await _db.Brands
            .AsNoTracking()
            .ToDictionaryAsync(b => b.Name.ToLowerInvariant(), b => b.Id, ct);

        var existingCategories = await _db.Categories
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Name.ToLowerInvariant(), c => c.Id, ct);

        var existingExternalIds = await _db.Products
            .AsNoTracking()
            .Where(p => p.DatasetId == datasetId)
            .Select(p => p.ExternalId)
            .ToListAsync(ct);

        var existingExtIdSet = new HashSet<string>(existingExternalIds, StringComparer.Ordinal);

        var newBrands = 0;
        var newCategories = 0;
        var newProducts = 0;

        foreach (var raw in rawProducts)
        {
            var externalId = ExtractExternalId(raw);
            if (string.IsNullOrWhiteSpace(externalId) || existingExtIdSet.Contains(externalId))
                continue;

            var title = GetStringProp(raw, "title");
            if (string.IsNullOrWhiteSpace(title)) continue;

            // ── Brand resolution ──
            int? brandId = null;
            var vendor = GetStringProp(raw, "vendor");
            if (!string.IsNullOrWhiteSpace(vendor))
            {
                var vendorKey = vendor.Trim().ToLowerInvariant();
                if (!existingBrands.TryGetValue(vendorKey, out var bid))
                {
                    var brand = new TrainingBrand { Name = vendor.Trim() };
                    _db.Brands.Add(brand);
                    await _db.SaveChangesAsync(ct);
                    existingBrands[vendorKey] = brand.Id;
                    bid = brand.Id;
                    newBrands++;
                }
                brandId = bid;
            }

            // ── Category resolution ──
            int? categoryId = null;
            var productType = GetStringProp(raw, "product_type");
            if (!string.IsNullOrWhiteSpace(productType))
            {
                var typeKey = productType.Trim().ToLowerInvariant();
                if (!existingCategories.TryGetValue(typeKey, out var cid))
                {
                    var cat = new TrainingCategory { Name = productType.Trim() };
                    _db.Categories.Add(cat);
                    await _db.SaveChangesAsync(ct);
                    existingCategories[typeKey] = cat.Id;
                    cid = cat.Id;
                    newCategories++;
                }
                categoryId = cid;
            }

            // ── Price from first variant ──
            decimal? price = null;
            string? currency = null;
            if (raw.TryGetProperty("variants", out var variants) && variants.ValueKind == JsonValueKind.Array)
            {
                foreach (var v in variants.EnumerateArray())
                {
                    var priceStr = GetStringProp(v, "price");
                    if (!string.IsNullOrWhiteSpace(priceStr) &&
                        decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) && p > 0)
                    {
                        price = p;
                        break;
                    }
                }
            }

            // ── Main image ──
            string? mainImage = null;
            if (raw.TryGetProperty("images", out var images) && images.ValueKind == JsonValueKind.Array)
            {
                foreach (var img in images.EnumerateArray())
                {
                    var src = GetStringProp(img, "src");
                    if (!string.IsNullOrWhiteSpace(src))
                    {
                        mainImage = src;
                        break;
                    }
                }
            }

            // ── "gender" and "shoe_type" from tags ──
            string? gender = null;
            string? shoeType = null;
            var tags = GetStringProp(raw, "tags");
            if (!string.IsNullOrWhiteSpace(tags))
            {
                var tagList = tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.ToLowerInvariant())
                    .ToHashSet();

                if (tagList.Contains("men") || tagList.Contains("muški") || tagList.Contains("musko"))
                    gender = "Men";
                else if (tagList.Contains("women") || tagList.Contains("ženski") || tagList.Contains("zensko"))
                    gender = "Women";
                else if (tagList.Contains("unisex"))
                    gender = "Unisex";
                else if (tagList.Contains("kids") || tagList.Contains("dečiji") || tagList.Contains("deciji"))
                    gender = "Kids";

                foreach (var stCandidate in new[]
                {
                    "sneakers", "boots", "sandals", "loafers", "heels", "flats",
                    "patike", "čizme", "sandale", "mokasine", "cipele"
                })
                {
                    if (tagList.Contains(stCandidate))
                    {
                        shoeType = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(stCandidate);
                        break;
                    }
                }
            }

            var now = DateTime.UtcNow;
            var product = new TrainingProduct
            {
                DatasetId = datasetId,
                ExternalId = externalId,
                BrandId = brandId,
                CategoryId = categoryId,
                Title = title,
                Description = GetStringProp(raw, "body_html"),
                Gender = gender,
                ShoeType = shoeType,
                Currency = currency ?? "EUR",
                Price = price,
                MainImageUrl = mainImage,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.Products.Add(product);
            existingExtIdSet.Add(externalId);
            newProducts++;

            // Batch save every 100 products to manage memory
            if (newProducts % 100 == 0)
                await _db.SaveChangesAsync(ct);
        }

        if (newProducts % 100 != 0)
            await _db.SaveChangesAsync(ct);

        return (newProducts, newBrands, newCategories);
    }

    private static string? ExtractExternalId(JsonElement product)
    {
        if (product.TryGetProperty("id", out var id))
        {
            return id.ValueKind switch
            {
                JsonValueKind.Number => id.GetInt64().ToString(CultureInfo.InvariantCulture),
                JsonValueKind.String => id.GetString(),
                _ => null
            };
        }
        return null;
    }

    private static string? GetStringProp(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var val) && val.ValueKind == JsonValueKind.String)
            return val.GetString();
        return null;
    }
}

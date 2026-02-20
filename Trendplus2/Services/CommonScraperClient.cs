using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Trendplus2.Dtos;
using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

namespace Trendplus2.Services
{
    public interface ICommonScraperClient
    {
        Task<CommonProductsResponse?> GetCommonProductsAsync(CommonProductsFilters filters, CancellationToken ct = default);
    }

    public class CommonScraperClient : ICommonScraperClient
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly IDistributedCache _cache;
        private readonly ILogger<CommonScraperClient> _logger;

        public CommonScraperClient(HttpClient httpClient, IConfiguration config, IDistributedCache cache, ILogger<CommonScraperClient> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _cache = cache;
            _logger = logger;
        }

        private string BuildCacheKey(CommonProductsFilters filters)
        {
            var raw = $"{filters.Gender}|{filters.Category}|{filters.Brand}|{filters.PriceMin}|{filters.PriceMax}|{filters.Sort}|{filters.Sale}|{filters.IsNew}|{filters.Pages}";
            return "common-products:" + System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw));
        }

        public async Task<CommonProductsResponse?> GetCommonProductsAsync(CommonProductsFilters filters, CancellationToken ct = default)
        {
            var cacheKey = BuildCacheKey(filters);

            var cached = await _cache.GetStringAsync(cacheKey, ct);
            if (!string.IsNullOrEmpty(cached))
            {
                _logger.LogInformation("Common products cache hit: {Key}", cacheKey);
                return JsonSerializer.Deserialize<CommonProductsResponse>(cached);
            }

            _logger.LogInformation("Common products cache MISS: {Key}", cacheKey);

            var pythonBaseUrl = _config["PythonScraper:BaseUrl"] ?? "http://localhost:8000";
            var url = $"{pythonBaseUrl.TrimEnd('/')}/scrapers/common";

            // Call Python scraper service
            var response = await _httpClient.PostAsJsonAsync(url, filters, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Python API error: {Status}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(ct);

            // Try to parse legacy shape and map to ProductMatchResult
            CommonProductsResponse mappedResult = new CommonProductsResponse();

            try
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                // Determine items array location: root.items or root
                JsonElement itemsEl;
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("items", out itemsEl) && itemsEl.ValueKind == JsonValueKind.Array)
                {
                    // root.items exists
                }
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    // root itself is array
                    itemsEl = root;
                }
                else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("count", out var _) && root.TryGetProperty("items", out itemsEl))
                {
                    // handled above
                }
                else
                {
                    // unexpected shape — attempt to deserialize to our DTO directly
                    try
                    {
                        mappedResult = JsonSerializer.Deserialize<CommonProductsResponse>(content) ?? new CommonProductsResponse();
                    }
                    catch
                    {
                        mappedResult = new CommonProductsResponse();
                    }

                    // cache and return
                    var json = JsonSerializer.Serialize(mappedResult);
                    await _cache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = System.TimeSpan.FromMinutes(15) }, ct);
                    return mappedResult;
                }

                // At this point itemsEl should be set either from root.items or root array
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("items", out var tmpItems)) itemsEl = tmpItems;
                // else itemsEl already set when root was array

                var list = new List<ProductMatchResult>();

                foreach (var el in itemsEl.EnumerateArray())
                {
                    // Expecting fields like: brand, name_zalando, name_deichmann, price_zalando, price_deichmann, image_zalando, image_deichmann, url_zalando, url_deichmann
                    var brand = el.TryGetProperty("brand", out var pb) ? pb.GetString() ?? string.Empty : string.Empty;

                    var nameZ = el.TryGetProperty("name_zalando", out var pnz) ? pnz.GetString() ?? string.Empty : (el.TryGetProperty("name", out var pn2) ? pn2.GetString() ?? string.Empty : string.Empty);
                    var nameD = el.TryGetProperty("name_deichmann", out var pnd) ? pnd.GetString() ?? string.Empty : (el.TryGetProperty("name_deichmann", out var _) ? string.Empty : string.Empty);

                    // Some legacy entries may store deichmann under 'name' in nested structure; try alternative keys
                    if (string.IsNullOrEmpty(nameD) && el.TryGetProperty("name_deichmann", out _) == false)
                    {
                        // try keys 'name_deichmann' already failed; try 'deichmann_name' or 'name_deich'
                        nameD = el.TryGetProperty("name_deichmann", out var alt1) ? alt1.GetString() ?? string.Empty : string.Empty;
                    }

                    var priceZ = el.TryGetProperty("price_zalando", out var ppz) ? ppz.GetString() ?? string.Empty : (el.TryGetProperty("price", out var pp2) ? pp2.GetString() ?? string.Empty : string.Empty);
                    var priceD = el.TryGetProperty("price_deichmann", out var ppd) ? ppd.GetString() ?? string.Empty : string.Empty;

                    var imgZ = el.TryGetProperty("image_zalando", out var piz) ? piz.GetString() ?? string.Empty : (el.TryGetProperty("image_url", out var piz2) ? piz2.GetString() ?? string.Empty : string.Empty);
                    var imgD = el.TryGetProperty("image_deichmann", out var pid) ? pid.GetString() ?? string.Empty : (el.TryGetProperty("image", out var pid2) ? pid2.GetString() ?? string.Empty : string.Empty);

                    var urlZ = el.TryGetProperty("url_zalando", out var puz) ? puz.GetString() ?? string.Empty : (el.TryGetProperty("url", out var pu2) ? pu2.GetString() ?? string.Empty : string.Empty);
                    var urlD = el.TryGetProperty("url_deichmann", out var pud) ? pud.GetString() ?? string.Empty : string.Empty;

                    var zalandoItem = new CommonProductItem
                    {
                        Brand = brand,
                        Name_Zalando = nameZ,
                        Price_Zalando = priceZ,
                        Image_Zalando = imgZ,
                        Url_Zalando = urlZ
                    };

                    var deichmannItem = new CommonProductItem
                    {
                        Brand = brand,
                        Name_Deichmann = nameD,
                        Price_Deichmann = priceD,
                        Image_Deichmann = imgD,
                        Url_Deichmann = urlD
                    };

                    var score = ComputeMatchScore(nameZ, nameD, brand, priceZ, priceD);

                    list.Add(new ProductMatchResult
                    {
                        Brand = brand,
                        ShoeType = filters.Category ?? string.Empty,
                        Zalando = zalandoItem,
                        Deichmann = deichmannItem,
                        Score = score
                    });
                }

                mappedResult.Count = list.Count;
                mappedResult.Items = list;

                // cache
                var outJson = JsonSerializer.Serialize(mappedResult);
                await _cache.SetStringAsync(cacheKey, outJson, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = System.TimeSpan.FromMinutes(15) }, ct);

                return mappedResult;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse Python response for common products");
                return null;
            }
        }

        private static decimal ComputeMatchScore(string nameZ, string nameD, string brand, string priceZ, string priceD)
        {
            // Score components: name similarity (0.6), brand match (0.2), price closeness (0.2)
            var nameScore = TokenJaccard(nameZ, nameD) * 0.6m;
            var brandScore = string.Equals((brand ?? string.Empty).Trim(), (brand ?? string.Empty).Trim(), System.StringComparison.OrdinalIgnoreCase) ? 0.2m : 0m;

            var pz = ParsePrice(priceZ);
            var pd = ParsePrice(priceD);
            decimal priceScore = 0m;
            if (pz.HasValue && pd.HasValue && pz.Value > 0 && pd.Value > 0)
            {
                var relDiff = System.Math.Abs(pz.Value - pd.Value) / System.Math.Max(pz.Value, pd.Value);
                priceScore = (decimal)System.Math.Max(0, 1 - relDiff) * 0.2m;
            }

            var total = nameScore + brandScore + priceScore;
            // clamp 0..1
            if (total > 1m) total = 1m;
            if (total < 0m) total = 0m;
            // scale to 0..100
            return System.Math.Round(total * 100m, 2);
        }

        private static decimal? ParsePrice(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            // Remove currency symbols and letters
            var cleaned = Regex.Replace(s, @"[^0-9,\.]", string.Empty).Trim();
            if (string.IsNullOrEmpty(cleaned)) return null;
            // Replace comma with dot if appropriate
            cleaned = cleaned.Replace(',', '.');
            if (decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var v)) return v;
            return null;
        }

        private static decimal TokenJaccard(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 0m;
            var ta = Tokenize(a);
            var tb = Tokenize(b);
            if (ta.Count == 0 || tb.Count == 0) return 0m;
            var intersect = ta.Intersect(tb).Count();
            var union = ta.Union(tb).Count();
            return union == 0 ? 0m : (decimal)intersect / (decimal)union;
        }

        private static HashSet<string> Tokenize(string s)
        {
            var cleaned = s.ToLowerInvariant();
            cleaned = Regex.Replace(cleaned, "[^a-z0-9 ]", " ");
            var parts = cleaned.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            return new HashSet<string>(parts);
        }
    }
}

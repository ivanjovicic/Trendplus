using System.Text.Json.Nodes;
using Api.Config;
using Domain.Model;
using Microsoft.Extensions.Options;

namespace Api.Services
{
    /// <summary>
    /// Calls SerpAPI's Amazon engine to search for shoe listings.
    /// Injected via typed HttpClient.
    /// </summary>
    public class AmazonShoesService
    {
        private readonly SerpApiOptions _opts;
        private readonly HttpClient     _http;
        private readonly ILogger<AmazonShoesService> _log;

        public AmazonShoesService(
            IOptions<SerpApiOptions> options,
            HttpClient http,
            ILogger<AmazonShoesService> log)
        {
            _opts = options.Value;
            _http = http;
            _http.Timeout = TimeSpan.FromSeconds(_opts.TimeoutSeconds);
            _log  = log;
        }

        /// <summary>
        /// Searches Amazon for shoes of the given type, optionally filtering by price.
        /// Returns at most <see cref="SerpApiOptions.MaxResults"/> items per call.
        /// </summary>
        public async Task<List<AmazonShoeProduct>> FetchAsync(
            string type,
            int?   minPrice,
            int?   maxPrice,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_opts.ApiKey) || _opts.ApiKey == "YOUR_KEY")
            {
                _log.LogWarning("SerpApi ApiKey not configured – returning empty list");
                return [];
            }

            var query = Uri.EscapeDataString(type.Trim() + " shoes");

            var url = $"https://serpapi.com/search"
                    + $"?engine=amazon"
                    + $"&api_key={_opts.ApiKey}"
                    + $"&amazon_domain={_opts.AmazonDomain}"
                    + $"&k={query}"
                    + $"&s=review-rank";     // sort by review rank

            _log.LogInformation("SerpApi Amazon query: {Query} domain={Domain}", query, _opts.AmazonDomain);

            JsonNode? root;
            try
            {
                root = await _http.GetFromJsonAsync<JsonNode>(url, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "SerpApi HTTP request failed for query={Query}", query);
                throw;
            }

            var organic = root?["organic_results"]?.AsArray();
            if (organic is null || organic.Count == 0)
            {
                _log.LogWarning("SerpApi returned no organic_results for query={Query}", query);
                return [];
            }

            var list = new List<AmazonShoeProduct>();

            foreach (var item in organic.Take(_opts.MaxResults))
            {
                if (item is null) continue;

                // ── Price ────────────────────────────────────────────────────
                // SerpAPI Amazon engine returns price as { "value": 29.99, "symbol": "€", "currency": "EUR" }
                var priceNode    = item["price"];
                decimal? price   = TryDecimal(priceNode?["value"]);
                decimal? origPrice = null;

                // "was_price" node if the item is on sale
                var wasNode = item["was_price"];
                if (wasNode != null)
                    origPrice = TryDecimal(wasNode["value"]);

                var currency = priceNode?["currency"]?.ToString()
                            ?? priceNode?["symbol"]?.ToString();

                // ── Price filter ─────────────────────────────────────────────
                if (price.HasValue)
                {
                    if (minPrice.HasValue && price < minPrice) continue;
                    if (maxPrice.HasValue && price > maxPrice) continue;
                }

                list.Add(new AmazonShoeProduct
                {
                    Asin          = item["asin"]?.ToString()    ?? string.Empty,
                    Name          = item["title"]?.ToString(),
                    Brand         = item["brand"]?.ToString(),
                    ImageUrl      = item["thumbnail"]?.ToString(),
                    ProductUrl    = item["link"]?.ToString(),
                    Rating        = TryFloat(item["rating"])    ?? 0f,
                    ReviewCount   = TryInt(item["reviews"])     ?? 0,
                    Price         = price,
                    OriginalPrice = origPrice,
                    Currency      = currency,
                    Category      = type.Trim(),
                    Domain        = _opts.AmazonDomain,
                    LastSynced    = DateTime.UtcNow,
                    CreatedAt     = DateTime.UtcNow,
                });
            }

            _log.LogInformation("SerpApi fetched {Count} shoes for query={Query}", list.Count, query);
            return list;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static decimal? TryDecimal(JsonNode? node)
        {
            if (node is null) return null;
            try { return node.GetValue<decimal>(); } catch { }
            if (decimal.TryParse(node.ToString(), out var d)) return d;
            return null;
        }

        private static float? TryFloat(JsonNode? node)
        {
            if (node is null) return null;
            try { return node.GetValue<float>(); } catch { }
            if (float.TryParse(node.ToString(), out var f)) return f;
            return null;
        }

        private static int? TryInt(JsonNode? node)
        {
            if (node is null) return null;
            try { return node.GetValue<int>(); } catch { }
            if (int.TryParse(node.ToString(), out var i)) return i;
            return null;
        }
    }
}

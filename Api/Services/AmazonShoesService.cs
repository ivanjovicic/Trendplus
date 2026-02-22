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
        /// Searches Amazon for shoes of the given type, optionally targeting a gender segment and filtering by price.
        /// Returns at most <see cref="SerpApiOptions.MaxResults"/> items per call.
        /// </summary>
        public async Task<List<AmazonShoeProduct>> FetchAsync(
            string  type,
            string? gender,
            int?    minPrice,
            int?    maxPrice,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_opts.ApiKey) || _opts.ApiKey == "YOUR_KEY")
            {
                _log.LogWarning("SerpApi ApiKey not configured – returning empty list");
                return [];
            }

            // Build keyword: prepend gender if specified ("women sneakers shoes")
            var normalizedGender = string.IsNullOrEmpty(gender) || gender == "all" ? null : gender.Trim().ToLowerInvariant();
            var genderPrefix     = normalizedGender is not null ? normalizedGender + " " : "";
            var query = Uri.EscapeDataString(genderPrefix + type.Trim() + " shoes");

            var url = $"https://serpapi.com/search"
                    + $"?engine=amazon"
                    + $"&api_key={_opts.ApiKey}"
                    + $"&amazon_domain={_opts.AmazonDomain}"
                    + $"&k={query}"
                    + $"&s=review-rank";     // sort by review rank

            _log.LogInformation("SerpApi Amazon query: {Query} domain={Domain}",
                query, _opts.AmazonDomain);

            JsonNode? root;
            try
            {
                using var request  = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await _http.SendAsync(request, ct);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    _log.LogError("SerpApi returned {Status} for query={Query}. Body: {Body}",
                        (int)response.StatusCode, query, body[..Math.Min(body.Length, 400)]);
                    throw new HttpRequestException(
                        $"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase}). Body: {body[..Math.Min(body.Length, 200)]}");
                }

                var jsonStr = await response.Content.ReadAsStringAsync(ct);
                root = System.Text.Json.Nodes.JsonNode.Parse(jsonStr);
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "SerpApi HTTP request failed for query={Query}", query);
                throw;
            }

            var rootObj = root as System.Text.Json.Nodes.JsonObject;
            var organic = rootObj?["organic_results"]?.AsArray();
            if (organic is null || organic.Count == 0)
            {
                _log.LogWarning("SerpApi returned no organic_results for query={Query}", query);
                return [];
            }

            var list = new List<AmazonShoeProduct>();

            foreach (var rawItem in organic.Take(_opts.MaxResults))
            {
                var item = rawItem as System.Text.Json.Nodes.JsonObject;
                if (item is null) continue;

                // ── Price ─────────────────────────────────────────────────────────
                // SerpAPI Amazon (amazon.de) returns:
                //   "extracted_price": 69.99            (float, the best source)
                //   "price": "69,99 €"                  (display string, skip)
                //   "original_price"/"was_price": ...   (if on sale)
                decimal? price     = TryDecimal(item["extracted_price"]);
                decimal? origPrice = TryDecimal(item["original_price"])
                                   ?? TryDecimal(item["was_price"]?["value"]);

                // Currency: infer from price string "69,99 €" → "€"
                var priceStr = item["price"]?.ToString() ?? "";
                var currency = priceStr.Contains('€') ? "EUR"
                             : priceStr.Contains('$') ? "USD"
                             : priceStr.Contains('£') ? "GBP"
                             : "EUR"; // default for amazon.de

                // ── Price filter ─────────────────────────────────────────────
                if (price.HasValue)
                {
                    if (minPrice.HasValue && price < minPrice) continue;
                    if (maxPrice.HasValue && price > maxPrice) continue;
                }

                list.Add(new AmazonShoeProduct
                {
                    Asin          = item["asin"]?.ToString()        ?? string.Empty,
                    Name          = item["title"]?.ToString(),
                    Brand         = item["brand"]?.ToString(),
                    ImageUrl      = item["thumbnail"]?.ToString(),
                    ProductUrl    = item["link_clean"]?.ToString()  // use canonical link (no tracking)
                                 ?? item["link"]?.ToString(),
                    Rating        = TryFloat(item["rating"])        ?? 0f,
                    ReviewCount   = TryInt(item["reviews"])         ?? 0,
                    Price         = price,
                    OriginalPrice = origPrice,
                    Currency      = currency,
                    Category      = type.Trim(),
                    Gender        = normalizedGender,
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

using System.Net.Http.Headers;
using System.Text.Json;
using Api.Config;
using Domain.Model;
using Microsoft.Extensions.Options;

namespace Api.Services
{
    /// <summary>
    /// Calls the eBay Browse API (buy/browse/v1/item_summary/search)
    /// to retrieve shoe listings. Injected via typed HttpClient.
    /// </summary>
    public class EbayBrowseService
    {
        private readonly EbayOptions _opts;
        private readonly HttpClient  _http;
        private readonly ILogger<EbayBrowseService> _log;

        public EbayBrowseService(
            IOptions<EbayOptions> options,
            HttpClient http,
            ILogger<EbayBrowseService> log)
        {
            _opts = options.Value;
            _http = http;
            _log  = log;
            _http.BaseAddress = new Uri("https://api.ebay.com/");
            _http.Timeout     = TimeSpan.FromSeconds(_opts.TimeoutSeconds);
        }

        /// <summary>
        /// Searches eBay for shoes of the given type, optionally targeting a gender segment and filtering by price.
        /// Returns at most <see cref="EbayOptions.MaxResults"/> items.
        /// </summary>
        public async Task<List<EbayShoeProduct>> SearchAsync(
            string   type,
            string?  gender,
            decimal? minPrice,
            decimal? maxPrice,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_opts.OAuthToken) ||
                _opts.OAuthToken.StartsWith("YOUR_EBAY"))
            {
                _log.LogWarning("eBay OAuthToken not configured – returning empty list");
                return [];
            }

            // ── Build URL ────────────────────────────────────────────────────
            var normalizedGender = string.IsNullOrEmpty(gender) || gender == "all" ? null : gender.Trim().ToLowerInvariant();
            var genderPrefix     = normalizedGender is not null ? normalizedGender + " " : "";
            var q = Uri.EscapeDataString(genderPrefix + type.Trim() + " shoes");

            // eBay Browse API price filter: combine range into one filter param
            // e.g. filter=price:[20..150],priceCurrency:EUR
            var filterParts = new List<string>();
            if (minPrice.HasValue || maxPrice.HasValue)
            {
                var lo = minPrice.HasValue ? minPrice.Value.ToString("F2") : "";
                var hi = maxPrice.HasValue ? maxPrice.Value.ToString("F2") : "";
                filterParts.Add($"price:[{lo}..{hi}]");
                filterParts.Add($"priceCurrency:{_opts.Currency}");
            }

            var url = $"buy/browse/v1/item_summary/search?q={q}&limit={_opts.MaxResults}";
            if (filterParts.Count > 0)
                url += "&filter=" + Uri.EscapeDataString(string.Join(",", filterParts));

            _log.LogInformation("eBay Browse query: {Query} marketplace={Marketplace}", q, _opts.Marketplace);

            // ── Headers ──────────────────────────────────────────────────────
            // Set per-request (token can change if refreshed in future)
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _opts.OAuthToken);
            request.Headers.Add("X-EBAY-C-MARKETPLACE-ID", _opts.Marketplace);
            // Contextual location for relevance (matches marketplace)
            var countryCode = _opts.Marketplace.Replace("EBAY_", "");
            request.Headers.Add("X-EBAY-C-ENDUSERCTX",
                $"contextualLocation=country={countryCode}");

            // ── Send ─────────────────────────────────────────────────────────
            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "eBay HTTP request failed for query={Query}", q);
                throw;
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _log.LogError("eBay returned {Status} for query={Query}. Body: {Body}",
                    (int)response.StatusCode, q,
                    body[..Math.Min(body.Length, 400)]);
                throw new HttpRequestException(
                    $"eBay API {(int)response.StatusCode} {response.ReasonPhrase}: {body[..Math.Min(body.Length, 200)]}");
            }

            // ── Parse ─────────────────────────────────────────────────────────
            var jsonStr = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(jsonStr);
            var root = doc.RootElement;

            if (!root.TryGetProperty("itemSummaries", out var summaries))
            {
                _log.LogWarning("eBay returned no itemSummaries for query={Query}", q);
                return [];
            }

            var list = new List<EbayShoeProduct>();

            foreach (var item in summaries.EnumerateArray())
            {
                // ── Price ───────────────────────────────────────────────────
                decimal? price    = null;
                string?  currency = _opts.Currency;
                if (item.TryGetProperty("price", out var priceEl))
                {
                    if (priceEl.TryGetProperty("value", out var pv) &&
                        decimal.TryParse(pv.GetString(),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var pVal) && pVal > 0)
                    {
                        price = pVal;
                    }

                    if (priceEl.TryGetProperty("currency", out var cv))
                        currency = cv.GetString();
                }

                // ── Seller feedback (closest to "rating" eBay has) ───────────
                float  feedbackScore = 0;
                int    feedbackCount = 0;
                if (item.TryGetProperty("seller", out var seller))
                {
                    if (seller.TryGetProperty("feedbackPercentage", out var fp))
                        float.TryParse(fp.GetString(),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out feedbackScore);

                    if (seller.TryGetProperty("feedbackScore", out var fs))
                        int.TryParse(fs.GetRawText(), out feedbackCount);
                }

                list.Add(new EbayShoeProduct
                {
                    EbayItemId  = GetStr(item, "itemId")    ?? string.Empty,
                    Name        = GetStr(item, "title"),
                    Brand       = GetStr(item, "brand"),      // not always present
                    Condition   = GetStr(item, "condition"),
                    Price       = price,
                    Currency    = currency,
                    Rating      = feedbackScore / 20f,        // 0-100 % → 0-5 stars
                    ReviewCount = feedbackCount,
                    TrendScore  = ShoeScoring.Compute(feedbackScore / 20f, feedbackCount, price),
                    ImageUrl    = item.TryGetProperty("image", out var img)
                                    ? GetStr(img, "imageUrl")
                                    : GetStr(item, "thumbnailImages"),
                    ProductUrl  = GetStr(item, "itemWebUrl"),
                    Category    = type.Trim(),
                    Gender      = normalizedGender,
                    Marketplace = _opts.Marketplace,
                    LastSynced  = DateTime.UtcNow,
                    CreatedAt   = DateTime.UtcNow,
                });
            }

            _log.LogInformation("eBay Browse fetched {Count} items for query={Query}", list.Count, q);
            return list;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string? GetStr(JsonElement el, string prop)
            => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
               ? v.GetString()
               : null;
    }
}

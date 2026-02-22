using System.Text.Json;
using Api.Config;
using Domain.Model;
using Microsoft.Extensions.Options;

namespace Api.Services
{
    /// <summary>
    /// Calls SerpAPI's Google Shopping engine to retrieve shoe listings.
    /// Injected via typed HttpClient.
    /// </summary>
    public class GoogleShoppingService
    {
        private readonly SerpApiOptions          _serpOpts;
        private readonly GoogleShoppingOptions   _gOpts;
        private readonly HttpClient              _http;
        private readonly ILogger<GoogleShoppingService> _log;

        public GoogleShoppingService(
            IOptions<SerpApiOptions>        serpOptions,
            IOptions<GoogleShoppingOptions> gOptions,
            HttpClient                      http,
            ILogger<GoogleShoppingService>  log)
        {
            _serpOpts = serpOptions.Value;
            _gOpts    = gOptions.Value;
            _http     = http;
            _http.Timeout = TimeSpan.FromSeconds(_gOpts.TimeoutSeconds);
            _log      = log;
        }

        /// <summary>
        /// Searches Google Shopping for shoes of the given type, optionally
        /// targeting a gender segment and filtering by price.
        /// </summary>
        public async Task<List<GoogleShoppingProduct>> FetchAsync(
            string  type,
            string? gender,
            int?    minPrice,
            int?    maxPrice,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_serpOpts.ApiKey) || _serpOpts.ApiKey == "YOUR_KEY")
            {
                _log.LogWarning("SerpApi ApiKey not configured – returning empty list");
                return [];
            }

            var normalizedGender = string.IsNullOrEmpty(gender) || gender == "all"
                ? null
                : gender.Trim().ToLowerInvariant();
            var genderPrefix = normalizedGender is not null ? normalizedGender + " " : "";
            var query        = Uri.EscapeDataString(genderPrefix + type.Trim() + " shoes");

            var url = $"https://serpapi.com/search.json"
                    + $"?engine=google_shopping"
                    + $"&q={query}"
                    + $"&api_key={_serpOpts.ApiKey}"
                    + $"&gl={_gOpts.CountryCode}"
                    + $"&hl={_gOpts.Language}"
                    + $"&num={_gOpts.MaxResults}";

            _log.LogInformation("Google Shopping query: {Query} gl={Gl} hl={Hl}", query, _gOpts.CountryCode, _gOpts.Language);

            string jsonStr;
            try
            {
                using var request  = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await _http.SendAsync(request, ct);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    _log.LogError("SerpApi returned {Status} for Google Shopping query={Query}. Body: {Body}",
                        (int)response.StatusCode, query, body[..Math.Min(body.Length, 400)]);
                    throw new HttpRequestException(
                        $"Google Shopping: response {(int)response.StatusCode} ({response.ReasonPhrase}): {body[..Math.Min(body.Length, 200)]}");
                }

                jsonStr = await response.Content.ReadAsStringAsync(ct);
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Google Shopping HTTP request failed for query={Query}", query);
                throw;
            }

            using var doc = JsonDocument.Parse(jsonStr);
            var root = doc.RootElement;

            if (!root.TryGetProperty("shopping_results", out var results))
            {
                _log.LogWarning("Google Shopping returned no shopping_results for query={Query}", query);
                return [];
            }

            var list     = new List<GoogleShoppingProduct>();
            int position = 1;

            foreach (var item in results.EnumerateArray().Take(_gOpts.MaxResults))
            {
                // ── Price ────────────────────────────────────────────────────
                // SerpAPI returns "price": "$69.99" or "65,00 €" (locale-dependent)
                decimal? price    = null;
                string?  currency = _gOpts.Currency;

                if (item.TryGetProperty("price", out var priceEl))
                {
                    var priceStr = priceEl.ValueKind == JsonValueKind.String
                        ? priceEl.GetString() ?? ""
                        : priceEl.GetRawText();

                    // Detect currency from symbol
                    if (priceStr.Contains('€'))      currency = "EUR";
                    else if (priceStr.Contains('$'))  currency = "USD";
                    else if (priceStr.Contains('£'))  currency = "GBP";

                    // Strip all non-numeric except decimal
                    var clean = new string(priceStr
                        .Replace(',', '.')
                        .Where(c => char.IsDigit(c) || c == '.')
                        .ToArray());

                    // If multiple dots, keep only last (e.g. "1.299.99" → "1299.99")
                    var parts = clean.Split('.');
                    if (parts.Length > 2)
                        clean = string.Join("", parts[..^1]) + "." + parts[^1];

                    if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var pVal) && pVal > 0)
                        price = pVal;
                }

                // ── Extracted price (numeric field, more reliable when present) ──
                if (!price.HasValue && item.TryGetProperty("extracted_price", out var ep))
                {
                    try { price = ep.GetDecimal(); } catch { }
                }

                // ── Price filter ─────────────────────────────────────────────
                if (price.HasValue)
                {
                    if (minPrice.HasValue && price < minPrice) continue;
                    if (maxPrice.HasValue && price > maxPrice) continue;
                }

                // ── Rating ───────────────────────────────────────────────────
                float rating      = 0f;
                int   reviewCount = 0;

                if (item.TryGetProperty("rating", out var rEl))
                    try { rating = rEl.GetSingle(); } catch { float.TryParse(rEl.GetRawText(), out rating); }

                if (item.TryGetProperty("reviews", out var revEl))
                    try { reviewCount = revEl.GetInt32(); } catch { int.TryParse(revEl.GetRawText(), out reviewCount); }

                // ── Product ID ───────────────────────────────────────────────
                string? productId = null;
                if (item.TryGetProperty("product_id", out var pidEl))
                    productId = pidEl.ValueKind == JsonValueKind.String ? pidEl.GetString() : null;

                // Fallback: use position-based synthetic ID
                if (string.IsNullOrWhiteSpace(productId))
                    productId = null; // allow null, handled by partial unique index in DB

                list.Add(new GoogleShoppingProduct
                {
                    ProductId   = productId,
                    Title       = GetStr(item, "title"),
                    Brand       = GetStr(item, "brand"),
                    Price       = price,
                    Currency    = currency,
                    Rating      = rating,
                    ReviewCount = reviewCount,
                    Position    = position++,
                    ImageUrl    = GetStr(item, "thumbnail"),
                    ProductUrl  = GetStr(item, "link"),
                    Category    = type.Trim(),
                    Gender      = normalizedGender,
                    Domain      = $"google.{_gOpts.CountryCode}",
                    TrendScore  = ShoeScoring.Compute(rating, reviewCount, price),
                    LastSynced  = DateTime.UtcNow,
                    CreatedAt   = DateTime.UtcNow,
                });
            }

            _log.LogInformation("Google Shopping fetched {Count} items for query={Query}", list.Count, query);
            return list;
        }

        private static string? GetStr(JsonElement el, string prop)
            => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
               ? v.GetString()
               : null;
    }
}

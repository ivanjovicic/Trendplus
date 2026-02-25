using System.Net.Http.Headers;
using System.Text.Json;
using Api.Config;
using Domain.Model;
using Microsoft.Extensions.Options;

namespace Api.Services
{
    /// <summary>
    /// Calls eBay Browse API (buy/browse/v1/item_summary/search)
    /// to retrieve shoe listings.
    /// Supports automatic OAuth token acquisition via Client Credentials flow.
    /// </summary>
    public class EbayBrowseService
    {
        // Shared token cache across instances (tokens are app-level, not user-level)
        private static readonly Dictionary<string, (string Token, DateTime ExpiresAt)> _tokenCache
            = new(StringComparer.OrdinalIgnoreCase);
        private static readonly SemaphoreSlim _tokenLock = new(1, 1);

        private readonly EbayOptions _opts;
        private readonly HttpClient _http;
        private readonly IOpenProductTrainingSignalProvider _trainingSignals;
        private readonly ILogger<EbayBrowseService> _log;

        public EbayBrowseService(
            IOptions<EbayOptions> options,
            HttpClient http,
            IOpenProductTrainingSignalProvider trainingSignals,
            ILogger<EbayBrowseService> log)
        {
            _opts = options.Value;
            _http = http;
            _trainingSignals = trainingSignals;
            _log = log;
            _http.BaseAddress = new Uri(_opts.IsSandbox
                ? "https://api.sandbox.ebay.com/"
                : "https://api.ebay.com/");
            _http.Timeout = TimeSpan.FromSeconds(_opts.TimeoutSeconds);
        }

        /// <summary>
        /// Searches eBay for shoes of given type, optionally filtered by gender and price.
        /// </summary>
        public async Task<List<EbayShoeProduct>> SearchAsync(
            string type,
            string? gender,
            decimal? minPrice,
            decimal? maxPrice,
            CancellationToken ct = default)
        {
            var hasStaticToken = !string.IsNullOrWhiteSpace(_opts.OAuthToken)
                && !_opts.OAuthToken.StartsWith("YOUR_EBAY", StringComparison.OrdinalIgnoreCase);
            var hasClientCreds = !string.IsNullOrWhiteSpace(_opts.ClientId)
                && !string.IsNullOrWhiteSpace(_opts.ClientSecret);

            if (!hasStaticToken && !hasClientCreds)
            {
                _log.LogWarning("eBay credentials not configured (set ClientId+ClientSecret or OAuthToken); returning empty list.");
                return [];
            }

            var normalizedGender = string.IsNullOrWhiteSpace(gender) || gender == "all"
                ? null
                : gender.Trim().ToLowerInvariant();
            var genderPrefix = normalizedGender is null ? string.Empty : $"{normalizedGender} ";
            var query = Uri.EscapeDataString($"{genderPrefix}{type.Trim()} shoes");

            var filterParts = new List<string>();
            if (minPrice.HasValue || maxPrice.HasValue)
            {
                var lo = minPrice.HasValue ? minPrice.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
                var hi = maxPrice.HasValue ? maxPrice.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
                filterParts.Add($"price:[{lo}..{hi}]");
                filterParts.Add($"priceCurrency:{_opts.Currency}");
            }

            var url = $"buy/browse/v1/item_summary/search?q={query}&limit={_opts.MaxResults}";
            if (filterParts.Count > 0)
            {
                url += "&filter=" + Uri.EscapeDataString(string.Join(",", filterParts));
            }

            var token = await GetOrFetchTokenAsync(ct);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("X-EBAY-C-MARKETPLACE-ID", _opts.Marketplace);

            var countryCode = _opts.Marketplace.Replace("EBAY_", string.Empty, StringComparison.OrdinalIgnoreCase);
            request.Headers.Add("X-EBAY-C-ENDUSERCTX", $"contextualLocation=country={countryCode}");

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "eBay request failed for query={Query}", query);
                throw;
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _log.LogError(
                    "eBay returned status={StatusCode} for query={Query}. body={Body}",
                    (int)response.StatusCode,
                    query,
                    body[..Math.Min(body.Length, 400)]);
                throw new HttpRequestException(
                    $"eBay API {(int)response.StatusCode} {response.ReasonPhrase}: {body[..Math.Min(body.Length, 200)]}");
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("itemSummaries", out var summaries))
            {
                _log.LogWarning("eBay returned no itemSummaries for query={Query}", query);
                return [];
            }

            var items = new List<EbayShoeProduct>();

            foreach (var item in summaries.EnumerateArray())
            {
                decimal? price = null;
                string? currency = _opts.Currency;

                if (item.TryGetProperty("price", out var priceEl))
                {
                    if (priceEl.TryGetProperty("value", out var valueEl) &&
                        decimal.TryParse(
                            valueEl.GetString(),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var parsedPrice) &&
                        parsedPrice > 0)
                    {
                        price = parsedPrice;
                    }

                    if (priceEl.TryGetProperty("currency", out var currencyEl))
                    {
                        currency = currencyEl.GetString();
                    }
                }

                float feedbackScore = 0;
                int feedbackCount = 0;
                if (item.TryGetProperty("seller", out var seller))
                {
                    if (seller.TryGetProperty("feedbackPercentage", out var feedbackPctEl))
                    {
                        float.TryParse(
                            feedbackPctEl.GetString(),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out feedbackScore);
                    }

                    if (seller.TryGetProperty("feedbackScore", out var feedbackCountEl))
                    {
                        if (!int.TryParse(feedbackCountEl.GetRawText(), out feedbackCount))
                        {
                            feedbackCount = 0;
                        }
                    }
                }

                var normalizedRating = feedbackScore / 20f;
                var runtimeSignals = await _trainingSignals.ResolveAsync(
                    GetStr(item, "brand"),
                    type.Trim(),
                    price,
                    ct);

                items.Add(new EbayShoeProduct
                {
                    EbayItemId = GetStr(item, "itemId") ?? string.Empty,
                    Name = GetStr(item, "title"),
                    Brand = GetStr(item, "brand"),
                    Condition = GetStr(item, "condition"),
                    Price = price,
                    Currency = currency,
                    Rating = normalizedRating,
                    ReviewCount = feedbackCount,
                    TrendScore = ShoeScoring.Compute(
                        normalizedRating,
                        feedbackCount,
                        price,
                        runtimeSignals.PopularityPriorScore,
                        runtimeSignals.DealScore),
                    ImageUrl = item.TryGetProperty("image", out var imageEl)
                        ? GetStr(imageEl, "imageUrl")
                        : GetStr(item, "thumbnailImages"),
                    ProductUrl = GetStr(item, "itemWebUrl"),
                    Category = type.Trim(),
                    Gender = normalizedGender,
                    Marketplace = _opts.Marketplace,
                    LastSynced = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                });
            }

            _log.LogInformation("eBay fetched {Count} items for query={Query}", items.Count, query);
            return items;
        }

        /// <summary>
        /// Returns a valid Bearer token — either the static override from config or a freshly
        /// fetched (and cached) Client Credentials token from the eBay OAuth endpoint.
        /// </summary>
        private async Task<string> GetOrFetchTokenAsync(CancellationToken ct)
        {
            // Prefer static override
            if (!string.IsNullOrWhiteSpace(_opts.OAuthToken) &&
                !_opts.OAuthToken.StartsWith("YOUR_EBAY", StringComparison.OrdinalIgnoreCase))
                return _opts.OAuthToken;

            var cacheKey = _opts.ClientId;

            // Fast path — check cache without locking
            if (_tokenCache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow < cached.ExpiresAt)
                return cached.Token;

            await _tokenLock.WaitAsync(ct);
            try
            {
                // Double-check after acquiring lock
                if (_tokenCache.TryGetValue(cacheKey, out cached) && DateTime.UtcNow < cached.ExpiresAt)
                    return cached.Token;

                var credentials = Convert.ToBase64String(
                    System.Text.Encoding.UTF8.GetBytes($"{_opts.ClientId}:{_opts.ClientSecret}"));

                using var tokenReq = new HttpRequestMessage(HttpMethod.Post, "identity/v1/oauth2/token");
                tokenReq.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                tokenReq.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["scope"]      = "https://api.ebay.com/oauth/api_scope"
                });

                HttpResponseMessage tokenResp;
                try
                {
                    tokenResp = await _http.SendAsync(tokenReq, ct);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "eBay token request failed");
                    throw;
                }

                var body = await tokenResp.Content.ReadAsStringAsync(ct);
                if (!tokenResp.IsSuccessStatusCode)
                {
                    _log.LogError("eBay OAuth token fetch failed {Status}: {Body}",
                        (int)tokenResp.StatusCode, body[..Math.Min(body.Length, 400)]);
                    throw new HttpRequestException(
                        $"eBay OAuth {(int)tokenResp.StatusCode}: {body[..Math.Min(body.Length, 200)]}");
                }

                using var doc = JsonDocument.Parse(body);
                var newToken   = doc.RootElement.GetProperty("access_token").GetString()!;
                var expiresIn  = doc.RootElement.TryGetProperty("expires_in", out var expEl)
                    ? expEl.GetInt32() : 7200;

                // Cache with 2-minute safety buffer
                _tokenCache[cacheKey] = (newToken, DateTime.UtcNow.AddSeconds(expiresIn - 120));
                _log.LogInformation("eBay OAuth token fetched (sandbox={IsSandbox}), expires in {Sec}s",
                    _opts.IsSandbox, expiresIn);

                return newToken;
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        private static string? GetStr(JsonElement el, string prop)
            => el.TryGetProperty(prop, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }
}

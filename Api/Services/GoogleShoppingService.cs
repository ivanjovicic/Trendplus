using System.Text.Json;
using Api.Config;
using Domain.Model;
using Microsoft.Extensions.Options;

namespace Api.Services
{
    /// <summary>
    /// Calls SerpAPI Google Shopping engine and maps results to analytics model.
    /// </summary>
    public class GoogleShoppingService
    {
        private readonly SerpApiOptions _serpOptions;
        private readonly GoogleShoppingOptions _googleOptions;
        private readonly HttpClient _http;
        private readonly IOpenProductTrainingSignalProvider _trainingSignals;
        private readonly ILogger<GoogleShoppingService> _log;

        public GoogleShoppingService(
            IOptions<SerpApiOptions> serpOptions,
            IOptions<GoogleShoppingOptions> googleOptions,
            HttpClient http,
            IOpenProductTrainingSignalProvider trainingSignals,
            ILogger<GoogleShoppingService> log)
        {
            _serpOptions = serpOptions.Value;
            _googleOptions = googleOptions.Value;
            _http = http;
            _trainingSignals = trainingSignals;
            _log = log;
            _http.Timeout = TimeSpan.FromSeconds(_googleOptions.TimeoutSeconds);
        }

        public async Task<List<GoogleShoppingProduct>> FetchAsync(
            string type,
            string? gender,
            int? minPrice,
            int? maxPrice,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_serpOptions.ApiKey) ||
                _serpOptions.ApiKey == "YOUR_KEY")
            {
                _log.LogWarning("SerpAPI API key is not configured; returning empty list.");
                return [];
            }

            var normalizedGender = string.IsNullOrWhiteSpace(gender) || gender == "all"
                ? null
                : gender.Trim().ToLowerInvariant();
            var genderPrefix = normalizedGender is null ? string.Empty : $"{normalizedGender} ";
            var query = Uri.EscapeDataString($"{genderPrefix}{type.Trim()} shoes");

            var url = "https://serpapi.com/search.json"
                + "?engine=google_shopping"
                + $"&q={query}"
                + $"&api_key={_serpOptions.ApiKey}"
                + $"&gl={_googleOptions.CountryCode}"
                + $"&hl={_googleOptions.Language}"
                + $"&num={_googleOptions.MaxResults}";

            string json;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await _http.SendAsync(request, ct);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    _log.LogError(
                        "Google Shopping request failed status={StatusCode} query={Query} body={Body}",
                        (int)response.StatusCode,
                        query,
                        body[..Math.Min(body.Length, 400)]);
                    throw new HttpRequestException(
                        $"Google Shopping API {(int)response.StatusCode} {response.ReasonPhrase}: {body[..Math.Min(body.Length, 200)]}");
                }

                json = await response.Content.ReadAsStringAsync(ct);
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Google Shopping request failed for query={Query}", query);
                throw;
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("shopping_results", out var shoppingResults))
            {
                _log.LogWarning("Google Shopping returned no shopping_results for query={Query}", query);
                return [];
            }

            var list = new List<GoogleShoppingProduct>();
            var position = 1;

            foreach (var item in shoppingResults.EnumerateArray().Take(_googleOptions.MaxResults))
            {
                decimal? price = null;
                string? currency = _googleOptions.Currency;

                if (item.TryGetProperty("price", out var priceElement))
                {
                    var rawPrice = priceElement.ValueKind == JsonValueKind.String
                        ? priceElement.GetString() ?? string.Empty
                        : priceElement.GetRawText();

                    if (rawPrice.Contains('€'))
                    {
                        currency = "EUR";
                    }
                    else if (rawPrice.Contains('$'))
                    {
                        currency = "USD";
                    }
                    else if (rawPrice.Contains('£'))
                    {
                        currency = "GBP";
                    }

                    var cleaned = new string(rawPrice
                        .Replace(',', '.')
                        .Where(c => char.IsDigit(c) || c == '.')
                        .ToArray());

                    var parts = cleaned.Split('.');
                    if (parts.Length > 2)
                    {
                        cleaned = string.Join(string.Empty, parts[..^1]) + "." + parts[^1];
                    }

                    if (decimal.TryParse(
                            cleaned,
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var parsedPrice) &&
                        parsedPrice > 0)
                    {
                        price = parsedPrice;
                    }
                }

                if (!price.HasValue && item.TryGetProperty("extracted_price", out var extractedPrice))
                {
                    try
                    {
                        price = extractedPrice.GetDecimal();
                    }
                    catch
                    {
                        // ignored
                    }
                }

                if (price.HasValue)
                {
                    if (minPrice.HasValue && price.Value < minPrice.Value)
                    {
                        continue;
                    }

                    if (maxPrice.HasValue && price.Value > maxPrice.Value)
                    {
                        continue;
                    }
                }

                float rating = 0f;
                int reviewCount = 0;

                if (item.TryGetProperty("rating", out var ratingElement))
                {
                    try
                    {
                        rating = ratingElement.GetSingle();
                    }
                    catch
                    {
                        if (!float.TryParse(ratingElement.GetRawText(), out rating))
                        {
                            rating = 0f;
                        }
                    }
                }

                if (item.TryGetProperty("reviews", out var reviewsElement))
                {
                    try
                    {
                        reviewCount = reviewsElement.GetInt32();
                    }
                    catch
                    {
                        if (!int.TryParse(reviewsElement.GetRawText(), out reviewCount))
                        {
                            reviewCount = 0;
                        }
                    }
                }

                string? productId = null;
                if (item.TryGetProperty("product_id", out var productIdElement) &&
                    productIdElement.ValueKind == JsonValueKind.String)
                {
                    productId = productIdElement.GetString();
                }

                var brand = GetString(item, "brand");
                var runtimeSignals = await _trainingSignals.ResolveAsync(brand, type.Trim(), price, ct);

                list.Add(new GoogleShoppingProduct
                {
                    ProductId = productId,
                    Title = GetString(item, "title"),
                    Brand = brand,
                    Price = price,
                    Currency = currency,
                    Rating = rating,
                    ReviewCount = reviewCount,
                    Position = position++,
                    ImageUrl = GetString(item, "thumbnail"),
                    ProductUrl = GetString(item, "link"),
                    Category = type.Trim(),
                    Gender = normalizedGender,
                    Domain = $"google.{_googleOptions.CountryCode}",
                    TrendScore = ShoeScoring.Compute(
                        rating,
                        reviewCount,
                        price,
                        runtimeSignals.PopularityPriorScore,
                        runtimeSignals.DealScore),
                    LastSynced = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                });
            }

            _log.LogInformation("Google Shopping fetched {Count} items for query={Query}", list.Count, query);
            return list;
        }

        private static string? GetString(JsonElement element, string propertyName)
            => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }
}

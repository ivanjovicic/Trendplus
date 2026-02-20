using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Trendplus2.Dtos;

namespace Trendplus2.Services
{
    public interface ICommonMatchesClient
    {
        Task<List<CommonProductItemDto>> GetCommonMatchesAsync(CommonMatchesFilters filters, CancellationToken ct = default);
    }

    public class CommonMatchesClient : ICommonMatchesClient
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly IDistributedCache _cache;
        private readonly ILogger<CommonMatchesClient> _logger;

        public CommonMatchesClient(HttpClient http, IConfiguration config, IDistributedCache cache, ILogger<CommonMatchesClient> logger)
        {
            _http = http;
            _config = config;
            _cache = cache;
            _logger = logger;
        }

        private string BuildCacheKey(CommonMatchesFilters f)
        {
            var raw = $"{f.Gender}|{f.Category}|{f.Brand}|{f.PriceMin}|{f.PriceMax}|{f.Sort}|{f.Sale}|{f.IsNew}|{f.Pages}";
            var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw));
            return "common-matches:" + base64;
        }

        public async Task<List<CommonProductItemDto>> GetCommonMatchesAsync(CommonMatchesFilters filters, CancellationToken ct = default)
        {
            var cacheKey = BuildCacheKey(filters);
            var cached = await _cache.GetStringAsync(cacheKey, ct);

            if (!string.IsNullOrEmpty(cached))
            {
                _logger.LogInformation("Common matches cache hit: {Key}", cacheKey);
                var cachedResponse = System.Text.Json.JsonSerializer.Deserialize<List<CommonProductItemDto>>(cached);
                if (cachedResponse != null)
                    return cachedResponse;
            }

            _logger.LogInformation("Common matches cache MISS: {Key}", cacheKey);

            var baseUrl = _config["PythonScraper:BaseUrl"] ?? "http://localhost:8000";
            var url = $"{baseUrl.TrimEnd('/')}/scrapers/common";

            var response = await _http.PostAsJsonAsync(url, filters, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Python /scrapers/common error: {Status}", response.StatusCode);
                return new List<CommonProductItemDto>();
            }

            var payload = await response.Content.ReadFromJsonAsync<CommonProductsResponse>(cancellationToken: ct);
            var items = payload?.Items ?? new List<CommonProductItemDto>();

            items = items.OrderByDescending(i => i.Score).ToList();

            var json = System.Text.Json.JsonSerializer.Serialize(items);
            await _cache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15) }, ct);

            return items;
        }
    }
}

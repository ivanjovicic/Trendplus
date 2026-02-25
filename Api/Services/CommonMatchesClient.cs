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
using Api.Models;
using System.Text.Json;

namespace Api.Services
{
    public interface ICommonMatchesClient
    {
        Task<List<CommonProductItem>> GetCommonMatchesAsync(CommonMatchesFilters filters, CancellationToken ct = default);
    }

    public class CommonMatchesClient : ICommonMatchesClient
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;
        private readonly IDistributedCache _cache;
        private readonly ILogger<CommonMatchesClient> _logger;

        public CommonMatchesClient(IHttpClientFactory httpFactory, IConfiguration config, IDistributedCache cache, ILogger<CommonMatchesClient> logger)
        {
            _httpFactory = httpFactory;
            _config = config;
            _cache = cache;
            _logger = logger;
        }

        private static string BuildCacheKey(CommonMatchesFilters f)
        {
            var raw = $"{f.Gender}|{f.Category}|{f.Brand}|{f.PriceMin}|{f.PriceMax}|{f.Sort}|{f.Sale}|{f.IsNew}|{f.Pages}|{f.MinScore}";
            var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw));
            return "common-matches:" + base64;
        }

        public async Task<List<CommonProductItem>> GetCommonMatchesAsync(CommonMatchesFilters filters, CancellationToken ct = default)
        {
            var cacheKey = BuildCacheKey(filters);
            var cached = await _cache.GetStringAsync(cacheKey, ct);

            if (!string.IsNullOrEmpty(cached))
            {
                _logger.LogInformation("Common matches cache hit: {Key}", cacheKey);
                var cachedResponse = JsonSerializer.Deserialize<List<CommonProductItem>>(cached);
                if (cachedResponse != null)
                    return cachedResponse;
            }

            _logger.LogInformation("Common matches cache MISS: {Key}", cacheKey);

            // Use named 'scraper' client so base address and timeout are configured centrally
            var client = _httpFactory.CreateClient("scraper");

            var response = await client.PostAsJsonAsync("/scrapers/common", filters, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Python /scrapers/common error: {Status}", response.StatusCode);
                return new List<CommonProductItem>();
            }

            var payload = await response.Content.ReadFromJsonAsync<CommonProductsResponse>(cancellationToken: ct);
            var items = payload?.Items ?? new List<CommonProductItem>();

            // Filter by MinScore here before caching
            var min = filters?.MinScore > 0 ? filters.MinScore : 60;
            items = items.Where(i => i.Score >= min).OrderByDescending(i => i.Score).ToList();

            var json = JsonSerializer.Serialize(items);
            await _cache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15) }, ct);

            return items;
        }
    }
}

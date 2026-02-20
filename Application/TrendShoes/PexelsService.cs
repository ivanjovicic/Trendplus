using Domain.Model.TrendShoes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace Application.TrendShoes
{
    public class PexelsService
    {
        private readonly string? _apiKey;
        private readonly HttpClient _http;
        private readonly ILogger<PexelsService> _logger;

        public PexelsService(
            IHttpClientFactory httpFactory,
            IConfiguration config,
            ILogger<PexelsService> logger)
        {
            _http = httpFactory.CreateClient();

            _apiKey = config["Pexels:ApiKey"];
            _logger = logger;

            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                try
                {
                    _http.DefaultRequestHeaders.Remove("Authorization");
                    _http.DefaultRequestHeaders.Add("Authorization", _apiKey);
                }
                catch (Exception ex)
                {
                    // Defensive: don't throw during construction - log and continue
                    _logger.LogWarning(ex, "Failed to add Pexels Authorization header");
                }
            }
            else
            {
                _logger.LogWarning("Pexels API key not configured. PexelsService will return mock/empty results.");
            }
        }

        public async Task<List<PexelsPhoto>> Search(string query, int count)
        {
            // If no API key, return empty list to allow fallback to mock behavior upstream
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogInformation("PexelsService.Search skipped because API key is missing");
                return new List<PexelsPhoto>();
            }

            try
            {
                var url = $"https://api.pexels.com/v1/search?query={Uri.EscapeDataString(query)}&per_page={count}";
                var res = await _http.GetFromJsonAsync<PexelsResponse>(url);
                return res?.Photos ?? new List<PexelsPhoto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pexels API request failed for query '{Query}'", query);
                return new List<PexelsPhoto>();
            }
        }
    }
}

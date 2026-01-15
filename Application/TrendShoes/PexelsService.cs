using Domain.Model.TrendShoes;
using Microsoft.Extensions.Configuration;
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
        private readonly string _apiKey;
        private readonly HttpClient _http;

        public PexelsService(
            IHttpClientFactory httpFactory,
            IConfiguration config)
        {
            _http = httpFactory.CreateClient();

            var apiKey = config["Pexels:ApiKey"];
            _http.DefaultRequestHeaders.Add("Authorization", apiKey);
        }

        public async Task<List<PexelsPhoto>> Search(string query, int count)
        {
            var url = $"https://api.pexels.com/v1/search?query={query}&per_page={count}";
            var res = await _http.GetFromJsonAsync<PexelsResponse>(url);
            return res?.Photos ?? new List<PexelsPhoto>();
        }
    }
}

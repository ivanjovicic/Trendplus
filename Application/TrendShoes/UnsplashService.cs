using Domain.Model.TrendShoes;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Application.TrendShoes
{
    public class UnsplashService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;

        public UnsplashService(
            IHttpClientFactory httpFactory,
            IConfiguration config)
        {
            _httpFactory = httpFactory;
            _config = config;
        }

        public async Task<List<UnsplashPhoto>> SearchImages(string query, int count)
        {
            var client = _httpFactory.CreateClient();

            var key = _config["Unsplash:AccessKey"];
            var appName = _config["Unsplash:AppName"] ?? "trendplus";

            var url =
                $"https://api.unsplash.com/search/photos" +
                $"?query={query}&per_page={count}&client_id={key}";

            var response = await client.GetFromJsonAsync<UnsplashResponse>(url);

            // Add UTM parameters to photographer links for proper attribution
            var photos = response!.results.Select(p => 
            {
                if (p.user?.links?.html != null)
                {
                    p.user.links.html = AddUtmParameters(p.user.links.html, appName);
                }
                return p;
            }).ToList();

            return photos;
        }

        private string AddUtmParameters(string url, string appName)
        {
            var separator = url.Contains('?') ? "&" : "?";
            return $"{url}{separator}utm_source={appName}&utm_medium=referral";
        }
    }

    public class UnsplashResponse
    {
        public List<UnsplashPhoto> results { get; set; }
    }
}

using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/release")]
    public class ReleaseController : ControllerBase
    {
        private readonly HttpClient _http;

        public ReleaseController(IHttpClientFactory factory)
        {
            _http = factory.CreateClient("scraper");
        }

        [HttpGet("{gender}")]
        public async Task<IActionResult> GetRelease(string gender)
        {
            try
            {
                var response = await _http.GetFromJsonAsync<object>($"/api/release-calendar?gender={Uri.EscapeDataString(gender)}");
                return Ok(response);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(502, new { error = "Failed to contact scraper service", detail = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Unexpected error", detail = ex.Message });
            }
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.Models;
using Api.Services;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/common-matches")]
    public class CommonMatchesController : ControllerBase
    {
        private readonly ICommonMatchesClient _client;

        public CommonMatchesController(ICommonMatchesClient client)
        {
            _client = client;
        }

        [HttpPost]
        public async Task<ActionResult<List<CommonProductItem>>> Post([FromBody] CommonMatchesFilters filters, CancellationToken ct)
        {
            if (filters == null)
                filters = new CommonMatchesFilters();

            var items = await _client.GetCommonMatchesAsync(filters, ct);

            // Apply MinScore filter (default 60)
            var min = filters.MinScore <= 0 ? 60 : filters.MinScore;
            var filtered = items.Where(i => i.Score >= min).ToList();

            return Ok(filtered);
        }
    }
}

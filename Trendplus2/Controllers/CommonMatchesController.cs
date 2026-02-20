using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Trendplus2.Dtos;
using Trendplus2.Services;

namespace Trendplus2.Controllers
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
        public async Task<ActionResult<List<CommonProductItemDto>>> Get([FromBody] CommonMatchesFilters filters, CancellationToken ct)
        {
            var items = await _client.GetCommonMatchesAsync(filters, ct);
            return Ok(items);
        }
    }
}

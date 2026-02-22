using Api.Services;
using Domain.Model;
using Infrastructure.DbContexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers
{
    /// <summary>
    /// Amazon shoe trend data via SerpAPI.
    ///
    /// GET  /api/shoes/sync?type=sneakers&amp;minPrice=20&amp;maxPrice=150
    ///      Fetches from Amazon (SerpAPI) and upserts into the analytics DB.
    ///
    /// GET  /api/shoes?type=sneakers&amp;page=1&amp;pageSize=20
    ///      Returns cached results from the analytics DB, sorted by rating desc.
    ///
    /// GET  /api/shoes/all?page=1&amp;pageSize=50
    ///      Returns all categories, newest-first.
    //
    /// DELETE /api/shoes/category/{category}
    ///      Removes all records for a given category (useful before a full re-sync).
    /// </summary>
    [ApiController]
    [Route("api/shoes")]
    [Produces("application/json")]
    public class AmazonShoesController : ControllerBase
    {
        private readonly AmazonShoesService  _amazon;
        private readonly AnalyticsDbContext  _db;
        private readonly ILogger<AmazonShoesController> _log;

        public AmazonShoesController(
            AmazonShoesService amazon,
            AnalyticsDbContext db,
            ILogger<AmazonShoesController> log)
        {
            _amazon = amazon;
            _db     = db;
            _log    = log;
        }

        // ── SYNC ────────────────────────────────────────────────────────────

        /// <summary>Fetch from Amazon and upsert into DB.</summary>
        [HttpGet("sync")]
        [ProducesResponseType(typeof(SyncResult), 200)]
        public async Task<IActionResult> Sync(
            [FromQuery] string type      = "sneakers",
            [FromQuery] int?   minPrice  = null,
            [FromQuery] int?   maxPrice  = null,
            CancellationToken  ct        = default)
        {
            if (string.IsNullOrWhiteSpace(type))
                return BadRequest("type is required");

            List<AmazonShoeProduct> fetched;
            try
            {
                fetched = await _amazon.FetchAsync(type, minPrice, maxPrice, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "SerpAPI fetch failed for type={Type}", type);
                return StatusCode(502, new { error = "SerpAPI request failed", detail = ex.Message });
            }

            int inserted = 0, updated = 0;

            foreach (var s in fetched)
            {
                if (string.IsNullOrWhiteSpace(s.Asin)) continue;

                var existing = await _db.AmazonShoeProducts
                    .FirstOrDefaultAsync(x => x.Asin == s.Asin, ct);

                if (existing is null)
                {
                    _db.AmazonShoeProducts.Add(s);
                    inserted++;
                }
                else
                {
                    existing.Name          = s.Name;
                    existing.Brand         = s.Brand;
                    existing.Price         = s.Price;
                    existing.OriginalPrice = s.OriginalPrice;
                    existing.Currency      = s.Currency;
                    existing.Rating        = s.Rating;
                    existing.ReviewCount   = s.ReviewCount;
                    existing.ImageUrl      = s.ImageUrl;
                    existing.ProductUrl    = s.ProductUrl;
                    existing.Category      = s.Category;
                    existing.Domain        = s.Domain;
                    existing.LastSynced    = DateTime.UtcNow;
                    updated++;
                }
            }

            await _db.SaveChangesAsync(ct);

            return Ok(new SyncResult
            {
                Total    = fetched.Count,
                Inserted = inserted,
                Updated  = updated,
                Type     = type,
            });
        }

        // ── GET by type ─────────────────────────────────────────────────────

        /// <summary>Returns shoes for a specific category, ordered by rating desc.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<AmazonShoeProduct>), 200)]
        public async Task<IActionResult> GetByType(
            [FromQuery] string type     = "sneakers",
            [FromQuery] int    page     = 1,
            [FromQuery] int    pageSize = 20,
            CancellationToken  ct       = default)
        {
            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _db.AmazonShoeProducts
                .Where(x => x.Category == type)
                .OrderByDescending(x => x.Rating)
                .ThenByDescending(x => x.ReviewCount);

            var total = await query.CountAsync(ct);
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return Ok(new PagedResult<AmazonShoeProduct>
            {
                Items    = items,
                Total    = total,
                Page     = page,
                PageSize = pageSize,
            });
        }

        // ── GET all ─────────────────────────────────────────────────────────

        /// <summary>Returns all categories, newest-synced first.</summary>
        [HttpGet("all")]
        [ProducesResponseType(typeof(PagedResult<AmazonShoeProduct>), 200)]
        public async Task<IActionResult> GetAll(
            [FromQuery] int   page     = 1,
            [FromQuery] int   pageSize = 50,
            CancellationToken ct       = default)
        {
            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var query = _db.AmazonShoeProducts
                .OrderByDescending(x => x.LastSynced)
                .ThenByDescending(x => x.Rating);

            var total = await query.CountAsync(ct);
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return Ok(new PagedResult<AmazonShoeProduct>
            {
                Items    = items,
                Total    = total,
                Page     = page,
                PageSize = pageSize,
            });
        }

        // ── GET categories summary ───────────────────────────────────────────

        /// <summary>Returns count + avg rating per category in the DB.</summary>
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories(CancellationToken ct = default)
        {
            var cats = await _db.AmazonShoeProducts
                .GroupBy(x => x.Category)
                .Select(g => new
                {
                    Category    = g.Key,
                    Count       = g.Count(),
                    AvgRating   = g.Average(x => (double)x.Rating),
                    AvgPrice    = g.Average(x => (double?)x.Price),
                    LastSynced  = g.Max(x => x.LastSynced),
                })
                .OrderByDescending(x => x.Count)
                .ToListAsync(ct);

            return Ok(cats);
        }

        // ── DELETE by category ───────────────────────────────────────────────

        /// <summary>Removes all records for a category.</summary>
        [HttpDelete("category/{category}")]
        public async Task<IActionResult> DeleteCategory(string category, CancellationToken ct = default)
        {
            var rows = await _db.AmazonShoeProducts
                .Where(x => x.Category == category)
                .ToListAsync(ct);

            _db.AmazonShoeProducts.RemoveRange(rows);
            await _db.SaveChangesAsync(ct);

            return Ok(new { deleted = rows.Count, category });
        }
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    public record SyncResult
    {
        public int    Total    { get; init; }
        public int    Inserted { get; init; }
        public int    Updated  { get; init; }
        public string Type     { get; init; } = string.Empty;
    }

    public record PagedResult<T>
    {
        public List<T> Items    { get; init; } = [];
        public int     Total    { get; init; }
        public int     Page     { get; init; }
        public int     PageSize { get; init; }
        public int     Pages    => PageSize > 0 ? (int)Math.Ceiling((double)Total / PageSize) : 0;
    }
}

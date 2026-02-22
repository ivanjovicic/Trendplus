using Api.Services;
using Domain.Model;
using Infrastructure.DbContexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/ebay/shoes")]
    [Produces("application/json")]
    public class EbayShoesController : ControllerBase
    {
        private readonly EbayBrowseService  _ebay;
        private readonly AnalyticsDbContext _db;
        private readonly IMemoryCache       _cache;
        private readonly ILogger<EbayShoesController> _log;

        private const string CacheCats = "ebay:cats";

        public EbayShoesController(
            EbayBrowseService ebay,
            AnalyticsDbContext db,
            IMemoryCache cache,
            ILogger<EbayShoesController> log)
        {
            _ebay  = ebay;
            _db    = db;
            _cache = cache;
            _log   = log;
        }

        // ── SYNC ──────────────────────────────────────────────────────────────

        /// <summary>Fetch from eBay Browse API and upsert into Analytics DB.</summary>
        [HttpGet("sync")]
        [ProducesResponseType(typeof(EbaySyncResult), 200)]
        public async Task<IActionResult> Sync(
            [FromQuery] string   type     = "sneakers",
            [FromQuery] string?  gender   = null,
            [FromQuery] decimal? minPrice = null,
            [FromQuery] decimal? maxPrice = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(type))
                return BadRequest("type is required");

            List<EbayShoeProduct> fetched;
            try
            {
                fetched = await _ebay.SearchAsync(type, gender, minPrice, maxPrice, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "eBay fetch failed for type={Type}", type);
                return StatusCode(502, new { error = "eBay API request failed", detail = ex.Message });
            }

            int inserted = 0, updated = 0;

            foreach (var s in fetched)
            {
                if (string.IsNullOrWhiteSpace(s.EbayItemId)) continue;

                var existing = await _db.EbayShoeProducts
                    .FirstOrDefaultAsync(x => x.EbayItemId == s.EbayItemId, ct);

                if (existing is null)
                {
                    _db.EbayShoeProducts.Add(s);
                    inserted++;
                }
                else
                {
                    existing.Name        = s.Name;
                    existing.Brand       = s.Brand;
                    existing.Condition   = s.Condition;
                    existing.Price       = s.Price;
                    existing.Currency    = s.Currency;
                    existing.Rating      = s.Rating;
                    existing.ReviewCount = s.ReviewCount;
                    existing.TrendScore  = s.TrendScore;
                    existing.ImageUrl    = s.ImageUrl;
                    existing.ProductUrl  = s.ProductUrl;
                    existing.Gender      = s.Gender;
                    existing.LastSynced  = DateTime.UtcNow;
                    updated++;
                }
            }

            await _db.SaveChangesAsync(ct);
            _cache.Remove(CacheCats);

            return Ok(new EbaySyncResult(fetched.Count, inserted, updated, type));
        }

        // ── GET BY TYPE ───────────────────────────────────────────────────────

        /// <summary>Return stored eBay shoes for a given category, ordered by rating.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(EbayPagedResult<EbayShoeProduct>), 200)]
        public async Task<IActionResult> GetByType(
            [FromQuery] string  type     = "sneakers",
            [FromQuery] string? gender   = null,
            [FromQuery] string  sortBy   = "rating",
            [FromQuery] int     page     = 1,
            [FromQuery] int     pageSize = 20,
            CancellationToken ct = default)
        {
            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var genderKey = gender ?? "all";
            var cacheKey  = $"ebay:list:{type}:{genderKey}:{sortBy}:{page}:{pageSize}";

            if (_cache.TryGetValue(cacheKey, out EbayPagedResult<EbayShoeProduct>? cached) && cached is not null)
                return Ok(cached);

            var q = _db.EbayShoeProducts.Where(x => x.Category == type);

            if (!string.IsNullOrEmpty(gender) && gender != "all")
                q = q.Where(x => x.Gender == gender);

            IOrderedQueryable<EbayShoeProduct> ordered = sortBy switch
            {
                "score"      => q.OrderByDescending(x => x.TrendScore),
                "popular"    => q.OrderByDescending(x => x.ReviewCount).ThenByDescending(x => x.Rating),
                "price_asc"  => q.OrderBy(x => x.Price).ThenByDescending(x => x.Rating),
                "price_desc" => q.OrderByDescending(x => x.Price).ThenByDescending(x => x.Rating),
                "newest"     => q.OrderByDescending(x => x.LastSynced).ThenByDescending(x => x.Rating),
                _            => q.OrderByDescending(x => x.Rating).ThenByDescending(x => x.Price),
            };

            var total = await ordered.CountAsync(ct);
            var items = await ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var result = new EbayPagedResult<EbayShoeProduct>(items, total, page, pageSize);
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(2));
            return Ok(result);
        }

        // ── GET ALL ───────────────────────────────────────────────────────────

        /// <summary>Return all stored eBay shoes paginated.</summary>
        [HttpGet("all")]
        [ProducesResponseType(typeof(EbayPagedResult<EbayShoeProduct>), 200)]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page     = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken ct = default)
        {
            var total = await _db.EbayShoeProducts.CountAsync(ct);
            var items = await _db.EbayShoeProducts
                .OrderByDescending(x => x.LastSynced)
                .ThenByDescending(x => x.Rating)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return Ok(new EbayPagedResult<EbayShoeProduct>(items, total, page, pageSize));
        }

        // ── CATEGORIES ────────────────────────────────────────────────────────

        /// <summary>Category summary: count, avg rating, avg price, last synced.</summary>
        [HttpGet("categories")]
        [ProducesResponseType(typeof(IEnumerable<EbayCategorySummary>), 200)]
        public async Task<IActionResult> GetCategories(CancellationToken ct = default)
        {
            if (_cache.TryGetValue(CacheCats, out object? cachedCats) && cachedCats is not null)
                return Ok(cachedCats);

            var result = await _db.EbayShoeProducts
                .GroupBy(x => x.Category)
                .Select(g => new EbayCategorySummary(
                    g.Key,
                    g.Count(),
                    (float)g.Average(x => x.Rating),
                    g.Any(x => x.Price != null)
                        ? (double?)g.Where(x => x.Price != null).Average(x => (double)x.Price!)
                        : null,
                    g.Max(x => x.LastSynced)))
                .OrderByDescending(x => x.Count)
                .ToListAsync(ct);

            _cache.Set(CacheCats, result, TimeSpan.FromMinutes(5));
            return Ok(result);
        }

        // ── DELETE CATEGORY ───────────────────────────────────────────────────

        /// <summary>Delete all items belonging to a category.</summary>
        [HttpDelete("category/{category}")]
        public async Task<IActionResult> DeleteCategory(
            string category,
            CancellationToken ct = default)
        {
            var rows = await _db.EbayShoeProducts
                .Where(x => x.Category == category)
                .ExecuteDeleteAsync(ct);

            return Ok(new { deleted = rows, category });
        }
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    public record EbaySyncResult(int Total, int Inserted, int Updated, string Type);

    public record EbayPagedResult<T>(
        IEnumerable<T> Items,
        int Total,
        int Page,
        int PageSize)
    {
        public int Pages => (int)Math.Ceiling(Total / (double)PageSize);
    }

    public record EbayCategorySummary(
        string?  Category,
        int      Count,
        float    AvgRating,
        double?  AvgPrice,
        DateTime LastSynced);
}

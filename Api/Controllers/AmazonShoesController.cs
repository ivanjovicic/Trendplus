using Api.Services;
using Domain.Model;
using Infrastructure.DbContexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

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
        private readonly IMemoryCache        _cache;
        private readonly ILogger<AmazonShoesController> _log;

        private const string CacheCats = "amz:cats";

        public AmazonShoesController(
            AmazonShoesService amazon,
            AnalyticsDbContext db,
            IMemoryCache cache,
            ILogger<AmazonShoesController> log)
        {
            _amazon = amazon;
            _db     = db;
            _cache  = cache;
            _log    = log;
        }

        // ── SYNC ────────────────────────────────────────────────────────────

        /// <summary>Fetch from Amazon and upsert into DB.</summary>
        [HttpGet("sync")]
        [ProducesResponseType(typeof(SyncResult), 200)]
        public async Task<IActionResult> Sync(
            [FromQuery] string  type      = "sneakers",
            [FromQuery] string? gender    = null,
            [FromQuery] int?    minPrice  = null,
            [FromQuery] int?    maxPrice  = null,
            CancellationToken   ct        = default)
        {
            if (string.IsNullOrWhiteSpace(type))
                return BadRequest("type is required");

            List<AmazonShoeProduct> fetched;
            try
            {
                fetched = await _amazon.FetchAsync(type, gender, minPrice, maxPrice, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "SerpAPI fetch failed for type={Type}", type);
                return StatusCode(502, new { error = "SerpAPI request failed", detail = ex.Message });
            }

            try
            {
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
                        existing.TrendScore    = s.TrendScore;
                        existing.ImageUrl      = s.ImageUrl;
                        existing.ProductUrl    = s.ProductUrl;
                        existing.Category      = s.Category;
                        existing.Gender        = s.Gender;
                        existing.Domain        = s.Domain;
                        existing.LastSynced    = DateTime.UtcNow;
                        updated++;
                    }
                }

                await _db.SaveChangesAsync(ct);
                _cache.Remove(CacheCats);

                return Ok(new SyncResult
                {
                    Total    = fetched.Count,
                    Inserted = inserted,
                    Updated  = updated,
                    Type     = type,
                });
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                _log.LogWarning(ex, "amazon_shoe_products table is missing; sync cannot persist data.");
                return StatusCode(503, new
                {
                    error = "amazon_shoe_products table is missing",
                    detail = "Pokreni analytics migracije (Database/Analytics/006_AddAmazonShoesTable.sql) i restartuj backend."
                });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Amazon shoes sync persistence failed for type={Type}", type);
                return StatusCode(500, new { error = "Failed to persist Amazon shoes", detail = ex.Message });
            }
        }

        // ── GET by type ─────────────────────────────────────────────────────

        /// <summary>Returns shoes for a specific category, sorted by the chosen strategy.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<AmazonShoeProduct>), 200)]
        public async Task<IActionResult> GetByType(
            [FromQuery] string  type     = "sneakers",
            [FromQuery] string? gender   = null,
            [FromQuery] string  sortBy   = "rating",
            [FromQuery] int     page     = 1,
            [FromQuery] int     pageSize = 20,
            CancellationToken   ct       = default)
        {
            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var genderKey = gender ?? "all";
            var cacheKey  = $"amz:list:{type}:{genderKey}:{sortBy}:{page}:{pageSize}";

            if (_cache.TryGetValue(cacheKey, out PagedResult<AmazonShoeProduct>? cached) && cached is not null)
                return Ok(cached);

            try
            {
                var q = _db.AmazonShoeProducts.Where(x => x.Category == type);

                if (!string.IsNullOrEmpty(gender) && gender != "all")
                    q = q.Where(x => x.Gender == gender);

                IOrderedQueryable<AmazonShoeProduct> query = sortBy switch
                {
                    "score"      => q.OrderByDescending(x => x.TrendScore),
                    "popular"    => q.OrderByDescending(x => x.ReviewCount).ThenByDescending(x => x.Rating),
                    "price_asc"  => q.OrderBy(x => x.Price).ThenByDescending(x => x.Rating),
                    "price_desc" => q.OrderByDescending(x => x.Price).ThenByDescending(x => x.Rating),
                    "newest"     => q.OrderByDescending(x => x.LastSynced).ThenByDescending(x => x.Rating),
                    _            => q.OrderByDescending(x => x.Rating).ThenByDescending(x => x.ReviewCount),
                };

                var total = await query.CountAsync(ct);
                var items = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(ct);

                var result = new PagedResult<AmazonShoeProduct>
                {
                    Items    = items,
                    Total    = total,
                    Page     = page,
                    PageSize = pageSize,
                };

                _cache.Set(cacheKey, result, TimeSpan.FromMinutes(2));
                return Ok(result);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                _log.LogWarning(ex, "amazon_shoe_products table is missing; returning empty page.");
                return Ok(new PagedResult<AmazonShoeProduct>
                {
                    Items = [],
                    Total = 0,
                    Page = page,
                    PageSize = pageSize
                });
            }
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

            try
            {
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
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                _log.LogWarning(ex, "amazon_shoe_products table is missing; returning empty page.");
                return Ok(new PagedResult<AmazonShoeProduct>
                {
                    Items = [],
                    Total = 0,
                    Page = page,
                    PageSize = pageSize
                });
            }
        }

        // ── GET categories summary ───────────────────────────────────────────

        /// <summary>Returns count + avg rating per category in the DB.</summary>
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories(CancellationToken ct = default)
        {
            if (_cache.TryGetValue(CacheCats, out object? cachedCats) && cachedCats is not null)
                return Ok(cachedCats);

            try
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

                _cache.Set(CacheCats, cats, TimeSpan.FromMinutes(5));
                return Ok(cats);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                _log.LogWarning(ex, "amazon_shoe_products table is missing; returning empty categories.");
                return Ok(Array.Empty<object>());
            }
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

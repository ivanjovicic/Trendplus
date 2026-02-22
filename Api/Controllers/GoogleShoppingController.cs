using Api.Services;
using Domain.Model;
using Infrastructure.DbContexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Api.Controllers
{
    /// <summary>
    /// Google Shopping trend data via SerpAPI.
    ///
    /// GET  /api/google/shopping/sync?type=sneakers&amp;gender=women&amp;minPrice=20&amp;maxPrice=200
    ///      Fetches from Google Shopping and upserts into the analytics DB.
    ///
    /// GET  /api/google/shopping?type=sneakers&amp;gender=all&amp;sortBy=score&amp;page=1&amp;pageSize=20
    ///      Returns cached, paged results sorted by chosen strategy.
    ///
    /// GET  /api/google/shopping/all?page=1&amp;pageSize=50
    ///      Returns all products, newest-synced first.
    ///
    /// GET  /api/google/shopping/categories
    ///      Returns per-category summary (count, avg rating, avg price).
    ///
    /// DELETE /api/google/shopping/category/{category}
    ///      Removes all records for a given category.
    /// </summary>
    [ApiController]
    [Route("api/google/shopping")]
    [Produces("application/json")]
    public class GoogleShoppingController : ControllerBase
    {
        private readonly GoogleShoppingService              _google;
        private readonly AnalyticsDbContext                 _db;
        private readonly IMemoryCache                       _cache;
        private readonly ILogger<GoogleShoppingController>  _log;

        private const string CacheCats = "goo:cats";

        public GoogleShoppingController(
            GoogleShoppingService              google,
            AnalyticsDbContext                 db,
            IMemoryCache                       cache,
            ILogger<GoogleShoppingController>  log)
        {
            _google = google;
            _db     = db;
            _cache  = cache;
            _log    = log;
        }

        // ── SYNC ────────────────────────────────────────────────────────────

        /// <summary>Fetch from Google Shopping and upsert into DB.</summary>
        [HttpGet("sync")]
        [ProducesResponseType(typeof(GoogleSyncResult), 200)]
        public async Task<IActionResult> Sync(
            [FromQuery] string  type     = "sneakers",
            [FromQuery] string? gender   = null,
            [FromQuery] int?    minPrice = null,
            [FromQuery] int?    maxPrice = null,
            CancellationToken   ct       = default)
        {
            if (string.IsNullOrWhiteSpace(type))
                return BadRequest("type is required");

            List<GoogleShoppingProduct> fetched;
            try
            {
                fetched = await _google.FetchAsync(type, gender, minPrice, maxPrice, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Google Shopping fetch failed for type={Type}", type);
                return StatusCode(502, new { error = "Google Shopping request failed", detail = ex.Message });
            }

            int inserted = 0, updated = 0;

            foreach (var s in fetched)
            {
                // Upsert strategy:
                //   • When ProductId is present → match on ProductId
                //   • Otherwise → match on Title + Category (prevent duplicates)
                GoogleShoppingProduct? existing = null;

                if (!string.IsNullOrWhiteSpace(s.ProductId))
                {
                    existing = await _db.GoogleShoppingProducts
                        .FirstOrDefaultAsync(x => x.ProductId == s.ProductId, ct);
                }
                else if (!string.IsNullOrWhiteSpace(s.Title))
                {
                    existing = await _db.GoogleShoppingProducts
                        .FirstOrDefaultAsync(x => x.Title == s.Title && x.Category == s.Category, ct);
                }

                if (existing is null)
                {
                    _db.GoogleShoppingProducts.Add(s);
                    inserted++;
                }
                else
                {
                    existing.Title       = s.Title;
                    existing.Brand       = s.Brand;
                    existing.Price       = s.Price;
                    existing.Currency    = s.Currency;
                    existing.Rating      = s.Rating;
                    existing.ReviewCount = s.ReviewCount;
                    existing.Position    = s.Position;
                    existing.TrendScore  = s.TrendScore;
                    existing.ImageUrl    = s.ImageUrl;
                    existing.ProductUrl  = s.ProductUrl;
                    existing.Category    = s.Category;
                    existing.Gender      = s.Gender;
                    existing.Domain      = s.Domain;
                    existing.LastSynced  = DateTime.UtcNow;
                    updated++;
                }
            }

            await _db.SaveChangesAsync(ct);

            // Invalidate category cache so fresh counts show up
            _cache.Remove(CacheCats);
            // Invalidate all list caches for this type
            foreach (var key in new[] { "all", "men", "women", "unisex" })
                foreach (var sort in new[] { "score", "rating", "popular", "price_asc", "price_desc", "position", "newest" })
                    _cache.Remove($"goo:list:{type}:{key}:{sort}:1:20");

            return Ok(new GoogleSyncResult(fetched.Count, inserted, updated, type));
        }

        // ── GET by type ─────────────────────────────────────────────────────

        /// <summary>Returns Google Shopping items for a category.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(GooglePagedResult<GoogleShoppingProduct>), 200)]
        public async Task<IActionResult> GetByType(
            [FromQuery] string  type     = "sneakers",
            [FromQuery] string? gender   = null,
            [FromQuery] string  sortBy   = "score",
            [FromQuery] int     page     = 1,
            [FromQuery] int     pageSize = 20,
            CancellationToken   ct       = default)
        {
            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var genderKey = gender ?? "all";
            var cacheKey  = $"goo:list:{type}:{genderKey}:{sortBy}:{page}:{pageSize}";

            if (_cache.TryGetValue(cacheKey, out GooglePagedResult<GoogleShoppingProduct>? cached) && cached is not null)
                return Ok(cached);

            var q = _db.GoogleShoppingProducts.Where(x => x.Category == type);

            if (!string.IsNullOrEmpty(gender) && gender != "all")
                q = q.Where(x => x.Gender == gender);

            IOrderedQueryable<GoogleShoppingProduct> ordered = sortBy switch
            {
                "score"      => q.OrderByDescending(x => x.TrendScore),
                "popular"    => q.OrderByDescending(x => x.ReviewCount).ThenByDescending(x => x.Rating),
                "price_asc"  => q.OrderBy(x => x.Price).ThenByDescending(x => x.Rating),
                "price_desc" => q.OrderByDescending(x => x.Price).ThenByDescending(x => x.Rating),
                "position"   => q.OrderBy(x => x.Position).ThenByDescending(x => x.TrendScore),
                "newest"     => q.OrderByDescending(x => x.LastSynced).ThenByDescending(x => x.Rating),
                _            => q.OrderByDescending(x => x.Rating).ThenByDescending(x => x.ReviewCount),
            };

            var total = await ordered.CountAsync(ct);
            var items = await ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var result = new GooglePagedResult<GoogleShoppingProduct>
            {
                Items    = items,
                Total    = total,
                Page     = page,
                PageSize = pageSize,
            };

            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(2));
            return Ok(result);
        }

        // ── GET all ─────────────────────────────────────────────────────────

        /// <summary>Returns all Google Shopping products, newest-synced first.</summary>
        [HttpGet("all")]
        [ProducesResponseType(typeof(GooglePagedResult<GoogleShoppingProduct>), 200)]
        public async Task<IActionResult> GetAll(
            [FromQuery] int   page     = 1,
            [FromQuery] int   pageSize = 50,
            CancellationToken ct       = default)
        {
            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var query = _db.GoogleShoppingProducts
                .OrderByDescending(x => x.LastSynced)
                .ThenByDescending(x => x.TrendScore);

            var total = await query.CountAsync(ct);
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return Ok(new GooglePagedResult<GoogleShoppingProduct>
            {
                Items    = items,
                Total    = total,
                Page     = page,
                PageSize = pageSize,
            });
        }

        // ── GET categories summary ───────────────────────────────────────────

        /// <summary>Returns count + avg rating + avg price per category.</summary>
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories(CancellationToken ct = default)
        {
            if (_cache.TryGetValue(CacheCats, out object? cached) && cached is not null)
                return Ok(cached);

            var cats = await _db.GoogleShoppingProducts
                .GroupBy(x => x.Category)
                .Select(g => new GoogleCategorySummary(
                    g.Key ?? "unknown",
                    g.Count(),
                    Math.Round(g.Average(x => (double)x.Rating), 2),
                    g.Average(x => (double?)x.Price),
                    g.Max(x => x.LastSynced)))
                .OrderByDescending(x => x.Count)
                .ToListAsync(ct);

            _cache.Set(CacheCats, cats, TimeSpan.FromMinutes(5));
            return Ok(cats);
        }

        // ── DELETE by category ───────────────────────────────────────────────

        /// <summary>Removes all records for a category.</summary>
        [HttpDelete("category/{category}")]
        public async Task<IActionResult> DeleteCategory(string category, CancellationToken ct = default)
        {
            var rows = await _db.GoogleShoppingProducts
                .Where(x => x.Category == category)
                .ToListAsync(ct);

            _db.GoogleShoppingProducts.RemoveRange(rows);
            await _db.SaveChangesAsync(ct);

            _cache.Remove(CacheCats);
            return Ok(new { deleted = rows.Count, category });
        }
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    public record GoogleSyncResult(int Total, int Inserted, int Updated, string Type);

    public record GooglePagedResult<T>
    {
        public List<T> Items    { get; init; } = [];
        public int     Total    { get; init; }
        public int     Page     { get; init; }
        public int     PageSize { get; init; }
        public int     Pages    => PageSize > 0 ? (int)Math.Ceiling((double)Total / PageSize) : 0;
    }

    public record GoogleCategorySummary(
        string   Category,
        int      Count,
        double   AvgRating,
        double?  AvgPrice,
        DateTime LastSynced);
}

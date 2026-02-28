using Application.Analytics.Queries.GetInventoryRecommendations;
using Application.Analytics.Queries.GetTrendIndex;
using Application.Analytics.Queries.GetTrendMomentum;
using Application.Analytics.Queries.GetTrendSnapshots;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

/// <summary>
/// Trend Engine rezultati — snapshots, momentum, Trendplus Index i inventory preporuke.
///
/// Podaci dolaze iz:
///   1. Python trend_engine/api.py  (scoring engine)
///   2. C# daily worker             (upisuje snapshot + momentum + index + inventory)
///   3. Ovaj controller             (čita + vraća JSON)
/// </summary>
[ApiController]
[Route("api/trends")]
[EnableRateLimiting("api-v1")]
[Produces("application/json")]
public sealed class TrendsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<TrendsController> _logger;

    public TrendsController(IMediator mediator, ILogger<TrendsController> logger)
    {
        _mediator = mediator;
        _logger   = logger;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  TOP PRODUCTS — snapshot po danu
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Vraća trending proizvode za dati dan, sortirane po score-u.
    ///
    /// GET /api/trends/top-products?date=2026-02-28&amp;market=DE&amp;top=50
    /// </summary>
    [HttpGet("top-products")]
    [ProducesResponseType(typeof(TrendSnapshotsResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTopProducts(
        [FromQuery] DateOnly? date     = null,
        [FromQuery] string?   market   = null,
        [FromQuery] string?   brand    = null,
        [FromQuery] string?   category = null,
        [FromQuery] int       top      = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetTrendSnapshotsQuery(date, market, brand, category, top), ct);
        return Ok(result);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  MOMENTUM — koji producti rastu / padaju
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Vraća momentum score za svaki proizvod između danas i juče.
    ///
    /// GET /api/trends/momentum?date=2026-02-28&amp;top=30&amp;rising=true
    ///
    /// MomentumScore je u [-1, 1]:
    ///   > 0  = raste       = 0  = stagnira      &lt; 0  = pada
    /// </summary>
    [HttpGet("momentum")]
    [ProducesResponseType(typeof(TrendMomentumResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMomentum(
        [FromQuery] DateOnly? date   = null,
        [FromQuery] string?   market = null,
        [FromQuery] string?   brand  = null,
        [FromQuery] int       top    = 30,
        [FromQuery] bool      rising = true,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetTrendMomentumQuery(date, market, brand, top, rising), ct);
        return Ok(result);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  TRENDPLUS INDEX
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Vraća Trendplus Index™ [0–100] za dati scope i historiju zadnjih N dana.
    ///
    /// GET /api/trends/index?scopeType=market&amp;scopeValue=DE&amp;daysBack=30
    ///
    /// scopeType: "market" | "brand" | "category" | "brand_market"
    /// scopeValue: npr. "DE", "nike", "sneaker", "nike|de"
    /// </summary>
    [HttpGet("index")]
    [ProducesResponseType(typeof(TrendIndexResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIndex(
        [FromQuery] string    scopeType  = "market",
        [FromQuery] string?   scopeValue = null,
        [FromQuery] DateOnly? date       = null,
        [FromQuery] int       daysBack   = 30,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetTrendIndexQuery(scopeType, scopeValue, date, daysBack), ct);
        return Ok(result);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  INVENTORY PREPORUKE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Vraća preporučene količine narudžbi po proizvodu za dati dan.
    ///
    /// GET /api/trends/inventory?date=2026-02-28&amp;brand=Nike&amp;top=50
    ///
    /// RecommendedQty = f(salesVelocity, trendScore, momentumScore, stock)
    /// </summary>
    [HttpGet("inventory")]
    [ProducesResponseType(typeof(InventoryRecommendationsResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInventoryRecommendations(
        [FromQuery] DateOnly? date     = null,
        [FromQuery] string?   brand    = null,
        [FromQuery] string?   category = null,
        [FromQuery] int       minQty   = 1,
        [FromQuery] int       top      = 100,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetInventoryRecommendationsQuery(date, brand, category, minQty, top), ct);
        return Ok(result);
    }
}

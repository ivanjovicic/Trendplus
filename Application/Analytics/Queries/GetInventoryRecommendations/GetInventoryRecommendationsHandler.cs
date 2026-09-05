using Application.Analytics.Services;
using Application.Artikli.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Analytics.Queries.GetInventoryRecommendations;

public class GetInventoryRecommendationsHandler
    : IRequestHandler<GetInventoryRecommendationsQuery, InventoryRecommendationsResult>
{
    private readonly IAnalyticsDbContext _db;
    private readonly ILogger<GetInventoryRecommendationsHandler> _logger;

    public GetInventoryRecommendationsHandler(
        IAnalyticsDbContext db,
        ILogger<GetInventoryRecommendationsHandler> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<InventoryRecommendationsResult> Handle(
        GetInventoryRecommendationsQuery request,
        CancellationToken                ct)
    {
        DateOnly targetDate = request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);

        _logger.LogInformation(
            "GetInventoryRecommendations: date={Date} brand={Brand} category={Category} top={Top}",
            targetDate, request.Brand, request.Category, request.Top);

        bool hasPrecomputed = await _db.InventoryRecommendations
            .AsNoTracking()
            .AnyAsync(r => r.SnapshotDate == targetDate, ct);

        List<InventoryRecommendationDto> items;

        if (hasPrecomputed)
        {
            // Čitaj precomputed iz DB
            var query = _db.InventoryRecommendations
                .AsNoTracking()
                .Where(r => r.SnapshotDate == targetDate
                         && r.RecommendedQty >= request.MinQty);

            if (!string.IsNullOrWhiteSpace(request.Brand))
            {
                var brandLower = request.Brand.ToLower();
                query = query.Where(r => r.Brand != null && r.Brand.ToLower().Contains(brandLower));
            }

            if (!string.IsNullOrWhiteSpace(request.Category))
            {
                var categoryLower = request.Category.ToLower();
                query = query.Where(r => r.Category != null && r.Category.ToLower().Contains(categoryLower));
            }

            items = await query
                .OrderByDescending(r => r.RecommendedQty)
                .Take(request.Top)
                .Select(r => new InventoryRecommendationDto(
                    r.Id,
                    r.SnapshotDate,
                    r.ProductId,
                    r.Brand,
                    r.Category,
                    r.SalesVelocity,
                    r.StockOnHand,
                    r.TrendScore,
                    r.MomentumScore,
                    r.RecommendedQty))
                .ToListAsync(ct);
        }
        else
        {
            // No reliable sales/stock denominator exists here. Never manufacture
            // velocity=1 and stock=0 because that creates an actionable fake order.
            _logger.LogWarning(
                "Nema precomputed inventory preporuka za {Date}; rezultat ostaje neakcionabilan.", targetDate);
            items = [];
        }

        return new InventoryRecommendationsResult(
            targetDate,
            items.Count,
            items,
            hasPrecomputed ? "good" : "insufficient_data",
            hasPrecomputed && items.Count > 0,
            UsedFallback: !hasPrecomputed);
    }
}

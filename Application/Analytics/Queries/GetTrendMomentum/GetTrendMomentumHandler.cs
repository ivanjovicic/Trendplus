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

namespace Application.Analytics.Queries.GetTrendMomentum;

public class GetTrendMomentumHandler : IRequestHandler<GetTrendMomentumQuery, TrendMomentumResult>
{
    private readonly IAnalyticsDbContext _db;
    private readonly ILogger<GetTrendMomentumHandler> _logger;

    public GetTrendMomentumHandler(IAnalyticsDbContext db, ILogger<GetTrendMomentumHandler> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<TrendMomentumResult> Handle(
        GetTrendMomentumQuery request,
        CancellationToken     ct)
    {
        DateOnly targetDate    = request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly yesterdayDate = targetDate.AddDays(-1);

        _logger.LogInformation(
            "GetTrendMomentum: date={Date} market={Market} brand={Brand} top={Top}",
            targetDate, request.Market, request.Brand, request.Top);

        // ── Pokušaj čitanja iz DB (precomputed) ─────────────────────────────
        var precomputedQuery = _db.TrendProductMomentums
            .AsNoTracking()
            .Where(m => m.SnapshotDate == targetDate);

        bool hasPrecomputed = await precomputedQuery.AnyAsync(ct);

        List<TrendMomentumDto> items;

        if (hasPrecomputed)
        {
            // Precomputed momentum u DB — samo pročitaj i enrichuj s imenom
            var snapshots = await _db.TrendProductSnapshots
                .AsNoTracking()
                .Where(s => s.SnapshotDate == targetDate)
                .ToDictionaryAsync(s => s.CanonicalKey, ct);

            var momentums = precomputedQuery
                .OrderByDescending(m => m.SnapshotDate); // will re-sort below

            if (!string.IsNullOrWhiteSpace(request.Market))
            {
                var keysInMarket = _db.TrendProductSnapshots
                    .AsNoTracking()
                    .Where(s => s.SnapshotDate == targetDate && s.Market == request.Market.ToUpperInvariant())
                    .Select(s => s.CanonicalKey);
                momentums = (IOrderedQueryable<Domain.Model.Analytics.TrendProductMomentum>)momentums
                    .Where(m => keysInMarket.Contains(m.CanonicalKey));
            }

            var momentumList = await momentums.ToListAsync(ct);

            items = momentumList
                .Select(m =>
                {
                    snapshots.TryGetValue(m.CanonicalKey, out var snap);
                    var brand = snap?.Brand ?? "";

                    if (!string.IsNullOrWhiteSpace(request.Brand)
                        && !brand.Contains(request.Brand, StringComparison.OrdinalIgnoreCase))
                        return null;

                    return new TrendMomentumDto(
                        m.CanonicalKey,
                        snap?.ProductName ?? m.CanonicalKey,
                        brand,
                        m.SnapshotDate,
                        double.IsFinite(m.MomentumScore) ? m.MomentumScore : null,
                        m.ScoreDelta,
                        m.RankDelta,
                        m.IsNewEntry,
                        snap?.Score,
                        snap?.RankGlobal);
                })
                .Where(d => d is not null)
                .Cast<TrendMomentumDto>()
                .ToList();
        }
        else
        {
            // Precomputed podaci ne postoje — računaj on-the-fly iz snapshots
            _logger.LogWarning(
                "Nema precomputed momentum za {Date}, računam on-the-fly.", targetDate);

            var todaySnaps = await _db.TrendProductSnapshots
                .AsNoTracking()
                .Where(s => s.SnapshotDate == targetDate)
                .ToListAsync(ct);

            var yesterdaySnaps = await _db.TrendProductSnapshots
                .AsNoTracking()
                .Where(s => s.SnapshotDate == yesterdayDate)
                .ToDictionaryAsync(s => s.CanonicalKey, ct);

            items = todaySnaps
                .Select(today =>
                {
                    yesterdaySnaps.TryGetValue(today.CanonicalKey, out var yest);

                    double? momentum = TrendScoringService.ComputeMomentum(
                        today.Score,
                        yest?.Score,
                        today.RankGlobal,
                        yest?.RankGlobal);

                    double scoreDelta = yest is null ? today.Score : today.Score - yest.Score;
                    int    rankDelta  = yest is null ? 0 : yest.RankGlobal - today.RankGlobal;

                    if (!string.IsNullOrWhiteSpace(request.Brand)
                        && !today.Brand.Contains(request.Brand, StringComparison.OrdinalIgnoreCase))
                        return null;

                    return new TrendMomentumDto(
                        today.CanonicalKey,
                        today.ProductName,
                        today.Brand,
                        today.SnapshotDate,
                        momentum,
                        scoreDelta,
                        rankDelta,
                        yest is null,
                        today.Score,
                        today.RankGlobal);
                })
                .Where(d => d is not null)
                .Cast<TrendMomentumDto>()
                .ToList();
        }

        // ── Sort + Top N ─────────────────────────────────────────────────────
        items = request.Rising
            ? items.OrderByDescending(d => d.MomentumScore.HasValue)
                .ThenByDescending(d => d.MomentumScore)
                .Take(request.Top).ToList()
            : items.OrderByDescending(d => d.MomentumScore.HasValue)
                .ThenBy(d => d.MomentumScore)
                .Take(request.Top).ToList();

        return new TrendMomentumResult(targetDate, items.Count, items);
    }
}

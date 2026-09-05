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

namespace Application.Analytics.Queries.GetTrendIndex;

public class GetTrendIndexHandler : IRequestHandler<GetTrendIndexQuery, TrendIndexResult>
{
    private readonly IAnalyticsDbContext _db;
    private readonly ILogger<GetTrendIndexHandler> _logger;

    public GetTrendIndexHandler(IAnalyticsDbContext db, ILogger<GetTrendIndexHandler> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<TrendIndexResult> Handle(
        GetTrendIndexQuery request,
        CancellationToken  ct)
    {
        DateOnly latestDate = request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly fromDate   = latestDate.AddDays(-request.DaysBack);

        _logger.LogInformation(
            "GetTrendIndex: scopeType={ScopeType} scopeValue={ScopeValue} date={Date}",
            request.ScopeType, request.ScopeValue, latestDate);

        var query = _db.TrendplusIndexRecords
            .AsNoTracking()
            .Where(r => r.SnapshotDate >= fromDate && r.SnapshotDate <= latestDate
                     && r.ScopeType == request.ScopeType.ToLowerInvariant());

        if (!string.IsNullOrWhiteSpace(request.ScopeValue))
            query = query.Where(r => r.ScopeValue == request.ScopeValue.ToLowerInvariant());

        bool hasData = await query.AnyAsync(ct);

        List<TrendIndexDto> history;

        if (hasData)
        {
            history = await query
                .OrderBy(r => r.SnapshotDate)
                .Select(r => new TrendIndexDto(
                    r.SnapshotDate,
                    r.ScopeType,
                    r.ScopeValue,
                    r.IndexValue,
                    r.BaseComponent,
                    r.MomentumComponent,
                    r.SocialComponent))
                .ToListAsync(ct);
        }
        else
        {
            // Fallback: računaj on-the-fly iz dagens snapshots + momentuma
            _logger.LogWarning("Nema TrendplusIndex podataka, računam on-the-fly.");

            var snapshots = await _db.TrendProductSnapshots
                .AsNoTracking()
                .Where(s => s.SnapshotDate == latestDate)
                .ToListAsync(ct);

            var filtered = string.IsNullOrWhiteSpace(request.ScopeValue)
                ? snapshots
                : request.ScopeType.ToLowerInvariant() switch
                {
                    "market"   => snapshots.Where(s => string.Equals(s.Market,   request.ScopeValue, StringComparison.OrdinalIgnoreCase)).ToList(),
                    "brand"    => snapshots.Where(s => string.Equals(s.Brand,    request.ScopeValue, StringComparison.OrdinalIgnoreCase)).ToList(),
                    "category" => snapshots.Where(s => string.Equals(s.Category, request.ScopeValue, StringComparison.OrdinalIgnoreCase)).ToList(),
                    _          => snapshots,
                };

            var momentumMap = await _db.TrendProductMomentums
                .AsNoTracking()
                .Where(m => m.SnapshotDate == latestDate)
                .ToDictionaryAsync(m => m.CanonicalKey, m => m.MomentumScore, ct);

            var scores    = filtered.Select(s => s.Score);
            var momentums = filtered
                .Select(s => momentumMap.TryGetValue(s.CanonicalKey, out var momentum)
                    && double.IsFinite(momentum) ? (double?)momentum : null)
                .Where(momentum => momentum.HasValue)
                .Select(momentum => momentum!.Value);
            double? avgSocial = filtered.Any(s => s.SocialScore.HasValue)
                ? filtered.Where(s => s.SocialScore.HasValue).Average(s => s.SocialScore!.Value)
                : null;

            var (idx, baseC, momC, socC) = TrendScoringService.ComputeExtendedTrendIndex(
                scores, momentums, avgSocial);

            history = new List<TrendIndexDto>
            {
                new(latestDate,
                    request.ScopeType,
                    request.ScopeValue ?? "all",
                    idx, baseC, momC, socC)
            };
        }

        double? latest = history.OrderByDescending(h => h.SnapshotDate)
                                .FirstOrDefault()?.IndexValue;

        return new TrendIndexResult(request.ScopeType, request.ScopeValue, latest, history);
    }
}

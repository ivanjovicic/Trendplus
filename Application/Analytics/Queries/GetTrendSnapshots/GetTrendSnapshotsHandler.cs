using Application.Artikli.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace Application.Analytics.Queries.GetTrendSnapshots;

public class GetTrendSnapshotsHandler : IRequestHandler<GetTrendSnapshotsQuery, TrendSnapshotsResult>
{
    private readonly IAnalyticsDbContext _db;
    private readonly ILogger<GetTrendSnapshotsHandler> _logger;

    public GetTrendSnapshotsHandler(IAnalyticsDbContext db, ILogger<GetTrendSnapshotsHandler> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<TrendSnapshotsResult> Handle(
        GetTrendSnapshotsQuery request,
        CancellationToken      ct)
    {
        DateOnly targetDate = request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);

        _logger.LogInformation(
            "GetTrendSnapshots: date={Date} market={Market} brand={Brand} top={Top}",
            targetDate, request.Market, request.Brand, request.Top);

        var query = _db.TrendProductSnapshots
            .AsNoTracking()
            .Where(s => s.SnapshotDate == targetDate);

        if (!string.IsNullOrWhiteSpace(request.Market))
            query = query.Where(s => s.Market == request.Market.ToUpperInvariant());

        if (!string.IsNullOrWhiteSpace(request.Brand))
        {
            var brandLower = request.Brand.ToLower();
            query = query.Where(s => s.Brand.ToLower().Contains(brandLower));
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var categoryLower = request.Category.ToLower();
            query = query.Where(s => s.Category != null && s.Category.ToLower().Contains(categoryLower));
        }

        var items = await query
            .OrderBy(s => s.RankGlobal)
            .Take(request.Top)
            .Select(s => new TrendSnapshotDto(
                s.Id,
                s.SnapshotDate,
                s.CanonicalKey,
                s.ProductName,
                s.Brand,
                s.Category,
                s.Market,
                s.Score,
                s.RankGlobal,
                s.SocialScore,
                s.SourceCount,
                s.UniqueSources))
            .ToListAsync(ct);

        return new TrendSnapshotsResult(targetDate, items.Count, items);
    }
}

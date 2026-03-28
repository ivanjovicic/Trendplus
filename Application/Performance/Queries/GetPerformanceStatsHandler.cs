using Application.Artikli.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace Application.Performance.Queries
{
    public class GetPerformanceStatsHandler : IRequestHandler<GetPerformanceStatsQuery, GetPerformanceStatsResult>
    {
        private readonly IAnalyticsDbContext _db;
        private readonly ILogger<GetPerformanceStatsHandler> _logger;

        public GetPerformanceStatsHandler(
            IAnalyticsDbContext db,
            ILogger<GetPerformanceStatsHandler> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<GetPerformanceStatsResult> Handle(
            GetPerformanceStatsQuery request,
            CancellationToken cancellationToken)
        {
            var safeTopCount = Math.Clamp(request.TopCount, 1, 200);
            var safeMinDuration = Math.Max(0, request.MinDurationMs);

            var query = _db.PerformanceLogs
                .AsNoTracking()
                .AsQueryable();

            // Apply filters
            if (safeMinDuration > 0)
            {
                query = query.Where(p => p.DurationMs >= safeMinDuration);
            }

            if (request.FromDate.HasValue)
            {
                query = query.Where(p => p.Timestamp >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(p => p.Timestamp <= request.ToDate.Value);
            }

            // Get slowest requests
            var slowestRequests = await query
                .OrderByDescending(p => p.DurationMs)
                .Take(safeTopCount)
                .Select(p => new PerformanceStatDto(
                    p.Id,
                    p.Timestamp,
                    p.RequestName,
                    p.DurationMs,
                    p.IsSuccess,
                    p.ExceptionMessage
                ))
                .ToListAsync(cancellationToken);

            // Execute summary in a single query to avoid parallel operations on the same DbContext.
            var summaryRow = await query
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    TotalRequests = g.Count(),
                    SlowRequests = g.Count(p => p.DurationMs >= 1000),
                    FailedRequests = g.Count(p => !p.IsSuccess),
                    AverageDurationMs = g.Average(p => (double?)p.DurationMs),
                    MaxDurationMs = g.Max(p => (long?)p.DurationMs)
                })
                .FirstOrDefaultAsync(cancellationToken);

            var summary = new PerformanceSummaryDto(
                TotalRequests: summaryRow?.TotalRequests ?? 0,
                SlowRequests: summaryRow?.SlowRequests ?? 0,
                FailedRequests: summaryRow?.FailedRequests ?? 0,
                AverageDurationMs: summaryRow?.AverageDurationMs is double averageDuration ? (long)Math.Round(averageDuration) : 0,
                MaxDurationMs: summaryRow?.MaxDurationMs ?? 0
            );

            _logger.LogInformation(
                "Performance stats retrieved: {Total} total, {Slow} slow, {Failed} failed",
                summary.TotalRequests,
                summary.SlowRequests,
                summary.FailedRequests
            );

            return new GetPerformanceStatsResult(slowestRequests, summary);
        }
    }
}

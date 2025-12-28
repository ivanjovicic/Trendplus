using Application.Artikli.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
            var query = _db.PerformanceLogs.AsQueryable();

            // Apply filters
            if (request.MinDurationMs > 0)
            {
                query = query.Where(p => p.DurationMs >= request.MinDurationMs);
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
                .Take(request.TopCount)
                .Select(p => new PerformanceStatDto(
                    p.Id,
                    p.Timestamp,
                    p.RequestName,
                    p.DurationMs,
                    p.IsSuccess,
                    p.ExceptionMessage
                ))
                .ToListAsync(cancellationToken);

            // Calculate summary stats
            var allLogs = await query.ToListAsync(cancellationToken);
            
            var summary = new PerformanceSummaryDto(
                TotalRequests: allLogs.Count,
                SlowRequests: allLogs.Count(p => p.DurationMs >= 1000),
                FailedRequests: allLogs.Count(p => !p.IsSuccess),
                AverageDurationMs: allLogs.Any() ? (long)allLogs.Average(p => p.DurationMs) : 0,
                MaxDurationMs: allLogs.Any() ? allLogs.Max(p => p.DurationMs) : 0
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

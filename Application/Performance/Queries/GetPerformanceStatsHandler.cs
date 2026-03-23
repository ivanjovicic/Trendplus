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

            // Calculate summary stats without materializing full result set.
            var totalRequestsTask = query.CountAsync(cancellationToken);
            var slowRequestsTask = query.CountAsync(p => p.DurationMs >= 1000, cancellationToken);
            var failedRequestsTask = query.CountAsync(p => !p.IsSuccess, cancellationToken);
            var averageDurationTask = query
                .Select(p => (double?)p.DurationMs)
                .AverageAsync(cancellationToken);
            var maxDurationTask = query
                .Select(p => (long?)p.DurationMs)
                .MaxAsync(cancellationToken);

            await Task.WhenAll(
                totalRequestsTask,
                slowRequestsTask,
                failedRequestsTask,
                averageDurationTask,
                maxDurationTask
            );

            var summary = new PerformanceSummaryDto(
                TotalRequests: totalRequestsTask.Result,
                SlowRequests: slowRequestsTask.Result,
                FailedRequests: failedRequestsTask.Result,
                AverageDurationMs: averageDurationTask.Result.HasValue ? (long)Math.Round(averageDurationTask.Result.Value) : 0,
                MaxDurationMs: maxDurationTask.Result ?? 0
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

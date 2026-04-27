using Application.Artikli.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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

            if (!string.IsNullOrWhiteSpace(request.RequestName))
            {
                var normalizedRequestName = request.RequestName.Trim().ToLower();
                query = query.Where(p => p.RequestName.ToLower().Contains(normalizedRequestName));
            }

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                var normalizedStatus = request.Status.Trim().ToLowerInvariant();
                query = normalizedStatus switch
                {
                    "success" => query.Where(p => p.IsSuccess),
                    "failed" => query.Where(p => !p.IsSuccess),
                    "slow" => query.Where(p => p.DurationMs >= 1000),
                    _ => query
                };
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

            var metricRows = await query
                .Select(p => new MetricRow(
                    p.Timestamp,
                    p.RequestName,
                    p.DurationMs,
                    p.IsSuccess))
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

            var sortedDurations = metricRows
                .Select(x => x.DurationMs)
                .OrderBy(x => x)
                .ToList();

            var summary = new PerformanceSummaryDto(
                TotalRequests: summaryRow?.TotalRequests ?? 0,
                SlowRequests: summaryRow?.SlowRequests ?? 0,
                FailedRequests: summaryRow?.FailedRequests ?? 0,
                AverageDurationMs: summaryRow?.AverageDurationMs is double averageDuration ? (long)Math.Round(averageDuration) : 0,
                MaxDurationMs: summaryRow?.MaxDurationMs ?? 0,
                P50DurationMs: Percentile(sortedDurations, 0.50),
                P95DurationMs: Percentile(sortedDurations, 0.95),
                P99DurationMs: Percentile(sortedDurations, 0.99)
            );

            var endpointStats = metricRows
                .GroupBy(x => x.RequestName)
                .Select(g =>
                {
                    var durations = g.Select(x => x.DurationMs).OrderBy(x => x).ToList();
                    return new EndpointPerformanceDto(
                        RequestName: g.Key,
                        RequestCount: g.Count(),
                        FailedRequests: g.Count(x => !x.IsSuccess),
                        SlowRequests: g.Count(x => x.DurationMs >= 1000),
                        AverageDurationMs: durations.Count > 0 ? (long)Math.Round(durations.Average()) : 0,
                        MaxDurationMs: durations.Count > 0 ? durations[^1] : 0,
                        P95DurationMs: Percentile(durations, 0.95));
                })
                .OrderByDescending(x => x.P95DurationMs)
                .ThenByDescending(x => x.RequestCount)
                .Take(safeTopCount)
                .ToList();

            var timeline = BuildTimeline(metricRows);

            _logger.LogInformation(
                "Performance stats retrieved: {Total} total, {Slow} slow, {Failed} failed",
                summary.TotalRequests,
                summary.SlowRequests,
                summary.FailedRequests
            );

            return new GetPerformanceStatsResult(slowestRequests, summary, endpointStats, timeline);
        }

        private static long Percentile(IReadOnlyList<long> sortedDurations, double percentile)
        {
            if (sortedDurations.Count == 0) return 0;
            if (sortedDurations.Count == 1) return sortedDurations[0];

            var index = (int)Math.Ceiling(percentile * sortedDurations.Count) - 1;
            index = Math.Clamp(index, 0, sortedDurations.Count - 1);
            return sortedDurations[index];
        }

        private static List<PerformanceTimelinePointDto> BuildTimeline(IReadOnlyList<MetricRow> rows)
        {
            if (rows.Count == 0) return [];

            var min = rows.Min(x => x.Timestamp);
            var max = rows.Max(x => x.Timestamp);
            var range = max - min;
            var bucket = range.TotalHours switch
            {
                <= 2 => TimeSpan.FromMinutes(5),
                <= 24 => TimeSpan.FromMinutes(30),
                <= 168 => TimeSpan.FromHours(2),
                _ => TimeSpan.FromDays(1)
            };

            static DateTime NormalizeUtc(DateTime value) =>
                value.Kind == DateTimeKind.Utc
                    ? value
                    : DateTime.SpecifyKind(value, DateTimeKind.Utc);

            var bucketTicks = bucket.Ticks;

            return rows
                .GroupBy(x =>
                {
                    var timestamp = NormalizeUtc(x.Timestamp);
                    return new DateTime(timestamp.Ticks - (timestamp.Ticks % bucketTicks), DateTimeKind.Utc);
                })
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var durations = g.Select(x => x.DurationMs).OrderBy(x => x).ToList();
                    return new PerformanceTimelinePointDto(
                        BucketStart: g.Key,
                        RequestCount: g.Count(),
                        FailedRequests: g.Count(x => !x.IsSuccess),
                        AverageDurationMs: durations.Count > 0 ? (long)Math.Round(durations.Average()) : 0,
                        P95DurationMs: Percentile(durations, 0.95));
                })
                .ToList();
        }

        private sealed record MetricRow(
            DateTime Timestamp,
            string RequestName,
            long DurationMs,
            bool IsSuccess);
    }
}

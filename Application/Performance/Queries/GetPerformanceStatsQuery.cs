using MediatR;
using System;
using System.Collections.Generic;

namespace Application.Performance.Queries
{
    public record GetPerformanceStatsQuery(
        int TopCount = 20,
        int MinDurationMs = 0,
        DateTime? FromDate = null,
        DateTime? ToDate = null,
        string? RequestName = null,
        string? Status = null
    ) : IRequest<GetPerformanceStatsResult>;

    public record GetPerformanceStatsResult(
        List<PerformanceStatDto> SlowestRequests,
        PerformanceSummaryDto Summary,
        List<EndpointPerformanceDto> EndpointStats,
        List<PerformanceTimelinePointDto> Timeline
    );

    public record PerformanceStatDto(
        long Id,
        DateTime Timestamp,
        string RequestName,
        long DurationMs,
        bool IsSuccess,
        string? ExceptionMessage
    );

    public record PerformanceSummaryDto(
        int TotalRequests,
        int SlowRequests,
        int FailedRequests,
        long AverageDurationMs,
        long MaxDurationMs,
        long P50DurationMs,
        long P95DurationMs,
        long P99DurationMs
    );

    public record EndpointPerformanceDto(
        string RequestName,
        int RequestCount,
        int FailedRequests,
        int SlowRequests,
        long AverageDurationMs,
        long MaxDurationMs,
        long P95DurationMs
    );

    public record PerformanceTimelinePointDto(
        DateTime BucketStart,
        int RequestCount,
        int FailedRequests,
        long AverageDurationMs,
        long P95DurationMs
    );
}

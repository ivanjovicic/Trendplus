using MediatR;
using System;
using System.Collections.Generic;

namespace Application.Performance.Queries
{
    public record GetPerformanceStatsQuery(
        int TopCount = 20,
        int MinDurationMs = 0,
        DateTime? FromDate = null,
        DateTime? ToDate = null
    ) : IRequest<GetPerformanceStatsResult>;

    public record GetPerformanceStatsResult(
        List<PerformanceStatDto> SlowestRequests,
        PerformanceSummaryDto Summary
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
        long MaxDurationMs
    );
}

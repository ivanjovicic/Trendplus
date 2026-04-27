export interface PerformanceStat {
    id: number;
    timestamp: string;
    requestName: string;
    durationMs: number;
    isSuccess: boolean;
    exceptionMessage?: string;
}

export interface PerformanceSummary {
    totalRequests: number;
    slowRequests: number;
    failedRequests: number;
    averageDurationMs: number;
    maxDurationMs: number;
    p50DurationMs: number;
    p95DurationMs: number;
    p99DurationMs: number;
}

export interface EndpointPerformance {
    requestName: string;
    requestCount: number;
    failedRequests: number;
    slowRequests: number;
    averageDurationMs: number;
    maxDurationMs: number;
    p95DurationMs: number;
}

export interface PerformanceTimelinePoint {
    bucketStart: string;
    requestCount: number;
    failedRequests: number;
    averageDurationMs: number;
    p95DurationMs: number;
}

export interface PerformanceStatsResponse {
    slowestRequests: PerformanceStat[];
    summary: PerformanceSummary;
    endpointStats: EndpointPerformance[];
    timeline: PerformanceTimelinePoint[];
}

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
}

export interface PerformanceStatsResponse {
    slowestRequests: PerformanceStat[];
    summary: PerformanceSummary;
}

import { PerformanceStatsResponse } from "../types/performance";

const API = import.meta.env.VITE_API_BASE_URL;

export async function getPerformanceStats(
    topCount: number = 20,
    minDurationMs: number = 1000,
    fromDate?: string,
    toDate?: string
): Promise<PerformanceStatsResponse> {
    const params = new URLSearchParams({
        topCount: topCount.toString(),
        minDurationMs: minDurationMs.toString(),
    });

    if (fromDate) params.append("fromDate", fromDate);
    if (toDate) params.append("toDate", toDate);

    // Use relative path in development (proxied by Vite), absolute in production
    const url = import.meta.env.DEV 
        ? `/api/performance?${params.toString()}`
        : `${API}/api/performance?${params.toString()}`;

    const response = await fetch(url);

    if (!response.ok) {
        throw new Error(`Failed to fetch performance stats: ${response.statusText}`);
    }

    return response.json();
}

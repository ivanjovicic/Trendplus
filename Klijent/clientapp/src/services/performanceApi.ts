import { PerformanceStatsResponse } from "../types/performance";
import { apiUrl } from "../utils/apiUrl";
import { fetchWithTimeout } from "../utils/fetchWithTimeout";

const PERFORMANCE_TIMEOUT_MS = 60_000;

function getPerformanceUrl(params: URLSearchParams): string {
    const baseUrl = apiUrl("/api/performance");
    const query = params.toString();
    return query ? `${baseUrl}?${query}` : baseUrl;
}

async function parseError(res: Response): Promise<string> {
    try {
        const body = await res.json();
        return body?.detail ?? body?.title ?? body?.message ?? `HTTP ${res.status}`;
    } catch {
        return `HTTP ${res.status}`;
    }
}

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

    const url = getPerformanceUrl(params);

    try {
        const response = await fetchWithTimeout(url, undefined, PERFORMANCE_TIMEOUT_MS);

        if (!response.ok) {
            throw new Error(await parseError(response));
        }

        return response.json();
    } catch (error) {
        if (error instanceof TypeError) {
            throw new Error("Network error while fetching performance stats");
        }

        throw error;
    }
}

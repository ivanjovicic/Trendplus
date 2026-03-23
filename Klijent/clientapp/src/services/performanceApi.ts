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
    const raw = await res.text().catch(() => "");
    if (!raw) {
        return `HTTP ${res.status}`;
    }

    try {
        const body = JSON.parse(raw);
        return body?.detail ?? body?.title ?? body?.message ?? raw;
    } catch {
        return raw;
    }
}

function toUtcIsoOrUndefined(value?: string): string | undefined {
    if (!value) return undefined;
    const parsed = new Date(value);
    if (Number.isNaN(parsed.getTime())) return value;
    return parsed.toISOString();
}

export async function getPerformanceStats(
    topCount: number = 20,
    minDurationMs: number = 1000,
    fromDate?: string,
    toDate?: string
): Promise<PerformanceStatsResponse> {
    const safeTopCount = Number.isFinite(topCount) ? Math.min(200, Math.max(1, Math.floor(topCount))) : 20;
    const safeMinDuration = Number.isFinite(minDurationMs) ? Math.max(0, Math.floor(minDurationMs)) : 1000;
    const fromDateParam = toUtcIsoOrUndefined(fromDate);
    const toDateParam = toUtcIsoOrUndefined(toDate);

    const params = new URLSearchParams({
        topCount: safeTopCount.toString(),
        minDurationMs: safeMinDuration.toString(),
    });

    if (fromDateParam) params.append("fromDate", fromDateParam);
    if (toDateParam) params.append("toDate", toDateParam);

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

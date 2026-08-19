import { LogsResponse } from "../types/logs";
import type { LogEntry } from "../types/logs";
import { apiUrl } from "../utils/apiUrl";
import { fetchWithTimeout } from "../utils/fetchWithTimeout";

const LOGS_TIMEOUT_MS = 45_000;

function toUtcIsoOrUndefined(value?: string): string | undefined {
    if (!value) return undefined;
    const parsed = new Date(value);
    if (Number.isNaN(parsed.getTime())) return value;
    return parsed.toISOString();
}

function adminHeaders(adminKey?: string): HeadersInit {
    const headers: Record<string, string> = {};
    if (adminKey?.trim()) {
        headers["X-Admin-Key"] = adminKey.trim();
    }
    return headers;
}

async function parseError(res: Response): Promise<string> {
    if (res.status === 401) return "Nedostaje admin key za pregled logova.";
    if (res.status === 403) return "Admin key nije ispravan za pregled logova.";

    const raw = await res.text().catch(() => "");
    if (!raw) return `HTTP ${res.status}`;

    try {
        const body = JSON.parse(raw);
        return body?.detail ?? body?.title ?? body?.message ?? raw;
    } catch {
        return raw;
    }
}

export async function getLogs(
    pageNumber: number = 1,
    pageSize: number = 100,
    level?: string,
    fromDate?: string,
    toDate?: string,
    searchText?: string,
    adminKey?: string
): Promise<LogsResponse> {
    const params = new URLSearchParams({
        pageNumber: pageNumber.toString(),
        pageSize: pageSize.toString(),
    });

    if (level) params.append("level", level);
    const fromDateParam = toUtcIsoOrUndefined(fromDate);
    const toDateParam = toUtcIsoOrUndefined(toDate);

    if (fromDateParam) params.append("fromDate", fromDateParam);
    if (toDateParam) params.append("toDate", toDateParam);
    if (searchText) params.append("searchText", searchText);

    const url = apiUrl(`/api/logs?${params.toString()}`);

    const response = await fetchWithTimeout(url, { headers: adminHeaders(adminKey) }, LOGS_TIMEOUT_MS);

    if (!response.ok) {
        throw new Error(await parseError(response));
    }

    return response.json();
}

export async function getLogById(id: number, adminKey?: string): Promise<LogEntry> {
    const url = apiUrl(`/api/logs/${id}`);

    const response = await fetchWithTimeout(url, { headers: adminHeaders(adminKey) }, LOGS_TIMEOUT_MS);
    if (!response.ok) {
        throw new Error(await parseError(response));
    }

    return response.json();
}

export async function clearLogs(
    adminKey: string,
    beforeDate?: string,
    level?: string
): Promise<{ deletedCount: number }> {
    const params = new URLSearchParams();
    const beforeDateParam = toUtcIsoOrUndefined(beforeDate);

    if (beforeDateParam) params.append("beforeDate", beforeDateParam);
    if (level) params.append("level", level);

    const query = params.toString();
    const url = apiUrl(`/api/logs/clear${query ? `?${query}` : ""}`);

    const response = await fetchWithTimeout(url, {
        method: "DELETE",
        headers: adminHeaders(adminKey),
    }, LOGS_TIMEOUT_MS);

    if (!response.ok) {
        throw new Error(await parseError(response));
    }

    return response.json();
}

import { LogsResponse } from "../types/logs";
import type { LogEntry } from "../types/logs";

const API = import.meta.env.VITE_API_BASE_URL;

export async function getLogs(
    pageNumber: number = 1,
    pageSize: number = 100,
    level?: string,
    fromDate?: string,
    toDate?: string,
    searchText?: string
): Promise<LogsResponse> {
    const params = new URLSearchParams({
        pageNumber: pageNumber.toString(),
        pageSize: pageSize.toString(),
    });

    if (level) params.append("level", level);
    if (fromDate) params.append("fromDate", fromDate);
    if (toDate) params.append("toDate", toDate);
    if (searchText) params.append("searchText", searchText);

    // Use relative path in development (proxied by Vite), absolute in production
    const url = import.meta.env.DEV 
        ? `/api/logs?${params.toString()}`
        : `${API}/api/logs?${params.toString()}`;

    const response = await fetch(url);

    if (!response.ok) {
        throw new Error(`Failed to fetch logs: ${response.statusText}`);
    }

    return response.json();
}

export async function getLogById(id: number): Promise<LogEntry> {
    const url = import.meta.env.DEV
        ? `/api/logs/${id}`
        : `${API}/api/logs/${id}`;

    const response = await fetch(url);
    if (!response.ok) {
        throw new Error(`Failed to fetch log by id: ${response.statusText}`);
    }

    return response.json();
}

export async function clearLogs(
    adminKey: string,
    beforeDate?: string,
    level?: string
): Promise<{ deletedCount: number }> {
    const params = new URLSearchParams();
    if (beforeDate) params.append("beforeDate", beforeDate);
    if (level) params.append("level", level);

    const query = params.toString();
    const url = import.meta.env.DEV
        ? `/api/logs/clear${query ? `?${query}` : ""}`
        : `${API}/api/logs/clear${query ? `?${query}` : ""}`;

    const response = await fetch(url, {
        method: "DELETE",
        headers: {
            "X-Admin-Key": adminKey,
        },
    });

    if (!response.ok) {
        throw new Error(`Failed to clear logs: ${response.statusText}`);
    }

    return response.json();
}

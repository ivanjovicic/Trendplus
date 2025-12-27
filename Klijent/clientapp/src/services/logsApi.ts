import { LogsResponse } from "../types/logs";

const API = import.meta.env.VITE_API_BASE_URL;

export async function getLogs(
    pageNumber: number = 1,
    pageSize: number = 100,
    level?: string,
    fromDate?: string,
    toDate?: string
): Promise<LogsResponse> {
    const params = new URLSearchParams({
        pageNumber: pageNumber.toString(),
        pageSize: pageSize.toString(),
    });

    if (level) params.append("level", level);
    if (fromDate) params.append("fromDate", fromDate);
    if (toDate) params.append("toDate", toDate);

    const response = await fetch(`${API}/api/logs?${params.toString()}`);

    if (!response.ok) {
        throw new Error(`Failed to fetch logs: ${response.statusText}`);
    }

    return response.json();
}

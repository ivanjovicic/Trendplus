import { OutboxStatsResponse, OutboxMessagesResponse } from "../types/outbox";
import { apiCircuitBreaker } from "../utils/circuitBreaker";

const API = import.meta.env.VITE_API_BASE_URL || "";

/**
 * Execute fetch request through Circuit Breaker
 */
async function fetchWithCircuitBreaker(
    url: string,
    options?: RequestInit
): Promise<Response> {
    return apiCircuitBreaker.execute(async () => {
        const response = await fetch(url, options);

        // Treat 5xx errors as circuit breaker failures
        if (response.status >= 500) {
            throw new Error(`Server error: ${response.status} ${response.statusText}`);
        }

        return response;
    });
}

export async function getOutboxStats(): Promise<OutboxStatsResponse> {
    const url = import.meta.env.DEV
        ? `/api/outbox/stats`
        : `${API}/api/outbox/stats`;

    const response = await fetchWithCircuitBreaker(url);

    if (!response.ok) {
        throw new Error(`Failed to fetch outbox stats: ${response.statusText}`);
    }

    return response.json();
}

export async function getOutboxMessages(
    pageNumber: number = 1,
    pageSize: number = 50,
    isProcessed?: boolean,
    eventType?: string,
    fromDate?: string,
    toDate?: string
): Promise<OutboxMessagesResponse> {
    const params = new URLSearchParams({
        pageNumber: pageNumber.toString(),
        pageSize: pageSize.toString(),
    });

    if (isProcessed !== undefined) params.append("isProcessed", isProcessed.toString());
    if (eventType) params.append("eventType", eventType);
    if (fromDate) params.append("fromDate", fromDate);
    if (toDate) params.append("toDate", toDate);

    const url = import.meta.env.DEV
        ? `/api/outbox/messages?${params.toString()}`
        : `${API}/api/outbox/messages?${params.toString()}`;

    const response = await fetchWithCircuitBreaker(url);

    if (!response.ok) {
        throw new Error(`Failed to fetch outbox messages: ${response.statusText}`);
    }

    return response.json();
}

export async function retryOutboxMessage(id: number): Promise<void> {
    const url = import.meta.env.DEV
        ? `/api/outbox/retry/${id}`
        : `${API}/api/outbox/retry/${id}`;

    const response = await fetchWithCircuitBreaker(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
    });

    if (!response.ok) {
        throw new Error(`Failed to retry message: ${response.statusText}`);
    }
}

export async function retryAllFailedMessages(): Promise<{ count: number }> {
    const url = import.meta.env.DEV
        ? `/api/outbox/retry-all-failed`
        : `${API}/api/outbox/retry-all-failed`;

    const response = await fetchWithCircuitBreaker(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
    });

    if (!response.ok) {
        throw new Error(`Failed to retry all failed messages: ${response.statusText}`);
    }

    return response.json();
}

export async function purgeProcessedMessages(olderThanDays: number = 7): Promise<{ count: number }> {
    const url = import.meta.env.DEV
        ? `/api/outbox/purge-processed?olderThanDays=${olderThanDays}`
        : `${API}/api/outbox/purge-processed?olderThanDays=${olderThanDays}`;

    const response = await fetchWithCircuitBreaker(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
    });

    if (!response.ok) {
        throw new Error(`Failed to purge processed messages: ${response.statusText}`);
    }

    return response.json();
}

export interface EventTypeStat {
    eventType: string;
    total: number;
    processed: number;
    pending: number;
    failed: number;
}

export async function getEventTypeStats(): Promise<EventTypeStat[]> {
    const url = import.meta.env.DEV
        ? `/api/outbox/stats-by-type`
        : `${API}/api/outbox/stats-by-type`;

    console.log("?? Fetching event type stats from:", url, "| DEV:", import.meta.env.DEV);

    const response = await fetchWithCircuitBreaker(url);

    if (!response.ok) {
        throw new Error(`Failed to fetch event type stats: ${response.statusText}`);
    }

    return response.json();
}

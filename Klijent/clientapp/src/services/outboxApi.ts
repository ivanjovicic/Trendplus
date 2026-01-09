import { OutboxStatsResponse, OutboxMessagesResponse } from "../types/outbox";

const API = import.meta.env.VITE_API_BASE_URL || "";

// Retry configuration
const RETRY_CONFIG = {
    maxRetries: 3,
    initialDelay: 1000, // 1 second
    maxDelay: 10000,    // 10 seconds
    backoffMultiplier: 2,
};

/**
 * Retry fetch with exponential backoff
 */
async function fetchWithRetry(
    url: string,
    options?: RequestInit,
    retries = RETRY_CONFIG.maxRetries
): Promise<Response> {
    let lastError: Error | null = null;
    
    for (let attempt = 0; attempt <= retries; attempt++) {
        try {
            const response = await fetch(url, options);
            
            // If 5xx error, retry
            if (response.status >= 500 && attempt < retries) {
                throw new Error(`Server error: ${response.status}`);
            }
            
            return response;
        } catch (error) {
            lastError = error as Error;
            
            if (attempt < retries) {
                const delay = Math.min(
                    RETRY_CONFIG.initialDelay * Math.pow(RETRY_CONFIG.backoffMultiplier, attempt),
                    RETRY_CONFIG.maxDelay
                );
                
                console.warn(`Retry attempt ${attempt + 1}/${retries} after ${delay}ms for ${url}`);
                await new Promise(resolve => setTimeout(resolve, delay));
            }
        }
    }
    
    throw lastError || new Error("Failed to fetch after retries");
}

export async function getOutboxStats(): Promise<OutboxStatsResponse> {
    const url = import.meta.env.DEV 
        ? `/api/outbox/stats`
        : `${API}/api/outbox/stats`;

    const response = await fetchWithRetry(url);

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

    const response = await fetchWithRetry(url);

    if (!response.ok) {
        throw new Error(`Failed to fetch outbox messages: ${response.statusText}`);
    }

    return response.json();
}

export async function retryOutboxMessage(id: number): Promise<void> {
    const url = import.meta.env.DEV 
        ? `/api/outbox/retry/${id}`
        : `${API}/api/outbox/retry/${id}`;

    const response = await fetchWithRetry(url, {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
        },
    });

    if (!response.ok) {
        throw new Error(`Failed to retry message: ${response.statusText}`);
    }
}

export async function retryAllFailedMessages(): Promise<{ count: number }> {
    const url = import.meta.env.DEV 
        ? `/api/outbox/retry-all-failed`
        : `${API}/api/outbox/retry-all-failed`;

    const response = await fetchWithRetry(url, {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
        },
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

    const response = await fetchWithRetry(url, {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
        },
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

    console.log("?? Fetching event type stats from:", url, "| DEV:", import.meta.env.DEV, "| API:", API);

    const response = await fetchWithRetry(url);

    if (!response.ok) {
        throw new Error(`Failed to fetch event type stats: ${response.statusText}`);
    }

    return response.json();
}

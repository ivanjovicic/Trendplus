export interface OutboxMessage {
    id: number;
    eventType: string;
    payload: string;
    createdAt: string;
    processedAt?: string;
    isProcessed: boolean;
    retryCount: number;
    errorMessage?: string;
    correlationId?: string;
}

export interface OutboxStats {
    total: number;
    processed: number;
    pending: number;
    failed: number;
    successRate: number;
}

export interface OutboxStatsResponse {
    stats: OutboxStats;
    recentMessages: OutboxMessage[];
}

export interface OutboxMessagesResponse {
    messages: OutboxMessage[];
    totalCount: number;
    pageNumber: number;
    pageSize: number;
}

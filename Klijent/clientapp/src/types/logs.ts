export interface LogEntry {
    timestamp: string;
    level: string;
    message: string;
    exception?: string;
    properties?: Record<string, any>;
}

export interface LogsResponse {
    logs: LogEntry[];
    totalCount: number;
    pageNumber: number;
    pageSize: number;
}

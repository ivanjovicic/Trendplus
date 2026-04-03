export interface LogEntry {
    id?: number;
    timestamp: string;
    level: string;
    message: string;
    exception?: string;
    properties?: {
        path?: string;
        userName?: string;
        clientApp?: string;
        correlationId?: string;
        [key: string]: any;
    };
}

export interface LogsResponse {
    logs: LogEntry[];
    totalCount: number;
    pageNumber: number;
    pageSize: number;
}

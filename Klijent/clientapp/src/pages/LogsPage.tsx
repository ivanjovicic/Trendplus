import React, { useCallback, useEffect, useMemo, useState } from "react";
import {
    AlertCircle,
    ChevronDown,
    ChevronRight,
    CircleAlert,
    CircleCheck,
    Clock3,
    Copy,
    Eye,
    Filter,
    Info,
    RefreshCw,
    Search,
    Trash2,
    X,
} from "lucide-react";
import { clearLogs, getLogById, getLogs } from "../services/logsApi";
import type { LogEntry } from "../types/logs";
import "./ObservabilityPages.css";

type TimePeriod = "" | "30m" | "1h" | "6h" | "1d" | "2d" | "7d";

const PAGE_SIZE = 100;

const timePeriodOptions: { value: TimePeriod; label: string }[] = [
    { value: "", label: "Custom range" },
    { value: "30m", label: "Last 30 min" },
    { value: "1h", label: "Last hour" },
    { value: "6h", label: "Last 6 hours" },
    { value: "1d", label: "Last day" },
    { value: "2d", label: "Last 2 days" },
    { value: "7d", label: "Last 7 days" },
];

function toDateTimeLocalValue(date: Date): string {
    const timezoneOffsetMs = date.getTimezoneOffset() * 60_000;
    return new Date(date.getTime() - timezoneOffsetMs).toISOString().slice(0, 16);
}

function getDateRangeFromPeriod(period: TimePeriod): { from: string; to: string } {
    if (!period) return { from: "", to: "" };

    const now = new Date();
    const minutesByPeriod: Record<Exclude<TimePeriod, "">, number> = {
        "30m": 30,
        "1h": 60,
        "6h": 6 * 60,
        "1d": 24 * 60,
        "2d": 2 * 24 * 60,
        "7d": 7 * 24 * 60,
    };

    const from = new Date(now.getTime() - minutesByPeriod[period] * 60 * 1000);
    return {
        from: toDateTimeLocalValue(from),
        to: toDateTimeLocalValue(now),
    };
}

function formatDate(timestamp: string): string {
    const date = new Date(timestamp);
    return date.toLocaleString("sr-RS", {
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit",
        second: "2-digit",
    });
}

function severityKind(level: string): "error" | "warning" | "info" | "neutral" {
    switch (level.toUpperCase()) {
        case "ERROR":
        case "FATAL":
            return "error";
        case "WARNING":
        case "WARN":
            return "warning";
        case "INFORMATION":
        case "INFO":
            return "info";
        default:
            return "neutral";
    }
}

function SeverityIcon({ level }: { level: string }) {
    const kind = severityKind(level);
    if (kind === "error") return <AlertCircle size={14} />;
    if (kind === "warning") return <CircleAlert size={14} />;
    if (kind === "info") return <Info size={14} />;
    return <CircleCheck size={14} />;
}

function rowKey(log: LogEntry, index: number): string {
    return String(log.id ?? `${log.timestamp}-${index}`);
}

function getCorrelationId(log: LogEntry): string {
    return log.properties?.correlationId?.trim() || "none";
}

export default function LogsPage() {
    const [logs, setLogs] = useState<LogEntry[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [selectedLevel, setSelectedLevel] = useState("");
    const [selectedPeriod, setSelectedPeriod] = useState<TimePeriod>("1d");
    const [fromDate, setFromDate] = useState(() => getDateRangeFromPeriod("1d").from);
    const [toDate, setToDate] = useState(() => getDateRangeFromPeriod("1d").to);
    const [currentPage, setCurrentPage] = useState(1);
    const [totalCount, setTotalCount] = useState(0);
    const [searchTerm, setSearchTerm] = useState("");
    const [debouncedSearchTerm, setDebouncedSearchTerm] = useState("");
    const [logIdInput, setLogIdInput] = useState("");
    const [loadingLogById, setLoadingLogById] = useState(false);
    const [clearingLogs, setClearingLogs] = useState(false);
    const [expandedRow, setExpandedRow] = useState<string | null>(null);
    const [copiedValue, setCopiedValue] = useState<string | null>(null);

    useEffect(() => {
        const timer = window.setTimeout(() => {
            setDebouncedSearchTerm(searchTerm.trim());
            setCurrentPage(1);
        }, 300);

        return () => window.clearTimeout(timer);
    }, [searchTerm]);

    const fetchLogs = useCallback(async () => {
        setLoading(true);
        setError(null);

        try {
            const result = await getLogs(
                currentPage,
                PAGE_SIZE,
                selectedLevel || undefined,
                fromDate || undefined,
                toDate || undefined,
                debouncedSearchTerm || undefined
            );

            setLogs(result.logs);
            setTotalCount(result.totalCount);
        } catch (err) {
            const message = err instanceof Error ? err.message : "Unable to load logs.";
            setError(message);
        } finally {
            setLoading(false);
        }
    }, [currentPage, debouncedSearchTerm, fromDate, selectedLevel, toDate]);

    useEffect(() => {
        void fetchLogs();
    }, [fetchLogs]);

    const pageStats = useMemo(() => {
        const errors = logs.filter((log) => severityKind(log.level) === "error").length;
        const warnings = logs.filter((log) => severityKind(log.level) === "warning").length;
        const info = logs.filter((log) => severityKind(log.level) === "info").length;
        const grouped = new Map<string, number>();

        for (const log of logs) {
            const correlationId = getCorrelationId(log);
            if (correlationId !== "none") {
                grouped.set(correlationId, (grouped.get(correlationId) ?? 0) + 1);
            }
        }

        return {
            errors,
            warnings,
            info,
            grouped,
            groupedCount: Array.from(grouped.values()).filter((count) => count > 1).length,
        };
    }, [logs]);

    const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

    const handlePeriodChange = (period: TimePeriod) => {
        setSelectedPeriod(period);
        const range = getDateRangeFromPeriod(period);
        setFromDate(range.from);
        setToDate(range.to);
        setCurrentPage(1);
    };

    const handleCustomDateChange = (field: "from" | "to", value: string) => {
        setSelectedPeriod("");
        if (field === "from") setFromDate(value);
        if (field === "to") setToDate(value);
        setCurrentPage(1);
    };

    const copyToClipboard = async (text: string, label: string) => {
        if (!text) return;
        await navigator.clipboard?.writeText(text);
        setCopiedValue(label);
        window.setTimeout(() => setCopiedValue(null), 1400);
    };

    const openLogById = async () => {
        const parsedId = Number.parseInt(logIdInput, 10);
        if (!Number.isInteger(parsedId) || parsedId <= 0) {
            setError("Enter a valid log ID.");
            return;
        }

        setLoadingLogById(true);
        setError(null);
        try {
            const log = await getLogById(parsedId);
            setLogs((current) => [log, ...current.filter((item) => item.id !== log.id)]);
            setExpandedRow(String(log.id ?? log.timestamp));
        } catch (err) {
            const message = err instanceof Error ? err.message : "Unable to load log by ID.";
            setError(message);
        } finally {
            setLoadingLogById(false);
        }
    };

    const handleClearLogs = async () => {
        if (!window.confirm("Confirm log deletion for the selected level and upper date boundary.")) {
            return;
        }

        const adminKey = window.prompt("Admin key");
        if (!adminKey?.trim()) {
            setError("Admin key is required.");
            return;
        }

        setClearingLogs(true);
        setError(null);
        try {
            await clearLogs(adminKey.trim(), toDate || undefined, selectedLevel || undefined);
            await fetchLogs();
        } catch (err) {
            const message = err instanceof Error ? err.message : "Unable to clear logs.";
            setError(message);
        } finally {
            setClearingLogs(false);
        }
    };

    const resetFilters = () => {
        const range = getDateRangeFromPeriod("1d");
        setSelectedLevel("");
        setSelectedPeriod("1d");
        setFromDate(range.from);
        setToDate(range.to);
        setSearchTerm("");
        setCurrentPage(1);
        setExpandedRow(null);
    };

    return (
        <div className="observability-page">
            <div className="observability-header">
                <div>
                    <span className="observability-eyebrow">System observability</span>
                    <h1 className="observability-title">
                        <Filter size={22} />
                        Logs
                    </h1>
                    <p className="observability-subtitle">Operational events grouped by request context and severity.</p>
                </div>

                <div className="observability-actions">
                    <span className="observability-status">
                        <span className="observability-status__dot" />
                        {copiedValue ? `${copiedValue} copied` : `${totalCount} records`}
                    </span>
                    <button
                        className="observability-icon-button"
                        type="button"
                        title="Refresh logs"
                        onClick={() => void fetchLogs()}
                        disabled={loading}
                    >
                        <RefreshCw size={17} />
                    </button>
                </div>
            </div>

            <section className="observability-kpis" aria-label="Log overview">
                <div className="observability-kpi">
                    <div className="observability-kpi__label">
                        <Clock3 size={15} />
                        Total
                    </div>
                    <div className="observability-kpi__value">{totalCount}</div>
                    <div className="observability-kpi__meta">Page {currentPage} of {totalPages}</div>
                </div>
                <div className="observability-kpi">
                    <div className="observability-kpi__label">
                        <AlertCircle size={15} />
                        Errors
                    </div>
                    <div className="observability-kpi__value">{pageStats.errors}</div>
                    <div className="observability-kpi__meta">Current page</div>
                </div>
                <div className="observability-kpi">
                    <div className="observability-kpi__label">
                        <CircleAlert size={15} />
                        Warnings
                    </div>
                    <div className="observability-kpi__value">{pageStats.warnings}</div>
                    <div className="observability-kpi__meta">Current page</div>
                </div>
                <div className="observability-kpi">
                    <div className="observability-kpi__label">
                        <Search size={15} />
                        Request Groups
                    </div>
                    <div className="observability-kpi__value">{pageStats.groupedCount}</div>
                    <div className="observability-kpi__meta">Repeated correlation IDs</div>
                </div>
            </section>

            <section className="observability-panel observability-filters" aria-label="Log filters">
                <div className="observability-field">
                    <label htmlFor="log-level">Severity</label>
                    <select
                        id="log-level"
                        className="observability-select"
                        value={selectedLevel}
                        onChange={(event) => {
                            setSelectedLevel(event.target.value);
                            setCurrentPage(1);
                        }}
                    >
                        <option value="">All severities</option>
                        <option value="Debug">Debug</option>
                        <option value="Information">Information</option>
                        <option value="Warning">Warning</option>
                        <option value="Error">Error</option>
                        <option value="Fatal">Fatal</option>
                    </select>
                </div>

                <div className="observability-field">
                    <label htmlFor="log-period">Range</label>
                    <select
                        id="log-period"
                        className="observability-select"
                        value={selectedPeriod}
                        onChange={(event) => handlePeriodChange(event.target.value as TimePeriod)}
                    >
                        {timePeriodOptions.map((option) => (
                            <option key={option.value} value={option.value}>
                                {option.label}
                            </option>
                        ))}
                    </select>
                </div>

                <div className="observability-field">
                    <label htmlFor="log-search">Search</label>
                    <input
                        id="log-search"
                        type="search"
                        className="observability-input"
                        placeholder="Message, path, user, correlation"
                        value={searchTerm}
                        onChange={(event) => setSearchTerm(event.target.value)}
                    />
                </div>

                <div className="observability-field">
                    <label htmlFor="log-from">From</label>
                    <input
                        id="log-from"
                        type="datetime-local"
                        className="observability-input"
                        value={fromDate}
                        onChange={(event) => handleCustomDateChange("from", event.target.value)}
                    />
                </div>

                <div className="observability-field">
                    <label htmlFor="log-to">To</label>
                    <input
                        id="log-to"
                        type="datetime-local"
                        className="observability-input"
                        value={toDate}
                        onChange={(event) => handleCustomDateChange("to", event.target.value)}
                    />
                </div>

                <div className="observability-field">
                    <label htmlFor="log-id">Log ID</label>
                    <div className="observability-inline-actions">
                        <input
                            id="log-id"
                            type="number"
                            min={1}
                            className="observability-input"
                            placeholder="ID"
                            value={logIdInput}
                            onChange={(event) => setLogIdInput(event.target.value)}
                            onKeyDown={(event) => {
                                if (event.key === "Enter") {
                                    void openLogById();
                                }
                            }}
                        />
                        <button
                            type="button"
                            className="observability-icon-button"
                            title="Open log by ID"
                            disabled={loadingLogById}
                            onClick={() => void openLogById()}
                        >
                            <Eye size={17} />
                        </button>
                    </div>
                </div>

                <div className="observability-inline-actions observability-filter-actions">
                    <button
                        className="observability-button observability-button--primary"
                        type="button"
                        onClick={() => void fetchLogs()}
                        disabled={loading}
                    >
                        <RefreshCw size={16} />
                        Refresh
                    </button>
                    <button className="observability-button" type="button" onClick={resetFilters}>
                        <X size={16} />
                        Reset
                    </button>
                    <button
                        className="observability-button observability-button--danger"
                        type="button"
                        onClick={() => void handleClearLogs()}
                        disabled={clearingLogs}
                    >
                        <Trash2 size={16} />
                        Clear
                    </button>
                </div>
            </section>

            {error && <div className="observability-error">{error}</div>}

            {loading ? (
                <div className="observability-loading">Loading logs...</div>
            ) : logs.length === 0 ? (
                <div className="observability-empty">No logs match the selected filters.</div>
            ) : (
                <>
                    <section className="observability-panel observability-table-shell" aria-label="Log table">
                        <table className="observability-table observability-table--logs">
                            <colgroup>
                                <col className="observability-col-log-time" />
                                <col className="observability-col-log-severity" />
                                <col className="observability-col-log-message" />
                                <col className="observability-col-log-path" />
                                <col className="observability-col-log-correlation" />
                                <col className="observability-col-log-actions" />
                            </colgroup>
                            <thead>
                                <tr>
                                    <th>Time</th>
                                    <th>Severity</th>
                                    <th>Message</th>
                                    <th>Path</th>
                                    <th>Correlation</th>
                                    <th className="observability-table__actions">Actions</th>
                                </tr>
                            </thead>
                            <tbody>
                                {logs.map((log, index) => {
                                    const key = rowKey(log, index);
                                    const isExpanded = expandedRow === key;
                                    const correlationId = getCorrelationId(log);
                                    const groupedCount = pageStats.grouped.get(correlationId) ?? 0;
                                    const kind = severityKind(log.level);

                                    return (
                                        <React.Fragment key={key}>
                                            <tr>
                                                <td className="observability-mono observability-muted">
                                                    {formatDate(log.timestamp)}
                                                </td>
                                                <td>
                                                    <span className={`severity-badge severity-badge--${kind}`}>
                                                        <SeverityIcon level={log.level} />
                                                        {log.level}
                                                    </span>
                                                </td>
                                                <td className="observability-message-cell">
                                                    <div className="observability-truncate" title={log.message}>
                                                        {log.message}
                                                    </div>
                                                </td>
                                                <td className="observability-mono">
                                                    <span className="observability-truncate" title={log.properties?.path || "-"}>
                                                        {log.properties?.path || "-"}
                                                    </span>
                                                </td>
                                                <td className="observability-mono">
                                                    <span className="observability-truncate" title={correlationId !== "none" ? correlationId : "-"}>
                                                        {correlationId !== "none" ? correlationId : "-"}
                                                        {groupedCount > 1 ? (
                                                            <span className="observability-muted"> ({groupedCount})</span>
                                                        ) : null}
                                                    </span>
                                                </td>
                                                <td className="observability-table__actions">
                                                    <div className="observability-inline-actions">
                                                        <button
                                                            type="button"
                                                            className="observability-icon-button"
                                                            title={isExpanded ? "Collapse details" : "Expand details"}
                                                            onClick={() => setExpandedRow(isExpanded ? null : key)}
                                                        >
                                                            {isExpanded ? <ChevronDown size={17} /> : <ChevronRight size={17} />}
                                                        </button>
                                                        <button
                                                            type="button"
                                                            className="observability-icon-button"
                                                            title="Copy log JSON"
                                                            onClick={() => void copyToClipboard(JSON.stringify(log, null, 2), "Log")}
                                                        >
                                                            <Copy size={16} />
                                                        </button>
                                                    </div>
                                                </td>
                                            </tr>

                                            {isExpanded ? (
                                                <tr className="observability-row-detail">
                                                    <td colSpan={6}>
                                                        <div className="observability-detail-grid">
                                                            <div className="observability-detail-box">
                                                                <h4>Context</h4>
                                                                <pre className="observability-pre observability-mono">
                                                                    {JSON.stringify(log.properties ?? {}, null, 2)}
                                                                </pre>
                                                            </div>
                                                            <div className="observability-detail-box">
                                                                <h4>{log.exception ? "Exception" : "Message"}</h4>
                                                                <pre className="observability-pre observability-mono">
                                                                    {log.exception || log.message}
                                                                </pre>
                                                            </div>
                                                        </div>
                                                    </td>
                                                </tr>
                                            ) : null}
                                        </React.Fragment>
                                    );
                                })}
                            </tbody>
                        </table>
                    </section>

                    <div className="observability-actions observability-actions--center">
                        <button
                            className="observability-button"
                            type="button"
                            onClick={() => setCurrentPage((page) => Math.max(1, page - 1))}
                            disabled={currentPage === 1 || loading}
                        >
                            Previous
                        </button>
                        <span className="observability-status">
                            Page {currentPage} / {totalPages}
                        </span>
                        <button
                            className="observability-button"
                            type="button"
                            onClick={() => setCurrentPage((page) => Math.min(totalPages, page + 1))}
                            disabled={currentPage >= totalPages || loading}
                        >
                            Next
                        </button>
                    </div>
                </>
            )}
        </div>
    );
}

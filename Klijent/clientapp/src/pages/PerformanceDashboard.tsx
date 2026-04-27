import { Fragment, useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
    Activity,
    ArrowUpDown,
    BarChart3,
    Clock3,
    Gauge,
    RefreshCw,
    TimerReset,
    TriangleAlert,
    X,
    Zap,
} from "lucide-react";
import {
    Area,
    AreaChart,
    Bar,
    BarChart,
    CartesianGrid,
    ResponsiveContainer,
    Tooltip,
    XAxis,
    YAxis,
} from "recharts";
import { usePingControl } from "../context/PingControlContext";
import { getPerformanceStats } from "../services/performanceApi";
import type { EndpointPerformance, PerformanceStat, PerformanceSummary, PerformanceTimelinePoint } from "../types/performance";
import "./ObservabilityPages.css";

type SortKey = "timestamp" | "requestName" | "durationMs" | "isSuccess";
type SortDirection = "asc" | "desc";
type StatusFilter = "all" | "success" | "failed" | "slow";

const DEFAULT_TOP_COUNT = 50;
const DEFAULT_MIN_DURATION = 0;
const AUTO_REFRESH_MS = 15_000;

function clampNumber(value: number, min: number, max: number): number {
    if (!Number.isFinite(value)) return min;
    return Math.min(max, Math.max(min, Math.floor(value)));
}

function toDateTimeLocalValue(date: Date): string {
    const timezoneOffsetMs = date.getTimezoneOffset() * 60_000;
    return new Date(date.getTime() - timezoneOffsetMs).toISOString().slice(0, 16);
}

function getQuickRange(hours: number): { from: string; to: string } {
    const now = new Date();
    const from = new Date(now.getTime() - hours * 60 * 60 * 1000);
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

function formatBucket(timestamp: string): string {
    const date = new Date(timestamp);
    return date.toLocaleString("sr-RS", {
        month: "2-digit",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit",
    });
}

function formatDuration(ms: number): string {
    if (!Number.isFinite(ms)) return "-";
    if (ms < 1000) return `${Math.round(ms)} ms`;
    return `${(ms / 1000).toFixed(2)} s`;
}

function rowStatus(stat: PerformanceStat): "success" | "failed" | "slow" {
    if (!stat.isSuccess) return "failed";
    if (stat.durationMs >= 1000) return "slow";
    return "success";
}

const tooltipStyle = {
    background: "var(--color-surface)",
    border: "var(--border-width-sm) solid var(--color-border)",
    borderRadius: "var(--radius-md)",
    color: "var(--color-text)",
};

export default function PerformanceDashboard() {
    const { apiPingEnabled } = usePingControl();
    const activeRequestIdRef = useRef(0);
    const initialRange = useMemo(() => getQuickRange(24), []);

    const [stats, setStats] = useState<PerformanceStat[]>([]);
    const [summary, setSummary] = useState<PerformanceSummary | null>(null);
    const [endpointStats, setEndpointStats] = useState<EndpointPerformance[]>([]);
    const [timeline, setTimeline] = useState<PerformanceTimelinePoint[]>([]);
    const [loading, setLoading] = useState(true);
    const [refreshing, setRefreshing] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [lastUpdatedAt, setLastUpdatedAt] = useState<Date | null>(null);

    const [sortKey, setSortKey] = useState<SortKey>("durationMs");
    const [sortDirection, setSortDirection] = useState<SortDirection>("desc");

    const [topCount, setTopCount] = useState(DEFAULT_TOP_COUNT);
    const [minDuration, setMinDuration] = useState(DEFAULT_MIN_DURATION);
    const [fromDate, setFromDate] = useState<string>(initialRange.from);
    const [toDate, setToDate] = useState<string>(initialRange.to);
    const [requestFilter, setRequestFilter] = useState("");
    const [debouncedRequestFilter, setDebouncedRequestFilter] = useState("");
    const [statusFilter, setStatusFilter] = useState<StatusFilter>("all");
    const [autoRefresh, setAutoRefresh] = useState(false);
    const [expandedRow, setExpandedRow] = useState<number | null>(null);

    useEffect(() => {
        const timer = window.setTimeout(() => {
            setDebouncedRequestFilter(requestFilter.trim());
        }, 350);

        return () => window.clearTimeout(timer);
    }, [requestFilter]);

    const fetchStats = useCallback(async (silent = false) => {
        const requestId = ++activeRequestIdRef.current;

        if (silent) {
            setRefreshing(true);
        } else {
            setLoading(true);
        }

        setError(null);

        try {
            const result = await getPerformanceStats(
                topCount,
                minDuration,
                fromDate || undefined,
                toDate || undefined,
                debouncedRequestFilter || undefined,
                statusFilter
            );

            if (requestId !== activeRequestIdRef.current) return;

            setStats(result.slowestRequests);
            setSummary(result.summary);
            setEndpointStats(result.endpointStats ?? []);
            setTimeline(result.timeline ?? []);
            setLastUpdatedAt(new Date());
        } catch (reason) {
            if (requestId !== activeRequestIdRef.current) return;
            setError(reason instanceof Error ? reason.message : "Unable to load performance data.");
        } finally {
            if (requestId === activeRequestIdRef.current) {
                setLoading(false);
                setRefreshing(false);
            }
        }
    }, [debouncedRequestFilter, fromDate, minDuration, statusFilter, toDate, topCount]);

    useEffect(() => {
        void fetchStats(false);
    }, [fetchStats]);

    useEffect(() => {
        if (!autoRefresh || !apiPingEnabled) return;

        const intervalId = window.setInterval(() => {
            void fetchStats(true);
        }, AUTO_REFRESH_MS);

        return () => window.clearInterval(intervalId);
    }, [apiPingEnabled, autoRefresh, fetchStats]);

    const sortedStats = useMemo(() => {
        const sortedDirection = sortDirection === "asc" ? 1 : -1;

        return [...stats].sort((a, b) => {
            let comparison = 0;
            switch (sortKey) {
                case "timestamp":
                    comparison = new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime();
                    break;
                case "requestName":
                    comparison = a.requestName.localeCompare(b.requestName, "sr");
                    break;
                case "durationMs":
                    comparison = a.durationMs - b.durationMs;
                    break;
                case "isSuccess":
                    comparison = Number(a.isSuccess) - Number(b.isSuccess);
                    break;
                default:
                    comparison = a.id - b.id;
                    break;
            }

            if (comparison === 0) return a.id - b.id;
            return comparison * sortedDirection;
        });
    }, [sortDirection, sortKey, stats]);

    const chartTimeline = useMemo(() => timeline.map((point) => ({
        ...point,
        bucketLabel: formatBucket(point.bucketStart),
        errorRate: point.requestCount > 0 ? Number(((point.failedRequests / point.requestCount) * 100).toFixed(2)) : 0,
    })), [timeline]);

    const topEndpointChart = useMemo(() => endpointStats.slice(0, 8).map((endpoint) => ({
        ...endpoint,
        shortName: endpoint.requestName.length > 34 ? `${endpoint.requestName.slice(0, 34)}...` : endpoint.requestName,
    })), [endpointStats]);

    const slowRate = summary?.totalRequests
        ? (summary.slowRequests / summary.totalRequests) * 100
        : 0;
    const failedRate = summary?.totalRequests
        ? (summary.failedRequests / summary.totalRequests) * 100
        : 0;
    const hasTimeline = chartTimeline.length > 0;
    const hasEndpointStats = topEndpointChart.length > 0;

    const sortIcon = (key: SortKey) => {
        if (sortKey !== key) return <ArrowUpDown size={13} />;
        return sortDirection === "asc" ? "ASC" : "DESC";
    };

    const handleSort = (key: SortKey) => {
        if (sortKey === key) {
            setSortDirection((previous) => (previous === "asc" ? "desc" : "asc"));
            return;
        }

        setSortKey(key);
        setSortDirection("asc");
    };

    const setQuickRangeHours = (hours: number) => {
        const range = getQuickRange(hours);
        setFromDate(range.from);
        setToDate(range.to);
    };

    const resetAllFilters = () => {
        const range = getQuickRange(24);
        setTopCount(DEFAULT_TOP_COUNT);
        setMinDuration(DEFAULT_MIN_DURATION);
        setFromDate(range.from);
        setToDate(range.to);
        setRequestFilter("");
        setDebouncedRequestFilter("");
        setStatusFilter("all");
        setExpandedRow(null);
    };

    return (
        <div className="observability-page">
            <div className="observability-header">
                <div>
                    <span className="observability-eyebrow">System observability</span>
                    <h1 className="observability-title">
                        <Gauge size={22} />
                        Performance
                    </h1>
                    <p className="observability-subtitle">Latency, error pressure, throughput, and slow endpoints.</p>
                </div>

                <div className="observability-actions">
                    <label className="observability-switch">
                        <input
                            type="checkbox"
                            checked={autoRefresh}
                            onChange={(event) => setAutoRefresh(event.target.checked)}
                            disabled={!apiPingEnabled}
                        />
                        Auto-refresh
                    </label>

                    <button
                        type="button"
                        className="observability-button observability-button--primary"
                        onClick={() => void fetchStats(true)}
                        disabled={loading || refreshing}
                    >
                        <RefreshCw size={16} />
                        {refreshing ? "Refreshing" : "Refresh"}
                    </button>
                </div>
            </div>

            <div className="observability-status observability-status--spaced">
                <span className="observability-status__dot" />
                {lastUpdatedAt
                    ? `Updated ${lastUpdatedAt.toLocaleString("sr-RS")}`
                    : "Waiting for first sample"}
                {!apiPingEnabled ? " - global polling paused" : ""}
            </div>

            {summary && (
                <section className="observability-kpis observability-kpis--six" aria-label="Performance overview">
                    <div className="observability-kpi">
                        <div className="observability-kpi__label">
                            <Activity size={15} />
                            Requests
                        </div>
                        <div className="observability-kpi__value">{summary.totalRequests}</div>
                        <div className="observability-kpi__meta">Filtered sample</div>
                    </div>

                    <div className="observability-kpi">
                        <div className="observability-kpi__label">
                            <TimerReset size={15} />
                            P50
                        </div>
                        <div className="observability-kpi__value">{formatDuration(summary.p50DurationMs)}</div>
                        <div className="observability-kpi__meta">Median latency</div>
                    </div>

                    <div className="observability-kpi">
                        <div className="observability-kpi__label">
                            <Zap size={15} />
                            P95 / P99
                        </div>
                        <div className="observability-kpi__value">{formatDuration(summary.p95DurationMs)}</div>
                        <div className="observability-kpi__meta">P99 {formatDuration(summary.p99DurationMs)}</div>
                    </div>

                    <div className="observability-kpi">
                        <div className="observability-kpi__label">
                            <TriangleAlert size={15} />
                            Errors
                        </div>
                        <div className="observability-kpi__value">{failedRate.toFixed(1)}%</div>
                        <div className="observability-kpi__meta">{summary.failedRequests} failed</div>
                    </div>

                    <div className="observability-kpi">
                        <div className="observability-kpi__label">
                            <Clock3 size={15} />
                            Slow
                        </div>
                        <div className="observability-kpi__value">{slowRate.toFixed(1)}%</div>
                        <div className="observability-kpi__meta">{summary.slowRequests} over 1s</div>
                    </div>

                    <div className="observability-kpi">
                        <div className="observability-kpi__label">
                            <BarChart3 size={15} />
                            Max
                        </div>
                        <div className="observability-kpi__value">{formatDuration(summary.maxDurationMs)}</div>
                        <div className="observability-kpi__meta">Avg {formatDuration(summary.averageDurationMs)}</div>
                    </div>
                </section>
            )}

            <section className="observability-panel observability-filters" aria-label="Performance filters">
                <div className="observability-field">
                    <label htmlFor="perf-top">Rows</label>
                    <input
                        id="perf-top"
                        type="number"
                        className="observability-input"
                        value={topCount}
                        onChange={(event) => setTopCount(clampNumber(Number(event.target.value), 1, 200))}
                        min={1}
                        max={200}
                    />
                </div>

                <div className="observability-field">
                    <label htmlFor="perf-min">Min duration</label>
                    <input
                        id="perf-min"
                        type="number"
                        className="observability-input"
                        value={minDuration}
                        onChange={(event) => setMinDuration(clampNumber(Number(event.target.value), 0, 120000))}
                        min={0}
                        max={120000}
                        step={100}
                    />
                </div>

                <div className="observability-field">
                    <label htmlFor="perf-from">From</label>
                    <input
                        id="perf-from"
                        type="datetime-local"
                        className="observability-input"
                        value={fromDate}
                        onChange={(event) => setFromDate(event.target.value)}
                    />
                </div>

                <div className="observability-field">
                    <label htmlFor="perf-to">To</label>
                    <input
                        id="perf-to"
                        type="datetime-local"
                        className="observability-input"
                        value={toDate}
                        onChange={(event) => setToDate(event.target.value)}
                    />
                </div>

                <div className="observability-field">
                    <label htmlFor="perf-request">Endpoint</label>
                    <input
                        id="perf-request"
                        type="search"
                        className="observability-input"
                        value={requestFilter}
                        onChange={(event) => setRequestFilter(event.target.value)}
                        placeholder="GET /api/logs"
                    />
                </div>

                <div className="observability-field">
                    <label htmlFor="perf-status">Status</label>
                    <select
                        id="perf-status"
                        className="observability-select"
                        value={statusFilter}
                        onChange={(event) => setStatusFilter(event.target.value as StatusFilter)}
                    >
                        <option value="all">All</option>
                        <option value="success">Success</option>
                        <option value="failed">Failed</option>
                        <option value="slow">Slow</option>
                    </select>
                </div>

                <div className="observability-inline-actions observability-filter-actions">
                    <button className="observability-button observability-button--compact" type="button" onClick={() => setQuickRangeHours(1)}>
                        1h
                    </button>
                    <button className="observability-button observability-button--compact" type="button" onClick={() => setQuickRangeHours(24)}>
                        24h
                    </button>
                    <button className="observability-button observability-button--compact" type="button" onClick={() => setQuickRangeHours(24 * 7)}>
                        7d
                    </button>
                    <button className="observability-button observability-button--compact" type="button" onClick={resetAllFilters}>
                        <X size={14} />
                        Reset
                    </button>
                </div>
            </section>

            {error && <div className="observability-error">{error}</div>}

            {loading ? (
                <div className="observability-loading">Loading performance telemetry...</div>
            ) : (
                <>
                    <section className="observability-chart-grid" aria-label="Performance charts">
                        <div className="observability-chart-card">
                            <h2 className="observability-card-title">Latency Timeline</h2>
                            {hasTimeline ? (
                                <div className="observability-chart">
                                    <ResponsiveContainer>
                                        <AreaChart data={chartTimeline}>
                                            <CartesianGrid vertical={false} />
                                            <XAxis dataKey="bucketLabel" minTickGap={22} />
                                            <YAxis tickFormatter={(value) => `${value}ms`} />
                                            <Tooltip contentStyle={tooltipStyle} formatter={(value: unknown, name?: string) => [formatDuration(Number(value)), name ?? "Value"]} />
                                            <Area
                                                type="monotone"
                                                dataKey="p95DurationMs"
                                                name="P95"
                                                stroke="var(--color-warning)"
                                                fill="var(--warning-soft)"
                                                strokeWidth={2}
                                            />
                                            <Area
                                                type="monotone"
                                                dataKey="averageDurationMs"
                                                name="Avg"
                                                stroke="var(--color-info)"
                                                fill="var(--info-soft)"
                                                strokeWidth={2}
                                            />
                                        </AreaChart>
                                    </ResponsiveContainer>
                                </div>
                            ) : (
                                <div className="observability-empty">No latency samples for this range.</div>
                            )}
                        </div>

                        <div className="observability-chart-card">
                            <h2 className="observability-card-title">Throughput & Error Rate</h2>
                            {hasTimeline ? (
                                <div className="observability-chart observability-chart--short">
                                    <ResponsiveContainer>
                                        <BarChart data={chartTimeline}>
                                            <CartesianGrid vertical={false} />
                                            <XAxis dataKey="bucketLabel" minTickGap={28} />
                                            <YAxis yAxisId="left" />
                                            <YAxis yAxisId="right" orientation="right" tickFormatter={(value) => `${value}%`} />
                                            <Tooltip contentStyle={tooltipStyle} />
                                            <Bar yAxisId="left" dataKey="requestCount" name="Requests" fill="var(--color-info)" radius={[4, 4, 0, 0]} />
                                            <Bar yAxisId="right" dataKey="errorRate" name="Error %" fill="var(--color-error)" radius={[4, 4, 0, 0]} />
                                        </BarChart>
                                    </ResponsiveContainer>
                                </div>
                            ) : (
                                <div className="observability-empty">No throughput samples for this range.</div>
                            )}
                        </div>
                    </section>

                    <section className="observability-chart-grid" aria-label="Endpoint analysis">
                        <div className="observability-chart-card">
                            <h2 className="observability-card-title">Slowest Endpoints by P95</h2>
                            {hasEndpointStats ? (
                                <div className="observability-chart observability-chart--short">
                                    <ResponsiveContainer>
                                        <BarChart data={topEndpointChart} layout="vertical" margin={{ left: 24 }}>
                                            <CartesianGrid horizontal={false} />
                                            <XAxis type="number" tickFormatter={(value) => `${value}ms`} />
                                            <YAxis type="category" dataKey="shortName" width={150} />
                                            <Tooltip contentStyle={tooltipStyle} formatter={(value: unknown) => formatDuration(Number(value))} />
                                            <Bar dataKey="p95DurationMs" name="P95" fill="var(--color-warning)" radius={[0, 4, 4, 0]} />
                                        </BarChart>
                                    </ResponsiveContainer>
                                </div>
                            ) : (
                                <div className="observability-empty">No endpoint rollups for this filter.</div>
                            )}
                        </div>

                        <div className="observability-chart-card">
                            <h2 className="observability-card-title">Latency Heatmap</h2>
                            {hasTimeline ? (
                                <div className="observability-heatmap">
                                    {chartTimeline.slice(-48).map((point) => {
                                        const heatClass = point.p95DurationMs >= 3000
                                            ? "observability-heatmap__cell--high"
                                            : point.p95DurationMs >= 1000
                                                ? "observability-heatmap__cell--medium"
                                                : "observability-heatmap__cell--low";

                                        return (
                                            <div
                                                key={point.bucketStart}
                                                className={`observability-heatmap__cell ${heatClass}`}
                                                title={`${point.bucketLabel}: ${formatDuration(point.p95DurationMs)} P95, ${point.requestCount} requests`}
                                            />
                                        );
                                    })}
                                </div>
                            ) : (
                                <div className="observability-empty">No latency buckets to render.</div>
                            )}
                        </div>
                    </section>

                    <section className="observability-panel observability-table-shell" aria-label="Slow request table">
                        <table className="observability-table observability-table--performance">
                            <thead>
                                <tr>
                                    <th onClick={() => handleSort("timestamp")}>Time {sortIcon("timestamp")}</th>
                                    <th onClick={() => handleSort("requestName")}>Request {sortIcon("requestName")}</th>
                                    <th className="observability-table__right" onClick={() => handleSort("durationMs")}>
                                        Duration {sortIcon("durationMs")}
                                    </th>
                                    <th className="observability-table__center" onClick={() => handleSort("isSuccess")}>
                                        Status {sortIcon("isSuccess")}
                                    </th>
                                    <th className="observability-table__actions">Details</th>
                                </tr>
                            </thead>
                            <tbody>
                                {sortedStats.map((stat) => {
                                    const status = rowStatus(stat);
                                    const isExpanded = expandedRow === stat.id;

                                    return (
                                        <Fragment key={stat.id}>
                                            <tr>
                                                <td className="observability-mono observability-muted">{formatDate(stat.timestamp)}</td>
                                                <td>
                                                    <span className="observability-truncate" title={stat.requestName}>{stat.requestName}</span>
                                                </td>
                                                <td className="observability-table__right observability-mono">{formatDuration(stat.durationMs)}</td>
                                                <td className="observability-table__center">
                                                    <span className={`status-badge status-badge--${status}`}>{status}</span>
                                                </td>
                                                <td className="observability-table__actions">
                                                    {stat.exceptionMessage ? (
                                                        <button
                                                            className="observability-button observability-button--compact"
                                                            type="button"
                                                            onClick={() => setExpandedRow(isExpanded ? null : stat.id)}
                                                        >
                                                            {isExpanded ? "Hide" : "Show"}
                                                        </button>
                                                    ) : (
                                                        <span className="observability-muted">-</span>
                                                    )}
                                                </td>
                                            </tr>

                                            {isExpanded && stat.exceptionMessage ? (
                                                <tr className="observability-row-detail">
                                                    <td colSpan={5}>
                                                        <div className="observability-detail-box">
                                                            <h4>Exception</h4>
                                                            <pre className="observability-pre observability-mono">
                                                                {stat.exceptionMessage}
                                                            </pre>
                                                        </div>
                                                    </td>
                                                </tr>
                                            ) : null}
                                        </Fragment>
                                    );
                                })}
                            </tbody>
                        </table>
                    </section>

                    {sortedStats.length === 0 && (
                        <div className="observability-empty observability-empty--spaced">
                            No performance rows match the selected filters.
                        </div>
                    )}
                </>
            )}
        </div>
    );
}

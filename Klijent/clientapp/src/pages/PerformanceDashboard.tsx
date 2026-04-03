import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { usePingControl } from "../context/PingControlContext";
import { getPerformanceStats } from "../services/performanceApi";
import { PerformanceStat, PerformanceSummary } from "../types/performance";

type SortKey = "timestamp" | "requestName" | "durationMs" | "isSuccess";
type SortDirection = "asc" | "desc";
type StatusFilter = "all" | "success" | "failed";

const DEFAULT_TOP_COUNT = 20;
const DEFAULT_MIN_DURATION = 1000;
const AUTO_REFRESH_MS = 15_000;

function clampNumber(value: number, min: number, max: number): number {
    if (!Number.isFinite(value)) return min;
    return Math.min(max, Math.max(min, Math.floor(value)));
}

function toDateTimeLocalValue(date: Date): string {
    const timezoneOffsetMs = date.getTimezoneOffset() * 60_000;
    return new Date(date.getTime() - timezoneOffsetMs).toISOString().slice(0, 16);
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

function formatDuration(ms: number): string {
    if (ms < 1000) return `${ms} ms`;
    return `${(ms / 1000).toFixed(2)} s`;
}

function durationColor(ms: number): string {
    if (ms < 1000) return "var(--success)";
    if (ms < 3000) return "var(--warning)";
    if (ms < 5000) return "var(--warning-strong)";
    return "var(--error)";
}

export default function PerformanceDashboard() {
    const { apiPingEnabled } = usePingControl();
    const activeRequestIdRef = useRef(0);

    const [stats, setStats] = useState<PerformanceStat[]>([]);
    const [summary, setSummary] = useState<PerformanceSummary | null>(null);
    const [loading, setLoading] = useState(true);
    const [refreshing, setRefreshing] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [lastUpdatedAt, setLastUpdatedAt] = useState<Date | null>(null);

    const [sortKey, setSortKey] = useState<SortKey>("durationMs");
    const [sortDirection, setSortDirection] = useState<SortDirection>("desc");

    const [topCount, setTopCount] = useState(DEFAULT_TOP_COUNT);
    const [minDuration, setMinDuration] = useState(DEFAULT_MIN_DURATION);
    const [fromDate, setFromDate] = useState<string>("");
    const [toDate, setToDate] = useState<string>("");
    const [requestFilter, setRequestFilter] = useState("");
    const [statusFilter, setStatusFilter] = useState<StatusFilter>("all");
    const [autoRefresh, setAutoRefresh] = useState(false);

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
                toDate || undefined
            );

            if (requestId !== activeRequestIdRef.current) return;

            setStats(result.slowestRequests);
            setSummary(result.summary);
            setLastUpdatedAt(new Date());
        } catch (reason) {
            if (requestId !== activeRequestIdRef.current) return;
            console.error("Error fetching performance stats:", reason);
            setError(reason instanceof Error ? reason.message : "Greska pri ucitavanju performance podataka");
        } finally {
            if (requestId === activeRequestIdRef.current) {
                setLoading(false);
                setRefreshing(false);
            }
        }
    }, [fromDate, minDuration, toDate, topCount]);

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

    const visibleStats = useMemo(() => {
        const normalizedRequestFilter = requestFilter.trim().toLocaleLowerCase("sr-Latn-RS");
        const sortedDirection = sortDirection === "asc" ? 1 : -1;

        return stats
            .filter((stat) => {
                if (statusFilter === "success" && !stat.isSuccess) return false;
                if (statusFilter === "failed" && stat.isSuccess) return false;
                if (!normalizedRequestFilter) return true;
                return stat.requestName.toLocaleLowerCase("sr-Latn-RS").includes(normalizedRequestFilter);
            })
            .sort((a, b) => {
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
    }, [requestFilter, sortDirection, sortKey, stats, statusFilter]);

    const sortIcon = (key: SortKey) => {
        if (sortKey !== key) return "-";
        return sortDirection === "asc" ? "^" : "v";
    };

    const handleSort = (key: SortKey) => {
        if (sortKey === key) {
            setSortDirection((prev) => (prev === "asc" ? "desc" : "asc"));
            return;
        }

        setSortKey(key);
        setSortDirection("asc");
    };

    const setQuickRangeHours = (hours: number) => {
        const now = new Date();
        const from = new Date(now.getTime() - hours * 60 * 60 * 1000);
        setFromDate(toDateTimeLocalValue(from));
        setToDate(toDateTimeLocalValue(now));
    };

    const resetAllFilters = () => {
        setTopCount(DEFAULT_TOP_COUNT);
        setMinDuration(DEFAULT_MIN_DURATION);
        setFromDate("");
        setToDate("");
        setRequestFilter("");
        setStatusFilter("all");
    };

    const failedRate = summary?.totalRequests
        ? (summary.failedRequests / summary.totalRequests) * 100
        : 0;
    const slowRate = summary?.totalRequests
        ? (summary.slowRequests / summary.totalRequests) * 100
        : 0;

    return (
        <div className="card max-w-[1400px]">
            <div className="toolbar flex flex-wrap items-center justify-between gap-4 mb-4">
                <h2 className="text-2xl font-bold text-contrast m-0">Performance dashboard</h2>

                <div className="flex flex-wrap items-center gap-3">
                    <label className="flex items-center gap-2 text-sm text-secondary">
                        <input
                            type="checkbox"
                            checked={autoRefresh}
                            onChange={(event) => setAutoRefresh(event.target.checked)}
                            disabled={!apiPingEnabled}
                        />
                        Auto-refresh (15s){!apiPingEnabled ? " - pauziran globalno" : ""}
                    </label>

                    <button
                        type="button"
                        className="button-big button-secondary"
                        style={{ width: "auto", padding: "10px 14px", margin: 0 }}
                        onClick={() => void fetchStats(true)}
                        disabled={loading || refreshing}
                    >
                        {refreshing ? "Osvezavam..." : "Osvezi sada"}
                    </button>
                </div>
            </div>

            <div className="mb-6 text-sm text-muted">
                {lastUpdatedAt
                    ? `Poslednje osvezavanje: ${lastUpdatedAt.toLocaleString("sr-RS")}`
                    : "Podaci jos nisu osvezeni."}
            </div>

            {summary && (
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-6 gap-4 mb-8">
                    <div className="p-4 rounded-xl border border-muted bg-surface-light">
                        <div className="text-xs uppercase tracking-wider text-muted mb-2">Total requests</div>
                        <div className="text-3xl font-bold" style={{ color: "var(--info)" }}>
                            {summary.totalRequests}
                        </div>
                    </div>

                    <div className="p-4 rounded-xl border border-muted bg-surface-light">
                        <div className="text-xs uppercase tracking-wider text-muted mb-2">Slow requests</div>
                        <div className="text-3xl font-bold" style={{ color: "var(--warning)" }}>
                            {summary.slowRequests}
                        </div>
                    </div>

                    <div className="p-4 rounded-xl border border-muted bg-surface-light">
                        <div className="text-xs uppercase tracking-wider text-muted mb-2">Failed requests</div>
                        <div className="text-3xl font-bold" style={{ color: "var(--error)" }}>
                            {summary.failedRequests}
                        </div>
                    </div>

                    <div className="p-4 rounded-xl border border-muted bg-surface-light">
                        <div className="text-xs uppercase tracking-wider text-muted mb-2">Avg duration</div>
                        <div className="text-3xl font-bold text-contrast">
                            {formatDuration(summary.averageDurationMs)}
                        </div>
                    </div>

                    <div className="p-4 rounded-xl border border-muted bg-surface-light">
                        <div className="text-xs uppercase tracking-wider text-muted mb-2">Max duration</div>
                        <div className="text-3xl font-bold" style={{ color: "var(--error)" }}>
                            {formatDuration(summary.maxDurationMs)}
                        </div>
                    </div>

                    <div className="p-4 rounded-xl border border-muted bg-surface-light">
                        <div className="text-xs uppercase tracking-wider text-muted mb-2">Error / Slow rate</div>
                        <div className="text-lg font-bold text-contrast">
                            {failedRate.toFixed(1)}% / {slowRate.toFixed(1)}%
                        </div>
                    </div>
                </div>
            )}

            <div className="toolbar grid grid-cols-1 md:grid-cols-2 xl:grid-cols-7 gap-4 mb-6 p-4 rounded-xl border border-muted bg-surface-darker">
                <div className="flex flex-col gap-1">
                    <label className="text-xs font-semibold uppercase tracking-wider text-muted">Broj zapisa</label>
                    <input
                        type="number"
                        className="input-big w-full"
                        value={topCount}
                        onChange={(event) => setTopCount(clampNumber(Number(event.target.value), 1, 200))}
                        min={1}
                        max={200}
                    />
                </div>

                <div className="flex flex-col gap-1">
                    <label className="text-xs font-semibold uppercase tracking-wider text-muted">Min trajanje (ms)</label>
                    <input
                        type="number"
                        className="input-big w-full"
                        value={minDuration}
                        onChange={(event) => setMinDuration(clampNumber(Number(event.target.value), 0, 120000))}
                        min={0}
                        max={120000}
                        step={100}
                    />
                </div>

                <div className="flex flex-col gap-1">
                    <label className="text-xs font-semibold uppercase tracking-wider text-muted">Od datuma</label>
                    <input
                        type="datetime-local"
                        className="input-big w-full"
                        value={fromDate}
                        onChange={(event) => setFromDate(event.target.value)}
                    />
                </div>

                <div className="flex flex-col gap-1">
                    <label className="text-xs font-semibold uppercase tracking-wider text-muted">Do datuma</label>
                    <input
                        type="datetime-local"
                        className="input-big w-full"
                        value={toDate}
                        onChange={(event) => setToDate(event.target.value)}
                    />
                </div>

                <div className="flex flex-col gap-1">
                    <label className="text-xs font-semibold uppercase tracking-wider text-muted">Request filter</label>
                    <input
                        type="search"
                        className="input-big w-full"
                        value={requestFilter}
                        onChange={(event) => setRequestFilter(event.target.value)}
                        placeholder="npr. GetTopProducts"
                    />
                </div>

                <div className="flex flex-col gap-1">
                    <label className="text-xs font-semibold uppercase tracking-wider text-muted">Status</label>
                    <select
                        className="input-big w-full"
                        value={statusFilter}
                        onChange={(event) => setStatusFilter(event.target.value as StatusFilter)}
                    >
                        <option value="all">Sve</option>
                        <option value="success">Samo uspeh</option>
                        <option value="failed">Samo greske</option>
                    </select>
                </div>

                <div className="flex flex-col gap-2 justify-end">
                    <div className="flex flex-wrap gap-2">
                        <button
                            type="button"
                            className="button-big button-secondary"
                            style={{ width: "auto", margin: 0, padding: "8px 10px", fontSize: "0.85rem" }}
                            onClick={() => setQuickRangeHours(24)}
                        >
                            24h
                        </button>
                        <button
                            type="button"
                            className="button-big button-secondary"
                            style={{ width: "auto", margin: 0, padding: "8px 10px", fontSize: "0.85rem" }}
                            onClick={() => setQuickRangeHours(24 * 7)}
                        >
                            7d
                        </button>
                        <button
                            type="button"
                            className="button-big button-secondary"
                            style={{ width: "auto", margin: 0, padding: "8px 10px", fontSize: "0.85rem" }}
                            onClick={() => setQuickRangeHours(24 * 30)}
                        >
                            30d
                        </button>
                    </div>

                    <button
                        type="button"
                        className="button-big button-secondary"
                        style={{ margin: 0 }}
                        onClick={resetAllFilters}
                    >
                        Resetuj sve
                    </button>
                </div>
            </div>

            {error && (
                <div className="mb-6 p-4 rounded-xl border border-error bg-error/10 text-error text-sm">
                    {error}
                </div>
            )}

            {loading ? (
                <div className="py-20 text-center text-muted uppercase tracking-widest text-xs font-bold">
                    Ucitavanje podataka o performansama...
                </div>
            ) : (
                <>
                    <div className="flex items-center justify-between mb-3 gap-4">
                        <h3 className="text-lg font-bold text-contrast m-0">Najsporiji zahtevi</h3>
                        <div className="text-sm text-muted">
                            Prikazano: {visibleStats.length} / {stats.length}
                        </div>
                    </div>

                    <div className="overflow-hidden rounded-xl border border-muted bg-surface-elevated">
                        <div className="overflow-x-auto">
                            <table className="min-w-full divide-y divide-muted text-sm">
                                <thead className="bg-surface-darker text-muted">
                                    <tr>
                                        <th
                                            className="px-4 py-3 text-left font-semibold uppercase tracking-wider cursor-pointer hover:text-contrast transition-colors"
                                            onClick={() => handleSort("timestamp")}
                                        >
                                            Vreme {sortIcon("timestamp")}
                                        </th>
                                        <th
                                            className="px-4 py-3 text-left font-semibold uppercase tracking-wider cursor-pointer hover:text-contrast transition-colors"
                                            onClick={() => handleSort("requestName")}
                                        >
                                            Request {sortIcon("requestName")}
                                        </th>
                                        <th
                                            className="px-4 py-3 text-right font-semibold uppercase tracking-wider cursor-pointer hover:text-contrast transition-colors"
                                            onClick={() => handleSort("durationMs")}
                                        >
                                            Trajanje {sortIcon("durationMs")}
                                        </th>
                                        <th
                                            className="px-4 py-3 text-center font-semibold uppercase tracking-wider cursor-pointer hover:text-contrast transition-colors"
                                            onClick={() => handleSort("isSuccess")}
                                        >
                                            Status {sortIcon("isSuccess")}
                                        </th>
                                        <th className="px-4 py-3 text-left font-semibold uppercase tracking-wider">
                                            Exception
                                        </th>
                                    </tr>
                                </thead>
                                <tbody className="divide-y divide-muted/50 text-contrast font-mono text-xs">
                                    {visibleStats.map((stat) => (
                                        <tr
                                            key={stat.id}
                                            className={stat.isSuccess ? "hover:bg-surface/30 transition-colors" : "bg-error/5 hover:bg-error/10 transition-colors"}
                                        >
                                            <td className="px-4 py-3 whitespace-nowrap opacity-75">{formatDate(stat.timestamp)}</td>
                                            <td className="px-4 py-3 font-semibold text-sm font-sans">{stat.requestName}</td>
                                            <td
                                                className="px-4 py-3 text-right font-bold text-sm"
                                                style={{ color: durationColor(stat.durationMs) }}
                                            >
                                                {formatDuration(stat.durationMs)}
                                            </td>
                                            <td className="px-4 py-3 text-center">
                                                {stat.isSuccess ? (
                                                    <span
                                                        className="px-2 py-1 rounded-lg text-[10px] font-bold uppercase"
                                                        style={{
                                                            color: "var(--success)",
                                                            border: "1px solid var(--success)",
                                                            background: "var(--surface-light)",
                                                        }}
                                                    >
                                                        Success
                                                    </span>
                                                ) : (
                                                    <span
                                                        className="px-2 py-1 rounded-lg text-[10px] font-bold uppercase"
                                                        style={{
                                                            color: "var(--error)",
                                                            border: "1px solid var(--error)",
                                                            background: "var(--surface-light)",
                                                        }}
                                                    >
                                                        Failed
                                                    </span>
                                                )}
                                            </td>
                                            <td className="px-4 py-3">
                                                {stat.exceptionMessage ? (
                                                    <details>
                                                        <summary className="cursor-pointer text-secondary text-xs">
                                                            Prikazi detalje
                                                        </summary>
                                                        <pre
                                                            className="mt-2 p-2 rounded border border-muted bg-surface-light text-[11px] whitespace-pre-wrap break-words"
                                                            style={{ fontFamily: "ui-monospace, SFMono-Regular, Menlo, Consolas, monospace" }}
                                                        >
                                                            {stat.exceptionMessage}
                                                        </pre>
                                                    </details>
                                                ) : (
                                                    <span className="text-muted">-</span>
                                                )}
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    </div>

                    {visibleStats.length === 0 && (
                        <div className="py-20 text-center text-muted border border-dashed border-muted rounded-xl mt-4">
                            Nema podataka za izabrane filtere.
                        </div>
                    )}
                </>
            )}
        </div>
    );
}

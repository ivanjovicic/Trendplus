import React, { useEffect, useMemo, useState } from "react";
import { getPerformanceStats } from "../services/performanceApi";
import { PerformanceStat, PerformanceSummary } from "../types/performance";

type SortKey = "timestamp" | "requestName" | "durationMs" | "isSuccess";
type SortDirection = "asc" | "desc";

export default function PerformanceDashboard() {
    const [stats, setStats] = useState<PerformanceStat[]>([]);
    const [summary, setSummary] = useState<PerformanceSummary | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [sortKey, setSortKey] = useState<SortKey>("durationMs");
    const [sortDirection, setSortDirection] = useState<SortDirection>("desc");

    // Filters
    const [topCount, setTopCount] = useState(20);
    const [minDuration, setMinDuration] = useState(1000);
    const [fromDate, setFromDate] = useState<string>("");
    const [toDate, setToDate] = useState<string>("");

    const fetchStats = async () => {
        setLoading(true);
        setError(null);

        try {
            const result = await getPerformanceStats(
                topCount,
                minDuration,
                fromDate || undefined,
                toDate || undefined
            );

            setStats(result.slowestRequests);
            setSummary(result.summary);
        } catch (err: any) {
            console.error("Error fetching performance stats:", err);
            setError(err?.message ?? "Greška pri učitavanju performansi");
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchStats();
    }, [topCount, minDuration, fromDate, toDate]);

    const formatDate = (timestamp: string) => {
        const date = new Date(timestamp);
        return date.toLocaleString("sr-RS", {
            year: "numeric",
            month: "2-digit",
            day: "2-digit",
            hour: "2-digit",
            minute: "2-digit",
            second: "2-digit",
        });
    };

    const formatDuration = (ms: number) => {
        if (ms < 1000) return `${ms}ms`;
        return `${(ms / 1000).toFixed(2)}s`;
    };

    const getDurationClass = (ms: number) => {
        if (ms < 1000) return "text-success";
        if (ms < 3000) return "text-warning";
        if (ms < 5000) return "text-accent-warning";
        return "text-error";
    };

    const handleSort = (key: SortKey) => {
        if (sortKey === key) {
            setSortDirection((prev) => (prev === "asc" ? "desc" : "asc"));
            return;
        }

        setSortKey(key);
        setSortDirection("asc");
    };

    const sortedStats = useMemo(() => {
        const factor = sortDirection === "asc" ? 1 : -1;

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
            }

            if (comparison === 0) return a.id - b.id;
            return comparison * factor;
        });
    }, [stats, sortDirection, sortKey]);

    const getSortIcon = (key: SortKey) => {
        if (sortKey !== key) return "⇅";
        return sortDirection === "asc" ? "↑" : "↓";
    };

    return (
        <div className="card max-w-[1400px]">
            <h2 className="text-2xl font-bold mb-6 text-contrast">
                ⚡ Performance Dashboard
            </h2>

            {/* Summary Cards */}
            {summary && (
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-4 mb-8">
                    <div className="p-4 rounded-xl border-2 border-info bg-info/10">
                        <div className="text-xs font-semibold uppercase tracking-wider text-muted mb-2">
                            Total Requests
                        </div>
                        <div className="text-3xl font-bold text-info">
                            {summary.totalRequests}
                        </div>
                    </div>

                    <div className="p-4 rounded-xl border-2 border-warning bg-warning/10">
                        <div className="text-xs font-semibold uppercase tracking-wider text-muted mb-2">
                            Slow Requests (&gt;1s)
                        </div>
                        <div className="text-3xl font-bold text-warning">
                            {summary.slowRequests}
                        </div>
                    </div>

                    <div className={`p-4 rounded-xl border-2 ${summary.failedRequests > 0 ? "border-error bg-error/10" : "border-success bg-success/10"}`}>
                        <div className="text-xs font-semibold uppercase tracking-wider text-muted mb-2">
                            Failed Requests
                        </div>
                        <div className={`text-3xl font-bold ${summary.failedRequests > 0 ? "text-error" : "text-success"}`}>
                            {summary.failedRequests}
                        </div>
                    </div>

                    <div className="p-4 rounded-xl border-2 border-muted bg-surface-darker">
                        <div className="text-xs font-semibold uppercase tracking-wider text-muted mb-2">
                            Average Duration
                        </div>
                        <div className="text-3xl font-bold text-contrast">
                            {formatDuration(summary.averageDurationMs)}
                        </div>
                    </div>

                    <div className="p-4 rounded-xl border-2 border-error bg-error/5">
                        <div className="text-xs font-semibold uppercase tracking-wider text-muted mb-2">
                            Max Duration
                        </div>
                        <div className="text-3xl font-bold text-error">
                            {formatDuration(summary.maxDurationMs)}
                        </div>
                    </div>
                </div>
            )}

            {/* Filters */}
            <div className="toolbar grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-4 mb-6 p-4 rounded-xl border border-muted bg-surface-darker">
                <div className="flex flex-col gap-1">
                    <label className="text-xs font-semibold uppercase tracking-wider text-muted">Broj zapisa</label>
                    <input
                        type="number"
                        className="input-big w-full"
                        value={topCount}
                        onChange={(e) => setTopCount(Number(e.target.value))}
                        min={1}
                        max={100}
                    />
                </div>

                <div className="flex flex-col gap-1">
                    <label className="text-xs font-semibold uppercase tracking-wider text-muted">Min. trajanje (ms)</label>
                    <input
                        type="number"
                        className="input-big w-full"
                        value={minDuration}
                        onChange={(e) => setMinDuration(Number(e.target.value))}
                        min={0}
                        step={100}
                    />
                </div>

                <div className="flex flex-col gap-1">
                    <label className="text-xs font-semibold uppercase tracking-wider text-muted">Od datuma</label>
                    <input
                        type="datetime-local"
                        className="input-big w-full"
                        value={fromDate}
                        onChange={(e) => setFromDate(e.target.value)}
                    />
                </div>

                <div className="flex flex-col gap-1">
                    <label className="text-xs font-semibold uppercase tracking-wider text-muted">Do datuma</label>
                    <input
                        type="datetime-local"
                        className="input-big w-full"
                        value={toDate}
                        onChange={(e) => setToDate(e.target.value)}
                    />
                </div>

                <div className="flex items-end">
                    <button
                        className="button-big button-secondary w-full"
                        onClick={() => {
                            setTopCount(20);
                            setMinDuration(1000);
                            setFromDate("");
                            setToDate("");
                        }}
                        type="button"
                    >
                        Resetuj
                    </button>
                </div>
            </div>

            {/* Error Message */}
            {error && (
                <div className="mb-6 p-4 rounded-xl border border-error bg-error/10 text-error text-sm">
                    {error}
                </div>
            )}

            {/* Slowest Requests Table */}
            {loading ? (
                <div className="py-20 text-center text-muted uppercase tracking-widest text-xs font-bold">
                    Učitavanje podataka o performansama...
                </div>
            ) : (
                <>
                    <h3 className="text-lg font-bold text-contrast mb-4">
                        🚀 Najsporiji zahtevi
                    </h3>

                    <div className="overflow-hidden rounded-xl border border-muted bg-surface-elevated">
                        <div className="overflow-x-auto">
                            <table className="min-w-full divide-y divide-muted text-sm">
                                <thead className="bg-surface-darker text-muted">
                                    <tr>
                                        <th className="px-4 py-3 text-left font-semibold uppercase tracking-wider cursor-pointer hover:text-contrast transition-colors" onClick={() => handleSort("timestamp")}>
                                            Vreme {getSortIcon("timestamp")}
                                        </th>
                                        <th className="px-4 py-3 text-left font-semibold uppercase tracking-wider cursor-pointer hover:text-contrast transition-colors" onClick={() => handleSort("requestName")}>
                                            Request {getSortIcon("requestName")}
                                        </th>
                                        <th className="px-4 py-3 text-right font-semibold uppercase tracking-wider cursor-pointer hover:text-contrast transition-colors" onClick={() => handleSort("durationMs")}>
                                            Trajanje {getSortIcon("durationMs")}
                                        </th>
                                        <th className="px-4 py-3 text-center font-semibold uppercase tracking-wider cursor-pointer hover:text-contrast transition-colors" onClick={() => handleSort("isSuccess")}>
                                            Status {getSortIcon("isSuccess")}
                                        </th>
                                    </tr>
                                </thead>
                                <tbody className="divide-y divide-muted/50 text-contrast font-mono text-xs">
                                    {sortedStats.map((stat) => (
                                        <React.Fragment key={stat.id}>
                                            <tr className={stat.isSuccess ? "hover:bg-surface/30 transition-colors" : "bg-error/5 hover:bg-error/10 transition-colors"}>
                                                <td className="px-4 py-2 whitespace-nowrap opacity-70">
                                                    {formatDate(stat.timestamp)}
                                                </td>
                                                <td className="px-4 py-2 font-semibold text-sm font-sans">{stat.requestName}</td>
                                                <td className={`px-4 py-2 text-right font-bold text-sm ${getDurationClass(stat.durationMs)}`}>
                                                    {formatDuration(stat.durationMs)}
                                                </td>
                                                <td className="px-4 py-2 text-center">
                                                    {stat.isSuccess ? (
                                                        <span className="px-2 py-1 rounded-lg border border-success bg-success/10 text-success text-[10px] font-bold uppercase">
                                                            Success
                                                        </span>
                                                    ) : (
                                                        <span className="px-2 py-1 rounded-lg border border-error bg-error/10 text-error text-[10px] font-bold uppercase">
                                                            Failed
                                                        </span>
                                                    )}
                                                </td>
                                            </tr>
                                        </React.Fragment>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    </div>

                    {sortedStats.length === 0 && (
                        <div className="py-20 text-center text-muted border border-dashed border-muted rounded-xl mt-4">
                            Nema podataka za zadate kriterijume pretrage.
                        </div>
                    )}
                </>
            )}
        </div>
    );
}

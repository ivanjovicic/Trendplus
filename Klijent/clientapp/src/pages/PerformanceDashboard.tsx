import React, { useState, useEffect } from "react";
import { getPerformanceStats } from "../services/performanceApi";
import { PerformanceStat, PerformanceSummary } from "../types/performance";

export default function PerformanceDashboard() {
    const [stats, setStats] = useState<PerformanceStat[]>([]);
    const [summary, setSummary] = useState<PerformanceSummary | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    
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
            setError(err?.message ?? "Greška pri u?itavanju performansi");
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

    const getDurationColor = (ms: number) => {
        if (ms < 1000) return "#059669"; // green
        if (ms < 3000) return "#f59e0b"; // yellow
        if (ms < 5000) return "#f97316"; // orange
        return "#dc2626"; // red
    };

    return (
        <div className="card" style={{ maxWidth: "1400px" }}>
            <h2 className="text-2xl font-semibold mb-6">? Performance Dashboard</h2>

            {/* Summary Cards */}
            {summary && (
                <div style={{ 
                    display: "grid", 
                    gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", 
                    gap: "1rem", 
                    marginBottom: "2rem" 
                }}>
                    <div style={{ 
                        background: "#eff6ff", 
                        padding: "1.5rem", 
                        borderRadius: "12px",
                        border: "2px solid #2563eb"
                    }}>
                        <div style={{ fontSize: "0.875rem", color: "#6b7280", marginBottom: "0.5rem" }}>
                            Total Requests
                        </div>
                        <div style={{ fontSize: "2rem", fontWeight: 700, color: "#2563eb" }}>
                            {summary.totalRequests}
                        </div>
                    </div>

                    <div style={{ 
                        background: "#fef3c7", 
                        padding: "1.5rem", 
                        borderRadius: "12px",
                        border: "2px solid #f59e0b"
                    }}>
                        <div style={{ fontSize: "0.875rem", color: "#6b7280", marginBottom: "0.5rem" }}>
                            Slow Requests (&gt;1s)
                        </div>
                        <div style={{ fontSize: "2rem", fontWeight: 700, color: "#f59e0b" }}>
                            {summary.slowRequests}
                        </div>
                    </div>

                    <div style={{ 
                        background: summary.failedRequests > 0 ? "#fef2f2" : "#f0fdf4", 
                        padding: "1.5rem", 
                        borderRadius: "12px",
                        border: `2px solid ${summary.failedRequests > 0 ? "#dc2626" : "#059669"}`
                    }}>
                        <div style={{ fontSize: "0.875rem", color: "#6b7280", marginBottom: "0.5rem" }}>
                            Failed Requests
                        </div>
                        <div style={{ 
                            fontSize: "2rem", 
                            fontWeight: 700, 
                            color: summary.failedRequests > 0 ? "#dc2626" : "#059669" 
                        }}>
                            {summary.failedRequests}
                        </div>
                    </div>

                    <div style={{ 
                        background: "#f3f4f6", 
                        padding: "1.5rem", 
                        borderRadius: "12px",
                        border: "2px solid #6b7280"
                    }}>
                        <div style={{ fontSize: "0.875rem", color: "#6b7280", marginBottom: "0.5rem" }}>
                            Average Duration
                        </div>
                        <div style={{ fontSize: "2rem", fontWeight: 700, color: "#374151" }}>
                            {formatDuration(summary.averageDurationMs)}
                        </div>
                    </div>

                    <div style={{ 
                        background: "#fef2f2", 
                        padding: "1.5rem", 
                        borderRadius: "12px",
                        border: "2px solid #dc2626"
                    }}>
                        <div style={{ fontSize: "0.875rem", color: "#6b7280", marginBottom: "0.5rem" }}>
                            Max Duration
                        </div>
                        <div style={{ fontSize: "2rem", fontWeight: 700, color: "#dc2626" }}>
                            {formatDuration(summary.maxDurationMs)}
                        </div>
                    </div>
                </div>
            )}

            {/* Filters */}
            <div style={{ 
                display: "grid", 
                gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", 
                gap: "1rem", 
                marginBottom: "1.5rem",
                padding: "1rem",
                background: "#f9fafb",
                borderRadius: "12px"
            }}>
                <div>
                    <label className="field-label" style={{ fontSize: "0.875rem" }}>Top Count</label>
                    <input
                        type="number"
                        className="input-big"
                        value={topCount}
                        onChange={(e) => setTopCount(Number(e.target.value))}
                        min={1}
                        max={100}
                        style={{ marginTop: "0.25rem", marginBottom: 0, fontSize: "0.95rem", padding: "8px 12px" }}
                    />
                </div>

                <div>
                    <label className="field-label" style={{ fontSize: "0.875rem" }}>Min Duration (ms)</label>
                    <input
                        type="number"
                        className="input-big"
                        value={minDuration}
                        onChange={(e) => setMinDuration(Number(e.target.value))}
                        min={0}
                        step={100}
                        style={{ marginTop: "0.25rem", marginBottom: 0, fontSize: "0.95rem", padding: "8px 12px" }}
                    />
                </div>

                <div>
                    <label className="field-label" style={{ fontSize: "0.875rem" }}>Od datuma</label>
                    <input
                        type="datetime-local"
                        className="input-big"
                        value={fromDate}
                        onChange={(e) => setFromDate(e.target.value)}
                        style={{ marginTop: "0.25rem", marginBottom: 0, fontSize: "0.95rem", padding: "8px 12px" }}
                    />
                </div>

                <div>
                    <label className="field-label" style={{ fontSize: "0.875rem" }}>Do datuma</label>
                    <input
                        type="datetime-local"
                        className="input-big"
                        value={toDate}
                        onChange={(e) => setToDate(e.target.value)}
                        style={{ marginTop: "0.25rem", marginBottom: 0, fontSize: "0.95rem", padding: "8px 12px" }}
                    />
                </div>

                <div style={{ display: "flex", alignItems: "flex-end" }}>
                    <button
                        className="button-big"
                        onClick={() => {
                            setTopCount(20);
                            setMinDuration(1000);
                            setFromDate("");
                            setToDate("");
                        }}
                        style={{ 
                            background: "#6b7280", 
                            padding: "8px 16px", 
                            marginTop: 0,
                            marginBottom: 0,
                            fontSize: "0.95rem"
                        }}
                    >
                        Reset
                    </button>
                </div>
            </div>

            {/* Loading / Error */}
            {loading && <p style={{ textAlign: "center", padding: "2rem" }}>U?itavanje...</p>}
            {error && <p className="error-msg">{error}</p>}

            {/* Slowest Requests Table */}
            {!loading && !error && (
                <>
                    <h3 className="text-lg font-semibold" style={{ marginBottom: "1rem" }}>
                        ?? Najsporiji zahtevi
                    </h3>

                    <div style={{ overflowX: "auto" }}>
                        <table style={{ 
                            width: "100%", 
                            borderCollapse: "collapse",
                            fontSize: "0.875rem"
                        }}>
                            <thead>
                                <tr style={{ 
                                    background: "#f3f4f6", 
                                    borderBottom: "2px solid #e5e7eb" 
                                }}>
                                    <th style={{ padding: "12px", textAlign: "left", fontWeight: 600 }}>
                                        Vreme
                                    </th>
                                    <th style={{ padding: "12px", textAlign: "left", fontWeight: 600 }}>
                                        Request
                                    </th>
                                    <th style={{ padding: "12px", textAlign: "right", fontWeight: 600 }}>
                                        Trajanje
                                    </th>
                                    <th style={{ padding: "12px", textAlign: "center", fontWeight: 600 }}>
                                        Status
                                    </th>
                                </tr>
                            </thead>
                            <tbody>
                                {stats.map((stat) => (
                                    <React.Fragment key={stat.id}>
                                        <tr style={{ 
                                            borderBottom: "1px solid #e5e7eb",
                                            background: stat.isSuccess ? "#ffffff" : "#fef2f2"
                                        }}>
                                            <td style={{ 
                                                padding: "12px", 
                                                whiteSpace: "nowrap",
                                                fontFamily: "monospace",
                                                fontSize: "0.8rem"
                                            }}>
                                                {formatDate(stat.timestamp)}
                                            </td>
                                            <td style={{ padding: "12px", fontWeight: 500 }}>
                                                {stat.requestName}
                                            </td>
                                            <td style={{ 
                                                padding: "12px", 
                                                textAlign: "right",
                                                fontWeight: 700,
                                                color: getDurationColor(stat.durationMs)
                                            }}>
                                                {formatDuration(stat.durationMs)}
                                            </td>
                                            <td style={{ padding: "12px", textAlign: "center" }}>
                                                {stat.isSuccess ? (
                                                    <span style={{
                                                        padding: "4px 12px",
                                                        borderRadius: "6px",
                                                        background: "#f0fdf4",
                                                        color: "#059669",
                                                        fontSize: "0.75rem",
                                                        fontWeight: 600
                                                    }}>
                                                        ? Success
                                                    </span>
                                                ) : (
                                                    <span style={{
                                                        padding: "4px 12px",
                                                        borderRadius: "6px",
                                                        background: "#fef2f2",
                                                        color: "#dc2626",
                                                        fontSize: "0.75rem",
                                                        fontWeight: 600
                                                    }}>
                                                        ? Failed
                                                    </span>
                                                )}
                                            </td>
                                        </tr>
                                        {stat.exceptionMessage && (
                                            <tr style={{ background: "#fef2f2" }}>
                                                <td colSpan={4} style={{ padding: "12px" }}>
                                                    <div style={{ 
                                                        color: "#dc2626", 
                                                        fontSize: "0.8rem",
                                                        fontFamily: "monospace"
                                                    }}>
                                                        <strong>Exception:</strong> {stat.exceptionMessage}
                                                    </div>
                                                </td>
                                            </tr>
                                        )}
                                    </React.Fragment>
                                ))}
                            </tbody>
                        </table>
                    </div>
                </>
            )}

            {!loading && !error && stats.length === 0 && (
                <p style={{ textAlign: "center", padding: "2rem", color: "#6b7280" }}>
                    Nema sporih zahteva koji odgovaraju filterima. ??
                </p>
            )}
        </div>
    );
}

import React, { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { getOutboxStats, getOutboxMessages, retryOutboxMessage, retryAllFailedMessages, purgeProcessedMessages, getEventTypeStats } from "../services/outboxApi";
import { OutboxStats, OutboxMessage } from "../types/outbox";

export default function OutboxDashboard() {
    const [stats, setStats] = useState<OutboxStats | null>(null);
    const [recentMessages, setRecentMessages] = useState<OutboxMessage[]>([]);
    const [eventTypeStats, setEventTypeStats] = useState<any[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [autoRefresh, setAutoRefresh] = useState(true);

    const fetchStats = async () => {
        try {
            const [statsResult, eventStatsResult] = await Promise.all([
                getOutboxStats(),
                getEventTypeStats()
            ]);
            
            setStats(statsResult.stats);
            setRecentMessages(statsResult.recentMessages);
            setEventTypeStats(eventStatsResult);
            setError(null);
        } catch (err: any) {
            console.error("Error fetching outbox stats:", err);
            setError(err?.message ?? "Greška pri u?itavanju outbox statistike");
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchStats();
    }, []);

    useEffect(() => {
        if (!autoRefresh) return;

        const interval = setInterval(fetchStats, 10000); // Refresh every 10s
        return () => clearInterval(interval);
    }, [autoRefresh]);

    const handleRetry = async (id: number) => {
        if (!confirm(`Da li želite da pokušate ponovo da pošaljete poruku ${id}?`)) {
            return;
        }

        try {
            await retryOutboxMessage(id);
            alert("Poruka je ozna?ena za ponovno slanje!");
            await fetchStats();
        } catch (err: any) {
            alert(`Greška: ${err.message}`);
        }
    };

    const handleRetryAllFailed = async () => {
        if (!confirm(`Da li želite da pokušate ponovo da pošaljete SVE neuspele poruke?`)) {
            return;
        }

        try {
            const result = await retryAllFailedMessages();
            alert(`${result.count} poruka je ozna?eno za ponovno slanje!`);
            await fetchStats();
        } catch (err: any) {
            alert(`Greška: ${err.message}`);
        }
    };

    const handlePurgeProcessed = async () => {
        const days = prompt("Obriši obra?ene poruke starije od koliko dana?", "7");
        if (!days) return;

        if (!confirm(`Da li želite da obrišete sve obra?ene poruke starije od ${days} dana?`)) {
            return;
        }

        try {
            const result = await purgeProcessedMessages(Number(days));
            alert(`${result.count} poruka je obrisano!`);
            await fetchStats();
        } catch (err: any) {
            alert(`Greška: ${err.message}`);
        }
    };

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

    const formatPayload = (payload: string) => {
        try {
            const parsed = JSON.parse(payload);
            return JSON.stringify(parsed, null, 2);
        } catch {
            return payload;
        }
    };

    return (
        <div className="card" style={{ maxWidth: "1400px" }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1.5rem" }}>
                <h2 className="text-2xl font-semibold">?? Outbox Dashboard</h2>
                
                <div style={{ display: "flex", gap: "1rem", alignItems: "center" }}>
                    <Link 
                        to="/outbox/messages"
                        className="button-big"
                        style={{ background: "#8b5cf6", padding: "8px 16px", marginBottom: 0, textDecoration: "none" }}
                    >
                        ?? All Messages
                    </Link>
                    
                    <label style={{ display: "flex", alignItems: "center", gap: "0.5rem", fontSize: "0.875rem" }}>
                        <input
                            type="checkbox"
                            checked={autoRefresh}
                            onChange={(e) => setAutoRefresh(e.target.checked)}
                        />
                        Auto-refresh (10s)
                    </label>
                    
                    <button
                        className="button-big"
                        onClick={fetchStats}
                        style={{ background: "#0891b2", padding: "8px 16px", marginBottom: 0 }}
                    >
                        ?? Refresh
                    </button>
                </div>
            </div>

            {/* Bulk Operations */}
            {stats && stats.failed > 0 && (
                <div style={{ 
                    display: "flex", 
                    gap: "1rem", 
                    marginBottom: "1.5rem",
                    padding: "1rem",
                    background: "#fef2f2",
                    borderRadius: "12px",
                    border: "2px solid #dc2626"
                }}>
                    <button
                        className="button-big"
                        onClick={handleRetryAllFailed}
                        style={{ background: "#dc2626", padding: "8px 16px", marginBottom: 0 }}
                    >
                        ?? Retry All Failed ({stats.failed})
                    </button>
                    
                    <button
                        className="button-big"
                        onClick={handlePurgeProcessed}
                        style={{ background: "#6b7280", padding: "8px 16px", marginBottom: 0 }}
                    >
                        ??? Purge Processed
                    </button>
                </div>
            )}

            {/* Summary Cards */}
            {stats && (
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
                            Total Messages
                        </div>
                        <div style={{ fontSize: "2rem", fontWeight: 700, color: "#2563eb" }}>
                            {stats.total}
                        </div>
                    </div>

                    <div style={{ 
                        background: "#f0fdf4", 
                        padding: "1.5rem", 
                        borderRadius: "12px",
                        border: "2px solid #059669"
                    }}>
                        <div style={{ fontSize: "0.875rem", color: "#6b7280", marginBottom: "0.5rem" }}>
                            Processed
                        </div>
                        <div style={{ fontSize: "2rem", fontWeight: 700, color: "#059669" }}>
                            {stats.processed}
                        </div>
                    </div>

                    <div style={{ 
                        background: "#fef3c7", 
                        padding: "1.5rem", 
                        borderRadius: "12px",
                        border: "2px solid #f59e0b"
                    }}>
                        <div style={{ fontSize: "0.875rem", color: "#6b7280", marginBottom: "0.5rem" }}>
                            Pending
                        </div>
                        <div style={{ fontSize: "2rem", fontWeight: 700, color: "#f59e0b" }}>
                            {stats.pending}
                        </div>
                    </div>

                    <div style={{ 
                        background: stats.failed > 0 ? "#fef2f2" : "#f3f4f6", 
                        padding: "1.5rem", 
                        borderRadius: "12px",
                        border: `2px solid ${stats.failed > 0 ? "#dc2626" : "#6b7280"}`
                    }}>
                        <div style={{ fontSize: "0.875rem", color: "#6b7280", marginBottom: "0.5rem" }}>
                            Failed (Retries ? 5)
                        </div>
                        <div style={{ 
                            fontSize: "2rem", 
                            fontWeight: 700, 
                            color: stats.failed > 0 ? "#dc2626" : "#6b7280" 
                        }}>
                            {stats.failed}
                        </div>
                    </div>

                    <div style={{ 
                        background: "#ecfdf5", 
                        padding: "1.5rem", 
                        borderRadius: "12px",
                        border: "2px solid #10b981"
                    }}>
                        <div style={{ fontSize: "0.875rem", color: "#6b7280", marginBottom: "0.5rem" }}>
                            Success Rate
                        </div>
                        <div style={{ fontSize: "2rem", fontWeight: 700, color: "#10b981" }}>
                            {stats.successRate.toFixed(1)}%
                        </div>
                    </div>
                </div>
            )}

            {/* Event Type Statistics */}
            {eventTypeStats.length > 0 && (
                <div style={{ marginBottom: "2rem" }}>
                    <h3 className="text-lg font-semibold" style={{ marginBottom: "1rem" }}>
                        ?? Event Type Statistics
                    </h3>
                    <div style={{ overflowX: "auto" }}>
                        <table style={{ 
                            width: "100%", 
                            borderCollapse: "collapse",
                            fontSize: "0.875rem"
                        }}>
                            <thead>
                                <tr style={{ background: "#f3f4f6", borderBottom: "2px solid #e5e7eb" }}>
                                    <th style={{ padding: "12px", textAlign: "left", fontWeight: 600 }}>
                                        Event Type
                                    </th>
                                    <th style={{ padding: "12px", textAlign: "right", fontWeight: 600 }}>
                                        Total
                                    </th>
                                    <th style={{ padding: "12px", textAlign: "right", fontWeight: 600 }}>
                                        Processed
                                    </th>
                                    <th style={{ padding: "12px", textAlign: "right", fontWeight: 600 }}>
                                        Pending
                                    </th>
                                    <th style={{ padding: "12px", textAlign: "right", fontWeight: 600 }}>
                                        Failed
                                    </th>
                                </tr>
                            </thead>
                            <tbody>
                                {eventTypeStats.map((stat) => (
                                    <tr key={stat.eventType} style={{ borderBottom: "1px solid #e5e7eb" }}>
                                        <td style={{ padding: "12px", fontWeight: 500 }}>
                                            {stat.eventType}
                                        </td>
                                        <td style={{ padding: "12px", textAlign: "right", fontWeight: 600 }}>
                                            {stat.total}
                                        </td>
                                        <td style={{ padding: "12px", textAlign: "right", color: "#059669" }}>
                                            {stat.processed}
                                        </td>
                                        <td style={{ padding: "12px", textAlign: "right", color: "#f59e0b" }}>
                                            {stat.pending}
                                        </td>
                                        <td style={{ padding: "12px", textAlign: "right", color: stat.failed > 0 ? "#dc2626" : "#6b7280" }}>
                                            {stat.failed}
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                </div>
            )}

            {loading && <p style={{ textAlign: "center", padding: "2rem" }}>U?itavanje...</p>}
            {error && <p className="error-msg">{error}</p>}

            {/* Recent Messages */}
            {!loading && !error && recentMessages.length > 0 && (
                <>
                    <h3 className="text-lg font-semibold" style={{ marginBottom: "1rem" }}>
                        ?? Poslednje poruke
                    </h3>

                    <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
                        {recentMessages.map((message) => (
                            <div
                                key={message.id}
                                style={{
                                    background: message.isProcessed ? "#f0fdf4" : message.retryCount >= 5 ? "#fef2f2" : "#fef3c7",
                                    border: `2px solid ${message.isProcessed ? "#059669" : message.retryCount >= 5 ? "#dc2626" : "#f59e0b"}`,
                                    borderRadius: "12px",
                                    padding: "1rem"
                                }}
                            >
                                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: "0.75rem" }}>
                                    <div>
                                        <div style={{ fontWeight: 700, fontSize: "1.125rem", marginBottom: "0.25rem" }}>
                                            {message.eventType}
                                        </div>
                                        <div style={{ fontSize: "0.875rem", color: "#6b7280", fontFamily: "monospace" }}>
                                            ID: {message.id} | CorrelationId: {message.correlationId}
                                        </div>
                                    </div>

                                    <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
                                        {message.isProcessed ? (
                                            <span style={{
                                                padding: "4px 12px",
                                                borderRadius: "6px",
                                                background: "#f0fdf4",
                                                color: "#059669",
                                                fontSize: "0.75rem",
                                                fontWeight: 600
                                            }}>
                                                ? Processed
                                            </span>
                                        ) : message.retryCount >= 5 ? (
                                            <>
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
                                                <button
                                                    onClick={() => handleRetry(message.id)}
                                                    style={{
                                                        background: "#dc2626",
                                                        color: "white",
                                                        border: "none",
                                                        borderRadius: "6px",
                                                        padding: "4px 12px",
                                                        fontSize: "0.75rem",
                                                        fontWeight: 600,
                                                        cursor: "pointer"
                                                    }}
                                                >
                                                    ?? Retry
                                                </button>
                                            </>
                                        ) : (
                                            <span style={{
                                                padding: "4px 12px",
                                                borderRadius: "6px",
                                                background: "#fef3c7",
                                                color: "#f59e0b",
                                                fontSize: "0.75rem",
                                                fontWeight: 600
                                            }}>
                                                ? Pending (Retry: {message.retryCount})
                                            </span>
                                        )}
                                    </div>
                                </div>

                                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem", marginBottom: "0.75rem" }}>
                                    <div>
                                        <div style={{ fontSize: "0.75rem", color: "#6b7280", marginBottom: "0.25rem" }}>
                                            Created At
                                        </div>
                                        <div style={{ fontFamily: "monospace", fontSize: "0.875rem" }}>
                                            {formatDate(message.createdAt)}
                                        </div>
                                    </div>
                                    {message.processedAt && (
                                        <div>
                                            <div style={{ fontSize: "0.75rem", color: "#6b7280", marginBottom: "0.25rem" }}>
                                                Processed At
                                            </div>
                                            <div style={{ fontFamily: "monospace", fontSize: "0.875rem" }}>
                                                {formatDate(message.processedAt)}
                                            </div>
                                        </div>
                                    )}
                                </div>

                                {message.errorMessage && (
                                    <div style={{ 
                                        background: "#fef2f2", 
                                        padding: "0.75rem", 
                                        borderRadius: "8px", 
                                        marginBottom: "0.75rem" 
                                    }}>
                                        <div style={{ fontSize: "0.75rem", color: "#dc2626", fontWeight: 600, marginBottom: "0.25rem" }}>
                                            Error:
                                        </div>
                                        <div style={{ fontSize: "0.875rem", color: "#dc2626", fontFamily: "monospace" }}>
                                            {message.errorMessage}
                                        </div>
                                    </div>
                                )}

                                <details>
                                    <summary style={{ 
                                        cursor: "pointer", 
                                        fontSize: "0.875rem", 
                                        fontWeight: 600, 
                                        marginBottom: "0.5rem" 
                                    }}>
                                        ?? Payload
                                    </summary>
                                    <pre style={{
                                        background: "#f9fafb",
                                        padding: "1rem",
                                        borderRadius: "8px",
                                        fontSize: "0.75rem",
                                        overflow: "auto",
                                        maxHeight: "300px"
                                    }}>
                                        {formatPayload(message.payload)}
                                    </pre>
                                </details>
                            </div>
                        ))}
                    </div>
                </>
            )}

            {!loading && !error && recentMessages.length === 0 && (
                <p style={{ textAlign: "center", padding: "2rem", color: "#6b7280" }}>
                    Nema outbox poruka. ?
                </p>
            )}
        </div>
    );
}

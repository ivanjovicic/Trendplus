import React, { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import {
    getOutboxStats,
    retryOutboxMessage,
    retryAllFailedMessages,
    purgeProcessedMessages,
    getEventTypeStats,
} from "../services/outboxApi";
import { OutboxStats, OutboxMessage } from "../types/outbox";
import { useToast } from "../components/Toast";
import ConfirmModal from "../components/ConfirmModal";
import PromptNumberModal from "../components/PromptNumberModal";
import { usePingControl } from "../context/PingControlContext";

interface EventTypeStat {
    eventType: string;
    total: number;
    processed: number;
    pending: number;
    failed: number;
}

export default function OutboxDashboard() {
    const { apiPingEnabled } = usePingControl();
    const toast = useToast();

    const [stats, setStats] = useState<OutboxStats | null>(null);
    const [recentMessages, setRecentMessages] = useState<OutboxMessage[]>([]);
    const [eventTypeStats, setEventTypeStats] = useState<EventTypeStat[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [autoRefresh, setAutoRefresh] = useState(true);

    const [confirmRetryId, setConfirmRetryId] = useState<number | null>(null);
    const [confirmRetryAll, setConfirmRetryAll] = useState(false);
    const [purgeModalOpen, setPurgeModalOpen] = useState(false);
    const [purgeDays, setPurgeDays] = useState(7);
    const [actionBusy, setActionBusy] = useState(false);

    const fetchStats = async () => {
        try {
            const [statsResult, eventStatsResult] = await Promise.all([
                getOutboxStats(),
                getEventTypeStats(),
            ]);

            setStats(statsResult.stats);
            setRecentMessages(statsResult.recentMessages);
            setEventTypeStats(eventStatsResult);
            setError(null);
        } catch (err) {
            console.error("Error fetching outbox stats:", err);
            setError((err as Error)?.message ?? "Greška pri učitavanju outbox statistike");
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchStats();
    }, []);

    useEffect(() => {
        if (!autoRefresh || !apiPingEnabled) return;

        const interval = setInterval(fetchStats, 10000);
        return () => clearInterval(interval);
    }, [autoRefresh, apiPingEnabled]);

    const doRetry = async (id: number) => {
        setActionBusy(true);
        try {
            await retryOutboxMessage(id);
            toast.success("Poruka je označena za ponovno slanje!");
            await fetchStats();
        } catch (err) {
            toast.error(`Greška: ${(err as Error).message}`);
        } finally {
            setActionBusy(false);
            setConfirmRetryId(null);
        }
    };

    const doRetryAllFailed = async () => {
        setActionBusy(true);
        try {
            const result = await retryAllFailedMessages();
            toast.success(`${result.count} poruka je ozna?eno za ponovno slanje!`);
            await fetchStats();
        } catch (err) {
            toast.error(`Greška: ${(err as Error).message}`);
        } finally {
            setActionBusy(false);
            setConfirmRetryAll(false);
        }
    };

    const doPurgeProcessed = async (days: number) => {
        setActionBusy(true);
        try {
            const result = await purgeProcessedMessages(Number(days));
            toast.success(`${result.count} poruka je obrisano!`);
            await fetchStats();
            setPurgeDays(days);
        } catch (err) {
            toast.error(`Greška: ${(err as Error).message}`);
        } finally {
            setActionBusy(false);
            setPurgeModalOpen(false);
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
            <div
                className="toolbar"
                style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    marginBottom: "1.5rem",
                    gap: "1rem",
                    flexWrap: "wrap",
                }}
            >
                <h2 className="text-2xl font-semibold" style={{ margin: 0 }}>
                    Outbox nadzor
                </h2>

                <div style={{ display: "flex", gap: "1rem", alignItems: "center", flexWrap: "wrap" }}>
                    <Link
                        to="/outbox/messages"
                        className="button-big"
                        style={{
                            background: "linear-gradient(135deg, #8b5cf6 0%, #6d28d9 100%)",
                            padding: "8px 16px",
                            marginBottom: 0,
                            textDecoration: "none",
                        }}
                    >
                        Sve poruke
                    </Link>

                    <label style={{ display: "flex", alignItems: "center", gap: "0.5rem", fontSize: "0.875rem" }}>
                        <input
                            type="checkbox"
                            checked={autoRefresh}
                            onChange={(e) => setAutoRefresh(e.target.checked)}
                            disabled={!apiPingEnabled}
                        />
                        Auto-refresh (10s){!apiPingEnabled ? " - pauziran globalno" : ""}
                    </label>

                    <button
                        className="button-big button-secondary"
                        type="button"
                        onClick={fetchStats}
                        style={{ padding: "8px 16px", marginBottom: 0 }}
                    >
                        Osveži
                    </button>
                </div>
            </div>

            {/* Bulk Operations */}
            {stats && stats.failed > 0 && (
                <div
                    className="toolbar"
                    style={{
                        display: "flex",
                        gap: "1rem",
                        marginBottom: "1.5rem",
                        background: "#fef2f2",
                        border: "2px solid #fecaca",
                        flexWrap: "wrap",
                    }}
                >
                    <button
                        className="button-big button-danger"
                        type="button"
                        onClick={() => setConfirmRetryAll(true)}
                        style={{ padding: "8px 16px", marginBottom: 0 }}
                    >
                        Ponovi sve neuspele ({stats.failed})
                    </button>

                    <button
                        className="button-big button-secondary"
                        type="button"
                        onClick={() => setPurgeModalOpen(true)}
                        style={{ padding: "8px 16px", marginBottom: 0 }}
                    >
                        Obriši obrađene
                    </button>
                </div>
            )}

            {/* Summary Cards */}
            {stats && (
                <div
                    style={{
                        display: "grid",
                        gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))",
                        gap: "1rem",
                        marginBottom: "2rem",
                    }}
                >
                    <div style={{ background: "#eff6ff", padding: "1.5rem", borderRadius: "12px", border: "2px solid #2563eb" }}>
                        <div style={{ fontSize: "0.875rem", color: "#6b7280", marginBottom: "0.5rem" }}>Total Messages</div>
                        <div style={{ fontSize: "2rem", fontWeight: 800, color: "#2563eb" }}>{stats.total}</div>
                    </div>

                    <div style={{ background: "#f0fdf4", padding: "1.5rem", borderRadius: "12px", border: "2px solid #059669" }}>
                        <div style={{ fontSize: "0.875rem", color: "#6b7280", marginBottom: "0.5rem" }}>Processed</div>
                        <div style={{ fontSize: "2rem", fontWeight: 800, color: "#059669" }}>{stats.processed}</div>
                    </div>

                    <div style={{ background: "#fef3c7", padding: "1.5rem", borderRadius: "12px", border: "2px solid #f59e0b" }}>
                        <div style={{ fontSize: "0.875rem", color: "#6b7280", marginBottom: "0.5rem" }}>Pending</div>
                        <div style={{ fontSize: "2rem", fontWeight: 800, color: "#f59e0b" }}>{stats.pending}</div>
                    </div>

                    <div
                        style={{
                            background: stats.failed > 0 ? "#fef2f2" : "#f3f4f6",
                            padding: "1.5rem",
                            borderRadius: "12px",
                            border: `2px solid ${stats.failed > 0 ? "#dc2626" : "#6b7280"}`,
                        }}
                    >
                        <div style={{ fontSize: "0.875rem", color: "#6b7280", marginBottom: "0.5rem" }}>Failed (Retries ? 5)</div>
                        <div style={{ fontSize: "2rem", fontWeight: 800, color: stats.failed > 0 ? "#dc2626" : "#6b7280" }}>
                            {stats.failed}
                        </div>
                    </div>

                    <div style={{ background: "#ecfdf5", padding: "1.5rem", borderRadius: "12px", border: "2px solid #10b981" }}>
                        <div style={{ fontSize: "0.875rem", color: "#6b7280", marginBottom: "0.5rem" }}>Success Rate</div>
                        <div style={{ fontSize: "2rem", fontWeight: 800, color: "#10b981" }}>{stats.successRate.toFixed(1)}%</div>
                    </div>
                </div>
            )}

            {/* Event Type Statistics */}
            {eventTypeStats.length > 0 && (
                <div style={{ marginBottom: "2rem" }}>
                    <h3 className="text-lg font-semibold" style={{ marginBottom: "1rem" }}>
                        Statistika po tipu događaja
                    </h3>
                    <div style={{ overflowX: "auto" }}>
                        <table className="table">
                            <thead>
                                <tr>
                                    <th>Event Type</th>
                                    <th style={{ textAlign: "right" }}>Total</th>
                                    <th style={{ textAlign: "right" }}>Processed</th>
                                    <th style={{ textAlign: "right" }}>Pending</th>
                                    <th style={{ textAlign: "right" }}>Failed</th>
                                </tr>
                            </thead>
                            <tbody>
                                {eventTypeStats.map((stat) => (
                                    <tr key={stat.eventType}>
                                        <td style={{ fontWeight: 700 }}>{stat.eventType}</td>
                                        <td style={{ textAlign: "right", fontWeight: 800 }}>{stat.total}</td>
                                        <td style={{ textAlign: "right", color: "#059669", fontWeight: 800 }}>{stat.processed}</td>
                                        <td style={{ textAlign: "right", color: "#f59e0b", fontWeight: 800 }}>{stat.pending}</td>
                                        <td style={{ textAlign: "right", color: stat.failed > 0 ? "#dc2626" : "#6b7280", fontWeight: 800 }}>
                                            {stat.failed}
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                </div>
            )}

            {loading && <p style={{ textAlign: "center", padding: "2rem" }}>Učitavanje...</p>}
            {error && <p className="error-msg">{error}</p>}

            {/* Recent Messages */}
            {!loading && !error && recentMessages.length > 0 && (
                <>
                    <h3 className="text-lg font-semibold" style={{ marginBottom: "1rem" }}>
                        Poslednje poruke
                    </h3>

                    <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
                        {recentMessages.map((message) => (
                            <div
                                key={message.id}
                                className="toolbar"
                                style={{
                                    background: message.isProcessed ? "#f0fdf4" : message.retryCount >= 5 ? "#fef2f2" : "#fef3c7",
                                    border: `2px solid ${
                                        message.isProcessed ? "#059669" : message.retryCount >= 5 ? "#dc2626" : "#f59e0b"
                                    }`,
                                }}
                            >
                                <div
                                    style={{
                                        display: "flex",
                                        justifyContent: "space-between",
                                        alignItems: "flex-start",
                                        marginBottom: "0.75rem",
                                        gap: "1rem",
                                        flexWrap: "wrap",
                                    }}
                                >
                                    <div>
                                        <div style={{ fontWeight: 900, fontSize: "1.125rem", marginBottom: "0.25rem" }}>
                                            {message.eventType}
                                        </div>
                                        <div style={{ fontSize: "0.875rem", color: "#6b7280", fontFamily: "monospace" }}>
                                            ID: {message.id} | CorrelationId: {message.correlationId}
                                        </div>
                                    </div>

                                    <div style={{ display: "flex", gap: "0.5rem", alignItems: "center", flexWrap: "wrap" }}>
                                        {message.isProcessed ? (
                                            <span
                                                style={{
                                                    padding: "4px 12px",
                                                    borderRadius: "8px",
                                                    background: "#f0fdf4",
                                                    color: "#059669",
                                                    fontSize: "0.75rem",
                                                    fontWeight: 800,
                                                    border: "1px solid #a7f3d0",
                                                }}
                                            >
                                                Processed
                                            </span>
                                        ) : message.retryCount >= 5 ? (
                                            <>
                                                <span
                                                    style={{
                                                        padding: "4px 12px",
                                                        borderRadius: "8px",
                                                        background: "#fef2f2",
                                                        color: "#dc2626",
                                                        fontSize: "0.75rem",
                                                        fontWeight: 800,
                                                        border: "1px solid #fecaca",
                                                    }}
                                                >
                                                    Failed
                                                </span>
                                                <button
                                                    className="button-big button-danger"
                                                    type="button"
                                                    onClick={() => setConfirmRetryId(message.id)}
                                                    style={{
                                                        width: "auto",
                                                        padding: "6px 12px",
                                                        marginBottom: 0,
                                                        fontSize: "0.8rem",
                                                        boxShadow: "none",
                                                    }}
                                                >
                                                    Retry
                                                </button>
                                            </>
                                        ) : (
                                            <span
                                                style={{
                                                    padding: "4px 12px",
                                                    borderRadius: "8px",
                                                    background: "#fef3c7",
                                                    color: "#92400e",
                                                    fontSize: "0.75rem",
                                                    fontWeight: 800,
                                                    border: "1px solid #fde68a",
                                                }}
                                            >
                                                Pending (Retry: {message.retryCount})
                                            </span>
                                        )}
                                    </div>
                                </div>

                                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem", marginBottom: "0.75rem" }}>
                                    <div>
                                        <div style={{ fontSize: "0.75rem", color: "#6b7280", marginBottom: "0.25rem" }}>Created At</div>
                                        <div style={{ fontFamily: "monospace", fontSize: "0.875rem" }}>{formatDate(message.createdAt)}</div>
                                    </div>
                                    {message.processedAt && (
                                        <div>
                                            <div style={{ fontSize: "0.75rem", color: "#6b7280", marginBottom: "0.25rem" }}>Processed At</div>
                                            <div style={{ fontFamily: "monospace", fontSize: "0.875rem" }}>{formatDate(message.processedAt)}</div>
                                        </div>
                                    )}
                                </div>

                                {message.errorMessage && (
                                    <div className="error-msg" style={{ marginBottom: "0.75rem" }}>
                                        <div style={{ fontSize: "0.75rem", fontWeight: 800, marginBottom: "0.25rem" }}>Error:</div>
                                        <div style={{ fontSize: "0.875rem", fontFamily: "monospace" }}>{message.errorMessage}</div>
                                    </div>
                                )}

                                <details>
                                    <summary style={{ cursor: "pointer", fontSize: "0.875rem", fontWeight: 800, marginBottom: "0.5rem" }}>
                                    Payload
                                    </summary>
                                    <pre
                                        style={{
                                            background: "#f9fafb",
                                            padding: "1rem",
                                            borderRadius: "12px",
                                            fontSize: "0.75rem",
                                            overflow: "auto",
                                            maxHeight: "300px",
                                            border: "1px solid #e5e7eb",
                                        }}
                                    >
                                        {formatPayload(message.payload)}
                                    </pre>
                                </details>
                            </div>
                        ))}
                    </div>
                </>
            )}

            {!loading && !error && recentMessages.length === 0 && (
                <p style={{ textAlign: "center", padding: "2rem", color: "#6b7280" }}>Nema outbox poruka.</p>
            )}

            <ConfirmModal
                isOpen={confirmRetryId != null}
                title="Ponovno slanje"
                message={
                    <>
                        Da li želite da pokušate ponovo da pošaljete poruku <strong>#{confirmRetryId}</strong>?
                    </>
                }
                confirmText="Retry"
                confirmVariant="danger"
                isBusy={actionBusy}
                onCancel={() => setConfirmRetryId(null)}
                onConfirm={() => confirmRetryId != null && doRetry(confirmRetryId)}
            />

            <ConfirmModal
                isOpen={confirmRetryAll}
                title="Retry svih neuspelih poruka"
                message={
                    <>
                        Da li želite da pokušate ponovo da pošaljete <strong>SVE</strong> neuspele poruke?
                    </>
                }
                confirmText="Retry All"
                confirmVariant="danger"
                isBusy={actionBusy}
                onCancel={() => setConfirmRetryAll(false)}
                onConfirm={doRetryAllFailed}
            />

            <PromptNumberModal
                isOpen={purgeModalOpen}
                title="Purge processed poruka"
                label="Obriši obrađene poruke starije od (dana)"
                description={
                    <>
                        Biće obrisane samo poruke koje su označene kao <strong>Processed</strong> i starije od izabranog broja dana.
                    </>
                }
                defaultValue={purgeDays}
                min={1}
                max={365}
                confirmText="Obriši"
                cancelText="Otkaži"
                isBusy={actionBusy}
                onCancel={() => setPurgeModalOpen(false)}
                onConfirm={doPurgeProcessed}
            />
        </div>
    );
}

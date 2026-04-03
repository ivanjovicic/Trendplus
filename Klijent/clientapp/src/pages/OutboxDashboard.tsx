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
            const [statsResult, eventStatsResult] = await Promise.allSettled([
                getOutboxStats(),
                getEventTypeStats(),
            ]);

            if (statsResult.status !== "fulfilled") {
                throw statsResult.reason;
            }

            setStats(statsResult.value.stats);
            setRecentMessages(statsResult.value.recentMessages);

            if (eventStatsResult.status === "fulfilled") {
                setEventTypeStats(eventStatsResult.value);
            } else {
                setEventTypeStats([]);
            }

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

    const badgeStyle = (status: "processed" | "pending" | "failed") => {
        switch (status) {
            case "processed":
                return { background: "var(--success-soft)", borderColor: "var(--success)", color: "var(--success)" };
            case "failed":
                return { background: "var(--error-soft)", borderColor: "var(--error)", color: "var(--error)" };
            default:
                return { background: "var(--warning-soft)", borderColor: "var(--warning)", color: "var(--warning)" };
        }
    };

    const rowStyle = (message: OutboxMessage) => {
        if (message.isProcessed) {
            return { background: "var(--success-soft)", borderColor: "var(--success)" };
        }
        if (message.retryCount >= 5) {
            return { background: "var(--error-soft)", borderColor: "var(--error)" };
        }
        return { background: "var(--warning-soft)", borderColor: "var(--warning)" };
    };

    return (
        <div className="card" style={{ maxWidth: "1400px" }}>
            <div className="toolbar flex flex-wrap gap-4 items-center justify-between mb-6">
                <h2 className="text-2xl font-semibold" style={{ margin: 0 }}>
                    Outbox nadzor
                </h2>

                <div className="flex flex-wrap gap-4 items-center">
                    <Link
                        to="/outbox/messages"
                        className="button-big"
                        style={{
                            background: "linear-gradient(135deg, var(--info), var(--focus-ring))",
                            padding: "8px 16px",
                            marginBottom: 0,
                            textDecoration: "none",
                        }}
                    >
                        Sve poruke
                    </Link>

                    <label className="flex items-center gap-2 text-sm">
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
                <div className="toolbar flex flex-wrap gap-4 mb-6 rounded-2xl border border-warning bg-surface-light p-4">
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
                <div className="grid gap-4 mb-8" style={{ gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))" }}>
                    <div className="rounded-2xl border border-muted bg-surface px-6 py-5">
                        <div className="text-xs text-muted mb-2">Total Messages</div>
                        <div className="text-3xl font-extrabold text-contrast">{stats.total}</div>
                    </div>

                    <div className="rounded-2xl border border-muted bg-surface px-6 py-5">
                        <div className="text-xs text-muted mb-2">Processed</div>
                        <div className="text-3xl font-extrabold text-success">{stats.processed}</div>
                    </div>

                    <div className="rounded-2xl border border-muted bg-surface px-6 py-5">
                        <div className="text-xs text-muted mb-2">Pending</div>
                        <div className="text-3xl font-extrabold text-warning">{stats.pending}</div>
                    </div>

                    <div
                        className="rounded-2xl bg-surface px-6 py-5"
                        style={{ borderColor: stats.failed > 0 ? "var(--error)" : "var(--border-hover)", borderWidth: 2, borderStyle: "solid" }}
                    >
                        <div className="text-xs mb-2" style={{ color: stats.failed > 0 ? "var(--error)" : "var(--text-muted)" }}>
                            Failed (Retries ≥ 5)
                        </div>
                        <div className="text-3xl font-extrabold" style={{ color: stats.failed > 0 ? "var(--error)" : "var(--text-secondary)" }}>
                            {stats.failed}
                        </div>
                    </div>

                    <div className="rounded-2xl border border-muted bg-surface px-6 py-5">
                        <div className="text-xs text-muted mb-2">Success Rate</div>
                        <div className="text-3xl font-extrabold text-success">{stats.successRate.toFixed(1)}%</div>
                    </div>
                </div>
            )}

            {/* Event Type Statistics */}
            {eventTypeStats.length > 0 && (
                    <div style={{ marginBottom: "2rem" }}>
                        <h3 className="text-lg font-semibold text-foreground mb-3">Statistika po tipu događaja</h3>
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
                                            <td style={{ textAlign: "right", color: "var(--success)", fontWeight: 800 }}>{stat.processed}</td>
                                            <td style={{ textAlign: "right", color: "var(--warning)", fontWeight: 800 }}>{stat.pending}</td>
                                            <td style={{ textAlign: "right", color: stat.failed > 0 ? "var(--error)" : "var(--text-secondary)", fontWeight: 800 }}>
                                                {stat.failed}
                                            </td>
                                        </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                </div>
            )}

            {loading && <p className="text-center py-8 text-secondary">Učitavanje...</p>}
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
                                className="toolbar rounded-2xl border-2 mb-2"
                                style={rowStyle(message)}
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
                                        <div style={{ fontSize: "0.875rem", color: "var(--text-muted)", fontFamily: "monospace" }}>
                                            ID: {message.id} | CorrelationId: {message.correlationId}
                                        </div>
                                    </div>

                                    <div style={{ display: "flex", gap: "0.5rem", alignItems: "center", flexWrap: "wrap" }}>
                                        {message.isProcessed ? (
                                            <span
                                                style={{
                                                    padding: "4px 12px",
                                                    borderRadius: "8px",
                                                    fontSize: "0.75rem",
                                                    fontWeight: 800,
                                                    ...badgeStyle("processed"),
                                                    border: `1px solid ${badgeStyle("processed").borderColor}`,
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
                                                        fontSize: "0.75rem",
                                                        fontWeight: 800,
                                                        ...badgeStyle("failed"),
                                                        border: `1px solid ${badgeStyle("failed").borderColor}`,
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
                                                    fontSize: "0.75rem",
                                                    fontWeight: 800,
                                                    ...badgeStyle("pending"),
                                                    border: `1px solid ${badgeStyle("pending").borderColor}`,
                                                }}
                                            >
                                                Pending (Retry: {message.retryCount})
                                            </span>
                                        )}
                                    </div>
                                </div>

                                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem", marginBottom: "0.75rem" }}>
                                    <div>
                                        <div style={{ fontSize: "0.75rem", color: "var(--text-muted)", marginBottom: "0.25rem" }}>Created At</div>
                                        <div style={{ fontFamily: "monospace", fontSize: "0.875rem" }}>{formatDate(message.createdAt)}</div>
                                    </div>
                                    {message.processedAt && (
                                        <div>
                                            <div style={{ fontSize: "0.75rem", color: "var(--text-muted)", marginBottom: "0.25rem" }}>Processed At</div>
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
                                    <summary className="cursor-pointer font-semibold text-sm mb-2">
                                    Payload
                                    </summary>
                                    <pre
                                        className="rounded-2xl border border-muted bg-surface-light overflow-auto"
                                        style={{ padding: "1rem", fontSize: "0.75rem", maxHeight: "300px" }}
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
                <p className="text-center py-8 text-secondary">Nema outbox poruka.</p>
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

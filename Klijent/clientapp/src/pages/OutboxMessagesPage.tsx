import React, { useState, useEffect } from "react";
import { getOutboxMessages, retryOutboxMessage } from "../services/outboxApi";
import { OutboxMessage } from "../types/outbox";
import { useToast } from "../components/Toast";
import ConfirmModal from "../components/ConfirmModal";

export default function OutboxMessagesPage() {
    const toast = useToast();

    const [messages, setMessages] = useState<OutboxMessage[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    // Pagination
    const [pageNumber, setPageNumber] = useState(1);
    const [pageSize] = useState(20);
    const [totalCount, setTotalCount] = useState(0);

    // Filters
    const [isProcessed, setIsProcessed] = useState<boolean | undefined>(undefined);
    const [eventType, setEventType] = useState("");
    const [fromDate, setFromDate] = useState("");
    const [toDate, setToDate] = useState("");

    const [confirmRetryId, setConfirmRetryId] = useState<number | null>(null);
    const [actionBusy, setActionBusy] = useState(false);

    const fetchMessages = async () => {
        setLoading(true);
        setError(null);

        try {
            const result = await getOutboxMessages(
                pageNumber,
                pageSize,
                isProcessed,
                eventType || undefined,
                fromDate || undefined,
                toDate || undefined
            );

            setMessages(result.messages);
            setTotalCount(result.totalCount);
        } catch (err) {
            console.error("Error fetching outbox messages:", err);
            setError((err as Error)?.message ?? "Greška pri učitavanju poruka");
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchMessages();
    }, [pageNumber, isProcessed, eventType, fromDate, toDate]);

    const doRetry = async (id: number) => {
        setActionBusy(true);
        try {
            await retryOutboxMessage(id);
            toast.success("Poruka je označena za ponovno slanje!");
            await fetchMessages();
        } catch (err) {
            toast.error(`Greška: ${(err as Error).message}`);
        } finally {
            setActionBusy(false);
            setConfirmRetryId(null);
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

    const totalPages = Math.ceil(totalCount / pageSize);

    return (
        <div className="card" style={{ maxWidth: "1400px" }}>
            <h2 className="text-2xl font-semibold mb-6">Outbox poruke</h2>

            {/* Filters */}
            <div
                className="toolbar"
                style={{
                    display: "grid",
                    gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))",
                    gap: "1rem",
                    marginBottom: "1.5rem",
                    background: "#f9fafb",
                }}
            >
                <div>
                    <label className="field-label" style={{ fontSize: "0.875rem" }}>
                        Status
                    </label>
                    <select
                        className="input-big"
                        value={isProcessed === undefined ? "all" : isProcessed ? "processed" : "pending"}
                        onChange={(e) => {
                            if (e.target.value === "all") setIsProcessed(undefined);
                            else setIsProcessed(e.target.value === "processed");
                            setPageNumber(1);
                        }}
                        style={{ marginTop: "0.25rem", marginBottom: 0, fontSize: "0.95rem", padding: "8px 12px" }}
                    >
                        <option value="all">Sve</option>
                        <option value="processed">Processed</option>
                        <option value="pending">Pending/Failed</option>
                    </select>
                </div>

                <div>
                    <label className="field-label" style={{ fontSize: "0.875rem" }}>
                        Event Type
                    </label>
                    <input
                        type="text"
                        className="input-big"
                        placeholder="ProdajaKreirana..."
                        value={eventType}
                        onChange={(e) => {
                            setEventType(e.target.value);
                            setPageNumber(1);
                        }}
                        style={{ marginTop: "0.25rem", marginBottom: 0, fontSize: "0.95rem", padding: "8px 12px" }}
                    />
                </div>

                <div>
                    <label className="field-label" style={{ fontSize: "0.875rem" }}>
                        Od datuma
                    </label>
                    <input
                        type="datetime-local"
                        className="input-big"
                        value={fromDate}
                        onChange={(e) => {
                            setFromDate(e.target.value);
                            setPageNumber(1);
                        }}
                        style={{ marginTop: "0.25rem", marginBottom: 0, fontSize: "0.95rem", padding: "8px 12px" }}
                    />
                </div>

                <div>
                    <label className="field-label" style={{ fontSize: "0.875rem" }}>
                        Do datuma
                    </label>
                    <input
                        type="datetime-local"
                        className="input-big"
                        value={toDate}
                        onChange={(e) => {
                            setToDate(e.target.value);
                            setPageNumber(1);
                        }}
                        style={{ marginTop: "0.25rem", marginBottom: 0, fontSize: "0.95rem", padding: "8px 12px" }}
                    />
                </div>

                <div style={{ display: "flex", alignItems: "flex-end" }}>
                    <button
                        className="button-big button-secondary"
                        type="button"
                        onClick={() => {
                            setIsProcessed(undefined);
                            setEventType("");
                            setFromDate("");
                            setToDate("");
                            setPageNumber(1);
                        }}
                        style={{ padding: "8px 16px", marginTop: 0, marginBottom: 0, fontSize: "0.95rem" }}
                    >
                        Reset
                    </button>
                </div>
            </div>

            {/* Summary */}
            <div className="toolbar" style={{ background: "#eff6ff", border: "1px solid #bfdbfe", color: "#1e40af", marginBottom: "1.5rem" }}>
                Prikazano <strong>{messages.length}</strong> od ukupno <strong>{totalCount}</strong> poruka
            </div>

            {loading && <p style={{ textAlign: "center", padding: "2rem" }}>Učitavanje...</p>}
            {error && <p className="error-msg">{error}</p>}

            {/* Messages */}
            {!loading && !error && messages.length > 0 && (
                <div style={{ display: "flex", flexDirection: "column", gap: "1rem", marginBottom: "1.5rem" }}>
                    {messages.map((message) => (
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
                            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: "0.75rem", gap: "1rem", flexWrap: "wrap" }}>
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
                                        <span style={{
                                            padding: "4px 12px",
                                            borderRadius: "8px",
                                            background: "#f0fdf4",
                                            color: "#059669",
                                            fontSize: "0.75rem",
                                            fontWeight: 800,
                                            border: "1px solid #a7f3d0"
                                        }}>
                                            Processed
                                        </span>
                                    ) : message.retryCount >= 5 ? (
                                        <>
                                            <span style={{
                                                padding: "4px 12px",
                                                borderRadius: "8px",
                                                background: "#fef2f2",
                                                color: "#dc2626",
                                                fontSize: "0.75rem",
                                                fontWeight: 800,
                                                border: "1px solid #fecaca"
                                            }}>
                                                Failed
                                            </span>
                                            <button
                                                className="button-big button-danger"
                                                type="button"
                                                onClick={() => setConfirmRetryId(message.id)}
                                                style={{ width: "auto", padding: "6px 12px", fontSize: "0.8rem", marginBottom: 0, boxShadow: "none" }}
                                            >
                                                Retry
                                            </button>
                                        </>
                                    ) : (
                                        <span style={{
                                            padding: "4px 12px",
                                            borderRadius: "8px",
                                            background: "#fef3c7",
                                            color: "#92400e",
                                            fontSize: "0.75rem",
                                            fontWeight: 800,
                                            border: "1px solid #fde68a"
                                        }}>
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
                                <pre style={{
                                    background: "#f9fafb",
                                    padding: "1rem",
                                    borderRadius: "12px",
                                    fontSize: "0.75rem",
                                    overflow: "auto",
                                    maxHeight: "300px",
                                    border: "1px solid #e5e7eb"
                                }}>
                                    {formatPayload(message.payload)}
                                </pre>
                            </details>
                        </div>
                    ))}
                </div>
            )}

            {!loading && !error && messages.length === 0 && (
                <p style={{ textAlign: "center", padding: "2rem", color: "#6b7280" }}>
                    Nema poruka koje odgovaraju filterima.
                </p>
            )}

            {/* Pagination */}
            {totalPages > 1 && (
                <div style={{ display: "flex", justifyContent: "center", alignItems: "center", gap: "0.5rem", marginTop: "2rem", flexWrap: "wrap" }}>
                    <button
                        type="button"
                        onClick={() => setPageNumber(Math.max(1, pageNumber - 1))}
                        disabled={pageNumber === 1}
                        className="button-big button-secondary"
                        style={{ padding: "8px 16px", marginBottom: 0, width: "auto" }}
                    >
                        ← Prethodna
                    </button>

                    <span style={{ fontSize: "0.875rem", color: "#6b7280", fontWeight: 800 }}>
                        Page {pageNumber} of {totalPages}
                    </span>

                    <button
                        type="button"
                        onClick={() => setPageNumber(Math.min(totalPages, pageNumber + 1))}
                        disabled={pageNumber === totalPages}
                        className="button-big button-secondary"
                        style={{ padding: "8px 16px", marginBottom: 0, width: "auto" }}
                    >
                        Sledeća →
                    </button>
                </div>
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
        </div>
    );
}

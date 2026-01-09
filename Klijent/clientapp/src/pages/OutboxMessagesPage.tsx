import React, { useState, useEffect } from "react";
import { getOutboxMessages, retryOutboxMessage } from "../services/outboxApi";
import { OutboxMessage } from "../types/outbox";

export default function OutboxMessagesPage() {
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
        } catch (err: any) {
            console.error("Error fetching outbox messages:", err);
            setError(err?.message ?? "Greška pri u?itavanju poruka");
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchMessages();
    }, [pageNumber, isProcessed, eventType, fromDate, toDate]);

    const handleRetry = async (id: number) => {
        if (!confirm(`Da li želite da pokušate ponovo da pošaljete poruku ${id}?`)) {
            return;
        }

        try {
            await retryOutboxMessage(id);
            alert("Poruka je ozna?ena za ponovno slanje!");
            await fetchMessages();
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

    const totalPages = Math.ceil(totalCount / pageSize);

    return (
        <div className="card" style={{ maxWidth: "1400px" }}>
            <h2 className="text-2xl font-semibold mb-6">?? Outbox Messages</h2>

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
                    <label className="field-label" style={{ fontSize: "0.875rem" }}>Status</label>
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
                    <label className="field-label" style={{ fontSize: "0.875rem" }}>Event Type</label>
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
                    <label className="field-label" style={{ fontSize: "0.875rem" }}>Od datuma</label>
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
                    <label className="field-label" style={{ fontSize: "0.875rem" }}>Do datuma</label>
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
                        className="button-big"
                        onClick={() => {
                            setIsProcessed(undefined);
                            setEventType("");
                            setFromDate("");
                            setToDate("");
                            setPageNumber(1);
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

            {/* Summary */}
            <div style={{ 
                padding: "1rem", 
                background: "#eff6ff", 
                borderRadius: "8px", 
                marginBottom: "1.5rem",
                fontSize: "0.875rem",
                color: "#1e40af"
            }}>
                Prikazano <strong>{messages.length}</strong> od ukupno <strong>{totalCount}</strong> poruka
            </div>

            {loading && <p style={{ textAlign: "center", padding: "2rem" }}>U?itavanje...</p>}
            {error && <p className="error-msg">{error}</p>}

            {/* Messages */}
            {!loading && !error && messages.length > 0 && (
                <div style={{ display: "flex", flexDirection: "column", gap: "1rem", marginBottom: "1.5rem" }}>
                    {messages.map((message) => (
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
            )}

            {!loading && !error && messages.length === 0 && (
                <p style={{ textAlign: "center", padding: "2rem", color: "#6b7280" }}>
                    Nema poruka koje odgovaraju filterima. ?
                </p>
            )}

            {/* Pagination */}
            {totalPages > 1 && (
                <div style={{ 
                    display: "flex", 
                    justifyContent: "center", 
                    alignItems: "center", 
                    gap: "0.5rem",
                    marginTop: "2rem"
                }}>
                    <button
                        onClick={() => setPageNumber(Math.max(1, pageNumber - 1))}
                        disabled={pageNumber === 1}
                        className="button-big"
                        style={{
                            padding: "8px 16px",
                            marginBottom: 0,
                            background: pageNumber === 1 ? "#d1d5db" : "#3b82f6",
                            cursor: pageNumber === 1 ? "not-allowed" : "pointer"
                        }}
                    >
                        ? Previous
                    </button>

                    <span style={{ fontSize: "0.875rem", color: "#6b7280" }}>
                        Page {pageNumber} of {totalPages}
                    </span>

                    <button
                        onClick={() => setPageNumber(Math.min(totalPages, pageNumber + 1))}
                        disabled={pageNumber === totalPages}
                        className="button-big"
                        style={{
                            padding: "8px 16px",
                            marginBottom: 0,
                            background: pageNumber === totalPages ? "#d1d5db" : "#3b82f6",
                            cursor: pageNumber === totalPages ? "not-allowed" : "pointer"
                        }}
                    >
                        Next ?
                    </button>
                </div>
            )}
        </div>
    );
}

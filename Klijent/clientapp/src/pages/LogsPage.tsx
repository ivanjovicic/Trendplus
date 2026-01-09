import React, { useState, useEffect } from "react";
import { getLogs } from "../services/logsApi";
import { LogEntry } from "../types/logs";

type TimePeriod = "" | "30m" | "1h" | "6h" | "1d" | "2d" | "7d";

const timePeriodOptions: { value: TimePeriod; label: string }[] = [
    { value: "", label: "Prilago?eni period" },
    { value: "30m", label: "Poslednjih 30 minuta" },
    { value: "1h", label: "Poslednji sat" },
    { value: "6h", label: "Poslednjih 6 sati" },
    { value: "1d", label: "Poslednji dan" },
    { value: "2d", label: "Poslednja 2 dana" },
    { value: "7d", label: "Poslednjih 7 dana" },
];

function getDateRangeFromPeriod(period: TimePeriod): { from: string; to: string } {
    if (!period) return { from: "", to: "" };
    
    const now = new Date();
    const to = now.toISOString().slice(0, 16); // Format: YYYY-MM-DDTHH:mm
    
    let from: Date;
    switch (period) {
        case "30m":
            from = new Date(now.getTime() - 30 * 60 * 1000);
            break;
        case "1h":
            from = new Date(now.getTime() - 60 * 60 * 1000);
            break;
        case "6h":
            from = new Date(now.getTime() - 6 * 60 * 60 * 1000);
            break;
        case "1d":
            from = new Date(now.getTime() - 24 * 60 * 60 * 1000);
            break;
        case "2d":
            from = new Date(now.getTime() - 2 * 24 * 60 * 60 * 1000);
            break;
        case "7d":
            from = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
            break;
        default:
            return { from: "", to: "" };
    }
    
    return { from: from.toISOString().slice(0, 16), to };
}

export default function LogsPage() {
    const [logs, setLogs] = useState<LogEntry[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    
    // Filters
    const [selectedLevel, setSelectedLevel] = useState<string>("");
    const [selectedPeriod, setSelectedPeriod] = useState<TimePeriod>("");
    const [fromDate, setFromDate] = useState<string>("");
    const [toDate, setToDate] = useState<string>("");
    
    // Pagination
    const [currentPage, setCurrentPage] = useState(1);
    const [totalCount, setTotalCount] = useState(0);
    const [pageSize] = useState(50);

    const handlePeriodChange = (period: TimePeriod) => {
        setSelectedPeriod(period);
        const range = getDateRangeFromPeriod(period);
        setFromDate(range.from);
        setToDate(range.to);
        setCurrentPage(1);
    };

    const handleCustomDateChange = (type: "from" | "to", value: string) => {
        setSelectedPeriod(""); // Switch to custom period
        if (type === "from") {
            setFromDate(value);
        } else {
            setToDate(value);
        }
        setCurrentPage(1);
    };

    const fetchLogs = async () => {
        setLoading(true);
        setError(null);

        try {
            const result = await getLogs(
                currentPage,
                pageSize,
                selectedLevel || undefined,
                fromDate || undefined,
                toDate || undefined
            );

            setLogs(result.logs);
            setTotalCount(result.totalCount);
        } catch (err: any) {
            console.error("Error fetching logs:", err);
            setError(err?.message ?? "Greška pri u?itavanju logova");
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchLogs();
    }, [currentPage, selectedLevel, fromDate, toDate]);

    const getLevelColor = (level: string) => {
        switch (level.toUpperCase()) {
            case "ERROR":
            case "FATAL":
                return "#dc2626";
            case "WARNING":
                return "#f59e0b";
            case "INFORMATION":
            case "INFO":
                return "#2563eb";
            case "DEBUG":
                return "#6b7280";
            default:
                return "#374151";
        }
    };

    const getLevelBgColor = (level: string) => {
        switch (level.toUpperCase()) {
            case "ERROR":
            case "FATAL":
                return "#fef2f2";
            case "WARNING":
                return "#fffbeb";
            case "INFORMATION":
            case "INFO":
                return "#eff6ff";
            case "DEBUG":
                return "#f9fafb";
            default:
                return "#f9fafb";
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

    const totalPages = Math.ceil(totalCount / pageSize);

    return (
        <div className="card" style={{ maxWidth: "1400px" }}>
            <h2 className="text-2xl font-semibold mb-6">
                {"\u{1F4CB}"} Pregled logova
            </h2>

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
                    <label className="field-label" style={{ fontSize: "0.875rem" }}>Nivo</label>
                    <select
                        className="input-big"
                        value={selectedLevel}
                        onChange={(e) => {
                            setSelectedLevel(e.target.value);
                            setCurrentPage(1);
                        }}
                        style={{ marginTop: "0.25rem", marginBottom: 0, fontSize: "0.95rem", padding: "8px 12px" }}
                    >
                        <option value="">Svi nivoi</option>
                        <option value="Debug">Debug</option>
                        <option value="Information">Information</option>
                        <option value="Warning">Warning</option>
                        <option value="Error">Error</option>
                        <option value="Fatal">Fatal</option>
                    </select>
                </div>

                <div>
                    <label className="field-label" style={{ fontSize: "0.875rem" }}>Vremenski period</label>
                    <select
                        className="input-big"
                        value={selectedPeriod}
                        onChange={(e) => handlePeriodChange(e.target.value as TimePeriod)}
                        style={{ marginTop: "0.25rem", marginBottom: 0, fontSize: "0.95rem", padding: "8px 12px" }}
                    >
                        {timePeriodOptions.map(opt => (
                            <option key={opt.value} value={opt.value}>{opt.label}</option>
                        ))}
                    </select>
                </div>

                <div>
                    <label className="field-label" style={{ fontSize: "0.875rem" }}>Od datuma</label>
                    <input
                        type="datetime-local"
                        className="input-big"
                        value={fromDate}
                        onChange={(e) => handleCustomDateChange("from", e.target.value)}
                        style={{ marginTop: "0.25rem", marginBottom: 0, fontSize: "0.95rem", padding: "8px 12px" }}
                    />
                </div>

                <div>
                    <label className="field-label" style={{ fontSize: "0.875rem" }}>Do datuma</label>
                    <input
                        type="datetime-local"
                        className="input-big"
                        value={toDate}
                        onChange={(e) => handleCustomDateChange("to", e.target.value)}
                        style={{ marginTop: "0.25rem", marginBottom: 0, fontSize: "0.95rem", padding: "8px 12px" }}
                    />
                </div>

                <div style={{ display: "flex", alignItems: "flex-end", gap: "0.5rem" }}>
                    <button
                        className="button-big"
                        onClick={() => fetchLogs()}
                        style={{ 
                            background: "#2563eb", 
                            padding: "8px 16px", 
                            marginTop: 0,
                            marginBottom: 0,
                            fontSize: "0.95rem"
                        }}
                        title="Osveži logove"
                    >
                        ?? Osveži
                    </button>
                    <button
                        className="button-big"
                        onClick={() => {
                            setSelectedLevel("");
                            setSelectedPeriod("");
                            setFromDate("");
                            setToDate("");
                            setCurrentPage(1);
                        }}
                        style={{ 
                            background: "#6b7280", 
                            padding: "8px 16px", 
                            marginTop: 0,
                            marginBottom: 0,
                            fontSize: "0.95rem"
                        }}
                    >
                        Resetuj
                    </button>
                </div>
            </div>

            {/* Stats */}
            <div style={{ 
                marginBottom: "1rem", 
                padding: "0.75rem", 
                background: "#f3f4f6", 
                borderRadius: "8px",
                fontSize: "0.95rem",
                display: "flex",
                justifyContent: "space-between",
                flexWrap: "wrap",
                gap: "0.5rem"
            }}>
                <span>
                    <strong>Ukupno:</strong> {totalCount} logova | <strong>Stranica:</strong> {currentPage} od {totalPages}
                </span>
                {selectedPeriod && (
                    <span style={{ color: "#2563eb", fontWeight: 500 }}>
                        ?? {timePeriodOptions.find(o => o.value === selectedPeriod)?.label}
                    </span>
                )}
            </div>

            {/* Loading / Error */}
            {loading && <p style={{ textAlign: "center", padding: "2rem" }}>U?itavanje...</p>}
            {error && <p className="error-msg">{error}</p>}

            {/* Logs Table */}
            {!loading && !error && (
                <>
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
                                        Nivo
                                    </th>
                                    <th style={{ padding: "12px", textAlign: "left", fontWeight: 600 }}>
                                        Poruka
                                    </th>
                                </tr>
                            </thead>
                            <tbody>
                                {logs.map((log, index) => (
                                    <React.Fragment key={index}>
                                        <tr style={{ 
                                            borderBottom: "1px solid #e5e7eb",
                                            background: getLevelBgColor(log.level)
                                        }}>
                                            <td style={{ 
                                                padding: "12px", 
                                                whiteSpace: "nowrap",
                                                fontFamily: "monospace",
                                                fontSize: "0.8rem"
                                            }}>
                                                {formatDate(log.timestamp)}
                                            </td>
                                            <td style={{ padding: "12px" }}>
                                                <span style={{
                                                    padding: "4px 12px",
                                                    borderRadius: "6px",
                                                    fontWeight: 600,
                                                    fontSize: "0.75rem",
                                                    color: getLevelColor(log.level),
                                                    background: "white",
                                                    border: `1px solid ${getLevelColor(log.level)}`,
                                                    display: "inline-block"
                                                }}>
                                                    {log.level.toUpperCase()}
                                                </span>
                                            </td>
                                            <td style={{ 
                                                padding: "12px",
                                                wordBreak: "break-word",
                                                lineHeight: 1.5
                                            }}>
                                                {log.message}
                                            </td>
                                        </tr>
                                        {log.exception && (
                                            <tr style={{ 
                                                background: "#fef2f2",
                                                borderBottom: "1px solid #e5e7eb"
                                            }}>
                                                <td colSpan={3} style={{ padding: "12px" }}>
                                                    <details>
                                                        <summary style={{ 
                                                            cursor: "pointer", 
                                                            fontWeight: 600,
                                                            color: "#dc2626",
                                                            marginBottom: "8px"
                                                        }}>
                                                            {"\u{1F41E}"} Exception Details
                                                        </summary>
                                                        <pre style={{
                                                            background: "#ffffff",
                                                            padding: "12px",
                                                            borderRadius: "8px",
                                                            border: "1px solid #fecaca",
                                                            fontSize: "0.75rem",
                                                            overflow: "auto",
                                                            lineHeight: 1.4
                                                        }}>
                                                            {log.exception}
                                                        </pre>
                                                    </details>
                                                </td>
                                            </tr>
                                        )}
                                    </React.Fragment>
                                ))}
                            </tbody>
                        </table>
                    </div>

                    {/* Pagination */}
                    {totalPages > 1 && (
                        <div style={{ 
                            display: "flex", 
                            justifyContent: "center", 
                            gap: "0.5rem", 
                            marginTop: "1.5rem",
                            flexWrap: "wrap"
                        }}>
                            <button
                                className="button-big"
                                onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
                                disabled={currentPage === 1}
                                style={{ 
                                    width: "auto", 
                                    padding: "8px 16px",
                                    fontSize: "0.95rem",
                                    marginTop: 0
                                }}
                            >
                                ? Prethodna
                            </button>

                            <span style={{ 
                                padding: "8px 16px", 
                                alignSelf: "center",
                                fontWeight: 600
                            }}>
                                {currentPage} / {totalPages}
                            </span>

                            <button
                                className="button-big"
                                onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}
                                disabled={currentPage === totalPages}
                                style={{ 
                                    width: "auto", 
                                    padding: "8px 16px",
                                    fontSize: "0.95rem",
                                    marginTop: 0
                                }}
                            >
                                Slede?a ?
                            </button>
                        </div>
                    )}
                </>
            )}

            {!loading && !error && logs.length === 0 && (
                <p style={{ textAlign: "center", padding: "2rem", color: "#6b7280" }}>
                    Nema logova koji odgovaraju filterima.
                </p>
            )}
        </div>
    );
}

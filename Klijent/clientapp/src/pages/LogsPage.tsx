import React, { useEffect, useRef, useState } from "react";
import { clearLogs, getLogById, getLogs } from "../services/logsApi";
import type { LogEntry } from "../types/logs";

type TimePeriod = "" | "30m" | "1h" | "6h" | "1d" | "2d" | "7d";

const PAGE_SIZE = 100;

const timePeriodOptions: { value: TimePeriod; label: string }[] = [
    { value: "", label: "Prilagođeni period" },
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
    const to = now.toISOString().slice(0, 16);

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

    return {
        from: from.toISOString().slice(0, 16),
        to,
    };
}

function getLevelColor(level: string): string {
    switch (level.toUpperCase()) {
        case "ERROR":
        case "FATAL":
            return "var(--error)";
        case "WARNING":
            return "var(--warning)";
        case "INFORMATION":
        case "INFO":
            return "var(--info)";
        case "DEBUG":
            return "var(--text-secondary)";
        default:
            return "var(--text-muted)";
    }
}

function getLevelBgColor(level: string): string {
    switch (level.toUpperCase()) {
        case "ERROR":
        case "FATAL":
            return "var(--error-soft)";
        case "WARNING":
            return "var(--warning-soft)";
        case "INFORMATION":
        case "INFO":
            return "var(--info-soft)";
        default:
            return "transparent";
    }
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

export default function LogsPage() {
    const [logs, setLogs] = useState<LogEntry[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [selectedLevel, setSelectedLevel] = useState("");
    const [selectedPeriod, setSelectedPeriod] = useState<TimePeriod>("");
    const [fromDate, setFromDate] = useState("");
    const [toDate, setToDate] = useState("");
    const [currentPage, setCurrentPage] = useState(1);
    const [totalCount, setTotalCount] = useState(0);
    const [selectedLog, setSelectedLog] = useState<LogEntry | null>(null);
    const [searchTerm, setSearchTerm] = useState("");
    const [debouncedSearchTerm, setDebouncedSearchTerm] = useState("");
    const [logIdInput, setLogIdInput] = useState("");
    const [loadingLogById, setLoadingLogById] = useState(false);
    const [clearingLogs, setClearingLogs] = useState(false);
    const adminKeyRef = useRef<string | null>(null);

    const ensureAdminKey = (actionLabel: string): string | null => {
        if (adminKeyRef.current) return adminKeyRef.current;
        const key = window.prompt(`Unesite admin key za ${actionLabel}`);
        if (!key || !key.trim()) return null;
        adminKeyRef.current = key.trim();
        return adminKeyRef.current;
    };

    useEffect(() => {
        const timer = window.setTimeout(() => {
            setDebouncedSearchTerm(searchTerm.trim());
            setCurrentPage(1);
        }, 300);

        return () => window.clearTimeout(timer);
    }, [searchTerm]);

    const fetchLogs = async () => {
        const adminKey = ensureAdminKey("pregled logova");
        if (!adminKey) {
            setLoading(false);
            setError("Admin key je obavezan za pregled logova.");
            setLogs([]);
            setTotalCount(0);
            return;
        }

        setLoading(true);
        setError(null);

        try {
            const result = await getLogs(
                currentPage,
                PAGE_SIZE,
                selectedLevel || undefined,
                fromDate || undefined,
                toDate || undefined,
                debouncedSearchTerm || undefined,
                adminKey
            );

            setLogs(result.logs);
            setTotalCount(result.totalCount);
        } catch (err) {
            const message = err instanceof Error ? err.message : "Greška pri učitavanju logova";
            setError(message);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchLogs();
    }, [currentPage, selectedLevel, fromDate, toDate, debouncedSearchTerm]);

    const handlePeriodChange = (period: TimePeriod) => {
        setSelectedPeriod(period);
        const range = getDateRangeFromPeriod(period);
        setFromDate(range.from);
        setToDate(range.to);
        setCurrentPage(1);
    };

    const handleCustomDateChange = (field: "from" | "to", value: string) => {
        setSelectedPeriod("");
        if (field === "from") setFromDate(value);
        if (field === "to") setToDate(value);
        setCurrentPage(1);
    };

    const copyToClipboard = (text: string) => {
        navigator.clipboard.writeText(text);
        alert("Kopirano u clipboard!");
    };

    const openLogById = async () => {
        const parsedId = Number.parseInt(logIdInput, 10);
        if (!Number.isInteger(parsedId) || parsedId <= 0) {
            setError("Unesi ispravan ID loga.");
            return;
        }

        const adminKey = ensureAdminKey("pregled logova");
        if (!adminKey) {
            setError("Admin key je obavezan za pregled logova.");
            return;
        }

        setLoadingLogById(true);
        setError(null);
        try {
            const log = await getLogById(parsedId, adminKey);
            setSelectedLog(log);
        } catch (err) {
            const message = err instanceof Error ? err.message : "Greška pri učitavanju loga po ID.";
            setError(message);
        } finally {
            setLoadingLogById(false);
        }
    };

    const handleClearLogs = async () => {
        if (!window.confirm("Potvrdi brisanje logova za zadate filtere.")) {
            return;
        }

        const adminKey = ensureAdminKey("brisanje logova");
        if (!adminKey) {
            setError("Admin key je obavezan za brisanje logova.");
            return;
        }

        setClearingLogs(true);
        setError(null);
        try {
            const result = await clearLogs(
                adminKey,
                toDate || undefined,
                selectedLevel || undefined
            );
            await fetchLogs();
            alert(`Obrisano logova: ${result.deletedCount}`);
        } catch (err) {
            const message = err instanceof Error ? err.message : "Greška pri brisanju logova.";
            setError(message);
        } finally {
            setClearingLogs(false);
        }
    };

    const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

    return (
        <div className="card max-w-[1400px]">
            <h2 className="text-2xl font-bold mb-6 text-contrast flex items-center gap-2">
                <span>{"\u{1F4CB}"}</span> Pregled logova
            </h2>

            <div className="toolbar grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-6 gap-4 mb-6 p-4 rounded-xl border border-muted bg-surface-darker">
                <div className="flex flex-col gap-1">
                    <label className="text-xs font-semibold uppercase tracking-wider text-muted">Nivo</label>
                    <select
                        className="input-big w-full"
                        value={selectedLevel}
                        onChange={(e) => {
                            setSelectedLevel(e.target.value);
                            setCurrentPage(1);
                        }}
                    >
                        <option value="">Svi nivoi</option>
                        <option value="Debug">Debug</option>
                        <option value="Information">Information</option>
                        <option value="Warning">Warning</option>
                        <option value="Error">Error</option>
                        <option value="Fatal">Fatal</option>
                    </select>
                </div>

                <div className="flex flex-col gap-1">
                    <label className="text-xs font-semibold uppercase tracking-wider text-muted">Vremenski period</label>
                    <select
                        className="input-big w-full"
                        value={selectedPeriod}
                        onChange={(e) => handlePeriodChange(e.target.value as TimePeriod)}
                    >
                        {timePeriodOptions.map((option) => (
                            <option key={option.value} value={option.value}>
                                {option.label}
                            </option>
                        ))}
                    </select>
                </div>

                <div className="flex flex-col gap-1">
                    <label className="text-xs font-semibold uppercase tracking-wider text-muted">Pretraga</label>
                    <input
                        type="text"
                        className="input-big w-full"
                        placeholder="Poruka, User, ID..."
                        value={searchTerm}
                        onChange={(e) => setSearchTerm(e.target.value)}
                    />
                </div>

                <div className="flex flex-col gap-1">
                    <label className="text-xs font-semibold uppercase tracking-wider text-muted">Od datuma</label>
                    <input
                        type="datetime-local"
                        className="input-big w-full"
                        value={fromDate}
                        onChange={(e) => handleCustomDateChange("from", e.target.value)}
                    />
                </div>

                <div className="flex flex-col gap-1">
                    <label className="text-xs font-semibold uppercase tracking-wider text-muted">Do datuma</label>
                    <input
                        type="datetime-local"
                        className="input-big w-full"
                        value={toDate}
                        onChange={(e) => handleCustomDateChange("to", e.target.value)}
                    />
                </div>

                <div className="flex items-end gap-2">
                    <button
                        className="button-big flex-1"
                        onClick={fetchLogs}
                        title="Osveži logove"
                        type="button"
                    >
                        {"\u{1F504}"} Osveži
                    </button>
                    <button
                        className="button-big button-secondary !px-3"
                        onClick={() => {
                            setSelectedLevel("");
                            setSelectedPeriod("");
                            setFromDate("");
                            setToDate("");
                            setSearchTerm("");
                            setCurrentPage(1);
                        }}
                        title="Resetuj filtere"
                        type="button"
                    >
                        Reset
                    </button>
                    <button
                        className="button-big button-danger !px-3"
                        onClick={() => {
                            void handleClearLogs();
                        }}
                        title="Obriši logove (admin)"
                        type="button"
                        disabled={clearingLogs}
                    >
                        {clearingLogs ? "..." : "Obriši"}
                    </button>
                </div>
            </div>

            <div className="flex justify-between items-center flex-wrap gap-4 mb-4 p-3 rounded-lg border border-muted bg-surface/30 text-sm">
                <span className="text-contrast">
                    <strong className="text-muted">Ukupno:</strong> {totalCount} logova |{" "}
                    <strong className="text-muted">Stranica:</strong> {currentPage} od {totalPages}
                </span>
                <div className="flex items-center gap-2">
                    <input
                        type="number"
                        min={1}
                        className="input-big !mb-0 !py-2 !px-3 w-36"
                        placeholder="ID loga"
                        value={logIdInput}
                        onChange={(e) => setLogIdInput(e.target.value)}
                        onKeyDown={(e) => {
                            if (e.key === "Enter") {
                                void openLogById();
                            }
                        }}
                    />
                    <button
                        type="button"
                        className="button-big !py-2 !px-4"
                        disabled={loadingLogById}
                        onClick={() => {
                            void openLogById();
                        }}
                    >
                        {loadingLogById ? "..." : "Otvori ID"}
                    </button>
                </div>
                {selectedPeriod && (
                    <span className="text-info font-bold">
                        {"\u{1F4C5}"} {timePeriodOptions.find((option) => option.value === selectedPeriod)?.label}
                    </span>
                )}
            </div>

            {error && (
                <div className="mb-4 rounded-lg border border-error bg-error/10 p-4 text-sm text-error">
                    {error}
                </div>
            )}

            {loading ? (
                <div className="py-20 text-center text-muted">Učitavanje logova...</div>
            ) : (
                <>
                    <div className="overflow-hidden rounded-xl border border-muted bg-surface-elevated">
                        <div className="overflow-x-auto">
                            <table className="min-w-full divide-y divide-muted text-sm">
                                <thead className="bg-surface-darker text-muted">
                                    <tr>
                                        <th className="px-4 py-3 text-left font-semibold uppercase tracking-wider w-40">Vreme</th>
                                        <th className="px-4 py-3 text-left font-semibold uppercase tracking-wider w-24">Nivo</th>
                                        <th className="px-4 py-3 text-left font-semibold uppercase tracking-wider">Poruka</th>
                                        <th className="px-4 py-3 text-right font-semibold uppercase tracking-wider w-20">Akcije</th>
                                    </tr>
                                </thead>
                                <tbody className="divide-y divide-muted/50 text-contrast font-mono text-[11px]">
                                    {logs.map((log, index) => (
                                        <React.Fragment key={log.id ?? `${log.timestamp}-${index}`}>
                                            <tr
                                                className="table-row-hover transition-colors cursor-pointer"
                                                style={{ backgroundColor: getLevelBgColor(log.level) }}
                                                onClick={() => setSelectedLog(log)}
                                            >
                                                <td className="px-4 py-2 whitespace-nowrap opacity-70">
                                                    {formatDate(log.timestamp)}
                                                </td>
                                                <td className="px-4 py-2">
                                                    <span
                                                        className="font-bold uppercase px-1.5 py-0.5 rounded text-[10px]"
                                                        style={{ 
                                                            color: getLevelColor(log.level),
                                                            backgroundColor: `${getLevelColor(log.level)}20`,
                                                            border: `1px solid ${getLevelColor(log.level)}40`
                                                        }}
                                                    >
                                                        {log.level}
                                                    </span>
                                                </td>
                                                <td className="px-4 py-2 break-all max-w-md truncate" title={log.message}>
                                                    {log.message}
                                                </td>
                                                <td className="px-4 py-2 text-right">
                                                    <button 
                                                        className="text-muted hover:text-contrast"
                                                        onClick={(e) => {
                                                            e.stopPropagation();
                                                            setSelectedLog(log);
                                                        }}
                                                    >
                                                        {"\u{1F441}"}
                                                    </button>
                                                </td>
                                            </tr>
                                            {log.exception && (
                                                <tr className="bg-error/5 border-l-2 border-l-error">
                                                    <td colSpan={4} className="px-4 py-1.5">
                                                        <div className="flex items-center gap-2 text-error text-[10px] font-bold opacity-80">
                                                            <span>{"\u{1F41E}"}</span>
                                                            <span className="truncate">{log.exception.split('\n')[0]}</span>
                                                        </div>
                                                    </td>
                                                </tr>
                                            )}
                                        </React.Fragment>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    </div>

                    {/* Pagination */}
                    <div className="flex justify-center flex-wrap gap-2 mt-6">
                        <button
                            className="button-big button-secondary !px-4"
                            onClick={() => setCurrentPage((page) => Math.max(1, page - 1))}
                            disabled={currentPage === 1 || loading}
                            type="button"
                        >
                            {"\u2190"} Prethodna
                        </button>
                        <div className="flex items-center gap-1 font-semibold mx-4 text-contrast">
                            Stranica {currentPage} od {totalPages}
                        </div>
                        <button
                            className="button-big button-secondary !px-4"
                            onClick={() => setCurrentPage((page) => Math.min(totalPages, page + 1))}
                            disabled={currentPage >= totalPages || loading}
                            type="button"
                        >
                            Sledeća {"\u2192"}
                        </button>
                    </div>

                    {/* Modal za Detalje */}
                    {selectedLog && (
                        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm" onClick={() => setSelectedLog(null)}>
                            <div className="bg-surface-elevated border border-muted w-full max-w-4xl max-h-[90vh] rounded-2xl shadow-2xl overflow-hidden flex flex-col" onClick={e => e.stopPropagation()}>
                                <div className="p-4 border-b border-muted bg-surface-darker flex justify-between items-center">
                                    <div className="flex items-center gap-3">
                                        <span className="font-bold uppercase px-2 py-1 rounded text-xs" style={{ 
                                            color: getLevelColor(selectedLog.level),
                                            backgroundColor: `${getLevelColor(selectedLog.level)}20`,
                                        }}>
                                            {selectedLog.level}
                                        </span>
                                        <h3 className="font-bold text-contrast truncate max-w-xl">{selectedLog.message}</h3>
                                    </div>
                                    <button onClick={() => setSelectedLog(null)} className="text-muted hover:text-contrast text-2xl">&times;</button>
                                </div>
                                
                                <div className="p-6 overflow-y-auto flex-1 custom-scrollbar">
                                    <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-6 text-sm">
                                        <div className="space-y-3">
                                            <div>
                                                <label className="text-xs text-muted uppercase font-bold">ID</label>
                                                <div className="text-contrast font-mono">{selectedLog.id ?? "-"}</div>
                                            </div>
                                            <div>
                                                <label className="text-xs text-muted uppercase font-bold">Vreme (Lokalno)</label>
                                                <div className="text-contrast font-mono">{formatDate(selectedLog.timestamp)}</div>
                                            </div>
                                            <div>
                                                <label className="text-xs text-muted uppercase font-bold">Korisnik</label>
                                                <div className="text-contrast font-mono">{selectedLog.properties?.userName || "Sistem"}</div>
                                            </div>
                                            <div>
                                                <label className="text-xs text-muted uppercase font-bold">Putanja</label>
                                                <div className="text-contrast font-mono">{selectedLog.properties?.path || "/"}</div>
                                            </div>
                                        </div>
                                        <div className="space-y-3">
                                            <div>
                                                <label className="text-xs text-muted uppercase font-bold">Correlation ID</label>
                                                <div className="text-info font-mono text-xs break-all flex items-center gap-2">
                                                    {selectedLog.properties?.correlationId}
                                                    <button onClick={() => copyToClipboard(selectedLog.properties?.correlationId || "")} className="text-[10px] underline hover:text-contrast">Copy</button>
                                                </div>
                                            </div>
                                            <div>
                                                <label className="text-xs text-muted uppercase font-bold">Klijent</label>
                                                <div className="text-contrast font-mono">{selectedLog.properties?.clientApp || "Browser"}</div>
                                            </div>
                                        </div>
                                    </div>

                                    {selectedLog.exception && (
                                        <div className="mb-6">
                                            <label className="text-xs text-error uppercase font-bold block mb-2">Stack Trace</label>
                                            <pre className="bg-surface-darker border border-error/20 p-4 rounded-xl text-[11px] text-error overflow-x-auto whitespace-pre font-mono leading-relaxed max-h-60 custom-scrollbar">
                                                {selectedLog.exception}
                                            </pre>
                                            <button 
                                                onClick={() => copyToClipboard(selectedLog.exception || "")}
                                                className="mt-2 text-xs text-muted hover:text-error underline"
                                            >
                                                Kopiraj Stack Trace
                                            </button>
                                        </div>
                                    )}

                                    <div>
                                        <label className="text-xs text-muted uppercase font-bold block mb-2">Raw Properties (JSON)</label>
                                        <pre className="bg-surface-darker border border-muted p-4 rounded-xl text-[11px] text-info overflow-x-auto font-mono">
                                            {JSON.stringify(selectedLog.properties, null, 2)}
                                        </pre>
                                    </div>
                                </div>

                                <div className="p-4 border-t border-muted bg-surface-darker flex justify-end gap-3">
                                    <button 
                                        className="button button-secondary text-sm"
                                        onClick={() => copyToClipboard(JSON.stringify(selectedLog, null, 2))}
                                    >
                                        Kopiraj ceo log
                                    </button>
                                    <button 
                                        className="button-big text-sm"
                                        onClick={() => setSelectedLog(null)}
                                    >
                                        Zatvori
                                    </button>
                                </div>
                            </div>
                        </div>
                    )}

                    {logs.length === 0 && !loading && (
                        <div className="py-20 text-center text-muted border border-dashed border-muted rounded-xl mt-4">
                            Nema logova za zadate kriterijume.
                        </div>
                    )}
                </>
            )}
        </div>
    );
}

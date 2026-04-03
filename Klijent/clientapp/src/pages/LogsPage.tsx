import React, { useEffect, useState } from "react";
import { getLogs } from "../services/logsApi";
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

    const fetchLogs = async () => {
        setLoading(true);
        setError(null);

        try {
            const result = await getLogs(
                currentPage,
                PAGE_SIZE,
                selectedLevel || undefined,
                fromDate || undefined,
                toDate || undefined
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
    }, [currentPage, selectedLevel, fromDate, toDate]);

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

    const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

    return (
        <div className="card max-w-[1400px]">
            <h2 className="text-2xl font-bold mb-6 text-contrast">
                {"\u{1F4CB}"} Pregled logova
            </h2>

            <div className="toolbar grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-4 mb-6 p-4 rounded-xl border border-muted bg-surface-darker">
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
                            setCurrentPage(1);
                        }}
                        title="Resetuj filtere"
                        type="button"
                    >
                        Reset
                    </button>
                </div>
            </div>

            <div className="flex justify-between items-center flex-wrap gap-4 mb-4 p-3 rounded-lg border border-muted bg-surface/30 text-sm">
                <span className="text-contrast">
                    <strong className="text-muted">Ukupno:</strong> {totalCount} logova |{" "}
                    <strong className="text-muted">Stranica:</strong> {currentPage} od {totalPages}
                </span>
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
                                    </tr>
                                </thead>
                                <tbody className="divide-y divide-muted/50 text-contrast font-mono text-xs">
                                    {logs.map((log, index) => (
                                        <React.Fragment key={`${log.timestamp}-${index}`}>
                                            <tr
                                                className="hover:bg-surface/30 transition-colors"
                                                style={{ backgroundColor: getLevelBgColor(log.level) }}
                                            >
                                                <td className="px-4 py-2 whitespace-nowrap opacity-70">
                                                    {formatDate(log.timestamp)}
                                                </td>
                                                <td className="px-4 py-2">
                                                    <span
                                                        className="font-bold uppercase"
                                                        style={{ color: getLevelColor(log.level) }}
                                                    >
                                                        {log.level}
                                                    </span>
                                                </td>
                                                <td className="px-4 py-2 break-all">
                                                    {log.message}
                                                </td>
                                            </tr>
                                            {log.exception && (
                                                <tr className="bg-error/5">
                                                    <td colSpan={3} className="px-4 py-3">
                                                        <details>
                                                            <summary className="cursor-pointer font-bold text-error mb-2">
                                                                {"\u{1F41E}"} Exception Details
                                                            </summary>
                                                            <pre className="rounded-xl border border-error/30 bg-surface px-3 py-2 text-[11px] leading-5 overflow-auto whitespace-pre-wrap">
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
                    </div>

                    <div className="flex justify-center flex-wrap gap-2 mt-6">
                        <button
                            className="button-big button-secondary !px-4"
                            onClick={() => setCurrentPage((page) => Math.max(1, page - 1))}
                            disabled={currentPage === 1 || loading}
                            type="button"
                        >
                            {"\u2190"} Prethodna
                        </button>
                        <div className="flex items-center gap-1 font-semibold mx-2 text-contrast">
                            {currentPage} / {totalPages}
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

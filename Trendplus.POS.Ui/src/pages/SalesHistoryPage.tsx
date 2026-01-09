import { useEffect, useState, useMemo } from "react";
import { getSalesHistory, type SaleListItem } from "../api/posApi";

type TimePeriod = "" | "today" | "yesterday" | "7d" | "30d" | "custom";

const timePeriodOptions: { value: TimePeriod; label: string }[] = [
    { value: "", label: "Sve prodaje" },
    { value: "today", label: "Danas" },
    { value: "yesterday", label: "Ju?e" },
    { value: "7d", label: "Poslednjih 7 dana" },
    { value: "30d", label: "Poslednjih 30 dana" },
    { value: "custom", label: "Prilago?eni period" },
];

function getDateRangeFromPeriod(period: TimePeriod): { from: string; to: string } {
    if (!period || period === "custom") return { from: "", to: "" };
    
    const now = new Date();
    const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    
    let from: Date;
    let to: Date = new Date(today.getTime() + 24 * 60 * 60 * 1000 - 1); // End of today
    
    switch (period) {
        case "today":
            from = today;
            break;
        case "yesterday":
            from = new Date(today.getTime() - 24 * 60 * 60 * 1000);
            to = new Date(today.getTime() - 1);
            break;
        case "7d":
            from = new Date(today.getTime() - 7 * 24 * 60 * 60 * 1000);
            break;
        case "30d":
            from = new Date(today.getTime() - 30 * 24 * 60 * 60 * 1000);
            break;
        default:
            return { from: "", to: "" };
    }
    
    return { 
        from: from.toISOString().slice(0, 16), 
        to: to.toISOString().slice(0, 16) 
    };
}

// Group sales by date
function groupSalesByDate(sales: SaleListItem[]): Map<string, SaleListItem[]> {
    const groups = new Map<string, SaleListItem[]>();
    
    for (const sale of sales) {
        const dateKey = new Date(sale.datumProdaje).toLocaleDateString("sr-RS", {
            year: "numeric",
            month: "2-digit",
            day: "2-digit"
        });
        
        if (!groups.has(dateKey)) {
            groups.set(dateKey, []);
        }
        groups.get(dateKey)!.push(sale);
    }
    
    return groups;
}

export default function SalesHistoryPage() {
    const [sales, setSales] = useState<SaleListItem[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    
    // Filters
    const [selectedPeriod, setSelectedPeriod] = useState<TimePeriod>("today");
    const [fromDate, setFromDate] = useState<string>("");
    const [toDate, setToDate] = useState<string>("");
    
    // Pagination
    const [currentPage, setCurrentPage] = useState(1);
    const [totalCount, setTotalCount] = useState(0);
    const [pageSize] = useState(100);

    const handlePeriodChange = (period: TimePeriod) => {
        setSelectedPeriod(period);
        if (period !== "custom") {
            const range = getDateRangeFromPeriod(period);
            setFromDate(range.from);
            setToDate(range.to);
        }
        setCurrentPage(1);
    };

    const fetchSales = async () => {
        setLoading(true);
        setError(null);

        try {
            const result = await getSalesHistory(
                currentPage,
                pageSize,
                fromDate || undefined,
                toDate || undefined
            );

            setSales(result.items);
            setTotalCount(result.totalCount);
        } catch (err: unknown) {
            console.error("Error fetching sales:", err);
            const message = err instanceof Error ? err.message : "Greška pri u?itavanju prodaja";
            setError(message);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        // Set initial period to today
        const range = getDateRangeFromPeriod("today");
        setFromDate(range.from);
        setToDate(range.to);
    }, []);

    useEffect(() => {
        if (fromDate || toDate || selectedPeriod === "") {
            fetchSales();
        }
    }, [currentPage, fromDate, toDate]);

    // Group sales by date
    const groupedSales = useMemo(() => groupSalesByDate(sales), [sales]);

    // Calculate totals
    const totals = useMemo(() => {
        const totalRevenue = sales.reduce((sum, s) => sum + s.ukupanIznos, 0);
        const totalTransactions = sales.length;
        const avgTransaction = totalTransactions > 0 ? totalRevenue / totalTransactions : 0;
        
        return { totalRevenue, totalTransactions, avgTransaction };
    }, [sales]);

    const formatDate = (dateString: string) => {
        const date = new Date(dateString);
        return date.toLocaleString("sr-RS", {
            hour: "2-digit",
            minute: "2-digit",
            second: "2-digit",
        });
    };

    const formatCurrency = (amount: number) => {
        return amount.toLocaleString("sr-RS", {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }) + " RSD";
    };

    const totalPages = Math.ceil(totalCount / pageSize);

    return (
        <div style={{ 
            minHeight: "100vh", 
            background: "#f3f4f6", 
            padding: 20 
        }}>
            <div style={{ 
                maxWidth: 1200, 
                margin: "0 auto" 
            }}>
                <h1 style={{ 
                    fontSize: 28, 
                    fontWeight: 700, 
                    marginBottom: 24,
                    color: "#111827"
                }}>
                    ?? Istorija prodaje
                </h1>

                {/* Summary Cards */}
                <div style={{ 
                    display: "grid", 
                    gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", 
                    gap: 16, 
                    marginBottom: 24 
                }}>
                    <div style={{ 
                        background: "white", 
                        padding: 20, 
                        borderRadius: 12, 
                        boxShadow: "0 1px 3px rgba(0,0,0,0.1)" 
                    }}>
                        <div style={{ color: "#6b7280", fontSize: 14, marginBottom: 8 }}>
                            Ukupan promet
                        </div>
                        <div style={{ fontSize: 24, fontWeight: 700, color: "#059669" }}>
                            {formatCurrency(totals.totalRevenue)}
                        </div>
                    </div>
                    
                    <div style={{ 
                        background: "white", 
                        padding: 20, 
                        borderRadius: 12, 
                        boxShadow: "0 1px 3px rgba(0,0,0,0.1)" 
                    }}>
                        <div style={{ color: "#6b7280", fontSize: 14, marginBottom: 8 }}>
                            Broj transakcija
                        </div>
                        <div style={{ fontSize: 24, fontWeight: 700, color: "#2563eb" }}>
                            {totals.totalTransactions}
                        </div>
                    </div>
                    
                    <div style={{ 
                        background: "white", 
                        padding: 20, 
                        borderRadius: 12, 
                        boxShadow: "0 1px 3px rgba(0,0,0,0.1)" 
                    }}>
                        <div style={{ color: "#6b7280", fontSize: 14, marginBottom: 8 }}>
                            Prose?na vrednost
                        </div>
                        <div style={{ fontSize: 24, fontWeight: 700, color: "#7c3aed" }}>
                            {formatCurrency(totals.avgTransaction)}
                        </div>
                    </div>
                </div>

                {/* Filters */}
                <div style={{ 
                    background: "white", 
                    padding: 20, 
                    borderRadius: 12, 
                    marginBottom: 24,
                    boxShadow: "0 1px 3px rgba(0,0,0,0.1)"
                }}>
                    <div style={{ 
                        display: "grid", 
                        gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", 
                        gap: 16,
                        alignItems: "end"
                    }}>
                        <div>
                            <label style={{ 
                                display: "block", 
                                fontSize: 14, 
                                fontWeight: 600, 
                                marginBottom: 6,
                                color: "#374151"
                            }}>
                                Vremenski period
                            </label>
                            <select
                                value={selectedPeriod}
                                onChange={(e) => handlePeriodChange(e.target.value as TimePeriod)}
                                style={{
                                    width: "100%",
                                    padding: "10px 12px",
                                    fontSize: 16,
                                    border: "2px solid #e5e7eb",
                                    borderRadius: 8,
                                    background: "white"
                                }}
                            >
                                {timePeriodOptions.map(opt => (
                                    <option key={opt.value} value={opt.value}>{opt.label}</option>
                                ))}
                            </select>
                        </div>

                        {selectedPeriod === "custom" && (
                            <>
                                <div>
                                    <label style={{ 
                                        display: "block", 
                                        fontSize: 14, 
                                        fontWeight: 600, 
                                        marginBottom: 6,
                                        color: "#374151"
                                    }}>
                                        Od datuma
                                    </label>
                                    <input
                                        type="datetime-local"
                                        value={fromDate}
                                        onChange={(e) => {
                                            setFromDate(e.target.value);
                                            setCurrentPage(1);
                                        }}
                                        style={{
                                            width: "100%",
                                            padding: "10px 12px",
                                            fontSize: 16,
                                            border: "2px solid #e5e7eb",
                                            borderRadius: 8
                                        }}
                                    />
                                </div>

                                <div>
                                    <label style={{ 
                                        display: "block", 
                                        fontSize: 14, 
                                        fontWeight: 600, 
                                        marginBottom: 6,
                                        color: "#374151"
                                    }}>
                                        Do datuma
                                    </label>
                                    <input
                                        type="datetime-local"
                                        value={toDate}
                                        onChange={(e) => {
                                            setToDate(e.target.value);
                                            setCurrentPage(1);
                                        }}
                                        style={{
                                            width: "100%",
                                            padding: "10px 12px",
                                            fontSize: 16,
                                            border: "2px solid #e5e7eb",
                                            borderRadius: 8
                                        }}
                                    />
                                </div>
                            </>
                        )}

                        <div>
                            <button
                                onClick={fetchSales}
                                style={{
                                    padding: "10px 20px",
                                    fontSize: 16,
                                    fontWeight: 600,
                                    background: "#2563eb",
                                    color: "white",
                                    border: "none",
                                    borderRadius: 8,
                                    cursor: "pointer"
                                }}
                            >
                                ?? Osveži
                            </button>
                        </div>
                    </div>
                </div>

                {/* Loading / Error */}
                {loading && (
                    <div style={{ textAlign: "center", padding: 40 }}>
                        <p style={{ fontSize: 18, color: "#6b7280" }}>U?itavanje...</p>
                    </div>
                )}

                {error && (
                    <div style={{ 
                        background: "#fef2f2", 
                        border: "1px solid #fecaca", 
                        borderRadius: 8, 
                        padding: 16,
                        marginBottom: 24
                    }}>
                        <p style={{ color: "#dc2626", margin: 0 }}>{error}</p>
                    </div>
                )}

                {/* Sales List by Date */}
                {!loading && !error && (
                    <>
                        {sales.length === 0 ? (
                            <div style={{ 
                                background: "white", 
                                padding: 40, 
                                borderRadius: 12, 
                                textAlign: "center",
                                boxShadow: "0 1px 3px rgba(0,0,0,0.1)"
                            }}>
                                <p style={{ fontSize: 18, color: "#6b7280" }}>
                                    Nema prodaja za izabrani period.
                                </p>
                            </div>
                        ) : (
                            Array.from(groupedSales.entries()).map(([date, daySales]) => {
                                const dayTotal = daySales.reduce((sum, s) => sum + s.ukupanIznos, 0);
                                
                                return (
                                    <div key={date} style={{ marginBottom: 24 }}>
                                        {/* Date Header */}
                                        <div style={{ 
                                            display: "flex", 
                                            justifyContent: "space-between", 
                                            alignItems: "center",
                                            marginBottom: 12,
                                            padding: "12px 16px",
                                            background: "#1f2937",
                                            borderRadius: 8,
                                            color: "white"
                                        }}>
                                            <div style={{ fontWeight: 600, fontSize: 18 }}>
                                                ?? {date}
                                            </div>
                                            <div style={{ display: "flex", gap: 24 }}>
                                                <span>
                                                    <strong>{daySales.length}</strong> transakcija
                                                </span>
                                                <span style={{ color: "#34d399", fontWeight: 700 }}>
                                                    {formatCurrency(dayTotal)}
                                                </span>
                                            </div>
                                        </div>

                                        {/* Sales Table */}
                                        <div style={{ 
                                            background: "white", 
                                            borderRadius: 8, 
                                            overflow: "hidden",
                                            boxShadow: "0 1px 3px rgba(0,0,0,0.1)"
                                        }}>
                                            <table style={{ 
                                                width: "100%", 
                                                borderCollapse: "collapse",
                                                fontSize: 14
                                            }}>
                                                <thead>
                                                    <tr style={{ background: "#f9fafb" }}>
                                                        <th style={{ padding: 12, textAlign: "left", fontWeight: 600 }}>
                                                            Vreme
                                                        </th>
                                                        <th style={{ padding: 12, textAlign: "left", fontWeight: 600 }}>
                                                            Broj ra?una
                                                        </th>
                                                        <th style={{ padding: 12, textAlign: "center", fontWeight: 600 }}>
                                                            Stavki
                                                        </th>
                                                        <th style={{ padding: 12, textAlign: "left", fontWeight: 600 }}>
                                                            Pla?anje
                                                        </th>
                                                        <th style={{ padding: 12, textAlign: "right", fontWeight: 600 }}>
                                                            Iznos
                                                        </th>
                                                    </tr>
                                                </thead>
                                                <tbody>
                                                    {daySales.map((sale) => (
                                                        <tr 
                                                            key={sale.id} 
                                                            style={{ borderTop: "1px solid #e5e7eb" }}
                                                        >
                                                            <td style={{ 
                                                                padding: 12, 
                                                                fontFamily: "monospace",
                                                                color: "#6b7280"
                                                            }}>
                                                                {formatDate(sale.datumProdaje)}
                                                            </td>
                                                            <td style={{ padding: 12, fontWeight: 500 }}>
                                                                {sale.brojRacuna || "-"}
                                                            </td>
                                                            <td style={{ padding: 12, textAlign: "center" }}>
                                                                <span style={{
                                                                    background: "#e0e7ff",
                                                                    color: "#3730a3",
                                                                    padding: "4px 10px",
                                                                    borderRadius: 12,
                                                                    fontWeight: 600,
                                                                    fontSize: 13
                                                                }}>
                                                                    {sale.brojStavki}
                                                                </span>
                                                            </td>
                                                            <td style={{ padding: 12, color: "#6b7280" }}>
                                                                {sale.nacinPlacanja || "Gotovina"}
                                                            </td>
                                                            <td style={{ 
                                                                padding: 12, 
                                                                textAlign: "right",
                                                                fontWeight: 700,
                                                                color: "#059669"
                                                            }}>
                                                                {formatCurrency(sale.ukupanIznos)}
                                                            </td>
                                                        </tr>
                                                    ))}
                                                </tbody>
                                            </table>
                                        </div>
                                    </div>
                                );
                            })
                        )}

                        {/* Pagination */}
                        {totalPages > 1 && (
                            <div style={{ 
                                display: "flex", 
                                justifyContent: "center", 
                                gap: 12, 
                                marginTop: 24 
                            }}>
                                <button
                                    onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
                                    disabled={currentPage === 1}
                                    style={{
                                        padding: "10px 20px",
                                        fontSize: 16,
                                        fontWeight: 600,
                                        background: currentPage === 1 ? "#e5e7eb" : "#3b82f6",
                                        color: currentPage === 1 ? "#9ca3af" : "white",
                                        border: "none",
                                        borderRadius: 8,
                                        cursor: currentPage === 1 ? "not-allowed" : "pointer"
                                    }}
                                >
                                    ? Prethodna
                                </button>

                                <span style={{ 
                                    padding: "10px 20px", 
                                    fontWeight: 600 
                                }}>
                                    {currentPage} / {totalPages}
                                </span>

                                <button
                                    onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}
                                    disabled={currentPage === totalPages}
                                    style={{
                                        padding: "10px 20px",
                                        fontSize: 16,
                                        fontWeight: 600,
                                        background: currentPage === totalPages ? "#e5e7eb" : "#3b82f6",
                                        color: currentPage === totalPages ? "#9ca3af" : "white",
                                        border: "none",
                                        borderRadius: 8,
                                        cursor: currentPage === totalPages ? "not-allowed" : "pointer"
                                    }}
                                >
                                    Slede?a ?
                                </button>
                            </div>
                        )}

                        {/* Stats Footer */}
                        <div style={{ 
                            marginTop: 24, 
                            padding: 16, 
                            background: "#f9fafb", 
                            borderRadius: 8,
                            textAlign: "center",
                            color: "#6b7280"
                        }}>
                            Prikazano {sales.length} od {totalCount} prodaja
                        </div>
                    </>
                )}
            </div>
        </div>
    );
}

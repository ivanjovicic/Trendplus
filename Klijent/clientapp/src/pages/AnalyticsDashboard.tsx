import React, { useCallback, useEffect, useState } from "react";
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer, PieChart, Pie, Cell } from 'recharts';
import { checkAnalyticsHealth, getInventoryStatus, getSalesSummary, getTopProducts } from "../services/analyticsApi";
import type { InventoryStatus, SalesSummary, TopProductsResult } from "../types/analytics";

interface DailySale {
  date: string;
  totalRevenue: number;
  transactionCount: number;
  totalUnits: number;
}

interface Comparison {
  current: {
    totalRevenue: number;
    totalTransactions: number;
    totalUnits: number;
  };
  previous: {
    totalRevenue: number;
    totalTransactions: number;
    totalUnits: number;
  } | null;
  change: {
    revenue: number;
    transactions: number;
    units: number;
  } | null;
}

interface CategoryData {
  kategorija: string;
  pol: string;
  totalRevenue: number;
  totalUnits: number;
  transactionCount: number;
}

interface GenderData {
  pol: string;
  totalRevenue: number;
  totalUnits: number;
}

interface SupplierData {
  dobavljacId: number | null;
  dobavljacNaziv: string;
  totalRevenue: number;
  totalUnits: number;
  transactionCount: number;
}

const COLORS = ['#059669', '#3b82f6', '#f59e0b', '#ec4899', '#8b5cf6', '#10b981', '#6366f1', '#f97316'];

export default function AnalyticsDashboard() {
  const [fromDate, setFromDate] = useState<string>("");
  const [toDate, setToDate] = useState<string>("");
  const [top, setTop] = useState(10);
  const [lowStockThreshold, setLowStockThreshold] = useState(2);
  const [dateRangePreset, setDateRangePreset] = useState<string>("30d");
  const [summary, setSummary] = useState<SalesSummary | null>(null);
  const [topProducts, setTopProducts] = useState<TopProductsResult | null>(null);
  const [inventory, setInventory] = useState<InventoryStatus | null>(null);
  const [dailySales, setDailySales] = useState<DailySale[]>([]);
  const [comparison, setComparison] = useState<Comparison | null>(null);
  const [categoryData, setCategoryData] = useState<CategoryData[]>([]);
  const [genderData, setGenderData] = useState<GenderData[]>([]);
  const [supplierData, setSupplierData] = useState<SupplierData[]>([]);
  const [healthStatus, setHealthStatus] = useState<string>("");
  const [quickInsights, setQuickInsights] = useState<any>(null);
  const [alerts, setAlerts] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [errors, setErrors] = useState<{ summary?: string; topProducts?: string; inventory?: string; health?: string }>({});

  const applyDateRangePreset = (preset: string) => {
    setDateRangePreset(preset);
    const now = new Date();
    const start = new Date(now);
    const end = new Date(now);
    end.setHours(23, 59, 59, 999);
    
    switch (preset) {
      case "today":
        start.setHours(0, 0, 0, 0);
        break;
      case "yesterday":
        start.setDate(now.getDate() - 1);
        start.setHours(0, 0, 0, 0);
        end.setDate(now.getDate() - 1);
        end.setHours(23, 59, 59, 999);
        break;
      case "7d":
        start.setDate(now.getDate() - 7);
        start.setHours(0, 0, 0, 0);
        break;
      case "30d":
        start.setDate(now.getDate() - 30);
        start.setHours(0, 0, 0, 0);
        break;
      case "90d":
        start.setDate(now.getDate() - 90);
        start.setHours(0, 0, 0, 0);
        break;
      case "thisMonth":
        start.setDate(1);
        start.setHours(0, 0, 0, 0);
        break;
      case "lastMonth":
        start.setMonth(now.getMonth() - 1, 1);
        start.setHours(0, 0, 0, 0);
        end.setMonth(now.getMonth(), 0);
        end.setHours(23, 59, 59, 999);
        break;
      case "custom":
        return;
      default:
        return;
    }
    
    setFromDate(start.toISOString().slice(0, 16));
    setToDate(end.toISOString().slice(0, 16));
  };

  // Initialize with 30d preset on mount
  useEffect(() => {
    applyDateRangePreset("30d");
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    setErrors({});
    setHealthStatus("");
    const newErrors: typeof errors = {};

    const API = import.meta.env.VITE_API_BASE_URL;

    try {
      const health = await checkAnalyticsHealth();
      setHealthStatus(`✅ Analytics baza: ${health.tables.salesFacts} prodaja, ${health.tables.salesLineFacts} stavki, ${health.tables.productsDim} proizvoda`);
    } catch (e: unknown) {
      console.error("Health check greška:", e);
      newErrors.health = e instanceof Error ? e.message : "Provera zdravlja nije uspela";
      setHealthStatus("");
    }

    try {
      const s = await getSalesSummary(fromDate || undefined, toDate || undefined);
      setSummary(s);
    } catch (e: unknown) {
      console.error("Summary greška:", e);
      newErrors.summary = e instanceof Error ? e.message : "Greška pri učitavanju sažetka prodaje";
      setSummary(null);
    }

    try {
      const t = await getTopProducts(top, fromDate || undefined, toDate || undefined);
      setTopProducts(t);
    } catch (e: unknown) {
      console.error("Top products greška:", e);
      newErrors.topProducts = e instanceof Error ? e.message : "Greška pri učitavanju top proizvoda";
      setTopProducts(null);
    }

    try {
      const i = await getInventoryStatus(lowStockThreshold);
      setInventory(i);
    } catch (e: unknown) {
      console.error("Inventory greška:", e);
      newErrors.inventory = e instanceof Error ? e.message : "Greška pri učitavanju statusa zaliha";
      setInventory(null);
    }

    try {
      const params = new URLSearchParams();
      if (fromDate) params.append("fromDate", fromDate);
      if (toDate) params.append("toDate", toDate);
      
      const res = await fetch(`${API}/api/analytics/sales/daily?${params.toString()}`);
      if (res.ok) {
        const data = await res.json();
        setDailySales(data);
      }
    } catch (e) {
      console.error("Daily sales greška:", e);
    }

    try {
      const params = new URLSearchParams();
      if (fromDate) params.append("fromDate", fromDate);
      if (toDate) params.append("toDate", toDate);
      
      const res = await fetch(`${API}/api/analytics/sales/comparison?${params.toString()}`);
      if (res.ok) {
        const data = await res.json();
        setComparison(data);
      }
    } catch (e) {
      console.error("Comparison greška:", e);
    }

    try {
      const params = new URLSearchParams();
      if (fromDate) params.append("fromDate", fromDate);
      if (toDate) params.append("toDate", toDate);
      
      const res = await fetch(`${API}/api/analytics/sales/by-category?${params.toString()}`);
      if (res.ok) {
        const data = await res.json();
        setCategoryData(data);
      }
    } catch (e) {
      console.error("Category data greška:", e);
    }

    try {
      const params = new URLSearchParams();
      if (fromDate) params.append("fromDate", fromDate);
      if (toDate) params.append("toDate", toDate);
      
      const res = await fetch(`${API}/api/analytics/sales/by-gender?${params.toString()}`);
      if (res.ok) {
        const data = await res.json();
        setGenderData(data);
      }
    } catch (e) {
      console.error("Gender data greška:", e);
    }

    // Supplier Data
    try {
      const params = new URLSearchParams();
      if (fromDate) params.append("fromDate", fromDate);
      if (toDate) params.append("toDate", toDate);
      
      const res = await fetch(`${API}/api/analytics/sales/by-supplier?${params.toString()}`);
      if (res.ok) {
        const data = await res.json();
        setSupplierData(data);
      }
    } catch (e) {
      console.error("Supplier data greška:", e);
    }

    // Quick Insights
    try {
      const params = new URLSearchParams();
      if (fromDate) params.append("fromDate", fromDate);
      if (toDate) params.append("toDate", toDate);
      
      const res = await fetch(`${API}/api/analytics/quick-insights?${params.toString()}`);
      if (res.ok) {
        const data = await res.json();
        setQuickInsights(data);
      }
    } catch (e) {
      console.error("Quick insights greška:", e);
    }

    // Alerts
    try {
      const res = await fetch(`${API}/api/analytics/alerts`);
      if (res.ok) {
        const data = await res.json();
        setAlerts(data);
      }
    } catch (e) {
      console.error("Alerts greška:", e);
    }

    setErrors(newErrors);
    setLoading(false);
  }, [fromDate, toDate, top, lowStockThreshold]);

  useEffect(() => {
    if (fromDate && toDate) {
      load();
    }
  }, [load]);

  const formatCurrency = (x: number) =>
    x.toLocaleString("sr-RS", { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + " RSD";

  const formatDate = (dateStr: string) => {
    const date = new Date(dateStr);
    return date.toLocaleDateString("sr-RS", { day: "2-digit", month: "2-digit" });
  };

  const hasAnyData = summary || topProducts || inventory;
  const hasErrors = Object.keys(errors).length > 0;

  const renderTrendIndicator = (change: number | undefined) => {
    if (!change) return null;
    const isPositive = change > 0;
    return (
      <div style={{ 
        fontSize: 13, 
        color: isPositive ? "#059669" : "#dc2626",
        display: "flex",
        alignItems: "center",
        gap: 4,
        marginTop: 4
      }}>
        {isPositive ? "↗️" : "↘️"}
        {Math.abs(change).toFixed(1)}% vs prethodni period
      </div>
    );
  };

  return (
    <div className="card" style={{ maxWidth: 1400 }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 24 }}>
        <h2 style={{ fontSize: 24, fontWeight: 700, margin: 0 }}>📈 Analitika - Pregled</h2>
        
        {/* Date Range Selector */}
        <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
          <select
            value={dateRangePreset}
            onChange={(e) => applyDateRangePreset(e.target.value)}
            style={{
              padding: "10px 16px",
              fontSize: 14,
              fontWeight: 500,
              border: "2px solid #e5e7eb",
              borderRadius: 8,
              background: "white",
              cursor: "pointer",
              minWidth: 200,
              transition: "all 0.2s"
            }}
            onMouseEnter={(e) => e.currentTarget.style.borderColor = "#059669"}
            onMouseLeave={(e) => e.currentTarget.style.borderColor = "#e5e7eb"}
          >
            <option value="today">📅 Danas</option>
            <option value="yesterday">📅 Juče</option>
            <option value="7d">📅 Poslednjih 7 dana</option>
            <option value="30d">📅 Poslednjih 30 dana</option>
            <option value="90d">📅 Poslednjih 90 dana</option>
            <option value="thisMonth">📅 Ovaj mesec</option>
            <option value="lastMonth">📅 Prošli mesec</option>
            <option value="custom">⚙️ Prilagođeni period</option>
          </select>
          
          <button 
            onClick={load}
            style={{
              padding: "10px 20px",
              fontSize: 14,
              fontWeight: 600,
              background: "#059669",
              color: "white",
              border: "none",
              borderRadius: 8,
              cursor: "pointer",
              display: "flex",
              alignItems: "center",
              gap: 6,
              transition: "all 0.2s"
            }}
            onMouseEnter={(e) => e.currentTarget.style.background = "#047857"}
            onMouseLeave={(e) => e.currentTarget.style.background = "#059669"}
          >
            🔄 Osveži
          </button>
        </div>
      </div>

      {healthStatus && (
        <div style={{
          background: "#f0fdf4",
          border: "2px solid #059669",
          borderRadius: 8,
          padding: 12,
          marginBottom: 16,
          fontSize: 14,
          color: "#047857"
        }}>
          {healthStatus}
        </div>
      )}

      {/* Quick Insights Widget */}
      {!loading && quickInsights && (
        <div style={{ 
          background: "linear-gradient(135deg, #667eea 0%, #764ba2 100%)", 
          borderRadius: 12, 
          padding: 20, 
          marginBottom: 20,
          color: "white",
          boxShadow: "0 4px 6px rgba(0,0,0,0.1)"
        }}>
          <h3 style={{ margin: "0 0 16px 0", fontSize: 18, fontWeight: 700, display: "flex", alignItems: "center", gap: 8 }}>
            💡 Brzi uvidi
          </h3>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: 16 }}>
            {quickInsights.bestDay && (
              <div>
                <div style={{ fontSize: 13, opacity: 0.9, marginBottom: 4 }}>🏆 Najbolji dan</div>
                <div style={{ fontSize: 18, fontWeight: 700 }}>
                  {quickInsights.bestDay}
                </div>
                <div style={{ fontSize: 14, opacity: 0.8 }}>
                  {formatCurrency(quickInsights.bestDayRevenue)}
                </div>
              </div>
            )}
            {quickInsights.topProduct && (
              <div>
                <div style={{ fontSize: 13, opacity: 0.9, marginBottom: 4 }}>📈 Top proizvod</div>
                <div style={{ fontSize: 18, fontWeight: 700 }}>
                  {quickInsights.topProduct}
                </div>
              </div>
            )}
            {quickInsights.lowStockAlert > 0 && (
              <div>
                <div style={{ fontSize: 13, opacity: 0.9, marginBottom: 4 }}>⚠️ Upozorenje</div>
                <div style={{ fontSize: 18, fontWeight: 700 }}>
                  {quickInsights.lowStockAlert} proizvoda
                </div>
                <div style={{ fontSize: 14, opacity: 0.8 }}>
                  ispod minimuma
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Alerts & Notifications */}
      {!loading && alerts.length > 0 && (
        <div style={{ marginBottom: 20 }}>
          <h3 style={{ fontSize: 18, fontWeight: 700, marginBottom: 12 }}>🔔 Obaveštenja</h3>
          <div style={{ display: "grid", gap: 12 }}>
            {alerts.map((alert, index) => (
              <div 
                key={index}
                style={{ 
                  background: alert.type === "error" ? "#fef2f2" : alert.type === "warning" ? "#fffbeb" : "#f0fdf4",
                  border: `2px solid ${alert.type === "error" ? "#dc2626" : alert.type === "warning" ? "#f59e0b" : "#059669"}`,
                  borderRadius: 8,
                  padding: 16,
                  display: "flex",
                  alignItems: "flex-start",
                  gap: 12
                }}
              >
                <span style={{ fontSize: 24 }}>{alert.icon}</span>
                <div style={{ flex: 1 }}>
                  <div style={{ fontWeight: 700, fontSize: 15, marginBottom: 4, color: "#111827" }}>
                    {alert.title}
                  </div>
                  <div style={{ fontSize: 14, color: "#6b7280" }}>
                    {alert.message}
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Export Buttons */}
      {!loading && summary && (
        <div style={{ 
          display: "flex", 
          gap: 12, 
          marginBottom: 20, 
          padding: 16, 
          background: "#f9fafb", 
          borderRadius: 8,
          alignItems: "center"
        }}>
          <span style={{ fontWeight: 600, fontSize: 14, color: "#374151" }}>📄 Izvezi izveštaj:</span>
          <button 
            onClick={() => {
              const params = new URLSearchParams();
              if (fromDate) params.append("fromDate", fromDate);
              if (toDate) params.append("toDate", toDate);
              window.open(`${import.meta.env.VITE_API_BASE_URL}/api/analytics/export?${params.toString()}`, '_blank');
            }}
            style={{
              padding: "8px 16px",
              fontSize: 14,
              fontWeight: 600,
              background: "#059669",
              color: "white",
              border: "none",
              borderRadius: 6,
              cursor: "pointer",
              display: "flex",
              alignItems: "center",
              gap: 6
            }}
          >
            📊 CSV
          </button>
          <button 
            onClick={async () => {
              try {
                const XLSX = await import('xlsx');
                
                // Prepare data
                const data: any[] = [];
                
                // Summary row
                data.push(['SAŽETAK PRODAJE']);
                data.push(['Ukupan promet', summary.totalRevenue]);
                data.push(['Ukupno transakcija', summary.totalTransactions]);
                data.push(['Prodate jedinice', summary.totalUnits]);
                data.push(['Prosečna korpa', summary.avgBasketValue]);
                data.push([]);
                
                // Daily sales
                if (dailySales.length > 0) {
                  data.push(['DNEVNA PRODAJA']);
                  data.push(['Datum', 'Promet', 'Transakcije', 'Jedinice']);
                  dailySales.forEach(sale => {
                    data.push([sale.date, sale.totalRevenue, sale.transactionCount, sale.totalUnits]);
                  });
                  data.push([]);
                }
                
                // Category data
                if (categoryData.length > 0) {
                  data.push(['PRODAJA PO KATEGORIJAMA']);
                  data.push(['Kategorija', 'Promet', 'Jedinice']);
                  categoryData.forEach(cat => {
                    data.push([cat.kategorija, cat.totalRevenue, cat.totalUnits]);
                  });
                  data.push([]);
                }
                
                // Top products
                if (topProducts) {
                  data.push(['TOP PROIZVODI PO PROMETU']);
                  data.push(['Artikal', 'Promet', 'Kom']);
                  topProducts.byRevenue.forEach(p => {
                    data.push([p.productName, p.totalRevenue, p.totalUnits]);
                  });
                  data.push([]);
                  
                  data.push(['TOP PROIZVODI PO KOLIČINI']);
                  data.push(['Artikal', 'Kom', 'Promet']);
                  topProducts.byUnits.forEach(p => {
                    data.push([p.productName, p.totalUnits, p.totalRevenue]);
                  });
                }
                
                // Create workbook and worksheet
                const ws = XLSX.utils.aoa_to_sheet(data);
                const wb = XLSX.utils.book_new();
                XLSX.utils.book_append_sheet(wb, ws, 'Analitika');
                
                // Set column widths
                ws['!cols'] = [
                  { wch: 30 },
                  { wch: 15 },
                  { wch: 15 },
                  { wch: 15 }
                ];
                
                // Download
                XLSX.writeFile(wb, `analytics_${new Date().toISOString().slice(0, 10)}.xlsx`);
              } catch (error) {
                console.error('Excel export error:', error);
                alert('Greška pri exportu Excel fajla. Molimo pokušajte ponovo.');
              }
            }}
            style={{
              padding: "8px 16px",
              fontSize: 14,
              fontWeight: 600,
              background: "#10b981",
              color: "white",
              border: "none",
              borderRadius: 6,
              cursor: "pointer",
              display: "flex",
              alignItems: "center",
              gap: 6
            }}
          >
            📗 Excel
          </button>
          <button 
            onClick={() => {
              alert("PDF export - Coming soon! Za sada koristite CSV ili Excel format.");
            }}
            style={{
              padding: "8px 16px",
              fontSize: 14,
              fontWeight: 600,
              background: "#3b82f6",
              color: "white",
              border: "none",
              borderRadius: 6,
              cursor: "pointer",
              display: "flex",
              alignItems: "center",
              gap: 6,
              opacity: 0.7
            }}
          >
            📄 PDF (uskoro)
          </button>
        </div>
      )}

      {hasErrors && (
        <div style={{ 
          background: "#fef2f2", 
          border: "2px solid #dc2626", 
          borderRadius: 8, 
          padding: 16,
          marginBottom: 20 
        }}>
          <div style={{ fontWeight: 600, color: "#dc2626", marginBottom: 8 }}>
            ⚠️ {errors.health ? "Problem sa povezivanjem na backend" : "Analytics tabele nisu kreirane ili nema podataka"}
          </div>
          
          {errors.health && (
            <div style={{ fontSize: 14, color: "#7f1d1d", marginBottom: 12, fontFamily: "monospace" }}>
              {errors.health}
            </div>
          )}

          {!errors.health && (
            <>
              <div style={{ fontSize: 14, color: "#6b7280", marginBottom: 8 }}>
                Potrebno je pokrenuti SQL skriptu na analytics bazi:
              </div>
              <code style={{ 
                display: "block", 
                background: "#1f2937", 
                color: "#e5e7eb", 
                padding: 12,
                borderRadius: 6,
                fontSize: 13,
                fontFamily: "monospace",
                marginBottom: 12
              }}>
                psql -d your_analytics_db -f Database/Analytics/001_CreateSalesFactTables.sql
              </code>
            </>
          )}
          
          {errors.summary && (
            <div style={{ fontSize: 13, color: "#7f1d1d", marginBottom: 4 }}>
              • Sažetak prodaje: {errors.summary}
            </div>
          )}
          {errors.topProducts && (
            <div style={{ fontSize: 13, color: "#7f1d1d", marginBottom: 4 }}>
              • Top proizvodi: {errors.topProducts}
            </div>
          )}
          {errors.inventory && (
            <div style={{ fontSize: 13, color: "#7f1d1d", marginBottom: 4 }}>
              • Zalihe: {errors.inventory}
            </div>
          )}
        </div>
      )}

      {/* Custom Date Range (only visible when custom is selected) */}
      {dateRangePreset === "custom" && (
        <div style={{ 
          background: "#f9fafb", 
          padding: 16, 
          borderRadius: 8, 
          marginBottom: 20,
          border: "2px solid #e5e7eb"
        }}>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr auto", gap: 12, alignItems: "end" }}>
            <div>
              <label style={{ display: "block", fontSize: 13, fontWeight: 600, marginBottom: 6, color: "#374151" }}>
                Od datuma
              </label>
              <input 
                type="datetime-local" 
                value={fromDate} 
                onChange={(e) => setFromDate(e.target.value)}
                style={{
                  width: "100%",
                  padding: "10px 12px",
                  fontSize: 14,
                  border: "2px solid #e5e7eb",
                  borderRadius: 8
                }}
              />
            </div>
            <div>
              <label style={{ display: "block", fontSize: 13, fontWeight: 600, marginBottom: 6, color: "#374151" }}>
                Do datuma
              </label>
              <input 
                type="datetime-local" 
                value={toDate} 
                onChange={(e) => setToDate(e.target.value)}
                style={{
                  width: "100%",
                  padding: "10px 12px",
                  fontSize: 14,
                  border: "2px solid #e5e7eb",
                  borderRadius: 8
                }}
              />
            </div>
            <button
              onClick={load}
              style={{
                padding: "10px 20px",
                fontSize: 14,
                fontWeight: 600,
                background: "#3b82f6",
                color: "white",
                border: "none",
                borderRadius: 8,
                cursor: "pointer"
              }}
            >
              Primeni
            </button>
          </div>
        </div>
      )}

      {loading && <p style={{ textAlign: "center", padding: "2rem" }}>Učitavanje...</p>}

      {!loading && !hasAnyData && !hasErrors && (
        <div style={{ textAlign: "center", padding: "3rem", color: "#6b7280" }}>
          <div style={{ fontSize: 48, marginBottom: 16 }}>📊</div>
          <div style={{ fontSize: 18, fontWeight: 600, marginBottom: 8 }}>Nema podataka</div>
          <div style={{ fontSize: 14 }}>Kreirajte prodaju da bi se pojavili podaci u analytics dashboard-u.</div>
        </div>
      )}

      {!loading && summary && (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: 12, marginBottom: 20 }}>
          <div className="card" style={{ margin: 0, border: "2px solid #059669" }}>
            <div style={{ color: "#6b7280", fontSize: 13 }}>💰 Ukupan promet</div>
            <div style={{ fontSize: 22, fontWeight: 800, color: "#059669" }}>{formatCurrency(summary.totalRevenue)}</div>
            {renderTrendIndicator(comparison?.change?.revenue)}
          </div>
          <div className="card" style={{ margin: 0, border: "2px solid #3b82f6" }}>
            <div style={{ color: "#6b7280", fontSize: 13 }}>🛒 Transakcije</div>
            <div style={{ fontSize: 22, fontWeight: 800, color: "#3b82f6" }}>{summary.totalTransactions}</div>
            {renderTrendIndicator(comparison?.change?.transactions)}
          </div>
          <div className="card" style={{ margin: 0, border: "2px solid #8b5cf6" }}>
            <div style={{ color: "#6b7280", fontSize: 13 }}>📦 Prodate jedinice</div>
            <div style={{ fontSize: 22, fontWeight: 800, color: "#8b5cf6" }}>{summary.totalUnits}</div>
            {renderTrendIndicator(comparison?.change?.units)}
          </div>
          <div className="card" style={{ margin: 0, border: "2px solid #f59e0b" }}>
            <div style={{ color: "#6b7280", fontSize: 13 }}>🛍️ Prosečna korpa</div>
            <div style={{ fontSize: 22, fontWeight: 800, color: "#f59e0b" }}>{formatCurrency(summary.avgBasketValue)}</div>
          </div>
          <div className="card" style={{ margin: 0, border: "2px solid #ec4899" }}>
            <div style={{ color: "#6b7280", fontSize: 13 }}>💵 Prosečna cena artikla</div>
            <div style={{ fontSize: 22, fontWeight: 800, color: "#ec4899" }}>{formatCurrency(summary.avgItemPrice)}</div>
          </div>
        </div>
      )}

      {!loading && dailySales.length > 0 && (
        <div className="card" style={{ marginBottom: 20 }}>
          <h3 className="text-lg font-semibold" style={{ marginBottom: 16 }}>📈 Dnevna prodaja</h3>
          <ResponsiveContainer width="100%" height={300}>
            <LineChart data={dailySales}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="date" tickFormatter={formatDate} />
              <YAxis />
              <Tooltip 
                formatter={(value, name) => {
                  if (name === "totalRevenue") return [formatCurrency(Number(value)), "Promet"];
                  if (name === "transactionCount") return [value, "Transakcije"];
                  if (name === "totalUnits") return [value, "Jedinice"];
                  return [value, String(name)];
                }}
                labelFormatter={(label) => `Datum: ${label}`}
              />
              <Legend />
              <Line type="monotone" dataKey="totalRevenue" stroke="#059669" name="Promet" strokeWidth={2} />
              <Line type="monotone" dataKey="transactionCount" stroke="#3b82f6" name="Transakcije" strokeWidth={2} />
            </LineChart>
          </ResponsiveContainer>
        </div>
      )}

      {!loading && (categoryData.length > 0 || genderData.length > 0) && (
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 20, marginBottom: 20 }}>
          {categoryData.length > 0 && (
            <div className="card" style={{ margin: 0 }}>
              <h3 className="text-lg font-semibold" style={{ marginBottom: 16 }}>🎯 Prodaja po kategorijama</h3>
              <ResponsiveContainer width="100%" height={350}>
                <PieChart>
                  <Pie
                    data={categoryData as unknown as any[]}
                    dataKey="totalRevenue"
                    nameKey="kategorija"
                    cx="50%"
                    cy="50%"
                    outerRadius={100}
                    label={(entry: CategoryData) => `${entry.kategorija}: ${((entry.totalRevenue / categoryData.reduce((sum, item) => sum + item.totalRevenue, 0)) * 100).toFixed(1)}%`}
                  >
                    {categoryData.map((entry, index) => (
                      <Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />
                    ))}
                  </Pie>
                  <Tooltip 
                    formatter={(value: any) => formatCurrency(value)}
                    contentStyle={{ background: 'white', border: '1px solid #e5e7eb', borderRadius: 8, padding: 12 }}
                  />
                  <Legend />
                </PieChart>
              </ResponsiveContainer>
              <div style={{ marginTop: 16 }}>
                <table className="table" style={{ fontSize: '0.875rem' }}>
                  <thead>
                    <tr>
                      <th>Kategorija</th>
                      <th style={{ textAlign: 'right' }}>Promet</th>
                      <th style={{ textAlign: 'right' }}>Kom</th>
                    </tr>
                  </thead>
                  <tbody>
                    {categoryData.map((cat, idx) => (
                      <tr key={idx}>
                        <td>
                          <span style={{ 
                            display: 'inline-block', 
                            width: 12, 
                            height: 12, 
                            background: COLORS[idx % COLORS.length], 
                            borderRadius: '50%',
                            marginRight: 8
                          }}></span>
                          {cat.kategorija}
                        </td>
                        <td style={{ textAlign: 'right', fontWeight: 600 }}>{formatCurrency(cat.totalRevenue)}</td>
                        <td style={{ textAlign: 'right' }}>{cat.totalUnits}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {genderData.length > 0 && (
            <div className="card" style={{ margin: 0 }}>
              <h3 className="text-lg font-semibold" style={{ marginBottom: 16 }}>👥 Prodaja po polu</h3>
              <ResponsiveContainer width="100%" height={350}>
                <PieChart>
                  <Pie
                    data={genderData as unknown as any[]}
                    dataKey="totalRevenue"
                    nameKey="pol"
                    cx="50%"
                    cy="50%"
                    outerRadius={100}
                    label={(entry: GenderData) => `${entry.pol}: ${((entry.totalRevenue / genderData.reduce((sum, item) => sum + item.totalRevenue, 0)) * 100).toFixed(1)}%`}
                  >
                    {genderData.map((entry, index) => (
                      <Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />
                    ))}
                  </Pie>
                  <Tooltip 
                    formatter={(value: any) => formatCurrency(value)}
                    contentStyle={{ background: 'white', border: '1px solid #e5e7eb', borderRadius: 8, padding: 12 }}
                  />
                  <Legend />
                </PieChart>
              </ResponsiveContainer>
              <div style={{ marginTop: 16 }}>
                <table className="table" style={{ fontSize: '0.875rem' }}>
                  <thead>
                    <tr>
                      <th>Pol</th>
                      <th style={{ textAlign: 'right' }}>Promet</th>
                      <th style={{ textAlign: 'right' }}>Kom</th>
                    </tr>
                  </thead>
                  <tbody>
                    {genderData.map((gen, idx) => (
                      <tr key={idx}>
                        <td>
                          <span style={{ 
                            display: 'inline-block', 
                            width: 12, 
                            height: 12, 
                            background: COLORS[idx % COLORS.length], 
                            borderRadius: '50%',
                            marginRight: 8
                          }}></span>
                          {gen.pol}
                        </td>
                        <td style={{ textAlign: 'right', fontWeight: 600 }}>{formatCurrency(gen.totalRevenue)}</td>
                        <td style={{ textAlign: 'right' }}>{gen.totalUnits}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>
      )}

      {/* NEW: Sales by Supplier */}
      {!loading && supplierData.length > 0 && (
        <div className="card" style={{ marginBottom: 20 }}>
          <h3 className="text-lg font-semibold" style={{ marginBottom: 16 }}>🏢 Prodaja po dobavljačima</h3>
          
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 20 }}>
            {/* Pie Chart */}
            <ResponsiveContainer width="100%" height={350}>
              <PieChart>
                <Pie
                  data={supplierData as unknown as any[]}
                  dataKey="totalRevenue"
                  nameKey="dobavljacNaziv"
                  cx="50%"
                  cy="50%"
                  outerRadius={100}
                  label={(entry: SupplierData) => `${entry.dobavljacNaziv}: ${((entry.totalRevenue / supplierData.reduce((sum, item) => sum + item.totalRevenue, 0)) * 100).toFixed(1)}%`}
                >
                  {supplierData.map((entry, index) => (
                    <Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />
                  ))}
                </Pie>
                <Tooltip 
                  formatter={(value: any) => formatCurrency(value)}
                  contentStyle={{ background: 'white', border: '1px solid #e5e7eb', borderRadius: 8, padding: 12 }}
                />
                <Legend />
              </PieChart>
            </ResponsiveContainer>

            {/* Table */}
            <div style={{ overflowX: 'auto' }}>
              <table className="table" style={{ fontSize: '0.875rem' }}>
                <thead>
                  <tr>
                    <th>Dobavljač</th>
                    <th style={{ textAlign: 'right' }}>Promet</th>
                    <th style={{ textAlign: 'right' }}>Kom</th>
                    <th style={{ textAlign: 'right' }}>Transakcije</th>
                  </tr>
                </thead>
                <tbody>
                  {supplierData.map((sup, idx) => (
                    <tr key={idx}>
                      <td>
                        <span style={{ 
                          display: 'inline-block', 
                          width: 12, 
                          height: 12, 
                          background: COLORS[idx % COLORS.length], 
                          borderRadius: '50%',
                          marginRight: 8
                        }}></span>
                        {sup.dobavljacNaziv}
                      </td>
                      <td style={{ textAlign: 'right', fontWeight: 600 }}>{formatCurrency(sup.totalRevenue)}</td>
                      <td style={{ textAlign: 'right' }}>{sup.totalUnits}</td>
                      <td style={{ textAlign: 'right' }}>{sup.transactionCount}</td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr style={{ background: '#f9fafb', fontWeight: 700 }}>
                    <td>UKUPNO</td>
                    <td style={{ textAlign: 'right' }}>{formatCurrency(supplierData.reduce((sum, s) => sum + s.totalRevenue, 0))}</td>
                    <td style={{ textAlign: 'right' }}>{supplierData.reduce((sum, s) => sum + s.totalUnits, 0)}</td>
                    <td style={{ textAlign: 'right' }}>{supplierData.reduce((sum, s) => sum + s.transactionCount, 0)}</td>
                  </tr>
                </tfoot>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* Inventory */}
      {!loading && inventory && (
        <div style={{ marginBottom: 20 }}>
          <h3 className="text-lg font-semibold" style={{ marginBottom: 10 }}>📦 Brzi pregled zaliha</h3>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: 12 }}>
            <div className="card" style={{ margin: 0 }}>
              <div style={{ color: "#6b7280", fontSize: 13 }}>Broj SKU</div>
              <div style={{ fontSize: 20, fontWeight: 800 }}>{inventory.totalSkuCount}</div>
            </div>
            <div className="card" style={{ margin: 0 }}>
              <div style={{ color: "#6b7280", fontSize: 13 }}>Ukupno na stanju</div>
              <div style={{ fontSize: 20, fontWeight: 800 }}>{inventory.totalOnHand}</div>
            </div>
            <div className="card" style={{ margin: 0 }}>
              <div style={{ color: "#6b7280", fontSize: 13 }}>Niske zalihe</div>
              <div style={{ fontSize: 20, fontWeight: 800, color: "#f59e0b" }}>{inventory.lowStockCount}</div>
            </div>
            <div className="card" style={{ margin: 0 }}>
              <div style={{ color: "#6b7280", fontSize: 13 }}>Bez zaliha</div>
              <div style={{ fontSize: 20, fontWeight: 800, color: "#dc2626" }}>{inventory.outOfStockCount}</div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

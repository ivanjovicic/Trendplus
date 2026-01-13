import React, { useCallback, useEffect, useState } from "react";
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer, PieChart, Pie, Cell, AreaChart, Area, LineChart, Line } from 'recharts';
import { getInventoryStatus, getTopProducts } from "../services/analyticsApi";
import type { InventoryStatus, TopProductsResult } from "../types/analytics";

interface TransactionStats {
  avgItemsPerTransaction: number;
  avgTransactionValue: number;
  totalTransactions: number;
}

interface PaymentMethodData {
  nacinPlacanja: string;
  totalRevenue: number;
  transactionCount: number;
  [key: string]: string | number;
}

interface WeekdayData {
  dayOfWeek: string;
  dayName: string;
  totalRevenue: number;
  transactionCount: number;
  [key: string]: string | number;
}

interface HourData {
  hour: number;
  totalRevenue: number;
  transactionCount: number;
  [key: string]: number;
}

interface ReorderSuggestion {
  id: number;
  naziv: string;
  kolicina: number;
  minimalnaKolicina: number;
  kategorija: string;
  nabavnaCena: number;
}

interface DailySalesComparison {
  date: string;
  totalRevenue: number;
  transactionCount: number;
  avgBasketValue: number;
}

interface CategoryTrend {
  date: string;
  [key: string]: string | number; // Allow both string (date) and number (revenue values)
}

const COLORS = ['#059669', '#3b82f6', '#f59e0b', '#ec4899', '#8b5cf6', '#10b981', '#6366f1', '#f97316'];

export default function AnalyticsDetails() {
  const [fromDate, setFromDate] = useState<string>("");
  const [toDate, setToDate] = useState<string>("");
  const [top, setTop] = useState(10);
  const [lowStockThreshold, setLowStockThreshold] = useState(2);
  const [dateRangePreset, setDateRangePreset] = useState<string>("30d");
  const [topProducts, setTopProducts] = useState<TopProductsResult | null>(null);
  const [inventory, setInventory] = useState<InventoryStatus | null>(null);
  const [transactionStats, setTransactionStats] = useState<TransactionStats | null>(null);
  const [paymentData, setPaymentData] = useState<PaymentMethodData[]>([]);
  const [weekdayData, setWeekdayData] = useState<WeekdayData[]>([]);
  const [hourData, setHourData] = useState<HourData[]>([]);
  const [reorderSuggestions, setReorderSuggestions] = useState<ReorderSuggestion[]>([]);
  const [dailySalesComparison, setDailySalesComparison] = useState<DailySalesComparison[]>([]);
  const [categoryTrends, setCategoryTrends] = useState<CategoryTrend[]>([]);
  const [loading, setLoading] = useState(true);
  const [errors, setErrors] = useState<{ topProducts?: string; inventory?: string }>({});

  const applyDateRangePreset = (preset: string) => {
    setDateRangePreset(preset);
    const now = new Date();
    const start = new Date(now);
    const end = new Date(now);
    end.setHours(23, 59, 59, 999);
    
    console.log("📅 applyDateRangePreset - preset:", preset, "now:", now.toISOString());
    
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
    
    const startISO = start.toISOString().slice(0, 16);
    const endISO = end.toISOString().slice(0, 16);
    
    console.log("📅 Setting dates - start:", startISO, "end:", endISO);
    
    setFromDate(startISO);
    setToDate(endISO);
  };

  useEffect(() => {
    // Set a very wide date range to catch all sales
    const now = new Date();
    const start = new Date('2020-01-01'); // Far in the past
    const end = new Date(now);
    end.setHours(23, 59, 59, 999);
    
    setFromDate(start.toISOString().slice(0, 16));
    setToDate(end.toISOString().slice(0, 16));
    
    console.log("🚀 Initial date range set:", {
      from: start.toISOString(),
      to: end.toISOString()
    });
  }, []);

  const load = useCallback(async () => {
    console.log("🔄 Starting load() - fromDate:", fromDate, "toDate:", toDate);
    setLoading(true);
    setErrors({});
    const newErrors: typeof errors = {};

    const API = import.meta.env.VITE_API_BASE_URL;
    console.log("🌐 API Base URL:", API);

    try {
      const t = await getTopProducts(top, fromDate || undefined, toDate || undefined, true); // Dodaj parametar za cached
      setTopProducts(t);
    } catch (e: unknown) {
      console.error("❌ Top products greška:", e);
      newErrors.topProducts = e instanceof Error ? e.message : "Greška pri učitavanju top proizvoda";
      setTopProducts(null);
    }

    try {
      const i = await getInventoryStatus(lowStockThreshold, true); // Dodaj parametar za cached
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
      const res = await fetch(`${API}/api/analytics/cached/sales/transaction-stats?${params.toString()}`); // cached
      if (res.ok) {
        const data = await res.json();
        setTransactionStats(data);
      }
    } catch (e) {
      console.error("Transaction stats greška:", e);
    }

    try {
      const params = new URLSearchParams();
      if (fromDate) params.append("fromDate", fromDate);
      if (toDate) params.append("toDate", toDate);
      const res = await fetch(`${API}/api/analytics/cached/sales/by-payment?${params.toString()}`); // cached
      if (res.ok) {
        const data = await res.json();
        setPaymentData(data);
      }
    } catch (e) {
      console.error("Payment data greška:", e);
    }

    try {
      const params = new URLSearchParams();
      if (fromDate) params.append("fromDate", fromDate);
      if (toDate) params.append("toDate", toDate);
      const res = await fetch(`${API}/api/analytics/cached/sales/by-weekday?${params.toString()}`); // cached
      if (res.ok) {
        const data = await res.json();
        setWeekdayData(data);
      }
    } catch (e) {
      console.error("Weekday data greška:", e);
    }

    try {
      const params = new URLSearchParams();
      if (fromDate) params.append("fromDate", fromDate);
      if (toDate) params.append("toDate", toDate);
      const res = await fetch(`${API}/api/analytics/cached/sales/by-hour?${params.toString()}`); // cached
      if (res.ok) {
        const data = await res.json();
        setHourData(data);
      }
    } catch (e) {
      console.error("Hour data greška:", e);
    }

    try {
      const res = await fetch(`${API}/api/analytics/cached/reorder-suggestions`); // cached
      if (res.ok) {
        const data = await res.json();
        setReorderSuggestions(data);
      }
    } catch (e) {
      console.error("Reorder suggestions greška:", e);
    }

    try {
      const params = new URLSearchParams();
      if (fromDate) params.append("fromDate", fromDate);
      if (toDate) params.append("toDate", toDate);
      const res = await fetch(`${API}/api/analytics/cached/sales/daily?${params.toString()}`); // cached
      if (res.ok) {
        const data: Array<{date: string; totalRevenue: number; transactionCount: number}> = await res.json();
        setDailySalesComparison(data.map((item) => ({
          date: item.date,
          totalRevenue: item.totalRevenue,
          transactionCount: item.transactionCount,
          avgBasketValue: item.totalRevenue / (item.transactionCount || 1)
        })));
      }
    } catch (e) {
      console.error("Daily sales comparison greška:", e);
    }

    try {
      const params = new URLSearchParams();
      if (fromDate) params.append("fromDate", fromDate);
      if (toDate) params.append("toDate", toDate);
      const res = await fetch(`${API}/api/analytics/cached/sales/category-trends?${params.toString()}`); // cached
      if (res.ok) {
        const data = await res.json();
        setCategoryTrends(data);
      }
    } catch (e) {
      console.error("Category trends greška:", e);
    }

    setErrors(newErrors);
    setLoading(false);
  }, [fromDate, toDate, top, lowStockThreshold]);

  useEffect(() => {
    console.log("📅 useEffect triggered - fromDate:", fromDate, "toDate:", toDate);
    if (fromDate && toDate) {
      console.log("✅ Calling load()...");
      load();
    } else {
      console.log("⚠️ Skipping load() - missing dates");
    }
  }, [load]);

  const formatCurrency = (x: number) =>
    x.toLocaleString("sr-RS", { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + " RSD";

  const hasErrors = Object.keys(errors).length > 0;

  return (
    <div className="card" style={{ maxWidth: 1400 }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 24 }}>
        <h2 style={{ fontSize: 24, fontWeight: 700, margin: 0 }}>📊 Detaljne analize</h2>
        
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
              minWidth: 200
            }}
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
          
          <button onClick={load} className="button-big" style={{ background: "#059669", padding: "10px 20px" }}>
            🔄 Osveži
          </button>
        </div>
      </div>

      {hasErrors && (
        <div style={{ background: "#fef2f2", border: "2px solid #dc2626", borderRadius: 8, padding: 16, marginBottom: 20 }}>
          <div style={{ fontWeight: 600, color: "#dc2626", marginBottom: 8 }}>⚠️ Problem sa učitavanjem podataka</div>
          {errors.topProducts && <div style={{ fontSize: 13, color: "#7f1d1d" }}>• Top proizvodi: {errors.topProducts}</div>}
          {errors.inventory && <div style={{ fontSize: 13, color: "#7f1d1d" }}>• Zalihe: {errors.inventory}</div>}
        </div>
      )}

      {dateRangePreset === "custom" && (
        <div style={{ background: "#f9fafb", padding: 16, borderRadius: 8, marginBottom: 20, border: "2px solid #e5e7eb" }}>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr auto", gap: 12, alignItems: "end" }}>
            <div>
              <label className="field-label">Od datuma</label>
              <input className="input-big" type="datetime-local" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
            </div>
            <div>
              <label className="field-label">Do datuma</label>
              <input className="input-big" type="datetime-local" value={toDate} onChange={(e) => setToDate(e.target.value)} />
            </div>
            <button onClick={load} className="button-big" style={{ background: "#3b82f6" }}>Primeni</button>
          </div>
        </div>
      )}

      <details style={{ marginBottom: 20 }}>
        <summary style={{ cursor: "pointer", padding: "12px 16px", background: "#f9fafb", borderRadius: 8, fontWeight: 600, fontSize: 14 }}>
          ⚙️ Napredna podešavanja
        </summary>
        <div style={{ padding: 16, background: "#f9fafb", borderRadius: 8, marginTop: 8, display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: 12 }}>
          <div>
            <label className="field-label">Top proizvoda</label>
            <input className="input-big" type="number" min={1} max={50} value={top} onChange={(e) => setTop(Number(e.target.value))} />
          </div>
          <div>
            <label className="field-label">Prag za niske zalihe</label>
            <input className="input-big" type="number" min={0} value={lowStockThreshold} onChange={(e) => setLowStockThreshold(Number(e.target.value))} />
          </div>
        </div>
      </details>

      {loading && <p style={{ textAlign: "center", padding: "2rem" }}>Učitavanje...</p>}

      {!loading && !topProducts && (
        <div style={{ padding: 20, background: "#fffbeb", borderRadius: 8, marginBottom: 20 }}>
          <div style={{ fontWeight: 600, color: "#f59e0b" }}>⚠️ Nema top proizvoda</div>
          <div style={{ fontSize: 14, color: "#92400e" }}>
            topProducts state je null ili undefined. Proveri Network tab ili Console za greške.
          </div>
        </div>
      )}

      {!loading && transactionStats && (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: 12, marginBottom: 20 }}>
          <div className="card" style={{ margin: 0, border: "2px solid #3b82f6" }}>
            <div style={{ color: "#6b7280", fontSize: 13 }}>📝 Prosečno artikala po računu</div>
            <div style={{ fontSize: 22, fontWeight: 800, color: "#3b82f6" }}>{transactionStats.avgItemsPerTransaction.toFixed(1)}</div>
          </div>
          <div className="card" style={{ margin: 0, border: "2px solid #8b5cf6" }}>
            <div style={{ color: "#6b7280", fontSize: 13 }}>💰 Prosečna vrednost računa</div>
            <div style={{ fontSize: 22, fontWeight: 800, color: "#8b5cf6" }}>{formatCurrency(transactionStats.avgTransactionValue)}</div>
          </div>
          <div className="card" style={{ margin: 0, border: "2px solid #059669" }}>
            <div style={{ color: "#6b7280", fontSize: 13 }}>🧾 Ukupno računa</div>
            <div style={{ fontSize: 22, fontWeight: 800, color: "#059669" }}>{transactionStats.totalTransactions}</div>
          </div>
        </div>
      )}

      {!loading && (paymentData.length > 0 || weekdayData.length > 0) && (
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 20, marginBottom: 20 }}>
          {paymentData.length > 0 && (
            <div className="card" style={{ margin: 0 }}>
              <h3 style={{ marginBottom: 16 }}>💳 Prodaja po načinu plaćanja</h3>
              <ResponsiveContainer width="100%" height={300}>
                <PieChart>
                  <Pie 
                    data={paymentData} 
                    dataKey="totalRevenue" 
                    nameKey="nacinPlacanja" 
                    cx="50%" 
                    cy="50%" 
                    outerRadius={80}
                    label={({ name, percent }) => `${name}: ${((percent ?? 0) * 100).toFixed(1)}%`}
                  >
                    {paymentData.map((_, index) => (<Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />))}
                  </Pie>
                  <Tooltip formatter={(value) => formatCurrency(Number(value))} />
                  <Legend />
                </PieChart>
              </ResponsiveContainer>
            </div>
          )}

          {weekdayData.length > 0 && (
            <div className="card" style={{ margin: 0 }}>
              <h3 style={{ marginBottom: 16 }}>📅 Prodaja po danima u nedelji</h3>
              <ResponsiveContainer width="100%" height={300}>
                <BarChart data={weekdayData}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="dayName" />
                  <YAxis />
                  <Tooltip formatter={(value, name) => {
                    if (name === "totalRevenue") return [formatCurrency(Number(value)), "Promet"];
                    return [value, "Transakcije"];
                  }} />
                  <Legend />
                  <Bar dataKey="totalRevenue" fill="#059669" name="Promet" />
                </BarChart>
              </ResponsiveContainer>
            </div>
          )}
        </div>
      )}

      {!loading && hourData.length > 0 && (
        <div className="card" style={{ marginBottom: 20 }}>
          <h3 style={{ marginBottom: 16 }}>⏰ Prodaja po satima (Peak Hours)</h3>
          <ResponsiveContainer width="100%" height={300}>
            <BarChart data={hourData}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="hour" label={{ value: 'Sat', position: 'insideBottom', offset: -5 }} />
              <YAxis />
              <Tooltip formatter={(value, name) => {
                if (name === "totalRevenue") return [formatCurrency(Number(value)), "Promet"];
                return [value, "Transakcije"];
              }} />
              <Legend />
              <Bar dataKey="totalRevenue" fill="#3b82f6" name="Promet" />
              <Bar dataKey="transactionCount" fill="#f59e0b" name="Transakcije" />
            </BarChart>
          </ResponsiveContainer>
        </div>
      )}

      {!loading && dailySalesComparison.length > 0 && (
        <div className="card" style={{ marginBottom: 20 }}>
          <h3 style={{ marginBottom: 16 }}>📈 Kumulativna prodaja - Trend</h3>
          <ResponsiveContainer width="100%" height={300}>
            <AreaChart data={dailySalesComparison}>
              <defs>
                <linearGradient id="colorRevenue" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="#059669" stopOpacity={0.8}/>
                  <stop offset="95%" stopColor="#059669" stopOpacity={0}/>
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="date" tickFormatter={(date) => new Date(date).toLocaleDateString('sr-RS', { day: '2-digit', month: '2-digit' })} />
              <YAxis />
              <Tooltip 
                formatter={(value, name) => {
                  if (name === "totalRevenue") return [formatCurrency(Number(value)), "Promet"];
                  if (name === "avgBasketValue") return [formatCurrency(Number(value)), "Prosečna korpa"];
                  return [value, String(name)];
                }}
                labelFormatter={(label) => new Date(String(label)).toLocaleDateString('sr-RS')}
              />
              <Legend />
              <Area type="monotone" dataKey="totalRevenue" stroke="#059669" fillOpacity={1} fill="url(#colorRevenue)" name="Promet" />
            </AreaChart>
          </ResponsiveContainer>
        </div>
      )}

      {!loading && dailySalesComparison.length > 0 && (
        <div className="card" style={{ marginBottom: 20 }}>
          <h3 style={{ marginBottom: 16 }}>📊 Poređenje metrika kroz vreme</h3>
          <ResponsiveContainer width="100%" height={300}>
            <LineChart data={dailySalesComparison}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="date" tickFormatter={(date) => new Date(date).toLocaleDateString('sr-RS', { day: '2-digit', month: '2-digit' })} />
              <YAxis yAxisId="left" />
              <YAxis yAxisId="right" orientation="right" />
              <Tooltip 
                formatter={(value, name) => {
                  if (name === "totalRevenue" || name === "avgBasketValue") {
                    return [formatCurrency(Number(value)), String(name)];
                  }
                  return [value, String(name)];
                }}
                labelFormatter={(label) => new Date(String(label)).toLocaleDateString('sr-RS')}
              />
              <Legend />
              <Line yAxisId="left" type="monotone" dataKey="totalRevenue" stroke="#059669" strokeWidth={2} name="Promet" />
              <Line yAxisId="right" type="monotone" dataKey="transactionCount" stroke="#3b82f6" strokeWidth={2} name="Transakcije" />
              <Line yAxisId="left" type="monotone" dataKey="avgBasketValue" stroke="#f59e0b" strokeWidth={2} name="Prosečna korpa" strokeDasharray="5 5" />
            </LineChart>
          </ResponsiveContainer>
        </div>
      )}

      {!loading && categoryTrends.length > 0 && (
        <div className="card" style={{ marginBottom: 20 }}>
          <h3 style={{ marginBottom: 16 }}>🎯 Prodaja po kategorijama kroz vreme</h3>
          <ResponsiveContainer width="100%" height={350}>
            <BarChart data={categoryTrends}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="date" tickFormatter={(date) => new Date(date).toLocaleDateString('sr-RS', { day: '2-digit', month: '2-digit' })} />
              <YAxis />
              <Tooltip 
                formatter={(value) => formatCurrency(Number(value))}
                labelFormatter={(label) => new Date(String(label)).toLocaleDateString('sr-RS')}
              />
              <Legend />
              {categoryTrends.length > 0 && Object.keys(categoryTrends[0])
                .filter(key => key !== 'date')
                .map((category, index) => (
                  <Bar key={category} dataKey={category} stackId="a" fill={COLORS[index % COLORS.length]} />
                ))}
            </BarChart>
          </ResponsiveContainer>
        </div>
      )}

      {!loading && reorderSuggestions.length > 0 && (
        <div className="card" style={{ marginBottom: 20, border: "2px solid #dc2626" }}>
          <h3 style={{ marginBottom: 16, color: "#dc2626" }}>🔔 Preporuke za naručivanje ({reorderSuggestions.length})</h3>
          <div style={{ overflowX: "auto" }}>
            <table className="table">
              <thead>
                <tr>
                  <th>Artikal</th>
                  <th>Kategorija</th>
                  <th style={{ textAlign: "right" }}>Trenutno</th>
                  <th style={{ textAlign: "right" }}>Minimum</th>
                  <th style={{ textAlign: "right" }}>Predlog</th>
                  <th style={{ textAlign: "right" }}>Nabavna cena</th>
                  <th style={{ textAlign: "right" }}>Ukupno</th>
                </tr>
              </thead>
              <tbody>
                {reorderSuggestions.map((item) => {
                  const suggestedQty = Math.max(10, (item.minimalnaKolicina || 5) * 2);
                  const totalCost = suggestedQty * (item.nabavnaCena || 0);
                  return (
                    <tr key={item.id}>
                      <td style={{ fontWeight: 600 }}>{item.naziv}</td>
                      <td>{item.kategorija || "N/A"}</td>
                      <td style={{ textAlign: "right", color: "#dc2626", fontWeight: 700 }}>{item.kolicina}</td>
                      <td style={{ textAlign: "right" }}>{item.minimalnaKolicina || 5}</td>
                      <td style={{ textAlign: "right", fontWeight: 600, color: "#059669" }}>{suggestedQty}</td>
                      <td style={{ textAlign: "right" }}>{formatCurrency(item.nabavnaCena || 0)}</td>
                      <td style={{ textAlign: "right", fontWeight: 700 }}>{formatCurrency(totalCost)}</td>
                    </tr>
                  );
                })}
              </tbody>
              <tfoot>
                <tr style={{ background: "#f9fafb", fontWeight: 700 }}>
                  <td colSpan={6} style={{ textAlign: "right", padding: 12 }}>UKUPAN TROŠAK NARUČIVANJA:</td>
                  <td style={{ textAlign: "right", padding: 12, color: "#059669", fontSize: 16 }}>
                    {formatCurrency(reorderSuggestions.reduce((sum, item) => {
                      const suggestedQty = Math.max(10, (item.minimalnaKolicina || 5) * 2);
                      return sum + suggestedQty * (item.nabavnaCena || 0);
                    }, 0))}
                  </td>
                </tr>
              </tfoot>
            </table>
          </div>
        </div>
      )}

      {!loading && inventory && (
        <div style={{ marginBottom: 20 }}>
          <h3 style={{ marginBottom: 10 }}>📦 Status zaliha</h3>
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
              <div style={{ color: "#6b7280", fontSize: 13 }}>Niske zalihe (prag: {lowStockThreshold})</div>
              <div style={{ fontSize: 20, fontWeight: 800, color: "#f59e0b" }}>{inventory.lowStockCount}</div>
            </div>
            <div className="card" style={{ margin: 0 }}>
              <div style={{ color: "#6b7280", fontSize: 13 }}>Bez zaliha</div>
              <div style={{ fontSize: 20, fontWeight: 800, color: "#dc2626" }}>{inventory.outOfStockCount}</div>
            </div>
          </div>
        </div>
      )}

      {!loading && topProducts && (
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
          <div>
            <h3 style={{ marginBottom: 10 }}>💰 Top proizvodi po prometu</h3>
            <div style={{ overflowX: "auto" }}>
              <table className="table">
                <thead>
                  <tr>
                    <th>Artikal</th>
                    <th style={{ textAlign: "right" }}>Promet</th>
                    <th style={{ textAlign: "right" }}>Kom</th>
                  </tr>
                </thead>
                <tbody>
                  {topProducts.byRevenue.map((p) => (
                    <tr key={p.productId}>
                      <td>{p.productName}</td>
                      <td style={{ textAlign: "right", fontWeight: 700 }}>{formatCurrency(p.totalRevenue)}</td>
                      <td style={{ textAlign: "right" }}>{p.totalUnits}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          <div>
            <h3 style={{ marginBottom: 10 }}>📊 Top proizvodi po količini</h3>
            <div style={{ overflowX: "auto" }}>
              <table className="table">
                <thead>
                  <tr>
                    <th>Artikal</th>
                    <th style={{ textAlign: "right" }}>Kom</th>
                    <th style={{ textAlign: "right" }}>Promet</th>
                  </tr>
                </thead>
                <tbody>
                  {topProducts.byUnits.map((p) => (
                    <tr key={p.productId}>
                      <td>{p.productName}</td>
                      <td style={{ textAlign: "right", fontWeight: 700 }}>{p.totalUnits}</td>
                      <td style={{ textAlign: "right" }}>{formatCurrency(p.totalRevenue)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

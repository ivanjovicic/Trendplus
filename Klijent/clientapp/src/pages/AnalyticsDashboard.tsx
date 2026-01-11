import React, { useCallback, useEffect, useState } from "react";
import { checkAnalyticsHealth, getInventoryStatus, getSalesSummary, getTopProducts } from "../services/analyticsApi";
import type { InventoryStatus, SalesSummary, TopProductsResult } from "../types/analytics";

export default function AnalyticsDashboard() {
  const [fromDate, setFromDate] = useState<string>("");
  const [toDate, setToDate] = useState<string>("");
  const [top, setTop] = useState(10);
  const [lowStockThreshold, setLowStockThreshold] = useState(2);
  const [summary, setSummary] = useState<SalesSummary | null>(null);
  const [topProducts, setTopProducts] = useState<TopProductsResult | null>(null);
  const [inventory, setInventory] = useState<InventoryStatus | null>(null);
  const [healthStatus, setHealthStatus] = useState<string>("");
  const [loading, setLoading] = useState(true);
  const [errors, setErrors] = useState<{ summary?: string; topProducts?: string; inventory?: string; health?: string }>({});

  const load = useCallback(async () => {
    setLoading(true);
    setErrors({});
    setHealthStatus("");
    const newErrors: typeof errors = {};

    try {
      const health = await checkAnalyticsHealth();
      setHealthStatus(`? Analytics baza: ${health.tables.salesFacts} prodaja, ${health.tables.salesLineFacts} stavki, ${health.tables.productsDim} proizvoda`);
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
      newErrors.summary = e instanceof Error ? e.message : "Greška pri u?itavanju sažetka prodaje";
      setSummary(null);
    }

    try {
      const t = await getTopProducts(top, fromDate || undefined, toDate || undefined);
      setTopProducts(t);
    } catch (e: unknown) {
      console.error("Top products greška:", e);
      newErrors.topProducts = e instanceof Error ? e.message : "Greška pri u?itavanju top proizvoda";
      setTopProducts(null);
    }

    try {
      const i = await getInventoryStatus(lowStockThreshold);
      setInventory(i);
    } catch (e: unknown) {
      console.error("Inventory greška:", e);
      newErrors.inventory = e instanceof Error ? e.message : "Greška pri u?itavanju statusa zaliha";
      setInventory(null);
    }

    setErrors(newErrors);
    setLoading(false);
  }, [fromDate, toDate, top, lowStockThreshold]);

  useEffect(() => {
    load();
  }, [load]);

  const formatCurrency = (x: number) =>
    x.toLocaleString("sr-RS", { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + " RSD";

  const hasAnyData = summary || topProducts || inventory;
  const hasErrors = Object.keys(errors).length > 0;

  return (
    <div className="card" style={{ maxWidth: 1400 }}>
      <h2 className="text-2xl font-semibold mb-6">?? Analitika</h2>

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

      {hasErrors && (
        <div style={{ 
          background: "#fef2f2", 
          border: "2px solid #dc2626", 
          borderRadius: 8, 
          padding: 16,
          marginBottom: 20 
        }}>
          <div style={{ fontWeight: 600, color: "#dc2626", marginBottom: 8 }}>
            ?? {errors.health ? "Problem sa povezivanjem na backend" : "Analytics tabele nisu kreirane ili nema podataka"}
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
          
          <div style={{ fontSize: 13, color: "#6b7280", marginTop: 12 }}>
            Nakon kreiranja tabela, osvežite stranicu ili kliknite "Osveži" dugme.
          </div>
        </div>
      )}

      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: 12, marginBottom: 16 }}>
        <div>
          <label className="field-label">Od datuma</label>
          <input className="input-big" type="datetime-local" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
        </div>
        <div>
          <label className="field-label">Do datuma</label>
          <input className="input-big" type="datetime-local" value={toDate} onChange={(e) => setToDate(e.target.value)} />
        </div>
        <div>
          <label className="field-label">Top proizvoda</label>
          <input className="input-big" type="number" min={1} max={50} value={top} onChange={(e) => setTop(Number(e.target.value))} />
        </div>
        <div>
          <label className="field-label">Prag za niske zalihe</label>
          <input className="input-big" type="number" min={0} value={lowStockThreshold} onChange={(e) => setLowStockThreshold(Number(e.target.value))} />
        </div>
        <div style={{ display: "flex", alignItems: "flex-end" }}>
          <button className="button-big" type="button" onClick={load} style={{ marginTop: 0 }}>
            ?? Osveži
          </button>
        </div>
      </div>

      {loading && <p style={{ textAlign: "center", padding: "2rem" }}>U?itavanje...</p>}

      {!loading && !hasAnyData && !hasErrors && (
        <div style={{ textAlign: "center", padding: "3rem", color: "#6b7280" }}>
          <div style={{ fontSize: 48, marginBottom: 16 }}>??</div>
          <div style={{ fontSize: 18, fontWeight: 600, marginBottom: 8 }}>Nema podataka</div>
          <div style={{ fontSize: 14 }}>Kreirajte prodaju da bi se pojavili podaci u analytics dashboard-u.</div>
        </div>
      )}

      {!loading && summary && (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: 12, marginBottom: 20 }}>
          <div className="card" style={{ margin: 0 }}>
            <div style={{ color: "#6b7280", fontSize: 13 }}>Ukupan promet</div>
            <div style={{ fontSize: 22, fontWeight: 800, color: "#059669" }}>{formatCurrency(summary.totalRevenue)}</div>
          </div>
          <div className="card" style={{ margin: 0 }}>
            <div style={{ color: "#6b7280", fontSize: 13 }}>Transakcije</div>
            <div style={{ fontSize: 22, fontWeight: 800 }}>{summary.totalTransactions}</div>
          </div>
          <div className="card" style={{ margin: 0 }}>
            <div style={{ color: "#6b7280", fontSize: 13 }}>Prodate jedinice</div>
            <div style={{ fontSize: 22, fontWeight: 800 }}>{summary.totalUnits}</div>
          </div>
          <div className="card" style={{ margin: 0 }}>
            <div style={{ color: "#6b7280", fontSize: 13 }}>Prose?na korpa</div>
            <div style={{ fontSize: 22, fontWeight: 800 }}>{formatCurrency(summary.avgBasketValue)}</div>
          </div>
          <div className="card" style={{ margin: 0 }}>
            <div style={{ color: "#6b7280", fontSize: 13 }}>Prose?na cena artikla</div>
            <div style={{ fontSize: 22, fontWeight: 800 }}>{formatCurrency(summary.avgItemPrice)}</div>
          </div>
        </div>
      )}

      {!loading && inventory && (
        <div style={{ marginBottom: 20 }}>
          <h3 className="text-lg font-semibold" style={{ marginBottom: 10 }}>?? Zalihe (ProductsDim)</h3>
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
            <h3 className="text-lg font-semibold" style={{ marginBottom: 10 }}>?? Top proizvodi po prometu</h3>
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
                  {topProducts.byRevenue.length === 0 ? (
                    <tr>
                      <td colSpan={3} style={{ textAlign: "center", color: "#6b7280", padding: "2rem" }}>
                        Nema podataka
                      </td>
                    </tr>
                  ) : (
                    topProducts.byRevenue.map((p) => (
                      <tr key={p.productId}>
                        <td>{p.productName}</td>
                        <td style={{ textAlign: "right", fontWeight: 700 }}>{formatCurrency(p.totalRevenue)}</td>
                        <td style={{ textAlign: "right" }}>{p.totalUnits}</td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>

          <div>
            <h3 className="text-lg font-semibold" style={{ marginBottom: 10 }}>?? Top proizvodi po koli?ini</h3>
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
                  {topProducts.byUnits.length === 0 ? (
                    <tr>
                      <td colSpan={3} style={{ textAlign: "center", color: "#6b7280", padding: "2rem" }}>
                        Nema podataka
                      </td>
                    </tr>
                  ) : (
                    topProducts.byUnits.map((p) => (
                      <tr key={p.productId}>
                        <td>{p.productName}</td>
                        <td style={{ textAlign: "right", fontWeight: 700 }}>{p.totalUnits}</td>
                        <td style={{ textAlign: "right" }}>{formatCurrency(p.totalRevenue)}</td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

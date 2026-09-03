import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import {
  checkAnalyticsHealth,
  getDailySales,
  getDashboardAdvanced,
  getInventoryStatus,
  getSalesSummary,
  getTopProductsAdvanced,
  getValidationCompleteness,
  getValidationFreshness,
  getValidationLostSales,
  getValidationNegativeQty,
} from "../services/analyticsApi";
import type { DashboardValidationEndpoint, DailySale, TopProductAdvancedItem, TopProductsAdvancedResult } from "../types/analytics";
import {
  ANALYTICS_PERIOD_PRESET_OPTIONS,
  type AnalyticsPeriodPreset,
  getAnalyticsPeriodPresetRange,
} from "../utils/analyticsPeriodPresets";
import { fmtNumber, fmtPct, fmtRsd } from "../utils/analyticsFormatters";
import "./AnalyticsDetails.css";

type TopTab = "revenue" | "units" | "velocity" | "margin";
type Tone = "good" | "warning" | "critical" | "neutral";

type AnalyticsDetailSummary = {
  totalRevenue: number;
  totalTransactions: number;
  totalUnits: number;
};

export function deriveAnalyticsDetailMetrics(
  summary: AnalyticsDetailSummary | null,
  fromDate: string,
  toDate: string,
): { days: number; revPerDay: number | null; txPerDay: number | null; unitsPerDay: number | null } {
  const days = Math.max(1, Math.floor((new Date(toDate).getTime() - new Date(fromDate).getTime()) / (24 * 3600 * 1000)) + 1);
  if (!summary) return { days, revPerDay: null, txPerDay: null, unitsPerDay: null };
  return {
    days,
    revPerDay: summary.totalRevenue / days,
    txPerDay: summary.totalTransactions / days,
    unitsPerDay: summary.totalUnits / days,
  };
}

interface TrendPoint {
  date: string;
  revenue: number;
  ma7: number | null;
  ma30: number | null;
  anomaly: "spike" | "drop" | null;
}

const tone = (s?: string | null): Tone => (!s ? "neutral" : s === "error" ? "critical" : (s as Tone));
const toneText = (t: Tone) => (t === "good" ? "Dobro" : t === "warning" ? "Upozorenje" : t === "critical" ? "Kriticno" : "Neutralno");
const trendArrow = (v?: number | null) => (v == null ? "\u2022" : v >= 0 ? "\u2191" : "\u2193");

function moving(values: number[], w: number): Array<number | null> {
  return values.map((_, i) => (i + 1 < w ? null : values.slice(i - w + 1, i + 1).reduce((a, b) => a + b, 0) / w));
}

function slope(values: number[]): number | null {
  if (values.length < 2) return null;
  const n = values.length;
  const xm = (n - 1) / 2;
  const ym = values.reduce((a, b) => a + b, 0) / n;
  let num = 0;
  let den = 0;
  for (let i = 0; i < n; i += 1) {
    num += (i - xm) * (values[i] - ym);
    den += (i - xm) * (i - xm);
  }
  return den === 0 ? null : num / den;
}

function std(values: number[]): number {
  if (values.length < 2) return 0;
  const m = values.reduce((a, b) => a + b, 0) / values.length;
  const v = values.reduce((a, x) => a + (x - m) ** 2, 0) / values.length;
  return Math.sqrt(v);
}

function topRows(data: TopProductsAdvancedResult | null, tab: TopTab): TopProductAdvancedItem[] {
  if (!data) return [];
  if (tab === "revenue") return data.byRevenue;
  if (tab === "units") return data.byUnits;
  if (tab === "velocity") return data.byVelocity;
  return data.byMarginImpact;
}

function getErrorText(reason: unknown, fallback: string): string {
  if (reason instanceof Error && reason.message.trim()) return reason.message;
  if (typeof reason === "string" && reason.trim()) return reason;
  return fallback;
}

function isTransientCancellationMessage(message: string): boolean {
  const normalized = message.toLowerCase();
  return normalized.includes("the operation was canceled")
    || normalized.includes("operation was canceled")
    || normalized.includes("request timeout")
    || normalized.includes("aborterror");
}

function compactErrorMessages(messages: string[]): string[] {
  const unique = Array.from(new Set(messages.map((item) => item.trim()).filter(Boolean)));
  if (unique.length === 0) return [];

  const stable: string[] = [];
  let transientCancelCount = 0;

  for (const message of unique) {
    if (isTransientCancellationMessage(message)) {
      transientCancelCount += 1;
      continue;
    }

    stable.push(message);
  }

  if (transientCancelCount > 0) {
    stable.push("Neki analytics upiti su privremeno prekinuti. Pokusajte osvezavanje.");
  }

  return stable;
}

export default function AnalyticsDetails() {
  const initialRange = getAnalyticsPeriodPresetRange("30d");
  const [preset, setPreset] = useState<AnalyticsPeriodPreset>("30d");
  const [fromDate, setFromDate] = useState(() => `${initialRange.fromDate}T00:00`);
  const [toDate, setToDate] = useState(() => `${initialRange.toDate}T23:59`);
  const [topTab, setTopTab] = useState<TopTab>("revenue");
  const [showFullList, setShowFullList] = useState(false);
  const [loading, setLoading] = useState(true);
  const [errors, setErrors] = useState<string[]>([]);
  const [healthText, setHealthText] = useState("");
  const [summary, setSummary] = useState<AnalyticsDetailSummary | null>(null);
  const [daily, setDaily] = useState<DailySale[]>([]);
  const [inventory, setInventory] = useState<{ totalSkuCount: number; lowStockCount: number; outOfStockCount: number } | null>(null);
  const [top, setTop] = useState<TopProductsAdvancedResult | null>(null);
  const [validC, setValidC] = useState<DashboardValidationEndpoint | null>(null);
  const [validF, setValidF] = useState<DashboardValidationEndpoint | null>(null);
  const [validL, setValidL] = useState<DashboardValidationEndpoint | null>(null);
  const [validN, setValidN] = useState<DashboardValidationEndpoint | null>(null);
  const [adv, setAdv] = useState<{ cards: Array<{ key: string; value: number; trendPct?: number | null; status: string; subtitle: string }>; insights: Array<{ badge: string; description: string; color?: string }>; actions: Array<{ priority: string; title: string; recommendation: string }>; validations: Array<{ severity: string; message: string }> } | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setErrors([]);
    const rs = await Promise.allSettled([
      checkAnalyticsHealth(),
      getSalesSummary(fromDate, toDate, true),
      getDailySales(fromDate, toDate, true),
      getInventoryStatus(2, true),
      getTopProductsAdvanced(20, fromDate, toDate, true),
      getDashboardAdvanced(fromDate, toDate, true),
      getValidationCompleteness(true),
      getValidationFreshness(true),
      getValidationLostSales(true),
      getValidationNegativeQty(fromDate, toDate, true),
    ]);
    const errs: string[] = [];
    if (rs[0].status === "fulfilled") setHealthText(`Analytics baza: ${rs[0].value.tables.salesFacts} prodaja, ${rs[0].value.tables.salesLineFacts} stavki, ${rs[0].value.tables.productsDim} proizvoda.`);
    else errs.push(getErrorText(rs[0].reason, "Health check nije dostupan."));
    if (rs[1].status === "fulfilled") setSummary(rs[1].value); else errs.push(getErrorText(rs[1].reason, "Sazetak nije ucitan."));
    if (rs[2].status === "fulfilled") setDaily(rs[2].value); else errs.push(getErrorText(rs[2].reason, "Dnevna serija nije ucitana."));
    if (rs[3].status === "fulfilled") setInventory(rs[3].value); else errs.push(getErrorText(rs[3].reason, "Zalihe nisu ucitane."));
    if (rs[4].status === "fulfilled") setTop(rs[4].value); else errs.push(getErrorText(rs[4].reason, "Top lista nije ucitana."));
    if (rs[5].status === "fulfilled") setAdv(rs[5].value); else errs.push(getErrorText(rs[5].reason, "Advanced snapshot nije ucitan."));
    setValidC(rs[6].status === "fulfilled" ? rs[6].value : null);
    setValidF(rs[7].status === "fulfilled" ? rs[7].value : null);
    setValidL(rs[8].status === "fulfilled" ? rs[8].value : null);
    setValidN(rs[9].status === "fulfilled" ? rs[9].value : null);
    setErrors(compactErrorMessages(errs));
    setLoading(false);
  }, [fromDate, toDate]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (!showFullList) return;
    const onEscape = (e: KeyboardEvent) => {
      if (e.key === "Escape") setShowFullList(false);
    };
    window.addEventListener("keydown", onEscape);
    return () => window.removeEventListener("keydown", onEscape);
  }, [showFullList]);

  const derived = useMemo(
    () => deriveAnalyticsDetailMetrics(summary, fromDate, toDate),
    [fromDate, summary, toDate],
  );

  const trend = useMemo<TrendPoint[]>(() => {
    const s = [...daily].sort((a, b) => a.date.localeCompare(b.date));
    if (s.length === 0) return [];
    const r = s.map((x) => x.totalRevenue);
    const m7 = moving(r, 7);
    const m30 = moving(r, 30);
    const dev = std(r);
    return s.map((x, i) => {
      const b = m30[i] ?? m7[i] ?? x.totalRevenue;
      const d = x.totalRevenue - b;
      const a = dev > 0 && d >= dev * 2 ? "spike" : dev > 0 && d <= -dev * 2 ? "drop" : null;
      return { date: x.date, revenue: x.totalRevenue, ma7: m7[i], ma30: m30[i], anomaly: a };
    });
  }, [daily]);

  const metric = useMemo(() => {
    const l7 = trend.slice(-7).map((x) => x.revenue);
    const p7 = trend.slice(-14, -7).map((x) => x.revenue);
    const sl = slope(l7);
    const sL = l7.reduce((a, b) => a + b, 0);
    const sP = p7.reduce((a, b) => a + b, 0);
    const pct = sP > 0 ? ((sL - sP) / sP) * 100 : null;
    const oos = inventory?.outOfStockCount ?? 0;
    const total = inventory?.totalSkuCount ?? 0;
    const inStock = total > 0 ? ((total - oos) / total) * 100 : null;
    const red = total > 0 ? ((inventory?.lowStockCount ?? 0) / total) * 100 : null;
    const pareto = adv?.cards.find((c) => c.key === "pareto")?.value ?? null;
    return { sl, pct, inStock, red, pareto };
  }, [adv?.cards, inventory, trend]);

  const shortTop = useMemo(() => topRows(top, topTab).slice(0, 5), [top, topTab]);
  const fullTop = useMemo(() => topRows(top, topTab).slice(0, 20), [top, topTab]);
  const gainers = useMemo(
    () =>
      (top?.byRevenue ?? [])
        .filter((x) => (x.trendPct ?? 0) > 0)
        .sort((a, b) => (b.trendPct ?? 0) - (a.trendPct ?? 0))
        .slice(0, 5),
    [top]
  );
  const losers = useMemo(
    () =>
      (top?.byRevenue ?? [])
        .filter((x) => (x.trendPct ?? 0) < 0)
        .sort((a, b) => (a.trendPct ?? 0) - (b.trendPct ?? 0))
        .slice(0, 5),
    [top]
  );

  return (
    <div className="analytics-details">
      <header className="ad-header">
        <div>
          <h1>Detaljne analize</h1>
          <p>{healthText || "Premium dark analytics pregled"}</p>
        </div>
        <div className="ad-controls">
          <select
            value={preset}
            onChange={(e) => {
              const p = e.target.value as AnalyticsPeriodPreset;
              setPreset(p);
              const r = p === "custom" ? null : getAnalyticsPeriodPresetRange(p);
              if (!r) return;
              setFromDate(`${r.fromDate}T00:00`);
              setToDate(`${r.toDate}T23:59`);
            }}
          >
            {ANALYTICS_PERIOD_PRESET_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
          <button onClick={() => void load()} disabled={loading}>
            Osvezi
          </button>
        </div>
      </header>

      <section className="ad-panel ad-legacy-banner">
        <strong>Legacy prikaz</strong>
        <p>Ovo je detaljni/legacy prikaz. Glavni analytics dashboard je <Link to="/analytics">Pregled poslovanja</Link>.</p>
      </section>

      {preset === "custom" && (
        <section className="ad-panel ad-custom">
          <label>
            Od
            <input type="datetime-local" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
          </label>
          <label>
            Do
            <input type="datetime-local" value={toDate} onChange={(e) => setToDate(e.target.value)} />
          </label>
        </section>
      )}

      {errors.length > 0 && (
        <section className="ad-panel ad-error">
          <h3>Greske pri ucitavanju</h3>
          {errors.map((x, i) => (
            <div key={`e-${i}`}>- {x}</div>
          ))}
        </section>
      )}

      {loading && (
        <section className="ad-skeleton-grid">
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={`sk-${i}`} className="ad-skeleton" />
          ))}
        </section>
      )}

      {!loading && (
        <>
          <section className="ad-grid ad-kpi-grid">
            <article className="ad-kpi-card"><span>Promet</span><strong>{fmtRsd(summary?.totalRevenue, 0, "Nije dostupno")}</strong><small>Ukupno u periodu</small></article>
            <article className="ad-kpi-card"><span>Transakcije</span><strong>{fmtNumber(summary?.totalTransactions, 0, "Nije dostupno")}</strong><small>Ukupan broj racuna</small></article>
            <article className="ad-kpi-card"><span>Jedinice</span><strong>{fmtNumber(summary?.totalUnits, 0, "Nije dostupno")}</strong><small>Ukupan broj komada</small></article>
            <article className="ad-kpi-card"><span>Promet/dan</span><strong>{fmtRsd(derived.revPerDay, 0, "Nije dostupno")}</strong><small>Formula: Promet / broj dana</small></article>
            <article className="ad-kpi-card"><span>Transakcije/dan</span><strong>{fmtNumber(derived.txPerDay, 1, "Nije dostupno")}</strong><small>Formula: Racuni / broj dana</small></article>
            <article className="ad-kpi-card"><span>Jedinice/dan</span><strong>{fmtNumber(derived.unitsPerDay, 1, "Nije dostupno")}</strong><small>Formula: Komadi / broj dana</small></article>
          </section>

          <section className="ad-grid ad-risk-grid">
            <article className={`ad-risk-card ${metric.inStock != null && metric.inStock >= 95 ? "good" : metric.inStock != null && metric.inStock >= 90 ? "warning" : "critical"}`}>
              <span>In-stock %</span><strong>{fmtPct(metric.inStock, 1, "Nije dostupno")}</strong><small>(SKU na stanju / ukupan SKU) * 100</small>
            </article>
            <article className={`ad-risk-card ${tone(validL?.status)}`}><span>OOS + Lost sales</span><strong>{fmtNumber(inventory?.outOfStockCount, 0, "Nije dostupno")} | {fmtRsd(validL?.lostSalesEstimate, 0, "Nije dostupno")}</strong><small>Rizik od rasprodatosti</small></article>
            <article className={`ad-risk-card ${metric.red != null && metric.red < 8 ? "good" : metric.red != null && metric.red < 15 ? "warning" : "critical"}`}><span>Red zone SKU %</span><strong>{fmtPct(metric.red, 1, "Nije dostupno")}</strong><small>Niska zaliha / ukupan SKU</small></article>
            <article className={`ad-risk-card ${metric.pareto != null && metric.pareto > 85 ? "warning" : "good"}`}><span>Pareto 80/20</span><strong>{fmtPct(metric.pareto, 1, "Nije dostupno")}</strong><small>Udeo prometa top 20 SKU</small></article>
            <article className={`ad-risk-card ${tone(validF?.status)}`}><span>Data Health</span><strong>{validC?.score == null ? "N/A" : fmtPct(validC.score * 100)} | {validF?.freshnessHours == null ? "N/A" : `${fmtNumber(validF.freshnessHours, 1)}h`}</strong><small>Completeness + freshness</small></article>
          </section>

          <section className="ad-grid ad-main-grid">
            <article className="ad-panel">
              <h3>Trend performansi</h3>
              {trend.length > 0 ? (
                <div className="ad-chart">
                  <ResponsiveContainer width="100%" height={300}>
                    <LineChart data={trend}>
                      <CartesianGrid strokeDasharray="3 3" stroke="var(--chart-grid, var(--theme-color-2a3556, #2a3556))" />
                      <XAxis dataKey="date" tick={{ fill: "var(--text-secondary, var(--theme-color-9fb2de, #9fb2de))", fontSize: 12 }} />
                      <YAxis tick={{ fill: "var(--text-secondary, var(--theme-color-9fb2de, #9fb2de))", fontSize: 12 }} />
                      <Tooltip
                        formatter={(v: number | string | undefined, n?: string) => [v == null || Number.isNaN(Number(v)) ? "Nije dostupno" : fmtRsd(Number(v)), n ?? "vrednost"]}
                        contentStyle={{ background: "var(--surface-elev-1, var(--theme-color-0f1730, #0f1730))", border: "1px solid var(--border-default, var(--theme-color-32406b, #32406b))" }}
                      />
                      <Line type="monotone" dataKey="revenue" stroke="var(--series-revenue, var(--theme-color-40d69f, #40d69f))" strokeWidth={2.2} dot={false} name="Promet" />
                      <Line type="monotone" dataKey="ma7" stroke="var(--series-ma7, var(--theme-color-6ca8ff, #6ca8ff))" strokeWidth={2} dot={false} name="MA7" />
                      <Line type="monotone" dataKey="ma30" stroke="var(--series-ma30, var(--theme-color-ffbe5a, #ffbe5a))" strokeWidth={2} dot={false} name="MA30" />
                    </LineChart>
                  </ResponsiveContainer>
                </div>
              ) : (
                <div className="ad-empty">Nema podataka za trend grafikon.</div>
              )}
              <div className="ad-meta"><span>Momentum: {fmtPct(metric.pct, 1, "Nije dostupno")}</span><span>Slope: {metric.sl == null ? "Nije dostupno" : fmtNumber(metric.sl, 2)}</span></div>
            </article>
            <article className="ad-panel">
              <h3>Quick Insights</h3>
              {(adv?.insights ?? []).slice(0, 3).map((x, i) => (
                <div key={`qi-${i}`} className={`ad-insight ${tone(x.color)}`}>
                  <strong>{x.badge}</strong>
                  <p>{x.description}</p>
                </div>
              ))}
              {(adv?.insights?.length ?? 0) === 0 && <div className="ad-empty">Nema insight signala.</div>}
            </article>
          </section>

          <section className="ad-grid ad-main-grid">
            <article className="ad-panel">
              <h3>Top gainers</h3>
              {gainers.length === 0 && <div className="ad-empty">Nema pozitivnih trendova.</div>}
              {gainers.map((x) => <div key={`g-${x.productId}`} className="ad-row"><span>{x.productName}</span><strong className="up">+{fmtPct(x.trendPct)}</strong></div>)}
            </article>
            <article className="ad-panel">
              <h3>Top losers</h3>
              {losers.length === 0 && <div className="ad-empty">Nema negativnih trendova.</div>}
              {losers.map((x) => <div key={`l-${x.productId}`} className="ad-row"><span>{x.productName}</span><strong className="down">{fmtPct(x.trendPct)}</strong></div>)}
            </article>
          </section>

          <section className="ad-panel">
            <div className="ad-top-head">
              <h3>Top proizvodi (kratka lista, max 5)</h3>
              <div className="ad-tabs">
                <button className={topTab === "revenue" ? "active" : ""} onClick={() => setTopTab("revenue")}>Promet</button>
                <button className={topTab === "units" ? "active" : ""} onClick={() => setTopTab("units")}>Komadi</button>
                <button className={topTab === "velocity" ? "active" : ""} onClick={() => setTopTab("velocity")}>Velocity</button>
                <button className={topTab === "margin" ? "active" : ""} onClick={() => setTopTab("margin")}>Marza</button>
                <button onClick={() => setShowFullList(true)}>Prikazi celu listu</button>
              </div>
            </div>
            <div className="ad-table-head">
              <span>SKU / Artikal</span>
              <span>Promet</span>
              <span>Kom</span>
              <span>Velocity</span>
              <span>Trend</span>
              <span>Status</span>
            </div>
            {shortTop.length === 0 && <div className="ad-empty">Nema podataka za izabrani tab.</div>}
            {shortTop.map((row) => (
              <div key={`${topTab}-${row.productId}`} className="ad-table-row">
                <span><strong>{row.sku}</strong><small>{row.productName}</small></span>
                <span>{fmtRsd(row.revenue)}</span>
                <span>{fmtNumber(row.units)}</span>
                <span>{fmtNumber(row.velocityUnitsPerDay, 2)}</span>
                <span
                  className={(row.trendPct ?? 0) >= 0 ? "up" : "down"}
                  title="Trend u odnosu na prethodni uporedivi period"
                >
                  {trendArrow(row.trendPct)} {fmtPct(row.trendPct)}
                </span>
                <span className={`status-tag ${tone(row.stockStatus)}`}>{toneText(tone(row.stockStatus))}</span>
              </div>
            ))}
          </section>

          <section className="ad-grid ad-main-grid">
            <article className="ad-panel">
              <h3>Data quality</h3>
              <div className="ad-row"><span>Completeness</span><strong>{validC?.score == null ? "Nije dostupno" : fmtPct(validC.score * 100)}</strong></div>
              <div className="ad-row"><span>Missing core fields</span><strong>{validC?.affectedSku ?? "Nije dostupno"}</strong></div>
              <div className="ad-row"><span>Freshness</span><strong>{validF?.freshnessHours == null ? "Nije dostupno" : `${fmtNumber(validF.freshnessHours, 1)}h`}</strong></div>
              <div className="ad-row"><span>Lost sales estimate</span><strong>{fmtRsd(validL?.lostSalesEstimate, 0, "Nije dostupno")}</strong></div>
              <div className="ad-row">
                <span>Negative qty</span>
                <strong>
                  {validN?.negativeQtyCount == null
                    ? "N/A"
                    : `${fmtNumber(validN.negativeQtyCount)}${validN.totalRows && validN.totalRows > 0 ? ` (${fmtPct((validN.negativeQtyCount / validN.totalRows) * 100, 3)})` : ""}`}
                </strong>
              </div>
            </article>
            <article className="ad-panel">
              <h3>Recommended actions</h3>
              {(adv?.actions ?? []).slice(0, 5).map((a, i) => (
                <div key={`ra-${i}`} className="ad-action">
                  <strong>[{a.priority}] {a.title}</strong>
                  <p>{a.recommendation}</p>
                </div>
              ))}
              {(adv?.actions?.length ?? 0) === 0 && <div className="ad-empty">Nema akcija za period.</div>}
              {(adv?.validations ?? []).slice(0, 3).map((v, i) => (
                <div key={`rv-${i}`} className={`ad-validation ${tone(v.severity)}`}>
                  <strong>{toneText(tone(v.severity))}</strong>
                  <p>{v.message}</p>
                </div>
              ))}
            </article>
          </section>

          {showFullList && (
            <div className="ad-modal-backdrop" onClick={() => setShowFullList(false)}>
              <section
                className="ad-modal"
                role="dialog"
                aria-modal="true"
                aria-label="Cela top lista proizvoda"
                onClick={(e) => e.stopPropagation()}
              >
                <div className="ad-modal-head">
                  <h3>Top proizvodi - cela lista (max 20)</h3>
                  <button className="ad-modal-close" onClick={() => setShowFullList(false)}>
                    Zatvori
                  </button>
                </div>
                <div className="ad-table-head">
                  <span>SKU / Artikal</span>
                  <span>Promet</span>
                  <span>Kom</span>
                  <span>Velocity</span>
                  <span>Trend</span>
                  <span>Status</span>
                </div>
                {fullTop.length === 0 && <div className="ad-empty">Nema podataka za izabrani tab.</div>}
                {fullTop.map((row) => (
                  <div key={`modal-${topTab}-${row.productId}`} className="ad-table-row">
                    <span>
                      <strong>{row.sku}</strong>
                      <small>{row.productName}</small>
                    </span>
                    <span>{fmtRsd(row.revenue)}</span>
                    <span>{fmtNumber(row.units)}</span>
                    <span>{fmtNumber(row.velocityUnitsPerDay, 2)}</span>
                    <span
                      className={(row.trendPct ?? 0) >= 0 ? "up" : "down"}
                      title="Trend u odnosu na prethodni uporedivi period"
                    >
                      {trendArrow(row.trendPct)} {fmtPct(row.trendPct)}
                    </span>
                    <span className={`status-tag ${tone(row.stockStatus)}`}>{toneText(tone(row.stockStatus))}</span>
                  </div>
                ))}
              </section>
            </div>
          )}
        </>
      )}
    </div>
  );
}


import { useCallback, useEffect, useMemo, useState } from "react";
import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
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
} from "../services/analyticsApi";
import type {
  DailySale,
  DashboardAdvancedSnapshot,
  DashboardMetricCard,
  DashboardValidationEndpoint,
  InventoryStatus,
  SalesSummary,
  TopProductAdvancedItem,
  TopProductsAdvancedResult,
} from "../types/analytics";
import "./AnalyticsDashboard.css";

type DatePreset = "today" | "yesterday" | "7d" | "30d" | "90d" | "thisMonth" | "lastMonth" | "custom";
type TopTabKey = "revenue" | "units" | "velocity" | "margin";
type Tone = "good" | "warning" | "critical" | "neutral";

const HELP: Record<string, string> = {
  promet: "Ukupan novac od prodaje u izabranom periodu.",
  transakcije: "Jedan racun = jedna transakcija.",
  jedinice: "Ukupan broj prodatih komada.",
  sku: "Jedinstvena interna sifra artikla.",
  velocity: "Prosecno prodata kolicina po danu.",
  oos: "Out of stock: artikal je rasprodat i nije dostupan za prodaju.",
  pareto: "Koliko mali broj artikala pravi vecinu prometa.",
  ma7: "7-dnevni pokretni prosek smanjuje dnevni sum i prikazuje realniji trend.",
  momentum: "Poredi poslednjih 7 dana sa prethodnih 7 dana.",
  elasticnost: "Pokazuje koliko se traznja menja kada se menja cena.",
  completeness: "Da li artikli imaju kljucna polja (naziv, sifra, kategorija).",
  freshness: "Koliko je vremena proslo od poslednjeg osvezavanja podataka.",
  margin: "Procenjeni uticaj na marzu (prodajna - nabavna cena).",
  trend: "Smer promene u odnosu na prethodni uporediv period.",
};

function formatInputDateTime(value: Date): string {
  const year = value.getFullYear();
  const month = String(value.getMonth() + 1).padStart(2, "0");
  const day = String(value.getDate()).padStart(2, "0");
  const hour = String(value.getHours()).padStart(2, "0");
  const minute = String(value.getMinutes()).padStart(2, "0");
  return `${year}-${month}-${day}T${hour}:${minute}`;
}

function parseInputDate(value: string): Date {
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? new Date() : parsed;
}

function statusTone(value?: string | null): Tone {
  if (!value) return "neutral";
  if (value === "good") return "good";
  if (value === "warning") return "warning";
  if (value === "critical" || value === "error") return "critical";
  return "neutral";
}

function statusLabel(value?: string | null): string {
  const tone = statusTone(value);
  if (tone === "good") return "Dobro";
  if (tone === "warning") return "Upozorenje";
  if (tone === "critical") return "Kriticno";
  return "Neutralno";
}

function formatCurrency(value: number): string {
  return `${new Intl.NumberFormat("sr-RS", {
    minimumFractionDigits: 0,
    maximumFractionDigits: 2,
  }).format(value)} RSD`;
}

function formatNumber(value: number, digits = 0): string {
  return new Intl.NumberFormat("sr-RS", {
    minimumFractionDigits: digits,
    maximumFractionDigits: digits,
  }).format(value);
}

function formatPercent(value?: number | null, digits = 1): string {
  if (value == null || Number.isNaN(value)) return "N/A";
  return `${formatNumber(value, digits)}%`;
}

function trendLabel(value?: number | null): string {
  if (value == null) return "Nema trenda";
  return value >= 0 ? "Rast" : "Pad";
}

function trAdvancedLabel(key: string, fallback: string): string {
  const map: Record<string, string> = {
    velocity: "Brzina prodaje (velocity)",
    oos: "Rasprodato (OOS)",
    pareto: "Pareto koncentracija",
    data_health: "Svezina podataka",
    completeness: "Kompletnost podataka",
  };
  return map[key] ?? fallback;
}

function trDynamic(text: string): string {
  return text
    .replace("Top SKU:", "Top sifra:")
    .replace("Lost sales estimate:", "Procena izgubljene prodaje:")
    .replace("Top 50 share:", "Udeo top 50:")
    .replace("Last import:", "Poslednji import:")
    .replace("Missing:", "Nedostajuca polja:")
    .replace("Completeness", "Kompletnost")
    .replace("Freshness", "Svezina")
    .replace("Lost Sales", "Izgubljena prodaja")
    .replace("Replenishment", "Dopuna zaliha")
    .replace("Data quality fix", "Ispravka kvaliteta podataka")
    .replace("Refresh pipeline", "Osvezavanje pipeline-a")
    .replace("Portfolio balance", "Balans asortimana")
    .replace("Monitor", "Pracenje")
    .replace("Lost sales estimate indicates stock-out pressure.", "Procena izgubljene prodaje ukazuje na pritisak rasprodatosti.")
    .replace("Completeness validation is below target.", "Validacija kompletnosti je ispod cilja.")
    .replace("Freshness validation indicates stale data.", "Validacija svezine pokazuje zastarele podatke.")
    .replace("Pareto concentration is elevated.", "Pareto koncentracija je povecana.");
}

function buildPresetRange(preset: DatePreset): { from: string; to: string } | null {
  const now = new Date();
  const from = new Date(now);
  const to = new Date(now);
  to.setHours(23, 59, 59, 999);
  if (preset === "today") from.setHours(0, 0, 0, 0);
  if (preset === "yesterday") {
    from.setDate(now.getDate() - 1);
    to.setDate(now.getDate() - 1);
    from.setHours(0, 0, 0, 0);
  }
  if (preset === "7d") from.setDate(now.getDate() - 6);
  if (preset === "30d") from.setDate(now.getDate() - 29);
  if (preset === "90d") from.setDate(now.getDate() - 89);
  if (preset === "thisMonth") from.setDate(1);
  if (preset === "lastMonth") {
    from.setMonth(now.getMonth() - 1, 1);
    to.setMonth(now.getMonth(), 0);
  }
  if (preset === "custom") return null;
  from.setHours(0, 0, 0, 0);
  return { from: formatInputDateTime(from), to: formatInputDateTime(to) };
}

function InfoTip({ text }: { text: string }) {
  return (
    <span className="info-tip" role="note" tabIndex={0} aria-label={text}>
      i
      <span className="info-tip-bubble">{text}</span>
    </span>
  );
}

export default function AnalyticsDashboard() {
  const [preset, setPreset] = useState<DatePreset>("30d");
  const [fromDate, setFromDate] = useState<string>(() => {
    const range = buildPresetRange("30d");
    return range?.from ?? formatInputDateTime(new Date());
  });
  const [toDate, setToDate] = useState<string>(() => {
    const range = buildPresetRange("30d");
    return range?.to ?? formatInputDateTime(new Date());
  });
  const [summary, setSummary] = useState<SalesSummary | null>(null);
  const [inventory, setInventory] = useState<InventoryStatus | null>(null);
  const [dailySales, setDailySales] = useState<DailySale[]>([]);
  const [advanced, setAdvanced] = useState<DashboardAdvancedSnapshot | null>(null);
  const [topAdvanced, setTopAdvanced] = useState<TopProductsAdvancedResult | null>(null);
  const [validCompleteness, setValidCompleteness] = useState<DashboardValidationEndpoint | null>(null);
  const [validFreshness, setValidFreshness] = useState<DashboardValidationEndpoint | null>(null);
  const [validLostSales, setValidLostSales] = useState<DashboardValidationEndpoint | null>(null);
  const [healthText, setHealthText] = useState("");
  const [topTab, setTopTab] = useState<TopTabKey>("revenue");
  const [errors, setErrors] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);

  const isInvalidFilterRange = useMemo(() => parseInputDate(fromDate) > parseInputDate(toDate), [fromDate, toDate]);
  const selectedDays = useMemo(() => {
    const diff = parseInputDate(toDate).getTime() - parseInputDate(fromDate).getTime();
    return Math.max(Math.floor(diff / (24 * 60 * 60 * 1000)) + 1, 1);
  }, [fromDate, toDate]);

  const applyPreset = useCallback((value: DatePreset) => {
    setPreset(value);
    const range = buildPresetRange(value);
    if (!range) return;
    setFromDate(range.from);
    setToDate(range.to);
  }, []);

  const load = useCallback(async () => {
    if (isInvalidFilterRange) {
      setErrors(["Proverite filtere: datum od ne moze biti posle datuma do."]);
      return;
    }
    setLoading(true);
    setErrors([]);
    const [healthR, summaryR, inventoryR, dailyR, advancedR, topAdvancedR, compR, freshR, lostR] =
      await Promise.allSettled([
        checkAnalyticsHealth(),
        getSalesSummary(fromDate, toDate, true),
        getInventoryStatus(2, true),
        getDailySales(fromDate, toDate, true),
        getDashboardAdvanced(fromDate, toDate, true),
        getTopProductsAdvanced(10, fromDate, toDate, true),
        getValidationCompleteness(true),
        getValidationFreshness(true),
        getValidationLostSales(true),
      ]);

    const nextErrors: string[] = [];
    if (healthR.status === "fulfilled") {
      setHealthText(
        `Analytics baza: ${healthR.value.tables.salesFacts} prodaja, ${healthR.value.tables.salesLineFacts} stavki, ${healthR.value.tables.productsDim} proizvoda`
      );
    } else {
      setHealthText("");
      nextErrors.push("Provera zdravstvenog stanja podataka nije dostupna.");
    }
    if (summaryR.status === "fulfilled") setSummary(summaryR.value);
    else {
      setSummary(null);
      nextErrors.push("Sazetak prodaje nije ucitan.");
    }
    if (inventoryR.status === "fulfilled") setInventory(inventoryR.value);
    else {
      setInventory(null);
      nextErrors.push("Status zaliha nije ucitan.");
    }
    if (dailyR.status === "fulfilled") setDailySales(dailyR.value);
    else setDailySales([]);
    if (advancedR.status === "fulfilled") setAdvanced(advancedR.value);
    else setAdvanced(null);
    if (topAdvancedR.status === "fulfilled") setTopAdvanced(topAdvancedR.value);
    else setTopAdvanced(null);
    setValidCompleteness(compR.status === "fulfilled" ? compR.value : null);
    setValidFreshness(freshR.status === "fulfilled" ? freshR.value : null);
    setValidLostSales(lostR.status === "fulfilled" ? lostR.value : null);
    setErrors(nextErrors);
    setLoading(false);
  }, [fromDate, toDate, isInvalidFilterRange]);

  useEffect(() => {
    void load();
  }, [load]);

  const advancedByKey = useMemo(() => {
    const map = new Map<string, DashboardMetricCard>();
    for (const c of advanced?.cards ?? []) map.set(c.key, c);
    return map;
  }, [advanced]);

  const movingStats = useMemo(() => {
    if (dailySales.length === 0) return { ma7Revenue: 0, momentumPct: null as number | null, elasticity: null as number | null };
    const sorted = [...dailySales].sort((a, b) => a.date.localeCompare(b.date));
    const last7 = sorted.slice(-7);
    const prev7 = sorted.slice(-14, -7);
    const sumR = (x: DailySale[]) => x.reduce((acc, v) => acc + v.totalRevenue, 0);
    const sumU = (x: DailySale[]) => x.reduce((acc, v) => acc + v.totalUnits, 0);
    const lastRev = sumR(last7);
    const prevRev = sumR(prev7);
    const lastUnits = sumU(last7);
    const prevUnits = sumU(prev7);
    const ma7Revenue = last7.length > 0 ? lastRev / last7.length : 0;
    const momentumPct = prevRev > 0 ? Number((((lastRev - prevRev) / prevRev) * 100).toFixed(2)) : null;
    const lastPrice = lastUnits > 0 ? lastRev / lastUnits : 0;
    const prevPrice = prevUnits > 0 ? prevRev / prevUnits : 0;
    const qtyChange = prevUnits > 0 ? (lastUnits - prevUnits) / prevUnits : 0;
    const priceChange = prevPrice > 0 ? (lastPrice - prevPrice) / prevPrice : 0;
    const elasticity = prevUnits > 0 && prevPrice > 0 && priceChange !== 0 ? Number((qtyChange / priceChange).toFixed(2)) : null;
    return { ma7Revenue, momentumPct, elasticity };
  }, [dailySales]);

  const derived = useMemo(() => {
    const totalSku = inventory?.totalSkuCount ?? 0;
    const out = inventory?.outOfStockCount ?? 0;
    const low = inventory?.lowStockCount ?? 0;
    const available = Math.max(totalSku - out, 0);
    return {
      revenuePerDay: summary ? summary.totalRevenue / selectedDays : 0,
      transactionsPerDay: summary ? summary.totalTransactions / selectedDays : 0,
      availablePct: totalSku > 0 ? (available / totalSku) * 100 : null,
      unavailablePct: totalSku > 0 ? (out / totalSku) * 100 : null,
      redZonePct: totalSku > 0 ? (low / totalSku) * 100 : null,
    };
  }, [inventory, summary, selectedDays]);

  const topRows = useMemo(() => {
    if (!topAdvanced) return [] as TopProductAdvancedItem[];
    if (topTab === "revenue") return topAdvanced.byRevenue ?? [];
    if (topTab === "units") return topAdvanced.byUnits ?? [];
    if (topTab === "velocity") return topAdvanced.byVelocity ?? [];
    return topAdvanced.byMarginImpact ?? [];
  }, [topAdvanced, topTab]);

  const validationRows = useMemo(
    () =>
      [
        validCompleteness ? { name: "Kompletnost", ...validCompleteness } : null,
        validFreshness ? { name: "Svezina", ...validFreshness } : null,
        validLostSales ? { name: "Izgubljena prodaja", ...validLostSales } : null,
      ].filter((x): x is { name: string } & DashboardValidationEndpoint => x !== null),
    [validCompleteness, validFreshness, validLostSales]
  );

  return (
    <div className="analytics-dashboard">
      <header className="analytics-header">
        <div>
          <h1>Analitika - Pregled</h1>
          <p className="with-tip">
            <span>Pregled KPI + detaljna analiza</span>
            <InfoTip text="Gore su najvazniji brojevi za odluku, dole su detaljne analize i tabele." />
          </p>
        </div>
        <div className="analytics-controls">
          <select value={preset} onChange={(e) => applyPreset(e.target.value as DatePreset)}>
            <option value="today">Danas</option>
            <option value="yesterday">Juce</option>
            <option value="7d">Poslednjih 7 dana</option>
            <option value="30d">Poslednjih 30 dana</option>
            <option value="90d">Poslednjih 90 dana</option>
            <option value="thisMonth">Ovaj mesec</option>
            <option value="lastMonth">Prosli mesec</option>
            <option value="custom">Prilagodjeno</option>
          </select>
          <button onClick={() => void load()} disabled={loading}>Osvezi</button>
        </div>
      </header>

      {preset === "custom" && (
        <section className="analytics-panel analytics-custom-range">
          <label>Od<input type="datetime-local" value={fromDate} onChange={(e) => setFromDate(e.target.value)} /></label>
          <label>Do<input type="datetime-local" value={toDate} onChange={(e) => setToDate(e.target.value)} /></label>
          <button onClick={() => void load()} disabled={loading}>Primeni</button>
        </section>
      )}

      {healthText && <div className="analytics-health">{healthText}</div>}
      {isInvalidFilterRange && <div className="analytics-empty warning">Proverite filtere: neispravan vremenski opseg.</div>}
      {errors.length > 0 && (
        <section className="analytics-panel analytics-errors">
          <h3>Validacione poruke</h3>
          <ul>{errors.map((e, i) => <li key={`err-${i}`}>{e}</li>)}</ul>
        </section>
      )}

      <section className="analytics-section">
        <h2 className="with-tip"><span>Pregledni dashboard</span><InfoTip text="Kljucne metrike za brzo poslovno odlucivanje." /></h2>
        {loading && <div className="analytics-skeleton-grid">{Array.from({ length: 8 }).map((_, i) => <div key={i} className="analytics-skeleton-card" />)}</div>}
        {!loading && summary && (
          <div className="analytics-card-grid">
            <article className="metric-card good"><span className="metric-label"><span>Ukupan promet</span><InfoTip text={HELP.promet} /></span><strong>{formatCurrency(summary.totalRevenue)}</strong></article>
            <article className="metric-card neutral"><span className="metric-label"><span>Transakcije</span><InfoTip text={HELP.transakcije} /></span><strong>{formatNumber(summary.totalTransactions)}</strong></article>
            <article className="metric-card neutral"><span className="metric-label"><span>Prodate jedinice</span><InfoTip text={HELP.jedinice} /></span><strong>{formatNumber(summary.totalUnits)}</strong></article>
            <article className="metric-card neutral"><span>Promet po danu</span><strong>{formatCurrency(derived.revenuePerDay)}</strong></article>
            <article className="metric-card neutral"><span>Transakcije po danu</span><strong>{formatNumber(derived.transactionsPerDay, 1)}</strong></article>
            <article className={`metric-card ${statusTone(advancedByKey.get("completeness")?.status)}`}><span className="metric-label"><span>Dostupnost SKU</span><InfoTip text={HELP.sku} /></span><strong>{formatPercent(derived.availablePct)}</strong><small>Nedostupno: {formatPercent(derived.unavailablePct)}</small></article>
            <article className={`metric-card ${statusTone(advancedByKey.get("oos")?.status)}`}><span className="metric-label"><span>Crvena zona zaliha</span><InfoTip text={HELP.oos} /></span><strong>{formatPercent(derived.redZonePct)}</strong></article>
            <article className={`metric-card ${statusTone(advancedByKey.get("velocity")?.status)}`}><span className="metric-label"><span>MA7 + Momentum</span><InfoTip text={`${HELP.ma7} ${HELP.momentum}`} /></span><strong>{formatCurrency(movingStats.ma7Revenue)}</strong><small>{trendLabel(movingStats.momentumPct)} {formatPercent(movingStats.momentumPct)}</small></article>
            <article className="metric-card neutral"><span className="metric-label"><span>Elasticnost (aproks.)</span><InfoTip text={HELP.elasticnost} /></span><strong>{movingStats.elasticity == null ? "N/A" : formatNumber(movingStats.elasticity, 2)}</strong></article>
          </div>
        )}

        {!loading && advanced && (
          <div className="analytics-card-grid compact">
            {advanced.cards.map((c) => (
              <article key={c.key} className={`metric-card ${statusTone(c.status)}`}>
                <span className="metric-label"><span>{trAdvancedLabel(c.key, c.label)}</span><InfoTip text={HELP[c.key] ?? "Napredna BI metrika."} /></span>
                <strong>
                  {formatNumber(c.value, c.unit === "%" ? 1 : 2)}{" "}
                  {c.unit === "units/day" ? "kom/dan" : c.unit === "hours old" ? "sati od osvezavanja" : c.unit}
                </strong>
                <small>{c.trendPct != null ? `${trendLabel(c.trendPct)} ${formatPercent(c.trendPct)}` : statusLabel(c.status)}</small>
                {c.subtitle && <small>{trDynamic(c.subtitle)}</small>}
              </article>
            ))}
          </div>
        )}

        {!loading && advanced && (
          <div className="analytics-panels-2">
            <section className="analytics-panel">
              <h3 className="with-tip"><span>Uvidi</span><InfoTip text="Automatski izdvojeni najvazniji signali iz podataka." /></h3>
              <p className="section-note">Kratko objasnjenje sta se desava i zasto je vazno za posao.</p>
              {advanced.insights.length === 0 && <div className="analytics-empty">Nema podataka za panel uvida.</div>}
              {advanced.insights.map((item, idx) => (
                <div key={`ins-${idx}`} className={`insight-row ${item.color}`}>
                  <span className="badge">{item.badge}</span>
                  <p>{trDynamic(item.description)}</p>
                </div>
              ))}
            </section>

            <section className="analytics-panel">
              <h3 className="with-tip"><span>Preporucene akcije</span><InfoTip text="Prakticni koraci koji pomazu rastu prometa ili smanjenju rizika." /></h3>
              <p className="section-note">P1 je najhitnije, P3 je redovno pracenje.</p>
              {advanced.actions.length === 0 && <div className="analytics-empty">Sve je u redu.</div>}
              {advanced.actions.map((item, idx) => (
                <div key={`act-${idx}`} className="action-row">
                  <span className={`priority ${item.priority.toLowerCase()}`}>{item.priority}</span>
                  <div>
                    <strong>{trDynamic(item.title)}</strong>
                    <p>{trDynamic(item.recommendation)}</p>
                  </div>
                </div>
              ))}
            </section>
          </div>
        )}

        {!loading && validationRows.length > 0 && (
          <section className="analytics-panel">
            <h3 className="with-tip"><span>Backend validacije</span><InfoTip text="Tehnicke kontrole kvaliteta podataka: kompletnost, svezina i procena izgubljene prodaje." /></h3>
            <div className="validation-grid">
              {validationRows.map((v) => (
                <article key={v.name} className={`validation-card ${statusTone(v.status)}`}>
                  <div className="validation-head"><strong>{v.name}</strong><span>{statusLabel(v.status)}</span></div>
                  <p>{trDynamic(v.message)}</p>
                </article>
              ))}
              {(advanced?.validations ?? []).map((v, idx) => (
                <article key={`sv-${idx}`} className={`validation-card ${statusTone(v.severity)}`}>
                  <div className="validation-head"><strong>Sistem</strong><span>{statusLabel(v.severity)}</span></div>
                  <p>{trDynamic(v.message)}</p>
                </article>
              ))}
            </div>
          </section>
        )}
      </section>

      <section className="analytics-section">
        <h2 className="with-tip"><span>Detaljna analiza</span><InfoTip text="Detaljniji pogled po trendu, zalihama i top proizvodima." /></h2>

        {!loading && dailySales.length > 0 && (
          <section className="analytics-panel">
            <h3 className="with-tip"><span>Dnevni trend prodaje</span><InfoTip text="Linijski grafikon pokazuje kretanje prometa i transakcija po danima." /></h3>
            <p className="section-note">Koristite ovaj grafikon da brzo uocite dane pada/rasta i nestabilnosti.</p>
            <div className="chart-wrap">
              <ResponsiveContainer width="100%" height={320}>
                <LineChart data={dailySales}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#2a3350" />
                  <XAxis dataKey="date" tick={{ fill: "#9aa4c7", fontSize: 12 }} />
                  <YAxis tick={{ fill: "#9aa4c7", fontSize: 12 }} />
                  <Tooltip
                    contentStyle={{ background: "#131a31", border: "1px solid #2a3350", color: "#ecf1ff" }}
                    formatter={(value: number | string | undefined, name?: string) => [
                      name === "totalRevenue"
                        ? formatCurrency(typeof value === "number" ? value : Number(value ?? 0))
                        : formatNumber(typeof value === "number" ? value : Number(value ?? 0)),
                      name === "totalRevenue" ? "Promet" : "Transakcije",
                    ]}
                  />
                  <Line type="monotone" dataKey="totalRevenue" stroke="#3dd9a4" strokeWidth={2.5} dot={false} />
                  <Line type="monotone" dataKey="transactionCount" stroke="#6ea8ff" strokeWidth={2} dot={false} />
                </LineChart>
              </ResponsiveContainer>
            </div>
          </section>
        )}

        {!loading && inventory && (
          <section className="analytics-panel">
            <h3 className="with-tip"><span>Brzi pregled zaliha</span><InfoTip text="Ukupno stanje i signal rizika od rasprodatosti." /></h3>
            <div className="stock-grid">
              <article className="stock-card"><span className="metric-label"><span>Ukupno SKU</span><InfoTip text={HELP.sku} /></span><strong>{formatNumber(inventory.totalSkuCount)}</strong></article>
              <article className="stock-card"><span>Ukupno na stanju</span><strong>{formatNumber(inventory.totalOnHand)}</strong></article>
              <article className="stock-card warning"><span>Niska zaliha</span><strong>{formatNumber(inventory.lowStockCount)}</strong></article>
              <article className="stock-card critical"><span>Bez zaliha</span><strong>{formatNumber(inventory.outOfStockCount)}</strong></article>
            </div>
          </section>
        )}

        {!loading && topAdvanced && (
          <section className="analytics-panel">
            <h3 className="with-tip"><span>Top proizvodi</span><InfoTip text="Tabela sa vise pogleda: promet, komadi, brzina prodaje i marza." /></h3>
            <p className="section-note">Hover na red prikazuje sazetak trenda. Status zalihe je obojen radi brzeg skeniranja.</p>
            <div className="top-tabs">
              <button title="Rangiranje artikala po ukupnom prometu" className={topTab === "revenue" ? "active" : ""} onClick={() => setTopTab("revenue")}>Top po prometu</button>
              <button title="Rangiranje po broju prodatih komada" className={topTab === "units" ? "active" : ""} onClick={() => setTopTab("units")}>Top po komadima</button>
              <button title="Rangiranje po prosecno prodatim komadima dnevno" className={topTab === "velocity" ? "active" : ""} onClick={() => setTopTab("velocity")}>Top po brzini prodaje</button>
              <button title="Rangiranje po procenjenom uticaju na marzu" className={topTab === "margin" ? "active" : ""} onClick={() => setTopTab("margin")}>Top po marzi</button>
            </div>
            {topTab === "margin" && !topAdvanced.marginAvailable && (
              <div className="analytics-empty warning">Nema dovoljno podataka za prikaz uticaja na marzu.</div>
            )}
            {topRows.length === 0 && <div className="analytics-empty">Nema podataka.</div>}
            {topRows.length > 0 && (
              <div className="top-table-wrap">
                <table className="top-table">
                  <thead>
                    <tr>
                      <th><span className="with-tip"><span>SKU / Artikal</span><InfoTip text={HELP.sku} /></span></th>
                      <th><span className="with-tip"><span>Promet</span><InfoTip text={HELP.promet} /></span></th>
                      <th><span className="with-tip"><span>Kom</span><InfoTip text={HELP.jedinice} /></span></th>
                      <th><span className="with-tip"><span>Brzina prodaje</span><InfoTip text={HELP.velocity} /></span></th>
                      <th><span className="with-tip"><span>Uticaj na marzu</span><InfoTip text={HELP.margin} /></span></th>
                      <th><span className="with-tip"><span>Trend</span><InfoTip text={HELP.trend} /></span></th>
                      <th><span className="with-tip"><span>Status zalihe</span><InfoTip text="Dobro = stabilno, Upozorenje = niska zaliha, Kriticno = rasprodato." /></span></th>
                    </tr>
                  </thead>
                  <tbody>
                    {topRows.map((row) => (
                      <tr
                        key={`${topTab}-${row.productId}`}
                        title={`Trend: ${formatPercent(row.trendPct)} | Promet: ${formatCurrency(row.revenue)} | Komada: ${formatNumber(row.units)}`}
                      >
                        <td><div className="sku-cell"><strong>{row.sku}</strong><span>{row.productName}</span></div></td>
                        <td>{formatCurrency(row.revenue)}</td>
                        <td>{formatNumber(row.units)}</td>
                        <td>{formatNumber(row.velocityUnitsPerDay, 2)}</td>
                        <td>{row.marginImpact == null ? "N/A" : formatCurrency(row.marginImpact)}</td>
                        <td className={row.trendPct != null && row.trendPct < 0 ? "trend down" : "trend up"}>{trendLabel(row.trendPct)} {formatPercent(row.trendPct)}</td>
                        <td><span className={`stock-pill ${statusTone(row.stockStatus)}`}>{statusLabel(row.stockStatus)}</span></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        )}

        {!loading && (
          <section className="analytics-panel">
            <h3 className="with-tip"><span>Pojmovnik za laike</span><InfoTip text="Kratka objasnjenja manje poznatih analitickih izraza." /></h3>
            <div className="glossary-grid">
              {[
                ["Brzina prodaje (Velocity)", HELP.velocity],
                ["OOS", HELP.oos],
                ["Pareto", HELP.pareto],
                ["MA7", HELP.ma7],
                ["Momentum", HELP.momentum],
                ["Elasticnost", HELP.elasticnost],
                ["Kompletnost (Completeness)", HELP.completeness],
                ["Svezina podataka (Data Health)", HELP.freshness],
                ["Uticaj na marzu (Margin impact)", HELP.margin],
                ["SKU", HELP.sku],
              ].map(([term, text]) => (
                <article key={term} className="glossary-card">
                  <strong>{term}</strong>
                  <p>{text}</p>
                </article>
              ))}
            </div>
          </section>
        )}
      </section>
    </div>
  );
}

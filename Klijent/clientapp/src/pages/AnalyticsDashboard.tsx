import { lazy, Suspense, useCallback, useEffect, useMemo, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import {
  checkAnalyticsHealth,
  getDashboardBootstrap,
  getStores,
} from "../services/analyticsApi";
import type {
  CategoryData,
  DailySale,
  DashboardAdvancedSnapshot,
  DashboardMetricCard,
  DashboardValidationEndpoint,
  GenderData,
  HourData,
  InventoryStatus,
  PaymentData,
  QuickInsights,
  SalesSummary,
  StoreOption,
  SupplierData,
  SupplierFilterOption,
  TopProductAdvancedItem,
  TopProductsAdvancedResult,
  TransactionStats,
  WeekdayData,
} from "../types/analytics";
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import InfoTip from "../components/ui/InfoTip";
import { buildAnalyticsDetailSnapshot, saveAnalyticsDetailSnapshot } from "../services/analyticsTableState";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import {
  ANALYTICS_PERIOD_PRESET_OPTIONS,
  type AnalyticsPeriodPreset,
  getAnalyticsPeriodPresetRange,
} from "../utils/analyticsPeriodPresets";
import "./AnalyticsDashboard.css";

type TopTabKey = "revenue" | "units" | "velocity" | "margin";
type Tone = "good" | "warning" | "critical" | "neutral";

const AnalyticsDashboardCharts = lazy(() => import("../components/analytics/AnalyticsDashboardCharts"));

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

const DEFAULT_WEEKDAYS = ["Nedelja", "Ponedeljak", "Utorak", "Sreda", "Cetvrtak", "Petak", "Subota"];

const topProductColumns: AnalyticsTableColumn<TopProductAdvancedItem>[] = [
  { key: "sku", header: "SKU", dataType: "text" },
  { key: "productName", header: "Artikal", dataType: "text" },
  { key: "revenue", header: "Promet", dataType: "currency" },
  { key: "units", header: "Kom", dataType: "number" },
  { key: "velocityUnitsPerDay", header: "Brzina prodaje", dataType: "number" },
  { key: "marginImpact", header: "Uticaj na marzu", dataType: "currency" },
  { key: "trendPct", header: "Trend %", dataType: "percent" },
  { key: "stockStatus", header: "Status zalihe", dataType: "text" },
];

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
  return `${new Intl.NumberFormat("sr-RS", { minimumFractionDigits: 0, maximumFractionDigits: 2 }).format(value)} RSD`;
}

function formatNumber(value: number, digits = 0): string {
  return new Intl.NumberFormat("sr-RS", { minimumFractionDigits: digits, maximumFractionDigits: digits }).format(value);
}

function formatPercent(value?: number | null, digits = 1): string {
  if (value == null || Number.isNaN(value)) return "N/A";
  return `${formatNumber(value, digits)}%`;
}

function trendLabel(value?: number | null): string {
  if (value == null) return "Nema trenda";
  return value >= 0 ? "Rast" : "Pad";
}

function buildStoreLabel(store: StoreOption): string {
  const extras = [store.city, store.region].filter(Boolean).join(", ");
  return extras ? `${store.storeName} (${extras})` : store.storeName;
}

function downloadCsv(filename: string, content: string) {
  const blob = new Blob([content], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
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
    stable.push("Neki analytics upiti su privremeno prekinuti. Osvezite stranicu za kompletne podatke.");
  }

  return stable;
}

function MetricCard(props: { label: string; value: string; tone?: Tone; infoTip?: string }) {
  return (
    <article className={`metric-card ${props.tone ?? "neutral"}`}>
      <span className="metric-label">
        <span>{props.label}</span>
        {props.infoTip ? <InfoTip text={props.infoTip} /> : null}
      </span>
      <strong>{props.value}</strong>
    </article>
  );
}

export default function AnalyticsDashboard() {
  const navigate = useNavigate();
  const location = useLocation();
  const [preset, setPreset] = useState<AnalyticsPeriodPreset>("30d");
  const initialRange = getAnalyticsPeriodPresetRange("30d");
  const [fromDate, setFromDate] = useState<string>(() => `${initialRange.fromDate}T00:00`);
  const [toDate, setToDate] = useState<string>(() => `${initialRange.toDate}T23:59`);
  const [selectedStore, setSelectedStore] = useState("");
  const [selectedSupplier, setSelectedSupplier] = useState("");
  const [stores, setStores] = useState<StoreOption[]>([]);
  const [supplierOptions, setSupplierOptions] = useState<SupplierFilterOption[]>([]);
  const [summary, setSummary] = useState<SalesSummary | null>(null);
  const [inventory, setInventory] = useState<InventoryStatus | null>(null);
  const [dailySales, setDailySales] = useState<DailySale[]>([]);
  const [categoryData, setCategoryData] = useState<CategoryData[]>([]);
  const [genderData, setGenderData] = useState<GenderData[]>([]);
  const [supplierData, setSupplierData] = useState<SupplierData[]>([]);
  const [weekdayData, setWeekdayData] = useState<WeekdayData[]>([]);
  const [hourData, setHourData] = useState<HourData[]>([]);
  const [paymentData, setPaymentData] = useState<PaymentData[]>([]);
  const [quickInsights, setQuickInsights] = useState<QuickInsights | null>(null);
  const [transactionStats, setTransactionStats] = useState<TransactionStats | null>(null);
  const [advanced, setAdvanced] = useState<DashboardAdvancedSnapshot | null>(null);
  const [topAdvanced, setTopAdvanced] = useState<TopProductsAdvancedResult | null>(null);
  const [validCompleteness, setValidCompleteness] = useState<DashboardValidationEndpoint | null>(null);
  const [validFreshness, setValidFreshness] = useState<DashboardValidationEndpoint | null>(null);
  const [validLostSales, setValidLostSales] = useState<DashboardValidationEndpoint | null>(null);
  const [healthText, setHealthText] = useState("");
  const [topTab, setTopTab] = useState<TopTabKey>("revenue");
  const [errors, setErrors] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const [showDetailedAnalysis, setShowDetailedAnalysis] = useState(false);

  const isInvalidFilterRange = useMemo(() => parseInputDate(fromDate) > parseInputDate(toDate), [fromDate, toDate]);
  const selectedDays = useMemo(() => {
    const diff = parseInputDate(toDate).getTime() - parseInputDate(fromDate).getTime();
    return Math.max(Math.floor(diff / (24 * 60 * 60 * 1000)) + 1, 1);
  }, [fromDate, toDate]);
  const storeId = useMemo(() => (selectedStore ? Number(selectedStore) : undefined), [selectedStore]);
  const supplierId = useMemo(() => (selectedSupplier ? Number(selectedSupplier) : undefined), [selectedSupplier]);

  const applyPreset = useCallback((value: AnalyticsPeriodPreset) => {
    setPreset(value);
    const range = value === "custom" ? null : getAnalyticsPeriodPresetRange(value);
    if (!range) return;
    setFromDate(`${range.fromDate}T00:00`);
    setToDate(`${range.toDate}T23:59`);
  }, []);

  const loadStores = useCallback(async () => {
    try {
      setStores(await getStores(true));
    } catch {
      setStores([]);
    }
  }, []);

  const loadHealth = useCallback(async () => {
    try {
      const health = await checkAnalyticsHealth();
      setHealthText(
        `Analytics baza: ${health.tables.salesFacts} prodaja, ${health.tables.salesLineFacts} stavki, ${health.tables.productsDim} proizvoda`
      );
    } catch (error) {
      setHealthText("");
      setErrors((current) =>
        compactErrorMessages([
          ...current,
          getErrorText(error, "Provera zdravstvenog stanja podataka nije dostupna."),
        ])
      );
    }
  }, []);

  const load = useCallback(async () => {
    if (isInvalidFilterRange) {
      setErrors(["Proverite filtere: datum od ne moze biti posle datuma do."]);
      return;
    }

    setLoading(true);
    setShowDetailedAnalysis(false);
    setErrors([]);

    const bootstrapR = await Promise.allSettled([
      getDashboardBootstrap(fromDate, toDate, true, storeId, supplierId),
    ]).then(([result]) => result);

    const nextErrors: string[] = [];
    if (bootstrapR.status === "fulfilled") {
      setSummary(bootstrapR.value.summary);
      setInventory(bootstrapR.value.inventory);
      setDailySales(bootstrapR.value.dailySales);
      setCategoryData(bootstrapR.value.categoryData);
      setGenderData(bootstrapR.value.genderData);
      setSupplierData(bootstrapR.value.supplierData);
      setSupplierOptions(bootstrapR.value.supplierOptions);
      setWeekdayData(bootstrapR.value.weekdayData);
      setHourData(bootstrapR.value.hourData);
      setPaymentData(bootstrapR.value.paymentData);
      setQuickInsights(bootstrapR.value.quickInsights);
      setTransactionStats(bootstrapR.value.transactionStats);
      setAdvanced(bootstrapR.value.advanced);
      setTopAdvanced(bootstrapR.value.topAdvanced);
      setValidCompleteness(bootstrapR.value.validationCompleteness);
      setValidFreshness(bootstrapR.value.validationFreshness);
      setValidLostSales(bootstrapR.value.validationLostSales);
      nextErrors.push(...bootstrapR.value.errors);
    } else {
      setSummary(null);
      setInventory(null);
      setDailySales([]);
      setCategoryData([]);
      setGenderData([]);
      setSupplierData([]);
      setSupplierOptions([]);
      setWeekdayData([]);
      setHourData([]);
      setPaymentData([]);
      setQuickInsights(null);
      setTransactionStats(null);
      setAdvanced(null);
      setTopAdvanced(null);
      setValidCompleteness(null);
      setValidFreshness(null);
      setValidLostSales(null);
      nextErrors.push(getErrorText(bootstrapR.reason, "Analytics dashboard bootstrap nije ucitan."));
    }

    setErrors(compactErrorMessages(nextErrors));
    setLoading(false);
    window.setTimeout(() => void loadHealth(), 0);
  }, [fromDate, isInvalidFilterRange, loadHealth, storeId, supplierId, toDate]);

  useEffect(() => {
    void loadStores();
  }, [loadStores]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (!selectedSupplier || supplierOptions.length === 0) return;
    if (supplierOptions.some((item) => item.supplierId === supplierId)) return;
    setSelectedSupplier("");
  }, [selectedSupplier, supplierId, supplierOptions]);

  useEffect(() => {
    if (loading || !summary) return;

    let cancelled = false;
    const show = () => {
      if (!cancelled) setShowDetailedAnalysis(true);
    };
    const idleWindow = window as Window & {
      requestIdleCallback?: (callback: () => void, options?: { timeout?: number }) => number;
      cancelIdleCallback?: (id: number) => void;
    };
    const usedIdleCallback = typeof idleWindow.requestIdleCallback === "function";
    const idleId = usedIdleCallback
      ? idleWindow.requestIdleCallback(show, { timeout: 900 })
      : window.setTimeout(show, 180);

    return () => {
      cancelled = true;
      if (usedIdleCallback && typeof idleWindow.cancelIdleCallback === "function") {
        idleWindow.cancelIdleCallback(idleId);
      } else {
        window.clearTimeout(idleId);
      }
    };
  }, [loading, summary]);

  const movingStats = useMemo(() => {
    if (dailySales.length === 0) return { ma7Revenue: 0, momentumPct: null as number | null, elasticity: null as number | null };
    const sorted = [...dailySales].sort((a, b) => a.date.localeCompare(b.date));
    const last7 = sorted.slice(-7);
    const prev7 = sorted.slice(-14, -7);
    const sumRevenue = (items: DailySale[]) => items.reduce((acc, item) => acc + item.totalRevenue, 0);
    const sumUnits = (items: DailySale[]) => items.reduce((acc, item) => acc + item.totalUnits, 0);
    const lastRevenue = sumRevenue(last7);
    const prevRevenue = sumRevenue(prev7);
    const lastUnits = sumUnits(last7);
    const prevUnits = sumUnits(prev7);
    const ma7Revenue = last7.length > 0 ? lastRevenue / last7.length : 0;
    const momentumPct = prevRevenue > 0 ? Number((((lastRevenue - prevRevenue) / prevRevenue) * 100).toFixed(2)) : null;
    const lastPrice = lastUnits > 0 ? lastRevenue / lastUnits : 0;
    const prevPrice = prevUnits > 0 ? prevRevenue / prevUnits : 0;
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
      redZonePct: totalSku > 0 ? (low / totalSku) * 100 : null,
    };
  }, [inventory, selectedDays, summary]);

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
      ].filter((item): item is { name: string } & DashboardValidationEndpoint => item !== null),
    [validCompleteness, validFreshness, validLostSales]
  );

  const categoryPieData = useMemo(() => {
    const totals = new Map<string, number>();
    for (const item of categoryData) totals.set(item.kategorija, (totals.get(item.kategorija) ?? 0) + item.totalRevenue);
    return Array.from(totals.entries()).map(([name, value]) => ({ name, value })).sort((a, b) => b.value - a.value);
  }, [categoryData]);

  const genderPieData = useMemo(
    () => genderData.map((item) => ({ name: item.pol || "Neodredjeno", value: item.totalRevenue })).sort((a, b) => b.value - a.value),
    [genderData]
  );

  const supplierBarData = useMemo(
    () => supplierData.slice().sort((a, b) => b.totalRevenue - a.totalRevenue).slice(0, 10).map((item) => ({ name: item.dobavljacNaziv, totalRevenue: item.totalRevenue })),
    [supplierData]
  );

  const weekdayChartData = useMemo(() => {
    const byDay = new Map<number, WeekdayData>();
    for (const item of weekdayData) byDay.set(item.dayOfWeek, item);
    return DEFAULT_WEEKDAYS.map((dayName, dayOfWeek) => ({
      dayOfWeek,
      dayName: byDay.get(dayOfWeek)?.dayName ?? dayName,
      totalRevenue: byDay.get(dayOfWeek)?.totalRevenue ?? 0,
    }));
  }, [weekdayData]);

  const hourChartData = useMemo(() => {
    const byHour = new Map<number, HourData>();
    for (const item of hourData) byHour.set(item.hour, item);
    return Array.from({ length: 24 }, (_, hour) => ({
      hour,
      label: `${String(hour).padStart(2, "0")}:00`,
      totalRevenue: byHour.get(hour)?.totalRevenue ?? 0,
    }));
  }, [hourData]);

  const paymentChartData = useMemo(
    () => paymentData.slice().sort((a, b) => b.totalRevenue - a.totalRevenue).map((item) => ({ name: item.nacinPlacanja || "Nepoznato", totalRevenue: item.totalRevenue })),
    [paymentData]
  );

  const exportTopRows = useCallback(() => {
    if (topRows.length === 0) return;
    const lines = [
      ["SKU", "Artikal", "Promet", "Komadi", "Brzina prodaje", "Uticaj na marzu", "Trend", "Status zalihe"],
      ...topRows.map((row) => [
        row.sku,
        row.productName,
        row.revenue.toFixed(2),
        row.units.toString(),
        row.velocityUnitsPerDay.toFixed(2),
        row.marginImpact == null ? "" : row.marginImpact.toFixed(2),
        row.trendPct == null ? "" : row.trendPct.toFixed(2),
        statusLabel(row.stockStatus),
      ]),
    ];
    downloadCsv(`analytics-top-proizvodi-${topTab}.csv`, lines.map((line) => line.map((value) => `"${String(value).replaceAll("\"", "\"\"")}"`).join(",")).join("\n"));
  }, [topRows, topTab]);

  const topTableFilters = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "fromDate", label: "Od", value: fromDate },
      { key: "toDate", label: "Do", value: toDate },
      { key: "storeId", label: "Prodavnica", value: storeId ?? "" },
      { key: "supplierId", label: "Dobavljac", value: supplierId ?? "" },
    ],
    [fromDate, supplierId, storeId, toDate]
  );

  const topTableMetadata = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "topTab", label: "Top pogled", value: topTab },
      { key: "generatedAt", label: "Health text", value: healthText },
    ],
    [healthText, topTab]
  );

  const openTopProductDetail = (row: TopProductAdvancedItem) => {
    const params = new URLSearchParams();
    params.set("fromDate", fromDate);
    params.set("toDate", toDate);
    if (storeId != null) params.set("storeId", String(storeId));
    if (supplierId != null) params.set("supplierId", String(supplierId));

    saveAnalyticsDetailSnapshot(
      buildAnalyticsDetailSnapshot({
        table: "top-products",
        recordId: String(row.productId),
        title: row.productName,
        subtitle: row.sku,
        columns: topProductColumns,
        row,
        metadata: topTableFilters,
      })
    );

    navigate(`/analitika/top-products/${row.productId}?${params.toString()}`, {
      state: { backgroundLocation: location },
    });
  };

  return (
    <div className="analytics-dashboard">
      <header className="analytics-header">
        <div>
          <h1>Pregled analitike</h1>
          <p className="with-tip">
            <span>KPI, trendovi i preporuke za odluke</span>
            <InfoTip text="Dashboard je fokusiran na promet, raspodelu prodaje i brze operativne odluke." />
          </p>
        </div>
        <div className="analytics-controls">
          <button onClick={() => void load()} disabled={loading}>{loading ? "Ucitavanje..." : "Osvezi"}</button>
        </div>
      </header>

      <section className="analytics-panel analytics-filter-bar">
        <div className="analytics-filter-grid">
          <label>
            Period
            <select value={preset} onChange={(e) => applyPreset(e.target.value as AnalyticsPeriodPreset)}>
              {ANALYTICS_PERIOD_PRESET_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>
          {preset === "custom" && (
            <>
              <label>
                Datum od
                <input type="datetime-local" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
              </label>
              <label>
                Datum do
                <input type="datetime-local" value={toDate} onChange={(e) => setToDate(e.target.value)} />
              </label>
            </>
          )}
          <label>
            Prodavnica
            <select value={selectedStore} onChange={(e) => setSelectedStore(e.target.value)}>
              <option value="">Sve prodavnice</option>
              {stores.map((store) => (
                <option key={store.storeId} value={store.storeId}>{buildStoreLabel(store)}</option>
              ))}
            </select>
          </label>
          <label>
            Dobavljac
            <select value={selectedSupplier} onChange={(e) => setSelectedSupplier(e.target.value)}>
              <option value="">Svi dobavljaci</option>
              {supplierOptions.map((supplier) => (
                <option key={supplier.supplierId} value={supplier.supplierId}>
                  {supplier.supplierName}
                </option>
              ))}
            </select>
          </label>
        </div>
        <div className="filter-chip-row">
          <span className="filter-chip">Opseg: {selectedDays} dana</span>
          <span className="filter-chip">Prodavnica: {storeId == null ? "Sve" : buildStoreLabel(stores.find((item) => item.storeId === storeId) ?? { storeId, storeName: `Prodavnica ${storeId}` })}</span>
          <span className="filter-chip">Dobavljac: {selectedSupplier ? supplierOptions.find((item) => item.supplierId === supplierId)?.supplierName ?? `ID ${supplierId}` : "Svi"}</span>
        </div>
      </section>

      {healthText && <div className="analytics-health">{healthText}</div>}
      {isInvalidFilterRange && <div className="analytics-empty warning">Proverite filtere: neispravan vremenski opseg.</div>}
      {errors.length > 0 && (
        <section className="analytics-panel analytics-errors">
          <h3>Validacione poruke</h3>
          <ul>{errors.map((error, index) => <li key={`err-${index}`}>{error}</li>)}</ul>
        </section>
      )}

      <section className="analytics-section">
        <h2 className="with-tip"><span>Pregledni dashboard</span><InfoTip text="Kljucne metrike za brzo poslovno odlucivanje." /></h2>
        {loading && <div className="analytics-skeleton-grid">{Array.from({ length: 8 }).map((_, i) => <div key={i} className="analytics-skeleton-card" />)}</div>}
        {!loading && summary && (
          <div className="analytics-card-grid">
            <MetricCard label="Ukupan promet" value={formatCurrency(summary.totalRevenue)} tone="good" infoTip={HELP.promet} />
            <MetricCard label="Transakcije" value={formatNumber(summary.totalTransactions)} infoTip={HELP.transakcije} />
            <MetricCard label="Prodate jedinice" value={formatNumber(summary.totalUnits)} infoTip={HELP.jedinice} />
            <MetricCard label="Promet po danu" value={formatCurrency(derived.revenuePerDay)} />
            <MetricCard label="Transakcije po danu" value={formatNumber(derived.transactionsPerDay, 1)} />
            <MetricCard label="Dostupnost SKU" value={formatPercent(derived.availablePct)} tone="good" infoTip={HELP.sku} />
            <MetricCard label="Crvena zona zaliha" value={formatPercent(derived.redZonePct)} tone="warning" infoTip={HELP.oos} />
            <MetricCard label="MA7 + Momentum" value={formatCurrency(movingStats.ma7Revenue)} tone="good" infoTip={HELP.ma7} />
            <MetricCard label="Elasticnost (aproks.)" value={movingStats.elasticity == null ? "N/A" : formatNumber(movingStats.elasticity, 2)} tone="neutral" infoTip={HELP.elasticnost} />
            <MetricCard label="Prosecna korpa" value={formatCurrency(summary.avgBasketValue)} tone="neutral" infoTip="Prosecna vrednost jednog racuna." />
          </div>
        )}

        {!loading && (quickInsights || transactionStats) && (
          <div className="analytics-card-grid compact">
            <MetricCard label="Najjaci dan" value={quickInsights?.bestDay ?? "N/A"} tone="good" infoTip="Dan u nedelji sa najvecim prometom." />
            <MetricCard label="Promet najboljeg dana" value={formatCurrency(quickInsights?.bestDayRevenue ?? 0)} tone="good" />
            <MetricCard label="Top proizvod" value={quickInsights?.topProduct ?? "N/A"} tone="neutral" />
            <MetricCard label="Stavki po transakciji" value={transactionStats ? formatNumber(transactionStats.avgItemsPerTransaction, 2) : "N/A"} tone="neutral" />
            <MetricCard label="Vrednost transakcije" value={transactionStats ? formatCurrency(transactionStats.avgTransactionValue) : "N/A"} tone="neutral" />
          </div>
        )}

        {!loading && advanced && (
          <div className="analytics-card-grid compact">
            {advanced.cards.map((card: DashboardMetricCard) => (
              <article key={card.key} className={`metric-card ${statusTone(card.status)}`}>
                <span className="metric-label"><span>{card.key === "velocity" ? "Brzina prodaje (velocity)" : card.key === "oos" ? "Rasprodato (OOS)" : card.key === "pareto" ? "Pareto koncentracija" : card.key === "data_health" ? "Svezina podataka" : card.key === "completeness" ? "Kompletnost podataka" : card.label}</span><InfoTip text={HELP[card.key] ?? "Napredna BI metrika."} /></span>
                <strong>{formatNumber(card.value, card.unit === "%" ? 1 : 2)} {card.unit === "units/day" ? "kom/dan" : card.unit === "hours old" ? "sati od osvezavanja" : card.unit}</strong>
                <small>{card.trendPct != null ? `${trendLabel(card.trendPct)} ${formatPercent(card.trendPct)}` : statusLabel(card.status)}</small>
                {card.subtitle && <small>{card.subtitle.replace("Top SKU:", "Top sifra:").replace("Lost sales estimate:", "Procena izgubljene prodaje:").replace("Top 50 share:", "Udeo top 50:").replace("Last import:", "Poslednji import:").replace("Missing:", "Nedostajuca polja:")}</small>}
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
              {advanced.insights.map((item, index) => (
                <div key={`ins-${index}`} className={`insight-row ${item.color}`}>
                  <span className="badge">{item.badge}</span>
                  <p>{item.description.replace("Lost sales estimate indicates stock-out pressure.", "Procena izgubljene prodaje ukazuje na pritisak rasprodatosti.").replace("Pareto concentration is elevated.", "Pareto koncentracija je povecana.")}</p>
                </div>
              ))}
            </section>

            <section className="analytics-panel">
              <h3 className="with-tip"><span>Preporucene akcije</span><InfoTip text="Prakticni koraci koji pomazu rastu prometa ili smanjenju rizika." /></h3>
              <p className="section-note">P1 je najhitnije, P3 je redovno pracenje.</p>
              {advanced.actions.length === 0 && <div className="analytics-empty">Sve je u redu.</div>}
              {advanced.actions.map((item, index) => (
                <div key={`act-${index}`} className="action-row">
                  <span className={`priority ${item.priority.toLowerCase()}`}>{item.priority}</span>
                  <div>
                    <strong>{item.title.replace("Replenishment", "Dopuna zaliha").replace("Portfolio balance", "Balans asortimana")}</strong>
                    <p>{item.recommendation.replace("Refresh pipeline", "Osvezavanje pipeline-a").replace("Data quality fix", "Ispravka kvaliteta podataka").replace("Monitor", "Pracenje")}</p>
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
              {validationRows.map((row) => (
                <article key={row.name} className={`validation-card ${statusTone(row.status)}`}>
                  <div className="validation-head"><strong>{row.name}</strong><span>{statusLabel(row.status)}</span></div>
                  <p>{row.message}</p>
                </article>
              ))}
              {(advanced?.validations ?? []).map((item, index) => (
                <article key={`sv-${index}`} className={`validation-card ${statusTone(item.severity)}`}>
                  <div className="validation-head"><strong>Sistem</strong><span>{statusLabel(item.severity)}</span></div>
                  <p>{item.message}</p>
                </article>
              ))}
            </div>
          </section>
        )}
      </section>

      <section className="analytics-section">
        <h2 className="with-tip"><span>Detaljna analiza</span><InfoTip text="Detaljniji pogled po trendu, raspodeli prodaje, zalihama i top proizvodima." /></h2>
        {!loading && summary && !showDetailedAnalysis && (
          <section className="analytics-panel">
            <div className="analytics-skeleton-grid">
              {Array.from({ length: 4 }).map((_, i) => <div key={i} className="analytics-skeleton-card" />)}
            </div>
          </section>
        )}
        {showDetailedAnalysis && (
          <>
        <Suspense fallback={
          <section className="analytics-panel">
            <div className="analytics-skeleton-grid">
              {Array.from({ length: 4 }).map((_, i) => <div key={i} className="analytics-skeleton-card" />)}
            </div>
          </section>
        }>
          <AnalyticsDashboardCharts
            dailySales={dailySales}
            categoryPieData={categoryPieData}
            genderPieData={genderPieData}
            supplierBarData={supplierBarData}
            weekdayChartData={weekdayChartData}
            hourChartData={hourChartData}
            paymentChartData={paymentChartData}
            formatCurrency={formatCurrency}
            formatNumber={formatNumber}
          />
        </Suspense>
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
            <div className="panel-head">
              <div>
                <h3 className="with-tip"><span>Top proizvodi</span><InfoTip text="Tabela sa vise pogleda: promet, komada, brzina prodaje i marza." /></h3>
                <p className="section-note">Hover na red prikazuje sazetak trenda. Status zalihe je obojen radi brzeg skeniranja.</p>
              </div>
              <div className="flex flex-wrap items-center gap-2">
                <button className="analytics-export-button" onClick={exportTopRows} disabled={topRows.length === 0}>Izvezi CSV</button>
                <AnalyticsTableToolbar
                  tableKey="top-products"
                  tableTitle="Analytics top proizvodi"
                  columns={topProductColumns}
                  rows={topRows}
                  filters={topTableFilters}
                  metadata={topTableMetadata}
                  defaultOrientation="landscape"
                />
              </div>
            </div>
            <div className="top-tabs">
              <button className={topTab === "revenue" ? "active" : ""} onClick={() => setTopTab("revenue")}>Top po prometu</button>
              <button className={topTab === "units" ? "active" : ""} onClick={() => setTopTab("units")}>Top po komadima</button>
              <button className={topTab === "velocity" ? "active" : ""} onClick={() => setTopTab("velocity")}>Top po brzini prodaje</button>
              <button className={topTab === "margin" ? "active" : ""} onClick={() => setTopTab("margin")}>Top po marzi</button>
            </div>
            {topTab === "margin" && !topAdvanced.marginAvailable && <div className="analytics-empty warning">Nema dovoljno podataka za prikaz uticaja na marzu.</div>}
            {topRows.length === 0 ? <div className="analytics-empty">Nema podataka.</div> : (
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
                        className="cursor-pointer"
                        onClick={() => openTopProductDetail(row)}
                        onKeyDown={(e) => {
                          if (e.key === "Enter" || e.key === " ") {
                            e.preventDefault();
                            openTopProductDetail(row);
                          }
                        }}
                        tabIndex={0}
                        aria-label={`Otvori detalj artikla ${row.productName}`}
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
          </>
        )}
      </section>
    </div>
  );
}

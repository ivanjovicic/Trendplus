import { lazy, Suspense, useCallback, useEffect, useMemo, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import {
  checkAnalyticsHealth,
  getAnalyticsRefreshStatus,
  getDashboardBootstrap,
  getStores,
  upsertAnalyticsAction,
} from "../services/analyticsApi";
import AnalyticsEmptyState from "../components/analytics/AnalyticsEmptyState";
import AnalyticsErrorState from "../components/analytics/AnalyticsErrorState";
import AnalyticsRefreshStatusBanner from "../components/analytics/AnalyticsRefreshStatusBanner";
import type {
  AnalyticsResponseMeta,
  AnalyticsRefreshStatus,
  AnalyticsActionSourceType,
  AnalyticsActionUpsertInput,
  CategoryData,
  DailySale,
  DashboardAdvancedSnapshot,
  DashboardDecisionAction,
  DashboardMetricCard,
  DashboardValidationEndpoint,
  ExecutiveDashboardSnapshot,
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
import AnalyticsTrustHeader from "../components/analytics/AnalyticsTrustHeader";
import ExecutiveKpiRow from "../components/analytics/ExecutiveKpiRow";
import InfoTip from "../components/ui/InfoTip";
import { buildAnalyticsDetailSnapshot, saveAnalyticsDetailSnapshot } from "../services/analyticsTableState";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import {
  ANALYTICS_PERIOD_PRESET_OPTIONS,
  type AnalyticsPeriodPreset,
  getAnalyticsPeriodPresetRange,
} from "../utils/analyticsPeriodPresets";
import {
  normalizeRecommendationPct,
  normalizeRecommendationQualityStatus,
} from "../utils/canonicalRecommendationSemantics";
import { fmtNumber, fmtPct, fmtRsd } from "../utils/analyticsFormatters";
import {
  getAnalyticsMetaMessage,
  isAnalyticsMetaInsufficient,
  isAnalyticsMetaError,
  isAnalyticsMetaWarning,
  shouldShowAnalyticsEmptyState,
} from "../utils/analyticsResponseMeta";
import {
  dataQualityStatusLabel,
  formatConfidence as formatDecisionConfidence,
  normalizeDataQualityStatus,
} from "../utils/analyticsQuality";
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

function decisionPriorityRank(priority?: string | null): number {
  if (priority === "P1") return 1;
  if (priority === "P2") return 2;
  return 3;
}

type DashboardActionFilters = {
  fromDate: string;
  toDate: string;
  storeId?: number;
  supplierId?: number;
};

const DASHBOARD_ACTION_SOURCE_TYPES: AnalyticsActionSourceType[] = [
  "dashboard",
  "product",
  "supplier",
  "inventory",
  "nivelacija",
  "data_quality",
];

function formatActionDateKey(value: string): string {
  if (!value) return "all";
  return value.slice(0, 10);
}

function sanitizeActionKeyPart(value: string | null | undefined): string {
  const normalized = (value ?? "")
    .trim()
    .toLowerCase()
    .replaceAll(" ", "_")
    .replaceAll("-", "_");

  const sanitized = normalized.replace(/[^a-z0-9_]/g, "");
  return sanitized || "signal";
}

function resolveDashboardActionUrl(action: DashboardDecisionAction): string {
  const value = action.actionUrl?.trim() || action.link?.trim();
  return value || "/analytics";
}

function inferDashboardActionSourceType(action: DashboardDecisionAction): AnalyticsActionSourceType {
  const explicit = (action.sourceType ?? "").trim().toLowerCase();
  if ((DASHBOARD_ACTION_SOURCE_TYPES as string[]).includes(explicit)) {
    return explicit as AnalyticsActionSourceType;
  }

  const url = resolveDashboardActionUrl(action).toLowerCase();
  if (url.includes("/analytics/products")) return "product";
  if (url.includes("/analytics/inventory")) return "inventory";
  if (url.includes("/analytics/supplier")) return "supplier";
  if (url.includes("/analytics/data-quality")) return "data_quality";
  if (url.includes("/analytics/pre-nivelacija-prioriteti")) return "nivelacija";
  return "dashboard";
}

function resolveDashboardActionType(action: DashboardDecisionAction): string {
  if (action.recommendationStatus?.trim()) {
    return sanitizeActionKeyPart(action.recommendationStatus);
  }

  return sanitizeActionKeyPart(action.title);
}

function buildDashboardFallbackActionKey(
  action: DashboardDecisionAction,
  filters: DashboardActionFilters,
): string {
  const sourceType = inferDashboardActionSourceType(action);
  const actionType = resolveDashboardActionType(action);
  const periodFrom = formatActionDateKey(filters.fromDate);
  const periodTo = formatActionDateKey(filters.toDate);
  const storePart = filters.storeId == null ? "all" : String(filters.storeId);
  const supplierPart = filters.supplierId == null ? "all" : String(filters.supplierId);
  return `${sourceType}:${actionType}:${periodFrom}:${periodTo}:${storePart}:${supplierPart}`;
}

function buildAnalyticsActionFromDashboardAction(
  action: DashboardDecisionAction,
  filters: DashboardActionFilters,
): AnalyticsActionUpsertInput {
  const sourceType = inferDashboardActionSourceType(action);
  const actionUrl = resolveDashboardActionUrl(action);
  const sourceKey = action.actionKey?.trim() || buildDashboardFallbackActionKey(action, filters);
  const priority = action.priority === "P1" || action.priority === "P2" || action.priority === "P3"
    ? action.priority
    : "P3";
  const actionType = resolveDashboardActionType(action);
  const periodFrom = formatActionDateKey(filters.fromDate);
  const periodTo = formatActionDateKey(filters.toDate);
  const metadata = {
    ...(action.metadata ?? {}),
    sourceType,
    actionType,
    periodFrom,
    periodTo,
    storeId: filters.storeId ?? "all",
    supplierId: filters.supplierId ?? "all",
  };

  return {
    sourceType,
    sourceKey,
    title: action.title,
    description: action.description?.trim() || action.statusReason?.trim() || action.reason,
    recommendationStatus: action.recommendationStatus ?? undefined,
    priority,
    impactEstimateRsd: action.impactEstimateRsd ?? undefined,
    confidencePct: action.confidencePct ?? undefined,
    reliabilityPct: action.reliabilityPct ?? undefined,
    dataQualityStatus: normalizeDataQualityStatus(action.dataQualityStatus),
    actionUrl,
    metadataJson: JSON.stringify(metadata),
  };
}

function mapLegacyActionLink(title: string): string {
  const normalized = title.trim().toLowerCase();
  if (normalized.includes("replenishment")) return "/analytics/inventory";
  if (normalized.includes("data")) return "/analytics/data-quality";
  if (normalized.includes("portfolio")) return "/analytics/products";
  if (normalized.includes("refresh")) return "/analytics/data-quality";
  return "/analytics";
}

function buildFallbackDecisionActionsFromAdvanced(advanced: DashboardAdvancedSnapshot | null): DashboardDecisionAction[] {
  if (!advanced?.actions?.length) return [];

  return advanced.actions.slice(0, 4).map((item) => {
    const confidencePct = normalizeRecommendationPct(item.confidencePct);
    const reliabilityPct = normalizeRecommendationPct(item.reliabilityPct);
    const dataQualityStatus = normalizeRecommendationQualityStatus(item.dataQualityStatus);

    return {
      sourceType: inferDashboardActionSourceType({ priority: item.priority || "P3", title: item.title || "", reason: item.recommendation || "", link: mapLegacyActionLink(item.title || "") }),
      priority: item.priority || "P3",
      title: item.title || "Operativna akcija",
      description: item.recommendation || "Potrebna je dodatna provera signala.",
      reason: item.recommendation || "Potrebna je dodatna provera signala.",
      statusReason: item.statusReason || item.recommendation || "Pouzdanost preporuke nije dostupna.",
      recommendationStatus: null,
      expectedImpact: null,
      impactEstimateRsd: null,
      confidencePct,
      reliabilityPct,
      recommendationAllowed: false,
      dataQualityStatus,
      actionUrl: mapLegacyActionLink(item.title || ""),
      metadata: { legacyAction: true },
      link: mapLegacyActionLink(item.title || ""),
      linkLabel: dataQualityStatus !== "good" ? "Otvori kvalitet podataka" : "Otvori povezani ekran",
    };
  });
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
  if (reason instanceof Error) {
    const message = reason.message.trim();
    const signature = `${reason.name} ${message}`.toLowerCase();

    if (
      signature.includes("apifailovertimeouterror")
      || signature.includes("api request timed out")
      || signature.includes("request timeout")
      || signature.includes("timed out")
    ) {
      return "Analytics endpoint trenutno odgovara sporo (timeout). Pokusajte osvezavanje za 30-60 sekundi.";
    }

    if (message) return message;
  }

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

function formatDecisionPctOrUnavailable(value: number | null | undefined): string {
  const normalized = normalizeRecommendationPct(value);
  if (normalized == null) return "nije dostupno";
  return fmtPct(normalized, 0);
}

function freshnessStatusLabel(value?: string | null): string {
  const normalized = (value ?? "").trim().toLowerCase();
  if (normalized === "fresh") return "Sve\u017Ee";
  if (normalized === "stale") return "Zastarelo";
  if (normalized === "critical") return "Kriti\u010Dno";
  return "Nepoznato";
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
  const [decisionActions, setDecisionActions] = useState<DashboardDecisionAction[]>([]);
  const [executive, setExecutive] = useState<ExecutiveDashboardSnapshot | null>(null);
  const [dashboardMeta, setDashboardMeta] = useState<AnalyticsResponseMeta | null>(null);
  const [refreshStatus, setRefreshStatus] = useState<AnalyticsRefreshStatus | null>(null);
  const [refreshStatusError, setRefreshStatusError] = useState<string | null>(null);
  const [addedToQueueKeys, setAddedToQueueKeys] = useState<Set<string>>(new Set());
  const [queueBusyKeys, setQueueBusyKeys] = useState<Set<string>>(new Set());
  const [queueErrorsByKey, setQueueErrorsByKey] = useState<Record<string, string>>({});
  const [healthText, setHealthText] = useState("");
  const [topTab, setTopTab] = useState<TopTabKey>("revenue");
  const [errors, setErrors] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const [showDetailedAnalysis, setShowDetailedAnalysis] = useState(false);
  const [showOverviewHelp, setShowOverviewHelp] = useState(false);

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

    const [bootstrapR, refreshStatusR] = await Promise.allSettled([
      getDashboardBootstrap(fromDate, toDate, true, storeId, supplierId),
      getAnalyticsRefreshStatus(),
    ]);

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
      setDecisionActions(
        (bootstrapR.value.decisionActions ?? []).length > 0
          ? [...(bootstrapR.value.decisionActions ?? [])]
          : buildFallbackDecisionActionsFromAdvanced(bootstrapR.value.advanced)
      );
      setExecutive(bootstrapR.value.executive ?? null);
      setDashboardMeta(bootstrapR.value.meta ?? null);
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
      setDecisionActions([]);
      setExecutive(null);
      setDashboardMeta(null);
      nextErrors.push(getErrorText(bootstrapR.reason, "Analytics dashboard bootstrap nije ucitan."));
    }

    if (refreshStatusR.status === "fulfilled") {
      setRefreshStatus(refreshStatusR.value);
      setRefreshStatusError(null);
    } else {
      setRefreshStatus(null);
      setRefreshStatusError(getErrorText(refreshStatusR.reason, "Status osvezavanja analitike nije dostupan."));
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

  const movingStats = useMemo(() => {
    if (dailySales.length === 0) return {
      ma7Revenue: 0,
      ma30Revenue: 0,
      momentumPct: null as number | null,
      elasticity: null as number | null,
    };
    const sorted = [...dailySales].sort((a, b) => a.date.localeCompare(b.date));
    const last7 = sorted.slice(-7);
    const last30 = sorted.slice(-30);
    const prev7 = sorted.slice(-14, -7);
    const sumRevenue = (items: DailySale[]) => items.reduce((acc, item) => acc + item.totalRevenue, 0);
    const sumUnits = (items: DailySale[]) => items.reduce((acc, item) => acc + item.totalUnits, 0);
    const lastRevenue = sumRevenue(last7);
    const last30Revenue = sumRevenue(last30);
    const prevRevenue = sumRevenue(prev7);
    const lastUnits = sumUnits(last7);
    const prevUnits = sumUnits(prev7);
    const ma7Revenue = last7.length > 0 ? lastRevenue / last7.length : 0;
    const ma30Revenue = last30.length > 0 ? last30Revenue / last30.length : 0;
    const momentumPct = prevRevenue > 0 ? Number((((lastRevenue - prevRevenue) / prevRevenue) * 100).toFixed(2)) : null;
    const lastPrice = lastUnits > 0 ? lastRevenue / lastUnits : 0;
    const prevPrice = prevUnits > 0 ? prevRevenue / prevUnits : 0;
    const qtyChange = prevUnits > 0 ? (lastUnits - prevUnits) / prevUnits : 0;
    const priceChange = prevPrice > 0 ? (lastPrice - prevPrice) / prevPrice : 0;
    const elasticity = prevUnits > 0 && prevPrice > 0 && priceChange !== 0 ? Number((qtyChange / priceChange).toFixed(2)) : null;
    return { ma7Revenue, ma30Revenue, momentumPct, elasticity };
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
  const dashboardMetaMessage = getAnalyticsMetaMessage(dashboardMeta);
  const dashboardRowCount = summary?.totalTransactions ?? dailySales.length ?? null;
  const hasFatalLoadError = !loading && ((errors.length > 0 && summary == null) || isAnalyticsMetaError(dashboardMeta));
  const showMetaWarning = !loading && !hasFatalLoadError && isAnalyticsMetaWarning(dashboardMeta);
  const emptyVariant = useMemo(() => {
    if (loading || hasFatalLoadError) return null;
    if (shouldShowAnalyticsEmptyState(dashboardMeta, dashboardRowCount)) {
      return isAnalyticsMetaInsufficient(dashboardMeta) ? "insufficient_data" as const : "no_data" as const;
    }
    if (summary && summary.totalTransactions === 0 && summary.totalUnits === 0) return "no_data" as const;
    return null;
  }, [dashboardMeta, dashboardRowCount, hasFatalLoadError, loading, summary]);
  const showEmptyState = emptyVariant !== null;
  const executiveDataQualityTone = normalizeDataQualityStatus(
    dashboardMeta?.dataQualityStatus ?? executive?.dataQualitySummary?.freshnessStatus ?? null
  );

  const topGainers = useMemo(
    () =>
      (topAdvanced?.byRevenue ?? [])
        .filter((row) => (row.trendPct ?? 0) > 0)
        .sort((a, b) => (b.trendPct ?? 0) - (a.trendPct ?? 0))
        .slice(0, 5),
    [topAdvanced]
  );

  const topLosers = useMemo(
    () =>
      (topAdvanced?.byRevenue ?? [])
        .filter((row) => (row.trendPct ?? 0) < 0)
        .sort((a, b) => (a.trendPct ?? 0) - (b.trendPct ?? 0))
        .slice(0, 5),
    [topAdvanced]
  );

  const validationRows = useMemo(
    () =>
      [
        validCompleteness ? { name: "Kompletnost", ...validCompleteness } : null,
        validFreshness ? { name: "Svezina", ...validFreshness } : null,
        validLostSales ? { name: "Izgubljena prodaja", ...validLostSales } : null,
      ].filter((item): item is { name: string } & DashboardValidationEndpoint => item !== null),
    [validCompleteness, validFreshness, validLostSales]
  );

  const prioritizedDecisionActions = useMemo(
    () =>
      [...decisionActions]
        .filter((item) => item && item.title && item.reason)
        .sort((a, b) => {
          const byPriority = decisionPriorityRank(a.priority) - decisionPriorityRank(b.priority);
          if (byPriority !== 0) return byPriority;
          return (b.confidencePct ?? -1) - (a.confidencePct ?? -1);
        })
        .slice(0, 5),
    [decisionActions]
  );
  const recommendationsBlocked = useMemo(
    () => prioritizedDecisionActions.length > 0 && prioritizedDecisionActions.every((action) => action.recommendationAllowed === false),
    [prioritizedDecisionActions]
  );
  const executiveDataQualityHighlights = useMemo(() => {
    const summary = executive?.dataQualitySummary;
    if (!summary) return [] as Array<{ key: string; label: string; value: number }>;

    return [
      { key: "missingSupplierCount", label: "Bez dobavlja\u010Da", value: summary.missingSupplierCount ?? 0 },
      { key: "missingCostCount", label: "Bez nabavne cene", value: summary.missingCostCount ?? 0 },
      { key: "insufficientSignalCount", label: "Nedovoljni signali", value: summary.insufficientSignalCount ?? 0 },
      { key: "ignoredRowsCount", label: "Ignorisani redovi", value: summary.ignoredRowsCount ?? 0 },
      { key: "zeroRevenueRowsCount", label: "Nulti prihod", value: summary.zeroRevenueRowsCount ?? 0 },
    ].filter((item) => item.value > 0);
  }, [executive?.dataQualitySummary]);

  const currentActionFilters = useMemo<DashboardActionFilters>(() => ({
    fromDate,
    toDate,
    storeId,
    supplierId,
  }), [fromDate, storeId, supplierId, toDate]);

  const getDecisionActionCardKey = useCallback((action: DashboardDecisionAction, index: number) => {
    const explicit = action.actionKey?.trim();
    if (explicit) return explicit;
    return `${buildDashboardFallbackActionKey(action, currentActionFilters)}:${index}`;
  }, [currentActionFilters]);

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
      { key: "supplierId", label: "Dobavlja\u010D", value: supplierId ?? "" },
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
      <AnalyticsTrustHeader
        title="Pregled poslovanja"
        description="Executive cockpit za prodaju i profit: prihod, mar\u017Eni doprinos, rizici i prioritetne odluke za izabrani period."
        periodFrom={fromDate}
        periodTo={toDate}
        lastRefreshAt={refreshStatus?.lastSuccessfulRefreshAtUtc ?? advanced?.generatedAtUtc ?? validFreshness?.lastImport ?? null}
        dataFreshnessStatus={refreshStatus?.dataFreshnessStatus ?? null}
        refreshIsRunning={refreshStatus?.isRunning ?? false}
        refreshCurrentStep={refreshStatus?.currentStep ?? null}
        dataSource="Analytics dashboard cache"
        dataQualityStatus={
          dashboardMeta?.dataQualityStatus
            ?? (validFreshness?.status === "good" || validFreshness?.status === "warning" || validFreshness?.status === "critical"
              ? validFreshness.status
            : null
            )
        }
        dataQualitySummary={{
          missingSupplierCount: executive?.dataQualitySummary?.missingSupplierCount ?? null,
          missingCostCount: executive?.dataQualitySummary?.missingCostCount ?? null,
          missingCategoryCount: null,
          insufficientSignalCount: executive?.dataQualitySummary?.insufficientSignalCount ?? null,
          ignoredRowsCount: executive?.dataQualitySummary?.ignoredRowsCount ?? null,
        }}
        mode="recommendation"
        recommendationNote="Dashboard prikazuje prioritetne preporuke sistema i operativne signale. Potvrdi odluku na ciljnom ekranu."
        methodologyHref="/analytics/data-quality"
        dataQualityHref="/analytics/data-quality"
        refreshStatusHref="/admin/configuration?panel=workers"
        compact
      />
      <AnalyticsRefreshStatusBanner
        status={refreshStatus}
        loading={loading}
        error={refreshStatusError}
      />
      <header className="analytics-header">
        <div>
          <h2 className="with-tip">
            <span>Executive Dashboard</span>
            <InfoTip text="Fokus na klju\u010Dne KPI-jeve, najva\u017Enije akcije i pouzdanost podataka. Grafikoni i detalji su ni\u017Ee na stranici." />
          </h2>
        </div>
        <div className="analytics-controls">
          <button onClick={() => void load()} disabled={loading}>{loading ? "U\u010Ditavanje..." : "Osve\u017Ei"}</button>
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
            Dobavlja\u010D
            <select value={selectedSupplier} onChange={(e) => setSelectedSupplier(e.target.value)}>
              <option value="">Svi dobavlja\u010Di</option>
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
          <span className="filter-chip">Dobavlja\u010D: {selectedSupplier ? supplierOptions.find((item) => item.supplierId === supplierId)?.supplierName ?? `ID ${supplierId}` : "Svi"}</span>
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
      {showMetaWarning ? (
        <div className="analytics-empty warning" role="status">
          {dashboardMetaMessage ?? "Prikazani podaci su delimicni ili fallback. Proverite status osvezavanja analitike."}
        </div>
      ) : null}
      {hasFatalLoadError ? (
        <AnalyticsErrorState
          title="Podaci trenutno nisu dostupni"
          message={dashboardMetaMessage || errors[0] || "Dashboard trenutno nije dostupan."}
          errorCode={dashboardMeta?.errorCode ?? null}
          correlationId={dashboardMeta?.correlationId ?? null}
          suggestions={[
            "Pokrenite osve\u017Eavanje analytics podataka i probajte ponovo.",
            "Ako problem traje, proverite data quality i backend logove.",
          ]}
          onRetry={() => {
            void load();
          }}
          helpHref="/analytics/data-quality"
        />
      ) : null}

      {!hasFatalLoadError ? (
      <>
      {showEmptyState ? (
        <AnalyticsEmptyState
          variant={emptyVariant ?? undefined}
          emptyReason={dashboardMeta?.emptyReason ?? dashboardMetaMessage ?? null}
          actions={[
            { label: "Pro\u0161iri period na 90 dana", onClick: () => applyPreset("90d") },
            { label: "Otvori Data Quality", href: "/analytics/data-quality" },
          ]}
          dataQualityHref="/analytics/data-quality"
        />
      ) : (
        <>
          <section className="analytics-section">
            <h2 className="with-tip"><span>Klju\u010Dni KPI-jevi</span><InfoTip text="4-5 metrika za brzu procenu: prihod, mar\u017Eni doprinos, jedinice, rizi\u010Dna zaliha i kvalitet podataka." /></h2>
            <p className="section-note">Transakcije u periodu: <strong>{summary == null ? "N/A" : fmtNumber(summary.totalTransactions)}</strong></p>
            <ExecutiveKpiRow
              loading={loading}
              totalRevenue={summary?.totalRevenue ?? null}
              marginContributionRsd={executive?.totalMarginContributionRsd ?? null}
              totalUnits={summary?.totalUnits ?? null}
              inventoryDangerValueRsd={executive?.inventoryDangerValueRsd ?? null}
              dataQualityTone={executiveDataQualityTone}
              dataQualityStatus={dashboardMeta?.dataQualityStatus ?? executive?.dataQualitySummary?.freshnessStatus ?? null}
              missingSupplierCount={executive?.dataQualitySummary?.missingSupplierCount ?? null}
              missingCostCount={executive?.dataQualitySummary?.missingCostCount ?? null}
            />
          </section>

          <section className="analytics-panel analytics-overview-help no-print">
            <button
              type="button"
              className={`overview-help-toggle${showOverviewHelp ? " open" : ""}`}
              onClick={() => setShowOverviewHelp((prev) => !prev)}
              aria-expanded={showOverviewHelp}
            >
              Kako \u010Ditati ovaj pregled?
            </button>
            {showOverviewHelp ? (
              <div className="overview-help-body">
                <p>Pregled kombinuje prihod, mar\u017Eni doprinos, zalihe, dobavlja\u010De, kvalitet podataka i preporuke sistema.</p>
                <p>Koristite ga za nedeljni pregled odluka: \u0161ta poja\u010Dati, \u0161ta proveriti i gde smanjiti rizik.</p>
              </div>
            ) : null}
          </section>

          <section className="analytics-exec-fold">
            <section className="analytics-panel analytics-decision-cockpit">
              <div className="decision-cockpit-head">
                <div>
                  <h2>\u0160ta treba uraditi ove nedelje?</h2>
                  <p>Top akcije generisane iz prodaje, zaliha, mar\u017Ee i kvaliteta podataka.</p>
                </div>
                <Link to="/analytics/actions" className="decision-all-actions-link">Akcije i preporuke</Link>
              </div>
              {recommendationsBlocked ? (
                <div className="analytics-empty warning">Nedovoljno podataka za preporuke. Prvo zatvorite Data Quality probleme.</div>
              ) : null}
              {loading ? (
                <div className="analytics-skeleton-grid">
                  {Array.from({ length: 3 }).map((_, i) => <div key={`decision-skeleton-${i}`} className="analytics-skeleton-card" />)}
                </div>
              ) : prioritizedDecisionActions.length === 0 ? (
                <div className="analytics-empty warning">Nema dovoljno pouzdanih podataka za automatske odluke.</div>
              ) : (
                <div className="decision-action-list">
                  {prioritizedDecisionActions.map((action, index) => {
                    const cardKey = getDecisionActionCardKey(action, index);
                    const isQueued = addedToQueueKeys.has(cardKey);
                    const isQueueBusy = queueBusyKeys.has(cardKey);
                    const queueError = queueErrorsByKey[cardKey];
                    const actionLink = resolveDashboardActionUrl(action);

                    return (
                      <article
                        key={cardKey}
                        className={`decision-action-card priority-${(action.priority || "P3").toLowerCase()}`}
                      >
                        <div className="decision-action-top">
                          <span className={`priority ${(action.priority || "P3").toLowerCase()}`}>{action.priority || "P3"}</span>
                          <strong>{action.title}</strong>
                          {action.recommendationAllowed === false ? (
                            <span className="decision-limited-badge">Signal ograni\u010Den</span>
                          ) : null}
                        </div>
                        <p className="decision-action-reason">{action.reason}</p>
                        {action.expectedImpact
                          ? <p className="decision-action-impact">Ocekivani uticaj: {action.expectedImpact}</p>
                          : action.impactEstimateRsd != null
                            ? <p className="decision-action-impact">Procenjeni uticaj: {fmtRsd(action.impactEstimateRsd, 0, "N/A")}</p>
                            : null}
                        <div className="decision-action-foot">
                          <div className="decision-quality-stack">
                            <span className="decision-confidence">
                              Sigurnost preporuke: {formatDecisionPctOrUnavailable(action.confidencePct)}
                            </span>
                            <span className="decision-confidence">
                              Pouzdanost signala: {formatDecisionPctOrUnavailable(action.reliabilityPct)}
                            </span>
                            <span className={`decision-quality quality-${normalizeDataQualityStatus(action.dataQualityStatus)}`}>
                              Data quality: {dataQualityStatusLabel(action.dataQualityStatus)}
                            </span>
                            {action.recommendationAllowed === false ? (
                              <small className="decision-status-reason">Signal je informativan; finalna preporuka je blokirana zbog kvaliteta ili pokrivenosti podataka.</small>
                            ) : null}
                            {normalizeDataQualityStatus(action.dataQualityStatus) !== "good" ? (
                              <Link to="/analytics/data-quality" className="decision-quality-link">
                                Otvori kvalitet podataka
                              </Link>
                            ) : null}
                            {action.statusReason ? <small className="decision-status-reason">{action.statusReason}</small> : null}
                          </div>
                          <div className="decision-action-links">
                            <Link to={actionLink} className="decision-link">{action.linkLabel || "Otvori ekran"}</Link>
                            <button
                              type="button"
                              className={`btn-add-to-queue${isQueued ? " added" : ""}`}
                              title="Dodaj u centralni red akcija"
                              disabled={isQueueBusy || isQueued}
                              onClick={async (e) => {
                                e.stopPropagation();
                                if (isQueueBusy || isQueued) return;
                                setQueueBusyKeys((prev) => {
                                  const next = new Set(prev);
                                  next.add(cardKey);
                                  return next;
                                });
                                setQueueErrorsByKey((prev) => {
                                  if (!(cardKey in prev)) return prev;
                                  const { [cardKey]: _discard, ...rest } = prev;
                                  return rest;
                                });
                                try {
                                  const actionInput = buildAnalyticsActionFromDashboardAction(action, currentActionFilters);
                                  await upsertAnalyticsAction(actionInput);
                                  setAddedToQueueKeys((prev) => {
                                    const next = new Set(prev);
                                    next.add(cardKey);
                                    return next;
                                  });
                                } catch (reason) {
                                  setQueueErrorsByKey((prev) => ({
                                    ...prev,
                                    [cardKey]: getErrorText(reason, "Akcija nije dodata u centralni red."),
                                  }));
                                } finally {
                                  setQueueBusyKeys((prev) => {
                                    const next = new Set(prev);
                                    next.delete(cardKey);
                                    return next;
                                  });
                                }
                              }}
                            >
                              {isQueueBusy ? "Dodajem..." : isQueued ? "U akcijama" : "Dodaj u akcije"}
                            </button>
                            {queueError ? <small className="decision-action-error">{queueError}</small> : null}
                          </div>
                        </div>
                      </article>
                    );
                  })}
                </div>
              )}
            </section>

            <div className="analytics-exec-side">
              <section className="analytics-panel analytics-exec-earn">
                <h2>Gde zarađujemo?</h2>
                <p className="section-note">Top dobavljači i artikli po maržnom doprinosu (top 5).</p>
                {(executive?.topMarginCategories?.length ?? 0) > 0 ? (
                  <div className="exec-top-categories">
                    {(executive?.topMarginCategories ?? []).slice(0, 3).map((item) => (
                      <Link key={item.key} to={item.link} className="exec-category-pill">
                        <span>{item.label}</span>
                        <strong>{fmtRsd(item.marginContribution)}</strong>
                      </Link>
                    ))}
                  </div>
                ) : null}
                {!executive || (executive.topSuppliers.length === 0 && executive.topMarginProducts.length === 0) ? (
                  <div className="analytics-empty">Nema dovoljno signala za prikaz top lista.</div>
                ) : (
                  <div className="exec-two-tables">
                    <div className="exec-table-wrap">
                      <h3>Top dobavljači</h3>
                      <table className="exec-table">
                        <thead>
                          <tr>
                            <th>Dobavljač</th>
                            <th className="num">Prihod</th>
                            <th className="num">Maržni doprinos</th>
                          </tr>
                        </thead>
                        <tbody>
                          {(executive?.topSuppliers ?? []).slice(0, 5).map((item) => (
                            <tr key={`${item.supplierId ?? "unknown"}:${item.supplierName}`}>
                              <td><Link to={item.link}>{item.supplierName}</Link></td>
                              <td className="num">{fmtRsd(item.revenue)}</td>
                              <td className="num">{fmtRsd(item.marginContribution)}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                    <div className="exec-table-wrap">
                      <h3>Top artikli</h3>
                      <table className="exec-table">
                        <thead>
                          <tr>
                            <th>Artikal</th>
                            <th className="num">Maržni doprinos</th>
                            <th className="num">Confidence</th>
                          </tr>
                        </thead>
                        <tbody>
                          {(executive?.topMarginProducts ?? []).slice(0, 5).map((item) => (
                            <tr key={item.key}>
                              <td>
                                <Link to={item.link} title={item.supplierName ? `Dobavljač: ${item.supplierName}` : undefined}>
                                  {item.label}
                                </Link>
                              </td>
                              <td className="num">{fmtRsd(item.marginContribution)}</td>
                              <td className="num">
                                <span className={`exec-pill dq-${normalizeDataQualityStatus(item.dataQualityStatus)}`}>{formatDecisionConfidence(item.confidencePct ?? null)}</span>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>
                )}
              </section>

              <section className="analytics-panel analytics-exec-lose">
                <h2>Gde gubimo novac?</h2>
                <p className="section-note">Najkritičniji negativni signali (top 5).</p>
                {!executive || (executive.negativeSignals ?? []).length === 0 ? (
                  <div className="analytics-empty">Nema dovoljno pouzdanih negativnih signala za prikaz.</div>
                ) : (
                  <div className="exec-signal-list">
                    {(executive.negativeSignals ?? []).slice(0, 5).map((signal, idx) => (
                      <article key={`${signal.signalType}-${idx}`} className={`exec-signal priority-${(signal.priority || "P3").toLowerCase()}`}>
                        <div className="exec-signal-top">
                          <span className={`priority ${(signal.priority || "P3").toLowerCase()}`}>{signal.priority || "P3"}</span>
                          <strong>{signal.title}</strong>
                        </div>
                        <p>{signal.description}</p>
                        <div className="exec-signal-foot">
                          <span className={`exec-pill dq-${normalizeDataQualityStatus(signal.dataQualityStatus)}`}>
                            Data quality: {dataQualityStatusLabel(signal.dataQualityStatus)}
                          </span>
                          <span className="exec-pill">{formatDecisionConfidence(signal.confidencePct ?? null)}</span>
                          {signal.impactEstimateRsd != null ? <span className="exec-pill">Uticaj: {fmtRsd(signal.impactEstimateRsd, 0, "N/A")}</span> : null}
                          <Link to={signal.link} className="exec-signal-link">Otvori</Link>
                        </div>
                      </article>
                    ))}
                  </div>
                )}
              </section>

              <section className="analytics-panel analytics-exec-data-quality">
                <h2>Kvalitet podataka</h2>
                <p className="section-note">Sa\u017Eetak problema koji blokiraju pouzdane preporuke.</p>
                <div className="exec-dq-grid">
                  {executiveDataQualityHighlights.length === 0 ? (
                    <div className="exec-dq-ok">Nema kriti\u010Dnih count signala u ovom preseku. Proverite ipak trend osve\u017Eavanja.</div>
                  ) : (
                    executiveDataQualityHighlights.map((item) => (
                      <div key={item.key}>
                        <span>{item.label}</span>
                        <strong>{item.value.toLocaleString("sr-RS")}</strong>
                      </div>
                    ))
                  )}
                </div>
                <p className="section-note">
                  Sve\u017Eina podataka: <strong>{freshnessStatusLabel(refreshStatus?.dataFreshnessStatus)}</strong>
                </p>
                <Link to="/analytics/data-quality" className="exec-dq-cta">Otvori Data Quality</Link>
              </section>
            </div>
          </section>
        </>
      )}

      <section className="analytics-section">
        <h2 className="with-tip"><span>Detaljna analiza</span><InfoTip text="Detaljniji pogled po trendu, raspodeli prodaje, zalihama i top proizvodima." /></h2>
        {!showDetailedAnalysis ? (
          <section className="analytics-panel analytics-details-collapsed">
            <div className="details-collapsed-row">
              <div>
                <h3>Detalji i grafici</h3>
                <p className="section-note">Napredne metrike, trendovi, top proizvodi i grafici su ispod ovog dugmeta.</p>
              </div>
              <button type="button" className="details-expand" onClick={() => setShowDetailedAnalysis(true)} disabled={loading}>
                Prikazi detaljnu analizu
              </button>
            </div>
          </section>
        ) : null}
        {showDetailedAnalysis && (
          <>
        {!loading && summary && (
          <section className="analytics-panel">
            <h3 className="with-tip"><span>Dodatne metrike</span><InfoTip text="Ostali KPI-jevi i signali koji ne moraju biti iznad folda, ali su korisni za dublji uvid." /></h3>
            <div className="analytics-card-grid compact">
              <MetricCard label="Ukupan promet" value={fmtRsd(summary.totalRevenue)} tone="good" infoTip={HELP.promet} />
              <MetricCard label="Transakcije" value={fmtNumber(summary.totalTransactions)} infoTip={HELP.transakcije} />
              <MetricCard label="Prodate jedinice" value={fmtNumber(summary.totalUnits)} infoTip={HELP.jedinice} />
              <MetricCard label="Promet po danu" value={fmtRsd(derived.revenuePerDay)} />
              <MetricCard label="Transakcije po danu" value={fmtNumber(derived.transactionsPerDay, 1)} />
              <MetricCard label="Dostupnost SKU" value={fmtPct(derived.availablePct)} tone="good" infoTip={HELP.sku} />
              <MetricCard label="Crvena zona zaliha" value={fmtPct(derived.redZonePct)} tone="warning" infoTip={HELP.oos} />
              <MetricCard label="MA7 + Momentum" value={fmtRsd(movingStats.ma7Revenue)} tone="good" infoTip={HELP.ma7} />
              <MetricCard label="Elasticnost (aproks.)" value={movingStats.elasticity == null ? "N/A" : fmtNumber(movingStats.elasticity, 2)} tone="neutral" infoTip={HELP.elasticnost} />
              <MetricCard label="Prosecna korpa" value={fmtRsd(summary.avgBasketValue)} tone="neutral" infoTip="Prosecna vrednost jednog racuna." />
            </div>
          </section>
        )}

        {!loading && (quickInsights || transactionStats) && (
          <section className="analytics-panel">
            <h3 className="with-tip"><span>Brzi uvidi</span><InfoTip text="Kratki signali za kontekst (nisu finalna preporuka)." /></h3>
            <div className="analytics-card-grid compact">
              <MetricCard label="Najjaci dan" value={quickInsights?.bestDay ?? "N/A"} tone="good" infoTip="Dan u nedelji sa najvecim prometom." />
              <MetricCard label="Promet najboljeg dana" value={fmtRsd(quickInsights?.bestDayRevenue ?? 0)} tone="good" />
              <MetricCard label="Top proizvod" value={quickInsights?.topProduct ?? "N/A"} tone="neutral" />
              <MetricCard label="Stavki po transakciji" value={transactionStats ? fmtNumber(transactionStats.avgItemsPerTransaction, 2) : "N/A"} tone="neutral" />
              <MetricCard label="Vrednost transakcije" value={transactionStats ? fmtRsd(transactionStats.avgTransactionValue) : "N/A"} tone="neutral" />
            </div>
          </section>
        )}

        {!loading && advanced && (
          <section className="analytics-panel">
            <h3 className="with-tip"><span>Napredne kartice</span><InfoTip text="Napredne BI metrike (velocity, pareto, data health...). Koristi za analizu, ne kao jedini izvor odluke." /></h3>
            <div className="analytics-card-grid compact">
              {advanced.cards.map((card: DashboardMetricCard) => (
                <article key={card.key} className={`metric-card ${statusTone(card.status)}`}>
                  <span className="metric-label"><span>{card.key === "velocity" ? "Brzina prodaje (velocity)" : card.key === "oos" ? "Rasprodato (OOS)" : card.key === "pareto" ? "Pareto koncentracija" : card.key === "data_health" ? "Svezina podataka" : card.key === "completeness" ? "Kompletnost podataka" : card.label}</span><InfoTip text={HELP[card.key] ?? "Napredna BI metrika."} /></span>
                  <strong>{fmtNumber(card.value, card.unit === "%" ? 1 : 2)} {card.unit === "units/day" ? "kom/dan" : card.unit === "hours old" ? "sati od osvezavanja" : card.unit}</strong>
                  <small>{card.trendPct != null ? `${trendLabel(card.trendPct)} ${fmtPct(card.trendPct)}` : statusLabel(card.status)}</small>
                  {card.subtitle && <small>{card.subtitle.replace("Top SKU:", "Top sifra:").replace("Lost sales estimate:", "Procena izgubljene prodaje:").replace("Top 50 share:", "Udeo top 50:").replace("Last import:", "Poslednji import:").replace("Missing:", "Nedostajuca polja:")}</small>}
                </article>
              ))}
            </div>
          </section>
        )}

        {!loading && advanced && (
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
        )}

        {!loading && validationRows.length > 0 && (
          <section className="analytics-panel">
            <h3 className="with-tip"><span>Kvalitet podataka</span><InfoTip text="Pouzdanost podataka koji hrane preporuke: kompletnost, svezina i procena izgubljene prodaje." /></h3>
            <p className="section-note">Ako je signal kritican, prvo resite kvalitet podataka pa tek onda donesite poslovnu odluku.</p>
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
        {!loading && (
          <section className="analytics-panel">
            <h3 className="with-tip"><span>Trend i promene</span><InfoTip text="Brz pregled dinamike prodaje kroz MA7, MA30 i momentum poslednjih 7 dana." /></h3>
            <div className="trend-signal-grid">
              <article className="trend-signal-card">
                <span>MA7 promet</span>
                <strong>{fmtRsd(movingStats.ma7Revenue)}</strong>
              </article>
              <article className="trend-signal-card">
                <span>MA30 promet</span>
                <strong>{fmtRsd(movingStats.ma30Revenue)}</strong>
              </article>
              <article className="trend-signal-card">
                <span>Momentum 7d</span>
                <strong className={movingStats.momentumPct != null && movingStats.momentumPct < 0 ? "trend down" : "trend up"}>
                  {fmtPct(movingStats.momentumPct)}
                </strong>
              </article>
              <article className="trend-signal-card">
                <span>Elasticnost</span>
                <strong>{movingStats.elasticity == null ? "N/A" : fmtNumber(movingStats.elasticity, 2)}</strong>
              </article>
            </div>
          </section>
        )}
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
            formatCurrency={(value: number) => fmtRsd(value)}
            formatNumber={(value: number, digits = 0) => fmtNumber(value, digits)}
          />
        </Suspense>
        {!loading && inventory && (
          <section className="analytics-panel">
            <h3 className="with-tip"><span>Brzi pregled zaliha</span><InfoTip text="Ukupno stanje i signal rizika od rasprodatosti." /></h3>
            <div className="stock-grid">
              <article className="stock-card"><span className="metric-label"><span>Ukupno SKU</span><InfoTip text={HELP.sku} /></span><strong>{fmtNumber(inventory.totalSkuCount)}</strong></article>
              <article className="stock-card"><span>Ukupno na stanju</span><strong>{fmtNumber(inventory.totalOnHand)}</strong></article>
              <article className="stock-card warning"><span>Niska zaliha</span><strong>{fmtNumber(inventory.lowStockCount)}</strong></article>
              <article className="stock-card critical"><span>Bez zaliha</span><strong>{fmtNumber(inventory.outOfStockCount)}</strong></article>
            </div>
          </section>
        )}

        {!loading && topAdvanced && (
          <section className="analytics-panel">
            <h3 className="with-tip"><span>Top rast / Top pad</span><InfoTip text="Najvece promene po trendu iz top prometa. Klik na red otvara detalj artikla." /></h3>
            <div className="trend-split-grid">
              <article className="trend-list">
                <h4>Top rast</h4>
                {topGainers.length === 0 && <div className="analytics-empty">Nema pozitivnih trendova.</div>}
                {topGainers.map((row) => (
                  <button key={`gain-${row.productId}`} type="button" className="trend-list-row up" onClick={() => openTopProductDetail(row)}>
                    <span>{row.productName}</span>
                    <strong>+{fmtPct(row.trendPct)}</strong>
                  </button>
                ))}
              </article>
              <article className="trend-list">
                <h4>Top pad</h4>
                {topLosers.length === 0 && <div className="analytics-empty">Nema negativnih trendova.</div>}
                {topLosers.map((row) => (
                  <button key={`loss-${row.productId}`} type="button" className="trend-list-row down" onClick={() => openTopProductDetail(row)}>
                    <span>{row.productName}</span>
                    <strong>{fmtPct(row.trendPct)}</strong>
                  </button>
                ))}
              </article>
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
            {topTab === "margin" && !topAdvanced.marginAvailable && <div className="analytics-empty warning">Nema dovoljno podataka za prikaz uticaja na marzu. <Link to="/analytics/data-quality">Data Quality</Link></div>}
            <p className="section-note">
              {topAdvanced.marginMessage ?? "Kvalitet marze po artiklu nije dostupan na ovom dashboard pogledu; backend jos ne vraca cost coverage / margin quality tier po redu."}{" "}
              <Link to="/analytics/data-quality">Data Quality</Link>
            </p>
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
                        title={`Trend: ${fmtPct(row.trendPct)} | Promet: ${fmtRsd(row.revenue)} | Komada: ${fmtNumber(row.units)}`}
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
                        <td>{fmtRsd(row.revenue)}</td>
                        <td>{fmtNumber(row.units)}</td>
                        <td>{fmtNumber(row.velocityUnitsPerDay, 2)}</td>
                        <td>
                          <div>{row.marginImpact == null ? "N/A" : fmtRsd(row.marginImpact)}</div>
                          <small>{row.marginQualityLabel ?? "Kvalitet marze nije dostupan"}</small>
                        </td>
                        <td className={row.trendPct != null && row.trendPct < 0 ? "trend down" : "trend up"}>
                          <div>{trendLabel(row.trendPct)} {fmtPct(row.trendPct)}</div>
                          {row.trendPct == null ? <small>Nema prethodnog perioda za PoP poredjenje.</small> : null}
                        </td>
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
      </>
      ) : null}
    </div>
  );
}

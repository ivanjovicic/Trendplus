import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { parseEntityIdParam } from "../validation/entityId";
import { Link, useLocation, useNavigate, useSearchParams } from "react-router-dom";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { getStores } from "../services/analyticsApi";
import {
  getShoeTypeSalesStats,
  type ShoeTypeSalesStat,
  type ShoeTypeSalesStatsResponse,
} from "../services/shoeTypeSalesStatsApi";
import type { StoreOption } from "../types/analytics";
import AnalyticsUnknownLink from "../components/analytics/AnalyticsUnknownLink";
import AnalyticsControlBar, {
  type AnalyticsControlBarChip,
  type AnalyticsControlBarField,
} from "../components/analytics/AnalyticsControlBar";
import AnalyticsDataTable from "../components/analytics/AnalyticsDataTable";
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import AnalyticsTrustHeader from "../components/analytics/AnalyticsTrustHeader";
import AnalyticsFilterLoadNotice from "../components/analytics/AnalyticsFilterLoadNotice";
import AnalyticsErrorState from "../components/analytics/AnalyticsErrorState";
import AnalyticsEmptyState from "../components/analytics/AnalyticsEmptyState";
import InfoTip from "../components/ui/InfoTip";
import UltraSpinner from "../components/ui/UltraSpinner";
import { buildAnalyticsDetailSnapshot, saveAnalyticsDetailSnapshot } from "../services/analyticsTableState";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import { dataScopeLabel, getDataScope, type DataScope } from "../utils/dataScope";
import {
  resolveStoreFilterFallbackState,
  resolveStoreFilterLoadFailure,
  STORE_FILTER_SCOPE_LOADING_MESSAGE,
} from "../utils/storeFilterFallbackState";
import { getSafeAnalyticsErrorMessage } from "../utils/analyticsErrorMessages";
import { CHART_TOOLTIP_STYLE, CHART_TOOLTIP_LABEL_STYLE } from "../utils/chartTooltipStyle";
import { fmtPct, fmtQty, fmtRsd, fmtSignedPct, getPresetRange, formatDate } from "../utils/analyticsFormatters";
import { toInclusiveCalendarDate, toUtcDateOnlyExclusive } from "../utils/analyticsDateRanges";
import {
  analyticsMetricDescriptions,
  buildPopMetricDescription,
  buildPrePostNivelacijaImpactDescription,
} from "../utils/analyticsMetricDescriptions";
import {
  RECOMMENDATION_CONFIDENCE_LABEL,
  RECOMMENDATION_RELIABILITY_LABEL,
  RECOMMENDATION_SIGNAL_UNAVAILABLE,
  RECOMMENDATION_STATUS_PRIORITY,
  normalizeRecommendationQualityStatus,
  recommendationQualityLabel,
  recommendationQualityStyle,
  recommendationReasonLabel,
  recommendationReasonHints,
  recommendationStatusLabel,
  recommendationStatusTone,
  recommendationStatusTooltipBrief,
  type CanonicalRecommendationStatus,
  type RecommendationQualityStatus,
} from "../utils/canonicalRecommendationSemantics";
import { qualityTierIcon, qualityTierClass, tierNeedsWarning, buildCoverageTooltip, buildRecommendationCaveat, buildMarginDetailNote, buildSnapshotBadgeLabel, buildSnapshotTooltip } from "../utils/marginQuality";
import {
  formatCategoryPrePostQuantityMetric,
  formatCategoryPrePostRevenueMetric,
} from "../utils/categoryPrePostDetailMetrics";
import {
  buildShoeTypeMarginComparisonProjection,
  formatShoeTypeMarginContributionShare,
} from "../utils/shoeTypeMarginComparison";
import {
  resolveShoeTypePercentValue,
  resolveShoeTypeSignedSharePct,
  resolveShoeTypeQuantitySharePct,
} from "../utils/shoeTypePercentRange";
import { resolveShoeTypeCoveragePct } from "../utils/shoeTypeSalesCoverage";
import { buildShoeTypeRecommendationProjection } from "../utils/shoeTypeStatusIdentity";
import { getAnalyticsDataFreshnessStatus } from "../utils/analyticsResponseMeta";
import { formatMetricDisplayValue } from "../utils/analyticsMetricValue";
import { readAnalyticsTableSort, writeAnalyticsTableSort } from "../utils/analyticsTableSortUrl";
import { compareNullableNumbers } from "../utils/nullableNumericSort";
import { useReliableAnalyticsQuery } from "../hooks/useReliableAnalyticsQuery";
import { buildStoreOptionLabel, getDuplicateStoreNames } from "../utils/storeFilterPresentation";
import "./ShoeTypeSalesStatsPage.css";

type PeriodPreset = "30d" | "90d" | "180d" | "365d" | "custom";
const SHOE_TYPE_ERROR_FALLBACK = "Greška pri učitavanju podataka po tipu obuće.";
const SHOE_TYPE_COST_SOURCE_ORDER = "Prioritet izvora troška: istorijski trošak sa prodajne stavke → tačan snapshot trošak → produkt-fallback/procena → bez troška.";
const SHOE_TYPE_COST_SOURCE_TOOLTIP = `${SHOE_TYPE_COST_SOURCE_ORDER} Snapshot i produkt-fallback/procena nisu istorijski trošak sa trenutka prodaje.`;
const SHOE_TYPE_SAFE_ERROR_MESSAGES = [
  SHOE_TYPE_ERROR_FALLBACK,
  "Statistika prodaje po tipu obuće trenutno nije dostupna.",
  "Podaci trenutno nisu dostupni.",
] as const;
type SortDir = "asc" | "desc";
type SortField =
  | "tipObuceNaziv"
  | "ukupanPromet"
  | "ukupnaKolicina"
  | "totalCost"
  | "sharePct"
  | "marginContribution"
  | "marginPct"
  | "popRevenueChangePct"
  | "prePostNivelacijaRevenueImpactPct"
  | "status";
const SHOE_SORT_FIELDS: readonly SortField[] = [
  "tipObuceNaziv",
  "ukupanPromet",
  "ukupnaKolicina",
  "totalCost",
  "sharePct",
  "marginContribution",
  "marginPct",
  "popRevenueChangePct",
  "prePostNivelacijaRevenueImpactPct",
  "status",
];
type DecisionStatus = CanonicalRecommendationStatus;

type ActiveFilters = {
  fromDate: string;
  toDate: string;
  sezonaId: number | null;
  storeId: number | null;
};

type DecisionShoeType = ShoeTypeSalesStat & {
  sharePct: number | null;
  totalCost: number | null;
  marginContribution: number;
  reliabilityPct: number | null;
  reliabilityAvailable: boolean;
  coveragePct: number | null;
  splitCoveragePct: number | null;
  confidencePct: number | null;
  recommendationConfidencePct: number | null;
  confidenceAvailable: boolean;
  recommendationAllowed: boolean;
  status: DecisionStatus;
  statusReason: string;
  dataQualityStatus: RecommendationQualityStatus;
  reasonCodes: string[];
};

const STATUS_PRIORITY: Record<DecisionStatus, number> = {
  ...RECOMMENDATION_STATUS_PRIORITY,
};

const decisionColumns: AnalyticsTableColumn<DecisionShoeType>[] = [
  { key: "tipObuceNaziv", header: "Tip obuće", dataType: "text" },
  { key: "ukupanPromet", header: "Promet", dataType: "currency" },
  { key: "ukupnaKolicina", header: "Količina", dataType: "number" },
  { key: "sharePct", header: "Neto udeo %", dataType: "percent" },
  { key: "marginContribution", header: "Maržni doprinos", dataType: "currency" },
  { key: "marginPct", header: "Marža %", dataType: "percent" },
  { key: "marginQualityLabel", header: "Kvalitet marže", dataType: "text" },
  { key: "popRevenueChangePct", header: "PoP trend %", dataType: "percent" },
  { key: "prePostNivelacijaRevenueImpactPct", header: "Uticaj nivelacije %", dataType: "percent" },
  { key: "splitCoveragePct", header: "Pre/post pokriće prometa %", dataType: "percent" },
  { key: "status", header: "Status signala", dataType: "text" },
  { key: "recommendationConfidencePct", header: RECOMMENDATION_CONFIDENCE_LABEL, dataType: "number" },
  { key: "coveragePct", header: "Udeo artikala sa nivelacijom %", dataType: "percent" },
  { key: "totalCost", header: "Nabavna vrednost (rešeni trošak)", dataType: "currency" },
];

const CHART_AXIS_TICK = { fill: "var(--chart-axis)", fontSize: 12, fontWeight: 600 };
const CHART_LEGEND_STYLE = { color: "var(--chart-axis)", fontSize: 12, fontWeight: 600, paddingTop: 10 };
const CHART_CURSOR_STYLE = { fill: "var(--dashboard-chart-hover)" };
const COMMAND_TOOLTIP_STYLE = {
  ...CHART_TOOLTIP_STYLE,
  background: "var(--chart-tooltip-bg)",
  border: "1px solid var(--border-default)",
  boxShadow: "var(--tooltip-box-shadow)",
  borderRadius: "12px",
};
const COMMAND_TOOLTIP_LABEL_STYLE = {
  ...CHART_TOOLTIP_LABEL_STYLE,
  color: "var(--chart-tooltip-text)",
  fontWeight: 700,
};

function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, value));
}

function toUtcRange(fromDate: string, toDate: string): { fromDate: string; toDate: string } {
  return {
    fromDate: `${fromDate}T00:00:00Z`,
    toDate: toUtcDateOnlyExclusive(toDate),
  };
}

function toDateOnly(value: string): string {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return value.slice(0, 10);
  return parsed.toISOString().slice(0, 10);
}

function smoothScrollToElement(element: HTMLElement, durationMs = 850): void {
  if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
    element.scrollIntoView({ behavior: "auto", block: "start" });
    return;
  }

  const startY = window.scrollY;
  const targetY = element.getBoundingClientRect().top + window.scrollY - 100;
  const distance = targetY - startY;
  if (Math.abs(distance) < 2) return;

  const startTime = performance.now();
  const easeInOutCubic = (t: number): number => (t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2);

  const tick = (now: number) => {
    const progress = clamp((now - startTime) / durationMs, 0, 1);
    const nextY = startY + distance * easeInOutCubic(progress);
    window.scrollTo(0, nextY);
    if (progress < 1) window.requestAnimationFrame(tick);
  };

  window.requestAnimationFrame(tick);
}

function normalizeName(value: string | null | undefined): string {
  return (value ?? "").trim().toUpperCase();
}

function sortMarker(field: SortField, activeField: SortField, dir: SortDir): string {
  if (field !== activeField) return "";
  return dir === "asc" ? " ^" : " v";
}

function isSortActive(field: SortField, activeField: SortField): boolean {
  return field === activeField;
}

function statusClass(status: DecisionStatus): string {
  const tone = recommendationStatusTone(status);
  if (tone === "boost") return "shoetype-decision-status status-boost";
  if (tone === "keep") return "shoetype-decision-status status-keep";
  if (tone === "review") return "shoetype-decision-status status-review";
  if (tone === "reduce") return "shoetype-decision-status status-reduce";
  return "shoetype-decision-status status-na";
}

function displayStatusLabel(status: DecisionStatus): string {
  return recommendationStatusLabel(status);
}

function trendClass(value: number | null | undefined): string {
  if (value == null || !Number.isFinite(value)) return "trend-neutral";
  if (value > 0) return "trend-up";
  if (value < 0) return "trend-down";
  return "trend-neutral";
}

type StatusTooltipData = {
  status: DecisionStatus;
  statusReason: string;
  sharePct: number | null;
  marginPct: number | null;
  popRevenueChangePct: number | null;
  prePostNivelacijaRevenueImpactPct: number | null;
  previousPeriodRevenue: number | null;
  splitCoveragePct: number | null;
  reliabilityPct: number | null;
  reliabilityAvailable: boolean;
  coveragePct: number | null;
  confidencePct: number | null;
  confidenceAvailable: boolean;
  dataQualityStatus: RecommendationQualityStatus;
  reasonCodes: string[];
  sharePctBasis?: "net_sales_signed" | null;
  sharePctNumerator?: number | null;
  sharePctDenominator?: number | null;
  sharePctUnavailableReason?: "non_positive_net_sales_denominator" | null;
};

function buildStatusTooltip(data: StatusTooltipData): string {
  const popText = data.popRevenueChangePct != null
    ? fmtSignedPct(data.popRevenueChangePct, 1)
    : data.previousPeriodRevenue != null && data.previousPeriodRevenue <= 0
      ? "Novo / bez prethodne baze"
      : "Nije dostupno";
  const impactText = data.prePostNivelacijaRevenueImpactPct != null
    ? fmtSignedPct(data.prePostNivelacijaRevenueImpactPct, 1)
    : "Nije dostupno";
  const reliabilityText = data.reliabilityAvailable ? fmtPct(data.reliabilityPct, 0) : RECOMMENDATION_SIGNAL_UNAVAILABLE;
  const confidenceText = data.confidenceAvailable ? fmtPct(data.confidencePct, 0) : RECOMMENDATION_SIGNAL_UNAVAILABLE;
  const qualityText = recommendationQualityLabel(data.dataQualityStatus);
  const hintText = recommendationReasonHints(data.reasonCodes).join(" | ");
  const shareText = data.sharePctBasis === "net_sales_signed"
    ? `${fmtPct(data.sharePct, 1)} (neto promet: ${fmtRsd(data.sharePctNumerator)} / ${fmtRsd(data.sharePctDenominator)}; povrati mogu dati <0% ili >100%)`
    : data.sharePctUnavailableReason === "non_positive_net_sales_denominator"
      ? "Nije dostupno (ukupan neto promet nije pozitivan)"
    : fmtPct(data.sharePct, 1);
  return `${recommendationStatusLabel(data.status)}: ${data.statusReason} | ${recommendationStatusTooltipBrief(data.status)} | Neto udeo ${shareText} | Marža ${fmtPct(data.marginPct, 1)} | PoP ${popText} | Nivelacija artikala ${fmtPct(data.coveragePct, 1)} | Uticaj nivelacije ${impactText} | Split pokriće ${fmtPct(data.splitCoveragePct, 1)} | ${RECOMMENDATION_RELIABILITY_LABEL} ${reliabilityText} | ${RECOMMENDATION_CONFIDENCE_LABEL} ${confidenceText} | Kvalitet ${qualityText}${hintText ? ` | Napomene: ${hintText}` : ""}`;
}

function describePopMetric(item: ShoeTypeSalesStat): { label: string; title: string; className: string } {
  if (item.popRevenueChangePct != null && !Number.isNaN(item.popRevenueChangePct)) {
    return {
      label: fmtSignedPct(item.popRevenueChangePct, 2),
      title: buildPopMetricDescription(item.previousPeriodRevenue),
      className: trendClass(item.popRevenueChangePct),
    };
  }

  if (item.previousPeriodRevenue === 0 && item.ukupanPromet > 0) {
    return {
      label: "Novo",
      title: "Tip obuće nije imao promet u prethodnom uporedivom periodu, pa PoP procenat nije smislen.",
      className: "trend-neutral",
    };
  }

  return {
    label: "Nije dostupno",
    title: "PoP trend nije dostupan jer ne postoji validna prethodna baza za poređenja.",
    className: "trend-neutral",
  };
}

export function describeNivelacijaImpactMetric(item: ShoeTypeSalesStat): { label: string; title: string; className: string } {
  if (Number.isFinite(item.prePostNivelacijaRevenueImpactPct)) {
    return {
      label: fmtSignedPct(item.prePostNivelacijaRevenueImpactPct, 2),
      title: buildPrePostNivelacijaImpactDescription(
        item.prePostNivelacijaRevenueCoveragePct,
        item.prePostSignalNote ? `Napomena: ${item.prePostSignalNote}` : undefined
      ),
      className: trendClass(item.prePostNivelacijaRevenueImpactPct),
    };
  }

  if (item.prePostSignalNote) {
    return {
      label: "Slab signal",
      title: item.prePostSignalNote,
      className: "trend-neutral",
    };
  }

  const coverage = item.prePostNivelacijaRevenueCoveragePct;
  if (typeof coverage !== "number" || !Number.isFinite(coverage) || coverage < 0) {
    return {
      label: "Nije dostupno",
      title: "Pre/post pokriće nije dostupno jer validno pokriće nije dostupno za ovaj skup podataka.",
      className: "trend-neutral",
    };
  }

  if (coverage === 0) {
    return {
      label: "0% pokriće",
      title: "Pre/post pokriće je izmereno kao 0%; nema artikala sa prodajom i pre i posle prve nivelacije, pa uticaj nije merljiv.",
      className: "trend-neutral",
    };
  }

  if (item.preNivelacijePromet <= 0 && item.posleNivelacijePromet > 0) {
    return {
      label: "Bez baze",
      title: "Postoji promet posle prve nivelacije, ali nema pre-nivelacija baze za smislen procenat promene.",
      className: "trend-neutral",
    };
  }

  return {
    label: "Nije dostupno",
    title: "Uticaj pre/post nivelacije nije dostupan za izabrani skup podataka.",
    className: "trend-neutral",
  };
}

function shoeTypeKey(item: { tipObuceId: number | null; tipObuceNaziv: string }): string {
  if (item.tipObuceId != null) return `id:${item.tipObuceId}`;
  return `name:${normalizeName(item.tipObuceNaziv)}`;
}

function parseShoeTypeDate(value: string | null): string | null {
  if (!value) return null;
  const date = value.slice(0, 10);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) return null;
  const parsed = new Date(`${date}T00:00:00.000Z`);
  return Number.isNaN(parsed.getTime()) || parsed.toISOString().slice(0, 10) !== date ? null : date;
}

function parseShoeTypePeriodPreset(value: string | null): PeriodPreset | null {
  return value === "30d" || value === "90d" || value === "180d" || value === "365d" || value === "custom"
    ? value
    : null;
}

function resolveShoeTypeUrlState(searchParams: URLSearchParams): { preset: PeriodPreset; filters: ActiveFilters } {
  const requestedPreset = parseShoeTypePeriodPreset(searchParams.get("periodPreset"));
  const fromDate = parseShoeTypeDate(searchParams.get("fromDate"));
  const toDate = parseShoeTypeDate(searchParams.get("toDate"));
  const hasValidRange = fromDate != null && toDate != null && fromDate <= toDate;
  const preset = hasValidRange ? requestedPreset ?? "custom" : requestedPreset && requestedPreset !== "custom" ? requestedPreset : "30d";
  const range = hasValidRange ? { fromDate, toDate } : preset === "custom" ? getPresetRange("30d") : getPresetRange(preset);
  return {
    preset,
    filters: {
      fromDate: range.fromDate,
      toDate: range.toDate,
      sezonaId: parseEntityIdParam(searchParams.get("sezonaId")),
      storeId: parseEntityIdParam(searchParams.get("storeId")),
    },
  };
}

function writeShoeTypeUrlState(current: URLSearchParams, preset: PeriodPreset, filters: ActiveFilters): URLSearchParams {
  const next = new URLSearchParams(current);
  next.set("periodPreset", preset);
  next.set("fromDate", filters.fromDate);
  next.set("toDate", filters.toDate);
  if (filters.sezonaId == null) next.delete("sezonaId");
  else next.set("sezonaId", String(filters.sezonaId));
  if (filters.storeId == null) next.delete("storeId");
  else next.set("storeId", String(filters.storeId));
  return next;
}

export default function ShoeTypeSalesStatsPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams, setSearchParams] = useSearchParams();
  const setSearchParamsRef = useRef(setSearchParams);
  setSearchParamsRef.current = setSearchParams;
  const detailSectionRef = useRef<HTMLElement>(null);
  const queryState = useMemo(() => resolveShoeTypeUrlState(searchParams), [searchParams]);

  const [periodPreset, setPeriodPreset] = useState<PeriodPreset>(queryState.preset);
  const [fromDate, setFromDate] = useState(queryState.filters.fromDate);
  const [toDate, setToDate] = useState(queryState.filters.toDate);
  const [sezonaId, setSezonaId] = useState<number | null>(queryState.filters.sezonaId);
  const [storeId, setStoreId] = useState<number | null>(queryState.filters.storeId);
  const [activeFilters, setActiveFilters] = useState<ActiveFilters>(queryState.filters);

  const [stores, setStores] = useState<StoreOption[]>([]);
  const [storesLoadError, setStoresLoadError] = useState<string | null>(null);
  const [storesWarning, setStoresWarning] = useState<string | null>(null);
  const [storesStale, setStoresStale] = useState(false);
  const [storesScope, setStoresScope] = useState<DataScope | null>(null);
  const [storeValidationNonce, setStoreValidationNonce] = useState(0);
  const [storesReloadNonce, setStoresReloadNonce] = useState(0);
  const [dataScope, setDataScopeValue] = useState<DataScope>(() => getDataScope());
  const pendingStoreIdRef = useRef<number | null>(null);
  const storesStateRef = useRef<{ scope: DataScope | null; stale: boolean; loadError: string | null }>({ scope: null, stale: false, loadError: null });
  storesStateRef.current = { scope: storesScope, stale: storesStale, loadError: storesLoadError };
  const duplicateStoreNames = useMemo(() => getDuplicateStoreNames(stores), [stores]);
  const [sortField, setSortField] = useState<SortField>(() => readAnalyticsTableSort(searchParams, SHOE_SORT_FIELDS, "status", "desc").field);
  const [sortDir, setSortDir] = useState<SortDir>(() => readAnalyticsTableSort(searchParams, SHOE_SORT_FIELDS, "status", "desc").dir);
  const [expandedTypeKey, setExpandedTypeKey] = useState<string | null>(null);

  useEffect(() => {
    const nextSort = readAnalyticsTableSort(searchParams, SHOE_SORT_FIELDS, "status", "desc");
    setSortField((current) => current === nextSort.field ? current : nextSort.field);
    setSortDir((current) => current === nextSort.dir ? current : nextSort.dir);
    setPeriodPreset((current) => current === queryState.preset ? current : queryState.preset);
    setFromDate((current) => current === queryState.filters.fromDate ? current : queryState.filters.fromDate);
    setToDate((current) => current === queryState.filters.toDate ? current : queryState.filters.toDate);
    setSezonaId((current) => current === queryState.filters.sezonaId ? current : queryState.filters.sezonaId);
    setStoreId((current) => current === queryState.filters.storeId ? current : queryState.filters.storeId);
    setActiveFilters((current) => (
      current.fromDate === queryState.filters.fromDate
      && current.toDate === queryState.filters.toDate
      && current.sezonaId === queryState.filters.sezonaId
      && current.storeId === queryState.filters.storeId
        ? current
        : queryState.filters
    ));
    const canonical = writeShoeTypeUrlState(searchParams, queryState.preset, queryState.filters);
    if (canonical.toString() !== searchParams.toString()) setSearchParams(canonical, { replace: true });
  }, [queryState, searchParams, setSearchParams]);

  const invalidRange = useMemo(() => {
    if (!fromDate || !toDate) return false;
    return new Date(fromDate) > new Date(toDate);
  }, [fromDate, toDate]);

  useEffect(() => {
    const handleScopeChange = () => {
      setDataScopeValue(getDataScope());
    };

    window.addEventListener("trendplus:data-scope-changed", handleScopeChange);
    return () => {
      window.removeEventListener("trendplus:data-scope-changed", handleScopeChange);
    };
  }, []);

  useEffect(() => {
    if (storesScope == null || storesScope === dataScope) return;
    pendingStoreIdRef.current = pendingStoreIdRef.current ?? storeId;
    setStoreId(null);
    setActiveFilters((current) => current.storeId == null ? current : { ...current, storeId: null });
    setSearchParamsRef.current((current) => {
      if (!current.has("storeId")) return current;
      const next = new URLSearchParams(current);
      next.delete("storeId");
      return next;
    }, { replace: true });
  }, [dataScope, storesScope, storeId]);

  useEffect(() => {
    let cancelled = false;
    const previousStores = stores;
    const requestedStoreId = pendingStoreIdRef.current ?? storeId;
    const loadStores = async () => {
      try {
        const nextStores = await getStores(true, dataScope);
        if (cancelled) return;
        const resolved = resolveStoreFilterFallbackState(nextStores, previousStores, requestedStoreId);
        setStores(resolved.stores);
        setStoresWarning(resolved.warning);
        setStoresStale(resolved.isStale);
        setStoresScope(dataScope);
        setStoresLoadError(null);
        pendingStoreIdRef.current = null;
        setStoreId(resolved.selectedStoreId);
        setActiveFilters((current) => current.storeId === resolved.selectedStoreId
          ? current
          : { ...current, storeId: resolved.selectedStoreId });
        setSearchParamsRef.current((current) => {
          const next = new URLSearchParams(current);
          if (resolved.selectedStoreId == null) next.delete("storeId");
          else next.set("storeId", String(resolved.selectedStoreId));
          return next;
        }, { replace: true });
        if (requestedStoreId != null) setStoreValidationNonce((value) => value + 1);
      } catch {
        if (cancelled) return;
        const resolved = resolveStoreFilterLoadFailure(previousStores, requestedStoreId);
        setStores(resolved.stores);
        setStoresWarning(resolved.warning);
        setStoresStale(true);
        setStoresScope(dataScope);
        setStoresLoadError("stores_load_failed");
        pendingStoreIdRef.current = requestedStoreId;
        setStoreId(null);
        setActiveFilters((current) => current.storeId == null ? current : { ...current, storeId: null });
        setSearchParamsRef.current((current) => {
          if (!current.has("storeId")) return current;
          const next = new URLSearchParams(current);
          next.delete("storeId");
          return next;
        }, { replace: true });
        if (requestedStoreId != null) setStoreValidationNonce((value) => value + 1);
      }
    };

    void loadStores();
    return () => {
      cancelled = true;
    };
  }, [dataScope, storesReloadNonce]);

  const shoeTypeQuery = useCallback((signal: AbortSignal) => {
    const currentRange = toUtcRange(activeFilters.fromDate, activeFilters.toDate);
    const { scope: loadedStoreScope, stale: storesAreStale, loadError: storeLoadError } = storesStateRef.current;
    const scopedStoreId = loadedStoreScope === dataScope && !storesAreStale && storeLoadError == null
      ? activeFilters.storeId
      : null;
    return getShoeTypeSalesStats({
      ...currentRange,
      sezonaId: activeFilters.sezonaId,
      storeId: scopedStoreId,
      dataScope,
      signal,
    });
  }, [activeFilters, dataScope, storeValidationNonce]);
  const {
    data,
    initialLoading,
    refetching,
    error: queryError,
    staleWarning,
    refetch,
  } = useReliableAnalyticsQuery<ShoeTypeSalesStatsResponse>({
    query: shoeTypeQuery,
    enabled: storesScope === dataScope || (storesScope == null && storeId == null),
    getErrorMessage: useCallback((reason: unknown) => getSafeAnalyticsErrorMessage(
      reason instanceof Error ? reason.message : null,
      null,
      SHOE_TYPE_ERROR_FALLBACK,
      SHOE_TYPE_SAFE_ERROR_MESSAGES,
    ), []),
  });
  const loading = initialLoading || refetching;
  const error = queryError
    ? getSafeAnalyticsErrorMessage(queryError, null, SHOE_TYPE_ERROR_FALLBACK, SHOE_TYPE_SAFE_ERROR_MESSAGES)
    : null;

  const decisionRows = useMemo<DecisionShoeType[]>(() => {
    const rows = data?.shoeTypes ?? [];
    if (rows.length === 0) return [];

    return rows.map((item) => {
      const sharePct = resolveShoeTypeSignedSharePct(item.sharePct);
      const totalCost = item.totalCost ?? null;
      const marginContribution = item.marginContribution;
      const splitCoveragePct = resolveShoeTypePercentValue(item.prePostNivelacijaRevenueCoveragePct);
      const coveragePct = resolveShoeTypeCoveragePct(
        item.brojArtikalaSaNivelacijom,
        item.brojArtikalaUkupno,
      );
      const recommendationProjection = buildShoeTypeRecommendationProjection(
        item.recommendation,
        item.reliabilityPct,
      );

      return {
        ...item,
        sharePct,
        totalCost,
        marginContribution,
        reliabilityPct: recommendationProjection.reliabilityPct,
        reliabilityAvailable: recommendationProjection.reliabilityAvailable,
        coveragePct,
        splitCoveragePct,
        confidencePct: recommendationProjection.confidencePct,
        recommendationConfidencePct: recommendationProjection.confidencePct,
        confidenceAvailable: recommendationProjection.confidenceAvailable,
        recommendationAllowed: recommendationProjection.recommendationAllowed,
        status: recommendationProjection.status,
        statusReason: recommendationProjection.statusReason,
        dataQualityStatus: normalizeRecommendationQualityStatus(item.recommendation?.dataQualityStatus),
        reasonCodes: item.recommendation?.reasonCodes ?? [],
      };
    });
  }, [data?.shoeTypes]);

  const sortedRows = useMemo(() => {
    const rows = [...decisionRows];
    return rows.sort((a, b) => {
      let compare = 0;

      if (sortField === "tipObuceNaziv") {
        compare = sortDir === "asc"
          ? a.tipObuceNaziv.localeCompare(b.tipObuceNaziv, "sr")
          : -a.tipObuceNaziv.localeCompare(b.tipObuceNaziv, "sr");
      } else if (sortField === "ukupanPromet") {
        compare = compareNullableNumbers(a.ukupanPromet, b.ukupanPromet, sortDir);
      } else if (sortField === "ukupnaKolicina") {
        compare = compareNullableNumbers(a.ukupnaKolicina, b.ukupnaKolicina, sortDir);
      } else if (sortField === "totalCost") {
        compare = compareNullableNumbers(a.totalCost, b.totalCost, sortDir);
      } else if (sortField === "sharePct") {
        compare = compareNullableNumbers(a.sharePct, b.sharePct, sortDir);
      } else if (sortField === "marginContribution") {
        compare = compareNullableNumbers(a.marginContribution, b.marginContribution, sortDir);
      } else if (sortField === "marginPct") {
        compare = compareNullableNumbers(a.marginPct, b.marginPct, sortDir);
      } else if (sortField === "popRevenueChangePct") {
        compare = compareNullableNumbers(a.popRevenueChangePct, b.popRevenueChangePct, sortDir);
      } else if (sortField === "prePostNivelacijaRevenueImpactPct") {
        compare = compareNullableNumbers(a.prePostNivelacijaRevenueImpactPct, b.prePostNivelacijaRevenueImpactPct, sortDir);
      } else if (sortField === "status") {
        compare = sortDir === "asc"
          ? STATUS_PRIORITY[a.status] - STATUS_PRIORITY[b.status]
          : STATUS_PRIORITY[b.status] - STATUS_PRIORITY[a.status];
      }

      if (compare === 0) compare = compareNullableNumbers(a.recommendationConfidencePct, b.recommendationConfidencePct, sortDir);
      if (compare === 0) compare = compareNullableNumbers(a.ukupanPromet, b.ukupanPromet, sortDir);

      return compare;
    });
  }, [decisionRows, sortDir, sortField]);

  const selectedRow = useMemo(
    () => sortedRows.find((row) => shoeTypeKey(row) === expandedTypeKey) ?? null,
    [expandedTypeKey, sortedRows]
  );

  useEffect(() => {
    if (!selectedRow && sortedRows.length > 0 && expandedTypeKey != null) {
      setExpandedTypeKey(null);
    }
  }, [expandedTypeKey, selectedRow, sortedRows.length]);

  useEffect(() => {
    if (!selectedRow || !detailSectionRef.current) return;
    const delay = 120;
    const timeoutId = window.setTimeout(() => {
      if (!detailSectionRef.current) return;
      smoothScrollToElement(detailSectionRef.current);
    }, delay);
    return () => window.clearTimeout(timeoutId);
  }, [selectedRow]);

  const totalRevenue = data ? data.totals.ukupanPromet : null;
  const totalMarginContribution = useMemo(
    () => data ? data.totals.ukupanMarzniDoprinos : null,
    [data?.totals.ukupanMarzniDoprinos]
  );

  const periodGrowthPct = useMemo(() => data?.totals.popRevenueChangePct ?? null, [data?.totals.popRevenueChangePct]);

  const concentrationData = useMemo(() => {
    if (sortedRows.length === 0) return [] as Array<{ name: string; sharePct: number }>;

    const ranked = [...sortedRows]
      .filter((row): row is typeof row & { sharePct: number } => resolveShoeTypeSignedSharePct(row.sharePct) != null)
      .sort((a, b) => b.sharePct - a.sharePct);
    if (ranked.length === 0) return [];
    const topRows = ranked.slice(0, 6).map((row) => ({
      name: row.tipObuceNaziv,
      sharePct: Number(row.sharePct.toFixed(2)),
    }));

    const remaining = ranked.slice(6).reduce((sum, row) => sum + row.sharePct, 0);
    const ostaliSharePct = resolveShoeTypeSignedSharePct(Number(remaining.toFixed(2)));
    if (ostaliSharePct != null && Math.abs(ostaliSharePct) > 0.1) {
      topRows.push({ name: "Ostali", sharePct: ostaliSharePct });
    }

    return topRows;
  }, [sortedRows]);

  const marginComparison = useMemo(
    () => buildShoeTypeMarginComparisonProjection(
      sortedRows,
      totalMarginContribution,
      (sharePct) => resolveShoeTypeSignedSharePct(sharePct) != null,
    ),
    [sortedRows, totalMarginContribution],
  );

  const avgMarginPct = data?.totals.prosecnaMarza ?? null;

  const counts = useMemo(() => {
    const increaseFocus = sortedRows.filter((row) => row.status === "increase_focus").length;
    const maintain = sortedRows.filter((row) => row.status === "maintain").length;
    const review = sortedRows.filter((row) => row.status === "review").length;
    const doNotTrust = sortedRows.filter((row) => row.status === "do_not_trust").length;
    const insufficientData = sortedRows.filter((row) => row.status === "insufficient_data").length;
    return { increaseFocus, maintain, review, doNotTrust, insufficientData };
  }, [sortedRows]);

  const activeSezonaLabel = useMemo(() => {
    if (activeFilters.sezonaId == null) return "Sve sezone";
    return data?.sezone.find((item) => item.id === activeFilters.sezonaId)?.naziv ?? String(activeFilters.sezonaId);
  }, [activeFilters.sezonaId, data?.sezone]);
  const activeStoreLabel = activeFilters.storeId == null
    ? "Svi objekti"
    : stores.find((store) => store.storeId === activeFilters.storeId)
      ? buildStoreOptionLabel(stores.find((store) => store.storeId === activeFilters.storeId)!, duplicateStoreNames)
      : `Nepoznat objekat (ID ${activeFilters.storeId})`;

  const emptyStateHint = useMemo(() => {
    if (!data || sortedRows.length > 0) return null;
    if (!data.dataWindowFrom || !data.dataWindowTo) {
      return "Nema podataka za izabrane filtere.";
    }

    const selectedFrom = new Date(`${activeFilters.fromDate}T00:00:00Z`);
    const selectedTo = new Date(toUtcDateOnlyExclusive(activeFilters.toDate));
    const dataFrom = new Date(data.dataWindowFrom);
    const dataTo = new Date(data.dataWindowTo);

    if (
      Number.isNaN(selectedFrom.getTime()) ||
      Number.isNaN(selectedTo.getTime()) ||
      Number.isNaN(dataFrom.getTime()) ||
      Number.isNaN(dataTo.getTime())
    ) {
      return "Nema podataka za izabrane filtere.";
    }

    if (selectedTo < dataFrom || selectedFrom > dataTo) {
      return `Izabrani period je van dostupnog raspona prodaje (${formatDate(data.dataWindowFrom)} - ${formatDate(data.dataWindowTo)}).`;
    }

    return "Nema podataka za izabrane filtere.";
  }, [activeFilters.fromDate, activeFilters.toDate, data, sortedRows.length]);

  const qualityNotes = useMemo(() => {
    if (!data) return [] as string[];

    const notes: string[] = [];
    const splitCoverage = resolveShoeTypePercentValue(data.dataQuality.revenueWithNivelacijaSplitSharePct);
    const historicalCostShare = resolveShoeTypePercentValue(data.dataQuality.historicalCostRevenueSharePct);
    const noCostShare = resolveShoeTypePercentValue(
      data.dataQuality.noCostRevenueSharePct ?? data.dataQuality.missingCostRevenueSharePct,
    );
    const estimatedCostShare = resolveShoeTypePercentValue(data.dataQuality.estimatedCostRevenueSharePct);
    const unknownShare = resolveShoeTypePercentValue(data.dataQuality.unknownTypeRevenueSharePct);

    if (splitCoverage != null && splitCoverage < 60) {
      notes.push(`Uporediv pre/posle signal trenutno pokriva ${fmtPct(splitCoverage, 1)} ukupnog prometa, pa ga treba čitati kao delimičan.`);
    }

    if (historicalCostShare != null && historicalCostShare < 100) {
      notes.push(`Istorijska nabavna cena postoji za ${fmtPct(historicalCostShare, 1)} prometa; marža za ostatak nije istorijski potvrđena na prodajnoj stavci.`);
    }

    if (noCostShare != null && noCostShare > 0) {
      notes.push(`Za ${fmtPct(noCostShare, 1)} prometa nije pronađena ni istorijska ni procenjena nabavna cena, pa marža nije merljiva.`);
    }

    if (estimatedCostShare != null && estimatedCostShare > 0) {
      notes.push(`Za ${fmtPct(estimatedCostShare, 1)} prometa nabavna cena je procenjena (bez direktnog troška) - maržu čitati oprezno.`);
    }

    if (unknownShare != null && unknownShare > 0) {
      notes.push(`Nepoznati tipovi obuće učestvuju sa ${fmtPct(unknownShare, 1)} ukupnog prometa.`);
    }

    const snapshotPct = resolveShoeTypePercentValue(data.totals.snapshotCostCoveragePct);
    if (data.totals.isSnapshotActive && snapshotPct != null && snapshotPct > 0) {
      notes.push(`Za ${fmtPct(snapshotPct, 1)} prometa trošak je stabilizovan zamrznutim snimkom. Ovo je reproduktivna procena, ne istorijska nabavna cena.`);
    }

    return notes;
  }, [data]);

  const headerDataQualityStatus = useMemo<"good" | "warning" | "critical" | "insufficient_data" | null>(() => {
    if (!data) return null;
    if ((data.shoeTypes ?? []).length === 0) return "insufficient_data";
    const missingCostShare = resolveShoeTypePercentValue(
      data.dataQuality.noCostRevenueSharePct ?? data.dataQuality.missingCostRevenueSharePct,
    );
    const splitCoverage = resolveShoeTypePercentValue(data.dataQuality.revenueWithNivelacijaSplitSharePct);
    if (missingCostShare == null || splitCoverage == null) return "insufficient_data";
    if (missingCostShare >= 50 || splitCoverage < 30) return "critical";
    if (qualityNotes.length > 0) return "warning";
    return "good";
  }, [data, qualityNotes.length]);

  const responseMeta = data?.meta ?? null;
  const trustDataQualityStatus = responseMeta?.dataQualityStatus ?? headerDataQualityStatus;
  const trustLastRefreshAt = responseMeta?.lastRefreshAtUtc ?? null;
  const trustIsPartial = responseMeta?.isPartial ?? false;
  const trustDataFreshnessStatus = getAnalyticsDataFreshnessStatus(responseMeta);
  const trustEmptyStateReason = responseMeta?.message ?? emptyStateHint;

  const showBlockingError = Boolean(queryError && !data);
  const showStaleError = Boolean(staleWarning && data);
  const emptyStateVariant = useMemo<"no_data" | "insufficient_data" | null>(() => {
    if (!data || sortedRows.length > 0) return null;
    if (headerDataQualityStatus === "insufficient_data") return "insufficient_data";
    return "no_data";
  }, [data, headerDataQualityStatus, sortedRows.length]);

  const toolbarFilters = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "fromDate", label: "Od", value: activeFilters.fromDate },
      { key: "toDate", label: "Do", value: activeFilters.toDate },
      { key: "sezonaId", label: "Sezona", value: activeSezonaLabel },
       { key: "storeId", label: "Objekat", value: activeStoreLabel },
      { key: "dataScope", label: "Opseg podataka", value: dataScopeLabel(dataScope) },
    ],
     [activeFilters.fromDate, activeStoreLabel, activeFilters.toDate, activeSezonaLabel, dataScope]
  );

  const toolbarMetadata = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "generatedAt", label: "Generisano", value: data?.generatedAt ?? "" },
      { key: "dataScope", label: "Opseg podataka", value: dataScopeLabel(data?.dataScope === "existing" || data?.dataScope === "imported" ? data.dataScope : dataScope) },
      { key: "tipova", label: "Tipova", value: formatMetricDisplayValue({ value: data?.totals.brojTipovaObuce, kind: "number", fallback: "Nije dostupno" }) },
      { key: "marginCoverage", label: "Pokrivenost istorijskim troškom %", value: fmtPct(resolveShoeTypePercentValue(data?.dataQuality.historicalCostRevenueSharePct), 1) },
      { key: "fallbackCoverage", label: "Promet sa produkt-fallback/procenom %", value: fmtPct(resolveShoeTypePercentValue(data?.dataQuality.estimatedCostRevenueSharePct), 1) },
      { key: "noCostCoverage", label: "Promet bez nabavne cene %", value: fmtPct(resolveShoeTypePercentValue(data?.dataQuality.noCostRevenueSharePct ?? data?.dataQuality.missingCostRevenueSharePct), 1) },
      { key: "splitCoverage", label: "Uporedivo pre/post pokriće", value: fmtPct(resolveShoeTypePercentValue(data?.dataQuality.revenueWithNivelacijaSplitSharePct), 1) },
      { key: "comparableArticleCount", label: "Uporedivih artikala", value: formatMetricDisplayValue({ value: data?.totals.comparableArticleCount, kind: "number", fallback: "Nije dostupno" }) },
      { key: "comparableImpact", label: "Ukupni uticaj nivelacije (uporediva kohorta)", value: fmtSignedPct(data?.totals.prePostNivelacijaRevenueImpactPct) },
      { key: "snapshotCoverage", label: "Pokrivenost troškom iz snimka %", value: fmtPct(resolveShoeTypePercentValue(data?.totals.snapshotCostCoveragePct), 1) },
      { key: "isSnapshotActive", label: "Snimak aktivan", value: data?.totals.isSnapshotActive ? "da" : "ne" },
      { key: "increaseFocus", label: recommendationStatusLabel("increase_focus"), value: counts.increaseFocus },
      { key: "maintain", label: recommendationStatusLabel("maintain"), value: counts.maintain },
      { key: "review", label: recommendationStatusLabel("review"), value: counts.review },
      { key: "doNotTrust", label: recommendationStatusLabel("do_not_trust"), value: counts.doNotTrust },
      { key: "insufficientData", label: recommendationStatusLabel("insufficient_data"), value: counts.insufficientData },
    ],
    [
      counts.doNotTrust,
      counts.increaseFocus,
      counts.insufficientData,
      counts.maintain,
      counts.review,
      data?.dataQuality.estimatedCostRevenueSharePct,
      data?.dataQuality.historicalCostRevenueSharePct,
      data?.dataQuality.noCostRevenueSharePct,
      data?.dataQuality.missingCostRevenueSharePct,
      data?.dataQuality.revenueWithNivelacijaSplitSharePct,
      data?.dataScope,
      data?.generatedAt,
      data?.totals.comparableArticleCount,
      data?.totals.prePostNivelacijaRevenueImpactPct,
      data?.totals.brojTipovaObuce,
      data?.totals.snapshotCostCoveragePct,
      data?.totals.isSnapshotActive,
      dataScope,
    ]
  );

  const openDetail = useCallback((row: DecisionShoeType) => {
    const recordId = row.tipObuceId != null
      ? String(row.tipObuceId)
      : "unknown-nepoznato";

    const params = new URLSearchParams();
    params.set("fromDate", `${activeFilters.fromDate}T00:00:00Z`);
    params.set("toDate", toUtcDateOnlyExclusive(activeFilters.toDate));
    if (activeFilters.sezonaId != null) params.set("sezonaId", String(activeFilters.sezonaId));
    if (activeFilters.storeId != null) params.set("storeId", String(activeFilters.storeId));
    params.set("dataScope", dataScope);

    saveAnalyticsDetailSnapshot(
      buildAnalyticsDetailSnapshot({
        table: "shoe-type-sales-stats",
        recordId,
        title: row.tipObuceNaziv,
        subtitle: "Detaljni pregled odluke po tipu obuće",
        columns: decisionColumns,
        row,
        metadata: toolbarFilters,
      })
    );

    navigate(`/analitika/shoe-type-sales-stats/${recordId}?${params.toString()}`, {
      state: { backgroundLocation: location },
    });
  }, [activeFilters.fromDate, activeFilters.sezonaId, activeFilters.storeId, activeFilters.toDate, dataScope, location, navigate, toolbarFilters]);

  const applyPreset = (preset: PeriodPreset) => {
    setPeriodPreset(preset);
    if (preset === "custom") return;
    const range = getPresetRange(preset);
    setSezonaId(null);
    setFromDate(range.fromDate);
    setToDate(range.toDate);
    const nextFilters = { fromDate: range.fromDate, toDate: range.toDate, sezonaId: null, storeId };
    setActiveFilters(nextFilters);
    setSearchParams((current) => writeShoeTypeUrlState(current, preset, nextFilters), { replace: true });
  };

  const handleSeasonChange = (value: string) => {
    const parsed = value ? Number(value) : null;
    setSezonaId(parsed);
    setPeriodPreset("custom");

    if (parsed == null) {
      const nextFilters = { fromDate, toDate, sezonaId: null, storeId };
      setActiveFilters(nextFilters);
      setSearchParams((current) => writeShoeTypeUrlState(current, "custom", nextFilters), { replace: true });
      return;
    }

    const selected = data?.sezone.find((item) => item.id === parsed);
    if (!selected) {
      const nextFilters = { fromDate, toDate, sezonaId: parsed, storeId };
      setActiveFilters(nextFilters);
      setSearchParams((current) => writeShoeTypeUrlState(current, "custom", nextFilters), { replace: true });
      return;
    }
    const newFrom = toDateOnly(selected.datumOd);
    const newTo = toDateOnly(selected.datumDo);
    setFromDate(newFrom);
    setToDate(newTo);
    const nextFilters = { fromDate: newFrom, toDate: newTo, sezonaId: parsed, storeId };
    setActiveFilters(nextFilters);
    setSearchParams((current) => writeShoeTypeUrlState(current, "custom", nextFilters), { replace: true });
  };

  const resetFilters = () => {
    const range = getPresetRange("30d");
    setPeriodPreset("30d");
    setFromDate(range.fromDate);
    setToDate(range.toDate);
    setSezonaId(null);
    setStoreId(null);
    const nextFilters = {
      fromDate: range.fromDate,
      toDate: range.toDate,
      sezonaId: null,
      storeId: null,
    };
    setActiveFilters(nextFilters);
    setSearchParams((current) => writeShoeTypeUrlState(current, "30d", nextFilters), { replace: true });
  };

  const handleSort = (field: SortField) => {
    const nextDir: SortDir = sortField === field
      ? (sortDir === "asc" ? "desc" : "asc")
      : (field === "tipObuceNaziv" ? "asc" : "desc");
    setSortField(field);
    setSortDir(nextDir);
    setSearchParams((current) => writeAnalyticsTableSort(current, field, nextDir), { replace: true });
  };

  const controlBarChips = useMemo<AnalyticsControlBarChip[]>(
    () => [
      {
        key: "scope",
        label: "Opseg",
        value: dataScopeLabel(data?.dataScope === "existing" || data?.dataScope === "imported" ? data.dataScope : dataScope),
        tone: "info",
      },
      {
        key: "period",
        label: "Period",
        value: `${activeFilters.fromDate} → ${activeFilters.toDate}`,
        tone: "neutral",
      },
      {
        key: "rows",
        label: "Prikazano",
        value: `${sortedRows.length.toLocaleString("sr-RS")} / ${decisionRows.length.toLocaleString("sr-RS")}`,
        tone: sortedRows.length < decisionRows.length ? "warning" : "success",
      },
    ],
    [
      activeFilters.fromDate,
      activeFilters.toDate,
      data?.dataScope,
      dataScope,
      decisionRows.length,
      sortedRows.length,
    ],
  );

  const controlBarFields = useMemo<AnalyticsControlBarField[]>(
    () => [
      {
        key: "preset",
        label: "Period",
        control: (
          <select value={periodPreset} onChange={(event) => applyPreset(event.target.value as PeriodPreset)}>
            <option value="30d">Poslednjih 30 dana</option>
            <option value="90d">Poslednjih 90 dana</option>
            <option value="180d">Poslednjih 180 dana</option>
            <option value="365d">Poslednjih 365 dana</option>
            <option value="custom">Prilagođeno</option>
          </select>
        ),
      },
      {
        key: "from",
        label: "Od",
        control: (
          <input
            type="date"
            value={fromDate}
            onChange={(event) => {
              const newFrom = event.target.value;
              setPeriodPreset("custom");
              setSezonaId(null);
              setFromDate(newFrom);
              if (newFrom.length === 10 && new Date(newFrom) <= new Date(toDate)) {
                const nextFilters = { fromDate: newFrom, toDate, sezonaId: null, storeId };
                setActiveFilters(nextFilters);
                setSearchParams((current) => writeShoeTypeUrlState(current, "custom", nextFilters), { replace: true });
              }
            }}
          />
        ),
      },
      {
        key: "to",
        label: "Do",
        control: (
          <input
            type="date"
            value={toDate}
            onChange={(event) => {
              const newTo = event.target.value;
              setPeriodPreset("custom");
              setSezonaId(null);
              setToDate(newTo);
              if (newTo.length === 10 && new Date(fromDate) <= new Date(newTo)) {
                const nextFilters = { fromDate, toDate: newTo, sezonaId: null, storeId };
                setActiveFilters(nextFilters);
                setSearchParams((current) => writeShoeTypeUrlState(current, "custom", nextFilters), { replace: true });
              }
            }}
          />
        ),
      },
      {
        key: "season",
        label: "Sezona",
        control: (
          <select value={sezonaId ?? ""} onChange={(event) => handleSeasonChange(event.target.value)}>
            <option value="">Sve sezone</option>
            {(data?.sezone ?? []).map((sezona) => (
              <option key={sezona.id} value={sezona.id}>
                {sezona.naziv}
              </option>
            ))}
          </select>
        ),
      },
      {
        key: "store",
        label: "Objekat",
        control: (
          <select
            disabled={storesLoadError != null || storesStale || storesScope !== dataScope}
            value={storesScope === dataScope && !storesStale ? storeId ?? "" : ""}
            onChange={(event) => {
              const newStore = event.target.value ? Number(event.target.value) : null;
              setStoreId(newStore);
              const nextFilters = { fromDate, toDate, sezonaId, storeId: newStore };
              setActiveFilters(nextFilters);
              setSearchParams((current) => writeShoeTypeUrlState(current, periodPreset, nextFilters), { replace: true });
            }}
          >
            <option value="">Svi objekti</option>
            {stores.map((store) => (
              <option key={store.storeId} value={store.storeId}>
                {buildStoreOptionLabel(store, duplicateStoreNames)}
              </option>
            ))}
          </select>
        ),
      },
    ],
    [data?.sezone, dataScope, fromDate, periodPreset, sezonaId, storeId, stores, storesLoadError, storesScope, storesStale, toDate],
  );

  return (
    <div className="shoetype-decision-page">
      <AnalyticsTrustHeader
        title="Prodaja po tipu obuće"
        description="Podržavajući signal za analizu asortimana po tipu obuće; nije samostalna konačna preporuka."
        periodFrom={data?.fromDate ? toDateOnly(data.fromDate) : activeFilters.fromDate}
        periodTo={toInclusiveCalendarDate(data?.toDate) ?? activeFilters.toDate}
        observedPeriodFrom={responseMeta?.observedPeriodFromUtc}
        observedPeriodTo={responseMeta?.observedPeriodToUtc}
        lastRefreshAt={trustLastRefreshAt}
        dataFreshnessStatus={trustDataFreshnessStatus}
        dataSource={`Analitika prodajnih činjenica (opseg: ${dataScopeLabel(data?.dataScope === "existing" || data?.dataScope === "imported" ? data.dataScope : dataScope)})`}
        dataQualityStatus={trustDataQualityStatus}
        meta={responseMeta}
        trustPending={loading}
        showOperationsTrust
        mode="signal"
        isPartial={trustIsPartial}
        recommendationNote="Tip obuće je podržavajući analitički signal, ne samostalna konačna preporuka. Backend status i razlog ostaju autoritativna evidencija."
        emptyStateReason={!loading && !showBlockingError && trustEmptyStateReason ? trustEmptyStateReason : null}
        methodologyHref="/analytics/data-quality"
        dataQualityHref="/analytics/data-quality"
        refreshStatusHref="/admin/configuration?panel=workers"
        compact
      />

      {storesWarning || (storesScope != null && storesScope !== dataScope)
        ? <AnalyticsFilterLoadNotice
            onRetry={() => setStoresReloadNonce((value) => value + 1)}
            message={storesWarning ?? STORE_FILTER_SCOPE_LOADING_MESSAGE}
            stale={(storesStale && storesLoadError == null) || storesScope !== dataScope}
          />
        : null}

      <AnalyticsControlBar
        title="Opseg i filteri"
        description="Period, sezona i objekat ostaju ovde; prioritetna lista ispod ostaje fokusirana na tip obuće."
        chips={controlBarChips}
        primaryAction={{
          key: "reset",
          label: loading ? "Učitavanje..." : "Poništi filtere",
          onClick: resetFilters,
          disabled: loading,
        }}
        secondaryActions={[
          {
            key: "data-quality",
            label: "Kvalitet podataka",
            to: "/analytics/data-quality",
            tone: "secondary",
          },
        ]}
        fields={controlBarFields}
        responsiveFilterLayout
        mobileFilterSummary="Period i filteri"
      />

      {invalidRange ? (
        <div className="shoetype-decision-message error">Datum od ne može biti posle datuma do.</div>
      ) : null}
      {showBlockingError ? (
        <AnalyticsErrorState
          title="Podaci trenutno nisu dostupni"
          message="Ne prikazujemo nule jer nije potvrđeno da je period stvarno prazan."
          onRetry={refetch}
          helpHref="/analytics/data-quality"
        />
      ) : null}
      {showStaleError ? (
        <div
          className="shoetype-decision-message info"
          role="status"
          aria-live="polite"
          data-testid="shoe-type-stale-refetch-warning"
        >
          Prikazujemo prethodno ucitane podatke. Novi upit nije uspeo.
        </div>
      ) : null}
      {loading && !data ? (
        <div className="shoetype-decision-loading" role="status" aria-live="polite">
          <UltraSpinner size="md" label="Učitavam tipove obuće" />
          <span>Učitavam tipove obuće...</span>
        </div>
      ) : null}
      {!loading && !showBlockingError && emptyStateHint ? (
        <AnalyticsEmptyState
          variant={emptyStateVariant ?? "no_data"}
            message={
              emptyStateVariant === "insufficient_data"
                ? "Ne prikazujemo automatsku preporuku jer signal nije dovoljno jak."
                : emptyStateHint
          }
          actions={[
            { label: "Proširite period pretrage." },
            { label: "Uklonite filter prodavnice ili sezone." },
          ]}
          dataQualityHref="/analytics/data-quality"
          refreshStatusHref="/admin/configuration?panel=workers"
          emptyReason={responseMeta?.emptyReason ?? null}
          onRetry={refetch}
        />
      ) : null}


      {data ? (
        <div
          className={`shoetype-decision-content${loading ? " shoetype-decision-content--refetching" : ""}`}
          aria-busy={loading || undefined}
        >
          {loading ? (
            <div className="shoetype-decision-refetch-overlay" aria-hidden="true">
              <UltraSpinner size="sm" label="Osvežavam podatke" />
            </div>
          ) : null}
          {!emptyStateHint ? (
            <section className="shoetype-decision-kpis">
              <article className="shoetype-decision-kpi analytics-kpi-card analytics-kpi-card--tone-info" data-note="Promet svih tipova obuće u izabranom periodu.">
                <span>Ukupan promet <InfoTip text="Zbir prodajnih vrednosti svih tipova obuće u izabranom periodu. Formula: Σ prodajna vrednost stavki po tipu u periodu (RSD)." /></span>
                <strong>{fmtRsd(totalRevenue)}</strong>
              </article>
              <article className="shoetype-decision-kpi analytics-kpi-card analytics-kpi-card--tone-success" data-note="Ukupan broj prodatih komada kroz sve tipove.">
                <span>Ukupno prodato <InfoTip text="Ukupan broj prodatih komada svih tipova obuće u izabranom periodu." /></span>
                <strong>{fmtQty(data.totals.ukupnaKolicina)}</strong>
              </article>
      <article className="shoetype-decision-kpi analytics-kpi-card analytics-kpi-card--tone-neutral" data-note="Trošak robe sa dostupnim ili procenjenim ulazom.">
                <span>Ukupna nabavna vrednost <InfoTip text={`Zbir troška robe za deo prometa sa dostupnim troškom. ${SHOE_TYPE_COST_SOURCE_TOOLTIP} Operativni troškovi nisu uključeni.`} /></span>
                <strong>{fmtRsd(data.totals.ukupanTrosak)}</strong>
              </article>
              <article className="shoetype-decision-kpi analytics-kpi-card analytics-kpi-card--tone-value" data-note="Bruto maržni doprinos po tipovima obuće.">
                <span>Ukupan maržni doprinos <InfoTip text="Zbir razlike između prodajne i nabavne vrednosti za sve stavke sa dostupnim troškom, grupisano po tipu obuće. Operativni troškovi, plate, zakup i ostali indirektni troškovi nisu uključeni." /></span>
                <strong>{fmtRsd(totalMarginContribution)}</strong>
                <small
                  className={`shoetype-decision-kpi-badge ${qualityTierClass(data.totals.marginQualityTier)}`}
                  title={data.totals.marginQualityTooltip ?? buildCoverageTooltip(data.totals.historicalCostCoveragePct, data.totals.estimatedCostCoveragePct, data.totals.noCostCoveragePct, fmtPct, data.totals.snapshotCostCoveragePct)}
                >
                  {qualityTierIcon(data.totals.marginQualityTier)} {data.totals.marginQualityShortLabel ?? data.totals.marginQualityLabel}
                </small>
                {data.totals.isSnapshotActive && data.totals.snapshotCostCoveragePct != null && data.totals.snapshotCostCoveragePct > 0 ? (
                  <small
                    className="shoetype-decision-kpi-badge quality-snapshot"
                    title={buildSnapshotTooltip(data.totals.snapshotCostCoveragePct, data.totals.snapshotGeneratedAtUtc, fmtPct)}
                  >
                    ★ {buildSnapshotBadgeLabel(data.totals.snapshotGeneratedAtUtc)}
                  </small>
                ) : null}
              </article>
              <article className="shoetype-decision-kpi analytics-kpi-card analytics-kpi-card--tone-info" data-note="Autoritativni backend agregat prosečne marže; bez merljivog denominatora prikazuje se kao nedostupno.">
                <span>Prosečna marža <InfoTip text="Ponderisana prosečna marža koju vraća backend. Računa se iz maržnog doprinosa i prometa sa pouzdano rešenim troškom poznatih tipova obuće; red „Nepoznato“ nije uključen, pa se može razlikovati od odnosa ukupnog maržnog doprinosa i prometa sa troškom. Frontend je ne izvodi iz redova." /></span>
                <strong>{fmtPct(avgMarginPct, 1)}</strong>
              </article>
              <article className="shoetype-decision-kpi analytics-kpi-card analytics-kpi-card--tone-success" data-note="Promena prometa prema prethodnom uporedivom periodu.">
                <span>PoP trend prometa <InfoTip text="Promena ukupnog prometa u odnosu na prethodni uporedivi period iste dužine. Formula: (trenutni promet – prethodni promet) / prethodni promet × 100. Nije dostupno ako prethodni period nije dostupan." /></span>
                <strong className={trendClass(periodGrowthPct)}>{fmtSignedPct(periodGrowthPct)}</strong>
              </article>
            </section>
          ) : null}

          {qualityNotes.length > 0 ? (
            <div className="shoetype-decision-message info">
              <strong>Kvalitet podataka:</strong> {qualityNotes.join(" ")}
            </div>
          ) : null}

          <section className="shoetype-decision-panels">
            <article className="shoetype-decision-card shoetype-decision-card--chart analytics-surface-panel">
              <h2>Koncentracija neto prometa po tipu obuće <InfoTip text="Grafikon prikazuje potpisani neto udeo ukupnog prometa po tipu obuće. Povrati mogu dati negativan udeo ili udeo veći od 100%; to je posledica neto imenice, ne greška prikaza." /></h2>
              <p>Rangirano po potpisanom neto prometu; povrati mogu dati negativan udeo ili udeo veći od 100%.</p>
              {concentrationData.length > 0 ? (
                <div className="shoetype-decision-chart-wrap" data-testid="shoe-type-concentration-chart">
                  <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={260}>
                    <BarChart data={concentrationData} layout="vertical" margin={{ top: 12, right: 16, left: 8, bottom: 8 }}>
                      <defs>
                        <linearGradient id="shoeShareGradient" x1="0" y1="0" x2="1" y2="0">
                          <stop offset="0%" stopColor="var(--chart-series-1)" />
                          <stop offset="100%" stopColor="var(--chart-series-3)" />
                        </linearGradient>
                      </defs>
                      <CartesianGrid strokeDasharray="2 6" stroke="var(--chart-grid)" />
                      <XAxis type="number" tick={CHART_AXIS_TICK} tickLine={false} axisLine={false} unit="%" />
                      <YAxis type="category" dataKey="name" width={180} tick={CHART_AXIS_TICK} tickLine={false} axisLine={false} />
                      <Tooltip contentStyle={COMMAND_TOOLTIP_STYLE} labelStyle={COMMAND_TOOLTIP_LABEL_STYLE} cursor={CHART_CURSOR_STYLE} formatter={(value: number | string | undefined) => value == null ? "Nije dostupno" : fmtPct(Number(value), 2)} />
                      <Legend wrapperStyle={CHART_LEGEND_STYLE} iconType="circle" iconSize={8} />
                      <Bar dataKey="sharePct" fill="url(#shoeShareGradient)" radius={[0, 10, 10, 0]} name="Neto udeo u prometu %" />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              ) : (
                <div className="shoetype-decision-empty">Nema podataka za grafikon koncentracije.</div>
              )}
            </article>

            <article className="shoetype-decision-card shoetype-decision-card--chart analytics-surface-panel">
              <h2>
                {marginComparison.mode === "value" ? "Maržni doprinos po tipu obuće" : "Promet vs Maržni doprinos"}
                <InfoTip text={marginComparison.mode === "value"
                  ? "Grafikon prikazuje stvarni maržni doprinos po tipu obuće kada ukupan maržni doprinos nije pozitivan. Udeo u maržnom doprinosu tada nije smislen procenat."
                  : "Grafikon poredi udeo u prometu i udeo u maržnom doprinosu po tipu obuće. Maržni doprinos nije neto profit i ne uključuje operativne troškove. Ako je deo troška procenjen iz raspoloživih podataka, i ovaj signal treba čitati oprezno."}
                />
              </h2>
              {marginComparison.mode === "share" ? (
                <p className="shoetype-decision-chart-desc">Poređenje udela u prometu i udela u maržnom doprinosu - tipovi obuće s visokim prometom ne moraju imati i visok maržni doprinos.</p>
              ) : null}
              {marginComparison.mode === "value" ? (
                <div data-testid="shoe-type-margin-value-chart">
                  <p className="shoetype-decision-message warning">
                    Ukupan maržni doprinos je {fmtRsd(totalMarginContribution)}. Udeo u maržnom doprinosu nije smislen procenat kada je ukupan doprinos nula ili negativan, zato grafikon prikazuje stvarne RSD vrednosti po tipu obuće.
                  </p>
                  <div className="shoetype-decision-chart-wrap">
                    <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={260}>
                      <BarChart data={marginComparison.data} layout="vertical" margin={{ top: 12, right: 16, left: 8, bottom: 8 }}>
                        <CartesianGrid strokeDasharray="2 6" stroke="var(--chart-grid)" />
                        <XAxis type="number" tick={CHART_AXIS_TICK} tickLine={false} axisLine={false} />
                        <YAxis type="category" dataKey="name" width={180} tick={CHART_AXIS_TICK} tickLine={false} axisLine={false} />
                        <Tooltip
                          contentStyle={COMMAND_TOOLTIP_STYLE}
                          labelStyle={COMMAND_TOOLTIP_LABEL_STYLE}
                          cursor={CHART_CURSOR_STYLE}
                          formatter={((value: any) => fmtRsd(Number(value))) as any}
                        />
                        <Legend wrapperStyle={CHART_LEGEND_STYLE} iconType="circle" iconSize={8} />
                        <Bar dataKey="marginContributionRsd" fill="var(--chart-negative)" radius={[0, 6, 6, 0]} name="Maržni doprinos (RSD)" />
                      </BarChart>
                    </ResponsiveContainer>
                  </div>
                </div>
              ) : marginComparison.mode === "share" ? (
                <div className="shoetype-decision-chart-wrap" data-testid="shoe-type-margin-share-chart">
                  <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={260}>
                    <BarChart data={marginComparison.data} layout="vertical" margin={{ top: 12, right: 16, left: 8, bottom: 8 }}>
                      <CartesianGrid strokeDasharray="2 6" stroke="var(--chart-grid)" />
                      <XAxis type="number" tick={CHART_AXIS_TICK} tickLine={false} axisLine={false} unit="%" />
                      <YAxis type="category" dataKey="name" width={180} tick={CHART_AXIS_TICK} tickLine={false} axisLine={false} />
                      <Tooltip
                        contentStyle={COMMAND_TOOLTIP_STYLE}
                        labelStyle={COMMAND_TOOLTIP_LABEL_STYLE}
                        cursor={CHART_CURSOR_STYLE}
                        formatter={((value: any) => value == null ? "Nije dostupno" : fmtPct(Number(value), 1)) as any}
                      />
                      <Legend
                        wrapperStyle={CHART_LEGEND_STYLE}
                        iconType="circle"
                        iconSize={8}
                        itemSorter={(item) => (item.dataKey === "udeoPrometa" ? 0 : 1)}
                      />
                      <Bar dataKey="udeoPrometa" fill="var(--chart-series-1)" radius={[0, 6, 6, 0]} name="Udeo u prometu %" />
                      <Bar dataKey="udeoMarznogDoprinosa" fill="var(--chart-series-3)" radius={[0, 6, 6, 0]} name="Udeo u maržnom doprinosu %" />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              ) : (
                <div className="shoetype-decision-empty">Nema podataka za poređenja.</div>
              )}
            </article>
          </section>

          <section className="shoetype-decision-panels">
            <article className="shoetype-decision-card analytics-surface-panel">
              <div className="shoetype-decision-table-head">
                <div>
                  <h2>Prioritetna lista tipova obuće</h2>
                  <div className="shoetype-priority-chip-row" aria-label="Raspodela preporuka">
                    <span className="priority-chip priority-chip-boost">{recommendationStatusLabel("increase_focus")} <strong>{counts.increaseFocus}</strong></span>
                    <span className="priority-chip priority-chip-keep">{recommendationStatusLabel("maintain")} <strong>{counts.maintain}</strong></span>
                    <span className="priority-chip priority-chip-review">{recommendationStatusLabel("review")} <strong>{counts.review}</strong></span>
                    <span className="priority-chip priority-chip-reduce">{recommendationStatusLabel("do_not_trust")} <strong>{counts.doNotTrust}</strong></span>
                    <span className="priority-chip priority-chip-na">{recommendationStatusLabel("insufficient_data")} <strong>{counts.insufficientData}</strong></span>
                  </div>
                  <p className="shoetype-decision-metric-note">
                    PoP trend = promena prometa prema prethodnom uporedivom periodu. Uticaj nivelacije = pre/post promena unutar prometa sa poznatim prvim datumom nivelacije.
                  </p>
                </div>
              </div>

              <AnalyticsDataTable
                testId="shoe-type-sales-stats-data-table"
                rowCount={sortedRows.length}
                toolbar={(
                  <AnalyticsTableToolbar
                    tableKey="shoe-type-sales-stats"
                    tableTitle="Podrška odluci - tipovi obuće"
                    columns={decisionColumns}
                    rows={sortedRows}
                    filters={toolbarFilters}
                    metadata={toolbarMetadata}
                    defaultOrientation="landscape"
                  />
                )}
              >
                <table className="shoetype-decision-table">
                  <thead>
                    <tr>
                      <th className={isSortActive("tipObuceNaziv", sortField) ? "is-sorted" : undefined}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("tipObuceNaziv", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("tipObuceNaziv", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("tipObuceNaziv", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("tipObuceNaziv")}
                        >
                          Tip obuće <span className="sort-indicator" aria-hidden="true">{sortMarker("tipObuceNaziv", sortField, sortDir)}</span> <InfoTip text="Naziv tipa obuće (npr. patike, sandale)." />
                        </button>
                      </th>
                      <th className={`analytics-data-table__numeric${isSortActive("ukupanPromet", sortField) ? " is-sorted" : ""}`}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("ukupanPromet", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("ukupanPromet", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("ukupanPromet", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("ukupanPromet")}
                        >
                          Promet <span className="sort-indicator" aria-hidden="true">{sortMarker("ukupanPromet", sortField, sortDir)}</span> <InfoTip text="Ukupna vrednost prodaje u izabranom periodu (RSD)." />
                        </button>
                      </th>
                      <th className={`analytics-data-table__numeric${isSortActive("ukupnaKolicina", sortField) ? " is-sorted" : ""}`}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("ukupnaKolicina", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("ukupnaKolicina", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("ukupnaKolicina", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("ukupnaKolicina")}
                        >
                          Količina <span className="sort-indicator" aria-hidden="true">{sortMarker("ukupnaKolicina", sortField, sortDir)}</span> <InfoTip text="Ukupan broj prodatih komada u izabranom periodu." />
                        </button>
                      </th>
                      <th className={`analytics-data-table__numeric${isSortActive("sharePct", sortField) ? " is-sorted" : ""}`}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("sharePct", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("sharePct", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("sharePct", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("sharePct")}
                        >
                          Neto udeo u prometu <span className="sort-indicator" aria-hidden="true">{sortMarker("sharePct", sortField, sortDir)}</span> <InfoTip text="Potpisani neto udeo ovog tipa obuće u ukupnom neto prometu. Povrati mogu dati vrednost ispod 0% ili iznad 100%." />
                        </button>
                      </th>
                      <th className={`analytics-data-table__numeric${isSortActive("marginContribution", sortField) ? " is-sorted" : ""}`}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("marginContribution", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("marginContribution", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("marginContribution", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("marginContribution")}
                        >
                          Maržni doprinos <span className="sort-indicator" aria-hidden="true">{sortMarker("marginContribution", sortField, sortDir)}</span> <InfoTip text="Zbir razlike između prodajne i nabavne vrednosti za stavke ovog tipa sa dostupnim troškom. Operativni troškovi, plate, zakup i ostali indirektni troškovi nisu uključeni." />
                        </button>
                      </th>
                      <th className={`analytics-data-table__numeric${isSortActive("marginPct", sortField) ? " is-sorted" : ""}`}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("marginPct", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("marginPct", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("marginPct", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("marginPct")}
                        >
                          Marža % <span className="sort-indicator" aria-hidden="true">{sortMarker("marginPct", sortField, sortDir)}</span> <InfoTip text={analyticsMetricDescriptions.marginPct} />
                        </button>
                      </th>
                      <th className={`analytics-data-table__numeric${isSortActive("popRevenueChangePct", sortField) ? " is-sorted" : ""}`}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("popRevenueChangePct", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("popRevenueChangePct", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("popRevenueChangePct", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("popRevenueChangePct")}
                        >
                          PoP trend <span className="sort-indicator" aria-hidden="true">{sortMarker("popRevenueChangePct", sortField, sortDir)}</span> <InfoTip text={analyticsMetricDescriptions.popRevenueChangePct} />
                        </button>
                      </th>
                      <th className={`analytics-data-table__numeric${isSortActive("prePostNivelacijaRevenueImpactPct", sortField) ? " is-sorted" : ""}`}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("prePostNivelacijaRevenueImpactPct", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("prePostNivelacijaRevenueImpactPct", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("prePostNivelacijaRevenueImpactPct", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("prePostNivelacijaRevenueImpactPct")}
                        >
                          Uticaj nivelacije / uporedivost <span className="sort-indicator" aria-hidden="true">{sortMarker("prePostNivelacijaRevenueImpactPct", sortField, sortDir)}</span> <InfoTip text={`${analyticsMetricDescriptions.prePostNivelacijaImpactPct} Pre/post pokriće prometa prikazano je uz uticaj i označava uporedivu kohortu.`} />
                        </button>
                      </th>
                      <th className={isSortActive("status", sortField) ? "is-sorted" : undefined}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("status", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("status", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("status", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("status")}
                        >
                          Status signala <span className="sort-indicator" aria-hidden="true">{sortMarker("status", sortField, sortDir)}</span> <InfoTip text={`${analyticsMetricDescriptions.recommendation} Ovaj ekran je podržavajući signal, ne samostalna konačna preporuka.`} />
                        </button>
                      </th>
                      <th className="align-center">Detalj <InfoTip text="Proširi inline detalj ili otvori puni detalj za ovaj tip obuće." /></th>
                    </tr>
                  </thead>
                  <tbody>
                    {sortedRows.length === 0 ? (
                      <tr>
                        <td colSpan={10} className="shoetype-decision-empty-row">
                          Nema podataka za izabrane filtere.
                        </td>
                      </tr>
                    ) : (
                      sortedRows.map((row, index) => {
                        const rowKey = shoeTypeKey(row);
                        const revenueRanked = sortField === "ukupanPromet" && sortDir === "desc";
                        const rank = revenueRanked ? index + 1 : null;
                        const expanded = expandedTypeKey === rowKey;
                        const popMetric = describePopMetric(row);
                        const nivelacijaImpactMetric = describeNivelacijaImpactMetric(row);
                        return (
                          <tr key={rowKey} className={[expanded ? "expanded-row" : "", rank != null && rank <= 3 ? `shoetype-rank-row shoetype-rank-row-${rank}` : ""].filter(Boolean).join(" ")}>
                            <td>
                              <div className="shoetype-name-cell">
                                {rank != null ? <span className={`shoetype-rank-badge ${rank <= 3 ? `rank-${rank}` : "rank-other"}`}>#{rank}</span> : null}
                                <AnalyticsUnknownLink
                                  value={row.tipObuceNaziv}
                                  issueType="missingShoeType"
                                  context={{
                                    originTable: "shoe-type-sales-stats",
                                    fromDate: activeFilters.fromDate,
                                    toDate: activeFilters.toDate,
                                    sezonaId: activeFilters.sezonaId,
                                    storeId: activeFilters.storeId,
                                    dataScope: dataScope,
                                  }}
                                />
                              </div>
                            </td>
                            <td className="analytics-data-table__numeric metric-strong">{fmtRsd(row.ukupanPromet)}</td>
                            <td className="analytics-data-table__numeric">{fmtQty(row.ukupnaKolicina)}</td>
                            <td className="analytics-data-table__numeric"><span className="metric-chip metric-chip-neutral">{fmtPct(row.sharePct, 2)}</span></td>
                            <td className="analytics-data-table__numeric metric-strong">{fmtRsd(row.marginContribution)}</td>
                            <td className="analytics-data-table__numeric">
                              <span>{fmtPct(row.marginPct, 1)}</span>
                              {tierNeedsWarning(row.marginQualityTier) ? (
                                <span className={`quality-pill ${qualityTierClass(row.marginQualityTier)}`} title={row.marginQualityTooltip ?? row.marginQualityLabel ?? ""}>
                                  marža
                                </span>
                              ) : null}
                            </td>
                            <td className="analytics-data-table__numeric" title={popMetric.title}><span className={`metric-chip trend-pill ${popMetric.className}`}>{popMetric.label}</span></td>
                            <td className="analytics-data-table__numeric" title={`${nivelacijaImpactMetric.title} Pre/post pokriće prometa: ${fmtPct(row.splitCoveragePct, 1)}.`}>
                              <span className={`metric-chip trend-pill ${nivelacijaImpactMetric.className}`}>{nivelacijaImpactMetric.label}</span>
                              <small className="shoetype-decision-table-submetric">Pre/post pokriće: {fmtPct(row.splitCoveragePct, 1)}</small>
                            </td>
                            <td>
                              <div className="shoetype-status-stack">
                                <span
                                  className={statusClass(row.status)}
                                  title={buildStatusTooltip(row)}
                                  aria-label={buildStatusTooltip(row)}
                                >
                                  {displayStatusLabel(row.status)}
                                </span>
                                <span className="shoetype-status-reason-chip" title={row.statusReason}>
                                  <strong>{row.recommendationAllowed ? "Razlog" : "Akcija blokirana"}</strong>: {row.statusReason} <InfoTip text={row.statusReason} />
                                </span>
                              </div>
                            </td>
                            <td className="align-center">
                              <button
                                type="button"
                                className="shoetype-decision-detail-btn"
                                onClick={() => setExpandedTypeKey(expanded ? null : rowKey)}
                              >
                                {expanded ? "Sakrij" : "Detalji"}
                              </button>
                            </td>
                          </tr>
                        );
                      })
                    )}
                  </tbody>
                </table>
              </AnalyticsDataTable>
            </article>
          </section>

          {selectedRow ? (
            <section className="shoetype-decision-detail" ref={detailSectionRef}>
              <div className="shoetype-decision-detail-head">
                <h3>Detalj signala: {selectedRow.tipObuceNaziv}</h3>
                <button type="button" onClick={() => openDetail(selectedRow)}>Otvori puni detalj</button>
              </div>

              <h4 className="shoetype-decision-detail-section-title">Poslovni pokazatelji</h4>
              <div className="shoetype-decision-detail-grid">
                <article>
                  <span>Promet <InfoTip text="Ukupna vrednost prodaje ovog tipa obuće u izabranom periodu. Formula: zbir prodajnih vrednosti stavki ovog tipa." /></span>
                  <strong>{fmtRsd(selectedRow.ukupanPromet)}</strong>
                </article>
                <article>
                  <span>Količina <InfoTip text="Ukupan broj prodatih komada ovog tipa obuće." /></span>
                  <strong>{fmtQty(selectedRow.ukupnaKolicina)}</strong>
                </article>
                <article>
                  <span>Nabavna vrednost (rešeni trošak) <InfoTip text={`Zbir troška robe za ovaj red. ${SHOE_TYPE_COST_SOURCE_TOOLTIP} Operativni troškovi nisu uključeni.`} /></span>
                  <strong>{fmtRsd(selectedRow.totalCost)}</strong>
                </article>
                <article>
                  <span>Maržni doprinos <InfoTip text="Zbir razlike između prodajne i nabavne vrednosti za stavke sa dostupnim troškom. Operativni troškovi, plate, zakup i ostali indirektni troškovi nisu uključeni." /></span>
                  <strong>{fmtRsd(selectedRow.marginContribution)}</strong>
                </article>
                <article>
                  <span>Marža % <InfoTip text={analyticsMetricDescriptions.marginPct} /></span>
                  <strong>{fmtSignedPct(selectedRow.marginPct, 2)}</strong>
                </article>
                <article>
                  <span>Neto udeo u prometu <InfoTip text="Potpisani neto udeo ovog tipa u ukupnom prometu. Formula: neto promet tipa / ukupni neto promet svih prikazanih tipova x 100. Povrati mogu dati vrednost ispod 0% ili iznad 100%." /></span>
                  <strong>{fmtPct(selectedRow.sharePct, 2)}</strong>
                </article>
                <article>
                  <span>Udeo u maržnom doprinosu <InfoTip text="Procenat koji ovaj tip obuće čini u ukupnom maržnom doprinosu. Formula: maržni doprinos tipa / ukupan maržni doprinos svih tipova x 100. Ovo nije udeo u profitu niti u neto zaradi." /></span>
                  <strong>{formatShoeTypeMarginContributionShare(selectedRow.marginContribution, totalMarginContribution, fmtPct)}</strong>
                </article>
                <article>
                  <span>Udeo u količini <InfoTip text="Procenat koji ovaj tip obuće čini u ukupno prodatoj količini." /></span>
                  <strong>{(() => {
                    const quantitySharePct = resolveShoeTypeQuantitySharePct(
                      selectedRow.ukupnaKolicina,
                      data?.totals.ukupnaKolicina,
                    );
                    return quantitySharePct == null ? "Nije dostupno" : fmtPct(quantitySharePct, 2);
                  })()}</strong>
                </article>
                <article>
                  <span>Broj artikala <InfoTip text="Ukupan broj različitih artikala ovog tipa obuće koji su prodati." /></span>
                  <strong>{selectedRow.brojArtikalaUkupno}</strong>
                </article>
                <article>
                  <span>Udeo artikala sa nivelacijom <InfoTip text="Udeo različitih artikala ovog tipa koji imaju registrovanu nivelaciju. Nulti denominator znači da procenat nije merljiv." /></span>
                  <strong>{fmtPct(selectedRow.coveragePct, 1)}</strong>
                </article>
              </div>

              <h4 className="shoetype-decision-detail-section-title">Trend u odnosu na prethodni period</h4>
              <div className="shoetype-decision-detail-grid">
                <article>
                  <span>PoP trend prometa <InfoTip text={analyticsMetricDescriptions.popRevenueChangePct} /></span>
                  <strong className={describePopMetric(selectedRow).className} title={describePopMetric(selectedRow).title}>
                    {describePopMetric(selectedRow).label}
                  </strong>
                </article>
                <article>
                  <span>Prethodni period promet <InfoTip text="Ukupan promet ovog tipa obuće u prethodnom periodu (iste dužine kao trenutni)." /></span>
                  <strong>{selectedRow.previousPeriodRevenue != null ? fmtRsd(selectedRow.previousPeriodRevenue) : "Nije dostupno"}</strong>
                </article>
                <article>
                  <span>PoP trend količine <InfoTip text="Procenat promene prodatih komada u odnosu na prethodni uporediv period." /></span>
                  <strong className={trendClass(selectedRow.popUnitsChangePct ?? null)}>
                    {fmtSignedPct(selectedRow.popUnitsChangePct)}
                  </strong>
                </article>
                <article>
                  <span>Prethodni period količina <InfoTip text="Broj prodatih komada ovog tipa u prethodnom periodu." /></span>
                  <strong>{selectedRow.previousPeriodUnits != null ? fmtQty(selectedRow.previousPeriodUnits) : "Nije dostupno"}</strong>
                </article>
              </div>

              <h4 className="shoetype-decision-detail-section-title">Nivelacija</h4>
              <div className="shoetype-decision-detail-grid">
                <article>
                  <span>Uticaj nivelacije na promet <InfoTip text={analyticsMetricDescriptions.prePostNivelacijaImpactPct} /></span>
                  <strong className={describeNivelacijaImpactMetric(selectedRow).className} title={describeNivelacijaImpactMetric(selectedRow).title}>
                    {describeNivelacijaImpactMetric(selectedRow).label}
                  </strong>
                </article>
                <article>
                  <span>Pre/post pokriće prometa <InfoTip text="Procenat prometa koji dolazi od artikala sa prodajom i pre i posle nivelacije." /></span>
                  <strong>{fmtPct(selectedRow.splitCoveragePct, 1)}</strong>
                </article>
                <article>
                  <span>Uporedivi artikli <InfoTip text="Broj artikala sa prodajom i pre i posle nivelacije (koristi se za proračun pre/post uticaja)." /></span>
                  <strong>{selectedRow.prePostComparableArticleCount ?? "Nije dostupno"}</strong>
                </article>
                <article>
                  <span>Pre nivelacije promet <InfoTip text="Zbir vrednosti prodaja pre prvog datuma nivelacije (ažuriranja cene) za ovaj tip." /></span>
                  <strong>{formatCategoryPrePostRevenueMetric(selectedRow.preNivelacijePromet)}</strong>
                </article>
                <article>
                  <span>Posle nivelacije promet <InfoTip text="Zbir vrednosti prodaja od prvog datuma nivelacije (ažuriranja cene) nadalje." /></span>
                  <strong>{formatCategoryPrePostRevenueMetric(selectedRow.posleNivelacijePromet)}</strong>
                </article>
                <article>
                  <span>Uporedivi promet pre nivelacije <InfoTip text="Promet samo artikala koji imaju prodaju i pre i posle prve nivelacije; ova kohorta je izvor ukupnog pre/post uticaja." /></span>
                  <strong>{formatCategoryPrePostRevenueMetric(selectedRow.comparablePreRevenue)}</strong>
                </article>
                <article>
                  <span>Uporedivi promet posle nivelacije <InfoTip text="Promet samo artikala koji imaju prodaju i pre i posle prve nivelacije; ova kohorta je izvor ukupnog pre/post uticaja." /></span>
                  <strong>{formatCategoryPrePostRevenueMetric(selectedRow.comparablePostRevenue)}</strong>
                </article>
                <article>
                        <span>Pre nivo količina <InfoTip text="Ukupan broj prodatih komada pre prvog datuma nivelacije." /></span>
                  <strong>{formatCategoryPrePostQuantityMetric(selectedRow.preNivelacijeKolicina)}</strong>
                </article>
                <article>
                        <span>Posle nivo količina <InfoTip text="Ukupan broj prodatih komada od prvog datuma nivelacije nadalje." /></span>
                  <strong>{formatCategoryPrePostQuantityMetric(selectedRow.posleNivelacijeKolicina)}</strong>
                </article>
                <article>
                  <span>Uporedive količine pre nivelacije <InfoTip text="Količina samo artikala sa prodajom i pre i posle prve nivelacije; koristi se za ukupni pre/post uticaj." /></span>
                  <strong>{formatCategoryPrePostQuantityMetric(selectedRow.comparablePreQuantity)}</strong>
                </article>
                <article>
                  <span>Uporedive količine posle nivelacije <InfoTip text="Količina samo artikala sa prodajom i pre i posle prve nivelacije; koristi se za ukupni pre/post uticaj." /></span>
                  <strong>{formatCategoryPrePostQuantityMetric(selectedRow.comparablePostQuantity)}</strong>
                </article>
                <article>
                        <span>Artikli sa nivelacijom <InfoTip text="Broj artikala sa registrovanom nivelacijom / ukupan broj artikala ovog tipa." /></span>
                  <strong>{selectedRow.brojArtikalaSaNivelacijom} / {selectedRow.brojArtikalaUkupno}</strong>
                </article>
              </div>

              <h4 className="shoetype-decision-detail-section-title">Kvalitet podataka</h4>
              <div className="shoetype-decision-detail-grid">
                <article>
                  <span>Kvalitet marže <InfoTip text="Klasifikacija pouzdanosti obračuna marže na osnovu pokrića nabavne cene: Potvrđena (≥80% istorijski), Delimično (≥50% istorijski), Procenjena (<50% istorijski), Bez troška (0% pokriće)." /></span>
                  <strong>
                    <span className={`shoetype-decision-kpi-badge ${qualityTierClass(selectedRow.marginQualityTier)}`}>
                      {qualityTierIcon(selectedRow.marginQualityTier)} {selectedRow.marginQualityLabel}
                    </span>
                  </strong>
                </article>
                <article>
                  <span>{RECOMMENDATION_RELIABILITY_LABEL} <InfoTip text={analyticsMetricDescriptions.reliabilityPct} /></span>
                  <strong>{selectedRow.reliabilityAvailable ? fmtPct(selectedRow.reliabilityPct, 1) : RECOMMENDATION_SIGNAL_UNAVAILABLE}</strong>
                </article>
                <article>
                  <span>Status kvaliteta preporuke <InfoTip text="Dobro = zeleno i upotrebljivo. Upozorenje = oprez. Kritično = ne veruj bez ručne provere. Nedovoljno podataka = neutralno." /></span>
                  <strong style={recommendationQualityStyle(selectedRow.dataQualityStatus)}>{recommendationQualityLabel(selectedRow.dataQualityStatus)}</strong>
                </article>
                <article>
                  <span>Pokrivenost istorijskim troškom % <InfoTip text={analyticsMetricDescriptions.costCoverage} /></span>
                  <strong>{fmtPct(selectedRow.historicalCostCoveragePct, 1)}</strong>
                </article>
                <article>
                  <span>Promet sa produkt-fallback/procenom % <InfoTip text="Procenat prometa gde nema direktnog istorijskog troška sa prodajne stavke, već se koristi produkt-fallback/procena. Ovo nije istorijski trošak sa trenutka prodaje." /></span>
                  <strong>{fmtPct(selectedRow.estimatedCostCoveragePct ?? selectedRow.fallbackCostCoveragePct, 1)}</strong>
                </article>
                <article>
                        <span>Promet bez nabavne cene % <InfoTip text="Procenat prometa koji nema ni istorijski trošak sa prodajne stavke, ni tačan snapshot trošak, ni produkt-fallback/procenu; zato ne ulazi u obračun maržnog doprinosa ni marže %." /></span>
                  <strong>{fmtPct(selectedRow.noCostCoveragePct, 1)}</strong>
                </article>
                {selectedRow.snapshotCostCoveragePct != null && selectedRow.snapshotCostCoveragePct > 0 ? (
                  <article>
                    <span>Pokrivenost troškom iz snimka % <InfoTip text="Procenat prometa gde je trošak stabilizovan snimkom radi reproduktivnosti izveštaja. Ovo nije istorijska nabavna cena sa trenutka prodaje." /></span>
                    <strong>{fmtPct(selectedRow.snapshotCostCoveragePct, 1)}</strong>
                  </article>
                ) : null}
                <article>
                  <span>{RECOMMENDATION_CONFIDENCE_LABEL} <InfoTip text={analyticsMetricDescriptions.recommendationConfidencePct} /></span>
                  <strong>{selectedRow.confidenceAvailable ? fmtPct(selectedRow.recommendationConfidencePct, 0) : RECOMMENDATION_SIGNAL_UNAVAILABLE}</strong>
                </article>
              </div>

              {selectedRow.prePostSignalNote ? (
                <p className="shoetype-decision-reason">
                  <strong>Napomena za pre/post signal:</strong> {selectedRow.prePostSignalNote}
                </p>
              ) : null}

              {(() => {
                const marginNote = buildMarginDetailNote(
                  selectedRow.marginQualityTier,
                  selectedRow.estimatedCostCoveragePct ?? selectedRow.fallbackCostCoveragePct,
                  selectedRow.historicalCostCoveragePct ?? selectedRow.marginDataCoveragePct,
                  fmtPct,
                  selectedRow.snapshotCostCoveragePct,
                  data.totals.isSnapshotActive
                );
                return marginNote ? (
                  <p className="shoetype-decision-reason">
                    <strong>Napomena za maržu:</strong> {marginNote}
                  </p>
                ) : null;
              })()}

              {(() => {
                const recCaveat = buildRecommendationCaveat(
                  selectedRow.marginQualityTier,
                  selectedRow.estimatedCostCoveragePct ?? selectedRow.fallbackCostCoveragePct,
                  fmtPct
                );
                return recCaveat ? (
                  <p className="shoetype-decision-reason">
                    <strong>Napomena za preporuku:</strong> {recCaveat}
                  </p>
                ) : null;
              })()}

              <p className="shoetype-decision-reason">
                <strong>Razlog preporuke:</strong> {selectedRow.statusReason}
              </p>
              {selectedRow.reasonCodes.length > 0 ? (
                <p className="shoetype-decision-reason">
                  <strong>Razlozi:</strong> {selectedRow.reasonCodes.map(recommendationReasonLabel).join(" | ")}
                </p>
              ) : null}
              {recommendationReasonHints(selectedRow.reasonCodes).map((hint) => (
                <p key={hint} className="shoetype-decision-reason">
                  <strong>Napomena:</strong> {hint}
                </p>
              ))}
              {(!selectedRow.reliabilityAvailable || !selectedRow.confidenceAvailable || selectedRow.dataQualityStatus !== "good") ? (
                <p className="shoetype-decision-reason">
                  <strong>Kvalitet podataka:</strong> Otvori <Link to="/analytics/data-quality">Kvalitet podataka</Link> da proveriš i ispraviš signal.
                </p>
              ) : null}
            </section>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}


import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useLocation, useNavigate, useSearchParams } from "react-router-dom";
import {
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { getStores } from "../services/analyticsApi";
import {
  getColorSalesStats,
  type ColorSalesStat,
  type ColorSalesStatsResponse,
} from "../services/colorSalesStatsApi";
import type { StoreOption } from "../types/analytics";
import AnalyticsControlBar, {
  type AnalyticsControlBarChip,
  type AnalyticsControlBarField,
} from "../components/analytics/AnalyticsControlBar";
import AnalyticsDataTable from "../components/analytics/AnalyticsDataTable";
import AnalyticsEmptyState from "../components/analytics/AnalyticsEmptyState";
import AnalyticsErrorState from "../components/analytics/AnalyticsErrorState";
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import AnalyticsTrustHeader from "../components/analytics/AnalyticsTrustHeader";
import AnalyticsFilterLoadNotice from "../components/analytics/AnalyticsFilterLoadNotice";
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
import { colorIdentityKey } from "../utils/colorIdentity";
import { fmtNumber, fmtPct, fmtQty, fmtRsd, fmtSignedPct, formatDate, getPresetRange } from "../utils/analyticsFormatters";
import { toInclusiveCalendarDate, toUtcDateOnlyExclusive } from "../utils/analyticsDateRanges";
import { resolvePresetFilterRange } from "../utils/analyticsPeriodPresets";
import {
  RECOMMENDATION_SIGNAL_UNAVAILABLE,
  RECOMMENDATION_STATUS_PRIORITY,
  recommendationStatusLabel,
  recommendationStatusTone,
  recommendationStatusTooltipBrief,
  type CanonicalRecommendationStatus,
} from "../utils/canonicalRecommendationSemantics";
import {
  formatCategoryPrePostQuantityMetric,
  formatCategoryPrePostRevenueMetric,
} from "../utils/categoryPrePostDetailMetrics";
import { buildColorRecommendationProjection } from "../utils/colorStatusIdentity";
import {
  resolveColorComplementPercent,
  resolveColorCountValue,
  resolveColorPercentValue,
} from "../utils/colorPercentRange";
import { resolveColorCoveragePct } from "../utils/colorSalesCoverage";
import { CHART_TOOLTIP_STYLE, CHART_TOOLTIP_LABEL_STYLE } from "../utils/chartTooltipStyle";
import { getAnalyticsDataFreshnessStatus } from "../utils/analyticsResponseMeta";
import { getSafeAnalyticsErrorMessage } from "../utils/analyticsErrorMessages";
import { readAnalyticsTableSort, writeAnalyticsTableSort } from "../utils/analyticsTableSortUrl";
import { useReliableAnalyticsQuery } from "../hooks/useReliableAnalyticsQuery";
import { buildStoreOptionLabel, getDuplicateStoreNames } from "../utils/storeFilterPresentation";
import "./ColorSalesStatsPage.css";

type PeriodPreset = "30d" | "90d" | "180d" | "365d" | "custom";
const COLOR_ERROR_FALLBACK = "Greška pri učitavanju podataka po boji.";
const COLOR_SAFE_ERROR_MESSAGES = [
  COLOR_ERROR_FALLBACK,
  "Statistika prodaje po boji artikla trenutno nije dostupna.",
  "Podaci trenutno nisu dostupni.",
] as const;
type SortDir = "asc" | "desc";
type SortField =
  | "boja"
  | "ukupanPromet"
  | "sharePct"
  | "marginContribution"
  | "popRevenueChangePct"
  | "prePostNivelacijaRevenueImpactPct"
  | "status";
const COLOR_SORT_FIELDS: readonly SortField[] = [
  "boja",
  "ukupanPromet",
  "sharePct",
  "marginContribution",
  "popRevenueChangePct",
  "prePostNivelacijaRevenueImpactPct",
  "status",
];
type ActiveFilters = {
  fromDate: string;
  toDate: string;
  sezonaId: number | null;
  storeId: number | null;
};

type DecisionColor = Omit<ColorSalesStat, "reliabilityPct"> & {
  sharePct: number | null;
  marginContribution: number;
  reliabilityPct: number | null;
  recommendationAllowed: boolean;
  coveragePct: number | null;
  splitCoveragePct: number | null;
  decisionScore: number | null;
  status: CanonicalRecommendationStatus;
  statusReason: string;
  reliabilityAvailable: boolean;
};

const decisionColumns: AnalyticsTableColumn<DecisionColor>[] = [
  { key: "boja", header: "Boja", dataType: "text" },
  { key: "ukupanPromet", header: "Promet", dataType: "currency" },
  { key: "sharePct", header: "Udeo %", dataType: "percent" },
  { key: "marginContribution", header: "Maržni doprinos", dataType: "currency" },
  { key: "popRevenueChangePct", header: "PoP trend %", dataType: "percent" },
  { key: "prePostNivelacijaRevenueImpactPct", header: "Uticaj nivelacije %", dataType: "percent" },
  {
    key: "comparablePreRevenue",
    header: "Uporedivo pre nivelacije",
    detailLabel: "Uporedivo pre nivelacije promet",
    dataType: "text",
    getValue: (row) => formatCategoryPrePostRevenueMetric(row.comparablePreRevenue),
  },
  {
    key: "comparablePostRevenue",
    header: "Uporedivo posle nivelacije",
    detailLabel: "Uporedivo posle nivelacije promet",
    dataType: "text",
    getValue: (row) => formatCategoryPrePostRevenueMetric(row.comparablePostRevenue),
  },
  {
    key: "comparablePreQuantity",
    header: "Uporedivo pre nivelacije kom",
    detailLabel: "Uporedivo pre nivelacije količina",
    dataType: "text",
    getValue: (row) => formatCategoryPrePostQuantityMetric(row.comparablePreQuantity),
  },
  {
    key: "comparablePostQuantity",
    header: "Uporedivo posle nivelacije kom",
    detailLabel: "Uporedivo posle nivelacije količina",
    dataType: "text",
    getValue: (row) => formatCategoryPrePostQuantityMetric(row.comparablePostQuantity),
  },
  { key: "prePostComparableArticleCount", header: "Artikli u uporedivoj kohorti", dataType: "number" },
  { key: "status", header: "Preporuka", dataType: "text", getValue: (row) => recommendationStatusLabel(row.status) },
  { key: "decisionScore", header: "Skor odluke (0–100)", dataType: "number" },
];

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

function sortMarker(field: SortField, activeField: SortField, dir: SortDir): string {
  if (field !== activeField) return "";
  return dir === "asc" ? " ^" : " v";
}

function isSortActive(field: SortField, activeField: SortField): boolean {
  return field === activeField;
}

function statusClass(status: CanonicalRecommendationStatus): string {
  const tone = recommendationStatusTone(status);
  if (tone === "boost") return "color-decision-status status-boost";
  if (tone === "keep") return "color-decision-status status-keep";
  if (tone === "review") return "color-decision-status status-review";
  if (tone === "reduce") return "color-decision-status status-reduce";
  return "color-decision-status status-na";
}

function displayStatusLabel(status: CanonicalRecommendationStatus): string {
  return recommendationStatusLabel(status);
}

function trendClass(value: number | null | undefined): string {
  if (value == null || !Number.isFinite(value)) return "trend-neutral";
  if (value > 0) return "trend-up";
  if (value < 0) return "trend-down";
  return "trend-neutral";
}

type StatusTooltipData = {
  status: CanonicalRecommendationStatus;
  statusReason: string;
  sharePct: number | null;
  marginPct: number | null;
  popRevenueChangePct: number | null;
  prePostNivelacijaRevenueImpactPct: number | null;
  previousPeriodRevenue: number | null;
  splitCoveragePct: number | null;
  reliabilityPct: number | null;
  reliabilityAvailable: boolean;
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
  return `${recommendationStatusLabel(data.status)}: ${data.statusReason} | ${recommendationStatusTooltipBrief(data.status)} | Udeo ${fmtPct(data.sharePct, 1)} | Marža ${fmtPct(data.marginPct, 1)} | Trend ${popText} | Uticaj nivelacije ${impactText} | Pokriće podele ${fmtPct(data.splitCoveragePct, 1)} | Pouzdanost ${reliabilityText}`;
}

export function describePopMetric(item: ColorSalesStat): { label: string; title: string; className: string } {
  if (item.popRevenueChangePct != null && Number.isFinite(item.popRevenueChangePct)) {
    return {
      label: fmtSignedPct(item.popRevenueChangePct, 2),
      title: `PoP trend poredi ukupan promet sa prethodnim uporedivim periodom. Prethodni period: ${fmtRsd(item.previousPeriodRevenue, 0, "Nije dostupno")}.`,
      className: trendClass(item.popRevenueChangePct),
    };
  }

  if (item.previousPeriodRevenue != null && item.previousPeriodRevenue <= 0 && item.ukupanPromet > 0) {
    return {
      label: "Novo",
      title: "Boja nije imala promet u prethodnom uporedivom periodu, pa PoP procenat nije smislen.",
      className: "trend-neutral",
    };
  }

  return {
    label: "Nije dostupno",
    title: "PoP trend nije dostupan jer ne postoji validna prethodna baza za poređenje.",
    className: "trend-neutral",
  };
}

export function describeNivelacijaImpactMetric(item: ColorSalesStat): { label: string; title: string; className: string } {
  if (Number.isFinite(item.prePostNivelacijaRevenueImpactPct)) {
    return {
      label: fmtSignedPct(item.prePostNivelacijaRevenueImpactPct, 2),
      title: `Pre/post uticaj meri promenu prometa unutar uporedive kohorte artikala sa prodajom pre i posle prve nivelacije. Pokriće: ${fmtPct(resolveColorPercentValue(item.prePostNivelacijaRevenueCoveragePct), 1)} prometa.`,
      className: trendClass(item.prePostNivelacijaRevenueImpactPct),
    };
  }

  const coverage = resolveColorPercentValue(item.prePostNivelacijaRevenueCoveragePct);
  if (coverage == null) {
    return {
      label: "Nije dostupno",
      title: "Pre/post pokriće nije dostupno jer validno pokriće nije dostupno za ovaj skup podataka.",
      className: "trend-neutral",
    };
  }

  if (coverage === 0) {
    return {
      label: "0% pokriće",
      title: "Pre/post pokriće je izmereno kao 0%; nema artikala sa prodajom i pre i posle nivelacije, pa uticaj nije merljiv.",
      className: "trend-neutral",
    };
  }

  if (item.comparablePreRevenue <= 0 && item.comparablePostRevenue > 0) {
    return {
      label: "Bez baze",
      title: "Postoji uporediv promet posle prve nivelacije, ali nema uporedive pre-nivelacija baze za smislen procenat promene.",
      className: "trend-neutral",
    };
  }

  return {
    label: "Nije dostupno",
    title: "Pre/post uticaj nivelacije nije dostupan za izabrani skup podataka.",
    className: "trend-neutral",
  };
}

function colorKey(item: { boja: string }): string {
  return colorIdentityKey(item.boja);
}

function parseColorDate(value: string | null): string | null {
  if (!value) return null;
  const date = value.slice(0, 10);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) return null;
  const parsed = new Date(`${date}T00:00:00.000Z`);
  return Number.isNaN(parsed.getTime()) || parsed.toISOString().slice(0, 10) !== date ? null : date;
}

function parseColorPositiveInteger(value: string | null): number | null {
  if (!value || !/^\d+$/.test(value)) return null;
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : null;
}

function parseColorPeriodPreset(value: string | null): PeriodPreset | null {
  return value === "30d" || value === "90d" || value === "180d" || value === "365d" || value === "custom"
    ? value
    : null;
}

function resolveColorUrlState(searchParams: URLSearchParams): { preset: PeriodPreset; filters: ActiveFilters } {
  const requestedPreset = parseColorPeriodPreset(searchParams.get("periodPreset"));
  const fromDate = parseColorDate(searchParams.get("fromDate"));
  const toDate = parseColorDate(searchParams.get("toDate"));
  const hasValidRange = fromDate != null && toDate != null && fromDate <= toDate;
  const preset = hasValidRange ? requestedPreset ?? "custom" : requestedPreset && requestedPreset !== "custom" ? requestedPreset : "30d";
  const range = hasValidRange ? { fromDate, toDate } : preset === "custom" ? getPresetRange("30d") : getPresetRange(preset);
  return {
    preset,
    filters: {
      fromDate: range.fromDate,
      toDate: range.toDate,
      sezonaId: parseColorPositiveInteger(searchParams.get("sezonaId")),
      storeId: parseColorPositiveInteger(searchParams.get("storeId")),
    },
  };
}

function writeColorUrlState(current: URLSearchParams, preset: PeriodPreset, filters: ActiveFilters): URLSearchParams {
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

export default function ColorSalesStatsPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams, setSearchParams] = useSearchParams();
  const setSearchParamsRef = useRef(setSearchParams);
  setSearchParamsRef.current = setSearchParams;
  const detailSectionRef = useRef<HTMLElement>(null);
  const queryState = useMemo(() => resolveColorUrlState(searchParams), [searchParams]);

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
  const [sortField, setSortField] = useState<SortField>(() => readAnalyticsTableSort(searchParams, COLOR_SORT_FIELDS, "status", "desc").field);
  const [sortDir, setSortDir] = useState<SortDir>(() => readAnalyticsTableSort(searchParams, COLOR_SORT_FIELDS, "status", "desc").dir);
  const [expandedColorKey, setExpandedColorKey] = useState<string | null>(null);

  useEffect(() => {
    const nextSort = readAnalyticsTableSort(searchParams, COLOR_SORT_FIELDS, "status", "desc");
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
    const canonical = writeColorUrlState(searchParams, queryState.preset, queryState.filters);
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

  const colorQuery = useCallback((signal: AbortSignal) => {
    const currentRange = toUtcRange(activeFilters.fromDate, activeFilters.toDate);
    const { scope: loadedStoreScope, stale: storesAreStale, loadError: storeLoadError } = storesStateRef.current;
    const scopedStoreId = loadedStoreScope === dataScope && !storesAreStale && storeLoadError == null
      ? activeFilters.storeId
      : null;
    return getColorSalesStats({
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
  } = useReliableAnalyticsQuery<ColorSalesStatsResponse>({
    query: colorQuery,
    enabled: storesScope === dataScope || (storesScope == null && storeId == null),
    getErrorMessage: useCallback((reason: unknown) => getSafeAnalyticsErrorMessage(
      reason instanceof Error ? reason.message : null,
      null,
      COLOR_ERROR_FALLBACK,
      COLOR_SAFE_ERROR_MESSAGES,
    ), []),
  });
  const loading = initialLoading || refetching;
  const error = queryError
    ? getSafeAnalyticsErrorMessage(queryError, null, COLOR_ERROR_FALLBACK, COLOR_SAFE_ERROR_MESSAGES)
    : null;

  const decisionRows = useMemo<DecisionColor[]>(() => {
    const rows = data?.colors ?? [];
    if (rows.length === 0) return [];

    return rows.map((item) => {
      const sharePct = resolveColorPercentValue(item.sharePct);
      const marginContribution = item.marginContribution;
      const splitCoveragePct = resolveColorPercentValue(item.prePostNivelacijaRevenueCoveragePct);
      const coveragePct = resolveColorCoveragePct(
        item.brojArtikalaSaNivelacijom,
        item.brojArtikalaUkupno,
      );

      const recommendationProjection = buildColorRecommendationProjection(
        item.recommendation,
        item.reliabilityPct,
      );

      return {
        ...item,
        sharePct,
        marginContribution,
        reliabilityPct: recommendationProjection.reliabilityPct,
        reliabilityAvailable: recommendationProjection.reliabilityAvailable,
        recommendationAllowed: recommendationProjection.recommendationAllowed,
        coveragePct,
        splitCoveragePct,
        decisionScore: recommendationProjection.recommendationAllowed
          ? resolveColorPercentValue(item.decisionScore)
          : null,
        status: recommendationProjection.status,
        statusReason: recommendationProjection.statusReason,
      };
    });
  }, [data?.colors]);

  const sortedRows = useMemo(() => {
    const rows = [...decisionRows];
    return rows.sort((a, b) => {
      let compare = 0;

      if (sortField === "boja") {
        compare = a.boja.localeCompare(b.boja, "sr");
      } else if (sortField === "ukupanPromet") {
        compare = a.ukupanPromet - b.ukupanPromet;
      } else if (sortField === "sharePct") {
        compare = (a.sharePct ?? -1) - (b.sharePct ?? -1);
      } else if (sortField === "marginContribution") {
        compare = a.marginContribution - b.marginContribution;
      } else if (sortField === "popRevenueChangePct") {
        compare = (a.popRevenueChangePct ?? -9999) - (b.popRevenueChangePct ?? -9999);
      } else if (sortField === "prePostNivelacijaRevenueImpactPct") {
        compare = (a.prePostNivelacijaRevenueImpactPct ?? -9999) - (b.prePostNivelacijaRevenueImpactPct ?? -9999);
      } else if (sortField === "status") {
        compare = RECOMMENDATION_STATUS_PRIORITY[a.status] - RECOMMENDATION_STATUS_PRIORITY[b.status];
      }

      if (compare === 0) compare = (a.decisionScore ?? -1) - (b.decisionScore ?? -1);
      if (compare === 0) compare = a.ukupanPromet - b.ukupanPromet;
      return sortDir === "asc" ? compare : -compare;
    });
  }, [decisionRows, sortDir, sortField]);

  const selectedRow = useMemo(
    () => sortedRows.find((row) => colorKey(row) === expandedColorKey) ?? null,
    [expandedColorKey, sortedRows]
  );
  const selectedDecisionScore = selectedRow?.decisionScore ?? null;

  useEffect(() => {
    if (!selectedRow && sortedRows.length > 0 && expandedColorKey != null) {
      setExpandedColorKey(null);
    }
  }, [expandedColorKey, selectedRow, sortedRows.length]);

  useEffect(() => {
    if (!selectedRow || !detailSectionRef.current) return;
    const timeoutId = window.setTimeout(() => {
      detailSectionRef.current?.scrollIntoView({
        behavior: "smooth",
        block: "start",
      });
    }, 100);
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
      .filter((row): row is typeof row & { sharePct: number } => resolveColorPercentValue(row.sharePct) != null)
      .sort((a, b) => b.sharePct - a.sharePct);
    if (ranked.length === 0) return [];
    const topRows = ranked.slice(0, 6).map((row) => ({
      name: row.boja,
      sharePct: Number(row.sharePct.toFixed(2)),
    }));

    const remaining = ranked.slice(6).reduce((sum, row) => sum + row.sharePct, 0);
    const ostaleSharePct = resolveColorPercentValue(Number(remaining.toFixed(2)));
    if (ostaleSharePct != null && ostaleSharePct > 0.1) {
      topRows.push({ name: "Ostale", sharePct: ostaleSharePct });
    }

    return topRows;
  }, [sortedRows]);

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
    const splitCoverage = resolveColorPercentValue(data.dataQuality.revenueWithNivelacijaSplitSharePct);
    const missingCostShare = resolveColorPercentValue(data.dataQuality.missingCostRevenueSharePct);
    const knownCostShare = resolveColorComplementPercent(missingCostShare);
    const unknownShare = resolveColorPercentValue(data.dataQuality.unknownColorRevenueSharePct);
    const costQualityDenominatorStatus = data.dataQuality.costQualityDenominatorStatus;

    if (splitCoverage != null && splitCoverage < 60) {
      notes.push(`Pre/post nivelacija trenutno pokriva ${fmtPct(splitCoverage, 1)} ukupnog prometa, pa taj signal treba čitati kao delimičan.`);
    }

    if (knownCostShare != null && knownCostShare < 100) {
      notes.push(`Marža i maržni doprinos su zasnovani na ${fmtPct(knownCostShare, 1)} prometa sa poznatom nabavnom cenom.`);
    }

    if (unknownShare != null && unknownShare > 0) {
      notes.push(`Nepoznate boje učestvuju sa ${fmtPct(unknownShare, 1)} ukupnog prometa.`);
    }

    if (costQualityDenominatorStatus === "unavailable_non_positive_net_revenue") {
      notes.push("Neto promet nije pozitivan, pa coverage troška i automatska preporuka nisu merljivi; signed iznosi ostaju prikazani.");
    }

    return notes;
  }, [data]);

  const toolbarFilters = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "fromDate", label: "Od", value: activeFilters.fromDate },
      { key: "toDate", label: "Do", value: activeFilters.toDate },
      { key: "sezonaId", label: "Sezona", value: activeSezonaLabel },
      {
        key: "storeId",
        label: "Objekat",
        value: activeFilters.storeId == null
          ? "Svi objekti"
          : stores.find((store) => store.storeId === activeFilters.storeId)
            ? buildStoreOptionLabel(stores.find((store) => store.storeId === activeFilters.storeId)!, duplicateStoreNames)
            : `Nepoznat objekat (ID ${activeFilters.storeId})`,
      },
      { key: "dataScope", label: "Opseg podataka", value: dataScopeLabel(dataScope) },
    ],
    [activeFilters.fromDate, activeFilters.storeId, activeFilters.toDate, activeSezonaLabel, dataScope]
  );

  const toolbarMetadata = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "generatedAt", label: "Generisano", value: data?.generatedAt ?? "" },
      { key: "dataScope", label: "Opseg podataka", value: dataScopeLabel(data?.dataScope === "existing" || data?.dataScope === "imported" ? data.dataScope : dataScope) },
      { key: "sourceLabel", label: "Izvor podataka", value: data?.lineage?.sourceLabel ?? "Nije dostupno" },
      { key: "sourceTables", label: "Izvorne tabele", value: data?.lineage?.sourceTables ?? "Nije dostupno" },
      { key: "observedPopulation", label: "Posmatrana populacija", value: data?.lineage?.observedPopulation ?? "Nije dostupno" },
      { key: "costPolicy", label: "Politika troška", value: data?.lineage?.costPolicy ?? "Nije dostupno" },
      { key: "prePostPolicy", label: "Politika pre/post kohorte", value: data?.lineage?.prePostPolicy ?? "Nije dostupno" },
      { key: "unknownPolicy", label: "Politika nepoznate boje", value: data?.lineage?.unknownPolicy ?? "Nije dostupno" },
      { key: "lineageBasis", label: "Osnova događaja nivelacije", value: data?.lineage ? `${data.lineage.salesArticlesWithMatchingNivelacija}/${data.lineage.salesArticleCount} artikala ima potvrđen događaj u istom opsegu` : "Nije dostupno" },
      { key: "nivelacijaEventCount", label: "Događaji nivelacije", value: data?.lineage?.eventCount ?? null },
      { key: "bojaCount", label: "Broj boja", value: fmtNumber(resolveColorCountValue(data?.totals.brojBoja)) },
      { key: "marginCoverage", label: "Promet sa nabavnom cenom", value: fmtPct(resolveColorComplementPercent(data?.dataQuality.missingCostRevenueSharePct), 1) },
      { key: "splitCoverage", label: "Pre/post pokriće", value: fmtPct(resolveColorPercentValue(data?.dataQuality.revenueWithNivelacijaSplitSharePct), 1) },
      { key: "signedEvidence", label: "Neto dokaz", value: data?.dataQuality.signedRevenuePolicy === "signed_net_revenue_preserved" ? "Neto promet i količina" : "Nije dostupno" },
      { key: "costDenominator", label: "Imenilac pokrića", value: data?.dataQuality.costQualityDenominatorStatus === "measured_positive_net_revenue" ? "Pozitivan neto promet" : "Nije merljivo" },
      { key: "decisionScore", label: "Skor odluke (0–100)", value: fmtPct(data?.totals.decisionScore, 2) },
      { key: "decisionScoreDenominator", label: "Imenilac skora odluke", value: data?.meta?.metricProvenance?.decisionScore?.denominator ?? "Nije dostupno" },
      { key: "decisionScoreActionability", label: "Akcionalnost skora odluke", value: data?.meta?.metricProvenance?.decisionScore?.actionability === "actionable" ? "Dozvoljeno" : data?.meta?.metricProvenance?.decisionScore?.actionability === "blocked" ? "Blokirano" : "Nije dostupno" },
      { key: "metricMarginDenominator", label: "Imenilac marže", value: data?.meta?.metricProvenance?.margin?.denominator ?? "Nije dostupno" },
      { key: "metricRevenueShareDenominator", label: "Imenilac udela prometa", value: data?.meta?.metricProvenance?.revenueShare?.denominator ?? "Nije dostupno" },
      { key: "metricConfidenceDenominator", label: "Imenilac sigurnosti", value: data?.meta?.metricProvenance?.confidence?.denominator ?? "Nije dostupno" },
      { key: "metricReliabilityDenominator", label: "Imenilac pouzdanosti", value: data?.meta?.metricProvenance?.reliability?.denominator ?? "Nije dostupno" },
      { key: "metricCountsDenominator", label: "Osnova brojanja artikala", value: data?.meta?.metricProvenance?.counts?.denominator ?? "Nije dostupno" },
      { key: "weightedMargin", label: "Ponderisana poznata marža", value: fmtPct(data?.dataQuality.weightedKnownMarginPct, 1) },
      { key: "comparableArticleCount", label: "Artikli u uporedivoj kohorti", value: data?.totals.comparableArticleCount ?? null },
      { key: "comparablePreRevenue", label: "Uporediv promet pre nivelacije", value: fmtRsd(data?.totals.comparablePreRevenue) },
      { key: "comparablePostRevenue", label: "Uporediv promet posle nivelacije", value: fmtRsd(data?.totals.comparablePostRevenue) },
      { key: "comparablePreQuantity", label: "Uporediva količina pre nivelacije", value: fmtQty(data?.totals.comparablePreQuantity) },
      { key: "comparablePostQuantity", label: "Uporediva količina posle nivelacije", value: fmtQty(data?.totals.comparablePostQuantity) },
      { key: "comparableRevenueCoveragePct", label: "Pokriće uporedive kohorte", value: fmtPct(data?.totals.comparableRevenueCoveragePct, 1) },
      { key: "prePostSignalNote", label: "Napomena uporedive kohorte", value: data?.totals.prePostSignalNote ?? "Nema napomene" },
      { key: "observedPreRevenue", label: "Posmatrani promet pre nivelacije", value: fmtRsd(data?.totals.observedPreRevenue) },
      { key: "observedPostRevenue", label: "Posmatrani promet posle nivelacije", value: fmtRsd(data?.totals.observedPostRevenue) },
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
      data?.dataQuality.missingCostRevenueSharePct,
      data?.dataQuality.costQualityDenominatorStatus,
      data?.dataQuality.revenueWithNivelacijaSplitSharePct,
      data?.dataQuality.signedRevenuePolicy,
      data?.dataQuality.weightedKnownMarginPct,
      data?.dataScope,
      data?.generatedAt,
      data?.lineage,
      data?.totals.comparableArticleCount,
      data?.totals.comparablePostQuantity,
      data?.totals.comparablePostRevenue,
      data?.totals.comparablePreQuantity,
      data?.totals.comparablePreRevenue,
      data?.totals.comparableRevenueCoveragePct,
      data?.totals.observedPostRevenue,
      data?.totals.observedPreRevenue,
      data?.totals.prePostSignalNote,
      data?.totals.brojBoja,
      dataScope,
    ]
  );

  const headerDataQualityStatus = useMemo(() => {
    if (!data) return null;
    if (sortedRows.length === 0) return "insufficient_data";
    const missingCostShare = resolveColorPercentValue(data.dataQuality.missingCostRevenueSharePct);
    const splitCoverage = resolveColorPercentValue(data.dataQuality.revenueWithNivelacijaSplitSharePct);
    if (missingCostShare == null || splitCoverage == null) {
      return "insufficient_data";
    }
    return qualityNotes.length > 0 ? "warning" : "good";
  }, [data, qualityNotes.length, sortedRows.length]);

  const responseMeta = data?.meta ?? null;
  const trustDataQualityStatus = responseMeta?.dataQualityStatus ?? headerDataQualityStatus;
  const trustLastRefreshAt = responseMeta?.lastRefreshAtUtc ?? null;
  const trustIsPartial = responseMeta?.isPartial ?? false;
  const trustDataFreshnessStatus = getAnalyticsDataFreshnessStatus(responseMeta);
  const trustEmptyStateReason = responseMeta?.message ?? emptyStateHint;
  const lineageBasis = useMemo(() => {
    if (!data?.lineage) return null;
    const scopeLabel = data.lineage.dataScope === "imported"
      ? "uvezeni podaci"
      : data.lineage.dataScope === "existing"
        ? "postojeći podaci"
        : "svi izvori podataka";
    const storeLabel = data.lineage.storeId == null
      ? "svi objekti"
      : stores.find((store) => store.storeId === data.lineage?.storeId)
        ? buildStoreOptionLabel(stores.find((store) => store.storeId === data.lineage?.storeId)!, duplicateStoreNames)
        : `nepoznat objekat (ID ${data.lineage.storeId})`;
    const matchedLabel = `${data.lineage.salesArticlesWithMatchingNivelacija}/${data.lineage.salesArticleCount} artikala sa potvrđenim događajem nivelacije`;
    const storePolicyLabel = data.lineage.storeId == null
      ? "događaji sa svih objekata"
      : "samo tačan objekat; događaji bez objekta su izuzeti";
    return `${scopeLabel}; ${storeLabel}; ${matchedLabel}; ${storePolicyLabel}`;
  }, [data?.lineage, stores]);
  const showBlockingError = Boolean(queryError && !data);
  const showStaleError = Boolean(staleWarning && data);

  const emptyStateVariant = useMemo<"no_data" | "insufficient_data" | "filtered_out" | null>(() => {
    if (!data || loading || sortedRows.length > 0) return null;
    if (data.meta?.emptyReason === "filtered_out") return "filtered_out";
    return "no_data";
  }, [data, loading, sortedRows.length]);

  const controlBarChips = useMemo<AnalyticsControlBarChip[]>(
    () => [
      {
        key: "scope",
        label: "Opseg",
        value: dataScopeLabel(dataScope),
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
        value: `${sortedRows.length.toLocaleString("sr-RS")} / ${(data?.colors?.length ?? 0).toLocaleString("sr-RS")}`,
        tone: sortedRows.length === 0 ? "warning" : "success",
      },
    ],
    [activeFilters.fromDate, activeFilters.toDate, data?.colors?.length, dataScope, sortedRows.length],
  );

  const openDetail = useCallback((row: DecisionColor) => {
    const recordId = encodeURIComponent(colorIdentityKey(row.boja));

    const params = new URLSearchParams();
    params.set("fromDate", `${activeFilters.fromDate}T00:00:00Z`);
    params.set("toDate", toUtcDateOnlyExclusive(activeFilters.toDate));
    if (activeFilters.sezonaId != null) params.set("sezonaId", String(activeFilters.sezonaId));
    if (activeFilters.storeId != null) params.set("storeId", String(activeFilters.storeId));
    params.set("dataScope", dataScope);

    saveAnalyticsDetailSnapshot(
      buildAnalyticsDetailSnapshot({
        table: "color-sales-stats",
        recordId,
        title: row.boja,
        subtitle: "Detalj odluke po boji",
        columns: decisionColumns,
        row,
        metadata: [...toolbarFilters, ...toolbarMetadata],
      })
    );

    navigate(`/analitika/color-sales-stats/${recordId}?${params.toString()}`, {
      state: { backgroundLocation: location },
    });
  }, [activeFilters.fromDate, activeFilters.sezonaId, activeFilters.storeId, activeFilters.toDate, dataScope, location, navigate, toolbarFilters, toolbarMetadata]);

  function applyPreset(preset: PeriodPreset) {
    setPeriodPreset(preset);
    if (preset === "custom") return;
    const range = getPresetRange(preset);
    setSezonaId(null);
    setFromDate(range.fromDate);
    setToDate(range.toDate);
  }

  function handleSeasonChange(value: string) {
    const parsed = value ? Number(value) : null;
    setSezonaId(parsed);
    setPeriodPreset("custom");

    if (parsed == null) return;

    const selected = data?.sezone.find((item) => item.id === parsed);
    if (!selected) return;
    setFromDate(toDateOnly(selected.datumOd));
    setToDate(toDateOnly(selected.datumDo));
  }

  const applyFilters = () => {
    if (invalidRange) {
      return;
    }

    const range = resolvePresetFilterRange(periodPreset, fromDate, toDate);
    setFromDate(range.fromDate);
    setToDate(range.toDate);
    const nextFilters = {
      fromDate: range.fromDate,
      toDate: range.toDate,
      sezonaId,
      storeId,
    };
    setActiveFilters(nextFilters);
    setSearchParams((current) => writeColorUrlState(current, periodPreset, nextFilters), { replace: true });
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
    setSearchParams((current) => writeColorUrlState(current, "30d", nextFilters), { replace: true });
  };

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
              setPeriodPreset("custom");
              setSezonaId(null);
              setFromDate(event.target.value);
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
              setPeriodPreset("custom");
              setSezonaId(null);
              setToDate(event.target.value);
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
            value={storesScope === dataScope && !storesStale ? storeId ?? "" : ""}
            disabled={storesLoadError != null || storesStale || storesScope !== dataScope}
            onChange={(event) => setStoreId(event.target.value ? Number(event.target.value) : null)}
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
    [data?.sezone, dataScope, fromDate, handleSeasonChange, periodPreset, sezonaId, storeId, stores, storesLoadError, storesScope, storesStale, toDate],
  );

  const handleSort = (field: SortField) => {
    const nextDir: SortDir = sortField === field
      ? (sortDir === "asc" ? "desc" : "asc")
      : (field === "boja" ? "asc" : "desc");
    setSortField(field);
    setSortDir(nextDir);
    setSearchParams((current) => writeAnalyticsTableSort(current, field, nextDir), { replace: true });
  };

  return (
    <div className="color-decision-page">
      <AnalyticsTrustHeader
        title="Prodaja po boji artikla"
        description="Podrška za odluku o bojama koje treba pojačati u nabavci."
        periodFrom={data?.fromDate ?? activeFilters.fromDate}
        periodTo={toInclusiveCalendarDate(data?.toDate) ?? activeFilters.toDate}
        requestedPeriodFrom={responseMeta?.requestedPeriodFromUtc}
        requestedPeriodTo={toInclusiveCalendarDate(responseMeta?.requestedPeriodToUtc) ?? activeFilters.toDate}
        effectivePeriodFrom={responseMeta?.effectivePeriodFromUtc ?? data?.fromDate}
        effectivePeriodTo={toInclusiveCalendarDate(responseMeta?.effectivePeriodToUtc ?? data?.toDate) ?? activeFilters.toDate}
        observedPeriodFrom={responseMeta?.observedPeriodFromUtc}
        observedPeriodTo={responseMeta?.observedPeriodToUtc}
        lastRefreshAt={trustLastRefreshAt}
        dataFreshnessStatus={trustDataFreshnessStatus}
        dataSource={data?.lineage?.sourceLabel ?? `Prodaja po boji artikla (opseg: ${dataScopeLabel(data?.dataScope === "existing" || data?.dataScope === "imported" ? data.dataScope : dataScope)})`}
        provenanceBasis={data?.lineage?.observedPopulation && data?.lineage?.prePostPolicy
          ? `${data.lineage.observedPopulation}; ${data.lineage.prePostPolicy}`
          : lineageBasis}
        dataQualityStatus={trustDataQualityStatus}
        mode="recommendation"
        isPartial={trustIsPartial}
        recommendationNote="Preporuke dolaze iz backenda; ovaj ekran zadržava odluku, period i kvalitet podataka na jednom mestu."
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
        description="Period, sezona i objekat ostaju ovde; prioritetna lista ispod ostaje fokusirana na boje."
        chips={controlBarChips}
        primaryAction={{
          key: "apply",
          label: loading ? "Učitavanje..." : "Primeni filtere",
          onClick: applyFilters,
          disabled: loading,
        }}
        secondaryActions={[
          {
            key: "reset",
            label: "Reset filtera",
            onClick: resetFilters,
            disabled: loading,
            tone: "secondary",
          },
          {
            key: "data-quality",
            label: "Kvalitet podataka",
            to: "/analytics/data-quality",
            tone: "secondary",
          },
        ]}
        fields={controlBarFields}
      />

      {invalidRange ? (
        <div className="color-decision-message error">Datum „Od” ne može biti posle datuma „Do”.</div>
      ) : null}
      {showBlockingError ? (
        <AnalyticsErrorState
          title="Boje trenutno nisu dostupne"
          message={error || "Ne prikazujemo nule jer nije potvrđeno da je period stvarno prazan."}
          onRetry={refetch}
          helpHref="/analytics/data-quality"
        />
      ) : null}
      {showStaleError ? (
        <div
          className="color-decision-message info"
          role="status"
          aria-live="polite"
          data-testid="color-stale-refetch-warning"
        >
          Prikazujemo prethodno učitane podatke. Novi upit nije uspeo.
        </div>
      ) : null}
      {loading ? (
        <div className="color-decision-message loading">
          <UltraSpinner size="sm" label="Učitavam boje" className="color-decision-loading-spinner" />
          <span>Učitavam boje...</span>
        </div>
      ) : null}
      {!loading && !showBlockingError && emptyStateVariant ? (
        <AnalyticsEmptyState
          variant={emptyStateVariant ?? undefined}
          message={emptyStateHint ?? undefined}
          emptyReason={responseMeta?.emptyReason ?? null}
          dataQualityHref="/analytics/data-quality"
          refreshStatusHref="/admin/configuration?panel=workers"
          onRetry={refetch}
        />
      ) : null}
      {!loading && !showBlockingError && qualityNotes.length > 0 ? (
        <div className="color-decision-message info">
          <strong>Kvalitet podataka:</strong> {qualityNotes.join(" ")}
        </div>
      ) : null}

      {!loading && data ? (
        <>
          {!emptyStateHint ? (
            <section className="color-decision-kpis">
              <article className="color-decision-kpi">
                <span>Ukupan promet</span>
                <strong>{fmtRsd(totalRevenue)}</strong>
              </article>
              <article className="color-decision-kpi">
                  <span>Ukupan maržni doprinos</span>
                <strong>{fmtRsd(totalMarginContribution)}</strong>
              </article>
              <article className="color-decision-kpi">
                <span>Rast/PAD vs prethodni period</span>
                <strong className={trendClass(periodGrowthPct)}>{fmtSignedPct(periodGrowthPct)}</strong>
              </article>
              <article className="color-decision-kpi">
                <span>Uporediva kohorta pre/post</span>
                <strong>{fmtRsd(data.totals.comparablePreRevenue)} → {fmtRsd(data.totals.comparablePostRevenue)}</strong>
                <small>{fmtNumber(data.totals.comparableArticleCount)} artikala · {fmtPct(data.totals.comparableRevenueCoveragePct, 1)} prometa</small>
              </article>
            </section>
          ) : null}

          <section className="color-decision-panels">
            <article className="color-decision-card">
              <h2>Koncentracija prometa po bojama</h2>
              <p>Top boje koje nose najveći deo prodaje.</p>
              {concentrationData.length > 0 ? (
                <div className="color-decision-chart-wrap">
                  <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={260}>
                    <BarChart data={concentrationData} layout="vertical" margin={{ top: 12, right: 16, left: 8, bottom: 8 }}>
                      <CartesianGrid strokeDasharray="3 3" stroke="var(--border-default)" />
                      <XAxis type="number" tick={{ fill: "var(--text-secondary)", fontSize: 12 }} unit="%" />
                      <YAxis type="category" dataKey="name" width={180} tick={{ fill: "var(--text-primary)", fontSize: 12 }} />
                      <Tooltip contentStyle={CHART_TOOLTIP_STYLE} labelStyle={CHART_TOOLTIP_LABEL_STYLE} formatter={(value: number | string | undefined) => value == null ? "Nije dostupno" : fmtPct(Number(value), 2)} />
                      <Bar dataKey="sharePct" fill="var(--accent-primary)" radius={[0, 8, 8, 0]} />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              ) : (
                <div className="color-decision-empty">Nema podataka za grafikon koncentracije.</div>
              )}
            </article>

            <article className="color-decision-card">
              <div className="color-decision-table-head">
                <div>
                  <h2>Prioritetna lista boja</h2>
                  <p>
                    {recommendationStatusLabel("increase_focus")}: {counts.increaseFocus} | {recommendationStatusLabel("maintain")}: {counts.maintain} | {recommendationStatusLabel("review")}: {counts.review} | {recommendationStatusLabel("do_not_trust")}: {counts.doNotTrust} | {recommendationStatusLabel("insufficient_data")}: {counts.insufficientData}
                  </p>
                  <p className="color-decision-metric-note">
                    Trend = promena prometa prema prethodnom uporedivom periodu. Uticaj nivelacije = pre/post promena unutar prometa sa poznatim prvim datumom nivelacije.
                  </p>
                </div>
              </div>

              <AnalyticsDataTable
                rowCount={sortedRows.length}
                toolbar={(
                  <AnalyticsTableToolbar
                    tableKey="color-sales-stats"
                    tableTitle="Podrška odluci - boje artikala"
                    columns={decisionColumns}
                    rows={sortedRows}
                    filters={toolbarFilters}
                    metadata={toolbarMetadata}
                    defaultOrientation="landscape"
                  />
                )}
              >
                <table className="color-decision-table">
                  <thead>
                    <tr>
                      <th>
                        <button type="button" onClick={() => handleSort("boja")}>
                          Boja{sortMarker("boja", sortField, sortDir)} <InfoTip text="Naziv boje artikla." />
                        </button>
                      </th>
                      <th className={`analytics-data-table__numeric${isSortActive("ukupanPromet", sortField) ? " is-sorted" : ""}`}>
                        <button type="button" onClick={() => handleSort("ukupanPromet")}>
                          Promet{sortMarker("ukupanPromet", sortField, sortDir)} <InfoTip text="Ukupna vrednost prodaje u izabranom periodu (RSD)." />
                        </button>
                      </th>
                      <th className={`analytics-data-table__numeric${isSortActive("sharePct", sortField) ? " is-sorted" : ""}`}>
                        <button type="button" onClick={() => handleSort("sharePct")}>
                          Udeo %{sortMarker("sharePct", sortField, sortDir)} <InfoTip text="Udeo u ukupnom prometu (procenat)." />
                        </button>
                      </th>
                      <th className={`analytics-data-table__numeric${isSortActive("marginContribution", sortField) ? " is-sorted" : ""}`}>
                        <button type="button" onClick={() => handleSort("marginContribution")}>
                          Maržni doprinos{sortMarker("marginContribution", sortField, sortDir)} <InfoTip text="Doprinos marže: razlika između prodajne vrednosti i nabavne vrednosti za prodatu robu." />
                        </button>
                      </th>
                      <th className={`analytics-data-table__numeric${isSortActive("popRevenueChangePct", sortField) ? " is-sorted" : ""}`}>
                        <button type="button" onClick={() => handleSort("popRevenueChangePct")}>
                          PoP trend{sortMarker("popRevenueChangePct", sortField, sortDir)} <InfoTip text="Promena ukupnog prometa u odnosu na prethodni uporedivi period. N/A ako prethodni period nije dostupan; Novo ako je prethodni promet bio 0." />
                        </button>
                      </th>
                      <th className={`analytics-data-table__numeric${isSortActive("prePostNivelacijaRevenueImpactPct", sortField) ? " is-sorted" : ""}`}>
                        <button type="button" onClick={() => handleSort("prePostNivelacijaRevenueImpactPct")}>
                          Uticaj nivelacije{sortMarker("prePostNivelacijaRevenueImpactPct", sortField, sortDir)} <InfoTip text="Pre/post promena prometa unutar uporedive kohorte artikala sa prodajom i pre i posle nivelacije. Nije isto što i trend prema prethodnom periodu." />
                        </button>
                      </th>
                      <th>
                        <button type="button" onClick={() => handleSort("status")}>
                          Preporuka{sortMarker("status", sortField, sortDir)} <InfoTip text="Sistemska preporuka: Pojacaj / Zadrzi / Pregledaj / Ne veruj / Nedovoljno podataka. Status i izvrsivost akcije su odvojeni signali." />
                        </button>
                      </th>
                      <th className="align-center">Detalj</th>
                    </tr>
                  </thead>
                  <tbody>
                    {sortedRows.length === 0 ? (
                      <tr>
                        <td colSpan={8} className="color-decision-empty-row">
                          Nema podataka za izabrane filtere.
                        </td>
                      </tr>
                    ) : (
                      sortedRows.map((row) => {
                        const rowKey = colorKey(row);
                        const expanded = expandedColorKey === rowKey;
                        const popMetric = describePopMetric(row);
                        const nivelacijaImpactMetric = describeNivelacijaImpactMetric(row);
                        return (
                          <tr key={rowKey} className={expanded ? "expanded-row" : ""}>
                            <td>{row.boja}</td>
                            <td className="analytics-data-table__numeric">{fmtRsd(row.ukupanPromet)}</td>
                            <td className="analytics-data-table__numeric">{fmtPct(row.sharePct, 2)}</td>
                            <td className="analytics-data-table__numeric">{fmtRsd(row.marginContribution)}</td>
                            <td className={["analytics-data-table__numeric", popMetric.className].join(" ")} title={popMetric.title}>{popMetric.label}</td>
                            <td className={["analytics-data-table__numeric", nivelacijaImpactMetric.className].join(" ")} title={nivelacijaImpactMetric.title}>{nivelacijaImpactMetric.label}</td>
                            <td>
                              <div className="color-status-stack">
                                <span
                                  className={statusClass(row.status)}
                                  title={buildStatusTooltip(row)}
                                  aria-label={buildStatusTooltip(row)}
                                >
                                  {displayStatusLabel(row.status)}
                                </span>
                                <span className="color-status-reason-chip" title={row.statusReason}>
                                  <strong>{row.recommendationAllowed ? "Razlog" : "Akcija blokirana"}</strong>: {row.statusReason} <InfoTip text={row.statusReason} />
                                </span>
                              </div>
                            </td>
                            <td className="align-center">
                              <button
                                type="button"
                                className="color-decision-detail-btn"
                                onClick={() => setExpandedColorKey(expanded ? null : rowKey)}
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
            <section className="color-decision-detail" ref={detailSectionRef}>
              <div className="color-decision-detail-head">
                <h3>Detalj odluke: {selectedRow.boja}</h3>
                <button type="button" onClick={() => openDetail(selectedRow)}>Otvori puni detalj</button>
              </div>

              <div className="color-decision-detail-grid">
                <article>
                  <span>PoP trend prometa</span>
                  <strong className={describePopMetric(selectedRow).className} title={describePopMetric(selectedRow).title}>
                    {describePopMetric(selectedRow).label}
                  </strong>
                </article>
                <article>
                  <span>Prethodni period promet</span>
                  <strong>{selectedRow.previousPeriodRevenue != null ? fmtRsd(selectedRow.previousPeriodRevenue) : "Nije dostupno"}</strong>
                </article>
                <article>
                  <span>Uticaj nivelacije na promet</span>
                  <strong className={describeNivelacijaImpactMetric(selectedRow).className} title={describeNivelacijaImpactMetric(selectedRow).title}>
                    {describeNivelacijaImpactMetric(selectedRow).label}
                  </strong>
                </article>
                <article>
                  <span>Pre/post pokriće uporedive kohorte</span>
                  <strong>{fmtPct(resolveColorPercentValue(selectedRow.prePostNivelacijaRevenueCoveragePct), 1)}</strong>
                </article>
                <article>
                  <span>Uporedivo pre nivelacije promet</span>
                  <strong>{formatCategoryPrePostRevenueMetric(selectedRow.comparablePreRevenue)}</strong>
                </article>
                <article>
                  <span>Uporedivo posle nivelacije promet</span>
                  <strong>{formatCategoryPrePostRevenueMetric(selectedRow.comparablePostRevenue)}</strong>
                </article>
                <article>
                  <span>Uporedivo pre nivelacije količina</span>
                  <strong>{formatCategoryPrePostQuantityMetric(selectedRow.comparablePreQuantity)}</strong>
                </article>
                <article>
                  <span>Uporedivo posle nivelacije količina</span>
                  <strong>{formatCategoryPrePostQuantityMetric(selectedRow.comparablePostQuantity)}</strong>
                </article>
                <article>
                  <span>Artikli u uporedivoj kohorti</span>
                  <strong>{fmtNumber(selectedRow.prePostComparableArticleCount)}</strong>
                </article>
                <article>
                  <span>Posmatrani pre/posle promet</span>
                  <strong>{formatCategoryPrePostRevenueMetric(selectedRow.preNivelacijePromet)} / {formatCategoryPrePostRevenueMetric(selectedRow.posleNivelacijePromet)}</strong>
                </article>
                <article>
                  <span>Artikli sa nivelacijom</span>
                  <strong>{selectedRow.brojArtikalaSaNivelacijom} / {selectedRow.brojArtikalaUkupno}</strong>
                </article>
                <article>
                  <span>Pouzdanost podataka</span>
                  <strong>{selectedRow.reliabilityAvailable ? fmtPct(selectedRow.reliabilityPct, 1) : RECOMMENDATION_SIGNAL_UNAVAILABLE}</strong>
                </article>
                <article>
                  <span>Pokriće marže</span>
                  <strong>{fmtPct(resolveColorPercentValue(selectedRow.marginDataCoveragePct), 1)}</strong>
                </article>
                <article>
                  <span>Marza %</span>
                  <strong>{fmtSignedPct(selectedRow.marginPct, 2)}</strong>
                </article>
                <article>
                  <span>Skor odluke (0–100)</span>
                  <strong>{selectedDecisionScore == null ? "Nije dostupno" : fmtNumber(selectedDecisionScore, 0)}</strong>
                </article>
              </div>

              <p className="color-decision-reason">
                <strong>Razlog preporuke:</strong> {selectedRow.statusReason}
              </p>
            </section>
          ) : null}
        </>
      ) : null}
    </div>
  );
}

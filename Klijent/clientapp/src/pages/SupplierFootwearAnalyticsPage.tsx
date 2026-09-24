import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useLocation, useNavigate, useSearchParams } from "react-router-dom";
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import AnalyticsControlBar, { type AnalyticsControlBarChip, type AnalyticsControlBarField } from "../components/analytics/AnalyticsControlBar";
import AnalyticsDataTable from "../components/analytics/AnalyticsDataTable";
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import AnalyticsTrustHeader from "../components/analytics/AnalyticsTrustHeader";
import { getDobavljaci } from "../services/dobavljaciApi";
import { buildAnalyticsDetailSnapshot, saveAnalyticsDetailSnapshot } from "../services/analyticsTableState";
import {
  getVendorSalesNivelacija,
  getVendorSalesNivelacijaOptions,
  type VendorSalesNivelacijaOption,
  type VendorSalesNivelacijaRecommendation,
  type VendorSalesNivelacijaResponse,
  type VendorSalesNivelacijaVendorStat,
} from "../services/vendorSalesNivelacijaApi";
import type { Dobavljac } from "../types/Dobavljaci";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import type { AnalyticsFreshnessStatus } from "../types/analytics";
import { fmtPct, fmtQty, fmtRsd, fmtSignedPct, getPresetRange } from "../utils/analyticsFormatters";
import { formatMetricDisplayValue, normalizeMetricNumber } from "../utils/analyticsMetricValue";
import { getSafeAnalyticsErrorMessage } from "../utils/analyticsErrorMessages";
import { getAnalyticsMetaMessage, isAnalyticsMetaInsufficient, isAnalyticsMetaWarning, shouldShowAnalyticsEmptyState } from "../utils/analyticsResponseMeta";
import { comparablePrePostMetric, hasComparablePrePostEvidence } from "../utils/prePostNivelacijaTrust";
import {
  resolvePreviousPeriodComparison,
  resolveSupplierPeriodGrowthPct,
  type PreviousPeriodComparisonState,
  SUPPLIER_PREVIOUS_PERIOD_FAILURE_NOTE,
} from "../utils/supplierPreviousPeriodComparison";
import { projectVendorSalesDataQuality } from "../utils/vendorSalesDataQuality";
import {
  buildSupplierVendorDetailRecordId,
  buildSupplierVendorKeys,
} from "../utils/supplierVendorIdentity";
import { getDataScope, normalizeDataScope, type DataScope } from "../utils/dataScope";
import type { SupplierEmbeddedPageProps } from "./supplierSharedState";
import "./SupplierFootwearAnalyticsPage.css";

type PeriodPreset = "30d" | "90d" | "180d" | "365d" | "custom";
type SortDir = "asc" | "desc";
type SortField = "vendorName" | "postRevenue" | "sharePct" | "topFootwearType" | "trendPct" | "status";
type DecisionStatus = VendorSalesNivelacijaRecommendation["status"];

type ActiveFilters = { fromDate: string; toDate: string; vendorId: number | null; category: string; storeId: number | null; dataScope: DataScope };
type SuggestedRange = { fromDate: string; toDate: string; label: string };
type DataQualityStatus = "good" | "warning" | "critical" | "insufficient_data" | null;

type DecisionVendor = VendorSalesNivelacijaVendorStat & {
  vendorRowKey: string;
  sharePct: number | null;
  trendPct: number | null;
  topFootwearType: string;
  topFootwearTypeSharePct: number | null;
  avgElasticity: number | null;
  confidencePct: number | null;
  recommendationAllowed: boolean;
  status: DecisionStatus;
  statusReason: string;
};

const STATUS_PRIORITY: Record<DecisionStatus, number> = {
  increase_focus: 5,
  maintain: 4,
  review: 3,
  insufficient_data: 2,
  do_not_trust: 1,
};

function comparableMetric(value: number | null | undefined, comparable: boolean): number | null {
  if (!comparable) return null;
  return normalizeMetricNumber(value);
}

function rowHasComparableEvidence(row: VendorSalesNivelacijaVendorStat): boolean {
  return hasComparablePrePostEvidence(row) && comparablePrePostMetric(row.postRevenue, row) != null;
}

export const decisionColumns: AnalyticsTableColumn<DecisionVendor>[] = [
  { key: "vendorName", header: "Dobavljač", dataType: "text" },
  { key: "postRevenue", header: "Promet", dataType: "currency", getValue: (row) => comparableMetric(row.postRevenue, rowHasComparableEvidence(row)) },
  { key: "sharePct", header: "Udeo %", dataType: "percent" },
  { key: "topFootwearType", header: "Glavni tip", dataType: "text" },
  { key: "topFootwearTypeSharePct", header: "Udeo tipa %", dataType: "percent" },
  { key: "trendPct", header: "Trend %", dataType: "percent" },
  { key: "status", header: "Preporuka", dataType: "text" },
  { key: "confidencePct", header: "Poverenje %", dataType: "percent", getValue: (row) => row.recommendationAllowed ? row.confidencePct : null },
];

function toUtcRange(fromDate: string, toDate: string) { return { from: `${fromDate}T00:00:00Z`, to: `${toDate}T23:59:59Z` }; }
function toDateOnly(value: string): string {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return value.slice(0, 10);
  return parsed.toISOString().slice(0, 10);
}
function buildPreviousRange(fromDate: string, toDate: string) {
  const currentFrom = new Date(`${fromDate}T00:00:00Z`);
  const currentTo = new Date(`${toDate}T23:59:59Z`);
  const durationMs = currentTo.getTime() - currentFrom.getTime() + 1000;
  const previousTo = new Date(currentFrom.getTime() - 1000);
  const previousFrom = new Date(previousTo.getTime() - durationMs + 1000);
  return { from: previousFrom.toISOString(), to: previousTo.toISOString() };
}
function fmtElasticity(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return "N/A";
  return value.toLocaleString("sr-RS", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}
function sortMarker(field: SortField, activeField: SortField, dir: SortDir): string { if (field !== activeField) return ""; return dir === "asc" ? " ^" : " v"; }
function statusClass(status: DecisionStatus): string {
  if (status === "increase_focus") return "sf-decision-status status-boost";
  if (status === "review" || status === "insufficient_data") return "sf-decision-status status-review";
  if (status === "do_not_trust") return "sf-decision-status status-reduce";
  return "sf-decision-status status-keep";
}
function statusDisplayLabel(status: DecisionStatus): string {
  if (status === "increase_focus") return "Pojačaj fokus";
  if (status === "maintain") return "Zadrži";
  if (status === "review") return "Proveri";
  if (status === "do_not_trust") return "Ne veruj";
  return "Nedovoljno podataka";
}
function trendClass(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return "trend-neutral";
  if (value > 0) return "trend-up";
  if (value < 0) return "trend-down";
  return "trend-neutral";
}

type StatusTooltipData = {
  status: DecisionStatus;
  statusReason: string;
  sharePct: number | null;
  trendPct: number | null;
  topFootwearType: string;
  topFootwearTypeSharePct: number | null;
  reliabilityPct: number | null;
  confidencePct: number | null;
  recommendationAllowed: boolean;
};

function buildStatusTooltip(data: StatusTooltipData): string {
  const trust = data.recommendationAllowed
    ? ` | Pouzdanost ${formatMetricDisplayValue({ value: data.reliabilityPct, kind: "percent", digits: 0 })} | Poverenje ${formatMetricDisplayValue({ value: data.confidencePct, kind: "percent", digits: 0 })}`
    : " | Pouzdanost Nije dostupno | Poverenje Nije dostupno";
  return `${statusDisplayLabel(data.status)}: ${data.statusReason} | Udeo ${formatMetricDisplayValue({ value: data.sharePct, kind: "percent" })} | Trend ${fmtSignedPct(data.trendPct, 1)} | Tip ${data.topFootwearType} (${formatMetricDisplayValue({ value: data.topFootwearTypeSharePct, kind: "percent" })})${trust}`;
}
const TYPE_INSIGHT_VISIBLE_CATEGORY_LIMIT = 8;

export function buildTypeInsightChartProjection(
  data: VendorSalesNivelacijaResponse | null,
  visibleCategoryLimit = TYPE_INSIGHT_VISIBLE_CATEGORY_LIMIT,
) {
  if (data?.typeInsightsAuthoritative !== true) {
    return {
      chartRows: [] as Array<{ name: string; sharePct: number }>,
      excludedSharePct: null as number | null,
      totalCategoryCount: 0,
      displayDenominatorLabel: null as string | null,
    };
  }

  const rankedCategories = (data.categoryStats ?? [])
    .map((item) => ({
      name: item.category.trim() || "Nepoznato",
      sharePct: normalizeMetricNumber(item.postRevenueSharePercent),
    }))
    .filter((item): item is { name: string; sharePct: number } => item.sharePct != null)
    .sort((a, b) => b.sharePct - a.sharePct || a.name.localeCompare(b.name, "sr"));

  const visibleCategories = rankedCategories.slice(0, visibleCategoryLimit);
  const excludedSharePct = rankedCategories
    .slice(visibleCategoryLimit)
    .reduce((sum, item) => sum + item.sharePct, 0);

  const chartRows = [...visibleCategories];
  if (excludedSharePct > 0) {
    chartRows.push({
      name: "Ostali",
      sharePct: Number(excludedSharePct.toFixed(2)),
    });
  }

  const denominator = data.typeInsightsDenominator ?? "comparable_post_revenue";
  const displayDenominatorLabel = excludedSharePct > 0
    ? `Udeo u odnosu na punu uporedivu kohortu (${denominator}); prikaz top ${visibleCategoryLimit} plus Ostali.`
    : `Udeo u odnosu na punu uporedivu kohortu (${denominator}); prikaz top ${Math.min(visibleCategoryLimit, rankedCategories.length)} kategorija.`;

  return {
    chartRows,
    excludedSharePct: excludedSharePct > 0 ? Number(excludedSharePct.toFixed(2)) : null,
    totalCategoryCount: rankedCategories.length,
    displayDenominatorLabel,
  };
}

function buildTypeInsights(data: VendorSalesNivelacijaResponse | null) {
  const projection = buildTypeInsightChartProjection(data);
  return { globalTypeShare: projection.chartRows };
}

function normalizeDataQualityStatus(value: string | null | undefined): DataQualityStatus {
  const normalized = (value ?? "").trim().toLowerCase();
  if (normalized === "good" || normalized === "warning" || normalized === "critical" || normalized === "insufficient_data") {
    return normalized;
  }

  return null;
}

function getDataQualityStatus(data: VendorSalesNivelacijaResponse | null): DataQualityStatus {
  if (!data) return null;

  const metaStatus = normalizeDataQualityStatus(data.meta?.dataQualityStatus ?? null);
  if (metaStatus) return metaStatus;

  if (data.meta?.warningCode || data.meta?.isPartial) {
    return "warning";
  }

  if ((data.vendorStats?.length ?? 0) === 0 || (data.articleStats?.length ?? 0) === 0) {
    return "insufficient_data";
  }

  const dataQuality = projectVendorSalesDataQuality(data.dataQuality);
  if (!dataQuality.isComplete) {
    return "insufficient_data";
  }

  if (dataQuality.analyzedRows === 0) {
    return "insufficient_data";
  }

  if ((dataQuality.inactiveRows ?? 0) > 0 || (dataQuality.lowPostCoverageRows ?? 0) > 0) {
    return "warning";
  }

  return "good";
}

export function resolveSupplierFootwearFreshnessStatus(data: VendorSalesNivelacijaResponse | null): AnalyticsFreshnessStatus {
  if (!data) return "unknown";

  const metaStatus = data.meta?.dataQualityStatus?.trim().toLowerCase();
  if (metaStatus === "critical") return "critical";
  if (data.meta?.warningCode || data.meta?.isPartial || metaStatus === "warning") return "stale";

  const lastRefreshAtUtc = data.meta?.lastRefreshAtUtc?.trim();
  return lastRefreshAtUtc && Number.isFinite(Date.parse(lastRefreshAtUtc)) ? "fresh" : "unknown";
}

export default function SupplierFootwearAnalyticsPage({
  embedded = false,
  sharedFilters,
  onTrustMetadataChange,
}: SupplierEmbeddedPageProps = {}) {
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const requestIdRef = useRef(0);
  const initialRange = useMemo(() => getPresetRange("30d"), []);
  const urlDataScopeParam = searchParams.get("dataScope");
  const [persistedDataScope, setPersistedDataScope] = useState<DataScope>(() => (
    normalizeDataScope(urlDataScopeParam ?? getDataScope())
  ));
  const effectiveDataScope = useMemo(() => {
    if (sharedFilters?.dataScope) return normalizeDataScope(sharedFilters.dataScope);
    if (urlDataScopeParam) return normalizeDataScope(urlDataScopeParam);
    return persistedDataScope;
  }, [persistedDataScope, sharedFilters?.dataScope, urlDataScopeParam]);

  const [periodPreset, setPeriodPreset] = useState<PeriodPreset>(sharedFilters?.periodPreset ?? "30d");
  const [fromDate, setFromDate] = useState(sharedFilters?.fromDate ?? initialRange.fromDate);
  const [toDate, setToDate] = useState(sharedFilters?.toDate ?? initialRange.toDate);
  const [vendorId, setVendorId] = useState<number | null>(sharedFilters?.supplierId ?? null);
  const [category, setCategory] = useState("");
  const [activeFilters, setActiveFilters] = useState<ActiveFilters>({
    fromDate: sharedFilters?.fromDate ?? initialRange.fromDate,
    toDate: sharedFilters?.toDate ?? initialRange.toDate,
    vendorId: sharedFilters?.supplierId ?? null,
    category: "",
    storeId: sharedFilters?.storeId ?? null,
    dataScope: sharedFilters?.dataScope
      ? normalizeDataScope(sharedFilters.dataScope)
      : normalizeDataScope(urlDataScopeParam ?? getDataScope()),
  });

  const [vendors, setVendors] = useState<Dobavljac[]>([]);
  const [data, setData] = useState<VendorSalesNivelacijaResponse | null>(null);
  const [previousRevenue, setPreviousRevenue] = useState<number | null>(null);
  const [previousPeriodState, setPreviousPeriodState] = useState<PreviousPeriodComparisonState>("empty");
  const [previousPeriodWarning, setPreviousPeriodWarning] = useState<string | null>(null);
  const [previousPeriodEmptyNote, setPreviousPeriodEmptyNote] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [dataHint, setDataHint] = useState<string | null>(null);
  const [suggestedRange, setSuggestedRange] = useState<SuggestedRange | null>(null);
  const [sortField, setSortField] = useState<SortField>("status");
  const [sortDir, setSortDir] = useState<SortDir>("desc");
  const [expandedVendorKey, setExpandedVendorKey] = useState<string | null>(null);

  const invalidRange = useMemo(() => (!fromDate || !toDate ? false : new Date(fromDate) > new Date(toDate)), [fromDate, toDate]);
  const isDirty = useMemo(() => (
    fromDate !== activeFilters.fromDate
    || toDate !== activeFilters.toDate
    || vendorId !== activeFilters.vendorId
    || category !== activeFilters.category
  ), [activeFilters.category, activeFilters.fromDate, activeFilters.toDate, activeFilters.vendorId, category, fromDate, toDate, vendorId]);

  useEffect(() => {
    if (embedded || sharedFilters?.dataScope || urlDataScopeParam) return;
    const handleScopeChange = () => {
      setPersistedDataScope(getDataScope());
    };

    window.addEventListener("trendplus:data-scope-changed", handleScopeChange);
    return () => window.removeEventListener("trendplus:data-scope-changed", handleScopeChange);
  }, [embedded, sharedFilters?.dataScope, urlDataScopeParam]);

  useEffect(() => {
    setActiveFilters((current) => (
      current.dataScope === effectiveDataScope
        ? current
        : { ...current, dataScope: effectiveDataScope }
    ));
  }, [effectiveDataScope]);

  useEffect(() => {
    setData(null);
    setError(null);
    setExpandedVendorKey(null);
    setPreviousRevenue(null);
    setPreviousPeriodState("empty");
    setPreviousPeriodWarning(null);
    setPreviousPeriodEmptyNote(null);
  }, [effectiveDataScope]);

  useEffect(() => {
    if (!sharedFilters) return;
    setPeriodPreset(sharedFilters.periodPreset);
    setFromDate(sharedFilters.fromDate);
    setToDate(sharedFilters.toDate);
    setVendorId(sharedFilters.supplierId);
    setActiveFilters((current) => {
      const next = {
        ...current,
        fromDate: sharedFilters.fromDate,
        toDate: sharedFilters.toDate,
        vendorId: sharedFilters.supplierId,
        storeId: sharedFilters.storeId,
        dataScope: normalizeDataScope(sharedFilters.dataScope),
      };
      return current.fromDate === next.fromDate
        && current.toDate === next.toDate
        && current.vendorId === next.vendorId
        && current.storeId === next.storeId
        && current.dataScope === next.dataScope
        ? current
        : next;
    });
  }, [sharedFilters]);

  useEffect(() => {
    const loadVendors = async () => {
      try {
        setVendors(await getDobavljaci());
      } catch {
        // Preserve the last known vendor list on transient failures instead of faking an empty filter set.
      }
    };
    void loadVendors();
  }, []);

  const load = useCallback(async (filters: ActiveFilters) => {
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);
    setDataHint(null);
    setSuggestedRange(null);
    setPreviousPeriodWarning(null);
    setPreviousPeriodEmptyNote(null);
    try {
      const currentRange = toUtcRange(filters.fromDate, filters.toDate);
      const previousRange = buildPreviousRange(filters.fromDate, filters.toDate);

      const [currentResult, previousResult] = await Promise.allSettled([
        getVendorSalesNivelacija({ ...currentRange, vendorId: filters.vendorId, category: filters.category || null, includeInactive: false, storeId: filters.storeId, dataScope: filters.dataScope }),
        getVendorSalesNivelacija({ ...previousRange, vendorId: filters.vendorId, category: filters.category || null, includeInactive: false, storeId: filters.storeId, dataScope: filters.dataScope }),
      ]);
      if (requestId !== requestIdRef.current) return;
      if (currentResult.status === "rejected") throw currentResult.reason;

      const currentData = currentResult.value;
      const previousComparison = resolvePreviousPeriodComparison(
        previousResult,
        (reason) => getSafeAnalyticsErrorMessage(
          reason instanceof Error ? reason.message : String(reason),
          undefined,
          SUPPLIER_PREVIOUS_PERIOD_FAILURE_NOTE,
        ),
      );
      setPreviousPeriodState(previousComparison.state);
      setPreviousPeriodWarning(previousComparison.warning);
      setPreviousPeriodEmptyNote(previousComparison.emptyBaselineNote);
      setPreviousRevenue(previousComparison.previousRevenue);

      const hasNoRows = currentData.vendorStats.length === 0 && currentData.articleStats.length === 0;
      const currentDataQuality = projectVendorSalesDataQuality(currentData.dataQuality);
      const likelyFilteredOutByInactive = hasNoRows
        && currentDataQuality.isComplete
        && (currentDataQuality.deduplicatedRows ?? 0) > 0
        && (currentDataQuality.inactiveRows ?? 0) > 0;

      const stillNoRows = currentData.vendorStats.length === 0 && currentData.articleStats.length === 0;
      if (stillNoRows) {
        let options: VendorSalesNivelacijaOption[] | null = null;
        try {
          options = await getVendorSalesNivelacijaOptions({
            vendorId: filters.vendorId,
            category: filters.category || undefined,
            storeId: filters.storeId,
            dataScope: filters.dataScope,
            take: 60,
          });
        } catch (reason) {
          const safeReason = getSafeAnalyticsErrorMessage(
            reason instanceof Error ? reason.message : null,
            undefined,
            "Opcije za predlog perioda trenutno nisu dostupne.",
          );
          setDataHint(`Nema analiziranih redova za izabrani period. ${safeReason}`);
        }

        if (requestId !== requestIdRef.current) return;

        const suggested = options?.find((item) => item.hasSalesWindow) ?? options?.[0];
        if (suggested) {
          const day = toDateOnly(suggested.eventDate);
          setSuggestedRange({
            fromDate: day,
            toDate: day,
            label: suggested.label,
          });
          setDataHint("Za izabrani period nema analiziranih redova. Predlozen je datum gde postoje nivelacije i/ili prodaja.");
        } else if (options === null) {
          // Keep options-unavailable visibly degraded; do not relabel it as a
          // successful no-match period.
        } else if (likelyFilteredOutByInactive) {
          setDataHint("U periodu postoje nivelacije, ali bez prodaje u pre/post prozoru. Ukljuci siri period ili proveri opciju sa neaktivnim artiklima.");
        } else {
          setDataHint("U izabranom periodu nema nivelacija za zadate filtere.");
        }
      }

      setData(currentData);
      setExpandedVendorKey(null);
    } catch (reason) {
      if (requestId !== requestIdRef.current) return;
      setData(null);
      setPreviousRevenue(null);
      setPreviousPeriodState("empty");
      setPreviousPeriodWarning(null);
      setPreviousPeriodEmptyNote(null);
      setDataHint(null);
      setSuggestedRange(null);
      setError(reason instanceof Error ? reason.message : "Greška pri učitavanju analize dobavljača i tipova obuće.");
    } finally {
      if (requestId === requestIdRef.current) setLoading(false);
    }
  }, []);

  useEffect(() => { void load(activeFilters); }, [activeFilters, load]);

  const typeInsights = useMemo(
    () => buildTypeInsights(data),
    [data],
  );
  const typeInsightChartProjection = useMemo(
    () => buildTypeInsightChartProjection(data),
    [data],
  );

  const decisionRows = useMemo<DecisionVendor[]>(() => {
    const rows = data?.vendorStats ?? [];
    if (rows.length === 0) return [];

    const evidenceRows = rows.filter(rowHasComparableEvidence);
    const totalRevenue = evidenceRows.reduce((sum, item) => sum + (comparableMetric(item.postRevenue, true) ?? 0), 0);
    const vendorRowKeys = buildSupplierVendorKeys(rows);
    return rows.flatMap((item, rowIndex) => {
      const recommendation = item.recommendation;
      if (!recommendation) return [];

      const vendorRowKey = vendorRowKeys[rowIndex];
      const hasComparableEvidence = rowHasComparableEvidence(item);
      const postRevenue = comparableMetric(item.postRevenue, hasComparableEvidence);
      const sharePct = postRevenue != null && totalRevenue > 0 ? (postRevenue / totalRevenue) * 100 : null;
      const trendPct = comparableMetric(item.semanticChangePercentRevenue ?? item.changePercent, hasComparableEvidence);
      const recommendationAllowed = recommendation.recommendationAllowed === true;

      const typeInsightsAvailable = data?.typeInsightsAuthoritative === true && item.typeInsightsAuthoritative === true;
      const topFootwearType = typeInsightsAvailable ? item.primaryFootwearType ?? "N/A" : "N/A";
      const topFootwearTypeSharePct = typeInsightsAvailable ? normalizeMetricNumber(item.primaryFootwearTypeSharePercent) : null;
      const avgElasticity = typeInsightsAvailable ? normalizeMetricNumber(item.primaryFootwearTypeAvgElasticity) : null;

      return [{
        ...item,
        vendorRowKey,
        sharePct,
        trendPct,
        topFootwearType,
        topFootwearTypeSharePct,
        avgElasticity,
        confidencePct: recommendationAllowed ? normalizeMetricNumber(recommendation.confidencePct) : null,
        recommendationAllowed,
        status: recommendation.status,
        statusReason: recommendation.summary,
      }];
    });
  }, [data?.typeInsightsAuthoritative, data?.vendorStats]);

  const sortedRows = useMemo(() => {
    const rows = [...decisionRows];
    return rows.sort((a, b) => {
      let compare = 0;
      if (sortField === "vendorName") compare = a.vendorName.localeCompare(b.vendorName, "sr");
      else if (sortField === "postRevenue") compare = (comparableMetric(a.postRevenue, rowHasComparableEvidence(a)) ?? -1) - (comparableMetric(b.postRevenue, rowHasComparableEvidence(b)) ?? -1);
      else if (sortField === "sharePct") compare = (a.sharePct ?? -1) - (b.sharePct ?? -1);
      else if (sortField === "topFootwearType") compare = a.topFootwearType.localeCompare(b.topFootwearType, "sr");
      else if (sortField === "trendPct") compare = (a.trendPct ?? -1) - (b.trendPct ?? -1);
      else if (sortField === "status") compare = STATUS_PRIORITY[a.status] - STATUS_PRIORITY[b.status];
      if (compare === 0) compare = (a.confidencePct ?? -1) - (b.confidencePct ?? -1);
      if (compare === 0) compare = (a.reliabilityPct ?? -1) - (b.reliabilityPct ?? -1);
      return sortDir === "asc" ? compare : -compare;
    });
  }, [decisionRows, sortDir, sortField]);

  const hasUntrustedRows = data?.vendorStats?.some((row) => !rowHasComparableEvidence(row)) ?? false;
  const serverTotalRevenue = normalizeMetricNumber(data?.totals.postRevenue);
  const fallbackTotalRevenue = data?.vendorStats?.length
    ? data.vendorStats.reduce<number | null>((sum, row) => {
      const metric = comparableMetric(row.postRevenue, rowHasComparableEvidence(row));
      return metric == null ? null : (sum ?? 0) + metric;
    }, 0)
    : null;
  const totalRevenue = data?.totals.hasComparableSalesWindow === true && !hasUntrustedRows
    ? serverTotalRevenue
      ?? fallbackTotalRevenue
    : null;
  const top5SharePct = useMemo(() => {
    if (sortedRows.length === 0 || totalRevenue == null || totalRevenue <= 0) return null;
    const top5 = [...sortedRows]
      .sort((a, b) => (comparableMetric(b.postRevenue, rowHasComparableEvidence(b)) ?? -1) - (comparableMetric(a.postRevenue, rowHasComparableEvidence(a)) ?? -1))
      .slice(0, 5)
      .map((item) => comparableMetric(item.postRevenue, rowHasComparableEvidence(item)))
      .filter((value): value is number => value != null)
      .reduce((sum, value) => sum + value, 0);
    return (top5 / totalRevenue) * 100;
  }, [sortedRows, totalRevenue]);
  const totalChangeRevenue = hasUntrustedRows ? null : data?.totals.changeRevenue ?? null;
  const periodGrowthPct = useMemo(
    () => resolveSupplierPeriodGrowthPct({
      previousPeriodState,
      previousRevenue,
      totalRevenue,
    }),
    [previousPeriodState, previousRevenue, totalRevenue],
  );
  const dominantTypeSummary = useMemo(() => {
    const topType = typeInsights.globalTypeShare[0];
    if (!topType) return "N/A";
    return `${topType.name} (${fmtPct(topType.sharePct, 1)})`;
  }, [typeInsights.globalTypeShare]);
  const vendorCounts = useMemo(() => ({
    increaseFocus: sortedRows.filter((row) => row.status === "increase_focus").length,
    maintain: sortedRows.filter((row) => row.status === "maintain").length,
    review: sortedRows.filter((row) => row.status === "review").length,
    doNotTrust: sortedRows.filter((row) => row.status === "do_not_trust").length,
    insufficientData: sortedRows.filter((row) => row.status === "insufficient_data").length,
  }), [sortedRows]);
  const selectedRow = useMemo(() => (!expandedVendorKey ? null : sortedRows.find((row) => row.vendorRowKey === expandedVendorKey) ?? null), [expandedVendorKey, sortedRows]);
  const dataMeta = data?.meta ?? null;
  const dataMetaMessage = getAnalyticsMetaMessage(dataMeta);
  const dataQualityProjection = useMemo(() => projectVendorSalesDataQuality(data?.dataQuality), [data?.dataQuality]);
  const hasTruncatedDetail = dataQualityProjection.isDetailTruncated === true;
  const showMetaWarning = !loading && !error && (isAnalyticsMetaWarning(dataMeta) || hasTruncatedDetail);
  const showEmptyState = !loading && !error && ((data?.vendorStats.length ?? 0) === 0 && (data?.articleStats.length ?? 0) === 0);
  const dataQualityStatus = useMemo(() => getDataQualityStatus(data), [data]);
  const recommendationAllowed = data?.recommendationAllowed === true;
  const typeInsightWarning = hasTruncatedDetail
    ? `Detalj prikazuje ${dataQualityProjection.returnedRows ?? "N/A"} od ${dataQualityProjection.analyzedRows ?? "N/A"} analiziranih redova. Tipovi obuće i elastičnost računaju se iz pune uporedive kohorte.`
    : data?.typeInsightsAuthoritative !== true && (dataQualityProjection.analyzedRows ?? 0) > 0
      ? "Tipovi obuće i elastičnost nisu potvrđeni punom uporedivom kohortom i prikazani su kao nedostupni."
      : null;
  const controlBarChips = useMemo<AnalyticsControlBarChip[]>(() => [
    {
      key: "period",
      label: "Period",
      value: `${fromDate} → ${toDate}`,
      tone: "info",
    },
    {
      key: "vendor",
      label: "Dobavljač",
      value: vendorId == null ? "Svi" : vendors.find((vendor) => vendor.id === vendorId)?.naziv ?? String(vendorId),
      tone: vendorId == null ? "neutral" : "success",
    },
    {
      key: "category",
      label: "Kategorija",
      value: category || "Sve",
      tone: category ? "warning" : "neutral",
    },
    {
      key: "rows",
      label: "Prikazano",
      value: `${sortedRows.length.toLocaleString("sr-RS")} redova`,
      tone: sortedRows.length === 0 ? "warning" : "success",
    },
    {
      key: "signal",
      label: "Signal",
      value: showMetaWarning ? "Delimičan" : (dataQualityStatus ?? "Nepoznat"),
      tone: showMetaWarning ? "warning" : dataQualityStatus === "good" ? "success" : dataQualityStatus === "warning" ? "warning" : dataQualityStatus === "critical" ? "critical" : "neutral",
    },
  ], [category, dataQualityStatus, fromDate, showMetaWarning, sortedRows.length, toDate, vendorId, vendors]);
  const controlBarFields = useMemo<AnalyticsControlBarField[]>(() => [
    {
      key: "periodPreset",
      label: "Period",
      control: (
        <select
          value={periodPreset}
          onChange={(e) => {
            const value = e.target.value as PeriodPreset;
            setPeriodPreset(value);
            if (value === "custom") return;
            const range = getPresetRange(value);
            setFromDate(range.fromDate);
            setToDate(range.toDate);
          }}
        >
          <option value="30d">Poslednjih 30 dana</option>
          <option value="90d">Poslednjih 90 dana</option>
          <option value="180d">Poslednjih 180 dana</option>
          <option value="365d">Poslednjih 365 dana</option>
          <option value="custom">Prilagođeno</option>
        </select>
      ),
    },
    {
      key: "fromDate",
      label: "Od",
      control: <input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />,
    },
    {
      key: "toDate",
      label: "Do",
      control: <input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />,
    },
    {
      key: "vendor",
      label: "Dobavljač",
      control: (
        <select value={vendorId ?? ""} onChange={(e) => setVendorId(e.target.value ? Number(e.target.value) : null)}>
          <option value="">Svi</option>
          {vendors.map((vendor) => (
            <option key={vendor.id} value={vendor.id}>{vendor.naziv}</option>
          ))}
        </select>
      ),
    },
    {
      key: "category",
      label: "Kategorija",
      control: (
        <select value={category} onChange={(e) => setCategory(e.target.value)}>
          <option value="">Sve</option>
          {(data?.categories ?? []).map((item) => (
            <option key={item} value={item}>{item}</option>
          ))}
        </select>
      ),
    },
  ], [category, data?.categories, fromDate, periodPreset, toDate, vendorId, vendors]);

  const toolbarFilters = useMemo<AnalyticsNamedValue[]>(() => [
    { key: "periodPreset", label: "Period", value: periodPreset },
    { key: "fromDate", label: "Od", value: activeFilters.fromDate },
    { key: "toDate", label: "Do", value: activeFilters.toDate },
      { key: "vendorId", label: "Dobavljač", value: activeFilters.vendorId ?? "" },
    { key: "category", label: "Kategorija", value: activeFilters.category },
    { key: "storeId", label: "Objekat", value: activeFilters.storeId ?? "" },
    { key: "dataScope", label: "Opseg podataka", value: activeFilters.dataScope },
  ], [activeFilters.category, activeFilters.dataScope, activeFilters.fromDate, activeFilters.storeId, activeFilters.toDate, activeFilters.vendorId, periodPreset]);

  const toolbarMetadata = useMemo<AnalyticsNamedValue[]>(() => [
    { key: "generatedAt", label: "Generisano", value: data?.generatedAt ?? "" },
    { key: "vendorsCount", label: "Dobavljača", value: formatMetricDisplayValue({ value: normalizeMetricNumber(data?.totals.vendorsCount), kind: "number", fallback: "N/A" }) },
    { key: "articlesCount", label: "Artikala", value: formatMetricDisplayValue({ value: normalizeMetricNumber(data?.totals.articlesCount), kind: "number", fallback: "N/A" }) },
    { key: "windowDays", label: "Prozor (dani)", value: formatMetricDisplayValue({ value: normalizeMetricNumber(data?.windowDays), kind: "number", fallback: "N/A" }) },
    { key: "detailDenominator", label: "Detalj / analiza", value: data?.dataQuality?.returnedRows != null && data?.dataQuality?.analyzedRows != null ? `${data.dataQuality.returnedRows} / ${data.dataQuality.analyzedRows}` : "N/A" },
    { key: "typeInsightSource", label: "Izvor tipova", value: data?.typeInsightsSource ?? "Nije dostupno" },
    { key: "typeInsightDenominator", label: "Imenilac tipova", value: data?.typeInsightsDenominator ?? "Nije dostupno" },
    { key: "typeInsightElasticityWeighting", label: "Tezina elasticnosti", value: data?.typeInsightsElasticityWeighting ?? "Nije dostupno" },
    { key: "typeInsightExcludedShare", label: "Udeo van prikaza", value: formatMetricDisplayValue({ value: typeInsightChartProjection.excludedSharePct, kind: "percent", fallback: "N/A" }) },
    { key: "requestedDataScope", label: "Traženi opseg", value: effectiveDataScope },
    { key: "effectiveDataScope", label: "Efektivni opseg", value: data?.dataScope ?? effectiveDataScope },
  ], [data?.dataQuality?.analyzedRows, data?.dataQuality?.returnedRows, data?.dataScope, data?.generatedAt, data?.totals.articlesCount, data?.totals.vendorsCount, data?.typeInsightsDenominator, data?.typeInsightsElasticityWeighting, data?.typeInsightsSource, data?.windowDays, effectiveDataScope, typeInsightChartProjection.excludedSharePct]);

  useEffect(() => {
    if (!embedded || !onTrustMetadataChange) return;

    if (!data) {
      onTrustMetadataChange(null);
      return;
    }

    onTrustMetadataChange({
      periodFrom: activeFilters.fromDate,
      periodTo: activeFilters.toDate,
      lastRefreshAt: data.meta?.lastRefreshAtUtc ?? null,
      dataFreshnessStatus: resolveSupplierFootwearFreshnessStatus(data),
      dataSource: "Supplier sales nivelacija po dobavljaču i tipu obuće",
      dataQualityStatus: dataQualityStatus ?? (showMetaWarning ? "warning" : "good"),
      recommendationAllowed,
      recommendationNote: previousPeriodState === "failed"
        ? "Asortiman je analitički signal. Uporedni prethodni period nije učitan."
        : "Asortiman je analitički signal. Finalna preporuka ostaje u centralnom dobavljačkom pregledu.",
      emptyStateReason: showEmptyState ? (dataMetaMessage ?? dataHint ?? null) : null,
    });
  }, [
    activeFilters.fromDate,
    activeFilters.toDate,
    data,
    dataHint,
    dataMetaMessage,
    dataQualityStatus,
    embedded,
    onTrustMetadataChange,
    previousPeriodState,
    recommendationAllowed,
    showEmptyState,
    showMetaWarning,
  ]);

  const handleSort = (field: SortField) => {
    if (sortField === field) { setSortDir((current) => (current === "asc" ? "desc" : "asc")); return; }
    setSortField(field);
    setSortDir(field === "vendorName" || field === "topFootwearType" ? "asc" : "desc");
  };
  const handlePresetChange = (value: PeriodPreset) => {
    setPeriodPreset(value);
    if (value === "custom") return;
    const range = getPresetRange(value);
    setFromDate(range.fromDate);
    setToDate(range.toDate);
  };
  const handleApplyFilters = () => {
    if (!invalidRange) {
      setActiveFilters({
        fromDate,
        toDate,
        vendorId,
        category,
        storeId: sharedFilters?.storeId ?? null,
        dataScope: effectiveDataScope,
      });
    }
  };
  const handleResetFilters = () => {
    const range = getPresetRange("30d");
    setPeriodPreset("30d");
    setFromDate(range.fromDate);
    setToDate(range.toDate);
    setVendorId(null);
    setCategory("");
    setActiveFilters({
      fromDate: sharedFilters?.fromDate ?? range.fromDate,
      toDate: sharedFilters?.toDate ?? range.toDate,
      vendorId: sharedFilters?.supplierId ?? null,
      category: "",
      storeId: sharedFilters?.storeId ?? null,
      dataScope: sharedFilters?.dataScope
        ? normalizeDataScope(sharedFilters.dataScope)
        : effectiveDataScope,
    });
  };
  const handleApplySuggestedRange = () => {
    if (!suggestedRange) return;
    setPeriodPreset("custom");
    setFromDate(suggestedRange.fromDate);
    setToDate(suggestedRange.toDate);
    setActiveFilters((current) => ({ ...current, fromDate: suggestedRange.fromDate, toDate: suggestedRange.toDate }));
  };

  const openVendorDetail = (row: DecisionVendor) => {
    saveAnalyticsDetailSnapshot(buildAnalyticsDetailSnapshot({
      table: "dobavljaci-tipovi-obuce",
      recordId: buildSupplierVendorDetailRecordId(row, row.vendorRowKey),
      title: row.vendorName,
      subtitle: "Podrska odluci po dobavljacu i tipu obuce",
      columns: decisionColumns,
      row,
      metadata: [...toolbarFilters, ...toolbarMetadata],
    }));
    navigate(`/analitika/dobavljaci-tipovi-obuce/${encodeURIComponent(String(row.vendorId ?? row.vendorName))}`, { state: { backgroundLocation: location } });
  };

  return (
    <div
      className={`sf-decision-page ${embedded ? "sf-decision-page--embedded" : ""}`}
      role={embedded ? "region" : undefined}
      aria-label={embedded ? "Asortiman dobavljača" : undefined}
    >
      {!embedded ? (
        <AnalyticsTrustHeader
          title="Dobavljači i tipovi obuće"
          description="Ovaj ekran prikazuje dodatni analitički signal za dobavljače i dominantne tipove obuće, uz isti period i kvalitet podataka koji koristi i konsolidovani dobavljački pregled."
          periodFrom={activeFilters.fromDate}
          periodTo={activeFilters.toDate}
          lastRefreshAt={data?.meta?.lastRefreshAtUtc ?? null}
          dataFreshnessStatus={resolveSupplierFootwearFreshnessStatus(data)}
          dataSource="Supplier sales nivelacija"
          dataQualityStatus={dataQualityStatus}
          mode="signal"
          recommendationNote="Asortiman je pomoćni signal. Finalna odluka ostaje u centralnom dobavljačkom pregledu."
          emptyStateReason={showEmptyState ? (dataMetaMessage ?? dataHint ?? null) : null}
          methodologyHref="/analytics/data-quality"
          dataQualityHref="/analytics/data-quality"
          refreshStatusHref="/admin/configuration?panel=workers"
          recommendationAllowed={recommendationAllowed}
          isPartial={showMetaWarning}
          compact
        />
      ) : null}

      {!embedded ? (
        <AnalyticsControlBar
          title="Kontrole asortimana"
          description="Filtriraj period, dobavljača i kategoriju bez menjanja poslovne logike preporuke."
          chips={controlBarChips}
          primaryAction={{
            key: "apply",
            label: "Primeni filtere",
            onClick: handleApplyFilters,
            disabled: loading || !isDirty || invalidRange,
          }}
          secondaryActions={[
            {
              key: "reset",
              label: "Reset filtera",
              onClick: handleResetFilters,
              disabled: loading || !isDirty,
              tone: "secondary",
            },
            {
              key: "quality",
              label: "Kvalitet podataka",
              to: "/analytics/data-quality",
              tone: "secondary",
            },
          ]}
          fields={controlBarFields}
        />
      ) : null}

      {invalidRange ? <div className="sf-decision-message error" role="alert">Datum 'od' ne može biti posle datuma 'do'.</div> : null}
      {error ? <div className="sf-decision-message error" role="alert">{error}</div> : null}
      {loading ? <div className="sf-decision-message loading" role="status" aria-live="polite">Učitavam dobavljače i tipove obuće...</div> : null}
      {!loading && !error && previousPeriodWarning ? (
        <div className="sf-decision-message warning" role="status" aria-live="polite">{previousPeriodWarning}</div>
      ) : null}
      {!loading && !error && !previousPeriodWarning && previousPeriodEmptyNote ? (
        <div className="sf-decision-message info" role="status" aria-live="polite">{previousPeriodEmptyNote}</div>
      ) : null}
      {!loading && !error && typeInsightWarning ? <div className="sf-decision-message warning" role="status" aria-live="polite">{typeInsightWarning}</div> : null}
      {!loading && !error && dataHint ? <div className="sf-decision-message info" role="status" aria-live="polite">{dataHint}</div> : null}
      {!embedded && !loading && !error && suggestedRange ? (
        <div className="sf-decision-message suggestion">
          <span>Predlog: {suggestedRange.label}</span>
          <button type="button" onClick={handleApplySuggestedRange}>Primeni predlog perioda</button>
        </div>
      ) : null}

      {!loading && data ? (
        <>
          {totalRevenue === 0 && decisionRows.length === 0 && (
            <div className="sf-decision-message warning">
              <strong>Zašto je promet 0 RSD?</strong>
              <p>
                {!dataQualityProjection.isComplete
                  ? "Kvalitet podataka nije potvrđen; nije bezbedno zaključiti da je promet stvarno nula."
                  : dataQualityProjection.rawRows === 0
                    ? "U izabranom periodu nema evidentirane nivelacije u Dnevniku promena. Pokušajte sa drugačijim periodom ili dobavljačem."
                    : dataQualityProjection.analyzedRows === 0 && (dataQualityProjection.inactiveRows ?? 0) > 0
                      ? `Nivelacije postoje (${dataQualityProjection.rawRows} redova), ali bez prodaje u 30-dnevnom post-prozoru. Pokušajte sa periodima gde postoji prodajna aktivnost.`
                      : `Analizirano je ${dataQualityProjection.analyzedRows} redova, ali bez detektovanog prometa. Proverite filtere ili proširite vremenski raspon.`}
              </p>
            </div>
          )}

          <section className="sf-decision-kpis">
            <article className="sf-decision-kpi analytics-kpi-card analytics-kpi-card--tone-info" data-note="Promet svih dobavljača u izabranom periodu."><span>Ukupan promet</span><strong>{formatMetricDisplayValue({ value: totalRevenue, kind: "currency" })}</strong></article>
            <article className="sf-decision-kpi analytics-kpi-card analytics-kpi-card--tone-success" data-note="Koliki deo prometa drzi pet najjacih dobavljaca."><span>Udeo top 5 dobavljaca</span><strong>{formatMetricDisplayValue({ value: top5SharePct, kind: "percent" })}</strong></article>
            <article className="sf-decision-kpi analytics-kpi-card analytics-kpi-card--tone-neutral" data-note="Apsolutna promena prometa u odnosu na pre period."><span>Ukupna promena prometa</span><strong className={trendClass(totalChangeRevenue)}>{formatMetricDisplayValue({ value: totalChangeRevenue, kind: "currency" })}</strong></article>
            <article className="sf-decision-kpi analytics-kpi-card analytics-kpi-card--tone-warning" data-note="Relativna promena prema prethodnom uporedivom periodu."><span>Rast/pad u odnosu na prethodni period</span><strong className={trendClass(periodGrowthPct)}>{fmtSignedPct(periodGrowthPct)}</strong></article>
            <article className="sf-decision-kpi analytics-kpi-card analytics-kpi-card--tone-value" data-note="Tip obuce koji trenutno nosi najveci deo prometa."><span>Dominantan tip obuce</span><strong>{dominantTypeSummary}</strong></article>
          </section>

          <section className="sf-decision-panels">
            <article className="sf-decision-card analytics-surface-panel">
              <h2>Koncentracija po tipu obuce</h2>
              <p>{typeInsightChartProjection.displayDenominatorLabel ?? "Tipovi obuce nisu potvrđeni punom uporedivom kohortom."}</p>
              {typeInsights.globalTypeShare.length > 0 ? (
                <div className="sf-decision-chart-wrap">
                  <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={260}>
                    <BarChart data={typeInsights.globalTypeShare} layout="vertical" margin={{ top: 12, right: 16, left: 8, bottom: 8 }}>
                      <CartesianGrid strokeDasharray="3 3" stroke="var(--border-default)" />
                      <XAxis type="number" tick={{ fill: "var(--text-secondary)", fontSize: 12 }} unit="%" />
                      <YAxis type="category" dataKey="name" width={180} tick={{ fill: "var(--text-primary)", fontSize: 12 }} />
                      <Tooltip formatter={(value: number | string | undefined) => formatMetricDisplayValue({ value: typeof value === "number" ? value : Number(value), kind: "percent", digits: 2 })} />
                      <Bar dataKey="sharePct" fill="var(--accent-primary)" radius={[0, 8, 8, 0]} />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              ) : <div className="sf-decision-empty">Nema podataka za grafikon tipova obuce.</div>}
            </article>
            <article className="sf-decision-card analytics-surface-panel">
              <AnalyticsDataTable
                testId="supplier-footwear-analytics-data-table"
                rowCount={sortedRows.length}
                truncationLabel={showMetaWarning ? "Delimičan signal" : "Pregled prioriteta"}
              >
              <div className="sf-decision-table-head">
                <div><h2>Prioritetna lista dobavljača</h2><p>Pojačaj: {vendorCounts.increaseFocus} | Zadrži: {vendorCounts.maintain} | Proveri: {vendorCounts.review} | Ne veruj: {vendorCounts.doNotTrust} | Nedovoljno: {vendorCounts.insufficientData}</p></div>
                <AnalyticsTableToolbar tableKey="dobavljaci-tipovi-obuce" tableTitle="Dobavljači i tipovi obuće" columns={decisionColumns} rows={sortedRows} filters={toolbarFilters} metadata={toolbarMetadata} defaultOrientation="landscape" />
              </div>
              <div className="sf-decision-table-wrap">
                <table className="sf-decision-table">
                  <thead>
                    <tr>
                      <th><button type="button" onClick={() => handleSort("vendorName")}>Dobavljač{sortMarker("vendorName", sortField, sortDir)}</button></th>
                      <th className="align-right"><button type="button" onClick={() => handleSort("postRevenue")}>Promet{sortMarker("postRevenue", sortField, sortDir)}</button></th>
                      <th className="align-right"><button type="button" onClick={() => handleSort("sharePct")}>Udeo{sortMarker("sharePct", sortField, sortDir)}</button></th>
                      <th><button type="button" onClick={() => handleSort("topFootwearType")}>Glavni tip{sortMarker("topFootwearType", sortField, sortDir)}</button></th>
                      <th className="align-right"><button type="button" onClick={() => handleSort("trendPct")}>Trend{sortMarker("trendPct", sortField, sortDir)}</button></th>
                      <th><button type="button" onClick={() => handleSort("status")}>Preporuka{sortMarker("status", sortField, sortDir)}</button></th>
                      <th className="align-center">Detalj</th>
                    </tr>
                  </thead>
                  <tbody>
                    {sortedRows.length === 0 ? (
                      <tr><td colSpan={7} className="sf-decision-empty-row">Nema podataka za izabrane filtere.</td></tr>
                    ) : (
                      sortedRows.map((row) => {
                        const rowId = row.vendorRowKey; const expanded = expandedVendorKey === rowId;
                        return (
                          <tr key={rowId} className={expanded ? "expanded-row" : ""}>
                            <td>{row.vendorName || "Nepoznat dobavljač"}</td>
                            <td className="align-right">{fmtRsd(comparableMetric(row.postRevenue, rowHasComparableEvidence(row)))}</td>
                            <td className="align-right">{formatMetricDisplayValue({ value: row.sharePct, kind: "percent", digits: 2 })}</td>
                            <td><strong>{row.topFootwearType}</strong><div className="sf-mini-note">{formatMetricDisplayValue({ value: row.topFootwearTypeSharePct, kind: "percent" })} udela kod dobavljača</div></td>
                            <td className={`align-right ${trendClass(row.trendPct)}`}>{fmtSignedPct(row.trendPct, 2)}</td>
                            <td>
                              <div className="sf-status-stack">
                                <span className={statusClass(row.status)} title={buildStatusTooltip(row)} aria-label={buildStatusTooltip(row)}>{statusDisplayLabel(row.status)}</span>
                                {row.statusReason ? (
                                  <span className="sf-status-reason" title={row.statusReason}>
                                    <strong>Razlog:</strong> {row.statusReason}
                                  </span>
                                ) : null}
                              </div>
                            </td>
                            <td className="align-center"><button type="button" className="sf-decision-detail-btn" onClick={() => setExpandedVendorKey(expanded ? null : rowId)}>{expanded ? "Sakrij" : "Detalji"}</button></td>
                          </tr>
                        );
                      })
                    )}
                  </tbody>
                </table>
              </div>
              </AnalyticsDataTable>
            </article>
          </section>

          {selectedRow ? (
            <section className="sf-decision-detail">
              <div className="sf-decision-detail-head"><h3>Detalj odluke: {selectedRow.vendorName || "Nepoznat dobavljač"}</h3><button type="button" onClick={() => openVendorDetail(selectedRow)}>Otvori puni detalj</button></div>
              <div className="sf-decision-detail-grid">
                <article className="analytics-kpi-card analytics-kpi-card--tone-neutral"><span>Pre nivelacije promet</span><strong>{fmtRsd(comparableMetric(selectedRow.preRevenue, rowHasComparableEvidence(selectedRow)))}</strong></article>
                <article className="analytics-kpi-card analytics-kpi-card--tone-info"><span>Posle nivelacije promet</span><strong>{fmtRsd(comparableMetric(selectedRow.postRevenue, rowHasComparableEvidence(selectedRow)))}</strong></article>
                <article className="analytics-kpi-card analytics-kpi-card--tone-neutral"><span>Pre nivo kolicina</span><strong>{fmtQty(comparableMetric(selectedRow.preQty, rowHasComparableEvidence(selectedRow)))}</strong></article>
                <article className="analytics-kpi-card analytics-kpi-card--tone-success"><span>Posle nivo kolicina</span><strong>{fmtQty(comparableMetric(selectedRow.postQty, rowHasComparableEvidence(selectedRow)))}</strong></article>
                <article className="analytics-kpi-card analytics-kpi-card--tone-info"><span>Glavni tip obuće</span><strong>{selectedRow.topFootwearType} ({formatMetricDisplayValue({ value: selectedRow.topFootwearTypeSharePct, kind: "percent" })})</strong></article>
                <article className="analytics-kpi-card analytics-kpi-card--tone-warning"><span>Elastičnost glavnog tipa</span><strong>{fmtElasticity(selectedRow.avgElasticity)}</strong></article>
                <article className="analytics-kpi-card analytics-kpi-card--tone-neutral"><span>Aktivni artikli</span><strong>{selectedRow.activeArticlesCount} / {selectedRow.articleCount}</strong></article>
                <article className="analytics-kpi-card analytics-kpi-card--tone-success"><span>Pouzdanost signala</span><strong>{formatMetricDisplayValue({ value: selectedRow.recommendationAllowed ? selectedRow.reliabilityPct : null, kind: "percent" })}</strong></article>
                <article className="analytics-kpi-card analytics-kpi-card--tone-value"><span>Poverenje preporuke</span><strong>{formatMetricDisplayValue({ value: selectedRow.recommendationAllowed ? selectedRow.confidencePct : null, kind: "percent" })}</strong></article>
              </div>
              {selectedRow.vendorId == null || !selectedRow.vendorRowKey.startsWith("id:") ? (
                <p className="sf-mini-note" role="status">Identitet dobavljača nije potvrđen. Detalj važi samo za izabrani red, bez spajanja po nazivu.</p>
              ) : null}
              <p className="sf-decision-reason"><strong>Razlog preporuke:</strong> {selectedRow.statusReason}</p>
            </section>
          ) : null}
        </>
      ) : null}
    </div>
  );
}

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
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
  type VendorSalesNivelacijaArticleStat,
  type VendorSalesNivelacijaRecommendation,
  type VendorSalesNivelacijaResponse,
  type VendorSalesNivelacijaVendorStat,
} from "../services/vendorSalesNivelacijaApi";
import type { Dobavljac } from "../types/Dobavljaci";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import { fmtPct, fmtQty, fmtRsd, fmtSignedPct, getPresetRange } from "../utils/analyticsFormatters";
import { getAnalyticsMetaMessage, isAnalyticsMetaInsufficient, isAnalyticsMetaWarning, shouldShowAnalyticsEmptyState } from "../utils/analyticsResponseMeta";
import type { SupplierEmbeddedPageProps } from "./supplierSharedState";
import "./SupplierFootwearAnalyticsPage.css";

type PeriodPreset = "30d" | "90d" | "180d" | "365d" | "custom";
type SortDir = "asc" | "desc";
type SortField = "vendorName" | "postRevenue" | "sharePct" | "topFootwearType" | "trendPct" | "status";
type DecisionStatus = VendorSalesNivelacijaRecommendation["status"];

type ActiveFilters = { fromDate: string; toDate: string; vendorId: number | null; category: string; storeId: number | null; dataScope: string | null };
type SuggestedRange = { fromDate: string; toDate: string; label: string };
type DataQualityStatus = "good" | "warning" | "critical" | "insufficient_data" | null;

type DecisionVendor = VendorSalesNivelacijaVendorStat & {
  sharePct: number;
  trendPct: number;
  topFootwearType: string;
  topFootwearTypeSharePct: number;
  avgElasticity: number | null;
  confidencePct: number;
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

const decisionColumns: AnalyticsTableColumn<DecisionVendor>[] = [
  { key: "vendorName", header: "Dobavljač", dataType: "text" },
  { key: "postRevenue", header: "Promet", dataType: "currency" },
  { key: "sharePct", header: "Udeo %", dataType: "percent" },
  { key: "topFootwearType", header: "Glavni tip", dataType: "text" },
  { key: "topFootwearTypeSharePct", header: "Udeo tipa %", dataType: "percent" },
  { key: "trendPct", header: "Trend %", dataType: "percent" },
  { key: "status", header: "Preporuka", dataType: "text" },
  { key: "confidencePct", header: "Poverenje %", dataType: "percent" },
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

type StatusTooltipData = { status: DecisionStatus; statusReason: string; sharePct: number; trendPct: number; topFootwearType: string; topFootwearTypeSharePct: number; reliabilityPct: number; confidencePct: number };

function buildStatusTooltip(data: StatusTooltipData): string {
  return `${statusDisplayLabel(data.status)}: ${data.statusReason} | Udeo ${fmtPct(data.sharePct, 1)} | Trend ${fmtSignedPct(data.trendPct, 1)} | Tip ${data.topFootwearType} (${fmtPct(data.topFootwearTypeSharePct, 1)}) | Pouzdanost ${fmtPct(data.reliabilityPct, 0)} | Poverenje ${fmtPct(data.confidencePct, 0)}`;
}
function normalizeName(value: string | null | undefined): string { return (value ?? "").trim().toUpperCase(); }
function vendorKey(vendor: { vendorId: number | null; vendorName: string }): string { if (vendor.vendorId != null) return `id:${vendor.vendorId}`; return `name:${normalizeName(vendor.vendorName)}`; }

function buildTypeInsights(articleStats: VendorSalesNivelacijaArticleStat[]) {
  const vendorCategoryRevenue = new Map<string, Map<string, number>>();
  const vendorCategoryElasticities = new Map<string, Map<string, number[]>>();
  const globalCategoryRevenue = new Map<string, number>();

  articleStats.forEach((row) => {
    const vKey = row.vendorId != null ? `id:${row.vendorId}` : `name:${normalizeName(row.vendorName)}`;
    const category = (row.category ?? "").trim() || "N/A";
    const revenue = Number.isFinite(row.postRevenue) ? row.postRevenue : 0;
    if (!vendorCategoryRevenue.has(vKey)) vendorCategoryRevenue.set(vKey, new Map());
    const categoryMap = vendorCategoryRevenue.get(vKey)!;
    categoryMap.set(category, (categoryMap.get(category) ?? 0) + revenue);
    if (!vendorCategoryElasticities.has(vKey)) vendorCategoryElasticities.set(vKey, new Map());
    const elasticityMap = vendorCategoryElasticities.get(vKey)!;
    if (!elasticityMap.has(category)) elasticityMap.set(category, []);
    if (row.priceElasticity != null && Number.isFinite(Number(row.priceElasticity))) elasticityMap.get(category)!.push(Number(row.priceElasticity));
    globalCategoryRevenue.set(category, (globalCategoryRevenue.get(category) ?? 0) + revenue);
  });

  const byVendor = new Map<string, { topType: string; topTypeSharePct: number; avgElasticity: number | null }>();
  vendorCategoryRevenue.forEach((categoryMap, key) => {
    let total = 0;
    let topType = "N/A";
    let topRevenue = 0;
    categoryMap.forEach((value, category) => { total += value; if (value > topRevenue) { topRevenue = value; topType = category; } });
    const topTypeSharePct = total > 0 ? (topRevenue / total) * 100 : 0;
    const categoryElasticities = vendorCategoryElasticities.get(key)?.get(topType) ?? [];
    const avgElasticity = categoryElasticities.length > 0 ? categoryElasticities.reduce((sum, value) => sum + value, 0) / categoryElasticities.length : null;
    byVendor.set(key, { topType, topTypeSharePct, avgElasticity });
  });

  const globalTopTypes = [...globalCategoryRevenue.entries()].sort((a, b) => b[1] - a[1]).slice(0, 8);
  const globalTotal = globalTopTypes.reduce((sum, item) => sum + item[1], 0);
  const globalTypeShare = globalTopTypes.map(([name, revenue]) => ({ name, sharePct: globalTotal > 0 ? (revenue / globalTotal) * 100 : 0 }));
  return { byVendor, globalTypeShare };
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

  if ((data.dataQuality.analyzedRows ?? 0) === 0) {
    return "insufficient_data";
  }

  if ((data.dataQuality.inactiveRows ?? 0) > 0 || (data.dataQuality.lowPostCoverageRows ?? 0) > 0) {
    return "warning";
  }

  return "good";
}

export default function SupplierFootwearAnalyticsPage({
  embedded = false,
  sharedFilters,
  onTrustMetadataChange,
}: SupplierEmbeddedPageProps = {}) {
  const navigate = useNavigate();
  const location = useLocation();
  const requestIdRef = useRef(0);
  const initialRange = useMemo(() => getPresetRange("30d"), []);

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
    dataScope: sharedFilters?.dataScope ?? null,
  });

  const [vendors, setVendors] = useState<Dobavljac[]>([]);
  const [data, setData] = useState<VendorSalesNivelacijaResponse | null>(null);
  const [previousRevenue, setPreviousRevenue] = useState<number | null>(null);
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
        dataScope: sharedFilters.dataScope,
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
    try {
      const currentRange = toUtcRange(filters.fromDate, filters.toDate);
      const previousRange = buildPreviousRange(filters.fromDate, filters.toDate);

      const [currentResult, previousResult] = await Promise.allSettled([
        getVendorSalesNivelacija({ ...currentRange, vendorId: filters.vendorId, category: filters.category || null, includeInactive: false, storeId: filters.storeId, dataScope: filters.dataScope }),
        getVendorSalesNivelacija({ ...previousRange, vendorId: filters.vendorId, category: filters.category || null, includeInactive: false, storeId: filters.storeId, dataScope: filters.dataScope }),
      ]);
      if (requestId !== requestIdRef.current) return;
      if (currentResult.status === "rejected") throw currentResult.reason;

      let currentData = currentResult.value;
      let previousData = previousResult.status === "fulfilled" ? previousResult.value : null;

      const hasNoRows = currentData.vendorStats.length === 0 && currentData.articleStats.length === 0;
      const likelyFilteredOutByInactive = hasNoRows && currentData.dataQuality.deduplicatedRows > 0 && currentData.dataQuality.inactiveRows > 0;

      const stillNoRows = currentData.vendorStats.length === 0 && currentData.articleStats.length === 0;
      if (stillNoRows) {
        const options = await getVendorSalesNivelacijaOptions({
          vendorId: filters.vendorId,
          category: filters.category || undefined,
          storeId: filters.storeId,
          dataScope: filters.dataScope,
          take: 60,
        }).catch(() => [] as VendorSalesNivelacijaOption[]);

        if (requestId !== requestIdRef.current) return;

        const suggested = options.find((item) => item.hasSalesWindow) ?? options[0];
        if (suggested) {
          const day = toDateOnly(suggested.eventDate);
          setSuggestedRange({
            fromDate: day,
            toDate: day,
            label: suggested.label,
          });
          setDataHint("Za izabrani period nema analiziranih redova. Predlozen je datum gde postoje nivelacije i/ili prodaja.");
        } else if (likelyFilteredOutByInactive) {
          setDataHint("U periodu postoje nivelacije, ali bez prodaje u pre/post prozoru. Ukljuci siri period ili proveri opciju sa neaktivnim artiklima.");
        } else {
          setDataHint("U izabranom periodu nema nivelacija za zadate filtere.");
        }
      }

      setData(currentData);
      setExpandedVendorKey(null);
      setPreviousRevenue(previousData?.totals.postRevenue ?? null);
    } catch (reason) {
      if (requestId !== requestIdRef.current) return;
      setData(null);
      setPreviousRevenue(null);
      setDataHint(null);
      setSuggestedRange(null);
      setError(reason instanceof Error ? reason.message : "Greška pri učitavanju analize dobavljača i tipova obuće.");
    } finally {
      if (requestId === requestIdRef.current) setLoading(false);
    }
  }, []);

  useEffect(() => { void load(activeFilters); }, [activeFilters, load]);

  const typeInsights = useMemo(() => buildTypeInsights(data?.articleStats ?? []), [data?.articleStats]);

  const decisionRows = useMemo<DecisionVendor[]>(() => {
    const rows = data?.vendorStats ?? [];
    if (rows.length === 0) return [];

    const totalRevenue = rows.reduce((sum, item) => sum + item.postRevenue, 0);
    return rows.flatMap((item) => {
      const recommendation = item.recommendation;
      if (!recommendation) return [];

      const key = vendorKey(item);
      const typeInsight = typeInsights.byVendor.get(key);
      const sharePct = totalRevenue > 0 ? (item.postRevenue / totalRevenue) * 100 : 0;
      const trendPct = item.changePercent;

      const topFootwearType = typeInsight?.topType ?? "N/A";
      const topFootwearTypeSharePct = typeInsight?.topTypeSharePct ?? 0;
      const avgElasticity = typeInsight?.avgElasticity ?? null;

      return [{
        ...item,
        sharePct,
        trendPct,
        topFootwearType,
        topFootwearTypeSharePct,
        avgElasticity,
        confidencePct: recommendation.confidencePct,
        status: recommendation.status,
        statusReason: recommendation.summary,
      }];
    });
  }, [data?.vendorStats, typeInsights.byVendor]);

  const sortedRows = useMemo(() => {
    const rows = [...decisionRows];
    return rows.sort((a, b) => {
      let compare = 0;
      if (sortField === "vendorName") compare = a.vendorName.localeCompare(b.vendorName, "sr");
      else if (sortField === "postRevenue") compare = a.postRevenue - b.postRevenue;
      else if (sortField === "sharePct") compare = a.sharePct - b.sharePct;
      else if (sortField === "topFootwearType") compare = a.topFootwearType.localeCompare(b.topFootwearType, "sr");
      else if (sortField === "trendPct") compare = a.trendPct - b.trendPct;
      else if (sortField === "status") compare = STATUS_PRIORITY[a.status] - STATUS_PRIORITY[b.status];
      if (compare === 0) compare = a.confidencePct - b.confidencePct;
      if (compare === 0) compare = a.reliabilityPct - b.reliabilityPct;
      return sortDir === "asc" ? compare : -compare;
    });
  }, [decisionRows, sortDir, sortField]);

  const totalRevenue = data?.totals.postRevenue ?? 0;
  const top5SharePct = useMemo(() => {
    if (sortedRows.length === 0 || totalRevenue <= 0) return 0;
    const top5 = [...sortedRows].sort((a, b) => b.postRevenue - a.postRevenue).slice(0, 5).reduce((sum, item) => sum + item.postRevenue, 0);
    return (top5 / totalRevenue) * 100;
  }, [sortedRows, totalRevenue]);
  const totalChangeRevenue = data?.totals.changeRevenue ?? 0;
  const periodGrowthPct = useMemo(() => (
    previousRevenue == null || previousRevenue <= 0
      ? data?.totals.changePercent ?? null
      : ((totalRevenue - previousRevenue) / previousRevenue) * 100
  ), [data?.totals.changePercent, previousRevenue, totalRevenue]);
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
  const selectedRow = useMemo(() => (!expandedVendorKey ? null : sortedRows.find((row) => vendorKey(row) === expandedVendorKey) ?? null), [expandedVendorKey, sortedRows]);
  const dataMeta = data?.meta ?? null;
  const dataMetaMessage = getAnalyticsMetaMessage(dataMeta);
  const showMetaWarning = !loading && !error && isAnalyticsMetaWarning(dataMeta);
  const showEmptyState = !loading && !error && ((data?.vendorStats.length ?? 0) === 0 && (data?.articleStats.length ?? 0) === 0);
  const dataQualityStatus = useMemo(() => getDataQualityStatus(data), [data]);
  const recommendationAllowed = dataQualityStatus === "good" || dataQualityStatus === "warning";
  const generatedAt = data?.generatedAt ?? null;
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
    { key: "dataScope", label: "Opseg podataka", value: activeFilters.dataScope ?? "" },
  ], [activeFilters.category, activeFilters.dataScope, activeFilters.fromDate, activeFilters.storeId, activeFilters.toDate, activeFilters.vendorId, periodPreset]);

  const toolbarMetadata = useMemo<AnalyticsNamedValue[]>(() => [
    { key: "generatedAt", label: "Generisano", value: data?.generatedAt ?? "" },
    { key: "vendorsCount", label: "Dobavljača", value: data?.totals.vendorsCount ?? 0 },
    { key: "articlesCount", label: "Artikala", value: data?.totals.articlesCount ?? 0 },
    { key: "windowDays", label: "Prozor (dani)", value: data?.windowDays ?? 0 },
  ], [data?.generatedAt, data?.totals.articlesCount, data?.totals.vendorsCount, data?.windowDays]);

  useEffect(() => {
    if (!embedded || !onTrustMetadataChange) return;

    if (!data) {
      onTrustMetadataChange(null);
      return;
    }

    onTrustMetadataChange({
      periodFrom: activeFilters.fromDate,
      periodTo: activeFilters.toDate,
      lastRefreshAt: data.meta?.lastRefreshAtUtc ?? data.generatedAt ?? null,
      dataFreshnessStatus: data.meta?.isPartial || data.meta?.warningCode ? "stale" : data.generatedAt ? "fresh" : "unknown",
      dataSource: "Supplier sales nivelacija po dobavljaču i tipu obuće",
      dataQualityStatus: dataQualityStatus ?? (showMetaWarning ? "warning" : "good"),
      recommendationAllowed,
      recommendationNote: "Asortiman je analitički signal. Finalna preporuka ostaje u centralnom dobavljačkom pregledu.",
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
  const handleApplyFilters = () => { if (!invalidRange) setActiveFilters({ fromDate, toDate, vendorId, category, storeId: sharedFilters?.storeId ?? null, dataScope: sharedFilters?.dataScope ?? null }); };
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
      dataScope: sharedFilters?.dataScope ?? null,
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
      recordId: String(row.vendorId ?? row.vendorName),
      title: row.vendorName,
      subtitle: "Podrska odluci po dobavljacu i tipu obuce",
      columns: decisionColumns,
      row,
      metadata: [...toolbarFilters, ...toolbarMetadata],
    }));
    navigate(`/analitika/dobavljaci-tipovi-obuce/${encodeURIComponent(String(row.vendorId ?? row.vendorName))}`, { state: { backgroundLocation: location } });
  };

  return (
    <div className={`sf-decision-page ${embedded ? "sf-decision-page--embedded" : ""}`}>
      {!embedded ? (
        <AnalyticsTrustHeader
          title="Dobavljači i tipovi obuće"
          description="Ovaj ekran prikazuje dodatni analitički signal za dobavljače i dominantne tipove obuće, uz isti period i kvalitet podataka koji koristi i konsolidovani dobavljački pregled."
          periodFrom={activeFilters.fromDate}
          periodTo={activeFilters.toDate}
          lastRefreshAt={data?.meta?.lastRefreshAtUtc ?? data?.generatedAt ?? null}
          dataFreshnessStatus={showMetaWarning || dataMeta?.isPartial ? "stale" : data?.generatedAt ? "fresh" : "unknown"}
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

      {false ? (
      <header className="sf-decision-header">
        <div>
          <h1 className="sf-decision-title">Dobavljači i tipovi obuće</h1>
          <p className="sf-decision-subtitle">Ekran za podrsku odluci koji spaja dobavljaca i dominantan tip obuce, da se brzo vidi gde je najveci promet, koji tip nosi rezultat i gde treba pojacati fokus.</p>
        </div>
        <div className="sf-decision-generated">Generisano: {generatedAt ? new Date(generatedAt ?? "").toLocaleString("sr-RS") : "-"}</div>
      </header>
      ) : null}

      {false ? (
      <section className="sf-decision-filters">
        <label className="sf-decision-field"><span>Period</span><select value={periodPreset} onChange={(e) => handlePresetChange(e.target.value as PeriodPreset)}><option value="30d">Poslednjih 30 dana</option><option value="90d">Poslednjih 90 dana</option><option value="180d">Poslednjih 180 dana</option><option value="365d">Poslednjih 365 dana</option><option value="custom">Prilagođeno</option></select></label>
        <label className="sf-decision-field"><span>Od</span><input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} /></label>
        <label className="sf-decision-field"><span>Do</span><input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} /></label>
        <label className="sf-decision-field"><span>Dobavljač</span><select value={vendorId ?? ""} onChange={(e) => setVendorId(e.target.value ? Number(e.target.value) : null)}><option value="">Svi</option>{vendors.map((vendor) => <option key={vendor.id} value={vendor.id}>{vendor.naziv}</option>)}</select></label>
        <label className="sf-decision-field"><span>Kategorija</span><select value={category} onChange={(e) => setCategory(e.target.value)}><option value="">Sve</option>{(data?.categories ?? []).map((item) => <option key={item} value={item}>{item}</option>)}</select></label>
        <div className="sf-decision-actions"><button type="button" onClick={handleApplyFilters} disabled={loading || invalidRange}>Primeni</button><button type="button" className="secondary" onClick={handleResetFilters} disabled={loading}>Reset</button></div>
      </section>
      ) : null}

      {invalidRange ? <div className="sf-decision-message error" role="alert">Datum 'od' ne može biti posle datuma 'do'.</div> : null}
      {error ? <div className="sf-decision-message error" role="alert">{error}</div> : null}
      {loading ? <div className="sf-decision-message loading" role="status" aria-live="polite">Učitavam dobavljače i tipove obuće...</div> : null}
      {!loading && !error && dataHint ? <div className="sf-decision-message info" role="status" aria-live="polite">{dataHint}</div> : null}
      {!loading && !error && suggestedRange ? (
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
                {data.dataQuality.rawRows === 0
                  ? "U izabranom periodu nema evidentirane nivelacije u Dnevniku promena. Pokušajte sa drugačijim periodom ili dobavljačem."
                  : data.dataQuality.analyzedRows === 0 && data.dataQuality.inactiveRows > 0
                    ? `Nivelacije postoje (${data.dataQuality.rawRows} redova), ali bez prodaje u 30-dnevnom post-prozoru. Pokušajte sa periodima gde postoji prodajna aktivnost.`
                    : `Analizirano je ${data.dataQuality.analyzedRows} redova, ali bez detektovanog prometa. Proverite filtere ili proširite vremenski raspon.`}
              </p>
            </div>
          )}

          <section className="sf-decision-kpis">
            <article className="sf-decision-kpi analytics-kpi-card analytics-kpi-card--tone-info" data-note="Promet svih dobavljača u izabranom periodu."><span>Ukupan promet</span><strong>{fmtRsd(totalRevenue)}</strong></article>
            <article className="sf-decision-kpi analytics-kpi-card analytics-kpi-card--tone-success" data-note="Koliki deo prometa drzi pet najjacih dobavljaca."><span>Udeo top 5 dobavljaca</span><strong>{fmtPct(top5SharePct)}</strong></article>
            <article className="sf-decision-kpi analytics-kpi-card analytics-kpi-card--tone-neutral" data-note="Apsolutna promena prometa u odnosu na pre period."><span>Ukupna promena prometa</span><strong className={trendClass(totalChangeRevenue)}>{fmtRsd(totalChangeRevenue)}</strong></article>
            <article className="sf-decision-kpi analytics-kpi-card analytics-kpi-card--tone-warning" data-note="Relativna promena prema prethodnom uporedivom periodu."><span>Rast/pad u odnosu na prethodni period</span><strong className={trendClass(periodGrowthPct)}>{fmtSignedPct(periodGrowthPct)}</strong></article>
            <article className="sf-decision-kpi analytics-kpi-card analytics-kpi-card--tone-value" data-note="Tip obuce koji trenutno nosi najveci deo prometa."><span>Dominantan tip obuce</span><strong>{dominantTypeSummary}</strong></article>
          </section>

          <section className="sf-decision-panels">
            <article className="sf-decision-card analytics-surface-panel">
              <h2>Koncentracija po tipu obuce</h2><p>Top tipovi obuce po udelu prometa u trenutnom filtru.</p>
              {typeInsights.globalTypeShare.length > 0 ? (
                <div className="sf-decision-chart-wrap">
                  <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={260}>
                    <BarChart data={typeInsights.globalTypeShare} layout="vertical" margin={{ top: 12, right: 16, left: 8, bottom: 8 }}>
                      <CartesianGrid strokeDasharray="3 3" stroke="var(--border-default)" />
                      <XAxis type="number" tick={{ fill: "var(--text-secondary)", fontSize: 12 }} unit="%" />
                      <YAxis type="category" dataKey="name" width={180} tick={{ fill: "var(--text-primary)", fontSize: 12 }} />
                      <Tooltip formatter={(value: number | string | undefined) => `${fmtPct(Number(value ?? 0), 2)}`} />
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
                        const rowId = vendorKey(row); const expanded = expandedVendorKey === rowId;
                        return (
                          <tr key={rowId} className={expanded ? "expanded-row" : ""}>
                            <td>{row.vendorName || "Nepoznat dobavljač"}</td>
                            <td className="align-right">{fmtRsd(row.postRevenue)}</td>
                            <td className="align-right">{fmtPct(row.sharePct, 2)}</td>
                            <td><strong>{row.topFootwearType}</strong><div className="sf-mini-note">{fmtPct(row.topFootwearTypeSharePct, 1)} udela kod dobavljača</div></td>
                            <td className={`align-right ${trendClass(row.trendPct)}`}>{fmtSignedPct(row.trendPct, 2)}</td>
                            <td><span className={statusClass(row.status)} title={buildStatusTooltip(row)} aria-label={buildStatusTooltip(row)}>{statusDisplayLabel(row.status)}</span></td>
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
                <article className="analytics-kpi-card analytics-kpi-card--tone-neutral"><span>Pre nivelacije promet</span><strong>{fmtRsd(selectedRow.preRevenue)}</strong></article>
                <article className="analytics-kpi-card analytics-kpi-card--tone-info"><span>Posle nivelacije promet</span><strong>{fmtRsd(selectedRow.postRevenue)}</strong></article>
                <article className="analytics-kpi-card analytics-kpi-card--tone-neutral"><span>Pre nivo kolicina</span><strong>{fmtQty(selectedRow.preQty)}</strong></article>
                <article className="analytics-kpi-card analytics-kpi-card--tone-success"><span>Posle nivo kolicina</span><strong>{fmtQty(selectedRow.postQty)}</strong></article>
                <article className="analytics-kpi-card analytics-kpi-card--tone-info"><span>Glavni tip obuće</span><strong>{selectedRow.topFootwearType} ({fmtPct(selectedRow.topFootwearTypeSharePct, 1)})</strong></article>
                <article className="analytics-kpi-card analytics-kpi-card--tone-warning"><span>Elastičnost glavnog tipa</span><strong>{fmtElasticity(selectedRow.avgElasticity)}</strong></article>
                <article className="analytics-kpi-card analytics-kpi-card--tone-neutral"><span>Aktivni artikli</span><strong>{selectedRow.activeArticlesCount} / {selectedRow.articleCount}</strong></article>
                <article className="analytics-kpi-card analytics-kpi-card--tone-success"><span>Pouzdanost signala</span><strong>{fmtPct(selectedRow.reliabilityPct, 1)}</strong></article>
                <article className="analytics-kpi-card analytics-kpi-card--tone-value"><span>Poverenje preporuke</span><strong>{fmtPct(selectedRow.confidencePct, 1)}</strong></article>
              </div>
              <p className="sf-decision-reason"><strong>Razlog preporuke:</strong> {selectedRow.statusReason}</p>
            </section>
          ) : null}
        </>
      ) : null}
    </div>
  );
}

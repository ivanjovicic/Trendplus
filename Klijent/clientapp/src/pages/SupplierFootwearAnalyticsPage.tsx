import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import { getDobavljaci } from "../services/dobavljaciApi";
import { buildAnalyticsDetailSnapshot, saveAnalyticsDetailSnapshot } from "../services/analyticsTableState";
import {
  getVendorSalesNivelacija,
  getVendorSalesNivelacijaOptions,
  type VendorSalesNivelacijaOption,
  type VendorSalesNivelacijaArticleStat,
  type VendorSalesNivelacijaResponse,
  type VendorSalesNivelacijaVendorStat,
} from "../services/vendorSalesNivelacijaApi";
import type { Dobavljac } from "../types/Dobavljaci";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import "./SupplierFootwearAnalyticsPage.css";

type PeriodPreset = "30d" | "90d" | "180d" | "365d" | "custom";
type SortDir = "asc" | "desc";
type SortField = "vendorName" | "postRevenue" | "sharePct" | "topFootwearType" | "trendPct" | "status";
type DecisionStatus = "Pojacaj" | "Zadrzi" | "Smanji";

type ActiveFilters = { fromDate: string; toDate: string; vendorId: number | null; category: string };
type SuggestedRange = { fromDate: string; toDate: string; label: string };

type DecisionVendor = VendorSalesNivelacijaVendorStat & {
  sharePct: number;
  trendPct: number;
  reliabilityPct: number;
  topFootwearType: string;
  topFootwearTypeSharePct: number;
  avgElasticity: number | null;
  decisionScore: number;
  status: DecisionStatus;
  statusReason: string;
};

const STATUS_PRIORITY: Record<DecisionStatus, number> = { Pojacaj: 3, Zadrzi: 2, Smanji: 1 };
const BOOST_SCORE_THRESHOLD = 68;
const KEEP_SCORE_THRESHOLD = 43;
const BOOST_MIN_RELIABILITY_PCT = 40;
const UNKNOWN_SUPPLIERS = new Set(["", "N/A", "NEPOZNATO", "UNKNOWN", "UNKNOWN SUPPLIER"]);

const decisionColumns: AnalyticsTableColumn<DecisionVendor>[] = [
  { key: "vendorName", header: "Dobavljac", dataType: "text" },
  { key: "postRevenue", header: "Promet", dataType: "currency" },
  { key: "sharePct", header: "Udeo %", dataType: "percent" },
  { key: "topFootwearType", header: "Glavni tip", dataType: "text" },
  { key: "topFootwearTypeSharePct", header: "Udeo tipa %", dataType: "percent" },
  { key: "trendPct", header: "Trend %", dataType: "percent" },
  { key: "status", header: "Preporuka", dataType: "text" },
  { key: "decisionScore", header: "Decision score", dataType: "number" },
];

function clamp(value: number, min: number, max: number): number { return Math.max(min, Math.min(max, value)); }
function toDateInput(date: Date): string { return date.toISOString().slice(0, 10); }
function getPresetRange(preset: Exclude<PeriodPreset, "custom">) {
  const to = new Date();
  const from = new Date(to);
  if (preset === "30d") from.setDate(from.getDate() - 29);
  if (preset === "90d") from.setDate(from.getDate() - 89);
  if (preset === "180d") from.setDate(from.getDate() - 179);
  if (preset === "365d") from.setDate(from.getDate() - 364);
  return { fromDate: toDateInput(from), toDate: toDateInput(to) };
}
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
function fmtRsd(value: number): string { return `${value.toLocaleString("sr-RS", { minimumFractionDigits: 0, maximumFractionDigits: 0 })} RSD`; }
function fmtPct(value: number | null | undefined, digits = 1): string {
  if (value == null || Number.isNaN(value)) return "N/A";
  return `${value.toLocaleString("sr-RS", { minimumFractionDigits: digits, maximumFractionDigits: digits })}%`;
}
function fmtSignedPct(value: number | null | undefined, digits = 1): string {
  if (value == null || Number.isNaN(value)) return "N/A";
  return `${value > 0 ? "+" : ""}${fmtPct(value, digits)}`;
}
function fmtQty(value: number): string { return `${value.toLocaleString("sr-RS")} kom`; }
function fmtElasticity(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return "N/A";
  return value.toLocaleString("sr-RS", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}
function sortMarker(field: SortField, activeField: SortField, dir: SortDir): string { if (field !== activeField) return ""; return dir === "asc" ? " ^" : " v"; }
function statusClass(status: DecisionStatus): string {
  if (status === "Pojacaj") return "sf-decision-status status-boost";
  if (status === "Smanji") return "sf-decision-status status-reduce";
  return "sf-decision-status status-keep";
}
function trendClass(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return "trend-neutral";
  if (value > 0) return "trend-up";
  if (value < 0) return "trend-down";
  return "trend-neutral";
}

type StatusReasonSignals = { sharePct: number; avgShare: number; trendPct: number; topTypeSharePct: number; reliabilityPct: number };
type StatusTooltipData = { status: DecisionStatus; statusReason: string; sharePct: number; trendPct: number; topFootwearType: string; topFootwearTypeSharePct: number; reliabilityPct: number };

function buildStatusReason(status: DecisionStatus, signals: StatusReasonSignals): string {
  const lowReliability = signals.reliabilityPct < BOOST_MIN_RELIABILITY_PCT;
  const positiveTrend = signals.trendPct > 0;
  const negativeTrend = signals.trendPct < 0;
  const concentratedType = signals.topTypeSharePct >= 45;

  if (status === "Pojacaj") {
    if (lowReliability) return "Signal je dobar, ali je pouzdanost niska; potvrditi pre veceg ulaganja.";
    if (positiveTrend && concentratedType) return "Jak trend i dominantan tip obuce koji nosi rezultat.";
    if (signals.sharePct >= signals.avgShare) return "Stabilan udeo i zdrav signal po tipu obuce.";
    return "Dobar potencijal rasta uz kontrolisani portfolio tipova.";
  }
  if (status === "Zadrzi") {
    if (lowReliability) return "Niza pouzdanost podataka; odluku drzati konzervativnom dok se signal ne stabilizuje.";
    if (negativeTrend) return "Trend slabi; zadrzati uz pojacan nadzor tipova koji opadaju.";
    return "Stabilan rezultat bez dovoljno jakog signala za promenu prioriteta.";
  }
  if (negativeTrend) return "Pad trenda i slab signal po tipu; smanjiti fokus.";
  return "Nizak doprinos i slabija relevantnost tipova; kandidat za smanjenje fokusa.";
}

function buildStatusTooltip(data: StatusTooltipData): string {
  return `${data.status}: ${data.statusReason} | Udeo ${fmtPct(data.sharePct, 1)} | Trend ${fmtSignedPct(data.trendPct, 1)} | Tip ${data.topFootwearType} (${fmtPct(data.topFootwearTypeSharePct, 1)}) | Pouzdanost ${fmtPct(data.reliabilityPct, 0)}`;
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

export default function SupplierFootwearAnalyticsPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const requestIdRef = useRef(0);
  const initialRange = useMemo(() => getPresetRange("30d"), []);

  const [periodPreset, setPeriodPreset] = useState<PeriodPreset>("30d");
  const [fromDate, setFromDate] = useState(initialRange.fromDate);
  const [toDate, setToDate] = useState(initialRange.toDate);
  const [vendorId, setVendorId] = useState<number | null>(null);
  const [category, setCategory] = useState("");
  const [activeFilters, setActiveFilters] = useState<ActiveFilters>({ fromDate: initialRange.fromDate, toDate: initialRange.toDate, vendorId: null, category: "" });

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

  useEffect(() => {
    const loadVendors = async () => {
      try { setVendors(await getDobavljaci()); } catch { setVendors([]); }
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
        getVendorSalesNivelacija({ ...currentRange, vendorId: filters.vendorId, category: filters.category || null, includeInactive: false }),
        getVendorSalesNivelacija({ ...previousRange, vendorId: filters.vendorId, category: filters.category || null, includeInactive: false }),
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
      setError(reason instanceof Error ? reason.message : "Greska pri ucitavanju dobavljaci-tipovi analitike.");
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
    const topShare = rows.reduce((max, item) => Math.max(max, totalRevenue > 0 ? (item.postRevenue / totalRevenue) * 100 : 0), 0);
    const deltaValues = rows.map((item) => item.changeRevenue);
    const minDelta = Math.min(...deltaValues);
    const maxDelta = Math.max(...deltaValues);
    const deltaSpan = maxDelta - minDelta;
    const avgShare = rows.length > 0 ? 100 / rows.length : 0;

    return rows.map((item) => {
      const key = vendorKey(item);
      const typeInsight = typeInsights.byVendor.get(key);
      const sharePct = totalRevenue > 0 ? (item.postRevenue / totalRevenue) * 100 : 0;
      const trendPct = item.changePercent;
      const coveragePct = item.articleCount > 0 ? (item.activeArticlesCount / item.articleCount) * 100 : 0;
      const knownSupplier = !UNKNOWN_SUPPLIERS.has(normalizeName(item.vendorName));
      const reliabilityPct = clamp(coveragePct * 0.7 + (knownSupplier ? 30 : 0), 0, 100);

      const shareNorm = topShare > 0 ? clamp((sharePct / topShare) * 100, 0, 100) : 0;
      const deltaNorm = deltaSpan > 0 ? clamp(((item.changeRevenue - minDelta) / deltaSpan) * 100, 0, 100) : 50;
      const trendNorm = clamp(((trendPct + 50) / 100) * 100, 0, 100);
      const decisionScore = Math.round(shareNorm * 0.35 + deltaNorm * 0.30 + trendNorm * 0.20 + reliabilityPct * 0.15);

      let status: DecisionStatus = "Smanji";
      if (decisionScore >= BOOST_SCORE_THRESHOLD) status = "Pojacaj";
      else if (decisionScore >= KEEP_SCORE_THRESHOLD) status = "Zadrzi";
      if (reliabilityPct < BOOST_MIN_RELIABILITY_PCT && status === "Pojacaj") status = "Zadrzi";

      const topFootwearType = typeInsight?.topType ?? "N/A";
      const topFootwearTypeSharePct = typeInsight?.topTypeSharePct ?? 0;
      const avgElasticity = typeInsight?.avgElasticity ?? null;
      const statusReason = buildStatusReason(status, { sharePct, avgShare, trendPct, topTypeSharePct: topFootwearTypeSharePct, reliabilityPct });

      return {
        ...item,
        sharePct,
        trendPct,
        reliabilityPct,
        topFootwearType,
        topFootwearTypeSharePct,
        avgElasticity,
        decisionScore,
        status,
        statusReason,
      };
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
      if (compare === 0) compare = a.decisionScore - b.decisionScore;
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
    boost: sortedRows.filter((row) => row.status === "Pojacaj").length,
    keep: sortedRows.filter((row) => row.status === "Zadrzi").length,
    reduce: sortedRows.filter((row) => row.status === "Smanji").length,
  }), [sortedRows]);
  const selectedRow = useMemo(() => (!expandedVendorKey ? null : sortedRows.find((row) => vendorKey(row) === expandedVendorKey) ?? null), [expandedVendorKey, sortedRows]);

  const toolbarFilters = useMemo<AnalyticsNamedValue[]>(() => [
    { key: "periodPreset", label: "Period", value: periodPreset },
    { key: "fromDate", label: "Od", value: activeFilters.fromDate },
    { key: "toDate", label: "Do", value: activeFilters.toDate },
    { key: "vendorId", label: "Dobavljac", value: activeFilters.vendorId ?? "" },
    { key: "category", label: "Kategorija", value: activeFilters.category },
  ], [activeFilters.category, activeFilters.fromDate, activeFilters.toDate, activeFilters.vendorId, periodPreset]);

  const toolbarMetadata = useMemo<AnalyticsNamedValue[]>(() => [
    { key: "generatedAt", label: "Generisano", value: data?.generatedAt ?? "" },
    { key: "vendorsCount", label: "Dobavljaca", value: data?.totals.vendorsCount ?? 0 },
    { key: "articlesCount", label: "Artikala", value: data?.totals.articlesCount ?? 0 },
    { key: "windowDays", label: "Window", value: data?.windowDays ?? 0 },
  ], [data?.generatedAt, data?.totals.articlesCount, data?.totals.vendorsCount, data?.windowDays]);

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
  const handleApplyFilters = () => { if (!invalidRange) setActiveFilters({ fromDate, toDate, vendorId, category }); };
  const handleResetFilters = () => {
    const range = getPresetRange("30d");
    setPeriodPreset("30d");
    setFromDate(range.fromDate);
    setToDate(range.toDate);
    setVendorId(null);
    setCategory("");
    setActiveFilters({ fromDate: range.fromDate, toDate: range.toDate, vendorId: null, category: "" });
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
      subtitle: "Decision support po dobavljacu i tipu obuce",
      columns: decisionColumns,
      row,
      metadata: [...toolbarFilters, ...toolbarMetadata],
    }));
    navigate(`/analitika/dobavljaci-tipovi-obuce/${encodeURIComponent(String(row.vendorId ?? row.vendorName))}`, { state: { backgroundLocation: location } });
  };

  return (
    <div className="sf-decision-page">
      <header className="sf-decision-header">
        <div>
          <h1 className="sf-decision-title">Dobavljaci i Tipovi Obuce</h1>
          <p className="sf-decision-subtitle">Decision-support ekran koji spaja dobavljaca i dominantan tip obuce, da se brzo vidi gde je najveci promet, koji tip nosi rezultat i gde treba pojacati fokus.</p>
        </div>
        <div className="sf-decision-generated">Generisano: {data?.generatedAt ? new Date(data.generatedAt).toLocaleString("sr-RS") : "-"}</div>
      </header>

      <section className="sf-decision-filters">
        <label className="sf-decision-field"><span>Period</span><select value={periodPreset} onChange={(e) => handlePresetChange(e.target.value as PeriodPreset)}><option value="30d">Poslednjih 30 dana</option><option value="90d">Poslednjih 90 dana</option><option value="180d">Poslednjih 180 dana</option><option value="365d">Poslednjih 365 dana</option><option value="custom">Prilagođeno</option></select></label>
        <label className="sf-decision-field"><span>Od</span><input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} /></label>
        <label className="sf-decision-field"><span>Do</span><input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} /></label>
        <label className="sf-decision-field"><span>Dobavljac</span><select value={vendorId ?? ""} onChange={(e) => setVendorId(e.target.value ? Number(e.target.value) : null)}><option value="">Svi</option>{vendors.map((vendor) => <option key={vendor.id} value={vendor.id}>{vendor.naziv}</option>)}</select></label>
        <label className="sf-decision-field"><span>Kategorija</span><select value={category} onChange={(e) => setCategory(e.target.value)}><option value="">Sve</option>{(data?.categories ?? []).map((item) => <option key={item} value={item}>{item}</option>)}</select></label>
        <div className="sf-decision-actions"><button type="button" onClick={handleApplyFilters} disabled={loading || invalidRange}>Primeni</button><button type="button" className="secondary" onClick={handleResetFilters} disabled={loading}>Reset</button></div>
      </section>

      {invalidRange ? <div className="sf-decision-message error">Datum od ne moze biti posle datuma do.</div> : null}
      {error ? <div className="sf-decision-message error">{error}</div> : null}
      {loading ? <div className="sf-decision-message loading">Ucitavam dobavljace i tipove obuce...</div> : null}
      {!loading && !error && dataHint ? <div className="sf-decision-message info">{dataHint}</div> : null}
      {!loading && !error && suggestedRange ? (
        <div className="sf-decision-message suggestion">
          <span>Predlog: {suggestedRange.label}</span>
          <button type="button" onClick={handleApplySuggestedRange}>Primeni predlog perioda</button>
        </div>
      ) : null}

      {!loading && data ? (
        <>
          <section className="sf-decision-kpis">
            <article className="sf-decision-kpi analytics-kpi-card analytics-kpi-card--tone-info" data-note="Promet svih dobavljaca u izabranom periodu."><span>Ukupan promet</span><strong>{fmtRsd(totalRevenue)}</strong></article>
            <article className="sf-decision-kpi analytics-kpi-card analytics-kpi-card--tone-success" data-note="Koliki deo prometa drzi pet najjacih dobavljaca."><span>Udeo top 5 dobavljaca</span><strong>{fmtPct(top5SharePct)}</strong></article>
            <article className="sf-decision-kpi analytics-kpi-card analytics-kpi-card--tone-neutral" data-note="Apsolutna promena prometa u odnosu na pre period."><span>Ukupna promena prometa</span><strong className={trendClass(totalChangeRevenue)}>{fmtRsd(totalChangeRevenue)}</strong></article>
            <article className="sf-decision-kpi analytics-kpi-card analytics-kpi-card--tone-warning" data-note="Relativna promena prema prethodnom uporedivom periodu."><span>Rast/PAD vs prethodni period</span><strong className={trendClass(periodGrowthPct)}>{fmtSignedPct(periodGrowthPct)}</strong></article>
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
              <div className="sf-decision-table-head">
                <div><h2>Prioritetna lista dobavljaca</h2><p>Pojacaj: {vendorCounts.boost} | Zadrzi: {vendorCounts.keep} | Smanji: {vendorCounts.reduce}</p></div>
                <AnalyticsTableToolbar tableKey="dobavljaci-tipovi-obuce" tableTitle="Dobavljaci-tipovi decision support" columns={decisionColumns} rows={sortedRows} filters={toolbarFilters} metadata={toolbarMetadata} defaultOrientation="landscape" />
              </div>
              <div className="sf-decision-table-wrap">
                <table className="sf-decision-table">
                  <thead>
                    <tr>
                      <th><button type="button" onClick={() => handleSort("vendorName")}>Dobavljac{sortMarker("vendorName", sortField, sortDir)}</button></th>
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
                            <td>{row.vendorName || "Nepoznat dobavljac"}</td>
                            <td className="align-right">{fmtRsd(row.postRevenue)}</td>
                            <td className="align-right">{fmtPct(row.sharePct, 2)}</td>
                            <td><strong>{row.topFootwearType}</strong><div className="sf-mini-note">{fmtPct(row.topFootwearTypeSharePct, 1)} udela kod dobavljaca</div></td>
                            <td className={`align-right ${trendClass(row.trendPct)}`}>{fmtSignedPct(row.trendPct, 2)}</td>
                            <td><span className={statusClass(row.status)} title={buildStatusTooltip(row)} aria-label={buildStatusTooltip(row)}>{row.status}</span></td>
                            <td className="align-center"><button type="button" className="sf-decision-detail-btn" onClick={() => setExpandedVendorKey(expanded ? null : rowId)}>{expanded ? "Sakrij" : "Detalji"}</button></td>
                          </tr>
                        );
                      })
                    )}
                  </tbody>
                </table>
              </div>
            </article>
          </section>

          {selectedRow ? (
            <section className="sf-decision-detail">
              <div className="sf-decision-detail-head"><h3>Detalj odluke: {selectedRow.vendorName || "Nepoznat dobavljac"}</h3><button type="button" onClick={() => openVendorDetail(selectedRow)}>Otvori puni detalj</button></div>
              <div className="sf-decision-detail-grid">
                <article className="analytics-kpi-card analytics-kpi-card--tone-neutral"><span>Pre nivelacije promet</span><strong>{fmtRsd(selectedRow.preRevenue)}</strong></article>
                <article className="analytics-kpi-card analytics-kpi-card--tone-info"><span>Posle nivelacije promet</span><strong>{fmtRsd(selectedRow.postRevenue)}</strong></article>
                <article className="analytics-kpi-card analytics-kpi-card--tone-neutral"><span>Pre nivo kolicina</span><strong>{fmtQty(selectedRow.preQty)}</strong></article>
                <article className="analytics-kpi-card analytics-kpi-card--tone-success"><span>Posle nivo kolicina</span><strong>{fmtQty(selectedRow.postQty)}</strong></article>
                <article className="analytics-kpi-card analytics-kpi-card--tone-info"><span>Glavni tip obuce</span><strong>{selectedRow.topFootwearType} ({fmtPct(selectedRow.topFootwearTypeSharePct, 1)})</strong></article>
                <article className="analytics-kpi-card analytics-kpi-card--tone-warning"><span>Elasticnost glavnog tipa</span><strong>{fmtElasticity(selectedRow.avgElasticity)}</strong></article>
                <article className="analytics-kpi-card analytics-kpi-card--tone-neutral"><span>Aktivni artikli</span><strong>{selectedRow.activeArticlesCount} / {selectedRow.articleCount}</strong></article>
                <article className="analytics-kpi-card analytics-kpi-card--tone-success"><span>Pouzdanost signala</span><strong>{fmtPct(selectedRow.reliabilityPct, 1)}</strong></article>
                <article className="analytics-kpi-card analytics-kpi-card--tone-value"><span>Decision score</span><strong>{selectedRow.decisionScore}</strong></article>
              </div>
              <p className="sf-decision-reason"><strong>Razlog preporuke:</strong> {selectedRow.statusReason}</p>
            </section>
          ) : null}
        </>
      ) : null}
    </div>
  );
}

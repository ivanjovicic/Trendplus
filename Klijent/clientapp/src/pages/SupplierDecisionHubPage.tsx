import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import {
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import InfoTip from "../components/ui/InfoTip";
import { getSezone } from "../services/sezoneApi";
import { buildAnalyticsDetailSnapshot, saveAnalyticsDetailSnapshot } from "../services/analyticsTableState";
import {
  getAllSupplierDecisionRanking,
  getSupplierDecisionSummary,
  type RankingItem,
  type RankingResponse,
  type SummaryResponse,
  type SupplierDecisionHubFilters,
} from "../services/supplierDecisionHubApi";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import type { Sezona } from "../types/Sezona";
import { fmtPct, fmtRsd, fmtSignedPct, getPresetRange } from "../utils/analyticsFormatters";
import { CHART_TOOLTIP_LABEL_STYLE, CHART_TOOLTIP_STYLE } from "../utils/chartTooltipStyle";
import {
  RECOMMENDATION_SIGNAL_UNAVAILABLE,
  normalizeRecommendationPct,
  normalizeRecommendationQualityStatus,
  recommendationQualityLabel,
  recommendationQualityStyle,
  recommendationReasonHints,
  type RecommendationQualityStatus,
} from "../utils/canonicalRecommendationSemantics";
import type { SupplierEmbeddedPageProps } from "./supplierSharedState";
import "./SupplierDecisionHubPage.css";

type PeriodPreset = "30d" | "90d" | "180d" | "365d" | "custom";
type SortDir = "asc" | "desc";
type SortField = "supplierName" | "revenue" | "sharePct" | "preMarkdownMarginPct" | "qualityTrendPct" | "status";
type DecisionStatus = "Pojacaj" | "Zadrzi" | "Smanji";

type ActiveFilters = {
  fromDate: string;
  toDate: string;
  seasonId: number | null;
  minRevenue: number | null;
  onlyHighConfidence: boolean;
  supplierId: number | null;
  storeId: number | null;
  dataScope: string | null;
};

type DecisionRow = RankingItem & {
  sharePct: number;
  marginContribution: number;
  qualityTrendPct: number;
  decisionScore: number;
  status: DecisionStatus;
  statusReason: string;
  normalizedConfidence: number;
  confidenceAvailable: boolean;
  reliabilityPct: number;
  reliabilityAvailable: boolean;
  dataQualityStatus: RecommendationQualityStatus;
  reasonCodes: string[];
};

const STATUS_PRIORITY: Record<DecisionStatus, number> = {
  Pojacaj: 3,
  Zadrzi: 2,
  Smanji: 1,
};
const BOOST_MIN_CONFIDENCE_PCT = 55;

const decisionColumns: AnalyticsTableColumn<DecisionRow>[] = [
  { key: "supplierName", header: "DobavljaÄ", dataType: "text" },
  { key: "revenue", header: "Prihod", dataType: "currency" },
  { key: "sharePct", header: "Udeo %", dataType: "percent" },
  { key: "preMarkdownMarginPct", header: "MarÅ¾a %", dataType: "percent" },
  { key: "qualityTrendPct", header: "Trend pune cene %", dataType: "percent" },
  { key: "status", header: "Preporuka", dataType: "text" },
  { key: "decisionScore", header: "Skor odluke", dataType: "number" },
];

function clamp(value: number, min: number, max: number): number { return Math.max(min, Math.min(max, value)); }
function sortMarker(field: SortField, activeField: SortField, dir: SortDir): string { if (field !== activeField) return ""; return dir === "asc" ? " ^" : " v"; }
function statusClass(status: DecisionStatus): string {
  if (status === "Pojacaj") return "sdh-decision-status status-boost";
  if (status === "Smanji") return "sdh-decision-status status-reduce";
  return "sdh-decision-status status-keep";
}
function statusDisplayLabel(status: DecisionStatus): string {
  if (status === "Pojacaj") return "PojaÄaj";
  if (status === "Smanji") return "Smanji";
  return "ZadrÅ¾i";
}
function trendClass(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return "trend-neutral";
  if (value > 0) return "trend-up";
  if (value < 0) return "trend-down";
  return "trend-neutral";
}
function buildPreviousRange(fromDate: string, toDate: string): { fromDate: string; toDate: string } {
  const currentFrom = new Date(`${fromDate}T00:00:00Z`);
  const currentTo = new Date(`${toDate}T23:59:59Z`);
  const durationMs = currentTo.getTime() - currentFrom.getTime() + 1000;
  const previousTo = new Date(currentFrom.getTime() - 1000);
  const previousFrom = new Date(previousTo.getTime() - durationMs + 1000);
  return { fromDate: previousFrom.toISOString().slice(0, 10), toDate: previousTo.toISOString().slice(0, 10) };
}

function recommendationToStatus(code: string): DecisionStatus {
  if (code === "EXPAND" || code === "EXPAND_SELECTIVELY") return "Pojacaj";
  if (code === "ASSORTMENT_REDUCE" || code === "PRICE_NEGOTIATE") return "Smanji";
  return "Zadrzi";
}

function buildStatusReason(status: DecisionStatus, code: string, qualityTrendPct: number, confidence: number): string {
  const lowConfidence = confidence < BOOST_MIN_CONFIDENCE_PCT;
  if (status === "Pojacaj") {
    if (lowConfidence) return "Signal za rast postoji, ali je pouzdanost graniÄna; Å¡iri postepeno.";
    if (code === "EXPAND" || code === "EXPAND_SELECTIVELY") return "DobavljaÄ drÅ¾i zdrav prodajni signal bez preterane zavisnosti od nivelacija.";
    return "Pozitivan zbirni signal za veÄ‡i fokus.";
  }
  if (status === "Zadrzi") {
    if (lowConfidence) return "NiÅ¾a pouzdanost podataka; odluku drÅ¾ati konzervativnom dok se signal ne stabilizuje.";
    if (qualityTrendPct < 0) return "Signal kvaliteta slabi; zadrÅ¾ati uz pojaÄan nadzor.";
    return "Stabilan signal bez jasnog razloga za promenu prioriteta.";
  }
  if (code === "ASSORTMENT_REDUCE") return "Visoka zavisnost od nivelacija i rizik neaktivne zalihe â€” smanjiti fokus u nabavci.";
  if (code === "PRICE_NEGOTIATE") return "Potreban je bolji cenovni ulaz â€” pregovoriti nabavne cene pre daljeg proÅ¡irivanja asortimana.";
  return "Nizak signal doprinosa i rizik po profitabilnost.";
}

function buildStatusTooltip(row: DecisionRow): string {
  const confidenceText = row.confidenceAvailable ? fmtPct(row.normalizedConfidence, 0) : RECOMMENDATION_SIGNAL_UNAVAILABLE;
  const reliabilityText = row.reliabilityAvailable ? fmtPct(row.reliabilityPct, 0) : RECOMMENDATION_SIGNAL_UNAVAILABLE;
  const qualityText = recommendationQualityLabel(row.dataQualityStatus);
  const hintText = recommendationReasonHints(row.reasonCodes).join(" | ");
  return statusDisplayLabel(row.status)
    + ": " + row.statusReason
    + " | Udeo " + fmtPct(row.sharePct, 1)
    + " | MarÅ¾a " + fmtPct(row.preMarkdownMarginPct * 100, 1)
    + " | Trend pune cene " + fmtSignedPct(row.qualityTrendPct, 1)
    + " | Confidence " + confidenceText
    + " | Pouzdanost " + reliabilityText
    + " | Kvalitet " + qualityText
    + (hintText ? " | Napomene: " + hintText : "");
}

export default function SupplierDecisionHubPage({ embedded = false, sharedFilters }: SupplierEmbeddedPageProps = {}) {
  const navigate = useNavigate();
  const location = useLocation();
  const requestIdRef = useRef(0);
  const initialRange = useMemo(() => getPresetRange("30d"), []);

  const [periodPreset, setPeriodPreset] = useState<PeriodPreset>(sharedFilters?.periodPreset ?? "30d");
  const [fromDate, setFromDate] = useState(sharedFilters?.fromDate ?? initialRange.fromDate);
  const [toDate, setToDate] = useState(sharedFilters?.toDate ?? initialRange.toDate);
  const [seasonId, setSeasonId] = useState<number | null>(null);
  const [minRevenue, setMinRevenue] = useState<number | null>(null);
  const [onlyHighConfidence, setOnlyHighConfidence] = useState(false);
  const [activeFilters, setActiveFilters] = useState<ActiveFilters>({
    fromDate: sharedFilters?.fromDate ?? initialRange.fromDate,
    toDate: sharedFilters?.toDate ?? initialRange.toDate,
    seasonId: null,
    minRevenue: null,
    onlyHighConfidence: false,
    supplierId: sharedFilters?.supplierId ?? null,
    storeId: sharedFilters?.storeId ?? null,
    dataScope: sharedFilters?.dataScope ?? null,
  });

  const [seasons, setSeasons] = useState<Sezona[]>([]);
  const [summary, setSummary] = useState<SummaryResponse | null>(null);
  const [previousSummary, setPreviousSummary] = useState<SummaryResponse | null>(null);
  const [ranking, setRanking] = useState<RankingResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sortField, setSortField] = useState<SortField>("status");
  const [sortDir, setSortDir] = useState<SortDir>("desc");
  const [expandedSupplierId, setExpandedSupplierId] = useState<number | null>(null);

  const invalidRange = useMemo(() => (!fromDate || !toDate ? false : new Date(fromDate) > new Date(toDate)), [fromDate, toDate]);

  useEffect(() => {
    if (!sharedFilters) return;
    setPeriodPreset(sharedFilters.periodPreset);
    setFromDate(sharedFilters.fromDate);
    setToDate(sharedFilters.toDate);
    setActiveFilters((current) => {
      const next = {
        ...current,
        fromDate: sharedFilters.fromDate,
        toDate: sharedFilters.toDate,
        supplierId: sharedFilters.supplierId,
        storeId: sharedFilters.storeId,
        dataScope: sharedFilters.dataScope,
      };
      return current.fromDate === next.fromDate
        && current.toDate === next.toDate
        && current.supplierId === next.supplierId
        && current.storeId === next.storeId
        && current.dataScope === next.dataScope
        ? current
        : next;
    });
  }, [sharedFilters]);

  useEffect(() => {
    const loadSeasons = async () => {
      try { setSeasons(await getSezone()); } catch { setSeasons([]); }
    };
    void loadSeasons();
  }, []);

  const load = useCallback(async (filters: ActiveFilters) => {
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);
    try {
      const baseFilters: SupplierDecisionHubFilters = {
        fromDate: filters.fromDate,
        toDate: filters.toDate,
        seasonId: filters.seasonId ?? undefined,
        minRevenue: filters.minRevenue ?? undefined,
        onlyHighConfidence: filters.onlyHighConfidence,
        supplierId: filters.supplierId ?? undefined,
        storeId: filters.storeId,
        dataScope: filters.dataScope,
      };
      const prevRange = buildPreviousRange(filters.fromDate, filters.toDate);
      const prevFilters: SupplierDecisionHubFilters = { ...baseFilters, fromDate: prevRange.fromDate, toDate: prevRange.toDate };

      const [summaryResult, rankingResult, previousResult] = await Promise.allSettled([
        getSupplierDecisionSummary(baseFilters),
        getAllSupplierDecisionRanking(baseFilters, { pageSize: 100, sortBy: "supplierQualityIndex", sortDir: "desc" }),
        getSupplierDecisionSummary(prevFilters),
      ]);
      if (requestId !== requestIdRef.current) return;
      if (summaryResult.status === "rejected" || rankingResult.status === "rejected") throw new Error("NeuspeÅ¡no uÄitavanje podataka skorkarte dobavljaÄa.");
      setSummary(summaryResult.value);
      setRanking(rankingResult.value);
      setPreviousSummary(previousResult.status === "fulfilled" ? previousResult.value : null);
      setExpandedSupplierId(null);
    } catch (reason) {
      if (requestId !== requestIdRef.current) return;
      setSummary(null);
      setPreviousSummary(null);
      setRanking(null);
      setError(reason instanceof Error ? reason.message : "GreÅ¡ka pri uÄitavanju skorkarte dobavljaÄa.");
    } finally {
      if (requestId === requestIdRef.current) setLoading(false);
    }
  }, []);

  useEffect(() => { void load(activeFilters); }, [activeFilters, load]);

  const decisionRows = useMemo<DecisionRow[]>(() => {
    const rows = ranking?.items ?? [];
    if (rows.length === 0) return [];
    const totalRevenue = rows.reduce((sum, item) => sum + item.revenue, 0);
    const topShare = rows.reduce((max, item) => Math.max(max, totalRevenue > 0 ? (item.revenue / totalRevenue) * 100 : 0), 0);

    return rows.map((item) => {
      const sharePct = totalRevenue > 0 ? (item.revenue / totalRevenue) * 100 : 0;
      const marginContribution = item.revenue * item.preMarkdownMarginPct;
      const qualityTrendPct = (item.fullPriceRevenueShare - item.markdownRevenueShare) * 100;
      const confidencePctValue = normalizeRecommendationPct(item.confidenceScore);
      const normalizedConfidence = confidencePctValue ?? 0;
      const qualityIndex = clamp(item.supplierQualityIndex <= 1 ? item.supplierQualityIndex * 100 : item.supplierQualityIndex, 0, 100);
      const shareNorm = topShare > 0 ? clamp((sharePct / topShare) * 100, 0, 100) : 0;
      const trendNorm = clamp(((qualityTrendPct + 40) / 80) * 100, 0, 100);
      const decisionScore = Math.round(qualityIndex * 0.40 + normalizedConfidence * 0.25 + shareNorm * 0.20 + trendNorm * 0.15);

      const status = recommendationToStatus(item.recommendationCode);
      const statusReason = item.statusReason
        ?? buildStatusReason(status, item.recommendationCode, qualityTrendPct, normalizedConfidence);

      return {
        ...item,
        sharePct,
        marginContribution,
        qualityTrendPct,
        decisionScore,
        status,
        statusReason,
        normalizedConfidence,
        confidenceAvailable: confidencePctValue != null,
        reliabilityPct: normalizeRecommendationPct(item.reliabilityPct) ?? 0,
        reliabilityAvailable: normalizeRecommendationPct(item.reliabilityPct) != null,
        dataQualityStatus: normalizeRecommendationQualityStatus(item.dataQualityStatus),
        reasonCodes: item.reasonCodes ?? [],
      };
    });
  }, [ranking?.items]);

  const sortedRows = useMemo(() => {
    const rows = [...decisionRows];
    return rows.sort((a, b) => {
      let compare = 0;
      if (sortField === "supplierName") compare = a.supplierName.localeCompare(b.supplierName, "sr");
      else if (sortField === "revenue") compare = a.revenue - b.revenue;
      else if (sortField === "sharePct") compare = a.sharePct - b.sharePct;
      else if (sortField === "preMarkdownMarginPct") compare = a.preMarkdownMarginPct - b.preMarkdownMarginPct;
      else if (sortField === "qualityTrendPct") compare = a.qualityTrendPct - b.qualityTrendPct;
      else if (sortField === "status") compare = STATUS_PRIORITY[a.status] - STATUS_PRIORITY[b.status];
      if (compare === 0) compare = a.decisionScore - b.decisionScore;
      return sortDir === "asc" ? compare : -compare;
    });
  }, [decisionRows, sortDir, sortField]);

  const totalRevenue = useMemo(() => sortedRows.reduce((sum, row) => sum + row.revenue, 0), [sortedRows]);
  const top5SharePct = useMemo(() => {
    if (sortedRows.length === 0 || totalRevenue <= 0) return 0;
    const top5 = [...sortedRows].sort((a, b) => b.revenue - a.revenue).slice(0, 5).reduce((sum, row) => sum + row.revenue, 0);
    return (top5 / totalRevenue) * 100;
  }, [sortedRows, totalRevenue]);
  const totalMarginContribution = useMemo(() => sortedRows.reduce((sum, row) => sum + row.marginContribution, 0), [sortedRows]);
  const fullPriceDeltaPctPoints = useMemo(() => {
    if (!summary || !previousSummary) return null;
    return (summary.fullPriceRevenueShare - previousSummary.fullPriceRevenueShare) * 100;
  }, [previousSummary, summary]);
  const supplierCounts = useMemo(() => ({
    boost: sortedRows.filter((row) => row.status === "Pojacaj").length,
    keep: sortedRows.filter((row) => row.status === "Zadrzi").length,
    reduce: sortedRows.filter((row) => row.status === "Smanji").length,
  }), [sortedRows]);
  const zeroStateExplanation = useMemo(() => {
    if (!summary || !ranking) return null;
    const allKeyMetricsZero =
      totalRevenue === 0 &&
      totalMarginContribution === 0 &&
      (summary.capitalAtRisk ?? 0) === 0 &&
      top5SharePct === 0;

    if (!allKeyMetricsZero) return null;

    if (ranking.totalCount === 0 || summary.supplierCount === 0) {
      return "Skorkarta se puni iz dobavljaÄa koji imaju artikle sa prvom nivelacijom u izabranom periodu. Ako takvih zapisa nema, scorecard KPI-jevi ostaju na nuli iako Pregled moÅ¾e imati promet, jer Pregled koristi Å¡iri prodajni skup.";
    }

    return "Postoje zapisi za Skorkartu, ali su kljuÄni pokazatelji trenutno 0. Proveri period, objekat, dobavljaÄa i minimalni prihod; ako Pregled ima promet, a Skorkarta ostaje na nuli, potreban je refresh analytics scorecard podataka.";
  }, [ranking, summary, top5SharePct, totalMarginContribution, totalRevenue]);
  const concentrationData = useMemo(() => {
    const top = [...sortedRows].sort((a, b) => b.sharePct - a.sharePct).slice(0, 8).map((row) => ({ name: row.supplierName, sharePct: row.sharePct }));
    const topShare = top.reduce((sum, row) => sum + row.sharePct, 0);
    const rest = clamp(100 - topShare, 0, 100);
    return rest > 0.1 ? [...top, { name: "Ostali", sharePct: rest }] : top;
  }, [sortedRows]);
  const selectedRow = useMemo(() => (expandedSupplierId == null ? null : sortedRows.find((row) => row.supplierId === expandedSupplierId) ?? null), [expandedSupplierId, sortedRows]);

  const toolbarFilters = useMemo<AnalyticsNamedValue[]>(() => [
    { key: "periodPreset", label: "Period", value: periodPreset },
    { key: "fromDate", label: "Od", value: activeFilters.fromDate },
    { key: "toDate", label: "Do", value: activeFilters.toDate },
    { key: "seasonId", label: "Sezona", value: activeFilters.seasonId ?? "" },
    { key: "minRevenue", label: "Min prihod", value: activeFilters.minRevenue ?? "" },
    { key: "onlyHighConfidence", label: "Samo visoka pouzdanost", value: activeFilters.onlyHighConfidence },
    { key: "supplierId", label: "DobavljaÄ", value: activeFilters.supplierId ?? "" },
    { key: "storeId", label: "Objekat", value: activeFilters.storeId ?? "" },
    { key: "dataScope", label: "Opseg podataka", value: activeFilters.dataScope ?? "" },
  ], [activeFilters.dataScope, activeFilters.fromDate, activeFilters.minRevenue, activeFilters.onlyHighConfidence, activeFilters.seasonId, activeFilters.storeId, activeFilters.supplierId, activeFilters.toDate, periodPreset]);

  const toolbarMetadata = useMemo<AnalyticsNamedValue[]>(() => [
    { key: "summaryFrom", label: "SaÅ¾etak od", value: summary?.from ?? "" },
    { key: "summaryTo", label: "SaÅ¾etak do", value: summary?.to ?? "" },
    { key: "supplierCount", label: "DobavljaÄa", value: summary?.supplierCount ?? 0 },
    { key: "capitalAtRisk", label: "Kapital u riziku", value: summary?.capitalAtRisk ?? 0 },
  ], [summary?.capitalAtRisk, summary?.from, summary?.supplierCount, summary?.to]);

  const handleSort = (field: SortField) => {
    if (sortField === field) { setSortDir((current) => (current === "asc" ? "desc" : "asc")); return; }
    setSortField(field);
    setSortDir(field === "supplierName" ? "asc" : "desc");
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
        seasonId,
        minRevenue,
        onlyHighConfidence,
        supplierId: sharedFilters?.supplierId ?? null,
        storeId: sharedFilters?.storeId ?? null,
        dataScope: sharedFilters?.dataScope ?? null,
      });
    }
  };
  const handleResetFilters = () => {
    const range = getPresetRange("30d");
    setPeriodPreset("30d");
    setFromDate(range.fromDate);
    setToDate(range.toDate);
    setSeasonId(null);
    setMinRevenue(null);
    setOnlyHighConfidence(false);
    setActiveFilters({
      fromDate: sharedFilters?.fromDate ?? range.fromDate,
      toDate: sharedFilters?.toDate ?? range.toDate,
      seasonId: null,
      minRevenue: null,
      onlyHighConfidence: false,
      supplierId: sharedFilters?.supplierId ?? null,
      storeId: sharedFilters?.storeId ?? null,
      dataScope: sharedFilters?.dataScope ?? null,
    });
  };

  const openSupplierDetail = (row: DecisionRow) => {
    saveAnalyticsDetailSnapshot(buildAnalyticsDetailSnapshot({
      table: "supplier-decision-hub",
      recordId: String(row.supplierId),
      title: row.supplierName,
      subtitle: "PodrÅ¡ka odluci za dobavljaÄe",
      columns: decisionColumns,
      row,
      metadata: [...toolbarFilters, ...toolbarMetadata],
    }));
    navigate(`/analitika/supplier-decision-hub/${row.supplierId}`, { state: { backgroundLocation: location } });
  };

  return (
    <div className={`sdh-decision-page ${embedded ? "sdh-decision-page--embedded" : ""}`}>
      {!embedded ? (
      <header className="sdh-decision-header">
        <div>
          <h1 className="sdh-decision-title">Skorkarta dobavljaÄa</h1>
          <p className="sdh-decision-subtitle">Skorkarta je radni ekran za odluke o dobavljaÄima. Kombinuje prihod, prodaju po punoj ceni, marÅ¾u, rizik zaliha i zavisnost od nivelacija za artikle koji imaju prvi signal sniÅ¾enja u izabranom periodu.</p>
          <details className="sdh-decision-help">
            <summary>Kako se Äita ovaj ekran?</summary>
            <div className="sdh-decision-help-content">
              <p><strong>Å ta prikazuje:</strong> Skorkarta ne meri sav promet dobavljaÄa. Ona meri dobavljaÄe kroz scorecard skup: artikle koji imaju prvu nivelaciju u izabranom periodu, uz prodaju pre/posle, marÅ¾u, zalihu i pouzdanost signala.</p>
              <p><strong>Kako se tumaÄi:</strong> ViÅ¡i prihod i marÅ¾a su dobri, ali samo ako ne dolaze uz preveliku zavisnost od sniÅ¾enja i neaktivnu zalihu. Niske ili prazne vrednosti mogu znaÄiti da u periodu nema dovoljno scorecard signala, ne nuÅ¾no da dobavljaÄ nema promet.</p>
              <p><strong>VaÅ¾no:</strong> Tab â€žPregledâ€ koristi Å¡iri prodajni skup. Zato Pregled moÅ¾e imati promet dok je Skorkarta prazna ili niÅ¾a, posebno za kratke periode bez novih nivelacija.</p>
              <p><strong>Å ta znaÄe kolone:</strong></p>
              <ul>
                <li><strong>Prihod:</strong> Ukupna vrednost prodaje dobavljaÄa u periodu (samo artikli sa nivelacijom).</li>
                <li><strong>Udeo:</strong> Koliki deo ukupnog prihoda dolazi od tog dobavljaÄa.</li>
                <li><strong>MarÅ¾a:</strong> Razlika izmeÄ‘u prodajne i nabavne cene kao procenat.</li>
                <li><strong>Trend pune cene:</strong> Pozitivan = veÄ‡i udeo prodaje po punoj ceni od udela nivelacija; negativan = veÄ‡a zavisnost od sniÅ¾enja.</li>
                <li><strong>Preporuka:</strong> Akcioni label za ekran: PojaÄaj, ZadrÅ¾i ili Smanji. Dobija se iz backend preporuke, skora odluke i pouzdanosti signala.</li>
              </ul>
              <p><strong>ZaÅ¡to nema podataka?</strong> NajÄeÅ¡Ä‡i razlozi: nema nivelacija u izabranom periodu, filteri su uski (kratak period ili specifiÄna prodavnica), dobavljaÄi nisu pravilno povezani sa artiklima, ili analitika nije osveÅ¾ena (pokreni u Konfiguracija â†’ Radnici).</p>
              <p><strong>Kako koristiti:</strong> Uporedi 30, 90 i 180 dana. KraÄ‡i period pokazuje sveÅ¾ signal, a duÅ¾i stabilniju sliku. Grafikon pokazuje koncentraciju prihoda, a tabela objaÅ¡njava akciju po dobavljaÄu.</p>
            </div>
          </details>
        </div>
      </header>
      ) : null}

      <section className="sdh-decision-context" aria-label="ObjaÅ¡njenje skorkarte">
        <div>
          <strong>Å ta meri Skorkarta?</strong>
          <span>Scorecard skup dobavljaÄa: artikli sa prvom nivelacijom u izabranom periodu, uz prihod, marÅ¾u, punu cenu, zalihu i pouzdanost signala.</span>
        </div>
        <div>
          <strong>Kako Äitati niske vrednosti?</strong>
          <span>Niska ili prazna Skorkarta ne znaÄi automatski da dobavljaÄ nema promet; moÅ¾e znaÄiti da u periodu nema dovoljno nivelacija za procenu.</span>
        </div>
      </section>

      {!embedded ? (
      <section className="sdh-decision-filters">
        <label className="sdh-decision-field">
          <span>
            Period 
            <InfoTip text="KraÄ‡i period bolje hvata sveÅ¾ signal, duÅ¾i period smanjuje sluÄajne oscilacije i daje stabilniji rang." />
          </span>
          <select value={periodPreset} onChange={(e) => handlePresetChange(e.target.value as PeriodPreset)}>
            <option value="30d">Poslednjih 30 dana</option>
            <option value="90d">Poslednjih 90 dana</option>
            <option value="180d">Poslednjih 180 dana</option>
            <option value="365d">Poslednjih 365 dana</option>
            <option value="custom">PrilagoÄ‘eno</option>
          </select>
        </label>
        <label className="sdh-decision-field">
          <span>
            Od
            <InfoTip text="PoÄetak scorecard perioda. UkljuÄuju se dobavljaÄi Äiji artikli imaju prvu nivelaciju od ovog datuma." />
          </span>
          <input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
        </label>
        <label className="sdh-decision-field">
          <span>
            Do
            <InfoTip text="Kraj scorecard perioda. Analiza ukljuÄuje signale do kraja ovog dana." />
          </span>
          <input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
        </label>
        <label className="sdh-decision-field">
          <span>
            Sezona
            <InfoTip text="OgraniÄi analizu na odreÄ‘enu sezonu ako su podaci povezani sa sezonom." />
          </span>
          <select value={seasonId ?? ""} onChange={(e) => setSeasonId(e.target.value ? Number(e.target.value) : null)}>
            <option value="">Sve sezone</option>
            {seasons.map((season) => <option key={season.id} value={season.id}>{season.naziv}</option>)}
          </select>
        </label>
        <label className="sdh-decision-field">
          <span>
            Min prihod
            <InfoTip text="Sakrije dobavljaÄe Äiji je ukupan prihod manji od ovog iznosa. Koristi se za fokus na veÄ‡e dobavljaÄe." />
          </span>
          <input type="number" value={minRevenue ?? ""} onChange={(e) => setMinRevenue(e.target.value ? Number(e.target.value) : null)} placeholder="npr. 500000" />
        </label>
        <label className="sdh-decision-field check">
          <span>
            Samo visoka pouzdanost
            <InfoTip text="Sakriva dobavljaÄe sa slabim ili nepotpunim signalom, na primer malo artikala, malo prodaje ili nedostajuÄ‡e nabavne cene." />
          </span>
          <input type="checkbox" checked={onlyHighConfidence} onChange={(e) => setOnlyHighConfidence(e.target.checked)} />
        </label>
        <div className="sdh-decision-actions">
          <button type="button" onClick={handleApplyFilters} disabled={loading || invalidRange}>Primeni</button>
          <button type="button" className="secondary" onClick={handleResetFilters} disabled={loading}>PoniÅ¡ti filtere</button>
        </div>
      </section>
      ) : null}

      {invalidRange ? <div className="sdh-decision-message error" role="alert">Datum 'od' ne moÅ¾e biti posle datuma 'do'.</div> : null}
      {error ? <div className="sdh-decision-message error" role="alert">{error}</div> : null}
      {loading ? <div className="sdh-decision-message loading" role="status" aria-live="polite">UÄitavam skorkarte dobavljaÄa...</div> : null}
      
      {!loading && !error && zeroStateExplanation ? (
        <div className="sdh-decision-message warning">
          <strong>Nema pronaÄ‘enih podataka za izabrane filtere</strong>
          <p>{zeroStateExplanation}</p>
          <div className="sdh-decision-no-data-help">
            <p><strong>PokuÅ¡aj:</strong></p>
            <ul>
              <li>ProÅ¡iri vremenski period (izaberi duÅ¾i raspon dana)</li>
              <li>Ukloni filter prodavnice ili sezone ako su postavljeni</li>
              <li>Smanji minimalni prihod filter ako je postavljen</li>
              <li>Proveri da li su dobavljaÄi pravilno povezani sa artiklima</li>
              <li>Ako Pregled ima podatke, a Skorkarta je prazna za viÅ¡e perioda, proveri analytics refresh u Konfiguracija â†’ Radnici</li>
            </ul>
          </div>
        </div>
      ) : null}

      {!loading && summary && ranking ? (
        <>
          {(summary.dataNote ?? ranking.dataNote) ? (
            <div className="sdh-decision-message info" role="note">
              <strong>Obuhvat podataka:</strong> {summary.dataNote ?? ranking.dataNote}
            </div>
          ) : null}
          <section className="sdh-decision-kpis">
            <article className="sdh-decision-kpi">
              <span>
                Ukupan prihod
                <InfoTip text="Zbir prihoda za sve uÄitane scorecard dobavljaÄe. Osnova su artikli sa prvom nivelacijom u periodu, pa se moÅ¾e razlikovati od ukupnog prometa u tabu Pregled." />
              </span>
              <strong>{fmtRsd(totalRevenue)}</strong>
            </article>
            <article className="sdh-decision-kpi">
              <span>
                Udeo top 5 dobavljaÄa
                <InfoTip text="Udeo prihoda koji donosi pet najveÄ‡ih dobavljaÄa u scorecard skupu. VeÄ‡a vrednost znaÄi veÄ‡u koncentraciju i veÄ‡i rizik oslanjanja na nekoliko partnera." />
              </span>
              <strong>{fmtPct(top5SharePct)}</strong>
            </article>
            <article className="sdh-decision-kpi">
              <span>
                Ukupan marÅ¾ni doprinos
                <InfoTip text="Procena marÅ¾nog doprinosa za prikazane dobavljaÄe: prihod ponderisan pre-markdown marÅ¾om. ViÅ¡a vrednost je bolja, ali je proveri zajedno sa rizikom zaliha." />
              </span>
              <strong>{fmtRsd(totalMarginContribution)}</strong>
            </article>
            <article className="sdh-decision-kpi">
              <span>
                Kapital u riziku
                <InfoTip text="Procena vrednosti neprodate ili sporo rotirajuÄ‡e zalihe kod prikazanih dobavljaÄa. NiÅ¾a vrednost je bolja; visoka vrednost traÅ¾i proveru nabavke i zaliha." />
              </span>
              <strong className="trend-down">{fmtRsd(summary.capitalAtRisk)}</strong>
            </article>
            <article className="sdh-decision-kpi">
              <span>
                Promena udela pune cene
                <InfoTip text="Razlika u udelu prodaje po punoj ceni u odnosu na prethodni isti period. Pozitivno znaÄi zdraviji signal; negativno znaÄi veÄ‡u zavisnost od sniÅ¾enja." />
              </span>
              <strong className={trendClass(fullPriceDeltaPctPoints)}>{fmtSignedPct(fullPriceDeltaPctPoints)}</strong>
            </article>
          </section>

          <section className="sdh-decision-panels">
            <article className="sdh-decision-card">
              <h2>Koncentracija prihoda</h2><p>Grafikon pokazuje koliko prihoda u scorecard skupu nose najveÄ‡i dobavljaÄi. Visoka koncentracija znaÄi da promena uslova ili kvaliteta kod jednog dobavljaÄa moÅ¾e jaÄe uticati na rezultat.</p>
              {concentrationData.length > 0 ? (
                <div className="sdh-decision-chart-wrap">
                  <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={260}>
                    <BarChart data={concentrationData} layout="vertical" margin={{ top: 12, right: 16, left: 8, bottom: 8 }}>
                      <CartesianGrid strokeDasharray="3 3" stroke="var(--border-default)" />
                      <XAxis type="number" tick={{ fill: "var(--text-secondary)", fontSize: 12 }} unit="%" />
                      <YAxis type="category" dataKey="name" width={180} tick={{ fill: "var(--text-primary)", fontSize: 12 }} />
                      <Tooltip
                        allowEscapeViewBox={{ x: true, y: true }}
                        contentStyle={CHART_TOOLTIP_STYLE}
                        labelStyle={CHART_TOOLTIP_LABEL_STYLE}
                        wrapperStyle={{ zIndex: 20, maxWidth: "min(320px, calc(100vw - 32px))" }}
                        formatter={(value: number | string | undefined) => [`${fmtPct(Number(value ?? 0), 2)}`, "Udeo prihoda"]}
                      />
                      <Bar dataKey="sharePct" fill="var(--accent-primary)" radius={[0, 8, 8, 0]} />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              ) : <div className="sdh-decision-empty">Nema podataka za grafikon koncentracije.</div>}
            </article>

            <article className="sdh-decision-card">
              <div className="sdh-decision-table-head">
                <div>
                  <h2>Rang lista dobavljaÄa</h2>
                  <p>PojaÄaj: <strong>{supplierCounts.boost}</strong> | ZadrÅ¾i: <strong>{supplierCounts.keep}</strong> | Smanji: <strong>{supplierCounts.reduce}</strong></p>
                  <p className="sdh-decision-table-subtitle">Lista koristi sve uÄitane scorecard dobavljaÄe, ne samo prvih 100 iz API paginacije. Sortiranje je lokalno po izabranoj koloni; preporuka je akcioni label izveden iz backend signala, skora odluke i pouzdanosti.</p>
                </div>
                <AnalyticsTableToolbar tableKey="supplier-decision-hub" tableTitle="Skorkarta dobavljaÄa - kompaktni prikaz" columns={decisionColumns} rows={sortedRows} filters={toolbarFilters} metadata={toolbarMetadata} defaultOrientation="landscape" />
              </div>
              <div className="sdh-decision-table-wrap">
                <table className="sdh-decision-table">
                  <thead>
                    <tr>
                      <th>
                        <button type="button" onClick={() => handleSort("supplierName")}>
                          DobavljaÄ
                          <InfoTip text="Naziv dobavljaÄa. Prazni nazivi se normalizuju na 'DobavljaÄ #ID' ili 'Nepoznat dobavljaÄ' da tabela nema blank redove." />
                          {sortMarker("supplierName", sortField, sortDir)}
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("revenue")}>
                          Prihod
                          <InfoTip text="Prihod dobavljaÄa u scorecard skupu za izabrani period." />
                          {sortMarker("revenue", sortField, sortDir)}
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("sharePct")}>
                          Udeo %
                          <InfoTip text="Udeo ovog dobavljaÄa u ukupnom scorecard prihodu. VeÄ‡i udeo znaÄi veÄ‡i uticaj na ukupne KPI-jeve." />
                          {sortMarker("sharePct", sortField, sortDir)}
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("preMarkdownMarginPct")}>
                          MarÅ¾a %
                          <InfoTip text="Pre-markdown marÅ¾a: procenat zarade pre prvog sniÅ¾enja. ViÅ¡a marÅ¾a je bolji signal, osim ako dolazi uz visok stock rizik." />
                          {sortMarker("preMarkdownMarginPct", sortField, sortDir)}
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("qualityTrendPct")}>
                          Trend pune cene %
                          <InfoTip text="Udeo pune cene minus udeo nivelacija. Pozitivno znaÄi zdraviju prodaju; negativno znaÄi veÄ‡u zavisnost od sniÅ¾enja." />
                          {sortMarker("qualityTrendPct", sortField, sortDir)}
                        </button>
                      </th>
                      <th>
                        <button type="button" onClick={() => handleSort("status")}>
                          Preporuka
                          <InfoTip text="Akcioni label za odluku: PojaÄaj, ZadrÅ¾i ili Smanji. Nije ruÄna ocena; izveden je iz backend preporuke, skora odluke i pouzdanosti." />
                          {sortMarker("status", sortField, sortDir)}
                        </button>
                      </th>
                      <th className="align-center">Detalj</th>
                    </tr>
                  </thead>
                  <tbody>
                    {sortedRows.length === 0 ? (
                      <tr>
                        <td colSpan={7} className="sdh-decision-empty-row">
                          <div>
                            <p>Nema pronaÄ‘enih dobavljaÄa za izabrane filtere.</p>
                            <p className="sdh-decision-table-helper">Ako Pregled ima promet, proÅ¡iri period ili ukloni uske filtere. Skorkarta koristi uÅ¾i scorecard skup zasnovan na prvim nivelacijama.</p>
                          </div>
                        </td>
                      </tr>
                    ) : (
                      sortedRows.map((row) => {
                        const expanded = expandedSupplierId === row.supplierId;
                        return (
                          <tr key={row.supplierId} className={expanded ? "expanded-row" : ""}>
                            <td>{row.supplierName}</td>
                            <td className="align-right">{fmtRsd(row.revenue)}</td>
                            <td className="align-right">{fmtPct(row.sharePct, 2)}</td>
                            <td className="align-right">{fmtPct(row.preMarkdownMarginPct * 100, 2)}</td>
                            <td className={`align-right ${trendClass(row.qualityTrendPct)}`}>{fmtSignedPct(row.qualityTrendPct, 2)}</td>
                            <td><span className={statusClass(row.status)} title={buildStatusTooltip(row)} aria-label={buildStatusTooltip(row)}>{statusDisplayLabel(row.status)}</span></td>
                            <td className="align-center"><button type="button" className="sdh-decision-detail-btn" onClick={() => setExpandedSupplierId(expanded ? null : row.supplierId)}>{expanded ? "Sakrij" : "Detalji"}</button></td>
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
            <section className="sdh-decision-detail">
              <div className="sdh-decision-detail-head">
                <h3>Detalj odluke: {selectedRow.supplierName}</h3>
                <button type="button" onClick={() => openSupplierDetail(selectedRow)}>Otvori puni detalj</button>
              </div>
              <div className="sdh-decision-detail-grid">
                <article>
                  <span>Prihod <InfoTip text="Ukupna vrednost prodaje ovog dobavljaÄa u izabranom periodu." /></span>
                  <strong>{fmtRsd(selectedRow.revenue)}</strong>
                </article>
                <article>
                  <span>Komadi <InfoTip text="Koliko artikala je prodato ovog dobavljaÄa u periodu." /></span>
                  <strong>{selectedRow.units.toLocaleString("sr-RS")} kom</strong>
                </article>
                <article>
                  <span>Udeo pune cene <InfoTip text="Koliki deo prihoda dolazi od prodaje po punoj ceni (bez sniÅ¾enja)." /></span>
                  <strong>{fmtPct(selectedRow.fullPriceRevenueShare * 100, 2)}</strong>
                </article>
                <article>
                  <span>Udeo nivelacija <InfoTip text="Koliki deo prihoda od ovog dobavljaÄa dolazi od prodaje sa sniÅ¾enjima (nivelacijama). ViÅ¡i procenat moÅ¾e ukazivati da je asortiman precenjen ili da potraÅ¾nja slabi." /></span>
                  <strong>{fmtPct(selectedRow.markdownRevenueShare * 100, 2)}</strong>
                </article>
                <article>
                  <span>Stopa neaktivnih artikala <InfoTip text="Koliki deo artikala ovog dobavljaÄa leÅ¾i na zalihi bez prodaje. ViÅ¡a stopa znaÄi prekomerne narudÅ¾bine u odnosu na potraÅ¾nju â€” rizik za kapital i skladiÅ¡te." /></span>
                  <strong>{fmtPct(selectedRow.deadStockRate * 100, 2)}</strong>
                </article>
                <article>
                  <span>Vrednost neprodate zalihe <InfoTip text="Procenjena vrednost artikala koji su na zalihi a se nisu prodali. To je kapital koji nije obrnut." /></span>
                  <strong>{fmtRsd(selectedRow.unsoldStockValue)}</strong>
                </article>
                <article>
                  <span>Stopa dobrih artikala <InfoTip text="Procenat artikala dobavljaÄa koji se redovno dobro prodaju â€” malo neaktivne zalihe, dobra marÅ¾a, pozitivan trend. ViÅ¡i procenat = pouzdaniji i predvidiviji asortiman." /></span>
                  <strong>{fmtPct(selectedRow.repeatWinnerRate * 100, 2)}</strong>
                </article>
                <article>
                  <span>Skor / indeks kvaliteta <InfoTip text="Dva pokazatelja: levi (0â€“100) je automatski skor na osnovu prodajnih signala, desni je indeks pouzdanosti asortimana. ViÅ¡i skor = bolji uÄinak. Korisno za poreÄ‘enje dobavljaÄa izmeÄ‘u sebe." /></span>
                  <strong>{selectedRow.mlSupplierScore.toFixed(1)} / {selectedRow.supplierQualityIndex.toFixed(1)}</strong>
                </article>
                <article>
                  <span>Confidence preporuke <InfoTip text="Backend confidence signal za RecommendationCode. Ovo nije isto sto i lokalni heuristic score." /></span>
                  <strong>{selectedRow.confidenceAvailable ? fmtPct(selectedRow.normalizedConfidence, 1) : RECOMMENDATION_SIGNAL_UNAVAILABLE}</strong>
                </article>
                <article>
                  <span>Recommendation reliability</span>
                  <strong>{selectedRow.reliabilityAvailable ? fmtPct(selectedRow.reliabilityPct, 1) : RECOMMENDATION_SIGNAL_UNAVAILABLE}</strong>
                </article>
                <article>
                  <span>Status kvaliteta preporuke</span>
                  <strong style={recommendationQualityStyle(selectedRow.dataQualityStatus)}>{recommendationQualityLabel(selectedRow.dataQualityStatus)}</strong>
                </article>
              </div>
              <p className="sdh-decision-reason">
                <strong>Razlog preporuke:</strong> {selectedRow.statusReason}
              </p>
              <p className="sdh-decision-reason">
                <strong>Skor odluke:</strong> {selectedRow.decisionScore} (lokalni pomocni indeks za rangiranje; backend jos nije poslao pun quality payload za ovaj screen)
              </p>
              {selectedRow.reasonCodes.length > 0 ? (
                <p className="sdh-decision-reason">
                  <strong>Reason codes:</strong> {selectedRow.reasonCodes.join(" | ")}
                </p>
              ) : null}
              {recommendationReasonHints(selectedRow.reasonCodes).map((hint) => (
                <p key={hint} className="sdh-decision-reason">
                  <strong>Napomena:</strong> {hint}
                </p>
              ))}
              {(!selectedRow.reliabilityAvailable || selectedRow.dataQualityStatus !== "good") ? (
                <p className="sdh-decision-reason">
                  <strong>Data quality:</strong> Otvori <Link to="/analytics/data-quality">Data Quality</Link> da proveris popravljive probleme.
                </p>
              ) : null}
            </section>
          ) : null}
        </>
      ) : null}
    </div>
  );
}

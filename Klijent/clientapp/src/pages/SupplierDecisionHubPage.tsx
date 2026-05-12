import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
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
import { BOOST_SCORE_THRESHOLD, KEEP_SCORE_THRESHOLD } from "../utils/analyticsConstants";
import { fmtPct, fmtRsd, fmtSignedPct, getPresetRange } from "../utils/analyticsFormatters";
import { CHART_TOOLTIP_LABEL_STYLE, CHART_TOOLTIP_STYLE } from "../utils/chartTooltipStyle";
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
};

const STATUS_PRIORITY: Record<DecisionStatus, number> = {
  Pojacaj: 3,
  Zadrzi: 2,
  Smanji: 1,
};
const BOOST_MIN_CONFIDENCE_PCT = 55;

const decisionColumns: AnalyticsTableColumn<DecisionRow>[] = [
  { key: "supplierName", header: "Dobavljač", dataType: "text" },
  { key: "revenue", header: "Prihod", dataType: "currency" },
  { key: "sharePct", header: "Udeo %", dataType: "percent" },
  { key: "preMarkdownMarginPct", header: "Marža %", dataType: "percent" },
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
  if (status === "Pojacaj") return "Pojačaj";
  if (status === "Smanji") return "Smanji";
  return "Zadrži";
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

function normalizeConfidence(value: number): number {
  if (!Number.isFinite(value)) return 0;
  if (value <= 1) return value * 100;
  return clamp(value, 0, 100);
}

function recommendationToStatus(code: string, confidence: number): DecisionStatus {
  if (code === "EXPAND" || code === "EXPAND_SELECTIVELY") return confidence >= BOOST_MIN_CONFIDENCE_PCT ? "Pojacaj" : "Zadrzi";
  if (code === "ASSORTMENT_REDUCE" || code === "PRICE_NEGOTIATE") return "Smanji";
  return "Zadrzi";
}

function buildStatusReason(status: DecisionStatus, code: string, qualityTrendPct: number, confidence: number): string {
  const lowConfidence = confidence < BOOST_MIN_CONFIDENCE_PCT;
  if (status === "Pojacaj") {
    if (lowConfidence) return "Signal za rast postoji, ali je pouzdanost granična; širi postepeno.";
    if (code === "EXPAND" || code === "EXPAND_SELECTIVELY") return "Dobavljač drži zdrav prodajni signal bez preterane zavisnosti od nivelacija.";
    return "Pozitivan zbirni signal za veći fokus.";
  }
  if (status === "Zadrzi") {
    if (lowConfidence) return "Niža pouzdanost podataka; odluku držati konzervativnom dok se signal ne stabilizuje.";
    if (qualityTrendPct < 0) return "Signal kvaliteta slabi; zadržati uz pojačan nadzor.";
    return "Stabilan signal bez jasnog razloga za promenu prioriteta.";
  }
  if (code === "ASSORTMENT_REDUCE") return "Visoka zavisnost od nivelacija i rizik neaktivne zalihe — smanjiti fokus u nabavci.";
  if (code === "PRICE_NEGOTIATE") return "Potreban je bolji cenovni ulaz — pregovoriti nabavne cene pre daljeg proširivanja asortimana.";
  return "Nizak signal doprinosa i rizik po profitabilnost.";
}

function buildStatusTooltip(row: DecisionRow): string {
  return `${statusDisplayLabel(row.status)}: ${row.statusReason} | Udeo ${fmtPct(row.sharePct, 1)} | Marža ${fmtPct(row.preMarkdownMarginPct * 100, 1)} | Trend pune cene ${fmtSignedPct(row.qualityTrendPct, 1)} | Pouzdanost ${fmtPct(row.normalizedConfidence, 0)}`;
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
      if (summaryResult.status === "rejected" || rankingResult.status === "rejected") throw new Error("Neuspešno učitavanje podataka skorkarte dobavljača.");
      setSummary(summaryResult.value);
      setRanking(rankingResult.value);
      setPreviousSummary(previousResult.status === "fulfilled" ? previousResult.value : null);
      setExpandedSupplierId(null);
    } catch (reason) {
      if (requestId !== requestIdRef.current) return;
      setSummary(null);
      setPreviousSummary(null);
      setRanking(null);
      setError(reason instanceof Error ? reason.message : "Greška pri učitavanju skorkarte dobavljača.");
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
      const normalizedConfidence = normalizeConfidence(item.confidenceScore);
      const qualityIndex = clamp(item.supplierQualityIndex <= 1 ? item.supplierQualityIndex * 100 : item.supplierQualityIndex, 0, 100);
      const shareNorm = topShare > 0 ? clamp((sharePct / topShare) * 100, 0, 100) : 0;
      const trendNorm = clamp(((qualityTrendPct + 40) / 80) * 100, 0, 100);
      const decisionScore = Math.round(qualityIndex * 0.40 + normalizedConfidence * 0.25 + shareNorm * 0.20 + trendNorm * 0.15);

      let status = recommendationToStatus(item.recommendationCode, normalizedConfidence);
      if (status === "Pojacaj" && decisionScore < BOOST_SCORE_THRESHOLD) status = "Zadrzi";
      if (
        status === "Zadrzi" &&
        decisionScore < KEEP_SCORE_THRESHOLD &&
        (qualityTrendPct < 0 || normalizedConfidence < BOOST_MIN_CONFIDENCE_PCT)
      ) {
        status = "Smanji";
      }
      const statusReason = buildStatusReason(status, item.recommendationCode, qualityTrendPct, normalizedConfidence);

      return { ...item, sharePct, marginContribution, qualityTrendPct, decisionScore, status, statusReason, normalizedConfidence };
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
      return "Skorkarta se puni iz dobavljača koji imaju artikle sa prvom nivelacijom u izabranom periodu. Ako takvih zapisa nema, scorecard KPI-jevi ostaju na nuli iako Pregled može imati promet, jer Pregled koristi širi prodajni skup.";
    }

    return "Postoje zapisi za Skorkartu, ali su ključni pokazatelji trenutno 0. Proveri period, objekat, dobavljača i minimalni prihod; ako Pregled ima promet, a Skorkarta ostaje na nuli, potreban je refresh analytics scorecard podataka.";
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
    { key: "supplierId", label: "Dobavljač", value: activeFilters.supplierId ?? "" },
    { key: "storeId", label: "Objekat", value: activeFilters.storeId ?? "" },
    { key: "dataScope", label: "Opseg podataka", value: activeFilters.dataScope ?? "" },
  ], [activeFilters.dataScope, activeFilters.fromDate, activeFilters.minRevenue, activeFilters.onlyHighConfidence, activeFilters.seasonId, activeFilters.storeId, activeFilters.supplierId, activeFilters.toDate, periodPreset]);

  const toolbarMetadata = useMemo<AnalyticsNamedValue[]>(() => [
    { key: "summaryFrom", label: "Sažetak od", value: summary?.from ?? "" },
    { key: "summaryTo", label: "Sažetak do", value: summary?.to ?? "" },
    { key: "supplierCount", label: "Dobavljača", value: summary?.supplierCount ?? 0 },
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
      subtitle: "Podrška odluci za dobavljače",
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
          <h1 className="sdh-decision-title">Skorkarta dobavljača</h1>
          <p className="sdh-decision-subtitle">Skorkarta je radni ekran za odluke o dobavljačima. Kombinuje prihod, prodaju po punoj ceni, maržu, rizik zaliha i zavisnost od nivelacija za artikle koji imaju prvi signal sniženja u izabranom periodu.</p>
          <details className="sdh-decision-help">
            <summary>Kako se čita ovaj ekran?</summary>
            <div className="sdh-decision-help-content">
              <p><strong>Šta prikazuje:</strong> Skorkarta ne meri sav promet dobavljača. Ona meri dobavljače kroz scorecard skup: artikle koji imaju prvu nivelaciju u izabranom periodu, uz prodaju pre/posle, maržu, zalihu i pouzdanost signala.</p>
              <p><strong>Kako se tumači:</strong> Viši prihod i marža su dobri, ali samo ako ne dolaze uz preveliku zavisnost od sniženja i neaktivnu zalihu. Niske ili prazne vrednosti mogu značiti da u periodu nema dovoljno scorecard signala, ne nužno da dobavljač nema promet.</p>
              <p><strong>Važno:</strong> Tab „Pregled” koristi širi prodajni skup. Zato Pregled može imati promet dok je Skorkarta prazna ili niža, posebno za kratke periode bez novih nivelacija.</p>
              <p><strong>Šta znače kolone:</strong></p>
              <ul>
                <li><strong>Prihod:</strong> Ukupna vrednost prodaje dobavljača u periodu (samo artikli sa nivelacijom).</li>
                <li><strong>Udeo:</strong> Koliki deo ukupnog prihoda dolazi od tog dobavljača.</li>
                <li><strong>Marža:</strong> Razlika između prodajne i nabavne cene kao procenat.</li>
                <li><strong>Trend pune cene:</strong> Pozitivan = veći udeo prodaje po punoj ceni od udela nivelacija; negativan = veća zavisnost od sniženja.</li>
                <li><strong>Preporuka:</strong> Akcioni label za ekran: Pojačaj, Zadrži ili Smanji. Dobija se iz backend preporuke, skora odluke i pouzdanosti signala.</li>
              </ul>
              <p><strong>Zašto nema podataka?</strong> Najčešći razlozi: nema nivelacija u izabranom periodu, filteri su uski (kratak period ili specifična prodavnica), dobavljači nisu pravilno povezani sa artiklima, ili analitika nije osvežena (pokreni u Konfiguracija → Radnici).</p>
              <p><strong>Kako koristiti:</strong> Uporedi 30, 90 i 180 dana. Kraći period pokazuje svež signal, a duži stabilniju sliku. Grafikon pokazuje koncentraciju prihoda, a tabela objašnjava akciju po dobavljaču.</p>
            </div>
          </details>
        </div>
      </header>
      ) : null}

      <section className="sdh-decision-context" aria-label="Objašnjenje skorkarte">
        <div>
          <strong>Šta meri Skorkarta?</strong>
          <span>Scorecard skup dobavljača: artikli sa prvom nivelacijom u izabranom periodu, uz prihod, maržu, punu cenu, zalihu i pouzdanost signala.</span>
        </div>
        <div>
          <strong>Kako čitati niske vrednosti?</strong>
          <span>Niska ili prazna Skorkarta ne znači automatski da dobavljač nema promet; može značiti da u periodu nema dovoljno nivelacija za procenu.</span>
        </div>
      </section>

      {!embedded ? (
      <section className="sdh-decision-filters">
        <label className="sdh-decision-field">
          <span>
            Period 
            <InfoTip text="Kraći period bolje hvata svež signal, duži period smanjuje slučajne oscilacije i daje stabilniji rang." />
          </span>
          <select value={periodPreset} onChange={(e) => handlePresetChange(e.target.value as PeriodPreset)}>
            <option value="30d">Poslednjih 30 dana</option>
            <option value="90d">Poslednjih 90 dana</option>
            <option value="180d">Poslednjih 180 dana</option>
            <option value="365d">Poslednjih 365 dana</option>
            <option value="custom">Prilagođeno</option>
          </select>
        </label>
        <label className="sdh-decision-field">
          <span>
            Od
            <InfoTip text="Početak scorecard perioda. Uključuju se dobavljači čiji artikli imaju prvu nivelaciju od ovog datuma." />
          </span>
          <input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
        </label>
        <label className="sdh-decision-field">
          <span>
            Do
            <InfoTip text="Kraj scorecard perioda. Analiza uključuje signale do kraja ovog dana." />
          </span>
          <input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
        </label>
        <label className="sdh-decision-field">
          <span>
            Sezona
            <InfoTip text="Ograniči analizu na određenu sezonu ako su podaci povezani sa sezonom." />
          </span>
          <select value={seasonId ?? ""} onChange={(e) => setSeasonId(e.target.value ? Number(e.target.value) : null)}>
            <option value="">Sve sezone</option>
            {seasons.map((season) => <option key={season.id} value={season.id}>{season.naziv}</option>)}
          </select>
        </label>
        <label className="sdh-decision-field">
          <span>
            Min prihod
            <InfoTip text="Sakrije dobavljače čiji je ukupan prihod manji od ovog iznosa. Koristi se za fokus na veće dobavljače." />
          </span>
          <input type="number" value={minRevenue ?? ""} onChange={(e) => setMinRevenue(e.target.value ? Number(e.target.value) : null)} placeholder="npr. 500000" />
        </label>
        <label className="sdh-decision-field check">
          <span>
            Samo visoka pouzdanost
            <InfoTip text="Sakriva dobavljače sa slabim ili nepotpunim signalom, na primer malo artikala, malo prodaje ili nedostajuće nabavne cene." />
          </span>
          <input type="checkbox" checked={onlyHighConfidence} onChange={(e) => setOnlyHighConfidence(e.target.checked)} />
        </label>
        <div className="sdh-decision-actions">
          <button type="button" onClick={handleApplyFilters} disabled={loading || invalidRange}>Primeni</button>
          <button type="button" className="secondary" onClick={handleResetFilters} disabled={loading}>Poništi filtere</button>
        </div>
      </section>
      ) : null}

      {invalidRange ? <div className="sdh-decision-message error" role="alert">Datum 'od' ne može biti posle datuma 'do'.</div> : null}
      {error ? <div className="sdh-decision-message error" role="alert">{error}</div> : null}
      {loading ? <div className="sdh-decision-message loading" role="status" aria-live="polite">Učitavam skorkarte dobavljača...</div> : null}
      
      {!loading && !error && zeroStateExplanation ? (
        <div className="sdh-decision-message warning">
          <strong>Nema pronađenih podataka za izabrane filtere</strong>
          <p>{zeroStateExplanation}</p>
          <div className="sdh-decision-no-data-help">
            <p><strong>Pokušaj:</strong></p>
            <ul>
              <li>Proširi vremenski period (izaberi duži raspon dana)</li>
              <li>Ukloni filter prodavnice ili sezone ako su postavljeni</li>
              <li>Smanji minimalni prihod filter ako je postavljen</li>
              <li>Proveri da li su dobavljači pravilno povezani sa artiklima</li>
              <li>Ako Pregled ima podatke, a Skorkarta je prazna za više perioda, proveri analytics refresh u Konfiguracija → Radnici</li>
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
                <InfoTip text="Zbir prihoda za sve učitane scorecard dobavljače. Osnova su artikli sa prvom nivelacijom u periodu, pa se može razlikovati od ukupnog prometa u tabu Pregled." />
              </span>
              <strong>{fmtRsd(totalRevenue)}</strong>
            </article>
            <article className="sdh-decision-kpi">
              <span>
                Udeo top 5 dobavljača
                <InfoTip text="Udeo prihoda koji donosi pet najvećih dobavljača u scorecard skupu. Veća vrednost znači veću koncentraciju i veći rizik oslanjanja na nekoliko partnera." />
              </span>
              <strong>{fmtPct(top5SharePct)}</strong>
            </article>
            <article className="sdh-decision-kpi">
              <span>
                Ukupan maržni doprinos
                <InfoTip text="Procena maržnog doprinosa za prikazane dobavljače: prihod ponderisan pre-markdown maržom. Viša vrednost je bolja, ali je proveri zajedno sa rizikom zaliha." />
              </span>
              <strong>{fmtRsd(totalMarginContribution)}</strong>
            </article>
            <article className="sdh-decision-kpi">
              <span>
                Kapital u riziku
                <InfoTip text="Procena vrednosti neprodate ili sporo rotirajuće zalihe kod prikazanih dobavljača. Niža vrednost je bolja; visoka vrednost traži proveru nabavke i zaliha." />
              </span>
              <strong className="trend-down">{fmtRsd(summary.capitalAtRisk)}</strong>
            </article>
            <article className="sdh-decision-kpi">
              <span>
                Promena udela pune cene
                <InfoTip text="Razlika u udelu prodaje po punoj ceni u odnosu na prethodni isti period. Pozitivno znači zdraviji signal; negativno znači veću zavisnost od sniženja." />
              </span>
              <strong className={trendClass(fullPriceDeltaPctPoints)}>{fmtSignedPct(fullPriceDeltaPctPoints)}</strong>
            </article>
          </section>

          <section className="sdh-decision-panels">
            <article className="sdh-decision-card">
              <h2>Koncentracija prihoda</h2><p>Grafikon pokazuje koliko prihoda u scorecard skupu nose najveći dobavljači. Visoka koncentracija znači da promena uslova ili kvaliteta kod jednog dobavljača može jače uticati na rezultat.</p>
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
                  <h2>Rang lista dobavljača</h2>
                  <p>Pojačaj: <strong>{supplierCounts.boost}</strong> | Zadrži: <strong>{supplierCounts.keep}</strong> | Smanji: <strong>{supplierCounts.reduce}</strong></p>
                  <p className="sdh-decision-table-subtitle">Lista koristi sve učitane scorecard dobavljače, ne samo prvih 100 iz API paginacije. Sortiranje je lokalno po izabranoj koloni; preporuka je akcioni label izveden iz backend signala, skora odluke i pouzdanosti.</p>
                </div>
                <AnalyticsTableToolbar tableKey="supplier-decision-hub" tableTitle="Skorkarta dobavljača - kompaktni prikaz" columns={decisionColumns} rows={sortedRows} filters={toolbarFilters} metadata={toolbarMetadata} defaultOrientation="landscape" />
              </div>
              <div className="sdh-decision-table-wrap">
                <table className="sdh-decision-table">
                  <thead>
                    <tr>
                      <th>
                        <button type="button" onClick={() => handleSort("supplierName")}>
                          Dobavljač
                          <InfoTip text="Naziv dobavljača. Prazni nazivi se normalizuju na 'Dobavljač #ID' ili 'Nepoznat dobavljač' da tabela nema blank redove." />
                          {sortMarker("supplierName", sortField, sortDir)}
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("revenue")}>
                          Prihod
                          <InfoTip text="Prihod dobavljača u scorecard skupu za izabrani period." />
                          {sortMarker("revenue", sortField, sortDir)}
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("sharePct")}>
                          Udeo %
                          <InfoTip text="Udeo ovog dobavljača u ukupnom scorecard prihodu. Veći udeo znači veći uticaj na ukupne KPI-jeve." />
                          {sortMarker("sharePct", sortField, sortDir)}
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("preMarkdownMarginPct")}>
                          Marža %
                          <InfoTip text="Pre-markdown marža: procenat zarade pre prvog sniženja. Viša marža je bolji signal, osim ako dolazi uz visok stock rizik." />
                          {sortMarker("preMarkdownMarginPct", sortField, sortDir)}
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("qualityTrendPct")}>
                          Trend pune cene %
                          <InfoTip text="Udeo pune cene minus udeo nivelacija. Pozitivno znači zdraviju prodaju; negativno znači veću zavisnost od sniženja." />
                          {sortMarker("qualityTrendPct", sortField, sortDir)}
                        </button>
                      </th>
                      <th>
                        <button type="button" onClick={() => handleSort("status")}>
                          Preporuka
                          <InfoTip text="Akcioni label za odluku: Pojačaj, Zadrži ili Smanji. Nije ručna ocena; izveden je iz backend preporuke, skora odluke i pouzdanosti." />
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
                            <p>Nema pronađenih dobavljača za izabrane filtere.</p>
                            <p className="sdh-decision-table-helper">Ako Pregled ima promet, proširi period ili ukloni uske filtere. Skorkarta koristi uži scorecard skup zasnovan na prvim nivelacijama.</p>
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
                  <span>Prihod <InfoTip text="Ukupna vrednost prodaje ovog dobavljača u izabranom periodu." /></span>
                  <strong>{fmtRsd(selectedRow.revenue)}</strong>
                </article>
                <article>
                  <span>Komadi <InfoTip text="Koliko artikala je prodato ovog dobavljača u periodu." /></span>
                  <strong>{selectedRow.units.toLocaleString("sr-RS")} kom</strong>
                </article>
                <article>
                  <span>Udeo pune cene <InfoTip text="Koliki deo prihoda dolazi od prodaje po punoj ceni (bez sniženja)." /></span>
                  <strong>{fmtPct(selectedRow.fullPriceRevenueShare * 100, 2)}</strong>
                </article>
                <article>
                  <span>Udeo nivelacija <InfoTip text="Koliki deo prihoda od ovog dobavljača dolazi od prodaje sa sniženjima (nivelacijama). Viši procenat može ukazivati da je asortiman precenjen ili da potražnja slabi." /></span>
                  <strong>{fmtPct(selectedRow.markdownRevenueShare * 100, 2)}</strong>
                </article>
                <article>
                  <span>Stopa neaktivnih artikala <InfoTip text="Koliki deo artikala ovog dobavljača leži na zalihi bez prodaje. Viša stopa znači prekomerne narudžbine u odnosu na potražnju — rizik za kapital i skladište." /></span>
                  <strong>{fmtPct(selectedRow.deadStockRate * 100, 2)}</strong>
                </article>
                <article>
                  <span>Vrednost neprodate zalihe <InfoTip text="Procenjena vrednost artikala koji su na zalihi a se nisu prodali. To je kapital koji nije obrnut." /></span>
                  <strong>{fmtRsd(selectedRow.unsoldStockValue)}</strong>
                </article>
                <article>
                  <span>Stopa dobrih artikala <InfoTip text="Procenat artikala dobavljača koji se redovno dobro prodaju — malo neaktivne zalihe, dobra marža, pozitivan trend. Viši procenat = pouzdaniji i predvidiviji asortiman." /></span>
                  <strong>{fmtPct(selectedRow.repeatWinnerRate * 100, 2)}</strong>
                </article>
                <article>
                  <span>Skor / indeks kvaliteta <InfoTip text="Dva pokazatelja: levi (0–100) je automatski skor na osnovu prodajnih signala, desni je indeks pouzdanosti asortimana. Viši skor = bolji učinak. Korisno za poređenje dobavljača između sebe." /></span>
                  <strong>{selectedRow.mlSupplierScore.toFixed(1)} / {selectedRow.supplierQualityIndex.toFixed(1)}</strong>
                </article>
                <article>
                  <span>Pouzdanost podataka <InfoTip text="Koliko su potpuni podaci za ovog dobavljača. Niska pouzdanost (ispod 55%) obično znači da nedostaju nabavne cene ili ima malo prodajnih signala — preporuku u tom slučaju uzmi sa rezervom." /></span>
                  <strong>{fmtPct(selectedRow.normalizedConfidence, 1)}</strong>
                </article>
              </div>
              <p className="sdh-decision-reason">
                <strong>Razlog preporuke:</strong> {selectedRow.statusReason}
              </p>
            </section>
          ) : null}
        </>
      ) : null}
    </div>
  );
}

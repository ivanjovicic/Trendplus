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
import { buildAnalyticsDetailSnapshot, saveAnalyticsDetailSnapshot } from "../services/analyticsTableState";
import { getPreNivelacijaPrioriteti } from "../services/preNivelacijaApi";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import type { PreNivelacijaPriorityResponse, PreNivelacijaSkuCandidate } from "../types/preNivelacija";
import { CHART_TOOLTIP_STYLE } from "../utils/chartTooltipStyle";
import "./PreNivelacijaPriorityPage.css";

type SortDir = "asc" | "desc";
type SortField =
  | "sku"
  | "supplierName"
  | "preNivelacijaScore"
  | "stockUnits"
  | "daysSinceLastSale"
  | "revenueDelta"
  | "status";
type DecisionStatus = "Pojacaj" | "Zadrzi" | "Smanji";

type ActiveFilters = {
  supplierId: number | null;
  seasonId: number | null;
  footwearTypeId: number | null;
  minScore: number;
  noSaleDaysMin: number;
};

type DecisionCandidate = PreNivelacijaSkuCandidate & {
  revenueDelta: number;
  marginDelta: number;
  reliabilityPct: number;
  decisionScore: number;
  status: DecisionStatus;
  statusReason: string;
};

type FocusFilter = "all" | "boost" | "keep" | "reduce" | "highPriority";
const FOCUS_LABELS: Record<FocusFilter, string> = {
  all: "Sve",
  boost: "Pojačaj",
  keep: "Zadrži",
  reduce: "Smanji",
  highPriority: "Visok prioritet",
};

const STATUS_PRIORITY: Record<DecisionStatus, number> = {
  Pojacaj: 3,
  Zadrzi: 2,
  Smanji: 1,
};
const BOOST_SCORE_THRESHOLD = 68;
const KEEP_SCORE_THRESHOLD = 43;
const BOOST_MIN_RELIABILITY_PCT = 40;

const decisionColumns: AnalyticsTableColumn<DecisionCandidate>[] = [
  { key: "sku", header: "SKU", dataType: "text" },
  { key: "supplierName", header: "Dobavljac", dataType: "text" },
  { key: "preNivelacijaScore", header: "Skor nivelacije", dataType: "number" },
  { key: "stockUnits", header: "Zaliha (kom)", dataType: "number" },
  { key: "daysSinceLastSale", header: "Dana bez prodaje", dataType: "number" },
  { key: "revenueDelta", header: "Isticanje vs sniženje (prihod)", dataType: "currency" },
  { key: "reliabilityPct", header: "Pouzdanost %", dataType: "number" },
  { key: "decisionScore", header: "Ocena preporuke", dataType: "number" },
  { key: "status", header: "Preporuka", dataType: "text" },
];

interface CustomSupplierTooltipProps {
  active?: boolean;
  payload?: Array<{ payload: { name: string; sharePct: number; weekOverWeekRiskDeltaPct: number } }>;
}

function CustomSupplierTooltip({ active, payload }: CustomSupplierTooltipProps) {
  if (!active || !payload || payload.length === 0) return null;
  const data = payload[0].payload;
  const wowPct = data.weekOverWeekRiskDeltaPct;
  return (
    <div style={CHART_TOOLTIP_STYLE}>
      <p style={{ margin: 0, fontSize: "12px", fontWeight: 500 }}>{data.name}</p>
      <p style={{ margin: "4px 0 0", fontSize: "12px", color: "var(--text-secondary)" }}>
        Udeo u akciji: {fmtPct(data.sharePct, 2)}
      </p>
      {wowPct != null ? (
        <p style={{ margin: "4px 0 0", fontSize: "12px", color: wowPct >= 0 ? "#ef4444" : "#16a34a" }}>
          Sedm. promena rizika: {wowPct >= 0 ? "+" : ""}{fmtPct(wowPct, 1)}
        </p>
      ) : null}
    </div>
  );
}

function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, value));
}

function fmtRsd(value: number): string {
  return `${value.toLocaleString("sr-RS", { minimumFractionDigits: 0, maximumFractionDigits: 0 })} RSD`;
}

function fmtPct(value: number | null | undefined, digits = 1): string {
  if (value == null || Number.isNaN(value)) return "N/A";
  return `${value.toLocaleString("sr-RS", { minimumFractionDigits: digits, maximumFractionDigits: digits })}%`;
}

function sortMarker(field: SortField, activeField: SortField, dir: SortDir): string {
  if (field !== activeField) return "";
  return dir === "asc" ? " ^" : " v";
}

function statusClass(status: DecisionStatus): string {
  if (status === "Pojacaj") return "pnp-decision-status status-boost";
  if (status === "Smanji") return "pnp-decision-status status-reduce";
  return "pnp-decision-status status-keep";
}

type StatusReasonSignals = {
  priorityBand: string;
  revenueDelta: number;
  reliabilityPct: number;
  decisionScore: number;
};

type StatusTooltipData = {
  status: DecisionStatus;
  statusReason: string;
  decisionScore: number;
  revenueDelta: number;
  reliabilityPct: number;
  confidence: string;
};

function buildStatusReason(status: DecisionStatus, signals: StatusReasonSignals): string {
  const lowReliability = signals.reliabilityPct < BOOST_MIN_RELIABILITY_PCT;
  const highPriority = signals.priorityBand.toLowerCase() === "high";
  const negativeDelta = signals.revenueDelta < 0;

  if (status === "Pojacaj") {
    if (lowReliability) return "Signal je dobar, ali je pouzdanost niska; potvrditi pre veceg ulaganja.";
    if (highPriority && !negativeDelta) return "Visok prioritet i bolji scenario prihoda uz isticanje.";
    return "Stabilan signal za veci fokus pre nivelacije.";
  }

  if (status === "Zadrzi") {
    if (lowReliability) return "Niza pouzdanost podataka; odluku drzati konzervativnom dok se signal ne stabilizuje.";
    if (negativeDelta) return "Scenario prihoda je slabiji od markdown alternative; pratiti bez eskalacije.";
    return "Stabilan rezultat bez dovoljno jakog signala za promenu prioriteta.";
  }

  if (negativeDelta) return "Nizak prioritet i slab scenario prihoda; spustiti fokus.";
  return "Nedovoljno jak signal za investiciju u dodatnu vidljivost.";
}

function buildStatusTooltip(data: StatusTooltipData): string {
  return `${data.status}: ${data.statusReason} | Skor ${data.decisionScore} | Delta ${fmtRsd(data.revenueDelta)} | Pouzdanost ${fmtPct(data.reliabilityPct, 0)} | Poverenje ${data.confidence}`;
}

function reliabilityFromConfidence(confidence: string): number {
  const normalized = (confidence ?? "").toLowerCase();
  if (normalized === "high") return 90;
  if (normalized === "medium") return 65;
  if (normalized === "low") return 35;
  return 50;
}

export default function PreNivelacijaPriorityPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const requestIdRef = useRef(0);

  const [supplierId, setSupplierId] = useState<number | null>(null);
  const [seasonId, setSeasonId] = useState<number | null>(null);
  const [footwearTypeId, setFootwearTypeId] = useState<number | null>(null);
  const [minScore, setMinScore] = useState<number>(40);
  const [noSaleDaysMin, setNoSaleDaysMin] = useState<number>(14);
  const [activeFilters, setActiveFilters] = useState<ActiveFilters>({
    supplierId: null,
    seasonId: null,
    footwearTypeId: null,
    minScore: 40,
    noSaleDaysMin: 14,
  });

  const [page, setPage] = useState(1);
  const [data, setData] = useState<PreNivelacijaPriorityResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sortField, setSortField] = useState<SortField>("status");
  const [sortDir, setSortDir] = useState<SortDir>("desc");
  const [expandedArtikalId, setExpandedArtikalId] = useState<number | null>(null);
  const [focusFilter, setFocusFilter] = useState<FocusFilter>("all");

  const load = useCallback(async (filters: ActiveFilters, nextPage: number) => {
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);

    try {
      const result = await getPreNivelacijaPrioriteti({
        supplierId: filters.supplierId ?? undefined,
        seasonId: filters.seasonId ?? undefined,
        footwearTypeId: filters.footwearTypeId ?? undefined,
        minScore: filters.minScore,
        noSaleDaysMin: filters.noSaleDaysMin,
        page: nextPage,
        pageSize: 60,
      });

      if (requestId !== requestIdRef.current) return;
      setData(result);
      setExpandedArtikalId(null);
    } catch (reason) {
      if (requestId !== requestIdRef.current) return;
      setData(null);
      setError(reason instanceof Error ? reason.message : "Greska pri ucitavanju pre-nivelacija prioriteta.");
    } finally {
      if (requestId === requestIdRef.current) {
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    void load(activeFilters, page);
  }, [activeFilters, load, page]);

  const supplierOptions = useMemo(
    () => (data?.supplierLeaderboard ?? []).filter((item) => item.supplierId != null),
    [data?.supplierLeaderboard]
  );

  const seasonOptions = useMemo(() => {
    const map = new Map<number, string>();
    (data?.candidates ?? []).forEach((item) => {
      if (item.seasonId != null && item.season && item.season !== "N/A") {
        map.set(item.seasonId, item.season);
      }
    });

    return [...map.entries()]
      .map(([id, label]) => ({ id, label }))
      .sort((a, b) => a.label.localeCompare(b.label, "sr"));
  }, [data?.candidates]);

  const footwearTypeOptions = useMemo(() => {
    const map = new Map<number, string>();
    (data?.candidates ?? []).forEach((item) => {
      if (item.footwearTypeId != null && item.footwearType && item.footwearType !== "N/A") {
        map.set(item.footwearTypeId, item.footwearType);
      }
    });

    return [...map.entries()]
      .map(([id, label]) => ({ id, label }))
      .sort((a, b) => a.label.localeCompare(b.label, "sr"));
  }, [data?.candidates]);

  const decisionRows = useMemo<DecisionCandidate[]>(() => {
    const rows = data?.candidates ?? [];
    if (rows.length === 0) return [];

    const revenueValues = rows.map((item) => item.revenueDeltaHighlightVsMarkdown);
    const minRevenueDelta = Math.min(...revenueValues);
    const maxRevenueDelta = Math.max(...revenueValues);
    const deltaSpan = maxRevenueDelta - minRevenueDelta;

    return rows.map((item) => {
      const revenueDelta = item.revenueDeltaHighlightVsMarkdown;
      const marginDelta = item.marginDeltaHighlightVsMarkdown;
      const reliabilityPct = reliabilityFromConfidence(item.confidence);
      const scoreBase = clamp(item.preNivelacijaScore, 0, 100);
      const deltaNorm = deltaSpan > 0 ? clamp(((revenueDelta - minRevenueDelta) / deltaSpan) * 100, 0, 100) : 50;
      const staleRiskNorm = clamp((item.daysSinceLastSale / 90) * 100, 0, 100);

      const decisionScore = Math.round(
        scoreBase * 0.50 +
        deltaNorm * 0.20 +
        staleRiskNorm * 0.15 +
        reliabilityPct * 0.15
      );

      let status: DecisionStatus = "Smanji";
      if (decisionScore >= BOOST_SCORE_THRESHOLD) status = "Pojacaj";
      else if (decisionScore >= KEEP_SCORE_THRESHOLD) status = "Zadrzi";

      if (reliabilityPct < BOOST_MIN_RELIABILITY_PCT && status === "Pojacaj") status = "Zadrzi";
      if ((item.priorityBand ?? "").toLowerCase() === "low" && status === "Pojacaj") status = "Zadrzi";
      if (revenueDelta < 0 && status === "Pojacaj") status = "Zadrzi";

      const statusReason = buildStatusReason(status, {
        priorityBand: item.priorityBand,
        revenueDelta,
        reliabilityPct,
        decisionScore,
      });

      return {
        ...item,
        revenueDelta,
        marginDelta,
        reliabilityPct,
        decisionScore,
        status,
        statusReason,
      };
    });
  }, [data?.candidates]);

  const sortedRows = useMemo(() => {
    const rows = [...decisionRows];
    return rows.sort((a, b) => {
      let compare = 0;

      if (sortField === "sku") compare = a.sku.localeCompare(b.sku, "sr");
      else if (sortField === "supplierName") compare = a.supplierName.localeCompare(b.supplierName, "sr");
      else if (sortField === "preNivelacijaScore") compare = a.preNivelacijaScore - b.preNivelacijaScore;
      else if (sortField === "stockUnits") compare = a.stockUnits - b.stockUnits;
      else if (sortField === "daysSinceLastSale") compare = a.daysSinceLastSale - b.daysSinceLastSale;
      else if (sortField === "revenueDelta") compare = a.revenueDelta - b.revenueDelta;
      else if (sortField === "status") compare = STATUS_PRIORITY[a.status] - STATUS_PRIORITY[b.status];

      if (compare === 0) compare = a.decisionScore - b.decisionScore;
      return sortDir === "asc" ? compare : -compare;
    });
  }, [decisionRows, sortDir, sortField]);

  const candidateCounts = useMemo(() => {
    const boost = sortedRows.filter((row) => row.status === "Pojacaj").length;
    const keep = sortedRows.filter((row) => row.status === "Zadrzi").length;
    const reduce = sortedRows.filter((row) => row.status === "Smanji").length;
    const highPriority = sortedRows.filter((row) => (row.priorityBand ?? "").toLowerCase() === "high").length;
    return { boost, keep, reduce, highPriority };
  }, [sortedRows]);

  const filteredRows = useMemo(() => {
    if (focusFilter === "all") return sortedRows;
    if (focusFilter === "boost") return sortedRows.filter((row) => row.status === "Pojacaj");
    if (focusFilter === "keep") return sortedRows.filter((row) => row.status === "Zadrzi");
    if (focusFilter === "reduce") return sortedRows.filter((row) => row.status === "Smanji");
    if (focusFilter === "highPriority") return sortedRows.filter((row) => (row.priorityBand ?? "").toLowerCase() === "high");
    return sortedRows;
  }, [focusFilter, sortedRows]);

  const isDirty =
    supplierId !== activeFilters.supplierId ||
    seasonId !== activeFilters.seasonId ||
    footwearTypeId !== activeFilters.footwearTypeId ||
    minScore !== activeFilters.minScore ||
    noSaleDaysMin !== activeFilters.noSaleDaysMin;

  const supplierActionShare = useMemo(() => {
    const items = data?.supplierLeaderboard ?? [];
    if (items.length === 0) return [] as Array<{ name: string; sharePct: number; weekOverWeekRiskDeltaPct: number }>;

    const top = [...items].sort((a, b) => b.actionScore - a.actionScore).slice(0, 7);
    const total = top.reduce((sum, item) => sum + item.actionScore, 0);
    if (total <= 0) return [];

    return top.map((item) => ({
      name: item.supplierName,
      sharePct: (item.actionScore / total) * 100,
      weekOverWeekRiskDeltaPct: item.weekOverWeekRiskDeltaPct,
    }));
  }, [data?.supplierLeaderboard]);

  const selectedRow = useMemo(() => {
    if (expandedArtikalId == null) return null;
    return sortedRows.find((row) => row.artikalId === expandedArtikalId) ?? null;
  }, [expandedArtikalId, sortedRows]);

  const canGoPrev = page > 1;
  const canGoNext = data ? page * data.pageSize < data.totalCandidates : false;

  const toolbarFilters = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "supplierId", label: "Dobavljač", value: activeFilters.supplierId ?? "" },
      { key: "seasonId", label: "Sezona", value: activeFilters.seasonId ?? "" },
      { key: "footwearTypeId", label: "Tip obuće", value: activeFilters.footwearTypeId ?? "" },
      { key: "minScore", label: "Min. skor", value: activeFilters.minScore },
      { key: "noSaleDaysMin", label: "Min. dana bez prodaje", value: activeFilters.noSaleDaysMin },
      { key: "page", label: "Strana", value: page },
    ],
    [activeFilters.footwearTypeId, activeFilters.minScore, activeFilters.noSaleDaysMin, activeFilters.seasonId, activeFilters.supplierId, page]
  );

  const toolbarMetadata = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "generatedAtUtc", label: "Generisano", value: data?.generatedAtUtc ?? "" },
      { key: "formulaVersion", label: "Formula", value: data?.formulaVersion ?? "" },
      { key: "totalCandidates", label: "Total", value: data?.totalCandidates ?? 0 },
    ],
    [data?.formulaVersion, data?.generatedAtUtc, data?.totalCandidates]
  );

  const handleSort = (field: SortField) => {
    if (sortField === field) {
      setSortDir((current) => (current === "asc" ? "desc" : "asc"));
      return;
    }
    setSortField(field);
    setSortDir(field === "sku" || field === "supplierName" ? "asc" : "desc");
  };

  const handleApplyFilters = () => {
    setPage(1);
    setFocusFilter("all");
    setActiveFilters({
      supplierId,
      seasonId,
      footwearTypeId,
      minScore,
      noSaleDaysMin,
    });
  };

  const handleResetFilters = () => {
    setSupplierId(null);
    setSeasonId(null);
    setFootwearTypeId(null);
    setMinScore(40);
    setNoSaleDaysMin(14);
    setPage(1);
    setFocusFilter("all");
    setActiveFilters({
      supplierId: null,
      seasonId: null,
      footwearTypeId: null,
      minScore: 40,
      noSaleDaysMin: 14,
    });
  };

  const openCandidateDetail = (row: DecisionCandidate) => {
    saveAnalyticsDetailSnapshot(
      buildAnalyticsDetailSnapshot({
        table: "pre-nivelacija-prioriteti",
        recordId: String(row.artikalId),
        title: row.sku,
        subtitle: row.supplierName,
        columns: decisionColumns,
        row,
        metadata: [...toolbarFilters, ...toolbarMetadata],
      })
    );

    navigate(`/analitika/pre-nivelacija-prioriteti/${row.artikalId}`, {
      state: { backgroundLocation: location },
    });
  };

  return (
    <div className="pnp-decision-page">
      <header className="pnp-decision-header">
        <div>
          <h1 className="pnp-decision-title">Prioriteti pre-nivelacije</h1>
          <p className="pnp-decision-subtitle">
            Operativna podrška za odluke po SKU pre faze sniženja: gde treba pojačati izlaganje,
            šta zadržati pod nadzorom i šta spustiti iz fokusa.
          </p>
        </div>
        <div className="pnp-decision-generated">
          Generisano: {data?.generatedAtUtc ? new Date(data.generatedAtUtc).toLocaleString("sr-RS") : "-"}
        </div>
      </header>

      <section className="pnp-decision-filters">
        <label className="pnp-decision-field">
          <span>Dobavljač</span>
          <select value={supplierId ?? ""} onChange={(e) => setSupplierId(e.target.value ? Number(e.target.value) : null)}>
            <option value="">Svi</option>
            {supplierOptions.map((item) => (
              <option key={item.supplierId ?? item.supplierName} value={item.supplierId ?? ""}>{item.supplierName}</option>
            ))}
          </select>
        </label>

        <label className="pnp-decision-field">
          <span>Sezona</span>
          <select value={seasonId ?? ""} onChange={(e) => setSeasonId(e.target.value ? Number(e.target.value) : null)}>
            <option value="">Sve</option>
            {seasonOptions.map((item) => (
              <option key={item.id} value={item.id}>{item.label}</option>
            ))}
          </select>
        </label>

        <label className="pnp-decision-field">
          <span>Tip obuce</span>
          <select value={footwearTypeId ?? ""} onChange={(e) => setFootwearTypeId(e.target.value ? Number(e.target.value) : null)}>
            <option value="">Svi</option>
            {footwearTypeOptions.map((item) => (
              <option key={item.id} value={item.id}>{item.label}</option>
            ))}
          </select>
        </label>

        <label className="pnp-decision-field">
          <span>Min. skor</span>
          <input type="number" min={0} max={100} value={minScore} onChange={(e) => setMinScore(Number(e.target.value) || 0)} />
        </label>

        <label className="pnp-decision-field">
          <span>Min. dana bez prodaje</span>
          <input type="number" min={0} value={noSaleDaysMin} onChange={(e) => setNoSaleDaysMin(Number(e.target.value) || 0)} />
        </label>

        <div className="pnp-decision-actions">
          {isDirty ? <span className="pnp-dirty-indicator">Neprimenjeni filteri</span> : null}
          <button type="button" onClick={handleApplyFilters} disabled={loading}>Primeni</button>
          <button type="button" className="secondary" onClick={handleResetFilters} disabled={loading}>Reset</button>
        </div>
      </section>

      {error ? <div className="pnp-decision-message error">{error}</div> : null}
      {loading ? <div className="pnp-decision-message loading">Učitavam prioritete pre-nivelacije...</div> : null}

      {!loading && data ? (
        <>
          {data.alerts && data.alerts.length > 0 ? (
            <section className="pnp-alerts">
              {data.alerts.map((alert, i) => (
                <div key={i} className={`pnp-alert pnp-alert--${alert.severity}`}>
                  <span className="pnp-alert-icon">
                    {alert.severity === "critical" ? "⚠" : alert.severity === "warning" ? "⚡" : "ℹ"}
                  </span>
                  <span>
                    {alert.message}
                    {alert.supplierName ? ` (${alert.supplierName})` : ""}
                  </span>
                </div>
              ))}
            </section>
          ) : null}

          <section className="pnp-decision-kpis">
            <article className="pnp-decision-kpi analytics-kpi-card analytics-kpi-card--tone-info" data-note="SKU koji zadovoljavaju filtere i prag skora.">
              <span>Kandidati <InfoTip text="Ukupan broj SKU koji zadovoljavaju filtere i imaju aktivan signal pre nivelacije (pre-nivelacioni skor ≥ min. skora). Ovo su artikli koji imaju zalihu i prodajni signal dovoljan za intervenciju." /></span>
              <strong>{data.summary.candidatesCount}</strong>
            </article>
            <article className="pnp-decision-kpi analytics-kpi-card analytics-kpi-card--tone-success" data-note="Kandidati sa najjacim signalom za brzu intervenciju.">
              <span>Visok prioritet <InfoTip text="SKU u prioritetnoj bandi 'high' — imaju najjači kompozitni signal (visok skor zalihe + stagnacija prodaje). Ovo su artikli gde je intervencija pre nivelacije najhitnija." /></span>
              <strong>{data.summary.highPriorityCount}</strong>
            </article>
            <article className="pnp-decision-kpi analytics-kpi-card analytics-kpi-card--tone-warning" data-note="Ukupna zaliha kod SKU koji nose operativni rizik.">
              <span>Zaliha pod rizikom <InfoTip text="Ukupna zaliha u komadima svih prikazanih kandidatskih SKU (u skladu sa filterima). Iskazano u komadima, ne u RSD vrednosti. Veća zaliha bez prodaje = veći operativni rizik." /></span>
              <strong>{data.summary.totalStockAtRisk}</strong>
              <em>kom ukupno</em>
            </article>
            <article className="pnp-decision-kpi analytics-kpi-card analytics-kpi-card--tone-value" data-note="Procena prihoda ako se kandidati istaknu umesto snize.">
              <span>Projekcija povećanja prihoda <InfoTip text="Procenjeni prihod: scenario isticanja minus scenario sniženja za sve 'Pojačaj' kandidate. PROCENA — bazirana na scenariju sa istorijskim podacima prodaje, nije garantovani prihod. Tretirati kao relativni signal, ne kao apsolutnu predikciju." /></span>
              <strong>{fmtRsd(data.summary.expectedHighlightRevenueUplift)}</strong>
            </article>
            <article className="pnp-decision-kpi analytics-kpi-card analytics-kpi-card--tone-warning" data-note="Procena gubitka koji moze da se izbegne pre nivelacije.">
              <span>Izbegljivi gubitak od sniženja <InfoTip text="Procenjeni gubitak prihoda koji se može izbeći pravovremenom intervencijom pre nivelacije. PROCENA bazirana na scenario modelu (isticanje vs. sniženje u 30-dnevnom prozoru). Apsolutni iznos je okvirna procena — relativni odnos između SKU-ova je relevantniji." /></span>
              <strong className="trend-down">{fmtRsd(data.summary.estimatedAvoidableMarkdownLoss)}</strong>
            </article>
          </section>

          <section className="pnp-decision-panels">
            <article className="pnp-decision-card analytics-surface-panel">
              <h2>Koncentracija akcije po dobavljačima</h2>
              <p>Top dobavljači po action score u aktuelnom prioritetnom setu.</p>
              {supplierActionShare.length > 0 ? (
                <div className="pnp-decision-chart-wrap">
                  <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={260}>
                    <BarChart data={supplierActionShare} layout="vertical" margin={{ top: 12, right: 16, left: 8, bottom: 8 }}>
                      <CartesianGrid strokeDasharray="3 3" stroke="var(--border-default)" />
                      <XAxis type="number" tick={{ fill: "var(--text-secondary)", fontSize: 12 }} unit="%" />
                      <YAxis type="category" dataKey="name" width={180} tick={{ fill: "var(--text-primary)", fontSize: 12 }} />
                      <Tooltip content={<CustomSupplierTooltip />} />
                      <Bar dataKey="sharePct" fill="var(--accent-primary)" radius={[0, 8, 8, 0]} />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              ) : (
                <div className="pnp-decision-empty">Nema podataka za grafikon koncentracije.</div>
              )}
            </article>

            <article className="pnp-decision-card analytics-surface-panel">
              <div className="pnp-decision-table-head">
                <div>
                  <h2>Prioritetna lista SKU</h2>
                  <p>
                    Pojačaj: {candidateCounts.boost} | Zadrži: {candidateCounts.keep} | Smanji: {candidateCounts.reduce} | Visok prioritet: {candidateCounts.highPriority}
                  </p>
                </div>
                <div className="pnp-decision-table-controls">
                  <button type="button" onClick={() => canGoPrev && setPage((p) => p - 1)} disabled={!canGoPrev || loading}>Prethodna</button>
                  <span>Strana {page}</span>
                  <button type="button" onClick={() => canGoNext && setPage((p) => p + 1)} disabled={!canGoNext || loading}>Sledeća</button>
                </div>
                <AnalyticsTableToolbar
                  tableKey="pre-nivelacija-prioriteti"
                  tableTitle="Pre-nivelacija decision support"
                  columns={decisionColumns}
                  rows={sortedRows}
                  filters={toolbarFilters}
                  metadata={toolbarMetadata}
                  defaultOrientation="landscape"
                />
              </div>

              <div className="pnp-focus-tabs" role="tablist">
                {(["all", "boost", "keep", "reduce", "highPriority"] as FocusFilter[]).map((f) => {
                  const count =
                    f === "all" ? sortedRows.length
                    : f === "boost" ? candidateCounts.boost
                    : f === "keep" ? candidateCounts.keep
                    : f === "reduce" ? candidateCounts.reduce
                    : candidateCounts.highPriority;
                  const tabClass = f === "boost" ? "tab-boost" : f === "keep" ? "tab-keep" : f === "reduce" ? "tab-reduce" : f === "highPriority" ? "tab-high" : "";
                  return (
                    <button
                      key={f}
                      role="tab"
                      type="button"
                      aria-selected={focusFilter === f}
                      className={`pnp-focus-tab ${tabClass}${focusFilter === f ? " active" : ""}`.trim()}
                      onClick={() => setFocusFilter(f)}
                    >
                      {FOCUS_LABELS[f]} ({count})
                    </button>
                  );
                })}
              </div>

              <div className="pnp-decision-table-wrap">
                <table className="pnp-decision-table">
                  <thead>
                    <tr>
                      <th>
                        <button type="button" onClick={() => handleSort("sku")}>SKU{sortMarker("sku", sortField, sortDir)}</button>
                      </th>
                      <th>
                        <button type="button" onClick={() => handleSort("supplierName")}>Dobavljač{sortMarker("supplierName", sortField, sortDir)}</button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("preNivelacijaScore")}>Skor{sortMarker("preNivelacijaScore", sortField, sortDir)}</button>
                        <InfoTip text="Skor nivelacije (0–100): kompozitni signal od pritiska zalihe, brzine prodaje (sell-through), dana bez prodaje, šanse za sniženje i marže potencijala. Viši skor = veći prioritet za intervenciju." />
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("stockUnits")}>Zaliha{sortMarker("stockUnits", sortField, sortDir)}</button>
                        <InfoTip text="Tekuća raspoloživa zaliha ovog SKU u komadima. Viša zaliha uz nisku prodaju = veći rizik i veći prioritet za akciju." />
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("daysSinceLastSale")}>Dana bez prod.{sortMarker("daysSinceLastSale", sortField, sortDir)}</button>
                        <InfoTip text="Broj kalendarskih dana od poslednje evidentirane prodaje ovog SKU. Veći broj = jači signal stagnacije zalihe. Vrednosti > 30 dana zaslužuju prioritetnu pažnju." />
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("revenueDelta")}>Isticanje vs sniženje{sortMarker("revenueDelta", sortField, sortDir)}</button>
                        <InfoTip text="Razlika procenjenog prihoda u 30-dnevnom prozoru: scenario isticanja minus scenario sniženja. Pozitivna vrednost = isplativije je istaknuti artikal pre nivelacije nego ga odmah sniziti. Negativno = sniženje verovatno donosi više." />
                      </th>
                      <th className="align-center">
                        Pouzdanost
                        <InfoTip text="Pouzdanost signala: Visoko / Srednje / Nisko. Okvirne vrednosti: Visoko ≈ 90%, Srednje ≈ 65%, Nisko ≈ 35%. Nije statistički precizan procenat — bazira se na confidence tagu iz modela. Nisko poverenje automatski spušta preporuku 'Pojačaj' → 'Zadrži'." />
                      </th>
                      <th>
                        <button type="button" onClick={() => handleSort("status")}>Preporuka{sortMarker("status", sortField, sortDir)}</button>
                        <InfoTip text="Sistemska preporuka: Pojačaj / Zadrži / Smanji — bazirana na oceni preporuke (0–100), pouzdanosti signala i prioritetnoj bandi. Formula: skor nivelacije×50% + delta prihoda×20% + stagnacija×15% + pouzdanost×15%. Pojačaj ≥68, Zadrži 43–67, Smanji <43." />
                      </th>
                      <th className="align-center">Detalj</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredRows.length === 0 ? (
                      <tr>
                        <td colSpan={9} className="pnp-decision-empty-row">Nema podataka za izabrane filtere.</td>
                      </tr>
                    ) : (
                      filteredRows.map((row) => {
                        const expanded = expandedArtikalId === row.artikalId;
                        const reliabilityLabel = row.reliabilityPct >= 70 ? "Visoko" : row.reliabilityPct >= BOOST_MIN_RELIABILITY_PCT ? "Srednje" : "Nisko";
                        const reliabilityClass = row.reliabilityPct >= 70 ? "signal-strong" : row.reliabilityPct >= BOOST_MIN_RELIABILITY_PCT ? "signal-watch" : "signal-weak";
                        return (
                          <tr key={row.artikalId} className={expanded ? "expanded-row" : ""}>
                            <td>{row.sku}</td>
                            <td title={row.supplierName}>{row.supplierName}</td>
                            <td className="align-right">
                              <div className="pnp-score-cell">
                                <span>{row.preNivelacijaScore.toFixed(1)}</span>
                                <div
                                  className="pnp-score-mini-bar"
                                  style={{ width: `${clamp(row.preNivelacijaScore, 0, 100)}%` }}
                                  data-level={row.preNivelacijaScore >= 68 ? "high" : row.preNivelacijaScore >= 43 ? "mid" : "low"}
                                />
                              </div>
                            </td>
                            <td className="align-right">{row.stockUnits}</td>
                            <td className="align-right">{row.daysSinceLastSale}</td>
                            <td className={`align-right ${row.revenueDelta >= 0 ? "trend-up" : "trend-down"}`}>{fmtRsd(row.revenueDelta)}</td>
                            <td className="align-center">
                              <span className={`pnp-signal-pill ${reliabilityClass}`}>{reliabilityLabel}</span>
                            </td>
                            <td>
                              <span
                                className={statusClass(row.status)}
                                title={buildStatusTooltip(row)}
                                aria-label={buildStatusTooltip(row)}
                              >
                                {row.status}
                              </span>
                            </td>
                            <td className="align-center">
                              <button
                                type="button"
                                className="pnp-decision-detail-btn"
                                onClick={() => setExpandedArtikalId(expanded ? null : row.artikalId)}
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
              </div>
            </article>
          </section>

          {selectedRow ? (
            <section className="pnp-decision-detail">
              <div className="pnp-decision-detail-head">
                <h3>Detalj odluke: {selectedRow.sku}</h3>
                <button type="button" onClick={() => openCandidateDetail(selectedRow)}>Otvori puni detalj</button>
              </div>

              <div className="pnp-decision-detail-grid">
                <article>
                  <span>Dobavljac</span>
                  <strong title={selectedRow.supplierName}>{selectedRow.supplierName}</strong>
                </article>
                <article>
                  <span>Prioritetna kategorija</span>
                  <strong>{selectedRow.priorityBand}</strong>
                </article>
                <article>
                  <span>Scenario isticanje (30d prihod)</span>
                  <strong>{fmtRsd(selectedRow.scenarioHighlightNow.expectedRevenue30d)}</strong>
                </article>
                <article>
                  <span>Scenario snizenje (30d prihod)</span>
                  <strong>{fmtRsd(selectedRow.scenarioMarkdownNow.expectedRevenue30d)}</strong>
                </article>
                <article>
                  <span>Delta prihoda</span>
                  <strong className={selectedRow.revenueDelta >= 0 ? "trend-up" : "trend-down"}>{fmtRsd(selectedRow.revenueDelta)}</strong>
                </article>
                <article>
                  <span>Delta marze</span>
                  <strong className={selectedRow.marginDelta >= 0 ? "trend-up" : "trend-down"}>{fmtRsd(selectedRow.marginDelta)}</strong>
                </article>
                <article>
                  <span>Zaliha (kom.)</span>
                  <strong>{selectedRow.stockUnits}</strong>
                </article>
                <article>
                  <span>Dana bez prodaje</span>
                  <strong>{selectedRow.daysSinceLastSale}</strong>
                </article>
                <article>
                  <span>Ocena preporuke</span>
                  <strong>{selectedRow.decisionScore}</strong>
                </article>
              </div>

              <p className="pnp-decision-reason">
                <strong>Razlog preporuke:</strong> {selectedRow.statusReason}
              </p>

              {selectedRow.scoreBreakdown ? (
                <div className="pnp-score-breakdown">
                  <h4>Komponente score-a</h4>
                  <div className="pnp-score-grid">
                    {[
                      { label: "Pritisak zalihe", value: selectedRow.scoreBreakdown.stockPressure },
                      { label: "Rizik brzine prodaje", value: selectedRow.scoreBreakdown.velocityRisk },
                      { label: "Rizik starosti prodaje", value: selectedRow.scoreBreakdown.recencyRisk },
                      { label: "Markdown šansa", value: selectedRow.scoreBreakdown.markdownOpportunity },
                      { label: "Margin potencijal", value: selectedRow.scoreBreakdown.marginPotential },
                      { label: "Sezonski boost", value: selectedRow.scoreBreakdown.seasonRecencyBoost },
                    ].map((c) => (
                      <div key={c.label} className="pnp-score-component">
                        <span>{c.label}</span>
                        <div className="pnp-score-bar-wrap">
                          <div className="pnp-score-bar" style={{ width: `${clamp(c.value, 0, 100)}%` }} />
                        </div>
                        <strong>{c.value.toFixed(1)}</strong>
                      </div>
                    ))}
                  </div>
                </div>
              ) : null}
            </section>
          ) : null}

          {data.queues && (data.queues.highlightNow.length > 0 || data.queues.monitor.length > 0 || data.queues.likelyMarkdownSoon.length > 0) ? (
            <section className="pnp-queues">
              <h2 className="pnp-queues-title">
                Redovi čekanja
                <InfoTip text="SKU raspoređeni u operativne kategorije: 'Odmah istaknuti' (Pojačaj), 'Pod nadzorom' (Zadrži), 'Verovatno sniženje' (Smanji). Raspored se bazira na prioritetnoj bandi i snazi signal kombinacije." />
              </h2>
              <div className="pnp-queues-grid">
                <article className="pnp-queue-panel pnp-queue-panel--boost">
                  <h3>Odmah istaknuti ({data.queues.highlightNow.length})</h3>
                  {data.queues.highlightNow.length === 0 ? (
                    <p className="pnp-queue-empty">Nema SKU u ovom redu.</p>
                  ) : (
                    data.queues.highlightNow.map((item) => (
                      <div key={item.artikalId} className="pnp-queue-item">
                        <div>
                          <div className="pnp-queue-item-sku">{item.sku}</div>
                          <div className="pnp-queue-item-supplier">{item.supplierName}</div>
                        </div>
                        <span className={`pnp-decision-status ${item.priorityBand.toLowerCase() === "high" ? "status-boost" : "status-keep"}`}>
                          {item.priorityBand}
                        </span>
                      </div>
                    ))
                  )}
                </article>
                <article className="pnp-queue-panel pnp-queue-panel--keep">
                  <h3>Pod nadzorom ({data.queues.monitor.length})</h3>
                  {data.queues.monitor.length === 0 ? (
                    <p className="pnp-queue-empty">Nema SKU u ovom redu.</p>
                  ) : (
                    data.queues.monitor.map((item) => (
                      <div key={item.artikalId} className="pnp-queue-item">
                        <div>
                          <div className="pnp-queue-item-sku">{item.sku}</div>
                          <div className="pnp-queue-item-supplier">{item.supplierName}</div>
                        </div>
                        <span className={`pnp-decision-status ${item.priorityBand.toLowerCase() === "high" ? "status-boost" : "status-keep"}`}>
                          {item.priorityBand}
                        </span>
                      </div>
                    ))
                  )}
                </article>
                <article className="pnp-queue-panel pnp-queue-panel--reduce">
                  <h3>Verovatni markdown ({data.queues.likelyMarkdownSoon.length})</h3>
                  {data.queues.likelyMarkdownSoon.length === 0 ? (
                    <p className="pnp-queue-empty">Nema SKU u ovom redu.</p>
                  ) : (
                    data.queues.likelyMarkdownSoon.map((item) => (
                      <div key={item.artikalId} className="pnp-queue-item">
                        <div>
                          <div className="pnp-queue-item-sku">{item.sku}</div>
                          <div className="pnp-queue-item-supplier">{item.supplierName}</div>
                        </div>
                        <span className="pnp-decision-status status-reduce">{item.priorityBand}</span>
                      </div>
                    ))
                  )}
                </article>
              </div>
            </section>
          ) : null}
        </>
      ) : null}
    </div>
  );
}

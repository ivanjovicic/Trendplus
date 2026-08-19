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
import AnalyticsControlBar, { type AnalyticsControlBarChip, type AnalyticsControlBarField } from "../components/analytics/AnalyticsControlBar";
import AnalyticsDataTable from "../components/analytics/AnalyticsDataTable";
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import AnalyticsTrustHeader from "../components/analytics/AnalyticsTrustHeader";
import AnalyticsErrorState from "../components/analytics/AnalyticsErrorState";
import AnalyticsEmptyState from "../components/analytics/AnalyticsEmptyState";
import InfoTip from "../components/ui/InfoTip";
import { buildAnalyticsDetailSnapshot, saveAnalyticsDetailSnapshot } from "../services/analyticsTableState";
import { getPreNivelacijaPrioriteti } from "../services/preNivelacijaApi";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import type { PreNivelacijaPriorityResponse, PreNivelacijaRecommendation, PreNivelacijaSkuCandidate } from "../types/preNivelacija";
import { CHART_TOOLTIP_STYLE } from "../utils/chartTooltipStyle";
import { fmtPct, fmtRsd } from "../utils/analyticsFormatters";
import { analyticsMetricDescriptions } from "../utils/analyticsMetricDescriptions";
import {
  getAnalyticsMetaMessage,
  isAnalyticsMetaInsufficient,
  isAnalyticsMetaWarning,
  shouldShowAnalyticsEmptyState,
} from "../utils/analyticsResponseMeta";
import {
  RECOMMENDATION_CONFIDENCE_LABEL,
  RECOMMENDATION_RELIABILITY_LABEL,
  RECOMMENDATION_SIGNAL_UNAVAILABLE,
  RECOMMENDATION_STATUS_PRIORITY,
  normalizeRecommendationPct,
  normalizeRecommendationQualityStatus,
  recommendationQualityLabel,
  recommendationQualityStyle,
  recommendationReasonHints,
  recommendationStatusLabel,
  recommendationStatusTone,
  recommendationStatusTooltipBrief,
  type RecommendationQualityStatus,
} from "../utils/canonicalRecommendationSemantics";
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
type DecisionStatus = PreNivelacijaRecommendation["status"];

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
  confidencePct: number;
  confidenceAvailable: boolean;
  reliabilityAvailable: boolean;
  status: DecisionStatus;
  statusReason: string;
  dataQualityStatus: RecommendationQualityStatus;
  reasonCodes: string[];
};

type FocusFilter = "all" | "increaseFocus" | "maintain" | "review" | "doNotTrust" | "insufficientData" | "highPriority";
const FOCUS_LABELS: Record<FocusFilter, string> = {
  all: "Sve",
  increaseFocus: recommendationStatusLabel("increase_focus"),
  maintain: recommendationStatusLabel("maintain"),
  review: recommendationStatusLabel("review"),
  doNotTrust: recommendationStatusLabel("do_not_trust"),
  insufficientData: "Nedovoljno podataka",
  highPriority: "Visok prioritet",
};

const STATUS_PRIORITY: Record<DecisionStatus, number> = {
  ...RECOMMENDATION_STATUS_PRIORITY,
};

const decisionColumns: AnalyticsTableColumn<DecisionCandidate>[] = [
  { key: "sku", header: "SKU", dataType: "text" },
  { key: "supplierName", header: "Dobavljač", dataType: "text" },
  { key: "preNivelacijaScore", header: "Skor nivelacije", dataType: "number" },
  { key: "stockUnits", header: "Zaliha (kom)", dataType: "number" },
  { key: "daysSinceLastSale", header: "Dana bez prodaje", dataType: "number" },
  { key: "revenueDelta", header: "Isticanje vs sniženje (prihod)", dataType: "currency" },
  { key: "reliabilityPct", header: RECOMMENDATION_RELIABILITY_LABEL, dataType: "number" },
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
        <p style={{ margin: "4px 0 0", fontSize: "12px", color: wowPct >= 0 ? "var(--error, var(--theme-color-ef4444, #ef4444))" : "var(--success, var(--theme-color-16a34a, #16a34a))" }}>
          Sedm. promena rizika: {wowPct >= 0 ? "+" : ""}{fmtPct(wowPct, 1)}
        </p>
      ) : null}
    </div>
  );
}

function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, value));
}

function sortMarker(field: SortField, activeField: SortField, dir: SortDir): string {
  if (field !== activeField) return "";
  return dir === "asc" ? " ^" : " v";
}

function statusClass(status: DecisionStatus): string {
  const tone = recommendationStatusTone(status);
  if (tone === "boost") return "pnp-decision-status status-boost";
  if (tone === "keep") return "pnp-decision-status status-keep";
  if (tone === "review") return "pnp-decision-status status-review";
  if (tone === "reduce") return "pnp-decision-status status-reduce";
  return "pnp-decision-status status-na";
}

function statusDisplayLabel(status: DecisionStatus): string {
  return recommendationStatusLabel(status);
}

function reliabilitySignalDisplay(row: DecisionCandidate): { label: string; className: string; title?: string } {
  if (!row.reliabilityAvailable) {
    return {
      label: "Nije dostupno",
      className: "pnp-signal-pill signal-na",
      title: RECOMMENDATION_SIGNAL_UNAVAILABLE,
    };
  }

  const label = fmtPct(row.reliabilityPct, 0);
  return {
    label,
    className: "pnp-signal-pill signal-na",
    title: `${RECOMMENDATION_RELIABILITY_LABEL}: ${label}`,
  };
}

function isHighPriorityCandidate(row: DecisionCandidate): boolean {
  return (row.priorityBand ?? "").toLowerCase() === "high" && row.status !== "insufficient_data";
}

type StatusTooltipData = {
  status: DecisionStatus;
  statusReason: string;
  decisionScore: number;
  revenueDelta: number;
  reliabilityPct: number;
  confidencePct: number;
  reliabilityAvailable: boolean;
  confidenceAvailable: boolean;
  dataQualityStatus: RecommendationQualityStatus;
  reasonCodes: string[];
};

function buildStatusTooltip(data: StatusTooltipData): string {
  const reliabilityText = data.reliabilityAvailable ? fmtPct(data.reliabilityPct, 0) : RECOMMENDATION_SIGNAL_UNAVAILABLE;
  const confidenceText = data.confidenceAvailable ? fmtPct(data.confidencePct, 0) : RECOMMENDATION_SIGNAL_UNAVAILABLE;
  const qualityText = recommendationQualityLabel(data.dataQualityStatus);
  const hintText = recommendationReasonHints(data.reasonCodes).join(" | ");
  return `${statusDisplayLabel(data.status)}: ${data.statusReason} | ${recommendationStatusTooltipBrief(data.status)} | Ocena ${data.decisionScore} | Delta ${fmtRsd(data.revenueDelta)} | ${RECOMMENDATION_RELIABILITY_LABEL} ${reliabilityText} | ${RECOMMENDATION_CONFIDENCE_LABEL} ${confidenceText} | Kvalitet ${qualityText}${hintText ? ` | Napomene: ${hintText}` : ""}`;
}

function getRecommendedNextStep(status: DecisionStatus): string {
  if (status === "increase_focus") return "Pojačaj izlaganje i proveri dopunu pre nivelacije.";
  if (status === "maintain") return "Zadrži pod nadzorom i prati naredni ciklus prodaje.";
  if (status === "review") return "Pregledaj signal pre odluke o jačem isticanju ili markdown-u.";
  if (status === "do_not_trust") return "Ne donosi odluku dok ne proveriš podatke i poslednju prodaju.";
  return "Sačekaj jači signal ili proširi kontekst pre odluke.";
}

function hasReasonCode(reasonCodes: string[], targets: string[]): boolean {
  const normalizedTargets = new Set(targets.map((value) => value.trim().toLowerCase()));
  return reasonCodes.some((code) => normalizedTargets.has((code ?? "").trim().toLowerCase()));
}

function hasMissingCostSignal(reasonCodes: string[]): boolean {
  return hasReasonCode(reasonCodes, ["missing_cost", "missing_cost_coverage"]);
}

function hasSparseSalesSignal(reasonCodes: string[]): boolean {
  return hasReasonCode(reasonCodes, ["sparse_sales", "tiny_sample", "insufficient_history"]);
}

function canShowMarkdownMarginSignal(row: DecisionCandidate): boolean {
  return !hasMissingCostSignal(row.reasonCodes)
    && row.status !== "insufficient_data"
    && row.dataQualityStatus !== "critical";
}

function hasLimitedMarkdownSignal(row: DecisionCandidate): boolean {
  return !row.reliabilityAvailable
    || !row.confidenceAvailable
    || row.dataQualityStatus !== "good"
    || row.status === "insufficient_data"
    || hasMissingCostSignal(row.reasonCodes)
    || hasSparseSalesSignal(row.reasonCodes);
}

function getMarkdownSignalLimitMessage(row: DecisionCandidate): string {
  if (hasMissingCostSignal(row.reasonCodes)) {
    return "Maržni scenario nije dostupan bez pouzdanog troška, pa signal treba čitati samo kao scenario prihoda.";
  }
  if (hasSparseSalesSignal(row.reasonCodes)) {
    return "Signal ima mali ili redak prodajni uzorak, pa scenario treba potvrditi pre poslovne odluke.";
  }
  if (!row.reliabilityAvailable || !row.confidenceAvailable || row.dataQualityStatus !== "good" || row.status === "insufficient_data") {
    return "Proveri pouzdanost, sigurnost preporuke i kvalitet podataka pre jače intervencije.";
  }

  return "Pouzdanost i kvalitet podataka ne pokazuju blokirajuće rizike za ovu preporuku.";
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
      setError(reason instanceof Error ? reason.message : "Greška pri učitavanju pre-nivelacija prioriteta.");
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

    return rows.map((item) => {
      const recommendation = item.recommendation;
      const revenueDelta = item.revenueDeltaHighlightVsMarkdown;
      const marginDelta = item.marginDeltaHighlightVsMarkdown;
      const confidencePctValue = normalizeRecommendationPct(recommendation.confidencePct);
      const reliabilityPctValue = normalizeRecommendationPct(recommendation.reliabilityPct ?? item.reliabilityPct);

      return {
        ...item,
        revenueDelta,
        marginDelta,
        confidencePct: confidencePctValue ?? 0,
        confidenceAvailable: confidencePctValue != null,
        reliabilityAvailable: reliabilityPctValue != null,
        reliabilityPct: reliabilityPctValue ?? 0,
        status: recommendation.status,
        statusReason: recommendation.summary,
        dataQualityStatus: normalizeRecommendationQualityStatus(recommendation.dataQualityStatus),
        reasonCodes: recommendation.reasonCodes ?? [],
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
      if (compare === 0) compare = a.confidencePct - b.confidencePct;
      return sortDir === "asc" ? compare : -compare;
    });
  }, [decisionRows, sortDir, sortField]);

  const candidateCounts = useMemo(() => {
    const increaseFocus = sortedRows.filter((row) => row.status === "increase_focus").length;
    const maintain = sortedRows.filter((row) => row.status === "maintain").length;
    const review = sortedRows.filter((row) => row.status === "review").length;
    const doNotTrust = sortedRows.filter((row) => row.status === "do_not_trust").length;
    const insufficientData = sortedRows.filter((row) => row.status === "insufficient_data").length;
    const highPriority = sortedRows.filter(isHighPriorityCandidate).length;
    return { increaseFocus, maintain, review, doNotTrust, insufficientData, highPriority };
  }, [sortedRows]);

  const filteredRows = useMemo(() => {
    if (focusFilter === "all") return sortedRows;
    if (focusFilter === "increaseFocus") return sortedRows.filter((row) => row.status === "increase_focus");
    if (focusFilter === "maintain") return sortedRows.filter((row) => row.status === "maintain");
    if (focusFilter === "review") return sortedRows.filter((row) => row.status === "review");
    if (focusFilter === "doNotTrust") return sortedRows.filter((row) => row.status === "do_not_trust");
    if (focusFilter === "insufficientData") return sortedRows.filter((row) => row.status === "insufficient_data");
    if (focusFilter === "highPriority") return sortedRows.filter(isHighPriorityCandidate);
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
  const dataMeta = data?.meta ?? null;
  const dataMetaMessage = getAnalyticsMetaMessage(dataMeta);
  const showMetaWarning = !loading && !error && isAnalyticsMetaWarning(dataMeta);
  const showEmptyState = !loading && !error && Boolean(data) && decisionRows.length === 0;
  const showInsufficientEmptyState = shouldShowAnalyticsEmptyState(dataMeta, decisionRows.length) && isAnalyticsMetaInsufficient(dataMeta);
  const emptyStateVariant: "no_data" | "insufficient_data" | "filtered_out" =
    showInsufficientEmptyState
      ? "insufficient_data"
      : focusFilter !== "all"
        ? "filtered_out"
        : "no_data";
  const emptyStateTitle =
    emptyStateVariant === "insufficient_data"
      ? "Signal nije dovoljno jak za prioritetnu listu."
      : emptyStateVariant === "filtered_out"
        ? "Nema rezultata za trenutne filtere."
        : "Nema kandidata za pre-nivelaciju.";
  const emptyStateMessage =
    emptyStateVariant === "insufficient_data"
      ? "Ne prikazujemo automatsku preporuku jer signal nije dovoljno jak."
      : emptyStateVariant === "filtered_out"
        ? "Promenite filtere dobavljača, sezone ili tipa obuće."
        : "Nema kandidata koji ispunjavaju trenutne filtere za pre-nivelacioni prioritet.";
  const emptyStateReason = dataMeta?.emptyReason ?? dataMetaMessage ?? null;
  const safeEmptyStateReason = emptyStateReason && /period/i.test(emptyStateReason) ? null : emptyStateReason;

  const attentionNotices = useMemo(() => {
    const notices: Array<{ key: string; title: string; detail: string; tone: "info" | "warning" | "critical" }> = [];

    if (candidateCounts.highPriority > 0) {
      notices.push({
        key: "high-priority",
        title: `${candidateCounts.highPriority} SKU traži brzu proveru`,
        detail: "Visok prioritet znači da je signal dovoljno jak da odmah pregledaš izlaganje, zalihu i sledeći korak.",
        tone: "info",
      });
    }

    const limitedSignalCount = candidateCounts.doNotTrust + candidateCounts.insufficientData;
    if (limitedSignalCount > 0) {
      notices.push({
        key: "limited-signal",
        title: `${limitedSignalCount} SKU ima ograničen signal`,
        detail: "Ove preporuke traže dodatnu proveru kvaliteta podataka, pouzdanosti ili poslednje prodaje pre odluke.",
        tone: "warning",
      });
    }

    if (showMetaWarning) {
      notices.push({
        key: "meta-warning",
        title: "Prikaz je delimičan ili fallback",
        detail: dataMetaMessage ?? "Proverite analytics refresh status i data quality signal pre jačih odluka.",
        tone: "critical",
      });
    } else if (candidateCounts.review > 0) {
      notices.push({
        key: "review",
        title: `${candidateCounts.review} SKU je za ručni pregled`,
        detail: "Pregledaj razlog preporuke i sledeći korak pre nego što artikal pojačaš ili spustiš iz fokusa.",
        tone: "warning",
      });
    }

    return notices.slice(0, 3);
  }, [candidateCounts.doNotTrust, candidateCounts.highPriority, candidateCounts.insufficientData, candidateCounts.review, dataMetaMessage, showMetaWarning]);

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

  const controlBarChips = useMemo<AnalyticsControlBarChip[]>(() => {
    const selectedSupplier = supplierOptions.find((item) => item.supplierId === supplierId);
    const selectedSeason = seasonOptions.find((item) => item.id === seasonId);
    const selectedFootwearType = footwearTypeOptions.find((item) => item.id === footwearTypeId);

    return [
      {
        key: "supplier",
        label: "Dobavljač",
        value: selectedSupplier?.supplierName ?? "Svi",
      },
      {
        key: "season",
        label: "Sezona",
        value: selectedSeason?.label ?? "Sve",
      },
      {
        key: "footwear",
        label: "Tip obuće",
        value: selectedFootwearType?.label ?? "Svi",
      },
      {
        key: "high-priority",
        label: "Visok prioritet",
        value: candidateCounts.highPriority.toLocaleString("sr-RS"),
        tone: "success",
      },
      {
        key: "limited-signal",
        label: "Ograničen signal",
        value: (candidateCounts.doNotTrust + candidateCounts.insufficientData).toLocaleString("sr-RS"),
        tone: "warning",
      },
    ];
  }, [candidateCounts.doNotTrust, candidateCounts.highPriority, candidateCounts.insufficientData, footwearTypeId, footwearTypeOptions, seasonId, seasonOptions, supplierId, supplierOptions]);

  const controlBarFields = useMemo<AnalyticsControlBarField[]>(() => [
    {
      key: "supplierId",
      label: "Dobavljač",
      control: (
        <select value={supplierId ?? ""} onChange={(e) => setSupplierId(e.target.value ? Number(e.target.value) : null)}>
          <option value="">Svi</option>
          {supplierOptions.map((item) => (
            <option key={item.supplierId ?? item.supplierName} value={item.supplierId ?? ""}>{item.supplierName}</option>
          ))}
        </select>
      ),
    },
    {
      key: "seasonId",
      label: "Sezona",
      control: (
        <select value={seasonId ?? ""} onChange={(e) => setSeasonId(e.target.value ? Number(e.target.value) : null)}>
          <option value="">Sve</option>
          {seasonOptions.map((item) => (
            <option key={item.id} value={item.id}>{item.label}</option>
          ))}
        </select>
      ),
    },
    {
      key: "footwearTypeId",
      label: "Tip obuće",
      control: (
        <select value={footwearTypeId ?? ""} onChange={(e) => setFootwearTypeId(e.target.value ? Number(e.target.value) : null)}>
          <option value="">Svi</option>
          {footwearTypeOptions.map((item) => (
            <option key={item.id} value={item.id}>{item.label}</option>
          ))}
        </select>
      ),
    },
    {
      key: "minScore",
      label: "Min. skor",
      control: (
        <input type="number" min={0} max={100} value={minScore} onChange={(e) => setMinScore(Number(e.target.value) || 0)} />
      ),
    },
    {
      key: "noSaleDaysMin",
      label: "Min. dana bez prodaje",
      control: (
        <input type="number" min={0} value={noSaleDaysMin} onChange={(e) => setNoSaleDaysMin(Number(e.target.value) || 0)} />
      ),
    },
  ], [footwearTypeId, footwearTypeOptions, minScore, noSaleDaysMin, seasonId, seasonOptions, supplierId, supplierOptions]);

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
      <AnalyticsTrustHeader
        title="Prioriteti pre-nivelacije"
        description="Operativna podrška za odluke po SKU pre faze sniženja."
        periodFrom={null}
        periodTo={null}
        lastRefreshAt={data?.generatedAtUtc ?? null}
        dataSource="Nivelacija analytics"
        mode="recommendation"
        dataQualityStatus={dataMeta?.dataQualityStatus ?? null}
        isPartial={showMetaWarning}
        emptyStateReason={showEmptyState ? (dataMetaMessage ?? null) : null}
        methodologyHref="/analytics/data-quality"
        dataQualityHref="/analytics/data-quality"
        refreshStatusHref="/admin/configuration?panel=workers"
        compact
      />
      <AnalyticsControlBar
        title="Opseg i filteri"
        description="Dobavljač, sezona, tip obuće i pragovi ostaju ovde; lista ispod ostaje fokusirana na pre-nivelaciju."
        chips={controlBarChips}
        primaryAction={{
          key: "apply",
          label: loading ? "Učitavanje..." : "Primeni filtere",
          onClick: handleApplyFilters,
          disabled: loading || !isDirty,
        }}
        secondaryActions={[
          {
            key: "reset",
            label: "Reset filtera",
            onClick: handleResetFilters,
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

      {error ? (
        <AnalyticsErrorState
          title="Podaci trenutno nisu dostupni"
          message={error || "Ne prikazujemo nule dok nije potvrđen prazan rezultat."}
          onRetry={() => void load(activeFilters, page)}
          helpHref="/analytics/data-quality"
        />
      ) : null}
      {showMetaWarning ? (
        <div className="pnp-decision-message warning" role="status">
          Prikazani podaci su delimični. {dataMetaMessage ?? "Proverite status osvežavanja i signal kvaliteta podataka."}
        </div>
      ) : null}
      {showEmptyState ? (
        <AnalyticsEmptyState
          variant={emptyStateVariant}
          title={emptyStateTitle}
          message={emptyStateMessage}
          actions={[
            { label: "Promenite filtere dobavljača, sezone ili tipa obuće." },
            { label: "Proverite kvalitet podataka.", href: "/analytics/data-quality" },
          ]}
          dataQualityHref="/analytics/data-quality"
          refreshStatusHref="/admin/configuration?panel=workers"
          emptyReason={safeEmptyStateReason}
          onRetry={() => void load(activeFilters, page)}
        />
      ) : null}
      {loading ? <div className="pnp-decision-message loading">Učitavam prioritete pre-nivelacije...</div> : null}

      {!loading && data && !showEmptyState ? (
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

          {attentionNotices.length > 0 ? (
            <section className="pnp-attention-strip" aria-label="Prioriteti i ograničenja signala">
              {attentionNotices.map((notice) => (
                <article key={notice.key} className={`pnp-attention-card pnp-attention-card--${notice.tone}`}>
                  <strong>{notice.title}</strong>
                  <p>{notice.detail}</p>
                </article>
              ))}
            </section>
          ) : null}

          <section className="pnp-decision-kpis">
            <article className="pnp-decision-kpi analytics-kpi-card analytics-kpi-card--tone-info" data-note="SKU koji zadovoljavaju filtere i prag skora.">
              <span>Kandidati <InfoTip text="Ukupan broj SKU koji zadovoljavaju filtere i imaju aktivan signal pre nivelacije (pre-nivelacioni skor ≥ min. skora). Ovo su artikli koji imaju zalihu i prodajni signal dovoljan za intervenciju." /></span>
              <strong>{data.summary.candidatesCount}</strong>
            </article>
            <article className="pnp-decision-kpi analytics-kpi-card analytics-kpi-card--tone-success" data-note="Kandidati sa najjačim signalom za brzu intervenciju.">
              <span>Visok prioritet <InfoTip text="SKU u prioritetnoj bandi 'high' – imaju najjači kompozitni signal (visok skor zalihe + stagnacija prodaje). Ovo su artikli gde je intervencija pre nivelacije najhitnija." /></span>
              <strong>{candidateCounts.highPriority}</strong>
            </article>
            <article className="pnp-decision-kpi analytics-kpi-card analytics-kpi-card--tone-warning" data-note="Ukupna zaliha kod SKU koji nose operativni rizik.">
              <span>Zaliha pod rizikom <InfoTip text="Ukupna zaliha u komadima svih prikazanih kandidatskih SKU (u skladu sa filterima). Iskazano u komadima, ne u RSD vrednosti. Veća zaliha bez prodaje = veći operativni rizik." /></span>
              <strong>{data.summary.totalStockAtRisk}</strong>
              <em>kom ukupno</em>
            </article>
            <article className="pnp-decision-kpi analytics-kpi-card analytics-kpi-card--tone-value" data-note="Procena prihoda ako se kandidati istaknu umesto da se sniže.">
              <span>Procena povećanja prihoda <InfoTip text="Procenjeni prihod: scenario isticanja minus scenario sniženja za sve 'Pojačaj' kandidate. PROCENA – bazirana na scenariju sa istorijskim podacima prodaje, nije garantovani prihod. Tretirati kao relativni signal, ne kao apsolutnu predikciju." /></span>
              <strong>{fmtRsd(data.summary.expectedHighlightRevenueUplift)}</strong>
            </article>
            <article className="pnp-decision-kpi analytics-kpi-card analytics-kpi-card--tone-warning" data-note="Procena gubitka koji može da se izbegne pre nivelacije.">
              <span>Procena izbegljivog gubitka od sniženja <InfoTip text="Procenjeni gubitak prihoda koji se može izbeći pravovremenom intervencijom pre nivelacije. PROCENA bazirana na scenario modelu (isticanje vs. sniženje u 30-dnevnom prozoru). Apsolutni iznos je okvirna procena – relativni odnos između SKU-ova je relevantniji." /></span>
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
                  <h2>Prioritetna lista SKU kandidata</h2>
                  <p>
                    {recommendationStatusLabel("increase_focus")}: {candidateCounts.increaseFocus} | {recommendationStatusLabel("maintain")}: {candidateCounts.maintain} | {recommendationStatusLabel("review")}: {candidateCounts.review} | {recommendationStatusLabel("do_not_trust")}: {candidateCounts.doNotTrust} | {recommendationStatusLabel("insufficient_data")}: {candidateCounts.insufficientData} | Visok prioritet: {candidateCounts.highPriority}
                  </p>
                </div>
              </div>

              <div className="pnp-focus-tabs" role="tablist">
                {(["all", "increaseFocus", "maintain", "review", "doNotTrust", "insufficientData", "highPriority"] as FocusFilter[]).map((f) => {
                  const count =
                    f === "all" ? sortedRows.length
                    : f === "increaseFocus" ? candidateCounts.increaseFocus
                    : f === "maintain" ? candidateCounts.maintain
                    : f === "review" ? candidateCounts.review
                    : f === "doNotTrust" ? candidateCounts.doNotTrust
                    : f === "insufficientData" ? candidateCounts.insufficientData
                    : candidateCounts.highPriority;
                  const tabClass = f === "increaseFocus" ? "tab-boost" : f === "maintain" ? "tab-keep" : f === "review" ? "tab-keep" : f === "doNotTrust" ? "tab-reduce" : f === "insufficientData" ? "tab-reduce" : f === "highPriority" ? "tab-high" : "";
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

              <AnalyticsDataTable
                testId="pre-nivelacija-prioriteti-data-table"
                rowCount={filteredRows.length}
                truncationLabel={focusFilter !== "all" ? `Fokus: ${FOCUS_LABELS[focusFilter]}` : undefined}
                toolbar={(
                  <div className="pnp-table-toolbar">
                    <div className="pnp-decision-table-controls">
                      <button type="button" onClick={() => canGoPrev && setPage((p) => p - 1)} disabled={!canGoPrev || loading}>Prethodna</button>
                      <span>Strana {page}</span>
                      <button type="button" onClick={() => canGoNext && setPage((p) => p + 1)} disabled={!canGoNext || loading}>Sledeća</button>
                    </div>
                    <AnalyticsTableToolbar
                      tableKey="pre-nivelacija-prioriteti"
                      tableTitle="Podrška za odluku pre nivelacije"
                      columns={decisionColumns}
                      rows={sortedRows}
                      filters={toolbarFilters}
                      metadata={toolbarMetadata}
                      defaultOrientation="landscape"
                    />
                  </div>
                )}
              >
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
                        <InfoTip text="Razlika procenjenog prihoda u 30-dnevnom prozoru: scenario isticanja minus scenario sniženja. Pozitivna vrednost = scenario više naginje isticanju pre nivelacije. Negativno = scenario više naginje sniženju. Ovo je signal, ne garantovani ishod." />
                      </th>
                      <th className="align-center">
                        {RECOMMENDATION_RELIABILITY_LABEL}
                        <InfoTip text={analyticsMetricDescriptions.reliabilityPct} />
                      </th>
                      <th>
                        <button type="button" onClick={() => handleSort("status")}>Preporuka{sortMarker("status", sortField, sortDir)}</button>
                        <InfoTip text="Backend je izvor istine za preporuku pre nivelacije. Status i razlog dolaze iz server-side scoring sloja; frontend više ne računa lokalnu preporuku." />
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
                        const reliability = reliabilitySignalDisplay(row);
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
                              <span
                                className={reliability.className}
                                title={reliability.title}
                                aria-label={reliability.title ?? reliability.label}
                              >
                                {reliability.label}
                              </span>
                            </td>
                            <td>
                              <div className="pnp-status-cell">
                                <span
                                  className={statusClass(row.status)}
                                  title={buildStatusTooltip(row)}
                                  aria-label={buildStatusTooltip(row)}
                                >
                                  {statusDisplayLabel(row.status)}
                                </span>
                                <small className="pnp-status-next">{getRecommendedNextStep(row.status)}</small>
                              </div>
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
              </AnalyticsDataTable>
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
                  <span>Dobavljač</span>
                  <strong title={selectedRow.supplierName}>{selectedRow.supplierName}</strong>
                </article>
                <article>
                  <span>Prioritetna kategorija</span>
                  <strong>{selectedRow.priorityBand}</strong>
                </article>
                <article>
                  <span>Scenario isticanje (30d procena prihoda)</span>
                  <strong>{fmtRsd(selectedRow.scenarioHighlightNow.expectedRevenue30d)}</strong>
                </article>
                <article>
                  <span>Scenario sniženje (30d procena prihoda)</span>
                  <strong>{fmtRsd(selectedRow.scenarioMarkdownNow.expectedRevenue30d)}</strong>
                </article>
                <article>
                  <span>Procenjena delta prihoda</span>
                  <strong className={selectedRow.revenueDelta >= 0 ? "trend-up" : "trend-down"}>{fmtRsd(selectedRow.revenueDelta)}</strong>
                </article>
                <article>
                  <span>Procenjena delta marže</span>
                  <strong className={canShowMarkdownMarginSignal(selectedRow) ? (selectedRow.marginDelta >= 0 ? "trend-up" : "trend-down") : ""}>
                    {canShowMarkdownMarginSignal(selectedRow) ? fmtRsd(selectedRow.marginDelta) : "Nije dostupno bez troška"}
                  </strong>
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
                <article>
                  <span>{RECOMMENDATION_RELIABILITY_LABEL} <InfoTip text={analyticsMetricDescriptions.reliabilityPct} /></span>
                  <strong>{selectedRow.reliabilityAvailable ? fmtPct(selectedRow.reliabilityPct, 1) : RECOMMENDATION_SIGNAL_UNAVAILABLE}</strong>
                </article>
                <article>
                  <span>Status kvaliteta preporuke</span>
                  <strong style={recommendationQualityStyle(selectedRow.dataQualityStatus)}>{recommendationQualityLabel(selectedRow.dataQualityStatus)}</strong>
                </article>
                <article>
                  <span>{RECOMMENDATION_CONFIDENCE_LABEL} <InfoTip text={analyticsMetricDescriptions.recommendationConfidencePct} /></span>
                  <strong>{selectedRow.confidenceAvailable ? fmtPct(selectedRow.confidencePct, 1) : RECOMMENDATION_SIGNAL_UNAVAILABLE}</strong>
                </article>
              </div>

              <div className="pnp-decision-callouts">
                <article className="pnp-decision-callout pnp-decision-callout--action">
                  <span>Sledeći korak</span>
                  <strong>{getRecommendedNextStep(selectedRow.status)}</strong>
                  <p>{selectedRow.statusReason}</p>
                </article>
                <article
                  className={`pnp-decision-callout ${
                    hasLimitedMarkdownSignal(selectedRow)
                      ? "pnp-decision-callout--warning"
                      : "pnp-decision-callout--info"
                  }`}
                >
                  <span>Ograničenja signala</span>
                  <strong>
                    {hasLimitedMarkdownSignal(selectedRow)
                      ? "Potrebna je dodatna provera"
                      : "Signal je upotrebljiv za odluku"}
                  </strong>
                  <p>
                    {getMarkdownSignalLimitMessage(selectedRow)}
                  </p>
                </article>
              </div>

              <p className="pnp-decision-reason">
                <strong>Razlog preporuke:</strong> {selectedRow.statusReason}
              </p>
              {selectedRow.reasonCodes.length > 0 ? (
                <div className="pnp-reason-code-list" aria-label="Kodovi signala">
                  {selectedRow.reasonCodes.map((reasonCode) => (
                    <span key={reasonCode} className="pnp-reason-code-chip">{reasonCode}</span>
                  ))}
                </div>
              ) : null}
              {recommendationReasonHints(selectedRow.reasonCodes).map((hint) => (
                <p key={hint} className="pnp-decision-reason pnp-decision-reason--note">
                  <strong>Napomena:</strong> {hint}
                </p>
              ))}
              {(!selectedRow.reliabilityAvailable || !selectedRow.confidenceAvailable || selectedRow.dataQualityStatus !== "good") ? (
                <p className="pnp-decision-reason pnp-decision-reason--warning">
                  <strong>Kvalitet podataka:</strong> Otvori <Link to="/analytics/data-quality">Data Quality</Link> da proveriš i ispraviš signal.
                </p>
              ) : null}

              {selectedRow.scoreBreakdown ? (
                <div className="pnp-score-breakdown">
                  <h4>Komponente score-a</h4>
                  <div className="pnp-score-grid">
                    {[
                      { label: "Pritisak zalihe", value: selectedRow.scoreBreakdown.stockPressure },
                      { label: "Rizik brzine prodaje", value: selectedRow.scoreBreakdown.velocityRisk },
                      { label: "Rizik starosti prodaje", value: selectedRow.scoreBreakdown.recencyRisk },
                      { label: "Markdown signal", value: selectedRow.scoreBreakdown.markdownOpportunity },
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
                <InfoTip text="SKU su raspoređeni po backend recommendation statusu (Pojačaj, Zadrži, Pregledaj, Ne veruj, Nedovoljno podataka) i pomoćnim prioritetnim signalima." />
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
                  <h3>Verovatni markdown signal ({data.queues.likelyMarkdownSoon.length})</h3>
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



import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link, useLocation, useNavigate, useSearchParams } from "react-router-dom";
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
import { getPreNivelacijaPrioriteti, PreNivelacijaApiError } from "../services/preNivelacijaApi";
import type { AnalyticsNamedValue } from "../types/analyticsTable";
import type { PreNivelacijaPriorityResponse } from "../types/preNivelacija";
import { decisionColumns, type DecisionCandidate, type DecisionStatus, type FiniteNumber, type NormalizedScenario } from "./preNivelacijaDecision";
import { CHART_TOOLTIP_STYLE } from "../utils/chartTooltipStyle";
import { fmtNumber, fmtPct, fmtRsd } from "../utils/analyticsFormatters";
import { analyticsMetricDescriptions } from "../utils/analyticsMetricDescriptions";
import { getSafeAnalyticsErrorMessage } from "../utils/analyticsErrorMessages";
import { getDataScope, normalizeDataScope, type DataScope } from "../utils/dataScope";
import { createAnalyticsDatasetProjections } from "../utils/analyticsDatasetProjections";
import { useReliableAnalyticsQuery } from "../hooks/useReliableAnalyticsQuery";
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
  normalizeRecommendationQualityStatus,
  recommendationQualityLabel,
  recommendationQualityStyle,
  recommendationReasonLabel,
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
type ActiveFilters = {
  supplierId: number | null;
  seasonId: number | null;
  footwearTypeId: number | null;
  minScore: number;
  noSaleDaysMin: number;
};

function getPreNivelacijaErrorDetails(reason: unknown): {
  message: string;
  errorCode: string | null;
  correlationId: string | null;
} {
  const preNivelacijaError = reason instanceof PreNivelacijaApiError ? reason : null;
  const maybeError = reason as { message?: unknown; errorCode?: unknown; correlationId?: unknown };
  const errorCode = preNivelacijaError?.errorCode
    ?? (typeof maybeError.errorCode === "string" ? maybeError.errorCode : null);
  const correlationId = preNivelacijaError?.correlationId
    ?? (typeof maybeError.correlationId === "string" ? maybeError.correlationId : null);
  const fallbackMessage = "Pre-nivelacija prioriteti trenutno nisu dostupni. Proverite status osvežavanja i pokušajte ponovo.";
  const rawMessage = preNivelacijaError?.message
    ?? (typeof maybeError.message === "string" ? maybeError.message : null);

  return {
    message: getSafeAnalyticsErrorMessage(
      preNivelacijaError ? rawMessage : null,
      errorCode,
      fallbackMessage,
    ),
    errorCode,
    correlationId,
  };
}

const DEFAULT_MIN_SCORE = 40;
const DEFAULT_NO_SALE_DAYS_MIN = 14;

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

function parseOptionalPositiveInteger(value: string | null): number | null {
  if (!value || !/^\d+$/.test(value)) return null;
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : null;
}

function parseBoundedInteger(value: string | null, fallback: number, min: number, max: number): number {
  if (!value || !/^\d+$/.test(value)) return fallback;
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed >= min && parsed <= max ? parsed : fallback;
}

function parseNonNegativeInteger(value: string | null, fallback: number): number {
  if (!value || !/^\d+$/.test(value)) return fallback;
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed >= 0 ? parsed : fallback;
}

function parseFocusFilter(value: string | null): FocusFilter {
  if (value && value in FOCUS_LABELS) return value as FocusFilter;
  return "all";
}

function sameActiveFilters(left: ActiveFilters, right: ActiveFilters): boolean {
  return left.supplierId === right.supplierId
    && left.seasonId === right.seasonId
    && left.footwearTypeId === right.footwearTypeId
    && left.minScore === right.minScore
    && left.noSaleDaysMin === right.noSaleDaysMin;
}

function buildPreNivelacijaSearchParams(filters: ActiveFilters, focus: FocusFilter, page: number, dataScope: DataScope): URLSearchParams {
  const params = new URLSearchParams();
  if (filters.supplierId != null) params.set("supplierId", String(filters.supplierId));
  if (filters.seasonId != null) params.set("seasonId", String(filters.seasonId));
  if (filters.footwearTypeId != null) params.set("footwearTypeId", String(filters.footwearTypeId));
  params.set("minScore", String(filters.minScore));
  params.set("noSaleDaysMin", String(filters.noSaleDaysMin));
  if (focus !== "all") params.set("focus", focus);
  if (page > 1) params.set("page", String(page));
  params.set("dataScope", dataScope);
  return params;
}

const STATUS_PRIORITY: Record<DecisionStatus, number> = {
  ...RECOMMENDATION_STATUS_PRIORITY,
};

interface CustomSupplierTooltipProps {
  active?: boolean;
  payload?: Array<{ payload: { name: string; sharePct: number; weekOverWeekRiskDeltaPct: FiniteNumber } }>;
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

function normalizeFiniteNumber(value: unknown): FiniteNumber {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function normalizeBoundedNumber(value: unknown, min: number, max: number): FiniteNumber {
  const normalized = normalizeFiniteNumber(value);
  return normalized != null && normalized >= min && normalized <= max ? normalized : null;
}

function normalizeNonNegativeNumber(value: unknown): FiniteNumber {
  return normalizeBoundedNumber(value, 0, Number.MAX_VALUE);
}

function normalizePositiveInteger(value: unknown): number | null {
  const normalized = normalizeNonNegativeNumber(value);
  return normalized != null && Number.isInteger(normalized) && normalized > 0 ? normalized : null;
}

function normalizePercentage(value: unknown): FiniteNumber {
  const normalized = normalizeFiniteNumber(value);
  if (normalized == null || normalized < 0) return null;
  const percentage = normalized <= 1 ? normalized * 100 : normalized;
  return percentage <= 100 ? percentage : null;
}

function normalizeScenario(value: unknown): NormalizedScenario {
  const scenario = value as Partial<Record<keyof NormalizedScenario, unknown>> | null;
  return {
    expectedUnits30d: normalizeNonNegativeNumber(scenario?.expectedUnits30d),
    expectedRevenue30d: normalizeNonNegativeNumber(scenario?.expectedRevenue30d),
    expectedMargin30d: normalizeFiniteNumber(scenario?.expectedMargin30d),
    effectivePrice: normalizeNonNegativeNumber(scenario?.effectivePrice),
  };
}

function normalizeScoreBreakdown(value: unknown): DecisionCandidate["scoreBreakdown"] {
  const breakdown = value as Partial<Record<keyof DecisionCandidate["scoreBreakdown"], unknown>> | null;
  return {
    stockPressure: normalizeBoundedNumber(breakdown?.stockPressure, 0, 100),
    velocityRisk: normalizeBoundedNumber(breakdown?.velocityRisk, 0, 100),
    recencyRisk: normalizeBoundedNumber(breakdown?.recencyRisk, 0, 100),
    markdownOpportunity: normalizeBoundedNumber(breakdown?.markdownOpportunity, 0, 100),
    marginPotential: normalizeBoundedNumber(breakdown?.marginPotential, 0, 100),
    seasonRecencyBoost: normalizeFiniteNumber(breakdown?.seasonRecencyBoost),
  };
}

function formatFiniteNumber(value: unknown, digits = 0): string {
  const normalized = normalizeFiniteNumber(value);
  return normalized == null ? RECOMMENDATION_SIGNAL_UNAVAILABLE : fmtNumber(normalized, digits, RECOMMENDATION_SIGNAL_UNAVAILABLE);
}

function formatGatedRsd(allowed: boolean, value: FiniteNumber): string {
  return allowed ? fmtRsd(value, 0, RECOMMENDATION_SIGNAL_UNAVAILABLE) : RECOMMENDATION_SIGNAL_UNAVAILABLE;
}

function deltaTrendClass(allowed: boolean, value: FiniteNumber): string {
  if (!allowed || value == null) return "";
  return value >= 0 ? "trend-up" : "trend-down";
}

function gatedNumericValue(allowed: boolean, value: FiniteNumber): FiniteNumber {
  return allowed ? value : null;
}

function formatNonNegativeNumber(value: unknown, digits = 0): string {
  const normalized = normalizeNonNegativeNumber(value);
  return normalized == null ? RECOMMENDATION_SIGNAL_UNAVAILABLE : fmtNumber(normalized, digits, RECOMMENDATION_SIGNAL_UNAVAILABLE);
}

function normalizeDecisionScore(value: unknown): number | null {
  return normalizeBoundedNumber(value, 0, 100);
}

function compareNullableNumbers(left: FiniteNumber, right: FiniteNumber, dir: SortDir): number {
  if (left == null && right == null) return 0;
  if (left == null) return 1;
  if (right == null) return -1;
  const compare = left - right;
  return dir === "asc" ? compare : -compare;
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
  if (!row.reliabilityAvailable || row.reliabilityPct == null) {
    return {
      label: "Nije dostupno",
      className: "pnp-signal-pill signal-na",
      title: RECOMMENDATION_SIGNAL_UNAVAILABLE,
    };
  }

  const label = fmtPct(row.reliabilityPct, 0);
  const className = row.reliabilityPct >= 70
    ? "pnp-signal-pill signal-strong"
    : row.reliabilityPct >= 40
      ? "pnp-signal-pill signal-watch"
      : "pnp-signal-pill signal-weak";
  return {
    label,
    className,
    title: `${RECOMMENDATION_RELIABILITY_LABEL}: ${label}`,
  };
}

function isHighPriorityCandidate(row: DecisionCandidate): boolean {
  // Priority band is the backend-owned risk population. Recommendation status
  // separately tells us whether the signal is actionable; insufficient data is
  // therefore still high priority, but must not be presented as an allowed action.
  return (row.priorityBand ?? "").toLowerCase() === "high";
}

function priorityBandLabel(value: string): string {
  const normalized = value.trim().toLowerCase();
  if (normalized === "high") return "Visok";
  if (normalized === "medium") return "Srednji";
  if (normalized === "low") return "Nizak";
  return "Nije klasifikovano";
}

type StatusTooltipData = {
  status: DecisionStatus;
  statusReason: string;
  decisionScore: number | null;
  decisionScoreAvailable: boolean;
  revenueDelta: FiniteNumber;
  reliabilityPct: number | null;
  confidencePct: number | null;
  reliabilityAvailable: boolean;
  confidenceAvailable: boolean;
  dataQualityStatus: RecommendationQualityStatus;
  reasonCodes: string[];
  recommendationAllowed: boolean;
};

function buildStatusTooltip(data: StatusTooltipData): string {
  const reliabilityText = data.reliabilityAvailable && data.reliabilityPct != null ? fmtPct(data.reliabilityPct, 0) : RECOMMENDATION_SIGNAL_UNAVAILABLE;
  const confidenceText = data.confidenceAvailable && data.confidencePct != null ? fmtPct(data.confidencePct, 0) : RECOMMENDATION_SIGNAL_UNAVAILABLE;
  const scoreText = data.decisionScoreAvailable && data.decisionScore != null ? formatFiniteNumber(data.decisionScore, 1) : RECOMMENDATION_SIGNAL_UNAVAILABLE;
  const deltaText = formatGatedRsd(data.recommendationAllowed, data.revenueDelta);
  const qualityText = recommendationQualityLabel(data.dataQualityStatus);
  const hintText = recommendationReasonHints(data.reasonCodes).join(" | ");
  const gateText = data.recommendationAllowed
    ? ""
    : " | Preporuka je blokirana; status je informativan, a ocena i sledeći korak nisu potvrđeni.";
  return `${statusDisplayLabel(data.status)}: ${data.statusReason} | ${recommendationStatusTooltipBrief(data.status)} | Ocena ${scoreText} | Delta ${deltaText} | ${RECOMMENDATION_RELIABILITY_LABEL} ${reliabilityText} | ${RECOMMENDATION_CONFIDENCE_LABEL} ${confidenceText} | Kvalitet ${qualityText}${hintText ? ` | Napomene: ${hintText}` : ""}${gateText}`;
}

function getRecommendedNextStep(status: DecisionStatus): string {
  if (status === "increase_focus") return "Pojačaj izlaganje i proveri dopunu pre nivelacije.";
  if (status === "maintain") return "Zadrži pod nadzorom i prati naredni ciklus prodaje.";
  if (status === "review") return "Pregledaj signal pre odluke o jačem isticanju ili markdown-u.";
  if (status === "do_not_trust") return "Ne donosi odluku dok ne proveriš podatke i poslednju prodaju.";
  return "Sačekaj jači signal ili proširi kontekst pre odluke.";
}

function getStatusNextStep(row: Pick<DecisionCandidate, "recommendationAllowed" | "status">): string {
  return row.recommendationAllowed
    ? getRecommendedNextStep(row.status)
    : "Preporuka je blokirana; proveri podatke pre odluke.";
}

function hasReasonCode(reasonCodes: string[], targets: string[]): boolean {
  const normalizedTargets = new Set(targets.map((value) => value.trim().toLowerCase()));
  return reasonCodes.some((code) => normalizedTargets.has((code ?? "").trim().toLowerCase()));
}

function hasMissingCostSignal(reasonCodes: string[]): boolean {
  return hasReasonCode(reasonCodes, ["missing_cost", "missing_cost_coverage"]);
}

function hasSparseSalesSignal(reasonCodes: string[]): boolean {
  return hasReasonCode(reasonCodes, ["sparse_sales", "tiny_sample", "insufficient_history", "no_sales_in_window", "zero_net_sales"]);
}

function hasSignedSalesSignal(reasonCodes: string[]): boolean {
  return hasReasonCode(reasonCodes, ["signed_adjustment", "signed_sales_adjustment", "signed_sales_non_positive", "negative_net_sales"]);
}

function canShowMarkdownMarginSignal(row: DecisionCandidate): boolean {
  return !hasMissingCostSignal(row.reasonCodes)
    && row.status !== "insufficient_data"
    && row.dataQualityStatus !== "critical"
    && row.marginDelta != null;
}

function hasLimitedMarkdownSignal(row: DecisionCandidate): boolean {
  return !row.recommendationAllowed
    || !row.reliabilityAvailable
    || !row.confidenceAvailable
    || row.preNivelacijaScore == null
    || row.stockUnits == null
    || row.daysSinceLastSale == null
    || row.dataQualityStatus !== "good"
    || row.status === "insufficient_data"
    || hasMissingCostSignal(row.reasonCodes)
    || hasSparseSalesSignal(row.reasonCodes)
    || hasSignedSalesSignal(row.reasonCodes);
}

function getMarkdownSignalLimitMessage(row: DecisionCandidate): string {
  if (hasMissingCostSignal(row.reasonCodes)) {
    return "Maržni scenario nije dostupan bez pouzdanog troška, pa signal treba čitati samo kao scenario prihoda.";
  }
  if (hasSparseSalesSignal(row.reasonCodes)) {
    return "Signal ima mali ili redak prodajni uzorak, pa scenario treba potvrditi pre poslovne odluke.";
  }
  if (hasSignedSalesSignal(row.reasonCodes)) {
    return "Prodajni signal sadrži povrate ili korekcije; preporuka je blokirana dok se ne potvrdi potpisani neto saldo.";
  }
  if (!row.reliabilityAvailable || !row.confidenceAvailable || row.dataQualityStatus !== "good" || row.status === "insufficient_data") {
    return "Proveri pouzdanost, sigurnost preporuke i kvalitet podataka pre jače intervencije.";
  }

  return "Pouzdanost i kvalitet podataka ne pokazuju blokirajuće rizike za ovu preporuku.";
}

export default function PreNivelacijaPriorityPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams, setSearchParams] = useSearchParams();
  const queryDataScope = normalizeDataScope(searchParams.get("dataScope") ?? getDataScope());
  const queryFilters = useMemo<ActiveFilters>(() => ({
    supplierId: parseOptionalPositiveInteger(searchParams.get("supplierId")),
    seasonId: parseOptionalPositiveInteger(searchParams.get("seasonId")),
    footwearTypeId: parseOptionalPositiveInteger(searchParams.get("footwearTypeId")),
    minScore: parseBoundedInteger(searchParams.get("minScore"), DEFAULT_MIN_SCORE, 0, 100),
    noSaleDaysMin: parseNonNegativeInteger(searchParams.get("noSaleDaysMin"), DEFAULT_NO_SALE_DAYS_MIN),
  }), [searchParams]);
  const queryFocus = parseFocusFilter(searchParams.get("focus"));
  const queryPage = parseBoundedInteger(searchParams.get("page"), 1, 1, Number.MAX_SAFE_INTEGER);

  const [supplierId, setSupplierId] = useState<number | null>(queryFilters.supplierId);
  const [seasonId, setSeasonId] = useState<number | null>(queryFilters.seasonId);
  const [footwearTypeId, setFootwearTypeId] = useState<number | null>(queryFilters.footwearTypeId);
  const [minScore, setMinScore] = useState<number>(queryFilters.minScore);
  const [noSaleDaysMin, setNoSaleDaysMin] = useState<number>(queryFilters.noSaleDaysMin);
  const [activeFilters, setActiveFilters] = useState<ActiveFilters>(queryFilters);

  const [page, setPage] = useState(queryPage);
  const [sortField, setSortField] = useState<SortField>("status");
  const [sortDir, setSortDir] = useState<SortDir>("desc");
  const [expandedArtikalId, setExpandedArtikalId] = useState<number | null>(null);
  const [focusFilter, setFocusFilter] = useState<FocusFilter>(queryFocus);
  const [dataScope, setDataScopeValue] = useState<DataScope>(() => queryDataScope);
  const dataScopeRef = useRef<DataScope>(queryDataScope);

  useEffect(() => {
    setSupplierId((current) => current === queryFilters.supplierId ? current : queryFilters.supplierId);
    setSeasonId((current) => current === queryFilters.seasonId ? current : queryFilters.seasonId);
    setFootwearTypeId((current) => current === queryFilters.footwearTypeId ? current : queryFilters.footwearTypeId);
    setMinScore((current) => current === queryFilters.minScore ? current : queryFilters.minScore);
    setNoSaleDaysMin((current) => current === queryFilters.noSaleDaysMin ? current : queryFilters.noSaleDaysMin);
    setActiveFilters((current) => sameActiveFilters(current, queryFilters) ? current : queryFilters);
    setPage((current) => current === queryPage ? current : queryPage);
    setFocusFilter((current) => current === queryFocus ? current : queryFocus);
  }, [queryFilters, queryFocus, queryPage]);

  useEffect(() => {
    const canonicalParams = buildPreNivelacijaSearchParams(queryFilters, queryFocus, queryPage, queryDataScope);
    if (canonicalParams.toString() === searchParams.toString()) return;
    setSearchParams(canonicalParams, { replace: true });
  }, [queryDataScope, queryFilters, queryFocus, queryPage, searchParams, setSearchParams]);

  useEffect(() => {
    if (dataScopeRef.current === queryDataScope) return;
    dataScopeRef.current = queryDataScope;
    setDataScopeValue(queryDataScope);
  }, [queryDataScope]);

  useEffect(() => {
    const handleScopeChange = () => {
      const nextScope = normalizeDataScope(getDataScope());
      dataScopeRef.current = nextScope;
      setDataScopeValue(nextScope);
      setSearchParams((current) => {
        if (current.get("dataScope") === nextScope) return current;
        const next = new URLSearchParams(current);
        next.set("dataScope", nextScope);
        return next;
      }, { replace: true });
    };

    window.addEventListener("trendplus:data-scope-changed", handleScopeChange);
    return () => window.removeEventListener("trendplus:data-scope-changed", handleScopeChange);
  }, [setSearchParams]);

  const preNivelacijaQuery = useCallback((signal: AbortSignal) => getPreNivelacijaPrioriteti({
    supplierId: activeFilters.supplierId ?? undefined,
    seasonId: activeFilters.seasonId ?? undefined,
    footwearTypeId: activeFilters.footwearTypeId ?? undefined,
    minScore: activeFilters.minScore,
    noSaleDaysMin: activeFilters.noSaleDaysMin,
    page,
    pageSize: 60,
    dataScope,
    signal,
  }), [activeFilters, dataScope, page]);
  const {
    data,
    initialLoading,
    refetching,
    error: queryError,
    errorReason,
    staleWarning,
    refetch,
  } = useReliableAnalyticsQuery<PreNivelacijaPriorityResponse>({
    query: preNivelacijaQuery,
    getErrorMessage: useCallback((reason: unknown) => getPreNivelacijaErrorDetails(reason).message, []),
  });
  const loading = initialLoading || refetching;
  const queryErrorDetails = errorReason ? getPreNivelacijaErrorDetails(errorReason) : null;
  const error = queryError
    ? {
        message: queryError,
        errorCode: queryErrorDetails?.errorCode ?? null,
        correlationId: queryErrorDetails?.correlationId ?? null,
      }
    : null;
  useEffect(() => {
    if (!data) return;
    setExpandedArtikalId((current) => current == null || data.candidates.some((candidate) => candidate.artikalId === current)
      ? current
      : null);
  }, [data]);

  const supplierOptions = useMemo(
    () => (data?.supplierLeaderboard ?? []).filter((item) => item.supplierId != null),
    [data?.supplierLeaderboard]
  );

  const seasonOptions = useMemo(() => {
    const facetSeasons = data?.filterFacets?.seasons ?? [];
    if (facetSeasons.length > 0) {
      return [...facetSeasons].sort((a, b) => a.label.localeCompare(b.label, "sr"));
    }

    const map = new Map<number, string>();
    (data?.candidates ?? []).forEach((item) => {
      if (item.seasonId != null && item.season && item.season !== "N/A") {
        map.set(item.seasonId, item.season);
      }
    });

    return [...map.entries()]
      .map(([id, label]) => ({ id, label }))
      .sort((a, b) => a.label.localeCompare(b.label, "sr"));
  }, [data?.candidates, data?.filterFacets?.seasons]);

  const footwearTypeOptions = useMemo(() => {
    const facetFootwearTypes = data?.filterFacets?.footwearTypes ?? [];
    if (facetFootwearTypes.length > 0) {
      return [...facetFootwearTypes].sort((a, b) => a.label.localeCompare(b.label, "sr"));
    }

    const map = new Map<number, string>();
    (data?.candidates ?? []).forEach((item) => {
      if (item.footwearTypeId != null && item.footwearType && item.footwearType !== "N/A") {
        map.set(item.footwearTypeId, item.footwearType);
      }
    });

    return [...map.entries()]
      .map(([id, label]) => ({ id, label }))
      .sort((a, b) => a.label.localeCompare(b.label, "sr"));
  }, [data?.candidates, data?.filterFacets?.footwearTypes]);

  const decisionRows = useMemo<DecisionCandidate[]>(() => {
    const rows = data?.candidates ?? [];
    if (rows.length === 0) return [];

    return rows.map((item) => {
      const recommendation = item.recommendation;
      const revenueDelta = normalizeFiniteNumber(item.revenueDeltaHighlightVsMarkdown);
      const marginDelta = normalizeFiniteNumber(item.marginDeltaHighlightVsMarkdown);
      const confidencePctValue = normalizePercentage(recommendation.confidencePct);
      const reliabilityPctValue = normalizePercentage(recommendation.reliabilityPct ?? item.reliabilityPct);
      const recommendationAllowed = recommendation.recommendationAllowed === true;
      const decisionScore = recommendationAllowed ? normalizeDecisionScore(item.decisionScore) : null;

      return {
        ...item,
        stockUnits: normalizeNonNegativeNumber(item.stockUnits),
        units180: normalizeFiniteNumber(item.units180),
        velocity180: normalizeFiniteNumber(item.velocity180),
        daysSinceLastSale: normalizeNonNegativeNumber(item.daysSinceLastSale),
        markdownEvents: normalizeNonNegativeNumber(item.markdownEvents),
        avgMarkdownPct: normalizePercentage(item.avgMarkdownPct),
        grossMarginPctEst: normalizeFiniteNumber(item.grossMarginPctEst),
        seasonRecencyBoost: normalizeFiniteNumber(item.seasonRecencyBoost),
        preNivelacijaScore: normalizeBoundedNumber(item.preNivelacijaScore, 0, 100),
        scoreBreakdown: normalizeScoreBreakdown(item.scoreBreakdown),
        scenarioHighlightNow: normalizeScenario(item.scenarioHighlightNow),
        scenarioMarkdownNow: normalizeScenario(item.scenarioMarkdownNow),
        marginDeltaHighlightVsMarkdown: marginDelta,
        revenueDeltaHighlightVsMarkdown: revenueDelta,
        revenueDelta,
        marginDelta,
        confidencePct: recommendationAllowed ? confidencePctValue : null,
        confidenceAvailable: recommendationAllowed && confidencePctValue != null,
        reliabilityAvailable: recommendationAllowed && reliabilityPctValue != null,
        reliabilityPct: recommendationAllowed ? reliabilityPctValue : null,
        recommendationAllowed,
        decisionScore,
        decisionScoreAvailable: decisionScore != null,
        status: recommendation.status,
        statusReason: recommendation.summary,
        dataQualityStatus: normalizeRecommendationQualityStatus(recommendation.dataQualityStatus),
        reasonCodes: recommendation.reasonCodes ?? [],
      };
    });
  }, [data?.candidates]);

  const tableRows = useMemo(() => {
    const rows = [...decisionRows];
    return rows.sort((a, b) => {
      let compare = 0;

      if (sortField === "sku") compare = a.sku.localeCompare(b.sku, "sr");
      else if (sortField === "supplierName") compare = a.supplierName.localeCompare(b.supplierName, "sr");
      else if (sortField === "preNivelacijaScore") compare = compareNullableNumbers(a.preNivelacijaScore, b.preNivelacijaScore, sortDir);
      else if (sortField === "stockUnits") compare = compareNullableNumbers(a.stockUnits, b.stockUnits, sortDir);
      else if (sortField === "daysSinceLastSale") compare = compareNullableNumbers(a.daysSinceLastSale, b.daysSinceLastSale, sortDir);
      else if (sortField === "revenueDelta") {
        compare = compareNullableNumbers(
          gatedNumericValue(a.recommendationAllowed, a.revenueDelta),
          gatedNumericValue(b.recommendationAllowed, b.revenueDelta),
          sortDir,
        );
      }
      else if (sortField === "status") compare = compareNullableNumbers(STATUS_PRIORITY[a.status] ?? null, STATUS_PRIORITY[b.status] ?? null, sortDir);

      if (compare === 0) compare = compareNullableNumbers(a.decisionScore, b.decisionScore, sortDir);
      if (compare === 0) compare = compareNullableNumbers(a.confidencePct, b.confidencePct, sortDir);
      if (compare === 0) compare = a.sku.localeCompare(b.sku, "sr");
      return compare;
    });
  }, [decisionRows, sortDir, sortField]);

  const candidateCounts = useMemo(() => {
    const increaseFocus = tableRows.filter((row) => row.status === "increase_focus").length;
    const maintain = tableRows.filter((row) => row.status === "maintain").length;
    const review = tableRows.filter((row) => row.status === "review").length;
    const doNotTrust = tableRows.filter((row) => row.status === "do_not_trust").length;
    const insufficientData = tableRows.filter((row) => row.status === "insufficient_data").length;
    const highPriority = tableRows.filter(isHighPriorityCandidate).length;
    return { increaseFocus, maintain, review, doNotTrust, insufficientData, highPriority };
  }, [tableRows]);

  const globalStatusCounts = useMemo(() => {
    const summary = data?.summary;
    if (!summary) {
      return null;
    }

    return {
      increaseFocus: summary.increaseFocusCount,
      maintain: summary.maintainCount,
      review: summary.reviewCount,
      doNotTrust: summary.doNotTrustCount,
      insufficientData: summary.insufficientDataCount,
      highPriority: summary.highPriorityCount,
    };
  }, [data?.summary]);

  const filteredTableRows = useMemo(() => {
    if (focusFilter === "all") return tableRows;
    if (focusFilter === "increaseFocus") return tableRows.filter((row) => row.status === "increase_focus");
    if (focusFilter === "maintain") return tableRows.filter((row) => row.status === "maintain");
    if (focusFilter === "review") return tableRows.filter((row) => row.status === "review");
    if (focusFilter === "doNotTrust") return tableRows.filter((row) => row.status === "do_not_trust");
    if (focusFilter === "insufficientData") return tableRows.filter((row) => row.status === "insufficient_data");
    if (focusFilter === "highPriority") return tableRows.filter(isHighPriorityCandidate);
    return tableRows;
  }, [focusFilter, tableRows]);

  const preNivelacijaProjections = useMemo(
    () => createAnalyticsDatasetProjections({
      canonicalRows: decisionRows,
      filteredRows: filteredTableRows,
      tableRows,
      chronologicalChartRows: decisionRows,
      exportRows: filteredTableRows,
      detailRows: filteredTableRows,
      pageRows: decisionRows,
      globalTotals: data?.summary ?? null,
      globalFacets: data?.filterFacets ?? null,
    }),
    [data?.filterFacets, data?.summary, decisionRows, filteredTableRows, tableRows],
  );

  const isDirty =
    supplierId !== activeFilters.supplierId ||
    seasonId !== activeFilters.seasonId ||
    footwearTypeId !== activeFilters.footwearTypeId ||
    minScore !== activeFilters.minScore ||
    noSaleDaysMin !== activeFilters.noSaleDaysMin;

  const supplierActionShare = useMemo(() => {
    const items = data?.supplierLeaderboard ?? [];
    if (items.length === 0) return [] as Array<{ name: string; sharePct: number; weekOverWeekRiskDeltaPct: FiniteNumber }>;

    const top = items
      .map((item) => ({ item, actionScore: normalizeNonNegativeNumber(item.actionScore) }))
      .filter((entry): entry is { item: (typeof items)[number]; actionScore: number } => entry.actionScore != null)
      .sort((a, b) => b.actionScore - a.actionScore)
      .slice(0, 7);
    const total = top.reduce((sum, entry) => sum + entry.actionScore, 0);
    if (total <= 0) return [];

    return top.map(({ item, actionScore }) => ({
      name: item.supplierName,
      sharePct: (actionScore / total) * 100,
      weekOverWeekRiskDeltaPct: normalizeFiniteNumber(item.weekOverWeekRiskDeltaPct),
    }));
  }, [data?.supplierLeaderboard]);

  const selectedRow = useMemo(() => {
    if (expandedArtikalId == null) return null;
    return preNivelacijaProjections.detailRows.find((row) => row.artikalId === expandedArtikalId) ?? null;
  }, [expandedArtikalId, preNivelacijaProjections.detailRows]);

  const canGoPrev = page > 1;
  const pageSize = data ? normalizePositiveInteger(data.pageSize) : null;
  const totalCandidates = data ? normalizeNonNegativeNumber(data.totalCandidates) : null;
  const canGoNext = pageSize != null && totalCandidates != null ? page * pageSize < totalCandidates : false;
  const dataMeta = data?.meta ?? null;
  const dataMetaMessage = getAnalyticsMetaMessage(dataMeta);
  const showMetaWarning = !loading && !error && isAnalyticsMetaWarning(dataMeta);
  const showFilteredOutState = !loading && !error && Boolean(data) && decisionRows.length > 0 && preNivelacijaProjections.filteredRows.length === 0;
  const showEmptyState = !loading && !error && Boolean(data) && (decisionRows.length === 0 || showFilteredOutState);
  const showInsufficientEmptyState = shouldShowAnalyticsEmptyState(dataMeta, decisionRows.length) && isAnalyticsMetaInsufficient(dataMeta);
  const emptyStateVariant: "no_data" | "insufficient_data" | "filtered_out" =
    showInsufficientEmptyState
      ? "insufficient_data"
      : showFilteredOutState
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

    if (globalStatusCounts && globalStatusCounts.highPriority > 0) {
      notices.push({
        key: "high-priority",
        title: `${globalStatusCounts.highPriority} SKU je u visokoj prioritetnoj bandi`,
        detail: "Visoka prioritetna banda je globalni rizik cele filtrirane populacije. Proveri akcioni status i kvalitet podataka pre odluke.",
        tone: "info",
      });
    }

    const limitedSignalCount = globalStatusCounts
      ? globalStatusCounts.doNotTrust + globalStatusCounts.insufficientData
      : null;
    if (limitedSignalCount != null && limitedSignalCount > 0) {
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
    } else if (globalStatusCounts && globalStatusCounts.review > 0) {
      notices.push({
        key: "review",
        title: `${globalStatusCounts.review} SKU je za ručni pregled`,
        detail: "Pregledaj razlog preporuke i sledeći korak pre nego što artikal pojačaš ili spustiš iz fokusa.",
        tone: "warning",
      });
    }

    return notices.slice(0, 3);
  }, [dataMetaMessage, globalStatusCounts, showMetaWarning]);

  const toolbarFilters = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "supplierId", label: "Dobavljač", value: activeFilters.supplierId ?? "" },
      { key: "seasonId", label: "Sezona", value: activeFilters.seasonId ?? "" },
      { key: "footwearTypeId", label: "Tip obuće", value: activeFilters.footwearTypeId ?? "" },
      { key: "minScore", label: "Min. skor", value: activeFilters.minScore },
      { key: "noSaleDaysMin", label: "Min. dana bez prodaje", value: activeFilters.noSaleDaysMin },
      { key: "focus", label: "Fokus", value: focusFilter },
      { key: "dataScope", label: "Opseg podataka", value: dataScope },
      { key: "page", label: "Strana", value: page },
    ],
    [activeFilters.footwearTypeId, activeFilters.minScore, activeFilters.noSaleDaysMin, activeFilters.seasonId, activeFilters.supplierId, dataScope, focusFilter, page]
  );

  const toolbarMetadata = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "generatedAtUtc", label: "Generisano", value: data?.generatedAtUtc ?? "" },
      { key: "formulaVersion", label: "Formula", value: data?.formulaVersion ?? "" },
      { key: "populationBasis", label: "Osnova brojanja", value: "Globalno = cela filtrirana populacija; strana = trenutno učitani redovi" },
      { key: "totalCandidates", label: "Ukupno kandidata (globalno)", value: data ? normalizeNonNegativeNumber(data.totalCandidates) : null },
      { key: "globalHighPriority", label: "Visok prioritet (globalno)", value: data ? normalizeNonNegativeNumber(data.summary.highPriorityCount) : null },
      { key: "visiblePageCandidates", label: "Kandidati (vidljiva strana)", value: decisionRows.length },
      { key: "visiblePageHighPriority", label: "Visok prioritet (vidljiva strana)", value: candidateCounts.highPriority },
      { key: "salesWindowFromUtc", label: "Prodajni prozor od (UTC)", value: data?.evidenceWindow?.salesWindowFromUtc ?? null },
      { key: "salesWindowToUtc", label: "Prodajni prozor do (UTC)", value: data?.evidenceWindow?.salesWindowToUtc ?? null },
      { key: "salesQuantityPolicy", label: "Politika količine", value: data?.evidenceWindow?.salesQuantityPolicy === "signed_net_quantity_preserved" ? "Potpisana neto količina; povrati i korekcije ostaju vidljivi" : data?.evidenceWindow?.salesQuantityPolicy ?? null },
      { key: "nonPositiveNetPolicy", label: "Nevažeći neto signal", value: data?.evidenceWindow?.nonPositiveNetPolicy === "recommendation_unavailable" ? "Preporuka nedostupna" : data?.evidenceWindow?.nonPositiveNetPolicy ?? null },
      { key: "candidatesWithReturns", label: "Kandidati sa povratima/korekcijama", value: data?.evidenceWindow?.candidatesWithReturns ?? null },
      { key: "candidatesWithoutSalesInWindow", label: "Bez prodaje u prozoru", value: data?.evidenceWindow?.candidatesWithoutSalesInWindow ?? null },
      { key: "suppliersWithUnavailablePreviousWeekDenominator", label: "Dobavljači bez validnog prethodnog prozora", value: data?.evidenceWindow?.suppliersWithUnavailablePreviousWeekDenominator ?? null },
    ],
    [candidateCounts.highPriority, data, decisionRows.length]
  );

  const evidenceBasis = useMemo(() => {
    const window = data?.evidenceWindow;
    if (!window) return null;
    return `UTC prozor ${window.salesWindowFromUtc} – ${window.salesWindowToUtc}; količine su potpisane neto vrednosti; nepozitivan neto i nepozitivan prethodni prozor ostaju nedostupni za preporuku.`;
  }, [data?.evidenceWindow]);

  const handleSort = (field: SortField) => {
    if (sortField === field) {
      setSortDir((current) => (current === "asc" ? "desc" : "asc"));
      return;
    }
    setSortField(field);
    setSortDir(field === "sku" || field === "supplierName" ? "asc" : "desc");
  };

  const syncQueryState = (filters: ActiveFilters, focus: FocusFilter, nextPage: number) => {
    setSearchParams(buildPreNivelacijaSearchParams(filters, focus, nextPage, dataScope), { replace: true });
  };

  const handleApplyFilters = () => {
    const nextFilters: ActiveFilters = {
      supplierId,
      seasonId,
      footwearTypeId,
      minScore,
      noSaleDaysMin,
    };
    setPage(1);
    setFocusFilter("all");
    setActiveFilters(nextFilters);
    syncQueryState(nextFilters, "all", 1);
  };

  const handleResetFilters = () => {
    const defaultFilters: ActiveFilters = {
      supplierId: null,
      seasonId: null,
      footwearTypeId: null,
      minScore: DEFAULT_MIN_SCORE,
      noSaleDaysMin: DEFAULT_NO_SALE_DAYS_MIN,
    };
    setSupplierId(null);
    setSeasonId(null);
    setFootwearTypeId(null);
    setMinScore(DEFAULT_MIN_SCORE);
    setNoSaleDaysMin(DEFAULT_NO_SALE_DAYS_MIN);
    setPage(1);
    setFocusFilter("all");
    setActiveFilters(defaultFilters);
    syncQueryState(defaultFilters, "all", 1);
  };

  const handleFocusChange = (nextFocus: FocusFilter) => {
    setFocusFilter(nextFocus);
    setPage(1);
    syncQueryState(activeFilters, nextFocus, 1);
  };

  const handlePageChange = (nextPage: number) => {
    const safePage = Math.max(1, nextPage);
    setPage(safePage);
    syncQueryState(activeFilters, focusFilter, safePage);
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
        label: "Visok prioritet (globalno)",
        value: data?.summary.highPriorityCount.toLocaleString("sr-RS") ?? RECOMMENDATION_SIGNAL_UNAVAILABLE,
        tone: "success",
      },
      {
        key: "limited-signal",
        label: "Ograničen signal (globalno)",
        value: globalStatusCounts
          ? (globalStatusCounts.doNotTrust + globalStatusCounts.insufficientData).toLocaleString("sr-RS")
          : RECOMMENDATION_SIGNAL_UNAVAILABLE,
        tone: "warning",
      },
    ];
  }, [data?.summary.highPriorityCount, footwearTypeId, footwearTypeOptions, globalStatusCounts, seasonId, seasonOptions, supplierId, supplierOptions]);

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

    const detailParams = buildPreNivelacijaSearchParams(activeFilters, focusFilter, page, dataScope);
    navigate(`/analitika/pre-nivelacija-prioriteti/${row.artikalId}?${detailParams.toString()}`, {
      state: { backgroundLocation: location },
    });
  };

  return (
    <div className="pnp-decision-page">
        <AnalyticsTrustHeader
        title="Prioriteti pre-nivelacije"
        description="Operativna podrška za odluke po SKU pre faze sniženja."
        periodFrom={data?.evidenceWindow?.salesWindowFromUtc ?? null}
        periodTo={data?.evidenceWindow?.salesWindowToUtc ?? null}
        lastRefreshAt={dataMeta?.lastRefreshAtUtc ?? null}
        dataSource={`Analitika pre-nivelacije (opseg: ${dataScope})`}
        provenanceBasis={evidenceBasis}
        mode={data?.recommendationAllowed === true ? "recommendation" : "signal"}
        recommendationAllowed={data?.recommendationAllowed ?? null}
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
          <h2 className="pnp-decision-title">Prioriteti pre-nivelacije</h2>
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
          message={error.message}
          errorCode={error.errorCode}
          correlationId={error.correlationId}
          onRetry={refetch}
          helpHref="/analytics/data-quality"
        />
      ) : null}
      {staleWarning && data ? (
        <div className="pnp-decision-message info" role="status" data-testid="pnp-stale-refetch-warning">
          Prikazujemo prethodno učitane podatke. Novi upit nije uspeo.
        </div>
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
            showFilteredOutState
              ? { label: "Vrati prikaz svih prioriteta.", onClick: () => handleFocusChange("all") }
              : { label: "Promenite filtere dobavljača, sezone ili tipa obuće." },
            { label: "Proverite kvalitet podataka.", href: "/analytics/data-quality" },
          ]}
          dataQualityHref="/analytics/data-quality"
          refreshStatusHref="/admin/configuration?panel=workers"
          emptyReason={safeEmptyStateReason}
          onRetry={refetch}
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
              <strong>{formatNonNegativeNumber(data.summary.candidatesCount)}</strong>
            </article>
            <article className="pnp-decision-kpi analytics-kpi-card analytics-kpi-card--tone-success" data-note="Kandidati sa najjačim signalom za brzu intervenciju.">
              <span>Visok prioritet <InfoTip text="Globalni broj SKU u visokoj prioritetnoj bandi u celoj filtriranoj populaciji. Status preporuke i kvalitet podataka odvojeno određuju da li je akcija dozvoljena." /></span>
              <strong>{formatNonNegativeNumber(data.summary.highPriorityCount)}</strong>
            </article>
            <article className="pnp-decision-kpi analytics-kpi-card analytics-kpi-card--tone-warning" data-note="Ukupna zaliha kod SKU koji nose operativni rizik.">
              <span>Zaliha pod rizikom <InfoTip text="Ukupna zaliha u komadima svih prikazanih kandidatskih SKU (u skladu sa filterima). Iskazano u komadima, ne u RSD vrednosti. Veća zaliha bez prodaje = veći operativni rizik." /></span>
              <strong>{formatNonNegativeNumber(data.summary.totalStockAtRisk)}</strong>
              <em>kom ukupno</em>
            </article>
            <article className="pnp-decision-kpi analytics-kpi-card analytics-kpi-card--tone-value" data-note="Procena prihoda ako se kandidati istaknu umesto da se sniže.">
              <span>Procena povećanja prihoda <InfoTip text="Procenjeni prihod: scenario isticanja minus scenario sniženja za sve 'Pojačaj' kandidate. PROCENA – bazirana na scenariju sa istorijskim podacima prodaje, nije garantovani prihod. Tretirati kao relativni signal, ne kao apsolutnu predikciju." /></span>
              <strong>{fmtRsd(normalizeFiniteNumber(data.summary.expectedHighlightRevenueUplift))}</strong>
            </article>
            <article className="pnp-decision-kpi analytics-kpi-card analytics-kpi-card--tone-warning" data-note="Procena gubitka koji može da se izbegne pre nivelacije.">
              <span>Procena izbegljivog gubitka od sniženja <InfoTip text="Procenjeni gubitak prihoda koji se može izbeći pravovremenom intervencijom pre nivelacije. PROCENA bazirana na scenario modelu (isticanje vs. sniženje u 30-dnevnom prozoru). Apsolutni iznos je okvirna procena – relativni odnos između SKU-ova je relevantniji." /></span>
              <strong className="trend-down">{fmtRsd(normalizeFiniteNumber(data.summary.estimatedAvoidableMarkdownLoss))}</strong>
            </article>
          </section>

          <section className="pnp-decision-panels">
            <article className="pnp-decision-card analytics-surface-panel">
              <h2>Koncentracija akcije po dobavljačima</h2>
              <p>Top dobavljači po skoru akcije u celoj filtriranoj prioritetnoj populaciji.</p>
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
                    Vidljiva strana: {recommendationStatusLabel("increase_focus")}: {candidateCounts.increaseFocus} | {recommendationStatusLabel("maintain")}: {candidateCounts.maintain} | {recommendationStatusLabel("review")}: {candidateCounts.review} | {recommendationStatusLabel("do_not_trust")}: {candidateCounts.doNotTrust} | {recommendationStatusLabel("insufficient_data")}: {candidateCounts.insufficientData} | Visok prioritet: {candidateCounts.highPriority}
                  </p>
                </div>
              </div>

              <div className="pnp-focus-tabs" role="tablist">
                {(["all", "increaseFocus", "maintain", "review", "doNotTrust", "insufficientData", "highPriority"] as FocusFilter[]).map((f) => {
                  const count =
                    f === "all" ? preNivelacijaProjections.tableRows.length
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
                      onClick={() => handleFocusChange(f)}
                    >
                      {FOCUS_LABELS[f]} ({count})
                    </button>
                  );
                })}
              </div>

              <AnalyticsDataTable
                testId="pre-nivelacija-prioriteti-data-table"
                rowCount={preNivelacijaProjections.filteredRows.length}
                truncationLabel={focusFilter !== "all" ? `Fokus: ${FOCUS_LABELS[focusFilter]}` : undefined}
                toolbar={(
                  <div className="pnp-table-toolbar">
                    <div className="pnp-decision-table-controls">
                      <button type="button" onClick={() => canGoPrev && handlePageChange(page - 1)} disabled={!canGoPrev || loading}>Prethodna</button>
                      <span>Strana {page}</span>
                      <button type="button" onClick={() => canGoNext && handlePageChange(page + 1)} disabled={!canGoNext || loading}>Sledeća</button>
                    </div>
                    <AnalyticsTableToolbar
                      tableKey="pre-nivelacija-prioriteti"
                      tableTitle="Podrška za odluku pre nivelacije"
                      columns={decisionColumns}
                      rows={[...preNivelacijaProjections.exportRows]}
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
                    {preNivelacijaProjections.filteredRows.length === 0 ? (
                      <tr>
                        <td colSpan={9} className="pnp-decision-empty-row">Nema podataka za izabrane filtere.</td>
                      </tr>
                    ) : (
                      preNivelacijaProjections.filteredRows.map((row) => {
                        const expanded = expandedArtikalId === row.artikalId;
                        const reliability = reliabilitySignalDisplay(row);
                        return (
                          <tr key={row.artikalId} className={expanded ? "expanded-row" : ""}>
                            <td>{row.sku}</td>
                            <td title={row.supplierName}>{row.supplierName}</td>
                            <td className="align-right">
                              <div className="pnp-score-cell">
                                <span>{formatFiniteNumber(row.preNivelacijaScore, 1)}</span>
                                {row.preNivelacijaScore != null ? (
                                  <div
                                    className="pnp-score-mini-bar"
                                    style={{ width: `${clamp(row.preNivelacijaScore, 0, 100)}%` }}
                                    data-level={row.preNivelacijaScore >= 68 ? "high" : row.preNivelacijaScore >= 43 ? "mid" : "low"}
                                  />
                                ) : null}
                              </div>
                            </td>
                            <td className="align-right">{formatNonNegativeNumber(row.stockUnits)}</td>
                            <td className="align-right">{formatNonNegativeNumber(row.daysSinceLastSale)}</td>
                            <td className={`align-right ${deltaTrendClass(row.recommendationAllowed, row.revenueDelta)}`}>{formatGatedRsd(row.recommendationAllowed, row.revenueDelta)}</td>
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
                                <small className="pnp-status-next">{getStatusNextStep(row)}</small>
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
                  <strong>{priorityBandLabel(selectedRow.priorityBand)}</strong>
                </article>
                <article>
                  <span>Scenario isticanje (30d procena prihoda)</span>
                  <strong>{formatGatedRsd(selectedRow.recommendationAllowed, selectedRow.scenarioHighlightNow.expectedRevenue30d)}</strong>
                </article>
                <article>
                  <span>Scenario sniženje (30d procena prihoda)</span>
                  <strong>{formatGatedRsd(selectedRow.recommendationAllowed, selectedRow.scenarioMarkdownNow.expectedRevenue30d)}</strong>
                </article>
                <article>
                  <span>Procenjena delta prihoda</span>
                  <strong className={deltaTrendClass(selectedRow.recommendationAllowed, selectedRow.revenueDelta)}>{formatGatedRsd(selectedRow.recommendationAllowed, selectedRow.revenueDelta)}</strong>
                </article>
                <article>
                  <span>Procenjena delta marže</span>
                  <strong className={selectedRow.marginDelta != null && canShowMarkdownMarginSignal(selectedRow) ? (selectedRow.marginDelta >= 0 ? "trend-up" : "trend-down") : ""}>
                    {canShowMarkdownMarginSignal(selectedRow)
                      ? fmtRsd(selectedRow.marginDelta)
                      : hasMissingCostSignal(selectedRow.reasonCodes)
                        ? "Nije dostupno bez troška"
                        : RECOMMENDATION_SIGNAL_UNAVAILABLE}
                  </strong>
                </article>
                <article>
                  <span>Zaliha (kom.)</span>
                  <strong>{formatNonNegativeNumber(selectedRow.stockUnits)}</strong>
                </article>
                <article>
                  <span>Dana bez prodaje</span>
                  <strong>{formatNonNegativeNumber(selectedRow.daysSinceLastSale)}</strong>
                </article>
                <article>
                  <span>Ocena preporuke</span>
                  <strong>{selectedRow.decisionScoreAvailable ? formatFiniteNumber(selectedRow.decisionScore, 1) : RECOMMENDATION_SIGNAL_UNAVAILABLE}</strong>
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
                {selectedRow.recommendationAllowed ? <article className="pnp-decision-callout pnp-decision-callout--action">
                  <span>Sledeći korak</span>
                  <strong>{getRecommendedNextStep(selectedRow.status)}</strong>
                  <p>{selectedRow.statusReason}</p>
                </article> : null}
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
                    <span key={reasonCode} className="pnp-reason-code-chip">{recommendationReasonLabel(reasonCode)}</span>
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
                          {c.value != null ? <div className="pnp-score-bar" style={{ width: `${clamp(c.value, 0, 100)}%` }} /> : null}
                        </div>
                        <strong>{formatFiniteNumber(c.value, 1)}</strong>
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



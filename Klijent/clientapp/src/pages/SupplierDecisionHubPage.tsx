import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router-dom";
import {
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import AnalyticsEmptyState from "../components/analytics/AnalyticsEmptyState";
import AnalyticsErrorState from "../components/analytics/AnalyticsErrorState";
import AnalyticsDataTable from "../components/analytics/AnalyticsDataTable";
import SupplierDecisionReportActions from "../components/analytics/SupplierDecisionReportActions";
import AnalyticsTrustHeader from "../components/analytics/AnalyticsTrustHeader";
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import KpiExplainButton from "../components/analytics/KpiExplainButton";
import InfoTip from "../components/ui/InfoTip";
import SupplierExplainabilitySnapshot from "../components/supplierDecisionHub/SupplierExplainabilitySnapshot";
import SupplierDetailDrawer from "../components/supplierDecisionHub/SupplierDetailDrawer";
import { getSezone } from "../services/sezoneApi";
import { getAnalyticsActions, getAnalyticsRefreshStatus, upsertAnalyticsAction } from "../services/analyticsApi";
import type { AnalyticsActionDataQualityStatus, AnalyticsActionStatus, AnalyticsRefreshStatus } from "../types/analytics";
import { buildSupplierDecisionReportPayload } from "../services/supplierDecisionReport";
import {
  calculateSupplierMarginContribution,
  classifySupplierMarginContributionEvidence,
} from "../services/supplierDecisionMargin";
import { buildSupplierDecisionReportHref, buildSupplierDecisionScorecardHref } from "../services/supplierDecisionReportQuery";
import {
  getAllSupplierDecisionRanking,
  getSupplierDecisionDetails,
  getSupplierDecisionSummary,
  type RecommendationCode,
  type RankingItem,
  type RankingResponse,
  type SummaryResponse,
  type SupplierDecisionDetailsResponse,
  SupplierDecisionApiError,
  type SupplierDecisionHubFilters,
} from "../services/supplierDecisionHubApi";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import type { Sezona } from "../types/Sezona";
import { formatDate, fmtNumber, fmtPct, fmtRsd, fmtSignedPct, getPresetRange } from "../utils/analyticsFormatters";
import { getAnalyticsActionWriteErrorMessage } from "../utils/analyticsActionWriteErrors";
import { getSafeAnalyticsErrorMessage } from "../utils/analyticsErrorMessages";
import { formatMetricDisplayValue, isFiniteMetricNumber } from "../utils/analyticsMetricValue";
import { supplierDecisionDatasetLabel, supplierDecisionReasonText } from "../utils/supplierDecisionLabels";
import {
  getAnalyticsMetaMessage,
  isAnalyticsMetaInsufficient,
  isAnalyticsMetaError,
  isAnalyticsMetaWarning,
  shouldShowAnalyticsEmptyState,
} from "../utils/analyticsResponseMeta";
import { CHART_TOOLTIP_LABEL_STYLE, CHART_TOOLTIP_STYLE } from "../utils/chartTooltipStyle";
import {
  RECOMMENDATION_STATUS_PRIORITY,
  RECOMMENDATION_SIGNAL_UNAVAILABLE,
  normalizeRecommendationPct,
  normalizeRecommendationQualityStatus,
  recommendationQualityStyle,
  recommendationReasonLabel,
  recommendationReasonHints,
  recommendationStatusTone,
  type CanonicalRecommendationStatus,
  type RecommendationQualityStatus,
} from "../utils/canonicalRecommendationSemantics";
import type { SupplierEmbeddedPageProps } from "./supplierSharedState";
import "./SupplierDecisionHubPage.css";

type PeriodPreset = "30d" | "90d" | "180d" | "365d" | "custom";
type SortDir = "asc" | "desc";
type SortField = "supplierName" | "revenue" | "sharePct" | "preMarkdownMarginPct" | "qualityTrendPct" | "status";
type DecisionStatus = CanonicalRecommendationStatus;

type ActiveFilters = {
  fromDate: string;
  toDate: string;
  category: string | null;
  gender: string | null;
  seasonId: number | null;
  minRevenue: number | null;
  onlyHighConfidence: boolean;
  excludeOosBeforeMarkdown: boolean;
  supplierId: number | null;
  storeId: number | null;
  dataScope: string | null;
};

export type DecisionRow = RankingItem & {
  sharePct: number | null;
  marginContribution: number | null;
  qualityTrendPct: number | null;
  status: DecisionStatus;
  statusReason: string;
  normalizedConfidence: number | null;
  confidenceAvailable: boolean;
  reliabilityPct: number | null;
  reliabilityAvailable: boolean;
  dataQualityStatus: RecommendationQualityStatus;
  reasonCodes: string[];
};

const OPEN_ACTION_STATUSES: AnalyticsActionStatus[] = ["new", "accepted", "deferred"];

/**
 * Vrednost `preMarkdownMarginPct` skorkarte stiže iz API-ja kao odnos od 0 do 1.
 * Compact table / export / detail percent columns use percent units (35 = 35%).
 * `sharePct` and `qualityTrendPct` on DecisionRow are already percent units.
 */
export function toSupplierDecisionMarginPercentUnits(ratio: number | null | undefined): number | null {
  if (!isValidSupplierRatio(ratio)) return null;
  return ratio * 100;
}

export function calculateSupplierQualityTrendPct(
  fullPriceRevenueShare: number | null | undefined,
  markdownRevenueShare: number | null | undefined,
): number | null {
  if (!isValidSupplierRatio(fullPriceRevenueShare) || !isValidSupplierRatio(markdownRevenueShare)) return null;
  const trendPct = (fullPriceRevenueShare - markdownRevenueShare) * 100;
  return Number.isFinite(trendPct) ? trendPct : null;
}

export function calculateSupplierRevenueSharePct(
  revenue: number | null | undefined,
  totalRevenue: number | null | undefined,
): number | null {
  if (!isFiniteMetricNumber(revenue) || !isFiniteMetricNumber(totalRevenue) || totalRevenue <= 0) return null;
  const sharePct = (revenue / totalRevenue) * 100;
  return Number.isFinite(sharePct) ? sharePct : null;
}

function compareFiniteMetrics(left: number | null | undefined, right: number | null | undefined): number {
  const leftValue = Number.isFinite(left) ? left! : null;
  const rightValue = Number.isFinite(right) ? right! : null;
  if (leftValue == null && rightValue == null) return 0;
  if (leftValue == null) return -1;
  if (rightValue == null) return 1;
  return leftValue - rightValue;
}

function fmtSupplierUnits(value: number | null | undefined): string {
  const formatted = fmtNumber(value, 0, RECOMMENDATION_SIGNAL_UNAVAILABLE);
  return formatted === RECOMMENDATION_SIGNAL_UNAVAILABLE ? formatted : `${formatted} kom`;
}

function isValidSupplierRatio(value: number | null | undefined): value is number {
  return isFiniteMetricNumber(value) && value >= 0 && value <= 1;
}

export const decisionColumns: AnalyticsTableColumn<DecisionRow>[] = [
  { key: "supplierName", header: "Dobavljač", dataType: "text" },
  { key: "revenue", header: "Prihod", dataType: "currency" },
  { key: "sharePct", header: "Udeo %", dataType: "percent" },
  {
    key: "preMarkdownMarginPct",
    header: "Marža %",
    dataType: "percent",
    getValue: (row) => toSupplierDecisionMarginPercentUnits(row.preMarkdownMarginPct),
  },
  { key: "qualityTrendPct", header: "Trend pune cene %", dataType: "percent" },
  { key: "status", header: "Signal skorkarte", dataType: "text" },
];

function clamp(value: number, min: number, max: number): number { return Math.max(min, Math.min(max, value)); }
function sortMarker(field: SortField, activeField: SortField, dir: SortDir): string { if (field !== activeField) return ""; return dir === "asc" ? " ^" : " v"; }
function statusClass(status: DecisionStatus): string {
  const tone = recommendationStatusTone(status);
  if (tone === "boost") return "sdh-decision-status status-boost";
  if (tone === "reduce") return "sdh-decision-status status-reduce";
  return "sdh-decision-status status-keep";
}
function statusDisplayLabel(status: DecisionStatus): string {
  if (status === "increase_focus") return "Pojačaj";
  if (status === "maintain") return "Zadrži";
  if (status === "review") return "Pregledaj";
  if (status === "do_not_trust") return "Ne veruj";
  return "Nedovoljno podataka";
}

function statusTooltip(status: DecisionStatus): string {
  if (status === "increase_focus") return "Pozitivan signal; povećati fokus uz standardnu kontrolu rizika.";
  if (status === "maintain") return "Stabilan signal; zadržati trenutni nivo fokusa.";
  if (status === "review") return "Mešovit signal; potreban ručni pregled pre promene fokusa.";
  if (status === "do_not_trust") return "Signal je nepouzdan za akciju; ne donositi odluku bez dodatne provere.";
  return "Nedovoljno podataka za pouzdanu preporuku.";
}

function supplierDecisionQualityLabel(status: RecommendationQualityStatus): string {
  if (status === "good") return "Dobar kvalitet podataka";
  if (status === "warning") return "Upozorenje kvaliteta podataka";
  if (status === "critical") return "Kritičan kvalitet podataka";
  return "Nedovoljno podataka";
}
export function trendClass(value: number | null | undefined): string {
  if (value == null || !Number.isFinite(value)) return "trend-neutral";
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

function recommendationToStatus(code: RecommendationCode): DecisionStatus {
  if (code === "EXPAND" || code === "EXPAND_SELECTIVELY") return "increase_focus";
  if (code === "HOLD") return "maintain";
  if (code === "OOS_FALSE_NEGATIVE" || code === "REVIEW_QUALITY") return "review";
  if (code === "ASSORTMENT_REDUCE" || code === "PRICE_NEGOTIATE") return "do_not_trust";
  return "insufficient_data";
}

function buildStatusTooltip(row: DecisionRow): string {
  const confidenceText = row.confidenceAvailable
    ? formatMetricDisplayValue({ value: row.normalizedConfidence, kind: "percent", digits: 0 })
    : RECOMMENDATION_SIGNAL_UNAVAILABLE;
  const reliabilityText = row.reliabilityAvailable
    ? formatMetricDisplayValue({ value: row.reliabilityPct, kind: "percent", digits: 0 })
    : RECOMMENDATION_SIGNAL_UNAVAILABLE;
  const shareText = formatMetricDisplayValue({ value: row.sharePct, kind: "percent", digits: 1 });
  const marginText = formatMetricDisplayValue({ value: toSupplierDecisionMarginPercentUnits(row.preMarkdownMarginPct), kind: "percent", digits: 1 });
  const qualityText = supplierDecisionQualityLabel(row.dataQualityStatus);
  const hintText = recommendationReasonHints(row.reasonCodes)
    .map((hint) => hint.replace("Marza", "Marža").replace("potvrdjen", "potvrđen").replace("poredjenje", "poređenje"))
    .join(" | ");
  return `${statusDisplayLabel(row.status)}: ${statusTooltip(row.status)} | ${row.statusReason} | Udeo ${shareText} | Marža ${marginText} | Trend pune cene ${fmtSignedPct(row.qualityTrendPct, 1)} | Sigurnost ${confidenceText} | Pouzdanost ${reliabilityText} | Kvalitet podataka ${qualityText}${hintText ? ` | Napomene: ${hintText}` : ""}`;
}

function toActionDataQualityStatus(value: RecommendationQualityStatus): AnalyticsActionDataQualityStatus {
  const normalized = (value ?? "").trim().toLowerCase();
  if (normalized === "good" || normalized === "warning" || normalized === "critical" || normalized === "insufficient_data") {
    return normalized;
  }

  return "insufficient_data";
}

function mapSupplierActionPriority(row: DecisionRow, recommendationAllowed: boolean): "P1" | "P2" {
  if (!recommendationAllowed) return "P2";
  return row.status === "do_not_trust" || row.status === "review" ? "P1" : "P2";
}

function buildSupplierActionSourceKey(row: DecisionRow, filters: ActiveFilters, recommendationAllowed: boolean): string {
  const actionKind = recommendationAllowed ? "negotiation" : "signal_check";
  return `supplier:${actionKind}:${row.supplierId}:${filters.fromDate}:${filters.toDate}:${filters.category ?? "all"}:${filters.gender ?? "all"}:${filters.seasonId ?? "all"}:${filters.minRevenue ?? "all"}:${filters.onlyHighConfidence}:${filters.excludeOosBeforeMarkdown}:${filters.storeId ?? "all"}:${filters.dataScope ?? "all"}`;
}

function formatSupplierPeriodRange(from: string | null | undefined, to: string | null | undefined): string {
  if (!from || !to) return "nije dostupno";
  return `${formatDate(from)} - ${formatDate(to)}`;
}

export default function SupplierDecisionHubPage({ embedded = false, sharedFilters, onTrustMetadataChange }: SupplierEmbeddedPageProps = {}) {
  const requestIdRef = useRef(0);
  const detailRequestIdRef = useRef(0);
  const detailAbortRef = useRef<AbortController | null>(null);
  const hasSummaryRef = useRef(false);
  const hasRankingRef = useRef(false);
  const initialRange = useMemo(() => getPresetRange("30d"), []);

  const [periodPreset, setPeriodPreset] = useState<PeriodPreset>(sharedFilters?.periodPreset ?? "30d");
  const [fromDate, setFromDate] = useState(sharedFilters?.fromDate ?? initialRange.fromDate);
  const [toDate, setToDate] = useState(sharedFilters?.toDate ?? initialRange.toDate);
  const [category, setCategory] = useState(sharedFilters?.category ?? "");
  const [gender, setGender] = useState(sharedFilters?.gender ?? "");
  const [seasonId, setSeasonId] = useState<number | null>(sharedFilters?.seasonId ?? null);
  const [minRevenue, setMinRevenue] = useState<number | null>(sharedFilters?.minRevenue ?? null);
  const [onlyHighConfidence, setOnlyHighConfidence] = useState(sharedFilters?.onlyHighConfidence === true);
  const [excludeOosBeforeMarkdown, setExcludeOosBeforeMarkdown] = useState(sharedFilters?.excludeOosBeforeMarkdown === true);
  const [activeFilters, setActiveFilters] = useState<ActiveFilters>({
    fromDate: sharedFilters?.fromDate ?? initialRange.fromDate,
    toDate: sharedFilters?.toDate ?? initialRange.toDate,
    category: sharedFilters?.category ?? null,
    gender: sharedFilters?.gender ?? null,
    seasonId: sharedFilters?.seasonId ?? null,
    minRevenue: sharedFilters?.minRevenue ?? null,
    onlyHighConfidence: sharedFilters?.onlyHighConfidence === true,
    excludeOosBeforeMarkdown: sharedFilters?.excludeOosBeforeMarkdown === true,
    supplierId: sharedFilters?.supplierId ?? null,
    storeId: sharedFilters?.storeId ?? null,
    dataScope: sharedFilters?.dataScope ?? null,
  });

  const [seasons, setSeasons] = useState<Sezona[]>([]);
  const [summary, setSummary] = useState<SummaryResponse | null>(null);
  const [previousSummary, setPreviousSummary] = useState<SummaryResponse | null>(null);
  const [ranking, setRanking] = useState<RankingResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<{ message: string; errorCode?: string | null; correlationId?: string | null } | null>(null);
  const [staleWarning, setStaleWarning] = useState<string | null>(null);
  const [sortField, setSortField] = useState<SortField>("status");
  const [sortDir, setSortDir] = useState<SortDir>("desc");
  const [expandedSupplierId, setExpandedSupplierId] = useState<number | null>(null);
  const [detailSupplierId, setDetailSupplierId] = useState<number | null>(null);
  const [supplierDetails, setSupplierDetails] = useState<SupplierDecisionDetailsResponse | null>(null);
  const [supplierDetailLoading, setSupplierDetailLoading] = useState(false);
  const [supplierDetailError, setSupplierDetailError] = useState<string | null>(null);
  const [refreshStatus, setRefreshStatus] = useState<AnalyticsRefreshStatus | null>(null);
  const [queuedActionKeys, setQueuedActionKeys] = useState<Set<string>>(new Set());
  const [queueBusyKey, setQueueBusyKey] = useState<string | null>(null);
  const [queueMessage, setQueueMessage] = useState<string | null>(null);

  const invalidRange = useMemo(() => (!fromDate || !toDate ? false : new Date(fromDate) > new Date(toDate)), [fromDate, toDate]);

  useEffect(() => {
    if (!sharedFilters) return;
    setPeriodPreset(sharedFilters.periodPreset);
    setFromDate(sharedFilters.fromDate);
    setToDate(sharedFilters.toDate);
    setCategory(sharedFilters.category ?? "");
    setGender(sharedFilters.gender ?? "");
    setSeasonId(sharedFilters.seasonId ?? null);
    setMinRevenue(sharedFilters.minRevenue ?? null);
    setOnlyHighConfidence(sharedFilters.onlyHighConfidence === true);
    setExcludeOosBeforeMarkdown(sharedFilters.excludeOosBeforeMarkdown === true);
    setActiveFilters((current) => {
      const next = {
        ...current,
        fromDate: sharedFilters.fromDate,
        toDate: sharedFilters.toDate,
        category: sharedFilters.category ?? null,
        gender: sharedFilters.gender ?? null,
        seasonId: sharedFilters.seasonId ?? null,
        minRevenue: sharedFilters.minRevenue ?? null,
        onlyHighConfidence: sharedFilters.onlyHighConfidence === true,
        excludeOosBeforeMarkdown: sharedFilters.excludeOosBeforeMarkdown === true,
        supplierId: sharedFilters.supplierId,
        storeId: sharedFilters.storeId,
        dataScope: sharedFilters.dataScope,
      };
      return current.fromDate === next.fromDate
        && current.toDate === next.toDate
        && current.category === next.category
        && current.gender === next.gender
        && current.seasonId === next.seasonId
        && current.minRevenue === next.minRevenue
        && current.onlyHighConfidence === next.onlyHighConfidence
        && current.excludeOosBeforeMarkdown === next.excludeOosBeforeMarkdown
        && current.supplierId === next.supplierId
        && current.storeId === next.storeId
        && current.dataScope === next.dataScope
        ? current
        : next;
    });
  }, [sharedFilters]);

  useEffect(() => {
    const loadSeasons = async () => {
      try {
        setSeasons(await getSezone());
      } catch {
        // Preserve the last known season list on transient failures instead of faking an empty filter set.
      }
    };
    void loadSeasons();
  }, []);

  const closeSupplierDetail = useCallback(() => {
    detailAbortRef.current?.abort();
    detailAbortRef.current = null;
    detailRequestIdRef.current += 1;
    setDetailSupplierId(null);
    setSupplierDetails(null);
    setSupplierDetailError(null);
    setSupplierDetailLoading(false);
  }, []);

  useEffect(() => () => closeSupplierDetail(), [closeSupplierDetail]);

  const load = useCallback(async (filters: ActiveFilters) => {
    const requestId = ++requestIdRef.current;
    closeSupplierDetail();
    setLoading(true);
    setError(null);
    setStaleWarning(null);
    try {
      const baseFilters: SupplierDecisionHubFilters = {
        fromDate: filters.fromDate,
        toDate: filters.toDate,
        category: filters.category ?? undefined,
        gender: filters.gender ?? undefined,
        seasonId: filters.seasonId ?? undefined,
        minRevenue: filters.minRevenue ?? undefined,
        onlyHighConfidence: filters.onlyHighConfidence,
        excludeOosBeforeMarkdown: filters.excludeOosBeforeMarkdown,
        supplierId: filters.supplierId ?? undefined,
        storeId: filters.storeId,
        dataScope: filters.dataScope,
      };
      const prevRange = buildPreviousRange(filters.fromDate, filters.toDate);
      const prevFilters: SupplierDecisionHubFilters = { ...baseFilters, fromDate: prevRange.fromDate, toDate: prevRange.toDate };

      const [summaryResult, rankingResult, previousResult, refreshStatusResult] = await Promise.allSettled([
        getSupplierDecisionSummary(baseFilters),
        getAllSupplierDecisionRanking(baseFilters, { pageSize: 100, sortBy: "supplierQualityIndex", sortDir: "desc" }),
        getSupplierDecisionSummary(prevFilters),
        getAnalyticsRefreshStatus(),
      ]);
      if (requestId !== requestIdRef.current) return;
      if (summaryResult.status === "rejected" || rankingResult.status === "rejected") {
        const reason = summaryResult.status === "rejected"
          ? summaryResult.reason
          : rankingResult.status === "rejected"
            ? rankingResult.reason
            : new Error("Neuspešno učitavanje podataka skorkarte dobavljača.");
        if (reason instanceof SupplierDecisionApiError) {
          throw reason;
        }
        throw new Error("Neuspešno učitavanje podataka skorkarte dobavljača.");
      }
      setSummary(summaryResult.value);
      hasSummaryRef.current = true;
      setRanking(rankingResult.value);
      hasRankingRef.current = true;
      setPreviousSummary(previousResult.status === "fulfilled" ? previousResult.value : null);
      setRefreshStatus(refreshStatusResult.status === "fulfilled" ? refreshStatusResult.value : null);
      setExpandedSupplierId(null);
    } catch (reason) {
      if (requestId !== requestIdRef.current) return;
      const hasPreviousData = hasSummaryRef.current || hasRankingRef.current;
      if (!hasPreviousData) {
        setSummary(null);
        setPreviousSummary(null);
        setRanking(null);
      } else {
        setStaleWarning("Prikazujemo prethodno učitane podatke. Novi upit nije uspeo i podaci mogu biti zastareli.");
      }
      if (reason instanceof SupplierDecisionApiError) {
        setError({
          message: reason.message,
          errorCode: reason.errorCode,
          correlationId: reason.correlationId,
        });
      } else {
        setError({
          message: reason instanceof Error ? reason.message : "Greška pri učitavanju skorkarte dobavljača.",
        });
      }
    } finally {
      if (requestId === requestIdRef.current) setLoading(false);
    }
  }, [closeSupplierDetail]);

  useEffect(() => { void load(activeFilters); }, [activeFilters, load]);

  useEffect(() => {
    let cancelled = false;
    setQueuedActionKeys(new Set());

    void (async () => {
      try {
        const responses = await Promise.all(
          OPEN_ACTION_STATUSES.map((status) => getAnalyticsActions({
            sourceType: "supplier",
            status,
            page: 1,
            pageSize: 200,
          })),
        );

        if (cancelled) return;
        const keys = new Set<string>();
        for (const response of responses) {
          for (const item of response.items) {
            if (item.sourceKey) keys.add(item.sourceKey);
          }
        }
        setQueuedActionKeys(keys);
      } catch {
        if (!cancelled) setQueuedActionKeys(new Set());
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [activeFilters.dataScope, activeFilters.fromDate, activeFilters.storeId, activeFilters.toDate]);

  const trustMetadata = summary?.trustMetadata ?? ranking?.trustMetadata ?? null;
  const scorecardMeta = ranking?.meta ?? summary?.meta ?? null;
  const scorecardMetaMessage = getAnalyticsMetaMessage(scorecardMeta);
  const recommendationAllowed = trustMetadata?.recommendationAllowed === true;
  const hasVisibleData = Boolean(summary && ranking);
  const showBlockingError = Boolean(
    isAnalyticsMetaError(scorecardMeta)
    || (error && !hasVisibleData)
  );
  const showMetaWarning = !loading && !showBlockingError && isAnalyticsMetaWarning(scorecardMeta);
  const resolvedLastRefreshAt = refreshStatus?.lastSuccessfulRefreshAtUtc ?? trustMetadata?.lastRefreshAtUtc ?? null;
  const requestedDatasetLabel = supplierDecisionDatasetLabel(trustMetadata?.requestedDataset);
  const effectiveDatasetLabel = supplierDecisionDatasetLabel(trustMetadata?.effectiveDataset);
  const effectivePeriodLabel = typeof trustMetadata?.effectivePeriodLabel === "string"
    ? trustMetadata.effectivePeriodLabel.trim() || null
    : null;
  const hasDatasetFallback = Boolean(
    trustMetadata?.usedFallback
    || (requestedDatasetLabel && effectiveDatasetLabel && requestedDatasetLabel !== effectiveDatasetLabel),
  );
  const fallbackReasonText = trustMetadata?.fallbackReason
    ? supplierDecisionReasonText(getSafeAnalyticsErrorMessage(
      trustMetadata.fallbackReason,
      trustMetadata.fallbackReasonCode,
      "Dodatni razlog pomoćnog skupa nije naveden.",
    ))
    : null;
  const requestedPeriodFrom = trustMetadata?.requestedPeriodFrom ?? trustMetadata?.requestedFrom ?? activeFilters.fromDate;
  const requestedPeriodTo = trustMetadata?.requestedPeriodTo ?? trustMetadata?.requestedTo ?? activeFilters.toDate;
  const effectivePeriodFrom = trustMetadata?.effectiveFrom ?? summary?.from ?? null;
  const effectivePeriodTo = trustMetadata?.effectiveTo ?? summary?.to ?? null;
  const observedPeriodFrom = summary?.from ?? null;
  const observedPeriodTo = summary?.to ?? null;

  const decisionRows = useMemo<DecisionRow[]>(() => {
    const rows = ranking?.items ?? [];
    if (rows.length === 0) return [];
    const totalRevenue = rows.reduce((sum, item) => Number.isFinite(item.revenue) ? sum + item.revenue : sum, 0);

    return rows.map((item) => {
      const sharePct = calculateSupplierRevenueSharePct(item.revenue, totalRevenue);
      const marginContribution = calculateSupplierMarginContribution(item);
      const qualityTrendPct = calculateSupplierQualityTrendPct(item.fullPriceRevenueShare, item.markdownRevenueShare);
      const confidencePctValue = normalizeRecommendationPct(item.confidenceScore);
      const normalizedConfidence = confidencePctValue ?? null;

      const status = recommendationAllowed
        ? recommendationToStatus(item.recommendationCode)
        : "insufficient_data";
      const backendStatusReason = typeof item.statusReason === "string" ? item.statusReason.trim() : "";
      const statusReason = backendStatusReason
        || (recommendationAllowed
          ? "Server nije dostavio obrazloženje za ovaj signal skorkarte."
          : (trustMetadata?.usedFallback
            ? "Za izabrani period nema dovoljno podataka; prikaz je pomoćni signal iz šireg skupa podataka."
            : "Nedovoljno podataka u izabranom periodu; signal skorkarte je pomoćnog karaktera."));
      const reliabilityPctValue = normalizeRecommendationPct(item.reliabilityPct);
      const reasonCodes = Array.isArray(item.reasonCodes)
        ? item.reasonCodes.filter((code): code is string => typeof code === "string")
        : [];

      return {
        ...item,
        sharePct,
        marginContribution,
        qualityTrendPct,
        status,
        statusReason,
        normalizedConfidence,
        confidenceAvailable: confidencePctValue != null,
        reliabilityPct: reliabilityPctValue ?? null,
        reliabilityAvailable: recommendationAllowed && reliabilityPctValue != null,
        dataQualityStatus: normalizeRecommendationQualityStatus(item.dataQualityStatus),
        reasonCodes,
      };
    });
  }, [ranking?.items, recommendationAllowed, trustMetadata?.usedFallback]);

  const sortedRows = useMemo(() => {
    const rows = [...decisionRows];
    return rows.sort((a, b) => {
      let compare = 0;
      if (sortField === "supplierName") compare = a.supplierName.localeCompare(b.supplierName, "sr");
      else if (sortField === "revenue") compare = compareFiniteMetrics(a.revenue, b.revenue);
      else if (sortField === "sharePct") compare = compareFiniteMetrics(a.sharePct, b.sharePct);
      else if (sortField === "preMarkdownMarginPct") compare = compareFiniteMetrics(a.preMarkdownMarginPct, b.preMarkdownMarginPct);
      else if (sortField === "qualityTrendPct") compare = compareFiniteMetrics(a.qualityTrendPct, b.qualityTrendPct);
      else if (sortField === "status") compare = RECOMMENDATION_STATUS_PRIORITY[a.status] - RECOMMENDATION_STATUS_PRIORITY[b.status];
      if (compare === 0) compare = (a.normalizedConfidence ?? -1) - (b.normalizedConfidence ?? -1);
      return sortDir === "asc" ? compare : -compare;
    });
  }, [decisionRows, sortDir, sortField]);

  const totalRevenue = useMemo(() => sortedRows.reduce((sum, row) => Number.isFinite(row.revenue) ? sum + row.revenue : sum, 0), [sortedRows]);
  const top5SharePct = useMemo(() => {
    if (sortedRows.length === 0 || totalRevenue <= 0) return null;
    const top5 = [...sortedRows].sort((a, b) => compareFiniteMetrics(b.revenue, a.revenue)).slice(0, 5).reduce((sum, row) => Number.isFinite(row.revenue) ? sum + row.revenue : sum, 0);
    return (top5 / totalRevenue) * 100;
  }, [sortedRows, totalRevenue]);
  const totalMarginContribution = useMemo(() => {
    const evidenceState = classifySupplierMarginContributionEvidence(sortedRows);
    if (evidenceState !== "measured" && evidenceState !== "measured_zero") return null;
    return sortedRows.reduce((sum, row) => sum + (row.marginContribution ?? 0), 0);
  }, [sortedRows]);
  const fullPriceDeltaPctPoints = useMemo(() => {
    if (!summary || !previousSummary) return null;
    return calculateSupplierQualityTrendPct(summary.fullPriceRevenueShare, previousSummary.fullPriceRevenueShare);
  }, [previousSummary, summary]);
  const supplierCounts = useMemo(() => ({
    boost: sortedRows.filter((row) => row.status === "increase_focus").length,
    keep: sortedRows.filter((row) => row.status === "maintain").length,
    caution: sortedRows.filter((row) => row.status === "review").length,
    reduce: sortedRows.filter((row) => row.status === "do_not_trust").length,
    insufficient: sortedRows.filter((row) => row.status === "insufficient_data").length,
  }), [sortedRows]);
  const zeroStateExplanation = useMemo(() => {
    if (!summary || !ranking) return null;

    if (!trustMetadata?.hasData && trustMetadata?.hasExplicitDateRange && !trustMetadata?.usedFallback) {
      return "Za traženi period nema zapisa skorkarte za dobavljače. Sistem nije koristio širi period kao pomoćni skup podataka, pa je rezultat eksplicitno prazan za ovaj opseg.";
    }

    if (!trustMetadata?.hasData && trustMetadata?.hasExplicitDateRange && trustMetadata?.usedFallback) {
      return `Za izabrani period nema dovoljno podataka. Korišćen je skup podataka ${trustMetadata.effectivePeriodLabel} kao pomoćni signal, ali ni on nema dovoljno zapisa skorkarte za prikaz.`;
    }

    const allKeyMetricsZero =
      totalRevenue === 0 &&
      totalMarginContribution === 0 &&
      (summary.capitalAtRisk ?? 0) === 0 &&
      top5SharePct === 0;

    if (!allKeyMetricsZero) return null;

    if (ranking.totalCount === 0 || summary.supplierCount === 0) {
      return "Skorkarta se puni iz dobavljača koji imaju artikle sa prvom nivelacijom u izabranom periodu. Ako takvih zapisa nema, pokazatelji skorkarte ostaju na nuli iako Pregled može imati promet, jer Pregled koristi širi prodajni skup.";
    }

    return "Postoje zapisi za skorkartu, ali su ključni pokazatelji trenutno 0. Proveri period, objekat, dobavljača i minimalni prihod; ako Pregled ima promet, a skorkarta ostaje na nuli, potrebno je osvežiti analitiku skorkarte.";
  }, [ranking, summary, top5SharePct, totalMarginContribution, totalRevenue, trustMetadata?.hasData, trustMetadata?.hasExplicitDateRange]);

  const emptyStateVariant = useMemo<"no_data" | "insufficient_data" | "filtered_out">(() => {
    const scorecardRowCount = ranking?.items.length ?? 0;
    if (
      trustMetadata?.dataCoverageStatus === "insufficient_data"
      || recommendationAllowed === false
      || (shouldShowAnalyticsEmptyState(scorecardMeta, scorecardRowCount) && isAnalyticsMetaInsufficient(scorecardMeta))
    ) {
      return "insufficient_data";
    }

    const hasNarrowFilters = Boolean(
      activeFilters.supplierId
      || activeFilters.storeId
      || activeFilters.category
      || activeFilters.gender
      || activeFilters.minRevenue
      || activeFilters.seasonId
      || activeFilters.onlyHighConfidence
      || activeFilters.excludeOosBeforeMarkdown,
    );
    if (hasNarrowFilters) {
      return "filtered_out";
    }

    return "no_data";
  }, [
    activeFilters.category,
    activeFilters.excludeOosBeforeMarkdown,
    activeFilters.gender,
    activeFilters.minRevenue,
    activeFilters.onlyHighConfidence,
    activeFilters.seasonId,
    activeFilters.storeId,
    activeFilters.supplierId,
    recommendationAllowed,
    ranking?.items.length,
    scorecardMeta,
    trustMetadata?.dataCoverageStatus,
  ]);

  useEffect(() => {
    if (!embedded || !onTrustMetadataChange) return;

    if (showBlockingError) {
      onTrustMetadataChange({
        periodFrom: activeFilters.fromDate,
        periodTo: activeFilters.toDate,
        requestedPeriodFrom: activeFilters.fromDate,
        requestedPeriodTo: activeFilters.toDate,
        lastRefreshAt: resolvedLastRefreshAt,
        dataFreshnessStatus: refreshStatus?.dataFreshnessStatus ?? "unknown",
        refreshIsRunning: refreshStatus?.isRunning ?? false,
        refreshCurrentStep: refreshStatus?.currentStep ?? null,
        dataSource: "Serverska skorkarta dobavljača",
        dataQualityStatus: "critical",
        recommendationAllowed: false,
        recommendationNote: getSafeAnalyticsErrorMessage(
          error?.message,
          error?.errorCode,
          "Skorkarta dobavljača trenutno nije dostupna.",
        ),
      });
      return;
    }

    if (!trustMetadata) {
      onTrustMetadataChange(null);
      return;
    }

    onTrustMetadataChange({
      periodFrom: requestedPeriodFrom,
      periodTo: requestedPeriodTo,
      requestedPeriodFrom,
      requestedPeriodTo,
      effectivePeriodFrom,
      effectivePeriodTo,
      observedPeriodFrom,
      observedPeriodTo,
      lastRefreshAt: resolvedLastRefreshAt,
      dataFreshnessStatus: refreshStatus?.dataFreshnessStatus ?? "unknown",
      refreshIsRunning: refreshStatus?.isRunning ?? false,
      refreshCurrentStep: refreshStatus?.currentStep ?? null,
      dataSource: `Serverska skorkarta dobavljača (traženo: ${requestedDatasetLabel ?? "nije dostupno"}, efektivno: ${effectiveDatasetLabel ?? trustMetadata?.coverage ?? "nije poznato"}, opseg: ${trustMetadata?.dataScope ?? activeFilters.dataScope ?? "svi"})`,
      provenanceBasis: trustMetadata?.provenanceBasis ?? null,
      dataQualityStatus: trustMetadata?.dataCoverageStatus ?? (trustMetadata?.recommendationAllowed ? "good" : "insufficient_data"),
      dataQualitySummary: {
        missingSupplierCount: trustMetadata?.missingSupplierNameCount ?? null,
        ignoredRowsCount: trustMetadata?.ignoredRowCount ?? null,
      },
      requestedDataset: trustMetadata?.requestedDataset ?? null,
      effectiveDataset: trustMetadata?.effectiveDataset ?? null,
      effectivePeriodLabel: trustMetadata?.effectivePeriodLabel ?? null,
      usedFallback: trustMetadata?.usedFallback ?? false,
      fallbackReason: trustMetadata?.fallbackReason ?? null,
      fallbackReasonCode: trustMetadata?.fallbackReasonCode ?? null,
      recommendationAllowed: trustMetadata?.recommendationAllowed ?? null,
      recommendationNote: recommendationAllowed
        ? "Skorkarta je signalni sloj uz aktivnu finalnu preporuku."
        : "Ovo je analitički signal. Finalna preporuka je u tabu Pregled.",
      emptyStateReason: !loading && sortedRows.length === 0 ? zeroStateExplanation : null,
    });
  }, [
    activeFilters.category,
    activeFilters.dataScope,
    activeFilters.excludeOosBeforeMarkdown,
    activeFilters.fromDate,
    activeFilters.gender,
    activeFilters.minRevenue,
    activeFilters.onlyHighConfidence,
    activeFilters.seasonId,
    activeFilters.toDate,
    embedded,
    error?.message,
    loading,
    onTrustMetadataChange,
    refreshStatus?.currentStep,
    refreshStatus?.dataFreshnessStatus,
    refreshStatus?.isRunning,
    resolvedLastRefreshAt,
    effectivePeriodFrom,
    effectivePeriodTo,
    observedPeriodFrom,
    observedPeriodTo,
    requestedPeriodFrom,
    requestedPeriodTo,
    showBlockingError,
    sortedRows.length,
    summary?.from,
    summary?.to,
    trustMetadata?.coverage,
    trustMetadata?.dataCoverageStatus,
    trustMetadata?.dataScope,
    trustMetadata?.effectiveDataset,
    trustMetadata?.effectiveFrom,
    trustMetadata?.effectivePeriodLabel,
    trustMetadata?.effectiveTo,
    trustMetadata?.fallbackReason,
    trustMetadata?.fallbackReasonCode,
    trustMetadata?.ignoredRowCount,
    trustMetadata?.missingSupplierNameCount,
    trustMetadata?.recommendationAllowed,
    trustMetadata?.requestedDataset,
    trustMetadata?.usedFallback,
    trustMetadata,
    zeroStateExplanation,
  ]);
  const concentrationData = useMemo(() => {
    const top = [...sortedRows]
      .filter((row) => row.sharePct != null)
      .sort((a, b) => (b.sharePct ?? -1) - (a.sharePct ?? -1))
      .slice(0, 8)
      .map((row) => ({ name: row.supplierName, sharePct: row.sharePct ?? 0 }));
    if (top.length === 0) return [];
    const topShare = top.reduce((sum, row) => sum + row.sharePct, 0);
    const rest = clamp(100 - topShare, 0, 100);
    return rest > 0.1 ? [...top, { name: "Ostali", sharePct: rest }] : top;
  }, [sortedRows]);
  const selectedRow = useMemo(() => (expandedSupplierId == null ? null : sortedRows.find((row) => row.supplierId === expandedSupplierId) ?? null), [expandedSupplierId, sortedRows]);
  const snapshotRow = selectedRow ?? sortedRows[0] ?? null;
  const snapshotPeriodLabel = `${formatDate(activeFilters.fromDate)} - ${formatDate(activeFilters.toDate)}`;

  const toolbarFilters = useMemo<AnalyticsNamedValue[]>(() => [
    { key: "periodPreset", label: "Period", value: periodPreset },
    { key: "fromDate", label: "Od", value: activeFilters.fromDate },
    { key: "toDate", label: "Do", value: activeFilters.toDate },
    { key: "category", label: "Kategorija", value: activeFilters.category ?? "" },
    { key: "gender", label: "Pol", value: activeFilters.gender ?? "" },
    { key: "seasonId", label: "Sezona", value: activeFilters.seasonId ?? "" },
    { key: "minRevenue", label: "Min prihod", value: activeFilters.minRevenue ?? "" },
    { key: "onlyHighConfidence", label: "Samo visoka pouzdanost", value: activeFilters.onlyHighConfidence },
    { key: "excludeOosBeforeMarkdown", label: "Isključi artikle bez zaliha pre sniženja", value: activeFilters.excludeOosBeforeMarkdown },
    { key: "supplierId", label: "Dobavljač", value: activeFilters.supplierId ?? "" },
    { key: "storeId", label: "Objekat", value: activeFilters.storeId ?? "" },
    { key: "dataScope", label: "Opseg podataka", value: activeFilters.dataScope ?? "" },
  ], [activeFilters.category, activeFilters.dataScope, activeFilters.excludeOosBeforeMarkdown, activeFilters.fromDate, activeFilters.gender, activeFilters.minRevenue, activeFilters.onlyHighConfidence, activeFilters.seasonId, activeFilters.storeId, activeFilters.supplierId, activeFilters.toDate, periodPreset]);

  const toolbarMetadata = useMemo<AnalyticsNamedValue[]>(() => [
    { key: "summaryFrom", label: "Sažetak od", value: summary?.from ?? "" },
    { key: "summaryTo", label: "Sažetak do", value: summary?.to ?? "" },
    { key: "requestedPeriodFrom", label: "Traženi period od", value: requestedPeriodFrom },
    { key: "requestedPeriodTo", label: "Traženi period do", value: requestedPeriodTo },
    { key: "effectivePeriodFrom", label: "Efektivni period od", value: effectivePeriodFrom ?? "" },
    { key: "effectivePeriodTo", label: "Efektivni period do", value: effectivePeriodTo ?? "" },
    { key: "observedPeriodFrom", label: "Posmatrani period od", value: observedPeriodFrom ?? "" },
    { key: "observedPeriodTo", label: "Posmatrani period do", value: observedPeriodTo ?? "" },
    { key: "supplierCount", label: "Dobavljača", value: summary?.supplierCount ?? null },
    { key: "capitalAtRisk", label: "Kapital u riziku", value: summary?.capitalAtRisk ?? null },
  ], [effectivePeriodFrom, effectivePeriodTo, observedPeriodFrom, observedPeriodTo, requestedPeriodFrom, requestedPeriodTo, summary?.capitalAtRisk, summary?.from, summary?.supplierCount, summary?.to]);

  const resolvedDecisionColumns = useMemo<AnalyticsTableColumn<DecisionRow>[]>(() => (
    decisionColumns.map((column) => (
      column.key === "status"
        ? { ...column, header: recommendationAllowed ? "Signal skorkarte" : "Pomoćni signal" }
        : column
    ))
  ), [recommendationAllowed]);

  const supplierLabel = useMemo(() => {
    if (activeFilters.supplierId == null) {
      return "Svi dobavljači";
    }

    const matched = sortedRows.find((row) => row.supplierId === activeFilters.supplierId);
    return matched?.supplierName ?? `Dobavljač #${activeFilters.supplierId}`;
  }, [activeFilters.supplierId, sortedRows]);

  const reportPayload = useMemo(() => {
    if (!summary || !ranking) {
      return null;
    }

    return buildSupplierDecisionReportPayload({
      periodLabel: periodPreset,
      fromDate: activeFilters.fromDate,
      toDate: activeFilters.toDate,
      supplierLabel,
      dataScopeLabel: activeFilters.dataScope ?? "all",
      category: activeFilters.category,
      gender: activeFilters.gender,
      seasonId: activeFilters.seasonId,
      minRevenue: activeFilters.minRevenue,
      onlyHighConfidence: activeFilters.onlyHighConfidence,
      excludeOosBeforeMarkdown: activeFilters.excludeOosBeforeMarkdown,
      freshnessStatus: refreshStatus?.dataFreshnessStatus ?? "unknown",
      lastRefreshAtUtc: resolvedLastRefreshAt,
      summary,
      trustMetadata,
      scorecardMeta,
      totalRevenue,
      totalMarginContribution,
      top5SharePct,
      supplierCounts,
      rows: sortedRows,
    });
  }, [
    activeFilters.dataScope,
    activeFilters.category,
    activeFilters.excludeOosBeforeMarkdown,
    activeFilters.fromDate,
    activeFilters.gender,
    activeFilters.minRevenue,
    activeFilters.onlyHighConfidence,
    activeFilters.seasonId,
    activeFilters.toDate,
    periodPreset,
    ranking,
    refreshStatus?.dataFreshnessStatus,
    resolvedLastRefreshAt,
    scorecardMeta,
    sortedRows,
    summary,
    supplierCounts,
    supplierLabel,
    top5SharePct,
    totalMarginContribution,
    totalRevenue,
    trustMetadata,
  ]);

  const durableReportHref = useMemo(() => {
    return buildSupplierDecisionReportHref({
      fromDate: activeFilters.fromDate,
      toDate: activeFilters.toDate,
      category: activeFilters.category,
      gender: activeFilters.gender,
      scope: activeFilters.dataScope ?? "all",
      seasonId: activeFilters.seasonId,
      minRevenue: activeFilters.minRevenue,
      onlyHighConfidence: activeFilters.onlyHighConfidence,
      excludeOosBeforeMarkdown: activeFilters.excludeOosBeforeMarkdown,
      supplierId: activeFilters.supplierId,
      storeId: activeFilters.storeId,
    });
  }, [activeFilters.category, activeFilters.dataScope, activeFilters.excludeOosBeforeMarkdown, activeFilters.fromDate, activeFilters.gender, activeFilters.minRevenue, activeFilters.onlyHighConfidence, activeFilters.seasonId, activeFilters.storeId, activeFilters.supplierId, activeFilters.toDate]);

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
        category: category.trim() || null,
        gender: gender || null,
        seasonId,
        minRevenue,
        onlyHighConfidence,
        excludeOosBeforeMarkdown,
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
    setCategory("");
    setGender("");
    setSeasonId(null);
    setMinRevenue(null);
    setOnlyHighConfidence(false);
    setExcludeOosBeforeMarkdown(false);
    setActiveFilters({
      fromDate: sharedFilters?.fromDate ?? range.fromDate,
      toDate: sharedFilters?.toDate ?? range.toDate,
      category: sharedFilters?.category ?? null,
      gender: sharedFilters?.gender ?? null,
      seasonId: null,
      minRevenue: null,
      onlyHighConfidence: false,
      excludeOosBeforeMarkdown: false,
      supplierId: sharedFilters?.supplierId ?? null,
      storeId: sharedFilters?.storeId ?? null,
      dataScope: sharedFilters?.dataScope ?? null,
    });
  };

  const openSupplierDetail = useCallback((row: DecisionRow) => {
    detailAbortRef.current?.abort();
    const controller = new AbortController();
    detailAbortRef.current = controller;
    const detailRequestId = ++detailRequestIdRef.current;

    setDetailSupplierId(row.supplierId);
    setSupplierDetails(null);
    setSupplierDetailError(null);
    setSupplierDetailLoading(true);

    void getSupplierDecisionDetails(
      row.supplierId,
      {
        fromDate: activeFilters.fromDate,
        toDate: activeFilters.toDate,
        category: activeFilters.category ?? undefined,
        gender: activeFilters.gender ?? undefined,
        seasonId: activeFilters.seasonId ?? undefined,
        minRevenue: activeFilters.minRevenue ?? undefined,
        onlyHighConfidence: activeFilters.onlyHighConfidence,
        excludeOosBeforeMarkdown: activeFilters.excludeOosBeforeMarkdown,
        storeId: activeFilters.storeId,
        dataScope: activeFilters.dataScope,
      },
      controller.signal,
    ).then((details) => {
      if (detailRequestId !== detailRequestIdRef.current || controller.signal.aborted) return;
      setSupplierDetails(details);
    }).catch((reason: unknown) => {
      if (detailRequestId !== detailRequestIdRef.current || controller.signal.aborted) return;
      const apiError = reason instanceof SupplierDecisionApiError ? reason : null;
      setSupplierDetailError(getSafeAnalyticsErrorMessage(
        apiError?.message ?? (reason instanceof Error ? reason.message : null),
        apiError?.errorCode,
        "Detalj dobavljača trenutno nije dostupan. Proverite kvalitet podataka i pokušajte ponovo.",
      ));
    }).finally(() => {
      if (detailRequestId === detailRequestIdRef.current && !controller.signal.aborted) {
        setSupplierDetailLoading(false);
      }
    });
  }, [activeFilters.category, activeFilters.dataScope, activeFilters.excludeOosBeforeMarkdown, activeFilters.fromDate, activeFilters.gender, activeFilters.minRevenue, activeFilters.onlyHighConfidence, activeFilters.seasonId, activeFilters.storeId, activeFilters.toDate]);

  const addSupplierSignalToQueue = useCallback(async (row: DecisionRow) => {
    if (recommendationAllowed !== true) {
      setQueueMessage("Konačna preporuka nije dozvoljena za ovaj signal. Proverite kvalitet podataka pre bilo kakve akcije.");
      return;
    }

    const sourceKey = buildSupplierActionSourceKey(row, activeFilters, recommendationAllowed);
    const alreadyQueued = queuedActionKeys.has(sourceKey);
    setQueueBusyKey(sourceKey);
    setQueueMessage(null);

    const nextRecommendationStatus = recommendationAllowed ? row.status : "SIGNAL_REVIEW";
    const title = recommendationAllowed
      ? `Pripremi razgovor sa dobavljačem: ${row.supplierName}`
      : `Proveri signal dobavljača: ${row.supplierName}`;
    const provenanceDescription = `Traženi period: ${formatSupplierPeriodRange(requestedPeriodFrom, requestedPeriodTo)}. Efektivni period: ${trustMetadata?.effectivePeriodLabel ?? formatSupplierPeriodRange(effectivePeriodFrom, effectivePeriodTo)}. Posmatrani podaci: ${formatSupplierPeriodRange(observedPeriodFrom, observedPeriodTo)}.`;
    const description = `${recommendationAllowed ? row.statusReason : "Finalna preporuka nije dozvoljena za traženi period; akcija je signalnog karaktera i zahteva proveru."} ${provenanceDescription}`;

    try {
      const action = await upsertAnalyticsAction({
        sourceType: "supplier",
        sourceKey,
        sourceId: row.supplierId,
        title,
        description,
        recommendationStatus: nextRecommendationStatus,
        priority: mapSupplierActionPriority(row, recommendationAllowed),
        impactEstimateRsd: Number.isFinite(row.unsoldStockValue) && row.unsoldStockValue > 0 ? row.unsoldStockValue : undefined,
        confidencePct: row.confidenceAvailable && row.normalizedConfidence != null ? Math.round(row.normalizedConfidence) : undefined,
        reliabilityPct: row.reliabilityAvailable && row.reliabilityPct != null ? Math.round(row.reliabilityPct) : undefined,
        dataQualityStatus: toActionDataQualityStatus(row.dataQualityStatus),
        actionUrl: buildSupplierDecisionScorecardHref({
          supplierId: row.supplierId,
          fromDate: activeFilters.fromDate,
          toDate: activeFilters.toDate,
          category: activeFilters.category,
          gender: activeFilters.gender,
          seasonId: activeFilters.seasonId,
          minRevenue: activeFilters.minRevenue,
          onlyHighConfidence: activeFilters.onlyHighConfidence,
          excludeOosBeforeMarkdown: activeFilters.excludeOosBeforeMarkdown,
          storeId: activeFilters.storeId,
          dataScope: activeFilters.dataScope,
        }),
        metadataJson: JSON.stringify({
          supplierId: row.supplierId,
          supplierName: row.supplierName,
          actionKind: recommendationAllowed ? "negotiation" : "signal_check",
          scorecardStatus: row.status,
          reasonCodes: row.reasonCodes,
          periodFrom: activeFilters.fromDate,
          periodTo: activeFilters.toDate,
          requestedPeriodFrom,
          requestedPeriodTo,
          effectivePeriodFrom,
          effectivePeriodTo,
          observedPeriodFrom,
          observedPeriodTo,
          effectivePeriodLabel: trustMetadata?.effectivePeriodLabel ?? null,
          requestedDataset: trustMetadata?.requestedDataset ?? null,
          effectiveDataset: trustMetadata?.effectiveDataset ?? null,
          usedFallback: trustMetadata?.usedFallback ?? false,
          fallbackReason: trustMetadata?.fallbackReason ?? null,
          fallbackReasonCode: trustMetadata?.fallbackReasonCode ?? null,
          dataCoverageStatus: trustMetadata?.dataCoverageStatus ?? null,
          provenanceBasis: trustMetadata?.provenanceBasis ?? null,
          category: activeFilters.category ?? "all",
          gender: activeFilters.gender ?? "all",
          seasonId: activeFilters.seasonId ?? "all",
          minRevenue: activeFilters.minRevenue ?? "all",
          onlyHighConfidence: activeFilters.onlyHighConfidence,
          excludeOosBeforeMarkdown: activeFilters.excludeOosBeforeMarkdown,
          storeId: activeFilters.storeId ?? "all",
          dataScope: activeFilters.dataScope ?? "all",
          recommendationAllowed,
        }),
      });

      setQueuedActionKeys((prev) => {
        const next = new Set(prev);
        next.add(sourceKey);
        if (action.sourceKey) next.add(action.sourceKey);
        return next;
      });
      setQueueMessage(alreadyQueued ? "Akcija je već u centralnom redu." : "Akcija dodata u centralni red.");
    } catch (reason) {
      setQueueMessage(getAnalyticsActionWriteErrorMessage(reason));
    } finally {
      setQueueBusyKey(null);
    }
  }, [activeFilters, effectivePeriodFrom, effectivePeriodTo, observedPeriodFrom, observedPeriodTo, queuedActionKeys, recommendationAllowed, requestedPeriodFrom, requestedPeriodTo, trustMetadata]);

  return (
    <div
      className={`sdh-decision-page ${embedded ? "sdh-decision-page--embedded" : ""}`}
      role={embedded ? "region" : undefined}
      aria-label={embedded ? "Skorkarta dobavljača" : undefined}
    >
      {!embedded ? (
      <header className="sdh-decision-header">
        <div>
          <details className="sdh-decision-help">
            <summary>Kako se čita ovaj ekran?</summary>
            <div className="sdh-decision-help-content">
              <p><strong>Šta prikazuje:</strong> Skorkarta ne meri sav promet dobavljača. Ona meri dobavljače kroz skup skorkarte: artikle koji imaju prvu nivelaciju u izabranom periodu, uz prodaju pre/posle, maržu, zalihu i pouzdanost signala.</p>
              <p><strong>Kako se tumači:</strong> Viši prihod i marža su dobri, ali samo ako ne dolaze uz preveliku zavisnost od sniženja i neaktivnu zalihu. Niske ili prazne vrednosti mogu značiti da u periodu nema dovoljno signala skorkarte, ne nužno da dobavljač nema promet.</p>
              <p><strong>Važno:</strong> Tab „Pregled” koristi širi prodajni skup. Zato Pregled može imati promet dok je Skorkarta prazna ili niža, posebno za kratke periode bez novih nivelacija.</p>
              <p><strong>Šta znače kolone:</strong></p>
              <ul>
                <li><strong>Prihod:</strong> Ukupna vrednost prodaje dobavljača u periodu (samo artikli sa nivelacijom).</li>
                <li><strong>Udeo:</strong> Koliki deo ukupnog prihoda dolazi od tog dobavljača.</li>
                <li><strong>Marža:</strong> Razlika između prodajne i nabavne cene kao procenat.</li>
                <li><strong>Trend pune cene:</strong> Pozitivan = veći udeo prodaje po punoj ceni od udela nivelacija; negativan = veća zavisnost od sniženja.</li>
                <li><strong>Signal skorkarte:</strong> Pojačaj, Zadrži, Oprez, Smanji / Ne veruj ili Nedovoljno podataka. Ovo nije konačna preporuka.</li>
              </ul>
              <p><strong>Zašto nema podataka?</strong> Najčešći razlozi: nema nivelacija u izabranom periodu, filteri su uski (kratak period ili specifična prodavnica), dobavljači nisu pravilno povezani sa artiklima, ili analitika nije osvežena (pokreni u Konfiguracija → Radnici).</p>
              <p><strong>Kako koristiti:</strong> Uporedi 30, 90 i 180 dana. Kraći period pokazuje svež signal, a duži stabilniju sliku. Grafikon pokazuje koncentraciju prihoda, a tabela objašnjava akciju po dobavljaču.</p>
            </div>
          </details>
        </div>
      </header>
      ) : null}

      {!embedded ? (
        <AnalyticsTrustHeader
          title="Skorkarta dobavljača — pomoćni signal"
          description="Skorkarta poredi dobavljače po signalu skorkarte. Koristi se za proveru i objašnjenje, dok je konačna poslovna preporuka u tabu Pregled."
          periodFrom={requestedPeriodFrom}
          periodTo={requestedPeriodTo}
          requestedPeriodFrom={requestedPeriodFrom}
          requestedPeriodTo={requestedPeriodTo}
          effectivePeriodFrom={effectivePeriodFrom}
          effectivePeriodTo={effectivePeriodTo}
          observedPeriodFrom={observedPeriodFrom}
          observedPeriodTo={observedPeriodTo}
          lastRefreshAt={resolvedLastRefreshAt}
          dataFreshnessStatus={refreshStatus?.dataFreshnessStatus ?? "unknown"}
          refreshIsRunning={refreshStatus?.isRunning ?? false}
          refreshCurrentStep={refreshStatus?.currentStep ?? null}
          dataSource="Materijalizovani prikaz skorkarte dobavljača"
          provenanceBasis={trustMetadata?.provenanceBasis ?? null}
          dataQualityStatus={trustMetadata?.dataCoverageStatus ?? (trustMetadata?.recommendationAllowed ? "good" : "insufficient_data")}
          dataQualitySummary={{
            missingSupplierCount: trustMetadata?.missingSupplierNameCount ?? null,
            ignoredRowsCount: trustMetadata?.ignoredRowCount ?? null,
          }}
          requestedDataset={trustMetadata?.requestedDataset ?? null}
          effectiveDataset={trustMetadata?.effectiveDataset ?? null}
          effectivePeriodLabel={trustMetadata?.effectivePeriodLabel ?? null}
          usedFallback={trustMetadata?.usedFallback ?? false}
          fallbackReason={trustMetadata?.fallbackReason ?? null}
          fallbackReasonCode={trustMetadata?.fallbackReasonCode ?? null}
          recommendationAllowed={trustMetadata?.recommendationAllowed ?? null}
          mode={recommendationAllowed ? "recommendation" : "signal"}
          isPartial={showMetaWarning}
          recommendationNote={recommendationAllowed
            ? "Skorkarta je signalni sloj uz aktivnu konačnu preporuku."
            : "Ovo je analitički signal. Konačna preporuka je u tabu Pregled."}
          emptyStateReason={!loading && !showBlockingError && sortedRows.length === 0 ? zeroStateExplanation : null}
          methodologyHref="/analytics/data-quality"
          dataQualityHref="/analytics/data-quality"
          refreshStatusHref="/admin/configuration?panel=workers"
          compact
        />
      ) : null}

      {snapshotRow ? (
        <section className="sdh-decision-snapshot">
          <SupplierExplainabilitySnapshot
            compact
            title="Sažetak objašnjenja signala"
            subjectLabel={snapshotRow.supplierName}
            recommendationCode={snapshotRow.recommendationCode}
            periodLabel={snapshotPeriodLabel}
            requestedPeriodFrom={requestedPeriodFrom}
            requestedPeriodTo={requestedPeriodTo}
            effectivePeriodFrom={effectivePeriodFrom}
            effectivePeriodTo={effectivePeriodTo}
            observedPeriodFrom={observedPeriodFrom}
            observedPeriodTo={observedPeriodTo}
            lastRefreshAt={trustMetadata?.lastRefreshAtUtc ?? resolvedLastRefreshAt}
            requestedDataset={trustMetadata?.requestedDataset ?? null}
            effectiveDataset={trustMetadata?.effectiveDataset ?? null}
            effectivePeriodLabel={trustMetadata?.effectivePeriodLabel ?? null}
            provenanceBasis={trustMetadata?.provenanceBasis ?? null}
            dataQualityStatus={trustMetadata?.dataCoverageStatus ?? (trustMetadata?.recommendationAllowed ? "good" : "insufficient_data")}
            recommendationAllowed={trustMetadata?.recommendationAllowed ?? null}
            usedFallback={trustMetadata?.usedFallback ?? false}
            fallbackReason={trustMetadata?.fallbackReason ?? null}
            fallbackReasonCode={trustMetadata?.fallbackReasonCode ?? null}
            confidencePct={snapshotRow.confidenceAvailable ? snapshotRow.normalizedConfidence : null}
            reliabilityPct={snapshotRow.reliabilityAvailable ? snapshotRow.reliabilityPct : null}
            reasonCodes={snapshotRow.reasonCodes}
            note={recommendationAllowed
              ? "Kompaktni sažetak prikazuje serverski signal iz skupa skorkarte."
              : "Kompaktni sažetak prikazuje pomoćni signal jer je konačna preporuka blokirana."}
          />
        </section>
      ) : null}

      <section className="sdh-decision-context" aria-label="Objašnjenje skorkarte">
        <div>
          <strong>Kako čitati skorkartu dobavljača?</strong>
          <span>Skorkarta poredi dobavljače po prometu, maržnom doprinosu, zavisnosti od nivelacija, riziku zaliha i pouzdanosti signala. Ovo je pomoćni signal; konačna preporuka je u tabu Pregled.</span>
        </div>
        <div>
          <strong>Šta meri Skorkarta?</strong>
          <span>Skup skorkarte dobavljača: artikli sa prvom nivelacijom u izabranom periodu, uz prihod, maržu, punu cenu, zalihu i pouzdanost signala.</span>
        </div>
        <div>
          <strong>Kako čitati niske vrednosti?</strong>
          <span>Niska ili prazna skorkarta ne znači automatski da dobavljač nema promet; može značiti da u periodu nema dovoljno nivelacija za procenu.</span>
        </div>
        <div>
          <strong>Odnos sa tabom Pregled</strong>
          <span>Pregled je glavni ekran za konačnu preporuku. Skorkarta ovde služi kao dodatni signal za proveru.</span>
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
            <InfoTip text="Početak perioda skorkarte. Uključuju se dobavljači čiji artikli imaju prvu nivelaciju od ovog datuma." />
          </span>
          <input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
        </label>
        <label className="sdh-decision-field">
          <span>
            Do
            <InfoTip text="Kraj perioda skorkarte. Analiza uključuje signale do kraja ovog dana." />
          </span>
          <input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
        </label>
        <label className="sdh-decision-field">
          <span>
            Kategorija
            <InfoTip text="Ograniči skorkartu na izabranu kategoriju." />
          </span>
          <input type="text" value={category} onChange={(e) => setCategory(e.target.value)} placeholder="npr. Patike" />
        </label>
        <label className="sdh-decision-field">
          <span>
            Pol
            <InfoTip text="Ograniči skorkartu na izabrani pol." />
          </span>
          <select value={gender} onChange={(e) => setGender(e.target.value)}>
            <option value="">Svi polovi</option>
            <option value="Žensko">Žensko</option>
            <option value="Muško">Muško</option>
            <option value="Unisex">Unisex</option>
            <option value="Dečije">Dečije</option>
          </select>
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
        <label className="sdh-decision-field check">
          <span>
            Isključi artikle bez zaliha pre sniženja
            <InfoTip text="Izostavi artikle koji su bili bez zaliha pre sniženja iz skorkarte." />
          </span>
          <input type="checkbox" checked={excludeOosBeforeMarkdown} onChange={(e) => setExcludeOosBeforeMarkdown(e.target.checked)} />
        </label>
        <div className="sdh-decision-actions">
          <button type="button" onClick={handleApplyFilters} disabled={loading || invalidRange}>Primeni</button>
          <button type="button" className="secondary" onClick={handleResetFilters} disabled={loading}>Poništi filtere</button>
        </div>
      </section>
      ) : null}

      {invalidRange ? <div className="sdh-decision-message error" role="alert">Datum 'od' ne može biti posle datuma 'do'.</div> : null}
      {showBlockingError ? (
        <AnalyticsErrorState
          title="Podaci trenutno nisu dostupni"
          message={error?.message ?? scorecardMetaMessage ?? "Ne prikazujemo nule jer nije potvrđeno da je period stvarno prazan."}
          errorCode={error?.errorCode ?? undefined}
          correlationId={error?.correlationId ?? undefined}
          onRetry={() => {
            void load(activeFilters);
          }}
          helpHref="/analytics/data-quality"
        />
      ) : null}

      {staleWarning ? (
        <div className="sdh-decision-message warning" role="note">{staleWarning}</div>
      ) : null}

      {showMetaWarning ? (
        <div className="sdh-decision-message warning" role="note">
          Prikazani podaci su delimični ili potiču iz pomoćnog skupa. {scorecardMetaMessage ?? "Proverite status osvežavanja analitike."}
        </div>
      ) : null}

      {queueMessage ? (
        <div className="sdh-decision-message info" role="status">{queueMessage}</div>
      ) : null}

      {!loading && !showBlockingError && hasDatasetFallback ? (
        <div className="sdh-decision-message warning" role="note">
          Prikazan je pomoćni skup podataka: {effectivePeriodLabel ?? effectiveDatasetLabel ?? "nije dostupno"}. Konačna preporuka je blokirana.
          {fallbackReasonText ? ` ${fallbackReasonText}` : ""}
        </div>
      ) : null}

      {!loading && !showBlockingError && sortedRows.length === 0 ? (
        <AnalyticsEmptyState
          variant={emptyStateVariant}
          message={
            emptyStateVariant === "insufficient_data"
              ? "Ne prikazujemo automatsku preporuku jer signal nije dovoljno jak."
              : emptyStateVariant === "filtered_out"
                ? "Promenite filtere ili proširite period."
                : (scorecardMetaMessage ?? "Nije bilo prodaje u izabranom periodu.")
          }
          reasons={[
            "U traženom periodu nema prodaje ili signala skorkarte.",
            "Filteri su suzili skup dobavljača na prazan rezultat.",
            "Dobavljači nisu povezani, osvežavanje je u toku ili je period previše uzak.",
          ]}
          actions={[
            { label: "Proširite period na 90d ili 180d." },
            { label: "Uklonite uske filtere (objekat/dobavljač)." },
            { label: "Otvorite kvalitet podataka radi provere blokera.", href: "/analytics/data-quality" },
          ]}
          dataQualityHref="/analytics/data-quality"
          refreshStatusHref="/admin/configuration?panel=workers"
          emptyReason={zeroStateExplanation}
          onRetry={() => {
            void load(activeFilters);
          }}
        />
      ) : null}
      {loading ? <div className="sdh-decision-message loading" role="status" aria-live="polite">Učitavam skorkarte dobavljača...</div> : null}

      {!loading && summary && ranking ? (
        <>
          {(trustMetadata?.dataNote ?? summary.dataNote ?? ranking.dataNote) ? (
            <div className="sdh-decision-message info" role="note">
            <strong>Obuhvat podataka:</strong> {trustMetadata?.dataNote ?? summary.dataNote ?? ranking.dataNote}
            </div>
          ) : null}
          <section className="sdh-decision-kpis">
            <article className="sdh-decision-kpi">
              <span>
                Ukupan prihod
                <InfoTip text="Zbir prihoda za sve učitane dobavljače u skorkarti. Osnova su artikli sa prvom nivelacijom u periodu, pa se može razlikovati od ukupnog prometa u tabu Pregled." />
              </span>
              <strong>{formatMetricDisplayValue({ value: totalRevenue, kind: "currency" })}</strong>
              <KpiExplainButton metricKey="revenue" ariaLabel="Kako je izračunat ukupan prihod" />
            </article>
            <article className="sdh-decision-kpi">
              <span>
                Udeo top 5 dobavljača
                <InfoTip text="Udeo prihoda koji donosi pet najvećih dobavljača u skupu skorkarte. Veća vrednost znači veću koncentraciju i veći rizik oslanjanja na nekoliko partnera." />
              </span>
              <strong>{formatMetricDisplayValue({ value: top5SharePct, kind: "percent" })}</strong>
              <KpiExplainButton metricKey="topSupplierRevenueShare" />
            </article>
            <article className="sdh-decision-kpi">
              <span>
                Ukupan maržni doprinos
                <InfoTip text="Procena maržnog doprinosa: prihod od prodaje po punoj ceni ponderisan pre-markdown maržom. Viša vrednost je bolja, ali proveri je zajedno sa rizikom zaliha." />
              </span>
              <strong>{formatMetricDisplayValue({ value: totalMarginContribution, kind: "currency" })}</strong>
              <KpiExplainButton metricKey="marginContribution" ariaLabel="Kako je izračunat ukupan maržni doprinos" />
            </article>
            <article className="sdh-decision-kpi">
              <span>
                Kapital u riziku
                <InfoTip text="Procena vrednosti neprodate ili sporo rotirajuće zalihe kod prikazanih dobavljača. Niža vrednost je bolja; visoka vrednost traži proveru nabavke i zaliha." />
              </span>
              <strong className="trend-down">{formatMetricDisplayValue({ value: summary.capitalAtRisk ?? null, kind: "currency" })}</strong>
              <KpiExplainButton metricKey="stockAtRisk" ariaLabel="Kako je izračunat lager u riziku" />
            </article>
            <article className="sdh-decision-kpi">
              <span>
                Promena udela pune cene
                <InfoTip text="Razlika u udelu prodaje po punoj ceni u odnosu na prethodni isti period. Pozitivno znači zdraviji signal; negativno znači veću zavisnost od sniženja." />
              </span>
              <strong className={trendClass(fullPriceDeltaPctPoints)}>{fmtSignedPct(fullPriceDeltaPctPoints)}</strong>
              <KpiExplainButton metricKey="fullPriceShareChange" ariaLabel="Kako je izračunata promena udela pune cene" />
            </article>
          </section>

          <section className="sdh-decision-panels">
            <article className="sdh-decision-card">
              <h2>Koncentracija prihoda</h2><p>Grafikon pokazuje koliko prihoda u skupu skorkarte nose najveći dobavljači. Visoka koncentracija znači da promena uslova ili kvaliteta kod jednog dobavljača može jače uticati na rezultat.</p>
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
                        formatter={(value: number | string | undefined) => [formatMetricDisplayValue({ value: typeof value === "number" ? value : Number(value), kind: "percent", digits: 2 }), "Udeo prihoda"]}
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
                  {recommendationAllowed ? (
                    <p>Pojačaj: <strong>{supplierCounts.boost}</strong> | Zadrži: <strong>{supplierCounts.keep}</strong> | Oprez: <strong>{supplierCounts.caution}</strong> | Smanji / Ne veruj: <strong>{supplierCounts.reduce}</strong> | Nedovoljno podataka: <strong>{supplierCounts.insufficient}</strong></p>
                  ) : (
                    <p>Pomoćni signal: <strong>{supplierCounts.insufficient}</strong> | Konačna preporuka je u tabu Pregled.</p>
                  )}
                  <p className="sdh-decision-table-subtitle">Lista koristi serverski signal skorkarte i serverske vrednosti sigurnosti/pouzdanosti, bez lokalnog izračunavanja konačnog statusa.</p>
                </div>
              </div>
              <AnalyticsDataTable
                testId="supplier-decision-hub-data-table"
                rowCount={sortedRows.length}
                truncationLabel={ranking.totalCount > sortedRows.length
                  ? `Ukupno u rezultatu: ${ranking.totalCount.toLocaleString("sr-RS")} dobavljača`
                  : undefined}
                toolbar={(
                  <AnalyticsTableToolbar
                    tableKey="supplier-decision-hub"
                    tableTitle="Skorkarta dobavljača - kompaktni prikaz"
                    columns={resolvedDecisionColumns}
                    rows={sortedRows}
                    filters={toolbarFilters}
                    metadata={toolbarMetadata}
                    defaultOrientation="landscape"
                    extraActions={<SupplierDecisionReportActions payload={reportPayload} durableReportHref={durableReportHref} disabled={loading || showBlockingError || !summary || !ranking} />}
                  />
                )}
              >
                <table className="min-w-full text-sm">
                  <thead>
                    <tr>
                      <th>
                        <button type="button" onClick={() => handleSort("supplierName")}>
                          Dobavljač
                          <InfoTip text="Naziv dobavljača. Prazni nazivi se normalizuju na 'Dobavljač #ID' ili 'Nepoznat dobavljač' da tabela nema blank redove." />
                          {sortMarker("supplierName", sortField, sortDir)}
                        </button>
                      </th>
                      <th className="analytics-data-table__numeric">
                        <button type="button" onClick={() => handleSort("revenue")}>
                          Prihod
                          <InfoTip text="Prihod dobavljača u skupu skorkarte za izabrani period." />
                          {sortMarker("revenue", sortField, sortDir)}
                        </button>
                      </th>
                      <th className="analytics-data-table__numeric">
                        <button type="button" onClick={() => handleSort("sharePct")}>
                          Udeo %
                          <InfoTip text="Udeo ovog dobavljača u ukupnom prihodu skorkarte. Veći udeo znači veći uticaj na ukupne pokazatelje." />
                          {sortMarker("sharePct", sortField, sortDir)}
                        </button>
                      </th>
                      <th className="analytics-data-table__numeric">
                        <button type="button" onClick={() => handleSort("preMarkdownMarginPct")}>
                          Marža %
                          <InfoTip text="Marža pre prvog sniženja: procenat zarade pre nivelacije. Viša marža je bolji signal, osim ako dolazi uz visok rizik zaliha." />
                          {sortMarker("preMarkdownMarginPct", sortField, sortDir)}
                        </button>
                      </th>
                      <th className="analytics-data-table__numeric">
                        <button type="button" onClick={() => handleSort("qualityTrendPct")}>
                          Trend pune cene %
                          <InfoTip text="Udeo pune cene minus udeo nivelacija. Pozitivno znači zdraviju prodaju; negativno znači veću zavisnost od sniženja." />
                          {sortMarker("qualityTrendPct", sortField, sortDir)}
                        </button>
                      </th>
                      <th>
                        <button type="button" onClick={() => handleSort("status")}>
                          {recommendationAllowed ? "Signal skorkarte" : "Pomoćni signal"}
                          <InfoTip text={recommendationAllowed
                            ? "Dodatni signal skorkarte (Pojačaj, Zadrži, Oprez, Smanji / Ne veruj, Nedovoljno podataka). Nije konačna preporuka."
                            : "Pomoćni signal zbog pomoćnog skupa/nedovoljnog uzorka. Konačna preporuka je u tabu Pregled."}
                          />
                          {sortMarker("status", sortField, sortDir)}
                        </button>
                      </th>
                      <th className="text-center">Detalj</th>
                    </tr>
                  </thead>
                  <tbody>
                    {sortedRows.length === 0 ? (
                      <tr>
                        <td colSpan={7} className="py-12 text-center text-secondary">
                          <div>
                            <p>Nema pronađenih dobavljača za izabrane filtere.</p>
                            <p className="sdh-decision-table-helper">Ako Pregled ima promet, proširi period ili ukloni uske filtere. Skorkarta koristi uži skup skorkarte zasnovan na prvim nivelacijama.</p>
                          </div>
                        </td>
                      </tr>
                    ) : (
                      sortedRows.map((row) => {
                        const expanded = expandedSupplierId === row.supplierId;
                        const displayedStatusLabel = recommendationAllowed
                          ? statusDisplayLabel(row.status)
                          : "Pomoćni signal";
                        return (
                          <tr key={row.supplierId} className={`analytics-data-table__row--interactive ${expanded ? "expanded-row" : ""}`}>
                            <td className="font-semibold text-contrast">{row.supplierName}</td>
                            <td className="analytics-data-table__numeric text-secondary">{fmtRsd(row.revenue)}</td>
                            <td className="analytics-data-table__numeric text-secondary">{formatMetricDisplayValue({ value: row.sharePct, kind: "percent", digits: 2 })}</td>
                            <td className="analytics-data-table__numeric text-secondary">{fmtPct(toSupplierDecisionMarginPercentUnits(row.preMarkdownMarginPct), 2)}</td>
                            <td className={`analytics-data-table__numeric text-secondary ${trendClass(row.qualityTrendPct)}`}>{fmtSignedPct(row.qualityTrendPct, 2)}</td>
                            <td>
                              <div className="sdh-decision-status-stack">
                                <span className={statusClass(row.status)} title={buildStatusTooltip(row)} aria-label={buildStatusTooltip(row)}>{displayedStatusLabel}</span>
                                {row.statusReason ? (
                                  <span className="sdh-decision-status-reason" title={row.statusReason}>
                                    <strong>Razlog:</strong> {row.statusReason}
                                  </span>
                                ) : null}
                              </div>
                            </td>
                            <td className="text-center"><button type="button" className="sdh-decision-detail-btn" onClick={() => setExpandedSupplierId(expanded ? null : row.supplierId)}>{expanded ? "Sakrij" : "Detalji"}</button></td>
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
            <section className="sdh-decision-detail">
              <div className="sdh-decision-detail-head">
                <h3>Detalj signala skorkarte: {selectedRow.supplierName}</h3>
                <div className="inline-flex items-center gap-2">
                  <button type="button" onClick={() => openSupplierDetail(selectedRow)}>Otvori puni detalj</button>
                  {recommendationAllowed === true ? (
                    <button
                      type="button"
                      onClick={() => void addSupplierSignalToQueue(selectedRow)}
                      disabled={queueBusyKey === buildSupplierActionSourceKey(selectedRow, activeFilters, recommendationAllowed) || queuedActionKeys.has(buildSupplierActionSourceKey(selectedRow, activeFilters, recommendationAllowed))}
                    >
                      {queueBusyKey === buildSupplierActionSourceKey(selectedRow, activeFilters, recommendationAllowed)
                        ? "Dodavanje..."
                        : queuedActionKeys.has(buildSupplierActionSourceKey(selectedRow, activeFilters, recommendationAllowed))
                          ? "U akcijama"
                          : "Dodaj u akcije"}
                    </button>
                  ) : (
                    <p className="sdh-decision-reason" role="note">
                      Akcija nije dostupna: konačna preporuka nije dozvoljena. <Link to="/analytics/data-quality">Proveri kvalitet podataka</Link>
                    </p>
                  )}
                </div>
              </div>
              <SupplierExplainabilitySnapshot
                subjectLabel={selectedRow.supplierName}
                recommendationCode={selectedRow.recommendationCode}
                periodLabel={snapshotPeriodLabel}
                requestedPeriodFrom={requestedPeriodFrom}
                requestedPeriodTo={requestedPeriodTo}
                effectivePeriodFrom={effectivePeriodFrom}
                effectivePeriodTo={effectivePeriodTo}
                observedPeriodFrom={observedPeriodFrom}
                observedPeriodTo={observedPeriodTo}
                lastRefreshAt={trustMetadata?.lastRefreshAtUtc ?? resolvedLastRefreshAt}
                requestedDataset={trustMetadata?.requestedDataset ?? null}
                effectiveDataset={trustMetadata?.effectiveDataset ?? null}
                effectivePeriodLabel={trustMetadata?.effectivePeriodLabel ?? null}
                dataQualityStatus={selectedRow.dataQualityStatus}
                recommendationAllowed={recommendationAllowed}
                usedFallback={trustMetadata?.usedFallback ?? false}
                fallbackReason={trustMetadata?.fallbackReason ?? null}
                fallbackReasonCode={trustMetadata?.fallbackReasonCode ?? null}
                confidencePct={selectedRow.confidenceAvailable ? selectedRow.normalizedConfidence : null}
                reliabilityPct={selectedRow.reliabilityAvailable ? selectedRow.reliabilityPct : null}
                reasonCodes={selectedRow.reasonCodes}
                note={selectedRow.statusReason}
              />
              <div className="sdh-decision-detail-grid">
                <article>
                  <span>Prihod <InfoTip text="Ukupna vrednost prodaje ovog dobavljača u izabranom periodu." /></span>
                  <strong>{fmtRsd(selectedRow.revenue)}</strong>
                </article>
                <article>
                  <span>Komadi <InfoTip text="Koliko artikala je prodato ovog dobavljača u periodu." /></span>
                  <strong>{fmtSupplierUnits(selectedRow.units)}</strong>
                </article>
                <article>
                  <span>Udeo pune cene <InfoTip text="Koliki deo prihoda dolazi od prodaje po punoj ceni (bez sniženja)." /></span>
                  <strong>{fmtPct(toSupplierDecisionMarginPercentUnits(selectedRow.fullPriceRevenueShare), 2)}</strong>
                </article>
                <article>
                  <span>Udeo nivelacija <InfoTip text="Koliki deo prihoda od ovog dobavljača dolazi od prodaje sa sniženjima (nivelacijama). Viši procenat može ukazivati da je asortiman precenjen ili da potražnja slabi." /></span>
                  <strong>{fmtPct(toSupplierDecisionMarginPercentUnits(selectedRow.markdownRevenueShare), 2)}</strong>
                  <KpiExplainButton metricKey="markdownDependency" ariaLabel="Kako je izračunata zavisnost od nivelacija" />
                </article>
                <article>
                  <span>Stopa neaktivnih artikala <InfoTip text="Koliki deo artikala ovog dobavljača leži na zalihi bez prodaje. Viša stopa znači prekomerne narudžbine u odnosu na potražnju — rizik za kapital i skladište." /></span>
                  <strong>{fmtPct(toSupplierDecisionMarginPercentUnits(selectedRow.deadStockRate), 2)}</strong>
                </article>
                <article>
                  <span>Vrednost neprodate zalihe <InfoTip text="Procenjena vrednost artikala koji su na zalihi a se nisu prodali. To je kapital koji nije obrnut." /></span>
                  <strong>{fmtRsd(selectedRow.unsoldStockValue)}</strong>
                </article>
                <article>
                  <span>Stopa dobrih artikala <InfoTip text="Procenat artikala dobavljača koji se redovno dobro prodaju — malo neaktivne zalihe, dobra marža, pozitivan trend. Viši procenat = pouzdaniji i predvidiviji asortiman." /></span>
                  <strong>{fmtPct(toSupplierDecisionMarginPercentUnits(selectedRow.repeatWinnerRate), 2)}</strong>
                </article>
                <article>
                  <span>Skor / indeks kvaliteta <InfoTip text="Dva pokazatelja: levi (0–100) je automatski skor na osnovu prodajnih signala, desni je indeks pouzdanosti asortimana. Viši skor = bolji učinak. Korisno za poređenje dobavljača između sebe." /></span>
                  <strong>{fmtNumber(selectedRow.mlSupplierScore, 1, RECOMMENDATION_SIGNAL_UNAVAILABLE)} / {fmtNumber(selectedRow.supplierQualityIndex, 1, RECOMMENDATION_SIGNAL_UNAVAILABLE)}</strong>
                </article>
                <article>
                  <span>Sigurnost signala <InfoTip text="Serverski signal sigurnosti za signal skorkarte. Ovo nije isto što i lokalni heuristički skor." /></span>
                  <strong>{selectedRow.confidenceAvailable ? formatMetricDisplayValue({ value: selectedRow.normalizedConfidence, kind: "percent", digits: 1 }) : RECOMMENDATION_SIGNAL_UNAVAILABLE}</strong>
                  <KpiExplainButton metricKey="confidencePct" ariaLabel="Kako je izračunata sigurnost preporuke" />
                </article>
                <article>
                  <span>Pouzdanost signala</span>
                  <strong>{selectedRow.reliabilityAvailable ? formatMetricDisplayValue({ value: selectedRow.reliabilityPct, kind: "percent", digits: 1 }) : RECOMMENDATION_SIGNAL_UNAVAILABLE}</strong>
                  <KpiExplainButton metricKey="reliabilityPct" ariaLabel="Kako je izračunata pouzdanost signala" />
                </article>
                <article>
                  <span>Status kvaliteta signala</span>
                  <strong style={recommendationQualityStyle(selectedRow.dataQualityStatus)}>{supplierDecisionQualityLabel(selectedRow.dataQualityStatus)}</strong>
                </article>
              </div>
              <p className="sdh-decision-reason">
                <strong>Razlog signala skorkarte:</strong> {selectedRow.statusReason}
              </p>
              {selectedRow.reasonCodes.length > 0 ? (
                <p className="sdh-decision-reason">
                  <strong>Razlozi:</strong> {selectedRow.reasonCodes.map(recommendationReasonLabel).join(" | ")}
                </p>
              ) : null}
              {recommendationReasonHints(selectedRow.reasonCodes).map((hint) => (
                <p key={hint} className="sdh-decision-reason">
                  <strong>Napomena:</strong> {hint}
                </p>
              ))}
              {(!selectedRow.reliabilityAvailable || selectedRow.dataQualityStatus !== "good") ? (
                <p className="sdh-decision-reason">
                  <strong>Kvalitet podataka:</strong> Otvori <Link to="/analytics/data-quality">Kvalitet podataka</Link> da proveriš popravljive probleme.
                </p>
              ) : null}
            </section>
          ) : null}
        </>
      ) : null}
      <SupplierDetailDrawer
        open={detailSupplierId !== null}
        loading={supplierDetailLoading}
        error={supplierDetailError}
        details={supplierDetails}
        onClose={closeSupplierDetail}
      />
    </div>
  );
}



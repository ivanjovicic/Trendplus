import { Fragment, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { parseEntityIdParam } from "../validation/entityId";
import { Link } from "react-router-dom";
import AnalyticsEmptyState from "../components/analytics/AnalyticsEmptyState";
import AnalyticsErrorState from "../components/analytics/AnalyticsErrorState";
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import AnalyticsTrustHeader from "../components/analytics/AnalyticsTrustHeader";
import KpiExplainButton from "../components/analytics/KpiExplainButton";
import InfoTip from "../components/ui/InfoTip";
import {
  AnalyticsMetaError,
  getAnalyticsActionSourceStatuses,
  getProductDecisionCenter,
  getProductDecisionTimeline,
  getProductDecisionTimelineExportCsv,
  getStores,
  getSupplierFilters,
  upsertAnalyticsActionWithResult,
} from "../services/analyticsApi";
import {
  fmtNumber,
  fmtPct,
  fmtRsd,
  formatDate,
  formatDateTime,
} from "../utils/analyticsFormatters";
import { formatMetricDisplayValue } from "../utils/analyticsMetricValue";
import {
  chunkProductDecisionActionStatusLookups,
  productDecisionActionStatusLookupSignature,
} from "../utils/productDecisionActionStatus";
import { ANALYTICS_VELOCITY_LABEL } from "../utils/analyticsVelocitySemantics";
import { ANALYTICS_UNAVAILABLE_LABEL } from "../utils/analyticsConstants";
import { getAnalyticsActionWriteErrorMessage } from "../utils/analyticsActionWriteErrors";
import { downloadDecisionTimelineExportCsv } from "../utils/decisionTimelineExport";
import {
  timelineEmptyReasonLabel,
  timelineEventTypeLabel,
  timelineGapReasonLabel,
} from "../utils/decisionTimelineLabels";
import {
  getAnalyticsMetaMessage,
  isAnalyticsMetaInsufficient,
  isAnalyticsMetaWarning,
  shouldShowAnalyticsEmptyState,
} from "../utils/analyticsResponseMeta";
import { analyticsMetricDescriptions } from "../utils/analyticsMetricDescriptions";
import {
  resolveSupplierFilterFallbackState,
  SUPPLIER_FILTER_LOAD_FAILED_MESSAGE,
  SUPPLIER_FILTER_STALE_LIST_MESSAGE,
} from "../utils/supplierFilterFallbackState";
import { getDataScope, type DataScope } from "../utils/dataScope";
import { recommendationReasonLabel } from "../utils/canonicalRecommendationSemantics";
import type {
  AnalyticsActionDataQualityStatus,
  AnalyticsActionSourceType,
  ProductDecisionCenterItem,
  ProductDecisionAlternativeRecommendation,
  ProductDecisionCenterResponse,
  ProductDecisionEvidenceNode,
  ProductDecisionDecisionTreeNode,
  ProductDecisionWhyPanel,
  ProductDecisionRecommendationStatus,
  ProductDecisionTimelineFilterResponse,
  DecisionTimelineFilterScope,
  StoreOption,
  SupplierFilterOption,
} from "../types/analytics";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import "./ProductDecisionCenterPage.css";

type SortField =
  | "productName"
  | "supplierName"
  | "revenue"
  | "unitsSold"
  | "velocityUnitsPerDay"
  | "marginPct"
  | "currentStock"
  | "trendPct"
  | "stockCoverDays"
  | "sellThroughRatio"
  | "confidencePct"
  | "recommendationStatus"
  | "dataQualityStatus";

type SortDir = "asc" | "desc";
type RecommendationFilter = "all" | ProductDecisionRecommendationStatus;
type DataQualityFilter = "all" | "good" | "warning" | "critical" | "insufficient_data";
type PeriodPreset = "last30" | "last60" | "last90" | "custom";
type OptionalActionStatusWarning = {
  message: string;
};

const PRODUCT_DECISION_INITIAL_RENDER_LIMIT = 50;
const PRODUCT_DECISION_RENDER_BATCH_SIZE = 50;

export type ProductDecisionSignalFields = {
  stockCoverDays?: number | null;
  stockCoverStatus?: string | null;
  stockCoverStatusLabel?: string | null;
  sellThroughRatio?: number | null;
  sellThroughStatus?: string | null;
  sellThroughStatusLabel?: string | null;
  signalConfidencePct?: number | null;
  recommendationAllowed?: boolean | null;
};

export type ProductDecisionRow = ProductDecisionCenterItem & ProductDecisionSignalFields;

const RECOMMENDATION_LABELS: Record<ProductDecisionRecommendationStatus, string> = {
  BOOST: "Pojačaj",
  REPLENISH: "Dopuni",
  WATCH: "Prati",
  MARKDOWN: "Snizi cenu",
  DO_NOT_ORDER: "Ne naručivati",
  FIX_DATA: "Proveriti podatke",
  INSUFFICIENT_DATA: "Nedovoljno podataka",
};

const RECOMMENDATION_OPTIONS: Array<{ value: RecommendationFilter; label: string }> = [
  { value: "all", label: "Sve preporuke" },
  { value: "REPLENISH", label: "Dopuni" },
  { value: "BOOST", label: "Pojačaj" },
  { value: "WATCH", label: "Prati" },
  { value: "MARKDOWN", label: "Snizi cenu" },
  { value: "DO_NOT_ORDER", label: "Ne naručivati" },
  { value: "FIX_DATA", label: "Za ispravku podataka" },
  { value: "INSUFFICIENT_DATA", label: "Nedovoljno podataka" },
];

const RECOMMENDATION_PRIORITY: Record<ProductDecisionRecommendationStatus, number> = {
  FIX_DATA: 7,
  BOOST: 6,
  REPLENISH: 5,
  MARKDOWN: 4,
  DO_NOT_ORDER: 3,
  WATCH: 2,
  INSUFFICIENT_DATA: 1,
};

export function productEvidencePriority(row: Pick<ProductDecisionCenterItem, "revenue" | "unitsSold" | "currentStock">): number {
  if (row.unitsSold > 0 || row.revenue > 0) return 3;
  if ((row.currentStock ?? 0) > 0) return 2;
  return 1;
}

const DATA_QUALITY_LABELS: Record<Exclude<DataQualityFilter, "all">, string> = {
  good: "Dobar",
  warning: "Upozorenje",
  critical: "Kritičan",
  insufficient_data: "Nedovoljno podataka",
};

const DATA_QUALITY_ORDER: Record<Exclude<DataQualityFilter, "all">, number> = {
  critical: 4,
  warning: 3,
  insufficient_data: 2,
  good: 1,
};

const REASON_CODE_MESSAGES: Record<string, string> = {
  high_velocity: "Artikal se brzo prodaje.",
  low_stock: "Zaliha je ispod bezbednog nivoa.",
  poor_margin: "Marža je ispod željenog nivoa.",
  stale_stock: "Artikal dugo nema prodaju.",
  missing_cost: "Nedostaje nabavna cena.",
  missing_supplier: "Nedostaje dobavljač.",
  insufficient_history: "Nema dovoljno istorije za sigurnu preporuku.",
  low_sample_size: "Uzorak prodaje je premali za sigurnu odluku.",
  no_sales_in_period: "U izabranom periodu nema evidentirane prodaje.",
  missing_last_sale: "Nedostaje datum poslednje prodaje.",
  margin_coverage_unavailable: "Pokrivenost nabavnom cenom nije dostupna bez prodaje.",
  replenish_needed: "Potrebna je dopuna da bi se izbegao gubitak prodaje.",
  high_stock_risk: "Postoji rizik od viška zalihe.",
  data_quality_blocker: "Kvalitet podataka blokira pouzdanu preporuku.",
  category_missing: "Nedostaje kategorija; tip obuće je korišćen za klasifikaciju.",
  footwear_type_missing: "Nedostaje tip obuće; kategorija je korišćena za klasifikaciju.",
  variant_data_incomplete: "Nedostaju podaci o boji ili veličini.",
  expected_impact_denominator_missing: "Nedostaje ulaz za procenu očekivanog uticaja.",
  data_quality_critical: "Kvalitet podataka je kritičan i traži proveru.",
  insufficient_data: "Signal nije dovoljno jak za pouzdanu preporuku.",
  positive_trend: "Trend podržava rast.",
  weak_signal: "Signal je slab i traži oprez.",
  monitor_only: "Potrebno je samo praćenje.",
  confidence_monitor: "Pouzdanost sugeriše praćenje, ne agresivnu akciju.",
  signal_gap: "Signal je previše tanak za čvrstu odluku.",
  selected_action_has_stronger_signal: "Odabrana preporuka ima jači signal.",
  demand_not_weak_enough: "Potražnja još nije dovoljno slaba za sniženje.",
  no_replenishment_gap: "Nema dovoljno razlike do minimalne zalihe.",
  margin_support_missing: "Marža ne podržava jaču akciju.",
  understock_risk: "Postoji rizik od premale zalihe.",
  enough_signal_for_action: "Signal je dovoljan za aktivniju akciju.",
  no_blocking_data_issue: "Nema blokirajućeg problema sa podacima.",
  weak_demand: "Potražnja nije dovoljno jaka.",
  negative_trend: "Trend nije povoljan.",
  stock_gap: "Postoji rupa u zalihama.",
};

type ConfidenceLevel = "high" | "medium" | "low" | "insufficient_data";

const CONFIDENCE_LEVEL_LABELS: Record<ConfidenceLevel, string> = {
  high: "Visoka sigurnost",
  medium: "Srednja sigurnost",
  low: "Niska sigurnost",
  insufficient_data: "Nedovoljno podataka",
};

const DRIVER_LABELS: Record<string, string> = {
  sales_velocity: "Brzina prodaje",
  margin: "Marža",
  stock_risk: "Rizik zalihe",
  trend: "Trend",
  supplier_reliability: "Pouzdanost dobavljača",
  missing_cost: "Nedostaje nabavna cena",
  sparse_sales: "Malo prodaje",
};

const EVIDENCE_CATEGORY_LABELS: Record<string, string> = {
  decision: "Odluka",
  evidence: "Dokaz",
  confidence: "Sigurnost",
  constraint: "Ograničenje",
  impact: "Uticaj",
};

const TABLE_COLUMNS: AnalyticsTableColumn<ProductDecisionCenterItem>[] = [
  { key: "productName", header: "Artikal", dataType: "text" },
  { key: "supplierName", header: "Dobavljač", dataType: "text" },
  { key: "revenue", header: "Prodaja / komadi", dataType: "currency" },
  { key: "velocityUnitsPerDay", header: ANALYTICS_VELOCITY_LABEL, dataType: "number" },
  { key: "marginPct", header: "Marža", dataType: "percent" },
  { key: "currentStock", header: "Zaliha", dataType: "number" },
  { key: "trendPct", header: "Trend", dataType: "percent" },
  { key: "stockCoverDays", header: "Pokrivenost zalihe", dataType: "number" },
  { key: "sellThroughRatio", header: "Prodajnost (odnos)", dataType: "ratio", formatHint: "ratio_0_to_1" },
  { key: "confidencePct", header: "Sigurnost preporuke", dataType: "number" },
  { key: "dataQualityStatus", header: "Kvalitet podataka", dataType: "text" },
  { key: "recommendationLabel", header: "Preporuka", dataType: "text" },
];

const PRODUCT_DECISION_PAGE_EXPLANATION =
  "Ovaj ekran predlaže šta uraditi sa artiklima: dopuniti, pojačati, sniziti cenu, pratiti ili proveriti podatke. Dobra marža ili zdrava zaliha same po sebi nisu dovoljne — ako je uzorak prodaje mali, preporuka ostaje blokirana dok ne stigne više dokaza.";

function toDateInputValue(date: Date): string {
  const parts = new Intl.DateTimeFormat("en-US", {
    timeZone: "Europe/Belgrade",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(date);
  const values = Object.fromEntries(parts.map((part) => [part.type, part.value]));
  return `${values.year}-${values.month}-${values.day}`;
}

function shiftDateInputValue(value: string, days: number): string {
  const parsed = new Date(`${value}T12:00:00Z`);
  parsed.setUTCDate(parsed.getUTCDate() + days);
  return parsed.toISOString().slice(0, 10);
}

export function defaultPeriodRange(now = new Date()) {
  const toDate = toDateInputValue(now);
  return { fromDate: shiftDateInputValue(toDate, -29), toDate };
}

export function applyPeriodPreset(preset: Exclude<PeriodPreset, "custom">, now = new Date()) {
  const toDate = toDateInputValue(now);
  const days = preset === "last60" ? -59 : preset === "last90" ? -89 : -29;
  return { fromDate: shiftDateInputValue(toDate, days), toDate };
}

function formatDateInputForDisplay(value: string): string {
  const parsed = new Date(`${value}T12:00:00Z`);
  if (Number.isNaN(parsed.getTime())) return value;
  return new Intl.DateTimeFormat("sr-RS", {
    timeZone: "Europe/Belgrade",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  }).format(parsed);
}

type ProductDecisionUrlState = {
  periodPreset: PeriodPreset;
  fromDate: string;
  toDate: string;
  storeId: number | null;
  supplierId: number | null;
  recommendationFilter: RecommendationFilter;
  dataQualityFilter: DataQualityFilter;
  search: string;
  sortField: SortField;
  sortDir: SortDir;
};

function isDateInputValue(value: string | null): value is string {
  return value != null && /^\d{4}-\d{2}-\d{2}$/.test(value);
}

function readProductDecisionUrlState(fallback: { fromDate: string; toDate: string }): ProductDecisionUrlState {
  const params = window.location.pathname === "/analytics/products"
    ? new URLSearchParams(window.location.search)
    : new URLSearchParams();
  const period = params.get("period");
  const periodPreset: PeriodPreset = period === "last60" || period === "last90" || period === "custom" ? period : "last30";
  const recommendation = params.get("recommendation") as RecommendationFilter | null;
  const quality = params.get("quality") as DataQualityFilter | null;
  const sort = params.get("sort") as SortField | null;
  const dir = params.get("dir");
  const hasExplicitPeriod = isDateInputValue(params.get("from")) || isDateInputValue(params.get("to"));
  const validRecommendations = RECOMMENDATION_OPTIONS.some((option) => option.value === recommendation);
  const validQuality = ["all", "good", "warning", "critical", "insufficient_data"].includes(quality ?? "");
  const validSort = ["productName", "supplierName", "revenue", "unitsSold", "velocityUnitsPerDay", "marginPct", "currentStock", "trendPct", "stockCoverDays", "sellThroughRatio", "confidencePct", "recommendationStatus", "dataQualityStatus"].includes(sort ?? "");
  return {
    periodPreset,
    fromDate: isDateInputValue(params.get("from")) ? params.get("from")! : hasExplicitPeriod ? fallback.fromDate : "",
    toDate: isDateInputValue(params.get("to")) ? params.get("to")! : hasExplicitPeriod ? fallback.toDate : "",
    storeId: parseEntityIdParam(params.get("store")),
    supplierId: parseEntityIdParam(params.get("supplier")),
    recommendationFilter: validRecommendations ? recommendation! : "all",
    dataQualityFilter: validQuality ? quality! : "all",
    search: params.get("search") ?? "",
    sortField: validSort ? sort! : "recommendationStatus",
    sortDir: dir === "asc" ? "asc" : "desc",
  };
}

function writeProductDecisionUrlState(state: ProductDecisionUrlState): void {
  if (window.location.pathname !== "/analytics/products") return;
  const params = new URLSearchParams(window.location.search);
  const values: Record<string, string | null> = {
    period: state.periodPreset,
    from: state.fromDate,
    to: state.toDate,
    store: state.storeId == null ? null : String(state.storeId),
    supplier: state.supplierId == null ? null : String(state.supplierId),
    recommendation: state.recommendationFilter === "all" ? null : state.recommendationFilter,
    quality: state.dataQualityFilter === "all" ? null : state.dataQualityFilter,
    search: state.search.trim() || null,
    sort: state.sortField === "recommendationStatus" ? null : state.sortField,
    dir: state.sortDir === "desc" ? null : state.sortDir,
  };
  for (const [key, value] of Object.entries(values)) {
    if (value == null) params.delete(key);
    else params.set(key, value);
  }
  const query = params.toString();
  window.history.replaceState(null, "", `${window.location.pathname}${query ? `?${query}` : ""}${window.location.hash}`);
}

export function compareNullable<T>(left: T | null | undefined, right: T | null | undefined, compare: (a: T, b: T) => number, direction: SortDir): number {
  const leftMissing = left == null;
  const rightMissing = right == null;
  if (leftMissing || rightMissing) {
    if (leftMissing && rightMissing) return 0;
    return leftMissing ? 1 : -1;
  }
  const result = compare(left, right);
  return direction === "asc" ? result : -result;
}

function canonicalDataQualityStatus(
  value: string | null | undefined,
): Exclude<DataQualityFilter, "all"> {
  const lower = (value ?? "").trim().toLowerCase();
  if (lower === "fair") return "warning";
  if (lower === "poor") return "critical";
  if (lower === "good" || lower === "warning" || lower === "critical" || lower === "insufficient_data") return lower;
  return "insufficient_data";
}

function recommendationToneClass(status: ProductDecisionRecommendationStatus): string {
  if (status === "BOOST") return "decision-pill decision-pill-boost";
  if (status === "REPLENISH") return "decision-pill decision-pill-replenish";
  if (status === "MARKDOWN") return "decision-pill decision-pill-markdown";
  if (status === "DO_NOT_ORDER") return "decision-pill decision-pill-stop";
  if (status === "FIX_DATA") return "decision-pill decision-pill-fix";
  if (status === "WATCH") return "decision-pill decision-pill-watch";
  return "decision-pill decision-pill-na";
}

function dataQualityClass(status: Exclude<DataQualityFilter, "all">): string {
  if (status === "good") return "dq-pill dq-good";
  if (status === "warning") return "dq-pill dq-warning";
  if (status === "critical") return "dq-pill dq-critical";
  return "dq-pill dq-insufficient";
}

function translateReasonCode(code: string): string {
  const normalized = (code ?? "").trim().toLowerCase();
  return REASON_CODE_MESSAGES[normalized] ?? recommendationReasonLabel(normalized);
}

function isDecisionBlocked(whyPanel: ProductDecisionWhyPanel): boolean {
  return !whyPanel.recommendationAllowed || whyPanel.confidenceLevel === "insufficient_data";
}

function blockedDecisionNextStep(whyPanel: ProductDecisionWhyPanel): string {
  const reasonCodes = new Set(whyPanel.reasonCodes.map((code) => code.trim().toLowerCase()));
  if (reasonCodes.has("margin_coverage_unavailable")) {
    return "Proširite period ili dopunite istoriju prodaje da bi se pokrivenost nabavnom cenom mogla izračunati.";
  }
  if (reasonCodes.has("missing_cost") || reasonCodes.has("missing_supplier") || reasonCodes.has("data_quality_blocker")) {
    return "Proverite kvalitet podataka i ponovite analizu.";
  }
  if (reasonCodes.has("insufficient_history") || reasonCodes.has("low_sample_size") || reasonCodes.has("no_sales_in_period")) {
    return "Proširite period na 60/90 dana ili dopunite istoriju prodaje, pa pokušajte ponovo.";
  }
  if (reasonCodes.has("missing_last_sale") || whyPanel.inputFreshnessStatus !== "fresh") {
    return "Osvežite ili proverite istoriju prodaje pre nove odluke.";
  }
  return "Proverite ulaze i ponovite analizu kada budu dostupni pouzdaniji podaci.";
}

function warningSummaryItems(items: Array<{ code: string; message: string }>): Array<{ code: string; message: string }> {
  const secondaryCodes = new Set([
    "insufficient_data",
    "product_recommendation_blocked",
    "expected_impact_denominator_missing",
  ]);
  const primary = items.filter((item) => !secondaryCodes.has(item.code.trim().toLowerCase()));
  return (primary.length ? primary : items).slice(0, 2);
}

function normalizeConfidenceLevel(
  value: string | null | undefined,
): ConfidenceLevel {
  const normalized = (value ?? "").trim().toLowerCase();
  if (normalized === "high" || normalized === "medium" || normalized === "low" || normalized === "insufficient_data") {
    return normalized;
  }

  return "insufficient_data";
}

function confidenceLevelClass(level: ConfidenceLevel): string {
  if (level === "high") return "confidence-pill confidence-high";
  if (level === "medium") return "confidence-pill confidence-medium";
  if (level === "low") return "confidence-pill confidence-low";
  return "confidence-pill confidence-insufficient";
}

function normalizeSignalList(values: string[] | null | undefined): string[] {
  const seen = new Set<string>();
  const result: string[] = [];
  for (const value of values ?? []) {
    const normalized = (value ?? "").trim();
    if (!normalized) continue;
    const key = normalized.toLowerCase();
    if (seen.has(key)) continue;
    seen.add(key);
    result.push(normalized);
  }
  return result;
}

function resolveConfidenceScore(row: ProductDecisionRow): number | null {
  if (row.confidenceScore != null && !Number.isNaN(row.confidenceScore)) {
    return row.confidenceScore;
  }

  return null;
}

function resolveExpectedImpactRsd(row: ProductDecisionRow): number | null {
  if (row.expectedImpactRsd != null && !Number.isNaN(row.expectedImpactRsd)) {
    return row.expectedImpactRsd;
  }

  return null;
}

function resolveWarningCodes(row: ProductDecisionRow): string[] {
  return normalizeSignalList(row.warningCodes);
}

function resolvePrimaryDrivers(row: ProductDecisionRow): string[] {
  return normalizeSignalList(row.primaryDrivers);
}

function resolveInputFreshnessStatus(row: ProductDecisionRow): "fresh" | "stale" | "critical" | "unknown" {
  return normalizeInputFreshnessStatus(row.inputFreshnessStatus);
}

function normalizeInputFreshnessStatus(
  value: string | null | undefined,
): "fresh" | "stale" | "critical" | "unknown" {
  const normalized = (value ?? "").trim().toLowerCase();
  if (normalized === "fresh" || normalized === "stale" || normalized === "critical" || normalized === "unknown") {
    return normalized;
  }

  return "unknown";
}

function whyPanelSummarySourceLabel(value: string | null | undefined): string {
  const normalized = (value ?? "").trim().toLowerCase();
  if (normalized === "recommendation_reason") return "Izvor objašnjenja: direktan razlog preporuke";
  if (normalized === "backend_composed") return "Izvor objašnjenja: backend kompozicija signala";
  if (normalized === "missing") return "Izvor objašnjenja: nije dostupno";
  return "Izvor objašnjenja: nepoznat";
}

function whyPanelFallbackLabel(value: string | null | undefined): string {
  const normalized = (value ?? "").trim().toLowerCase();
  if (normalized === "recommendation_reason_missing") return "Fallback: RecommendationReason nije bio dostupan";
  if (!normalized) return "Fallback: backend kompozicija je korišćena";
  return `Fallback: ${value}`;
}

function decisionTreeCategoryLabel(value: string | null | undefined): string {
  const normalized = (value ?? "").trim().toLowerCase();
  if (normalized === "decision") return "Odluka";
  if (normalized === "gate") return "Uslov";
  if (normalized === "branch") return "Grana";
  if (normalized === "outcome") return "Ishod";
  return "Grana";
}

function buildProductDecisionWhyPanel(row: ProductDecisionRow): ProductDecisionWhyPanel {
  if (row.whyPanel) {
    return row.whyPanel;
  }

  const confidenceLevel = normalizeConfidenceLevel(row.confidenceLevel);
  const confidenceScore = resolveConfidenceScore(row);
  const primaryDrivers = resolvePrimaryDrivers(row);
  const warningCodes = resolveWarningCodes(row);
  const expectedImpactRsd = resolveExpectedImpactRsd(row);
  const inputFreshnessStatus = resolveInputFreshnessStatus(row);
  const confidenceBreakdown = row.confidenceBreakdown ?? [];
  const alternativeRecommendations = row.alternativeRecommendations ?? [];
  const evidenceChain = row.evidenceChain ?? [];
  const summarySource = row.recommendationReason.trim() ? "recommendation_reason" : "backend_composed";

  return {
    recommendationStatus: row.recommendationStatus,
    recommendationLabel: displayRecommendationLabel(row),
    recommendationReason: row.recommendationReason,
    recommendedAction: row.recommendedAction,
    explainabilityText: row.explainabilityText,
    summarySource,
    summaryFallbackUsed: summarySource !== "recommendation_reason",
    summaryFallbackReason: summarySource !== "recommendation_reason" ? "recommendation_reason_missing" : null,
    reasonCodes: [...row.reasonCodes],
    primaryDrivers,
    warningCodes,
    confidenceLevel,
    confidenceScore,
    confidencePct: row.confidencePct,
    reliabilityPct: row.reliabilityPct,
    dataQualityStatus: row.dataQualityStatus,
    inputFreshnessStatus,
    recommendationAllowed: Boolean(row.recommendationAllowed),
    expectedImpactRsd,
    impactWindowDays: row.impactWindowDays ?? null,
    riskIfIgnored: row.riskIfIgnored,
    confidenceBreakdown,
    alternativeRecommendations,
    evidenceChain,
  };
}

function confidenceLevelLabel(level: ConfidenceLevel): string {
  return CONFIDENCE_LEVEL_LABELS[level];
}

function confidenceScoreText(level: ConfidenceLevel, score: number | null): string {
  if (level === "insufficient_data" || score == null) {
    return confidenceLevelLabel(level);
  }

  return `${confidenceLevelLabel(level)} · ${fmtNumber(score, 0, ANALYTICS_UNAVAILABLE_LABEL)}%`;
}

function inputFreshnessLabel(value: string | null | undefined): string {
  const normalized = (value ?? "").trim().toLowerCase();
  if (normalized === "fresh") return "Sveže";
  if (normalized === "stale") return "Zastarelo";
  if (normalized === "critical") return "Kritično";
  return "Nije poznato";
}

function lifecycleStateLabel(value: string | null | undefined): string {
  const normalized = (value ?? "").trim().toLowerCase();
  if (normalized === "issued") return "Izdata";
  if (normalized === "accepted") return "Prihvaćena";
  if (normalized === "rejected") return "Odbijena";
  if (normalized === "ignored") return "Ignorisana";
  if (normalized === "executed") return "Izvršena";
  return "Nije dostupno";
}

function recommendationTypeLabel(value: string | null | undefined): string {
  const normalized = (value ?? "").trim().toUpperCase();
  if (!normalized) return "Nije dostupno";
  return RECOMMENDATION_LABELS[normalized as ProductDecisionRecommendationStatus] ?? value!.trim();
}

function timelineScopeLabel(
  scope: DecisionTimelineFilterScope | null | undefined,
  fallbackSourceKey: string,
  fallbackFrom: string,
  fallbackTo: string,
): string {
  const sourceKey = scope?.sourceKey?.trim() || fallbackSourceKey;
  const family = recommendationTypeLabel(scope?.recommendationType);
  const from = formatDate(scope?.periodFromUtc ?? fallbackFrom, fallbackFrom);
  const to = formatDate(scope?.periodToUtc ?? fallbackTo, fallbackTo);
  return `Entitet: ${sourceKey} · Porodica: ${family} · Period: ${from} – ${to}`;
}

function primaryDriverLabel(value: string): string {
  return DRIVER_LABELS[value] ?? value;
}

function evidenceCategoryLabel(value: string): string {
  return EVIDENCE_CATEGORY_LABELS[value] ?? value;
}

function stockCoverStatusLabel(status: string | null | undefined): string {
  const normalized = (status ?? "").trim().toLowerCase();
  if (normalized === "low_cover" || normalized === "low") return "Niska pokrivenost";
  if (normalized === "healthy") return "Zdrava pokrivenost";
  if (normalized === "overstock" || normalized === "high") return "Prekomerna zaliha";
  if (normalized === "slow_stock" || normalized === "slow") return "Spor obrt";
  if (normalized === "no_velocity") return "Bez rotacije";
  if (normalized === "out_of_stock_risk") return "Rizik rasprodaje";
  return "Nedovoljno podataka";
}

function sellThroughStatusLabel(status: string | null | undefined): string {
  const normalized = (status ?? "").trim().toLowerCase();
  if (normalized === "good") return "Dobra prodajnost";
  if (normalized === "warning") return "Upozorenje prodajnosti";
  if (normalized === "critical") return "Kritična prodajnost";
  return "Nedovoljno podataka";
}

function formatSignalMetricValue(value: number | null | undefined, status: string | null | undefined, unit: "days" | "ratio"): string {
  if (value == null || Number.isNaN(value)) {
    return (status ?? "").trim().toLowerCase() === "insufficient_data"
      ? "Nedovoljno podataka"
      : "Nije dostupno";
  }

  if (unit === "days") {
    return formatMetricDisplayValue({ value, kind: "days" });
  }

  return formatMetricDisplayValue({ value, kind: "ratioPercent" });
}

function buildSupplierDecisionUrl(supplierId: number): string {
  return `/analytics/supplier?supplierId=${supplierId}`;
}

function buildInventoryDecisionUrl(row: ProductDecisionRow): string {
  const params = new URLSearchParams();
  if (row.sku) params.set("sku", row.sku);
  params.set("productId", String(row.productId));
  const query = params.toString();
  return query ? `/analytics/inventory?${query}` : "/analytics/inventory";
}

// Source enums, backend recommendation text and reason payloads stay canonical.
// We localize only UI-owned labels/maps here so backend copy drift stays visible
// instead of being silently rewritten on arbitrary source text.
function displayRecommendationLabel(row: ProductDecisionRow): string {
  return RECOMMENDATION_LABELS[row.recommendationStatus] ?? row.recommendationLabel;
}

function buildSourceKey(
  row: ProductDecisionRow,
  actionKind: string,
  fromDate: string,
  toDate: string,
  storeId: number | null,
  supplierId: number | null,
): string {
  return `product:${row.productId}:${actionKind}:${fromDate}:${toDate}:${storeId ?? "all"}:${supplierId ?? "all"}`;
}

function recommendationActionTitle(status: ProductDecisionRecommendationStatus, productName: string): string {
  if (status === "REPLENISH") return `Dopuni: ${productName}`;
  if (status === "BOOST") return `Pojačaj: ${productName}`;
  if (status === "MARKDOWN") return `Snizi: ${productName}`;
  if (status === "DO_NOT_ORDER") return `Ne naručivati: ${productName}`;
  if (status === "FIX_DATA") return `Proveri podatke: ${productName}`;
  if (status === "WATCH") return `Prati: ${productName}`;
  return `Proveri: ${productName}`;
}

function mapActionPriority(row: ProductDecisionRow): "P1" | "P2" | "P3" {
  const dataQuality = canonicalDataQualityStatus(row.dataQualityStatus);
  const recommendationStatusValue = row["recommendationStatus"];
  const hasCriticalOos = recommendationStatusValue === "REPLENISH"
    && row.stockGap != null
    && row.currentStock != null
    && row.stockGap > 0
    && row.currentStock <= 0;
  const hasLargeLostSales = row.lostSalesEstimate != null && row.lostSalesEstimate >= 100_000;
  const hasCriticalDataIssue = recommendationStatusValue === "FIX_DATA" && dataQuality === "critical";

  if (hasCriticalOos || hasLargeLostSales || hasCriticalDataIssue) return "P1";
  if (recommendationStatusValue === "WATCH" || recommendationStatusValue === "INSUFFICIENT_DATA") return "P3";
  return "P2";
}

function hasDataQualityGap(reasonCodes: string[]): boolean {
  return reasonCodes.some((code) => {
    const normalized = (code ?? "").trim().toLowerCase();
    return normalized === "missing_cost" || normalized === "missing_supplier";
  });
}

function toActionDataQualityStatus(value: string | null | undefined): AnalyticsActionDataQualityStatus {
  const normalized = (value ?? "").trim().toLowerCase();
  if (normalized === "good" || normalized === "warning" || normalized === "critical" || normalized === "insufficient_data") {
    return normalized;
  }

  return "insufficient_data";
}

function buildActionStatusWarning(_reason: unknown): OptionalActionStatusWarning {
  return { message: "Status akcija trenutno nije dostupan." };
}

export function buildProductQueueSpec(row: ProductDecisionRow): {
  sourceType: AnalyticsActionSourceType;
  actionKind: string;
  title: string;
  recommendationStatus: string;
  priority: "P1" | "P2" | "P3";
  dueAtUtc: string;
} {
  const dueAtUtc = new Date(Date.now() + 14 * 24 * 60 * 60 * 1000).toISOString();
  const normalizedCover = (row.stockCoverStatus ?? "").trim().toLowerCase();

  if (normalizedCover === "insufficient_data" || row.recommendationAllowed !== true) {
    return {
      sourceType: "product",
      actionKind: "signal_check",
      title: `Proveri signal: ${row.productName}`,
      recommendationStatus: "SIGNAL_REVIEW",
      priority: "P3",
      dueAtUtc,
    };
  }

  if (normalizedCover === "out_of_stock_risk" || normalizedCover === "low_cover" || normalizedCover === "low") {
    return {
      sourceType: "product",
      actionKind: "replenish",
      title: recommendationActionTitle("REPLENISH", row.productName),
      recommendationStatus: "REPLENISH",
      priority: normalizedCover === "out_of_stock_risk" ? "P1" : "P2",
      dueAtUtc,
    };
  }

  if (normalizedCover === "slow_stock" || normalizedCover === "slow" || normalizedCover === "no_velocity") {
    return {
      sourceType: "product",
      actionKind: "slow_stock_review",
      title: `Proveri sporu zalihu: ${row.productName}`,
      recommendationStatus: "SLOW_STOCK_REVIEW",
      priority: normalizedCover === "no_velocity" ? "P3" : "P2",
      dueAtUtc,
    };
  }

  const reasonCodes = row.reasonCodes ?? [];
  const recommendationStatusValue = row["recommendationStatus"];
  const dataQualityGap = recommendationStatusValue === "FIX_DATA" || hasDataQualityGap(reasonCodes);

  if (dataQualityGap) {
    return {
      sourceType: "data_quality",
      actionKind: "data_quality_fix",
      title: "Dopuni podatke za pouzdaniju analitiku",
      recommendationStatus: "FIX_DATA",
      priority: toActionDataQualityStatus(row.dataQualityStatus) === "critical" ? "P1" : "P2",
      dueAtUtc,
    };
  }

  if (recommendationStatusValue === "INSUFFICIENT_DATA") {
    return {
      sourceType: "product",
      actionKind: "signal_check",
      title: `Proveri signal: ${row.productName}`,
      recommendationStatus: "SIGNAL_REVIEW",
      priority: "P3",
      dueAtUtc,
    };
  }

  return {
    sourceType: "product",
    actionKind: recommendationStatusValue.toLowerCase(),
    title: recommendationActionTitle(recommendationStatusValue, row.productName),
    recommendationStatus: recommendationStatusValue,
    priority: mapActionPriority(row),
    dueAtUtc,
  };
}

export default function ProductDecisionCenterPage() {
  const initialRange = useMemo(() => defaultPeriodRange(), []);
  const initialUrlState = useMemo(() => readProductDecisionUrlState(initialRange), [initialRange]);

  const [periodPreset, setPeriodPreset] = useState<PeriodPreset>(initialUrlState.periodPreset);
  const [fromDate, setFromDate] = useState(initialUrlState.fromDate);
  const [toDate, setToDate] = useState(initialUrlState.toDate);
  const [storeId, setStoreId] = useState<number | null>(initialUrlState.storeId);
  const [supplierId, setSupplierId] = useState<number | null>(initialUrlState.supplierId);
  const [recommendationFilter, setRecommendationFilter] = useState<RecommendationFilter>(initialUrlState.recommendationFilter);
  const [dataQualityFilter, setDataQualityFilter] = useState<DataQualityFilter>(initialUrlState.dataQualityFilter);
  const [search, setSearch] = useState(initialUrlState.search);
  const [serverSearch, setServerSearch] = useState("");
  const [sortField, setSortField] = useState<SortField>(initialUrlState.sortField);
  const [sortDir, setSortDir] = useState<SortDir>(initialUrlState.sortDir);
  const [visibleRowLimit, setVisibleRowLimit] = useState(PRODUCT_DECISION_INITIAL_RENDER_LIMIT);
  const [expandedProductId, setExpandedProductId] = useState<number | null>(null);
  const [timelineByProductId, setTimelineByProductId] = useState<Record<number, ProductDecisionTimelineFilterResponse | null>>({});
  const [timelineLoadingProductId, setTimelineLoadingProductId] = useState<number | null>(null);
  const [timelineFamilyFilter, setTimelineFamilyFilter] = useState<"row" | "all">("row");
  const [timelineError, setTimelineError] = useState<string | null>(null);
  const [timelineExportError, setTimelineExportError] = useState<string | null>(null);
  const [timelineExportingProductId, setTimelineExportingProductId] = useState<number | null>(null);
  const [evidenceSnapshotByProductId, setEvidenceSnapshotByProductId] = useState<Record<number, { capturedAtUtc: string; recommendationId: string } | null>>({});
  const [dataScope, setDataScope] = useState<DataScope>(() => getDataScope());
  const timelineRequestSeqRef = useRef(0);
  const dataRequestSeqRef = useRef(0);

  const [stores, setStores] = useState<StoreOption[]>([]);
  const [suppliers, setSuppliers] = useState<SupplierFilterOption[]>([]);
  const [supplierFiltersWarning, setSupplierFiltersWarning] = useState<string | null>(null);
  const [supplierFiltersStale, setSupplierFiltersStale] = useState(false);
  const suppliersRef = useRef(suppliers);
  const supplierIdRef = useRef(supplierId);
  suppliersRef.current = suppliers;
  supplierIdRef.current = supplierId;
  const [payload, setPayload] = useState<ProductDecisionCenterResponse | null>(null);
  const payloadRef = useRef<ProductDecisionCenterResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<{ message: string; errorCode?: string | null; correlationId?: string | null } | null>(null);
  const [staleWarning, setStaleWarning] = useState<string | null>(null);
  const [actionStatusWarning, setActionStatusWarning] = useState<OptionalActionStatusWarning | null>(null);
  const [queueMessage, setQueueMessage] = useState<string | null>(null);
  const [queueBusyKey, setQueueBusyKey] = useState<string | null>(null);
  const [queuedActionKeys, setQueuedActionKeys] = useState<Set<string> | null>(null);
  const queuedActionKeysRef = useRef<Set<string> | null>(null);
  const queueBusyKeyRef = useRef<string | null>(null);
  queuedActionKeysRef.current = queuedActionKeys;

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setServerSearch(search.trim());
    }, 300);

    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    writeProductDecisionUrlState({
      periodPreset,
      fromDate,
      toDate,
      storeId,
      supplierId,
      recommendationFilter,
      dataQualityFilter,
      search,
      sortField,
      sortDir,
    });
  }, [dataQualityFilter, fromDate, periodPreset, recommendationFilter, search, sortDir, sortField, storeId, supplierId, toDate]);

  useEffect(() => {
    const handlePopState = () => {
      const next = readProductDecisionUrlState(defaultPeriodRange());
      setPeriodPreset(next.periodPreset);
      setFromDate(next.fromDate);
      setToDate(next.toDate);
      setStoreId(next.storeId);
      setSupplierId(next.supplierId);
      setRecommendationFilter(next.recommendationFilter);
      setDataQualityFilter(next.dataQualityFilter);
      setSearch(next.search);
      setSortField(next.sortField);
      setSortDir(next.sortDir);
    };
    window.addEventListener("popstate", handlePopState);
    return () => window.removeEventListener("popstate", handlePopState);
  }, []);

  useEffect(() => {
    setQueueMessage(null);
  }, [dataQualityFilter, fromDate, periodPreset, recommendationFilter, search, sortDir, sortField, storeId, supplierId, toDate]);

  useEffect(() => {
    const handleScopeChange = () => {
      // A supplier choice belongs to the previous dataset and must not narrow the next one.
      setSupplierId(null);
      setDataScope(getDataScope());
    };

    window.addEventListener("trendplus:data-scope-changed", handleScopeChange);
    return () => {
      window.removeEventListener("trendplus:data-scope-changed", handleScopeChange);
    };
  }, []);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const items = await getStores();
        if (!cancelled) setStores(items);
      } catch {
        if (!cancelled) {
          // Preserve the last known store list on transient failures instead of faking an empty filter set.
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const items = await getSupplierFilters(fromDate, toDate, true, storeId, dataScope);
        if (!cancelled) {
          const resolved = resolveSupplierFilterFallbackState(items, suppliersRef.current);
          setSupplierFiltersWarning(resolved.warning);
          setSupplierFiltersStale(resolved.isStale);
          setSuppliers(resolved.suppliers);
          if (
            resolved.shouldClearSelection
            || (
              !resolved.isStale
              && supplierIdRef.current != null
              && !resolved.suppliers.some((supplier) => supplier.supplierId === supplierIdRef.current)
            )
          ) {
            setSupplierId(null);
          }
        }
      } catch {
        if (!cancelled) {
          // Preserve prior options without claiming that they match the active period and scope.
          setSupplierFiltersWarning(SUPPLIER_FILTER_LOAD_FAILED_MESSAGE);
          setSupplierFiltersStale(true);
          setSupplierId(null);
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [dataScope, fromDate, toDate, storeId]);

  const loadData = useCallback(async () => {
    const requestSeq = ++dataRequestSeqRef.current;
    setLoading(true);
    setError(null);
    setStaleWarning(null);
    try {
      const response = await getProductDecisionCenter({
        fromDate,
        toDate,
        storeId,
        supplierId,
        top: 500,
        dataScope,
        search: serverSearch || null,
      });
      if (dataRequestSeqRef.current !== requestSeq) {
        return;
      }
      setPayload(response);
      payloadRef.current = response;
      if (response.meta?.defaultPeriodBasis === "source_horizon") {
        const resolvedFrom = response.periodFromUtc?.slice(0, 10);
        const resolvedTo = response.periodToUtc?.slice(0, 10);
        if (resolvedFrom && resolvedTo) {
          setFromDate(resolvedFrom);
          setToDate(resolvedTo);
        }
      }
    } catch (reason) {
      if (dataRequestSeqRef.current !== requestSeq) {
        return;
      }
      const hasPreviousPayload = payloadRef.current != null;
      if (reason instanceof AnalyticsMetaError) {
        setError({
          message: reason.message,
          errorCode: reason.errorCode,
          correlationId: reason.correlationId,
        });
      } else {
        const message = reason instanceof Error ? reason.message : "Greška pri učitavanju podataka za Odluke o proizvodima.";
        setError({ message });
      }
      if (hasPreviousPayload) {
        setStaleWarning("Prikazujemo prethodno učitane podatke. Novi upit nije uspeo i podaci mogu biti zastareli.");
      } else {
        setPayload(null);
      }
    } finally {
      if (dataRequestSeqRef.current === requestSeq) {
        setLoading(false);
      }
    }
  }, [dataScope, fromDate, serverSearch, supplierId, storeId, toDate]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const rows = useMemo(
    () => (payload?.rows ?? []) as ProductDecisionRow[],
    [payload?.rows],
  );
  const responseMeta = payload?.meta ?? null;
  const responseMetaMessage = getAnalyticsMetaMessage(responseMeta);

  const filteredRows = useMemo(() => {
    const normalizedSearch = search.trim().toLowerCase();
    return rows.filter((row) => {
      if (recommendationFilter !== "all" && row.recommendationStatus !== recommendationFilter) return false;
      if (dataQualityFilter !== "all" && canonicalDataQualityStatus(row.dataQualityStatus) !== dataQualityFilter) return false;
      if (!normalizedSearch) return true;
      const text = `${row.productName} ${row.sku} ${row.supplierName ?? ""}`.toLowerCase();
      return text.includes(normalizedSearch);
    });
  }, [rows, recommendationFilter, dataQualityFilter, search]);

  const sortedRows = useMemo(() => {
    const copy = [...filteredRows];
    copy.sort((a, b) => {
      if (sortField === "productName") return compareNullable(a.productName, b.productName, (left, right) => left.localeCompare(right, "sr"), sortDir);
      if (sortField === "supplierName") return compareNullable(a.supplierName, b.supplierName, (left, right) => left.localeCompare(right, "sr"), sortDir);
      if (sortField === "revenue") return compareNullable(a.revenue, b.revenue, (left, right) => left - right, sortDir);
      if (sortField === "unitsSold") return compareNullable(a.unitsSold, b.unitsSold, (left, right) => left - right, sortDir);
      if (sortField === "velocityUnitsPerDay") return compareNullable(a.velocityUnitsPerDay, b.velocityUnitsPerDay, (left, right) => left - right, sortDir);
      if (sortField === "marginPct") return compareNullable(a.marginPct, b.marginPct, (left, right) => left - right, sortDir);
      if (sortField === "currentStock") return compareNullable(a.currentStock, b.currentStock, (left, right) => left - right, sortDir);
      if (sortField === "trendPct") return compareNullable(a.trendPct, b.trendPct, (left, right) => left - right, sortDir);
      if (sortField === "stockCoverDays") return compareNullable(a.stockCoverDays, b.stockCoverDays, (left, right) => left - right, sortDir);
      if (sortField === "sellThroughRatio") return compareNullable(a.sellThroughRatio, b.sellThroughRatio, (left, right) => left - right, sortDir);
      if (sortField === "confidencePct") return compareNullable(a.confidencePct, b.confidencePct, (left, right) => left - right, sortDir);
      if (sortField === "dataQualityStatus") {
        return compareNullable(a.dataQualityStatus, b.dataQualityStatus, (left, right) => DATA_QUALITY_ORDER[canonicalDataQualityStatus(left)] - DATA_QUALITY_ORDER[canonicalDataQualityStatus(right)], sortDir);
      }
      return compareNullable(
        a,
        b,
        (left, right) => (productEvidencePriority(left) * 100 + RECOMMENDATION_PRIORITY[left.recommendationStatus])
          - (productEvidencePriority(right) * 100 + RECOMMENDATION_PRIORITY[right.recommendationStatus]),
        sortDir,
      );
    });
    return copy;
  }, [filteredRows, sortDir, sortField]);
  const visibleRows = useMemo(() => sortedRows.slice(0, visibleRowLimit), [sortedRows, visibleRowLimit]);

  useEffect(() => {
    setVisibleRowLimit(PRODUCT_DECISION_INITIAL_RENDER_LIMIT);
  }, [dataScope, fromDate, recommendationFilter, dataQualityFilter, search, serverSearch, sortField, sortDir, storeId, supplierId, toDate]);

  const actionStatusLookupItems = useMemo(() => {
    const candidates = filteredRows.map((row) => {
      const queueSpec = buildProductQueueSpec(row);
      return {
        sourceType: queueSpec.sourceType,
        sourceKey: buildSourceKey(row, queueSpec.actionKind, fromDate, toDate, storeId, supplierId),
      };
    });

    return Array.from(new Map(
      candidates.map((entry) => [`${entry.sourceType}::${entry.sourceKey}`, entry])
    ).values());
  }, [filteredRows, fromDate, storeId, supplierId, toDate]);

  const actionStatusLookupSignature = useMemo(
    () => productDecisionActionStatusLookupSignature(actionStatusLookupItems),
    [actionStatusLookupItems],
  );

  useEffect(() => {
    let cancelled = false;

    const lookupItems = actionStatusLookupItems;

    if (lookupItems.length === 0) {
      setActionStatusWarning(null);
      setQueuedActionKeys(new Set());
      setEvidenceSnapshotByProductId((previous) => (Object.keys(previous).length === 0 ? previous : {}));
      return () => {
        cancelled = true;
      };
    }

    (async () => {
      setActionStatusWarning(null);
      try {
        const chunks = chunkProductDecisionActionStatusLookups(lookupItems);
        const settled = await Promise.allSettled(chunks.map((items) => getAnalyticsActionSourceStatuses({ items })));

        if (cancelled) return;

        const successfulStatuses = settled
          .filter((result): result is PromiseFulfilledResult<Awaited<ReturnType<typeof getAnalyticsActionSourceStatuses>>> => result.status === "fulfilled")
          .flatMap((result) => result.value.items);
        const hasFailedChunk = settled.some((result) => result.status === "rejected");
        const currentKeys = new Set(lookupItems.map((item) => item.sourceKey));
        const keys = new Set<string>();
        for (const key of queuedActionKeysRef.current ?? []) {
          if (currentKeys.has(key)) keys.add(key);
        }
        const snapshots: Record<number, { capturedAtUtc: string; recommendationId: string }> = {};
        for (const item of successfulStatuses) {
          if (item.exists && item.sourceKey) keys.add(item.sourceKey);
        }
        for (const row of filteredRows) {
          const queueSpec = buildProductQueueSpec(row);
          const sourceKey = buildSourceKey(row, queueSpec.actionKind, fromDate, toDate, storeId, supplierId);
          const status = successfulStatuses.find((entry) => entry.sourceType === queueSpec.sourceType && entry.sourceKey === sourceKey);
          if (status?.hasEvidenceSnapshot && status.evidenceSnapshotCapturedAtUtc && status.evidenceSnapshotRecommendationId) {
            snapshots[row.productId] = {
              capturedAtUtc: status.evidenceSnapshotCapturedAtUtc,
              recommendationId: status.evidenceSnapshotRecommendationId,
            };
          }
        }

        if (successfulStatuses.length === 0 && hasFailedChunk) {
          setQueuedActionKeys(null);
          setEvidenceSnapshotByProductId({});
        } else {
          setQueuedActionKeys(keys);
        }
        if (successfulStatuses.length > 0 || !hasFailedChunk) {
          setEvidenceSnapshotByProductId(snapshots);
        }
        setActionStatusWarning(hasFailedChunk ? buildActionStatusWarning(null) : null);
      } catch (reason) {
        if (!cancelled) {
          setQueuedActionKeys(null);
          setEvidenceSnapshotByProductId({});
          setActionStatusWarning(buildActionStatusWarning(reason));
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [actionStatusLookupItems, actionStatusLookupSignature, filteredRows, fromDate, storeId, supplierId, toDate]);
  const hasBlockingError = Boolean(error && !payload);
  const showMetaWarning = !loading && !hasBlockingError && isAnalyticsMetaWarning(responseMeta);
  const showInsufficientState = !loading
    && !hasBlockingError
    && shouldShowAnalyticsEmptyState(responseMeta, rows.length)
    && isAnalyticsMetaInsufficient(responseMeta);
  const showNoDataState = !loading && !hasBlockingError && !showInsufficientState && rows.length === 0;
  const showFilteredOutState = !loading && !hasBlockingError && !showInsufficientState && rows.length > 0 && sortedRows.length === 0;
  const hideKpiChrome = hasBlockingError || showInsufficientState || showNoDataState || showFilteredOutState;

  const kpis = useMemo(() => ({
    replenishCount: payload?.summary.replenishCount ?? 0,
    boostCount: payload?.summary.highPotentialCount ?? 0,
    markdownCount: payload?.summary.markdownCount ?? 0,
    doNotOrderCount: payload?.summary.doNotOrderCount ?? 0,
    fixDataCount: payload?.summary.badDataCount ?? 0,
    actionableCount: payload?.summary.actionableCount ?? 0,
    blockedCount: payload?.summary.blockedCount ?? 0,
    insufficientEvidenceCount: payload?.summary.insufficientEvidenceCount ?? 0,
    lostSalesEstimate: payload ? payload.summary.lostSalesEstimate ?? null : null,
    slowStockCapital: payload ? payload.summary.slowStockCapital ?? null : null,
    stockCoverRiskCount: payload?.summary.stockCoverRiskCount ?? 0,
    insufficientStockCoverageCount: payload?.summary.insufficientStockCoverageCount ?? 0,
    lowCoverSkus: payload?.summary.lowCoverCount ?? 0,
    slowStockSkus: payload?.summary.slowStockCount ?? 0,
    goodSellThroughSkus: payload?.summary.goodSellThroughCount ?? 0,
  }), [payload]);

  const trustQualitySummary = useMemo(() => {
    if (!rows.length) return undefined;
    let missingSupplierCount = 0;
    let missingCostCount = 0;
    let insufficientSignalCount = 0;
    for (const row of rows) {
      const codes = row.reasonCodes ?? [];
      if (codes.some((code) => code.toLowerCase() === "missing_supplier")) missingSupplierCount += 1;
      if (codes.some((code) => code.toLowerCase() === "missing_cost")) missingCostCount += 1;
      if (codes.some((code) => code.toLowerCase() === "insufficient_history")) insufficientSignalCount += 1;
    }
    return {
      missingSupplierCount,
      missingCostCount,
      insufficientSignalCount,
    };
  }, [rows]);

  const tableFilters = useMemo<AnalyticsNamedValue[]>(() => [
    { key: "fromDate", label: "Od datuma", value: fromDate },
    { key: "toDate", label: "Do datuma", value: toDate },
    { key: "storeId", label: "Prodavnica", value: storeId ?? "Sve" },
    { key: "supplierId", label: "Dobavljač", value: supplierId ?? "Svi" },
    { key: "recommendationFilter", label: "Preporuka", value: recommendationFilter },
    { key: "dataQualityFilter", label: "Kvalitet podataka", value: dataQualityFilter },
    { key: "search", label: "Pretraga", value: search || "-" },
  ], [dataQualityFilter, fromDate, recommendationFilter, search, storeId, supplierId, toDate]);

  const tableMetadata = useMemo<AnalyticsNamedValue[]>(() => [
    { key: "generatedAtUtc", label: "Generisano", value: payload?.generatedAtUtc ?? ANALYTICS_UNAVAILABLE_LABEL },
    { key: "returnedRows", label: "Vraćeno redova", value: payload?.totalRows ?? null },
    { key: "analyzedRows", label: "Analizirano redova", value: payload?.analyzedRows ?? null },
    { key: "hiddenRows", label: "Skriveno zbog limita", value: payload?.ignoredRowsCount ?? null },
    { key: "filteredRows", label: "Prikazano redova", value: sortedRows.length },
  ], [payload?.analyzedRows, payload?.generatedAtUtc, payload?.ignoredRowsCount, payload?.totalRows, sortedRows.length]);

  const populationSummary = payload
    ? payload.analyzedRows != null
      ? `Vraćeno ${payload.totalRows} redova; analizirano ${payload.analyzedRows}; ${payload.ignoredRowsCount != null ? `skriveno zbog limita ${payload.ignoredRowsCount}.` : "skriveni redovi nisu prijavljeni."} KPI brojači koriste vraćene redove, novčani KPI analiziranu populaciju, a dozvola za akciju dolazi iz backend-a.`
      : `Vraćeno ${payload.totalRows} redova; analizirana populacija nije prijavljena u ovom odgovoru.`
    : null;

  const actionabilitySummary = payload && typeof payload.summary.actionableCount === "number"
    ? `Akcione preporuke dozvoljene backend-om: ${payload.summary.actionableCount}; blokirano ili nedostupno: ${payload.summary.blockedCount ?? 0}; nedovoljno dokaza: ${payload.summary.insufficientEvidenceCount ?? 0}.`
    : null;

  const handlePeriodPresetChange = (value: PeriodPreset) => {
    setPeriodPreset(value);
    if (value === "custom") return;
    const range = applyPeriodPreset(value);
    setFromDate(range.fromDate);
    setToDate(range.toDate);
  };

  const setSort = (field: SortField) => {
    if (sortField === field) {
      setSortDir((prevDir) => (prevDir === "asc" ? "desc" : "asc"));
    } else {
      setSortDir("desc");
      setSortField(field);
    }
  };

  const loadDecisionTimeline = useCallback(async (row: ProductDecisionRow, familyMode: "row" | "all") => {
    const requestSeq = ++timelineRequestSeqRef.current;
    setTimelineLoadingProductId(row.productId);
    setTimelineError(null);
    setTimelineExportError(null);
    try {
      const timeline = await getProductDecisionTimeline({
        fromDate,
        toDate,
        sourceType: row.sourceType ?? "product",
        sourceKey: row.sourceKey ?? `product:${row.productId}`,
          productId: row.productId,
          recommendationType: familyMode === "row"
          ? (row.recommendationType ?? row.recommendationStatus)
          : null,
      });
      if (timelineRequestSeqRef.current !== requestSeq) {
        return;
      }
      setTimelineByProductId((current) => ({ ...current, [row.productId]: timeline }));
    } catch (err) {
      if (timelineRequestSeqRef.current !== requestSeq) {
        return;
      }
      setTimelineByProductId((current) => ({ ...current, [row.productId]: null }));
      setTimelineError(err instanceof Error ? err.message : "Decision Timeline nije dostupan.");
    } finally {
      if (timelineRequestSeqRef.current === requestSeq) {
        setTimelineLoadingProductId((current) => (current === row.productId ? null : current));
      }
    }
  }, [fromDate, toDate]);

  const exportDecisionTimeline = useCallback(async (row: ProductDecisionRow, familyMode: "row" | "all") => {
    setTimelineExportingProductId(row.productId);
    setTimelineExportError(null);
    try {
      const csv = await getProductDecisionTimelineExportCsv({
        fromDate,
        toDate,
        sourceType: row.sourceType ?? "product",
        sourceKey: row.sourceKey ?? `product:${row.productId}`,
        productId: row.productId,
        recommendationType: familyMode === "row"
          ? (row.recommendationType ?? row.recommendationStatus)
          : null,
      });
      downloadDecisionTimelineExportCsv(
        `decision-timeline-${row.productId}.csv`,
        csv,
      );
    } catch (err) {
      setTimelineExportError(err instanceof Error ? err.message : "Decision Timeline export trenutno nije dostupan.");
    } finally {
      setTimelineExportingProductId((current) => (current === row.productId ? null : current));
    }
  }, [fromDate, toDate]);

  const expandedTimelineRow = useMemo(() => {
    if (expandedProductId == null) {
      return null;
    }

    return sortedRows.find((row) => row.productId === expandedProductId) ?? null;
  }, [expandedProductId, sortedRows]);

  useEffect(() => {
    if (!expandedTimelineRow || expandedTimelineRow.whyPanel) return;
    let cancelled = false;
    void getProductDecisionCenter({
      fromDate,
      toDate,
      storeId,
      supplierId,
      dataScope,
      search: serverSearch || null,
      detailProductId: expandedTimelineRow.productId,
    }).then((response) => {
      const detail = response.rows.find((item) => item.productId === expandedTimelineRow.productId);
      if (cancelled || !detail) return;
      setPayload((current) => {
        if (!current) return current;
        const next = { ...current, rows: current.rows.map((item) => item.productId === detail.productId ? detail : item) };
        payloadRef.current = next;
        return next;
      });
    }).catch(() => {
      // The compact row remains usable; timeline and primary decision fields stay available.
    });
    return () => { cancelled = true; };
  }, [dataScope, expandedTimelineRow?.productId, expandedTimelineRow?.whyPanel, fromDate, serverSearch, storeId, supplierId, toDate]);

  useEffect(() => {
    if (!expandedTimelineRow) {
      return;
    }

    void loadDecisionTimeline(expandedTimelineRow, timelineFamilyFilter);
  }, [
    expandedTimelineRow?.productId,
    expandedTimelineRow?.sourceType,
    expandedTimelineRow?.sourceKey,
    expandedTimelineRow?.recommendationType,
    expandedTimelineRow?.recommendationStatus,
    loadDecisionTimeline,
    timelineFamilyFilter,
  ]);

  const toggleExpandedRow = useCallback((productId: number) => {
    setExpandedProductId((current) => (current === productId ? null : productId));
  }, []);

  const addRowToCentralActions = useCallback(async (row: ProductDecisionRow) => {
    const queueSpec = buildProductQueueSpec(row);
    const sourceKey = buildSourceKey(row, queueSpec.actionKind, fromDate, toDate, storeId, supplierId);
    const confidenceLevel = normalizeConfidenceLevel(row.confidenceLevel);
    const confidenceScore = resolveConfidenceScore(row);
    const warningCodes = resolveWarningCodes(row);
    const primaryDrivers = resolvePrimaryDrivers(row);
    const expectedImpactRsd = resolveExpectedImpactRsd(row);
    const inputFreshnessStatus = resolveInputFreshnessStatus(row);

    if (queueBusyKeyRef.current === sourceKey || queuedActionKeys?.has(sourceKey)) {
      return;
    }

    queueBusyKeyRef.current = sourceKey;
    setQueueBusyKey(sourceKey);
    setQueueMessage(null);
    try {
      const reasonText = row.explainabilityText ?? row.recommendationReason;

      const result = await upsertAnalyticsActionWithResult({
        sourceType: queueSpec.sourceType,
        sourceKey,
        sourceId: row.productId,
        title: queueSpec.title,
        description: reasonText,
        recommendationStatus: queueSpec.recommendationStatus,
        priority: queueSpec.priority,
        dueAtUtc: queueSpec.dueAtUtc,
        impactEstimateRsd: expectedImpactRsd ?? undefined,
        expectedImpactRsd: expectedImpactRsd ?? undefined,
        confidencePct: row.confidencePct,
        reliabilityPct: row.reliabilityPct ?? undefined,
        dataQualityStatus: toActionDataQualityStatus(row.dataQualityStatus),
        actionUrl: queueSpec.sourceType === "data_quality" ? "/analytics/data-quality" : "/analytics/products",
        sourceRecommendationId: row.recommendationId ?? undefined,
        recommendationType: row.recommendationType ?? row.recommendationStatus,
        expectedImpactBasis: "product_decision_center",
        impactWindowDays: row.impactWindowDays ?? undefined,
        confidenceLevel,
        confidenceScore: confidenceScore ?? undefined,
        warningCodes,
        primaryDrivers,
        reasonCodes: [...(row.reasonCodes ?? [])],
        decisionReason: reasonText ?? undefined,
        recommendedAction: row.recommendedAction,
        generatedAtUtc: payload?.generatedAtUtc ?? undefined,
        inputFreshnessStatus,
        explainabilityText: reasonText ?? undefined,
        periodFromUtc: fromDate,
        periodToUtc: toDate,
        evidenceChain: (row.evidenceChain ?? []).map((node) => ({
          category: node.category,
          code: node.code,
          label: node.label,
          valueText: node.valueText,
          sourceFields: [...node.sourceFields],
          isMissing: node.isMissing,
          detail: node.detail ?? null,
        })),
        confidenceBreakdown: (row.confidenceBreakdown ?? []).map((node) => ({
          category: node.category,
          code: node.code,
          label: node.label,
          valueText: node.valueText,
          sourceFields: [...node.sourceFields],
          isMissing: node.isMissing,
          detail: node.detail ?? null,
        })),
        metadataJson: JSON.stringify({
          productId: row.productId,
          sku: row.sku,
          supplierId: row.supplierId ?? null,
          actionKind: queueSpec.actionKind,
          recommendationStatus: row.recommendationStatus,
          stockCoverStatus: row.stockCoverStatus,
          sellThroughStatus: row.sellThroughStatus,
          stockCoverDays: row.stockCoverDays,
          sellThroughRatio: row.sellThroughRatio,
          recommendationAllowed: row.recommendationAllowed ?? null,
          recommendationId: row.recommendationId ?? null,
          sourceType: row.sourceType ?? null,
          sourceKey: row.sourceKey ?? null,
          recommendationType: row.recommendationType ?? null,
          confidenceLevel,
          confidenceScore,
          primaryDrivers,
          warningCodes,
          expectedImpactRsd,
          impactWindowDays: row.impactWindowDays ?? null,
          riskIfIgnored: row.riskIfIgnored ?? null,
          explainabilityText: reasonText,
          inputFreshnessStatus,
          periodFrom: fromDate,
          periodTo: toDate,
          storeId: storeId ?? "all",
          supplierFilterId: supplierId ?? "all",
        }),
      });

      setQueuedActionKeys((prev) => {
        const next = new Set(prev);
        next.add(sourceKey);
        if (result.sourceKey) next.add(result.sourceKey);
        if (result.item.sourceKey) next.add(result.item.sourceKey);
        return next;
      });
      const capturedEvidence = result.item.ledgerSnapshot?.evidenceSnapshot;
      setEvidenceSnapshotByProductId((prev) => ({
        ...prev,
        [row.productId]: capturedEvidence
          ? {
              capturedAtUtc: capturedEvidence.capturedAtUtc,
              recommendationId: capturedEvidence.recommendationId,
            }
          : null,
      }));
      setQueueMessage(result.existing
        ? "Akcija je već u centralnim akcijama."
        : capturedEvidence
          ? "Akcija je dodata i evidence snapshot je snimljen."
          : "Akcija je dodata u centralni red.");
    } catch (reason) {
      setQueueMessage(getAnalyticsActionWriteErrorMessage(reason));
    } finally {
      queueBusyKeyRef.current = null;
      setQueueBusyKey(null);
    }
  }, [fromDate, queuedActionKeys, storeId, supplierId, toDate]);

  return (
    <section className="product-decision-page">
      <AnalyticsTrustHeader
        title="Odluke o proizvodima"
        description="Pregled preporuka za dopunu, pojačanje, cenu, praćenje i proveru podataka po artiklu."
        periodFrom={payload?.periodFromUtc ?? fromDate}
        periodTo={payload?.periodToUtc ?? toDate}
        observedPeriodTo={responseMeta?.observedPeriodToUtc ?? null}
        lastRefreshAt={responseMeta?.lastRefreshAtUtc ?? null}
        dataSource="Pregled odluka o proizvodima"
        dataQualityStatus={responseMeta?.dataQualityStatus ?? null}
        dataQualitySummary={trustQualitySummary}
        mode="recommendation"
        isPartial={isAnalyticsMetaWarning(responseMeta)}
        recommendationNote="Finalni status preporuke dolazi iz backend sistema za odlučivanje."
        emptyStateReason={!loading && !hasBlockingError && sortedRows.length === 0 ? (responseMetaMessage ?? "Nema kandidata za izabrane filtere i period.") : null}
        methodologyHref="/analytics/data-quality"
        dataQualityHref="/analytics/data-quality"
        refreshStatusHref="/admin/configuration?panel=workers"
        compact
      />

      {responseMeta?.comparisonUnavailableReasonCode === "beyond_source_horizon" ? (
        <div className="product-decision-message product-decision-message-info" role="status">
          Poređenje nije dostupno: izabrani period prelazi poslednji opaženi datum prodaje. Nema podataka posle prikazanog horizonta.
        </div>
      ) : null}

      {showMetaWarning ? (
        <div className="product-decision-message product-decision-message-info" role="status">
          Prikazani podaci su delimični ili fallback. {responseMetaMessage ?? "Proverite status osvežavanja analitike."}
        </div>
      ) : null}

      {populationSummary ? (
        <div className="product-decision-message product-decision-message-info" role="status">
          {populationSummary}
        </div>
      ) : null}
      {actionabilitySummary ? (
        <div className="product-decision-message product-decision-message-info" role="status">
          {actionabilitySummary}
        </div>
      ) : null}

      <header className="product-decision-header">
        <div>
          <h1>Odluke o proizvodima</h1>
          <p>{PRODUCT_DECISION_PAGE_EXPLANATION}</p>
        </div>
        <AnalyticsTableToolbar
          tableKey="product-decision-center"
          tableTitle="Odluke o proizvodima"
          columns={TABLE_COLUMNS}
          rows={sortedRows}
          filters={tableFilters}
          metadata={tableMetadata}
        />
      </header>

      {!hideKpiChrome ? (
        <section className="product-decision-kpis" aria-label="KPI kartice">
        <article className="kpi-card">
          <span>Za dopunu</span>
          <strong>{fmtNumber(kpis.replenishCount, 0, "0")}</strong>
          <KpiExplainButton metricKey="replenishCount" ariaLabel="Kako je izračunat broj proizvoda za dopunu" />
        </article>
        <article className="kpi-card">
          <span>Za pojačanje</span>
          <strong>{fmtNumber(kpis.boostCount, 0, "0")}</strong>
          <KpiExplainButton metricKey="boostCount" ariaLabel="Kako je izračunat broj proizvoda za pojačanje" />
        </article>
        <article className="kpi-card">
          <span>Za sniženje</span>
          <strong>{fmtNumber(kpis.markdownCount, 0, "0")}</strong>
          <KpiExplainButton metricKey="markdownCount" ariaLabel="Kako je izračunat broj proizvoda za sniženje" />
        </article>
        <article className="kpi-card">
          <span>Ne naručivati</span>
          <strong>{fmtNumber(kpis.doNotOrderCount, 0, "0")}</strong>
          <KpiExplainButton metricKey="doNotOrderCount" ariaLabel="Kako je izračunat broj proizvoda koje ne treba naručivati" />
        </article>
        <article className="kpi-card">
          <span>Za ispravku podataka</span>
          <strong>{fmtNumber(kpis.fixDataCount, 0, "0")}</strong>
          <KpiExplainButton metricKey="fixDataCount" ariaLabel="Kako je izračunat broj proizvoda za proveru podataka" />
        </article>
        <article className="kpi-card">
          <span>Procena izgubljene prodaje</span>
          <strong>{fmtRsd(kpis.lostSalesEstimate, 0, ANALYTICS_UNAVAILABLE_LABEL)}</strong>
          <KpiExplainButton metricKey="lostSalesEstimate" ariaLabel="Kako je izračunata procena izgubljene prodaje" />
        </article>
        <article className="kpi-card">
          <span>Kapital u sporoj zalihi</span>
          <strong>{fmtRsd(kpis.slowStockCapital, 0, ANALYTICS_UNAVAILABLE_LABEL)}</strong>
          <KpiExplainButton metricKey="slowStockCapital" ariaLabel="Kako je izračunat kapital u sporoj zalihi" />
        </article>
        <article className="kpi-card">
          <span>Rizik pokrivenosti</span>
          <strong>{fmtNumber(kpis.stockCoverRiskCount, 0, "0")}</strong>
          <KpiExplainButton metricKey="stockCoverDays" ariaLabel="Kako je izračunat broj artikala sa rizičnom pokrivenošću zalihe" />
        </article>
        <article className="kpi-card">
          <span>Nedovoljno podataka za pokrivenost</span>
          <strong>{fmtNumber(kpis.insufficientStockCoverageCount, 0, "0")}</strong>
          <KpiExplainButton metricKey="stockCoverDays" ariaLabel="Kako je izračunat broj artikala bez dovoljno podataka za pokrivenost zalihe" />
        </article>
        <article className="kpi-card">
          <span>SKU sa niskom pokrivenošću</span>
          <strong>{fmtNumber(kpis.lowCoverSkus, 0, "0")}</strong>
          <KpiExplainButton metricKey="stockCoverDays" ariaLabel="Kako je izračunat broj artikala sa niskom pokrivenošću" />
        </article>
        <article className="kpi-card">
          <span>SKU sa sporim obrtom</span>
          <strong>{fmtNumber(kpis.slowStockSkus, 0, "0")}</strong>
          <KpiExplainButton metricKey="stockCoverDays" ariaLabel="Kako je izračunat broj artikala sa sporim obrtom" />
        </article>
        <article className="kpi-card">
          <span>SKU sa dobrom prodajnošću</span>
          <strong>{fmtNumber(kpis.goodSellThroughSkus, 0, "0")}</strong>
          <KpiExplainButton metricKey="sellThrough" ariaLabel="Kako je izračunat broj artikala sa dobrom prodajnošću" />
        </article>
        </section>
      ) : null}

      <section className="product-decision-filters" aria-label="Filteri perioda i opsega">
        <div className="filter-grid">
          <label>
            Period
            <select value={periodPreset} onChange={(event) => handlePeriodPresetChange(event.target.value as PeriodPreset)}>
              <option value="last30">Poslednjih 30 dana</option>
              <option value="last60">Poslednjih 60 dana</option>
              <option value="last90">Poslednjih 90 dana</option>
              <option value="custom">Prilagođeni period</option>
            </select>
          </label>
          <label>
            Od datuma
            <input
              type="date"
              aria-label="Od datuma"
              value={fromDate}
              onChange={(event) => {
                setFromDate(event.target.value);
                setPeriodPreset("custom");
              }}
            />
            <span className="product-decision-filter-period-note">Prikaz: {formatDateInputForDisplay(fromDate)}</span>
          </label>
          <label>
            Do datuma
            <input
              type="date"
              aria-label="Do datuma"
              value={toDate}
              onChange={(event) => {
                setToDate(event.target.value);
                setPeriodPreset("custom");
              }}
            />
            <span className="product-decision-filter-period-note">Prikaz: {formatDateInputForDisplay(toDate)}</span>
          </label>
          <label>
            Prodavnica
            <select value={storeId ?? ""} onChange={(event) => setStoreId(event.target.value ? Number(event.target.value) : null)}>
              <option value="">Sve prodavnice</option>
              {stores.map((store) => (
                <option key={store.storeId} value={store.storeId}>
                  {store.storeName}
                </option>
              ))}
            </select>
          </label>
          <label>
            Dobavljač
            {supplierFiltersStale ? <span className="product-decision-message product-decision-message-info" style={{ marginLeft: "0.5rem" }}>Zastarela lista</span> : null}
            <select
              value={supplierId ?? ""}
              onChange={(event) => setSupplierId(event.target.value ? Number(event.target.value) : null)}
              disabled={supplierFiltersStale}
              aria-invalid={supplierFiltersStale || undefined}
            >
              <option value="">{supplierFiltersStale ? "Izbor je privremeno blokiran" : "Svi dobavljači"}</option>
              {suppliers.map((supplier) => (
                <option key={supplier.supplierId} value={supplier.supplierId} disabled={supplierFiltersStale}>
                  {supplier.supplierName}
                </option>
              ))}
            </select>
            {supplierFiltersWarning ? (
              <p className="product-decision-message product-decision-message-info" style={{ marginTop: "0.5rem" }} role="status">
                {supplierFiltersWarning}
                {supplierFiltersStale ? ` ${SUPPLIER_FILTER_STALE_LIST_MESSAGE}` : ""}
              </p>
            ) : null}
          </label>
          <label>
            Preporuka
            <select value={recommendationFilter} onChange={(event) => setRecommendationFilter(event.target.value as RecommendationFilter)}>
              {RECOMMENDATION_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>
          <label>
            Kvalitet podataka
            <select value={dataQualityFilter} onChange={(event) => setDataQualityFilter(event.target.value as DataQualityFilter)}>
              <option value="all">Sve</option>
              <option value="good">Dobar</option>
              <option value="warning">Upozorenje</option>
              <option value="critical">Kritičan</option>
              <option value="insufficient_data">Nedovoljno podataka</option>
            </select>
          </label>
          <label>
            Sortiranje
            <select value={`${sortField}:${sortDir}`} onChange={(event) => {
              const [nextField, nextDir] = event.target.value.split(":");
              setSortField(nextField as SortField);
              setSortDir(nextDir as SortDir);
            }}>
              <option value="recommendationStatus:desc">Preporuka (prioritet)</option>
              <option value="confidencePct:desc">Sigurnost preporuke opadajuće</option>
              <option value="revenue:desc">Promet opadajuće</option>
              <option value="velocityUnitsPerDay:desc">{ANALYTICS_VELOCITY_LABEL} opadajuće</option>
              <option value="stockCoverDays:asc">Pokrivenost zalihe rastuće</option>
              <option value="sellThroughRatio:desc">Prodajnost opadajuće</option>
              <option value="trendPct:desc">Trend opadajuće</option>
              <option value="dataQualityStatus:desc">Kvalitet podataka (kritično prvo)</option>
              <option value="productName:asc">Artikal A-Z</option>
            </select>
          </label>
          <label>
            Pretraga (naziv/PLU)
            <input
              type="search"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="npr. Air, 45123..."
            />
          </label>
        </div>
      </section>

      {showMetaWarning ? (
        <div className="product-decision-message product-decision-message-info" role="status">
          Prikazani podaci su delimični ili fallback. {responseMetaMessage ?? "Proverite status osvežavanja analitike."}
        </div>
      ) : null}

      {queueMessage ? <div className="product-decision-message product-decision-message-info">{queueMessage}</div> : null}
      {staleWarning ? <div className="product-decision-message product-decision-message-info">{staleWarning}</div> : null}
      {actionStatusWarning ? (
        <div className="product-decision-message product-decision-message-info" role="status">
          <strong>{actionStatusWarning.message}</strong>
        </div>
      ) : null}
      {!hasBlockingError && error ? (
        <div className="product-decision-message product-decision-message-info">
          Prikazujemo prethodno učitane podatke. Novi upit nije uspeo.
        </div>
      ) : null}
      {loading ? <div className="product-decision-message">Učitavanje podataka za Odluke o proizvodima...</div> : null}
      {hasBlockingError ? (
        <AnalyticsErrorState
          title="Podaci trenutno nisu dostupni"
          message={error?.message ?? "Ne prikazujemo nule jer nije potvrđeno da je period stvarno prazan."}
          errorCode={error?.errorCode ?? undefined}
          correlationId={error?.correlationId ?? undefined}
          onRetry={() => {
            void loadData();
          }}
          helpHref="/analytics/data-quality"
        />
      ) : null}

      {showInsufficientState ? (
        <AnalyticsEmptyState
          variant="insufficient_data"
          message="Ne prikazujemo automatsku preporuku jer signal nije dovoljno jak."
          reasons={[
            "U periodu nema dovoljno prodajnih događaja za signal preporuke.",
            "Filteri su previše uski (prodavnica/dobavljač).",
            "Nedostaju ključni ulazi (nabavna cena, dobavljač).",
          ]}
          actions={[
            { label: "Proširite period (npr. 60 ili 90 dana)." },
            { label: "Uklonite uske filtere i pokušajte ponovo." },
            { label: "Otvorite Kvalitet podataka i proverite blokere signala.", href: "/analytics/data-quality" },
          ]}
          dataQualityHref="/analytics/data-quality"
          refreshStatusHref="/admin/configuration?panel=workers"
          emptyReason={responseMeta?.emptyReason ?? responseMetaMessage ?? null}
          onRetry={() => {
            void loadData();
          }}
        />
      ) : null}

      {showNoDataState ? (
        <AnalyticsEmptyState
          variant="no_data"
          message={responseMetaMessage ?? "Nema podataka za izabrani period."}
          reasons={[
            "Izabrani period je preuzak.",
            "Nije bilo prodaje u traženom periodu.",
            "Osvežavanje analitike još nije završeno.",
          ]}
          dataQualityHref="/analytics/data-quality"
          refreshStatusHref="/admin/configuration?panel=workers"
          emptyReason={responseMeta?.emptyReason ?? responseMetaMessage ?? null}
        />
      ) : null}

      {showFilteredOutState ? (
        <AnalyticsEmptyState
          variant="filtered_out"
          message="Promenite filtere ili proširite period."
          reasons={[
            "Pretraga, filter preporuke ili filter kvaliteta podataka su previše restriktivni.",
            "Kombinacija prodavnice i dobavljača trenutno nema kandidate.",
          ]}
          dataQualityHref="/analytics/data-quality"
          refreshStatusHref="/admin/configuration?panel=workers"
          onRetry={() => {
            void loadData();
          }}
        />
      ) : null}

      {!loading && !hasBlockingError && sortedRows.length > 0 ? (
        <>
        <p className="product-decision-table-scroll-hint" id="product-decision-table-scroll-hint">
          Na užem ekranu prevucite tabelu horizontalno da pregledate sve kolone.
        </p>
        <div
          className="product-decision-table-wrap"
          role="region"
          aria-label="Odluke o proizvodima; pomerajte horizontalno da pregledate sve kolone"
          aria-describedby="product-decision-table-scroll-hint"
          tabIndex={0}
        >
          <table className="product-decision-table">
            <thead>
              <tr>
                <th onClick={() => setSort("productName")}>Artikal</th>
                <th onClick={() => setSort("supplierName")}>Dobavljač</th>
                <th onClick={() => setSort("revenue")}>Prodaja / komadi</th>
                <th onClick={() => setSort("velocityUnitsPerDay")}>{ANALYTICS_VELOCITY_LABEL}</th>
                <th onClick={() => setSort("marginPct")}>Marža</th>
                <th onClick={() => setSort("currentStock")}>Zaliha</th>
                <th onClick={() => setSort("trendPct")}>Trend</th>
                <th onClick={() => setSort("stockCoverDays")}>Pokrivenost zalihe</th>
                <th onClick={() => setSort("sellThroughRatio")}>Prodajnost</th>
                <th onClick={() => setSort("confidencePct")}>Sigurnost preporuke</th>
                <th onClick={() => setSort("dataQualityStatus")}>Kvalitet podataka</th>
                <th onClick={() => setSort("recommendationStatus")}>Preporuka</th>
                <th>Akcija</th>
              </tr>
            </thead>
            <tbody>
              {visibleRows.map((row) => {
                  const expanded = expandedProductId === row.productId;
                  const queueSpec = buildProductQueueSpec(row);
                  const sourceKey = buildSourceKey(row, queueSpec.actionKind, fromDate, toDate, storeId, supplierId);
                  const isQueued = queuedActionKeys?.has(sourceKey) ?? false;
                  const isQueueBusy = queueBusyKey === sourceKey;
                  const actionStatusKnown = queuedActionKeys != null;
                  const whyPanel = buildProductDecisionWhyPanel(row);
                  const dataQuality = canonicalDataQualityStatus(whyPanel.dataQualityStatus);
                  const confidenceLevel = normalizeConfidenceLevel(whyPanel.confidenceLevel);
                  const confidenceScore = whyPanel.confidenceScore ?? null;
                  const decisionBlocked = isDecisionBlocked(whyPanel);
                  const warningCodes = whyPanel.warningCodes;
                  const primaryDrivers = whyPanel.primaryDrivers;
                  const expectedImpactRsd = whyPanel.expectedImpactRsd ?? null;
                  const inputFreshnessStatus = normalizeInputFreshnessStatus(whyPanel.inputFreshnessStatus);
                  const reasonCodeItems = whyPanel.reasonCodes.length
                    ? whyPanel.reasonCodes.map((code) => ({ code, message: translateReasonCode(code) }))
                    : null;
                  const warningCodeItems = warningCodes.length
                    ? warningCodes.map((code) => ({ code, message: translateReasonCode(code) }))
                    : null;
                  const warningSummaryCodeItems = warningCodeItems ? warningSummaryItems(warningCodeItems) : null;
                  const primaryDriverItems = primaryDrivers.length
                    ? primaryDrivers.map((driver) => ({ code: driver, label: primaryDriverLabel(driver) }))
                    : null;
                  const confidenceBreakdownItems: ProductDecisionEvidenceNode[] = whyPanel.confidenceBreakdown ?? [];
                  const alternativeRecommendationsItems: ProductDecisionAlternativeRecommendation[] = whyPanel.alternativeRecommendations ?? [];
                  const evidenceChainItems: ProductDecisionEvidenceNode[] = whyPanel.evidenceChain ?? [];
                  const decisionTreeItems: ProductDecisionDecisionTreeNode[] = whyPanel.decisionTree ?? [];
                  const supplierUrl = row.supplierId != null ? buildSupplierDecisionUrl(row.supplierId) : null;
                  const inventoryUrl = (row.productId > 0 || row.sku) ? buildInventoryDecisionUrl(row) : null;

                  return (
                    <Fragment key={`${row.productId}:${row.recommendationStatus}`}>
                      <tr className="data-row" onClick={() => toggleExpandedRow(row.productId)} title="Klik za detalje preporuke.">
                        <td>
                          <strong>{row.productName}</strong>
                          <small>{row.sku || ANALYTICS_UNAVAILABLE_LABEL} | {row.category ?? row.tipObuce ?? ANALYTICS_UNAVAILABLE_LABEL}</small>
                        </td>
                        <td>{row.supplierName ?? ANALYTICS_UNAVAILABLE_LABEL}</td>
                        <td>
                          <span>{fmtRsd(row.revenue, 0, ANALYTICS_UNAVAILABLE_LABEL)}</span>
                          <small>{fmtNumber(row.unitsSold, 0, "0")} kom</small>
                        </td>
                        <td>{fmtNumber(row.velocityUnitsPerDay, 2, ANALYTICS_UNAVAILABLE_LABEL)}</td>
                        <td>
                          <span>{fmtPct(row.marginPct, 1)}</span>
                          <small>{row.marginQualityLabel ?? "Nedovoljno podataka"} | pokriće: {fmtPct(row.marginCoveragePct, 1, "Nije dostupno")}</small>
                        </td>
                        <td>
                          <span>{fmtNumber(row.currentStock, 0, "Nije dostupno")}</span>
                          <small>min: {fmtNumber(row.minStock, 0, "Nije dostupno")} | gap: {fmtNumber(row.stockGap, 0, "Nije dostupan")}</small>
                        </td>
                        <td>{fmtPct(row.trendPct, 1)}</td>
                        <td>
                          <span>{formatSignalMetricValue(row.stockCoverDays, row.stockCoverStatus, "days")}</span>
                          <small>{row.stockCoverStatusLabel ?? stockCoverStatusLabel(row.stockCoverStatus)}</small>
                        </td>
                        <td>
                          <span>{formatSignalMetricValue(row.sellThroughRatio, row.sellThroughStatus, "ratio")}</span>
                          <small>{row.sellThroughStatusLabel ?? sellThroughStatusLabel(row.sellThroughStatus)}</small>
                        </td>
                        <td>
                          <span className={`${confidenceLevelClass(confidenceLevel)}${decisionBlocked ? " confidence-pill-blocked" : ""}`}>
                            {decisionBlocked ? "Blokirano — nedovoljno dokaza" : confidenceScoreText(confidenceLevel, confidenceScore)}
                          </span>
                          <small title="Pouzdanost signala meri stabilnost i pokrivenost ulaza; ne znači da je preporuka spremna za akciju.">
                            Pouzdanost signala: {whyPanel.reliabilityPct != null ? `${fmtNumber(whyPanel.reliabilityPct, 0, ANALYTICS_UNAVAILABLE_LABEL)}%` : ANALYTICS_UNAVAILABLE_LABEL}
                          </small>
                        </td>
                        <td>
                          <span className={dataQualityClass(dataQuality)}>{DATA_QUALITY_LABELS[dataQuality]}</span>
                        </td>
                        <td>
                          <span className={recommendationToneClass(row.recommendationStatus)}>
                            {displayRecommendationLabel(row)}
                          </span>
                          {decisionBlocked ? (
                            <div className="decision-gate decision-gate-blocked" data-testid="decision-gate" role="status">
                              <strong>Preporuka blokirana — nije greška u izračunu</strong>
                              <span>{whyPanel.explainabilityText ?? whyPanel.recommendationReason ?? "Nema dovoljno dokaza za sigurnu odluku."}</span>
                              <small>Sledeći korak: {blockedDecisionNextStep(whyPanel)}</small>
                            </div>
                          ) : null}
                          {warningSummaryCodeItems?.length ? (
                            <small className="recommendation-warning-summary">
                              Upozorenja: {warningSummaryCodeItems.map((item) => item.message).join(" · ")}
                            </small>
                          ) : null}
                          <button
                            type="button"
                            className="why-button"
                            onClick={(event) => {
                              event.stopPropagation();
                              toggleExpandedRow(row.productId);
                            }}
                            title={whyPanel.explainabilityText ?? whyPanel.recommendationReason}
                          >
                            Zašto?
                          </button>
                        </td>
                        <td>
                          <span>{row.recommendedAction}</span>
                          <small>{expectedImpactRsd != null
                            ? `Procena uticaja: ${fmtRsd(expectedImpactRsd, 0, ANALYTICS_UNAVAILABLE_LABEL)}`
                            : decisionBlocked
                              ? "Uticaj se ne procenjuje dok je preporuka blokirana."
                              : "Procena uticaja nije dostupna."}</small>
                          {expectedImpactRsd == null && !decisionBlocked ? (
                            <small className="recommendation-warning-summary">Upozorenje: nedostaje ulaz za procenu uticaja.</small>
                          ) : null}
                          <button
                            type="button"
                            className={`btn-add-to-queue${isQueued ? " added" : ""}`}
                            onClick={(event) => {
                              event.stopPropagation();
                              void addRowToCentralActions(row);
                            }}
                            disabled={isQueueBusy || isQueued}
                            title={isQueued
                              ? "Akcija je već u centralnom redu."
                              : actionStatusKnown
                                ? "Dodaj u centralni red akcija"
                                : "Dodaj u centralni red akcija. Status postojećih akcija trenutno nije dostupan."}
                          >
                            {isQueueBusy
                              ? "Dodavanje..."
                              : isQueued
                                ? "U akcijama"
                                : queueSpec.recommendationStatus === "SIGNAL_REVIEW"
                                  ? "Dodaj u proveru"
                                  : "Dodaj u akcije"}
                          </button>
                        </td>
                      </tr>
                      {expanded ? (
                        <tr className="reason-row">
                          <td colSpan={13}>
                            <div className="reason-content reason-content-expanded">
                              <div className="reason-headline">
                                <div>
                                  <h4>{row.productName}</h4>
                                  <p>{row.supplierName ?? "Dobavljač nije dodeljen"}</p>
                                </div>
                                <div className="reason-statuses">
                                  <span className={recommendationToneClass(row.recommendationStatus)}>
                                    {displayRecommendationLabel(row)}
                                  </span>
                                  <span className={dataQualityClass(dataQuality)}>
                                    {DATA_QUALITY_LABELS[dataQuality]}
                                  </span>
                                  <span className={confidenceLevelClass(confidenceLevel)}>{confidenceScoreText(confidenceLevel, confidenceScore)}</span>
                                  <span className="confidence-badge">Svežina ulaza: {inputFreshnessLabel(inputFreshnessStatus)}</span>
                                  <span className="confidence-badge">{whyPanelSummarySourceLabel(whyPanel.summarySource)}</span>
                                  {whyPanel.summaryFallbackUsed ? (
                                    <span className="confidence-badge">{whyPanelFallbackLabel(whyPanel.summaryFallbackReason)}</span>
                                  ) : null}
                                </div>
                              </div>

                              <div className="reason-block">
                                <strong>Zašto ova preporuka?</strong> {whyPanel.explainabilityText ?? whyPanel.recommendationReason ?? "Objašnjenje nije dostupno."}
                              </div>

                              <div className="reason-block">
                                <strong>Životni ciklus preporuke:</strong>{" "}
                                {lifecycleStateLabel(row.lifecycleState ?? whyPanel.lifecycleState ?? row.recommendationLifecycle?.lifecycleState)}
                                <small>
                                  Ishod za učenje:{" "}
                                  {(row.learningEligible ?? whyPanel.learningEligible ?? row.recommendationLifecycle?.learningEligible)
                                    ? "Mereno — može u statistiku"
                                    : "Nije mereno — prihvatanje nije uspeh"}
                                </small>
                              </div>

                              <div className="reason-block" data-testid="decision-evidence-snapshot">
                                <strong>Snimak dokaza:</strong>{" "}
                                {evidenceSnapshotByProductId[row.productId]
                                  ? `Snimljen ${formatDateTime(evidenceSnapshotByProductId[row.productId]!.capturedAtUtc, "Nije dostupno")}`
                                  : row.evidenceSnapshotStatus === "available"
                                    ? "Dostupan"
                                    : "Nije snimljen — snapshot se zamrzava tek kada preporuka uđe u akcije"}
                                {row.evidenceSnapshotPreview ? (
                                  <small>
                                    Pregled: {recommendationTypeLabel(row.evidenceSnapshotPreview.recommendationType)}
                                    {" · "}
                                    {formatDateTime(row.evidenceSnapshotPreview.periodFromUtc, "Nije dostupno")} – {formatDateTime(row.evidenceSnapshotPreview.periodToUtc, "Nije dostupno")}
                                  </small>
                                ) : null}
                              </div>

                              <div className="reason-block" data-testid="decision-timeline-panel">
                                <strong>Istorija odluke:</strong>
                                <div className="reason-statuses" style={{ marginTop: "0.5rem" }}>
                                  <button
                                    type="button"
                                    className="why-button"
                                    onClick={(event) => {
                                      event.stopPropagation();
                                      setTimelineFamilyFilter("row");
                                    }}
                                  >
                                    Porodica reda
                                  </button>
                                  <button
                                    type="button"
                                    className="why-button"
                                    onClick={(event) => {
                                      event.stopPropagation();
                                      setTimelineFamilyFilter("all");
                                    }}
                                  >
                                    Sve porodice
                                  </button>
                                  <button
                                    type="button"
                                    className="why-button"
                                    data-testid="decision-timeline-export"
                                    disabled={timelineExportingProductId === row.productId}
                                    onClick={(event) => {
                                      event.stopPropagation();
                                      void exportDecisionTimeline(row, timelineFamilyFilter);
                                    }}
                                  >
                                    {timelineExportingProductId === row.productId ? "Izvoz…" : "Preuzmi CSV"}
                                  </button>
                                </div>
                                {timelineLoadingProductId === row.productId ? (
                                  <small>Učitavanje istorije odluke…</small>
                                ) : null}
                                {timelineError && expandedProductId === row.productId ? (
                                  <small className="recommendation-warning-summary">{timelineError}</small>
                                ) : null}
                                {timelineExportError && expandedProductId === row.productId ? (
                                  <small className="recommendation-warning-summary" data-testid="decision-timeline-export-error">
                                    {timelineExportError}
                                  </small>
                                ) : null}
                                {(() => {
                                  const timeline = timelineByProductId[row.productId];
                                  if (!timeline) {
                                    return <small>Istorija još nije učitana za ovaj entitet.</small>;
                                  }
                                  return (
                                    <>
                                      <small data-testid="decision-timeline-scope">
                                        {timelineScopeLabel(
                                          timeline.scope,
                                          row.sourceKey ?? `product:${row.productId}`,
                                          fromDate,
                                          toDate,
                                        )}
                                      </small>
                                      {timeline.emptyReason ? (
                                        <small data-testid="decision-timeline-empty">
                                          {timelineEmptyReasonLabel(timeline.emptyReason)}
                                        </small>
                                      ) : (
                                        <ol className="confidence-breakdown-list">
                                          {timeline.timelines.map((item) => (
                                            <li key={item.timelineId}>
                                              <div className="evidence-chain-headline">
                                                <span className="evidence-chain-label">
                                                  {recommendationTypeLabel(item.recommendationType)}
                                                </span>
                                              </div>
                                              <small>
                                                Događaji: {item.events.map((eventItem) => timelineEventTypeLabel(eventItem.eventType)).join(" → ") || "nema"}
                                              </small>
                                              {item.gaps.length ? (
                                                <small className="recommendation-warning-summary">
                                                  Praznine: {item.gaps.map((gap) => timelineGapReasonLabel(gap.gapReason)).join(", ")}
                                                </small>
                                              ) : null}
                                            </li>
                                          ))}
                                        </ol>
                                      )}
                                    </>
                                  );
                                })()}
                              </div>

                              <div className="reason-block">
                                <strong>Raspodela pouzdanosti:</strong>
                                {confidenceBreakdownItems.length ? (
                                  <ol className="confidence-breakdown-list">
                                    {confidenceBreakdownItems.map((item) => (
                                      <li key={item.code} className={`confidence-breakdown-item confidence-breakdown-${item.category}`}>
                                        <div className="evidence-chain-headline">
                                          <span className="evidence-chain-category">{evidenceCategoryLabel(item.category)}</span>
                                          <span className="evidence-chain-label">{item.label}</span>
                                        </div>
                                        <span className={`evidence-chain-value${item.isMissing ? " evidence-chain-missing" : ""}`}>{item.valueText}</span>
                                        {item.detail ? <small>{item.detail}</small> : null}
                                        <small className="evidence-chain-source">Izvor: {item.sourceFields.join(" · ")}</small>
                                      </li>
                                    ))}
                                  </ol>
                                ) : (
                                  <span>Raspodela pouzdanosti nije dostupna.</span>
                                )}
                              </div>

                              <div className="reason-block">
                                <strong>Alternativne preporuke:</strong>
                                {alternativeRecommendationsItems.length ? (
                                  <ol className="alternative-recommendations-list">
                                    {alternativeRecommendationsItems.map((item) => {
                                      const itemConfidenceLevel = normalizeConfidenceLevel(item.confidenceLevel);
                                      const itemDataQuality = canonicalDataQualityStatus(item.dataQualityStatus);
                                      return (
                                        <li key={`${item.rank}:${item.recommendationStatus}`} className="alternative-recommendation-item">
                                          <div className="evidence-chain-headline">
                                            <span className="evidence-chain-category">Alternativa {item.rank}</span>
                                            <span className="evidence-chain-label">{item.recommendationLabel}</span>
                                          </div>
                                          <div className="reason-statuses">
                                            <span className={recommendationToneClass(item.recommendationStatus)}>
                                              {item.recommendationLabel}
                                            </span>
                                            <span className={confidenceLevelClass(itemConfidenceLevel)}>
                                              {confidenceScoreText(itemConfidenceLevel, item.confidenceScore)}
                                            </span>
                                            <span className={dataQualityClass(itemDataQuality)}>
                                              {DATA_QUALITY_LABELS[itemDataQuality]}
                                            </span>
                                          </div>
                                          <span className="evidence-chain-value">{item.recommendedAction}</span>
                                          <small>{item.reason}</small>
                                          <small className="evidence-chain-source">Zašto niže: {item.whyLowerRanked}</small>
                                          {item.reasonCodes.length ? (
                                            <ul className="reason-chip-list">
                                              {item.reasonCodes.map((code) => (
                                                <li key={code} className="reason-chip">
                                                  {translateReasonCode(code)}
                                                </li>
                                              ))}
                                            </ul>
                                          ) : null}
                                        </li>
                                      );
                                    })}
                                  </ol>
                                ) : (
                                  <span>Alternativne preporuke nisu dostupne.</span>
                                )}
                              </div>

                              <div className="reason-block">
                                <strong>Put odluke:</strong>
                                {decisionTreeItems.length ? (
                                  <ol className="decision-tree-list">
                                    {decisionTreeItems.map((item) => (
                                      <li
                                        key={item.code}
                                        className={`decision-tree-item decision-tree-${item.category}${item.isSelected ? " decision-tree-selected" : " decision-tree-rejected"}`}
                                      >
                                        <div className="evidence-chain-headline">
                                          <span className="evidence-chain-category">{decisionTreeCategoryLabel(item.category)}</span>
                                          <span className="evidence-chain-label">{item.label}</span>
                                          <span className="decision-tree-state">{item.isSelected ? "izabrana grana" : "sporedna grana"}</span>
                                        </div>
                                        <span className="evidence-chain-value">{item.valueText}</span>
                                        {item.detail ? <small>{item.detail}</small> : null}
                                        <small className="evidence-chain-source">Izvor: {item.sourceFields.join(" · ")}</small>
                                      </li>
                                    ))}
                                  </ol>
                                ) : (
                                  <span>Put odluke nije dostupan.</span>
                                )}
                              </div>

                              <div className="reason-block">
                                <strong>Lanac dokaza:</strong>
                                {evidenceChainItems.length ? (
                                  <ol className="evidence-chain-list">
                                    {evidenceChainItems.map((item) => (
                                      <li key={item.code} className={`evidence-chain-item evidence-chain-${item.category}`}>
                                        <div className="evidence-chain-headline">
                                          <span className="evidence-chain-category">{evidenceCategoryLabel(item.category)}</span>
                                          <span className="evidence-chain-label">{item.label}</span>
                                        </div>
                                        <span className={`evidence-chain-value${item.isMissing ? " evidence-chain-missing" : ""}`}>{item.valueText}</span>
                                        {item.detail ? <small>{item.detail}</small> : null}
                                        <small className="evidence-chain-source">Izvor: {item.sourceFields.join(" · ")}</small>
                                      </li>
                                    ))}
                                  </ol>
                                ) : (
                                  <span>Lanac dokaza nije dostupan.</span>
                                )}
                              </div>

                              <div className="reason-block">
                                <strong>Glavni pokretači:</strong>
                                {primaryDriverItems?.length ? (
                                  <ul className="reason-chip-list">
                                    {primaryDriverItems.map((item) => (
                                      <li key={item.code} className="reason-chip">
                                        {item.label}
                                      </li>
                                    ))}
                                  </ul>
                                ) : (
                                  <span> Nema dovoljno signala za izdvajanje glavnih pokretača.</span>
                                )}
                              </div>

                              <div className="reason-block">
                                <strong>Upozorenja:</strong>
                                {warningCodeItems?.length ? (
                                  <ul className="reason-code-list">
                                    {warningCodeItems.map((item) => (
                                      <li key={item.code}>
                                        <span>{item.message}</span>
                                        {item.message !== item.code ? <small>{item.code}</small> : null}
                                      </li>
                                    ))}
                                  </ul>
                                ) : (
                                  <span> Nema dodatnih upozorenja.</span>
                                )}
                              </div>

                              <div className="reason-block">
                                <strong>Očekivani uticaj:</strong> {expectedImpactRsd != null ? fmtRsd(expectedImpactRsd, 0, ANALYTICS_UNAVAILABLE_LABEL) : ANALYTICS_UNAVAILABLE_LABEL}
                                {whyPanel.impactWindowDays != null ? <span> u prozoru od {fmtNumber(whyPanel.impactWindowDays, 0, "0")} dana</span> : null}
                                {expectedImpactRsd == null ? (
                                  <div className="reason-warning-inline">Nema pouzdane procene uticaja jer nedostaje ulazni signal.</div>
                                ) : null}
                              </div>

                              <div className="reason-block">
                                <strong>Rizik ako se ignoriše:</strong> {whyPanel.riskIfIgnored || "Rizik nije specificiran."}
                              </div>

                              <div className="reason-block">
                                <strong>Razlozi preporuke:</strong>
                                {reasonCodeItems?.length ? (
                                  <ul className="reason-code-list">
                                    {reasonCodeItems.map((item) => (
                                      <li key={item.code}>
                                        <span>{item.message}</span>
                                        {item.message !== item.code ? <small>{item.code}</small> : null}
                                      </li>
                                    ))}
                                  </ul>
                                ) : (
                                  <span> Nema šifara razloga. Koristi se samo tekst razloga.</span>
                                )}
                              </div>

                              <div className="reason-metric-grid">
                                <div>
                                  <strong>Prihod:</strong> {fmtRsd(row.revenue, 0, ANALYTICS_UNAVAILABLE_LABEL)}
                                  <KpiExplainButton metricKey="revenue" ariaLabel="Kako je izračunat prihod" />
                                </div>
                                <div>
                                  <strong>Prodati komadi:</strong> {fmtNumber(row.unitsSold, 0, "0")}
                                  <KpiExplainButton metricKey="unitsSold" ariaLabel="Kako je izračunat broj prodatih jedinica" />
                                </div>
                                <div>
                                  <strong>{ANALYTICS_VELOCITY_LABEL}:</strong> {fmtNumber(row.velocityUnitsPerDay, 2, ANALYTICS_UNAVAILABLE_LABEL)}
                                  <KpiExplainButton metricKey="velocity" ariaLabel="Kako je izračunata brzina prodaje" />
                                </div>
                                <div><strong>Marža:</strong> {fmtPct(row.marginPct, 1)}</div>
                                <div>
                                  <strong>Maržni doprinos:</strong> {fmtRsd(row.marginContribution, 0, ANALYTICS_UNAVAILABLE_LABEL)}
                                  <KpiExplainButton metricKey="marginContribution" ariaLabel="Kako je izračunat maržni doprinos" />
                                </div>
                                <div><strong>Trenutna zaliha:</strong> {fmtNumber(row.currentStock, 0, "Nije dostupna")}</div>
                                <div><strong>Dani od poslednje prodaje:</strong> {row.daysSinceLastSale != null ? `${fmtNumber(row.daysSinceLastSale, 0, "0")} dana` : ANALYTICS_UNAVAILABLE_LABEL}</div>
                                <div><strong>Trend:</strong> {fmtPct(row.trendPct, 1)}</div>
                                <div>
                                  <strong>Procena izgubljene prodaje:</strong> {fmtRsd(row.lostSalesEstimate, 0, ANALYTICS_UNAVAILABLE_LABEL)}
                                  <KpiExplainButton metricKey="lostSalesEstimate" ariaLabel="Kako je izračunata procena izgubljene prodaje" />
                                </div>
                                <div>
                                  <strong>Kapital u sporoj zalihi:</strong> {fmtRsd(row.slowStockCapital, 0, ANALYTICS_UNAVAILABLE_LABEL)}
                                  <KpiExplainButton metricKey="slowStockCapital" ariaLabel="Kako je izračunat kapital u sporoj zalihi" />
                                </div>
                                <div>
                                  <strong>Pokrivenost zalihe:</strong> {formatSignalMetricValue(row.stockCoverDays, row.stockCoverStatus, "days")}
                                  <KpiExplainButton metricKey="stockCoverDays" ariaLabel="Kako je izračunata pokrivenost zalihe" />
                                </div>
                                <div>
                                  <strong>Prodajnost:</strong> {formatSignalMetricValue(row.sellThroughRatio, row.sellThroughStatus, "ratio")}
                                  <KpiExplainButton metricKey="sellThrough" ariaLabel="Kako je izračunat signal prodajnosti" />
                                </div>
                                <div><strong>Pokrivenost nabavnom cenom:</strong> {fmtPct(row.marginCoveragePct, 1, "Nije dostupno")}</div>
                                <div>
                                  <strong>Pouzdanost:</strong> {whyPanel.reliabilityPct != null ? `${fmtNumber(whyPanel.reliabilityPct, 0, ANALYTICS_UNAVAILABLE_LABEL)}%` : ANALYTICS_UNAVAILABLE_LABEL}
                                  <KpiExplainButton metricKey="reliabilityPct" ariaLabel="Kako je izračunata pouzdanost signala" />
                                </div>
                                <div>
                                  <strong>Kvalitet podataka:</strong> {DATA_QUALITY_LABELS[dataQuality]}
                                  <KpiExplainButton metricKey="confidencePct" ariaLabel="Kako je izračunata sigurnost preporuke" />
                                </div>
                              </div>

                              <div className="reason-actions">
                                <button
                                  type="button"
                                  className={`btn-add-to-queue${isQueued ? " added" : ""}`}
                                  disabled={isQueueBusy || isQueued}
                                  onClick={() => void addRowToCentralActions(row)}
                                  title={isQueued
                                    ? "Akcija je već u centralnom redu."
                                    : actionStatusKnown
                                      ? "Dodaj u centralni red akcija"
                                      : "Dodaj u centralni red akcija. Status postojećih akcija trenutno nije dostupan."}
                                >
                                  {isQueueBusy
                                    ? "Dodavanje..."
                                    : isQueued
                                      ? "U akcijama"
                                      : queueSpec.recommendationStatus === "SIGNAL_REVIEW"
                                        ? "Dodaj u proveru"
                                        : "Dodaj u akcije"}
                                </button>
                                {supplierUrl ? <Link className="reason-link-btn" to={supplierUrl}>Otvori dobavljača</Link> : null}
                                {inventoryUrl ? <Link className="reason-link-btn" to={inventoryUrl}>Otvori zalihe</Link> : null}
                                <span>
                                  <InfoTip text={analyticsMetricDescriptions.recommendationReason} />
                                </span>
                              </div>
                            </div>
                          </td>
                        </tr>
                      ) : null}
                    </Fragment>
                  );
                })}
            </tbody>
          </table>
        </div>
        <div className="product-decision-render-summary">
          <span role="status" aria-live="polite">Prikazano {visibleRows.length} od {sortedRows.length} redova.</span>
          {visibleRows.length < sortedRows.length ? (
            <button
              type="button"
              onClick={() => setVisibleRowLimit((current) => Math.min(current + PRODUCT_DECISION_RENDER_BATCH_SIZE, sortedRows.length))}
            >
              Prikaži još {Math.min(PRODUCT_DECISION_RENDER_BATCH_SIZE, sortedRows.length - visibleRows.length)}
            </button>
          ) : null}
        </div>
        </>
      ) : null}
    </section>
  );
}



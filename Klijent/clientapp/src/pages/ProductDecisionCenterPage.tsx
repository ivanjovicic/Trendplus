import { Fragment, useCallback, useEffect, useMemo, useRef, useState } from "react";
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
  getStores,
  getSupplierFilters,
  upsertAnalyticsActionWithResult,
} from "../services/analyticsApi";
import {
  fmtNumber,
  fmtPct,
  fmtRsd,
} from "../utils/analyticsFormatters";
import { getAnalyticsActionWriteErrorMessage } from "../utils/analyticsActionWriteErrors";
import {
  getAnalyticsMetaMessage,
  isAnalyticsMetaInsufficient,
  isAnalyticsMetaWarning,
  shouldShowAnalyticsEmptyState,
} from "../utils/analyticsResponseMeta";
import { analyticsMetricDescriptions } from "../utils/analyticsMetricDescriptions";
import type {
  AnalyticsActionDataQualityStatus,
  AnalyticsActionSourceType,
  ProductDecisionCenterItem,
  ProductDecisionCenterResponse,
  ProductDecisionRecommendationStatus,
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
  { value: "FIX_DATA", label: "Proveriti podatke" },
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
  replenish_needed: "Potrebna je dopuna da bi se izbegao gubitak prodaje.",
  high_stock_risk: "Postoji rizik od viška zalihe.",
  data_quality_blocker: "Kvalitet podataka blokira pouzdanu preporuku.",
  expected_impact_denominator_missing: "Nedostaje ulaz za procenu očekivanog uticaja.",
  data_quality_critical: "Kvalitet podataka je kritičan i traži proveru.",
  insufficient_data: "Signal nije dovoljno jak za pouzdanu preporuku.",
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

const TABLE_COLUMNS: AnalyticsTableColumn<ProductDecisionCenterItem>[] = [
  { key: "productName", header: "Artikal", dataType: "text" },
  { key: "supplierName", header: "Dobavljač", dataType: "text" },
  { key: "revenue", header: "Prodaja / komadi", dataType: "currency" },
  { key: "velocityUnitsPerDay", header: "Brzina prodaje", dataType: "number" },
  { key: "marginPct", header: "Marža", dataType: "percent" },
  { key: "currentStock", header: "Zaliha", dataType: "number" },
  { key: "trendPct", header: "Trend", dataType: "percent" },
  { key: "stockCoverDays", header: "Pokrivenost zalihe", dataType: "number" },
  { key: "sellThroughRatio", header: "Obrt zalihe", dataType: "percent" },
  { key: "confidencePct", header: "Sigurnost preporuke", dataType: "number" },
  { key: "dataQualityStatus", header: "Kvalitet podataka", dataType: "text" },
  { key: "recommendationLabel", header: "Preporuka", dataType: "text" },
];

const PRODUCT_DECISION_PAGE_EXPLANATION =
  "Ovaj ekran predlaže šta uraditi sa artiklima: dopuniti, pojačati, sniziti cenu, pratiti ili proveriti podatke. Preporuka je blokirana kada podaci nisu dovoljno pouzdani.";

function toDateInputValue(date: Date): string {
  return date.toISOString().slice(0, 10);
}

function defaultPeriodRange() {
  const to = new Date();
  const from = new Date(to);
  from.setDate(from.getDate() - 29);
  return { fromDate: toDateInputValue(from), toDate: toDateInputValue(to) };
}

function applyPeriodPreset(preset: Exclude<PeriodPreset, "custom">) {
  const to = new Date();
  const from = new Date(to);
  if (preset === "last60") from.setDate(from.getDate() - 59);
  else if (preset === "last90") from.setDate(from.getDate() - 89);
  else from.setDate(from.getDate() - 29);
  return { fromDate: toDateInputValue(from), toDate: toDateInputValue(to) };
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
  return REASON_CODE_MESSAGES[normalized] ?? code;
}

function normalizeConfidenceLevel(
  value: string | null | undefined,
  confidenceScore: number | null | undefined,
): ConfidenceLevel {
  const normalized = (value ?? "").trim().toLowerCase();
  if (normalized === "high" || normalized === "medium" || normalized === "low" || normalized === "insufficient_data") {
    return normalized;
  }

  if (confidenceScore == null || Number.isNaN(confidenceScore)) {
    return "insufficient_data";
  }

  if (confidenceScore >= 80) return "high";
  if (confidenceScore >= 60) return "medium";
  return "low";
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

  if (row.confidenceLevel === "insufficient_data") {
    return null;
  }

  return Number.isNaN(row.confidencePct) ? null : row.confidencePct;
}

function resolveExpectedImpactRsd(row: ProductDecisionRow): number | null {
  if (row.expectedImpactRsd != null && !Number.isNaN(row.expectedImpactRsd)) {
    return row.expectedImpactRsd;
  }

  return row.lostSalesEstimate > 0 ? row.lostSalesEstimate : null;
}

function resolveWarningCodes(row: ProductDecisionRow): string[] {
  const source = row.warningCodes?.length ? row.warningCodes : row.reasonCodes;
  return normalizeSignalList(source).filter((code) => {
    const normalized = code.toLowerCase();
    return normalized === "missing_cost"
      || normalized === "missing_supplier"
      || normalized === "insufficient_history"
      || normalized === "expected_impact_denominator_missing"
      || normalized === "data_quality_critical"
      || normalized === "insufficient_data"
      || normalized === "data_quality_blocker";
  });
}

function resolvePrimaryDrivers(row: ProductDecisionRow): string[] {
  const drivers: string[] = [];
  const add = (driver: string) => {
    if (!drivers.includes(driver)) drivers.push(driver);
  };

  if (row.velocityUnitsPerDay > 0.5 || row.unitsSold >= 20) add("sales_velocity");
  if (row.marginPct != null || row.marginContribution > 0) add("margin");
  if (row.stockGap > 0 || row.currentStock <= row.minStock || ["low_cover", "out_of_stock_risk", "slow_stock", "no_velocity"].includes((row.stockCoverStatus ?? "").trim().toLowerCase())) {
    add("stock_risk");
  }
  if (row.trendPct != null) add("trend");
  if (resolveWarningCodes(row).some((code) => code === "missing_cost")) add("missing_cost");
  if ((row.supplierName && row.reliabilityPct < 70) || resolveWarningCodes(row).some((code) => code === "missing_supplier")) {
    add("supplier_reliability");
  }
  if (row.unitsSold < 8 || row.daysSinceLastSale == null) add("sparse_sales");

  return drivers;
}

function resolveInputFreshnessStatus(row: ProductDecisionRow, confidenceLevel: ConfidenceLevel): "fresh" | "stale" | "critical" | "unknown" {
  if (confidenceLevel === "insufficient_data" || canonicalDataQualityStatus(row.dataQualityStatus) === "critical") {
    return "critical";
  }

  if (row.daysSinceLastSale == null) {
    return "unknown";
  }

  if (row.daysSinceLastSale > 60) {
    return "stale";
  }

  return "fresh";
}

function confidenceLevelLabel(level: ConfidenceLevel): string {
  return CONFIDENCE_LEVEL_LABELS[level];
}

function confidenceScoreText(level: ConfidenceLevel, score: number | null): string {
  if (level === "insufficient_data" || score == null) {
    return confidenceLevelLabel(level);
  }

  return `${confidenceLevelLabel(level)} · ${fmtNumber(score, 0, "N/A")}%`;
}

function inputFreshnessLabel(value: string | null | undefined): string {
  const normalized = (value ?? "").trim().toLowerCase();
  if (normalized === "fresh") return "Sveže";
  if (normalized === "stale") return "Zastarelo";
  if (normalized === "critical") return "Kritično";
  return "Nije poznato";
}

function primaryDriverLabel(value: string): string {
  return DRIVER_LABELS[value] ?? value;
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
  if (normalized === "good") return "Dobar obrt zalihe";
  if (normalized === "warning") return "Upozorenje za obrt zalihe";
  if (normalized === "critical") return "Kritičan obrt zalihe";
  return "Nedovoljno podataka";
}

function formatSignalMetricValue(value: number | null | undefined, status: string | null | undefined, unit: "days" | "ratio"): string {
  if (value == null || Number.isNaN(value)) {
    return (status ?? "").trim().toLowerCase() === "insufficient_data"
      ? "Nedovoljno podataka"
      : "Nije dostupno";
  }

  if (unit === "days") {
    return `${fmtNumber(value, 1, "N/A")} dana`;
  }

  return fmtPct(value * 100, 1);
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
  if (status === "DO_NOT_ORDER") return `Ne naručuj: ${productName}`;
  if (status === "FIX_DATA") return `Proveri podatke: ${productName}`;
  if (status === "WATCH") return `Prati: ${productName}`;
  return `Proveri: ${productName}`;
}

function mapActionPriority(row: ProductDecisionRow): "P1" | "P2" | "P3" {
  const dataQuality = canonicalDataQualityStatus(row.dataQualityStatus);
  const recommendationStatusValue = row["recommendationStatus"];
  const hasCriticalOos = recommendationStatusValue === "REPLENISH" && row.stockGap > 0 && row.currentStock <= 0;
  const hasLargeLostSales = row.lostSalesEstimate >= 100_000;
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

  if (normalizedCover === "insufficient_data" || row.recommendationAllowed === false) {
    return {
      sourceType: "product",
      actionKind: "signal_check",
      title: `Proveri signal: ${row.productName}`,
      recommendationStatus: "SIGNAL_REVIEW",
      priority: "P3",
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

  const [periodPreset, setPeriodPreset] = useState<PeriodPreset>("last30");
  const [fromDate, setFromDate] = useState(initialRange.fromDate);
  const [toDate, setToDate] = useState(initialRange.toDate);
  const [storeId, setStoreId] = useState<number | null>(null);
  const [supplierId, setSupplierId] = useState<number | null>(null);
  const [recommendationFilter, setRecommendationFilter] = useState<RecommendationFilter>("all");
  const [dataQualityFilter, setDataQualityFilter] = useState<DataQualityFilter>("all");
  const [search, setSearch] = useState("");
  const [sortField, setSortField] = useState<SortField>("recommendationStatus");
  const [sortDir, setSortDir] = useState<SortDir>("desc");
  const [expandedProductId, setExpandedProductId] = useState<number | null>(null);

  const [stores, setStores] = useState<StoreOption[]>([]);
  const [suppliers, setSuppliers] = useState<SupplierFilterOption[]>([]);
  const [payload, setPayload] = useState<ProductDecisionCenterResponse | null>(null);
  const payloadRef = useRef<ProductDecisionCenterResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<{ message: string; errorCode?: string | null; correlationId?: string | null } | null>(null);
  const [staleWarning, setStaleWarning] = useState<string | null>(null);
  const [queueMessage, setQueueMessage] = useState<string | null>(null);
  const [queueBusyKey, setQueueBusyKey] = useState<string | null>(null);
  const [queuedActionKeys, setQueuedActionKeys] = useState<Set<string>>(new Set());
  const [actionStatusWarning, setActionStatusWarning] = useState<string | null>(null);
  const queueBusyKeyRef = useRef<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const items = await getStores();
        if (!cancelled) setStores(items);
      } catch {
        if (!cancelled) setStores([]);
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
        const items = await getSupplierFilters(fromDate, toDate, true, storeId);
        if (!cancelled) setSuppliers(items);
      } catch {
        if (!cancelled) setSuppliers([]);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [fromDate, toDate, storeId]);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    setStaleWarning(null);
    try {
      const response = await getProductDecisionCenter({
        fromDate,
        toDate,
        storeId,
        supplierId,
        top: 1200,
      });
      setPayload(response);
      payloadRef.current = response;
    } catch (reason) {
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
      setLoading(false);
    }
  }, [fromDate, supplierId, storeId, toDate]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const rows = (payload?.rows ?? []) as ProductDecisionRow[];
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
      let diff = 0;
      if (sortField === "productName") diff = a.productName.localeCompare(b.productName, "sr");
      else if (sortField === "supplierName") diff = (a.supplierName ?? "").localeCompare(b.supplierName ?? "", "sr");
      else if (sortField === "revenue") diff = a.revenue - b.revenue;
      else if (sortField === "unitsSold") diff = a.unitsSold - b.unitsSold;
      else if (sortField === "velocityUnitsPerDay") diff = a.velocityUnitsPerDay - b.velocityUnitsPerDay;
      else if (sortField === "marginPct") diff = (a.marginPct ?? -9999) - (b.marginPct ?? -9999);
      else if (sortField === "currentStock") diff = a.currentStock - b.currentStock;
      else if (sortField === "trendPct") diff = (a.trendPct ?? -9999) - (b.trendPct ?? -9999);
      else if (sortField === "stockCoverDays") diff = (a.stockCoverDays ?? -9999) - (b.stockCoverDays ?? -9999);
      else if (sortField === "sellThroughRatio") diff = (a.sellThroughRatio ?? -9999) - (b.sellThroughRatio ?? -9999);
      else if (sortField === "confidencePct") diff = a.confidencePct - b.confidencePct;
      else if (sortField === "dataQualityStatus") {
        diff = DATA_QUALITY_ORDER[canonicalDataQualityStatus(a.dataQualityStatus)] - DATA_QUALITY_ORDER[canonicalDataQualityStatus(b.dataQualityStatus)];
      } else {
        diff = RECOMMENDATION_PRIORITY[a.recommendationStatus] - RECOMMENDATION_PRIORITY[b.recommendationStatus];
      }

      return sortDir === "asc" ? diff : -diff;
    });
    return copy;
  }, [filteredRows, sortDir, sortField]);

  useEffect(() => {
    let cancelled = false;

    const candidates = sortedRows.map((row) => {
      const queueSpec = buildProductQueueSpec(row);
      return {
        sourceType: queueSpec.sourceType,
        sourceKey: buildSourceKey(row, queueSpec.actionKind, fromDate, toDate, storeId, supplierId),
      };
    });

    const lookupItems = Array.from(new Map(
      candidates.map((entry) => [`${entry.sourceType}::${entry.sourceKey}`, entry])
    ).values());

    if (lookupItems.length === 0) {
      setActionStatusWarning(null);
      setQueuedActionKeys((previous) => (previous.size === 0 ? previous : new Set()));
      return () => {
        cancelled = true;
      };
    }

    (async () => {
      try {
        const statuses = await getAnalyticsActionSourceStatuses({
          items: lookupItems,
        });

        if (cancelled) return;

        const keys = new Set<string>();
        for (const item of statuses.items) {
          if (item.exists && item.sourceKey) keys.add(item.sourceKey);
        }

        setQueuedActionKeys(keys);
        setActionStatusWarning(null);
      } catch {
        if (!cancelled) {
          setQueuedActionKeys(new Set());
          setActionStatusWarning("Status akcija trenutno nije dostupan.");
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [fromDate, sortedRows, storeId, supplierId, toDate]);
  const hasBlockingError = Boolean(error && !payload);
  const showMetaWarning = !loading && !hasBlockingError && isAnalyticsMetaWarning(responseMeta);
  const showInsufficientState = !loading
    && !hasBlockingError
    && shouldShowAnalyticsEmptyState(responseMeta, rows.length)
    && isAnalyticsMetaInsufficient(responseMeta);
  const showNoDataState = !loading && !hasBlockingError && !showInsufficientState && rows.length === 0;
  const showFilteredOutState = !loading && !hasBlockingError && !showInsufficientState && rows.length > 0 && sortedRows.length === 0;

  const kpis = useMemo(() => ({
    replenishCount: rows.filter((x) => x["recommendationStatus"] === "REPLENISH").length,
    boostCount: rows.filter((x) => x["recommendationStatus"] === "BOOST").length,
    markdownCount: rows.filter((x) => x["recommendationStatus"] === "MARKDOWN").length,
    doNotOrderCount: rows.filter((x) => x["recommendationStatus"] === "DO_NOT_ORDER").length,
    fixDataCount: rows.filter((x) => x["recommendationStatus"] === "FIX_DATA").length,
    lostSalesEstimate: payload?.summary.lostSalesEstimate ?? 0,
    slowStockCapital: payload?.summary.slowStockCapital ?? 0,
    stockCoverRiskCount: rows.filter((x) => {
      const status = (x.stockCoverStatus ?? "").toLowerCase();
      return status === "low_cover" || status === "low" || status === "out_of_stock_risk" || status === "insufficient_data";
    }).length,
    lowCoverSkus: rows.filter((x) => {
      const status = (x.stockCoverStatus ?? "").toLowerCase();
      return status === "low_cover" || status === "low" || status === "out_of_stock_risk";
    }).length,
    slowStockSkus: rows.filter((x) => {
      const status = (x.stockCoverStatus ?? "").toLowerCase();
      return status === "slow_stock" || status === "slow" || status === "no_velocity";
    }).length,
    goodSellThroughSkus: rows.filter((x) => (x.sellThroughStatus ?? "").toLowerCase() === "good").length,
  }), [payload?.summary.lostSalesEstimate, payload?.summary.slowStockCapital, rows]);

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
    { key: "generatedAtUtc", label: "Generisano", value: payload?.generatedAtUtc ?? "N/A" },
    { key: "totalRows", label: "Ukupno redova", value: payload?.totalRows ?? 0 },
    { key: "filteredRows", label: "Prikazano redova", value: sortedRows.length },
  ], [payload?.generatedAtUtc, payload?.totalRows, sortedRows.length]);

  const handlePeriodPresetChange = (value: PeriodPreset) => {
    setPeriodPreset(value);
    if (value === "custom") return;
    const range = applyPeriodPreset(value);
    setFromDate(range.fromDate);
    setToDate(range.toDate);
  };

  const setSort = (field: SortField) => {
    setSortField((prevField) => {
      if (prevField === field) {
        setSortDir((prevDir) => (prevDir === "asc" ? "desc" : "asc"));
        return prevField;
      }
      setSortDir("desc");
      return field;
    });
  };

  const toggleExpandedRow = useCallback((productId: number) => {
    setExpandedProductId((current) => (current === productId ? null : productId));
  }, []);

  const addRowToCentralActions = useCallback(async (row: ProductDecisionRow) => {
    const queueSpec = buildProductQueueSpec(row);
    const sourceKey = buildSourceKey(row, queueSpec.actionKind, fromDate, toDate, storeId, supplierId);
    const confidenceLevel = normalizeConfidenceLevel(row.confidenceLevel, row.confidenceScore ?? row.confidencePct);
    const confidenceScore = resolveConfidenceScore(row);
    const warningCodes = resolveWarningCodes(row);
    const primaryDrivers = resolvePrimaryDrivers(row);
    const expectedImpactRsd = resolveExpectedImpactRsd(row);
    const inputFreshnessStatus = resolveInputFreshnessStatus(row, confidenceLevel);

    if (queueBusyKeyRef.current === sourceKey || queuedActionKeys.has(sourceKey)) {
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
      setQueueMessage(result.existing
        ? "Akcija je već u centralnim akcijama."
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
        lastRefreshAt={payload?.generatedAtUtc ?? null}
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

      {showMetaWarning ? (
        <div className="product-decision-message product-decision-message-info" role="status">
          Prikazani podaci su delimični ili fallback. {responseMetaMessage ?? "Proverite status osvežavanja analitike."}
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

      {!hasBlockingError ? (
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
          <span>Proveriti podatke</span>
          <strong>{fmtNumber(kpis.fixDataCount, 0, "0")}</strong>
          <KpiExplainButton metricKey="fixDataCount" ariaLabel="Kako je izračunat broj proizvoda za proveru podataka" />
        </article>
        <article className="kpi-card">
          <span>Procena izgubljene prodaje</span>
          <strong>{fmtRsd(kpis.lostSalesEstimate, 0, "N/A")}</strong>
          <KpiExplainButton metricKey="lostSalesEstimate" ariaLabel="Kako je izračunata procena izgubljene prodaje" />
        </article>
        <article className="kpi-card">
          <span>Kapital u sporoj zalihi</span>
          <strong>{fmtRsd(kpis.slowStockCapital, 0, "N/A")}</strong>
          <KpiExplainButton metricKey="slowStockCapital" ariaLabel="Kako je izračunat kapital u sporoj zalihi" />
        </article>
        <article className="kpi-card">
          <span>Rizik pokrivenosti</span>
          <strong>{fmtNumber(kpis.stockCoverRiskCount, 0, "0")}</strong>
          <KpiExplainButton metricKey="stockCoverDays" ariaLabel="Kako je izračunat broj artikala sa rizičnom pokrivenošću zalihe" />
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
          <span>SKU sa dobrim obrtom</span>
          <strong>{fmtNumber(kpis.goodSellThroughSkus, 0, "0")}</strong>
          <KpiExplainButton metricKey="sellThrough" ariaLabel="Kako je izračunat broj artikala sa dobrim obrtom zalihe" />
        </article>
      </section>
      ) : null}

      <section className="product-decision-filters">
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
              value={fromDate}
              onChange={(event) => {
                setFromDate(event.target.value);
                setPeriodPreset("custom");
              }}
            />
          </label>
          <label>
            Do datuma
            <input
              type="date"
              value={toDate}
              onChange={(event) => {
                setToDate(event.target.value);
                setPeriodPreset("custom");
              }}
            />
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
            <select value={supplierId ?? ""} onChange={(event) => setSupplierId(event.target.value ? Number(event.target.value) : null)}>
              <option value="">Svi dobavljači</option>
              {suppliers.map((supplier) => (
                <option key={supplier.supplierId} value={supplier.supplierId}>
                  {supplier.supplierName}
                </option>
              ))}
            </select>
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
              <option value="velocityUnitsPerDay:desc">Brzina prodaje opadajuće</option>
              <option value="stockCoverDays:asc">Pokrivenost zalihe rastuće</option>
              <option value="sellThroughRatio:desc">Obrt zalihe opadajuće</option>
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

      {queueMessage ? <div className="product-decision-message product-decision-message-info">{queueMessage}</div> : null}
      {staleWarning ? <div className="product-decision-message product-decision-message-info">{staleWarning}</div> : null}
      {!hasBlockingError && error ? (
        <div className="product-decision-message product-decision-message-info">
          Prikazujemo prethodno učitane podatke. Novi upit nije uspeo.
        </div>
      ) : null}
      {!hasBlockingError && actionStatusWarning ? (
        <div className="product-decision-message product-decision-message-info" role="status">
          {actionStatusWarning}
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
        <div className="product-decision-table-wrap">
          <table className="product-decision-table">
            <thead>
              <tr>
                <th onClick={() => setSort("productName")}>Artikal</th>
                <th onClick={() => setSort("supplierName")}>Dobavljač</th>
                <th onClick={() => setSort("revenue")}>Prodaja / komadi</th>
                <th onClick={() => setSort("velocityUnitsPerDay")}>Brzina prodaje</th>
                <th onClick={() => setSort("marginPct")}>Marža</th>
                <th onClick={() => setSort("currentStock")}>Zaliha</th>
                <th onClick={() => setSort("trendPct")}>Trend</th>
                <th onClick={() => setSort("stockCoverDays")}>Pokrivenost zalihe</th>
                <th onClick={() => setSort("sellThroughRatio")}>Obrt zalihe</th>
                <th onClick={() => setSort("confidencePct")}>Sigurnost preporuke</th>
                <th onClick={() => setSort("dataQualityStatus")}>Kvalitet podataka</th>
                <th onClick={() => setSort("recommendationStatus")}>Preporuka</th>
                <th>Akcija</th>
              </tr>
            </thead>
            <tbody>
              {sortedRows.map((row) => {
                  const expanded = expandedProductId === row.productId;
                  const queueSpec = buildProductQueueSpec(row);
                  const sourceKey = buildSourceKey(row, queueSpec.actionKind, fromDate, toDate, storeId, supplierId);
                  const isQueued = queuedActionKeys.has(sourceKey);
                  const isQueueBusy = queueBusyKey === sourceKey;
                  const dataQuality = canonicalDataQualityStatus(row.dataQualityStatus);
                  const confidenceLevel = normalizeConfidenceLevel(row.confidenceLevel, row.confidenceScore ?? row.confidencePct);
                  const confidenceScore = resolveConfidenceScore(row);
                  const warningCodes = resolveWarningCodes(row);
                  const primaryDrivers = resolvePrimaryDrivers(row);
                  const expectedImpactRsd = resolveExpectedImpactRsd(row);
                  const inputFreshnessStatus = resolveInputFreshnessStatus(row, confidenceLevel);
                  const reasonCodeItems = row.reasonCodes.length
                    ? row.reasonCodes.map((code) => ({ code, message: translateReasonCode(code) }))
                    : null;
                  const warningCodeItems = warningCodes.length
                    ? warningCodes.map((code) => ({ code, message: translateReasonCode(code) }))
                    : null;
                  const primaryDriverItems = primaryDrivers.length
                    ? primaryDrivers.map((driver) => ({ code: driver, label: primaryDriverLabel(driver) }))
                    : null;
                  const supplierUrl = row.supplierId != null ? buildSupplierDecisionUrl(row.supplierId) : null;
                  const inventoryUrl = (row.productId > 0 || row.sku) ? buildInventoryDecisionUrl(row) : null;

                  return (
                    <Fragment key={`${row.productId}:${row.recommendationStatus}`}>
                      <tr className="data-row" onClick={() => toggleExpandedRow(row.productId)} title="Klik za detalje preporuke.">
                        <td>
                          <strong>{row.productName}</strong>
                          <small>{row.sku || "N/A"} | {row.category ?? row.tipObuce ?? "N/A"}</small>
                        </td>
                        <td>{row.supplierName ?? "N/A"}</td>
                        <td>
                          <span>{fmtRsd(row.revenue, 0, "N/A")}</span>
                          <small>{fmtNumber(row.unitsSold, 0, "0")} kom</small>
                        </td>
                        <td>{fmtNumber(row.velocityUnitsPerDay, 2, "N/A")}</td>
                        <td>
                          <span>{fmtPct(row.marginPct, 1)}</span>
                          <small>{row.marginQualityLabel ?? "N/A"} | pokriće: {fmtPct(row.marginCoveragePct, 1)}</small>
                        </td>
                        <td>
                          <span>{fmtNumber(row.currentStock, 0, "0")}</span>
                          <small>min: {fmtNumber(row.minStock, 0, "0")} | gap: {fmtNumber(row.stockGap, 0, "0")}</small>
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
                          <span className={confidenceLevelClass(confidenceLevel)}>{confidenceScoreText(confidenceLevel, confidenceScore)}</span>
                          <small>Pouzdanost: {row.reliabilityPct != null ? `${fmtNumber(row.reliabilityPct, 0, "N/A")}%` : "N/A"}</small>
                        </td>
                        <td>
                          <span className={dataQualityClass(dataQuality)}>{DATA_QUALITY_LABELS[dataQuality]}</span>
                        </td>
                        <td>
                          <span className={recommendationToneClass(row.recommendationStatus)}>
                            {displayRecommendationLabel(row)}
                          </span>
                          {warningCodeItems?.length ? (
                            <small className="recommendation-warning-summary">
                              Upozorenja: {warningCodeItems.slice(0, 3).map((item) => item.message).join(" · ")}
                            </small>
                          ) : null}
                          <button
                            type="button"
                            className="why-button"
                            onClick={(event) => {
                              event.stopPropagation();
                              toggleExpandedRow(row.productId);
                            }}
                            title={row.explainabilityText ?? row.recommendationReason}
                          >
                            Zašto?
                          </button>
                        </td>
                        <td>
                          <span>{row.recommendedAction}</span>
                          <small>{expectedImpactRsd != null ? `${fmtRsd(expectedImpactRsd, 0, "N/A")} potencijalnog uticaja` : "Procena uticaja nije dostupna."}</small>
                          {expectedImpactRsd == null ? (
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
                            title={isQueued ? "Akcija je već u centralnom redu." : "Dodaj u centralni red akcija"}
                          >
                            {isQueueBusy ? "Dodavanje..." : isQueued ? "U akcijama" : "Dodaj u akcije"}
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
                                </div>
                              </div>

                              <div className="reason-block">
                                <strong>Zašto ova preporuka?</strong> {row.explainabilityText || row.recommendationReason || "Objašnjenje nije dostupno."}
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
                                <strong>Očekivani uticaj:</strong> {expectedImpactRsd != null ? fmtRsd(expectedImpactRsd, 0, "N/A") : "Nije dostupan"}
                                {row.impactWindowDays != null ? <span> u prozoru od {fmtNumber(row.impactWindowDays, 0, "0")} dana</span> : null}
                                {expectedImpactRsd == null ? (
                                  <div className="reason-warning-inline">Nema pouzdane procene uticaja jer nedostaje ulazni signal.</div>
                                ) : null}
                              </div>

                              <div className="reason-block">
                                <strong>Rizik ako se ignoriše:</strong> {row.riskIfIgnored || "Rizik nije specificiran."}
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
                                  <strong>Prihod:</strong> {fmtRsd(row.revenue, 0, "N/A")}
                                  <KpiExplainButton metricKey="revenue" ariaLabel="Kako je izračunat prihod" />
                                </div>
                                <div>
                                  <strong>Prodati komadi:</strong> {fmtNumber(row.unitsSold, 0, "0")}
                                  <KpiExplainButton metricKey="unitsSold" ariaLabel="Kako je izračunat broj prodatih jedinica" />
                                </div>
                                <div>
                                  <strong>Brzina prodaje:</strong> {fmtNumber(row.velocityUnitsPerDay, 2, "N/A")}
                                  <KpiExplainButton metricKey="velocity" ariaLabel="Kako je izračunata brzina prodaje" />
                                </div>
                                <div><strong>Marža:</strong> {fmtPct(row.marginPct, 1)}</div>
                                <div>
                                  <strong>Maržni doprinos:</strong> {fmtRsd(row.marginContribution, 0, "N/A")}
                                  <KpiExplainButton metricKey="marginContribution" ariaLabel="Kako je izračunat maržni doprinos" />
                                </div>
                                <div><strong>Trenutna zaliha:</strong> {fmtNumber(row.currentStock, 0, "0")}</div>
                                <div><strong>Dani od poslednje prodaje:</strong> {row.daysSinceLastSale != null ? `${fmtNumber(row.daysSinceLastSale, 0, "0")} dana` : "N/A"}</div>
                                <div><strong>Trend:</strong> {fmtPct(row.trendPct, 1)}</div>
                                <div>
                                  <strong>Procena izgubljene prodaje:</strong> {fmtRsd(row.lostSalesEstimate, 0, "N/A")}
                                  <KpiExplainButton metricKey="lostSalesEstimate" ariaLabel="Kako je izračunata procena izgubljene prodaje" />
                                </div>
                                <div>
                                  <strong>Kapital u sporoj zalihi:</strong> {fmtRsd(row.slowStockCapital, 0, "N/A")}
                                  <KpiExplainButton metricKey="slowStockCapital" ariaLabel="Kako je izračunat kapital u sporoj zalihi" />
                                </div>
                                <div>
                                  <strong>Pokrivenost zalihe:</strong> {formatSignalMetricValue(row.stockCoverDays, row.stockCoverStatus, "days")}
                                  <KpiExplainButton metricKey="stockCoverDays" ariaLabel="Kako je izračunata pokrivenost zalihe" />
                                </div>
                                <div>
                                  <strong>Obrt zalihe:</strong> {formatSignalMetricValue(row.sellThroughRatio, row.sellThroughStatus, "ratio")}
                                  <KpiExplainButton metricKey="sellThrough" ariaLabel="Kako je izračunat signal obrta zalihe" />
                                </div>
                                <div><strong>Pokrivenost nabavnom cenom:</strong> {fmtPct(row.marginCoveragePct, 1)}</div>
                                <div>
                                  <strong>Pouzdanost:</strong> {row.reliabilityPct != null ? `${fmtNumber(row.reliabilityPct, 0, "N/A")}%` : "N/A"}
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
                                  title={isQueued ? "Akcija je već u centralnom redu." : "Dodaj u centralni red akcija"}
                                >
                                  {isQueueBusy ? "Dodavanje..." : isQueued ? "U akcijama" : "Dodaj u akcije"}
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
      ) : null}
    </section>
  );
}



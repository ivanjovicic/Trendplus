import { useCallback, useDeferredValue, useEffect, useMemo, useRef, useState } from "react";
import { Warehouse } from "lucide-react";
import { useSearchParams } from "react-router-dom";
import { AnalyticsMetaError, createInventoryReportSchedule, exportInventoryReport, getAnalyticsActionSourceStatuses, getForecast, getInventoryActionSuggestions, getInventoryAlerts, getInventoryBalance, getInventoryInsights, getInventoryItemDetail, getInventoryList, getInventoryReportSchedules, getInventoryStoreComparison, getRebalanceSuggestions, getSizeCurve, getStores, getSupplierFilters, previewInventoryReport, printBlankInventoryForm, runInventoryReportScheduleNow, saveInventoryActionDecision, upsertAnalyticsActionWithResult } from "../services/analyticsApi";
import { downloadExport, resolveApiUrl, waitForExport } from "../services/exportApi";
import type { AnalyticsActionDataQualityStatus, AnalyticsResponseMeta, ForecastDto, InventoryActionSuggestion, InventoryActionWorkflow, InventoryAlertListDto, InventoryBalance, InventoryInsights, InventoryItemDetail, InventoryPagedResponse, InventoryReportSchedule, InventoryReportScheduleInput, InventoryStoreComparison, RebalanceListDto, SizeCurveDto, StoreOption, SupplierFilterOption } from "../types/analytics";
import AnalyticsEmptyState from "../components/analytics/AnalyticsEmptyState";
import AnalyticsErrorState from "../components/analytics/AnalyticsErrorState";
import AnalyticsControlBar from "../components/analytics/AnalyticsControlBar";
import AnalyticsTrustHeader from "../components/analytics/AnalyticsTrustHeader";
import { ActionWorkflowPanel } from "../components/inventory/ActionWorkflowPanel";
import { DecisionSummaryBar } from "../components/inventory/DecisionSummaryBar";
import { DemandForecastPanel } from "../components/inventory/DemandForecastPanel";
import { ErrorBoundary } from "../components/ErrorBoundary";
import { ExportSchedulerPanel } from "../components/inventory/ExportSchedulerPanel";
import { InventoryAlertsFeed } from "../components/inventory/InventoryAlertsFeed";
import { InventoryInsightPanels } from "../components/inventory/InventoryInsightPanels";
import { InventoryItemsTable } from "../components/inventory/InventoryItemsTable";
import { InventoryKPICards } from "../components/inventory/InventoryKPICards";
import { InventoryPriorityPanels } from "../components/inventory/InventoryPriorityPanels";
import { MailSchedulerPanel } from "../components/inventory/MailSchedulerPanel";
import { RebalancingTable } from "../components/inventory/RebalancingTable";
import { SKUDetailModal } from "../components/inventory/SKUDetailModal";
import { SizeCurvePanel } from "../components/inventory/SizeCurvePanel";
import { StoreComparisonPanel } from "../components/inventory/StoreComparisonPanel";
import KpiExplainButton from "../components/analytics/KpiExplainButton";
import { computeInventorySignalKpis, INVENTORY_SIGNAL_KPI_PAGE_SCOPE_NOTE } from "../components/inventory/inventorySignalKpis";
import { buildForecastRestockSuggestion, buildInventoryRow, buildInventoryScreenCsvFilename, buildInventoryScreenCsvLines, buildInventoryServerExportContractNote, buildInventoryWorkflowCentralQueueMetadata, buildOffPageDetailPlaceholderRow, buildSupplierChart, createScheduleDraft, formatPercent, INVENTORY_EXPOSURE_BASIS, inventoryRiskSortScopeWarning, isInventoryPageLocalRiskSort, resolveForecastRestockDaysSinceMovement, resolveInventoryExposureRsdFromRow, validateScheduleDraft } from "../components/inventory/inventoryUtils";
import { getDataScope } from "../utils/dataScope";
import type { InventoryRow } from "../components/inventory/types";
import { fmtNumber, formatDateTime } from "../utils/analyticsFormatters";
import { getAnalyticsActionWriteErrorMessage } from "../utils/analyticsActionWriteErrors";
import { getSafeAnalyticsErrorMessage } from "../utils/analyticsErrorMessages";
import { getAnalyticsMetaMessage, isAnalyticsMetaInsufficient, isAnalyticsMetaWarning, shouldShowAnalyticsEmptyState } from "../utils/analyticsResponseMeta";
import {
  resolveSupplierFilterFallbackState,
  SUPPLIER_FILTER_LOAD_FAILED_MESSAGE,
  SUPPLIER_FILTER_STALE_LIST_MESSAGE,
} from "../utils/supplierFilterFallbackState";
import { useReliableAnalyticsQuery } from "../hooks/useReliableAnalyticsQuery";

const PAGE_SIZE_OPTIONS = [25, 50, 100, 250];
const INVENTORY_SORT_OPTIONS = ["kolicina", "naziv", "vrednost", "azuriranje", "oosRisk", "overstockRisk"] as const;
const DEFAULT_INVENTORY_PAGE_SIZE = 50;
const DEFAULT_COMPARE_STORES = 3;
const TOP_SUPPLIERS_CHART = 6;
const TOP_RISK_ITEMS = 5;
const TOP_VALUE_ITEMS = 5;
const FORECAST_OOS_DISPLAY = 7;
const FORECAST_OVERSTOCK_DISPLAY = 7;

function parseInventoryPositiveInt(value: string | null, fallback: number): number {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
}

function parseInventoryPageSize(value: string | null): number {
  const parsed = parseInventoryPositiveInt(value, DEFAULT_INVENTORY_PAGE_SIZE);
  return PAGE_SIZE_OPTIONS.includes(parsed) ? parsed : DEFAULT_INVENTORY_PAGE_SIZE;
}

function parseInventoryCompareStores(value: string | null): number[] {
  if (!value) return [];
  return Array.from(new Set(value.split(",")
    .map((entry) => Number(entry))
    .filter((entry) => Number.isInteger(entry) && entry > 0)));
}

function parseInventorySort(value: string | null): string {
  return value && INVENTORY_SORT_OPTIONS.includes(value as (typeof INVENTORY_SORT_OPTIONS)[number])
    ? value
    : "kolicina";
}
const ALERTS_DISPLAY_COUNT = 12;
const REBALANCE_DISPLAY_COUNT = 20;
const REBALANCE_FETCH_LIMIT = 20;
const FORECAST_FETCH_LIMIT = 50;
const INVENTORY_SIGNAL_LOOKBACK_DAYS = 30;
const OOS_RISK_THRESHOLD = 0.25;
const OVERSTOCK_RISK_THRESHOLD = 0.5;
const STORE_COMPARISON_SECTION_ID = "inventory-store-comparison";
const ACTION_WORKFLOW_SECTION_ID = "inventory-action-workflow";
const INVENTORY_ACTIONS_QUEUE_URL = "/analytics/actions?sourceType=inventory";

type InventoryPageError = { message: string; errorCode?: string | null; correlationId?: string | null };

type InventoryLifecycleSnapshot = {
  balance: InventoryBalance;
  pageData: InventoryPagedResponse;
  insights: InventoryInsights;
  storeComparison: InventoryStoreComparison;
  actionWorkflow: InventoryActionWorkflow;
  forecast: ForecastDto;
  alerts: InventoryAlertListDto;
  rebalance: RebalanceListDto;
};

function toInventoryPageError(reason: unknown, fallback: string): InventoryPageError {
  if (reason instanceof AnalyticsMetaError) {
    return {
      message: reason.message,
      errorCode: reason.errorCode,
      correlationId: reason.correlationId,
    };
  }

  if (reason instanceof Error) {
    return { message: reason.message || fallback };
  }

  if (typeof reason === "string" && reason.trim()) {
    return { message: reason };
  }

  return { message: fallback };
}

function toSafeInventoryInlineError(reason: unknown, fallback: string): string {
  const pageError = toInventoryPageError(reason, fallback);
  return getSafeAnalyticsErrorMessage(pageError.message, pageError.errorCode, fallback);
}

function createInventorySignalWindow() {
  const toDate = new Date();
  const fromDate = new Date(toDate);
  fromDate.setUTCDate(fromDate.getUTCDate() - INVENTORY_SIGNAL_LOOKBACK_DAYS);
  return { fromDate: fromDate.toISOString(), toDate: toDate.toISOString() };
}

function toActionDataQualityStatus(value: string | null | undefined): AnalyticsActionDataQualityStatus {
  const normalized = (value ?? "").trim().toLowerCase();
  if (normalized === "good" || normalized === "warning" || normalized === "critical" || normalized === "insufficient_data") {
    return normalized;
  }

  return "insufficient_data";
}

function resolveLatestTimestamp(values: Array<string | null | undefined>): string | null {
  const latest = values
    .filter((value): value is string => Boolean(value))
    .map((value) => ({ value, time: new Date(value).getTime() }))
    .filter((entry) => !Number.isNaN(entry.time))
    .sort((left, right) => right.time - left.time)[0];

  return latest?.value ?? null;
}

type InventoryTrustSource = {
  label: string;
  meta?: AnalyticsResponseMeta | null;
};

type InventoryTrustAggregation = {
  meta: AnalyticsResponseMeta | null;
  sourceLabels: string[];
  degradedSourceLabels: string[];
};

const INVENTORY_TRUST_QUALITY_RANK: Record<string, number> = {
  good: 0,
  unknown: 1,
  warning: 2,
  insufficient_data: 2,
  stale: 3,
  critical: 4,
  error: 5,
};

function normalizeInventoryTrustQuality(meta: AnalyticsResponseMeta): string {
  if (meta.success === false || meta.errorCode) return "error";

  const normalized = (meta.dataQualityStatus ?? "").trim().toLowerCase();
  if (normalized === "critical" || normalized === "stale" || normalized === "insufficient_data") return normalized;
  if (normalized === "warning" || isAnalyticsMetaWarning(meta)) return "warning";
  if (normalized === "good" && !isAnalyticsMetaWarning(meta)) return "good";
  return "unknown";
}

function aggregateInventoryTrust(sources: InventoryTrustSource[]): InventoryTrustAggregation {
  const availableSources = sources.filter((source): source is InventoryTrustSource & { meta: AnalyticsResponseMeta } => Boolean(source.meta));
  if (availableSources.length === 0) {
    return { meta: null, sourceLabels: [], degradedSourceLabels: [] };
  }

  const rankedSources = availableSources
    .map((source) => ({ ...source, quality: normalizeInventoryTrustQuality(source.meta) }))
    .sort((left, right) => (INVENTORY_TRUST_QUALITY_RANK[right.quality] ?? 1) - (INVENTORY_TRUST_QUALITY_RANK[left.quality] ?? 1));
  const worstSource = rankedSources[0];
  const allRefreshTimestampsKnown = availableSources.every((source) => {
    const value = source.meta.lastRefreshAtUtc;
    return typeof value === "string" && value.length > 0 && !Number.isNaN(new Date(value).getTime());
  });
  const oldestRefreshAt = allRefreshTimestampsKnown
    ? availableSources
      .map((source) => source.meta.lastRefreshAtUtc!)
      .sort((left, right) => new Date(left).getTime() - new Date(right).getTime())[0]
    : null;
  const degradedSourceLabels = rankedSources
    .filter((source) => source.quality !== "good")
    .map((source) => source.label);
  const worstMessage = worstSource.meta.warningMessage ?? worstSource.meta.errorMessage ?? worstSource.meta.message ?? null;
  const compositeMessage = degradedSourceLabels.length > 0 && worstMessage
    ? `${degradedSourceLabels.join(", ")}: ${worstMessage}`
    : worstMessage;

  return {
    sourceLabels: availableSources.map((source) => source.label),
    degradedSourceLabels,
    meta: {
      ...worstSource.meta,
      success: availableSources.every((source) => source.meta.success === true),
      dataQualityStatus: worstSource.quality,
      isPartial: availableSources.some((source) => source.meta.isPartial === true || isAnalyticsMetaWarning(source.meta))
        || worstSource.quality !== "good",
      lastRefreshAtUtc: oldestRefreshAt,
      warningMessage: compositeMessage,
    },
  };
}

type SnapshotFreshnessSource = {
  timestamp?: string | null;
  status?: string | null;
};

type SecondarySnapshotFreshness = {
  timestamp: string | null;
  status: "fresh" | "stale" | "critical" | "unknown";
};

function normalizeSnapshotFreshnessStatus(value: string | null | undefined): SecondarySnapshotFreshness["status"] {
  const normalized = (value ?? "").trim().toLowerCase();
  if (normalized === "trusted" || normalized === "fresh") return "fresh";
  if (normalized === "stale") return "stale";
  if (normalized === "critical") return "critical";
  return "unknown";
}

function resolveSecondarySnapshotFreshness(sources: SnapshotFreshnessSource[]): SecondarySnapshotFreshness {
  const normalized = sources.map((source) => ({
    timestamp: source.timestamp ?? null,
    status: normalizeSnapshotFreshnessStatus(source.status),
  }));
  const status = normalized.some((source) => source.status === "critical")
    ? "critical"
    : normalized.some((source) => source.status === "stale")
      ? "stale"
      : normalized.some((source) => source.status === "fresh")
        ? "fresh"
        : "unknown";
  const timestamp = resolveLatestTimestamp(
    normalized
      .filter((source) => source.status !== "unknown")
      .map((source) => source.timestamp),
  );

  return { timestamp, status };
}

function snapshotFreshnessLabel(status: SecondarySnapshotFreshness["status"]): string {
  if (status === "fresh") return "potvrđeno svež";
  if (status === "stale") return "zastareo";
  if (status === "critical") return "kritično zastareo";
  return "nepoznat";
}

export function buildInventorySignalActionSpec(row: InventoryRow): {
  sourceKey: string;
  title: string;
  recommendationStatus: string;
  priority: "P1" | "P2" | "P3";
  description: string;
  dueAtUtc: string;
  expectedImpactRsd?: number | null;
} {
  const dueAtUtc = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString();
  const normalizedCover = (row.stockCoverStatus ?? "").trim().toLowerCase();
  const normalizedSellThrough = (row.sellThroughStatus ?? "").trim().toLowerCase();

  if (row.recommendationAllowed !== true || normalizedCover === "insufficient_data" || normalizedSellThrough === "insufficient_data") {
    return {
      sourceKey: `inventory:signal_check:${row.id}:${row.idObjekat ?? "all"}`,
      title: `Proveri signal zalihe: ${row.naziv}`,
      recommendationStatus: "SIGNAL_REVIEW",
      priority: "P2",
      description: `Signal nije dovoljan za finalnu akciju. Pokrivenost zalihe: ${row.stockCoverStatusLabel}. Prodajni obrt: ${row.sellThroughStatusLabel}.`,
      dueAtUtc,
      // Exposure may exist on the row, but a review action must not claim confirmed expected impact.
      expectedImpactRsd: null,
    };
  }

  if (normalizedCover === "out_of_stock_risk" || normalizedCover === "low_cover" || normalizedCover === "low") {
    const isCritical = normalizedCover === "out_of_stock_risk";
    return {
      sourceKey: `inventory:replenish:${row.id}:${row.idObjekat ?? "all"}`,
      title: `Dopuni artikal: ${row.naziv}`,
      recommendationStatus: "REPLENISH",
      priority: isCritical ? "P1" : "P2",
      description: `${row.signalText}. Pokrivenost zalihe: ${row.stockCoverStatusLabel}. Prodajni obrt: ${row.sellThroughStatusLabel}.`,
      dueAtUtc,
      // Stock exposure exists on the row, but inventory has no authoritative expected-impact source.
      expectedImpactRsd: null,
    };
  }

  if (normalizedCover === "slow_stock" || normalizedCover === "slow" || normalizedCover === "no_velocity") {
    return {
      sourceKey: `inventory:slow_stock_review:${row.id}:${row.idObjekat ?? "all"}`,
      title: `Proveri sporu zalihu: ${row.naziv}`,
      recommendationStatus: "SLOW_STOCK_REVIEW",
      priority: normalizedCover === "slow_stock" || normalizedCover === "slow" ? "P2" : "P3",
      description: `${row.signalText}. Artikal zahteva proveru sporog obrta i odluke o markdown/transfer akciji.`,
      dueAtUtc,
      expectedImpactRsd: null,
    };
  }

  return {
    sourceKey: `inventory:signal_check:${row.id}:${row.idObjekat ?? "all"}`,
    title: `Proveri signal zalihe: ${row.naziv}`,
    recommendationStatus: "SIGNAL_REVIEW",
    priority: "P2",
    description: `Signal nije dovoljan za finalnu akciju. Pokrivenost zalihe: ${row.stockCoverStatusLabel}. Prodajni obrt: ${row.sellThroughStatusLabel}.`,
    dueAtUtc,
    expectedImpactRsd: null,
  };
}

export default function InventoryPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [schedules, setSchedules] = useState<InventoryReportSchedule[]>([]);
  const [stores, setStores] = useState<StoreOption[]>([]);
  const [suppliers, setSuppliers] = useState<SupplierFilterOption[]>([]);
  const [supplierFiltersWarning, setSupplierFiltersWarning] = useState<string | null>(null);
  const [supplierFiltersStale, setSupplierFiltersStale] = useState(false);
  const suppliersRef = useRef(suppliers);
  suppliersRef.current = suppliers;
  const [filtersLoading, setFiltersLoading] = useState(true);
  const [searchInput, setSearchInput] = useState(() => searchParams.get("search") ?? "");
  const [selectedStoreId, setSelectedStoreId] = useState<number | null>(() => {
    const parsed = parseInventoryPositiveInt(searchParams.get("storeId"), 0);
    return parsed > 0 ? parsed : null;
  });
  const [selectedSupplierId, setSelectedSupplierId] = useState<number | null>(() => {
    const parsed = parseInventoryPositiveInt(searchParams.get("supplierId"), 0);
    return parsed > 0 ? parsed : null;
  });
  const [compareStoreIds, setCompareStoreIds] = useState<number[]>(() => parseInventoryCompareStores(searchParams.get("compareStores")));
  const [sortBy, setSortBy] = useState(() => parseInventorySort(searchParams.get("sortBy")));
  const [pageNumber, setPageNumber] = useState(() => parseInventoryPositiveInt(searchParams.get("page"), 1));
  const [pageSize, setPageSize] = useState(() => parseInventoryPageSize(searchParams.get("pageSize")));
  const [detailRow, setDetailRow] = useState<InventoryRow | null>(null);
  const [detailTab, setDetailTab] = useState<"overview" | "sizeCurve">("overview");
  const [detailData, setDetailData] = useState<InventoryItemDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [detailSizeCurve, setDetailSizeCurve] = useState<SizeCurveDto | null>(null);
  const [detailSizeCurveLoading, setDetailSizeCurveLoading] = useState(false);
  const [exportBusy, setExportBusy] = useState(false);
  const [exportStatus, setExportStatus] = useState<string | null>(null);
  const [printOrientation, setPrintOrientation] = useState<"landscape" | "portrait">("landscape");
  const [workflowBusyKey, setWorkflowBusyKey] = useState<string | null>(null);
  const [queueBusyKey, setQueueBusyKey] = useState<string | null>(null);
  const [queuedSuggestionKeys, setQueuedSuggestionKeys] = useState<string[]>([]);
  const [workflowOverride, setWorkflowOverride] = useState<InventoryActionWorkflow | null>(null);
  const [schedulerBusy, setSchedulerBusy] = useState(false);
  const [schedulerMessage, setSchedulerMessage] = useState<string | null>(null);
  const [scheduleDraft, setScheduleDraft] = useState<InventoryReportScheduleInput>(createScheduleDraft);
  const [alertSeverityFilter, setAlertSeverityFilter] = useState<"" | "critical" | "warning" | "info">("");
  const [sizeCurve, setSizeCurve] = useState<SizeCurveDto | null>(null);
  const [sizeCurveLoading, setSizeCurveLoading] = useState(false);
  const [sizeCurveError, setSizeCurveError] = useState<string | null>(null);
  const [sizeCurveSkuId, setSizeCurveSkuId] = useState<number | null>(null);
  const [reloadNonce, setReloadNonce] = useState(0);
  const [inventoryDataScope, setInventoryDataScope] = useState(() => getDataScope());
  const deferredSearch = useDeferredValue(searchInput);
  const trimmedSearch = deferredSearch.trim();
  const inventorySignalWindow = useMemo(createInventorySignalWindow, [reloadNonce, inventoryDataScope]);
  const exportContractNote = useMemo(
    () => buildInventoryServerExportContractNote(inventoryDataScope),
    [inventoryDataScope],
  );
  const serverSortBy = isInventoryPageLocalRiskSort(sortBy) ? "kolicina" : sortBy;
  const selectedStoreName = selectedStoreId == null ? null : stores.find((store) => store.storeId === selectedStoreId)?.storeName ?? null;
  const rebalanceScopeLabel = selectedStoreId == null
    ? "za sve prodavnice"
    : `za prodavnicu ${selectedStoreName ?? `#${selectedStoreId}`}`;
  const mountedRef = useRef(true);

  useEffect(() => {
    const nextSearch = searchParams.get("search") ?? "";
    const nextStore = parseInventoryPositiveInt(searchParams.get("storeId"), 0);
    const nextSupplier = parseInventoryPositiveInt(searchParams.get("supplierId"), 0);
    setSearchInput((current) => current === nextSearch ? current : nextSearch);
    setSelectedStoreId((current) => {
      const next = nextStore > 0 ? nextStore : null;
      return current === next ? current : next;
    });
    setSelectedSupplierId((current) => {
      const next = nextSupplier > 0 ? nextSupplier : null;
      return current === next ? current : next;
    });
    setCompareStoreIds((current) => {
      const next = parseInventoryCompareStores(searchParams.get("compareStores"));
      return current.length === next.length && current.every((value, index) => value === next[index]) ? current : next;
    });
    setSortBy((current) => {
      const next = parseInventorySort(searchParams.get("sortBy"));
      return current === next ? current : next;
    });
    setPageNumber((current) => {
      const next = parseInventoryPositiveInt(searchParams.get("page"), 1);
      return current === next ? current : next;
    });
    setPageSize((current) => {
      const next = parseInventoryPageSize(searchParams.get("pageSize"));
      return current === next ? current : next;
    });
  }, [searchParams]);

  useEffect(() => {
    setSearchParams((current) => {
      const next = new URLSearchParams(current);
      const setOrDelete = (key: string, value: string | null) => {
        if (value == null || value.length === 0) next.delete(key);
        else next.set(key, value);
      };
      setOrDelete("search", searchInput);
      setOrDelete("storeId", selectedStoreId == null ? null : String(selectedStoreId));
      setOrDelete("supplierId", selectedSupplierId == null ? null : String(selectedSupplierId));
      setOrDelete("compareStores", compareStoreIds.length > 0 ? compareStoreIds.join(",") : null);
      setOrDelete("sortBy", sortBy === "kolicina" ? null : sortBy);
      setOrDelete("page", pageNumber === 1 ? null : String(pageNumber));
      setOrDelete("pageSize", pageSize === DEFAULT_INVENTORY_PAGE_SIZE ? null : String(pageSize));
      return next.toString() === current.toString() ? current : next;
    }, { replace: true });
  }, [compareStoreIds, pageNumber, pageSize, searchInput, selectedStoreId, selectedSupplierId, setSearchParams, sortBy]);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

  useEffect(() => {
    const handleScopeChange = () => {
      if (mountedRef.current) {
        const nextDataScope = getDataScope();
        if (nextDataScope === inventoryDataScope) {
          setReloadNonce((current) => current + 1);
        } else {
          // A supplier selected in the previous dataset must not narrow the next dataset.
          setSelectedSupplierId(null);
          setPageNumber(1);
          setInventoryDataScope(nextDataScope);
        }
      }
    };

    window.addEventListener("trendplus:data-scope-changed", handleScopeChange);
    return () => window.removeEventListener("trendplus:data-scope-changed", handleScopeChange);
  }, [inventoryDataScope]);

  useEffect(() => {
    let cancelled = false;
    void getStores(true)
      .then((nextStores) => {
        if (cancelled) return;
        setStores(nextStores);
        setCompareStoreIds((current) => {
          const next = current.length > 0
            ? current
            : nextStores.slice(0, DEFAULT_COMPARE_STORES).map((store) => store.storeId);
          if (current.length === next.length && current.every((id, index) => id === next[index])) {
            return current;
          }
          return next;
        });
      })
      .catch(console.error)
      .finally(() => {
        if (!cancelled) setFiltersLoading(false);
      });
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    let cancelled = false;
    void getInventoryReportSchedules()
      .then((nextSchedules) => {
        if (!cancelled) setSchedules(nextSchedules);
      })
      .catch((reason) => {
        if (!cancelled) setSchedulerMessage(toSafeInventoryInlineError(reason, "Rasporedi izveštaja trenutno nisu dostupni."));
      });
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    let cancelled = false;
    void getSupplierFilters(undefined, undefined, true, selectedStoreId ?? undefined, inventoryDataScope)
      .then((nextSuppliers) => {
        if (cancelled) return;
        const resolved = resolveSupplierFilterFallbackState(nextSuppliers, suppliersRef.current);
        setSupplierFiltersWarning(resolved.warning);
        setSupplierFiltersStale(resolved.isStale);
        setSuppliers(resolved.suppliers);
        if (resolved.shouldClearSelection && selectedSupplierId != null) {
          setSelectedSupplierId(null);
          return;
        }
        if (
          !resolved.isStale
          && selectedSupplierId != null
          && !resolved.suppliers.some((entry) => entry.supplierId === selectedSupplierId)
        ) {
          setSelectedSupplierId(null);
        }
      })
      .catch(() => {
        if (!cancelled) {
          // Preserve prior options without claiming that they match the active period and scope.
          setSupplierFiltersWarning(SUPPLIER_FILTER_LOAD_FAILED_MESSAGE);
          setSupplierFiltersStale(true);
          setSelectedSupplierId(null);
        }
      });
    return () => { cancelled = true; };
  }, [inventoryDataScope, selectedStoreId, selectedSupplierId]);

  const inventoryQuery = useCallback(async (signal: AbortSignal): Promise<InventoryLifecycleSnapshot> => {
    const results = await Promise.allSettled([
      getInventoryBalance(true, selectedStoreId, selectedSupplierId, inventoryDataScope, signal),
      getInventoryList({
        pageNumber,
        pageSize,
        search: trimmedSearch || undefined,
        storeId: selectedStoreId,
        supplierId: selectedSupplierId,
        sortBy: serverSortBy,
        dataScope: inventoryDataScope,
        signal,
        ...inventorySignalWindow,
      }),
      getInventoryInsights({
        search: trimmedSearch || undefined,
        storeId: selectedStoreId,
        supplierId: selectedSupplierId,
        sortBy: serverSortBy,
        dataScope: inventoryDataScope,
        signal,
      }),
      getInventoryStoreComparison({
        compareStoreIds,
        supplierId: selectedSupplierId,
        search: trimmedSearch || undefined,
        dataScope: inventoryDataScope,
        signal,
      }),
      getInventoryActionSuggestions({
        storeId: selectedStoreId,
        supplierId: selectedSupplierId,
        search: trimmedSearch || undefined,
        dataScope: inventoryDataScope,
        signal,
      }),
      getForecast({ storeId: selectedStoreId, supplierId: selectedSupplierId, top: FORECAST_FETCH_LIMIT, signal }),
      getInventoryAlerts({ storeId: selectedStoreId, supplierId: selectedSupplierId, signal }),
      getRebalanceSuggestions({ fromStoreId: selectedStoreId, supplierId: selectedSupplierId, top: REBALANCE_FETCH_LIMIT, signal }),
    ]);
    const failed = results.find((result) => result.status === "rejected");
    if (failed?.status === "rejected") throw failed.reason;

    return {
      balance: (results[0] as PromiseFulfilledResult<InventoryBalance>).value,
      pageData: (results[1] as PromiseFulfilledResult<InventoryPagedResponse>).value,
      insights: (results[2] as PromiseFulfilledResult<InventoryInsights>).value,
      storeComparison: (results[3] as PromiseFulfilledResult<InventoryStoreComparison>).value,
      actionWorkflow: (results[4] as PromiseFulfilledResult<InventoryActionWorkflow>).value,
      forecast: (results[5] as PromiseFulfilledResult<ForecastDto>).value,
      alerts: (results[6] as PromiseFulfilledResult<InventoryAlertListDto>).value,
      rebalance: (results[7] as PromiseFulfilledResult<RebalanceListDto>).value,
    };
  }, [compareStoreIds, inventoryDataScope, inventorySignalWindow, pageNumber, pageSize, selectedStoreId, selectedSupplierId, serverSortBy, trimmedSearch]);
  const {
    data: inventorySnapshot,
    initialLoading,
    refetching,
    error: queryError,
    errorReason,
    staleWarning,
    staleReason,
    refetch,
  } = useReliableAnalyticsQuery<InventoryLifecycleSnapshot>({
    query: inventoryQuery,
    getErrorMessage: useCallback(
      (reason: unknown) => toInventoryPageError(reason, "Podaci o zalihama trenutno nisu dostupni.").message,
      [],
    ),
  });
  const balance = inventorySnapshot?.balance ?? null;
  const pageData = inventorySnapshot?.pageData ?? null;
  const insights = inventorySnapshot?.insights ?? null;
  const storeComparison = inventorySnapshot?.storeComparison ?? null;
  const actionWorkflow = inventorySnapshot?.actionWorkflow ?? null;
  const forecast = inventorySnapshot?.forecast ?? null;
  const alerts = inventorySnapshot?.alerts ?? null;
  const rebalance = inventorySnapshot?.rebalance ?? null;
  const loading = initialLoading || refetching;
  const insightsLoading = loading;
  const insightsError = queryError ?? staleWarning;
  const operationsLoading = loading;
  const forecastLoading = loading;
  const alertsLoading = loading;
  const rebalanceLoading = loading;
  const forecastError = queryError;
  const alertsError = queryError;
  const rebalanceError = queryError;
  useEffect(() => {
    setWorkflowOverride(inventorySnapshot?.actionWorkflow ?? null);
  }, [inventorySnapshot]);
  const effectiveActionWorkflow = workflowOverride ?? actionWorkflow;
  const inventoryError = queryError || staleWarning
    ? toInventoryPageError(
      errorReason ?? staleReason ?? queryError ?? staleWarning,
      "Podaci o zalihama trenutno nisu dostupni.",
    )
    : null;
  const error = inventoryError;

  useEffect(() => {
    if (!detailRow) {
      setDetailData(null);
      setDetailError(null);
      setDetailLoading(false);
      setDetailSizeCurve(null);
      setDetailSizeCurveLoading(false);
      return;
    }
    let cancelled = false;
    const controller = new AbortController();
    setDetailLoading(true);
    setDetailError(null);
    void getInventoryItemDetail(detailRow.id, {
      storeId: selectedStoreId ?? detailRow.idObjekat,
      supplierId: selectedSupplierId ?? detailRow.idDobavljac,
      dataScope: inventoryDataScope,
      signal: controller.signal,
      ...inventorySignalWindow,
    })
      .then((nextDetail) => {
        if (!cancelled) setDetailData(nextDetail);
      })
      .catch((reason) => {
        if (!cancelled) {
          setDetailData(null);
          setDetailError(toSafeInventoryInlineError(reason, "Detalj artikla trenutno nije dostupan."));
          setDetailRow((current) =>
            current?.contextStatus === "loadingContext"
              ? { ...current, contextStatus: "contextMissing" }
              : current,
          );
        }
      })
      .finally(() => {
        if (!cancelled) setDetailLoading(false);
      });
    return () => {
      cancelled = true;
      controller.abort();
    };
  }, [detailRow, inventoryDataScope, inventorySignalWindow, selectedStoreId, selectedSupplierId]);

  useEffect(() => {
    if (!detailRow || detailTab !== "sizeCurve") {
      setDetailSizeCurve(null);
      setDetailSizeCurveLoading(false);
      return;
    }
    let cancelled = false;
    const controller = new AbortController();
    setDetailSizeCurveLoading(true);
    void getSizeCurve({
      skuId: detailRow.id,
      storeId: detailRow.idObjekat ?? selectedStoreId ?? undefined,
      signal: controller.signal,
    })
      .then((nextCurve) => {
        if (!cancelled) setDetailSizeCurve(nextCurve);
      })
      .catch(() => {
        if (!cancelled) setDetailSizeCurve(null);
      })
      .finally(() => {
        if (!cancelled) setDetailSizeCurveLoading(false);
      });
    return () => {
      cancelled = true;
      controller.abort();
    };
  }, [detailRow, detailTab, selectedStoreId]);

  useEffect(() => {
    if (sizeCurveSkuId == null) {
      setSizeCurve(null);
      setSizeCurveError(null);
      return;
    }
    let cancelled = false;
    const controller = new AbortController();
    setSizeCurveLoading(true);
    setSizeCurveError(null);
    void getSizeCurve({ skuId: sizeCurveSkuId, storeId: selectedStoreId, signal: controller.signal })
      .then((data) => {
        if (!cancelled) setSizeCurve(data);
      })
      .catch((reason) => {
        if (!cancelled) {
          setSizeCurve(null);
          setSizeCurveError(toSafeInventoryInlineError(reason, "Signal raspodele veličina trenutno nije dostupan."));
        }
      })
      .finally(() => {
        if (!cancelled) setSizeCurveLoading(false);
      });
    return () => {
      cancelled = true;
      controller.abort();
    };
  }, [sizeCurveSkuId, selectedStoreId]);

  const rows = useMemo(() => (pageData?.items ?? []).map((item) => buildInventoryRow(item, stores, suppliers)), [pageData, stores, suppliers]);
  const totalCount = pageData?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const totalValue = balance?.estimatedInventoryValue ?? null;
  const activeSkuShare = useMemo(() => (balance && balance.totalSku > 0 ? ((balance.totalSku - balance.outOfStockCount) / balance.totalSku) * 100 : null), [balance]);
  const lowStockShare = useMemo(() => (balance && balance.totalSku > 0 ? (balance.lowStockCount / balance.totalSku) * 100 : null), [balance]);
  const avgUnitsPerSku = useMemo(() => (balance && balance.totalSku > 0 ? balance.totalOnHand / balance.totalSku : null), [balance]);
  const chartData = useMemo(() => buildSupplierChart(rows).sort((left, right) => right.totalValue - left.totalValue).slice(0, TOP_SUPPLIERS_CHART), [rows]);
  const topRiskRows = useMemo(
    () =>
      rows
        .slice()
        .sort((left, right) => {
          const rank: Record<InventoryRow["stockState"], number> = {
            critical: 0,
            warning: 1,
            unknown: 2,
            healthy: 3,
          };
          const byState = rank[left.stockState] - rank[right.stockState];
          if (byState !== 0) return byState;
          return (right.reorderGap ?? -1) - (left.reorderGap ?? -1);
        })
        .slice(0, TOP_RISK_ITEMS),
    [rows],
  );
  const highestValueRows = useMemo(() => rows.slice().sort((left, right) => (right.estimatedValueAmount ?? Number.NEGATIVE_INFINITY) - (left.estimatedValueAmount ?? Number.NEGATIVE_INFINITY)).slice(0, TOP_VALUE_ITEMS), [rows]);
  const forecastMetricsByRowKey = useMemo(() => new Map(rows.map((row) => {
    const matching = (forecast?.items ?? []).filter((item) => item.skuId === row.id && (row.idObjekat == null || item.storeId === row.idObjekat));
    const oosRisk = matching.reduce((max, item) => item.probabilityOfOOSIn7d == null ? max : Math.max(max, item.probabilityOfOOSIn7d), Number.NEGATIVE_INFINITY);
    const overstockRisk = matching.reduce((max, item) => item.overstockRisk == null ? max : Math.max(max, item.overstockRisk), Number.NEGATIVE_INFINITY);
    return [`${row.id}:${row.idObjekat ?? 0}`, {
      oosRisk: Number.isFinite(oosRisk) ? oosRisk : null,
      overstockRisk: Number.isFinite(overstockRisk) ? overstockRisk : null,
    }];
  })), [forecast, rows]);
  const displayedRows = useMemo(() => {
    if (!isInventoryPageLocalRiskSort(sortBy)) return rows;
    return rows.slice().sort((left, right) => {
      const leftMetrics = forecastMetricsByRowKey.get(`${left.id}:${left.idObjekat ?? 0}`);
      const rightMetrics = forecastMetricsByRowKey.get(`${right.id}:${right.idObjekat ?? 0}`);
      return sortBy === "oosRisk"
        ? (rightMetrics?.oosRisk ?? -1) - (leftMetrics?.oosRisk ?? -1)
        : (rightMetrics?.overstockRisk ?? -1) - (leftMetrics?.overstockRisk ?? -1);
    });
  }, [forecastMetricsByRowKey, rows, sortBy]);

  const riskSortScopeWarning = useMemo(
    () => inventoryRiskSortScopeWarning(sortBy, { pageSize, totalPages, totalCount }),
    [pageSize, sortBy, totalCount, totalPages],
  );

  useEffect(() => {
    let cancelled = false;

    const signalKeys = displayedRows.map((row) => buildInventorySignalActionSpec(row).sourceKey);
    const workflowKeys = (effectiveActionWorkflow?.items ?? [])
      .map((item) => item.suggestionKey)
      .filter((key) => Boolean(key));
    const sourceKeys = Array.from(new Set([...signalKeys, ...workflowKeys]));

    if (sourceKeys.length === 0) {
      setQueuedSuggestionKeys([]);
      return () => {
        cancelled = true;
      };
    }

      void (async () => {
        try {
          const response = await getAnalyticsActionSourceStatuses({
            items: sourceKeys.map((sourceKey) => ({
              sourceType: "inventory",
            sourceKey,
          })),
        });

        if (cancelled) return;
        setQueuedSuggestionKeys(
          response.items
            .filter((item: { exists: boolean }) => item.exists)
            .map((item: { sourceKey: string }) => item.sourceKey),
        );
        } catch (reason) {
        if (!cancelled) {
          // Keep the last known queue state when the lookup fails so queued items do not look unqueued.
          console.warn("Neuspešna provera statusa inventory akcija po sourceKey.", reason);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [displayedRows, effectiveActionWorkflow]);

  const signalKpis = useMemo(
    () => computeInventorySignalKpis(rows, totalCount, pageSize),
    [pageSize, rows, totalCount],
  );
  const primaryInventoryTrust = useMemo(
    () => aggregateInventoryTrust([
      { label: "Lista artikala", meta: pageData?.meta },
      { label: "Bilans", meta: balance?.meta },
      { label: "Insights", meta: insights?.meta },
    ]),
    [balance?.meta, insights?.meta, pageData?.meta],
  );
  const secondaryPanelFreshness = useMemo(
    () => resolveSecondarySnapshotFreshness([
      { timestamp: forecast?.snapshotFreshnessUtc, status: forecast?.provenanceStatus },
      { timestamp: alerts?.snapshotFreshnessUtc, status: alerts?.snapshotFreshnessStatus },
      { timestamp: rebalance?.snapshotFreshnessUtc, status: rebalance?.snapshotFreshnessStatus },
      { timestamp: sizeCurve?.snapshotFreshnessUtc, status: sizeCurve?.snapshotFreshnessStatus },
    ]),
    [alerts?.snapshotFreshnessStatus, alerts?.snapshotFreshnessUtc, forecast?.provenanceStatus, forecast?.snapshotFreshnessUtc, rebalance?.snapshotFreshnessStatus, rebalance?.snapshotFreshnessUtc, sizeCurve?.snapshotFreshnessStatus, sizeCurve?.snapshotFreshnessUtc],
  );
  const secondaryPanelsSettled = !forecastLoading && !alertsLoading && !rebalanceLoading && !sizeCurveLoading;
  const primaryRefreshAt = primaryInventoryTrust.meta?.lastRefreshAtUtc ?? null;
  const primaryMeta = primaryInventoryTrust.meta;
  const inventoryMetas = useMemo(
    () => ([primaryMeta, storeComparison?.meta, effectiveActionWorkflow?.meta].filter((meta): meta is AnalyticsResponseMeta => Boolean(meta))),
    [effectiveActionWorkflow?.meta, primaryMeta, storeComparison?.meta],
  );
  const warningMeta = inventoryMetas.find((meta) => isAnalyticsMetaWarning(meta)) ?? null;
  const inventoryMetaMessage = getAnalyticsMetaMessage(warningMeta ?? primaryMeta);
  const showMetaWarning = !loading && !error && warningMeta != null;
  const dataQualityNeedsReview = !loading
    && !error
    && inventoryMetas.some((meta) => {
      const normalizedStatus = meta.dataQualityStatus?.trim().toLowerCase() ?? "";
      return isAnalyticsMetaWarning(meta)
        || normalizedStatus === "warning"
        || normalizedStatus === "critical"
        || normalizedStatus === "insufficient_data"
        || normalizedStatus === "error";
    });
  const showInsufficientEmptyState = !loading
    && !error
    && shouldShowAnalyticsEmptyState(primaryMeta, totalCount)
    && isAnalyticsMetaInsufficient(primaryMeta);
  const hasActivePrimaryFilters = Boolean(trimmedSearch) || selectedStoreId != null || selectedSupplierId != null;
  const emptyReasonCode = primaryMeta?.emptyReason?.trim().toLowerCase() ?? "";
  const showFilteredEmptyState = !showInsufficientEmptyState
    && (hasActivePrimaryFilters || emptyReasonCode.includes("filter"));
  const showEmptyState = !loading && !error && pageData != null && (showInsufficientEmptyState || totalCount === 0);
  const freshnessLineageNote = useMemo(() => {
    if (!secondaryPanelsSettled) {
      return null;
    }

    const freshnessLabel = snapshotFreshnessLabel(secondaryPanelFreshness.status);
    if (!secondaryPanelFreshness.timestamp) {
      return `Sekundarni inventory snapshoti imaju status svežine „${freshnessLabel}“. Vreme odgovora nije poslednje uspešno osvežavanje.`;
    }

    if (!primaryRefreshAt) {
      return `Sekundarni paneli imaju ${freshnessLabel} svežinu: ${formatDateTime(secondaryPanelFreshness.timestamp)}. Primarni bilans nema potvrđen refresh.`;
    }

    const primaryTime = new Date(primaryRefreshAt).getTime();
    const secondaryTime = new Date(secondaryPanelFreshness.timestamp).getTime();
    const deltaMinutes = Math.abs(secondaryTime - primaryTime) / 60000;

    if (deltaMinutes < 30) {
      return null;
    }

    return `Primarni bilans je osvežen ${formatDateTime(primaryRefreshAt)}, a sekundarni snapshoti (${freshnessLabel}) ${formatDateTime(secondaryPanelFreshness.timestamp)}.`;
  }, [primaryRefreshAt, secondaryPanelFreshness, secondaryPanelsSettled]);
  const signalSearchLineageNote = searchInput.trim().length > 0
    ? "Napomena: tekst pretraga ne utiče na prognozu, upozorenja i redistribuciju; ti paneli slede samo prodavnicu i dobavljača."
    : null;

  const refreshSchedules = async () => setSchedules(await getInventoryReportSchedules());
  const refreshOperations = async () => {
    await refetch();
  };

  async function runServerExport(format: "pdf" | "xlsx" | "csv", preview = false) {
    if (totalCount === 0 || exportBusy) return;
    try {
      setExportBusy(true);
      setExportStatus(preview ? "Pripremam print preview na serveru..." : "Server priprema dokument za izvoz...");
      if (preview) {
        const previewResult = await previewInventoryReport({ orientation: printOrientation, includeFiltersAndMetadata: true, search: trimmedSearch || undefined, storeId: selectedStoreId, supplierId: selectedSupplierId, sortBy: serverSortBy, dataScope: inventoryDataScope });
        if (previewResult.printUrl) window.open(resolveApiUrl(previewResult.printUrl), "_blank", "noopener");
        setExportStatus("Print preview je otvoren u novom tabu.");
        return;
      }
      const result = await exportInventoryReport({ format, orientation: printOrientation, includeFiltersAndMetadata: true, forceAsync: totalCount > 5000, search: trimmedSearch || undefined, storeId: selectedStoreId, supplierId: selectedSupplierId, sortBy: serverSortBy, dataScope: inventoryDataScope });
      if (result.isAsync) {
        setExportStatus("Dokument je u redu cekanja. Cekam da eksport bude spreman...");
        const completed = await waitForExport(result.documentId);
        if (completed.downloadUrl) downloadExport(completed.downloadUrl, completed.fileName);
        setExportStatus("Eksport je zavrsen i preuzet.");
      } else if (result.downloadUrl) {
        downloadExport(result.downloadUrl, result.fileName);
        setExportStatus("Eksport je preuzet.");
      }
    } catch (reason) {
      setExportStatus(toSafeInventoryInlineError(reason, "Eksport nije uspeo."));
    } finally {
      setExportBusy(false);
    }
  }

  async function runBlankPrint() {
    try {
      setExportBusy(true);
      setExportStatus("Pripremam prazan obrazac za stampu...");
      const result = await printBlankInventoryForm({ orientation: printOrientation });
      if (result.printUrl) window.open(resolveApiUrl(result.printUrl), "_blank", "noopener");
      setExportStatus("Prazan obrazac je otvoren u novom tabu.");
    } catch (reason) {
      setExportStatus(toSafeInventoryInlineError(reason, "Priprema praznog obrasca nije uspela."));
    } finally {
      setExportBusy(false);
    }
  }

  function exportVisibleCsv() {
    // Screen CSV must follow the visible table order (displayedRows), including page-local risk sorts.
    const lines = buildInventoryScreenCsvLines(displayedRows);
    const blob = new Blob([`\uFEFF${lines.join("\n")}`], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = buildInventoryScreenCsvFilename(pageNumber, sortBy);
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
    const sortNote = isInventoryPageLocalRiskSort(sortBy)
      ? ` (redosled: ${sortBy === "oosRisk" ? "OOS rizik" : "Overstock rizik"}, trenutna strana)`
      : "";
    setExportStatus(`CSV za trenutnu stranu${sortNote} je preuzet.`);
  }

  async function updateWorkflowStatus(item: InventoryActionSuggestion, status: "approved" | "deferred" | "closed") {
    try {
      setWorkflowBusyKey(item.suggestionKey);
      await saveInventoryActionDecision(item.suggestionKey, { actionType: item.actionType, status, note: item.note ?? "" });
      await refreshOperations();
    } catch (reason) {
      setExportStatus(toSafeInventoryInlineError(reason, "Čuvanje odluke nije uspelo."));
    } finally {
      setWorkflowBusyKey(null);
    }
  }

  function mapWorkflowPriorityToQueuePriority(priority: string): "P1" | "P2" | "P3" {
    const normalized = priority.trim().toLowerCase();
    if (normalized === "critical" || normalized === "high") return "P1";
    if (normalized === "medium") return "P2";
    if (normalized === "low" || normalized === "optional") return "P3";
    return "P2";
  }

  async function addWorkflowSuggestionToCentralQueue(item: InventoryActionSuggestion) {
    try {
      setQueueBusyKey(item.suggestionKey);
      const result = await upsertAnalyticsActionWithResult({
        sourceType: "inventory",
        sourceKey: item.suggestionKey,
        sourceId: item.artikalId,
        title: item.label,
        description: item.reason,
        recommendationStatus: item.actionType,
        priority: mapWorkflowPriorityToQueuePriority(item.priority),
        actionUrl: "/analytics/inventory",
        metadataJson: JSON.stringify(buildInventoryWorkflowCentralQueueMetadata(item)),
      });
      setQueuedSuggestionKeys((current) => (
        current.includes(item.suggestionKey) ? current : [...current, item.suggestionKey]
      ));
      setExportStatus(result.existing
        ? "Akcija je već u centralnim akcijama."
        : "Akcija je dodata u centralni red.");
    } catch (reason) {
      setExportStatus(getAnalyticsActionWriteErrorMessage(reason));
    } finally {
      setQueueBusyKey(null);
    }
  }

  async function saveSchedule() {
    const validationMessage = validateScheduleDraft(scheduleDraft);
    if (validationMessage) {
      setSchedulerMessage(validationMessage);
      return;
    }

    try {
      setSchedulerBusy(true);
      setSchedulerMessage("Cuvam raspored i pripremam scheduler...");
      await createInventoryReportSchedule(scheduleDraft);
      await refreshSchedules();
      setScheduleDraft(createScheduleDraft());
      setSchedulerMessage("Raspored je sacuvan.");
    } catch (reason) {
      setSchedulerMessage(toSafeInventoryInlineError(reason, "Čuvanje rasporeda nije uspelo."));
    } finally {
      setSchedulerBusy(false);
    }
  }

  async function runScheduleNow(id: number) {
    try {
      setSchedulerBusy(true);
      const result = await runInventoryReportScheduleNow(id);
      await refreshSchedules();
      setSchedulerMessage(result.message);
    } catch (reason) {
      setSchedulerMessage(toSafeInventoryInlineError(reason, "Ručno pokretanje nije uspelo."));
    } finally {
      setSchedulerBusy(false);
    }
  }

  function toggleCompareStore(storeId: number) {
    setCompareStoreIds((current) => current.includes(storeId) ? current.filter((value) => value !== storeId) : current.length >= DEFAULT_COMPARE_STORES ? [...current.slice(1), storeId] : [...current, storeId]);
  }

  function copyCurrentFiltersToSchedule() {
    setScheduleDraft((current) => ({ ...current, search: trimmedSearch, storeId: selectedStoreId, supplierId: selectedSupplierId, sortBy: serverSortBy }));
    setSchedulerMessage("Trenutni filteri su prepisani u scheduler formu.");
  }

  function scrollToSection(id: string) {
    document.getElementById(id)?.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  function openDetail(row: InventoryRow, tab: "overview" | "sizeCurve" = "overview") {
    setDetailRow(row);
    setDetailTab(tab);
  }

  function openDetailBySku(skuId: number, storeId?: number, label?: string) {
    const existingRow = rows.find((row) => row.id === skuId && (storeId == null || row.idObjekat === storeId)) ?? rows.find((row) => row.id === skuId);
    if (existingRow) {
      openDetail(existingRow);
      return;
    }
    openDetail(buildOffPageDetailPlaceholderRow(skuId, stores, suppliers, { storeId, label }));
  }

  function retryDetailFetch() {
    if (!detailRow) return;
    const currentRow = detailRow;
    setDetailRow(null);
    window.setTimeout(() => setDetailRow(currentRow), 0);
  }

  function queueForecastRestock(item: ForecastDto["items"][number]) {
    if (item.forecast7d == null || item.probabilityOfOOSIn7d == null) {
      setExportStatus("Forecast signal nema dovoljno evidencije za predlog dopune.");
      return;
    }

    const row = rows.find((entry) => entry.id === item.skuId && (entry.idObjekat == null || entry.idObjekat === item.storeId));
    if (!row) {
      setExportStatus("Predlog dopune nije moguće dodati bez učitanog stock baseline-a.");
      return;
    }

    if (detailRow?.id === item.skuId && detailLoading) {
      setExportStatus("Sačekajte učitavanje detalja zastarelosti pre dodavanja predloga prognoze.");
      return;
    }

    const daysSinceMovement = resolveForecastRestockDaysSinceMovement(
      item.skuId,
      detailRow,
      detailData,
      detailLoading,
    );
    const suggestion = buildForecastRestockSuggestion(row, item, stores, daysSinceMovement);
    setWorkflowOverride((current) => {
      const base = current ?? effectiveActionWorkflow ?? { generatedAtUtc: "", pendingCount: 0, approvedCount: 0, deferredCount: 0, closedCount: 0, items: [] };
      if (base.items.some((entry) => entry.suggestionKey === suggestion.suggestionKey)) return base;
      return {
        ...base,
        pendingCount: base.pendingCount + 1,
        items: [suggestion, ...base.items],
      };
    });
    setExportStatus("Signal prognoze je dodat u tok akcija kao predlog dopune.");
    scrollToSection(ACTION_WORKFLOW_SECTION_ID);
  }

  function compareStoresFromRebalance(fromStoreId: number, toStoreId: number) {
    setCompareStoreIds(Array.from(new Set([fromStoreId, toStoreId])));
    scrollToSection(STORE_COMPARISON_SECTION_ID);
  }

  async function addSignalRowToCentralQueue(row: InventoryRow) {
    const actionSpec = buildInventorySignalActionSpec(row);
    setQueueBusyKey(actionSpec.sourceKey);
    try {
      const result = await upsertAnalyticsActionWithResult({
        sourceType: "inventory",
        sourceKey: actionSpec.sourceKey,
        sourceId: row.id,
        title: actionSpec.title,
        description: actionSpec.description,
        recommendationStatus: actionSpec.recommendationStatus,
        priority: actionSpec.priority,
        dueAtUtc: actionSpec.dueAtUtc,
        confidencePct: row.signalConfidencePct ?? undefined,
        dataQualityStatus: toActionDataQualityStatus(row.dataQualityStatus),
        actionUrl: "/analytics/inventory",
        metadataJson: JSON.stringify({
          actionKind: actionSpec.recommendationStatus,
          stockCoverStatus: row.stockCoverStatus,
          sellThroughStatus: row.sellThroughStatus,
          stockCoverDays: row.stockCoverDays,
          sellThroughRatio: row.sellThroughRatio,
          recommendationAllowed: row.recommendationAllowed,
          inventoryExposureRsd: resolveInventoryExposureRsdFromRow(row),
          inventoryExposureBasis: INVENTORY_EXPOSURE_BASIS,
        }),
      });
      setQueuedSuggestionKeys((current) => (
        current.includes(actionSpec.sourceKey) ? current : [...current, actionSpec.sourceKey]
      ));
      setExportStatus(result.existing
        ? "Akcija je već u centralnim akcijama."
        : "Akcija je dodata u centralni red.");
    } catch (reason) {
      setExportStatus(getAnalyticsActionWriteErrorMessage(reason));
    } finally {
      setQueueBusyKey(null);
    }
  }

  function reviewSlowStock(row: InventoryRow) {
    openDetail(row);
    setExportStatus(`Otvoren detalj za sporu zalihu: ${row.naziv}.`);
  }

  function retryPageLoad() {
    setReloadNonce((current) => current + 1);
  }

  if (loading && !pageData && !balance) return <div className="rounded-3xl border border-muted surface-light p-8 text-center text-muted">Učitavanje bilansa zaliha...</div>;
  if (error && (!pageData || !balance)) {
    return (
      <AnalyticsErrorState
        title="Podaci trenutno nisu dostupni"
        message={error.message || "Ne prikazujemo nule jer nije potvrđeno da je period stvarno prazan."}
        errorCode={error.errorCode ?? undefined}
        correlationId={error.correlationId ?? undefined}
        onRetry={() => {
          retryPageLoad();
        }}
        helpHref="/analytics/data-quality"
      />
    );
  }

  if (showEmptyState) {
    return (
      <AnalyticsEmptyState
        variant={showInsufficientEmptyState ? "insufficient_data" : (showFilteredEmptyState ? "filtered_out" : "no_data")}
        message={inventoryMetaMessage ?? (showInsufficientEmptyState
          ? "Nema dovoljno signala za pouzdan prikaz zaliha."
          : "Nema podataka o zalihama za izabrani opseg.")}
        reasons={[
          showInsufficientEmptyState
            ? "Podaci jos nisu dovoljno kompletni za odluku."
            : "Izabrani filteri suzavaju rezultat na prazan skup.",
          "Proverite refresh status i data quality signal.",
          "Proširite opseg ili uklonite deo filtera.",
        ]}
        dataQualityHref="/analytics/data-quality"
        refreshStatusHref="/admin/configuration?panel=workers"
        onRetry={() => {
          retryPageLoad();
        }}
      />
    );
  }

  return (
    <ErrorBoundary fallback={<div className="rounded-3xl border border-[var(--error)] bg-[var(--surface-darker)] p-8 text-center text-[var(--error)]">Bilans stanja trenutno nije mogao da se prikaže. Osveži stranicu ili pokušaj ponovo za nekoliko trenutaka.</div>}>
      <div className="space-y-6">
      <AnalyticsTrustHeader
        title="Analitika zaliha"
        description="Operativni pregled zaliha: dopuna, rizik nestanka, višak, transferi i tok odluka. Status poverenja objedinjuje listu artikala, bilans i uvide."
        periodFrom={null}
        periodTo={null}
        lastRefreshAt={primaryRefreshAt}
        dataSource="Snimak analitike zaliha"
        dataQualityStatus={primaryMeta?.dataQualityStatus ?? null}
        mode="recommendation"
        isPartial={isAnalyticsMetaWarning(primaryMeta)}
        recommendationNote="Tok akcija vode korisnici; preporučeni podaci sa servera ostaju izvor istine."
        emptyStateReason={showEmptyState ? (inventoryMetaMessage ?? null) : null}
        methodologyHref="/analytics/data-quality"
        dataQualityHref="/analytics/data-quality"
        refreshStatusHref="/admin/configuration?panel=workers"
        compact
      />
      {freshnessLineageNote ? (
        <div className="rounded-2xl border border-[var(--warning)] bg-[var(--surface-darker)] px-4 py-3 text-sm text-[var(--warning)]" role="note">
          {freshnessLineageNote}
        </div>
      ) : null}
      {showMetaWarning ? (
        <div className="rounded-2xl border border-[var(--warning)] bg-[var(--surface-darker)] px-4 py-3 text-sm text-[var(--warning)]" role="status">
          Prikazani podaci su delimični ili fallback. {inventoryMetaMessage ?? "Proverite status osvežavanja i data quality signal."}
          {primaryInventoryTrust.degradedSourceLabels.length > 0 ? ` Izvor(i) sa ograničenjem: ${primaryInventoryTrust.degradedSourceLabels.join(", ")}.` : ""}
        </div>
      ) : null}
      {staleWarning && inventorySnapshot ? (
        <div className="rounded-2xl border border-[var(--warning)] bg-[var(--surface-darker)] px-4 py-3 text-sm text-[var(--warning)]" role="status" data-testid="inventory-stale-refetch-warning">
          Prikazujemo prethodno učitane inventory podatke. Novi upit nije uspeo.
        </div>
      ) : null}
      <section className="rounded-[24px] border border-muted surface-light p-4">
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-sm text-secondary">Kako se računaju ključni signali zaliha:</span>
          <KpiExplainButton metricKey="stockAtRisk" ariaLabel="Kako je izračunat lager u riziku" />
          <KpiExplainButton metricKey="slowStockCapital" ariaLabel="Kako je izračunat kapital u sporoj zalihi" />
          <KpiExplainButton metricKey="outOfStockRisk" ariaLabel="Kako je izračunat rizik nestanka zalihe" />
          <KpiExplainButton metricKey="lostSalesEstimate" ariaLabel="Kako je izračunata procena izgubljene prodaje" />
          <KpiExplainButton metricKey="stockCoverDays" ariaLabel="Kako je izračunata pokrivenost zalihe" />
          <KpiExplainButton metricKey="sellThrough" ariaLabel="Kako je izračunat prodajni obrt" />
        </div>
      </section>
      {signalKpis.scope === "page" ? (
        <div className="rounded-2xl border border-[var(--warning)] bg-[var(--surface-darker)] px-4 py-3 text-sm text-[var(--warning)]" role="status" data-testid="inventory-signal-kpi-scope-note">
          {INVENTORY_SIGNAL_KPI_PAGE_SCOPE_NOTE} ({fmtNumber(rows.length, 0, "0")} od {fmtNumber(totalCount, 0, "0")} artikala).
        </div>
      ) : null}
      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <article className="rounded-2xl border border-muted bg-[var(--surface-darker)] p-4">
          <div className="text-xs uppercase tracking-[0.2em] text-muted">Rizik pokrivenosti zalihe</div>
          <div className="mt-2 text-2xl font-semibold text-contrast">{fmtNumber(signalKpis.stockCoverRiskCount, 0, "0")}</div>
          <div className="mt-2 text-sm text-secondary">SKU sa niskom pokrivenošću, OOS rizikom ili nedovoljnim signalom.</div>
        </article>
        <article className="rounded-2xl border border-muted bg-[var(--surface-darker)] p-4">
          <div className="text-xs uppercase tracking-[0.2em] text-muted">Niska pokrivenost artikala</div>
          <div className="mt-2 text-2xl font-semibold text-contrast">{fmtNumber(signalKpis.lowCoverSkus, 0, "0")}</div>
          <div className="mt-2 text-sm text-secondary">Prioritet za dopunu i zaštitu od rasprodaje.</div>
        </article>
        <article className="rounded-2xl border border-muted bg-[var(--surface-darker)] p-4">
          <div className="text-xs uppercase tracking-[0.2em] text-muted">Spor obrt artikala</div>
          <div className="mt-2 text-2xl font-semibold text-contrast">{fmtNumber(signalKpis.slowStockSkus, 0, "0")}</div>
          <div className="mt-2 text-sm text-secondary">Artikli sa sporim obrtom ili bez rotacije.</div>
        </article>
        <article className="rounded-2xl border border-muted bg-[var(--surface-darker)] p-4">
          <div className="text-xs uppercase tracking-[0.2em] text-muted">Dobar prodajni obrt</div>
          <div className="mt-2 text-2xl font-semibold text-contrast">{fmtNumber(signalKpis.goodSellThroughSkus, 0, "0")}</div>
          <div className="mt-2 text-sm text-secondary">SKU sa zdravim tempom izlaza robe.</div>
        </article>
      </section>
      <section className="overflow-hidden rounded-[30px] border border-muted bg-[radial-gradient(circle_at_top_left,var(--theme-color-rgba-68-208-255-0p1, rgba(68,208,255,0.1)),transparent_32%),var(--surface-elevated)] p-6 shadow-xl">
        <div className="flex flex-col gap-6 lg:flex-row lg:items-start lg:justify-between">
          <div className="max-w-[760px]">
            <div className="mb-3 inline-flex items-center gap-2 rounded-full border border-muted bg-[var(--surface-darker)] px-3 py-1 text-xs font-semibold uppercase tracking-[0.24em] text-[var(--info)]"><Warehouse size={14} />Bilans stanja</div>
            <h3 className="text-2xl font-semibold tracking-tight text-contrast md:text-3xl">Operativni pregled zaliha: dopuna, rizik nestanka, višak, transferi i tok odluka.</h3>
            <p className="mt-3 max-w-[640px] text-sm leading-6 text-secondary md:text-base">Pregled vodi od prioriteta i signala ka dubinskoj analizi i operativnom izvozu bez promene poslovne logike.</p>
          </div>
          <div className="grid min-w-[280px] gap-3 sm:grid-cols-2">
            <div className="rounded-2xl border border-muted bg-[var(--surface-darker)] p-4">
              <div className="text-xs uppercase tracking-[0.22em] text-[var(--warning)]">Aktivni SKU</div>
              <div className="mt-2 text-3xl font-semibold text-contrast">{formatPercent(activeSkuShare)}</div>
              <div className="mt-2 text-sm text-secondary">Udeo artikala koji nisu bez zaliha.</div>
              <KpiExplainButton metricKey="activeSkuShare" ariaLabel="Kako je izračunato: Aktivni SKU" />
            </div>
            <div className="rounded-2xl border border-muted bg-[var(--surface-darker)] p-4">
              <div className="text-xs uppercase tracking-[0.22em] text-[var(--text-primary)]">Stanje fonda</div>
              <div data-testid="inventory-health-snapshot-only" className="mt-2 text-lg font-semibold text-contrast">Istorijska serija nije dostupna</div>
              <div className="mt-2 text-sm text-secondary">Trenutni snapshot ne daje backend-obranjeni health score ni istorijski trend. Za ovaj prikaz nisu dostupni period, izvor, svežina i kvalitet istorijskih opažanja.</div>
            </div>
          </div>
        </div>
      </section>

      <section className="rounded-[28px] border border-muted surface-light p-5 shadow-lg">
        <AnalyticsControlBar
          title="Filteri i akcije"
          description="Pretraži bilans, suzi lokaciju i ostavi operativne akcije sekundarnim u odnosu na pregled odluka."
          chips={[
            {
              key: "rows",
              label: "Prikazano",
              value: `${fmtNumber(rows.length, 0, "0")} od ${fmtNumber(totalCount, 0, "0")} artikala`,
              tone: "info",
            },
            {
              key: "page",
              label: "Strana",
              value: `${fmtNumber(pageNumber, 0, "0")} / ${fmtNumber(totalPages, 0, "0")}`,
            },
            ...(riskSortScopeWarning
              ? [{
                  key: "risk-sort",
                  label: "Rizik sort",
                  value: "Lokalno po strani",
                  tone: "warning" as const,
                }]
              : []),
          ]}
          primaryAction={{
            key: "queue",
            label: "Otvori centralni red akcija",
            to: INVENTORY_ACTIONS_QUEUE_URL,
          }}
          secondaryActions={[
            {
              key: "refresh",
              label: "Osveži",
              onClick: retryPageLoad,
              tone: "secondary",
            },
          ]}
          fields={[
            {
              key: "search",
              label: "Pretraga artikala",
              span: "wide",
              control: (
                <input
                  role="searchbox"
                  aria-label="Pretraga artikala"
                  value={searchInput}
                  onChange={(event) => { setSearchInput(event.target.value); setPageNumber(1); }}
                  placeholder="Pretraga po PLU ili nazivu artikla"
                />
              ),
            },
            {
              key: "store",
              label: "Prodavnica",
              control: (
                <select
                  aria-label="Filter po prodavnici"
                  value={selectedStoreId ?? ""}
                  onChange={(event) => { setSelectedStoreId(event.target.value ? Number(event.target.value) : null); setSelectedSupplierId(null); setPageNumber(1); }}
                >
                  <option value="">Sve prodavnice</option>
                  {stores.map((store) => <option key={store.storeId} value={store.storeId}>{store.storeName}</option>)}
                </select>
              ),
            },
            {
              key: "supplier",
              label: "Dobavljač",
              control: (
                <div className="space-y-2">
                  <select
                    aria-label="Filter po dobavljaču"
                    value={selectedSupplierId ?? ""}
                    onChange={(event) => { setSelectedSupplierId(event.target.value ? Number(event.target.value) : null); setPageNumber(1); }}
                    disabled={filtersLoading || supplierFiltersStale}
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
                    <p className="text-[11px] font-semibold tracking-wide text-[var(--warning)]" role="status">
                      {supplierFiltersWarning}
                      {supplierFiltersStale ? ` ${SUPPLIER_FILTER_STALE_LIST_MESSAGE}` : ""}
                    </p>
                  ) : null}
                </div>
              ),
            },
            {
              key: "sort",
              label: "Sortiranje",
              control: (
                <div className="space-y-2">
                  <select
                    aria-label="Sortiranje tabele artikala"
                    value={sortBy}
                    onChange={(event) => { setSortBy(event.target.value); setPageNumber(1); }}
                  >
                    <option value="kolicina">Količina opadajuće</option>
                    <option value="naziv">Naziv A-Z</option>
                    <option value="vrednost">Vrednost opadajuce</option>
                    <option value="azuriranje">Poslednje ažuriranje</option>
                    <option value="oosRisk">OOS rizik opadajuce (samo trenutna strana)</option>
                    <option value="overstockRisk">Overstock rizik opadajuce (samo trenutna strana)</option>
                  </select>
                  {riskSortScopeWarning ? (
                    <p className="text-[11px] font-semibold tracking-wide text-[var(--warning)]" role="status" data-testid="inventory-risk-sort-scope-warning">
                      {riskSortScopeWarning}
                    </p>
                  ) : null}
                </div>
              ),
            },
            {
              key: "page-size",
              label: "Veličina strane",
              control: (
                <select
                  aria-label="Veličina strane tabele artikala"
                  value={pageSize}
                  onChange={(event) => { setPageSize(Number(event.target.value)); setPageNumber(1); }}
                >
                  {PAGE_SIZE_OPTIONS.map((option) => <option key={option} value={option}>{option} redova</option>)}
                </select>
              ),
            },
          ]}
        />

        {exportStatus ? <div className="mt-3 rounded-2xl border border-[var(--info)] bg-[var(--surface-darker)] px-4 py-3 text-sm text-[var(--info)]">{exportStatus}</div> : null}
        {error ? <div className="mt-3 rounded-2xl border border-[var(--error)] bg-[var(--surface-darker)] px-4 py-3 text-sm text-[var(--error)]">{error.message}</div> : null}
      </section>

      <div className="space-y-1">
        <h2 className="text-xl font-semibold text-contrast">1. Odluke sada</h2>
        <p className="text-sm text-muted">Najbitniji prioriteti i koraci odluke koje treba doneti odmah.</p>
      </div>

      <DecisionSummaryBar
        balance={balance}
        actionWorkflow={effectiveActionWorkflow}
        outOfStockCount={balance?.outOfStockCount}
        lowStockCount={balance?.lowStockCount}
        dataQualityWarning={dataQualityNeedsReview}
        dataQualityHref="/analytics/data-quality"
        loading={loading && !balance && !effectiveActionWorkflow}
      />

      {/* Panel za kritične odluke i tok akcija */}
      <ErrorBoundary fallback={<div className="rounded-[28px] border border-error bg-surface-darker p-5 text-sm text-error">Panel toka akcija nije mogao da se prikaže. Osveži stranicu.</div>}>
        <ActionWorkflowPanel
          sectionId={ACTION_WORKFLOW_SECTION_ID}
          actionWorkflow={effectiveActionWorkflow}
          operationsLoading={operationsLoading}
          workflowBusyKey={workflowBusyKey}
          onUpdateWorkflowStatus={(item, status) => void updateWorkflowStatus(item, status)}
        />
      </ErrorBoundary>

      <div className="space-y-1">
        <h2 className="text-xl font-semibold text-contrast">2. Rizici i signali</h2>
        <p className="text-sm text-muted">Signalizacija rizika praznih polica, prekomernih zaliha i transfer potencijala.</p>
      </div>

      {signalSearchLineageNote ? (
        <div className="rounded-2xl border border-border bg-surface-darker px-4 py-3 text-sm text-muted" data-testid="signal-lineage-note">
          {signalSearchLineageNote}
        </div>
      ) : null}

      <div className="grid gap-5 xl:grid-cols-2">
        <ErrorBoundary fallback={<div className="rounded-[28px] border border-error bg-surface-darker p-5 text-sm text-error">Alerts nisu dostupni. Osveži stranicu.</div>}>
          <InventoryAlertsFeed alerts={alerts} alertsLoading={alertsLoading} alertsError={alertsError} alertSeverityFilter={alertSeverityFilter} onSeverityFilterChange={setAlertSeverityFilter} displayCount={ALERTS_DISPLAY_COUNT} onOpenSizeCurve={setSizeCurveSkuId} onOpenDetail={openDetailBySku} />
        </ErrorBoundary>
        <ErrorBoundary fallback={<div className="rounded-[28px] border border-error bg-surface-darker p-5 text-sm text-error">Forecast nije dostupan. Osveži stranicu.</div>}>
          <DemandForecastPanel forecast={forecast} forecastLoading={forecastLoading} forecastError={forecastError} rows={rows} stores={stores} oosThreshold={OOS_RISK_THRESHOLD} overstockThreshold={OVERSTOCK_RISK_THRESHOLD} oosDisplayCount={FORECAST_OOS_DISPLAY} overstockDisplayCount={FORECAST_OVERSTOCK_DISPLAY} onSuggestRestock={queueForecastRestock} />
        </ErrorBoundary>
      </div>

      {/* Predlozi za preraspodelu i transfer */}
      <ErrorBoundary fallback={<div className="rounded-[28px] border border-error bg-surface-darker p-5 text-sm text-error">Predlozi preraspodele nisu dostupni. Osveži stranicu.</div>}>
        <RebalancingTable rebalance={rebalance} rebalanceLoading={rebalanceLoading} rebalanceError={rebalanceError} rows={rows} stores={stores} displayCount={REBALANCE_DISPLAY_COUNT} scopeLabel={rebalanceScopeLabel} onCompareStores={compareStoresFromRebalance} />
      </ErrorBoundary>

      <div className="space-y-1">
        <h2 className="text-xl font-semibold text-contrast">3. Detaljna analiza zaliha</h2>
        <p className="text-sm text-muted">KPI, prioriteti, poredjenje prodavnica i lista artikala za dublji pregled.</p>
      </div>

      <InventoryKPICards totalSku={balance?.totalSku} totalOnHand={balance?.totalOnHand} lowStockCount={balance?.lowStockCount} lowStockShare={lowStockShare} avgUnitsPerSku={avgUnitsPerSku} totalValue={totalValue} />
      <InventoryInsightPanels insights={insights} insightsLoading={insightsLoading} insightsError={insightsError} stores={stores} suppliers={suppliers} rows={rows} onOpenDetail={openDetail} />
      <InventoryPriorityPanels rows={rows} topRiskRows={topRiskRows} highestValueRows={highestValueRows} chartData={chartData} balance={balance} lowStockShare={lowStockShare} totalCount={totalCount} onOpenDetail={openDetail} />

      <div className="grid gap-5 xl:grid-cols-2">
        <StoreComparisonPanel sectionId={STORE_COMPARISON_SECTION_ID} stores={stores} compareStoreIds={compareStoreIds} comparison={storeComparison} operationsLoading={operationsLoading} onToggleStore={toggleCompareStore} />
        <SizeCurvePanel sizeCurveSkuId={sizeCurveSkuId} sizeCurve={sizeCurve} sizeCurveLoading={sizeCurveLoading} sizeCurveError={sizeCurveError} onChangeSkuId={setSizeCurveSkuId} />
      </div>

      {/* Detail Table - scrollable inventory list */}
      <InventoryItemsTable rows={displayedRows} loading={loading} totalCount={totalCount} pageNumber={pageNumber} totalPages={totalPages} onOpenDetail={openDetail} onPreviousPage={() => setPageNumber((current) => Math.max(1, current - 1))} onNextPage={() => setPageNumber((current) => Math.min(totalPages, current + 1))} onAddToActions={(row) => void addSignalRowToCentralQueue(row)} onReviewSlowStock={reviewSlowStock} isRowQueued={(row) => queuedSuggestionKeys.includes(buildInventorySignalActionSpec(row).sourceKey)} isRowQueueBusy={(row) => queueBusyKey === buildInventorySignalActionSpec(row).sourceKey} />

      <div className="space-y-1">
        <h2 className="text-xl font-semibold text-contrast">4. Izvoz i raspored izveštaja</h2>
        <p className="text-sm text-muted">Operativne opcije za stampu, eksport i scheduler su dostupne po potrebi.</p>
      </div>

      <section className="rounded-[28px] border border-muted surface-light p-5 shadow-lg">
        <details>
          <summary className="cursor-pointer text-sm font-semibold text-contrast">Izvoz i scheduler</summary>
          <div className="mt-4">
            <ExportSchedulerPanel
              contractNote={exportContractNote}
              printOrientation={printOrientation}
              onPrintOrientationChange={setPrintOrientation}
              onPrintPreview={() => void runServerExport("pdf", true)}
              onPrintBlank={() => void runBlankPrint()}
              onExportCsv={exportVisibleCsv}
              onExportCsvFiltered={() => void runServerExport("csv")}
              onExportExcel={() => void runServerExport("xlsx")}
              onExportPdf={() => void runServerExport("pdf")}
              onRefresh={retryPageLoad}
              schedules={schedules}
              scheduleDraft={scheduleDraft}
              setScheduleDraft={setScheduleDraft}
              schedulerBusy={schedulerBusy}
              schedulerMessage={schedulerMessage}
              onCopyCurrentFilters={copyCurrentFiltersToSchedule}
              onSaveSchedule={saveSchedule}
              onRunScheduleNow={(id) => void runScheduleNow(id)}
              exportBusy={exportBusy}
              totalCount={totalCount}
              rowsLength={rows.length}
              exportStatus={exportStatus}
            />
          </div>
        </details>
      </section>

      {/* Detail Modal */}
      <SKUDetailModal detailRow={detailRow} detailData={detailData} detailLoading={detailLoading} detailError={detailError} detailTab={detailTab} detailSizeCurve={detailSizeCurve} detailSizeCurveLoading={detailSizeCurveLoading} onRetry={retryDetailFetch} onTabChange={setDetailTab} onClose={() => setDetailRow(null)} />
      </div>
    </ErrorBoundary>
  );
}



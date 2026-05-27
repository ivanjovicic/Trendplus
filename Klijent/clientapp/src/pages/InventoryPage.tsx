import { useDeferredValue, useEffect, useMemo, useRef, useState } from "react";
import { Download, FileSpreadsheet, FileText, Printer, RefreshCw, Search, Warehouse } from "lucide-react";
import { AnalyticsMetaError, createInventoryReportSchedule, exportInventoryReport, getAnalyticsActionSourceStatuses, getForecast, getInventoryActionSuggestions, getInventoryAlerts, getInventoryBalance, getInventoryInsights, getInventoryItemDetail, getInventoryList, getInventoryReportSchedules, getInventoryStoreComparison, getRebalanceSuggestions, getSizeCurve, getStores, getSupplierFilters, previewInventoryReport, printBlankInventoryForm, runInventoryReportScheduleNow, saveInventoryActionDecision, upsertAnalyticsActionWithResult } from "../services/analyticsApi";
import { downloadExport, resolveApiUrl, waitForExport } from "../services/exportApi";
import type { AnalyticsActionDataQualityStatus, AnalyticsResponseMeta, ForecastDto, InventoryActionSuggestion, InventoryActionWorkflow, InventoryAlertListDto, InventoryBalance, InventoryInsights, InventoryItemDetail, InventoryPagedResponse, InventoryReportSchedule, InventoryReportScheduleInput, InventoryStoreComparison, RebalanceListDto, SizeCurveDto, StoreOption, SupplierFilterOption } from "../types/analytics";
import AnalyticsEmptyState from "../components/analytics/AnalyticsEmptyState";
import AnalyticsErrorState from "../components/analytics/AnalyticsErrorState";
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
import { buildInventoryRow, buildSupplierChart, createScheduleDraft, csvEscape, formatPercent } from "../components/inventory/inventoryUtils";
import type { InventoryRow } from "../components/inventory/types";
import { fmtNumber } from "../utils/analyticsFormatters";
import { getAnalyticsMetaMessage, isAnalyticsMetaInsufficient, isAnalyticsMetaWarning, shouldShowAnalyticsEmptyState } from "../utils/analyticsResponseMeta";

const PAGE_SIZE_OPTIONS = [25, 50, 100, 250];
const DEFAULT_COMPARE_STORES = 3;
const TOP_SUPPLIERS_CHART = 6;
const TOP_RISK_ITEMS = 5;
const TOP_VALUE_ITEMS = 5;
const FORECAST_OOS_DISPLAY = 7;
const FORECAST_OVERSTOCK_DISPLAY = 7;
const ALERTS_DISPLAY_COUNT = 12;
const REBALANCE_DISPLAY_COUNT = 20;
const REBALANCE_FETCH_LIMIT = 20;
const FORECAST_FETCH_LIMIT = 50;
const OOS_RISK_THRESHOLD = 0.25;
const OVERSTOCK_RISK_THRESHOLD = 0.5;
const STORE_COMPARISON_SECTION_ID = "inventory-store-comparison";
const ACTION_WORKFLOW_SECTION_ID = "inventory-action-workflow";
const INVENTORY_ACTIONS_QUEUE_URL = "/analytics/actions?sourceType=inventory";

type PreviousLoadState = { pageNumber: number; pageSize: number; selectedStoreId: number | null; selectedSupplierId: number | null; sortBy: string; trimmedSearch: string; compareStoreIdsKey: string };
type InventoryPageError = { message: string; errorCode?: string | null; correlationId?: string | null };

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

function toActionDataQualityStatus(value: string | null | undefined): AnalyticsActionDataQualityStatus {
  const normalized = (value ?? "").trim().toLowerCase();
  if (normalized === "good" || normalized === "warning" || normalized === "critical" || normalized === "insufficient_data") {
    return normalized;
  }

  return "insufficient_data";
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

  if (row.recommendationAllowed === false || normalizedCover === "insufficient_data" || normalizedSellThrough === "insufficient_data") {
    return {
      sourceKey: `inventory:signal_check:${row.id}:${row.idObjekat ?? "all"}`,
      title: `Proveri signal zalihe: ${row.naziv}`,
      recommendationStatus: "SIGNAL_REVIEW",
      priority: "P2",
      description: `Signal nije dovoljan za finalnu akciju. Stock cover: ${row.stockCoverStatusLabel}. Sell-through: ${row.sellThroughStatusLabel}.`,
      dueAtUtc,
      expectedImpactRsd: row.estimatedValueAmount ?? row.estimatedValue ?? null,
    };
  }

  if (normalizedCover === "out_of_stock_risk" || normalizedCover === "low_cover" || normalizedCover === "low") {
    const isCritical = normalizedCover === "out_of_stock_risk";
    return {
      sourceKey: `inventory:replenish:${row.id}:${row.idObjekat ?? "all"}`,
      title: `Dopuni artikal: ${row.naziv}`,
      recommendationStatus: "REPLENISH",
      priority: isCritical ? "P1" : "P2",
      description: `${row.signalText}. Stock cover: ${row.stockCoverStatusLabel}. Sell-through: ${row.sellThroughStatusLabel}.`,
      dueAtUtc,
      expectedImpactRsd: row.estimatedValueAmount ?? row.estimatedValue ?? null,
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
      expectedImpactRsd: row.estimatedValueAmount ?? row.estimatedValue ?? null,
    };
  }

  return {
    sourceKey: `inventory:signal_check:${row.id}:${row.idObjekat ?? "all"}`,
    title: `Proveri signal zalihe: ${row.naziv}`,
    recommendationStatus: "SIGNAL_REVIEW",
    priority: "P2",
    description: `Signal nije dovoljan za finalnu akciju. Stock cover: ${row.stockCoverStatusLabel}. Sell-through: ${row.sellThroughStatusLabel}.`,
    dueAtUtc,
    expectedImpactRsd: row.estimatedValueAmount ?? row.estimatedValue ?? null,
  };
}

export default function InventoryPage() {
  const [balance, setBalance] = useState<InventoryBalance | null>(null);
  const [pageData, setPageData] = useState<InventoryPagedResponse | null>(null);
  const [insights, setInsights] = useState<InventoryInsights | null>(null);
  const [storeComparison, setStoreComparison] = useState<InventoryStoreComparison | null>(null);
  const [actionWorkflow, setActionWorkflow] = useState<InventoryActionWorkflow | null>(null);
  const [schedules, setSchedules] = useState<InventoryReportSchedule[]>([]);
  const [stores, setStores] = useState<StoreOption[]>([]);
  const [suppliers, setSuppliers] = useState<SupplierFilterOption[]>([]);
  const [loading, setLoading] = useState(true);
  const [insightsLoading, setInsightsLoading] = useState(true);
  const [filtersLoading, setFiltersLoading] = useState(true);
  const [operationsLoading, setOperationsLoading] = useState(true);
  const [error, setError] = useState<InventoryPageError | null>(null);
  const [searchInput, setSearchInput] = useState("");
  const [selectedStoreId, setSelectedStoreId] = useState<number | null>(null);
  const [selectedSupplierId, setSelectedSupplierId] = useState<number | null>(null);
  const [compareStoreIds, setCompareStoreIds] = useState<number[]>([]);
  const [sortBy, setSortBy] = useState("kolicina");
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(50);
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
  const [schedulerBusy, setSchedulerBusy] = useState(false);
  const [schedulerMessage, setSchedulerMessage] = useState<string | null>(null);
  const [scheduleDraft, setScheduleDraft] = useState<InventoryReportScheduleInput>(createScheduleDraft);
  const [forecast, setForecast] = useState<ForecastDto | null>(null);
  const [forecastLoading, setForecastLoading] = useState(true);
  const [forecastError, setForecastError] = useState<string | null>(null);
  const [alerts, setAlerts] = useState<InventoryAlertListDto | null>(null);
  const [alertsLoading, setAlertsLoading] = useState(true);
  const [alertSeverityFilter, setAlertSeverityFilter] = useState<"" | "critical" | "warning" | "info">("");
  const [rebalance, setRebalance] = useState<RebalanceListDto | null>(null);
  const [rebalanceLoading, setRebalanceLoading] = useState(true);
  const [sizeCurve, setSizeCurve] = useState<SizeCurveDto | null>(null);
  const [sizeCurveLoading, setSizeCurveLoading] = useState(false);
  const [sizeCurveSkuId, setSizeCurveSkuId] = useState<number | null>(null);
  const [reloadNonce, setReloadNonce] = useState(0);
  const deferredSearch = useDeferredValue(searchInput);
  const trimmedSearch = deferredSearch.trim();
  const serverSortBy = sortBy === "oosRisk" || sortBy === "overstockRisk" ? "kolicina" : sortBy;
  const previousLoadRef = useRef<PreviousLoadState | null>(null);

  useEffect(() => {
    let cancelled = false;
    void getStores(true)
      .then((nextStores) => {
        if (cancelled) return;
        setStores(nextStores);
        setCompareStoreIds((current) => current.length > 0 ? current : nextStores.slice(0, DEFAULT_COMPARE_STORES).map((store) => store.storeId));
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
        if (!cancelled) setSchedulerMessage(reason instanceof Error ? reason.message : String(reason));
      });
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    let cancelled = false;
    void getSupplierFilters(undefined, undefined, true, selectedStoreId ?? undefined)
      .then((nextSuppliers) => {
        if (cancelled) return;
        setSuppliers(nextSuppliers);
        if (selectedSupplierId != null && !nextSuppliers.some((entry) => entry.supplierId === selectedSupplierId)) {
          setSelectedSupplierId(null);
        }
      })
      .catch(() => {
        if (!cancelled) setSuppliers([]);
      });
    return () => { cancelled = true; };
  }, [selectedStoreId, selectedSupplierId]);

  useEffect(() => {
    const currentLoad = {
      pageNumber,
      pageSize,
      selectedStoreId,
      selectedSupplierId,
      sortBy,
      trimmedSearch,
      compareStoreIdsKey: compareStoreIds.join(","),
    };
    const previousLoad = previousLoadRef.current;
    const isFirstLoad = previousLoad == null;
    const shouldRefreshSignals = isFirstLoad || previousLoad.selectedStoreId !== selectedStoreId || previousLoad.selectedSupplierId !== selectedSupplierId;
    const shouldRefreshOperations = isFirstLoad
      || previousLoad.selectedStoreId !== selectedStoreId
      || previousLoad.selectedSupplierId !== selectedSupplierId
      || previousLoad.trimmedSearch !== trimmedSearch
      || previousLoad.compareStoreIdsKey !== currentLoad.compareStoreIdsKey;

    previousLoadRef.current = currentLoad;

    let cancelled = false;
    setLoading(true);
    setInsightsLoading(true);
    setError(null);
    if (shouldRefreshOperations) setOperationsLoading(true);
    if (shouldRefreshSignals) {
      setForecastLoading(true);
      setAlertsLoading(true);
      setRebalanceLoading(true);
      setForecastError(null);
    }

    const setFirstError = (reason: unknown, fallback: string) => {
      setError((current) => current ?? toInventoryPageError(reason, fallback));
    };

    const primaryTasks = [
      { key: "balance" as const, promise: getInventoryBalance(true, selectedStoreId, selectedSupplierId) },
      { key: "list" as const, promise: getInventoryList({ pageNumber, pageSize, search: trimmedSearch || undefined, storeId: selectedStoreId, supplierId: selectedSupplierId, sortBy: serverSortBy }) },
    ];

    void Promise.allSettled(primaryTasks.map((task) => task.promise))
      .then((results) => {
        if (cancelled) return;
        results.forEach((result, index) => {
          const task = primaryTasks[index];
          if (result.status === "rejected") {
            setFirstError(result.reason, "Bilans zaliha trenutno nije dostupan.");
            return;
          }
          switch (task.key) {
            case "balance": setBalance(result.value as InventoryBalance); break;
            case "list": setPageData(result.value as InventoryPagedResponse); break;
          }
        });
      })
      .finally(() => {
        if (cancelled) return;
        setLoading(false);
      });

    void getInventoryInsights({ search: trimmedSearch || undefined, storeId: selectedStoreId, supplierId: selectedSupplierId, sortBy: serverSortBy })
      .then((result) => {
        if (!cancelled) setInsights(result);
      })
      .catch((reason) => {
        if (!cancelled) {
          setFirstError(reason, "Inventory uvidi trenutno nisu dostupni.");
        }
      })
      .finally(() => {
        if (cancelled) return;
        setInsightsLoading(false);
      });

    if (shouldRefreshOperations) {
      const operationTasks = [
        { key: "storeComparison" as const, promise: getInventoryStoreComparison({ compareStoreIds, supplierId: selectedSupplierId, search: trimmedSearch || undefined }) },
        { key: "actionWorkflow" as const, promise: getInventoryActionSuggestions({ storeId: selectedStoreId, supplierId: selectedSupplierId, search: trimmedSearch || undefined }) },
      ];

      void Promise.allSettled(operationTasks.map((task) => task.promise))
        .then((results) => {
          if (cancelled) return;
          results.forEach((result, index) => {
            const task = operationTasks[index];
            if (result.status === "rejected") {
              setFirstError(result.reason, "Operativni inventory paneli trenutno nisu dostupni.");
              return;
            }

            switch (task.key) {
              case "storeComparison":
                setStoreComparison(result.value as InventoryStoreComparison);
                break;
              case "actionWorkflow":
                setActionWorkflow(result.value as InventoryActionWorkflow);
                break;
            }
          });
        })
        .finally(() => {
          if (cancelled) return;
          setOperationsLoading(false);
        });
    }

    if (shouldRefreshSignals) {
      const signalTasks = [
        { key: "forecast" as const, promise: getForecast({ storeId: selectedStoreId, supplierId: selectedSupplierId, top: FORECAST_FETCH_LIMIT }) },
        { key: "alerts" as const, promise: getInventoryAlerts({ storeId: selectedStoreId, supplierId: selectedSupplierId }) },
        { key: "rebalance" as const, promise: getRebalanceSuggestions({ supplierId: selectedSupplierId, top: REBALANCE_FETCH_LIMIT }) },
      ];

      void Promise.allSettled(signalTasks.map((task) => task.promise))
        .then((results) => {
          if (cancelled) return;
          results.forEach((result, index) => {
            const task = signalTasks[index];
            if (result.status === "rejected") {
              if (task.key === "forecast") {
                const nextError = toInventoryPageError(result.reason, "Forecast podaci trenutno nisu dostupni.");
                setForecastError(nextError.message);
              } else {
                setFirstError(result.reason, "Signalni inventory paneli trenutno nisu dostupni.");
              }
              return;
            }

            switch (task.key) {
              case "forecast":
                setForecast(result.value as ForecastDto);
                break;
              case "alerts":
                setAlerts(result.value as InventoryAlertListDto);
                break;
              case "rebalance":
                setRebalance(result.value as RebalanceListDto);
                break;
            }
          });
        })
        .finally(() => {
          if (cancelled) return;
          setForecastLoading(false);
          setAlertsLoading(false);
          setRebalanceLoading(false);
        });
    }

    return () => { cancelled = true; };
  }, [compareStoreIds, pageNumber, pageSize, reloadNonce, selectedStoreId, selectedSupplierId, sortBy, trimmedSearch]);

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
    setDetailLoading(true);
    setDetailError(null);
    void getInventoryItemDetail(detailRow.id)
      .then((nextDetail) => {
        if (!cancelled) setDetailData(nextDetail);
      })
      .catch((reason) => {
        if (!cancelled) {
          setDetailData(null);
          setDetailError(reason instanceof Error ? reason.message : String(reason));
        }
      })
      .finally(() => {
        if (!cancelled) setDetailLoading(false);
      });
    return () => { cancelled = true; };
  }, [detailRow]);

  useEffect(() => {
    if (!detailRow || detailTab !== "sizeCurve") {
      setDetailSizeCurve(null);
      setDetailSizeCurveLoading(false);
      return;
    }
    let cancelled = false;
    setDetailSizeCurveLoading(true);
    void getSizeCurve({ skuId: detailRow.id, storeId: detailRow.idObjekat ?? selectedStoreId ?? undefined })
      .then((nextCurve) => {
        if (!cancelled) setDetailSizeCurve(nextCurve);
      })
      .catch(console.error)
      .finally(() => {
        if (!cancelled) setDetailSizeCurveLoading(false);
      });
    return () => { cancelled = true; };
  }, [detailRow, detailTab, selectedStoreId]);

  useEffect(() => {
    if (sizeCurveSkuId == null) {
      setSizeCurve(null);
      return;
    }
    let cancelled = false;
    setSizeCurveLoading(true);
    void getSizeCurve({ skuId: sizeCurveSkuId, storeId: selectedStoreId })
      .then((data) => {
        if (!cancelled) setSizeCurve(data);
      })
      .catch(console.error)
      .finally(() => {
        if (!cancelled) setSizeCurveLoading(false);
      });
    return () => { cancelled = true; };
  }, [sizeCurveSkuId, selectedStoreId]);

  const rows = useMemo(() => (pageData?.items ?? []).map((item) => buildInventoryRow(item, stores, suppliers)), [pageData, stores, suppliers]);
  const totalCount = pageData?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const totalValue = balance?.estimatedInventoryValue ?? rows.reduce((sum, row) => sum + row.estimatedValueAmount, 0);
  const activeSkuShare = useMemo(() => (balance && balance.totalSku > 0 ? ((balance.totalSku - balance.outOfStockCount) / balance.totalSku) * 100 : 0), [balance]);
  const lowStockShare = useMemo(() => (balance && balance.totalSku > 0 ? (balance.lowStockCount / balance.totalSku) * 100 : 0), [balance]);
  const avgUnitsPerSku = useMemo(() => (balance && balance.totalSku > 0 ? balance.totalOnHand / balance.totalSku : 0), [balance]);
  const inventoryHealthScore = useMemo(() => Math.max(0, Math.round(100 - (balance && balance.totalSku > 0 ? (balance.outOfStockCount / balance.totalSku) * 60 : 0) - (balance && balance.totalSku > 0 ? (balance.lowStockCount / balance.totalSku) * 25 : 0))), [balance]);
  const healthTrendPoints = useMemo(() => Array.from({ length: 7 }, (_, index) => {
    const slope = index - 6;
    const lowStockDrift = lowStockShare * 0.06 * slope;
    const activeSkuDrift = activeSkuShare * 0.018 * (6 - index);
    return Math.max(42, Math.min(99, +(inventoryHealthScore + activeSkuDrift - lowStockDrift).toFixed(1)));
  }), [activeSkuShare, inventoryHealthScore, lowStockShare]);
  const healthSparklinePath = useMemo(() => {
    const width = 60;
    const height = 24;
    const min = Math.min(...healthTrendPoints);
    const max = Math.max(...healthTrendPoints);
    return healthTrendPoints.map((point, index) => {
      const x = (index / Math.max(healthTrendPoints.length - 1, 1)) * width;
      const y = max === min ? height / 2 : height - ((point - min) / (max - min)) * height;
      return `${index === 0 ? "M" : "L"} ${x.toFixed(2)} ${y.toFixed(2)}`;
    }).join(" ");
  }, [healthTrendPoints]);
  const chartData = useMemo(() => buildSupplierChart(rows).sort((left, right) => right.totalValue - left.totalValue).slice(0, TOP_SUPPLIERS_CHART), [rows]);
  const topRiskRows = useMemo(() => rows.slice().sort((left, right) => (left.stockState === right.stockState ? right.reorderGap - left.reorderGap : { critical: 0, warning: 1, healthy: 2 }[left.stockState] - { critical: 0, warning: 1, healthy: 2 }[right.stockState])).slice(0, TOP_RISK_ITEMS), [rows]);
  const highestValueRows = useMemo(() => rows.slice().sort((left, right) => right.estimatedValueAmount - left.estimatedValueAmount).slice(0, TOP_VALUE_ITEMS), [rows]);
  const inventoryLastRefreshAt = useMemo(() => {
    const timestamps = [
      actionWorkflow?.generatedAtUtc,
      forecast?.generatedAtUtc,
      alerts?.generatedAtUtc,
      rebalance?.generatedAtUtc,
      storeComparison?.generatedAtUtc,
    ].filter((value): value is string => Boolean(value));

    if (timestamps.length === 0) return null;
    return timestamps
      .map((value) => ({ value, time: new Date(value).getTime() }))
      .filter((entry) => !Number.isNaN(entry.time))
      .sort((a, b) => b.time - a.time)[0]?.value ?? null;
  }, [actionWorkflow?.generatedAtUtc, alerts?.generatedAtUtc, forecast?.generatedAtUtc, rebalance?.generatedAtUtc, storeComparison?.generatedAtUtc]);
  const forecastMetricsByRowKey = useMemo(() => new Map(rows.map((row) => {
    const matching = (forecast?.items ?? []).filter((item) => item.skuId === row.id && (row.idObjekat == null || item.storeId === row.idObjekat));
    return [`${row.id}:${row.idObjekat ?? 0}`, {
      oosRisk: matching.reduce((max, item) => Math.max(max, item.probabilityOfOOSIn7d), 0),
      overstockRisk: matching.reduce((max, item) => Math.max(max, item.overstockRisk), 0),
    }];
  })), [forecast, rows]);
  const displayedRows = useMemo(() => {
    if (sortBy !== "oosRisk" && sortBy !== "overstockRisk") return rows;
    return rows.slice().sort((left, right) => {
      const leftMetrics = forecastMetricsByRowKey.get(`${left.id}:${left.idObjekat ?? 0}`);
      const rightMetrics = forecastMetricsByRowKey.get(`${right.id}:${right.idObjekat ?? 0}`);
      return sortBy === "oosRisk"
        ? (rightMetrics?.oosRisk ?? 0) - (leftMetrics?.oosRisk ?? 0)
        : (rightMetrics?.overstockRisk ?? 0) - (leftMetrics?.overstockRisk ?? 0);
    });
  }, [forecastMetricsByRowKey, rows, sortBy]);

  useEffect(() => {
    let cancelled = false;

    const signalKeys = displayedRows.map((row) => buildInventorySignalActionSpec(row).sourceKey);
    const workflowKeys = (actionWorkflow?.items ?? [])
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
          setQueuedSuggestionKeys([]);
          console.warn("Neuspešna provera statusa inventory akcija po sourceKey.", reason);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [actionWorkflow, displayedRows]);

  const signalKpis = useMemo(() => {
    const lowCoverSkus = rows.filter((row) => {
      const status = (row.stockCoverStatus ?? "").toLowerCase();
      return status === "low_cover" || status === "low" || status === "out_of_stock_risk";
    }).length;

    const slowStockSkus = rows.filter((row) => {
      const status = (row.stockCoverStatus ?? "").toLowerCase();
      return status === "slow_stock" || status === "slow" || status === "no_velocity";
    }).length;

    const goodSellThroughSkus = rows.filter((row) => (row.sellThroughStatus ?? "").toLowerCase() === "good").length;
    const stockCoverRiskCount = rows.filter((row) => {
      const status = (row.stockCoverStatus ?? "").toLowerCase();
      return status === "low_cover" || status === "low" || status === "out_of_stock_risk" || status === "insufficient_data";
    }).length;

    return {
      stockCoverRiskCount,
      lowCoverSkus,
      slowStockSkus,
      goodSellThroughSkus,
    };
  }, [rows]);
  const inventoryMetas = useMemo(
    () => ([pageData?.meta, balance?.meta, insights?.meta, storeComparison?.meta, actionWorkflow?.meta].filter((meta): meta is AnalyticsResponseMeta => Boolean(meta))),
    [actionWorkflow?.meta, balance?.meta, insights?.meta, pageData?.meta, storeComparison?.meta]
  );
  const primaryMeta = inventoryMetas[0] ?? null;
  const warningMeta = inventoryMetas.find((meta) => isAnalyticsMetaWarning(meta)) ?? null;
  const inventoryMetaMessage = getAnalyticsMetaMessage(warningMeta ?? primaryMeta);
  const showMetaWarning = !loading && !error && warningMeta != null;
  const showInsufficientEmptyState = !loading
    && !error
    && shouldShowAnalyticsEmptyState(primaryMeta, totalCount)
    && isAnalyticsMetaInsufficient(primaryMeta);
  const hasActivePrimaryFilters = Boolean(trimmedSearch) || selectedStoreId != null || selectedSupplierId != null;
  const emptyReasonCode = primaryMeta?.emptyReason?.trim().toLowerCase() ?? "";
  const showFilteredEmptyState = !showInsufficientEmptyState
    && (hasActivePrimaryFilters || emptyReasonCode.includes("filter"));
  const showEmptyState = !loading && !error && pageData != null && (showInsufficientEmptyState || totalCount === 0);

  const refreshSchedules = async () => setSchedules(await getInventoryReportSchedules());
  const refreshOperations = async () => {
    const [nextComparison, nextWorkflow] = await Promise.all([
      getInventoryStoreComparison({ compareStoreIds, supplierId: selectedSupplierId, search: trimmedSearch || undefined }),
      getInventoryActionSuggestions({ storeId: selectedStoreId, supplierId: selectedSupplierId, search: trimmedSearch || undefined }),
    ]);
    setStoreComparison(nextComparison);
    setActionWorkflow(nextWorkflow);
  };

  async function runServerExport(format: "pdf" | "xlsx" | "csv", preview = false) {
    if (totalCount === 0 || exportBusy) return;
    try {
      setExportBusy(true);
      setExportStatus(preview ? "Pripremam print preview na serveru..." : "Server priprema dokument za izvoz...");
      if (preview) {
        const previewResult = await previewInventoryReport({ orientation: printOrientation, includeFiltersAndMetadata: true, search: trimmedSearch || undefined, storeId: selectedStoreId, supplierId: selectedSupplierId, sortBy: serverSortBy });
        if (previewResult.printUrl) window.open(resolveApiUrl(previewResult.printUrl), "_blank", "noopener");
        setExportStatus("Print preview je otvoren u novom tabu.");
        return;
      }
      const result = await exportInventoryReport({ format, orientation: printOrientation, includeFiltersAndMetadata: true, forceAsync: totalCount > 5000, search: trimmedSearch || undefined, storeId: selectedStoreId, supplierId: selectedSupplierId, sortBy: serverSortBy });
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
      setExportStatus(reason instanceof Error ? reason.message : "Eksport nije uspeo.");
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
      setExportStatus(reason instanceof Error ? reason.message : "Priprema praznog obrasca nije uspela.");
    } finally {
      setExportBusy(false);
    }
  }

  function exportVisibleCsv() {
    const lines = [
      ["PLU", "Naziv", "Dobavljač", "Prodavnica", "Status", "Kolicina", "Minimum", "Gap", "NabavnaCena", "Vrednost"].join(";"),
      ...rows.map((row) => [csvEscape(row.plu ?? ""), csvEscape(row.naziv), csvEscape(row.supplierName), csvEscape(row.storeName), csvEscape(row.stockStateLabel), row.quantity, row.minimum, row.reorderGap, row.unitCost.toFixed(2), row.estimatedValueAmount.toFixed(2)].join(";")),
    ];
    const blob = new Blob([`\uFEFF${lines.join("\n")}`], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `bilans-stanja-strana-${pageNumber}.csv`;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
    setExportStatus("CSV za trenutnu stranu je preuzet.");
  }

  async function updateWorkflowStatus(item: InventoryActionSuggestion, status: "approved" | "deferred" | "closed") {
    try {
      setWorkflowBusyKey(item.suggestionKey);
      await saveInventoryActionDecision(item.suggestionKey, { actionType: item.actionType, status, note: item.note ?? "" });
      await refreshOperations();
    } catch (reason) {
      setExportStatus(reason instanceof Error ? reason.message : "Cuvanje odluke nije uspelo.");
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
        impactEstimateRsd: item.estimatedValue,
        actionUrl: "/analytics/inventory",
        metadataJson: JSON.stringify({
          suggestionKey: item.suggestionKey,
          actionType: item.actionType,
          suggestedQty: item.suggestedQty,
          fromStoreName: item.fromStoreName,
          toStoreName: item.toStoreName,
        }),
      });
      setQueuedSuggestionKeys((current) => (
        current.includes(item.suggestionKey) ? current : [...current, item.suggestionKey]
      ));
      setExportStatus(result.existing
        ? "Akcija je već u centralnim akcijama."
        : "Akcija je dodata u centralni red.");
    } catch (reason) {
      setExportStatus(reason instanceof Error ? reason.message : "Dodavanje u centralne akcije nije uspelo.");
    } finally {
      setQueueBusyKey(null);
    }
  }

  async function saveSchedule() {
    try {
      setSchedulerBusy(true);
      setSchedulerMessage("Cuvam raspored i pripremam scheduler...");
      await createInventoryReportSchedule(scheduleDraft);
      await refreshSchedules();
      setScheduleDraft(createScheduleDraft());
      setSchedulerMessage("Raspored je sacuvan.");
    } catch (reason) {
      setSchedulerMessage(reason instanceof Error ? reason.message : "Cuvanje rasporeda nije uspelo.");
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
      setSchedulerMessage(reason instanceof Error ? reason.message : "Rucno pokretanje nije uspelo.");
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
    openDetail(buildInventoryRow({
      id: skuId,
      naziv: label ?? `SKU #${skuId}`,
      plu: null,
      kolicina: 0,
      minimalnaKolicina: 0,
      nabavnaCena: 0,
      estimatedValue: 0,
      idObjekat: storeId ?? null,
      idDobavljac: null,
    }, stores, suppliers));
  }

  function retryDetailFetch() {
    if (!detailRow) return;
    const currentRow = detailRow;
    setDetailRow(null);
    window.setTimeout(() => setDetailRow(currentRow), 0);
  }

  function queueForecastRestock(item: ForecastDto["items"][number]) {
    const row = rows.find((entry) => entry.id === item.skuId && (entry.idObjekat == null || entry.idObjekat === item.storeId)) ?? buildInventoryRow({
      id: item.skuId,
      naziv: `SKU #${item.skuId}`,
      plu: null,
      kolicina: 0,
      minimalnaKolicina: Math.ceil(item.forecast7d),
      nabavnaCena: 0,
      estimatedValue: 0,
      idObjekat: item.storeId,
      idDobavljac: null,
      velicina: item.sizeCode,
    }, stores, suppliers);
    const suggestionKey = `forecast-${item.skuId}-${item.storeId}-${item.sizeCode}`;
    setActionWorkflow((current) => {
      const base = current ?? { generatedAtUtc: "", pendingCount: 0, approvedCount: 0, deferredCount: 0, closedCount: 0, items: [] };
      if (base.items.some((entry) => entry.suggestionKey === suggestionKey)) return base;
      return {
        ...base,
        pendingCount: base.pendingCount + 1,
        items: [{
          suggestionKey,
          actionType: "dopuna",
          priority: item.probabilityOfOOSIn7d > 0.7 ? "critical" : "high",
          label: `Predlozena dopuna za ${row.naziv}`,
          reason: `Forecast 7d je ${item.forecast7d.toFixed(1)} kom, a OOS rizik ${Math.round(item.probabilityOfOOSIn7d * 100)}%.`,
          status: "pending",
          artikalId: item.skuId,
          plu: row.plu,
          naziv: row.naziv,
          fromStoreName: null,
          toStoreName: stores.find((store) => store.storeId === item.storeId)?.storeName ?? row.storeName,
          suggestedQty: Math.max(1, Math.ceil(item.forecast7d)),
          estimatedValue: row.unitCost * Math.max(1, Math.ceil(item.forecast7d)),
          daysSinceMovement: detailData?.daysSinceMovement ?? 0,
          note: `Automatski dodat iz forecast sekcije za velicinu ${item.sizeCode}.`,
          updatedAtUtc: new Date().toISOString(),
        }, ...base.items],
      };
    });
    setExportStatus("Forecast signal je dodat u workflow kao predlog dopune.");
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
        expectedImpactRsd: actionSpec.expectedImpactRsd ?? undefined,
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
        }),
      });
      setQueuedSuggestionKeys((current) => (
        current.includes(actionSpec.sourceKey) ? current : [...current, actionSpec.sourceKey]
      ));
      setExportStatus(result.existing
        ? "Akcija je već u centralnim akcijama."
        : "Akcija je dodata u centralni red.");
    } catch (reason) {
      setExportStatus(reason instanceof Error ? reason.message : "Dodavanje signalne akcije nije uspelo.");
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

  if (loading && !pageData && !balance) return <div className="rounded-3xl border border-muted surface-light p-8 text-center text-muted">Učitavanje bilansa stanja...</div>;
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
          ? "Nema dovoljno signala za pouzdan inventory prikaz."
          : "Nema inventory podataka za izabrani opseg.")}
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
        title="Inventory analytics"
        description="Decision cockpit za zalihe: dopuna, OOS rizik, višak zalihe, transferi i workflow odluka."
        periodFrom={null}
        periodTo={null}
        lastRefreshAt={primaryMeta?.lastRefreshAtUtc ?? primaryMeta?.generatedAtUtc ?? inventoryLastRefreshAt}
        dataSource="Inventory analytics snapshot"
        dataQualityStatus={primaryMeta?.dataQualityStatus ?? null}
        mode="recommendation"
        isPartial={isAnalyticsMetaWarning(primaryMeta)}
        recommendationNote="Workflow akcije su korisnički vođene; backend recommendation payload ostaje izvor istine."
        emptyStateReason={showEmptyState ? (inventoryMetaMessage ?? null) : null}
        methodologyHref="/analytics/data-quality"
        dataQualityHref="/analytics/data-quality"
        refreshStatusHref="/admin/configuration?panel=workers"
        compact
      />
      {showMetaWarning ? (
        <div className="rounded-2xl border border-[var(--warning)] bg-[var(--surface-darker)] px-4 py-3 text-sm text-[var(--warning)]" role="status">
          Prikazani podaci su delimični ili fallback. {inventoryMetaMessage ?? "Proverite status osvežavanja i data quality signal."}
        </div>
      ) : null}
      <section className="rounded-[24px] border border-muted surface-light p-4">
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-sm text-secondary">Kako se računaju ključni inventory signali:</span>
          <KpiExplainButton metricKey="stockAtRisk" ariaLabel="Kako je izračunat lager u riziku" />
          <KpiExplainButton metricKey="slowStockCapital" ariaLabel="Kako je izračunat kapital u sporoj zalihi" />
          <KpiExplainButton metricKey="outOfStockRisk" ariaLabel="Kako je izračunat rizik nestanka zalihe" />
          <KpiExplainButton metricKey="lostSalesEstimate" ariaLabel="Kako je izračunata procena izgubljene prodaje" />
          <KpiExplainButton metricKey="stockCoverDays" ariaLabel="Kako je izračunata pokrivenost zalihe" />
          <KpiExplainButton metricKey="sellThrough" ariaLabel="Kako je izračunat sell-through" />
        </div>
      </section>
      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <article className="rounded-2xl border border-muted bg-[var(--surface-darker)] p-4">
          <div className="text-xs uppercase tracking-[0.2em] text-muted">Stock cover risk</div>
          <div className="mt-2 text-2xl font-semibold text-contrast">{fmtNumber(signalKpis.stockCoverRiskCount, 0, "0")}</div>
          <div className="mt-2 text-sm text-secondary">SKU sa niskom pokrivenošću, OOS rizikom ili nedovoljnim signalom.</div>
        </article>
        <article className="rounded-2xl border border-muted bg-[var(--surface-darker)] p-4">
          <div className="text-xs uppercase tracking-[0.2em] text-muted">Low cover SKU</div>
          <div className="mt-2 text-2xl font-semibold text-contrast">{fmtNumber(signalKpis.lowCoverSkus, 0, "0")}</div>
          <div className="mt-2 text-sm text-secondary">Prioritet za dopunu i zaštitu od rasprodaje.</div>
        </article>
        <article className="rounded-2xl border border-muted bg-[var(--surface-darker)] p-4">
          <div className="text-xs uppercase tracking-[0.2em] text-muted">Slow stock SKU</div>
          <div className="mt-2 text-2xl font-semibold text-contrast">{fmtNumber(signalKpis.slowStockSkus, 0, "0")}</div>
          <div className="mt-2 text-sm text-secondary">Artikli sa sporim obrtom ili bez rotacije.</div>
        </article>
        <article className="rounded-2xl border border-muted bg-[var(--surface-darker)] p-4">
          <div className="text-xs uppercase tracking-[0.2em] text-muted">Good sell-through SKU</div>
          <div className="mt-2 text-2xl font-semibold text-contrast">{fmtNumber(signalKpis.goodSellThroughSkus, 0, "0")}</div>
          <div className="mt-2 text-sm text-secondary">SKU sa zdravim tempom izlaza robe.</div>
        </article>
      </section>
      <section className="overflow-hidden rounded-[30px] border border-muted bg-[radial-gradient(circle_at_top_left,var(--theme-color-rgba-68-208-255-0p1, rgba(68,208,255,0.1)),transparent_32%),var(--surface-elevated)] p-6 shadow-xl">
        <div className="flex flex-col gap-6 lg:flex-row lg:items-start lg:justify-between">
          <div className="max-w-[760px]">
            <div className="mb-3 inline-flex items-center gap-2 rounded-full border border-muted bg-[var(--surface-darker)] px-3 py-1 text-xs font-semibold uppercase tracking-[0.24em] text-[var(--info)]"><Warehouse size={14} />Bilans stanja</div>
            <h3 className="text-2xl font-semibold tracking-tight text-contrast md:text-3xl">Decision cockpit za zalihe: dopuna, OOS rizik, višak zalihe, transferi i workflow odluka.</h3>
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
              <div className="flex items-center justify-between gap-3">
                <div className="text-xs uppercase tracking-[0.22em] text-[var(--text-primary)]">Stanje fonda</div>
                <svg width="60" height="24" viewBox="0 0 60 24" aria-hidden="true" className="shrink-0">
                  <path d={healthSparklinePath} fill="none" stroke="var(--focus-ring)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              </div>
              <div className="mt-2 text-2xl font-semibold text-contrast">{inventoryHealthScore}<span className="text-sm font-normal text-secondary">/100</span></div>
              <div className="mt-2 text-sm text-secondary">{inventoryHealthScore >= 85 ? "Stabilan fond robe." : inventoryHealthScore >= 65 ? "Potrebno praćenje kritičnih SKU." : "Povećan rizik od praznih polica."}</div>
              <KpiExplainButton metricKey="inventoryHealthScore" ariaLabel="Kako je izračunato: Stanje fonda" />
            </div>
          </div>
        </div>
      </section>

      <section className="rounded-[28px] border border-muted surface-light p-5 shadow-lg">
        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-3 xl:flex-row xl:items-end xl:justify-between">
            <div>
              <h2 className="text-lg font-semibold text-contrast">Filteri i akcije</h2>
              <p className="text-sm text-muted">Pretraži bilans, suzi lokaciju i odmah pokreni report ili štampu.</p>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <span className="inline-flex rounded-xl border border-muted overflow-hidden text-xs font-semibold" role="group" aria-label="Orijentacija štampe">
                <button
                  type="button"
                  aria-pressed={printOrientation === "landscape"}
                  onClick={() => setPrintOrientation("landscape")}
                  className={`px-3 py-2 transition-colors duration-150 ${printOrientation === "landscape" ? "bg-[var(--info)] text-white" : "surface-elevated text-contrast hover:bg-[var(--surface-darker)]"}`}
                  title="Horizontalno (A4 landscape)"
                >↔ Hor.</button>
                <button
                  type="button"
                  aria-pressed={printOrientation === "portrait"}
                  onClick={() => setPrintOrientation("portrait")}
                  className={`px-3 py-2 border-l border-muted transition-colors duration-150 ${printOrientation === "portrait" ? "bg-[var(--info)] text-white" : "surface-elevated text-contrast hover:bg-[var(--surface-darker)]"}`}
                  title="Vertikalno (A4 portrait)"
                >↕ Ver.</button>
              </span>
              <button type="button" aria-label="Otvori print preview filtriranog izveštaja" onClick={() => void runServerExport("pdf", true)} disabled={exportBusy || totalCount === 0} className="inline-flex items-center gap-2 rounded-xl border border-muted surface-elevated px-3 py-2 text-xs font-semibold text-contrast transition-all duration-200 hover:border-[var(--info)] hover:shadow-md disabled:cursor-not-allowed disabled:opacity-60"><Printer size={14} />Print preview</button>
              <button type="button" aria-label="Odštampaj prazan obrazac bilansa stanja" onClick={() => void runBlankPrint()} disabled={exportBusy} className="inline-flex items-center gap-2 rounded-xl border border-muted surface-elevated px-3 py-2 text-xs font-semibold text-contrast transition-all duration-200 hover:border-[var(--warning)] hover:shadow-md disabled:cursor-not-allowed disabled:opacity-60"><Printer size={14} />Prazan obrazac</button>
              <button type="button" aria-label="Izvezi CSV za trenutni ekran" onClick={exportVisibleCsv} disabled={rows.length === 0} className="inline-flex items-center gap-2 rounded-xl border border-muted bg-[var(--surface-darker)] px-3 py-2 text-xs font-semibold text-[var(--info)] transition-all duration-200 hover:border-[var(--info)] hover:shadow-md disabled:cursor-not-allowed disabled:opacity-60"><Download size={14} />CSV ekran</button>
              <button type="button" aria-label="Izvezi CSV filtrirano" onClick={() => void runServerExport("csv")} disabled={exportBusy || totalCount === 0} className="inline-flex items-center gap-2 rounded-xl border border-muted bg-[var(--surface-darker)] px-3 py-2 text-xs font-semibold text-[var(--info)] transition-all duration-200 hover:border-[var(--info)] hover:shadow-md disabled:cursor-not-allowed disabled:opacity-60"><Download size={14} />CSV filtrirano</button>
              <button type="button" aria-label="Izvezi Excel filtrirano" onClick={() => void runServerExport("xlsx")} disabled={exportBusy || totalCount === 0} className="inline-flex items-center gap-2 rounded-xl border border-muted bg-[var(--surface-darker)] px-3 py-2 text-xs font-semibold text-[var(--success)] transition-all duration-200 hover:border-[var(--success)] hover:shadow-md disabled:cursor-not-allowed disabled:opacity-60"><FileSpreadsheet size={14} />Excel filtrirano</button>
              <button type="button" aria-label="Izvezi PDF filtrirano" onClick={() => void runServerExport("pdf")} disabled={exportBusy || totalCount === 0} className="inline-flex items-center gap-2 rounded-xl border border-muted bg-[var(--surface-darker)] px-3 py-2 text-xs font-semibold text-[var(--error)] transition-all duration-200 hover:border-[var(--error)] hover:shadow-md disabled:cursor-not-allowed disabled:opacity-60"><FileText size={14} />PDF filtrirano</button>
              <a href={INVENTORY_ACTIONS_QUEUE_URL} className="inline-flex items-center gap-2 rounded-xl border border-muted surface-elevated px-3 py-2 text-xs font-semibold text-[var(--info)] transition-all duration-200 hover:border-[var(--info)] hover:shadow-md">Otvori centralni red akcija</a>
              <button type="button" aria-label="Osveži stranicu bilansa stanja" onClick={() => window.location.reload()} className="inline-flex items-center gap-2 rounded-xl border border-muted surface-elevated px-3 py-2 text-xs font-semibold text-contrast transition-all duration-200 hover:border-secondary hover:shadow-md"><RefreshCw size={14} />Osveži</button>
            </div>
          </div>

          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-[minmax(0,1.5fr)_repeat(4,minmax(0,1fr))]">
            <label className="flex items-center gap-3 rounded-2xl border border-muted bg-[var(--surface-darker)] px-4 py-3 transition-all duration-200 hover:border-secondary focus-within:border-[var(--focus-ring)] focus-within:ring-2 focus-within:ring-[var(--focus-ring)] focus-within:ring-opacity-30">
              <Search size={16} className="text-[var(--info)]" />
              <input role="searchbox" aria-label="Pretraga artikala" value={searchInput} onChange={(event) => { setSearchInput(event.target.value); setPageNumber(1); }} placeholder="Pretraga po PLU ili nazivu artikla" className="w-full bg-transparent text-sm text-contrast outline-none placeholder:text-muted focus:outline-none" />
            </label>
            <label className="rounded-2xl border border-muted bg-[var(--surface-darker)] px-4 py-3 text-sm text-contrast transition-all duration-200 hover:border-secondary focus-within:border-[var(--focus-ring)] focus-within:ring-2 focus-within:ring-[var(--focus-ring)] focus-within:ring-opacity-30">
              <span className="mb-1 block text-[11px] uppercase tracking-[0.2em] text-muted">Prodavnica</span>
              <select aria-label="Filter po prodavnici" value={selectedStoreId ?? ""} onChange={(event) => { setSelectedStoreId(event.target.value ? Number(event.target.value) : null); setSelectedSupplierId(null); setPageNumber(1); }} className="w-full bg-transparent outline-none focus:outline-none cursor-pointer">
                <option value="">Sve prodavnice</option>
                {stores.map((store) => <option key={store.storeId} value={store.storeId}>{store.storeName}</option>)}
              </select>
            </label>
            <label className={`rounded-2xl border border-muted bg-[var(--surface-darker)] px-4 py-3 text-sm text-contrast transition-all duration-200 ${filtersLoading ? 'opacity-60 cursor-not-allowed' : 'hover:border-secondary focus-within:border-[var(--focus-ring)] focus-within:ring-2 focus-within:ring-[var(--focus-ring)] focus-within:ring-opacity-30'}`}>
              <span className="mb-1 block text-[11px] uppercase tracking-[0.2em] text-muted">Dobavljač</span>
              <select aria-label="Filter po dobavljaču" value={selectedSupplierId ?? ""} onChange={(event) => { setSelectedSupplierId(event.target.value ? Number(event.target.value) : null); setPageNumber(1); }} className="w-full bg-transparent outline-none focus:outline-none cursor-pointer disabled:cursor-not-allowed disabled:opacity-50" disabled={filtersLoading}>
                <option value="">Svi dobavljači</option>
                {suppliers.map((supplier) => <option key={supplier.supplierId} value={supplier.supplierId}>{supplier.supplierName}</option>)}
              </select>
            </label>
            <label className="rounded-2xl border border-muted bg-[var(--surface-darker)] px-4 py-3 text-sm text-contrast transition-all duration-200 hover:border-secondary focus-within:border-[var(--focus-ring)] focus-within:ring-2 focus-within:ring-[var(--focus-ring)] focus-within:ring-opacity-30">
              <span className="mb-1 block text-[11px] uppercase tracking-[0.2em] text-muted">Sortiranje</span>
              <select aria-label="Sortiranje tabele artikala" value={sortBy} onChange={(event) => { setSortBy(event.target.value); setPageNumber(1); }} className="w-full bg-transparent outline-none focus:outline-none cursor-pointer">
                <option value="kolicina">Količina opadajuće</option>
                <option value="naziv">Naziv A-Z</option>
                <option value="vrednost">Vrednost opadajuce</option>
                <option value="azuriranje">Poslednje ažuriranje</option>
                <option value="oosRisk">OOS rizik opadajuce</option>
                <option value="overstockRisk">Overstock rizik opadajuce</option>
              </select>
            </label>
            <label className="rounded-2xl border border-muted bg-[var(--surface-darker)] px-4 py-3 text-sm text-contrast transition-all duration-200 hover:border-secondary focus-within:border-[var(--focus-ring)] focus-within:ring-2 focus-within:ring-[var(--focus-ring)] focus-within:ring-opacity-30">
              <span className="mb-1 block text-[11px] uppercase tracking-[0.2em] text-muted">Veličina strane</span>
              <select aria-label="Veličina strane tabele artikala" value={pageSize} onChange={(event) => { setPageSize(Number(event.target.value)); setPageNumber(1); }} className="w-full bg-transparent outline-none focus:outline-none cursor-pointer">
                {PAGE_SIZE_OPTIONS.map((option) => <option key={option} value={option}>{option} redova</option>)}
              </select>
            </label>
          </div>

          {exportStatus ? <div className="rounded-2xl border border-[var(--info)] bg-[var(--surface-darker)] px-4 py-3 text-sm text-[var(--info)]">{exportStatus}</div> : null}
          {error ? <div className="rounded-2xl border border-[var(--error)] bg-[var(--surface-darker)] px-4 py-3 text-sm text-[var(--error)]">{error.message}</div> : null}
        </div>
      </section>

      <div className="space-y-1">
        <h2 className="text-xl font-semibold text-contrast">1. Odluke sada</h2>
        <p className="text-sm text-muted">Najbitniji prioriteti i workflow koraci koje treba doneti odmah.</p>
      </div>

      <DecisionSummaryBar
        balance={balance}
        actionWorkflow={actionWorkflow}
        outOfStockCount={balance?.outOfStockCount}
        lowStockCount={balance?.lowStockCount}
        dataQualityWarning={false}
        loading={loading && !balance && !actionWorkflow}
      />

      {/* Decision-Critical Workflow Panel */}
      <ErrorBoundary fallback={<div className="rounded-[28px] border border-error bg-surface-darker p-5 text-sm text-error">Workflow panel nije mogao da se prikaže. Osveži stranicu.</div>}>
        <ActionWorkflowPanel
          sectionId={ACTION_WORKFLOW_SECTION_ID}
          actionWorkflow={actionWorkflow}
          operationsLoading={operationsLoading}
          workflowBusyKey={workflowBusyKey}
          onUpdateWorkflowStatus={(item, status) => void updateWorkflowStatus(item, status)}
        />
      </ErrorBoundary>

      <div className="space-y-1">
        <h2 className="text-xl font-semibold text-contrast">2. Rizici i signali</h2>
        <p className="text-sm text-muted">Signalizacija rizika praznih polica, prekomernih zaliha i transfer potencijala.</p>
      </div>

      <div className="grid gap-5 xl:grid-cols-2">
        <ErrorBoundary fallback={<div className="rounded-[28px] border border-error bg-surface-darker p-5 text-sm text-error">Alerts nisu dostupni. Osveži stranicu.</div>}>
          <InventoryAlertsFeed alerts={alerts} alertsLoading={alertsLoading} alertSeverityFilter={alertSeverityFilter} onSeverityFilterChange={setAlertSeverityFilter} displayCount={ALERTS_DISPLAY_COUNT} onOpenSizeCurve={setSizeCurveSkuId} onOpenDetail={openDetailBySku} />
        </ErrorBoundary>
        <ErrorBoundary fallback={<div className="rounded-[28px] border border-error bg-surface-darker p-5 text-sm text-error">Forecast nije dostupan. Osveži stranicu.</div>}>
          <DemandForecastPanel forecast={forecast} forecastLoading={forecastLoading} forecastError={forecastError} rows={rows} stores={stores} oosThreshold={OOS_RISK_THRESHOLD} overstockThreshold={OVERSTOCK_RISK_THRESHOLD} oosDisplayCount={FORECAST_OOS_DISPLAY} overstockDisplayCount={FORECAST_OVERSTOCK_DISPLAY} onSuggestRestock={queueForecastRestock} />
        </ErrorBoundary>
      </div>

      {/* Rebalancing & Transfer Suggestions */}
      <ErrorBoundary fallback={<div className="rounded-[28px] border border-error bg-surface-darker p-5 text-sm text-error">Rebalancing sugestije nisu dostupne. Osveži stranicu.</div>}>
        <RebalancingTable rebalance={rebalance} rebalanceLoading={rebalanceLoading} rows={rows} stores={stores} displayCount={REBALANCE_DISPLAY_COUNT} onCompareStores={compareStoresFromRebalance} />
      </ErrorBoundary>

      <div className="space-y-1">
        <h2 className="text-xl font-semibold text-contrast">3. Detaljna analiza zaliha</h2>
        <p className="text-sm text-muted">KPI, prioriteti, poredjenje prodavnica i lista artikala za dublji pregled.</p>
      </div>

      <InventoryKPICards totalSku={balance?.totalSku} totalOnHand={balance?.totalOnHand} lowStockCount={balance?.lowStockCount} lowStockShare={lowStockShare} avgUnitsPerSku={avgUnitsPerSku} totalValue={totalValue} />
      <InventoryInsightPanels insights={insights} insightsLoading={insightsLoading} stores={stores} suppliers={suppliers} rows={rows} onOpenDetail={openDetail} />
      <InventoryPriorityPanels rows={rows} topRiskRows={topRiskRows} highestValueRows={highestValueRows} chartData={chartData} balance={balance} lowStockShare={lowStockShare} totalCount={totalCount} onOpenDetail={openDetail} />

      <div className="grid gap-5 xl:grid-cols-2">
        <StoreComparisonPanel sectionId={STORE_COMPARISON_SECTION_ID} stores={stores} compareStoreIds={compareStoreIds} comparison={storeComparison} operationsLoading={operationsLoading} onToggleStore={toggleCompareStore} />
        <SizeCurvePanel sizeCurveSkuId={sizeCurveSkuId} sizeCurve={sizeCurve} sizeCurveLoading={sizeCurveLoading} onChangeSkuId={setSizeCurveSkuId} />
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
              printOrientation={printOrientation}
              onPrintOrientationChange={setPrintOrientation}
              onPrintPreview={() => void runServerExport("pdf", true)}
              onPrintBlank={() => void runBlankPrint()}
              onExportCsv={exportVisibleCsv}
              onExportCsvFiltered={() => void runServerExport("csv")}
              onExportExcel={() => void runServerExport("xlsx")}
              onExportPdf={() => void runServerExport("pdf")}
              onRefresh={() => window.location.reload()}
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



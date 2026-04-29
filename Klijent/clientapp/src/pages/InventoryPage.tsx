import { useDeferredValue, useEffect, useMemo, useRef, useState } from "react";
import { Download, FileSpreadsheet, FileText, Printer, RefreshCw, Search, Warehouse } from "lucide-react";
import { createInventoryReportSchedule, exportInventoryReport, getForecast, getInventoryActionSuggestions, getInventoryAlerts, getInventoryBalance, getInventoryInsights, getInventoryItemDetail, getInventoryList, getInventoryReportSchedules, getInventoryStoreComparison, getRebalanceSuggestions, getSizeCurve, getStores, getSupplierFilters, previewInventoryReport, printBlankInventoryForm, runInventoryReportScheduleNow, saveInventoryActionDecision } from "../services/analyticsApi";
import { downloadExport, resolveApiUrl, waitForExport } from "../services/exportApi";
import type { ForecastDto, InventoryActionSuggestion, InventoryActionWorkflow, InventoryAlertListDto, InventoryBalance, InventoryInsights, InventoryItemDetail, InventoryPagedResponse, InventoryReportSchedule, InventoryReportScheduleInput, InventoryStoreComparison, RebalanceListDto, SizeCurveDto, StoreOption, SupplierFilterOption } from "../types/analytics";
import { ActionWorkflowPanel } from "../components/inventory/ActionWorkflowPanel";
import { DemandForecastPanel } from "../components/inventory/DemandForecastPanel";
import { ErrorBoundary } from "../components/ErrorBoundary";
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
import { buildInventoryRow, buildSupplierChart, createScheduleDraft, csvEscape, formatPercent } from "../components/inventory/inventoryUtils";
import type { InventoryRow } from "../components/inventory/types";

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

type PreviousLoadState = { pageNumber: number; pageSize: number; selectedStoreId: number | null; selectedSupplierId: number | null; sortBy: string; trimmedSearch: string; compareStoreIdsKey: string };

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
  const [error, setError] = useState<string | null>(null);
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

    const setFirstError = (message: string) => {
      setError((current) => current ?? message);
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
            const message = result.reason instanceof Error ? result.reason.message : String(result.reason);
            setFirstError(message);
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
          const message = reason instanceof Error ? reason.message : String(reason);
          setFirstError(message);
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
              const message = result.reason instanceof Error ? result.reason.message : String(result.reason);
              setFirstError(message);
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
              const message = result.reason instanceof Error ? result.reason.message : String(result.reason);
              if (task.key === "forecast") {
                setForecastError(message);
              } else {
                setFirstError(message);
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
  }, [compareStoreIds, pageNumber, pageSize, selectedStoreId, selectedSupplierId, sortBy, trimmedSearch]);

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
      ["PLU", "Naziv", "Dobavljac", "Prodavnica", "Status", "Kolicina", "Minimum", "Gap", "NabavnaCena", "Vrednost"].join(";"),
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
      const base = current ?? { generatedAtUtc: new Date().toISOString(), pendingCount: 0, approvedCount: 0, deferredCount: 0, closedCount: 0, items: [] };
      if (base.items.some((entry) => entry.suggestionKey === suggestionKey)) return base;
      return {
        ...base,
        generatedAtUtc: new Date().toISOString(),
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

  if (loading && !pageData && !balance) return <div className="rounded-3xl border border-muted surface-light p-8 text-center text-muted">Ucitavanje bilansa stanja...</div>;
  if (error && !pageData) return <div className="rounded-3xl border border-[var(--error)] bg-[var(--surface-darker)] p-8 text-center text-[var(--error)]">Greska: {error}</div>;

  return (
    <ErrorBoundary fallback={<div className="rounded-3xl border border-[var(--error)] bg-[var(--surface-darker)] p-8 text-center text-[var(--error)]">Bilans stanja trenutno nije mogao da se prikaze. Osvezi stranicu ili pokusaj ponovo za nekoliko trenutaka.</div>}>
      <div className="space-y-6">
      <section className="overflow-hidden rounded-[30px] border border-muted bg-[radial-gradient(circle_at_top_left,var(--theme-color-rgba-68-208-255-0p1, rgba(68,208,255,0.1)),transparent_32%),var(--surface-elevated)] p-6 shadow-xl">
        <div className="flex flex-col gap-6 lg:flex-row lg:items-start lg:justify-between">
          <div className="max-w-[760px]">
            <div className="mb-3 inline-flex items-center gap-2 rounded-full border border-muted bg-[var(--surface-darker)] px-3 py-1 text-xs font-semibold uppercase tracking-[0.24em] text-[var(--info)]"><Warehouse size={14} />Bilans stanja</div>
            <h3 className="text-2xl font-semibold tracking-tight text-contrast md:text-3xl">Operativni pregled zaliha sa stampom i report izvozom.</h3>
            <p className="mt-3 max-w-[640px] text-sm leading-6 text-secondary md:text-base">Stranica sada spaja KPI pregled, filtriranje po prodavnici i dobavljacu, tabelarni rad, detalje artikla i server-side dokumente za deljenje sa timom.</p>
          </div>
          <div className="grid min-w-[280px] gap-3 sm:grid-cols-2">
            <div className="rounded-2xl border border-muted bg-[var(--surface-darker)] p-4">
              <div className="text-xs uppercase tracking-[0.22em] text-[var(--warning)]">Aktivni SKU</div>
              <div className="mt-2 text-3xl font-semibold text-contrast">{formatPercent(activeSkuShare)}</div>
              <div className="mt-2 text-sm text-secondary">Udeo artikala koji nisu bez zaliha.</div>
            </div>
            <div className="rounded-2xl border border-muted bg-[var(--surface-darker)] p-4">
              <div className="flex items-center justify-between gap-3">
                <div className="text-xs uppercase tracking-[0.22em] text-[var(--text-primary)]">Stanje fonda</div>
                <svg width="60" height="24" viewBox="0 0 60 24" aria-hidden="true" className="shrink-0">
                  <path d={healthSparklinePath} fill="none" stroke="var(--focus-ring)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              </div>
              <div className="mt-2 text-2xl font-semibold text-contrast">{inventoryHealthScore}<span className="text-sm font-normal text-secondary">/100</span></div>
              <div className="mt-2 text-sm text-secondary">{inventoryHealthScore >= 85 ? "Stabilan fond robe." : inventoryHealthScore >= 65 ? "Potrebno pracenje kriticnih SKU." : "Povecan rizik od praznih polica."}</div>
            </div>
          </div>
        </div>
      </section>

      <section className="rounded-[28px] border border-muted surface-light p-5 shadow-lg">
        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-3 xl:flex-row xl:items-end xl:justify-between">
            <div>
              <h2 className="text-lg font-semibold text-contrast">Filteri i akcije</h2>
              <p className="text-sm text-muted">Pretrazi bilans, suzi lokaciju i odmah pokreni report ili stampu.</p>
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
              <button type="button" aria-label="Otvori print preview filtriranog izvestaja" onClick={() => void runServerExport("pdf", true)} disabled={exportBusy || totalCount === 0} className="inline-flex items-center gap-2 rounded-xl border border-muted surface-elevated px-3 py-2 text-xs font-semibold text-contrast transition-all duration-200 hover:border-[var(--info)] hover:shadow-md disabled:cursor-not-allowed disabled:opacity-60"><Printer size={14} />Print preview</button>
              <button type="button" aria-label="Odštampaj prazan obrazac bilansa stanja" onClick={() => void runBlankPrint()} disabled={exportBusy} className="inline-flex items-center gap-2 rounded-xl border border-muted surface-elevated px-3 py-2 text-xs font-semibold text-contrast transition-all duration-200 hover:border-[var(--warning)] hover:shadow-md disabled:cursor-not-allowed disabled:opacity-60"><Printer size={14} />Prazan obrazac</button>
              <button type="button" aria-label="Izvezi CSV za trenutni ekran" onClick={exportVisibleCsv} disabled={rows.length === 0} className="inline-flex items-center gap-2 rounded-xl border border-muted bg-[var(--surface-darker)] px-3 py-2 text-xs font-semibold text-[var(--info)] transition-all duration-200 hover:border-[var(--info)] hover:shadow-md disabled:cursor-not-allowed disabled:opacity-60"><Download size={14} />CSV ekran</button>
              <button type="button" aria-label="Izvezi CSV filtrirano" onClick={() => void runServerExport("csv")} disabled={exportBusy || totalCount === 0} className="inline-flex items-center gap-2 rounded-xl border border-muted bg-[var(--surface-darker)] px-3 py-2 text-xs font-semibold text-[var(--info)] transition-all duration-200 hover:border-[var(--info)] hover:shadow-md disabled:cursor-not-allowed disabled:opacity-60"><Download size={14} />CSV filtrirano</button>
              <button type="button" aria-label="Izvezi Excel filtrirano" onClick={() => void runServerExport("xlsx")} disabled={exportBusy || totalCount === 0} className="inline-flex items-center gap-2 rounded-xl border border-muted bg-[var(--surface-darker)] px-3 py-2 text-xs font-semibold text-[var(--success)] transition-all duration-200 hover:border-[var(--success)] hover:shadow-md disabled:cursor-not-allowed disabled:opacity-60"><FileSpreadsheet size={14} />Excel filtrirano</button>
              <button type="button" aria-label="Izvezi PDF filtrirano" onClick={() => void runServerExport("pdf")} disabled={exportBusy || totalCount === 0} className="inline-flex items-center gap-2 rounded-xl border border-muted bg-[var(--surface-darker)] px-3 py-2 text-xs font-semibold text-[var(--error)] transition-all duration-200 hover:border-[var(--error)] hover:shadow-md disabled:cursor-not-allowed disabled:opacity-60"><FileText size={14} />PDF filtrirano</button>
              <button type="button" aria-label="Osvezi stranicu bilansa stanja" onClick={() => window.location.reload()} className="inline-flex items-center gap-2 rounded-xl border border-muted surface-elevated px-3 py-2 text-xs font-semibold text-contrast transition-all duration-200 hover:border-secondary hover:shadow-md"><RefreshCw size={14} />Osvezi</button>
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
              <span className="mb-1 block text-[11px] uppercase tracking-[0.2em] text-muted">Dobavljac</span>
              <select aria-label="Filter po dobavljacu" value={selectedSupplierId ?? ""} onChange={(event) => { setSelectedSupplierId(event.target.value ? Number(event.target.value) : null); setPageNumber(1); }} className="w-full bg-transparent outline-none focus:outline-none cursor-pointer disabled:cursor-not-allowed disabled:opacity-50" disabled={filtersLoading}>
                <option value="">Svi dobavljaci</option>
                {suppliers.map((supplier) => <option key={supplier.supplierId} value={supplier.supplierId}>{supplier.supplierName}</option>)}
              </select>
            </label>
            <label className="rounded-2xl border border-muted bg-[var(--surface-darker)] px-4 py-3 text-sm text-contrast transition-all duration-200 hover:border-secondary focus-within:border-[var(--focus-ring)] focus-within:ring-2 focus-within:ring-[var(--focus-ring)] focus-within:ring-opacity-30">
              <span className="mb-1 block text-[11px] uppercase tracking-[0.2em] text-muted">Sortiranje</span>
              <select aria-label="Sortiranje tabele artikala" value={sortBy} onChange={(event) => { setSortBy(event.target.value); setPageNumber(1); }} className="w-full bg-transparent outline-none focus:outline-none cursor-pointer">
                <option value="kolicina">Kolicina opadajuce</option>
                <option value="naziv">Naziv A-Z</option>
                <option value="vrednost">Vrednost opadajuce</option>
                <option value="azuriranje">Poslednje azuriranje</option>
                <option value="oosRisk">OOS rizik opadajuce</option>
                <option value="overstockRisk">Overstock rizik opadajuce</option>
              </select>
            </label>
            <label className="rounded-2xl border border-muted bg-[var(--surface-darker)] px-4 py-3 text-sm text-contrast transition-all duration-200 hover:border-secondary focus-within:border-[var(--focus-ring)] focus-within:ring-2 focus-within:ring-[var(--focus-ring)] focus-within:ring-opacity-30">
              <span className="mb-1 block text-[11px] uppercase tracking-[0.2em] text-muted">Velicina strane</span>
              <select aria-label="Velicina strane tabele artikala" value={pageSize} onChange={(event) => { setPageSize(Number(event.target.value)); setPageNumber(1); }} className="w-full bg-transparent outline-none focus:outline-none cursor-pointer">
                {PAGE_SIZE_OPTIONS.map((option) => <option key={option} value={option}>{option} redova</option>)}
              </select>
            </label>
          </div>

          {exportStatus ? <div className="rounded-2xl border border-[var(--info)] bg-[var(--surface-darker)] px-4 py-3 text-sm text-[var(--info)]">{exportStatus}</div> : null}
          {error ? <div className="rounded-2xl border border-[var(--error)] bg-[var(--surface-darker)] px-4 py-3 text-sm text-[var(--error)]">{error}</div> : null}
        </div>
      </section>

      <InventoryKPICards totalSku={balance?.totalSku} totalOnHand={balance?.totalOnHand} lowStockCount={balance?.lowStockCount} lowStockShare={lowStockShare} avgUnitsPerSku={avgUnitsPerSku} totalValue={totalValue} />
      <MailSchedulerPanel scheduleDraft={scheduleDraft} setScheduleDraft={setScheduleDraft} schedules={schedules} schedulerBusy={schedulerBusy} schedulerMessage={schedulerMessage} onCopyCurrentFilters={copyCurrentFiltersToSchedule} onSaveSchedule={saveSchedule} onRunScheduleNow={(id) => void runScheduleNow(id)} />

      <div className="grid gap-5 xl:grid-cols-2">
        <StoreComparisonPanel sectionId={STORE_COMPARISON_SECTION_ID} stores={stores} compareStoreIds={compareStoreIds} comparison={storeComparison} operationsLoading={operationsLoading} onToggleStore={toggleCompareStore} />
        <ActionWorkflowPanel sectionId={ACTION_WORKFLOW_SECTION_ID} actionWorkflow={actionWorkflow} operationsLoading={operationsLoading} workflowBusyKey={workflowBusyKey} onUpdateWorkflowStatus={(item, status) => void updateWorkflowStatus(item, status)} />
      </div>

      <InventoryInsightPanels insights={insights} insightsLoading={insightsLoading} stores={stores} suppliers={suppliers} rows={rows} onOpenDetail={openDetail} />
      <InventoryPriorityPanels rows={rows} topRiskRows={topRiskRows} highestValueRows={highestValueRows} chartData={chartData} balance={balance} lowStockShare={lowStockShare} totalCount={totalCount} onOpenDetail={openDetail} />
      <InventoryAlertsFeed alerts={alerts} alertsLoading={alertsLoading} alertSeverityFilter={alertSeverityFilter} onSeverityFilterChange={setAlertSeverityFilter} displayCount={ALERTS_DISPLAY_COUNT} onOpenSizeCurve={setSizeCurveSkuId} onOpenDetail={openDetailBySku} />
      <DemandForecastPanel forecast={forecast} forecastLoading={forecastLoading} forecastError={forecastError} rows={rows} stores={stores} oosThreshold={OOS_RISK_THRESHOLD} overstockThreshold={OVERSTOCK_RISK_THRESHOLD} oosDisplayCount={FORECAST_OOS_DISPLAY} overstockDisplayCount={FORECAST_OVERSTOCK_DISPLAY} onSuggestRestock={queueForecastRestock} />
      <SizeCurvePanel sizeCurveSkuId={sizeCurveSkuId} sizeCurve={sizeCurve} sizeCurveLoading={sizeCurveLoading} onChangeSkuId={setSizeCurveSkuId} />
      <RebalancingTable rebalance={rebalance} rebalanceLoading={rebalanceLoading} rows={rows} stores={stores} displayCount={REBALANCE_DISPLAY_COUNT} onCompareStores={compareStoresFromRebalance} />
      <InventoryItemsTable rows={displayedRows} loading={loading} totalCount={totalCount} pageNumber={pageNumber} totalPages={totalPages} onOpenDetail={openDetail} onPreviousPage={() => setPageNumber((current) => Math.max(1, current - 1))} onNextPage={() => setPageNumber((current) => Math.min(totalPages, current + 1))} />
      <SKUDetailModal detailRow={detailRow} detailData={detailData} detailLoading={detailLoading} detailError={detailError} detailTab={detailTab} detailSizeCurve={detailSizeCurve} detailSizeCurveLoading={detailSizeCurveLoading} onRetry={retryDetailFetch} onTabChange={setDetailTab} onClose={() => setDetailRow(null)} />
      </div>
    </ErrorBoundary>
  );
}

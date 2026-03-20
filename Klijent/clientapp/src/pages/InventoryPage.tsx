import { useDeferredValue, useEffect, useMemo, useRef, useState } from "react";
import { Download, FileSpreadsheet, FileText, Printer, RefreshCw, Search, Warehouse } from "lucide-react";
import { createInventoryReportSchedule, exportInventoryReport, getForecast, getInventoryActionSuggestions, getInventoryAlerts, getInventoryBalance, getInventoryInsights, getInventoryItemDetail, getInventoryList, getInventoryReportSchedules, getInventoryStoreComparison, getRebalanceSuggestions, getSizeCurve, getStores, getSupplierFilters, previewInventoryReport, runInventoryReportScheduleNow, saveInventoryActionDecision } from "../services/analyticsApi";
import { downloadExport, resolveApiUrl, waitForExport } from "../services/exportApi";
import type { ForecastDto, InventoryActionSuggestion, InventoryActionWorkflow, InventoryAlertListDto, InventoryBalance, InventoryInsights, InventoryItemDetail, InventoryPagedResponse, InventoryReportSchedule, InventoryReportScheduleInput, InventoryStoreComparison, RebalanceListDto, SizeCurveDto, StoreOption, SupplierFilterOption } from "../types/analytics";
import { ActionWorkflowPanel } from "../components/inventory/ActionWorkflowPanel";
import { DemandForecastPanel } from "../components/inventory/DemandForecastPanel";
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
  const [detailData, setDetailData] = useState<InventoryItemDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [exportBusy, setExportBusy] = useState(false);
  const [exportStatus, setExportStatus] = useState<string | null>(null);
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
    const errorMessages: string[] = [];
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

    const tasks = [
      { key: "balance" as const, promise: getInventoryBalance(true, selectedStoreId, selectedSupplierId) },
      { key: "list" as const, promise: getInventoryList({ pageNumber, pageSize, search: trimmedSearch || undefined, storeId: selectedStoreId, supplierId: selectedSupplierId, sortBy }) },
      { key: "insights" as const, promise: getInventoryInsights({ search: trimmedSearch || undefined, storeId: selectedStoreId, supplierId: selectedSupplierId, sortBy }) },
      ...(shouldRefreshOperations ? [
        { key: "storeComparison" as const, promise: getInventoryStoreComparison({ compareStoreIds, supplierId: selectedSupplierId, search: trimmedSearch || undefined }) },
        { key: "actionWorkflow" as const, promise: getInventoryActionSuggestions({ storeId: selectedStoreId, supplierId: selectedSupplierId, search: trimmedSearch || undefined }) },
      ] : []),
      ...(shouldRefreshSignals ? [
        { key: "forecast" as const, promise: getForecast({ storeId: selectedStoreId, supplierId: selectedSupplierId, top: FORECAST_FETCH_LIMIT }) },
        { key: "alerts" as const, promise: getInventoryAlerts({ storeId: selectedStoreId, supplierId: selectedSupplierId }) },
        { key: "rebalance" as const, promise: getRebalanceSuggestions({ supplierId: selectedSupplierId, top: REBALANCE_FETCH_LIMIT }) },
      ] : []),
    ];

    void Promise.allSettled(tasks.map((task) => task.promise))
      .then((results) => {
        if (cancelled) return;
        results.forEach((result, index) => {
          const task = tasks[index];
          if (result.status === "rejected") {
            const message = result.reason instanceof Error ? result.reason.message : String(result.reason);
            if (task.key === "forecast") setForecastError(message);
            else errorMessages.push(message);
            return;
          }
          switch (task.key) {
            case "balance": setBalance(result.value as InventoryBalance); break;
            case "list": setPageData(result.value as InventoryPagedResponse); break;
            case "insights": setInsights(result.value as InventoryInsights); break;
            case "storeComparison": setStoreComparison(result.value as InventoryStoreComparison); break;
            case "actionWorkflow": setActionWorkflow(result.value as InventoryActionWorkflow); break;
            case "forecast": setForecast(result.value as ForecastDto); break;
            case "alerts": setAlerts(result.value as InventoryAlertListDto); break;
            case "rebalance": setRebalance(result.value as RebalanceListDto); break;
          }
        });
        if (errorMessages.length > 0) setError(errorMessages[0]);
      })
      .finally(() => {
        if (cancelled) return;
        setLoading(false);
        setInsightsLoading(false);
        if (shouldRefreshOperations) setOperationsLoading(false);
        if (shouldRefreshSignals) {
          setForecastLoading(false);
          setAlertsLoading(false);
          setRebalanceLoading(false);
        }
      });

    return () => { cancelled = true; };
  }, [compareStoreIds, pageNumber, pageSize, selectedStoreId, selectedSupplierId, sortBy, trimmedSearch]);

  useEffect(() => {
    if (!detailRow) {
      setDetailData(null);
      setDetailError(null);
      setDetailLoading(false);
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
  const chartData = useMemo(() => buildSupplierChart(rows).sort((left, right) => right.totalValue - left.totalValue).slice(0, TOP_SUPPLIERS_CHART), [rows]);
  const topRiskRows = useMemo(() => rows.slice().sort((left, right) => (left.stockState === right.stockState ? right.reorderGap - left.reorderGap : { critical: 0, warning: 1, healthy: 2 }[left.stockState] - { critical: 0, warning: 1, healthy: 2 }[right.stockState])).slice(0, TOP_RISK_ITEMS), [rows]);
  const highestValueRows = useMemo(() => rows.slice().sort((left, right) => right.estimatedValueAmount - left.estimatedValueAmount).slice(0, TOP_VALUE_ITEMS), [rows]);

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
        const previewResult = await previewInventoryReport({ orientation: "landscape", includeFiltersAndMetadata: true, search: trimmedSearch || undefined, storeId: selectedStoreId, supplierId: selectedSupplierId, sortBy });
        if (previewResult.printUrl) window.open(resolveApiUrl(previewResult.printUrl), "_blank", "noopener");
        setExportStatus("Print preview je otvoren u novom tabu.");
        return;
      }
      const result = await exportInventoryReport({ format, orientation: "landscape", includeFiltersAndMetadata: true, forceAsync: totalCount > 5000, search: trimmedSearch || undefined, storeId: selectedStoreId, supplierId: selectedSupplierId, sortBy });
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

  function exportVisibleCsv() {
    if (rows.length === 0) return;
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
    setScheduleDraft((current) => ({ ...current, search: trimmedSearch, storeId: selectedStoreId, supplierId: selectedSupplierId, sortBy }));
    setSchedulerMessage("Trenutni filteri su prepisani u scheduler formu.");
  }

  if (loading && !pageData && !balance) return <div className="rounded-3xl border border-[#202430] bg-[#141821] p-8 text-center text-[#a5b4cf]">Ucitavanje bilansa stanja...</div>;
  if (error && !pageData) return <div className="rounded-3xl border border-[#5b1f2c] bg-[#211116] p-8 text-center text-[#ffc3cf]">Greska: {error}</div>;

  return (
    <div className="space-y-6">
      <section className="overflow-hidden rounded-[30px] border border-[#273247] bg-[radial-gradient(circle_at_top_left,_rgba(68,208,255,0.24),_transparent_32%),linear-gradient(135deg,#121827_0%,#10131b_40%,#0f1722_100%)] p-6 shadow-[0_25px_80px_-45px_rgba(68,208,255,0.5)]">
        <div className="flex flex-col gap-6 lg:flex-row lg:items-start lg:justify-between">
          <div className="max-w-[760px]">
            <div className="mb-3 inline-flex items-center gap-2 rounded-full border border-[#30516d] bg-[#102231] px-3 py-1 text-xs font-semibold uppercase tracking-[0.24em] text-[#8edbff]"><Warehouse size={14} />Bilans stanja</div>
            <h1 className="text-3xl font-semibold tracking-tight text-white md:text-4xl">Operativni pregled zaliha sa stampom i report izvozom.</h1>
            <p className="mt-3 max-w-[640px] text-sm leading-6 text-[#a8b6d0] md:text-base">Stranica sada spaja KPI pregled, filtriranje po prodavnici i dobavljacu, tabelarni rad, detalje artikla i server-side dokumente za deljenje sa timom.</p>
          </div>
          <div className="grid min-w-[280px] gap-3 sm:grid-cols-2">
            <div className="rounded-2xl border border-[#294a63] bg-[#101d29]/80 p-4">
              <div className="text-xs uppercase tracking-[0.22em] text-[#89d9ff]">Health score</div>
              <div className="mt-2 text-3xl font-semibold text-white">{inventoryHealthScore}</div>
              <div className="mt-2 text-sm text-[#a8bdd1]">{inventoryHealthScore >= 85 ? "Stabilan fond robe." : inventoryHealthScore >= 65 ? "Potrebno pracenje kriticnih SKU." : "Povecan rizik od praznih polica."}</div>
            </div>
            <div className="rounded-2xl border border-[#3f3520] bg-[#21180f]/80 p-4">
              <div className="text-xs uppercase tracking-[0.22em] text-[#f2c66d]">Aktivni SKU</div>
              <div className="mt-2 text-3xl font-semibold text-white">{formatPercent(activeSkuShare)}</div>
              <div className="mt-2 text-sm text-[#d5c19c]">Udeo artikala koji nisu bez zaliha.</div>
            </div>
          </div>
        </div>
      </section>

      <section className="rounded-[28px] border border-[#242936] bg-[#12161f] p-5 shadow-[0_18px_45px_-30px_rgba(0,0,0,0.9)]">
        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-3 xl:flex-row xl:items-end xl:justify-between">
            <div>
              <h2 className="text-lg font-semibold text-white">Filteri i akcije</h2>
              <p className="text-sm text-[#93a2bd]">Pretrazi bilans, suzi lokaciju i odmah pokreni report ili stampu.</p>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <button type="button" onClick={() => void runServerExport("pdf", true)} disabled={exportBusy || totalCount === 0} className="inline-flex items-center gap-2 rounded-xl border border-[#41516b] bg-[#1d2432] px-3 py-2 text-xs font-semibold text-[#d8e5f8] disabled:cursor-not-allowed disabled:opacity-60"><Printer size={14} />Print preview</button>
              <button type="button" onClick={exportVisibleCsv} disabled={rows.length === 0} className="inline-flex items-center gap-2 rounded-xl border border-[#345269] bg-[#152534] px-3 py-2 text-xs font-semibold text-[#9fe0ff] disabled:cursor-not-allowed disabled:opacity-60"><Download size={14} />CSV ekran</button>
              <button type="button" onClick={() => void runServerExport("csv")} disabled={exportBusy || totalCount === 0} className="inline-flex items-center gap-2 rounded-xl border border-[#27485e] bg-[#11202d] px-3 py-2 text-xs font-semibold text-[#b7e7ff] disabled:cursor-not-allowed disabled:opacity-60"><Download size={14} />CSV filtrirano</button>
              <button type="button" onClick={() => void runServerExport("xlsx")} disabled={exportBusy || totalCount === 0} className="inline-flex items-center gap-2 rounded-xl border border-[#36543f] bg-[#17261d] px-3 py-2 text-xs font-semibold text-[#aef3bf] disabled:cursor-not-allowed disabled:opacity-60"><FileSpreadsheet size={14} />Excel filtrirano</button>
              <button type="button" onClick={() => void runServerExport("pdf")} disabled={exportBusy || totalCount === 0} className="inline-flex items-center gap-2 rounded-xl border border-[#5f445e] bg-[#261827] px-3 py-2 text-xs font-semibold text-[#ffc8fb] disabled:cursor-not-allowed disabled:opacity-60"><FileText size={14} />PDF filtrirano</button>
              <button type="button" onClick={() => window.location.reload()} className="inline-flex items-center gap-2 rounded-xl border border-[#3b4558] bg-[#1f2532] px-3 py-2 text-xs font-semibold text-[#dbe6fb]"><RefreshCw size={14} />Osvezi</button>
            </div>
          </div>

          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-[minmax(0,1.5fr)_repeat(4,minmax(0,1fr))]">
            <label className="flex items-center gap-3 rounded-2xl border border-[#283042] bg-[#10141c] px-4 py-3">
              <Search size={16} className="text-[#7ec6ff]" />
              <input value={searchInput} onChange={(event) => { setSearchInput(event.target.value); setPageNumber(1); }} placeholder="Pretraga po PLU ili nazivu artikla" className="w-full bg-transparent text-sm text-white outline-none placeholder:text-[#73809a]" />
            </label>
            <label className="rounded-2xl border border-[#283042] bg-[#10141c] px-4 py-3 text-sm text-[#dbe6fb]">
              <span className="mb-1 block text-[11px] uppercase tracking-[0.2em] text-[#7f8fa9]">Prodavnica</span>
              <select value={selectedStoreId ?? ""} onChange={(event) => { setSelectedStoreId(event.target.value ? Number(event.target.value) : null); setSelectedSupplierId(null); setPageNumber(1); }} className="w-full bg-transparent outline-none">
                <option value="">Sve prodavnice</option>
                {stores.map((store) => <option key={store.storeId} value={store.storeId}>{store.storeName}</option>)}
              </select>
            </label>
            <label className="rounded-2xl border border-[#283042] bg-[#10141c] px-4 py-3 text-sm text-[#dbe6fb]">
              <span className="mb-1 block text-[11px] uppercase tracking-[0.2em] text-[#7f8fa9]">Dobavljac</span>
              <select value={selectedSupplierId ?? ""} onChange={(event) => { setSelectedSupplierId(event.target.value ? Number(event.target.value) : null); setPageNumber(1); }} className="w-full bg-transparent outline-none" disabled={filtersLoading}>
                <option value="">Svi dobavljaci</option>
                {suppliers.map((supplier) => <option key={supplier.supplierId} value={supplier.supplierId}>{supplier.supplierName}</option>)}
              </select>
            </label>
            <label className="rounded-2xl border border-[#283042] bg-[#10141c] px-4 py-3 text-sm text-[#dbe6fb]">
              <span className="mb-1 block text-[11px] uppercase tracking-[0.2em] text-[#7f8fa9]">Sortiranje</span>
              <select value={sortBy} onChange={(event) => { setSortBy(event.target.value); setPageNumber(1); }} className="w-full bg-transparent outline-none">
                <option value="kolicina">Kolicina opadajuce</option>
                <option value="naziv">Naziv A-Z</option>
                <option value="vrednost">Vrednost opadajuce</option>
                <option value="azuriranje">Poslednje azuriranje</option>
              </select>
            </label>
            <label className="rounded-2xl border border-[#283042] bg-[#10141c] px-4 py-3 text-sm text-[#dbe6fb]">
              <span className="mb-1 block text-[11px] uppercase tracking-[0.2em] text-[#7f8fa9]">Velicina strane</span>
              <select value={pageSize} onChange={(event) => { setPageSize(Number(event.target.value)); setPageNumber(1); }} className="w-full bg-transparent outline-none">
                {PAGE_SIZE_OPTIONS.map((option) => <option key={option} value={option}>{option} redova</option>)}
              </select>
            </label>
          </div>

          {exportStatus ? <div className="rounded-2xl border border-[#284058] bg-[#101a24] px-4 py-3 text-sm text-[#9edcff]">{exportStatus}</div> : null}
          {error ? <div className="rounded-2xl border border-[#6a2334] bg-[#241118] px-4 py-3 text-sm text-[#ffbdcb]">{error}</div> : null}
        </div>
      </section>

      <InventoryKPICards totalSku={balance?.totalSku} totalOnHand={balance?.totalOnHand} lowStockCount={balance?.lowStockCount} lowStockShare={lowStockShare} avgUnitsPerSku={avgUnitsPerSku} totalValue={totalValue} />
      <MailSchedulerPanel scheduleDraft={scheduleDraft} setScheduleDraft={setScheduleDraft} schedules={schedules} schedulerBusy={schedulerBusy} schedulerMessage={schedulerMessage} onCopyCurrentFilters={copyCurrentFiltersToSchedule} onSaveSchedule={saveSchedule} onRunScheduleNow={(id) => void runScheduleNow(id)} />

      <div className="grid gap-5 xl:grid-cols-2">
        <StoreComparisonPanel stores={stores} compareStoreIds={compareStoreIds} comparison={storeComparison} operationsLoading={operationsLoading} onToggleStore={toggleCompareStore} />
        <ActionWorkflowPanel actionWorkflow={actionWorkflow} operationsLoading={operationsLoading} workflowBusyKey={workflowBusyKey} onUpdateWorkflowStatus={(item, status) => void updateWorkflowStatus(item, status)} />
      </div>

      <InventoryInsightPanels insights={insights} insightsLoading={insightsLoading} stores={stores} suppliers={suppliers} rows={rows} onOpenDetail={setDetailRow} />
      <InventoryPriorityPanels rows={rows} topRiskRows={topRiskRows} highestValueRows={highestValueRows} chartData={chartData} balance={balance} lowStockShare={lowStockShare} totalCount={totalCount} onOpenDetail={setDetailRow} />
      <InventoryAlertsFeed alerts={alerts} alertsLoading={alertsLoading} alertSeverityFilter={alertSeverityFilter} onSeverityFilterChange={setAlertSeverityFilter} displayCount={ALERTS_DISPLAY_COUNT} onOpenSizeCurve={setSizeCurveSkuId} />
      <DemandForecastPanel forecast={forecast} forecastLoading={forecastLoading} forecastError={forecastError} rows={rows} stores={stores} oosThreshold={OOS_RISK_THRESHOLD} overstockThreshold={OVERSTOCK_RISK_THRESHOLD} oosDisplayCount={FORECAST_OOS_DISPLAY} overstockDisplayCount={FORECAST_OVERSTOCK_DISPLAY} />
      <SizeCurvePanel sizeCurveSkuId={sizeCurveSkuId} sizeCurve={sizeCurve} sizeCurveLoading={sizeCurveLoading} onChangeSkuId={setSizeCurveSkuId} />
      <RebalancingTable rebalance={rebalance} rebalanceLoading={rebalanceLoading} rows={rows} stores={stores} displayCount={REBALANCE_DISPLAY_COUNT} />
      <InventoryItemsTable rows={rows} loading={loading} totalCount={totalCount} pageNumber={pageNumber} totalPages={totalPages} onOpenDetail={setDetailRow} onPreviousPage={() => setPageNumber((current) => Math.max(1, current - 1))} onNextPage={() => setPageNumber((current) => Math.min(totalPages, current + 1))} />
      <SKUDetailModal detailRow={detailRow} detailData={detailData} detailLoading={detailLoading} detailError={detailError} onClose={() => setDetailRow(null)} />
    </div>
  );
}

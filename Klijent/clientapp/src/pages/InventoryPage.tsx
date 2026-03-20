import { useDeferredValue, useEffect, useState } from "react";
import { AlertTriangle, Archive, ArrowRightLeft, CheckCircle2, Clock3, Download, FileSpreadsheet, FileText, GitCompareArrows, Mail, Play, Printer, RefreshCw, Search, Tag, TrendingDown, TrendingUp, Truck, Warehouse, XCircle } from "lucide-react";
import { Bar, BarChart, CartesianGrid, ComposedChart, Line, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import Modal from "../components/Modal";
import {
  createInventoryReportSchedule,
  exportInventoryReport,
  getForecast,
  getInventoryActionSuggestions,
  getInventoryAlerts,
  getInventoryBalance,
  getInventoryInsights,
  getInventoryItemDetail,
  getInventoryList,
  getInventoryReportSchedules,
  getInventoryStoreComparison,
  getRebalanceSuggestions,
  getSizeCurve,
  getStores,
  getSupplierFilters,
  previewInventoryReport,
  runInventoryReportScheduleNow,
  saveInventoryActionDecision,
} from "../services/analyticsApi";
import { downloadExport, resolveApiUrl, waitForExport } from "../services/exportApi";
import type {
  ForecastDto,
  InventoryActionSuggestion,
  InventoryActionWorkflow,
  InventoryAlertListDto,
  InventoryBalance,
  InventoryInsightItem,
  InventoryInsights,
  InventoryItemDetail,
  InventoryListItem,
  InventoryPagedResponse,
  InventoryReportSchedule,
  InventoryReportScheduleInput,
  InventoryStoreComparison,
  RebalanceListDto,
  SizeCurveDto,
  StoreOption,
  SupplierFilterOption,
} from "../types/analytics";

type InventoryRow = InventoryListItem & {
  supplierName: string;
  storeName: string;
  quantity: number;
  minimum: number;
  reorderGap: number;
  stockState: "critical" | "warning" | "healthy";
  stockStateLabel: string;
  estimatedValueAmount: number;
  unitCost: number;
  coverageRatio: number | null;
};

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
const WEEKDAY_OPTIONS = [
  { value: 1, label: "Ponedeljak" },
  { value: 2, label: "Utorak" },
  { value: 3, label: "Sreda" },
  { value: 4, label: "Cetvrtak" },
  { value: 5, label: "Petak" },
  { value: 6, label: "Subota" },
  { value: 0, label: "Nedelja" },
];

function formatNumber(value: number, digits = 0) {
  return value.toLocaleString("sr-RS", { maximumFractionDigits: digits });
}

function formatCurrency(value: number) {
  return value.toLocaleString("sr-RS", { style: "currency", currency: "RSD", maximumFractionDigits: 0 });
}

function formatPercent(value: number) {
  return `${value.toLocaleString("sr-RS", { maximumFractionDigits: 1 })}%`;
}

function csvEscape(value: string | number | null | undefined) {
  const raw = value == null ? "" : String(value);
  return /[",\n;]/.test(raw) ? `"${raw.replace(/"/g, "\"\"")}"` : raw;
}

function getStockState(quantity: number, minimum: number) {
  if (quantity <= 0) return { key: "critical" as const, label: "Bez zaliha", badge: "bg-[#4b1622] text-[#ffb4c2] border-[#7d2940]", panel: "from-[#411520] to-[#27141a]" };
  if (quantity <= minimum) return { key: "warning" as const, label: "Niska zaliha", badge: "bg-[#493518] text-[#ffd590] border-[#7c5822]", panel: "from-[#412d11] to-[#211a12]" };
  return { key: "healthy" as const, label: "Stabilno", badge: "bg-[#163829] text-[#9ff0c7] border-[#1f6c49]", panel: "from-[#123726] to-[#111b16]" };
}

function buildInventoryRow(item: InventoryListItem, stores: StoreOption[], suppliers: SupplierFilterOption[]): InventoryRow {
  const quantity = item.kolicina ?? 0;
  const minimum = item.minimalnaKolicina ?? 0;
  const supplierName = suppliers.find((entry) => entry.supplierId === item.idDobavljac)?.supplierName ?? (item.idDobavljac != null ? `Dobavljac #${item.idDobavljac}` : "Nerasporedjen");
  const storeName = stores.find((entry) => entry.storeId === item.idObjekat)?.storeName ?? (item.idObjekat != null ? `Objekat #${item.idObjekat}` : "Sve lokacije");
  const unitCost = item.nabavnaCena ?? 0;
  const estimatedValueAmount = item.estimatedValue ?? unitCost * quantity;
  const coverageRatio = minimum > 0 ? quantity / minimum : null;
  const stock = getStockState(quantity, minimum);

  return {
    ...item,
    supplierName,
    storeName,
    quantity,
    minimum,
    reorderGap: Math.max(minimum - quantity, 0),
    stockState: stock.key,
    stockStateLabel: stock.label,
    estimatedValueAmount,
    unitCost,
    coverageRatio,
  };
}

function getCoverageText(row: InventoryRow) {
  if (row.coverageRatio == null) return "Bez minimuma";
  if (row.coverageRatio >= 2) return "Komforna zaliha";
  if (row.coverageRatio >= 1) return "Na minimumu";
  return "Ispod minimuma";
}

function getRecommendation(row: InventoryRow) {
  if (row.stockState === "critical") return "Hitno proveriti dopunu ili redistribuciju iz druge lokacije.";
  if (row.stockState === "warning") return `Planirati dopunu od najmanje ${formatNumber(Math.max(row.reorderGap, 1))} komada.`;
  if (row.quantity >= Math.max(row.minimum * 3, 15)) return "Zaliha je komforna; proveri da li je kapital previse vezan u robi.";
  return "Zaliha je stabilna i ne zahteva hitnu akciju.";
}

function formatDateTime(value?: string | null) {
  if (!value) return "Nema podataka";
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return value;
  return parsed.toLocaleString("sr-RS");
}

function getAgingTone(bucket: string) {
  switch (bucket) {
    case "0-30":
      return "border-[#1f6c49] bg-[#123726] text-[#9ff0c7]";
    case "31-60":
      return "border-[#42628a] bg-[#162335] text-[#abd7ff]";
    case "61-90":
      return "border-[#7c5822] bg-[#412d11] text-[#ffd590]";
    default:
      return "border-[#7d2940] bg-[#411520] text-[#ffb4c2]";
  }
}

function getAbcTone(bucket: string) {
  switch (bucket) {
    case "A":
      return "border-[#5f445e] bg-[#261827] text-[#ffc8fb]";
    case "B":
      return "border-[#36543f] bg-[#17261d] text-[#aef3bf]";
    default:
      return "border-[#3b4558] bg-[#1f2532] text-[#dbe6fb]";
  }
}

function getHistoryDirection(quantity?: number | null) {
  if ((quantity ?? 0) > 0) return "Ulaz";
  if ((quantity ?? 0) < 0) return "Izlaz";
  return "Promena";
}

function buildSupplierChart(rows: InventoryRow[]) {
  const totals = new Map<string, number>();
  for (const row of rows) totals.set(row.supplierName, (totals.get(row.supplierName) ?? 0) + row.estimatedValueAmount);
  return Array.from(totals.entries()).map(([supplierName, totalValue]) => ({ supplierName, totalValue })).sort((a, b) => b.totalValue - a.totalValue).slice(0, TOP_SUPPLIERS_CHART);
}

function buildRowFromInsightItem(item: InventoryInsightItem, stores: StoreOption[], suppliers: SupplierFilterOption[]) {
  return buildInventoryRow({
    id: item.id,
    plu: item.plu,
    naziv: item.naziv,
    kolicina: item.quantity,
    minimalnaKolicina: item.minimum,
    nabavnaCena: item.estimatedValue > 0 && item.quantity > 0 ? item.estimatedValue / item.quantity : 0,
    estimatedValue: item.estimatedValue,
    idObjekat: stores.find((store) => store.storeName === item.storeName)?.storeId ?? null,
    idDobavljac: suppliers.find((supplier) => supplier.supplierName === item.supplierName)?.supplierId ?? null,
  }, stores, suppliers);
}

function buildStoreLabel(store: StoreOption) {
  const extras = [store.city, store.region].filter(Boolean).join(", ");
  return extras ? `${store.storeName} (${extras})` : store.storeName;
}

function getActionTypeTone(actionType: string) {
  switch (actionType) {
    case "dopuna":
      return "border-[#28574d] bg-[#102b24] text-[#9ff0c7]";
    case "transfer":
      return "border-[#30516d] bg-[#102231] text-[#8edbff]";
    case "markdown":
      return "border-[#7c5822] bg-[#412d11] text-[#ffd590]";
    default:
      return "border-[#7d2940] bg-[#411520] text-[#ffb4c2]";
  }
}

function getActionStatusTone(status: string) {
  switch (status) {
    case "approved":
      return "border-[#28574d] bg-[#102b24] text-[#9ff0c7]";
    case "deferred":
      return "border-[#4b5670] bg-[#172031] text-[#dbe6fb]";
    case "closed":
      return "border-[#6b2c38] bg-[#281319] text-[#ffc3cf]";
    default:
      return "border-[#30516d] bg-[#102231] text-[#8edbff]";
  }
}

function getPriorityTone(priority: string) {
  switch (priority) {
    case "critical":
      return "text-[#ffb4c2]";
    case "high":
      return "text-[#ffd590]";
    case "medium":
      return "text-[#9fe0ff]";
    default:
      return "text-[#dbe6fb]";
  }
}

function getAlertSeverityTone(severity: string) {
  switch (severity) {
    case "critical": return "border-[#7d2940] bg-[#411520] text-[#ffb4c2]";
    case "warning": return "border-[#7c5822] bg-[#412d11] text-[#ffd590]";
    default: return "border-[#30516d] bg-[#102231] text-[#8edbff]";
  }
}

function getRebalanceUrgencyTone(urgency: string) {
  switch (urgency) {
    case "urgent": return "border-[#7d2940] bg-[#411520] text-[#ffb4c2]";
    case "recommended": return "border-[#7c5822] bg-[#412d11] text-[#ffd590]";
    default: return "border-[#33405a] bg-[#182131] text-[#dbe6fb]";
  }
}

function createScheduleDraft(): InventoryReportScheduleInput {
  return {
    name: "",
    isEnabled: true,
    frequency: "daily",
    dayOfWeek: 1,
    runAtLocalTime: "08:00",
    timeZoneId: "Europe/Belgrade",
    format: "pdf",
    orientation: "landscape",
    includeFiltersAndMetadata: true,
    recipientsCsv: "",
    subject: "",
    search: "",
    storeId: null,
    supplierId: null,
    sortBy: "kolicina",
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
  const [alertSeverityFilter, setAlertSeverityFilter] = useState("");
  const [rebalance, setRebalance] = useState<RebalanceListDto | null>(null);
  const [rebalanceLoading, setRebalanceLoading] = useState(true);
  const [sizeCurve, setSizeCurve] = useState<SizeCurveDto | null>(null);
  const [sizeCurveLoading, setSizeCurveLoading] = useState(false);
  const [sizeCurveSkuId, setSizeCurveSkuId] = useState<number | null>(null);
  const deferredSearch = useDeferredValue(searchInput);
  const trimmedSearch = deferredSearch.trim();

  useEffect(() => {
    let cancelled = false;
    void getStores(true).then((nextStores) => {
      if (!cancelled) {
        setStores(nextStores);
        setCompareStoreIds((current) => current.length > 0 ? current : nextStores.slice(0, DEFAULT_COMPARE_STORES).map((store) => store.storeId));
      }
    }).catch(console.error).finally(() => {
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
    void getSupplierFilters(undefined, undefined, true, selectedStoreId ?? undefined).then((nextSuppliers) => {
      if (cancelled) return;
      setSuppliers(nextSuppliers);
      if (selectedSupplierId != null && !nextSuppliers.some((entry) => entry.supplierId === selectedSupplierId)) setSelectedSupplierId(null);
    }).catch(() => {
      if (!cancelled) setSuppliers([]);
    });
    return () => { cancelled = true; };
  }, [selectedStoreId, selectedSupplierId]);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setInsightsLoading(true);
    setError(null);
    void Promise.all([
      getInventoryBalance(true, selectedStoreId, selectedSupplierId),
      getInventoryList({ pageNumber, pageSize, search: trimmedSearch || undefined, storeId: selectedStoreId, supplierId: selectedSupplierId, sortBy }),
      getInventoryInsights({ search: trimmedSearch || undefined, storeId: selectedStoreId, supplierId: selectedSupplierId, sortBy }),
    ]).then(([nextBalance, nextPage, nextInsights]) => {
      if (cancelled) return;
      setBalance(nextBalance);
      setPageData(nextPage);
      setInsights(nextInsights);
    }).catch((reason) => {
      if (!cancelled) setError(reason instanceof Error ? reason.message : String(reason));
    }).finally(() => {
      if (!cancelled) {
        setLoading(false);
        setInsightsLoading(false);
      }
    });
    return () => { cancelled = true; };
  }, [pageNumber, pageSize, selectedStoreId, selectedSupplierId, sortBy, trimmedSearch]);

  useEffect(() => {
    let cancelled = false;
    setOperationsLoading(true);
    void Promise.all([
      getInventoryStoreComparison({ compareStoreIds, supplierId: selectedSupplierId, search: trimmedSearch || undefined }),
      getInventoryActionSuggestions({ storeId: selectedStoreId, supplierId: selectedSupplierId, search: trimmedSearch || undefined }),
    ])
      .then(([nextComparison, nextWorkflow]) => {
        if (cancelled) return;
        setStoreComparison(nextComparison);
        setActionWorkflow(nextWorkflow);
      })
      .catch((reason) => {
        if (!cancelled) setError(reason instanceof Error ? reason.message : String(reason));
      })
      .finally(() => {
        if (!cancelled) setOperationsLoading(false);
      });
    return () => { cancelled = true; };
  }, [compareStoreIds, selectedStoreId, selectedSupplierId, trimmedSearch]);

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

  // Forecast, alerts, rebalancing — refresh on store / supplier filter change
  useEffect(() => {
    let cancelled = false;
    setForecastLoading(true);
    setAlertsLoading(true);
    setRebalanceLoading(true);
    setForecastError(null);
    void Promise.allSettled([
      getForecast({ storeId: selectedStoreId, supplierId: selectedSupplierId, top: FORECAST_FETCH_LIMIT }),
      getInventoryAlerts({ storeId: selectedStoreId, supplierId: selectedSupplierId }),
      getRebalanceSuggestions({ supplierId: selectedSupplierId, top: REBALANCE_FETCH_LIMIT }),
    ]).then(([forecastResult, alertsResult, rebalanceResult]) => {
      if (cancelled) return;
      if (forecastResult.status === "fulfilled") {
        setForecast(forecastResult.value);
      } else {
        setForecastError(forecastResult.reason instanceof Error ? forecastResult.reason.message : String(forecastResult.reason));
      }
      if (alertsResult.status === "fulfilled") setAlerts(alertsResult.value);
      if (rebalanceResult.status === "fulfilled") setRebalance(rebalanceResult.value);
      setForecastLoading(false);
      setAlertsLoading(false);
      setRebalanceLoading(false);
    });
    return () => { cancelled = true; };
  }, [selectedStoreId, selectedSupplierId]);

  // Size curve — on-demand by SKU ID
  useEffect(() => {
    if (sizeCurveSkuId == null) { setSizeCurve(null); return; }
    let cancelled = false;
    setSizeCurveLoading(true);
    void getSizeCurve({ skuId: sizeCurveSkuId, storeId: selectedStoreId })
      .then((data) => { if (!cancelled) setSizeCurve(data); })
      .catch(console.error)
      .finally(() => { if (!cancelled) setSizeCurveLoading(false); });
    return () => { cancelled = true; };
  }, [sizeCurveSkuId, selectedStoreId]);

  const rows = (pageData?.items ?? []).map((item) => buildInventoryRow(item, stores, suppliers));
  const totalCount = pageData?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const totalValue = balance?.estimatedInventoryValue ?? rows.reduce((sum, row) => sum + row.estimatedValueAmount, 0);
  const activeSkuShare = balance && balance.totalSku > 0 ? ((balance.totalSku - balance.outOfStockCount) / balance.totalSku) * 100 : 0;
  const lowStockShare = balance && balance.totalSku > 0 ? (balance.lowStockCount / balance.totalSku) * 100 : 0;
  const avgUnitsPerSku = balance && balance.totalSku > 0 ? balance.totalOnHand / balance.totalSku : 0;
  const inventoryHealthScore = Math.max(0, Math.round(100 - (balance && balance.totalSku > 0 ? (balance.outOfStockCount / balance.totalSku) * 60 : 0) - (balance && balance.totalSku > 0 ? (balance.lowStockCount / balance.totalSku) * 25 : 0)));
  const chartData = buildSupplierChart(rows);
  const topRiskRows = rows.slice().sort((a, b) => (a.stockState === b.stockState ? b.reorderGap - a.reorderGap : { critical: 0, warning: 1, healthy: 2 }[a.stockState] - { critical: 0, warning: 1, healthy: 2 }[b.stockState])).slice(0, TOP_RISK_ITEMS);
  const highestValueRows = rows.slice().sort((a, b) => b.estimatedValueAmount - a.estimatedValueAmount).slice(0, TOP_VALUE_ITEMS);
  const agingBuckets = insights?.aging ?? [];
  const abcBuckets = insights?.abc ?? [];
  const agedItems = insights?.topAgedItems ?? [];
  const capitalLockedItems = insights?.topCapitalLockedItems ?? [];
  const staleBucket = agingBuckets.find((bucket) => bucket.bucketKey === "90+");
  const classABucket = abcBuckets.find((bucket) => bucket.bucketKey === "A");
  const comparisonStores = storeComparison?.stores ?? [];
  const comparisonRisks = storeComparison?.sharedRisks ?? [];
  const workflowItems = actionWorkflow?.items ?? [];

  async function refreshSchedules() {
    const nextSchedules = await getInventoryReportSchedules();
    setSchedules(nextSchedules);
  }

  async function refreshOperations() {
    const [nextComparison, nextWorkflow] = await Promise.all([
      getInventoryStoreComparison({ compareStoreIds, supplierId: selectedSupplierId, search: trimmedSearch || undefined }),
      getInventoryActionSuggestions({ storeId: selectedStoreId, supplierId: selectedSupplierId, search: trimmedSearch || undefined }),
    ]);
    setStoreComparison(nextComparison);
    setActionWorkflow(nextWorkflow);
  }

  async function runServerExport(format: "pdf" | "xlsx" | "csv", preview = false) {
    if (totalCount === 0 || exportBusy) return;
    try {
      setExportBusy(true);
      setExportStatus(preview ? "Pripremam print preview na serveru..." : "Server priprema dokument za izvoz...");
      if (preview) {
        const previewResult = await previewInventoryReport({
          orientation: "landscape",
          includeFiltersAndMetadata: true,
          search: trimmedSearch || undefined,
          storeId: selectedStoreId,
          supplierId: selectedSupplierId,
          sortBy,
        });
        if (previewResult.printUrl) window.open(resolveApiUrl(previewResult.printUrl), "_blank", "noopener");
        setExportStatus("Print preview je otvoren u novom tabu.");
        return;
      }
      const result = await exportInventoryReport({
        format,
        orientation: "landscape",
        includeFiltersAndMetadata: true,
        forceAsync: totalCount > 5000,
        search: trimmedSearch || undefined,
        storeId: selectedStoreId,
        supplierId: selectedSupplierId,
        sortBy,
      });
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
      await saveInventoryActionDecision(item.suggestionKey, {
        actionType: item.actionType,
        status,
        note: item.note ?? "",
      });
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
    setCompareStoreIds((current) => {
      if (current.includes(storeId)) {
        return current.filter((value) => value !== storeId);
      }
      if (current.length >= DEFAULT_COMPARE_STORES) {
        return [...current.slice(1), storeId];
      }
      return [...current, storeId];
    });
  }

  function copyCurrentFiltersToSchedule() {
    setScheduleDraft((current) => ({
      ...current,
      search: trimmedSearch,
      storeId: selectedStoreId,
      supplierId: selectedSupplierId,
      sortBy,
    }));
    setSchedulerMessage("Trenutni filteri su prepisani u scheduler formu.");
  }

  if (loading && !pageData && !balance) return <div className="rounded-3xl border border-[#202430] bg-[#141821] p-8 text-center text-[#a5b4cf]">Ucitavanje bilansa stanja...</div>;
  if (error && !pageData) return <div className="rounded-3xl border border-[#5b1f2c] bg-[#211116] p-8 text-center text-[#ffc3cf]">Greska: {error}</div>;

  return (
    <div className="space-y-6">
      <section className="overflow-hidden rounded-[30px] border border-[#273247] bg-[radial-gradient(circle_at_top_left,_rgba(68,208,255,0.24),_transparent_32%),linear-gradient(135deg,#121827_0%,#10131b_40%,#0f1722_100%)] p-6 shadow-[0_25px_80px_-45px_rgba(68,208,255,0.5)]">
        <div className="flex flex-col gap-6 lg:flex-row lg:items-start lg:justify-between">
          <div className="max-w-[760px]">
            <div className="mb-3 inline-flex items-center gap-2 rounded-full border border-[#30516d] bg-[#102231] px-3 py-1 text-xs font-semibold uppercase tracking-[0.24em] text-[#8edbff]">
              <Warehouse size={14} />
              Bilans stanja
            </div>
            <h1 className="text-3xl font-semibold tracking-tight text-white md:text-4xl">Operativni pregled zaliha sa stampom i report izvozom.</h1>
            <p className="mt-3 max-w-[640px] text-sm leading-6 text-[#a8b6d0] md:text-base">
              Stranica sada spaja KPI pregled, filtriranje po prodavnici i dobavljacu, tabelarni rad, detalje artikla i server-side dokumente za deljenje sa timom.
            </p>
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

      <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
        {[
          { label: "Ukupno SKU", value: balance ? formatNumber(balance.totalSku) : "-", note: "Broj jedinstvenih artikala u izabranom opsegu." },
          { label: "Ukupno na stanju", value: balance ? formatNumber(balance.totalOnHand) : "-", note: "Ukupna raspoloziva kolicina robe." },
          { label: "Niska zaliha", value: balance ? formatNumber(balance.lowStockCount) : "-", note: `${formatPercent(lowStockShare)} fonda je blizu minimuma.` },
          { label: "Prosecno po SKU", value: formatNumber(avgUnitsPerSku, 1), note: "Srednja kolicina robe po artiklu." },
          { label: "Procena vrednosti", value: formatCurrency(totalValue), note: "Nabavna vrednost ukupne zalihe." },
        ].map((card) => (
          <article key={card.label} className="rounded-[24px] border border-[#252c39] bg-gradient-to-br from-[#151d2c] to-[#10141b] p-5 shadow-[0_16px_40px_-32px_rgba(0,0,0,0.9)]">
            <div className="text-xs uppercase tracking-[0.22em] text-[#90a2bf]">{card.label}</div>
            <div className="mt-4 text-2xl font-semibold text-white">{card.value}</div>
            <p className="mt-3 text-sm leading-5 text-[#9caac3]">{card.note}</p>
          </article>
        ))}
      </section>

      <section className="grid gap-5 xl:grid-cols-[1.1fr_0.9fr]">
        <div className="rounded-[28px] border border-[#232935] bg-[#12161f] p-5">
          <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
            <div>
              <h2 className="text-lg font-semibold text-white">Mail scheduler za dnevni i nedeljni report</h2>
              <p className="text-sm text-[#90a0ba]">Zakazi PDF/Excel/CSV bilans stanja, sa lokalnim vremenom, filterima i ručnim pokretanjem.</p>
            </div>
            <div className="flex flex-wrap gap-2">
              <button type="button" onClick={copyCurrentFiltersToSchedule} className="inline-flex items-center gap-2 rounded-xl border border-[#30516d] bg-[#102231] px-3 py-2 text-xs font-semibold text-[#8edbff]">
                <RefreshCw size={14} />
                Preuzmi trenutne filtere
              </button>
              <button type="button" onClick={() => void saveSchedule()} disabled={schedulerBusy || !scheduleDraft.name.trim() || !scheduleDraft.recipientsCsv.trim()} className="inline-flex items-center gap-2 rounded-xl border border-[#36543f] bg-[#17261d] px-3 py-2 text-xs font-semibold text-[#aef3bf] disabled:cursor-not-allowed disabled:opacity-60">
                <Mail size={14} />
                Sacuvaj raspored
              </button>
            </div>
          </div>

          <div className="mt-5 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
            <label className="rounded-2xl border border-[#283042] bg-[#10141c] px-4 py-3 text-sm text-[#dbe6fb]">
              <span className="mb-1 block text-[11px] uppercase tracking-[0.2em] text-[#7f8fa9]">Naziv rasporeda</span>
              <input value={scheduleDraft.name} onChange={(event) => setScheduleDraft((current) => ({ ...current, name: event.target.value }))} placeholder="Npr. Nedeljni retail PDF" className="w-full bg-transparent outline-none placeholder:text-[#73809a]" />
            </label>
            <label className="rounded-2xl border border-[#283042] bg-[#10141c] px-4 py-3 text-sm text-[#dbe6fb]">
              <span className="mb-1 block text-[11px] uppercase tracking-[0.2em] text-[#7f8fa9]">Primaoci</span>
              <input value={scheduleDraft.recipientsCsv} onChange={(event) => setScheduleDraft((current) => ({ ...current, recipientsCsv: event.target.value }))} placeholder="manager@firma.rs; retail@firma.rs" className="w-full bg-transparent outline-none placeholder:text-[#73809a]" />
            </label>
            <label className="rounded-2xl border border-[#283042] bg-[#10141c] px-4 py-3 text-sm text-[#dbe6fb]">
              <span className="mb-1 block text-[11px] uppercase tracking-[0.2em] text-[#7f8fa9]">Frekvencija</span>
              <select value={scheduleDraft.frequency} onChange={(event) => setScheduleDraft((current) => ({ ...current, frequency: event.target.value as "daily" | "weekly" }))} className="w-full bg-transparent outline-none">
                <option value="daily">Dnevno</option>
                <option value="weekly">Nedeljno</option>
              </select>
            </label>
            <label className="rounded-2xl border border-[#283042] bg-[#10141c] px-4 py-3 text-sm text-[#dbe6fb]">
              <span className="mb-1 block text-[11px] uppercase tracking-[0.2em] text-[#7f8fa9]">Dan / vreme</span>
              <div className="grid grid-cols-[1fr_auto] gap-2">
                <select value={scheduleDraft.dayOfWeek ?? 1} onChange={(event) => setScheduleDraft((current) => ({ ...current, dayOfWeek: Number(event.target.value) }))} className="w-full bg-transparent outline-none">
                  {WEEKDAY_OPTIONS.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                </select>
                <input type="time" value={scheduleDraft.runAtLocalTime} onChange={(event) => setScheduleDraft((current) => ({ ...current, runAtLocalTime: event.target.value }))} className="rounded-xl border border-[#33405a] bg-[#182131] px-3 py-2 text-[#dbe6fb] outline-none" />
              </div>
            </label>
            <label className="rounded-2xl border border-[#283042] bg-[#10141c] px-4 py-3 text-sm text-[#dbe6fb]">
              <span className="mb-1 block text-[11px] uppercase tracking-[0.2em] text-[#7f8fa9]">Format</span>
              <select value={scheduleDraft.format} onChange={(event) => setScheduleDraft((current) => ({ ...current, format: event.target.value as "pdf" | "xlsx" | "csv" }))} className="w-full bg-transparent outline-none">
                <option value="pdf">PDF</option>
                <option value="xlsx">Excel</option>
                <option value="csv">CSV</option>
              </select>
            </label>
            <label className="rounded-2xl border border-[#283042] bg-[#10141c] px-4 py-3 text-sm text-[#dbe6fb]">
              <span className="mb-1 block text-[11px] uppercase tracking-[0.2em] text-[#7f8fa9]">Predmet mejla</span>
              <input value={scheduleDraft.subject ?? ""} onChange={(event) => setScheduleDraft((current) => ({ ...current, subject: event.target.value }))} placeholder="Bilans stanja | dnevni pregled" className="w-full bg-transparent outline-none placeholder:text-[#73809a]" />
            </label>
            <label className="rounded-2xl border border-[#283042] bg-[#10141c] px-4 py-3 text-sm text-[#dbe6fb]">
              <span className="mb-1 block text-[11px] uppercase tracking-[0.2em] text-[#7f8fa9]">Time zone</span>
              <input value={scheduleDraft.timeZoneId} onChange={(event) => setScheduleDraft((current) => ({ ...current, timeZoneId: event.target.value }))} className="w-full bg-transparent outline-none" />
            </label>
            <label className="flex items-center gap-3 rounded-2xl border border-[#283042] bg-[#10141c] px-4 py-3 text-sm text-[#dbe6fb]">
              <input type="checkbox" checked={scheduleDraft.isEnabled} onChange={(event) => setScheduleDraft((current) => ({ ...current, isEnabled: event.target.checked }))} />
              <span>Raspored je aktivan odmah po čuvanju</span>
            </label>
          </div>

          {schedulerMessage ? <div className="mt-4 rounded-2xl border border-[#284058] bg-[#101a24] px-4 py-3 text-sm text-[#9edcff]">{schedulerMessage}</div> : null}

          <div className="mt-5 space-y-3">
            {schedules.length === 0 ? <div className="rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-8 text-center text-sm text-[#8797b4]">Jos nema sacuvanih rasporeda za Bilans stanja.</div> : schedules.map((schedule) => (
              <div key={schedule.id} className="rounded-2xl border border-[#243040] bg-[#10141b] p-4">
                <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                  <div>
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="text-sm font-semibold text-white">{schedule.name}</span>
                      <span className={`inline-flex rounded-full border px-2.5 py-1 text-[11px] font-semibold ${schedule.isEnabled ? "border-[#28574d] bg-[#102b24] text-[#9ff0c7]" : "border-[#6b2c38] bg-[#281319] text-[#ffc3cf]"}`}>{schedule.isEnabled ? "Aktivan" : "Pauziran"}</span>
                      <span className="inline-flex rounded-full border border-[#33405a] bg-[#182131] px-2.5 py-1 text-[11px] font-semibold text-[#dbe6fb]">{schedule.frequency === "weekly" ? "Nedeljno" : "Dnevno"} u {schedule.runAtLocalTime}</span>
                    </div>
                    <div className="mt-2 text-sm text-[#90a0ba]">{schedule.format.toUpperCase()} | {schedule.recipientsCsv}</div>
                    <div className="mt-2 text-xs text-[#7f8fa9]">
                      Poslednje pokretanje: {schedule.lastRunAtUtc ? new Date(schedule.lastRunAtUtc).toLocaleString("sr-RS") : "jos nije pokrenuto"}{schedule.lastRunStatus ? ` | status: ${schedule.lastRunStatus}` : ""}
                    </div>
                    {schedule.lastError ? <div className="mt-2 text-xs text-[#ffbdcb]">{schedule.lastError}</div> : null}
                  </div>
                  <button type="button" onClick={() => void runScheduleNow(schedule.id)} disabled={schedulerBusy} className="inline-flex items-center gap-2 rounded-xl border border-[#30516d] bg-[#102231] px-3 py-2 text-xs font-semibold text-[#8edbff] disabled:cursor-not-allowed disabled:opacity-60">
                    <Play size={14} />
                    Pokreni sada
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>

        <div className="rounded-[28px] border border-[#232935] bg-[#12161f] p-5">
          <div className="flex items-start gap-3">
            <div className="rounded-2xl border border-[#30516d] bg-[#102231] p-3 text-[#8edbff]"><Mail size={18} /></div>
            <div>
              <h2 className="text-lg font-semibold text-white">Šta scheduler sada pokriva</h2>
              <p className="mt-2 text-sm leading-6 text-[#90a0ba]">Scheduler koristi isti server-side export kao ručni PDF/Excel, pa menadžment dobija isti izgled i iste filtere kao operativa na ekranu.</p>
            </div>
          </div>
          <div className="mt-5 grid gap-3">
            {[
              "Dnevni ili nedeljni PDF/Excel/CSV report za izabrani store, dobavljača ili pretragu.",
              "Ručni 'run now' za proveru pre nego što raspored pustiš timu.",
              "Fail-safe ponašanje: ako SMTP nije uključen, dokument se i dalje generiše i scheduler ne pada.",
              "Subject, filter scope i lokalno vreme se čuvaju uz svaki raspored."
            ].map((line) => (
              <div key={line} className="rounded-2xl border border-[#243040] bg-[#10141b] px-4 py-3 text-sm text-[#dbe6fb]">{line}</div>
            ))}
          </div>
        </div>
      </section>

      <section className="grid gap-5 xl:grid-cols-[1.02fr_0.98fr]">
        <div className="rounded-[28px] border border-[#232935] bg-[#12161f] p-5">
          <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
            <div>
              <h2 className="text-lg font-semibold text-white">Drill-down po prodavnici</h2>
              <p className="text-sm text-[#90a0ba]">Uporedi do tri lokacije po zdravlju zalihe, vezanom kapitalu i zajedničkim rizicima.</p>
            </div>
            <div className="rounded-full border border-[#33405a] bg-[#182131] px-3 py-1 text-xs font-semibold text-[#dbe6fb]">
              {comparisonStores.length} lokacije u poređenju
            </div>
          </div>

          <div className="mt-4 flex flex-wrap gap-2">
            {stores.map((store) => {
              const active = compareStoreIds.includes(store.storeId);
              return (
                <button key={store.storeId} type="button" onClick={() => toggleCompareStore(store.storeId)} className={`rounded-full border px-3 py-1.5 text-xs font-semibold transition ${active ? "border-[#30516d] bg-[#102231] text-[#8edbff]" : "border-[#33405a] bg-[#182131] text-[#dbe6fb]"}`}>
                  {buildStoreLabel(store)}
                </button>
              );
            })}
          </div>

          <div className="mt-5 grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            {operationsLoading && comparisonStores.length === 0 ? <div className="col-span-full rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-8 text-center text-sm text-[#8797b4]">Ucitavam poredenje lokacija...</div> : comparisonStores.map((store) => (
              <article key={store.storeId} className="rounded-2xl border border-[#243040] bg-[#10141b] p-4">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <div className="text-sm font-semibold text-white">{store.storeName}</div>
                    <div className="mt-1 text-xs text-[#90a0ba]">{formatNumber(store.totalSku)} SKU | {formatCurrency(store.estimatedValue)}</div>
                  </div>
                  <GitCompareArrows size={16} className="text-[#8edbff]" />
                </div>
                <div className="mt-4 grid grid-cols-2 gap-3 text-sm">
                  <div className="rounded-2xl bg-[#131e2b] p-3"><div className="text-xs uppercase tracking-[0.18em] text-[#89d9ff]">Healthy</div><div className="mt-2 text-lg font-semibold text-white">{formatPercent(store.healthySharePct)}</div></div>
                  <div className="rounded-2xl bg-[#241b11] p-3"><div className="text-xs uppercase tracking-[0.18em] text-[#f0c36b]">Low stock</div><div className="mt-2 text-lg font-semibold text-white">{formatNumber(store.lowStockCount)}</div></div>
                  <div className="rounded-2xl bg-[#26161a] p-3"><div className="text-xs uppercase tracking-[0.18em] text-[#ffbdcb]">Critical</div><div className="mt-2 text-lg font-semibold text-white">{formatNumber(store.criticalCount)}</div></div>
                  <div className="rounded-2xl bg-[#1d1726] p-3"><div className="text-xs uppercase tracking-[0.18em] text-[#c4a3ff]">90+ dana</div><div className="mt-2 text-lg font-semibold text-white">{formatNumber(store.stale90PlusCount)}</div></div>
                </div>
              </article>
            ))}
          </div>

          <div className="mt-5 rounded-2xl border border-[#243040] bg-[#10141b] p-4">
            <div className="text-sm font-semibold text-white">Zaključak poređenja</div>
            <div className="mt-2 text-sm leading-6 text-[#90a0ba]">{storeComparison?.summary ?? "Nema dovoljno podataka za zaključak."}</div>
            <div className="mt-4 space-y-3">
              {comparisonRisks.length === 0 ? <div className="text-sm text-[#8797b4]">Za izabrane lokacije nema zajedničkih low-stock rizika.</div> : comparisonRisks.map((risk) => (
                <div key={risk.skuKey} className="flex items-center justify-between gap-3 rounded-xl border border-[#283142] bg-[#141b26] px-3 py-3">
                  <div>
                    <div className="text-sm font-semibold text-white">{risk.label}</div>
                    <div className="mt-1 text-xs text-[#90a0ba]">{risk.impactedStores.join(" | ")}</div>
                  </div>
                  <div className="rounded-full border border-[#7c5822] bg-[#412d11] px-2.5 py-1 text-[11px] font-semibold text-[#ffd590]">{risk.storeCoverage} lokacije</div>
                </div>
              ))}
            </div>
          </div>
        </div>

        <div className="rounded-[28px] border border-[#232935] bg-[#12161f] p-5">
          <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
            <div>
              <h2 className="text-lg font-semibold text-white">Predlog akcije workflow</h2>
              <p className="text-sm text-[#90a0ba]">Dopuna, transfer, markdown i clearance predlozi sa statusom obrade i brzim odlukama.</p>
            </div>
            <div className="rounded-full border border-[#33405a] bg-[#182131] px-3 py-1 text-xs font-semibold text-[#dbe6fb]">
              {workflowItems.length} aktivnih predloga
            </div>
          </div>

          <div className="mt-4 grid gap-3 sm:grid-cols-4">
            <div className="rounded-2xl border border-[#243040] bg-[#10141b] p-3"><div className="text-xs uppercase tracking-[0.18em] text-[#8edbff]">Pending</div><div className="mt-2 text-xl font-semibold text-white">{formatNumber(actionWorkflow?.pendingCount ?? 0)}</div></div>
            <div className="rounded-2xl border border-[#243040] bg-[#10141b] p-3"><div className="text-xs uppercase tracking-[0.18em] text-[#9ff0c7]">Approved</div><div className="mt-2 text-xl font-semibold text-white">{formatNumber(actionWorkflow?.approvedCount ?? 0)}</div></div>
            <div className="rounded-2xl border border-[#243040] bg-[#10141b] p-3"><div className="text-xs uppercase tracking-[0.18em] text-[#dbe6fb]">Deferred</div><div className="mt-2 text-xl font-semibold text-white">{formatNumber(actionWorkflow?.deferredCount ?? 0)}</div></div>
            <div className="rounded-2xl border border-[#243040] bg-[#10141b] p-3"><div className="text-xs uppercase tracking-[0.18em] text-[#ffbdcb]">Closed</div><div className="mt-2 text-xl font-semibold text-white">{formatNumber(actionWorkflow?.closedCount ?? 0)}</div></div>
          </div>

          <div className="mt-5 space-y-3">
            {operationsLoading && workflowItems.length === 0 ? <div className="rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-8 text-center text-sm text-[#8797b4]">Ucitavam workflow predloge...</div> : workflowItems.length === 0 ? <div className="rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-8 text-center text-sm text-[#8797b4]">Za trenutne filtere nema otvorenih predloga akcije.</div> : workflowItems.map((item) => (
              <div key={item.suggestionKey} className="rounded-2xl border border-[#243040] bg-[#10141b] p-4">
                <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className={`inline-flex rounded-full border px-2.5 py-1 text-[11px] font-semibold ${getActionTypeTone(item.actionType)}`}>
                        {item.actionType === "dopuna" ? <Truck size={12} /> : item.actionType === "transfer" ? <GitCompareArrows size={12} /> : item.actionType === "markdown" ? <Tag size={12} /> : <Archive size={12} />}
                        <span className="ml-1 capitalize">{item.actionType}</span>
                      </span>
                      <span className={`inline-flex rounded-full border px-2.5 py-1 text-[11px] font-semibold ${getActionStatusTone(item.status)}`}>{item.status}</span>
                      <span className={`text-xs font-semibold uppercase tracking-[0.18em] ${getPriorityTone(item.priority)}`}>{item.priority}</span>
                    </div>
                    <div className="mt-3 text-sm font-semibold text-white">{item.label}</div>
                    <div className="mt-1 text-sm leading-6 text-[#90a0ba]">{item.reason}</div>
                    <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-[#7f8fa9]">
                      <span>Artikal: {item.naziv}</span>
                      {item.fromStoreName ? <span>Iz: {item.fromStoreName}</span> : null}
                      {item.toStoreName ? <span>U: {item.toStoreName}</span> : null}
                      <span>Qty: {formatNumber(item.suggestedQty)}</span>
                      <span>Vrednost: {formatCurrency(item.estimatedValue)}</span>
                    </div>
                    {item.note ? <div className="mt-2 text-xs text-[#dbe6fb]">Napomena: {item.note}</div> : null}
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <button type="button" onClick={() => void updateWorkflowStatus(item, "approved")} disabled={workflowBusyKey === item.suggestionKey} className="inline-flex items-center gap-2 rounded-xl border border-[#28574d] bg-[#102b24] px-3 py-2 text-xs font-semibold text-[#9ff0c7] disabled:cursor-not-allowed disabled:opacity-60"><CheckCircle2 size={14} />Odobri</button>
                    <button type="button" onClick={() => void updateWorkflowStatus(item, "deferred")} disabled={workflowBusyKey === item.suggestionKey} className="inline-flex items-center gap-2 rounded-xl border border-[#33405a] bg-[#182131] px-3 py-2 text-xs font-semibold text-[#dbe6fb] disabled:cursor-not-allowed disabled:opacity-60"><Clock3 size={14} />Odlozi</button>
                    <button type="button" onClick={() => void updateWorkflowStatus(item, "closed")} disabled={workflowBusyKey === item.suggestionKey} className="inline-flex items-center gap-2 rounded-xl border border-[#6b2c38] bg-[#281319] px-3 py-2 text-xs font-semibold text-[#ffc3cf] disabled:cursor-not-allowed disabled:opacity-60"><XCircle size={14} />Zatvori</button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="grid gap-5 xl:grid-cols-[1.05fr_0.95fr]">
        <div className="rounded-[28px] border border-[#232935] bg-[#12161f] p-5">
          <div className="flex flex-col gap-2 md:flex-row md:items-end md:justify-between">
            <div>
              <h2 className="text-lg font-semibold text-white">Aging i obrt fonda robe</h2>
              <p className="text-sm text-[#90a0ba]">Dani bez kretanja su racunati po poslednjem movement-u, uz fallback na poslednje azuriranje artikla.</p>
            </div>
            <div className="rounded-full border border-[#33405a] bg-[#182131] px-3 py-1 text-xs font-semibold text-[#dbe6fb]">
              {insightsLoading ? "Ucitavanje aging analitike..." : `${formatNumber(staleBucket?.itemCount ?? 0)} artikala je u 90+ dana`}
            </div>
          </div>

          <div className="mt-5 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
            {agingBuckets.length === 0 ? (
              <div className="col-span-full rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-8 text-center text-sm text-[#8797b4]">Aging analitika nije dostupna za trenutne filtere.</div>
            ) : agingBuckets.map((bucket) => (
              <article key={bucket.bucketKey} className="rounded-2xl border border-[#243040] bg-[#10141b] p-4">
                <div className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${getAgingTone(bucket.bucketKey)}`}>{bucket.label}</div>
                <div className="mt-4 text-2xl font-semibold text-white">{formatNumber(bucket.itemCount)}</div>
                <div className="mt-2 text-sm text-[#95a7c1]">{formatNumber(bucket.totalUnits)} komada | {formatCurrency(bucket.estimatedValue)}</div>
              </article>
            ))}
          </div>

          <div className="mt-5 rounded-2xl border border-[#243040] bg-[#10141b] p-4">
            <div className="flex items-center gap-2 text-sm font-semibold text-white">
              <Clock3 size={16} className="text-[#8edbff]" />
              Najstariji artikli u filtriranom skupu
            </div>
            <div className="mt-3 space-y-3">
              {agedItems.length === 0 ? <div className="text-sm text-[#8797b4]">Nema artikala za aging ranking.</div> : agedItems.map((item) => (
                <button key={`aged-${item.id}`} type="button" onClick={() => setDetailRow(rows.find((row) => row.id === item.id) ?? buildRowFromInsightItem(item, stores, suppliers))} className="flex w-full items-center justify-between rounded-xl border border-[#283142] bg-[#141b26] px-3 py-3 text-left transition hover:border-[#3e4a61]">
                  <div className="min-w-0">
                    <div className="truncate text-sm font-semibold text-white">{item.naziv}</div>
                    <div className="truncate text-xs text-[#94a3bd]">{item.plu ?? "Bez PLU"} | {item.supplierName ?? "Nerasporedjen dobavljac"}</div>
                  </div>
                  <div className="text-right">
                    <div className="text-sm font-semibold text-[#ffd3db]">{formatNumber(item.daysSinceMovement)} dana</div>
                    <div className={`mt-1 inline-flex rounded-full border px-2 py-0.5 text-[11px] font-semibold ${getAgingTone(item.agingBucket)}`}>{item.agingLabel}</div>
                  </div>
                </button>
              ))}
            </div>
          </div>
        </div>

        <div className="rounded-[28px] border border-[#232935] bg-[#12161f] p-5">
          <div className="flex flex-col gap-2 md:flex-row md:items-end md:justify-between">
            <div>
              <h2 className="text-lg font-semibold text-white">ABC segmentacija kapitala</h2>
              <p className="text-sm text-[#90a0ba]">Klasa A predstavlja artikle koji nose najveci deo nabavne vrednosti filtrirane zalihe.</p>
            </div>
            <div className="rounded-full border border-[#4d3a57] bg-[#201326] px-3 py-1 text-xs font-semibold text-[#ffc8fb]">
              {insightsLoading ? "Ucitavanje ABC klase..." : `${formatNumber(classABucket?.itemCount ?? 0)} artikala u klasi A`}
            </div>
          </div>

          <div className="mt-5 grid gap-3 md:grid-cols-3">
            {abcBuckets.length === 0 ? (
              <div className="col-span-full rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-8 text-center text-sm text-[#8797b4]">ABC raspodela nije dostupna za trenutne filtere.</div>
            ) : abcBuckets.map((bucket) => (
              <article key={bucket.bucketKey} className="rounded-2xl border border-[#243040] bg-[#10141b] p-4">
                <div className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${getAbcTone(bucket.bucketKey)}`}>{bucket.label}</div>
                <div className="mt-4 text-2xl font-semibold text-white">{formatPercent(bucket.valueSharePct)}</div>
                <div className="mt-2 text-sm text-[#95a7c1]">{formatNumber(bucket.itemCount)} artikala | {formatCurrency(bucket.estimatedValue)}</div>
              </article>
            ))}
          </div>

          <div className="mt-5 rounded-2xl border border-[#243040] bg-[#10141b] p-4">
            <div className="text-sm font-semibold text-white">Kapital najvise vezan u ovim artiklima</div>
            <div className="mt-3 space-y-3">
              {capitalLockedItems.length === 0 ? <div className="text-sm text-[#8797b4]">Nema artikala za ABC ranking.</div> : capitalLockedItems.map((item) => (
                <button key={`capital-${item.id}`} type="button" onClick={() => setDetailRow(rows.find((row) => row.id === item.id) ?? buildRowFromInsightItem(item, stores, suppliers))} className="flex w-full items-center justify-between rounded-xl border border-[#283142] bg-[#141b26] px-3 py-3 text-left transition hover:border-[#3e4a61]">
                  <div className="min-w-0">
                    <div className="truncate text-sm font-semibold text-white">{item.naziv}</div>
                    <div className="truncate text-xs text-[#94a3bd]">{item.storeName ?? "Sve lokacije"} | {item.quantity} kom</div>
                  </div>
                  <div className="text-right">
                    <div className="text-sm font-semibold text-[#bde6ff]">{formatCurrency(item.estimatedValue)}</div>
                    <div className={`mt-1 inline-flex rounded-full border px-2 py-0.5 text-[11px] font-semibold ${getAbcTone(item.abcClass)}`}>Klasa {item.abcClass}</div>
                  </div>
                </button>
              ))}
            </div>
          </div>
        </div>
      </section>

      <section className="grid gap-5 xl:grid-cols-[1.15fr_0.85fr]">
        <div className="rounded-[28px] border border-[#232935] bg-[#12161f] p-5">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="text-lg font-semibold text-white">Rizik i prioriteti</h2>
              <p className="text-sm text-[#90a0ba]">Najrizicniji artikli i oni sa najvecom vezanom vrednoscu na trenutnoj strani.</p>
            </div>
            <span className="rounded-full border border-[#2d445e] bg-[#132031] px-3 py-1 text-xs font-semibold text-[#8edbff]">{rows.length} redova na ekranu</span>
          </div>

          <div className="mt-5 grid gap-4 lg:grid-cols-2">
            <div className="rounded-2xl border border-[#243040] bg-[#10141b] p-4">
              <h3 className="text-sm font-semibold text-white">Najveci rizici</h3>
              <div className="mt-3 space-y-3">
                {topRiskRows.length === 0 ? <div className="text-sm text-[#8797b4]">Nema rizicnih artikala na ovoj strani.</div> : topRiskRows.map((row) => (
                  <button key={`risk-${row.id}`} type="button" onClick={() => setDetailRow(row)} className="flex w-full items-center justify-between rounded-xl border border-[#283142] bg-[#141b26] px-3 py-3 text-left transition hover:border-[#3e4a61]">
                    <div className="min-w-0"><div className="truncate text-sm font-semibold text-white">{row.naziv}</div><div className="truncate text-xs text-[#94a3bd]">{row.plu ?? "Bez PLU"} | {row.supplierName}</div></div>
                    <div className="text-right"><div className="text-sm font-semibold text-[#ffd3db]">{row.quantity}</div><div className="text-xs text-[#f7b8c7]">{row.stockStateLabel}</div></div>
                  </button>
                ))}
              </div>
            </div>

            <div className="rounded-2xl border border-[#243040] bg-[#10141b] p-4">
              <h3 className="text-sm font-semibold text-white">Najveca vrednost</h3>
              <div className="mt-3 space-y-3">
                {highestValueRows.length === 0 ? <div className="text-sm text-[#8797b4]">Nema podataka za prikaz.</div> : highestValueRows.map((row) => (
                  <button key={`value-${row.id}`} type="button" onClick={() => setDetailRow(row)} className="flex w-full items-center justify-between rounded-xl border border-[#283142] bg-[#141b26] px-3 py-3 text-left transition hover:border-[#3e4a61]">
                    <div className="min-w-0"><div className="truncate text-sm font-semibold text-white">{row.naziv}</div><div className="truncate text-xs text-[#94a3bd]">{row.storeName}</div></div>
                    <div className="text-right"><div className="text-sm font-semibold text-[#bde6ff]">{formatCurrency(row.estimatedValueAmount)}</div><div className="text-xs text-[#94a3bd]">{row.quantity} kom</div></div>
                  </button>
                ))}
              </div>
            </div>
          </div>
        </div>

        <div className="rounded-[28px] border border-[#232935] bg-[#12161f] p-5">
          <div>
            <h2 className="text-lg font-semibold text-white">Vrednost po dobavljacu</h2>
            <p className="text-sm text-[#90a0ba]">Top dobavljaci po procenjenoj vrednosti u trenutnoj tabeli.</p>
          </div>
          <div className="mt-5 h-[320px]">{chartData.length === 0 ? <div className="flex h-full items-center justify-center rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] text-sm text-[#8797b4]">Nema dovoljno podataka za grafikon.</div> : <ResponsiveContainer width="100%" height="100%"><BarChart data={chartData} layout="vertical" margin={{ top: 10, right: 12, bottom: 10, left: 12 }}><CartesianGrid strokeDasharray="3 3" stroke="#233042" /><XAxis type="number" tick={{ fill: "#92a4bf", fontSize: 12 }} tickFormatter={(value) => `${Math.round(Number(value) / 1000)}k`} /><YAxis type="category" dataKey="supplierName" width={110} tick={{ fill: "#92a4bf", fontSize: 11 }} /><Tooltip cursor={{ fill: "rgba(68,208,255,0.08)" }} formatter={(value: number | string | undefined) => formatCurrency(typeof value === "number" ? value : Number(value ?? 0))} /><Bar dataKey="totalValue" fill="#44d0ff" radius={[0, 10, 10, 0]} /></BarChart></ResponsiveContainer>}</div>
          <div className="mt-5 rounded-2xl border border-[#243040] bg-[#10141b] p-4">
            <div className="grid gap-3 sm:grid-cols-3">
              <div className="rounded-2xl bg-[#131e2b] p-3"><div className="text-xs uppercase tracking-[0.2em] text-[#89d9ff]">Bez zaliha</div><div className="mt-2 text-xl font-semibold text-white">{balance ? formatNumber(balance.outOfStockCount) : "-"}</div></div>
              <div className="rounded-2xl bg-[#241b11] p-3"><div className="text-xs uppercase tracking-[0.2em] text-[#f0c36b]">Low stock share</div><div className="mt-2 text-xl font-semibold text-white">{formatPercent(lowStockShare)}</div></div>
              <div className="rounded-2xl bg-[#1d1726] p-3"><div className="text-xs uppercase tracking-[0.2em] text-[#c4a3ff]">Ukupno filtrirano</div><div className="mt-2 text-xl font-semibold text-white">{formatNumber(totalCount)}</div></div>
            </div>
          </div>
        </div>
      </section>

      {/* ── Inventory Alerts ──────────────────────────────────────── */}
      <section className="rounded-[28px] border border-[#232935] bg-[#12161f] p-5">
        <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
          <div className="flex items-center gap-3">
            <div className="rounded-2xl border border-[#7d2940] bg-[#411520] p-2.5 text-[#ffb4c2]">
              <AlertTriangle size={18} />
            </div>
            <div>
              <h2 className="text-lg font-semibold text-white">Inventory Alerts</h2>
              <p className="text-sm text-[#90a0ba]">AI-generisani kritični signali iz zalihe. Osvežava se automatski.</p>
            </div>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            {(["", "critical", "warning", "info"] as const).map((sev) => (
              <button key={sev || "all"} type="button" onClick={() => setAlertSeverityFilter(sev)} className={`rounded-full border px-3 py-1 text-xs font-semibold transition ${alertSeverityFilter === sev ? "border-[#44d0ff] bg-[#102231] text-[#44d0ff]" : "border-[#33405a] bg-[#182131] text-[#dbe6fb]"}`}>
                {sev === "" ? "Sve" : sev === "critical" ? "Kritično" : sev === "warning" ? "Upozorenje" : "Info"}
              </button>
            ))}
            <div className="rounded-full border border-[#33405a] bg-[#182131] px-3 py-1 text-xs font-semibold text-[#dbe6fb]">
              {alertsLoading ? "..." : `${alerts?.totalCount ?? 0} ukupno`}
            </div>
          </div>
        </div>

        {!alerts?.snapshotAvailable ? (
          <div className="mt-4 rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-8 text-center text-sm text-[#8797b4]">
            {alertsLoading ? "Učitavam alertove..." : "Alertovi nisu dostupni. Snapshot tabela je prazna ili nije pokrenuta analitika."}
            {alerts?.warning ? <div className="mt-2 text-xs text-[#ffd590]">{alerts.warning}</div> : null}
          </div>
        ) : (
          <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            {(alerts?.items ?? [])
              .filter((a) => !alertSeverityFilter || a.severity === alertSeverityFilter)
              .slice(0, ALERTS_DISPLAY_COUNT)
              .map((alert, idx) => (
                <article key={idx} className="rounded-2xl border border-[#243040] bg-[#10141b] p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div className={`inline-flex rounded-full border px-2.5 py-1 text-[11px] font-semibold ${getAlertSeverityTone(alert.severity)}`}>
                      {alert.severity === "critical" ? "Kritično" : alert.severity === "warning" ? "Upozorenje" : "Info"}
                    </div>
                    <div className="rounded-full border border-[#33405a] bg-[#182131] px-2 py-0.5 text-[11px] font-semibold text-[#dbe6fb]">
                      {Math.round(alert.confidenceScore * 100)}%
                    </div>
                  </div>
                  <div className="mt-3 text-sm font-semibold text-white">{alert.title}</div>
                  <div className="mt-1 text-xs leading-5 text-[#90a0ba]">{alert.message}</div>
                  <div className="mt-3 flex flex-wrap gap-x-3 gap-y-1 text-[11px] text-[#7f8fa9]">
                    <span>Tip: {alert.alertType}</span>
                    {alert.sizeCode ? <span>Vel: {alert.sizeCode}</span> : null}
                    <button type="button" onClick={() => setSizeCurveSkuId((prev) => prev === alert.skuId ? null : alert.skuId)} className="text-[#44d0ff] transition hover:text-[#6de0ff]">
                      Size curve →
                    </button>
                  </div>
                </article>
              ))}
            {(alerts?.items ?? []).filter((a) => !alertSeverityFilter || a.severity === alertSeverityFilter).length === 0 ? (
              <div className="col-span-full rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-8 text-center text-sm text-[#8797b4]">Nema alertova za izabrani filter.</div>
            ) : null}
          </div>
        )}
      </section>

      {/* ── Demand Forecast ───────────────────────────────────────── */}
      <section className="rounded-[28px] border border-[#232935] bg-[#12161f] p-5">
        <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
          <div className="flex items-center gap-3">
            <div className="rounded-2xl border border-[#30516d] bg-[#102231] p-2.5 text-[#8edbff]">
              <TrendingDown size={18} />
            </div>
            <div>
              <h2 className="text-lg font-semibold text-white">Demand Forecast & Out-of-Stock Risk</h2>
              <p className="text-sm text-[#90a0ba]">Prognoza potražnje po SKU i veličini. Rizik OOS u 7 dana i overstock signali.</p>
            </div>
          </div>
          <div className="rounded-full border border-[#33405a] bg-[#182131] px-3 py-1 text-xs font-semibold text-[#dbe6fb]">
            {forecastLoading ? "Učitavam..." : `${forecast?.totalCount ?? 0} SKU u prognozi`}
          </div>
        </div>

        {!forecast?.snapshotAvailable ? (
          <div className="mt-4 rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-8 text-center text-sm text-[#8797b4]">
            {forecastLoading ? "Učitavam forecast..." : forecastError ?? "Forecast nije dostupan. Snapshot tabela je prazna."}
            {forecast?.warning ? <div className="mt-2 text-xs text-[#ffd590]">{forecast.warning}</div> : null}
          </div>
        ) : (
          <div className="mt-4 grid gap-5 xl:grid-cols-2">
            <div className="rounded-2xl border border-[#243040] bg-[#10141b] p-4">
              <h3 className="flex items-center gap-2 text-sm font-semibold text-white">
                <TrendingDown size={14} className="text-[#ffb4c2]" />
                Najveći OOS rizik u 7 dana
              </h3>
              <div className="mt-3 space-y-2">
                {(forecast?.items ?? [])
                  .filter((f) => f.probabilityOfOOSIn7d > OOS_RISK_THRESHOLD)
                  .sort((a, b) => b.probabilityOfOOSIn7d - a.probabilityOfOOSIn7d)
                  .slice(0, FORECAST_OOS_DISPLAY)
                  .map((f, idx) => {
                    const name = rows.find((r) => r.id === f.skuId)?.naziv ?? `SKU #${f.skuId}`;
                    const store = stores.find((s) => s.storeId === f.storeId)?.storeName ?? `Objekat #${f.storeId}`;
                    return (
                      <div key={idx} className="flex items-start justify-between gap-3 rounded-xl border border-[#283142] bg-[#141b26] px-3 py-2">
                        <div className="min-w-0">
                          <div className="truncate text-sm font-semibold text-white">{name}</div>
                          <div className="truncate text-xs text-[#90a0ba]">{store} | vel. {f.sizeCode}</div>
                          <div className="mt-1 text-xs text-[#8797b4]">{f.explanation}</div>
                        </div>
                        <div className="shrink-0 text-right">
                          <div className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${f.probabilityOfOOSIn7d > 0.7 ? "border-[#7d2940] bg-[#411520] text-[#ffb4c2]" : f.probabilityOfOOSIn7d > 0.4 ? "border-[#7c5822] bg-[#412d11] text-[#ffd590]" : "border-[#33405a] bg-[#182131] text-[#dbe6fb]"}`}>
                            {Math.round(f.probabilityOfOOSIn7d * 100)}% OOS
                          </div>
                          <div className="mt-1 text-xs text-[#7f8fa9]">7d: {f.forecast7d.toFixed(1)}</div>
                        </div>
                      </div>
                    );
                  })}
                {(forecast?.items ?? []).filter((f) => f.probabilityOfOOSIn7d > OOS_RISK_THRESHOLD).length === 0 ? (
                  <div className="rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-6 text-center text-sm text-[#8797b4]">Nema visokog OOS rizika za trenutne filtere.</div>
                ) : null}
              </div>
            </div>

            <div className="rounded-2xl border border-[#243040] bg-[#10141b] p-4">
              <h3 className="flex items-center gap-2 text-sm font-semibold text-white">
                <TrendingUp size={14} className="text-[#9ff0c7]" />
                Overstock rizik (28 dana)
              </h3>
              <div className="mt-3 space-y-2">
                {(forecast?.items ?? [])
                  .filter((f) => f.overstockRisk > OVERSTOCK_RISK_THRESHOLD)
                  .sort((a, b) => b.overstockRisk - a.overstockRisk)
                  .slice(0, FORECAST_OVERSTOCK_DISPLAY)
                  .map((f, idx) => {
                    const name = rows.find((r) => r.id === f.skuId)?.naziv ?? `SKU #${f.skuId}`;
                    const store = stores.find((s) => s.storeId === f.storeId)?.storeName ?? `Objekat #${f.storeId}`;
                    return (
                      <div key={idx} className="flex items-start justify-between gap-3 rounded-xl border border-[#283142] bg-[#141b26] px-3 py-2">
                        <div className="min-w-0">
                          <div className="truncate text-sm font-semibold text-white">{name}</div>
                          <div className="truncate text-xs text-[#90a0ba]">{store} | vel. {f.sizeCode}</div>
                        </div>
                        <div className="shrink-0 text-right">
                          <div className="inline-flex rounded-full border border-[#36543f] bg-[#17261d] px-2.5 py-1 text-xs font-semibold text-[#aef3bf]">
                            {Math.round(f.overstockRisk * 100)}% over
                          </div>
                          <div className="mt-1 text-xs text-[#7f8fa9]">28d: {f.forecast28d.toFixed(1)}</div>
                        </div>
                      </div>
                    );
                  })}
                {(forecast?.items ?? []).filter((f) => f.overstockRisk > OVERSTOCK_RISK_THRESHOLD).length === 0 ? (
                  <div className="rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-6 text-center text-sm text-[#8797b4]">Nema overstock signala za trenutne filtere.</div>
                ) : null}
              </div>
            </div>
          </div>
        )}
      </section>

      {/* ── Size Curve Intelligence ───────────────────────────────── */}
      <section className="rounded-[28px] border border-[#232935] bg-[#12161f] p-5">
        <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
          <div>
            <h2 className="text-lg font-semibold text-white">Size Curve Intelligence</h2>
            <p className="text-sm text-[#90a0ba]">Upiši ID artikla da vidiš distribuciju veličina naspram idealnog kurva. Detektuje broken-run, dead size i core size.</p>
          </div>
          <div className="flex items-center gap-2">
            <label className="flex items-center gap-2 rounded-2xl border border-[#283042] bg-[#10141c] px-3 py-2">
              <Search size={14} className="shrink-0 text-[#7ec6ff]" />
              <input
                type="number"
                placeholder="ArtikelID"
                value={sizeCurveSkuId ?? ""}
                onChange={(e) => setSizeCurveSkuId(e.target.value ? Number(e.target.value) : null)}
                className="w-28 bg-transparent text-sm text-white outline-none placeholder:text-[#73809a]"
              />
            </label>
            {sizeCurveSkuId != null && (
              <button type="button" onClick={() => setSizeCurveSkuId(null)} className="rounded-xl border border-[#33405a] bg-[#182131] px-3 py-2 text-xs font-semibold text-[#dbe6fb]">
                Poništi
              </button>
            )}
          </div>
        </div>

        {sizeCurveSkuId == null ? (
          <div className="mt-4 rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-8 text-center text-sm text-[#8797b4]">
            Upiši ID artikla u polje iznad da prikažeš size curve analizu.
          </div>
        ) : sizeCurveLoading ? (
          <div className="mt-4 rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-8 text-center text-sm text-[#8797b4]">Učitavam size curve za SKU #{sizeCurveSkuId}...</div>
        ) : !sizeCurve?.snapshotAvailable || (sizeCurve?.items ?? []).length === 0 ? (
          <div className="mt-4 rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-8 text-center text-sm text-[#8797b4]">
            Nema size curve podataka za SKU #{sizeCurveSkuId}.
          </div>
        ) : (
          <div className="mt-4">
            <div className="mb-3 flex flex-wrap gap-2">
              {sizeCurve.items.filter((p) => p.isCoreSizeMissing).length > 0 && (
                <span className="inline-flex rounded-full border border-[#7d2940] bg-[#411520] px-2.5 py-1 text-xs font-semibold text-[#ffb4c2]">
                  {sizeCurve.items.filter((p) => p.isCoreSizeMissing).length} core size nedostaju
                </span>
              )}
              {sizeCurve.items.filter((p) => p.isDeadSize).length > 0 && (
                <span className="inline-flex rounded-full border border-[#7c5822] bg-[#412d11] px-2.5 py-1 text-xs font-semibold text-[#ffd590]">
                  {sizeCurve.items.filter((p) => p.isDeadSize).length} mrtve veličine
                </span>
              )}
              {sizeCurve.items.some((p) => p.brokenRun) && (
                <span className="inline-flex rounded-full border border-[#30516d] bg-[#102231] px-2.5 py-1 text-xs font-semibold text-[#8edbff]">
                  Broken run detektovan
                </span>
              )}
            </div>
            <div className="h-[240px]">
              <ResponsiveContainer width="100%" height="100%">
                <ComposedChart data={sizeCurve.items.map((p) => ({ name: p.sizeCode, actual: +(p.actualSizeShare * 100).toFixed(1), ideal: +(p.idealSizeShare * 100).toFixed(1) }))}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#233042" />
                  <XAxis dataKey="name" tick={{ fill: "#92a4bf", fontSize: 12 }} />
                  <YAxis tick={{ fill: "#92a4bf", fontSize: 12 }} unit="%" />
                  <Tooltip formatter={(val: number | string | undefined) => `${val ?? 0}%`} contentStyle={{ background: "#141c29", border: "1px solid #2b3a50", color: "#dde7f7" }} />
                  <Bar dataKey="actual" fill="#44d0ff" radius={[6, 6, 0, 0]} name="Stvarno" />
                  <Line type="monotone" dataKey="ideal" stroke="#ffd590" strokeWidth={2} dot={false} name="Idealno" />
                  <ReferenceLine y={0} stroke="#334055" />
                </ComposedChart>
              </ResponsiveContainer>
            </div>
            <div className="mt-4 grid gap-2 sm:grid-cols-2 xl:grid-cols-4">
              {sizeCurve.items.slice(0, 8).map((p) => (
                <div key={p.sizeCode} className={`rounded-2xl border p-3 ${p.isCoreSizeMissing ? "border-[#7d2940] bg-[#411520]" : p.isDeadSize ? "border-[#7c5822] bg-[#412d11]" : "border-[#243040] bg-[#10141b]"}`}>
                  <div className="flex items-center justify-between">
                    <span className="text-sm font-semibold text-white">vel. {p.sizeCode}</span>
                    <span className="text-xs text-[#8797b4]">{(p.deviationPct * 100).toFixed(0)}pp</span>
                  </div>
                  <div className="mt-2 grid grid-cols-2 gap-2 text-xs">
                    <div><div className="text-[#89d9ff]">Stvarno</div><div className="font-semibold text-white">{(p.actualSizeShare * 100).toFixed(1)}%</div></div>
                    <div><div className="text-[#ffd590]">Idealno</div><div className="font-semibold text-white">{(p.idealSizeShare * 100).toFixed(1)}%</div></div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}
      </section>

      {/* ── Smart Rebalancing ─────────────────────────────────────── */}
      <section className="rounded-[28px] border border-[#232935] bg-[#12161f] p-5">
        <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
          <div className="flex items-center gap-3">
            <div className="rounded-2xl border border-[#28574d] bg-[#102b24] p-2.5 text-[#9ff0c7]">
              <ArrowRightLeft size={18} />
            </div>
            <div>
              <h2 className="text-lg font-semibold text-white">Smart Rebalancing</h2>
              <p className="text-sm text-[#90a0ba]">Predlozi za redistribuciju robe između lokacija. Sortirano po urgentnosti i očekivanim uštedama.</p>
            </div>
          </div>
          <div className="rounded-full border border-[#33405a] bg-[#182131] px-3 py-1 text-xs font-semibold text-[#dbe6fb]">
            {rebalanceLoading ? "Učitavam..." : `${rebalance?.totalCount ?? 0} predloga`}
          </div>
        </div>

        {!rebalance?.snapshotAvailable ? (
          <div className="mt-4 rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-8 text-center text-sm text-[#8797b4]">
            {rebalanceLoading ? "Učitavam predloge za redistribuciju..." : "Redistribucija nije dostupna. Snapshot tabela je prazna."}
          </div>
        ) : (rebalance?.items ?? []).length === 0 ? (
          <div className="mt-4 rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-8 text-center text-sm text-[#8797b4]">
            Nema preporučenih redistribucija za trenutne filtere.
          </div>
        ) : (
          <div className="mt-4 overflow-hidden rounded-2xl border border-[#242d3b]">
            <div className="overflow-x-auto">
              <table className="min-w-full text-sm">
                <thead className="bg-[#0f131a] text-left text-[#90a0ba]">
                  <tr>
                    <th className="px-4 py-3">Urgentnost</th>
                    <th className="px-4 py-3">Iz</th>
                    <th className="px-4 py-3">U</th>
                    <th className="px-4 py-3">SKU</th>
                    <th className="px-4 py-3">Vel.</th>
                    <th className="px-4 py-3 text-right">Qty</th>
                    <th className="px-4 py-3 text-right">Sačuvana prodaja</th>
                    <th className="px-4 py-3">Razlog</th>
                  </tr>
                </thead>
                <tbody>
                  {rebalance.items.slice(0, REBALANCE_DISPLAY_COUNT).map((item, idx) => {
                    const name = rows.find((r) => r.id === item.skuId)?.naziv ?? `SKU #${item.skuId}`;
                    const fromStore = stores.find((s) => s.storeId === item.fromStoreId)?.storeName ?? `#${item.fromStoreId}`;
                    const toStore = stores.find((s) => s.storeId === item.toStoreId)?.storeName ?? `#${item.toStoreId}`;
                    return (
                      <tr key={idx} className="border-t border-[#1c2230] bg-[#11161d] text-[#dbe6fb] hover:bg-[#151c26]">
                        <td className="px-4 py-3">
                          <span className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${getRebalanceUrgencyTone(item.urgency)}`}>
                            {item.urgency === "urgent" ? "Hitno" : item.urgency === "recommended" ? "Preporučeno" : "Opciono"}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-[#c7d4e8]">{fromStore}</td>
                        <td className="px-4 py-3 text-[#c7d4e8]">{toStore}</td>
                        <td className="px-4 py-3 font-semibold text-white">{name}</td>
                        <td className="px-4 py-3 text-[#c7d4e8]">{item.sizeCode}</td>
                        <td className="px-4 py-3 text-right font-semibold text-white">{item.recommendedQty}</td>
                        <td className="px-4 py-3 text-right text-[#9ff0c7]">{formatCurrency(item.expectedSavedSales)}</td>
                        <td className="max-w-[220px] truncate px-4 py-3 text-[#90a0ba]">{item.reason}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        )}
      </section>

      <section className="rounded-[28px] border border-[#232935] bg-[#12161f] p-5">
        <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
          <div>
            <h2 className="text-lg font-semibold text-white">Tabela artikala</h2>
            <p className="text-sm text-[#90a0ba]">Klik na red otvara detalj sa preporukom akcije i operativnim kontekstom.</p>
          </div>
          <div className="text-sm text-[#96a5bf]">Prikazano <span className="font-semibold text-white">{rows.length}</span> od <span className="font-semibold text-white">{formatNumber(totalCount)}</span> artikala</div>
        </div>

        <div className="mt-4 overflow-hidden rounded-2xl border border-[#242d3b]">
          <div className="overflow-x-auto">
            <table className="min-w-full text-sm">
              <thead className="bg-[#0f131a] text-left text-[#90a0ba]">
                <tr>
                  <th className="px-4 py-3">Artikal</th>
                  <th className="px-4 py-3">Status</th>
                  <th className="px-4 py-3 text-right">Kolicina</th>
                  <th className="px-4 py-3 text-right">Minimum</th>
                  <th className="px-4 py-3 text-right">Gap</th>
                  <th className="px-4 py-3 text-right">Nabavna</th>
                  <th className="px-4 py-3 text-right">Vrednost</th>
                  <th className="px-4 py-3">Prodavnica</th>
                  <th className="px-4 py-3">Dobavljac</th>
                </tr>
              </thead>
              <tbody>
                {loading ? (
                  <tr><td colSpan={9} className="px-4 py-10 text-center text-[#8797b4]">Ucitavam tabelu...</td></tr>
                ) : rows.length === 0 ? (
                  <tr><td colSpan={9} className="px-4 py-10 text-center text-[#8797b4]">Nema artikala za zadate filtere.</td></tr>
                ) : rows.map((row) => {
                  const stock = getStockState(row.quantity, row.minimum);
                  return (
                    <tr key={row.id} className="cursor-pointer border-t border-[#1c2230] bg-[#11161d] text-[#dbe6fb] transition hover:bg-[#151c26]" onClick={() => setDetailRow(row)}>
                      <td className="px-4 py-3"><div className="flex flex-col"><span className="font-semibold text-white">{row.naziv}</span><span className="text-xs text-[#8fa1be]">{row.plu ?? "Bez PLU"} | {getCoverageText(row)}</span></div></td>
                      <td className="px-4 py-3"><span className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${stock.badge}`}>{row.stockStateLabel}</span></td>
                      <td className="px-4 py-3 text-right font-semibold text-white">{formatNumber(row.quantity)}</td>
                      <td className="px-4 py-3 text-right text-[#c7d4e8]">{formatNumber(row.minimum)}</td>
                      <td className="px-4 py-3 text-right text-[#f7c983]">{formatNumber(row.reorderGap)}</td>
                      <td className="px-4 py-3 text-right text-[#b8d7f0]">{formatCurrency(row.unitCost)}</td>
                      <td className="px-4 py-3 text-right font-semibold text-[#9fe0ff]">{formatCurrency(row.estimatedValueAmount)}</td>
                      <td className="px-4 py-3 text-[#c7d4e8]">{row.storeName}</td>
                      <td className="px-4 py-3 text-[#c7d4e8]">{row.supplierName}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>

        <div className="mt-4 flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div className="text-sm text-[#90a0ba]">Strana <span className="font-semibold text-white">{pageNumber}</span> od <span className="font-semibold text-white">{totalPages}</span></div>
          <div className="flex items-center gap-2">
            <button type="button" onClick={() => setPageNumber((current) => Math.max(1, current - 1))} disabled={pageNumber <= 1 || loading} className="rounded-xl border border-[#33405a] bg-[#182131] px-3 py-2 text-xs font-semibold text-[#dbe6fb] disabled:cursor-not-allowed disabled:opacity-50">Prethodna</button>
            <button type="button" onClick={() => setPageNumber((current) => Math.min(totalPages, current + 1))} disabled={pageNumber >= totalPages || loading} className="rounded-xl border border-[#33405a] bg-[#182131] px-3 py-2 text-xs font-semibold text-[#dbe6fb] disabled:cursor-not-allowed disabled:opacity-50">Sledeca</button>
          </div>
        </div>
      </section>

      <Modal isOpen={detailRow != null} onClose={() => setDetailRow(null)} title={detailRow ? `Detalj artikla: ${detailRow.naziv}` : "Detalj artikla"} size="lg">
        {detailRow ? (
          <div className="space-y-5 text-[#0f172a]">
            <div className={`rounded-2xl border border-[#e2e8f0] bg-gradient-to-br ${getStockState(detailRow.quantity, detailRow.minimum).panel} p-5 text-white`}>
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <div className="text-xs uppercase tracking-[0.22em] text-white/70">Status artikla</div>
                  <div className="mt-2 text-2xl font-semibold">{detailRow.stockStateLabel}</div>
                  <div className="mt-2 text-sm text-white/80">{getRecommendation(detailRow)}</div>
                  <div className="mt-3 flex flex-wrap gap-2">
                    {detailData?.abcClass ? <span className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${getAbcTone(detailData.abcClass)}`}>ABC {detailData.abcClass}</span> : null}
                    {detailData?.agingLabel ? <span className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${getAgingTone(detailData.agingBucket)}`}>{detailData.agingLabel}</span> : null}
                  </div>
                </div>
                <div className="rounded-2xl border border-white/15 bg-white/10 px-4 py-3 text-right">
                  <div className="text-xs uppercase tracking-[0.2em] text-white/70">Procena vrednosti</div>
                  <div className="mt-2 text-xl font-semibold">{formatCurrency(detailRow.estimatedValueAmount)}</div>
                  <div className="mt-2 text-xs text-white/75">
                    {detailData ? `${formatNumber(detailData.daysSinceMovement)} dana bez kretanja` : "Ucitavam aging detalj..."}
                  </div>
                </div>
              </div>
            </div>

            {detailLoading ? <div className="rounded-2xl border border-[#dbe4f0] bg-[#f8fafc] px-4 py-3 text-sm text-[#475569]">Ucitavam istoriju kretanja i dodatne detalje artikla...</div> : null}
            {detailError ? <div className="rounded-2xl border border-[#fecdd3] bg-[#fff1f2] px-4 py-3 text-sm text-[#9f1239]">{detailError}</div> : null}

            <div className="grid gap-4 md:grid-cols-2">
              {[
                ["PLU", detailRow.plu ?? "Nije dodeljen"],
                ["Prodavnica", detailData?.storeName ?? detailRow.storeName],
                ["Dobavljac", detailData?.supplierName ?? detailRow.supplierName],
                ["Kolicina", formatNumber(detailRow.quantity)],
                ["Minimalna kolicina", formatNumber(detailRow.minimum)],
                ["Gap do minimuma", formatNumber(detailRow.reorderGap)],
                ["Nabavna cena", formatCurrency(detailRow.unitCost)],
                ["Pokrice minimuma", detailRow.coverageRatio == null ? "Bez minimuma" : `${detailRow.coverageRatio.toFixed(2)}x`],
                ["Poslednje kretanje", formatDateTime(detailData?.lastMovementAt)],
                ["Dana bez kretanja", detailData ? formatNumber(detailData.daysSinceMovement) : "Ucitavanje..."],
                ["Kretanja u 30 dana", detailData ? formatNumber(detailData.movementCount30d) : "Ucitavanje..."],
                ["Kategorija", detailData?.kategorija ?? "Nije upisano"],
                ["Pol", detailData?.pol ?? "Nije upisano"],
                ["Materijal", detailData?.materijal ?? "Nije upisano"],
                ["Poslednje azuriranje", formatDateTime(detailData?.updatedAt)],
              ].map(([label, value]) => <div key={label} className="rounded-2xl border border-[#e2e8f0] bg-[#f8fafc] p-4"><div className="text-xs uppercase tracking-[0.18em] text-[#64748b]">{label}</div><div className="mt-2 text-base font-semibold text-[#0f172a]">{value}</div></div>)}
            </div>

            <div className="rounded-2xl border border-[#e2e8f0] bg-[#f8fafc] p-4">
              <div className="text-xs uppercase tracking-[0.18em] text-[#64748b]">Predlog akcije</div>
              <ul className="mt-3 list-disc space-y-2 pl-5 text-sm leading-6 text-[#1e293b]">
                <li>{getRecommendation(detailRow)}</li>
                <li>{detailRow.stockState === "critical" ? "Proveriti da li postoji zamenski artikal ili redistribucija iz druge lokacije." : detailRow.stockState === "warning" ? "Dopunu povezati sa sledecom nabavkom dobavljaca i prioritet dati artiklima sa najvecom traznjom." : "Ako je prodaja sporija od plana, razmotriti akcijsku cenu ili preraspodelu izmedju lokacija."}</li>
                <li>{detailData?.abcClass === "A" ? "Klasa A: proveri da li je vezani kapital u skladu sa planom prodaje i sezonom." : detailData?.abcClass === "C" ? "Klasa C: artikli nose manji deo kapitala, ali aging lako postaje signal za ciscenje zalihe." : "Klasa B: balansirati dopunu i obrt bez prevelikog vezivanja kapitala."}</li>
                <li>Za deljenje sa timom koristi PDF ili Excel filtrirani izvoz iz vrha stranice.</li>
              </ul>
            </div>

            <div className="rounded-2xl border border-[#e2e8f0] bg-[#f8fafc] p-4">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <div className="text-xs uppercase tracking-[0.18em] text-[#64748b]">Istorija kretanja</div>
                  <div className="mt-1 text-sm text-[#475569]">Poslednjih 12 promjena za izabrani artikal, sa dokumentom i poreklom podatka.</div>
                </div>
                <div className="text-xs font-semibold uppercase tracking-[0.18em] text-[#64748b]">{detailData?.history.length ?? 0} stavki</div>
              </div>

              <div className="mt-4 space-y-3">
                {detailData?.history?.length ? detailData.history.map((entry) => (
                  <div key={entry.movementId} className="rounded-2xl border border-[#dbe4f0] bg-white p-4">
                    <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                      <div className="min-w-0">
                        <div className="flex flex-wrap items-center gap-2">
                          <span className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${getAgingTone((entry.kolicina ?? 0) > 0 ? "0-30" : "90+")}`}>{getHistoryDirection(entry.kolicina)}</span>
                          <span className="text-sm font-semibold text-[#0f172a]">{entry.tipPromene}</span>
                          {entry.dataOrigin ? <span className="rounded-full border border-[#cbd5e1] bg-[#f8fafc] px-2 py-0.5 text-[11px] font-semibold text-[#475569]">{entry.dataOrigin}</span> : null}
                        </div>
                        <div className="mt-2 text-sm text-[#475569]">{formatDateTime(entry.datum)}</div>
                        <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-[#64748b]">
                          <span>Dokument: {entry.brojDokumenta ?? "Nije upisan"}</span>
                          <span>Korisnik: {entry.korisnikIme ?? "Nepoznato"}</span>
                          <span>Prodavnica: {entry.storeName ?? detailData.storeName ?? "Nije vezano"}</span>
                          <span>Dobavljac: {entry.supplierName ?? detailData.supplierName ?? "Nije vezano"}</span>
                        </div>
                      </div>
                      <div className="rounded-2xl border border-[#dbe4f0] bg-[#f8fafc] px-4 py-3 text-right">
                        <div className="text-xs uppercase tracking-[0.18em] text-[#64748b]">Kolicina / iznos</div>
                        <div className="mt-2 text-sm font-semibold text-[#0f172a]">{entry.kolicina == null ? "N/A" : formatNumber(entry.kolicina)}</div>
                        <div className="text-xs text-[#64748b]">{formatCurrency(entry.iznos)}</div>
                      </div>
                    </div>
                    {entry.komentar || entry.staraCena != null || entry.novaCena != null ? (
                      <div className="mt-3 border-t border-[#e2e8f0] pt-3 text-xs text-[#64748b]">
                        {entry.komentar ? <div>Komentar: {entry.komentar}</div> : null}
                        {entry.staraCena != null || entry.novaCena != null ? <div>Cena: {entry.staraCena != null ? formatCurrency(entry.staraCena) : "-"} → {entry.novaCena != null ? formatCurrency(entry.novaCena) : "-"}</div> : null}
                      </div>
                    ) : null}
                  </div>
                )) : <div className="rounded-2xl border border-dashed border-[#cbd5e1] bg-white px-4 py-8 text-center text-sm text-[#64748b]">Za ovaj artikal nema evidentiranih istorijskih kretanja.</div>}
              </div>
            </div>
          </div>
        ) : null}
      </Modal>
    </div>
  );
}

import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { useSearchParams } from "react-router-dom";
import {
  Bar,
  BarChart,
  CartesianGrid,
  ComposedChart,
  Legend,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import InfoTip from "../components/ui/InfoTip";
import { savePrintPayload } from "../services/analyticsTableState";
import { getStores } from "../services/analyticsApi";
import {
  getDailySalesStats,
  type DailySalesRow,
  type DailySalesTableResponse,
} from "../services/dailySalesStatsApi";
import type { StoreOption } from "../types/analytics";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import { getDataScope } from "../utils/dataScope";
import UltraSpinner from "../components/ui/UltraSpinner";
import { CHART_TOOLTIP_LABEL_STYLE, CHART_TOOLTIP_STYLE } from "../utils/chartTooltipStyle";
import { fmtPct, fmtRsd, fmtRsdShort, fmtSignedPct, getPresetRange } from "../utils/analyticsFormatters";
import "./DailySalesStatsPage.css";

type PeriodPreset = "30d" | "90d" | "180d" | "365d" | "custom";
type SortDir = "asc" | "desc";
type SortKey =
  | "date"
  | "firstShiftTotalItems"
  | "secondShiftTotalItems"
  | "totalRevenue"
  | "othersCount"
  | "totalItemsSold"
  | `supplier:${number}`;

type ActiveFilters = {
  fromDate: string;
  toDate: string;
  storeId: number | null;
  topN: number;
};

type PeriodSummary = {
  totalRevenue: number;
  totalVisibleItems: number;
  totalItemsInRange: number;
  totalDays: number;
  avgRevenuePerDay: number;
  avgItemsPerDay: number;
  avgRevenuePerItem: number;
  firstShiftItems: number;
  secondShiftItems: number;
  firstShiftSharePct: number;
  secondShiftSharePct: number;
  offShiftItems: number;
  offShiftRevenue: number;
  offShiftSharePct: number;
  unknownSupplierPct: number;
  uniqueSuppliersInRange: number;
};

type ComparisonCard = {
  key: string;
  label: string;
  currentValue: number;
  previousValue: number;
  deltaPct: number | null;
  formatter: (value: number) => string;
};

type TrendPoint = {
  date: string;
  label: string;
  fullLabel: string;
  totalRevenue: number;
  totalItemsSold: number;
  ma7Revenue: number;
  ma7Items: number;
};

type ShiftMixPoint = {
  date: string;
  label: string;
  fullLabel: string;
  firstShiftTotalItems: number;
  secondShiftTotalItems: number;
  totalItemsSold: number;
};

type SupplierConcentrationPoint = {
  supplierName: string;
  displayName: string;
  totalQty: number;
  totalRevenue: number;
  qtySharePct: number;
  revenueSharePct: number;
  cumulativeQtySharePct: number;
};

type WeekdayPoint = {
  weekday: number;
  dayName: string;
  avgRevenue: number;
  avgItems: number;
  firstShiftSharePct: number;
  dayCount: number;
};

type InsightTone = "good" | "warning" | "danger" | "info";

type InsightCard = {
  title: string;
  detail: string;
  tone: InsightTone;
};

type AnomalyPoint = {
  date: string;
  label: string;
  revenue: number;
  items: number;
  deviationPct: number;
  deviationValue: number;
};

const DEFAULT_TOP_N = 15;
const BLANK_SUPPLIER_COLUMN_COUNT = 15;
const BLANK_PRINT_ROW_COUNT = 31;
const SHIFT_PLACEHOLDER = "__________";
const FIRST_SHIFT_LABEL = "06:00-13:59";
const SECOND_SHIFT_LABEL = "14:00-21:59";
const CHART_GRID_STROKE = "var(--border-default)";
const CHART_AXIS_TICK = { fill: "var(--text-secondary)", fontSize: 12 };
const CHART_LEGEND_STYLE = { color: "var(--text-secondary)" };
const COMPACT_NUMBER_FORMATTER = new Intl.NumberFormat("sr-RS", { notation: "compact", maximumFractionDigits: 1 });
const WEEKDAY_ORDER: Array<{ key: number; label: string }> = [
  { key: 1, label: "Pon" },
  { key: 2, label: "Uto" },
  { key: 3, label: "Sre" },
  { key: 4, label: "Cet" },
  { key: 5, label: "Pet" },
  { key: 6, label: "Sub" },
  { key: 0, label: "Ned" },
];

function parseDateInputOrDefault(value: string | null, fallback: string): string {
  if (!value) return fallback;
  const normalized = value.slice(0, 10);
  return /^\d{4}-\d{2}-\d{2}$/.test(normalized) ? normalized : fallback;
}

function parseNullableInt(value: string | null): number | null {
  if (!value) return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

function parseTopN(value: string | null): number {
  if (!value) return DEFAULT_TOP_N;
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) return DEFAULT_TOP_N;
  return Math.min(25, Math.max(1, Math.round(parsed)));
}

function buildStoreLabel(store: StoreOption): string {
  const extras = [store.city, store.region].filter(Boolean).join(", ");
  return extras ? `${store.storeName} (${extras})` : store.storeName;
}

function fmtNumber(value: number): string {
  return value.toLocaleString("sr-RS");
}

function fmtCompactNumber(value: number): string {
  return COMPACT_NUMBER_FORMATTER.format(value);
}

function fmtDelta(deltaPct: number | null, currentValue: number, previousValue: number): string {
  if (deltaPct == null) {
    if (previousValue === 0 && currentValue > 0) return "Nova baza";
    return "N/A";
  }
  return fmtSignedPct(deltaPct, 1);
}

function fmtDate(value: string): string {
  const normalized = value.slice(0, 10);
  const match = normalized.match(/^(\d{4})-(\d{2})-(\d{2})$/);
  if (!match) {
    const parsed = new Date(value);
    if (Number.isNaN(parsed.getTime())) return value;
    return parsed.toLocaleDateString("sr-RS");
  }

  const [, yearRaw, monthRaw, dayRaw] = match;
  const year = Number(yearRaw);
  const month = Number(monthRaw);
  const day = Number(dayRaw);
  const parsed = new Date(year, month - 1, day);
  if (Number.isNaN(parsed.getTime())) return value;
  return parsed.toLocaleDateString("sr-RS");
}

function fmtDateShort(value: string | null | undefined): string {
  if (!value) return "";
  const normalized = value.slice(0, 10);
  const match = normalized.match(/^(\d{4})-(\d{2})-(\d{2})$/);
  if (match) {
    const [, y, m, d] = match;
    const yearShort = y.slice(2);
    return `${d}.${m}.${yearShort}`;
  }
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return "";
  const dd = String(parsed.getDate()).padStart(2, "0");
  const mm = String(parsed.getMonth() + 1).padStart(2, "0");
  const yy = String(parsed.getFullYear()).slice(2);
  return `${dd}.${mm}.${yy}`;
}

function fmtDateISO(value: string | null | undefined): string {
  if (!value) return "";
  // Extract just the date part (YYYY-MM-DD) from ISO string
  return value.slice(0, 10);
}

function sortMarker(field: SortKey, active: SortKey, dir: SortDir): ReactNode | null {
  if (field !== active) return null;
  // Use simple Unicode badges; kept small to avoid encoding issues in common setups
  const up = "▲";
  const down = "▼";
  return <span className="sort-badge">{dir === "asc" ? up : down}</span>;
}

function sum(values: number[]): number {
  return values.reduce((acc, value) => acc + value, 0);
}

function hasMissingShiftSummary(row: DailySalesRow): boolean {
  return row.totalItemsSold > 0 && row.firstShiftTotalItems === 0 && row.secondShiftTotalItems === 0;
}

function shiftExportValue(row: DailySalesRow, shift: "first" | "second"): string | number {
  if (hasMissingShiftSummary(row)) return SHIFT_PLACEHOLDER;
  return shift === "first" ? row.firstShiftTotalItems : row.secondShiftTotalItems;
}

function shiftDisplayValue(row: DailySalesRow, shift: "first" | "second"): string {
  if (hasMissingShiftSummary(row)) return "N/A";
  const value = shift === "first" ? row.firstShiftTotalItems : row.secondShiftTotalItems;
  return fmtNumber(value);
}

function safeDivide(value: number, total: number): number {
  return total === 0 ? 0 : value / total;
}

function parseDateOnly(value: string): Date | null {
  const normalized = value.slice(0, 10);
  const match = normalized.match(/^(\d{4})-(\d{2})-(\d{2})$/);
  if (!match) {
    const parsed = new Date(value);
    return Number.isNaN(parsed.getTime()) ? null : parsed;
  }

  const [, yearRaw, monthRaw, dayRaw] = match;
  return new Date(Date.UTC(Number(yearRaw), Number(monthRaw) - 1, Number(dayRaw)));
}

function addDaysToDateInput(value: string, days: number): string {
  const parsed = parseDateOnly(value);
  if (!parsed) return value;
  parsed.setUTCDate(parsed.getUTCDate() + days);
  return parsed.toISOString().slice(0, 10);
}

function getInclusiveDayCount(fromDate: string, toDate: string): number {
  const from = parseDateOnly(fromDate);
  const to = parseDateOnly(toDate);
  if (!from || !to) return 0;
  return Math.floor((to.getTime() - from.getTime()) / 86_400_000) + 1;
}

function getPreviousPeriodRange(filters: ActiveFilters): { fromDate: string; toDate: string } {
  const dayCount = Math.max(1, getInclusiveDayCount(filters.fromDate, filters.toDate));
  return {
    fromDate: addDaysToDateInput(filters.fromDate, -dayCount),
    toDate: addDaysToDateInput(filters.fromDate, -1),
  };
}

function calculateDeltaPct(currentValue: number, previousValue: number): number | null {
  if (previousValue === 0) {
    return currentValue === 0 ? 0 : null;
  }
  return ((currentValue - previousValue) / previousValue) * 100;
}

function truncateLabel(value: string, maxLength = 18): string {
  if (value.length <= maxLength) return value;
  return `${value.slice(0, Math.max(0, maxLength - 3))}...`;
}

function buildRollingAverage(rows: DailySalesRow[], index: number, accessor: (row: DailySalesRow) => number, windowSize = 7): number {
  const start = Math.max(0, index - (windowSize - 1));
  const slice = rows.slice(start, index + 1);
  if (slice.length === 0) return 0;
  return slice.reduce((acc, row) => acc + accessor(row), 0) / slice.length;
}

function summarizePeriod(response: DailySalesTableResponse | null): PeriodSummary {
  const rows = response?.dateRows ?? [];
  const totalRevenue = rows.reduce((acc, row) => acc + row.totalRevenue, 0);
  const totalVisibleItems = rows.reduce((acc, row) => acc + row.totalItemsSold, 0);
  const totalItemsInRange = response?.metadata.totalItemsInRange ?? totalVisibleItems;
  const totalDays = response?.metadata.totalDays ?? rows.length;
  const firstShiftItems = rows.reduce((acc, row) => acc + row.firstShiftTotalItems, 0);
  const secondShiftItems = rows.reduce((acc, row) => acc + row.secondShiftTotalItems, 0);
  const shiftAccountedItems = firstShiftItems + secondShiftItems;
  const offShiftItems = response?.metadata.offShiftItems ?? 0;
  const offShiftRevenue = response?.metadata.offShiftRevenue ?? 0;

  return {
    totalRevenue,
    totalVisibleItems,
    totalItemsInRange,
    totalDays,
    avgRevenuePerDay: safeDivide(totalRevenue, totalDays),
    avgItemsPerDay: safeDivide(totalVisibleItems, totalDays),
    avgRevenuePerItem: safeDivide(totalRevenue, totalVisibleItems),
    firstShiftItems,
    secondShiftItems,
    firstShiftSharePct: safeDivide(firstShiftItems, shiftAccountedItems) * 100,
    secondShiftSharePct: safeDivide(secondShiftItems, shiftAccountedItems) * 100,
    offShiftItems,
    offShiftRevenue,
    offShiftSharePct: safeDivide(offShiftItems, totalItemsInRange) * 100,
    unknownSupplierPct: response?.metadata.unknownSupplierPct ?? 0,
    uniqueSuppliersInRange: response?.metadata.uniqueSuppliersInRange ?? 0,
  };
}

function comparisonTone(deltaPct: number | null): InsightTone {
  if (deltaPct == null) return "info";
  if (deltaPct >= 5) return "good";
  if (deltaPct <= -5) return "danger";
  return "warning";
}

export default function DailySalesStatsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const requestIdRef = useRef(0);

  const initialRange = useMemo(() => getPresetRange("30d"), []);
  const queryFromDate = parseDateInputOrDefault(searchParams.get("fromDate"), initialRange.fromDate);
  const queryToDate = parseDateInputOrDefault(searchParams.get("toDate"), initialRange.toDate);
  const queryStoreId = parseNullableInt(searchParams.get("storeId"));
  const queryTopN = parseTopN(searchParams.get("topN"));
  const queryDataScope = (searchParams.get("dataScope") ?? getDataScope()).trim() || "all";
  const hasExplicitDate = searchParams.has("fromDate") || searchParams.has("toDate");
  const initialPreset: PeriodPreset = hasExplicitDate ? "custom" : "30d";

  const [periodPreset, setPeriodPreset] = useState<PeriodPreset>(initialPreset);
  const [fromDate, setFromDate] = useState(queryFromDate);
  const [toDate, setToDate] = useState(queryToDate);
  const [storeId, setStoreId] = useState<number | null>(queryStoreId);
  const [topN, setTopN] = useState<number>(queryTopN);
  const [activeFilters, setActiveFilters] = useState<ActiveFilters>({
    fromDate: queryFromDate,
    toDate: queryToDate,
    storeId: queryStoreId,
    topN: queryTopN,
  });

  const [stores, setStores] = useState<StoreOption[]>([]);
  const [data, setData] = useState<DailySalesTableResponse | null>(null);
  const [previousData, setPreviousData] = useState<DailySalesTableResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sortKey, setSortKey] = useState<SortKey>("date");
  const [sortDir, setSortDir] = useState<SortDir>("desc");
  const [qualityPanelOpen, setQualityPanelOpen] = useState(false);

  const memoizedQueryDataScope = useMemo(() => queryDataScope, [queryDataScope]);

  const invalidRange = useMemo(() => {
    if (!fromDate || !toDate) return false;
    return fromDate > toDate;
  }, [fromDate, toDate]);

  useEffect(() => {
    const loadStores = async () => {
      try {
        setStores(await getStores(true));
      } catch {
        setStores([]);
      }
    };

    void loadStores();
  }, []);

  const load = useCallback(async (filters: ActiveFilters, signal?: AbortSignal) => {
    const requestId = ++requestIdRef.current;
    const previousRange = getPreviousPeriodRange(filters);
    setLoading(true);
    setError(null);

    try {
      const currentPromise = getDailySalesStats({
        fromDate: filters.fromDate,
        toDate: filters.toDate,
        storeId: filters.storeId,
        topN: filters.topN,
        dataScope: memoizedQueryDataScope,
        signal,
      });

      const previousPromise = getDailySalesStats({
        fromDate: previousRange.fromDate,
        toDate: previousRange.toDate,
        storeId: filters.storeId,
        topN: filters.topN,
        dataScope: memoizedQueryDataScope,
        signal,
      }).catch((reason) => {
        if (reason instanceof DOMException && reason.name === "AbortError") {
          throw reason;
        }
        return null;
      });

      const [result, previousResult] = await Promise.all([currentPromise, previousPromise]);

      if (requestId !== requestIdRef.current) return;
      setData(result);
      setPreviousData(previousResult);
    } catch (reason) {
      if (reason instanceof DOMException && reason.name === "AbortError") {
        return;
      }
      if (requestId !== requestIdRef.current) return;
      setData(null);
      setPreviousData(null);
      setError(reason instanceof Error ? reason.message : "Greska pri ucitavanju dnevne prodaje.");
    } finally {
      if (requestId === requestIdRef.current) {
        setLoading(false);
      }
    }
  }, [memoizedQueryDataScope]);

  useEffect(() => {
    const controller = new AbortController();
    void load(activeFilters, controller.signal);
    return () => controller.abort();
  }, [activeFilters, load]);

  const supplierHeaders = data?.topSuppliersOrder ?? [];
  const previousRange = useMemo(() => getPreviousPeriodRange(activeFilters), [activeFilters]);
  const timeSeriesRows = useMemo(
    () => [...(data?.dateRows ?? [])].sort((left, right) => left.date.localeCompare(right.date)),
    [data?.dateRows]
  );

  const sortedRows = useMemo(() => {
    const rows = [...(data?.dateRows ?? [])];
    const resolveValue = (row: DailySalesRow, key: SortKey): number | string => {
      if (key === "date") return new Date(row.date).getTime();
      if (key === "firstShiftTotalItems") return row.firstShiftTotalItems;
      if (key === "secondShiftTotalItems") return row.secondShiftTotalItems;
      if (key === "totalRevenue") return row.totalRevenue;
      if (key === "othersCount") return row.othersCount;
      if (key === "totalItemsSold") return row.totalItemsSold;
      if (key.startsWith("supplier:")) {
        const index = Number(key.split(":")[1]);
        return row.topSupplierCounts[index] ?? 0;
      }
      return 0;
    };

    return rows.sort((a, b) => {
      const left = resolveValue(a, sortKey);
      const right = resolveValue(b, sortKey);
      let compare = 0;

      if (typeof left === "number" && typeof right === "number") {
        compare = left - right;
      } else {
        compare = String(left).localeCompare(String(right), "sr");
      }

      return sortDir === "asc" ? compare : -compare;
    });
  }, [data?.dateRows, sortDir, sortKey]);

  const mismatchCount = useMemo(
    () =>
      timeSeriesRows.filter((row) => {
        const bySuppliers = sum(row.topSupplierCounts) + row.othersCount;
        return bySuppliers !== row.totalItemsSold;
      }).length,
    [timeSeriesRows]
  );

  const missingShiftCount = useMemo(
    () => timeSeriesRows.filter((row) => hasMissingShiftSummary(row)).length,
    [timeSeriesRows]
  );

  const currentSummary = useMemo(() => summarizePeriod(data), [data]);
  const previousSummary = useMemo(() => summarizePeriod(previousData), [previousData]);

  const toolbarColumns = useMemo<AnalyticsTableColumn<DailySalesRow>[]>(() => {
    const baseColumns: AnalyticsTableColumn<DailySalesRow>[] = [
      { key: "date", header: "Datum", dataType: "date", getValue: (row) => row.date },
      { key: "firstShiftTotalItems", header: "Prva smena: __________", dataType: "number", getValue: () => "" },
      { key: "secondShiftTotalItems", header: "Druga smena: __________", dataType: "number", getValue: () => "" },
      { key: "totalRevenue", header: "Ukupan prihod", dataType: "currency" },
    ];

    const supplierColumns: AnalyticsTableColumn<DailySalesRow>[] = supplierHeaders.map((name, index) => {
      const displayName = sortedRows.length === 0 ? "" : name;
      return {
        key: `supplier:${index}`,
        header: displayName,
        dataType: "number",
        getValue: (row) => row.topSupplierCounts[index] ?? 0,
        detailLabel: `${displayName} (kom.)`,
      };
    });

    return [
      ...baseColumns,
      ...supplierColumns,
      { key: "othersCount", header: "Ostali (kom.)", dataType: "number" },
      { key: "totalItemsSold", header: "Ukupno proizvoda", dataType: "number" },
    ];
  }, [supplierHeaders, sortedRows.length]);

  const toolbarFilters = useMemo<AnalyticsNamedValue[]>(() => [
    { key: "fromDate", label: "Od", value: activeFilters.fromDate },
    { key: "toDate", label: "Do", value: activeFilters.toDate },
    { key: "storeId", label: "Objekat", value: activeFilters.storeId ?? "Svi objekti" },
    { key: "topN", label: "Top dobavljača", value: activeFilters.topN },
    { key: "dataScope", label: "Opseg podataka", value: memoizedQueryDataScope },
  ], [activeFilters.fromDate, activeFilters.storeId, activeFilters.toDate, activeFilters.topN, memoizedQueryDataScope]);

  const toolbarMetadata = useMemo<AnalyticsNamedValue[]>(() => [
    { key: "requestedFrom", label: "Zahtevan od", value: fmtDateISO(data?.requestedFrom) ?? "" },
    { key: "requestedTo", label: "Zahtevan do", value: fmtDateISO(data?.requestedTo) ?? "" },
    { key: "totalDays", label: "Broj dana", value: data?.metadata.totalDays ?? 0 },
    { key: "unknownSupplierPct", label: "Udeo nepoznatih dobavljača %", value: data?.metadata.unknownSupplierPct ?? 0 },
    { key: "firstShiftHeader", label: "Prva smena", value: FIRST_SHIFT_LABEL },
    { key: "secondShiftHeader", label: "Druga smena", value: SECOND_SHIFT_LABEL },
    { key: "warnings", label: "Upozorenja", value: data?.metadata.warnings.join(" | ") ?? "" },
  ], [data?.metadata.totalDays, data?.metadata.unknownSupplierPct, data?.metadata.warnings, data?.requestedFrom, data?.requestedTo]);

  const printRows = useMemo(() => sortedRows.slice(0, 30), [sortedRows]);

  const trendData = useMemo<TrendPoint[]>(() => (
    timeSeriesRows.map((row, index) => ({
      date: row.date,
      label: fmtDateShort(row.date),
      fullLabel: fmtDate(row.date),
      totalRevenue: row.totalRevenue,
      totalItemsSold: row.totalItemsSold,
      ma7Revenue: buildRollingAverage(timeSeriesRows, index, (currentRow) => currentRow.totalRevenue, 7),
      ma7Items: buildRollingAverage(timeSeriesRows, index, (currentRow) => currentRow.totalItemsSold, 7),
    }))
  ), [timeSeriesRows]);

  const shiftMixData = useMemo<ShiftMixPoint[]>(() => (
    timeSeriesRows.map((row) => ({
      date: row.date,
      label: fmtDateShort(row.date),
      fullLabel: fmtDate(row.date),
      firstShiftTotalItems: row.firstShiftTotalItems,
      secondShiftTotalItems: row.secondShiftTotalItems,
      totalItemsSold: row.totalItemsSold,
    }))
  ), [timeSeriesRows]);

  const supplierConcentration = useMemo(() => {
    if (!data) {
      return {
        chartData: [] as SupplierConcentrationPoint[],
        displayChartData: [] as SupplierConcentrationPoint[],
        top3QtySharePct: 0,
        top5QtySharePct: 0,
        suppliersTo80Pct: 0,
      };
    }

    const supplierQtyBasis = Math.max(
      data.metadata.totalItemsInRange,
      data.topSuppliers.reduce((acc, supplier) => acc + supplier.totalQty, 0)
    );
    const supplierRevenueBasis = Math.max(
      currentSummary.totalRevenue,
      data.topSuppliers.reduce((acc, supplier) => acc + supplier.totalRevenue, 0)
    );

    const baseRows = data.topSuppliers.map((supplier) => ({
      supplierName: supplier.supplierName,
      displayName: truncateLabel(supplier.supplierName),
      totalQty: supplier.totalQty,
      totalRevenue: supplier.totalRevenue,
      qtySharePct: safeDivide(supplier.totalQty, supplierQtyBasis) * 100,
      revenueSharePct: safeDivide(supplier.totalRevenue, supplierRevenueBasis) * 100,
      cumulativeQtySharePct: 0,
    }));

    const topSupplierQty = baseRows.reduce((acc, row) => acc + row.totalQty, 0);
    const topSupplierRevenue = baseRows.reduce((acc, row) => acc + row.totalRevenue, 0);
    const othersQty = Math.max(0, supplierQtyBasis - topSupplierQty);
    const othersRevenue = Math.max(0, supplierRevenueBasis - topSupplierRevenue);

    const allRows = [...baseRows];
    if (othersQty > 0 || othersRevenue > 0) {
      allRows.push({
        supplierName: "Ostali",
        displayName: "Ostali",
        totalQty: othersQty,
        totalRevenue: othersRevenue,
        qtySharePct: safeDivide(othersQty, supplierQtyBasis) * 100,
        revenueSharePct: safeDivide(othersRevenue, supplierRevenueBasis) * 100,
        cumulativeQtySharePct: 0,
      });
    }

    let runningShare = 0;
    const chartData = allRows.map((row) => {
      runningShare += row.qtySharePct;
      return {
        ...row,
        cumulativeQtySharePct: runningShare,
      };
    });

    const top3QtySharePct = safeDivide(baseRows.slice(0, 3).reduce((acc, row) => acc + row.totalQty, 0), supplierQtyBasis) * 100;
    const top5QtySharePct = safeDivide(baseRows.slice(0, 5).reduce((acc, row) => acc + row.totalQty, 0), supplierQtyBasis) * 100;

    let cumulative = 0;
    let suppliersTo80Pct = 0;
    for (let index = 0; index < baseRows.length; index += 1) {
      cumulative += baseRows[index].qtySharePct;
      if (cumulative >= 80) {
        suppliersTo80Pct = index + 1;
        break;
      }
    }

    const displayChartData = chartData.length <= 9
      ? chartData
      : (() => {
          const othersRow = chartData.find((row) => row.supplierName === "Ostali");
          const headRows = chartData.filter((row) => row.supplierName !== "Ostali").slice(0, 8);
          return othersRow ? [...headRows, othersRow] : headRows;
        })();

    return {
      chartData,
      displayChartData,
      top3QtySharePct,
      top5QtySharePct,
      suppliersTo80Pct,
    };
  }, [currentSummary.totalRevenue, data]);

  const weekdayData = useMemo<WeekdayPoint[]>(() => {
    const buckets = new Map<number, { revenue: number; items: number; firstShift: number; secondShift: number; dayCount: number }>();

    timeSeriesRows.forEach((row) => {
      const parsed = parseDateOnly(row.date);
      if (!parsed) return;
      const weekday = parsed.getUTCDay();
      const current = buckets.get(weekday) ?? { revenue: 0, items: 0, firstShift: 0, secondShift: 0, dayCount: 0 };
      current.revenue += row.totalRevenue;
      current.items += row.totalItemsSold;
      current.firstShift += row.firstShiftTotalItems;
      current.secondShift += row.secondShiftTotalItems;
      current.dayCount += 1;
      buckets.set(weekday, current);
    });

    return WEEKDAY_ORDER.map(({ key, label }) => {
      const bucket = buckets.get(key) ?? { revenue: 0, items: 0, firstShift: 0, secondShift: 0, dayCount: 0 };
      const shiftItems = bucket.firstShift + bucket.secondShift;
      return {
        weekday: key,
        dayName: label,
        avgRevenue: safeDivide(bucket.revenue, bucket.dayCount),
        avgItems: safeDivide(bucket.items, bucket.dayCount),
        firstShiftSharePct: safeDivide(bucket.firstShift, shiftItems) * 100,
        dayCount: bucket.dayCount,
      };
    });
  }, [timeSeriesRows]);

  const comparisonCards = useMemo<ComparisonCard[]>(() => [
    {
      key: "revenue",
      label: "Prihod",
      currentValue: currentSummary.totalRevenue,
      previousValue: previousSummary.totalRevenue,
      deltaPct: calculateDeltaPct(currentSummary.totalRevenue, previousSummary.totalRevenue),
      formatter: fmtRsdShort,
    },
    {
      key: "items",
      label: "Komadi",
      currentValue: currentSummary.totalVisibleItems,
      previousValue: previousSummary.totalVisibleItems,
      deltaPct: calculateDeltaPct(currentSummary.totalVisibleItems, previousSummary.totalVisibleItems),
      formatter: fmtNumber,
    },
    {
      key: "avgRevenuePerDay",
      label: "Prihod / dan",
      currentValue: currentSummary.avgRevenuePerDay,
      previousValue: previousSummary.avgRevenuePerDay,
      deltaPct: calculateDeltaPct(currentSummary.avgRevenuePerDay, previousSummary.avgRevenuePerDay),
      formatter: fmtRsdShort,
    },
    {
      key: "avgRevenuePerItem",
      label: "RSD / komad",
      currentValue: currentSummary.avgRevenuePerItem,
      previousValue: previousSummary.avgRevenuePerItem,
      deltaPct: calculateDeltaPct(currentSummary.avgRevenuePerItem, previousSummary.avgRevenuePerItem),
      formatter: fmtRsdShort,
    },
  ], [currentSummary, previousSummary]);

  const bestRevenueDay = useMemo(
    () => timeSeriesRows.reduce<DailySalesRow | null>((best, row) => (!best || row.totalRevenue > best.totalRevenue ? row : best), null),
    [timeSeriesRows]
  );

  const weakestRevenueDay = useMemo(
    () => timeSeriesRows.reduce<DailySalesRow | null>((lowest, row) => (!lowest || row.totalRevenue < lowest.totalRevenue ? row : lowest), null),
    [timeSeriesRows]
  );

  const dayOverDayChanges = useMemo(() => {
    const changes = [];
    for (let index = 1; index < timeSeriesRows.length; index += 1) {
      const current = timeSeriesRows[index];
      const previous = timeSeriesRows[index - 1];
      const revenueDelta = current.totalRevenue - previous.totalRevenue;
      changes.push({
        date: current.date,
        label: fmtDate(current.date),
        revenueDelta,
        revenueDeltaPct: calculateDeltaPct(current.totalRevenue, previous.totalRevenue),
      });
    }
    return changes;
  }, [timeSeriesRows]);

  const biggestJump = useMemo(
    () => dayOverDayChanges.reduce<typeof dayOverDayChanges[number] | null>((best, item) => (!best || item.revenueDelta > best.revenueDelta ? item : best), null),
    [dayOverDayChanges]
  );

  const biggestDrop = useMemo(
    () => dayOverDayChanges.reduce<typeof dayOverDayChanges[number] | null>((lowest, item) => (!lowest || item.revenueDelta < lowest.revenueDelta ? item : lowest), null),
    [dayOverDayChanges]
  );

  const anomalyRows = useMemo<AnomalyPoint[]>(() => {
    return trendData
      .map((point) => {
        const deviationValue = point.totalRevenue - point.ma7Revenue;
        const deviationPct = calculateDeltaPct(point.totalRevenue, point.ma7Revenue) ?? 0;
        return {
          date: point.date,
          label: point.fullLabel,
          revenue: point.totalRevenue,
          items: point.totalItemsSold,
          deviationPct,
          deviationValue,
        };
      })
      .sort((left, right) => Math.abs(right.deviationValue) - Math.abs(left.deviationValue))
      .slice(0, 3);
  }, [trendData]);

  const qualitySignals = useMemo(() => ([
    {
      key: "unknown",
      label: "Nepoznati dobavljac",
      value: fmtPct(data?.metadata.unknownSupplierPct ?? 0, 1),
      tone: (data?.metadata.unknownSupplierPct ?? 0) >= 5 ? "danger" : (data?.metadata.unknownSupplierPct ?? 0) > 0 ? "warning" : "good",
      description: "Udeo prodaje bez mapiranog dobavljača.",
    },
    {
      key: "offShiftItems",
      label: "Van smene (kom.)",
      value: fmtNumber(data?.metadata.offShiftItems ?? 0),
      tone: (data?.metadata.offShiftItems ?? 0) > 0 ? "warning" : "good",
      description: "Prodaja sa satnicom van definisanih smena.",
    },
    {
      key: "offShiftRevenue",
      label: "Van smene (RSD)",
      value: fmtRsdShort(data?.metadata.offShiftRevenue ?? 0),
      tone: (data?.metadata.offShiftRevenue ?? 0) > 0 ? "warning" : "good",
      description: "Prihod evidentiran van operativnih smena.",
    },
    {
      key: "mismatch",
      label: "Dani nepodudaranja",
      value: fmtNumber(mismatchCount),
      tone: mismatchCount > 0 ? "danger" : "good",
      description: "Dani gde se totals ne poklapaju sa top+others sabiranjem.",
    },
    {
      key: "missingShift",
      label: "Dani bez satnice",
      value: fmtNumber(missingShiftCount),
      tone: missingShiftCount > 0 ? "warning" : "good",
      description: "Dani sa prometom bez pouzdanog razdvajanja po smenama.",
    },
    {
      key: "duplicateReceipts",
      label: "Dupli računi",
      value: fmtNumber(data?.metadata.duplicateReceiptGroupCount ?? 0),
      tone: (data?.metadata.duplicateReceiptGroupCount ?? 0) > 0 ? "danger" : "good",
      description: "Isti broj računa više puta za isti datum i objekat.",
    },
    {
      key: "receiptMismatch",
      label: "Neusklađeni računi",
      value: fmtNumber(data?.metadata.receiptAmountMismatchCount ?? 0),
      tone: (data?.metadata.receiptAmountMismatchCount ?? 0) > 0 ? "danger" : "good",
      description: "Računi gde dnevnik i suma stavki ne daju isti iznos.",
    },
    {
      key: "nonStandardReceipts",
      label: "Nestandardni računi",
      value: fmtNumber(data?.metadata.nonStandardReceiptCount ?? 0),
      tone: (data?.metadata.nonStandardReceiptCount ?? 0) > 0 ? "warning" : "good",
      description: "Dokumenti sa nenumerickim brojem racuna, npr. DUG.",
    },
    {
      key: "nonStandardRevenue",
      label: "Nestandardni RSD",
      value: fmtRsdShort(data?.metadata.nonStandardReceiptRevenue ?? 0),
      tone: (data?.metadata.nonStandardReceiptRevenue ?? 0) > 0 ? "warning" : "good",
      description: "Promet ostvaren kroz nestandardne prodajne dokumente.",
    },
    {
      key: "suppliers",
      label: "Aktivni dobavljači",
      value: fmtNumber(data?.metadata.uniqueSuppliersInRange ?? 0),
      tone: "info",
      description: "Broj dobavljača sa prometom u opsegu.",
    },
  ]), [
    data?.metadata.duplicateReceiptGroupCount,
    data?.metadata.nonStandardReceiptCount,
    data?.metadata.nonStandardReceiptRevenue,
    data?.metadata.offShiftItems,
    data?.metadata.offShiftRevenue,
    data?.metadata.receiptAmountMismatchCount,
    data?.metadata.uniqueSuppliersInRange,
    data?.metadata.unknownSupplierPct,
    mismatchCount,
    missingShiftCount,
  ]);

  // Data Health badge: count non-info, non-good signals
  const dataHealthSummary = useMemo(() => {
    const problemSignals = qualitySignals.filter((s) => s.tone === "danger" || s.tone === "warning");
    const dangerCount = qualitySignals.filter((s) => s.tone === "danger").length;
    const total = problemSignals.length;
    if (dangerCount > 0) return { tone: "danger" as const, label: `\u26a0 ${total} problem${total === 1 ? "" : "a"} u podacima`, count: total };
    if (total > 0) return { tone: "warning" as const, label: `\u26a0 ${total} upozorenje${total === 1 ? "" : "a"}`, count: total };
    return { tone: "good" as const, label: "\u2713 Podaci u redu", count: 0 };
  }, [qualitySignals]);

  const heuristicInsights = useMemo<InsightCard[]>(() => {
    const insights: InsightCard[] = [];
    const revenueDeltaPct = comparisonCards.find((item) => item.key === "revenue")?.deltaPct ?? null;

    if (revenueDeltaPct != null && revenueDeltaPct >= 8) {
      insights.push({
        title: "Pozitivan momentum",
        detail: `Prihod je ${fmtSignedPct(revenueDeltaPct, 1)} u odnosu na prethodni period ${fmtDateShort(previousRange.fromDate)} - ${fmtDateShort(previousRange.toDate)}.`,
        tone: "good",
      });
    } else if (revenueDeltaPct != null && revenueDeltaPct <= -8) {
      insights.push({
        title: "Pad prihoda",
        detail: `Prihod je ${fmtSignedPct(revenueDeltaPct, 1)} u odnosu na prethodni period. Vredi pregledati anomalne dane i smenski miks.`,
        tone: "danger",
      });
    }

    const shiftGap = currentSummary.secondShiftSharePct - currentSummary.firstShiftSharePct;
    if (shiftGap >= 10) {
      insights.push({
        title: "Druga smena nosi prodaju",
        detail: `Druga smena drži ${fmtPct(currentSummary.secondShiftSharePct, 1)} vidljivih komada, što je ${fmtPct(shiftGap, 1)} više od prve smene.`,
        tone: "info",
      });
    } else if (shiftGap <= -10) {
      insights.push({
        title: "Prva smena dominira",
        detail: `Prva smena drži ${fmtPct(currentSummary.firstShiftSharePct, 1)} vidljivih komada. Vredi provjeriti raspored osoblja i dopunu ujutru.`,
        tone: "info",
      });
    }

    if (supplierConcentration.top3QtySharePct >= 55) {
      insights.push({
        title: "Visoka koncentracija dobavljača",
        detail: `Top 3 dobavljača nose ${fmtPct(supplierConcentration.top3QtySharePct, 1)} komada. Rizik zavisnosti je povišen.`,
        tone: "warning",
      });
    }

    if ((data?.metadata.unknownSupplierPct ?? 0) >= 5 || mismatchCount > 0 || missingShiftCount > 0) {
      insights.push({
        title: "Upozorenje: podaci zahtevaju pažnju",
        detail: `Unknown share je ${fmtPct(data?.metadata.unknownSupplierPct ?? 0, 1)}, mismatch dana ${fmtNumber(mismatchCount)}, dana bez satnice ${fmtNumber(missingShiftCount)}.`,
        tone: "warning",
      });
    }

    if ((data?.metadata.duplicateReceiptGroupCount ?? 0) > 0 || (data?.metadata.receiptAmountMismatchCount ?? 0) > 0) {
      insights.push({
        title: "Prodaja trazi rekonsilijaciju",
        detail: `Duplih racuna je ${fmtNumber(data?.metadata.duplicateReceiptGroupCount ?? 0)}, a racuna sa mismatch-om između dnevnika i stavki ${fmtNumber(data?.metadata.receiptAmountMismatchCount ?? 0)}.`,
        tone: "danger",
      });
    }

    if ((data?.metadata.nonStandardReceiptCount ?? 0) > 0) {
      insights.push({
        title: "Postoje nestandardni dokumenti prodaje",
        detail: `${fmtNumber(data?.metadata.nonStandardReceiptCount ?? 0)} dokumenta nose ${fmtRsdShort(data?.metadata.nonStandardReceiptRevenue ?? 0)}. DUG se pojavljuje ${fmtNumber(data?.metadata.debtReceiptCount ?? 0)} put(a).`,
        tone: "warning",
      });
    }

    if ((data?.metadata.offShiftItems ?? 0) > 0) {
      insights.push({
        title: "Ima prodaje van smene",
        detail: `${fmtNumber(data?.metadata.offShiftItems ?? 0)} komada i ${fmtRsdShort(data?.metadata.offShiftRevenue ?? 0)} evidentirano je van standardne satnice.`,
        tone: "warning",
      });
    }

    if (insights.length === 0) {
      insights.push({
        title: "Stabilan pregled",
        detail: "Nema jakog negativnog signala u periodu. Fokus prebaci na trend, weekday obrazac i top dobavljače.",
        tone: "good",
      });
    }

    return insights.slice(0, 4);
  }, [
    comparisonCards,
    data?.metadata.debtReceiptCount,
    currentSummary.firstShiftSharePct,
    currentSummary.secondShiftSharePct,
    data?.metadata.duplicateReceiptGroupCount,
    data?.metadata.nonStandardReceiptCount,
    data?.metadata.nonStandardReceiptRevenue,
    data?.metadata.offShiftItems,
    data?.metadata.offShiftRevenue,
    data?.metadata.receiptAmountMismatchCount,
    data?.metadata.unknownSupplierPct,
    mismatchCount,
    missingShiftCount,
    previousRange.fromDate,
    previousRange.toDate,
    supplierConcentration.top3QtySharePct,
  ]);

  const dayPatternSummary = useMemo(() => {
    const workingDays = weekdayData.filter((row) => row.weekday !== 0 && row.dayCount > 0);
    const candidateDays = workingDays.length > 0
      ? workingDays
      : weekdayData.filter((row) => row.dayCount > 0);

    const strongestDay = candidateDays.reduce<WeekdayPoint | null>(
      (best, row) => (!best || row.avgRevenue > best.avgRevenue ? row : best),
      null
    );
    const weakestDay = candidateDays.reduce<WeekdayPoint | null>(
      (lowest, row) => (!lowest || row.avgRevenue < lowest.avgRevenue ? row : lowest),
      null
    );
    return { strongestDay, weakestDay };
  }, [weekdayData]);

  const chartTickInterval = useMemo(
    () => Math.max(0, Math.ceil(Math.max(timeSeriesRows.length, 1) / 10) - 1),
    [timeSeriesRows.length]
  );

  const handleSort = useCallback((field: SortKey) => {
    setSortKey((previous) => {
      if (previous === field) {
        setSortDir((current) => (current === "asc" ? "desc" : "asc"));
        return previous;
      }
      setSortDir(field === "date" ? "desc" : "desc");
      return field;
    });
  }, []);

  const applyPreset = (preset: PeriodPreset) => {
    setPeriodPreset(preset);
    if (preset === "custom") return;
    const range = getPresetRange(preset);
    setFromDate(range.fromDate);
    setToDate(range.toDate);
    const next = { fromDate: range.fromDate, toDate: range.toDate, storeId, topN };
    setActiveFilters(next);
    updateQueryParams(next);
  };

  const updateQueryParams = (filters: ActiveFilters) => {
    const params = new URLSearchParams();
    params.set("fromDate", filters.fromDate);
    params.set("toDate", filters.toDate);
    if (filters.storeId != null) params.set("storeId", String(filters.storeId));
    params.set("topN", String(filters.topN));
    params.set("dataScope", memoizedQueryDataScope);
    setSearchParams(params, { replace: true });
  };

  const handleApplyFilters = () => {
    if (invalidRange) {
      setError("Datum 'od' ne može biti posle datuma 'do'.");
      return;
    }

    const next = {
      fromDate,
      toDate,
      storeId,
      topN: Math.min(25, Math.max(1, Math.round(topN || DEFAULT_TOP_N))),
    };
    setTopN(next.topN);
    setActiveFilters(next);
    updateQueryParams(next);
  };

  const handleResetFilters = () => {
    const range = getPresetRange("30d");
    const next = {
      fromDate: range.fromDate,
      toDate: range.toDate,
      storeId: null,
      topN: DEFAULT_TOP_N,
    };
    setPeriodPreset("30d");
    setFromDate(range.fromDate);
    setToDate(range.toDate);
    setStoreId(null);
    setTopN(DEFAULT_TOP_N);
    setActiveFilters(next);
    updateQueryParams(next);
  };

  const handleJumpToAvailableData = () => {
    const min = data?.metadata.minAvailableDate;
    const max = data?.metadata.maxAvailableDate;
    if (!min || !max) return;
    const newFrom = min.slice(0, 10);
    const newTo = max.slice(0, 10);
    const next = { fromDate: newFrom, toDate: newTo, storeId, topN };
    setPeriodPreset("custom");
    setFromDate(newFrom);
    setToDate(newTo);
    setActiveFilters(next);
    updateQueryParams(next);
  };

  const handlePrintBlank = useCallback(() => {
    const manualSupplierColumns = Array.from({ length: BLANK_SUPPLIER_COLUMN_COUNT }, (_, index) => ({
      key: `manualSupplier:${index + 1}`,
      header: "",
      dataType: "text",
    }));

    const blankColumns = [
      { key: "date",    header: "Datum",                    dataType: "text" },
      { key: "worker1", header: "I sm.",                    dataType: "text" },
      { key: "worker2", header: "II sm.",                   dataType: "text" },
      { key: "others",  header: "Uk. sm.",                  dataType: "text" },
      ...manualSupplierColumns,
      { key: "revenue", header: "Ost.",                     dataType: "text" },
      { key: "total",   header: "Ukupno",                    dataType: "text" },
    ];

    const blankRows = Array.from({ length: BLANK_PRINT_ROW_COUNT }, () =>
      Object.fromEntries(blankColumns.map((col) => [col.key, ""]))
    );

    const stateKey = savePrintPayload({
      tableKey: "daily-sales-stats-blank",
      tableTitle: "Dnevna prodaja po smeni",
      columns: blankColumns,
      rows: blankRows,
      filters: [],
      metadata: [],
      locale: "sr-RS",
      documentType: "daily-sales-blank",
    });

    window.open(
      `/print/analytics/${encodeURIComponent("daily-sales-stats")}?stateKey=${encodeURIComponent(stateKey)}`,
      "_blank",
      "noopener"
    );
  }, []);

  return (
    <div className="daily-sales-page">
      <header className="daily-sales-header">
        <div>
          <h1>Prodaja po smeni</h1>
          <p>
            Dnevni pregled po smenama: količine, prihodi i top dobavljači.
          </p>
        </div>
        <div className="daily-sales-generated">
          Opseg: {fmtDateShort(data?.requestedFrom ?? fromDate)} - {fmtDateShort(data?.requestedTo ?? toDate)}
        </div>
      </header>

      <section className="daily-sales-filters">
        <label>
          <span>Period</span>
          <select value={periodPreset} onChange={(event) => applyPreset(event.target.value as PeriodPreset)}>
            <option value="30d">Poslednjih 30 dana</option>
            <option value="90d">Poslednjih 90 dana</option>
            <option value="180d">Poslednjih 180 dana</option>
            <option value="365d">Poslednjih 365 dana</option>
            <option value="custom">Prilagođeno</option>
          </select>
        </label>

        <label>
          <span>Od</span>
          <input
            type="date"
            value={fromDate}
            onChange={(event) => {
              setPeriodPreset("custom");
              setFromDate(event.target.value);
            }}
          />
        </label>

        <label>
          <span>Do</span>
          <input
            type="date"
            value={toDate}
            onChange={(event) => {
              setPeriodPreset("custom");
              setToDate(event.target.value);
            }}
          />
        </label>

        <label>
          <span>Objekat</span>
          <select
            value={storeId ?? ""}
            onChange={(event) => setStoreId(event.target.value ? Number(event.target.value) : null)}
          >
            <option value="">Svi objekti</option>
            {stores.map((store) => (
              <option key={store.storeId} value={store.storeId}>
                {buildStoreLabel(store)}
              </option>
            ))}
          </select>
        </label>

        <label>
          <span>Top dobavljača</span>
          <input
            type="number"
            min={1}
            max={25}
            value={topN}
            onChange={(event) => setTopN(parseTopN(event.target.value))}
          />
        </label>

        <div className="daily-sales-actions">
          <button type="button" onClick={handleApplyFilters} disabled={loading}>
            Primeni
          </button>
          <button type="button" className="secondary" onClick={handleResetFilters} disabled={loading}>
            Reset
          </button>
        </div>
      </section>

      {invalidRange ? (
        <div className="daily-sales-message error">Datum 'od' ne može biti posle datuma 'do'.</div>
      ) : null}
      {error ? <div className="daily-sales-message error">{error}</div> : null}
      {loading ? (
        <div className="daily-sales-message loading">
          <UltraSpinner size="sm" label="Loading daily sales data" className="daily-sales-inline-spinner" />
          <span>Učitavam dnevne podatke...</span>
        </div>
      ) : null}

      {!loading && data ? (
        <>
          <section className="daily-sales-kpis">
            <article>
              <span>Ukupan prihod <InfoTip text="Suma prihoda od prodaje za sve dane u izabranom opsegu i prodavnici. Prihod / dan je prosek po broju kalendarskih dana u opsegu." /></span>
              <strong>{fmtRsd(currentSummary.totalRevenue, 2)}</strong>
              <small>{fmtRsdShort(currentSummary.avgRevenuePerDay)} / dan</small>
            </article>
            <article>
              <span>Ukupno komada <InfoTip text="Ukupan broj prodatih komada vidljivih u tabeli. Moze biti manji od baze ako je primenjen filter na prodavnicu ili top-N dobavljaca." /></span>
              <strong>{fmtNumber(currentSummary.totalVisibleItems)}</strong>
              <small>{fmtNumber(Math.round(currentSummary.avgItemsPerDay))} / dan</small>
            </article>
            <article>
              <span>Dana u opsegu <InfoTip text="Broj kalendarskih dana izabranog perioda. Koristi se kao imenilac za dnevne proseke. Prethodni period je isti opseg, pomeren unazad." /></span>
              <strong>{fmtNumber(data.metadata.totalDays)}</strong>
              <small>Prethodni period: {fmtDateShort(previousRange.fromDate)} - {fmtDateShort(previousRange.toDate)}</small>
            </article>
            <article>
              <span>RSD po komadu <InfoTip text="Prosecna prodajna cena po komadu: Ukupan prihod / Ukupno komada. Bazira se na vidljivim komadima u tabeli, ne na svim transakcijama." /></span>
              <strong>{fmtRsd(currentSummary.avgRevenuePerItem, 2)}</strong>
              <small>Na osnovu vidljivih komada u tabeli</small>
            </article>
            <article>
              <span>Prva smena <InfoTip text="Udeo komada prodatih u prvoj smeni (06:00–13:59) u odnosu na ukupne smenske komade (prva + druga). Dani bez razdvajanja po smenama nisu ukljuceni u ovaj procenat." /></span>
              <strong>{fmtPct(currentSummary.firstShiftSharePct, 1)}</strong>
              <small>{fmtNumber(currentSummary.firstShiftItems)} komada</small>
            </article>
            <article>
              <span>Druga smena <InfoTip text="Udeo komada prodatih u drugoj smeni (14:00–21:59) u odnosu na ukupne smenske komade. Komplementarno sa Prvom smenom." /></span>
              <strong>{fmtPct(currentSummary.secondShiftSharePct, 1)}</strong>
              <small>{fmtNumber(currentSummary.secondShiftItems)} komada</small>
            </article>
            <article>
              <span>Udeo top 3 dob. <InfoTip text="Procenat komada koje nose tri dobavljaca sa najvecim prometom u opsegu. Formula: (top 3 dobavljaci) / ukupni komadi × 100. Visoka vrednost = visoka zavisnost od malog broja dobavljaca." /></span>
              <strong>{fmtPct(supplierConcentration.top3QtySharePct, 1)}</strong>
              <small>Udeo top 3 dobavljača po komadima</small>
            </article>
          </section>

          <section className="daily-sales-table-card">
            <div className="daily-sales-table-head">
              <div>
                <h2>Tabela po danima</h2>
                <p>
                  Top dobavljači su određeni globalno za izabrani opseg, a kolone prikazuju dnevne komade.
                </p>
              </div>
              <AnalyticsTableToolbar
                tableKey="daily-sales-stats"
                tableTitle="Dnevna prodaja po smeni i dobavljačima"
                columns={toolbarColumns}
                rows={sortedRows}
                printRows={printRows}
                filters={toolbarFilters}
                metadata={toolbarMetadata}
                defaultOrientation="portrait"
                documentType="daily-sales-filled"
                extraActions={(
                  <button
                    type="button"
                    onClick={handlePrintBlank}
                    className="inline-flex items-center gap-2 rounded-xl border border-border bg-surface px-3 py-2 text-xs font-semibold text-muted"
                    title="Otvori prazan obrazac za ručno popunjavanje"
                  >
                    Štampaj obrazac
                  </button>
                )}
              />
            </div>

            <div className="daily-sales-table-wrap">
              <table className="daily-sales-table">
                <thead>
                  <tr>
                    <th className="col-date">
                      <button type="button" onClick={() => handleSort("date")}>
                        Datum{sortMarker("date", sortKey, sortDir)}
                      </button>
                    </th>
                    <th className="align-right">
                      <button type="button" onClick={() => handleSort("firstShiftTotalItems")}>
                        Prva smena{sortMarker("firstShiftTotalItems", sortKey, sortDir)}{" "}
                        <InfoTip text="Suma komada prodatih od 06:00 do 13:59." />
                      </button>
                    </th>
                    <th className="align-right">
                      <button type="button" onClick={() => handleSort("secondShiftTotalItems")}>
                        Druga smena{sortMarker("secondShiftTotalItems", sortKey, sortDir)}{" "}
                        <InfoTip text="Suma komada prodatih od 14:00 do 21:59." />
                      </button>
                    </th>
                    <th className="align-right">
                      <button type="button" onClick={() => handleSort("totalRevenue")}>
                        Prihod dana{sortMarker("totalRevenue", sortKey, sortDir)}
                      </button>
                    </th>
                    {supplierHeaders.map((name, index) => {
                      const displayName = sortedRows.length === 0 ? "" : name;
                      return (
                        <th key={`supplier-header-${index}`} className="align-right">
                          <button type="button" onClick={() => handleSort(`supplier:${index}`)}>
                            {displayName}{sortMarker(`supplier:${index}`, sortKey, sortDir)}
                          </button>
                        </th>
                      );
                    })}
                    <th className="align-right">
                      <button type="button" onClick={() => handleSort("othersCount")}>
                        Ostali{sortMarker("othersCount", sortKey, sortDir)}{" "}
                        <InfoTip text="Komadi dobavljača koji nisu u top N listi za izabrani opseg." />
                      </button>
                    </th>
                    <th className="align-right">
                      <button type="button" onClick={() => handleSort("totalItemsSold")}>
                        Ukupno kom{sortMarker("totalItemsSold", sortKey, sortDir)}
                      </button>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {sortedRows.length === 0 ? (
                    <tr>
                      <td colSpan={7 + supplierHeaders.length} className="daily-sales-empty-row">
                        Nema podataka za izabrane filtere.
                      </td>
                    </tr>
                  ) : (
                    sortedRows.map((row) => {
                      const supplierTotal = sum(row.topSupplierCounts) + row.othersCount;
                      const mismatch = supplierTotal !== row.totalItemsSold;
                      return (
                        <tr key={row.date} className={mismatch ? "row-mismatch" : ""}>
                          <td>{fmtDate(row.date)}</td>
                          <td className="align-right">{shiftDisplayValue(row, "first")}</td>
                          <td className="align-right">{shiftDisplayValue(row, "second")}</td>
                          <td className="align-right">{fmtRsd(row.totalRevenue, 2)}</td>
                          {supplierHeaders.map((_, index) => (
                            <td key={`${row.date}-supplier-${index}`} className="align-right">
                              {fmtNumber(row.topSupplierCounts[index] ?? 0)}
                            </td>
                          ))}
                          <td className="align-right">{fmtNumber(row.othersCount)}</td>
                          <td className="align-right">
                            {fmtNumber(row.totalItemsSold)}
                            {mismatch ? <span className="mismatch-badge">Check</span> : null}
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>
            {mismatchCount > 0 ? (
              <p className="daily-sales-footnote">
                Upozorenje: {mismatchCount} redova ima mismatch između total kolone i top+others sabiranja.
              </p>
            ) : null}
          </section>

          {data.metadata.totalItemsInRange === 0 && data.metadata.minAvailableDate ? (
            <section className="daily-sales-no-data-banner">
              <p>
                Nema prodaje u izabranom periodu. Podaci su dostupni od{" "}
                <strong>{fmtDate(data.metadata.minAvailableDate)}</strong> do{" "}
                <strong>{fmtDate(data.metadata.maxAvailableDate!)}</strong>.
              </p>
              <button type="button" onClick={handleJumpToAvailableData}>
                Prikazi dostupne podatke
              </button>
            </section>
          ) : data.metadata.warnings.length > 0 ? (
            <section className="daily-sales-warnings">
              {data.metadata.warnings.map((warning) => (
                <p key={warning}>{warning}</p>
              ))}
            </section>
          ) : null}

          <section className="daily-sales-section-grid daily-sales-section-grid--double">
            <article className="daily-sales-panel">
              <div className="daily-sales-panel-head">
                <div>
                  <h2 className="with-tip">
                    <span>Poređenje sa prethodnim periodom</span>
                    <InfoTip text="Trenutni opseg se poredi sa prethodnim periodom istog trajanja." />
                  </h2>
                  <p>
                    Trenutni opseg: {fmtDateShort(activeFilters.fromDate)} - {fmtDateShort(activeFilters.toDate)}.
                    Prethodni opseg: {fmtDateShort(previousRange.fromDate)} - {fmtDateShort(previousRange.toDate)}.
                  </p>
                </div>
              </div>

              <div className="daily-sales-compare-cards">
                {comparisonCards.map((card) => (
                  <article key={card.key} className="daily-sales-compare-card" data-tone={comparisonTone(card.deltaPct)}>
                    <span>{card.label}</span>
                    <strong>{card.formatter(card.currentValue)}</strong>
                    <small>Prethodno: {card.formatter(card.previousValue)}</small>
                    <div className="daily-sales-delta">{fmtDelta(card.deltaPct, card.currentValue, card.previousValue)}</div>
                  </article>
                ))}
              </div>
            </article>

            <article className="daily-sales-panel daily-sales-panel--quality">
              <div className="daily-sales-panel-head">
                <div>
                  <h2 className="with-tip">
                    <span>Kvalitet podataka</span>
                    <InfoTip text="Signali koji utiču na pouzdanost odluka u ovom periodu. Nepoznati dobavljač: prodaja bez mapiranog dobavljača. Dani nepodudaranja: zbir po dobavljačima ne odgovara dnevnom totalu. Dani bez satnice: nema pouzdanog smenskog razdvajanja. Dupli/neusklađeni računi: neregularnosti u kasi. Visoke vrednosti na bilo kom signalu = zadržite oprez pri interpretaciji trendova." />
                  </h2>
                  <p>Dijagnosticki sloj — bitno samo ako planirate dublje analize pouzdanosti.</p>
                </div>
                <button
                  type="button"
                  className={`daily-sales-health-badge daily-sales-health-badge--${dataHealthSummary.tone}`}
                  onClick={() => setQualityPanelOpen((prev) => !prev)}
                  aria-expanded={qualityPanelOpen}
                  title={qualityPanelOpen ? "Sakrij detalje kvaliteta" : "Prikaži detalje kvaliteta"}
                >
                  {dataHealthSummary.label}
                  <span className="daily-sales-health-caret">{qualityPanelOpen ? "▲" : "▼"}</span>
                </button>
              </div>

              {qualityPanelOpen ? (
                <div className="daily-sales-quality-grid">
                  {qualitySignals.map((signal) => (
                    <article key={signal.key} className="daily-sales-quality-card" data-tone={signal.tone}>
                      <span>{signal.label}</span>
                      <strong>{signal.value}</strong>
                      <small>{signal.description}</small>
                    </article>
                  ))}
                </div>
              ) : null}
            </article>
          </section>

          <section className="daily-sales-panel">
            <div className="daily-sales-panel-head">
              <div>
                <h2 className="with-tip">
                  <span>Trend prihoda i komada</span>
                  <InfoTip text="Dnevni trend sa 7-dnevnim pokretnim prosekom (MA7) za prihod i komade. Pokretni prosek gladi kratkorocne oscilacije i otkriva stvarni pravac kretanja. Dobar za detekciju pozitivnog ili negativnog momenta i nestabilnosti prodaje." />
                </h2>
                <p>Koristi 7d prosek da odvojis stvarni trend od dnevnog suma.</p>
              </div>
            </div>

            <div className="daily-sales-chart-wrap">
              <ResponsiveContainer width="100%" height={360}>
                <LineChart data={trendData} margin={{ top: 8, right: 18, left: 4, bottom: 8 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke={CHART_GRID_STROKE} />
                  <XAxis dataKey="label" tick={CHART_AXIS_TICK} interval={chartTickInterval} />
                  <YAxis yAxisId="revenue" tick={CHART_AXIS_TICK} tickFormatter={(value: number) => fmtCompactNumber(value)} />
                  <YAxis yAxisId="items" orientation="right" tick={CHART_AXIS_TICK} tickFormatter={(value: number) => fmtCompactNumber(value)} />
                  <Tooltip
                    contentStyle={CHART_TOOLTIP_STYLE}
                    labelStyle={CHART_TOOLTIP_LABEL_STYLE}
                    labelFormatter={(_, payload) => payload?.[0]?.payload?.fullLabel ?? ""}
                    formatter={(value: number | string | undefined, name: string | undefined) => {
                      const seriesName = name ?? "";
                      const numericValue = typeof value === "number" ? value : Number(value ?? 0);
                      if (seriesName === "Prihod" || seriesName === "Prihod MA7") {
                        return [fmtRsdShort(numericValue), seriesName];
                      }
                      return [fmtNumber(Math.round(numericValue)), seriesName];
                    }}
                  />
                  <Legend wrapperStyle={CHART_LEGEND_STYLE} />
                  <Line yAxisId="revenue" type="monotone" dataKey="totalRevenue" name="Prihod" stroke="var(--accent-primary)" strokeWidth={2.5} dot={false} />
                  <Line yAxisId="revenue" type="monotone" dataKey="ma7Revenue" name="Prihod MA7" stroke="var(--accent-warning)" strokeWidth={2} dot={false} strokeDasharray="6 4" />
                  <Line yAxisId="items" type="monotone" dataKey="totalItemsSold" name="Komadi" stroke="var(--accent-success)" strokeWidth={2.5} dot={false} />
                  <Line yAxisId="items" type="monotone" dataKey="ma7Items" name="Komadi MA7" stroke="var(--accent-info, #44d0ff)" strokeWidth={2} dot={false} strokeDasharray="4 4" />
                </LineChart>
              </ResponsiveContainer>
            </div>
          </section>

          <section className="daily-sales-section-grid daily-sales-section-grid--analytics">
            <article className="daily-sales-panel">
              <div className="daily-sales-panel-head">
                <div>
                  <h2 className="with-tip">
                    <span>Smenski miks po danima</span>
                    <InfoTip text="Stacked bar pokazuje raspodelu komada po smenama po danima. Koristi za staffing i dopunu robe." />
                  </h2>
                  <p>Prva smena: {FIRST_SHIFT_LABEL}. Druga smena: {SECOND_SHIFT_LABEL}.</p>
                </div>
              </div>

              <div className="daily-sales-chart-wrap">
                <ResponsiveContainer width="100%" height={320}>
                  <BarChart data={shiftMixData} margin={{ top: 8, right: 12, left: 0, bottom: 8 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke={CHART_GRID_STROKE} />
                    <XAxis dataKey="label" tick={CHART_AXIS_TICK} interval={chartTickInterval} />
                    <YAxis tick={CHART_AXIS_TICK} tickFormatter={(value: number) => fmtCompactNumber(value)} />
                    <Tooltip
                      contentStyle={CHART_TOOLTIP_STYLE}
                      labelStyle={CHART_TOOLTIP_LABEL_STYLE}
                      labelFormatter={(_, payload) => payload?.[0]?.payload?.fullLabel ?? ""}
                      formatter={(value: number | string | undefined, name: string | undefined) => [fmtNumber(Number(value ?? 0)), name ?? ""]}
                    />
                    <Legend wrapperStyle={CHART_LEGEND_STYLE} />
                    <Bar dataKey="firstShiftTotalItems" name="Prva smena" stackId="shift" fill="var(--accent-primary)" radius={[4, 4, 0, 0]} />
                    <Bar dataKey="secondShiftTotalItems" name="Druga smena" stackId="shift" fill="var(--accent-success)" radius={[4, 4, 0, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              </div>

              <div className="daily-sales-mini-stats">
                <div>
                  <span>Prva smena</span>
                  <strong>{fmtPct(currentSummary.firstShiftSharePct, 1)}</strong>
                </div>
                <div>
                  <span>Druga smena</span>
                  <strong>{fmtPct(currentSummary.secondShiftSharePct, 1)}</strong>
                </div>
                <div>
                  <span>Dani bez satnice</span>
                  <strong>{fmtNumber(missingShiftCount)}</strong>
                </div>
              </div>
            </article>

            <article className="daily-sales-panel">
              <div className="daily-sales-panel-head">
                <div>
                  <h2 className="with-tip">
                    <span>Koncentracija dobavljača</span>
                    <InfoTip text="Koliki deo prodaje nose vodeći dobavljači i koliko dobavljača treba za 80% komada." />
                  </h2>
                  <p>Pareto pogled za procenu zavisnosti od nekoliko dobavljača.</p>
                </div>
              </div>

              <div className="daily-sales-chart-wrap">
                <ResponsiveContainer width="100%" height={320}>
                  <BarChart data={supplierConcentration.displayChartData} layout="vertical" margin={{ top: 8, right: 18, left: 8, bottom: 8 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke={CHART_GRID_STROKE} />
                    <XAxis type="number" tick={CHART_AXIS_TICK} tickFormatter={(value: number) => `${Math.round(value)}%`} />
                    <YAxis type="category" dataKey="displayName" width={120} tick={CHART_AXIS_TICK} />
                    <Tooltip
                      contentStyle={CHART_TOOLTIP_STYLE}
                      labelStyle={CHART_TOOLTIP_LABEL_STYLE}
                      formatter={(value: number | string | undefined, name: string | undefined, payload) => {
                        const seriesName = name ?? "";
                        const row = payload?.payload as SupplierConcentrationPoint | undefined;
                        const numericValue = typeof value === "number" ? value : Number(value ?? 0);
                        if (seriesName === "Udeo komada") return [fmtPct(numericValue, 1), seriesName];
                        return [row ? fmtNumber(row.totalQty) : fmtNumber(numericValue), seriesName];
                      }}
                    />
                    <Legend wrapperStyle={CHART_LEGEND_STYLE} />
                    <Bar dataKey="qtySharePct" name="Udeo komada" fill="var(--accent-primary)" radius={[0, 4, 4, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              </div>

              <div className="daily-sales-mini-stats">
                <div>
                  <span>Top 3 share</span>
                  <strong>{fmtPct(supplierConcentration.top3QtySharePct, 1)}</strong>
                </div>
                <div>
                  <span>Top 5 share</span>
                  <strong>{fmtPct(supplierConcentration.top5QtySharePct, 1)}</strong>
                </div>
                <div>
                  <span>Dobavljaca za 80%</span>
                  <strong>{supplierConcentration.suppliersTo80Pct > 0 ? fmtNumber(supplierConcentration.suppliersTo80Pct) : "N/A"}</strong>
                </div>
              </div>
            </article>

            <article className="daily-sales-panel">
              <div className="daily-sales-panel-head">
                <div>
                  <h2 className="with-tip">
                    <span>Obrazac po danu u nedelji</span>
                    <InfoTip text="Prosecan prihod i komadi po danu u nedelji. Koristi za raspored tima, cilj po danu i dopunu." />
                  </h2>
                  <p>
                    Najbolji prodajni dan: <strong>{dayPatternSummary.strongestDay?.dayName ?? "N/A"}</strong>.
                    Najmirniji radni dan: <strong>{dayPatternSummary.weakestDay?.dayName ?? "N/A"}</strong>.
                  </p>
                </div>
              </div>

              <div className="daily-sales-chart-wrap">
                <ResponsiveContainer width="100%" height={320}>
                  <ComposedChart data={weekdayData} margin={{ top: 8, right: 18, left: 0, bottom: 8 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke={CHART_GRID_STROKE} />
                    <XAxis dataKey="dayName" tick={CHART_AXIS_TICK} />
                    <YAxis yAxisId="revenue" tick={CHART_AXIS_TICK} tickFormatter={(value: number) => fmtCompactNumber(value)} />
                    <YAxis yAxisId="items" orientation="right" tick={CHART_AXIS_TICK} tickFormatter={(value: number) => fmtCompactNumber(value)} />
                    <Tooltip
                      contentStyle={CHART_TOOLTIP_STYLE}
                      labelStyle={CHART_TOOLTIP_LABEL_STYLE}
                      formatter={(value: number | string | undefined, name: string | undefined) => {
                        const seriesName = name ?? "";
                        const numericValue = typeof value === "number" ? value : Number(value ?? 0);
                        if (seriesName === "Avg prihod") return [fmtRsdShort(numericValue), seriesName];
                        return [fmtNumber(Math.round(numericValue)), seriesName];
                      }}
                    />
                    <Legend wrapperStyle={CHART_LEGEND_STYLE} />
                    <Bar yAxisId="revenue" dataKey="avgRevenue" name="Avg prihod" fill="var(--accent-warning)" radius={[4, 4, 0, 0]} />
                    <Line yAxisId="items" type="monotone" dataKey="avgItems" name="Avg komadi" stroke="var(--accent-info, #44d0ff)" strokeWidth={2.5} dot={{ r: 3 }} />
                  </ComposedChart>
                </ResponsiveContainer>
              </div>

              <div className="daily-sales-mini-stats">
                {weekdayData.map((row) => (
                  <div key={row.dayName}>
                    <span>{row.dayName}</span>
                    <strong>{fmtRsdShort(row.avgRevenue)}</strong>
                  </div>
                ))}
              </div>
            </article>

            <article className="daily-sales-panel">
              <div className="daily-sales-panel-head">
                <div>
                  <h2 className="with-tip">
                    <span>Heuristicki signali i anomalije</span>
                    <InfoTip text="Pregledni heuristicki signali i nekoliko dana koji najvise odstupaju od 7d proseka. Ovo nije recommendation status model." />
                  </h2>
                  <p>Brz pregled gde treba dodatna analiza, bez kopanja po celoj tabeli.</p>
                </div>
              </div>

              <div className="daily-sales-insight-list">
                {heuristicInsights.map((insight, index) => (
                  <article key={`${insight.title}-${index}`} className="daily-sales-insight-card" data-tone={insight.tone}>
                    <strong>{insight.title}</strong>
                    <p>{insight.detail}</p>
                  </article>
                ))}
              </div>

              <div className="daily-sales-anomaly-summary">
                <div>
                  <span>Najbolji dan</span>
                  <strong>{bestRevenueDay ? `${fmtDate(bestRevenueDay.date)} | ${fmtRsdShort(bestRevenueDay.totalRevenue)}` : "N/A"}</strong>
                </div>
                <div>
                  <span>Najslabiji dan</span>
                  <strong>{weakestRevenueDay ? `${fmtDate(weakestRevenueDay.date)} | ${fmtRsdShort(weakestRevenueDay.totalRevenue)}` : "N/A"}</strong>
                </div>
                <div>
                  <span>Najveci skok</span>
                  <strong>{biggestJump ? `${biggestJump.label} | ${fmtSignedPct(biggestJump.revenueDeltaPct, 1)}` : "N/A"}</strong>
                </div>
                <div>
                  <span>Najveci pad</span>
                  <strong>{biggestDrop ? `${biggestDrop.label} | ${fmtSignedPct(biggestDrop.revenueDeltaPct, 1)}` : "N/A"}</strong>
                </div>
              </div>

              <div className="daily-sales-anomaly-table">
                <div className="daily-sales-anomaly-head">
                  <span>Datum</span>
                  <span>Prihod</span>
                  <span>Komadi</span>
                  <span>Odstupanje vs MA7</span>
                </div>
                {anomalyRows.map((row) => (
                  <div key={row.date} className="daily-sales-anomaly-row">
                    <span>{row.label}</span>
                    <span>{fmtRsdShort(row.revenue)}</span>
                    <span>{fmtNumber(row.items)}</span>
                    <span className={row.deviationValue >= 0 ? "trend-up" : "trend-down"}>{fmtSignedPct(row.deviationPct, 1)}</span>
                  </div>
                ))}
              </div>
            </article>
          </section>

        </>
      ) : null}
    </div>
  );
}

import { Fragment, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router-dom";
import AnalyticsEmptyState from "../components/analytics/AnalyticsEmptyState";
import AnalyticsErrorState from "../components/analytics/AnalyticsErrorState";
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import AnalyticsTrustHeader from "../components/analytics/AnalyticsTrustHeader";
import InfoTip from "../components/ui/InfoTip";
import {
  AnalyticsMetaError,
  getAnalyticsActions,
  getProductDecisionCenter,
  getStores,
  getSupplierFilters,
  upsertAnalyticsAction,
} from "../services/analyticsApi";
import {
  fmtNumber,
  fmtPct,
  fmtRsd,
} from "../utils/analyticsFormatters";
import {
  getAnalyticsMetaMessage,
  isAnalyticsMetaInsufficient,
  isAnalyticsMetaWarning,
  shouldShowAnalyticsEmptyState,
} from "../utils/analyticsResponseMeta";
import { analyticsMetricDescriptions } from "../utils/analyticsMetricDescriptions";
import type {
  AnalyticsActionStatus,
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
  | "confidencePct"
  | "recommendationStatus"
  | "dataQualityStatus";

type SortDir = "asc" | "desc";
type RecommendationFilter = "all" | ProductDecisionRecommendationStatus;
type DataQualityFilter = "all" | "good" | "warning" | "critical" | "insufficient_data";
type PeriodPreset = "last30" | "last60" | "last90" | "custom";

const RECOMMENDATION_LABELS: Record<ProductDecisionRecommendationStatus, string> = {
  BOOST: "Pojacaj",
  REPLENISH: "Dopuni",
  WATCH: "Prati",
  MARKDOWN: "Snizi cenu",
  DO_NOT_ORDER: "Ne narucivati",
  FIX_DATA: "Proveriti podatke",
  INSUFFICIENT_DATA: "Nedovoljno podataka",
};

const RECOMMENDATION_OPTIONS: Array<{ value: RecommendationFilter; label: string }> = [
  { value: "all", label: "Sve preporuke" },
  { value: "REPLENISH", label: "Dopuni" },
  { value: "BOOST", label: "Pojacaj" },
  { value: "WATCH", label: "Prati" },
  { value: "MARKDOWN", label: "Snizi cenu" },
  { value: "DO_NOT_ORDER", label: "Ne narucivati" },
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
  critical: "Kritican",
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
  poor_margin: "Marza je ispod zeljenog nivoa.",
  stale_stock: "Artikal dugo nema prodaju.",
  missing_cost: "Nedostaje nabavna cena.",
  missing_supplier: "Nedostaje dobavljač.",
  insufficient_history: "Nema dovoljno istorije za sigurnu preporuku.",
  replenish_needed: "Potrebna je dopuna da bi se izbegao gubitak prodaje.",
  high_stock_risk: "Postoji rizik od viska zalihe.",
  data_quality_blocker: "Kvalitet podataka blokira pouzdanu preporuku.",
};

const TABLE_COLUMNS: AnalyticsTableColumn<ProductDecisionCenterItem>[] = [
  { key: "productName", header: "Artikal", dataType: "text" },
  { key: "supplierName", header: "Dobavljač", dataType: "text" },
  { key: "revenue", header: "Prodaja / komadi", dataType: "currency" },
  { key: "velocityUnitsPerDay", header: "Velocity", dataType: "number" },
  { key: "marginPct", header: "Marza", dataType: "percent" },
  { key: "currentStock", header: "Zaliha", dataType: "number" },
  { key: "trendPct", header: "Trend", dataType: "percent" },
  { key: "confidencePct", header: "Confidence", dataType: "number" },
  { key: "dataQualityStatus", header: "Data quality", dataType: "text" },
  { key: "recommendationLabel", header: "Preporuka", dataType: "text" },
];

const OPEN_ACTION_STATUSES: AnalyticsActionStatus[] = ["new", "accepted", "deferred"];

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

function buildSupplierDecisionUrl(supplierId: number): string {
  return `/analytics/supplier?supplierId=${supplierId}`;
}

function buildInventoryDecisionUrl(row: ProductDecisionCenterItem): string {
  const params = new URLSearchParams();
  if (row.sku) params.set("sku", row.sku);
  params.set("productId", String(row.productId));
  const query = params.toString();
  return query ? `/analytics/inventory?${query}` : "/analytics/inventory";
}

function buildSourceKey(
  row: ProductDecisionCenterItem,
  fromDate: string,
  toDate: string,
  storeId: number | null,
  supplierId: number | null,
): string {
  return `product:${row.productId}:${row.recommendationStatus}:${fromDate}:${toDate}:${storeId ?? "all"}:${supplierId ?? "all"}`;
}

function recommendationActionTitle(status: ProductDecisionRecommendationStatus, productName: string): string {
  if (status === "REPLENISH") return `Dopuni: ${productName}`;
  if (status === "BOOST") return `Pojacaj: ${productName}`;
  if (status === "MARKDOWN") return `Snizi: ${productName}`;
  if (status === "DO_NOT_ORDER") return `Ne narucuj: ${productName}`;
  if (status === "FIX_DATA") return `Proveri podatke: ${productName}`;
  if (status === "WATCH") return `Prati: ${productName}`;
  return `Proveri: ${productName}`;
}

function mapActionPriority(row: ProductDecisionCenterItem): "P1" | "P2" | "P3" {
  const dataQuality = canonicalDataQualityStatus(row.dataQualityStatus);
  const hasCriticalOos = row.recommendationStatus === "REPLENISH" && row.stockGap > 0 && row.currentStock <= 0;
  const hasLargeLostSales = row.lostSalesEstimate >= 100_000;
  const hasCriticalDataIssue = row.recommendationStatus === "FIX_DATA" && dataQuality === "critical";

  if (hasCriticalOos || hasLargeLostSales || hasCriticalDataIssue) return "P1";
  if (row.recommendationStatus === "WATCH" || row.recommendationStatus === "INSUFFICIENT_DATA") return "P3";
  return "P2";
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

  useEffect(() => {
    let cancelled = false;
    // UX choice: clear queued keys on sourceKey-defining filter change
    // to avoid temporary false-positive "U akcijama" badges for the previous context.
    setQueuedActionKeys(new Set());
    (async () => {
      try {
        const responses = await Promise.all(
          OPEN_ACTION_STATUSES.map((status) => getAnalyticsActions({
            sourceType: "product",
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
  }, [fromDate, toDate, storeId, supplierId]);

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
        const message = reason instanceof Error ? reason.message : "Greška pri ucitavanju Product Decision Center podataka.";
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

  const rows = payload?.rows ?? [];
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
  const hasBlockingError = Boolean(error && !payload);
  const showMetaWarning = !loading && !hasBlockingError && isAnalyticsMetaWarning(responseMeta);
  const showInsufficientState = !loading
    && !hasBlockingError
    && shouldShowAnalyticsEmptyState(responseMeta, rows.length)
    && isAnalyticsMetaInsufficient(responseMeta);
  const showNoDataState = !loading && !hasBlockingError && !showInsufficientState && rows.length === 0;
  const showFilteredOutState = !loading && !hasBlockingError && !showInsufficientState && rows.length > 0 && sortedRows.length === 0;

  const kpis = useMemo(() => ({
    replenishCount: rows.filter((x) => x.recommendationStatus === "REPLENISH").length,
    boostCount: rows.filter((x) => x.recommendationStatus === "BOOST").length,
    markdownCount: rows.filter((x) => x.recommendationStatus === "MARKDOWN").length,
    doNotOrderCount: rows.filter((x) => x.recommendationStatus === "DO_NOT_ORDER").length,
    fixDataCount: rows.filter((x) => x.recommendationStatus === "FIX_DATA").length,
    lostSalesEstimate: payload?.summary.lostSalesEstimate ?? 0,
    slowStockCapital: payload?.summary.slowStockCapital ?? 0,
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
    { key: "dataQualityFilter", label: "Data quality", value: dataQualityFilter },
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

  const addRowToCentralActions = useCallback(async (row: ProductDecisionCenterItem) => {
    const sourceKey = buildSourceKey(row, fromDate, toDate, storeId, supplierId);
    const alreadyQueued = queuedActionKeys.has(sourceKey);

    setQueueBusyKey(sourceKey);
    setQueueMessage(null);
    try {
      const reasonText = row.recommendationReason;

      const action = await upsertAnalyticsAction({
        sourceType: "product",
        sourceKey,
        sourceId: row.productId,
        title: recommendationActionTitle(row.recommendationStatus, row.productName),
        description: reasonText,
        recommendationStatus: row.recommendationStatus,
        priority: mapActionPriority(row),
        impactEstimateRsd: row.lostSalesEstimate > 0 ? row.lostSalesEstimate : undefined,
        confidencePct: row.confidencePct,
        reliabilityPct: row.reliabilityPct ?? undefined,
        dataQualityStatus: canonicalDataQualityStatus(row.dataQualityStatus),
        actionUrl: "/analytics/products",
        metadataJson: JSON.stringify({
          productId: row.productId,
          sku: row.sku,
          supplierId: row.supplierId ?? null,
          recommendationStatus: row.recommendationStatus,
          periodFrom: fromDate,
          periodTo: toDate,
          storeId: storeId ?? "all",
          supplierFilterId: supplierId ?? "all",
        }),
      });

      setQueuedActionKeys((prev) => {
        const next = new Set(prev);
        next.add(sourceKey);
        if (action.sourceKey) next.add(action.sourceKey);
        return next;
      });
      setQueueMessage(alreadyQueued ? "Akcija je vec u centralnom redu." : "Akcija dodata u centralni red.");
    } catch (reason) {
      setQueueMessage(reason instanceof Error ? reason.message : "Dodavanje akcije nije uspelo.");
    } finally {
      setQueueBusyKey(null);
    }
  }, [fromDate, queuedActionKeys, storeId, supplierId, toDate]);

  return (
    <section className="product-decision-page">
      <AnalyticsTrustHeader
        title="Odluke o proizvodima"
        description="Jedan ekran za dopunu, pojačanje, praćenje, sniženje, zaustavljanje narudžbine i proveru podataka."
        periodFrom={payload?.periodFromUtc ?? fromDate}
        periodTo={payload?.periodToUtc ?? toDate}
        lastRefreshAt={payload?.generatedAtUtc ?? null}
        dataSource="Product decision snapshot"
        dataQualityStatus={responseMeta?.dataQualityStatus ?? null}
        dataQualitySummary={trustQualitySummary}
        mode="recommendation"
        isPartial={isAnalyticsMetaWarning(responseMeta)}
        recommendationNote="Finalni recommendation status dolazi iz backend decision engine-a."
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
          <p>Jedan ekran za dopunu, pojačanje, praćenje, sniženje, zaustavljanje narudžbine i proveru podataka.</p>
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
        </article>
        <article className="kpi-card">
          <span>Za pojačanje</span>
          <strong>{fmtNumber(kpis.boostCount, 0, "0")}</strong>
        </article>
        <article className="kpi-card">
          <span>Za sniženje</span>
          <strong>{fmtNumber(kpis.markdownCount, 0, "0")}</strong>
        </article>
        <article className="kpi-card">
          <span>Ne narucivati</span>
          <strong>{fmtNumber(kpis.doNotOrderCount, 0, "0")}</strong>
        </article>
        <article className="kpi-card">
          <span>Proveriti podatke</span>
          <strong>{fmtNumber(kpis.fixDataCount, 0, "0")}</strong>
        </article>
        <article className="kpi-card">
          <span>Procena izgubljene prodaje</span>
          <strong>{fmtRsd(kpis.lostSalesEstimate, 0, "N/A")}</strong>
        </article>
        <article className="kpi-card">
          <span>Kapital u sporoj zalihi</span>
          <strong>{fmtRsd(kpis.slowStockCapital, 0, "N/A")}</strong>
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
              <option value="custom">Custom</option>
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
            Data quality
            <select value={dataQualityFilter} onChange={(event) => setDataQualityFilter(event.target.value as DataQualityFilter)}>
              <option value="all">Sve</option>
              <option value="good">Dobar</option>
              <option value="warning">Upozorenje</option>
              <option value="critical">Kritican</option>
              <option value="insufficient_data">Nedovoljno podataka</option>
            </select>
          </label>
          <label>
            Sort
            <select value={`${sortField}:${sortDir}`} onChange={(event) => {
              const [nextField, nextDir] = event.target.value.split(":");
              setSortField(nextField as SortField);
              setSortDir(nextDir as SortDir);
            }}>
              <option value="recommendationStatus:desc">Preporuka (prioritet)</option>
              <option value="confidencePct:desc">Confidence opadajuce</option>
              <option value="revenue:desc">Promet opadajuce</option>
              <option value="velocityUnitsPerDay:desc">Velocity opadajuce</option>
              <option value="trendPct:desc">Trend opadajuce</option>
              <option value="dataQualityStatus:desc">Data quality (kriticno prvo)</option>
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
          Prikazujemo prethodno ucitane podatke. Novi upit nije uspeo.
        </div>
      ) : null}
      {loading ? <div className="product-decision-message">Ucitavanje Product Decision Center podataka...</div> : null}
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
            "U periodu nema dovoljno prodajnih događaja za recommendation signal.",
            "Filteri su previše uski (prodavnica/dobavljač).",
            "Nedostaju ključni ulazi (nabavna cena, dobavljač).",
          ]}
          actions={[
            { label: "Proširite period (npr. 60 ili 90 dana)." },
            { label: "Uklonite uske filtere i pokušajte ponovo." },
            { label: "Otvorite Data Quality i proverite blokere signala.", href: "/analytics/data-quality" },
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
            "Nije bilo prodaje u trazenom periodu.",
            "Izabrani period je preuzak.",
            "Analytics osvežavanje jos nije zavrseno.",
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
            "Pretraga, recommendation filter ili data quality filter su previše restriktivni.",
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
                <th onClick={() => setSort("velocityUnitsPerDay")}>Velocity</th>
                <th onClick={() => setSort("marginPct")}>Marza</th>
                <th onClick={() => setSort("currentStock")}>Zaliha</th>
                <th onClick={() => setSort("trendPct")}>Trend</th>
                <th onClick={() => setSort("confidencePct")}>Confidence</th>
                <th onClick={() => setSort("dataQualityStatus")}>Data quality</th>
                <th onClick={() => setSort("recommendationStatus")}>Preporuka</th>
                <th>Akcija</th>
              </tr>
            </thead>
            <tbody>
              {sortedRows.map((row) => {
                  const expanded = expandedProductId === row.productId;
                  const sourceKey = buildSourceKey(row, fromDate, toDate, storeId, supplierId);
                  const isQueued = queuedActionKeys.has(sourceKey);
                  const isQueueBusy = queueBusyKey === sourceKey;
                  const dataQuality = canonicalDataQualityStatus(row.dataQualityStatus);
                  const reasonCodeItems = row.reasonCodes.length
                    ? row.reasonCodes.map((code) => ({ code, message: translateReasonCode(code) }))
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
                          <small>{row.marginQualityLabel ?? "N/A"} | pokrice: {fmtPct(row.marginCoveragePct, 1)}</small>
                        </td>
                        <td>
                          <span>{fmtNumber(row.currentStock, 0, "0")}</span>
                          <small>min: {fmtNumber(row.minStock, 0, "0")} | gap: {fmtNumber(row.stockGap, 0, "0")}</small>
                        </td>
                        <td>{fmtPct(row.trendPct, 1)}</td>
                        <td>
                          <span>{fmtNumber(row.confidencePct, 0, "N/A")}%</span>
                          <small>Reliability: {row.reliabilityPct != null ? `${fmtNumber(row.reliabilityPct, 0, "N/A")}%` : "N/A"}</small>
                        </td>
                        <td>
                          <span className={dataQualityClass(dataQuality)}>{DATA_QUALITY_LABELS[dataQuality]}</span>
                        </td>
                        <td>
                          <span className={recommendationToneClass(row.recommendationStatus)}>
                            {row.recommendationLabel}
                          </span>
                          <button
                            type="button"
                            className="why-button"
                            onClick={(event) => {
                              event.stopPropagation();
                              toggleExpandedRow(row.productId);
                            }}
                            title={row.recommendationReason}
                          >
                            Zasto?
                          </button>
                        </td>
                        <td>
                          <span>{row.recommendedAction}</span>
                          <small>{fmtRsd(row.lostSalesEstimate, 0, "N/A")} potencijalnog uticaja</small>
                          <button
                            type="button"
                            className={`btn-add-to-queue${isQueued ? " added" : ""}`}
                            onClick={(event) => {
                              event.stopPropagation();
                              void addRowToCentralActions(row);
                            }}
                            disabled={isQueueBusy || isQueued}
                            title={isQueued ? "Akcija je vec u centralnom redu." : "Dodaj u centralni red akcija"}
                          >
                            {isQueueBusy ? "Dodavanje..." : isQueued ? "U akcijama" : "Dodaj u akcije"}
                          </button>
                        </td>
                      </tr>
                      {expanded ? (
                        <tr className="reason-row">
                          <td colSpan={11}>
                            <div className="reason-content reason-content-expanded">
                              <div className="reason-headline">
                                <div>
                                  <h4>{row.productName}</h4>
                                  <p>{row.supplierName ?? "Dobavljač nije dodeljen"}</p>
                                </div>
                                <div className="reason-statuses">
                                  <span className={recommendationToneClass(row.recommendationStatus)}>
                                    {row.recommendationLabel} ({row.recommendationStatus})
                                  </span>
                                  <span className={dataQualityClass(dataQuality)}>
                                    {DATA_QUALITY_LABELS[dataQuality]}
                                  </span>
                                  <span className="confidence-badge">Confidence: {fmtNumber(row.confidencePct, 0, "N/A")}%</span>
                                </div>
                              </div>

                              <div className="reason-block">
                                <strong>Razlog:</strong> {row.recommendationReason || "Razlog nije dostupan."}
                              </div>

                              <div className="reason-block">
                                <strong>Reason codes:</strong>
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
                                  <span> Nema reason code vrednosti. Koristi se samo tekst razloga.</span>
                                )}
                              </div>

                              <div className="reason-metric-grid">
                                <div><strong>Revenue:</strong> {fmtRsd(row.revenue, 0, "N/A")}</div>
                                <div><strong>Units sold:</strong> {fmtNumber(row.unitsSold, 0, "0")}</div>
                                <div><strong>Velocity:</strong> {fmtNumber(row.velocityUnitsPerDay, 2, "N/A")}</div>
                                <div><strong>Marza:</strong> {fmtPct(row.marginPct, 1)}</div>
                                <div><strong>Margin contribution:</strong> {fmtRsd(row.marginContribution, 0, "N/A")}</div>
                                <div><strong>Current stock:</strong> {fmtNumber(row.currentStock, 0, "0")}</div>
                                <div><strong>Days since last sale:</strong> {row.daysSinceLastSale != null ? `${fmtNumber(row.daysSinceLastSale, 0, "0")} dana` : "N/A"}</div>
                                <div><strong>Trend:</strong> {fmtPct(row.trendPct, 1)}</div>
                                <div><strong>Lost sales estimate:</strong> {fmtRsd(row.lostSalesEstimate, 0, "N/A")}</div>
                                <div><strong>Slow stock capital:</strong> {fmtRsd(row.slowStockCapital, 0, "N/A")}</div>
                                <div><strong>Cost coverage:</strong> {fmtPct(row.marginCoveragePct, 1)}</div>
                                <div><strong>Reliability:</strong> {row.reliabilityPct != null ? `${fmtNumber(row.reliabilityPct, 0, "N/A")}%` : "N/A"}</div>
                                <div><strong>Data quality:</strong> {DATA_QUALITY_LABELS[dataQuality]}</div>
                              </div>

                              <div className="reason-actions">
                                <button
                                  type="button"
                                  className={`btn-add-to-queue${isQueued ? " added" : ""}`}
                                  disabled={isQueueBusy || isQueued}
                                  onClick={() => void addRowToCentralActions(row)}
                                  title={isQueued ? "Akcija je vec u centralnom redu." : "Dodaj u centralni red akcija"}
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



import { Fragment, useCallback, useEffect, useMemo, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import InfoTip from "../components/ui/InfoTip";
import {
  getProductDecisionCenter,
  getStores,
  getSupplierFilters,
  upsertAnalyticsAction,
} from "../services/analyticsApi";
import type {
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
  | "recommendationStatus";

type SortDir = "asc" | "desc";
type StockFilter = "all" | "low" | "balanced" | "high";
type DataQualityFilter = "all" | "good" | "warning" | "critical";

const RECOMMENDATION_OPTIONS: Array<{ value: "all" | ProductDecisionRecommendationStatus; label: string }> = [
  { value: "all", label: "Sve preporuke" },
  { value: "BOOST", label: "Pojačaj" },
  { value: "REPLENISH", label: "Dopuni" },
  { value: "WATCH", label: "Prati" },
  { value: "MARKDOWN", label: "Snizi cenu" },
  { value: "DO_NOT_ORDER", label: "Ne naručuj" },
  { value: "FIX_DATA", label: "Proveri podatke" },
  { value: "INSUFFICIENT_DATA", label: "Nedovoljno podataka" },
];

const TABLE_COLUMNS: AnalyticsTableColumn<ProductDecisionCenterItem>[] = [
  { key: "sku", header: "Šifra", dataType: "text" },
  { key: "productName", header: "Artikal", dataType: "text" },
  { key: "supplierName", header: "Dobavljač", dataType: "text" },
  { key: "revenue", header: "Promet", dataType: "currency" },
  { key: "unitsSold", header: "Komadi", dataType: "number" },
  { key: "velocityUnitsPerDay", header: "Velocity", dataType: "number" },
  { key: "marginPct", header: "Marža %", dataType: "percent" },
  { key: "currentStock", header: "Zaliha", dataType: "number" },
  { key: "trendPct", header: "Trend %", dataType: "percent" },
  { key: "confidencePct", header: "Confidence %", dataType: "number" },
  { key: "recommendationLabel", header: "Preporuka", dataType: "text" },
  { key: "recommendedAction", header: "Akcija", dataType: "text" },
];

function toDateInputValue(date: Date): string {
  return date.toISOString().slice(0, 10);
}

function defaultPeriodRange() {
  const to = new Date();
  const from = new Date(to);
  from.setDate(from.getDate() - 29);
  return { fromDate: toDateInputValue(from), toDate: toDateInputValue(to) };
}

function applyPeriodPreset(preset: "last30" | "last60" | "last90") {
  const to = new Date();
  const from = new Date(to);
  if (preset === "last60") from.setDate(from.getDate() - 59);
  else if (preset === "last90") from.setDate(from.getDate() - 89);
  else from.setDate(from.getDate() - 29);
  return { fromDate: toDateInputValue(from), toDate: toDateInputValue(to) };
}

function formatCurrency(value: number | null | undefined): string {
  const safe = Number.isFinite(value ?? NaN) ? Number(value) : 0;
  return `${safe.toLocaleString("sr-RS", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} RSD`;
}

function formatNumber(value: number | null | undefined): string {
  const safe = Number.isFinite(value ?? NaN) ? Number(value) : 0;
  return safe.toLocaleString("sr-RS");
}

function formatPercent(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return "N/A";
  return `${Number(value).toLocaleString("sr-RS", { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`;
}

function recommendationPriority(status: ProductDecisionRecommendationStatus): number {
  if (status === "FIX_DATA") return 7;
  if (status === "BOOST") return 6;
  if (status === "REPLENISH") return 5;
  if (status === "MARKDOWN") return 4;
  if (status === "DO_NOT_ORDER") return 3;
  if (status === "WATCH") return 2;
  return 1;
}

function stockStatusOf(row: ProductDecisionCenterItem): StockFilter {
  if (row.stockGap > 0) return "low";
  if (row.currentStock > Math.max(row.minStock * 3, row.minStock + 10)) return "high";
  return "balanced";
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

function toCanonicalDataQualityStatus(
  value: string | null | undefined,
): "good" | "warning" | "critical" | "insufficient_data" | undefined {
  if (!value) return undefined;
  const lower = value.toLowerCase();
  if (lower === "fair") return "warning";
  if (lower === "poor") return "critical";
  if (lower === "good" || lower === "warning" || lower === "critical" || lower === "insufficient_data") return lower;
  return undefined;
}

export default function ProductDecisionCenterPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const initialRange = useMemo(() => defaultPeriodRange(), []);

  const [periodPreset, setPeriodPreset] = useState<"last30" | "last60" | "last90" | "custom">("last30");
  const [fromDate, setFromDate] = useState(initialRange.fromDate);
  const [toDate, setToDate] = useState(initialRange.toDate);
  const [storeId, setStoreId] = useState<number | null>(null);
  const [supplierId, setSupplierId] = useState<number | null>(null);
  const [recommendationFilter, setRecommendationFilter] = useState<"all" | ProductDecisionRecommendationStatus>("all");
  const [stockFilter, setStockFilter] = useState<StockFilter>("all");
  const [dataQualityFilter, setDataQualityFilter] = useState<DataQualityFilter>("all");
  const [search, setSearch] = useState("");
  const [sortField, setSortField] = useState<SortField>("recommendationStatus");
  const [sortDir, setSortDir] = useState<SortDir>("desc");
  const [expandedProductId, setExpandedProductId] = useState<number | null>(null);

  const [stores, setStores] = useState<StoreOption[]>([]);
  const [suppliers, setSuppliers] = useState<SupplierFilterOption[]>([]);
  const [payload, setPayload] = useState<ProductDecisionCenterResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [addedToQueueIds, setAddedToQueueIds] = useState<Set<number>>(new Set());

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
    try {
      const response = await getProductDecisionCenter({
        fromDate,
        toDate,
        storeId,
        supplierId,
        top: 1200,
      });
      setPayload(response);
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : "Greška pri učitavanju Product Decision Center podataka.";
      setError(message);
      setPayload(null);
    } finally {
      setLoading(false);
    }
  }, [fromDate, toDate, storeId, supplierId]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const rows = payload?.rows ?? [];

  const filteredRows = useMemo(() => {
    const normalizedSearch = search.trim().toLowerCase();
    return rows.filter((row) => {
      if (recommendationFilter !== "all" && row.recommendationStatus !== recommendationFilter) return false;
      if (stockFilter !== "all" && stockStatusOf(row) !== stockFilter) return false;
      if (dataQualityFilter !== "all" && row.dataQualityStatus !== dataQualityFilter) return false;
      if (!normalizedSearch) return true;
      const text = `${row.productName} ${row.sku} ${row.supplierName ?? ""}`.toLowerCase();
      return text.includes(normalizedSearch);
    });
  }, [rows, recommendationFilter, stockFilter, dataQualityFilter, search]);

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
      else diff = recommendationPriority(a.recommendationStatus) - recommendationPriority(b.recommendationStatus);
      return sortDir === "asc" ? diff : -diff;
    });
    return copy;
  }, [filteredRows, sortDir, sortField]);

  const kpis = useMemo(() => {
    const replenishCount = sortedRows.filter((x) => x.recommendationStatus === "REPLENISH").length;
    const markdownCount = sortedRows.filter((x) => x.recommendationStatus === "MARKDOWN").length;
    const highPotentialCount = sortedRows.filter((x) => x.recommendationStatus === "BOOST").length;
    const badDataCount = sortedRows.filter((x) => x.recommendationStatus === "FIX_DATA").length;
    const lostSalesEstimate = sortedRows.reduce((sum, x) => sum + x.lostSalesEstimate, 0);
    const slowStockCapital = payload?.summary.slowStockCapital ?? 0;
    return {
      replenishCount,
      markdownCount,
      highPotentialCount,
      badDataCount,
      lostSalesEstimate,
      slowStockCapital,
    };
  }, [payload?.summary.slowStockCapital, sortedRows]);

  const tableFilters = useMemo<AnalyticsNamedValue[]>(() => [
    { key: "fromDate", label: "Od datuma", value: fromDate },
    { key: "toDate", label: "Do datuma", value: toDate },
    { key: "storeId", label: "Prodavnica", value: storeId ?? "Sve" },
    { key: "supplierId", label: "Dobavljač", value: supplierId ?? "Svi" },
    { key: "recommendationFilter", label: "Preporuka", value: recommendationFilter },
    { key: "stockFilter", label: "Status zalihe", value: stockFilter },
    { key: "dataQualityFilter", label: "Data quality", value: dataQualityFilter },
    { key: "search", label: "Pretraga", value: search || "—" },
  ], [dataQualityFilter, fromDate, recommendationFilter, search, stockFilter, storeId, supplierId, toDate]);

  const tableMetadata = useMemo<AnalyticsNamedValue[]>(() => [
    { key: "generatedAtUtc", label: "Generisano", value: payload?.generatedAtUtc ?? "N/A" },
    { key: "totalRows", label: "Ukupno redova", value: payload?.totalRows ?? 0 },
    { key: "filteredRows", label: "Prikazano redova", value: sortedRows.length },
  ], [payload?.generatedAtUtc, payload?.totalRows, sortedRows.length]);

  const handlePeriodPresetChange = (value: "last30" | "last60" | "last90" | "custom") => {
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

  const openDetail = (row: ProductDecisionCenterItem) => {
    const params = new URLSearchParams();
    params.set("fromDate", fromDate);
    params.set("toDate", toDate);
    if (storeId != null) params.set("storeId", String(storeId));
    if (supplierId != null) params.set("supplierId", String(supplierId));
    navigate(`/analitika/top-products-advanced/${row.productId}?${params.toString()}`, {
      state: { backgroundLocation: location },
    });
  };

  return (
    <section className="product-decision-page">
      <header className="product-decision-header">
        <div>
          <h1>Product Decision Center</h1>
          <p>Jedan ekran za odluke: dopuni, pojačaj, prati, snizi, ne naručuj ili proveri podatke.</p>
        </div>
        <AnalyticsTableToolbar
          tableKey="product-decision-center"
          tableTitle="Product Decision Center"
          columns={TABLE_COLUMNS}
          rows={sortedRows}
          filters={tableFilters}
          metadata={tableMetadata}
        />
      </header>

      <section className="product-decision-kpis" aria-label="KPI kartice">
        <article className="kpi-card">
          <span>Artikala za dopunu</span>
          <strong>{formatNumber(kpis.replenishCount)}</strong>
        </article>
        <article className="kpi-card">
          <span>Artikala za sniženje</span>
          <strong>{formatNumber(kpis.markdownCount)}</strong>
        </article>
        <article className="kpi-card">
          <span>Artikala sa visokim potencijalom</span>
          <strong>{formatNumber(kpis.highPotentialCount)}</strong>
        </article>
        <article className="kpi-card">
          <span>Artikala sa lošim podacima</span>
          <strong>{formatNumber(kpis.badDataCount)}</strong>
        </article>
        <article className="kpi-card">
          <span>Procena izgubljene prodaje</span>
          <strong>{formatCurrency(kpis.lostSalesEstimate)}</strong>
        </article>
        <article className="kpi-card">
          <span>Kapital u sporoj zalihi</span>
          <strong>{formatCurrency(kpis.slowStockCapital)}</strong>
        </article>
      </section>

      <section className="product-decision-filters">
        <div className="filter-grid">
          <label>
            Period
            <select value={periodPreset} onChange={(event) => handlePeriodPresetChange(event.target.value as "last30" | "last60" | "last90" | "custom")}>
              <option value="last30">Poslednjih 30 dana</option>
              <option value="last60">Poslednjih 60 dana</option>
              <option value="last90">Poslednjih 90 dana</option>
              <option value="custom">Custom</option>
            </select>
          </label>
          <label>
            Od datuma
            <input type="date" value={fromDate} onChange={(event) => {
              setFromDate(event.target.value);
              setPeriodPreset("custom");
            }} />
          </label>
          <label>
            Do datuma
            <input type="date" value={toDate} onChange={(event) => {
              setToDate(event.target.value);
              setPeriodPreset("custom");
            }} />
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
            <select value={recommendationFilter} onChange={(event) => setRecommendationFilter(event.target.value as "all" | ProductDecisionRecommendationStatus)}>
              {RECOMMENDATION_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>
          <label>
            Status zalihe
            <select value={stockFilter} onChange={(event) => setStockFilter(event.target.value as StockFilter)}>
              <option value="all">Sve</option>
              <option value="low">Niska</option>
              <option value="balanced">Balans</option>
              <option value="high">Visoka</option>
            </select>
          </label>
          <label>
            Data quality
            <select value={dataQualityFilter} onChange={(event) => setDataQualityFilter(event.target.value as DataQualityFilter)}>
              <option value="all">Sve</option>
              <option value="good">Good</option>
              <option value="warning">Warning</option>
              <option value="critical">Critical</option>
            </select>
          </label>
          <label>
            Pretraga (naziv/šifra)
            <input
              type="search"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="npr. Air, 45123..."
            />
          </label>
        </div>
      </section>

      {loading ? <div className="product-decision-message">Učitavanje Product Decision Center podataka...</div> : null}
      {error ? <div className="product-decision-message product-decision-message-error">{error}</div> : null}

      {!loading && !error ? (
        <div className="product-decision-table-wrap">
          <table className="product-decision-table">
            <thead>
              <tr>
                <th onClick={() => setSort("productName")}>Artikal</th>
                <th onClick={() => setSort("supplierName")}>Dobavljač</th>
                <th onClick={() => setSort("revenue")}>Promet</th>
                <th onClick={() => setSort("unitsSold")}>Komadi</th>
                <th onClick={() => setSort("velocityUnitsPerDay")}>Velocity</th>
                <th onClick={() => setSort("marginPct")}>Marža</th>
                <th onClick={() => setSort("currentStock")}>Zaliha</th>
                <th onClick={() => setSort("trendPct")}>Trend</th>
                <th onClick={() => setSort("confidencePct")}>Confidence</th>
                <th onClick={() => setSort("recommendationStatus")}>Preporuka</th>
                <th>Akcija</th>
              </tr>
            </thead>
            <tbody>
              {sortedRows.length === 0 ? (
                <tr>
                  <td colSpan={11} className="empty-cell">Nema podataka za izabrane filtere.</td>
                </tr>
              ) : (
                sortedRows.map((row) => {
                  const expanded = expandedProductId === row.productId;
                  return (
                    <Fragment key={row.productId}>
                      <tr onClick={() => openDetail(row)} className="data-row">
                        <td>
                          <strong>{row.productName}</strong>
                          <small>{row.sku || "N/A"} | {row.category ?? row.tipObuce ?? "N/A"}</small>
                        </td>
                        <td>{row.supplierName ?? "N/A"}</td>
                        <td>{formatCurrency(row.revenue)}</td>
                        <td>{formatNumber(row.unitsSold)}</td>
                        <td>{row.velocityUnitsPerDay.toLocaleString("sr-RS", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
                        <td>
                          <span>{formatPercent(row.marginPct)}</span>
                          <small>{row.marginQualityLabel ?? "N/A"} ({formatPercent(row.marginCoveragePct)})</small>
                        </td>
                        <td>
                          <span>{formatNumber(row.currentStock)}</span>
                          <small>min: {formatNumber(row.minStock)} | gap: {formatNumber(row.stockGap)}</small>
                        </td>
                        <td>{formatPercent(row.trendPct)}</td>
                        <td>{formatNumber(row.confidencePct)}</td>
                        <td>
                          <span className={recommendationToneClass(row.recommendationStatus)}>
                            {row.recommendationLabel}
                          </span>
                          <button
                            type="button"
                            className="why-button"
                            onClick={(event) => {
                              event.stopPropagation();
                              setExpandedProductId((current) => current === row.productId ? null : row.productId);
                            }}
                            title={row.recommendationReason}
                          >
                            Zašto?
                          </button>
                        </td>
                        <td>
                          <span>{row.recommendedAction}</span>
                          <small>Data quality: {row.dataQualityStatus}</small>
                          <button
                            type="button"
                            className={`btn-add-to-queue${addedToQueueIds.has(row.productId) ? " added" : ""}`}
                            title="Dodaj u centralni red akcija"
                            onClick={async (e) => {
                              e.stopPropagation();
                              try {
                                await upsertAnalyticsAction({
                                  sourceType: "product",
                                  sourceKey: `product_${row.productId}_${row.recommendationStatus}`,
                                  sourceId: row.productId,
                                  title: `${row.productName} — ${row.recommendationLabel ?? row.recommendationStatus}`,
                                  description: row.recommendationReason,
                                  recommendationStatus: row.recommendationStatus,
                                  priority: row.confidencePct != null && row.confidencePct >= 80 ? "P1" : row.confidencePct != null && row.confidencePct >= 50 ? "P2" : "P3",
                                  impactEstimateRsd: row.revenue ?? undefined,
                                  confidencePct: row.confidencePct ?? undefined,
                                  dataQualityStatus: toCanonicalDataQualityStatus(row.dataQualityStatus),
                                  actionUrl: `/analitika/top-products-advanced/${row.productId}`,
                                });
                                setAddedToQueueIds((prev) => new Set([...prev, row.productId]));
                              } catch {
                                // silently ignore
                              }
                            }}
                          >
                            {addedToQueueIds.has(row.productId) ? "✓ Dodato" : "+ Dodaj u akcije"}
                          </button>
                        </td>
                      </tr>
                      {expanded ? (
                        <tr className="reason-row">
                          <td colSpan={11}>
                            <div className="reason-content">
                              <strong>Obrazloženje preporuke:</strong> {row.recommendationReason}
                              <span>
                                <InfoTip text="Confidence pokazuje pouzdanost odluke na osnovu pokrivenosti podacima i stabilnosti signala." />
                              </span>
                            </div>
                          </td>
                        </tr>
                      ) : null}
                    </Fragment>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      ) : null}
    </section>
  );
}

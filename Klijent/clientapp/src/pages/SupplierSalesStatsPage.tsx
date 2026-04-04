import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import {
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { getStores } from "../services/analyticsApi";
import {
  getSupplierSalesStats,
  type SupplierSalesStat,
  type SupplierSalesStatsResponse,
} from "../services/supplierSalesStatsApi";
import type { StoreOption } from "../types/analytics";
import AnalyticsUnknownLink from "../components/analytics/AnalyticsUnknownLink";
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import { buildAnalyticsDetailSnapshot, saveAnalyticsDetailSnapshot } from "../services/analyticsTableState";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import "./SupplierSalesStatsPage.css";

type PeriodPreset = "30d" | "90d" | "custom";
type SortDir = "asc" | "desc";
type SortField = "dobavljacNaziv" | "ukupanPromet" | "sharePct" | "marginContribution" | "trendPct" | "status";
type DecisionStatus = "Pojacaj" | "Zadrzi" | "Smanji";

type ActiveFilters = {
  fromDate: string;
  toDate: string;
  storeId: number | null;
};

type DecisionSupplier = SupplierSalesStat & {
  sharePct: number;
  marginContribution: number;
  trendPct: number | null;
  reliabilityPct: number;
  coveragePct: number;
  decisionScore: number;
  status: DecisionStatus;
  statusReason: string;
};

const STATUS_PRIORITY: Record<DecisionStatus, number> = {
  Pojacaj: 3,
  Zadrzi: 2,
  Smanji: 1,
};

const UNKNOWN_SUPPLIERS = new Set([
  "",
  "NEPOZNATO",
  "NEPOZNAT DOBAVLJAC",
  "UNKNOWN SUPPLIER",
]);

const decisionColumns: AnalyticsTableColumn<DecisionSupplier>[] = [
  { key: "dobavljacNaziv", header: "Dobavljac", dataType: "text" },
  { key: "ukupanPromet", header: "Promet", dataType: "currency" },
  { key: "sharePct", header: "Udeo %", dataType: "percent" },
  { key: "marginContribution", header: "Marzni doprinos", dataType: "currency" },
  { key: "trendPct", header: "Trend %", dataType: "percent" },
  { key: "status", header: "Preporuka", dataType: "text" },
  { key: "decisionScore", header: "Decision score", dataType: "number" },
];

function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, value));
}

function toDateInput(date: Date): string {
  return date.toISOString().slice(0, 10);
}

function getPresetRange(preset: Exclude<PeriodPreset, "custom">): { fromDate: string; toDate: string } {
  const to = new Date();
  const from = new Date(to);
  if (preset === "30d") from.setDate(from.getDate() - 29);
  if (preset === "90d") from.setDate(from.getDate() - 89);

  return {
    fromDate: toDateInput(from),
    toDate: toDateInput(to),
  };
}

function toUtcRange(fromDate: string, toDate: string): { fromDate: string; toDate: string } {
  return {
    fromDate: `${fromDate}T00:00:00Z`,
    toDate: `${toDate}T23:59:59Z`,
  };
}

function buildPreviousRange(fromDate: string, toDate: string): { fromDate: string; toDate: string } {
  const currentFrom = new Date(`${fromDate}T00:00:00Z`);
  const currentTo = new Date(`${toDate}T23:59:59Z`);
  const durationMs = currentTo.getTime() - currentFrom.getTime() + 1000;

  const previousTo = new Date(currentFrom.getTime() - 1000);
  const previousFrom = new Date(previousTo.getTime() - durationMs + 1000);

  return {
    fromDate: previousFrom.toISOString(),
    toDate: previousTo.toISOString(),
  };
}

function fmtRsd(value: number): string {
  return `${value.toLocaleString("sr-RS", { minimumFractionDigits: 0, maximumFractionDigits: 0 })} RSD`;
}

function fmtPct(value: number | null | undefined, digits = 1): string {
  if (value == null || Number.isNaN(value)) return "N/A";
  return `${value.toLocaleString("sr-RS", { minimumFractionDigits: digits, maximumFractionDigits: digits })}%`;
}

function fmtSignedPct(value: number | null | undefined, digits = 1): string {
  if (value == null || Number.isNaN(value)) return "N/A";
  const sign = value > 0 ? "+" : "";
  return `${sign}${fmtPct(value, digits)}`;
}

function fmtQty(value: number): string {
  return `${value.toLocaleString("sr-RS")} kom`;
}

function sortMarker(field: SortField, activeField: SortField, dir: SortDir): string {
  if (field !== activeField) return "";
  return dir === "asc" ? " ^" : " v";
}

function statusClass(status: DecisionStatus): string {
  if (status === "Pojacaj") return "supplier-decision-status status-boost";
  if (status === "Smanji") return "supplier-decision-status status-reduce";
  return "supplier-decision-status status-keep";
}

function trendClass(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return "trend-neutral";
  if (value > 0) return "trend-up";
  if (value < 0) return "trend-down";
  return "trend-neutral";
}

type StatusReasonSignals = {
  trendPct: number | null;
  marginPct: number;
  avgMargin: number;
  reliabilityPct: number;
};

type StatusTooltipData = {
  status: DecisionStatus;
  statusReason: string;
  sharePct: number;
  marginPct: number;
  trendPct: number | null;
  reliabilityPct: number;
};

function buildStatusReason(status: DecisionStatus, signals: StatusReasonSignals): string {
  const lowReliability = signals.reliabilityPct < 35;
  const positiveTrend = (signals.trendPct ?? 0) > 0;
  const negativeTrend = (signals.trendPct ?? 0) < 0;
  const strongMargin = signals.marginPct >= signals.avgMargin;

  if (status === "Pojacaj") {
    if (lowReliability) return "Signal je dobar, ali je pouzdanost niska; potvrditi pre veceg ulaganja.";
    if (positiveTrend && strongMargin) return "Jak promet, zdrava marza i rastuci trend.";
    if (positiveTrend) return "Dobar promet i pozitivan trend; kandidat za veci fokus.";
    return "Stabilan doprinos i solidna marza; opravdan fokus u nabavci.";
  }

  if (status === "Zadrzi") {
    if (lowReliability) return "Niza pouzdanost podataka; odluku drzati konzervativnom dok se signal ne stabilizuje.";
    if (negativeTrend && !strongMargin) return "Trend slabi i marza je ispod proseka; zadrzati uz pojacan nadzor.";
    return "Stabilan rezultat bez dovoljno jakog signala za promenu prioriteta.";
  }

  if (negativeTrend) return "Pad trenda uz nizak doprinos; smanjiti fokus i rasteretiti asortiman.";
  return "Nizak doprinos bez jasnog potencijala rasta; kandidat za smanjenje fokusa.";
}

function buildStatusTooltip(data: StatusTooltipData): string {
  return `${data.status}: ${data.statusReason} | Udeo ${fmtPct(data.sharePct, 1)} | Marza ${fmtPct(data.marginPct, 1)} | Trend ${fmtSignedPct(data.trendPct, 1)} | Pouzdanost ${fmtPct(data.reliabilityPct, 0)}`;
}

function normalizeName(value: string | null | undefined): string {
  return (value ?? "").trim().toUpperCase();
}

function buildStoreLabel(store: StoreOption): string {
  const extras = [store.city, store.region].filter(Boolean).join(", ");
  return extras ? `${store.storeName} (${extras})` : store.storeName;
}

function supplierKey(supplier: { dobavljacId: number | null; dobavljacNaziv: string }): string {
  if (supplier.dobavljacId != null) return `id:${supplier.dobavljacId}`;
  return `name:${normalizeName(supplier.dobavljacNaziv)}`;
}

export default function SupplierSalesStatsPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const requestIdRef = useRef(0);

  const initialRange = useMemo(() => getPresetRange("90d"), []);
  const [periodPreset, setPeriodPreset] = useState<PeriodPreset>("90d");
  const [fromDate, setFromDate] = useState(initialRange.fromDate);
  const [toDate, setToDate] = useState(initialRange.toDate);
  const [storeId, setStoreId] = useState<number | null>(null);
  const [activeFilters, setActiveFilters] = useState<ActiveFilters>({
    fromDate: initialRange.fromDate,
    toDate: initialRange.toDate,
    storeId: null,
  });

  const [stores, setStores] = useState<StoreOption[]>([]);
  const [data, setData] = useState<SupplierSalesStatsResponse | null>(null);
  const [previousPeriodRevenue, setPreviousPeriodRevenue] = useState<number | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sortField, setSortField] = useState<SortField>("status");
  const [sortDir, setSortDir] = useState<SortDir>("desc");
  const [expandedSupplierKey, setExpandedSupplierKey] = useState<string | null>(null);

  const invalidRange = useMemo(() => {
    if (!fromDate || !toDate) return false;
    return new Date(fromDate) > new Date(toDate);
  }, [fromDate, toDate]);

  useEffect(() => {
    const loadStores = async () => {
      try {
        const items = await getStores(true);
        setStores(items);
      } catch {
        setStores([]);
      }
    };

    void loadStores();
  }, []);

  const load = useCallback(async (filters: ActiveFilters) => {
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);
    setPreviousPeriodRevenue(null);

    try {
      const currentRange = toUtcRange(filters.fromDate, filters.toDate);

      // Load current period first so the page renders immediately.
      const currentResult = await getSupplierSalesStats({
        ...currentRange,
        storeId: filters.storeId,
      });

      if (requestId !== requestIdRef.current) return;
      setData(currentResult);
      setLoading(false);

      // Load previous period in the background — non-blocking for initial render.
      const previousRange = buildPreviousRange(filters.fromDate, filters.toDate);
      try {
        const previousResult = await getSupplierSalesStats({
          ...previousRange,
          storeId: filters.storeId,
        });
        if (requestId !== requestIdRef.current) return;
        setPreviousPeriodRevenue(previousResult.totals.ukupanPromet);
      } catch {
        if (requestId === requestIdRef.current) setPreviousPeriodRevenue(null);
      }
    } catch (reason) {
      if (requestId !== requestIdRef.current) return;
      setData(null);
      setPreviousPeriodRevenue(null);
      setLoading(false);
      setError(reason instanceof Error ? reason.message : "Greska pri ucitavanju podataka o dobavljacima.");
    }
  }, []);

  useEffect(() => {
    void load(activeFilters);
  }, [activeFilters, load]);

  const decisionSuppliers = useMemo<DecisionSupplier[]>(() => {
    const suppliers = data?.suppliers ?? [];
    if (suppliers.length === 0) return [];

    const totalRevenue = suppliers.reduce((sum, item) => sum + item.ukupanPromet, 0);
    const topShare = suppliers.reduce((max, item) => {
      const share = totalRevenue > 0 ? (item.ukupanPromet / totalRevenue) * 100 : 0;
      return Math.max(max, share);
    }, 0);

    const marginValues = suppliers.map((item) => item.marginPct);
    const minMargin = Math.min(...marginValues);
    const maxMargin = Math.max(...marginValues);
    const marginSpan = maxMargin - minMargin;
    const avgMargin = marginValues.reduce((sum, value) => sum + value, 0) / marginValues.length;

    return suppliers.map((supplier) => {
      const sharePct = totalRevenue > 0 ? (supplier.ukupanPromet / totalRevenue) * 100 : 0;
      const marginContribution = supplier.ukupanPromet * (supplier.marginPct / 100);
      const trendPct = supplier.promenaPrometa;
      const coveragePct = supplier.brojArtikalaUkupno > 0
        ? (supplier.brojArtikalaSaNivelacijom / supplier.brojArtikalaUkupno) * 100
        : 0;

      const knownSupplier = !UNKNOWN_SUPPLIERS.has(normalizeName(supplier.dobavljacNaziv));
      const reliabilityPct = clamp(coveragePct * 0.8 + (knownSupplier ? 20 : 0), 0, 100);

      const shareNorm = topShare > 0 ? clamp((sharePct / topShare) * 100, 0, 100) : 0;
      const marginNorm = marginSpan > 0
        ? clamp(((supplier.marginPct - minMargin) / marginSpan) * 100, 0, 100)
        : 50;
      const trendNorm = trendPct == null ? 50 : clamp(((trendPct + 30) / 60) * 100, 0, 100);

      const decisionScore = Math.round(
        shareNorm * 0.35 +
        marginNorm * 0.30 +
        trendNorm * 0.20 +
        reliabilityPct * 0.15
      );

      let status: DecisionStatus = "Smanji";
      if (decisionScore >= 70) status = "Pojacaj";
      else if (decisionScore >= 45) status = "Zadrzi";

      if (reliabilityPct < 35 && status === "Pojacaj") {
        status = "Zadrzi";
      }

      const statusReason = buildStatusReason(status, {
        trendPct,
        marginPct: supplier.marginPct,
        avgMargin,
        reliabilityPct,
      });

      return {
        ...supplier,
        sharePct,
        marginContribution,
        trendPct,
        reliabilityPct,
        coveragePct,
        decisionScore,
        status,
        statusReason,
      };
    });
  }, [data?.suppliers]);

  const sortedSuppliers = useMemo(() => {
    const rows = [...decisionSuppliers];
    return rows.sort((a, b) => {
      let compare = 0;

      if (sortField === "dobavljacNaziv") {
        compare = a.dobavljacNaziv.localeCompare(b.dobavljacNaziv, "sr");
      } else if (sortField === "ukupanPromet") {
        compare = a.ukupanPromet - b.ukupanPromet;
      } else if (sortField === "sharePct") {
        compare = a.sharePct - b.sharePct;
      } else if (sortField === "marginContribution") {
        compare = a.marginContribution - b.marginContribution;
      } else if (sortField === "trendPct") {
        compare = (a.trendPct ?? -9999) - (b.trendPct ?? -9999);
      } else if (sortField === "status") {
        compare = STATUS_PRIORITY[a.status] - STATUS_PRIORITY[b.status];
      }

      if (compare === 0) {
        compare = a.decisionScore - b.decisionScore;
      }

      if (compare === 0) {
        compare = a.ukupanPromet - b.ukupanPromet;
      }

      return sortDir === "asc" ? compare : -compare;
    });
  }, [decisionSuppliers, sortDir, sortField]);

  const selectedSupplier = useMemo(
    () => sortedSuppliers.find((row) => supplierKey(row) === expandedSupplierKey) ?? null,
    [expandedSupplierKey, sortedSuppliers]
  );

  useEffect(() => {
    if (!selectedSupplier && sortedSuppliers.length > 0 && expandedSupplierKey != null) {
      setExpandedSupplierKey(null);
    }
  }, [expandedSupplierKey, selectedSupplier, sortedSuppliers.length]);

  const totalRevenue = data?.totals.ukupanPromet ?? 0;
  const top5SharePct = useMemo(() => {
    if (sortedSuppliers.length === 0 || totalRevenue <= 0) return 0;
    const top5Revenue = [...sortedSuppliers]
      .sort((a, b) => b.ukupanPromet - a.ukupanPromet)
      .slice(0, 5)
      .reduce((sum, row) => sum + row.ukupanPromet, 0);
    return (top5Revenue / totalRevenue) * 100;
  }, [sortedSuppliers, totalRevenue]);

  const totalMarginContribution = useMemo(
    () => sortedSuppliers.reduce((sum, row) => sum + row.marginContribution, 0),
    [sortedSuppliers]
  );

  const periodGrowthPct = useMemo(() => {
    if (previousPeriodRevenue == null || previousPeriodRevenue <= 0) return null;
    return ((totalRevenue - previousPeriodRevenue) / previousPeriodRevenue) * 100;
  }, [previousPeriodRevenue, totalRevenue]);

  const concentrationData = useMemo(() => {
    if (sortedSuppliers.length === 0) return [] as Array<{ name: string; sharePct: number }>;

    const ranked = [...sortedSuppliers]
      .sort((a, b) => b.sharePct - a.sharePct);

    const topRows = ranked.slice(0, 6).map((row) => ({
      name: row.dobavljacNaziv,
      sharePct: Number(row.sharePct.toFixed(2)),
    }));

    const remaining = ranked.slice(6).reduce((sum, row) => sum + row.sharePct, 0);
    if (remaining > 0.1) {
      topRows.push({ name: "Ostali", sharePct: Number(remaining.toFixed(2)) });
    }

    return topRows;
  }, [sortedSuppliers]);

  const supplierCounts = useMemo(() => {
    const boost = sortedSuppliers.filter((row) => row.status === "Pojacaj").length;
    const keep = sortedSuppliers.filter((row) => row.status === "Zadrzi").length;
    const reduce = sortedSuppliers.filter((row) => row.status === "Smanji").length;
    return { boost, keep, reduce };
  }, [sortedSuppliers]);

  const toolbarFilters = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "fromDate", label: "Od", value: activeFilters.fromDate },
      { key: "toDate", label: "Do", value: activeFilters.toDate },
      { key: "storeId", label: "Objekat", value: activeFilters.storeId ?? "Svi objekti" },
    ],
    [activeFilters.fromDate, activeFilters.storeId, activeFilters.toDate]
  );

  const toolbarMetadata = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "generatedAt", label: "Generisano", value: data?.generatedAt ?? "" },
      { key: "suppliers", label: "Dobavljaca", value: data?.totals.brojDobavljaca ?? 0 },
      { key: "boost", label: "Pojacaj", value: supplierCounts.boost },
      { key: "keep", label: "Zadrzi", value: supplierCounts.keep },
      { key: "reduce", label: "Smanji", value: supplierCounts.reduce },
    ],
    [data?.generatedAt, data?.totals.brojDobavljaca, supplierCounts.boost, supplierCounts.keep, supplierCounts.reduce]
  );

  const openSupplierDetail = useCallback((supplier: DecisionSupplier) => {
    const recordId = supplier.dobavljacId != null
      ? String(supplier.dobavljacId)
      : `unknown-${encodeURIComponent(supplier.dobavljacNaziv)}`;

    const params = new URLSearchParams();
    params.set("fromDate", `${activeFilters.fromDate}T00:00:00Z`);
    params.set("toDate", `${activeFilters.toDate}T23:59:59Z`);
    if (activeFilters.storeId != null) params.set("storeId", String(activeFilters.storeId));

    saveAnalyticsDetailSnapshot(
      buildAnalyticsDetailSnapshot({
        table: "supplier-sales-stats",
        recordId,
        title: supplier.dobavljacNaziv,
        subtitle: "Supplier decision detail",
        columns: decisionColumns,
        row: supplier,
        metadata: toolbarFilters,
      })
    );

    navigate(`/analitika/supplier-sales-stats/${recordId}?${params.toString()}`, {
      state: { backgroundLocation: location },
    });
  }, [activeFilters.fromDate, activeFilters.storeId, activeFilters.toDate, location, navigate, toolbarFilters]);

  const applyPreset = (preset: PeriodPreset) => {
    setPeriodPreset(preset);
    if (preset === "custom") return;
    const range = getPresetRange(preset);
    setFromDate(range.fromDate);
    setToDate(range.toDate);
  };

  const handleApplyFilters = () => {
    if (invalidRange) {
      setError("Datum od ne moze biti posle datuma do.");
      return;
    }

    setActiveFilters({
      fromDate,
      toDate,
      storeId,
    });
  };

  const handleResetFilters = () => {
    const range = getPresetRange("90d");
    setPeriodPreset("90d");
    setFromDate(range.fromDate);
    setToDate(range.toDate);
    setStoreId(null);
    setActiveFilters({
      fromDate: range.fromDate,
      toDate: range.toDate,
      storeId: null,
    });
  };

  const handleSort = (field: SortField) => {
    setSortField((previousField) => {
      if (previousField === field) {
        setSortDir((previousDir) => (previousDir === "asc" ? "desc" : "asc"));
        return previousField;
      }

      setSortDir(field === "dobavljacNaziv" ? "asc" : "desc");
      return field;
    });
  };

  return (
    <div className="supplier-decision-page">
      <header className="supplier-decision-header">
        <div>
          <h1 className="supplier-decision-title">Prodaja po dobavljacima</h1>
          <p className="supplier-decision-subtitle">
            Decision-support pregled za izbor dobavljaca: fokus na promet, doprinos i akciju.
          </p>
        </div>
        {data?.generatedAt ? (
          <div className="supplier-decision-generated">
            Generisano: {new Date(data.generatedAt).toLocaleString("sr-RS")}
          </div>
        ) : null}
      </header>

      <section className="supplier-decision-filters">
        <label className="supplier-decision-field">
          <span>Period</span>
          <select value={periodPreset} onChange={(event) => applyPreset(event.target.value as PeriodPreset)}>
            <option value="30d">Poslednjih 30 dana</option>
            <option value="90d">Poslednjih 90 dana</option>
            <option value="custom">Prilagodjeno</option>
          </select>
        </label>

        <label className="supplier-decision-field">
          <span>Od</span>
          <input type="date" value={fromDate} onChange={(event) => setFromDate(event.target.value)} />
        </label>

        <label className="supplier-decision-field">
          <span>Do</span>
          <input type="date" value={toDate} onChange={(event) => setToDate(event.target.value)} />
        </label>

        <label className="supplier-decision-field">
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

        <div className="supplier-decision-actions">
          <button type="button" onClick={handleApplyFilters} disabled={loading}>
            Primeni
          </button>
          <button type="button" className="secondary" onClick={handleResetFilters} disabled={loading}>
            Reset
          </button>
        </div>
      </section>

      {invalidRange ? (
        <div className="supplier-decision-message error">Datum od ne moze biti posle datuma do.</div>
      ) : null}
      {error ? <div className="supplier-decision-message error">{error}</div> : null}
      {loading ? <div className="supplier-decision-message loading">Ucitavam dobavljace...</div> : null}

      {!loading && data ? (
        <>
          <section className="supplier-decision-kpis">
            <article className="supplier-decision-kpi">
              <span>Ukupan promet</span>
              <strong>{fmtRsd(totalRevenue)}</strong>
            </article>
            <article className="supplier-decision-kpi">
              <span>Udeo top 5 dobavljaca</span>
              <strong>{fmtPct(top5SharePct)}</strong>
            </article>
            <article className="supplier-decision-kpi">
              <span>Ukupan marzni doprinos</span>
              <strong>{fmtRsd(totalMarginContribution)}</strong>
            </article>
            <article className="supplier-decision-kpi">
              <span>Rast/PAD vs prethodni period</span>
              <strong className={trendClass(periodGrowthPct)}>{fmtSignedPct(periodGrowthPct)}</strong>
            </article>
          </section>

          <section className="supplier-decision-panels">
            <article className="supplier-decision-card">
              <h2>Koncentracija prometa po dobavljacima</h2>
              <p>Top udeo prometa za brzu procenu gde je biznis koncentrisan.</p>
              {concentrationData.length > 0 ? (
                <div className="supplier-decision-chart-wrap">
                  <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={260}>
                    <BarChart data={concentrationData} layout="vertical" margin={{ top: 12, right: 16, left: 8, bottom: 8 }}>
                      <CartesianGrid strokeDasharray="3 3" stroke="var(--border-default)" />
                      <XAxis type="number" tick={{ fill: "var(--text-secondary)", fontSize: 12 }} unit="%" />
                      <YAxis type="category" dataKey="name" width={180} tick={{ fill: "var(--text-primary)", fontSize: 12 }} />
                      <Tooltip
                        formatter={(value: number | string | undefined) => `${fmtPct(Number(value ?? 0), 2)}`}
                        labelStyle={{ color: "var(--text-primary)" }}
                      />
                      <Bar dataKey="sharePct" fill="var(--accent-primary)" radius={[0, 8, 8, 0]} />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              ) : (
                <div className="supplier-decision-empty">Nema podataka za grafikon koncentracije.</div>
              )}
            </article>

            <article className="supplier-decision-card">
              <div className="supplier-decision-table-head">
                <div>
                  <h2>Prioritetna lista dobavljaca</h2>
                  <p>
                    Pojacaj: {supplierCounts.boost} | Zadrzi: {supplierCounts.keep} | Smanji: {supplierCounts.reduce}
                  </p>
                </div>
                <AnalyticsTableToolbar
                  tableKey="supplier-sales-stats"
                  tableTitle="Supplier decision support"
                  columns={decisionColumns}
                  rows={sortedSuppliers}
                  filters={toolbarFilters}
                  metadata={toolbarMetadata}
                  defaultOrientation="landscape"
                />
              </div>

              <div className="supplier-decision-table-wrap">
                <table className="supplier-decision-table">
                  <thead>
                    <tr>
                      <th>
                        <button type="button" onClick={() => handleSort("dobavljacNaziv")}>
                          Dobavljac{sortMarker("dobavljacNaziv", sortField, sortDir)}
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("ukupanPromet")}>
                          Promet{sortMarker("ukupanPromet", sortField, sortDir)}
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("sharePct")}>
                          Udeo{sortMarker("sharePct", sortField, sortDir)}
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("marginContribution")}>
                          Marzni doprinos{sortMarker("marginContribution", sortField, sortDir)}
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("trendPct")}>
                          Trend{sortMarker("trendPct", sortField, sortDir)}
                        </button>
                      </th>
                      <th>
                        <button type="button" onClick={() => handleSort("status")}>
                          Preporuka{sortMarker("status", sortField, sortDir)}
                        </button>
                      </th>
                      <th className="align-center">Detalj</th>
                    </tr>
                  </thead>
                  <tbody>
                    {sortedSuppliers.length === 0 ? (
                      <tr>
                        <td colSpan={7} className="supplier-decision-empty-row">
                          Nema podataka za izabrane filtere.
                        </td>
                      </tr>
                    ) : (
                      sortedSuppliers.map((supplier) => {
                        const rowKey = supplierKey(supplier);
                        const isExpanded = expandedSupplierKey === rowKey;
                        return (
                          <tr key={rowKey} className={isExpanded ? "expanded-row" : ""}>
                            <td>
                              <AnalyticsUnknownLink
                                value={supplier.dobavljacNaziv}
                                issueType="missingSupplier"
                                context={{
                                  originTable: "supplier-sales-stats",
                                  fromDate: activeFilters.fromDate,
                                  toDate: activeFilters.toDate,
                                  storeId: activeFilters.storeId,
                                }}
                              />
                            </td>
                            <td className="align-right">{fmtRsd(supplier.ukupanPromet)}</td>
                            <td className="align-right">{fmtPct(supplier.sharePct, 2)}</td>
                            <td className="align-right">{fmtRsd(supplier.marginContribution)}</td>
                            <td className={`align-right ${trendClass(supplier.trendPct)}`}>{fmtSignedPct(supplier.trendPct, 2)}</td>
                            <td>
                              <span
                                className={statusClass(supplier.status)}
                                title={buildStatusTooltip(supplier)}
                                aria-label={buildStatusTooltip(supplier)}
                              >
                                {supplier.status}
                              </span>
                            </td>
                            <td className="align-center">
                              <button
                                type="button"
                                className="supplier-decision-detail-btn"
                                onClick={() => setExpandedSupplierKey(isExpanded ? null : rowKey)}
                              >
                                {isExpanded ? "Sakrij" : "Detalji"}
                              </button>
                            </td>
                          </tr>
                        );
                      })
                    )}
                  </tbody>
                </table>
              </div>
            </article>
          </section>

          {selectedSupplier ? (
            <section className="supplier-decision-detail">
              <div className="supplier-decision-detail-head">
                <h3>Detalj odluke: {selectedSupplier.dobavljacNaziv}</h3>
                <button type="button" onClick={() => openSupplierDetail(selectedSupplier)}>
                  Otvori puni detalj
                </button>
              </div>

              <div className="supplier-decision-detail-grid">
                <article>
                  <span>Pre nivelacije promet</span>
                  <strong>{fmtRsd(selectedSupplier.preNivelacijePromet)}</strong>
                </article>
                <article>
                  <span>Posle nivelacije promet</span>
                  <strong>{fmtRsd(selectedSupplier.posleNivelacijePromet)}</strong>
                </article>
                <article>
                  <span>Pre nivo kolicina</span>
                  <strong>{fmtQty(selectedSupplier.preNivelacijeKolicina)}</strong>
                </article>
                <article>
                  <span>Posle nivo kolicina</span>
                  <strong>{fmtQty(selectedSupplier.posleNivelacijeKolicina)}</strong>
                </article>
                <article>
                  <span>Artikli sa nivelacijom</span>
                  <strong>{selectedSupplier.brojArtikalaSaNivelacijom} / {selectedSupplier.brojArtikalaUkupno}</strong>
                </article>
                <article>
                  <span>Pouzdanost podataka</span>
                  <strong>{fmtPct(selectedSupplier.reliabilityPct, 1)}</strong>
                </article>
                <article>
                  <span>Marza %</span>
                  <strong>{fmtSignedPct(selectedSupplier.marginPct, 2)}</strong>
                </article>
                <article>
                  <span>Decision score</span>
                  <strong>{selectedSupplier.decisionScore}</strong>
                </article>
              </div>

              <p className="supplier-decision-reason">
                <strong>Razlog preporuke:</strong> {selectedSupplier.statusReason}
              </p>
            </section>
          ) : null}
        </>
      ) : null}
    </div>
  );
}

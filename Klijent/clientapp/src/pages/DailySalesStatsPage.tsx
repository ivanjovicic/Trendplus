import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import InfoTip from "../components/ui/InfoTip";
import { getStores } from "../services/analyticsApi";
import {
  getDailySalesStats,
  type DailySalesRow,
  type DailySalesTableResponse,
} from "../services/dailySalesStatsApi";
import type { StoreOption } from "../types/analytics";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import { getDataScope } from "../utils/dataScope";
import "./DailySalesStatsPage.css";

type PeriodPreset = "30d" | "90d" | "custom";
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

const DEFAULT_TOP_N = 15;

function toDateInput(date: Date): string {
  return date.toISOString().slice(0, 10);
}

function getPresetRange(preset: Exclude<PeriodPreset, "custom">): { fromDate: string; toDate: string } {
  const to = new Date();
  const from = new Date(to);
  if (preset === "30d") from.setDate(from.getDate() - 29);
  if (preset === "90d") from.setDate(from.getDate() - 89);
  return { fromDate: toDateInput(from), toDate: toDateInput(to) };
}

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

function fmtRsd(value: number): string {
  return `${value.toLocaleString("sr-RS", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} RSD`;
}

function fmtDate(value: string): string {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return value;
  return parsed.toLocaleDateString("sr-RS");
}

function sortMarker(field: SortKey, active: SortKey, dir: SortDir): string {
  if (field !== active) return "";
  return dir === "asc" ? " ^" : " v";
}

function sum(values: number[]): number {
  return values.reduce((acc, value) => acc + value, 0);
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
  const [firstShiftNote, setFirstShiftNote] = useState("_______");
  const [secondShiftNote, setSecondShiftNote] = useState("_______");
  const [activeFilters, setActiveFilters] = useState<ActiveFilters>({
    fromDate: queryFromDate,
    toDate: queryToDate,
    storeId: queryStoreId,
    topN: queryTopN,
  });

  const [stores, setStores] = useState<StoreOption[]>([]);
  const [data, setData] = useState<DailySalesTableResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sortKey, setSortKey] = useState<SortKey>("date");
  const [sortDir, setSortDir] = useState<SortDir>("desc");

  const invalidRange = useMemo(() => {
    if (!fromDate || !toDate) return false;
    return new Date(fromDate) > new Date(toDate);
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
    setLoading(true);
    setError(null);

    try {
      const result = await getDailySalesStats({
        fromDate: filters.fromDate,
        toDate: filters.toDate,
        storeId: filters.storeId,
        topN: filters.topN,
        dataScope: queryDataScope,
        signal,
      });

      if (requestId !== requestIdRef.current) return;
      setData(result);
    } catch (reason) {
      if (reason instanceof DOMException && reason.name === "AbortError") {
        return;
      }
      if (requestId !== requestIdRef.current) return;
      setData(null);
      setError(reason instanceof Error ? reason.message : "Greska pri ucitavanju dnevne prodaje.");
    } finally {
      if (requestId === requestIdRef.current) {
        setLoading(false);
      }
    }
  }, [queryDataScope]);

  useEffect(() => {
    const controller = new AbortController();
    void load(activeFilters, controller.signal);
    return () => controller.abort();
  }, [activeFilters, load]);

  const supplierHeaders = data?.topSuppliersOrder ?? [];

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

  const totalRevenue = useMemo(
    () => sortedRows.reduce((acc, row) => acc + row.totalRevenue, 0),
    [sortedRows]
  );

  const totalItems = useMemo(
    () => sortedRows.reduce((acc, row) => acc + row.totalItemsSold, 0),
    [sortedRows]
  );

  const mismatchCount = useMemo(
    () =>
      sortedRows.filter((row) => {
        const bySuppliers = sum(row.topSupplierCounts) + row.othersCount;
        return bySuppliers !== row.totalItemsSold;
      }).length,
    [sortedRows]
  );

  const toolbarColumns = useMemo<AnalyticsTableColumn<DailySalesRow>[]>(() => {
    const baseColumns: AnalyticsTableColumn<DailySalesRow>[] = [
      { key: "date", header: "Datum", dataType: "date", getValue: (row) => row.date },
      { key: "firstShiftTotalItems", header: "Prva smena (kom.)", dataType: "number" },
      { key: "secondShiftTotalItems", header: "Druga smena (kom.)", dataType: "number" },
      { key: "totalRevenue", header: "Ukupan prihod", dataType: "currency" },
    ];

    const supplierColumns: AnalyticsTableColumn<DailySalesRow>[] = supplierHeaders.map((name, index) => ({
      key: `supplier:${index}`,
      header: name,
      dataType: "number",
      getValue: (row) => row.topSupplierCounts[index] ?? 0,
      detailLabel: `${name} (kom.)`,
    }));

    return [
      ...baseColumns,
      ...supplierColumns,
      { key: "othersCount", header: "Ostali (kom.)", dataType: "number" },
      { key: "totalItemsSold", header: "Ukupno proizvoda", dataType: "number" },
    ];
  }, [supplierHeaders]);

  const toolbarFilters = useMemo<AnalyticsNamedValue[]>(() => [
    { key: "fromDate", label: "Od", value: activeFilters.fromDate },
    { key: "toDate", label: "Do", value: activeFilters.toDate },
    { key: "storeId", label: "Objekat", value: activeFilters.storeId ?? "Svi objekti" },
    { key: "topN", label: "Top dobavljaca", value: activeFilters.topN },
    { key: "dataScope", label: "Data scope", value: queryDataScope },
  ], [activeFilters.fromDate, activeFilters.storeId, activeFilters.toDate, activeFilters.topN, queryDataScope]);

  const toolbarMetadata = useMemo<AnalyticsNamedValue[]>(() => [
    { key: "requestedFrom", label: "Requested from", value: data?.requestedFrom ?? "" },
    { key: "requestedTo", label: "Requested to", value: data?.requestedTo ?? "" },
    { key: "totalDays", label: "Broj dana", value: data?.metadata.totalDays ?? 0 },
    { key: "unknownSupplierPct", label: "Unknown supplier %", value: data?.metadata.unknownSupplierPct ?? 0 },
    { key: "firstShiftHeader", label: "Prva smena", value: firstShiftNote },
    { key: "secondShiftHeader", label: "Druga smena", value: secondShiftNote },
    { key: "warnings", label: "Upozorenja", value: data?.metadata.warnings.join(" | ") ?? "" },
  ], [data?.metadata.totalDays, data?.metadata.unknownSupplierPct, data?.metadata.warnings, data?.requestedFrom, data?.requestedTo, firstShiftNote, secondShiftNote]);

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
  };

  const updateQueryParams = (filters: ActiveFilters) => {
    const params = new URLSearchParams();
    params.set("fromDate", filters.fromDate);
    params.set("toDate", filters.toDate);
    if (filters.storeId != null) params.set("storeId", String(filters.storeId));
    params.set("topN", String(filters.topN));
    params.set("dataScope", queryDataScope);
    setSearchParams(params, { replace: true });
  };

  const handleApplyFilters = () => {
    if (invalidRange) {
      setError("Datum od ne moze biti posle datuma do.");
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

  return (
    <div className="daily-sales-page">
      <header className="daily-sales-header">
        <div>
          <h1>Dnevna prodaja po smeni i dobavljacima</h1>
          <p>
            Dnevni pregled smenskih kolicina, prihoda i top dobavljaca po prodatim komadima.
          </p>
        </div>
        {data ? (
          <div className="daily-sales-generated">
            Opseg: {fmtDate(data.requestedFrom)} - {fmtDate(data.requestedTo)}
          </div>
        ) : null}
      </header>

      <section className="daily-sales-shift-head">
        <label>
          <span>Prva smena:</span>
          <input
            value={firstShiftNote}
            onChange={(event) => setFirstShiftNote(event.target.value)}
            placeholder="_______"
          />
        </label>
        <label>
          <span>Druga smena:</span>
          <input
            value={secondShiftNote}
            onChange={(event) => setSecondShiftNote(event.target.value)}
            placeholder="_______"
          />
        </label>
      </section>

      <section className="daily-sales-filters">
        <label>
          <span>Period</span>
          <select value={periodPreset} onChange={(event) => applyPreset(event.target.value as PeriodPreset)}>
            <option value="30d">Poslednjih 30 dana</option>
            <option value="90d">Poslednjih 90 dana</option>
            <option value="custom">Prilagodjeno</option>
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
          <span>Top dobavljaca</span>
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
        <div className="daily-sales-message error">Datum od ne moze biti posle datuma do.</div>
      ) : null}
      {error ? <div className="daily-sales-message error">{error}</div> : null}
      {loading ? <div className="daily-sales-message loading">Ucitavam dnevne podatke...</div> : null}

      {!loading && data ? (
        <>
          <section className="daily-sales-kpis">
            <article>
              <span>Ukupan prihod</span>
              <strong>{fmtRsd(totalRevenue)}</strong>
            </article>
            <article>
              <span>Ukupno komada</span>
              <strong>{fmtNumber(totalItems)}</strong>
            </article>
            <article>
              <span>Dana u opsegu</span>
              <strong>{fmtNumber(data.metadata.totalDays)}</strong>
            </article>
            <article>
              <span>Unknown supplier %</span>
              <strong>{data.metadata.unknownSupplierPct.toLocaleString("sr-RS", { maximumFractionDigits: 2 })}%</strong>
            </article>
          </section>

          {data.metadata.warnings.length > 0 ? (
            <section className="daily-sales-warnings">
              {data.metadata.warnings.map((warning) => (
                <p key={warning}>{warning}</p>
              ))}
            </section>
          ) : null}

          <section className="daily-sales-table-card">
            <div className="daily-sales-table-head">
              <div>
                <h2>Tabela po danima</h2>
                <p>
                  Top dobavljaci su odredjeni globalno za izabrani opseg, a kolone prikazuju dnevne komade.
                </p>
              </div>
              <AnalyticsTableToolbar
                tableKey="daily-sales-stats"
                tableTitle="Dnevna prodaja po smeni i dobavljacima"
                columns={toolbarColumns}
                rows={sortedRows}
                filters={toolbarFilters}
                metadata={toolbarMetadata}
                defaultOrientation="landscape"
              />
            </div>

            <div className="daily-sales-table-wrap">
              <table className="daily-sales-table">
                <thead>
                  <tr>
                    <th>
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
                    {supplierHeaders.map((name, index) => (
                      <th key={`supplier-header-${name}-${index}`} className="align-right">
                        <button type="button" onClick={() => handleSort(`supplier:${index}`)}>
                          {name}{sortMarker(`supplier:${index}`, sortKey, sortDir)}
                        </button>
                      </th>
                    ))}
                    <th className="align-right">
                      <button type="button" onClick={() => handleSort("othersCount")}>
                        Ostali{sortMarker("othersCount", sortKey, sortDir)}{" "}
                        <InfoTip text="Komadi dobavljaca koji nisu u top N listi za izabrani opseg." />
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
                          <td className="align-right">{fmtNumber(row.firstShiftTotalItems)}</td>
                          <td className="align-right">{fmtNumber(row.secondShiftTotalItems)}</td>
                          <td className="align-right">{fmtRsd(row.totalRevenue)}</td>
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
                Upozorenje: {mismatchCount} redova ima mismatch izmedju total kolone i top+others sabiranja.
              </p>
            ) : null}
          </section>
        </>
      ) : null}
    </div>
  );
}

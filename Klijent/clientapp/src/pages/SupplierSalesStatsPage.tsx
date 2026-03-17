import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import {
  getSupplierSalesStats,
  type SezonaOption,
  type SupplierSalesStat,
  type SupplierSalesStatsResponse,
} from "../services/supplierSalesStatsApi";
import "./SupplierSalesStatsPage.css";

type SortDir = "asc" | "desc";
type SortField =
  | "dobavljacNaziv"
  | "preNivelacijePromet"
  | "preNivelacijeKolicina"
  | "posleNivelacijePromet"
  | "posleNivelacijeKolicina"
  | "ukupanPromet"
  | "ukupnaKolicina"
  | "promenaPrometa"
  | "marginPct"
  | "brojArtikalaSaNivelacijom";

const columns: Array<{ field: SortField; label: string; align?: "left" | "right" | "center" }> = [
  { field: "dobavljacNaziv", label: "Dobavljac" },
  { field: "preNivelacijePromet", label: "Pre promet", align: "right" },
  { field: "preNivelacijeKolicina", label: "Pre kom", align: "right" },
  { field: "posleNivelacijePromet", label: "Posle promet", align: "right" },
  { field: "posleNivelacijeKolicina", label: "Posle kom", align: "right" },
  { field: "ukupanPromet", label: "Ukupan promet", align: "right" },
  { field: "ukupnaKolicina", label: "Ukupna kolicina", align: "right" },
  { field: "promenaPrometa", label: "Promena prometa %", align: "right" },
  { field: "marginPct", label: "Marza %", align: "right" },
  { field: "brojArtikalaSaNivelacijom", label: "Artikli sa/ukupno", align: "center" },
];

function toDateInput(date: Date): string {
  return date.toISOString().slice(0, 10);
}

function toDateOnly(value: string | null | undefined): string {
  if (!value) return "";
  return value.slice(0, 10);
}

function toUtcRange(fromDate: string, toDate: string): { fromDate: string; toDate: string } {
  return {
    fromDate: `${fromDate}T00:00:00Z`,
    toDate: `${toDate}T23:59:59Z`,
  };
}

function fmtRsd(value: number): string {
  return `${value.toLocaleString("sr-RS", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} RSD`;
}

function fmtQty(value: number): string {
  return `${value.toLocaleString("sr-RS")} kom`;
}

function fmtSignedPct(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return "N/A";
  const sign = value > 0 ? "+" : "";
  return `${sign}${value.toLocaleString("sr-RS", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}%`;
}

function metricTone(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value) || value === 0) return "tone-neutral";
  return value > 0 ? "tone-positive" : "tone-negative";
}

function compareValues(a: string | number, b: string | number, dir: SortDir): number {
  const result =
    typeof a === "string" && typeof b === "string"
      ? a.localeCompare(b, "sr")
      : Number(a) - Number(b);
  return dir === "asc" ? result : -result;
}

function sortMarker(field: SortField, activeField: SortField, dir: SortDir): string {
  if (field !== activeField) return "";
  return dir === "asc" ? " ▲" : " ▼";
}

function SortButton(props: {
  field: SortField;
  label: string;
  activeField: SortField;
  dir: SortDir;
  align?: "left" | "right" | "center";
  onClick: (field: SortField) => void;
}) {
  return (
    <button
      type="button"
      className={`supplier-sales-sort-btn ${props.align ? `align-${props.align}` : ""}`}
      onClick={() => props.onClick(props.field)}
    >
      {props.label}
      {sortMarker(props.field, props.activeField, props.dir)}
    </button>
  );
}

export default function SupplierSalesStatsPage() {
  const [data, setData] = useState<SupplierSalesStatsResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sezonaId, setSezonaId] = useState<number | null>(null);
  const [fromDate, setFromDate] = useState<string>(() => {
    const date = new Date();
    date.setDate(date.getDate() - 89);
    return toDateInput(date);
  });
  const [toDate, setToDate] = useState<string>(() => toDateInput(new Date()));
  const [sortField, setSortField] = useState<SortField>("ukupanPromet");
  const [sortDir, setSortDir] = useState<SortDir>("desc");

  const invalidRange = useMemo(() => {
    if (!fromDate || !toDate) return false;
    return new Date(fromDate) > new Date(toDate);
  }, [fromDate, toDate]);

  const sezone = data?.sezone ?? [];

  const applySeason = useCallback((season: SezonaOption | undefined) => {
    if (!season) return;
    setFromDate(toDateOnly(season.datumOd));
    setToDate(toDateOnly(season.datumDo));
  }, []);

  const load = useCallback(async () => {
    if (invalidRange) {
      setError("Datum od ne moze biti posle datuma do.");
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const query =
        sezonaId != null
          ? { sezonaId }
          : { ...toUtcRange(fromDate, toDate) };

      const response = await getSupplierSalesStats(query);
      setData(response);
    } catch (e) {
      setData(null);
      setError(e instanceof Error ? e.message : "Greska pri ucitavanju statistike dobavljaca.");
    } finally {
      setLoading(false);
    }
  }, [fromDate, invalidRange, sezonaId, toDate]);

  useEffect(() => {
    void load();
  }, [load]);

  const sortedSuppliers = useMemo(() => {
    const source = data?.suppliers ?? [];
    const getValue = (item: SupplierSalesStat, field: SortField): string | number => {
      if (field === "dobavljacNaziv") return item.dobavljacNaziv;
      if (field === "brojArtikalaSaNivelacijom") return item.brojArtikalaSaNivelacijom;
      return item[field] ?? 0;
    };

    return [...source].sort((a, b) => compareValues(getValue(a, sortField), getValue(b, sortField), sortDir));
  }, [data?.suppliers, sortDir, sortField]);

  const top10Chart = useMemo(
    () =>
      [...(data?.suppliers ?? [])]
        .sort((a, b) => b.ukupanPromet - a.ukupanPromet)
        .slice(0, 10)
        .map((item) => ({
          dobavljacNaziv: item.dobavljacNaziv,
          preNivelacijePromet: item.preNivelacijePromet,
          posleNivelacijePromet: item.posleNivelacijePromet,
        })),
    [data?.suppliers]
  );

  const handleSort = (field: SortField) => {
    const textField = field === "dobavljacNaziv";
    setSortField((prevField) => {
      if (prevField === field) {
        setSortDir((prevDir) => (prevDir === "asc" ? "desc" : "asc"));
        return prevField;
      }

      setSortDir(textField ? "asc" : "desc");
      return field;
    });
  };

  const handleSeasonChange = (value: string) => {
    if (!value) {
      setSezonaId(null);
      return;
    }

    const nextSeasonId = Number(value);
    setSezonaId(nextSeasonId);
    applySeason(sezone.find((item) => item.id === nextSeasonId));
  };

  const handleCustomFromDate = (value: string) => {
    setSezonaId(null);
    setFromDate(value);
  };

  const handleCustomToDate = (value: string) => {
    setSezonaId(null);
    setToDate(value);
  };

  return (
    <div className="supplier-sales-page">
      <header className="supplier-sales-header">
        <div>
          <h1 className="supplier-sales-title">Statistika prodaje po dobavljacima</h1>
          <p className="supplier-sales-subtitle">
            Poredi promet i kolicinu pre prve nivelacije i posle prve nivelacije po artiklu u izabranom periodu,
            uz ukupan pregled za sve artikle dobavljaca.
          </p>
        </div>
        {data?.generatedAt ? (
          <div className="supplier-sales-generated">
            Generisano: {new Date(data.generatedAt).toLocaleString("sr-RS")}
          </div>
        ) : null}
      </header>

      <section className="supplier-sales-filterbar">
        <div className="supplier-sales-field">
          <label className="supplier-sales-label">Sezona</label>
          <select
            className="supplier-sales-input"
            value={sezonaId ?? ""}
            onChange={(e) => handleSeasonChange(e.target.value)}
          >
            <option value="">Prilagodjeni period</option>
            {sezone.map((season) => (
              <option key={season.id} value={season.id}>
                {season.naziv}
              </option>
            ))}
          </select>
        </div>

        <div className="supplier-sales-field">
          <label className="supplier-sales-label">Od</label>
          <input
            className="supplier-sales-input"
            type="date"
            value={fromDate}
            onChange={(e) => handleCustomFromDate(e.target.value)}
          />
        </div>

        <div className="supplier-sales-field">
          <label className="supplier-sales-label">Do</label>
          <input
            className="supplier-sales-input"
            type="date"
            value={toDate}
            onChange={(e) => handleCustomToDate(e.target.value)}
          />
        </div>

        <div className="supplier-sales-actions">
          <button
            type="button"
            className="supplier-sales-btn supplier-sales-btn-primary"
            onClick={() => void load()}
            disabled={loading}
          >
            Primeni
          </button>
          <button
            type="button"
            className="supplier-sales-btn supplier-sales-btn-secondary"
            onClick={() => {
              const today = new Date();
              const start = new Date();
              start.setDate(start.getDate() - 89);
              setSezonaId(null);
              setFromDate(toDateInput(start));
              setToDate(toDateInput(today));
            }}
            disabled={loading}
          >
            Poslednjih 90 dana
          </button>
        </div>
      </section>

      <div className="supplier-sales-note">
        Ako izaberes sezonu, datumi se automatski popunjavaju iz sifarnika sezona. Za prilagodjeni period ostavi
        sezonu praznom i unesi datume rucno.
      </div>

      {invalidRange ? <div className="supplier-sales-error">Datum od ne moze biti posle datuma do.</div> : null}
      {error ? <div className="supplier-sales-error">{error}</div> : null}
      {loading ? <div className="supplier-sales-loading">Ucitavam statistiku dobavljaca...</div> : null}

      {!loading && data ? (
        <>
          <section className="supplier-sales-kpis">
            <article className="supplier-sales-kpi">
              <span className="supplier-sales-kpi-label">Ukupan promet</span>
              <strong className="supplier-sales-kpi-value">{fmtRsd(data.totals.ukupanPromet)}</strong>
              <span className="supplier-sales-kpi-meta">{data.totals.brojDobavljaca} dobavljaca</span>
            </article>
            <article className="supplier-sales-kpi">
              <span className="supplier-sales-kpi-label">Pre nivelacije</span>
              <strong className="supplier-sales-kpi-value">{fmtRsd(data.totals.prePromet)}</strong>
              <span className="supplier-sales-kpi-meta">{fmtQty(data.totals.preKolicina)}</span>
            </article>
            <article className="supplier-sales-kpi">
              <span className="supplier-sales-kpi-label">Posle nivelacije</span>
              <strong className="supplier-sales-kpi-value">{fmtRsd(data.totals.poslePromet)}</strong>
              <span className="supplier-sales-kpi-meta">{fmtQty(data.totals.posleKolicina)}</span>
            </article>
            <article className="supplier-sales-kpi">
              <span className="supplier-sales-kpi-label">Promena prometa</span>
              <strong className={`supplier-sales-kpi-value ${metricTone(data.totals.promenaPrometaPct)}`}>
                {fmtSignedPct(data.totals.promenaPrometaPct)}
              </strong>
              <span className="supplier-sales-kpi-meta">{fmtQty(data.totals.ukupnaKolicina)}</span>
            </article>
          </section>

          <section className="supplier-sales-card">
            <h2 className="supplier-sales-section-title">Top 10 dobavljaca: pre vs posle promet</h2>
            {top10Chart.length > 0 ? (
              <ResponsiveContainer width="100%" height={350}>
                <BarChart data={top10Chart} margin={{ top: 12, right: 16, bottom: 12, left: 8 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#334155" />
                  <XAxis
                    dataKey="dobavljacNaziv"
                    tick={{ fill: "#cbd5e1", fontSize: 12 }}
                    angle={-20}
                    height={70}
                    textAnchor="end"
                  />
                  <YAxis tick={{ fill: "#cbd5e1", fontSize: 12 }} />
                  <Tooltip
                    formatter={(value) => fmtRsd(Number(value))}
                    labelStyle={{ color: "#0f172a" }}
                  />
                  <Legend />
                  <Bar dataKey="preNivelacijePromet" name="Pre snizenja" fill="#6366f1" radius={[6, 6, 0, 0]} />
                  <Bar dataKey="posleNivelacijePromet" name="Posle snizenja" fill="#22c55e" radius={[6, 6, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            ) : (
              <div className="supplier-sales-empty">Nema dovoljno podataka za grafikon.</div>
            )}
          </section>

          <section className="supplier-sales-card">
            <div className="supplier-sales-table-head">
              <h2 className="supplier-sales-section-title">Tabela po dobavljacima</h2>
              <span className="supplier-sales-table-meta">
                Period: {toDateOnly(data.fromDate) || fromDate} - {toDateOnly(data.toDate) || toDate}
              </span>
            </div>

            <div className="supplier-sales-table-wrap">
              <table className="supplier-sales-table">
                <thead>
                  <tr>
                    {columns.map((column) => (
                      <th key={column.field} className={column.align ? `align-${column.align}` : ""}>
                        <SortButton
                          field={column.field}
                          label={column.label}
                          activeField={sortField}
                          dir={sortDir}
                          align={column.align}
                          onClick={handleSort}
                        />
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {sortedSuppliers.length === 0 ? (
                    <tr>
                      <td colSpan={columns.length} className="supplier-sales-empty-row">
                        Nema prodaje za izabrane filtere.
                      </td>
                    </tr>
                  ) : (
                    sortedSuppliers.map((supplier) => (
                      <tr key={supplier.dobavljacId ?? `unknown-${supplier.dobavljacNaziv}`}>
                        <td>{supplier.dobavljacNaziv}</td>
                        <td className="align-right">{fmtRsd(supplier.preNivelacijePromet)}</td>
                        <td className="align-right">{fmtQty(supplier.preNivelacijeKolicina)}</td>
                        <td className="align-right">{fmtRsd(supplier.posleNivelacijePromet)}</td>
                        <td className="align-right">{fmtQty(supplier.posleNivelacijeKolicina)}</td>
                        <td className="align-right">{fmtRsd(supplier.ukupanPromet)}</td>
                        <td className="align-right">{fmtQty(supplier.ukupnaKolicina)}</td>
                        <td className={`align-right ${metricTone(supplier.promenaPrometa)}`}>
                          {fmtSignedPct(supplier.promenaPrometa)}
                        </td>
                        <td className={`align-right ${metricTone(supplier.marginPct)}`}>{fmtSignedPct(supplier.marginPct)}</td>
                        <td className="align-center">
                          {supplier.brojArtikalaSaNivelacijom} / {supplier.brojArtikalaUkupno}
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </section>
        </>
      ) : null}
    </div>
  );
}

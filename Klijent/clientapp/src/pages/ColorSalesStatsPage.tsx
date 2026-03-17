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
  getColorSalesStats,
  type ColorSalesStat,
  type ColorSalesStatsResponse,
  type SezonaOption,
} from "../services/colorSalesStatsApi";
import "./ColorSalesStatsPage.css";

type SortDir = "asc" | "desc";
type SortField =
  | "boja"
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
  { field: "boja", label: "Boja" },
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
      className={`color-sales-sort-btn ${props.align ? `align-${props.align}` : ""}`}
      onClick={() => props.onClick(props.field)}
    >
      {props.label}
      {sortMarker(props.field, props.activeField, props.dir)}
    </button>
  );
}

export default function ColorSalesStatsPage() {
  const [data, setData] = useState<ColorSalesStatsResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sezonaId, setSezonaId] = useState<number | null>(null);
  const [fromDate, setFromDate] = useState<string>(() => {
    const date = new Date();
    date.setDate(date.getDate() - 89);
    return toDateInput(date);
  });
  const [toDate, setToDate] = useState<string>(() => toDateInput(new Date()));
  const [sortField, setSortField] = useState<SortField>("boja");
  const [sortDir, setSortDir] = useState<SortDir>("asc");

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

      const response = await getColorSalesStats(query);
      setData(response);
    } catch (e) {
      setData(null);
      setError(e instanceof Error ? e.message : "Greska pri ucitavanju statistike boja artikala.");
    } finally {
      setLoading(false);
    }
  }, [fromDate, invalidRange, sezonaId, toDate]);

  useEffect(() => {
    void load();
  }, [load]);

  const sortedColors = useMemo(() => {
    const source = data?.colors ?? [];
    const getValue = (item: ColorSalesStat, field: SortField): string | number => {
      if (field === "boja") return item.boja;
      if (field === "brojArtikalaSaNivelacijom") return item.brojArtikalaSaNivelacijom;
      return item[field] ?? 0;
    };

    return [...source].sort((a, b) => compareValues(getValue(a, sortField), getValue(b, sortField), sortDir));
  }, [data?.colors, sortDir, sortField]);

  const top10Chart = useMemo(
    () =>
      [...(data?.colors ?? [])]
        .sort((a, b) => b.ukupanPromet - a.ukupanPromet)
        .slice(0, 10)
        .map((item) => ({
          boja: item.boja,
          preNivelacijePromet: item.preNivelacijePromet,
          posleNivelacijePromet: item.posleNivelacijePromet,
        })),
    [data?.colors]
  );

  const handleSort = (field: SortField) => {
    const textField = field === "boja";
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
    <div className="color-sales-page">
      <header className="color-sales-header">
        <div>
          <h1 className="color-sales-title">Statistika prodaje po boji artikla</h1>
          <p className="color-sales-subtitle">
            Prikaz prometa i kolicine pre i posle nivelacije grupisano po boji artikla za izabrani period.
          </p>
        </div>
        {data?.generatedAt ? (
          <div className="color-sales-generated">
            Generisano: {new Date(data.generatedAt).toLocaleString("sr-RS")}
          </div>
        ) : null}
      </header>

      <section className="color-sales-filterbar">
        <div className="color-sales-field">
          <label className="color-sales-label">Sezona</label>
          <select
            className="color-sales-input"
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

        <div className="color-sales-field">
          <label className="color-sales-label">Od</label>
          <input
            className="color-sales-input"
            type="date"
            value={fromDate}
            onChange={(e) => handleCustomFromDate(e.target.value)}
          />
        </div>

        <div className="color-sales-field">
          <label className="color-sales-label">Do</label>
          <input
            className="color-sales-input"
            type="date"
            value={toDate}
            onChange={(e) => handleCustomToDate(e.target.value)}
          />
        </div>

        <div className="color-sales-actions">
          <button
            type="button"
            className="color-sales-btn color-sales-btn-primary"
            onClick={() => void load()}
            disabled={loading}
          >
            Primeni
          </button>
          <button
            type="button"
            className="color-sales-btn color-sales-btn-secondary"
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

      <div className="color-sales-note">
        Ako izaberes sezonu, datumi se automatski popunjavaju iz sifarnika sezona. Za prilagodjeni period ostavi
        sezonu praznom i unesi datume rucno.
      </div>

      {invalidRange ? <div className="color-sales-error">Datum od ne moze biti posle datuma do.</div> : null}
      {error ? <div className="color-sales-error">{error}</div> : null}
      {loading ? <div className="color-sales-loading">Ucitavam statistiku boja artikala...</div> : null}

      {!loading && data ? (
        <>
          <section className="color-sales-kpis">
            <article className="color-sales-kpi">
              <span className="color-sales-kpi-label">Ukupan promet</span>
              <strong className="color-sales-kpi-value">{fmtRsd(data.totals.ukupanPromet)}</strong>
              <span className="color-sales-kpi-meta">{data.totals.brojBoja} boja</span>
            </article>
            <article className="color-sales-kpi">
              <span className="color-sales-kpi-label">Pre nivelacije</span>
              <strong className="color-sales-kpi-value">{fmtRsd(data.totals.prePromet)}</strong>
              <span className="color-sales-kpi-meta">{fmtQty(data.totals.preKolicina)}</span>
            </article>
            <article className="color-sales-kpi">
              <span className="color-sales-kpi-label">Posle nivelacije</span>
              <strong className="color-sales-kpi-value">{fmtRsd(data.totals.poslePromet)}</strong>
              <span className="color-sales-kpi-meta">{fmtQty(data.totals.posleKolicina)}</span>
            </article>
            <article className="color-sales-kpi">
              <span className="color-sales-kpi-label">Promena prometa</span>
              <strong className={`color-sales-kpi-value ${metricTone(data.totals.promenaPrometaPct)}`}>
                {fmtSignedPct(data.totals.promenaPrometaPct)}
              </strong>
              <span className="color-sales-kpi-meta">{fmtQty(data.totals.ukupnaKolicina)}</span>
            </article>
          </section>

          <section className="color-sales-card">
            <h2 className="color-sales-section-title">Top 10 boja: pre vs posle promet</h2>
            {top10Chart.length > 0 ? (
              <ResponsiveContainer width="100%" height={350}>
                <BarChart data={top10Chart} margin={{ top: 12, right: 16, bottom: 12, left: 8 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#334155" />
                  <XAxis
                    dataKey="boja"
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
              <div className="color-sales-empty">Nema dovoljno podataka za grafikon.</div>
            )}
          </section>

          <section className="color-sales-card">
            <div className="color-sales-table-head">
              <h2 className="color-sales-section-title">Tabela po boji artikla</h2>
              <span className="color-sales-table-meta">
                Period: {toDateOnly(data.fromDate) || fromDate} - {toDateOnly(data.toDate) || toDate}
              </span>
            </div>

            <div className="color-sales-table-wrap">
              <table className="color-sales-table">
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
                  {sortedColors.length === 0 ? (
                    <tr>
                      <td colSpan={columns.length} className="color-sales-empty-row">
                        Nema prodaje za izabrane filtere.
                      </td>
                    </tr>
                  ) : (
                    sortedColors.map((color) => (
                      <tr key={color.boja}>
                        <td>{color.boja}</td>
                        <td className="align-right">{fmtRsd(color.preNivelacijePromet)}</td>
                        <td className="align-right">{fmtQty(color.preNivelacijeKolicina)}</td>
                        <td className="align-right">{fmtRsd(color.posleNivelacijePromet)}</td>
                        <td className="align-right">{fmtQty(color.posleNivelacijeKolicina)}</td>
                        <td className="align-right">{fmtRsd(color.ukupanPromet)}</td>
                        <td className="align-right">{fmtQty(color.ukupnaKolicina)}</td>
                        <td className={`align-right ${metricTone(color.promenaPrometa)}`}>
                          {fmtSignedPct(color.promenaPrometa)}
                        </td>
                        <td className={`align-right ${metricTone(color.marginPct)}`}>
                          {fmtSignedPct(color.marginPct)}
                        </td>
                        <td className="align-center">
                          {color.brojArtikalaSaNivelacijom} / {color.brojArtikalaUkupno}
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

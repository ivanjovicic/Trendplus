import { useCallback, useEffect, useMemo, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
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
  getShoeTypeSalesStats,
  type SezonaOption,
  type ShoeTypeSalesStat,
  type ShoeTypeSalesStatsResponse,
} from "../services/shoeTypeSalesStatsApi";
import AnalyticsUnknownLink from "../components/analytics/AnalyticsUnknownLink";
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import { buildAnalyticsDetailSnapshot, saveAnalyticsDetailSnapshot } from "../services/analyticsTableState";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import "./ShoeTypeSalesStatsPage.css";

type SortDir = "asc" | "desc";
type SortField =
  | "tipObuceNaziv"
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
  { field: "tipObuceNaziv", label: "Tip obuce" },
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

const analyticsColumns: AnalyticsTableColumn<ShoeTypeSalesStat>[] = [
  { key: "tipObuceNaziv", header: "Tip obuce", dataType: "text" },
  { key: "preNivelacijePromet", header: "Pre promet", dataType: "currency" },
  { key: "preNivelacijeKolicina", header: "Pre kom", dataType: "number" },
  { key: "posleNivelacijePromet", header: "Posle promet", dataType: "currency" },
  { key: "posleNivelacijeKolicina", header: "Posle kom", dataType: "number" },
  { key: "ukupanPromet", header: "Ukupan promet", dataType: "currency" },
  { key: "ukupnaKolicina", header: "Ukupna kolicina", dataType: "number" },
  { key: "promenaPrometa", header: "Promena prometa %", dataType: "percent" },
  { key: "marginPct", header: "Marza %", dataType: "percent" },
  {
    key: "artikliSaNivelacijom",
    header: "Artikli sa/ukupno",
    dataType: "text",
    getValue: (row) => `${row.brojArtikalaSaNivelacijom} / ${row.brojArtikalaUkupno}`,
  },
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
      className={`shoetype-sales-sort-btn ${props.align ? `align-${props.align}` : ""}`}
      onClick={() => props.onClick(props.field)}
    >
      {props.label}
      {sortMarker(props.field, props.activeField, props.dir)}
    </button>
  );
}

export default function ShoeTypeSalesStatsPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const [data, setData] = useState<ShoeTypeSalesStatsResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sezonaId, setSezonaId] = useState<number | null>(null);
  const [fromDate, setFromDate] = useState<string>(() => {
    const date = new Date();
    date.setDate(date.getDate() - 89);
    return toDateInput(date);
  });
  const [toDate, setToDate] = useState<string>(() => toDateInput(new Date()));
  const [sortField, setSortField] = useState<SortField>("tipObuceNaziv");
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

      const response = await getShoeTypeSalesStats(query);
      setData(response);
    } catch (e) {
      setData(null);
      setError(e instanceof Error ? e.message : "Greska pri ucitavanju statistike tipova obuce.");
    } finally {
      setLoading(false);
    }
  }, [fromDate, invalidRange, sezonaId, toDate]);

  useEffect(() => {
    void load();
  }, [load]);

  const sortedShoeTypes = useMemo(() => {
    const source = data?.shoeTypes ?? [];
    const getValue = (item: ShoeTypeSalesStat, field: SortField): string | number => {
      if (field === "tipObuceNaziv") return item.tipObuceNaziv;
      if (field === "brojArtikalaSaNivelacijom") return item.brojArtikalaSaNivelacijom;
      return item[field] ?? 0;
    };

    return [...source].sort((a, b) => compareValues(getValue(a, sortField), getValue(b, sortField), sortDir));
  }, [data?.shoeTypes, sortDir, sortField]);

  const top10Chart = useMemo(
    () =>
      [...(data?.shoeTypes ?? [])]
        .sort((a, b) => b.ukupanPromet - a.ukupanPromet)
        .slice(0, 10)
        .map((item) => ({
          tipObuceNaziv: item.tipObuceNaziv,
          preNivelacijePromet: item.preNivelacijePromet,
          posleNivelacijePromet: item.posleNivelacijePromet,
        })),
    [data?.shoeTypes]
  );

  const toolbarFilters = useMemo<AnalyticsNamedValue[]>(() => {
    if (sezonaId != null) {
      return [{ key: "sezonaId", label: "Sezona", value: sezonaId }];
    }

    return [
      { key: "fromDate", label: "Od", value: fromDate },
      { key: "toDate", label: "Do", value: toDate },
    ];
  }, [fromDate, sezonaId, toDate]);

  const toolbarMetadata = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "generatedAt", label: "Generisano", value: data?.generatedAt ?? "" },
      { key: "brojTipova", label: "Tipova obuce", value: data?.totals.brojTipovaObuce ?? 0 },
    ],
    [data?.generatedAt, data?.totals.brojTipovaObuce]
  );

  const openShoeTypeDetail = (shoeType: ShoeTypeSalesStat) => {
    console.info("Opened shoe type analytics detail", { id: shoeType.tipObuceId, shoeType: shoeType.tipObuceNaziv });
    const recordId = shoeType.tipObuceId != null
      ? String(shoeType.tipObuceId)
      : `unknown-${encodeURIComponent(shoeType.tipObuceNaziv)}`;
    const params = new URLSearchParams();

    if (sezonaId != null) {
      params.set("sezonaId", String(sezonaId));
    } else {
      params.set("fromDate", `${fromDate}T00:00:00Z`);
      params.set("toDate", `${toDate}T23:59:59Z`);
    }

    saveAnalyticsDetailSnapshot(
      buildAnalyticsDetailSnapshot({
        table: "shoe-type-sales-stats",
        recordId,
        title: shoeType.tipObuceNaziv,
        subtitle: "Prodaja po tipu obuce",
        columns: analyticsColumns,
        row: shoeType,
        metadata: toolbarFilters,
      })
    );

    navigate(`/analitika/shoe-type-sales-stats/${recordId}?${params.toString()}`, {
      state: { backgroundLocation: location },
    });
  };

  const handleSort = (field: SortField) => {
    const textField = field === "tipObuceNaziv";
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
    <div className="shoetype-sales-page">
      <header className="shoetype-sales-header">
        <div>
          <h1 className="shoetype-sales-title">Statistika prodaje po tipu obuce</h1>
          <p className="shoetype-sales-subtitle">
            Poredi promet i kolicinu pre prve nivelacije i posle prve nivelacije po artiklu u izabranom periodu,
            uz ukupan pregled za sve artikle svakog tipa obuce.
          </p>
        </div>
        {data?.generatedAt ? (
          <div className="shoetype-sales-generated">
            Generisano: {new Date(data.generatedAt).toLocaleString("sr-RS")}
          </div>
        ) : null}
      </header>

      <section className="shoetype-sales-filterbar">
        <div className="shoetype-sales-field">
          <label className="shoetype-sales-label">Sezona</label>
          <select
            className="shoetype-sales-input"
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

        <div className="shoetype-sales-field">
          <label className="shoetype-sales-label">Od</label>
          <input
            className="shoetype-sales-input"
            type="date"
            value={fromDate}
            onChange={(e) => handleCustomFromDate(e.target.value)}
          />
        </div>

        <div className="shoetype-sales-field">
          <label className="shoetype-sales-label">Do</label>
          <input
            className="shoetype-sales-input"
            type="date"
            value={toDate}
            onChange={(e) => handleCustomToDate(e.target.value)}
          />
        </div>

        <div className="shoetype-sales-actions">
          <button
            type="button"
            className="shoetype-sales-btn shoetype-sales-btn-primary"
            onClick={() => void load()}
            disabled={loading}
          >
            Primeni
          </button>
          <button
            type="button"
            className="shoetype-sales-btn shoetype-sales-btn-secondary"
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

      <div className="shoetype-sales-note">
        Ako izaberes sezonu, datumi se automatski popunjavaju iz sifarnika sezona. Za prilagodjeni period ostavi
        sezonu praznom i unesi datume rucno.
      </div>

      {invalidRange ? <div className="shoetype-sales-error">Datum od ne moze biti posle datuma do.</div> : null}
      {error ? <div className="shoetype-sales-error">{error}</div> : null}
      {loading ? <div className="shoetype-sales-loading">Ucitavam statistiku tipova obuce...</div> : null}

      {!loading && data ? (
        <>
          <section className="shoetype-sales-kpis">
            <article className="shoetype-sales-kpi">
              <span className="shoetype-sales-kpi-label">Ukupan promet</span>
              <strong className="shoetype-sales-kpi-value">{fmtRsd(data.totals.ukupanPromet)}</strong>
              <span className="shoetype-sales-kpi-meta">{data.totals.brojTipovaObuce} tipova obuce</span>
            </article>
            <article className="shoetype-sales-kpi">
              <span className="shoetype-sales-kpi-label">Pre nivelacije</span>
              <strong className="shoetype-sales-kpi-value">{fmtRsd(data.totals.prePromet)}</strong>
              <span className="shoetype-sales-kpi-meta">{fmtQty(data.totals.preKolicina)}</span>
            </article>
            <article className="shoetype-sales-kpi">
              <span className="shoetype-sales-kpi-label">Posle nivelacije</span>
              <strong className="shoetype-sales-kpi-value">{fmtRsd(data.totals.poslePromet)}</strong>
              <span className="shoetype-sales-kpi-meta">{fmtQty(data.totals.posleKolicina)}</span>
            </article>
            <article className="shoetype-sales-kpi">
              <span className="shoetype-sales-kpi-label">Promena prometa</span>
              <strong className={`shoetype-sales-kpi-value ${metricTone(data.totals.promenaPrometaPct)}`}>
                {fmtSignedPct(data.totals.promenaPrometaPct)}
              </strong>
              <span className="shoetype-sales-kpi-meta">{fmtQty(data.totals.ukupnaKolicina)}</span>
            </article>
          </section>

          <section className="shoetype-sales-card">
            <h2 className="shoetype-sales-section-title">Top 10 tipova obuce: pre vs posle promet</h2>
            {top10Chart.length > 0 ? (
              <ResponsiveContainer width="100%" height={350}>
                <BarChart data={top10Chart} margin={{ top: 12, right: 16, bottom: 12, left: 8 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#334155" />
                  <XAxis
                    dataKey="tipObuceNaziv"
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
              <div className="shoetype-sales-empty">Nema dovoljno podataka za grafikon.</div>
            )}
          </section>

          <section className="shoetype-sales-card">
            <div className="shoetype-sales-table-head">
              <div>
                <h2 className="shoetype-sales-section-title">Tabela po tipu obuce</h2>
                <span className="shoetype-sales-table-meta">
                  Period: {toDateOnly(data.fromDate) || fromDate} - {toDateOnly(data.toDate) || toDate}
                </span>
              </div>
              <AnalyticsTableToolbar
                tableKey="shoe-type-sales-stats"
                tableTitle="Statistika prodaje po tipu obuce"
                columns={analyticsColumns}
                rows={sortedShoeTypes}
                filters={toolbarFilters}
                metadata={toolbarMetadata}
                defaultOrientation="landscape"
              />
            </div>

            <div className="shoetype-sales-table-wrap">
              <table className="shoetype-sales-table">
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
                  {sortedShoeTypes.length === 0 ? (
                    <tr>
                      <td colSpan={columns.length} className="shoetype-sales-empty-row">
                        Nema prodaje za izabrane filtere.
                      </td>
                    </tr>
                  ) : (
                    sortedShoeTypes.map((shoeType) => (
                      <tr
                        key={shoeType.tipObuceId ?? `unknown-${shoeType.tipObuceNaziv}`}
                        className="cursor-pointer"
                        onClick={() => openShoeTypeDetail(shoeType)}
                        onKeyDown={(e) => {
                          if (e.key === "Enter" || e.key === " ") {
                            e.preventDefault();
                            openShoeTypeDetail(shoeType);
                          }
                        }}
                        tabIndex={0}
                        aria-label={`Otvori detalj tipa obuce ${shoeType.tipObuceNaziv}`}
                      >
                        <td>
                          <AnalyticsUnknownLink
                            value={shoeType.tipObuceNaziv}
                            issueType="missingShoeType"
                            context={{
                              originTable: "shoe-type-sales-stats",
                              fromDate,
                              toDate,
                              sezonaId,
                            }}
                          />
                        </td>
                        <td className="align-right">{fmtRsd(shoeType.preNivelacijePromet)}</td>
                        <td className="align-right">{fmtQty(shoeType.preNivelacijeKolicina)}</td>
                        <td className="align-right">{fmtRsd(shoeType.posleNivelacijePromet)}</td>
                        <td className="align-right">{fmtQty(shoeType.posleNivelacijeKolicina)}</td>
                        <td className="align-right">{fmtRsd(shoeType.ukupanPromet)}</td>
                        <td className="align-right">{fmtQty(shoeType.ukupnaKolicina)}</td>
                        <td className={`align-right ${metricTone(shoeType.promenaPrometa)}`}>
                          {fmtSignedPct(shoeType.promenaPrometa)}
                        </td>
                        <td className={`align-right ${metricTone(shoeType.marginPct)}`}>
                          {fmtSignedPct(shoeType.marginPct)}
                        </td>
                        <td className="align-center">
                          {shoeType.brojArtikalaSaNivelacijom} / {shoeType.brojArtikalaUkupno}
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

import React, { useEffect, useMemo, useState } from "react";
import { ClipboardList, ChevronLeft, ChevronRight, ArrowUp, ArrowDown, ArrowUpDown, ExternalLink } from "lucide-react";
import { useNavigate, useParams } from "react-router-dom";
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import DnevnikPromenaDetail from "../components/DnevnikPromenaDetail";
import Modal from "../components/Modal";
import { getDnevnikPromena } from "../services/dnevnikPromenaApi";
import type { DnevnikPromenaItem } from "../types/dnevnikPromena";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import { InventoryKpiRow, InventoryPageShell, InventoryPanel, InventoryState } from "../components/inventory/InventoryPageShell";

const TABLE_STATE_KEY = "dnevnik-promena:list-state:v1";

type StoredTableState = {
  pageNumber?: number;
  pageSize?: number;
  filterTipPromene?: string;
  searchNaziv?: string;
  searchBrojRacuna?: string;
  filterFromDate?: string;
  filterToDate?: string;
  showFilters?: boolean;
  sortBy?: "datum" | "tipPromene" | "iznos" | "naziv";
  sortDir?: "asc" | "desc";
};

const analyticsColumns: AnalyticsTableColumn<DnevnikPromenaItem>[] = [
  { key: "datum", header: "Datum", dataType: "datetime" },
  { key: "tipPromene", header: "Tip", dataType: "text" },
  { key: "artikalNaziv", header: "Artikal", dataType: "text" },
  { key: "dobavljacNaziv", header: "Dobavljac", dataType: "text" },
  { key: "brojRacuna", header: "Racun", dataType: "text" },
  { key: "iznos", header: "Iznos", dataType: "currency" },
  { key: "staraProdajnaCena", header: "Stara cena", dataType: "currency" },
  { key: "novaProdajnaCena", header: "Nova cena", dataType: "currency" },
  { key: "komentar", header: "Komentar", dataType: "text" },
  { key: "korisnikIme", header: "Korisnik", dataType: "text" },
];

function readStoredTableState(): StoredTableState {
  try {
    const raw = sessionStorage.getItem(TABLE_STATE_KEY);
    if (!raw) return {};

    const parsed = JSON.parse(raw) as StoredTableState;
    return parsed ?? {};
  } catch {
    return {};
  }
}

function formatDate(dateStr: string) {
  const date = new Date(dateStr);
  return date.toLocaleString("sr-RS", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function getTipPromeneStyle(tip: string): React.CSSProperties {
  const tipLower = tip.toLowerCase();
  if (tipLower.includes("prodaja")) return { backgroundColor: "var(--success, var(--theme-color-10b981, #10b981))", color: "var(--text-primary, var(--theme-color-0f172a, var(--theme-color-0f172a, #0f172a)))" };
  if (tipLower.includes("nivelacija")) return { backgroundColor: "var(--error, var(--theme-color-ef4444, var(--theme-color-ef4444, #ef4444)))", color: "var(--text-primary, var(--theme-color-0f172a, #0f172a))" };
  if (tipLower.includes("unos")) return { backgroundColor: "var(--info, var(--theme-color-3b82f6, #3b82f6))", color: "var(--text-primary, var(--theme-color-0f172a, var(--theme-color-0f172a, #0f172a)))" };
  if (tipLower.includes("korekcija")) return { backgroundColor: "var(--warning, var(--theme-color-f59e0b, var(--theme-color-f59e0b, #f59e0b)))", color: "var(--surface-default, var(--theme-color-f4f7fb, #f4f7fb))" };
  if (tipLower.includes("povracaj")) return { backgroundColor: "var(--gray-600, var(--theme-color-475569, var(--theme-color-475569, #475569)))", color: "var(--text-primary, var(--theme-color-0f172a, var(--theme-color-0f172a, #0f172a)))" };
  return { backgroundColor: "var(--gray-500, var(--theme-color-64748b, var(--theme-color-64748b, #64748b)))", color: "var(--text-primary, var(--theme-color-0f172a, var(--theme-color-0f172a, #0f172a)))" };
}

export default function DnevnikPromenaPage() {
  const navigate = useNavigate();
  const params = useParams<{ id?: string }>();
  const initialState = React.useMemo(() => readStoredTableState(), []);

  const [promene, setPromene] = useState<DnevnikPromenaItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [tipoviPromena, setTipoviPromena] = useState<string[]>([]);
  const [pageNumber, setPageNumber] = useState(initialState.pageNumber ?? 1);
  const [pageSize, setPageSize] = useState(initialState.pageSize ?? 50);

  const [filterTipPromene, setFilterTipPromene] = useState<string | "">(initialState.filterTipPromene ?? "");
  const [searchNaziv, setSearchNaziv] = useState(initialState.searchNaziv ?? "");
  const [searchBrojRacuna, setSearchBrojRacuna] = useState(initialState.searchBrojRacuna ?? "");
  const [filterFromDate, setFilterFromDate] = useState(initialState.filterFromDate ?? "");
  const [filterToDate, setFilterToDate] = useState(initialState.filterToDate ?? "");
  const [showFilters, setShowFilters] = useState(initialState.showFilters ?? false);

  const [sortBy, setSortBy] = useState<"datum" | "tipPromene" | "iznos" | "naziv">(initialState.sortBy ?? "datum");
  const [sortDir, setSortDir] = useState<"asc" | "desc">(initialState.sortDir ?? "desc");

  const detailId = useMemo(() => {
    const parsed = Number(params.id);
    return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
  }, [params.id]);

  useEffect(() => {
    try {
      sessionStorage.setItem(
        TABLE_STATE_KEY,
        JSON.stringify({
          pageNumber,
          pageSize,
          filterTipPromene,
          searchNaziv,
          searchBrojRacuna,
          filterFromDate,
          filterToDate,
          showFilters,
          sortBy,
          sortDir,
        } satisfies StoredTableState)
      );
    } catch {
      // best-effort table state persistence
    }
  }, [pageNumber, pageSize, filterTipPromene, searchNaziv, searchBrojRacuna, filterFromDate, filterToDate, showFilters, sortBy, sortDir]);

  useEffect(() => {
    let aborted = false;

    const loadTipovi = async () => {
      try {
        const API = import.meta.env.VITE_API_BASE_URL;
        const res = await fetch(`${API}/api/dnevnik-promena/tipovi`);
        if (!res.ok) throw new Error("Failed to load tipovi");
        const data = await res.json();
        if (!aborted) setTipoviPromena(data ?? []);
      } catch (err) {
        console.error("Failed to load tip promene options:", err);
      }
    };

    void loadTipovi();
    return () => {
      aborted = true;
    };
  }, []);

  const filters = useMemo(() => {
    const f: Record<string, string | number> = {};

    if (filterTipPromene !== "" && filterTipPromene.trim()) f.tipPromene = filterTipPromene.trim();
    if (searchNaziv.trim()) f.naziv = searchNaziv.trim();
    if (searchBrojRacuna.trim()) f.brojRacuna = searchBrojRacuna.trim();
    if (filterFromDate) f.fromDate = filterFromDate;
    if (filterToDate) f.toDate = filterToDate;

    f.sortBy = sortBy;
    f.sortDir = sortDir;

    return f;
  }, [filterTipPromene, searchNaziv, searchBrojRacuna, filterFromDate, filterToDate, sortBy, sortDir]);

  useEffect(() => {
    let aborted = false;

    const load = async () => {
      setLoading(true);
      setError(null);

      try {
        const data = await getDnevnikPromena(pageNumber, pageSize, filters);
        if (aborted) return;

        setPromene(data.items ?? []);
        setTotalCount(data.totalCount ?? 0);
      } catch (err: unknown) {
        if (aborted) return;
        console.error(err);
        setError((err as Error)?.message ?? "Greska pri ucitavanju dnevnika promena.");
      } finally {
        if (!aborted) setLoading(false);
      }
    };

    void load();
    return () => {
      aborted = true;
    };
  }, [pageNumber, pageSize, filters]);

  const handleSort = (column: "datum" | "tipPromene" | "iznos" | "naziv") => {
    if (sortBy === column) {
      setSortDir(sortDir === "asc" ? "desc" : "asc");
    } else {
      setSortBy(column);
      setSortDir("asc");
    }
    setPageNumber(1);
  };

  const renderSortIndicator = (column: "datum" | "tipPromene" | "iznos" | "naziv") => {
    if (sortBy !== column) return <ArrowUpDown size={12} className="ml-1 inline opacity-40" />;
    return sortDir === "asc"
      ? <ArrowUp size={12} className="ml-1 inline" style={{ color: "var(--info, var(--theme-color-3b82f6, var(--theme-color-3b82f6, #3b82f6)))" }} />
        : <ArrowDown size={12} className="ml-1 inline" style={{ color: "var(--info, var(--theme-color-3b82f6, var(--theme-color-3b82f6, #3b82f6)))" }} />;
  };


  const clearFilters = () => {
    setFilterTipPromene("");
    setSearchNaziv("");
    setSearchBrojRacuna("");
    setFilterFromDate("");
    setFilterToDate("");
    setPageNumber(1);
  };

  const activeFiltersCount = [
    filterTipPromene !== "",
    searchNaziv,
    searchBrojRacuna,
    filterFromDate,
    filterToDate,
  ].filter(Boolean).length;

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  const toolbarFilters = useMemo<AnalyticsNamedValue[]>(() => ([
    { key: "tipPromene", label: "Tip promene", value: filterTipPromene || "" },
    { key: "naziv", label: "Artikal", value: searchNaziv || "" },
    { key: "brojRacuna", label: "Broj racuna", value: searchBrojRacuna || "" },
    { key: "fromDate", label: "Datum od", value: filterFromDate || "" },
    { key: "toDate", label: "Datum do", value: filterToDate || "" },
  ]), [filterFromDate, filterTipPromene, filterToDate, searchBrojRacuna, searchNaziv]);

  const toolbarMetadata = useMemo<AnalyticsNamedValue[]>(() => ([
    { key: "pageNumber", label: "Strana", value: pageNumber },
    { key: "pageSize", label: "Po strani", value: pageSize },
    { key: "totalCount", label: "Ukupno zapisa", value: totalCount },
  ]), [pageNumber, pageSize, totalCount]);

  const openDetail = (id: number) => {
    console.info("Opened DnevnikPromena detail", { id });
    navigate(`/dnevnik-promena/${id}`);
  };

  const closeDetail = () => {
    navigate("/dnevnik-promena", { replace: true });
  };

  return (
    <InventoryPageShell
      icon={ClipboardList}
      title="Dnevnik promena"
      subtitle="Audit pregled svih poslovnih promena po artiklima, racunima i korisnicima."
      actions={
        <>
          <AnalyticsTableToolbar
            tableKey="dnevnik-promena"
            tableTitle="Dnevnik promena"
            columns={analyticsColumns}
            rows={promene}
            filters={toolbarFilters}
            metadata={toolbarMetadata}
            defaultOrientation="landscape"
          />
          <button
            onClick={() => setShowFilters(!showFilters)}
            className="rounded-xl border px-3 py-2 text-xs font-semibold text-contrast transition hover:opacity-90"
            style={{ borderColor: "var(--info, var(--theme-color-3b82f6, var(--theme-color-3b82f6, #3b82f6)))", backgroundColor: "var(--info, var(--theme-color-3b82f6, var(--theme-color-3b82f6, #3b82f6)))" }}
          >
            {showFilters ? "Sakrij filtere" : `Filteri ${activeFiltersCount > 0 ? `(${activeFiltersCount})` : ""}`}
          </button>
        </>
      }
    >
      <InventoryKpiRow
        items={[
          { label: "Zapisa", value: `${totalCount}` },
          { label: "Prikazano", value: `${promene.length}` },
          { label: "Strana", value: `${pageNumber}/${totalPages}` },
          { label: "Status", value: loading ? "Ucitavanje" : error ? "Greska" : "Spremno", tone: loading ? "warning" : error ? "danger" : "positive" },
        ]}
      />

      <InventoryPanel>
        <div className="mb-4 flex flex-wrap items-center gap-2">
          <button
            className="rounded-lg border border-muted bg-[var(--surface-darker)] px-3 py-1.5 text-xs text-contrast disabled:opacity-40"
            disabled={pageNumber <= 1}
            onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
          >
            <ChevronLeft size={14} />
          </button>
          <span className="text-sm text-secondary">{pageNumber} / {totalPages}</span>
          <button
            className="rounded-lg border border-muted bg-[var(--surface-darker)] px-3 py-1.5 text-xs text-contrast disabled:opacity-40"
            disabled={pageNumber >= totalPages}
            onClick={() => setPageNumber((p) => Math.min(totalPages, p + 1))}
          >
            <ChevronRight size={14} />
          </button>
          <span className="mx-1 text-muted">|</span>
          <span className="text-xs text-secondary">Po strani</span>
          <select
            className="rounded-lg border border-muted bg-[var(--surface-darker)] px-2 py-1 text-xs text-contrast"
            value={pageSize}
            onChange={(e) => {
              setPageSize(Number(e.target.value));
              setPageNumber(1);
            }}
          >
            {[25, 50, 100, 200].map((s) => (
              <option key={s} value={s}>{s}</option>
            ))}
          </select>
        </div>

        {showFilters && (
          <div className="mb-4 grid gap-3 rounded-xl border border-muted bg-[var(--surface-darker)] p-3 md:grid-cols-2 xl:grid-cols-5">
            <div>
              <label className="mb-1 block text-xs uppercase tracking-wide text-secondary">Tip promene</label>
              <select
                className="control-muted w-full rounded-lg border px-2 py-2 text-sm"
                value={filterTipPromene}
                onChange={(e) => {
                  setFilterTipPromene(e.target.value);
                  setPageNumber(1);
                }}
              >
                <option value="">Sve promene</option>
                {tipoviPromena.map((tip) => (
                  <option key={tip} value={tip}>{tip}</option>
                ))}
              </select>
            </div>

            <div>
              <label className="mb-1 block text-xs uppercase tracking-wide text-secondary">Artikal</label>
              <input
                type="text"
                className="control-muted w-full rounded-lg border px-2 py-2 text-sm"
                value={searchNaziv}
                onChange={(e) => {
                  setSearchNaziv(e.target.value);
                  setPageNumber(1);
                }}
              />
            </div>

            <div>
              <label className="mb-1 block text-xs uppercase tracking-wide text-secondary">Broj racuna</label>
              <input
                type="text"
                className="control-muted w-full rounded-lg border px-2 py-2 text-sm"
                value={searchBrojRacuna}
                onChange={(e) => {
                  setSearchBrojRacuna(e.target.value);
                  setPageNumber(1);
                }}
              />
            </div>

            <div>
              <label className="mb-1 block text-xs uppercase tracking-wide text-secondary">Datum od</label>
              <input
                type="date"
                className="control-muted w-full rounded-lg border px-2 py-2 text-sm"
                value={filterFromDate}
                onChange={(e) => {
                  setFilterFromDate(e.target.value);
                  setPageNumber(1);
                }}
              />
            </div>

            <div>
              <label className="mb-1 block text-xs uppercase tracking-wide text-secondary">Datum do</label>
              <input
                type="date"
                className="control-muted w-full rounded-lg border px-2 py-2 text-sm"
                value={filterToDate}
                onChange={(e) => {
                  setFilterToDate(e.target.value);
                  setPageNumber(1);
                }}
              />
            </div>

            <div className="xl:col-span-5">
              <button
                onClick={clearFilters}
                className="rounded-lg border border-muted bg-[var(--surface-light)] px-3 py-2 text-sm text-contrast transition hover:opacity-90"
              >
                Resetuj filtere
              </button>
            </div>
          </div>
        )}

        {loading && promene.length === 0 && <InventoryState message="Ucitavanje dnevnika promena..." tone="warning" />}
        {error && <InventoryState message={error} tone="danger" />}

        {!loading && !error && promene.length === 0 && (
          <InventoryState message="Nema rezultata za zadate filtere." tone="neutral" />
        )}

        {!error && promene.length > 0 && (
          <div className="overflow-x-auto rounded-xl border border-muted">
            <table className="min-w-full divide-y divide-[var(--border-default)] text-sm">
              <thead className="bg-[var(--surface-darker)] text-secondary">
                <tr>
                  <th className="cursor-pointer px-3 py-3 text-left" onClick={() => handleSort("datum")}>Datum{renderSortIndicator("datum")}</th>
                  <th className="cursor-pointer px-3 py-3 text-left" onClick={() => handleSort("tipPromene")}>Tip{renderSortIndicator("tipPromene")}</th>
                  <th className="cursor-pointer px-3 py-3 text-left" onClick={() => handleSort("naziv")}>Artikal{renderSortIndicator("naziv")}</th>
                  <th className="px-3 py-3 text-left">Dobavljac</th>
                  <th className="px-3 py-3 text-left">Racun</th>
                  <th className="cursor-pointer px-3 py-3 text-right" onClick={() => handleSort("iznos")}>Iznos{renderSortIndicator("iznos")}</th>
                  <th className="px-3 py-3 text-center">Stara</th>
                  <th className="px-3 py-3 text-center">Nova</th>
                  <th className="px-3 py-3 text-left">Komentar</th>
                  <th className="px-3 py-3 text-left">Korisnik</th>
                  <th className="px-3 py-3 text-center">Detalji</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[var(--border-default)] bg-[var(--surface-light)] text-contrast">
                {promene.map((item) => (
                  <tr
                    key={item.id}
                    className="cursor-pointer hover:bg-[var(--surface-elevated)] focus-within:bg-[var(--surface-elevated)]"
                    onClick={() => openDetail(item.id)}
                    onKeyDown={(e) => {
                      if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        openDetail(item.id);
                      }
                    }}
                    tabIndex={0}
                    aria-label={`Otvori detalje promene ${item.id}`}
                  >
                    <td className="px-3 py-3 text-xs text-secondary">{formatDate(item.datum)}</td>
                    <td className="px-3 py-3">
                      <span className="inline-block rounded-md px-2 py-1 text-xs font-semibold" style={getTipPromeneStyle(item.tipPromene)}>
                        {item.tipPromene}
                      </span>
                    </td>
                    <td className="px-3 py-3 font-medium">{item.artikalNaziv || "-"}</td>
                    <td className="px-3 py-3 text-secondary">{item.dobavljacNaziv || "-"}</td>
                    <td className="px-3 py-3 font-mono text-xs text-secondary">{item.brojRacuna || "-"}</td>
                    <td className="px-3 py-3 text-right font-semibold" style={{ color: item.iznos >= 0 ? "var(--success, var(--theme-color-10b981, var(--theme-color-10b981, #10b981)))" : "var(--error, var(--theme-color-ef4444, var(--theme-color-ef4444, #ef4444)))" }}>{item.iznos.toFixed(2)} RSD</td>
                    <td className="px-3 py-3 text-center text-xs text-secondary">{item.staraProdajnaCena != null ? `${item.staraProdajnaCena.toFixed(2)} RSD` : "-"}</td>
                    <td className="px-3 py-3 text-center text-xs font-semibold" style={{ color: "var(--success, var(--theme-color-10b981, var(--theme-color-10b981, #10b981)))" }}>{item.novaProdajnaCena != null ? `${item.novaProdajnaCena.toFixed(2)} RSD` : "-"}</td>
                    <td className="max-w-[220px] px-3 py-3 text-xs text-secondary">{item.komentar || "-"}</td>
                    <td className="px-3 py-3 text-xs text-secondary">{item.korisnikIme || "-"}</td>
                    <td className="px-3 py-3 text-center">
                      <button
                        type="button"
                        className="inline-flex items-center gap-1 rounded-lg border px-2 py-1 text-xs font-semibold text-contrast transition hover:opacity-90"
                        style={{ borderColor: "var(--info, var(--theme-color-3b82f6, var(--theme-color-3b82f6, #3b82f6)))", backgroundColor: "var(--info, var(--theme-color-3b82f6, var(--theme-color-3b82f6, #3b82f6)))" }}
                        onClick={(e) => {
                          e.stopPropagation();
                          openDetail(item.id);
                        }}
                      >
                        <ExternalLink size={12} />
                        Detalji
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </InventoryPanel>

      <Modal
        isOpen={detailId !== null}
        onClose={closeDetail}
        title={detailId !== null ? `Dnevnik promena #${detailId}` : "Dnevnik promena"}
        size="lg"
      >
        {detailId !== null ? <DnevnikPromenaDetail id={detailId} /> : null}
      </Modal>
    </InventoryPageShell>
  );
}

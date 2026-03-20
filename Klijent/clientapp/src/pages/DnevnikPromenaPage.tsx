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

    loadTipovi();
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
        setError((err as Error)?.message ?? "Greška pri učitavanju dnevnika promena.");
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
      ? <ArrowUp size={12} className="ml-1 inline text-[#4F8EF7]" />
      : <ArrowDown size={12} className="ml-1 inline text-[#4F8EF7]" />;
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

  const getTipPromeneColor = (tip: string) => {
    const tipLower = tip.toLowerCase();
    if (tipLower.includes("prodaja")) return "bg-emerald-700";
    if (tipLower.includes("nivelacija")) return "bg-rose-700";
    if (tipLower.includes("unos")) return "bg-blue-700";
    if (tipLower.includes("korekcija")) return "bg-amber-700";
    if (tipLower.includes("povracaj")) return "bg-violet-700";
    return "bg-slate-600";
  };

  return (
    <InventoryPageShell
      icon={ClipboardList}
      title="Dnevnik promena"
      subtitle="Audit pregled svih poslovnih promena po artiklima, računima i korisnicima."
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
            className="rounded-xl border border-[#3760b7] bg-[#2d4f95] px-3 py-2 text-xs font-semibold text-white"
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
          { label: "Status", value: loading ? "Učitavanje" : error ? "Greška" : "Spremno", tone: loading ? "warning" : error ? "danger" : "positive" },
        ]}
      />

      <InventoryPanel>
        <div className="mb-4 flex flex-wrap items-center gap-2">
          <button
            className="rounded-lg border border-[#3c4458] bg-[#222734] px-3 py-1.5 text-xs text-[#dbe6fb] disabled:opacity-40"
            disabled={pageNumber <= 1}
            onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
          >
            <ChevronLeft size={14} />
          </button>
          <span className="text-sm text-[#9aabc7]">{pageNumber} / {totalPages}</span>
          <button
            className="rounded-lg border border-[#3c4458] bg-[#222734] px-3 py-1.5 text-xs text-[#dbe6fb] disabled:opacity-40"
            disabled={pageNumber >= totalPages}
            onClick={() => setPageNumber((p) => Math.min(totalPages, p + 1))}
          >
            <ChevronRight size={14} />
          </button>
          <span className="mx-1 text-[#57637a]">|</span>
          <span className="text-xs text-[#9aabc7]">Po strani</span>
          <select
            className="rounded-lg border border-[#2f323b] bg-[#14161d] px-2 py-1 text-xs text-[#dbe6fb]"
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
          <div className="mb-4 grid gap-3 rounded-xl border border-[#2f323b] bg-[#14161d] p-3 md:grid-cols-2 xl:grid-cols-5">
            <div>
              <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Tip promene</label>
              <select
                className="w-full rounded-lg border border-[#2f323b] bg-[#1a1b1f] px-2 py-2 text-sm text-[#dbe6fb]"
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
              <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Artikal</label>
              <input
                type="text"
                className="w-full rounded-lg border border-[#2f323b] bg-[#1a1b1f] px-2 py-2 text-sm text-[#dbe6fb]"
                value={searchNaziv}
                onChange={(e) => {
                  setSearchNaziv(e.target.value);
                  setPageNumber(1);
                }}
              />
            </div>

            <div>
              <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Broj računa</label>
              <input
                type="text"
                className="w-full rounded-lg border border-[#2f323b] bg-[#1a1b1f] px-2 py-2 text-sm text-[#dbe6fb]"
                value={searchBrojRacuna}
                onChange={(e) => {
                  setSearchBrojRacuna(e.target.value);
                  setPageNumber(1);
                }}
              />
            </div>

            <div>
              <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Datum od</label>
              <input
                type="date"
                className="w-full rounded-lg border border-[#2f323b] bg-[#1a1b1f] px-2 py-2 text-sm text-[#dbe6fb]"
                value={filterFromDate}
                onChange={(e) => {
                  setFilterFromDate(e.target.value);
                  setPageNumber(1);
                }}
              />
            </div>

            <div>
              <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Datum do</label>
              <input
                type="date"
                className="w-full rounded-lg border border-[#2f323b] bg-[#1a1b1f] px-2 py-2 text-sm text-[#dbe6fb]"
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
                className="rounded-lg border border-[#3c4458] bg-[#222734] px-3 py-2 text-sm text-[#dbe6fb]"
              >
                Resetuj filtere
              </button>
            </div>
          </div>
        )}

        {loading && promene.length === 0 && <InventoryState message="Učitavanje dnevnika promena..." tone="warning" />}
        {error && <InventoryState message={error} tone="danger" />}

        {!loading && !error && promene.length === 0 && (
          <InventoryState message="Nema rezultata za zadate filtere." tone="neutral" />
        )}

        {!error && promene.length > 0 && (
          <div className="overflow-x-auto rounded-xl border border-[#2f323b]">
            <table className="min-w-full divide-y divide-[#2f323b] text-sm">
              <thead className="bg-[#14161d] text-[#93a7c8]">
                <tr>
                  <th className="cursor-pointer px-3 py-3 text-left" onClick={() => handleSort("datum")}>Datum{renderSortIndicator("datum")}</th>
                  <th className="cursor-pointer px-3 py-3 text-left" onClick={() => handleSort("tipPromene")}>Tip{renderSortIndicator("tipPromene")}</th>
                  <th className="cursor-pointer px-3 py-3 text-left" onClick={() => handleSort("naziv")}>Artikal{renderSortIndicator("naziv")}</th>
                  <th className="px-3 py-3 text-left">Dobavljač</th>
                  <th className="px-3 py-3 text-left">Račun</th>
                  <th className="cursor-pointer px-3 py-3 text-right" onClick={() => handleSort("iznos")}>Iznos{renderSortIndicator("iznos")}</th>
                  <th className="px-3 py-3 text-center">Stara</th>
                  <th className="px-3 py-3 text-center">Nova</th>
                  <th className="px-3 py-3 text-left">Komentar</th>
                  <th className="px-3 py-3 text-left">Korisnik</th>
                  <th className="px-3 py-3 text-center">Detalji</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#262a34] bg-[#1a1b1f] text-[#dbe6fb]">
                {promene.map((item) => (
                  <tr
                    key={item.id}
                    className="cursor-pointer hover:bg-[#1f2330] focus-within:bg-[#1f2330]"
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
                    <td className="px-3 py-3 text-xs text-[#a4b3cd]">{formatDate(item.datum)}</td>
                    <td className="px-3 py-3">
                      <span className={`inline-block rounded-md px-2 py-1 text-xs font-semibold text-white ${getTipPromeneColor(item.tipPromene)}`}>
                        {item.tipPromene}
                      </span>
                    </td>
                    <td className="px-3 py-3 font-medium">{item.artikalNaziv || "-"}</td>
                    <td className="px-3 py-3 text-[#b1bfd7]">{item.dobavljacNaziv || "-"}</td>
                    <td className="px-3 py-3 font-mono text-xs text-[#a4b3cd]">{item.brojRacuna || "-"}</td>
                    <td className={`px-3 py-3 text-right font-semibold ${item.iznos >= 0 ? "text-emerald-300" : "text-rose-300"}`}>{item.iznos.toFixed(2)} RSD</td>
                    <td className="px-3 py-3 text-center text-xs text-[#b1bfd7]">{item.staraProdajnaCena != null ? `${item.staraProdajnaCena.toFixed(2)} RSD` : "-"}</td>
                    <td className="px-3 py-3 text-center text-xs font-semibold text-emerald-300">{item.novaProdajnaCena != null ? `${item.novaProdajnaCena.toFixed(2)} RSD` : "-"}</td>
                    <td className="max-w-[220px] px-3 py-3 text-xs text-[#b1bfd7]">{item.komentar || "-"}</td>
                    <td className="px-3 py-3 text-xs text-[#b1bfd7]">{item.korisnikIme || "-"}</td>
                    <td className="px-3 py-3 text-center">
                      <button
                        type="button"
                        className="inline-flex items-center gap-1 rounded-lg border border-[#3760b7] bg-[#2d4f95] px-2 py-1 text-xs font-semibold text-white"
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

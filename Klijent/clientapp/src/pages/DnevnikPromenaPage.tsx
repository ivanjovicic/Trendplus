import React, { useEffect, useMemo, useState } from "react";
import { ClipboardList } from "lucide-react";
import { getDnevnikPromena } from "../services/dnevnikPromenaApi";
import type { DnevnikPromenaItem } from "../types/dnevnikPromena";
import { InventoryKpiRow, InventoryPageShell, InventoryPanel, InventoryState } from "../components/inventory/InventoryPageShell";

export default function DnevnikPromenaPage() {
  const [promene, setPromene] = useState<DnevnikPromenaItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [tipoviPromena, setTipoviPromena] = useState<string[]>([]);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(50);

  const [filterTipPromene, setFilterTipPromene] = useState<string | "">("");
  const [searchNaziv, setSearchNaziv] = useState("");
  const [searchBrojRacuna, setSearchBrojRacuna] = useState("");
  const [filterFromDate, setFilterFromDate] = useState("");
  const [filterToDate, setFilterToDate] = useState("");
  const [showFilters, setShowFilters] = useState(false);

  const [sortBy, setSortBy] = useState<"datum" | "tipPromene" | "iznos" | "naziv">("datum");
  const [sortDir, setSortDir] = useState<"asc" | "desc">("desc");

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
        setError((err as Error)?.message ?? "Greška pri ucitavanju dnevnika promena.");
      } finally {
        if (!aborted) setLoading(false);
      }
    };

    load();

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
    if (sortBy !== column) return null;
    return sortDir === "asc" ? " ?" : " ?";
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

  const formatDate = (dateStr: string) => {
    const date = new Date(dateStr);
    return date.toLocaleString("sr-RS", {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
    });
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
      subtitle="Audit pregled svih poslovnih promena po artiklima, racunima i korisnicima."
      actions={
        <button
          onClick={() => setShowFilters(!showFilters)}
          className="rounded-xl border border-[#3760b7] bg-[#2d4f95] px-3 py-2 text-xs font-semibold text-white"
        >
          {showFilters ? "Sakrij filtere" : `Filteri ${activeFiltersCount > 0 ? `(${activeFiltersCount})` : ""}`}
        </button>
      }
    >
      <InventoryKpiRow
        items={[
          { label: "Zapisa", value: `${totalCount}` },
          { label: "Prikazano", value: `${promene.length}` },
          { label: "Strana", value: `${pageNumber}/${totalPages}` },
          { label: "Status", value: loading ? "Ucitavanje" : error ? "Greška" : "Spremno", tone: loading ? "warning" : error ? "danger" : "positive" },
        ]}
      />

      <InventoryPanel>
        <div className="mb-4 flex flex-wrap items-center gap-2">
          <button
            className="rounded-lg border border-[#3c4458] bg-[#222734] px-3 py-1.5 text-xs text-[#dbe6fb] disabled:opacity-40"
            disabled={pageNumber <= 1}
            onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
          >
            ?
          </button>
          <span className="text-sm text-[#9aabc7]">{pageNumber} / {totalPages}</span>
          <button
            className="rounded-lg border border-[#3c4458] bg-[#222734] px-3 py-1.5 text-xs text-[#dbe6fb] disabled:opacity-40"
            disabled={pageNumber >= totalPages}
            onClick={() => setPageNumber((p) => Math.min(totalPages, p + 1))}
          >
            ?
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
              <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Broj racuna</label>
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

        {loading && promene.length === 0 && <InventoryState message="Ucitavanje dnevnika promena..." tone="warning" />}
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
                  <th className="px-3 py-3 text-left">Dobavljac</th>
                  <th className="px-3 py-3 text-left">Racun</th>
                  <th className="cursor-pointer px-3 py-3 text-right" onClick={() => handleSort("iznos")}>Iznos{renderSortIndicator("iznos")}</th>
                  <th className="px-3 py-3 text-center">Stara</th>
                  <th className="px-3 py-3 text-center">Nova</th>
                  <th className="px-3 py-3 text-left">Komentar</th>
                  <th className="px-3 py-3 text-left">Korisnik</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#262a34] bg-[#1a1b1f] text-[#dbe6fb]">
                {promene.map((item) => (
                  <tr key={item.id} className="hover:bg-[#1f2330]">
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
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </InventoryPanel>
    </InventoryPageShell>
  );
}

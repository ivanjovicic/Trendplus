import { useEffect, useMemo, useState } from "react";
import {
  ChartCandlestick,
  ArrowUp,
  ArrowDown,
  ArrowUpDown,
  ChevronLeft,
  ChevronRight,
  X,
} from "lucide-react";
import { getNivelacije } from "../services/artikliApi";
import { NivelacijaItem } from "../types/nivelacije";
import { InventoryKpiRow, InventoryPageShell, InventoryPanel } from "../components/inventory/InventoryPageShell";

type SortBy = "datum" | "artikalid" | "stara" | "nova" | "naziv";
type SortDir = "asc" | "desc";

function SortIcon({ field, sortBy, sortDir }: { field: SortBy; sortBy: SortBy; sortDir: SortDir }) {
  if (sortBy !== field) return <ArrowUpDown className="ml-1 inline-block opacity-35" size={12} />;
  return sortDir === "asc"
    ? <ArrowUp className="ml-1 inline-block text-[var(--info)]" size={12} />
    : <ArrowDown className="ml-1 inline-block text-[var(--info)]" size={12} />;
}

export default function NivelacijePage() {
  const [items, setItems] = useState<NivelacijaItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize] = useState(50);
  const [totalCount, setTotalCount] = useState(0);

  const [artikalId, setArtikalId] = useState<string>("");
  const [naziv, setNaziv] = useState<string>("");
  const [fromDate, setFromDate] = useState<string>("");
  const [toDate, setToDate] = useState<string>("");

  const [sortBy, setSortBy] = useState<SortBy>("datum");
  const [sortDir, setSortDir] = useState<SortDir>("desc");

  const totalPages = useMemo(() => Math.max(1, Math.ceil(totalCount / pageSize)), [totalCount, pageSize]);

  const fetchData = async () => {
    setLoading(true);
    setError(null);

    try {
      const res = await getNivelacije(pageNumber, pageSize, {
        artikalId: artikalId ? Number(artikalId) : undefined,
        naziv: naziv || undefined,
        fromDate: fromDate || undefined,
        toDate: toDate || undefined,
        sortBy,
        sortDir,
      });

      setItems(res.items);
      setTotalCount(res.totalCount);
    } catch (e: any) {
      setError(e?.message ?? "Greska pri ucitavanju nivelacija");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pageNumber, sortBy, sortDir, artikalId, naziv, fromDate, toDate]);

  const toggleSort = (field: SortBy) => {
    if (sortBy !== field) {
      setSortBy(field);
      setSortDir("desc");
      setPageNumber(1);
      return;
    }
    setSortDir((prev) => (prev === "desc" ? "asc" : "desc"));
    setPageNumber(1);
  };

  return (
    <InventoryPageShell
      icon={ChartCandlestick}
      title="Pregled nivelacija"
      subtitle="Istorija svih promena cena sa filtriranjem po artiklu, periodu i smeru sortiranja."
    >
      <InventoryKpiRow
        items={[
          { label: "Ukupno zapisa", value: `${totalCount}` },
          { label: "Stranica", value: `${pageNumber}/${totalPages}` },
          { label: "Sortiranje", value: `${sortBy} ${sortDir.toUpperCase()}` },
          { label: "Status", value: loading ? "Ucitavanje" : error ? "Greska" : "Aktivno", tone: loading ? "warning" : error ? "danger" : "positive" },
        ]}
      />

      <InventoryPanel>
        <div className="mb-4 grid gap-3 md:grid-cols-2 xl:grid-cols-5">
          <div>
            <label className="mb-1 block text-xs uppercase tracking-wide text-muted">Artikal ID</label>
            <input
              className="w-full rounded-xl border border-muted bg-surface-darker px-3 py-2 text-sm text-contrast outline-none transition focus:border-[var(--focus-ring)]"
              value={artikalId}
              onChange={(e) => {
                setArtikalId(e.target.value);
                setPageNumber(1);
              }}
              placeholder="npr. 123"
            />
          </div>

          <div>
            <label className="mb-1 block text-xs uppercase tracking-wide text-muted">Naziv sadrzi</label>
            <input
              className="w-full rounded-xl border border-muted bg-surface-darker px-3 py-2 text-sm text-contrast outline-none transition focus:border-[var(--focus-ring)]"
              value={naziv}
              onChange={(e) => {
                setNaziv(e.target.value);
                setPageNumber(1);
              }}
              placeholder="npr. patike"
            />
          </div>

          <div>
            <label className="mb-1 block text-xs uppercase tracking-wide text-muted">Od datuma</label>
            <input
              type="datetime-local"
              className="w-full rounded-xl border border-muted bg-surface-darker px-3 py-2 text-sm text-contrast outline-none transition focus:border-[var(--focus-ring)]"
              value={fromDate}
              onChange={(e) => {
                setFromDate(e.target.value);
                setPageNumber(1);
              }}
            />
          </div>

          <div>
            <label className="mb-1 block text-xs uppercase tracking-wide text-muted">Do datuma</label>
            <input
              type="datetime-local"
              className="w-full rounded-xl border border-muted bg-surface-darker px-3 py-2 text-sm text-contrast outline-none transition focus:border-[var(--focus-ring)]"
              value={toDate}
              onChange={(e) => {
                setToDate(e.target.value);
                setPageNumber(1);
              }}
            />
          </div>

          <div className="flex items-end">
            <button
              className="w-full rounded-xl border border-muted bg-surface px-4 py-2 text-sm font-semibold text-contrast transition hover:bg-surface-elevated"
              onClick={() => {
                setArtikalId("");
                setNaziv("");
                setFromDate("");
                setToDate("");
                setSortBy("datum");
                setSortDir("desc");
                setPageNumber(1);
              }}
            >
              <X size={14} className="mr-1 inline-block" />
              Reset sve
            </button>
          </div>
        </div>

        {(artikalId || naziv || fromDate || toDate) && (
          <div className="mb-3 flex flex-wrap gap-2">
            {artikalId && (
              <span className="inline-flex items-center gap-1 rounded-full border border-info bg-info/10 px-2 py-0.5 text-xs text-info">
                ID: {artikalId}
                <button type="button" onClick={() => { setArtikalId(""); setPageNumber(1); }} className="ml-0.5 hover:text-contrast">x</button>
              </span>
            )}
            {naziv && (
              <span className="inline-flex items-center gap-1 rounded-full border border-info bg-info/10 px-2 py-0.5 text-xs text-info">
                Naziv: {naziv}
                <button type="button" onClick={() => { setNaziv(""); setPageNumber(1); }} className="ml-0.5 hover:text-contrast">x</button>
              </span>
            )}
            {fromDate && (
              <span className="inline-flex items-center gap-1 rounded-full border border-info bg-info/10 px-2 py-0.5 text-xs text-info">
                Od: {fromDate.replace("T", " ")}
                <button type="button" onClick={() => { setFromDate(""); setPageNumber(1); }} className="ml-0.5 hover:text-contrast">x</button>
              </span>
            )}
            {toDate && (
              <span className="inline-flex items-center gap-1 rounded-full border border-info bg-info/10 px-2 py-0.5 text-xs text-info">
                Do: {toDate.replace("T", " ")}
                <button type="button" onClick={() => { setToDate(""); setPageNumber(1); }} className="ml-0.5 hover:text-contrast">x</button>
              </span>
            )}
          </div>
        )}

        {loading && <p className="py-8 text-center text-sm text-muted">Ucitavanje...</p>}
        {error && <p className="py-8 text-center text-sm font-medium text-rose-300">{error}</p>}

        {!loading && !error && (
          <div className="overflow-x-auto rounded-xl border border-muted">
            <table className="min-w-full divide-y divide-muted text-sm">
              <thead className="bg-surface-darker text-muted">
                <tr>
                  <th className="cursor-pointer select-none px-3 py-3 text-left hover:text-contrast" onClick={() => toggleSort("datum")}>Datum<SortIcon field="datum" sortBy={sortBy} sortDir={sortDir} /></th>
                  <th className="cursor-pointer select-none px-3 py-3 text-left hover:text-contrast" onClick={() => toggleSort("artikalid")}>Artikal<SortIcon field="artikalid" sortBy={sortBy} sortDir={sortDir} /></th>
                  <th className="cursor-pointer select-none px-3 py-3 text-left hover:text-contrast" onClick={() => toggleSort("naziv")}>Naziv<SortIcon field="naziv" sortBy={sortBy} sortDir={sortDir} /></th>
                  <th className="cursor-pointer select-none px-3 py-3 text-right hover:text-contrast" onClick={() => toggleSort("stara")}>Stara cena<SortIcon field="stara" sortBy={sortBy} sortDir={sortDir} /></th>
                  <th className="cursor-pointer select-none px-3 py-3 text-right hover:text-contrast" onClick={() => toggleSort("nova")}>Nova cena<SortIcon field="nova" sortBy={sortBy} sortDir={sortDir} /></th>
                  <th className="px-3 py-3 text-left">Korisnik</th>
                  <th className="px-3 py-3 text-left">Komentar</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-muted bg-surface-elevated text-contrast">
                {items.map((it) => (
                  <tr key={it.id} className="hover:bg-surface">
                    <td className="whitespace-nowrap px-3 py-3 font-mono text-xs text-muted">{new Date(it.datum).toLocaleString("sr-RS")}</td>
                    <td className="px-3 py-3">{it.artikalId ?? "-"}</td>
                    <td className="px-3 py-3">{it.artikalNaziv ?? ""}</td>
                    <td className="px-3 py-3 text-right text-secondary">{it.staraProdajnaCena ?? "-"}</td>
                    <td className="px-3 py-3 text-right font-semibold text-emerald-300">{it.novaProdajnaCena ?? "-"}</td>
                    <td className="px-3 py-3">{it.korisnikIme ?? "-"}</td>
                    <td className="px-3 py-3">{it.komentar ?? ""}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            {items.length === 0 && <p className="py-8 text-center text-sm text-muted">Nema rezultata.</p>}
          </div>
        )}

        {totalPages > 1 && (
          <div className="mt-4 flex items-center justify-center gap-3">
            <button
              className="rounded-lg border border-muted bg-surface px-2 py-2 text-contrast disabled:opacity-40"
              onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
              disabled={pageNumber === 1}
              title="Prethodna strana"
            >
              <ChevronLeft size={16} />
            </button>
            <span className="text-sm text-muted">Strana {pageNumber} / {totalPages}</span>
            <button
              className="rounded-lg border border-muted bg-surface px-2 py-2 text-contrast disabled:opacity-40"
              onClick={() => setPageNumber((p) => Math.min(totalPages, p + 1))}
              disabled={pageNumber === totalPages}
              title="Sledeca strana"
            >
              <ChevronRight size={16} />
            </button>
          </div>
        )}
      </InventoryPanel>
    </InventoryPageShell>
  );
}


import { useCallback, useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
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

const SORT_FIELDS: SortBy[] = ["datum", "artikalid", "stara", "nova", "naziv"];

function parseSortBy(value: string | null): SortBy {
  if (value && SORT_FIELDS.includes(value as SortBy)) return value as SortBy;
  return "datum";
}

function parseSortDir(value: string | null): SortDir {
  return value === "asc" ? "asc" : "desc";
}

function parsePage(value: string | null): number {
  const parsed = Number.parseInt(value ?? "1", 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 1;
}

function sortAria(sortBy: SortBy, field: SortBy, sortDir: SortDir): "ascending" | "descending" | "none" {
  if (sortBy !== field) return "none";
  return sortDir === "asc" ? "ascending" : "descending";
}

function SortIcon({ field, sortBy, sortDir }: { field: SortBy; sortBy: SortBy; sortDir: SortDir }) {
  if (sortBy !== field) return <ArrowUpDown className="ml-1 inline-block opacity-35" size={12} aria-hidden />;
  return sortDir === "asc"
    ? <ArrowUp className="ml-1 inline-block text-[var(--info)]" size={12} aria-hidden />
    : <ArrowDown className="ml-1 inline-block text-[var(--info)]" size={12} aria-hidden />;
}

export default function NivelacijePage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [items, setItems] = useState<NivelacijaItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [pageSize] = useState(50);
  const [totalCount, setTotalCount] = useState(0);

  const artikalId = searchParams.get("artikalId") ?? "";
  const naziv = searchParams.get("naziv") ?? "";
  const fromDate = searchParams.get("fromDate") ?? "";
  const toDate = searchParams.get("toDate") ?? "";
  const sortBy = parseSortBy(searchParams.get("sortBy"));
  const sortDir = parseSortDir(searchParams.get("sortDir"));
  const pageNumber = parsePage(searchParams.get("page"));

  const totalPages = useMemo(() => Math.max(1, Math.ceil(totalCount / pageSize)), [totalCount, pageSize]);

  const patchSearchParams = useCallback((patch: Record<string, string | null>, resetPage = false) => {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      for (const [key, value] of Object.entries(patch)) {
        if (value === null || value === "") next.delete(key);
        else next.set(key, value);
      }
      if (resetPage) next.delete("page");
      return next;
    }, { replace: true });
  }, [setSearchParams]);

  const fetchData = useCallback(async () => {
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
      setError(e?.message ?? "Greška pri učitavanju nivelacija");
    } finally {
      setLoading(false);
    }
  }, [artikalId, fromDate, naziv, pageNumber, pageSize, sortBy, sortDir, toDate]);

  useEffect(() => {
    void fetchData();
  }, [fetchData]);

  const toggleSort = (field: SortBy) => {
    if (sortBy !== field) {
      patchSearchParams({ sortBy: field, sortDir: "desc", page: null }, true);
      return;
    }
    patchSearchParams({ sortDir: sortDir === "desc" ? "asc" : "desc", page: null }, true);
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
          { label: "Status", value: loading ? "Učitavanje" : error ? "Greška" : "Aktivno", tone: loading ? "warning" : error ? "danger" : "positive" },
        ]}
      />

      <p className="rounded-lg border border-info/30 bg-info/5 px-3 py-2 text-sm text-muted">
        Promena cene bez izabrane prodavnice je lančana i važi za sve prodavnice.
      </p>

      <InventoryPanel>
        <div className="mb-4 grid gap-3 md:grid-cols-2 xl:grid-cols-5">
          <div>
            <label htmlFor="nivelacije-artikal-id" className="mb-1 block text-xs uppercase tracking-wide text-muted">Artikal ID</label>
            <input
              id="nivelacije-artikal-id"
              className="w-full rounded-xl border border-muted bg-surface-darker px-3 py-2 text-sm text-contrast outline-none transition focus:border-[var(--focus-ring)]"
              value={artikalId}
              onChange={(e) => {
                patchSearchParams({ artikalId: e.target.value || null }, true);
              }}
              placeholder="npr. 123"
            />
          </div>

          <div>
            <label htmlFor="nivelacije-naziv" className="mb-1 block text-xs uppercase tracking-wide text-muted">Naziv sadrži</label>
            <input
              id="nivelacije-naziv"
              className="w-full rounded-xl border border-muted bg-surface-darker px-3 py-2 text-sm text-contrast outline-none transition focus:border-[var(--focus-ring)]"
              value={naziv}
              onChange={(e) => {
                patchSearchParams({ naziv: e.target.value || null }, true);
              }}
              placeholder="npr. patike"
            />
          </div>

          <div>
            <label htmlFor="nivelacije-from-date" className="mb-1 block text-xs uppercase tracking-wide text-muted">Od datuma (Beograd)</label>
            <input
              id="nivelacije-from-date"
              type="date"
              className="w-full rounded-xl border border-muted bg-surface-darker px-3 py-2 text-sm text-contrast outline-none transition focus:border-[var(--focus-ring)]"
              value={fromDate}
              onChange={(e) => {
                patchSearchParams({ fromDate: e.target.value || null }, true);
              }}
            />
          </div>

          <div>
            <label htmlFor="nivelacije-to-date" className="mb-1 block text-xs uppercase tracking-wide text-muted">Do datuma, uključujući ceo dan</label>
            <input
              id="nivelacije-to-date"
              type="date"
              className="w-full rounded-xl border border-muted bg-surface-darker px-3 py-2 text-sm text-contrast outline-none transition focus:border-[var(--focus-ring)]"
              value={toDate}
              onChange={(e) => {
                patchSearchParams({ toDate: e.target.value || null }, true);
              }}
            />
          </div>

          <div className="flex items-end">
            <button
              type="button"
              className="w-full rounded-xl border border-muted bg-surface px-4 py-2 text-sm font-semibold text-contrast transition hover:bg-surface-elevated"
              onClick={() => {
                setSearchParams(new URLSearchParams(), { replace: true });
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
                <button type="button" onClick={() => patchSearchParams({ artikalId: null }, true)} className="ml-0.5 hover:text-contrast">x</button>
              </span>
            )}
            {naziv && (
              <span className="inline-flex items-center gap-1 rounded-full border border-info bg-info/10 px-2 py-0.5 text-xs text-info">
                Naziv: {naziv}
                <button type="button" onClick={() => patchSearchParams({ naziv: null }, true)} className="ml-0.5 hover:text-contrast">x</button>
              </span>
            )}
            {fromDate && (
              <span className="inline-flex items-center gap-1 rounded-full border border-info bg-info/10 px-2 py-0.5 text-xs text-info">
                Od: {fromDate.replace("T", " ")}
                <button type="button" onClick={() => patchSearchParams({ fromDate: null }, true)} className="ml-0.5 hover:text-contrast">x</button>
              </span>
            )}
            {toDate && (
              <span className="inline-flex items-center gap-1 rounded-full border border-info bg-info/10 px-2 py-0.5 text-xs text-info">
                Do: {toDate.replace("T", " ")}
                <button type="button" onClick={() => patchSearchParams({ toDate: null }, true)} className="ml-0.5 hover:text-contrast">x</button>
              </span>
            )}
          </div>
        )}

        {loading && <p className="py-8 text-center text-sm text-muted">Učitavanje...</p>}
        {error && (
          <div className="py-8 text-center" role="alert">
            <p className="text-sm font-medium text-rose-300">{error}</p>
            <button
              type="button"
              className="mt-3 rounded-lg border border-muted bg-surface px-4 py-2 text-sm font-semibold text-contrast transition hover:bg-surface-elevated"
              onClick={() => void fetchData()}
            >
              Pokušaj ponovo
            </button>
          </div>
        )}

        {!loading && !error && (
          <div className="overflow-x-auto rounded-xl border border-muted">
            <table className="min-w-full divide-y divide-muted text-sm">
              <thead className="bg-surface-darker text-muted">
                <tr>
                  <th scope="col" aria-sort={sortAria(sortBy, "datum", sortDir)} className="cursor-pointer select-none px-3 py-3 text-left hover:text-contrast" onClick={() => toggleSort("datum")}>Datum<SortIcon field="datum" sortBy={sortBy} sortDir={sortDir} /></th>
                  <th scope="col" className="px-3 py-3 text-left">Tip</th>
                  <th scope="col" aria-sort={sortAria(sortBy, "artikalid", sortDir)} className="cursor-pointer select-none px-3 py-3 text-left hover:text-contrast" onClick={() => toggleSort("artikalid")}>Artikal<SortIcon field="artikalid" sortBy={sortBy} sortDir={sortDir} /></th>
                  <th scope="col" className="px-3 py-3 text-left">Prodavnica</th>
                  <th scope="col" aria-sort={sortAria(sortBy, "naziv", sortDir)} className="cursor-pointer select-none px-3 py-3 text-left hover:text-contrast" onClick={() => toggleSort("naziv")}>Naziv<SortIcon field="naziv" sortBy={sortBy} sortDir={sortDir} /></th>
                  <th scope="col" aria-sort={sortAria(sortBy, "stara", sortDir)} className="cursor-pointer select-none px-3 py-3 text-right hover:text-contrast" onClick={() => toggleSort("stara")}>Stara cena<SortIcon field="stara" sortBy={sortBy} sortDir={sortDir} /></th>
                  <th scope="col" aria-sort={sortAria(sortBy, "nova", sortDir)} className="cursor-pointer select-none px-3 py-3 text-right hover:text-contrast" onClick={() => toggleSort("nova")}>Nova cena<SortIcon field="nova" sortBy={sortBy} sortDir={sortDir} /></th>
                  <th scope="col" className="px-3 py-3 text-left">Korisnik</th>
                  <th scope="col" className="px-3 py-3 text-left">Komentar</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-muted bg-surface-elevated text-contrast">
                {items.map((it) => (
                  <tr key={it.id} className="hover:bg-surface">
                    <td className="whitespace-nowrap px-3 py-3 font-mono text-xs text-muted">{new Date(it.datum).toLocaleString("sr-RS")}</td>
                    <td className="px-3 py-3">{it.tipPromene === "Nivelacija" ? "Uvezena nivelacija" : "Promena cene"}</td>
                    <td className="px-3 py-3">{it.artikalId ?? "-"}</td>
                    <td className="px-3 py-3">{it.idObjekat == null ? "Sve prodavnice (lančano)" : `Prodavnica #${it.idObjekat}`}</td>
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
              type="button"
              className="rounded-lg border border-muted bg-surface px-2 py-2 text-contrast disabled:opacity-40"
              onClick={() => {
                const prev = pageNumber - 1;
                patchSearchParams({ page: prev <= 1 ? null : String(prev) });
              }}
              disabled={pageNumber === 1}
              title="Prethodna strana"
            >
              <ChevronLeft size={16} />
            </button>
            <span className="text-sm text-muted">Strana {pageNumber} / {totalPages}</span>
            <button
              type="button"
              className="rounded-lg border border-muted bg-surface px-2 py-2 text-contrast disabled:opacity-40"
              onClick={() => patchSearchParams({ page: String(Math.min(totalPages, pageNumber + 1)) })}
              disabled={pageNumber === totalPages}
              title="Sledeća strana"
            >
              <ChevronRight size={16} />
            </button>
          </div>
        )}
      </InventoryPanel>
    </InventoryPageShell>
  );
}

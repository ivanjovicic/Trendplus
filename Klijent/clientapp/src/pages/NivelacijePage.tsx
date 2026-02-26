import { useEffect, useMemo, useState } from "react";
import { ChartCandlestick } from "lucide-react";
import { getNivelacije } from "../services/artikliApi";
import { NivelacijaItem } from "../types/nivelacije";
import { InventoryKpiRow, InventoryPageShell, InventoryPanel } from "../components/inventory/InventoryPageShell";

type SortBy = "datum" | "artikalid" | "stara" | "nova" | "naziv";
type SortDir = "asc" | "desc";

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
      setError(e?.message ?? "Greška pri ucitavanju nivelacija");
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
    setSortDir(prev => (prev === "desc" ? "asc" : "desc"));
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
          { label: "Status", value: loading ? "Ucitavanje" : error ? "Greška" : "Aktivno", tone: loading ? "warning" : error ? "danger" : "positive" },
        ]}
      />

      <InventoryPanel>
        <div className="mb-4 grid gap-3 md:grid-cols-2 xl:grid-cols-5">
          <div>
            <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Artikal ID</label>
            <input
              className="w-full rounded-xl border border-[#2f323b] bg-[#14161d] px-3 py-2 text-sm text-[#e3ebff] outline-none transition focus:border-[#4f8cff]"
              value={artikalId}
              onChange={e => {
                setArtikalId(e.target.value);
                setPageNumber(1);
              }}
              placeholder="npr. 123"
            />
          </div>

          <div>
            <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Naziv sadrži</label>
            <input
              className="w-full rounded-xl border border-[#2f323b] bg-[#14161d] px-3 py-2 text-sm text-[#e3ebff] outline-none transition focus:border-[#4f8cff]"
              value={naziv}
              onChange={e => {
                setNaziv(e.target.value);
                setPageNumber(1);
              }}
              placeholder="npr. patike"
            />
          </div>

          <div>
            <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Od datuma</label>
            <input
              type="datetime-local"
              className="w-full rounded-xl border border-[#2f323b] bg-[#14161d] px-3 py-2 text-sm text-[#e3ebff] outline-none transition focus:border-[#4f8cff]"
              value={fromDate}
              onChange={e => {
                setFromDate(e.target.value);
                setPageNumber(1);
              }}
            />
          </div>

          <div>
            <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Do datuma</label>
            <input
              type="datetime-local"
              className="w-full rounded-xl border border-[#2f323b] bg-[#14161d] px-3 py-2 text-sm text-[#e3ebff] outline-none transition focus:border-[#4f8cff]"
              value={toDate}
              onChange={e => {
                setToDate(e.target.value);
                setPageNumber(1);
              }}
            />
          </div>

          <div className="flex items-end">
            <button
              className="w-full rounded-xl border border-[#3c4458] bg-[#222734] px-4 py-2 text-sm font-semibold text-[#dbe6fb] transition hover:bg-[#2b3140]"
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
              Reset
            </button>
          </div>
        </div>

        {loading && <p className="py-8 text-center text-sm text-[#9aabc7]">Ucitavanje...</p>}
        {error && <p className="py-8 text-center text-sm font-medium text-rose-300">{error}</p>}

        {!loading && !error && (
          <div className="overflow-x-auto rounded-xl border border-[#2f323b]">
            <table className="min-w-full divide-y divide-[#2f323b] text-sm">
              <thead className="bg-[#14161d] text-[#93a7c8]">
                <tr>
                  <th className="cursor-pointer px-3 py-3 text-left" onClick={() => toggleSort("datum")}>Datum</th>
                  <th className="cursor-pointer px-3 py-3 text-left" onClick={() => toggleSort("artikalid")}>Artikal</th>
                  <th className="cursor-pointer px-3 py-3 text-left" onClick={() => toggleSort("naziv")}>Naziv</th>
                  <th className="cursor-pointer px-3 py-3 text-right" onClick={() => toggleSort("stara")}>Stara cena</th>
                  <th className="cursor-pointer px-3 py-3 text-right" onClick={() => toggleSort("nova")}>Nova cena</th>
                  <th className="px-3 py-3 text-left">Korisnik</th>
                  <th className="px-3 py-3 text-left">Komentar</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#262a34] bg-[#1a1b1f] text-[#dbe6fb]">
                {items.map(it => (
                  <tr key={it.id} className="hover:bg-[#1f2330]">
                    <td className="whitespace-nowrap px-3 py-3 font-mono text-xs text-[#a4b3cd]">{new Date(it.datum).toLocaleString("sr-RS")}</td>
                    <td className="px-3 py-3">{it.artikalId ?? "-"}</td>
                    <td className="px-3 py-3">{it.artikalNaziv ?? ""}</td>
                    <td className="px-3 py-3 text-right text-[#b9c7df]">{it.staraProdajnaCena ?? "-"}</td>
                    <td className="px-3 py-3 text-right font-semibold text-emerald-300">{it.novaProdajnaCena ?? "-"}</td>
                    <td className="px-3 py-3">{it.korisnikIme ?? "-"}</td>
                    <td className="px-3 py-3">{it.komentar ?? ""}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            {items.length === 0 && <p className="py-8 text-center text-sm text-[#9aabc7]">Nema rezultata.</p>}
          </div>
        )}

        {totalPages > 1 && (
          <div className="mt-4 flex items-center justify-center gap-3">
            <button
              className="rounded-lg border border-[#3c4458] bg-[#222734] px-3 py-2 text-sm text-[#dbe6fb] disabled:opacity-40"
              onClick={() => setPageNumber(p => Math.max(1, p - 1))}
              disabled={pageNumber === 1}
            >
              Prethodna
            </button>
            <span className="text-sm text-[#93a7c8]">{pageNumber} / {totalPages}</span>
            <button
              className="rounded-lg border border-[#3c4458] bg-[#222734] px-3 py-2 text-sm text-[#dbe6fb] disabled:opacity-40"
              onClick={() => setPageNumber(p => Math.min(totalPages, p + 1))}
              disabled={pageNumber === totalPages}
            >
              Sledeca
            </button>
          </div>
        )}
      </InventoryPanel>
    </InventoryPageShell>
  );
}

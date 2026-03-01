import React, { useEffect, useMemo, useState } from "react";
import { ChevronLeft, ChevronRight, RotateCcw, Search } from "lucide-react";
import PovracajWizard from "../components/povracaj/PovracajWizard";
import { getPovracaji } from "../services/povracajApi";
import type { PovracajZaglavlje } from "../types/povracaj";
import { InventoryKpiRow, InventoryPageShell, InventoryPanel } from "../components/inventory/InventoryPageShell";

export default function PovracajPage() {
  const [showWizard, setShowWizard] = useState(false);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const [items, setItems] = useState<PovracajZaglavlje[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize] = useState(25);
  const [searchQuery, setSearchQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await getPovracaji(pageNumber, pageSize);
      setItems(res.items ?? []);
      setTotalCount(res.totalCount ?? 0);
    } catch (e: unknown) {
      console.error(e);
      setError(e instanceof Error ? e.message : "Greska pri ucitavanju povracaja");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pageNumber]);

  const handleSuccess = () => {
    setShowWizard(false);
    setSuccessMessage("Zapisnik o povracaju je uspesno kreiran.");
    setTimeout(() => setSuccessMessage(null), 5000);
    setPageNumber(1);
    void load();
  };

  const handleCancel = () => {
    setShowWizard(false);
  };

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  const visibleItems = useMemo(() => {
    const q = searchQuery.trim().toLowerCase();
    return items.filter((item) => {
      if (statusFilter !== "all" && item.status !== statusFilter) return false;
      if (!q) return true;
      return (
        item.brojZapisnika.toLowerCase().includes(q) ||
        (item.dobavljacNaziv ?? "").toLowerCase().includes(q)
      );
    });
  }, [items, searchQuery, statusFilter]);

  const availableStatuses = useMemo(() => {
    return Array.from(new Set(items.map((item) => item.status))).sort((a, b) => a.localeCompare(b, "sr-Latn"));
  }, [items]);

  const formatDate = (iso: string) => {
    const d = new Date(iso);
    return d.toLocaleString("sr-RS", {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
    });
  };

  return (
    <InventoryPageShell
      icon={RotateCcw}
      title="Povracaj robe"
      subtitle="Enterprise pregled i unos povracaja sa filtrima, paginacijom i wizard tokom."
      actions={
        !showWizard ? (
          <button
            type="button"
            onClick={() => setShowWizard(true)}
            className="rounded-xl border border-[#3760b7] bg-[#2d4f95] px-4 py-2 text-sm font-semibold text-white transition hover:bg-[#3760b7]"
          >
            + Novi povracaj
          </button>
        ) : null
      }
    >
      <InventoryKpiRow
        items={[
          { label: "Ukupno povracaja", value: `${totalCount}` },
          { label: "Filtrirano", value: `${visibleItems.length}` },
          { label: "Status", value: loading ? "Ucitavanje" : error ? "Greska" : "Spremno", tone: loading ? "warning" : error ? "danger" : "positive" },
          { label: "Rezim", value: showWizard ? "Wizard" : `Lista (${pageNumber}/${totalPages})` },
        ]}
      />

      {successMessage && (
        <div className="rounded-xl border border-emerald-700 bg-emerald-950/30 px-4 py-3 text-sm font-medium text-emerald-300">
          {successMessage}
        </div>
      )}

      <InventoryPanel>
        {showWizard ? (
          <PovracajWizard onSuccess={handleSuccess} onCancel={handleCancel} />
        ) : (
          <>
            <h2 className="mb-3 text-lg font-semibold text-[#f3f6ff]">Kreirani povracaji</h2>

            <div className="mb-3 grid gap-2 md:grid-cols-[1fr_220px]">
              <label className="relative">
                <Search size={14} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-[#7f8ea9]" />
                <input
                  value={searchQuery}
                  onChange={(event) => setSearchQuery(event.target.value)}
                  placeholder="Pretraga po broju zapisnika ili dobavljacu..."
                  className="w-full rounded-xl border border-[#2f323b] bg-[#14161d] py-2 pl-9 pr-3 text-sm text-[#e3ebff] outline-none transition focus:border-[#4f8cff]"
                />
              </label>
              <select
                value={statusFilter}
                onChange={(event) => setStatusFilter(event.target.value)}
                className="rounded-xl border border-[#2f323b] bg-[#14161d] px-3 py-2 text-sm text-[#e3ebff] outline-none transition focus:border-[#4f8cff]"
              >
                <option value="all">Svi statusi</option>
                {availableStatuses.map((status) => (
                  <option key={status} value={status}>
                    {status}
                  </option>
                ))}
              </select>
            </div>

            {loading && <p className="py-8 text-center text-sm text-[#9aabc7]">Ucitavanje...</p>}
            {error && <p className="py-8 text-center text-sm font-medium text-rose-300">{error}</p>}

            {!loading && !error && items.length === 0 && (
              <div className="py-10 text-center">
                <p className="mb-2 text-base text-[#dbe6fb]">Nema kreiranih povracaja</p>
                <p className="text-sm text-[#9aabc7]">Kliknite na dugme "Novi povracaj" da kreirate zapisnik.</p>
              </div>
            )}

            {!loading && !error && items.length > 0 && visibleItems.length === 0 && (
              <div className="rounded-xl border border-[#2f323b] bg-[#14161d] px-4 py-8 text-center text-sm text-[#9aabc7]">
                Nema rezultata za izabrani filter.
              </div>
            )}

            {!loading && !error && visibleItems.length > 0 && (
              <>
                <div className="overflow-x-auto rounded-xl border border-[#2f323b]">
                  <table className="min-w-full divide-y divide-[#2f323b] text-sm">
                    <thead className="bg-[#14161d] text-[#93a7c8]">
                      <tr>
                        <th className="px-3 py-3 text-left">Broj</th>
                        <th className="px-3 py-3 text-left">Datum</th>
                        <th className="px-3 py-3 text-left">Dobavljac</th>
                        <th className="px-3 py-3 text-left">Status</th>
                        <th className="px-3 py-3 text-right">Iznos</th>
                        <th className="px-3 py-3 text-center">Stavke</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-[#262a34] bg-[#1a1b1f] text-[#dbe6fb]">
                      {visibleItems.map((p) => (
                        <tr key={p.id} className="hover:bg-[#1f2330]">
                          <td className="px-3 py-3 font-mono font-semibold">{p.brojZapisnika}</td>
                          <td className="whitespace-nowrap px-3 py-3">{formatDate(p.datumPovracaja)}</td>
                          <td className="px-3 py-3">{p.dobavljacNaziv ?? `#${p.dobavljacId}`}</td>
                          <td className="px-3 py-3">{p.status}</td>
                          <td className="px-3 py-3 text-right font-semibold">{p.ukupanIznos.toFixed(2)} RSD</td>
                          <td className="px-3 py-3 text-center">{p.brojStavki ?? "-"}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                <div className="mt-4 flex items-center justify-between gap-3">
                  <div className="text-sm text-[#9aabc7]">Prikazano: {visibleItems.length} / {totalCount}</div>
                  <div className="flex items-center gap-2">
                    <button
                      type="button"
                      disabled={pageNumber <= 1}
                      onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
                      className="inline-flex items-center gap-1 rounded-lg border border-[#3c4458] bg-[#222734] px-3 py-2 text-sm text-[#dbe6fb] disabled:opacity-40"
                    >
                      <ChevronLeft size={14} /> Prethodna
                    </button>
                    <span className="text-sm text-[#9aabc7]">{pageNumber} / {totalPages}</span>
                    <button
                      type="button"
                      disabled={pageNumber >= totalPages}
                      onClick={() => setPageNumber((p) => Math.min(totalPages, p + 1))}
                      className="inline-flex items-center gap-1 rounded-lg border border-[#3c4458] bg-[#222734] px-3 py-2 text-sm text-[#dbe6fb] disabled:opacity-40"
                    >
                      Sledeca <ChevronRight size={14} />
                    </button>
                  </div>
                </div>
              </>
            )}
          </>
        )}
      </InventoryPanel>
    </InventoryPageShell>
  );
}

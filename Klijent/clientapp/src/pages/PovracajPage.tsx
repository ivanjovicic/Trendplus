import React, { useEffect, useState } from "react";
import { RotateCcw } from "lucide-react";
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

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await getPovracaji(pageNumber, pageSize);
      setItems(res.items ?? []);
      setTotalCount(res.totalCount ?? 0);
    } catch (e: unknown) {
      console.error(e);
      setError(e instanceof Error ? e.message : "Greška pri učitavanju povraćaja");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pageNumber]);

  const handleSuccess = () => {
    setShowWizard(false);
    setSuccessMessage("Zapisnik o povraćaju uspešno kreiran!");
    setTimeout(() => setSuccessMessage(null), 5000);
    setPageNumber(1);
    load();
  };

  const handleCancel = () => {
    setShowWizard(false);
  };

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

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
      title="Povraćaj robe"
      subtitle="Praćenje zapisnika povraćaja sa brzim otvaranjem wizard toka za novi unos."
      actions={
        !showWizard ? (
          <button
            type="button"
            onClick={() => setShowWizard(true)}
            className="rounded-xl border border-[#3760b7] bg-[#2d4f95] px-4 py-2 text-sm font-semibold text-white transition hover:bg-[#3760b7]"
          >
            + Novi povraćaj
          </button>
        ) : null
      }
    >
      <InventoryKpiRow
        items={[
          { label: "Ukupno povraćaja", value: `${totalCount}` },
          { label: "Stranica", value: `${pageNumber}/${totalPages}` },
          { label: "Status", value: loading ? "Učitavanje" : error ? "Greška" : "Spremno", tone: loading ? "warning" : error ? "danger" : "positive" },
          { label: "Režim", value: showWizard ? "Wizard" : "Pregled" },
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
            <h2 className="mb-3 text-lg font-semibold text-[#f3f6ff]">Kreirani povraćaji</h2>

            {loading && <p className="py-8 text-center text-sm text-[#9aabc7]">Učitavanje...</p>}
            {error && <p className="py-8 text-center text-sm font-medium text-rose-300">{error}</p>}

            {!loading && !error && items.length === 0 && (
              <div className="py-10 text-center">
                <p className="mb-2 text-base text-[#dbe6fb]">Nema kreiranih povraćaja</p>
                <p className="text-sm text-[#9aabc7]">Kliknite na dugme "Novi povraćaj" da kreirate zapisnik.</p>
              </div>
            )}

            {!loading && !error && items.length > 0 && (
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
                      {items.map((p) => (
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
                  <div className="text-sm text-[#9aabc7]">Prikazano: {items.length} / {totalCount}</div>
                  <div className="flex items-center gap-2">
                    <button
                      type="button"
                      disabled={pageNumber <= 1}
                      onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
                      className="rounded-lg border border-[#3c4458] bg-[#222734] px-3 py-2 text-sm text-[#dbe6fb] disabled:opacity-40"
                    >
                      ?
                    </button>
                    <span className="text-sm text-[#9aabc7]">{pageNumber} / {totalPages}</span>
                    <button
                      type="button"
                      disabled={pageNumber >= totalPages}
                      onClick={() => setPageNumber((p) => Math.min(totalPages, p + 1))}
                      className="rounded-lg border border-[#3c4458] bg-[#222734] px-3 py-2 text-sm text-[#dbe6fb] disabled:opacity-40"
                    >
                      ?
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

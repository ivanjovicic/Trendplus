import Modal from "../Modal";
import type { InventoryItemDetail, SizeCurveDto } from "../../types/analytics";
import { formatCurrency, formatDateTime, formatNumber, getAgingTone, getHistoryDirection, getRecommendation, getStockState, getAbcTone } from "./inventoryUtils";
import { SizeCurveVisualization } from "./SizeCurveVisualization";
import type { InventoryRow } from "./types";

type SKUDetailModalProps = {
  detailRow: InventoryRow | null;
  detailData: InventoryItemDetail | null;
  detailLoading: boolean;
  detailError: string | null;
  detailTab: "overview" | "sizeCurve";
  detailSizeCurve: SizeCurveDto | null;
  detailSizeCurveLoading: boolean;
  onClose: () => void;
  onRetry: () => void;
  onTabChange: (tab: "overview" | "sizeCurve") => void;
};

export function SKUDetailModal({
  detailRow,
  detailData,
  detailLoading,
  detailError,
  detailTab,
  detailSizeCurve,
  detailSizeCurveLoading,
  onClose,
  onRetry,
  onTabChange,
}: SKUDetailModalProps) {
  const hasPlaceholderContext = detailRow?.contextStatus != null;
  const showPlaceholderValues = hasPlaceholderContext && detailData == null;
  const detailQuantity = detailData?.kolicina ?? (showPlaceholderValues ? null : detailRow?.quantity ?? null);
  const detailMinimum = detailData?.minimalnaKolicina ?? (showPlaceholderValues ? null : detailRow?.minimum ?? null);
  const detailUnitCost = detailData?.nabavnaCena ?? (showPlaceholderValues ? null : detailRow?.unitCost ?? null);
  const detailEstimatedValue = detailData?.estimatedValue ?? (showPlaceholderValues ? null : detailRow?.estimatedValueAmount ?? null);
  const detailCoverageRatio = detailQuantity != null && detailMinimum != null && detailMinimum > 0 ? detailQuantity / detailMinimum : null;
  const detailGap = detailQuantity != null && detailMinimum != null ? Math.max(detailMinimum - detailQuantity, 0) : null;
  const resolvedStockState = detailQuantity != null && detailMinimum != null ? getStockState(detailQuantity, detailMinimum) : null;
  const showContextBanner = hasPlaceholderContext && detailData == null;
  const contextBannerText = detailRow?.contextStatus === "loadingContext"
    ? (detailError ? "Kontekst artikla nije pronađen. Prikazuju se samo ograničeni podaci." : "Učitavam kontekst artikla...")
    : detailRow?.contextStatus === "contextMissing"
      ? "Kontekst artikla nije pronađen."
      : null;
  const statusCardClass = resolvedStockState
    ? `rounded-2xl border border-[var(--border-default)] bg-gradient-to-br ${resolvedStockState.panel} p-5 text-white`
    : "rounded-2xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-5 text-[var(--text-primary)]";
  const statusLabel = resolvedStockState?.label ?? (showPlaceholderValues ? "Kontekst nije učitan" : detailRow?.stockStateLabel ?? "Nije dostupno");
  const recommendationText = showPlaceholderValues
    ? "Kontekst artikla nije učitan. Sačekajte učitavanje ili pokušajte ponovo."
    : detailRow
      ? getRecommendation(detailRow)
      : "Nije dostupno";

  return (
    <Modal isOpen={detailRow != null} onClose={onClose} title={detailRow ? `Detalj artikla: ${detailRow.naziv}` : "Detalj artikla"} size="lg">
      {detailRow ? (
        <div className="space-y-5 text-[var(--text-primary)]">
          <div className="flex flex-wrap gap-2">
            <button type="button" aria-label="Prikazi pregled artikla" onClick={() => onTabChange("overview")} className={`rounded-full border px-3 py-1.5 text-xs font-semibold transition ${detailTab === "overview" ? "border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]" : "border-[var(--border-default)] bg-white text-[var(--text-primary)]"}`}>Pregled</button>
            <button type="button" aria-label="Prikazi size curve artikla" onClick={() => onTabChange("sizeCurve")} className={`rounded-full border px-3 py-1.5 text-xs font-semibold transition ${detailTab === "sizeCurve" ? "border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]" : "border-[var(--border-default)] bg-white text-[var(--text-primary)]"}`}>Size Curve</button>
          </div>

          {detailTab === "sizeCurve" ? (
            detailSizeCurveLoading ? <div className="rounded-2xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-4 py-8 text-center text-sm text-[var(--text-primary)]">Ucitavam size curve za SKU #{detailRow.id}...</div> : !detailSizeCurve?.snapshotAvailable || (detailSizeCurve.items ?? []).length === 0 ? <div className="rounded-2xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-4 py-8 text-center text-sm text-[var(--text-primary)]">Nema size curve podataka za ovaj artikal.</div> : <div className="rounded-2xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-4"><SizeCurveVisualization items={detailSizeCurve.items} cardLimit={6} /></div>
          ) : (
            <>
          {showContextBanner && contextBannerText ? (
            <div className="rounded-2xl border border-dashed border-[var(--border-default)] bg-[var(--surface-elevated)] px-4 py-3 text-sm text-[var(--text-primary)]">
              {contextBannerText}
            </div>
          ) : null}
          <div className={statusCardClass}>
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <div className={`text-xs uppercase tracking-[0.22em] ${resolvedStockState ? "text-white/70" : "text-[var(--text-primary)]"}`}>Status artikla</div>
                <div className={`mt-2 text-2xl font-semibold ${resolvedStockState ? "text-white" : "text-[var(--text-primary)]"}`}>{statusLabel}</div>
                <div className={`mt-2 text-sm ${resolvedStockState ? "text-white/80" : "text-[var(--text-primary)]"}`}>{recommendationText}</div>
                <div className="mt-3 flex flex-wrap gap-2">
                  {detailData?.abcClass ? <span className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${getAbcTone(detailData.abcClass)}`}>ABC {detailData.abcClass}</span> : null}
                  {detailData?.agingLabel ? <span className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${getAgingTone(detailData.agingBucket)}`}>{detailData.agingLabel}</span> : null}
                </div>
              </div>
              <div className={`rounded-2xl px-4 py-3 text-right ${resolvedStockState ? "border border-white/15 bg-white/10" : "border border-[var(--border-default)] bg-white"}`}>
                <div className={`text-xs uppercase tracking-[0.2em] ${resolvedStockState ? "text-white/70" : "text-[var(--text-primary)]"}`}>Procena vrednosti</div>
                <div className={`mt-2 text-xl font-semibold ${resolvedStockState ? "text-white" : "text-[var(--text-primary)]"}`}>{detailEstimatedValue == null ? "Nije dostupno" : formatCurrency(detailEstimatedValue)}</div>
                <div className={`mt-2 text-xs ${resolvedStockState ? "text-white/75" : "text-[var(--text-primary)]"}`}>
                  {detailData ? `${formatNumber(detailData.daysSinceMovement)} dana bez kretanja` : "Ucitavam aging detalj..."}
                </div>
              </div>
            </div>
          </div>

          {detailLoading ? <div className="rounded-2xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-4 py-3 text-sm text-[var(--text-primary)]">Ucitavam istoriju kretanja i dodatne detalje artikla...</div> : null}
          {detailError ? <div className="rounded-2xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-4 py-3 text-sm text-[var(--text-primary)]"><div>{detailError}</div><button type="button" aria-label="Pokusaj ponovo ucitavanje detalja artikla" onClick={onRetry} className="mt-3 rounded-lg border border-[var(--border-default)] bg-white px-3 py-1.5 text-xs font-semibold text-[var(--text-primary)]">Pokusaj ponovo</button></div> : null}

          <div className="grid gap-4 md:grid-cols-2">
            {[
              ["PLU", detailRow.plu ?? "Nije dodeljen"],
              ["Prodavnica", detailData?.storeName ?? detailRow.storeName],
              ["Dobavljac", detailData?.supplierName ?? detailRow.supplierName],
              ["Kolicina", detailQuantity == null ? "Nije dostupno" : formatNumber(detailQuantity)],
              ["Minimalna kolicina", detailMinimum == null ? "Nije dostupno" : formatNumber(detailMinimum)],
              ["Gap do minimuma", detailGap == null ? "Nije dostupno" : formatNumber(detailGap)],
              ["Nabavna cena", detailUnitCost == null ? "Nije dostupno" : formatCurrency(detailUnitCost)],
              ["Pokrice minimuma", detailCoverageRatio == null ? "Nije dostupno" : `${detailCoverageRatio.toFixed(2)}x`],
              ["Poslednje kretanje", formatDateTime(detailData?.lastMovementAt)],
              ["Dana bez kretanja", detailData ? formatNumber(detailData.daysSinceMovement) : "Ucitavanje..."],
              ["Kretanja u 30 dana", detailData ? formatNumber(detailData.movementCount30d) : "Ucitavanje..."],
              ["Kategorija", detailData?.kategorija ?? "Nije upisano"],
              ["Pol", detailData?.pol ?? "Nije upisano"],
              ["Materijal", detailData?.materijal ?? "Nije upisano"],
              ["Poslednje ažuriranje", formatDateTime(detailData?.updatedAt)],
            ].map(([label, value]) => <div key={label} className="rounded-2xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-4"><div className="text-xs uppercase tracking-[0.18em] text-[var(--text-primary)]">{label}</div><div className="mt-2 text-base font-semibold text-[var(--text-primary)]">{value}</div></div>)}
          </div>

          <div className="rounded-2xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-4">
            <div className="text-xs uppercase tracking-[0.18em] text-[var(--text-primary)]">Predlog akcije</div>
            <ul className="mt-3 list-disc space-y-2 pl-5 text-sm leading-6 text-[var(--text-primary)]">
              <li>{getRecommendation(detailRow)}</li>
              <li>{detailRow.stockState === "critical" ? "Proveriti da li postoji zamenski artikal ili redistribucija iz druge lokacije." : detailRow.stockState === "warning" ? "Dopunu povezati sa sledecom nabavkom dobavljaca i prioritet dati artiklima sa najvecom traznjom." : "Ako je prodaja sporija od plana, razmotriti akcijsku cenu ili preraspodelu izmedju lokacija."}</li>
              <li>{detailData?.abcClass === "A" ? "Klasa A: proveri da li je vezani kapital u skladu sa planom prodaje i sezonom." : detailData?.abcClass === "C" ? "Klasa C: artikli nose manji deo kapitala, ali aging lako postaje signal za ciscenje zalihe." : "Klasa B: balansirati dopunu i obrt bez prevelikog vezivanja kapitala."}</li>
              <li>Za deljenje sa timom koristi PDF ili Excel filtrirani izvoz iz vrha stranice.</li>
            </ul>
          </div>

          <div className="rounded-2xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-4">
            <div className="flex items-center justify-between gap-3">
              <div>
                <div className="text-xs uppercase tracking-[0.18em] text-[var(--text-primary)]">Istorija kretanja</div>
                <div className="mt-1 text-sm text-[var(--text-primary)]">Poslednjih 12 promena za izabrani artikal, sa dokumentom i poreklom podatka.</div>
              </div>
              <div className="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--text-primary)]">{detailData?.history.length ?? 0} stavki</div>
            </div>

            <div className="mt-4 space-y-3">
              {detailData?.history?.length ? detailData.history.map((entry) => (
                <div key={entry.movementId} className="rounded-2xl border border-[var(--border-default)] bg-white p-4">
                  <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${getAgingTone((entry.kolicina ?? 0) > 0 ? "0-30" : "90+")}`}>{getHistoryDirection(entry.kolicina)}</span>
                        <span className="text-sm font-semibold text-[var(--text-primary)]">{entry.tipPromene}</span>
                        {entry.dataOrigin ? <span className="rounded-full border border-[var(--border-default)] bg-[var(--surface-elevated)] px-2 py-0.5 text-[11px] font-semibold text-[var(--text-primary)]">{entry.dataOrigin}</span> : null}
                      </div>
                      <div className="mt-2 text-sm text-[var(--text-primary)]">{formatDateTime(entry.datum)}</div>
                      <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-[var(--text-primary)]">
                        <span>Dokument: {entry.brojDokumenta ?? "Nije upisan"}</span>
                        <span>Korisnik: {entry.korisnikIme ?? "Nepoznato"}</span>
                        <span>Prodavnica: {entry.storeName ?? detailData.storeName ?? "Nije vezano"}</span>
                        <span>Dobavljac: {entry.supplierName ?? detailData.supplierName ?? "Nije vezano"}</span>
                      </div>
                    </div>
                    <div className="rounded-2xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-4 py-3 text-right">
                      <div className="text-xs uppercase tracking-[0.18em] text-[var(--text-primary)]">Kolicina / iznos</div>
                      <div className="mt-2 text-sm font-semibold text-[var(--text-primary)]">{entry.kolicina == null ? "N/A" : formatNumber(entry.kolicina)}</div>
                      <div className="text-xs text-[var(--text-primary)]">{formatCurrency(entry.iznos)}</div>
                    </div>
                  </div>
                  {entry.komentar || entry.staraCena != null || entry.novaCena != null ? (
                    <div className="mt-3 border-t border-[var(--border-default)] pt-3 text-xs text-[var(--text-primary)]">
                      {entry.komentar ? <div>Komentar: {entry.komentar}</div> : null}
                      {entry.staraCena != null || entry.novaCena != null ? <div>Cena: {entry.staraCena != null ? formatCurrency(entry.staraCena) : "-"} -&gt; {entry.novaCena != null ? formatCurrency(entry.novaCena) : "-"}</div> : null}
                    </div>
                  ) : null}
                </div>
              )) : <div className="rounded-2xl border border-dashed border-[var(--border-default)] bg-white px-4 py-8 text-center text-sm text-[var(--text-primary)]">Za ovaj artikal nema evidentiranih istorijskih kretanja.</div>}
            </div>
          </div>
            </>
          )}
        </div>
      ) : null}
    </Modal>
  );
}


import InfoTip from "../ui/InfoTip";
import { formatCurrency, formatNumber, getCoverageText, getStockState } from "./inventoryUtils";
import type { InventoryRow } from "./types";

type InventoryItemsTableProps = {
  rows: InventoryRow[];
  loading: boolean;
  totalCount: number;
  pageNumber: number;
  totalPages: number;
  onOpenDetail: (row: InventoryRow) => void;
  onPreviousPage: () => void;
  onNextPage: () => void;
};

export function InventoryItemsTable({
  rows,
  loading,
  totalCount,
  pageNumber,
  totalPages,
  onOpenDetail,
  onPreviousPage,
  onNextPage,
}: InventoryItemsTableProps) {
  return (
    <section className="rounded-[28px] border border-[var(--border-default)] bg-[var(--surface-elevated)] p-5">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h2 className="text-lg font-semibold text-white">Tabela artikala</h2>
          <p className="text-sm text-[var(--text-primary)]">Klik na red otvara detalj sa preporukom akcije i operativnim kontekstom.</p>
        </div>
        <div className="text-sm text-[var(--text-primary)]">Prikazano <span className="font-semibold text-white">{rows.length}</span> od <span className="font-semibold text-white">{formatNumber(totalCount)}</span> artikala</div>
      </div>

      <div className="mt-4 overflow-hidden rounded-2xl border border-[var(--border-default)]">
        <div className="overflow-x-auto">
          <table aria-label="Lista artikala na stanju" className="min-w-full text-sm">
            <thead className="bg-[var(--surface-elevated)] text-left text-[var(--text-primary)]">
              <tr>
                <th className="px-4 py-3">
                  Artikal
                  <InfoTip text="Naziv artikla i PLU kod. Klik na red otvara detalj sa preporukom akcije, istorijatom i size curve analizom." />
                </th>
                <th className="px-4 py-3">
                  Status
                  <InfoTip text="Status zalihe: Kritično (ispod minimuma), Niska zaliha (≤ 20% iznad minimuma), Uredu (iznad minimuma). Bazira se na definisanom minimumu ili fallback pragovima." />
                </th>
                <th className="px-4 py-3 text-right">
                  Kolicina
                  <InfoTip text="Trenutna raspoloziva kolicina u komadima. Ne uracunava nerasporedjen ili blokiran fond." />
                </th>
                <th className="px-4 py-3 text-right">
                  Minimum
                  <InfoTip text="Minimalni nivo zalihe definisan za ovaj artikal i prodavnicu. Kada je kolicina <= minimuma, artikal ulazi u 'Niska zaliha' ili 'Kriticno' status." />
                </th>
                <th className="px-4 py-3 text-right">
                  Gap
                  <InfoTip text="Koliko komada nedostaje do minimalnog nivoa zalihe. Formula: minimum − količina (klampovano na 0). Nula = zaliha dostiže ili prelazi minimum — dopuna nije hitna. Pozitivna vrednost = koliko treba naručiti da se dostigne minimum." />
                </th>
                <th className="px-4 py-3 text-right">
                  Nabavna
                  <InfoTip text="Nabavna cena po komadu. Moze biti istorijska (iz poslednjeg prijema) ili fallback procena. Koristiti oprezno — nije uvek ažurna." />
                </th>
                <th className="px-4 py-3 text-right">
                  Vrednost
                  <InfoTip text="Procenjena nabavna vrednost zalihe ovog artikla. Formula: kolicina × nabavna cena. Podlezna nepreciznosti ako je cena fallback." />
                </th>
                <th className="px-4 py-3">
                  Prodavnica
                  <InfoTip text="Lokacija (prodajni objekat) gde se artikal nalazi." />
                </th>
                <th className="px-4 py-3">Dobavljac</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr><td colSpan={9} className="px-4 py-10 text-center text-[var(--text-primary)]">Ucitavam tabelu...</td></tr>
              ) : rows.length === 0 ? (
                <tr><td colSpan={9} className="px-4 py-10 text-center text-[var(--text-primary)]">Nema artikala za zadate filtere.</td></tr>
              ) : rows.map((row) => {
                const stock = getStockState(row.quantity, row.minimum);
                const stockBorder = row.stockState === "critical" ? "border-l-4 border-l-[var(--border-default)]" : row.stockState === "warning" ? "border-l-4 border-l-[var(--border-default)]" : "border-l-4 border-l-[var(--border-default)]";
                return (
                  <tr key={row.id} role="button" tabIndex={0} aria-label={`Otvori detalje za ${row.naziv} - ${row.stockStateLabel}`} className={`cursor-pointer border-t border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)] transition-all duration-200 hover:bg-[var(--surface-light)] hover:border-t-[var(--border-hover, var(--theme-color-293243, #293243))] focus:outline-none focus-visible:bg-[var(--surface-elevated)] focus-visible:border-t-[var(--focus-ring, var(--theme-color-44d0ff, #44d0ff))] focus-visible:ring-2 focus-visible:ring-[var(--focus-ring, var(--theme-color-44d0ff, #44d0ff))] focus-visible:ring-opacity-30 ${stockBorder}`} onClick={() => onOpenDetail(row)} onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onOpenDetail(row); } }}>
                    <td className="px-4 py-3"><div className="flex flex-col"><span className="font-semibold text-white">{row.naziv}</span><span className="text-xs text-[var(--text-primary)]">{row.plu ?? "Bez PLU"} | {getCoverageText(row)}</span></div></td>
                    <td className="px-4 py-3"><span className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${stock.badge}`}>{row.stockStateLabel}</span></td>
                    <td className="px-4 py-3 text-right font-semibold text-white">{formatNumber(row.quantity)}</td>
                    <td className="px-4 py-3 text-right text-[var(--text-primary)]">{formatNumber(row.minimum)}</td>
                    <td className="px-4 py-3 text-right text-[var(--text-primary)]">{formatNumber(row.reorderGap)}</td>
                    <td className="px-4 py-3 text-right text-[var(--text-primary)]">{formatCurrency(row.unitCost)}</td>
                    <td className="px-4 py-3 text-right font-semibold text-[var(--text-primary)]">{formatCurrency(row.estimatedValueAmount)}</td>
                    <td className="px-4 py-3 text-[var(--text-primary)]">{row.storeName}</td>
                    <td className="px-4 py-3 text-[var(--text-primary)]">{row.supplierName}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>

      <div className="mt-4 flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <div className="text-sm text-[var(--text-primary)]">Strana <span className="font-semibold text-white">{pageNumber}</span> od <span className="font-semibold text-white">{totalPages}</span></div>
        <div className="flex items-center gap-2">
          <button type="button" aria-label="Idi na prethodnu stranu tabele artikala" onClick={onPreviousPage} disabled={pageNumber <= 1 || loading} className="rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-3 py-2 text-xs font-semibold text-[var(--text-primary)] transition-all duration-200 hover:border-[var(--border-default)] hover:bg-[var(--surface-light)] hover:shadow-md focus:outline-none focus-visible:border-[var(--border-default)] focus-visible:ring-2 focus-visible:ring-[var(--theme-color-44d0ff, #44d0ff)] focus-visible:ring-opacity-30 disabled:cursor-not-allowed disabled:opacity-50 disabled:hover:border-[var(--border-default)] disabled:hover:bg-[var(--surface-light)] disabled:hover:shadow-none">Prethodna</button>
          <button type="button" aria-label="Idi na sledecu stranu tabele artikala" onClick={onNextPage} disabled={pageNumber >= totalPages || loading} className="rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-3 py-2 text-xs font-semibold text-[var(--text-primary)] transition-all duration-200 hover:border-[var(--border-default)] hover:bg-[var(--surface-light)] hover:shadow-md focus:outline-none focus-visible:border-[var(--border-default)] focus-visible:ring-2 focus-visible:ring-[var(--theme-color-44d0ff, #44d0ff)] focus-visible:ring-opacity-30 disabled:cursor-not-allowed disabled:opacity-50 disabled:hover:border-[var(--border-default)] disabled:hover:bg-[var(--surface-light)] disabled:hover:shadow-none">Sledeca</button>
        </div>
      </div>
    </section>
  );
}


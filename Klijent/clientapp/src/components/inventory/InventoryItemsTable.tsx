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
    <section className="rounded-[28px] border border-[#232935] bg-[#12161f] p-5">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h2 className="text-lg font-semibold text-white">Tabela artikala</h2>
          <p className="text-sm text-[#90a0ba]">Klik na red otvara detalj sa preporukom akcije i operativnim kontekstom.</p>
        </div>
        <div className="text-sm text-[#96a5bf]">Prikazano <span className="font-semibold text-white">{rows.length}</span> od <span className="font-semibold text-white">{formatNumber(totalCount)}</span> artikala</div>
      </div>

      <div className="mt-4 overflow-hidden rounded-2xl border border-[#242d3b]">
        <div className="overflow-x-auto">
          <table aria-label="Lista artikala na stanju" className="min-w-full text-sm">
            <thead className="bg-[#0f131a] text-left text-[#90a0ba]">
              <tr>
                <th className="px-4 py-3">Artikal</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3 text-right">Kolicina</th>
                <th className="px-4 py-3 text-right">Minimum</th>
                <th className="px-4 py-3 text-right">Gap</th>
                <th className="px-4 py-3 text-right">Nabavna</th>
                <th className="px-4 py-3 text-right">Vrednost</th>
                <th className="px-4 py-3">Prodavnica</th>
                <th className="px-4 py-3">Dobavljac</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr><td colSpan={9} className="px-4 py-10 text-center text-[#8797b4]">Ucitavam tabelu...</td></tr>
              ) : rows.length === 0 ? (
                <tr><td colSpan={9} className="px-4 py-10 text-center text-[#8797b4]">Nema artikala za zadate filtere.</td></tr>
              ) : rows.map((row) => {
                const stock = getStockState(row.quantity, row.minimum);
                const stockBorder = row.stockState === "critical" ? "border-l-4 border-l-[#7d2940]" : row.stockState === "warning" ? "border-l-4 border-l-[#7c5822]" : "border-l-4 border-l-[#1f6c49]";
                return (
                  <tr key={row.id} className={`cursor-pointer border-t border-[#1c2230] bg-[#11161d] text-[#dbe6fb] transition hover:bg-[#151c26] ${stockBorder}`} onClick={() => onOpenDetail(row)}>
                    <td className="px-4 py-3"><div className="flex flex-col"><span className="font-semibold text-white">{row.naziv}</span><span className="text-xs text-[#8fa1be]">{row.plu ?? "Bez PLU"} | {getCoverageText(row)}</span></div></td>
                    <td className="px-4 py-3"><span className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${stock.badge}`}>{row.stockStateLabel}</span></td>
                    <td className="px-4 py-3 text-right font-semibold text-white">{formatNumber(row.quantity)}</td>
                    <td className="px-4 py-3 text-right text-[#c7d4e8]">{formatNumber(row.minimum)}</td>
                    <td className="px-4 py-3 text-right text-[#f7c983]">{formatNumber(row.reorderGap)}</td>
                    <td className="px-4 py-3 text-right text-[#b8d7f0]">{formatCurrency(row.unitCost)}</td>
                    <td className="px-4 py-3 text-right font-semibold text-[#9fe0ff]">{formatCurrency(row.estimatedValueAmount)}</td>
                    <td className="px-4 py-3 text-[#c7d4e8]">{row.storeName}</td>
                    <td className="px-4 py-3 text-[#c7d4e8]">{row.supplierName}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>

      <div className="mt-4 flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <div className="text-sm text-[#90a0ba]">Strana <span className="font-semibold text-white">{pageNumber}</span> od <span className="font-semibold text-white">{totalPages}</span></div>
        <div className="flex items-center gap-2">
          <button type="button" aria-label="Idi na prethodnu stranu tabele artikala" onClick={onPreviousPage} disabled={pageNumber <= 1 || loading} className="rounded-xl border border-[#33405a] bg-[#182131] px-3 py-2 text-xs font-semibold text-[#dbe6fb] disabled:cursor-not-allowed disabled:opacity-50">Prethodna</button>
          <button type="button" aria-label="Idi na sledecu stranu tabele artikala" onClick={onNextPage} disabled={pageNumber >= totalPages || loading} className="rounded-xl border border-[#33405a] bg-[#182131] px-3 py-2 text-xs font-semibold text-[#dbe6fb] disabled:cursor-not-allowed disabled:opacity-50">Sledeca</button>
        </div>
      </div>
    </section>
  );
}

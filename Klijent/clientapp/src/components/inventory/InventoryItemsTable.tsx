import KpiExplainButton from "../analytics/KpiExplainButton";
import {
  formatCurrency,
  formatNumber,
  formatSellThroughRatio,
  formatStockCoverDays,
  getCoverageText,
  getStockState,
} from "./inventoryUtils";
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
  onAddToActions: (row: InventoryRow) => void;
  onReviewSlowStock: (row: InventoryRow) => void;
  isRowQueued: (row: InventoryRow) => boolean;
  isRowQueueBusy: (row: InventoryRow) => boolean;
};

function isActionableLowCover(status: string): boolean {
  const normalized = (status ?? "").trim().toLowerCase();
  return normalized === "low_cover" || normalized === "low" || normalized === "out_of_stock_risk";
}

function isSlowSignal(status: string): boolean {
  const normalized = (status ?? "").trim().toLowerCase();
  return normalized === "slow_stock" || normalized === "slow" || normalized === "no_velocity";
}

function isSignalReviewOnly(status: string): boolean {
  const normalized = (status ?? "").trim().toLowerCase();
  return normalized === "insufficient_data";
}

export function InventoryItemsTable({
  rows,
  loading,
  totalCount,
  pageNumber,
  totalPages,
  onOpenDetail,
  onPreviousPage,
  onNextPage,
  onAddToActions,
  onReviewSlowStock,
  isRowQueued,
  isRowQueueBusy,
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
                <th className="px-4 py-3">Artikal</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3 text-right">Kolicina</th>
                <th className="px-4 py-3 text-right">Minimum</th>
                <th className="px-4 py-3 text-right">Gap</th>
                <th className="px-4 py-3">Pokrivenost zalihe</th>
                <th className="px-4 py-3">Sell-through</th>
                <th className="px-4 py-3">Signal</th>
                <th className="px-4 py-3 text-right">Nabavna</th>
                <th className="px-4 py-3 text-right">Vrednost</th>
                <th className="px-4 py-3">Prodavnica</th>
                <th className="px-4 py-3">Dobavljac</th>
              </tr>
              <tr>
                <th className="px-4 py-2" colSpan={5}></th>
                <th className="px-4 py-2">
                  <KpiExplainButton metricKey="stockCoverDays" ariaLabel="Kako je izračunata pokrivenost zalihe" />
                </th>
                <th className="px-4 py-2">
                  <KpiExplainButton metricKey="sellThrough" ariaLabel="Kako je izračunat sell-through signal" />
                </th>
                <th className="px-4 py-2"></th>
                <th className="px-4 py-2" colSpan={4}></th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr><td colSpan={12} className="px-4 py-10 text-center text-[var(--text-primary)]">Ucitavam tabelu...</td></tr>
              ) : rows.length === 0 ? (
                <tr><td colSpan={12} className="px-4 py-10 text-center text-[var(--text-primary)]">Nema artikala za zadate filtere.</td></tr>
              ) : rows.map((row) => {
                const stock = getStockState(row.quantity, row.minimum);
                const stockBorder = row.stockState === "critical" ? "border-l-4 border-l-[var(--border-default)]" : row.stockState === "warning" ? "border-l-4 border-l-[var(--border-default)]" : "border-l-4 border-l-[var(--border-default)]";
                const isQueued = isRowQueued(row);
                const isQueueBusy = isRowQueueBusy(row);
                const showQueueButton = isActionableLowCover(row.stockCoverStatus) || isSlowSignal(row.stockCoverStatus) || isSignalReviewOnly(row.stockCoverStatus);
                return (
                  <tr key={row.id} role="button" tabIndex={0} aria-label={`Otvori detalje za ${row.naziv} - ${row.stockStateLabel}`} className={`cursor-pointer border-t border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)] transition-all duration-200 hover:bg-[var(--surface-light)] hover:border-t-[var(--border-hover, var(--theme-color-293243, #293243))] focus:outline-none focus-visible:bg-[var(--surface-elevated)] focus-visible:border-t-[var(--focus-ring, var(--theme-color-44d0ff, #44d0ff))] focus-visible:ring-2 focus-visible:ring-[var(--focus-ring, var(--theme-color-44d0ff, #44d0ff))] focus-visible:ring-opacity-30 ${stockBorder}`} onClick={() => onOpenDetail(row)} onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); onOpenDetail(row); } }}>
                    <td className="px-4 py-3"><div className="flex flex-col"><span className="font-semibold text-white">{row.naziv}</span><span className="text-xs text-[var(--text-primary)]">{row.plu ?? "Bez PLU"} | {getCoverageText(row)}</span></div></td>
                    <td className="px-4 py-3"><span className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${stock.badge}`}>{row.stockStateLabel}</span></td>
                    <td className="px-4 py-3 text-right font-semibold text-white">{formatNumber(row.quantity)}</td>
                    <td className="px-4 py-3 text-right text-[var(--text-primary)]">{formatNumber(row.minimum)}</td>
                    <td className="px-4 py-3 text-right text-[var(--text-primary)]">{formatNumber(row.reorderGap)}</td>
                    <td className="px-4 py-3">
                      <div className="flex flex-col gap-1">
                        <span className="font-semibold text-white">{formatStockCoverDays(row.stockCoverDays, row.stockCoverStatus)}</span>
                        <span>{row.stockCoverStatusLabel}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex flex-col gap-1">
                        <span className="font-semibold text-white">{formatSellThroughRatio(row.sellThroughRatio, row.sellThroughStatus)}</span>
                        <span>{row.sellThroughStatusLabel}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex flex-col gap-2">
                        <span>{row.signalText}</span>
                        {showQueueButton ? (
                          <button
                            type="button"
                            className="rounded-lg border border-[var(--warning)] bg-[var(--surface-darker)] px-2 py-1 text-xs font-semibold text-[var(--warning)]"
                            disabled={isQueueBusy || isQueued}
                            onClick={(event) => {
                              event.stopPropagation();
                              onAddToActions(row);
                            }}
                          >
                            {isQueueBusy ? "Dodavanje..." : isQueued ? "U akcijama" : "Dodaj u akcije"}
                          </button>
                        ) : null}
                        {isSlowSignal(row.stockCoverStatus) ? (
                          <button
                            type="button"
                            className="rounded-lg border border-[var(--info)] bg-[var(--surface-darker)] px-2 py-1 text-xs font-semibold text-[var(--info)]"
                            onClick={(event) => {
                              event.stopPropagation();
                              onReviewSlowStock(row);
                            }}
                          >
                            Pregledaj sporu zalihu
                          </button>
                        ) : null}
                      </div>
                    </td>
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


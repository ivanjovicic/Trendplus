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

function isInsufficientStatus(status: string): boolean {
  return (status ?? "").trim().toLowerCase() === "insufficient_data";
}

function stockAccentClass(state: string): string {
  if (state === "critical") return "border-l-[var(--error)]";
  if (state === "warning" || state === "unknown") return "border-l-[var(--warning)]";
  return "border-l-[var(--success)]";
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
    <section className="rounded-[30px] border border-[var(--border-default)] bg-[linear-gradient(180deg,var(--surface-elevated)_0%,var(--surface-default)_100%)] p-5 shadow-[0_24px_70px_-56px_rgba(0,0,0,0.9)]">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <p className="mb-1 text-[11px] font-extrabold uppercase tracking-[0.16em] text-muted">Inventory analytics</p>
          <h2 className="text-xl font-semibold tracking-[-0.03em] text-contrast">Tabela artikala</h2>
          <p className="text-sm leading-relaxed text-secondary">Klik na red otvara detalj sa preporukom akcije, zalihom, prodavnicom i operativnim kontekstom.</p>
        </div>
        <div className="inline-flex w-fit items-center gap-2 rounded-2xl border border-[var(--border-default)] bg-[var(--surface-light)] px-3 py-2 text-sm text-secondary">
          Prikazano <span className="font-semibold text-contrast">{rows.length.toLocaleString("sr-RS")}</span> od <span className="font-semibold text-contrast">{formatNumber(totalCount)}</span> artikala
        </div>
      </div>

      <div className="mt-4 overflow-hidden rounded-3xl border border-[var(--border-default)] bg-[var(--surface-darker)]">
        <div className="overflow-x-auto">
          <table aria-label="Lista artikala na stanju" className="min-w-full border-separate border-spacing-0 text-sm">
            <thead className="bg-[var(--surface-darker)] text-left text-secondary">
              <tr>
                <th className="px-4 py-3 text-xs font-black uppercase tracking-[0.08em]">Artikal</th>
                <th className="px-4 py-3 text-xs font-black uppercase tracking-[0.08em]">Status</th>
                <th className="px-4 py-3 text-right text-xs font-black uppercase tracking-[0.08em]">Količina</th>
                <th className="px-4 py-3 text-right text-xs font-black uppercase tracking-[0.08em]">Minimum</th>
                <th className="px-4 py-3 text-right text-xs font-black uppercase tracking-[0.08em]">Gap</th>
                <th className="px-4 py-3 text-xs font-black uppercase tracking-[0.08em]">Pokrivenost zalihe</th>
                <th className="px-4 py-3 text-xs font-black uppercase tracking-[0.08em]">Sell-through</th>
                <th className="px-4 py-3 text-xs font-black uppercase tracking-[0.08em]">Signal</th>
                <th className="px-4 py-3 text-right text-xs font-black uppercase tracking-[0.08em]">Nabavna</th>
                <th className="px-4 py-3 text-right text-xs font-black uppercase tracking-[0.08em]">Vrednost</th>
                <th className="px-4 py-3 text-xs font-black uppercase tracking-[0.08em]">Prodavnica</th>
                <th className="px-4 py-3 text-xs font-black uppercase tracking-[0.08em]">Dobavljač</th>
              </tr>
              <tr className="border-t border-[var(--border-default)] bg-[var(--surface-darker)]">
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
                <tr><td colSpan={12} className="px-4 py-12 text-center text-secondary">Učitavam tabelu...</td></tr>
              ) : rows.length === 0 ? (
                <tr><td colSpan={12} className="px-4 py-12 text-center text-secondary">Nema artikala za zadate filtere.</td></tr>
              ) : rows.map((row) => {
                const stock = getStockState(row.quantity, row.minimum);
                const stockBorder = `border-l-4 ${stockAccentClass(row.stockState)}`;
                const isQueued = isRowQueued(row);
                const isQueueBusy = isRowQueueBusy(row);
                const showQueueButton = row.recommendationAllowed === false
                  || isActionableLowCover(row.stockCoverStatus)
                  || isSlowSignal(row.stockCoverStatus)
                  || isSignalReviewOnly(row.stockCoverStatus)
                  || isInsufficientStatus(row.sellThroughStatus);
                return (
                  <tr
                    key={row.id}
                    role="button"
                    tabIndex={0}
                    aria-label={`Otvori detalje za ${row.naziv} - ${row.stockStateLabel}`}
                    className={`cursor-pointer bg-[var(--surface-elevated)] text-[var(--text-primary)] transition-all duration-200 hover:bg-[var(--surface-light)] focus:outline-none focus-visible:bg-[var(--surface-elevated)] focus-visible:ring-2 focus-visible:ring-[var(--focus-ring, var(--theme-color-44d0ff, #44d0ff))] focus-visible:ring-opacity-30 ${stockBorder}`}
                    onClick={() => onOpenDetail(row)}
                    onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); onOpenDetail(row); } }}
                  >
                    <td className="border-t border-[var(--border-default)] px-4 py-3">
                      <div className="flex flex-col gap-1">
                        <span className="font-semibold text-contrast">{row.naziv}</span>
                        <span className="text-xs text-secondary">{row.plu ?? "Bez PLU"} | {getCoverageText(row)}</span>
                      </div>
                    </td>
                    <td className="border-t border-[var(--border-default)] px-4 py-3"><span className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${stock.badge}`}>{row.stockStateLabel}</span></td>
                    <td className="border-t border-[var(--border-default)] px-4 py-3 text-right font-semibold text-contrast">{formatNumber(row.quantity)}</td>
                    <td className="border-t border-[var(--border-default)] px-4 py-3 text-right text-secondary">{formatNumber(row.minimum)}</td>
                    <td className="border-t border-[var(--border-default)] px-4 py-3 text-right text-secondary">{formatNumber(row.reorderGap)}</td>
                    <td className="border-t border-[var(--border-default)] px-4 py-3">
                      <div className="flex flex-col gap-1">
                        <span className="font-semibold text-contrast">{formatStockCoverDays(row.stockCoverDays, row.stockCoverStatus)}</span>
                        <span className="text-secondary">{row.stockCoverStatusLabel}</span>
                      </div>
                    </td>
                    <td className="border-t border-[var(--border-default)] px-4 py-3">
                      <div className="flex flex-col gap-1">
                        <span className="font-semibold text-contrast">{formatSellThroughRatio(row.sellThroughRatio, row.sellThroughStatus)}</span>
                        <span className="text-secondary">{row.sellThroughStatusLabel}</span>
                      </div>
                    </td>
                    <td className="border-t border-[var(--border-default)] px-4 py-3">
                      <div className="flex flex-col gap-2 text-secondary">
                        <span>{row.signalText}</span>
                        {showQueueButton ? (
                          <button
                            type="button"
                            className="w-fit rounded-full border border-[var(--warning)] bg-warning-soft px-2.5 py-1 text-xs font-semibold text-[var(--warning)] transition hover:translate-y-[-1px]"
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
                            className="w-fit rounded-full border border-[var(--info)] bg-[var(--info)]/10 px-2.5 py-1 text-xs font-semibold text-[var(--info)] transition hover:translate-y-[-1px]"
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
                    <td className="border-t border-[var(--border-default)] px-4 py-3 text-right text-secondary">{formatCurrency(row.unitCost)}</td>
                    <td className="border-t border-[var(--border-default)] px-4 py-3 text-right font-semibold text-contrast">{formatCurrency(row.estimatedValueAmount)}</td>
                    <td className="border-t border-[var(--border-default)] px-4 py-3 text-secondary">{row.storeName}</td>
                    <td className="border-t border-[var(--border-default)] px-4 py-3 text-secondary">{row.supplierName}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>

      <div className="mt-4 flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <div className="text-sm text-secondary">Strana <span className="font-semibold text-contrast">{pageNumber}</span> od <span className="font-semibold text-contrast">{totalPages}</span></div>
        <div className="flex items-center gap-2">
          <button type="button" aria-label="Idi na prethodnu stranu tabele artikala" onClick={onPreviousPage} disabled={pageNumber <= 1 || loading} className="rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-3 py-2 text-xs font-semibold text-secondary transition-all duration-200 hover:border-[var(--info)] hover:bg-[var(--surface-light)] hover:text-contrast disabled:cursor-not-allowed disabled:opacity-50">Prethodna</button>
          <button type="button" aria-label="Idi na sledeću stranu tabele artikala" onClick={onNextPage} disabled={pageNumber >= totalPages || loading} className="rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-3 py-2 text-xs font-semibold text-secondary transition-all duration-200 hover:border-[var(--info)] hover:bg-[var(--surface-light)] hover:text-contrast disabled:cursor-not-allowed disabled:opacity-50">Sledeća</button>
        </div>
      </div>
    </section>
  );
}

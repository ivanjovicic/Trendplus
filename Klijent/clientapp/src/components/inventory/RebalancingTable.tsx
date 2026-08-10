import { ArrowRightLeft } from "lucide-react";
import type { RebalanceListDto, StoreOption } from "../../types/analytics";
import { fmtNumber } from "../../utils/analyticsFormatters";
import { formatCurrency, formatSignalCountBadge, getRebalanceUrgencyTone } from "./inventoryUtils";
import type { InventoryRow } from "./types";

type RebalancingTableProps = {
  rebalance: RebalanceListDto | null;
  rebalanceLoading: boolean;
  rebalanceError?: string | null;
  rows: InventoryRow[];
  stores: StoreOption[];
  displayCount: number;
  scopeLabel: string;
  onCompareStores: (fromStoreId: number, toStoreId: number) => void;
};

export function RebalancingTable({
  rebalance,
  rebalanceLoading,
  rebalanceError,
  rows,
  stores,
  displayCount,
  scopeLabel,
  onCompareStores,
}: RebalancingTableProps) {
  return (
    <section className="rounded-[28px] border border-[var(--border-default)] bg-[var(--surface-elevated)] p-5">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div className="flex items-center gap-3">
          <div className="rounded-2xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-2.5 text-[var(--text-primary)]">
            <ArrowRightLeft size={18} />
          </div>
          <div>
            <h2 className="text-lg font-semibold text-foreground">Pametna redistribucija</h2>
            <p className="text-sm text-[var(--text-primary)]">Predlozi za redistribuciju robe između lokacija. Sortirano po urgentnosti i očekivanim uštedama.</p>
            <div className="mt-2 inline-flex rounded-full border border-[var(--border-default)] bg-[var(--surface-elevated)] px-3 py-1 text-xs font-semibold text-[var(--text-primary)]">
              Opseg: {scopeLabel}
            </div>
          </div>
        </div>
        <div className="rounded-full border border-[var(--border-default)] bg-[var(--surface-elevated)] px-3 py-1 text-xs font-semibold text-[var(--text-primary)]">
          {rebalanceLoading ? "Učitavam..." : formatSignalCountBadge(rebalance?.returnedCount ?? rebalance?.totalCount, rebalance?.totalMatchingCount, "predloga", rebalance?.isTruncated)}
        </div>
      </div>

      {rebalanceError ? (
        <div className="mt-4 rounded-2xl border border-[var(--error)] bg-[var(--surface-elevated)] px-4 py-8 text-center text-sm text-[var(--error)]">
          {rebalanceError}
        </div>
      ) : !rebalance?.snapshotAvailable ? (
        <div className="mt-4 rounded-2xl border border-dashed border-[var(--border-default)] bg-[var(--surface-elevated)] px-4 py-8 text-center text-sm text-[var(--text-primary)]">
          {rebalanceLoading ? "Učitavam predloge za redistribuciju..." : "Redistribucija nije dostupna. Snapshot tabela je prazna."}
        </div>
      ) : (rebalance.items ?? []).length === 0 ? (
        <div className="mt-4 rounded-2xl border border-dashed border-[var(--border-default)] bg-[var(--surface-elevated)] px-4 py-8 text-center text-sm text-[var(--text-primary)]">
          Nema preporučenih redistribucija {scopeLabel}.
        </div>
      ) : (
        <div className="mt-4 overflow-hidden rounded-2xl border border-[var(--border-default)]">
          <div className="overflow-x-auto">
            <table className="min-w-full text-sm">
              <thead className="bg-[var(--surface-elevated)] text-left text-[var(--text-primary)]">
                <tr>
                  <th className="px-4 py-3">Urgentnost</th>
                  <th className="px-4 py-3">Iz</th>
                  <th className="px-4 py-3">U</th>
                  <th className="px-4 py-3">SKU</th>
                  <th className="px-4 py-3">Vel.</th>
                  <th className="px-4 py-3 text-right">Qty</th>
                  <th className="px-4 py-3 text-right">Sačuvana prodaja</th>
                  <th className="px-4 py-3">Razlog</th>
                  <th className="px-4 py-3 text-right">Akcija</th>
                </tr>
              </thead>
              <tbody>
                {rebalance.items.slice(0, displayCount).map((item, index) => {
                  const name = rows.find((row) => row.id === item.skuId)?.naziv ?? `SKU #${item.skuId}`;
                  const fromStore = stores.find((store) => store.storeId === item.fromStoreId)?.storeName ?? `#${item.fromStoreId}`;
                  const toStore = stores.find((store) => store.storeId === item.toStoreId)?.storeName ?? `#${item.toStoreId}`;
                  const urgencyLabel = item.urgency === "urgent" ? "Hitno" : item.urgency === "recommended" ? "Preporučeno" : item.urgency === "optional" ? "Opciono" : "Nepoznato";
                  const urgencyTone = item.urgency ? getRebalanceUrgencyTone(item.urgency) : "border-dashed border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]";

                  return (
                    <tr key={`${item.skuId}-${item.fromStoreId}-${item.toStoreId}-${item.sizeCode}-${index}`} className={`border-t border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)] hover:bg-[var(--surface-light)] ${item.urgency === "urgent" ? "border-l-4 border-l-[var(--border-default)]" : ""}`}>
                      <td className="px-4 py-3">
                        <span className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${urgencyTone}`}>
                          {urgencyLabel}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-[var(--text-primary)]">{fromStore}</td>
                      <td className="px-4 py-3 text-[var(--text-primary)]">{toStore}</td>
                      <td className="px-4 py-3 font-semibold text-foreground">{name}</td>
                      <td className="px-4 py-3 text-[var(--text-primary)]">{item.sizeCode}</td>
                      <td className="px-4 py-3 text-right font-semibold text-foreground">{fmtNumber(item.recommendedQty)}</td>
                      <td className="px-4 py-3 text-right text-[var(--text-primary)]">{formatCurrency(item.expectedSavedSales)}</td>
                      <td className="max-w-[220px] truncate px-4 py-3 text-[var(--text-primary)]">{item.reason}</td>
                      <td className="px-4 py-3 text-right">
                        <button type="button" aria-label={`Uporedi lokacije ${fromStore} i ${toStore}`} onClick={() => onCompareStores(item.fromStoreId, item.toStoreId)} className="rounded-lg border border-[var(--border-default)] bg-[var(--surface-elevated)] px-2.5 py-1 text-[11px] font-semibold text-[var(--text-primary)] transition-all duration-200 hover:border-[var(--border-default)] hover:bg-[var(--surface-light)] hover:text-foreground hover:shadow-md focus:outline-none focus-visible:border-[var(--border-default)] focus-visible:ring-2 focus-visible:ring-[var(--theme-color-44d0ff, #44d0ff)] focus-visible:ring-opacity-30">
                          Uporedi lokacije
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}
      {rebalance?.warning ? <p className="mt-3 text-xs text-warning">Napomena: {rebalance.warning}</p> : null}
    </section>
  );
}


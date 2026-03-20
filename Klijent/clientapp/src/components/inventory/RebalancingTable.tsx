import { ArrowRightLeft } from "lucide-react";
import type { RebalanceListDto, StoreOption } from "../../types/analytics";
import { formatCurrency, getRebalanceUrgencyTone } from "./inventoryUtils";
import type { InventoryRow } from "./types";

type RebalancingTableProps = {
  rebalance: RebalanceListDto | null;
  rebalanceLoading: boolean;
  rows: InventoryRow[];
  stores: StoreOption[];
  displayCount: number;
};

export function RebalancingTable({
  rebalance,
  rebalanceLoading,
  rows,
  stores,
  displayCount,
}: RebalancingTableProps) {
  return (
    <section className="rounded-[28px] border border-[#232935] bg-[#12161f] p-5">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div className="flex items-center gap-3">
          <div className="rounded-2xl border border-[#28574d] bg-[#102b24] p-2.5 text-[#9ff0c7]">
            <ArrowRightLeft size={18} />
          </div>
          <div>
            <h2 className="text-lg font-semibold text-white">Smart Rebalancing</h2>
            <p className="text-sm text-[#90a0ba]">Predlozi za redistribuciju robe izmedju lokacija. Sortirano po urgentnosti i ocekivanim ustedama.</p>
          </div>
        </div>
        <div className="rounded-full border border-[#33405a] bg-[#182131] px-3 py-1 text-xs font-semibold text-[#dbe6fb]">
          {rebalanceLoading ? "Ucitavam..." : `${rebalance?.totalCount ?? 0} predloga`}
        </div>
      </div>

      {!rebalance?.snapshotAvailable ? (
        <div className="mt-4 rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-8 text-center text-sm text-[#8797b4]">
          {rebalanceLoading ? "Ucitavam predloge za redistribuciju..." : "Redistribucija nije dostupna. Snapshot tabela je prazna."}
        </div>
      ) : (rebalance.items ?? []).length === 0 ? (
        <div className="mt-4 rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-8 text-center text-sm text-[#8797b4]">
          Nema preporucenih redistribucija za trenutne filtere.
        </div>
      ) : (
        <div className="mt-4 overflow-hidden rounded-2xl border border-[#242d3b]">
          <div className="overflow-x-auto">
            <table className="min-w-full text-sm">
              <thead className="bg-[#0f131a] text-left text-[#90a0ba]">
                <tr>
                  <th className="px-4 py-3">Urgentnost</th>
                  <th className="px-4 py-3">Iz</th>
                  <th className="px-4 py-3">U</th>
                  <th className="px-4 py-3">SKU</th>
                  <th className="px-4 py-3">Vel.</th>
                  <th className="px-4 py-3 text-right">Qty</th>
                  <th className="px-4 py-3 text-right">Sacuvana prodaja</th>
                  <th className="px-4 py-3">Razlog</th>
                </tr>
              </thead>
              <tbody>
                {rebalance.items.slice(0, displayCount).map((item, index) => {
                  const name = rows.find((row) => row.id === item.skuId)?.naziv ?? `SKU #${item.skuId}`;
                  const fromStore = stores.find((store) => store.storeId === item.fromStoreId)?.storeName ?? `#${item.fromStoreId}`;
                  const toStore = stores.find((store) => store.storeId === item.toStoreId)?.storeName ?? `#${item.toStoreId}`;

                  return (
                    <tr key={`${item.skuId}-${item.fromStoreId}-${item.toStoreId}-${item.sizeCode}-${index}`} className="border-t border-[#1c2230] bg-[#11161d] text-[#dbe6fb] hover:bg-[#151c26]">
                      <td className="px-4 py-3">
                        <span className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${getRebalanceUrgencyTone(item.urgency)}`}>
                          {item.urgency === "urgent" ? "Hitno" : item.urgency === "recommended" ? "Preporuceno" : "Opciono"}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-[#c7d4e8]">{fromStore}</td>
                      <td className="px-4 py-3 text-[#c7d4e8]">{toStore}</td>
                      <td className="px-4 py-3 font-semibold text-white">{name}</td>
                      <td className="px-4 py-3 text-[#c7d4e8]">{item.sizeCode}</td>
                      <td className="px-4 py-3 text-right font-semibold text-white">{item.recommendedQty}</td>
                      <td className="px-4 py-3 text-right text-[#9ff0c7]">{formatCurrency(item.expectedSavedSales)}</td>
                      <td className="max-w-[220px] truncate px-4 py-3 text-[#90a0ba]">{item.reason}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </section>
  );
}

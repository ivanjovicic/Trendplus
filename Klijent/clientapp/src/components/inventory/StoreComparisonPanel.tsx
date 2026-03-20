import { GitCompareArrows } from "lucide-react";
import type { InventoryStoreComparison, StoreOption } from "../../types/analytics";
import { buildStoreLabel, formatCurrency, formatNumber, formatPercent } from "./inventoryUtils";

type StoreComparisonPanelProps = {
  stores: StoreOption[];
  compareStoreIds: number[];
  comparison: InventoryStoreComparison | null;
  operationsLoading: boolean;
  onToggleStore: (storeId: number) => void;
  sectionId?: string;
};

export function StoreComparisonPanel({
  stores,
  compareStoreIds,
  comparison,
  operationsLoading,
  onToggleStore,
  sectionId,
}: StoreComparisonPanelProps) {
  const comparisonStores = comparison?.stores ?? [];
  const comparisonRisks = comparison?.sharedRisks ?? [];

  return (
    <section id={sectionId} className="rounded-[28px] border border-[#232935] bg-[#12161f] p-5">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h2 className="text-lg font-semibold text-white">Drill-down po prodavnici</h2>
          <p className="text-sm text-[#90a0ba]">Uporedi do tri lokacije po zdravlju zalihe, vezanom kapitalu i zajednickim rizicima.</p>
        </div>
        <div className="rounded-full border border-[#33405a] bg-[#182131] px-3 py-1 text-xs font-semibold text-[#dbe6fb]">
          {comparisonStores.length} lokacije u poredjenju
        </div>
      </div>

      <div className="mt-4 flex flex-wrap gap-2">
        {stores.map((store) => {
          const active = compareStoreIds.includes(store.storeId);
          return (
            <button key={store.storeId} type="button" onClick={() => onToggleStore(store.storeId)} className={`rounded-full border px-3 py-1.5 text-xs font-semibold transition ${active ? "border-[#30516d] bg-[#102231] text-[#8edbff]" : "border-[#33405a] bg-[#182131] text-[#dbe6fb]"}`}>
              {buildStoreLabel(store)}
            </button>
          );
        })}
      </div>

      <div className="mt-5 grid gap-3 md:grid-cols-2 xl:grid-cols-3">
        {operationsLoading && comparisonStores.length === 0 ? <div className="col-span-full rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-8 text-center text-sm text-[#8797b4]">Ucitavam poredjenje lokacija...</div> : comparisonStores.map((store) => (
          <article key={store.storeId} className="rounded-2xl border border-[#243040] bg-[#10141b] p-4">
            <div className="flex items-start justify-between gap-3">
              <div>
                <div className="text-sm font-semibold text-white">{store.storeName}</div>
                <div className="mt-1 text-xs text-[#90a0ba]">{formatNumber(store.totalSku)} SKU | {formatCurrency(store.estimatedValue)}</div>
              </div>
              <GitCompareArrows size={16} className="text-[#8edbff]" />
            </div>
            <div className="mt-4 grid grid-cols-2 gap-3 text-sm">
              <div className="rounded-2xl bg-[#131e2b] p-3"><div className="text-xs uppercase tracking-[0.18em] text-[#89d9ff]">Healthy</div><div className="mt-2 text-lg font-semibold text-white">{formatPercent(store.healthySharePct)}</div></div>
              <div className="rounded-2xl bg-[#241b11] p-3"><div className="text-xs uppercase tracking-[0.18em] text-[#f0c36b]">Low stock</div><div className="mt-2 text-lg font-semibold text-white">{formatNumber(store.lowStockCount)}</div></div>
              <div className="rounded-2xl bg-[#26161a] p-3"><div className="text-xs uppercase tracking-[0.18em] text-[#ffbdcb]">Critical</div><div className="mt-2 text-lg font-semibold text-white">{formatNumber(store.criticalCount)}</div></div>
              <div className="rounded-2xl bg-[#1d1726] p-3"><div className="text-xs uppercase tracking-[0.18em] text-[#c4a3ff]">90+ dana</div><div className="mt-2 text-lg font-semibold text-white">{formatNumber(store.stale90PlusCount)}</div></div>
            </div>
          </article>
        ))}
      </div>

      <div className="mt-5 rounded-2xl border border-[#243040] bg-[#10141b] p-4">
        <div className="text-sm font-semibold text-white">Zakljucak poredjenja</div>
        <div className="mt-2 text-sm leading-6 text-[#90a0ba]">{comparison?.summary ?? "Nema dovoljno podataka za zakljucak."}</div>
        <div className="mt-4 space-y-3">
          {comparisonRisks.length === 0 ? <div className="text-sm text-[#8797b4]">Za izabrane lokacije nema zajednickih low-stock rizika.</div> : comparisonRisks.map((risk) => (
            <div key={risk.skuKey} className="flex items-center justify-between gap-3 rounded-xl border border-[#283142] bg-[#141b26] px-3 py-3">
              <div>
                <div className="text-sm font-semibold text-white">{risk.label}</div>
                <div className="mt-1 text-xs text-[#90a0ba]">{risk.impactedStores.join(" | ")}</div>
              </div>
              <div className="rounded-full border border-[#7c5822] bg-[#412d11] px-2.5 py-1 text-[11px] font-semibold text-[#ffd590]">{risk.storeCoverage} lokacije</div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}

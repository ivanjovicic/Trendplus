import { TrendingDown, TrendingUp } from "lucide-react";
import type { ForecastDto, StoreOption } from "../../types/analytics";
import { TONE } from "./toneMap";
import type { InventoryRow } from "./types";

type DemandForecastPanelProps = {
  forecast: ForecastDto | null;
  forecastLoading: boolean;
  forecastError: string | null;
  rows: InventoryRow[];
  stores: StoreOption[];
  oosThreshold: number;
  overstockThreshold: number;
  oosDisplayCount: number;
  overstockDisplayCount: number;
};

export function DemandForecastPanel({
  forecast,
  forecastLoading,
  forecastError,
  rows,
  stores,
  oosThreshold,
  overstockThreshold,
  oosDisplayCount,
  overstockDisplayCount,
}: DemandForecastPanelProps) {
  const highOosItems = (forecast?.items ?? [])
    .filter((item) => item.probabilityOfOOSIn7d > oosThreshold)
    .sort((left, right) => right.probabilityOfOOSIn7d - left.probabilityOfOOSIn7d)
    .slice(0, oosDisplayCount);

  const overstockItems = (forecast?.items ?? [])
    .filter((item) => item.overstockRisk > overstockThreshold)
    .sort((left, right) => right.overstockRisk - left.overstockRisk)
    .slice(0, overstockDisplayCount);

  return (
    <section className="rounded-[28px] border border-[#232935] bg-[#12161f] p-5">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div className="flex items-center gap-3">
          <div className="rounded-2xl border border-[#30516d] bg-[#102231] p-2.5 text-[#8edbff]">
            <TrendingDown size={18} />
          </div>
          <div>
            <h2 className="text-lg font-semibold text-white">Demand Forecast &amp; Out-of-Stock Risk</h2>
            <p className="text-sm text-[#90a0ba]">Prognoza potraznje po SKU i velicini. Rizik OOS u 7 dana i overstock signali.</p>
          </div>
        </div>
        <div className="rounded-full border border-[#33405a] bg-[#182131] px-3 py-1 text-xs font-semibold text-[#dbe6fb]">
          {forecastLoading ? "Ucitavam..." : `${forecast?.totalCount ?? 0} SKU u prognozi`}
        </div>
      </div>

      {!forecast?.snapshotAvailable ? (
        <div className="mt-4 rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-8 text-center text-sm text-[#8797b4]">
          {forecastLoading ? "Ucitavam forecast..." : forecastError ?? "Forecast nije dostupan. Snapshot tabela je prazna."}
          {forecast?.warning ? <div className="mt-2 text-xs text-[#ffd590]">{forecast.warning}</div> : null}
        </div>
      ) : (
        <div className="mt-4 grid gap-5 xl:grid-cols-2">
          <div className="rounded-2xl border border-[#243040] bg-[#10141b] p-4">
            <h3 className="flex items-center gap-2 text-sm font-semibold text-white">
              <TrendingDown size={14} className="text-[#ffb4c2]" />
              Najveci OOS rizik u 7 dana
            </h3>
            <div className="mt-3 space-y-2">
              {highOosItems.map((item) => {
                const name = rows.find((row) => row.id === item.skuId)?.naziv ?? `SKU #${item.skuId}`;
                const store = stores.find((entry) => entry.storeId === item.storeId)?.storeName ?? `Objekat #${item.storeId}`;
                const tone = item.probabilityOfOOSIn7d > 0.7 ? TONE.severity.critical : item.probabilityOfOOSIn7d > 0.4 ? TONE.severity.warning : TONE.severity.info;

                return (
                  <div key={`${item.skuId}-${item.storeId}-${item.sizeCode}`} className="flex items-start justify-between gap-3 rounded-xl border border-[#283142] bg-[#141b26] px-3 py-2">
                    <div className="min-w-0">
                      <div className="truncate text-sm font-semibold text-white">{name}</div>
                      <div className="truncate text-xs text-[#90a0ba]">{store} | vel. {item.sizeCode}</div>
                      <div className="mt-1 text-xs text-[#8797b4]">{item.explanation}</div>
                    </div>
                    <div className="shrink-0 text-right">
                      <div className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${tone}`}>
                        {Math.round(item.probabilityOfOOSIn7d * 100)}% OOS
                      </div>
                      <div className="mt-1 text-xs text-[#7f8fa9]">7d: {item.forecast7d.toFixed(1)}</div>
                    </div>
                  </div>
                );
              })}
              {highOosItems.length === 0 ? (
                <div className="rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-6 text-center text-sm text-[#8797b4]">Nema visokog OOS rizika za trenutne filtere.</div>
              ) : null}
            </div>
          </div>

          <div className="rounded-2xl border border-[#243040] bg-[#10141b] p-4">
            <h3 className="flex items-center gap-2 text-sm font-semibold text-white">
              <TrendingUp size={14} className="text-[#9ff0c7]" />
              Overstock rizik (28 dana)
            </h3>
            <div className="mt-3 space-y-2">
              {overstockItems.map((item) => {
                const name = rows.find((row) => row.id === item.skuId)?.naziv ?? `SKU #${item.skuId}`;
                const store = stores.find((entry) => entry.storeId === item.storeId)?.storeName ?? `Objekat #${item.storeId}`;

                return (
                  <div key={`${item.skuId}-${item.storeId}-${item.sizeCode}`} className="flex items-start justify-between gap-3 rounded-xl border border-[#283142] bg-[#141b26] px-3 py-2">
                    <div className="min-w-0">
                      <div className="truncate text-sm font-semibold text-white">{name}</div>
                      <div className="truncate text-xs text-[#90a0ba]">{store} | vel. {item.sizeCode}</div>
                    </div>
                    <div className="shrink-0 text-right">
                      <div className="inline-flex rounded-full border border-[#36543f] bg-[#17261d] px-2.5 py-1 text-xs font-semibold text-[#aef3bf]">
                        {Math.round(item.overstockRisk * 100)}% over
                      </div>
                      <div className="mt-1 text-xs text-[#7f8fa9]">28d: {item.forecast28d.toFixed(1)}</div>
                    </div>
                  </div>
                );
              })}
              {overstockItems.length === 0 ? (
                <div className="rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-6 text-center text-sm text-[#8797b4]">Nema overstock signala za trenutne filtere.</div>
              ) : null}
            </div>
          </div>
        </div>
      )}
    </section>
  );
}

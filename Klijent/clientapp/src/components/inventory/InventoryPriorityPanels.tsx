import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { InventoryBalance } from "../../types/analytics";
import { formatCurrency, formatNumber, formatPercent } from "./inventoryUtils";
import type { InventoryRow } from "./types";

type InventoryPriorityPanelsProps = {
  rows: InventoryRow[];
  topRiskRows: InventoryRow[];
  highestValueRows: InventoryRow[];
  chartData: Array<{ supplierName: string; totalValue: number }>;
  balance: InventoryBalance | null;
  lowStockShare: number;
  totalCount: number;
  onOpenDetail: (row: InventoryRow) => void;
};

export function InventoryPriorityPanels({
  rows,
  topRiskRows,
  highestValueRows,
  chartData,
  balance,
  lowStockShare,
  totalCount,
  onOpenDetail,
}: InventoryPriorityPanelsProps) {
  return (
    <section className="grid gap-5 xl:grid-cols-[1.15fr_0.85fr]">
      <div className="rounded-[28px] border border-[#232935] bg-[#12161f] p-5">
        <div className="flex items-center justify-between">
          <div>
            <h2 className="text-lg font-semibold text-white">Rizik i prioriteti</h2>
            <p className="text-sm text-[#90a0ba]">Najrizicniji artikli i oni sa najvecom vezanom vrednoscu na trenutnoj strani.</p>
          </div>
          <span className="rounded-full border border-[#2d445e] bg-[#132031] px-3 py-1 text-xs font-semibold text-[#8edbff]">{rows.length} redova na ekranu</span>
        </div>

        <div className="mt-5 grid gap-4 lg:grid-cols-2">
          <div className="rounded-2xl border border-[#243040] bg-[#10141b] p-4">
            <h3 className="text-sm font-semibold text-white">Najveci rizici</h3>
            <div className="mt-3 space-y-3">
              {topRiskRows.length === 0 ? <div className="text-sm text-[#8797b4]">Nema rizicnih artikala na ovoj strani.</div> : topRiskRows.map((row) => (
                <button key={`risk-${row.id}`} type="button" onClick={() => onOpenDetail(row)} className="flex w-full items-center justify-between rounded-xl border border-[#283142] bg-[#141b26] px-3 py-3 text-left transition hover:border-[#3e4a61]">
                  <div className="min-w-0"><div className="truncate text-sm font-semibold text-white">{row.naziv}</div><div className="truncate text-xs text-[#94a3bd]">{row.plu ?? "Bez PLU"} | {row.supplierName}</div></div>
                  <div className="text-right"><div className="text-sm font-semibold text-[#ffd3db]">{row.quantity}</div><div className="text-xs text-[#f7b8c7]">{row.stockStateLabel}</div></div>
                </button>
              ))}
            </div>
          </div>

          <div className="rounded-2xl border border-[#243040] bg-[#10141b] p-4">
            <h3 className="text-sm font-semibold text-white">Najveca vrednost</h3>
            <div className="mt-3 space-y-3">
              {highestValueRows.length === 0 ? <div className="text-sm text-[#8797b4]">Nema podataka za prikaz.</div> : highestValueRows.map((row) => (
                <button key={`value-${row.id}`} type="button" onClick={() => onOpenDetail(row)} className="flex w-full items-center justify-between rounded-xl border border-[#283142] bg-[#141b26] px-3 py-3 text-left transition hover:border-[#3e4a61]">
                  <div className="min-w-0"><div className="truncate text-sm font-semibold text-white">{row.naziv}</div><div className="truncate text-xs text-[#94a3bd]">{row.storeName}</div></div>
                  <div className="text-right"><div className="text-sm font-semibold text-[#bde6ff]">{formatCurrency(row.estimatedValueAmount)}</div><div className="text-xs text-[#94a3bd]">{row.quantity} kom</div></div>
                </button>
              ))}
            </div>
          </div>
        </div>
      </div>

      <div className="rounded-[28px] border border-[#232935] bg-[#12161f] p-5">
        <div>
          <h2 className="text-lg font-semibold text-white">Vrednost po dobavljacu</h2>
          <p className="text-sm text-[#90a0ba]">Top dobavljaci po procenjenoj vrednosti u trenutnoj tabeli.</p>
        </div>
        <div className="mt-5 h-[320px]">
          {chartData.length === 0 ? (
            <div className="flex h-full items-center justify-center rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] text-sm text-[#8797b4]">Nema dovoljno podataka za grafikon.</div>
          ) : (
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={chartData} layout="vertical" margin={{ top: 10, right: 12, bottom: 10, left: 12 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#233042" />
                <XAxis type="number" tick={{ fill: "#92a4bf", fontSize: 12 }} tickFormatter={(value) => `${Math.round(Number(value) / 1000)}k`} />
                <YAxis type="category" dataKey="supplierName" width={110} tick={{ fill: "#92a4bf", fontSize: 11 }} />
                <Tooltip cursor={{ fill: "rgba(68,208,255,0.08)" }} formatter={(value: number | string | undefined) => formatCurrency(typeof value === "number" ? value : Number(value ?? 0))} />
                <Bar dataKey="totalValue" fill="#44d0ff" radius={[0, 10, 10, 0]} />
              </BarChart>
            </ResponsiveContainer>
          )}
        </div>
        <div className="mt-5 rounded-2xl border border-[#243040] bg-[#10141b] p-4">
          <div className="grid gap-3 sm:grid-cols-3">
            <div className="rounded-2xl bg-[#131e2b] p-3"><div className="text-xs uppercase tracking-[0.2em] text-[#89d9ff]">Bez zaliha</div><div className="mt-2 text-xl font-semibold text-white">{balance ? formatNumber(balance.outOfStockCount) : "-"}</div></div>
            <div className="rounded-2xl bg-[#241b11] p-3"><div className="text-xs uppercase tracking-[0.2em] text-[#f0c36b]">Low stock share</div><div className="mt-2 text-xl font-semibold text-white">{formatPercent(lowStockShare)}</div></div>
            <div className="rounded-2xl bg-[#1d1726] p-3"><div className="text-xs uppercase tracking-[0.2em] text-[#c4a3ff]">Ukupno filtrirano</div><div className="mt-2 text-xl font-semibold text-white">{formatNumber(totalCount)}</div></div>
          </div>
        </div>
      </div>
    </section>
  );
}

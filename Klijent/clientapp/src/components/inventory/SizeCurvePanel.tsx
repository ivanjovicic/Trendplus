import { Search } from "lucide-react";
import { Bar, CartesianGrid, ComposedChart, Line, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { SizeCurveDto } from "../../types/analytics";

type SizeCurvePanelProps = {
  sizeCurveSkuId: number | null;
  sizeCurve: SizeCurveDto | null;
  sizeCurveLoading: boolean;
  onChangeSkuId: (value: number | null) => void;
};

export function SizeCurvePanel({
  sizeCurveSkuId,
  sizeCurve,
  sizeCurveLoading,
  onChangeSkuId,
}: SizeCurvePanelProps) {
  const items = sizeCurve?.items ?? [];
  const missingCoreCount = items.filter((item) => item.isCoreSizeMissing).length;
  const deadSizeCount = items.filter((item) => item.isDeadSize).length;
  const hasBrokenRun = items.some((item) => item.brokenRun);

  return (
    <section className="rounded-[28px] border border-[#232935] bg-[#12161f] p-5">
      <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
        <div>
          <h2 className="text-lg font-semibold text-white">Size Curve Intelligence</h2>
          <p className="text-sm text-[#90a0ba]">Upisi ID artikla da vidis distribuciju velicina naspram idealnog kurva. Detektuje broken-run, dead size i core size.</p>
        </div>
        <div className="flex items-center gap-2">
          <label className="flex items-center gap-2 rounded-2xl border border-[#283042] bg-[#10141c] px-3 py-2">
            <Search size={14} className="shrink-0 text-[#7ec6ff]" />
            <input
              type="number"
              placeholder="ArtikelID"
              value={sizeCurveSkuId ?? ""}
              onChange={(event) => onChangeSkuId(event.target.value ? Number(event.target.value) : null)}
              className="w-28 bg-transparent text-sm text-white outline-none placeholder:text-[#73809a]"
            />
          </label>
          {sizeCurveSkuId != null ? (
            <button type="button" onClick={() => onChangeSkuId(null)} className="rounded-xl border border-[#33405a] bg-[#182131] px-3 py-2 text-xs font-semibold text-[#dbe6fb]">
              Ponisti
            </button>
          ) : null}
        </div>
      </div>

      {sizeCurveSkuId == null ? (
        <div className="mt-4 rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-8 text-center text-sm text-[#8797b4]">
          Upisi ID artikla u polje iznad da prikazes size curve analizu.
        </div>
      ) : sizeCurveLoading ? (
        <div className="mt-4 rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-8 text-center text-sm text-[#8797b4]">Ucitavam size curve za SKU #{sizeCurveSkuId}...</div>
      ) : !sizeCurve?.snapshotAvailable || items.length === 0 ? (
        <div className="mt-4 rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-8 text-center text-sm text-[#8797b4]">
          Nema size curve podataka za SKU #{sizeCurveSkuId}.
        </div>
      ) : (
        <div className="mt-4">
          <div className="mb-3 flex flex-wrap gap-2">
            {missingCoreCount > 0 ? (
              <span className="inline-flex rounded-full border border-[#7d2940] bg-[#411520] px-2.5 py-1 text-xs font-semibold text-[#ffb4c2]">
                {missingCoreCount} core size nedostaju
              </span>
            ) : null}
            {deadSizeCount > 0 ? (
              <span className="inline-flex rounded-full border border-[#7c5822] bg-[#412d11] px-2.5 py-1 text-xs font-semibold text-[#ffd590]">
                {deadSizeCount} mrtve velicine
              </span>
            ) : null}
            {hasBrokenRun ? (
              <span className="inline-flex rounded-full border border-[#30516d] bg-[#102231] px-2.5 py-1 text-xs font-semibold text-[#8edbff]">
                Broken run detektovan
              </span>
            ) : null}
          </div>
          <div className="h-[240px]">
            <ResponsiveContainer width="100%" height="100%">
              <ComposedChart data={items.map((item) => ({ name: item.sizeCode, actual: +(item.actualSizeShare * 100).toFixed(1), ideal: +(item.idealSizeShare * 100).toFixed(1) }))}>
                <CartesianGrid strokeDasharray="3 3" stroke="#233042" />
                <XAxis dataKey="name" tick={{ fill: "#92a4bf", fontSize: 12 }} />
                <YAxis tick={{ fill: "#92a4bf", fontSize: 12 }} unit="%" />
                <Tooltip formatter={(value: number | string | undefined) => `${value ?? 0}%`} contentStyle={{ background: "#141c29", border: "1px solid #2b3a50", color: "#dde7f7" }} />
                <Bar dataKey="actual" fill="#44d0ff" radius={[6, 6, 0, 0]} name="Stvarno" />
                <Line type="monotone" dataKey="ideal" stroke="#ffd590" strokeWidth={2} dot={false} name="Idealno" />
                <ReferenceLine y={0} stroke="#334055" />
              </ComposedChart>
            </ResponsiveContainer>
          </div>
          <div className="mt-4 grid gap-2 sm:grid-cols-2 xl:grid-cols-4">
            {items.slice(0, 8).map((item) => (
              <div key={item.sizeCode} className={`rounded-2xl border p-3 ${item.isCoreSizeMissing ? "border-[#7d2940] bg-[#411520]" : item.isDeadSize ? "border-[#7c5822] bg-[#412d11]" : "border-[#243040] bg-[#10141b]"}`}>
                <div className="flex items-center justify-between">
                  <span className="text-sm font-semibold text-white">vel. {item.sizeCode}</span>
                  <span className="text-xs text-[#8797b4]">{(item.deviationPct * 100).toFixed(0)}pp</span>
                </div>
                <div className="mt-2 grid grid-cols-2 gap-2 text-xs">
                  <div>
                    <div className="text-[#89d9ff]">Stvarno</div>
                    <div className="font-semibold text-white">{(item.actualSizeShare * 100).toFixed(1)}%</div>
                  </div>
                  <div>
                    <div className="text-[#ffd590]">Idealno</div>
                    <div className="font-semibold text-white">{(item.idealSizeShare * 100).toFixed(1)}%</div>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </section>
  );
}

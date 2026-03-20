import { Search } from "lucide-react";
import type { SizeCurveDto } from "../../types/analytics";
import { SizeCurveVisualization } from "./SizeCurveVisualization";

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
        <SizeCurveVisualization items={items} />
      )}
    </section>
  );
}

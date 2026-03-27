import { Search } from "lucide-react";
import { LoadingSkeleton } from "../LoadingSkeleton";
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
    <section className="rounded-[28px] border border-[var(--border-default)] bg-[var(--surface-elevated)] p-5">
      <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
        <div>
          <h2 className="text-lg font-semibold text-white">Size Curve Intelligence</h2>
          <p className="text-sm text-[var(--text-primary)]">Upisi ID artikla da vidis distribuciju velicina naspram idealnog kurva. Detektuje broken-run, dead size i core size.</p>
        </div>
        <div className="flex items-center gap-2">
          <label className="flex items-center gap-2 rounded-2xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-3 py-2">
            <Search size={14} className="shrink-0 text-[var(--text-primary)]" />
            <input
              type="number"
              aria-label="Unos SKU ID za size curve"
              placeholder="ArtikelID"
              value={sizeCurveSkuId ?? ""}
              onChange={(event) => onChangeSkuId(event.target.value ? Number(event.target.value) : null)}
              className="w-28 bg-transparent text-sm text-white outline-none placeholder:text-[var(--text-primary)]"
            />
          </label>
          {sizeCurveSkuId != null ? (
            <button type="button" aria-label="Ponisti size curve izbor artikla" onClick={() => onChangeSkuId(null)} className="rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-3 py-2 text-xs font-semibold text-[var(--text-primary)]">
              Ponisti
            </button>
          ) : null}
        </div>
      </div>

      {sizeCurveSkuId == null ? (
        <div className="mt-4 rounded-2xl border border-dashed border-[var(--border-default)] bg-[var(--surface-elevated)] px-4 py-8 text-center text-sm text-[var(--text-primary)]">
          Upisi ID artikla u polje iznad da prikazes size curve analizu.
        </div>
      ) : sizeCurveLoading ? (
        <div className="mt-4 rounded-2xl border border-dashed border-[var(--border-default)] bg-[var(--surface-elevated)] p-4 text-center text-sm text-[var(--text-primary)]"><div className="mb-4">Ucitavam size curve za SKU #{sizeCurveSkuId}...</div><LoadingSkeleton type="messages" count={1} /></div>
      ) : !sizeCurve?.snapshotAvailable || items.length === 0 ? (
        <div className="mt-4 rounded-2xl border border-dashed border-[var(--border-default)] bg-[var(--surface-elevated)] px-4 py-8 text-center text-sm text-[var(--text-primary)]">
          Nema size curve podataka za SKU #{sizeCurveSkuId}.
        </div>
      ) : (
        <SizeCurveVisualization items={items} />
      )}
    </section>
  );
}


import { Search } from "lucide-react";
import { LoadingSkeleton } from "../LoadingSkeleton";
import type { SizeCurveDto } from "../../types/analytics";
import { SizeCurveVisualization } from "./SizeCurveVisualization";

type SizeCurvePanelProps = {
  sizeCurveSkuId: number | null;
  sizeCurve: SizeCurveDto | null;
  sizeCurveLoading: boolean;
  sizeCurveError?: string | null;
  onChangeSkuId: (value: number | null) => void;
};

function getSizeCurveWarningLabel(warning?: string | null): string | null {
  const normalized = warning?.trim().toLowerCase();
  if (!normalized) return null;

  if (normalized.includes("nema redova") || normalized.includes("empty") || normalized.includes("no_rows")) {
    return "Size curve snapshot nema redove za izabrani opseg.";
  }

  if (normalized.includes("nepotpun") || normalized.includes("partial") || normalized.includes("incomplete")) {
    return "Size curve snapshot sadrži delimične ili nepotpune podatke.";
  }

  if (normalized.includes("nije dostupan") || normalized.includes("missing") || normalized.includes("unavailable")) {
    return "Size curve snapshot trenutno nije dostupan.";
  }

  return "Size curve snapshot ima ograničenje kvaliteta podataka.";
}

export function SizeCurvePanel({
  sizeCurveSkuId,
  sizeCurve,
  sizeCurveLoading,
  sizeCurveError,
  onChangeSkuId,
}: SizeCurvePanelProps) {
  const items = sizeCurve?.items ?? [];
  const warningLabel = getSizeCurveWarningLabel(sizeCurve?.warning);

  return (
    <section className="rounded-[28px] border border-[var(--border-default)] bg-[var(--surface-elevated)] p-5">
      <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
        <div>
          <h2 className="text-lg font-semibold text-foreground">Size curve analiza</h2>
          <p className="text-sm text-[var(--text-primary)]">Upiši ID artikla da vidiš distribuciju veličina u odnosu na idealnu krivu. Detektuje broken-run, dead size i core size.</p>
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
              className="w-28 bg-transparent text-sm text-foreground outline-none placeholder:text-[var(--text-primary)]"
            />
          </label>
          {sizeCurveSkuId != null ? (
            <button type="button" aria-label="Poništi size curve izbor artikla" onClick={() => onChangeSkuId(null)} className="rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-3 py-2 text-xs font-semibold text-[var(--text-primary)]">
              Poništi
            </button>
          ) : null}
        </div>
      </div>

      {sizeCurveSkuId == null ? (
        <div className="mt-4 rounded-2xl border border-dashed border-[var(--border-default)] bg-[var(--surface-elevated)] px-4 py-8 text-center text-sm text-[var(--text-primary)]">
          Upiši ID artikla u polje iznad da prikažeš size curve analizu.
        </div>
      ) : sizeCurveError ? (
        <div className="mt-4 rounded-2xl border border-[var(--error)] bg-[var(--surface-elevated)] px-4 py-8 text-center text-sm text-[var(--error)]">
          {sizeCurveError}
        </div>
      ) : sizeCurveLoading ? (
        <div className="mt-4 rounded-2xl border border-dashed border-[var(--border-default)] bg-[var(--surface-elevated)] p-4 text-center text-sm text-[var(--text-primary)]"><div className="mb-4">Učitavam size curve za SKU #{sizeCurveSkuId}...</div><LoadingSkeleton type="messages" count={1} /></div>
      ) : !sizeCurve?.snapshotAvailable ? (
        <div className="mt-4 rounded-2xl border border-dashed border-[var(--border-default)] bg-[var(--surface-elevated)] px-4 py-8 text-center text-sm text-[var(--text-primary)]">
          <div>Size curve nije dostupna za SKU #{sizeCurveSkuId}.</div>
          {warningLabel ? <div className="mt-2 text-xs text-warning">{warningLabel}</div> : null}
        </div>
      ) : items.length === 0 ? (
        <div className="mt-4 rounded-2xl border border-dashed border-[var(--border-default)] bg-[var(--surface-elevated)] px-4 py-8 text-center text-sm text-[var(--text-primary)]">
          <div>Size curve snapshot je dostupan, ali nema podataka za SKU #{sizeCurveSkuId} u izabranom opsegu.</div>
          {warningLabel ? <div className="mt-2 text-xs text-warning">{warningLabel}</div> : null}
        </div>
      ) : (
        <>
          {warningLabel ? <div className="mt-4 rounded-2xl border border-warning/40 bg-[var(--surface-elevated)] px-4 py-3 text-sm text-warning">{warningLabel}</div> : null}
          <SizeCurveVisualization items={items} />
        </>
      )}
    </section>
  );
}


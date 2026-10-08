import { fmtPct, fmtQty, fmtRsd, fmtSignedPct } from "./analyticsFormatters";

export type SupplierNegotiationFactInput = {
  revenue: number | null | undefined;
  units: number | null | undefined;
  marginContribution: number | null | undefined;
  marginPct: number | null | undefined;
  marginCoveragePct: number | null | undefined;
  revenueTrendPct: number | null | undefined;
  inventoryValue: number | null | undefined;
  inventoryUnits: number | null | undefined;
  inventoryValueCoveragePct: number | null | undefined;
  agedValue: number | null | undefined;
  agedUnits: number | null | undefined;
  agedValueCoveragePct: number | null | undefined;
};

const isKnown = (value: number | null | undefined): value is number => value != null && Number.isFinite(value);

function coverageSuffix(coveragePct: number | null | undefined): string {
  if (!isKnown(coveragePct)) return " (pokrivenost vrednosti nije poznata)";
  return coveragePct >= 100 ? "" : ` (delimično: pokrivenost ${fmtPct(coveragePct, 1)})`;
}

/**
 * RQ595 "negotiation facts": up to five plain-language facts formatted from values the
 * backend already returned. A fact is omitted when its value is unknown; nothing is turned
 * into zero and no order recommendation is derived here.
 */
export function buildSupplierNegotiationFacts(input: SupplierNegotiationFactInput): string[] {
  const facts: string[] = [];

  if (isKnown(input.revenue)) {
    facts.push(`Promet u izabranom periodu: ${fmtRsd(input.revenue)}${isKnown(input.units) ? ` za ${fmtQty(input.units)}` : ""}.`);
  }

  if (isKnown(input.marginContribution) && isKnown(input.marginCoveragePct) && input.marginCoveragePct > 0) {
    const margin = isKnown(input.marginPct) ? ` uz ponderisanu maržu ${fmtPct(input.marginPct, 1)}` : "";
    facts.push(`Maržni doprinos ${fmtRsd(input.marginContribution)}${margin}; nabavna cena je poznata za ${fmtPct(input.marginCoveragePct, 1)} prometa.`);
  }

  if (isKnown(input.revenueTrendPct)) {
    facts.push(`Promet ${fmtSignedPct(input.revenueTrendPct, 1)} u odnosu na prethodni period iste dužine.`);
  }

  if (isKnown(input.inventoryValue)) {
    const units = isKnown(input.inventoryUnits) ? ` (${fmtQty(input.inventoryUnits)})` : "";
    facts.push(`Poznata vrednost zalihe na poslednjem preseku: ${fmtRsd(input.inventoryValue)}${units}${coverageSuffix(input.inventoryValueCoveragePct)}.`);
  }

  if (isKnown(input.agedValue) && input.agedValue > 0) {
    const units = isKnown(input.agedUnits) ? ` u ${fmtQty(input.agedUnits)}` : "";
    facts.push(`${fmtRsd(input.agedValue)} poznate zalihe starije od 90 dana od prijema${units}${coverageSuffix(input.agedValueCoveragePct)}.`);
  }

  return facts.slice(0, 5);
}

/**
 * Shared margin quality classification and rendering helpers.
 *
 * Quality tiers (from backend MarginQualityClassifier):
 *   confirmed  — ≥80% historical cost coverage
 *   partial    — ≥50% historical
 *   estimated  — <50% historical, but some coverage exists
 *   no_data    — 0% coverage
 */

export type MarginQualityTier = "confirmed" | "partial" | "estimated" | "no_data";

export interface MarginQualityInfo {
  tier: MarginQualityTier;
  label: string;
  shortLabel: string;
  tooltip: string;
  historicalCostCoveragePct: number;
  estimatedCostCoveragePct: number;
  noCostCoveragePct: number;
}

/** CSS class suffix for the tier badge */
export function qualityTierClass(tier: MarginQualityTier | string | null | undefined): string {
  switch (tier) {
    case "confirmed":
      return "quality-confirmed";
    case "partial":
      return "quality-partial";
    case "estimated":
      return "quality-estimated";
    case "no_data":
      return "quality-nodata";
    default:
      return "quality-estimated";
  }
}

/** Icon for the quality tier */
export function qualityTierIcon(tier: MarginQualityTier | string | null | undefined): string {
  switch (tier) {
    case "confirmed":
      return "✓";
    case "partial":
      return "◐";
    case "estimated":
      return "⚠";
    case "no_data":
      return "✕";
    default:
      return "⚠";
  }
}

/** Whether this tier needs a visible warning badge */
export function tierNeedsWarning(tier: MarginQualityTier | string | null | undefined): boolean {
  return tier !== "confirmed";
}

/**
 * Build a coverage breakdown tooltip string from raw percentages.
 */
export function buildCoverageTooltip(
  historicalPct: number | null | undefined,
  estimatedPct: number | null | undefined,
  noCostPct: number | null | undefined,
  fmtPct: (v: number | null | undefined, digits?: number) => string
): string {
  return [
    `Istorijski trošak: ${fmtPct(historicalPct ?? 0, 1)}`,
    `Procenjeni (fallback) trošak: ${fmtPct(estimatedPct ?? 0, 1)}`,
    `Bez troška: ${fmtPct(noCostPct ?? 0, 1)}`,
  ].join(" · ");
}

/**
 * Build a recommendation caveat string based on margin quality tier.
 * Returns null if no caveat is needed.
 */
export function buildRecommendationCaveat(
  tier: MarginQualityTier | string | null | undefined,
  estimatedPct: number | null | undefined,
  fmtPct: (v: number | null | undefined, digits?: number) => string
): string | null {
  if (tier === "confirmed") return null;

  if (tier === "no_data") {
    return "Nabavna cena nije dostupna — preporuka ne sadrži maržni signal.";
  }

  if (tier === "estimated") {
    return `Marža je dominantno procenjena iz fallback troška artikla (${fmtPct(estimatedPct ?? 0, 1)} prometa). Preporuka može biti manje pouzdana.`;
  }

  // partial
  return `Deo marže (${fmtPct(estimatedPct ?? 0, 1)} prometa) je procenjen iz fallback troška. Preporuku čitajte oprezno.`;
}

/**
 * Build a margin caveat note for the detail section.
 * Always returns a string (may be empty for confirmed tier).
 */
export function buildMarginDetailNote(
  tier: MarginQualityTier | string | null | undefined,
  estimatedPct: number | null | undefined,
  historicalPct: number | null | undefined,
  fmtPct: (v: number | null | undefined, digits?: number) => string
): string | null {
  if (tier === "confirmed") return null;

  if (tier === "no_data") {
    return "Nabavna cena nije dostupna za ovaj red. Maržni doprinos nije moguće obračunati.";
  }

  if (tier === "estimated") {
    return `Marža je dominantno procenjena: istorijski trošak pokriva samo ${fmtPct(historicalPct ?? 0, 1)} prometa, a ${fmtPct(estimatedPct ?? 0, 1)} koristi fallback trošak sa kartice artikla. Nabavna cena nije zamrznuta na prodajnoj stavci, pa se marža za ovaj red može promeniti ako se nabavna cena ažurira.`;
  }

  // partial
  return `Marža je delimično procenjena: ${fmtPct(historicalPct ?? 0, 1)} prometa ima istorijski trošak, a ${fmtPct(estimatedPct ?? 0, 1)} koristi fallback trošak.`;
}

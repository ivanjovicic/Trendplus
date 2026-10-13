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

function isKnownPct(value: number | null | undefined): value is number {
  return value != null && Number.isFinite(value);
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
  fmtPct: (v: number | null | undefined, digits?: number) => string,
  snapshotPct?: number | null
): string {
  const parts = [
    `Istorijski trošak sa prodajne stavke: ${fmtPct(historicalPct, 1)}`,
    `Produkt-fallback/procena: ${fmtPct(estimatedPct, 1)}`,
    `Bez troška: ${fmtPct(noCostPct, 1)}`,
  ];
  if (snapshotPct != null && snapshotPct > 0) {
    parts.splice(1, 0, `Tačan snapshot trošak: ${fmtPct(snapshotPct, 1)}`);
  }
  return parts.join(" · ");
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

  const estimatedShare = isKnownPct(estimatedPct) ? ` (${fmtPct(estimatedPct, 1)} prometa)` : "";

  if (tier === "estimated") {
    return `Marža je dominantno procenjena iz fallback troška artikla${estimatedShare}. Preporuka može biti manje pouzdana.`;
  }

  // partial
  return `Deo marže${estimatedShare} je procenjen iz fallback troška. Preporuku čitajte oprezno.`;
}

/**
 * Build a margin caveat note for the detail section.
 * Always returns a string (may be empty for confirmed tier).
 */
export function buildMarginDetailNote(
  tier: MarginQualityTier | string | null | undefined,
  estimatedPct: number | null | undefined,
  historicalPct: number | null | undefined,
  fmtPct: (v: number | null | undefined, digits?: number) => string,
  snapshotPct?: number | null,
  isSnapshotActive?: boolean
): string | null {
  if (tier === "confirmed" && !(isSnapshotActive && (snapshotPct ?? 0) > 0)) return null;

  const snapshotNote =
    isSnapshotActive && (snapshotPct ?? 0) > 0
      ? ` Deo marže (snimak: ${fmtPct(snapshotPct, 1)}) pokriven je zamrznutom procenom troška. Snimak je stabilizovan za reproduktivnost, ali nije ekvivalent istorijskoj nabavnoj ceni sa trenutka prodaje.`
      : "";

  if (tier === "no_data") {
    return `Nabavna cena nije dostupna za ovaj red. Maržni doprinos nije moguće obračunati.${snapshotNote}`;
  }

  const historicalClause = isKnownPct(historicalPct) ? `${fmtPct(historicalPct, 1)} prometa ima istorijski trošak` : null;
  const estimatedClause = isKnownPct(estimatedPct) ? `${fmtPct(estimatedPct, 1)} koristi fallback trošak` : null;

  if (tier === "estimated") {
    const coverage = historicalClause && estimatedClause
      ? `istorijski trošak pokriva samo ${fmtPct(historicalPct, 1)} prometa, a ${fmtPct(estimatedPct, 1)} koristi fallback trošak sa kartice artikla`
      : [historicalClause, estimatedClause].filter(Boolean).join(", ") || "udeo istorijskog i fallback troška nije dostupan";
    return `Marža je dominantno procenjena: ${coverage}. Nabavna cena nije zamrznuta na prodajnoj stavci, pa se marža za ovaj red može promeniti ako se nabavna cena ažurira.${snapshotNote}`;
  }

  if (tier === "partial") {
    const coverage = [historicalClause, estimatedClause].filter(Boolean).join(", a ") || "udeo istorijskog i fallback troška nije dostupan";
    return `Marža je delimično procenjena: ${coverage}.${snapshotNote}`;
  }

  // confirmed tier but snapshot is active
  return snapshotNote.trimStart() || null;
}

/**
 * Returns a short badge label for the snapshot indicator.
 * Shows date if generatedAtUtc is provided.
 */
export function buildSnapshotBadgeLabel(generatedAtUtc?: string | null): string {
  if (!generatedAtUtc) return "Zamrznuta procena";
  const parsed = new Date(generatedAtUtc);
  if (Number.isNaN(parsed.getTime())) return "Zamrznuta procena";
  return `Snimak od ${parsed.toLocaleDateString("sr-RS")}`;
}

/**
 * Returns the tooltip text for the snapshot badge.
 */
export function buildSnapshotTooltip(
  snapshotPct: number,
  generatedAtUtc: string | null | undefined,
  fmtPct: (v: number | null | undefined, digits?: number) => string
): string {
  const datePart = generatedAtUtc
    ? (() => {
        const parsed = new Date(generatedAtUtc);
        return Number.isNaN(parsed.getTime())
          ? ""
          : ` Generisan: ${parsed.toLocaleDateString("sr-RS")}.`;
      })()
    : "";
  return `Trošak je stabilizovan snimkom radi reproduktivnosti izveštaja. Ovo nije istorijska nabavna cena sa trenutka prodaje. Snimak pokriva ${fmtPct(snapshotPct, 1)} prometa.${datePart}`;
}

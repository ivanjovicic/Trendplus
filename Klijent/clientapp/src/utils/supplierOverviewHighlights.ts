/** Minimal row shape needed for the supplier overview highlight strip. */
export type SupplierOverviewHighlightRow = {
  dobavljacNaziv: string;
  isUnknown?: boolean;
  ukupanPromet: number;
  popRevenueChangePct: number | null;
};

/**
 * Presentation-only shortcuts for the 5-second read of the supplier overview: the largest known
 * supplier by revenue and the largest backend PoP rise/fall. Nothing is recalculated; unknown
 * suppliers and rows without a finite backend value are skipped, so a missing value never wins.
 */
export function buildSupplierOverviewHighlights<T extends SupplierOverviewHighlightRow>(rows: readonly T[]): {
  leader: T | null;
  topGrowth: T | null;
  topDecline: T | null;
} {
  const known = rows.filter((row) => !row.isUnknown);
  let leader: T | null = null;
  let topGrowth: T | null = null;
  let topDecline: T | null = null;
  for (const row of known) {
    if (Number.isFinite(row.ukupanPromet) && row.ukupanPromet > 0 && (leader == null || row.ukupanPromet > leader.ukupanPromet)) {
      leader = row;
    }
    const pop = row.popRevenueChangePct;
    if (pop == null || !Number.isFinite(pop)) continue;
    if (pop > 0 && (topGrowth == null || pop > (topGrowth.popRevenueChangePct as number))) topGrowth = row;
    if (pop < 0 && (topDecline == null || pop < (topDecline.popRevenueChangePct as number))) topDecline = row;
  }
  return { leader, topGrowth, topDecline };
}

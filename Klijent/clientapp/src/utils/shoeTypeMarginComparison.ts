export type ShoeTypeComparisonRow = {
  name: string;
  udeoPrometa: number;
  udeoMarznogDoprinosa: number | null;
};

export function hasComparableShoeTypeMarginTotal(
  totalMarginContribution: number | null | undefined,
): boolean {
  return typeof totalMarginContribution === "number" && Number.isFinite(totalMarginContribution);
}

export function resolveShoeTypeMarginContributionSharePct(
  marginContribution: number | null | undefined,
  totalMarginContribution: number | null | undefined,
): number | null {
  if (
    typeof marginContribution !== "number" ||
    !Number.isFinite(marginContribution) ||
    !hasComparableShoeTypeMarginTotal(totalMarginContribution)
  ) {
    return null;
  }

  if (totalMarginContribution === 0) {
    return marginContribution === 0 ? 0 : null;
  }

  const sharePct = (marginContribution / totalMarginContribution) * 100;
  return Number.isFinite(sharePct) ? sharePct : null;
}

export function buildShoeTypeComparisonData(
  rows: Array<{
    tipObuceNaziv: string;
    ukupanPromet: number;
    marginContribution: number;
    sharePct?: number | null;
  }>,
  totalMarginContribution: number | null | undefined,
): ShoeTypeComparisonRow[] {
  if (rows.length === 0 || !hasComparableShoeTypeMarginTotal(totalMarginContribution)) {
    return [];
  }

  const ranked = [...rows]
    .filter((row): row is typeof row & { sharePct: number } => row.sharePct != null && Number.isFinite(row.sharePct))
    .sort((a, b) => b.ukupanPromet - a.ukupanPromet);

  return ranked.slice(0, 8).map((row) => {
    const marginSharePct = resolveShoeTypeMarginContributionSharePct(
      row.marginContribution,
      totalMarginContribution,
    );

    return {
      name: row.tipObuceNaziv,
      udeoPrometa: Number(row.sharePct.toFixed(1)),
      udeoMarznogDoprinosa: marginSharePct == null ? null : Number(marginSharePct.toFixed(1)),
    };
  });
}

export function formatShoeTypeMarginContributionShare(
  marginContribution: number | null | undefined,
  totalMarginContribution: number | null | undefined,
  formatPct: (value: number, digits?: number) => string,
): string {
  const sharePct = resolveShoeTypeMarginContributionSharePct(marginContribution, totalMarginContribution);
  return sharePct == null ? "Nije dostupno" : formatPct(sharePct, 2);
}

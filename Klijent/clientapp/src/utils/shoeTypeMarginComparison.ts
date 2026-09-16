export type ShoeTypeComparisonRow = {
  name: string;
  udeoPrometa: number;
  udeoMarznogDoprinosa: number | null;
};

export type ShoeTypeMarginComparisonMode = "unavailable" | "share" | "value";

export type ShoeTypeMarginComparisonChartRow = ShoeTypeComparisonRow & {
  marginContributionRsd: number;
};

export type ShoeTypeMarginComparisonProjection = {
  mode: ShoeTypeMarginComparisonMode;
  data: ShoeTypeMarginComparisonChartRow[];
};

type ShoeTypeMarginComparisonInputRow = {
  tipObuceNaziv: string;
  ukupanPromet: number;
  marginContribution: number;
  sharePct?: number | null;
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

export function formatShoeTypeMarginContributionShare(
  marginContribution: number | null | undefined,
  totalMarginContribution: number | null | undefined,
  formatPct: (value: number, digits?: number) => string,
): string {
  const sharePct = resolveShoeTypeMarginContributionSharePct(marginContribution, totalMarginContribution);
  return sharePct == null ? "Nije dostupno" : formatPct(sharePct, 2);
}

const EMPTY_MARGIN_COMPARISON: ShoeTypeMarginComparisonProjection = {
  mode: "unavailable",
  data: [],
};

export function buildShoeTypeMarginComparisonProjection(
  rows: ShoeTypeMarginComparisonInputRow[],
  totalMarginContribution: number | null | undefined,
  isValidSharePct: (value: number | null | undefined) => boolean = (value) =>
    typeof value === "number" && Number.isFinite(value),
): ShoeTypeMarginComparisonProjection {
  if (!hasComparableShoeTypeMarginTotal(totalMarginContribution)) {
    return EMPTY_MARGIN_COMPARISON;
  }

  const ranked = [...rows]
    .filter((row): row is ShoeTypeMarginComparisonInputRow & { sharePct: number } => (
      isValidSharePct(row.sharePct) && Number.isFinite(row.marginContribution)
    ))
    .sort((a, b) => b.ukupanPromet - a.ukupanPromet)
    .slice(0, 8);

  if (ranked.length === 0) {
    return EMPTY_MARGIN_COMPARISON;
  }

  const hasPositiveMarginDenominator = totalMarginContribution! > 0;

  return {
    mode: hasPositiveMarginDenominator ? "share" : "value",
    data: ranked.map((row) => {
      const marginSharePct = hasPositiveMarginDenominator
        ? resolveShoeTypeMarginContributionSharePct(row.marginContribution, totalMarginContribution)
        : null;

      return {
        name: row.tipObuceNaziv,
        udeoPrometa: Number(row.sharePct.toFixed(1)),
        udeoMarznogDoprinosa: marginSharePct == null ? null : Number(marginSharePct.toFixed(1)),
        marginContributionRsd: Number(row.marginContribution.toFixed(2)),
      };
    }),
  };
}

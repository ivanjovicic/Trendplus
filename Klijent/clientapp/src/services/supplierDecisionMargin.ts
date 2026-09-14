export const SUPPLIER_MARGIN_CONTRIBUTION_DEFINITION =
  "Full-price prihod × pre-markdown marža (procena)";

export type SupplierMarginContributionEvidenceState =
  | "measured"
  | "measured_zero"
  | "unavailable"
  | "partial"
  | "non_finite";

type SupplierMarginContributionInput = {
  revenue?: number | null;
  preMarkdownMarginPct?: number | null;
  fullPriceRevenueShare?: number | null;
};

function isValidRatio(value: number | null | undefined): value is number {
  return value != null && Number.isFinite(value) && value >= 0 && value <= 1;
}

export function calculateSupplierMarginContribution({
  revenue,
  preMarkdownMarginPct,
  fullPriceRevenueShare,
}: SupplierMarginContributionInput): number | null {
  if (
    revenue == null
    || preMarkdownMarginPct == null
    || fullPriceRevenueShare == null
  ) {
    return null;
  }

  if (!Number.isFinite(revenue) || revenue < 0 || !isValidRatio(preMarkdownMarginPct) || !isValidRatio(fullPriceRevenueShare)) {
    return null;
  }

  const contribution = revenue * preMarkdownMarginPct * fullPriceRevenueShare;
  return Number.isFinite(contribution) ? Math.round(contribution * 100) / 100 : null;
}

export function classifySupplierMarginContributionEvidence(
  rows: SupplierMarginContributionInput[],
): SupplierMarginContributionEvidenceState {
  if (rows.length === 0) return "unavailable";

  if (rows.some((row) => [row.revenue, row.preMarkdownMarginPct, row.fullPriceRevenueShare]
    .some((value) => value != null && !Number.isFinite(value)))) {
    return "non_finite";
  }

  if (rows.some((row) => calculateSupplierMarginContribution(row) == null)) {
    return rows.some((row) => calculateSupplierMarginContribution(row) != null) ? "partial" : "unavailable";
  }

  return rows.every((row) => calculateSupplierMarginContribution(row) === 0) ? "measured_zero" : "measured";
}

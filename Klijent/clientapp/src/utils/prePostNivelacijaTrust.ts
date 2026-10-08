import { normalizeMetricNumber } from "./analyticsMetricValue";

export type PrePostEvidenceRow = {
  hasComparableSalesWindow?: boolean | null;
};

export function hasComparablePrePostEvidence(row: PrePostEvidenceRow | null | undefined): boolean {
  return row?.hasComparableSalesWindow === true;
}

export function comparablePrePostMetric(
  value: number | string | null | undefined,
  row: PrePostEvidenceRow | null | undefined,
): number | null {
  if (!hasComparablePrePostEvidence(row)) return null;
  return normalizeMetricNumber(value);
}

export function comparablePrePostTotal(
  value: number | string | null | undefined,
  hasComparableSalesWindow?: boolean | null,
): number | null {
  if (hasComparableSalesWindow !== true) return null;
  return normalizeMetricNumber(value);
}

export type PostRevenueShareRow = PrePostEvidenceRow & {
  postRevenueSharePercent?: number | null;
};

export type RevenueBaselineRow = {
  hasRevenueBaseline?: boolean | null;
  revenueBaselineReason?: string | null;
  semanticChangePercentRevenue?: number | null;
  changePercent?: number | null;
};

const REVENUE_BASELINE_LABELS: Record<string, string> = {
  missing_pre_revenue_window: "Nedostaje pre-prozor prihoda",
  no_pre_revenue_baseline_uplift: "Nova osnova; procenat promene nije uporediv",
  no_pre_revenue_baseline_flat: "Nema prihoda u pre-prozoru; procenat nije dostupan",
  mixed_baseline_evidence: "Polazna osnova nije potpuna za sve artikle",
  missing_comparable_rows: "Nema uporedivih redova",
};

export function revenueBaselineLabel(row: RevenueBaselineRow | null | undefined): string | null {
  if (row?.hasRevenueBaseline !== false) return null;
  const reason = row.revenueBaselineReason;
  return reason ? REVENUE_BASELINE_LABELS[reason] ?? "Uporediva osnova nije dostupna" : "Uporediva osnova nije dostupna";
}

export function baselineAwareRevenueChangePercent(row: RevenueBaselineRow | null | undefined): number | null {
  if (row?.hasRevenueBaseline === false) return null;
  if (row?.semanticChangePercentRevenue != null) return normalizeMetricNumber(row.semanticChangePercentRevenue);
  return normalizeMetricNumber(row?.changePercent);
}

export function resolvePostRevenueSharePercent(row: PostRevenueShareRow | null | undefined): number | null {
  if (!hasComparablePrePostEvidence(row) || row?.postRevenueSharePercent == null) {
    return null;
  }
  return normalizeMetricNumber(row.postRevenueSharePercent);
}

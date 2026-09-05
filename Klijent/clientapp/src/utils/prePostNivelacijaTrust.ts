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

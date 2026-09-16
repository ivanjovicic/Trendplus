import { formatMetricDisplayValue, isFiniteMetricNumber } from "./analyticsMetricValue";

export function resolveCategoryPrePostRawMetric(
  value: number | null | undefined,
): number | null {
  return isFiniteMetricNumber(value) ? value : null;
}

export function formatCategoryPrePostRevenueMetric(
  value: number | null | undefined,
  fallback = "Nije dostupno",
): string {
  return formatMetricDisplayValue({
    value,
    kind: "currency",
    fallback,
  });
}

export function formatCategoryPrePostQuantityMetric(
  value: number | null | undefined,
  fallback = "Nije dostupno",
): string {
  return formatMetricDisplayValue({
    value,
    kind: "qty",
    fallback,
  });
}

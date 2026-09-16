import { formatMetricDisplayValue, isFiniteMetricNumber } from "./analyticsMetricValue";

export function resolveColorPrePostRevenueMetric(
  value: number | null | undefined,
): number | null {
  return isFiniteMetricNumber(value) ? value : null;
}

export function resolveColorPrePostQuantityMetric(
  value: number | null | undefined,
): number | null {
  return isFiniteMetricNumber(value) ? value : null;
}

export function formatColorPrePostRevenueMetric(
  value: number | null | undefined,
  fallback = "Nije dostupno",
): string {
  return formatMetricDisplayValue({
    value,
    kind: "currency",
    fallback,
  });
}

export function formatColorPrePostQuantityMetric(
  value: number | null | undefined,
  fallback = "Nije dostupno",
): string {
  return formatMetricDisplayValue({
    value,
    kind: "qty",
    fallback,
  });
}

import {
  fmtNumber,
  fmtPct,
  fmtPctFromRatio,
  fmtQty,
  fmtRsd,
} from "./analyticsFormatters";

export type AnalyticsMetricValueStatus =
  | "valid_zero"
  | "unavailable"
  | "insufficient_data"
  | "error"
  | "stale"
  | "partial"
  | "not_applicable";

type MetricKind =
  | "currency"
  | "number"
  | "percent"
  | "ratioPercent"
  | "days"
  | "qty";

type ResolveMetricDisplayOptions = {
  value: number | null | undefined;
  kind: MetricKind;
  digits?: number;
  status?: AnalyticsMetricValueStatus | null;
  fallback?: string;
};

export function isFiniteMetricNumber(value: number | null | undefined): value is number {
  return typeof value === "number" && Number.isFinite(value);
}

export function normalizeMetricNumber(value: number | string | null | undefined): number | null {
  if (typeof value === "number") {
    return Number.isFinite(value) ? value : null;
  }

  if (typeof value !== "string" || value.trim().length === 0) {
    return null;
  }

  const parsed = Number(value.trim().replace(/\s/g, "").replace(",", "."));
  return Number.isFinite(parsed) ? parsed : null;
}

export function resolveMetricValueStatus(input: {
  value: number | null | undefined;
  status?: AnalyticsMetricValueStatus | null;
  error?: boolean;
  insufficient?: boolean;
  stale?: boolean;
  partial?: boolean;
  notApplicable?: boolean;
}): AnalyticsMetricValueStatus | null {
  if (input.status) return input.status;
  if (input.error) return "error";
  if (input.notApplicable) return "not_applicable";
  if (input.stale) return "stale";
  if (input.partial) return "partial";
  if (input.insufficient) return "insufficient_data";
  if (!isFiniteMetricNumber(input.value)) return "unavailable";
  if (input.value === 0) return "valid_zero";
  return null;
}

export function metricStatusLabel(
  status: AnalyticsMetricValueStatus | null | undefined,
  fallback = "Nije dostupno",
): string {
  if (status === "insufficient_data") return "Nedovoljno podataka";
  if (status === "error") return "Greška";
  if (status === "stale") return "Zastarelo";
  if (status === "partial") return "Delimično";
  if (status === "not_applicable") return "Nije primenljivo";
  if (status === "unavailable") return fallback;
  return fallback;
}

export function formatMetricDisplayValue(options: ResolveMetricDisplayOptions): string {
  const status = options.status ?? resolveMetricValueStatus({ value: options.value });
  if (!isFiniteMetricNumber(options.value)) {
    return metricStatusLabel(status, options.fallback ?? "Nije dostupno");
  }

  switch (options.kind) {
    case "currency":
      return fmtRsd(options.value, options.digits ?? 0, options.fallback ?? "Nije dostupno");
    case "number":
      return fmtNumber(options.value, options.digits ?? 0, options.fallback ?? "Nije dostupno");
    case "percent":
      return fmtPct(options.value, options.digits ?? 1, options.fallback ?? "Nije dostupno");
    case "ratioPercent":
      return fmtPctFromRatio(options.value, options.digits ?? 1, options.fallback ?? "Nije dostupno");
    case "days":
      return `${fmtNumber(options.value, options.digits ?? 1, options.fallback ?? "Nije dostupno")} dana`;
    case "qty":
      return fmtQty(options.value, options.digits ?? 0, options.fallback ?? "Nije dostupno");
    default:
      return options.fallback ?? "Nije dostupno";
  }
}

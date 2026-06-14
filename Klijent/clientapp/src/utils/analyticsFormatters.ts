import {
  getAnalyticsPeriodPresetRange,
  type AnalyticsComparablePeriodPreset,
} from "./analyticsPeriodPresets";

type DateLikeValue = string | Date | null | undefined;

export function fmtNumber(value: number | null | undefined, digits = 0, fallback = "N/A"): string {
  if (value == null || Number.isNaN(value)) return fallback;
  return value.toLocaleString("sr-RS", {
    minimumFractionDigits: digits,
    maximumFractionDigits: digits,
  });
}

export function fmtRsd(value: number | null | undefined, digits = 0, fallback = "N/A"): string {
  const formatted = fmtNumber(value, digits, fallback);
  return formatted === fallback ? fallback : `${formatted} RSD`;
}

export function fmtRsdShort(value: number | null | undefined): string {
  return fmtRsd(value, 0);
}

export function fmtRsdCompact(value: number | null | undefined, digits = 1, fallback = "N/A"): string {
  if (value == null || Number.isNaN(value)) return fallback;

  const absolute = Math.abs(value);
  if (absolute >= 1_000_000) {
    return `${fmtNumber(value / 1_000_000, digits, fallback)}M RSD`;
  }

  if (absolute >= 1_000) {
    return `${fmtNumber(value / 1_000, digits, fallback)}k RSD`;
  }

  return fmtRsd(value, 0, fallback);
}

export function fmtPct(value: number | null | undefined, digits = 1): string {
  if (value == null || Number.isNaN(value)) return "N/A";
  return `${fmtNumber(value, digits)}%`;
}

export function fmtPctFromRatio(value: number | null | undefined, digits = 1, fallback = "N/A"): string {
  if (value == null || Number.isNaN(value)) return fallback;
  return fmtPct(value * 100, digits);
}

export function fmtSignedPct(value: number | null | undefined, digits = 1): string {
  if (value == null || Number.isNaN(value)) return "N/A";
  const sign = value > 0 ? "+" : "";
  return `${sign}${fmtPct(value, digits)}`;
}

export function fmtQty(value: number | null | undefined, digits = 0, fallback = "N/A"): string {
  const formatted = fmtNumber(value, digits, fallback);
  return formatted === fallback ? fallback : `${formatted} kom`;
}

export function formatDate(value: DateLikeValue, fallback = "-"): string {
  if (value == null) return fallback;
  const parsed = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    if (typeof value === "string" && value.trim()) return value;
    return fallback;
  }

  return parsed.toLocaleDateString("sr-RS");
}

export function formatDateTime(value: DateLikeValue, fallback = "-"): string {
  if (value == null) return fallback;
  const parsed = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    if (typeof value === "string" && value.trim()) return value;
    return fallback;
  }

  return parsed.toLocaleString("sr-RS", {
    dateStyle: "medium",
    timeStyle: "short",
  });
}

export function getPresetRange(
  preset: AnalyticsComparablePeriodPreset,
  now?: Date
): { fromDate: string; toDate: string } {
  return getAnalyticsPeriodPresetRange(preset, now);
}

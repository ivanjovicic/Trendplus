import { ANALYTICS_UNAVAILABLE_LABEL } from "./analyticsConstants";
import {
  getAnalyticsPeriodPresetRange,
  type AnalyticsComparablePeriodPreset,
} from "./analyticsPeriodPresets";

type DateLikeValue = string | Date | null | undefined;

export function fmtNumber(value: number | null | undefined, digits = 0, fallback = ANALYTICS_UNAVAILABLE_LABEL): string {
  if (value == null || !Number.isFinite(value)) return fallback;
  return value.toLocaleString("sr-RS", {
    minimumFractionDigits: digits,
    maximumFractionDigits: digits,
  });
}

export function fmtRsd(value: number | null | undefined, digits = 0, fallback = ANALYTICS_UNAVAILABLE_LABEL): string {
  const formatted = fmtNumber(value, digits, fallback);
  return formatted === fallback ? fallback : `${formatted} RSD`;
}

export function fmtRsdShort(value: number | null | undefined): string {
  return fmtRsd(value, 0);
}

export function fmtRsdCompact(value: number | null | undefined, digits = 1, fallback = ANALYTICS_UNAVAILABLE_LABEL): string {
  if (value == null || !Number.isFinite(value)) return fallback;

  const absolute = Math.abs(value);
  if (absolute >= 1_000_000) {
    return `${fmtNumber(value / 1_000_000, digits, fallback)}M RSD`;
  }

  if (absolute >= 1_000) {
    return `${fmtNumber(value / 1_000, digits, fallback)}k RSD`;
  }

  return fmtRsd(value, 0, fallback);
}

export function fmtPct(value: number | null | undefined, digits = 1, fallback = ANALYTICS_UNAVAILABLE_LABEL): string {
  if (value == null || !Number.isFinite(value)) return fallback;
  return `${fmtNumber(value, digits)}%`;
}

export function fmtPctFromRatio(value: number | null | undefined, digits = 1, fallback = ANALYTICS_UNAVAILABLE_LABEL): string {
  if (value == null || !Number.isFinite(value)) return fallback;
  return fmtPct(value * 100, digits, fallback);
}

export function fmtSignedPct(value: number | null | undefined, digits = 1): string {
  if (value == null || !Number.isFinite(value)) return ANALYTICS_UNAVAILABLE_LABEL;
  const sign = value > 0 ? "+" : "";
  return `${sign}${fmtPct(value, digits)}`;
}

/** Percentage-point deltas (1 = one percentage point, not 1% relative growth). */
export function fmtSignedPctPoints(value: number | null | undefined, digits = 1, fallback = ANALYTICS_UNAVAILABLE_LABEL): string {
  if (value == null || !Number.isFinite(value)) return fallback;
  const sign = value > 0 ? "+" : "";
  return `${sign}${fmtNumber(value, digits)} pp`;
}

export function fmtPctPoints(value: number | null | undefined, digits = 1, fallback = ANALYTICS_UNAVAILABLE_LABEL): string {
  if (value == null || !Number.isFinite(value)) return fallback;
  return `${fmtNumber(value, digits)} pp`;
}

export function fmtQty(value: number | null | undefined, digits = 0, fallback = ANALYTICS_UNAVAILABLE_LABEL): string {
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

  return parsed.toLocaleDateString("sr-RS", {
    day: "numeric",
    month: "numeric",
    year: "numeric",
    timeZone: "UTC",
  });
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
    timeZone: "UTC",
  });
}

export function getPresetRange(
  preset: AnalyticsComparablePeriodPreset,
  now?: Date
): { fromDate: string; toDate: string } {
  return getAnalyticsPeriodPresetRange(preset, now);
}

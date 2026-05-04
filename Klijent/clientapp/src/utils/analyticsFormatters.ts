import {
  getAnalyticsPeriodPresetRange,
  type AnalyticsComparablePeriodPreset,
} from "./analyticsPeriodPresets";

export function fmtRsd(value: number, digits = 0): string {
  return `${value.toLocaleString("sr-RS", {
    minimumFractionDigits: digits,
    maximumFractionDigits: digits,
  })} RSD`;
}

export function fmtRsdShort(value: number): string {
  return fmtRsd(value, 0);
}

export function fmtPct(value: number | null | undefined, digits = 1): string {
  if (value == null || Number.isNaN(value)) return "N/A";
  return `${value.toLocaleString("sr-RS", {
    minimumFractionDigits: digits,
    maximumFractionDigits: digits,
  })}%`;
}

export function fmtSignedPct(value: number | null | undefined, digits = 1): string {
  if (value == null || Number.isNaN(value)) return "N/A";
  const sign = value > 0 ? "+" : "";
  return `${sign}${fmtPct(value, digits)}`;
}

export function fmtQty(value: number): string {
  return `${value.toLocaleString("sr-RS")} kom`;
}

export function getPresetRange(
  preset: AnalyticsComparablePeriodPreset
): { fromDate: string; toDate: string } {
  return getAnalyticsPeriodPresetRange(preset);
}

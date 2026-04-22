export const ANALYTICS_PERIOD_PRESET_VALUES = ["30d", "90d", "180d", "365d", "custom"] as const;

export type AnalyticsPeriodPreset = (typeof ANALYTICS_PERIOD_PRESET_VALUES)[number];
export type AnalyticsComparablePeriodPreset = Exclude<AnalyticsPeriodPreset, "custom">;

export const ANALYTICS_PERIOD_PRESET_OPTIONS: ReadonlyArray<{
  value: AnalyticsPeriodPreset;
  label: string;
}> = [
  { value: "30d", label: "Poslednjih 30 dana" },
  { value: "90d", label: "Poslednjih 90 dana" },
  { value: "180d", label: "Poslednjih 180 dana" },
  { value: "365d", label: "Poslednjih 365 dana" },
  { value: "custom", label: "Prilagođeno" },
];

export function isAnalyticsPeriodPreset(value: string): value is AnalyticsPeriodPreset {
  return (ANALYTICS_PERIOD_PRESET_VALUES as readonly string[]).includes(value);
}

export function getAnalyticsPeriodPresetRange(
  preset: AnalyticsComparablePeriodPreset,
  now = new Date()
): { fromDate: string; toDate: string } {
  const from = new Date(now);
  const to = new Date(now);
  to.setHours(23, 59, 59, 999);

  if (preset === "30d") from.setDate(from.getDate() - 29);
  if (preset === "90d") from.setDate(from.getDate() - 89);
  if (preset === "180d") from.setDate(from.getDate() - 179);
  if (preset === "365d") from.setDate(from.getDate() - 364);

  from.setHours(0, 0, 0, 0);

  const toDateInput = (date: Date): string => date.toISOString().slice(0, 10);
  return { fromDate: toDateInput(from), toDate: toDateInput(to) };
}

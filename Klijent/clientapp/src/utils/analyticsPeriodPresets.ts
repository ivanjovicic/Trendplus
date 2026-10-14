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

const PRESET_DAY_COUNT: Record<AnalyticsComparablePeriodPreset, number> = {
  "30d": 30,
  "90d": 90,
  "180d": 180,
  "365d": 365,
};

// The backend resolves fromDate/toDate as Europe/Belgrade calendar days (BelgradeCalendarDatePolicy),
// so "today" must be the Belgrade date, not the UTC date, during the hours around local midnight.
const belgradeDateFormatter = new Intl.DateTimeFormat("en-US", {
  timeZone: "Europe/Belgrade",
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
});

function toBelgradeDateInput(date: Date): string {
  const parts = Object.fromEntries(
    belgradeDateFormatter.formatToParts(date).map((part) => [part.type, part.value]),
  );
  return `${parts.year}-${parts.month}-${parts.day}`;
}

function shiftDateInput(value: string, days: number): string {
  const date = new Date(`${value}T12:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

export function getAnalyticsPeriodPresetRange(
  preset: AnalyticsComparablePeriodPreset,
  now = new Date()
): { fromDate: string; toDate: string } {
  const toDate = toBelgradeDateInput(now);
  const dayCount = PRESET_DAY_COUNT[preset] ?? 1;
  return { fromDate: shiftDateInput(toDate, -(dayCount - 1)), toDate };
}

export function resolvePresetFilterRange(
  preset: AnalyticsPeriodPreset,
  draftFromDate: string,
  draftToDate: string,
  now?: Date,
): { fromDate: string; toDate: string } {
  if (preset === "custom") {
    return { fromDate: draftFromDate, toDate: draftToDate };
  }
  return getAnalyticsPeriodPresetRange(preset, now);
}

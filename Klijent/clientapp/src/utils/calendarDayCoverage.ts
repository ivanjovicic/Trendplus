import type { DailySale } from "../types/analytics";

export type CalendarDayStatus =
  | "observed_sale"
  | "verified_empty"
  | "unknown_day"
  | "partial_day"
  | "outside_window";

export type SourceDayCoverageStatus =
  | "observed_sales"
  | "verified_empty"
  | "partial"
  | "failed"
  | "unverified";

export interface CalendarDayPoint {
  date: string;
  status: CalendarDayStatus;
  totalRevenue: number | null;
  totalUnits: number | null;
  sourceScopeKey?: string;
}

export interface CalendarDayCoverageEvidence {
  scopeKey: string;
  fromDate: string;
  toDate: string;
  days: readonly { date: string; status: SourceDayCoverageStatus }[];
}

export interface CalendarDayCoverageOptions {
  scopeKey?: string;
  evidence?: CalendarDayCoverageEvidence | null;
}

export function normalizeCalendarDate(value: string): string | null {
  const date = value.slice(0, 10);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) return null;

  const parsed = new Date(`${date}T00:00:00.000Z`);
  return Number.isFinite(parsed.getTime()) && parsed.toISOString().slice(0, 10) === date
    ? date
    : null;
}

function addCalendarDays(date: string, days: number): string {
  const timestamp = Date.parse(`${date}T00:00:00.000Z`) + days * 24 * 60 * 60 * 1000;
  return new Date(timestamp).toISOString().slice(0, 10);
}

function isWithinPeriod(date: string, fromDate: string, toDate: string): boolean {
  return date >= fromDate && date <= toDate;
}

function coverageMatchesRequest(
  options: CalendarDayCoverageOptions | undefined,
  fromDate: string,
  toDate: string,
): options is CalendarDayCoverageOptions & { scopeKey: string; evidence: CalendarDayCoverageEvidence } {
  const scopeKey = options?.scopeKey?.trim();
  const evidence = options?.evidence;
  return Boolean(
    scopeKey
      && evidence
      && evidence.scopeKey === scopeKey
      && normalizeCalendarDate(evidence.fromDate) === fromDate
      && normalizeCalendarDate(evidence.toDate) === toDate,
  );
}

export function buildCalendarDayGrid(
  dailySales: readonly DailySale[],
  requestedFromDate: string,
  requestedToDate: string,
  options?: CalendarDayCoverageOptions,
): CalendarDayPoint[] {
  const fromDate = normalizeCalendarDate(requestedFromDate);
  const toDate = normalizeCalendarDate(requestedToDate);
  if (!fromDate || !toDate || fromDate > toDate) return [];

  const hasMatchingCoverage = coverageMatchesRequest(options, fromDate, toDate);
  const scopeKey = hasMatchingCoverage ? options.scopeKey : undefined;
  const coverageByDate = new Map<string, SourceDayCoverageStatus | "conflict">();
  if (hasMatchingCoverage) {
    for (const day of options.evidence.days) {
      const date = normalizeCalendarDate(day.date);
      if (!date || !isWithinPeriod(date, fromDate, toDate)) continue;
      const existing = coverageByDate.get(date);
      coverageByDate.set(date, existing == null || existing === day.status ? day.status : "conflict");
    }
  }

  const salesByDate = new Map<string, DailySale | null>();
  const duplicateSalesDates = new Set<string>();
  for (const sale of dailySales) {
    const date = normalizeCalendarDate(sale.date);
    if (!date || !isWithinPeriod(date, fromDate, toDate)) continue;
    if (salesByDate.has(date)) {
      duplicateSalesDates.add(date);
      salesByDate.set(date, null);
      continue;
    }
    const isFinite = Number.isFinite(sale.totalRevenue) && Number.isFinite(sale.totalUnits);
    salesByDate.set(date, isFinite ? sale : null);
  }

  const days: CalendarDayPoint[] = [];
  for (let date = fromDate; date <= toDate; date = addCalendarDays(date, 1)) {
    const sale = salesByDate.get(date);
    const hasSaleRow = salesByDate.has(date);
    const coverageStatus = coverageByDate.get(date);

    if (hasSaleRow) {
      if (!sale || duplicateSalesDates.has(date) || coverageStatus === "conflict") {
        days.push({ date, status: "partial_day", totalRevenue: null, totalUnits: null });
      } else if (coverageStatus === "partial" || coverageStatus === "failed" || coverageStatus === "unverified" || coverageStatus === "verified_empty") {
        days.push({ date, status: "partial_day", totalRevenue: sale.totalRevenue, totalUnits: sale.totalUnits });
      } else {
        days.push({ date, status: "observed_sale", totalRevenue: sale.totalRevenue, totalUnits: sale.totalUnits });
      }
      continue;
    }

    if (coverageStatus === "verified_empty" && scopeKey) {
      days.push({ date, status: "verified_empty", totalRevenue: 0, totalUnits: 0, sourceScopeKey: scopeKey });
    } else if (coverageStatus != null) {
      days.push({ date, status: "partial_day", totalRevenue: null, totalUnits: null });
    } else {
      days.push({ date, status: "unknown_day", totalRevenue: null, totalUnits: null });
    }
  }

  return days;
}

export function getCompleteCalendarDayWindow(
  days: readonly CalendarDayPoint[],
  startIndex: number,
  length: number,
  expectedScopeKey?: string,
): CalendarDayPoint[] | null {
  if (!Number.isInteger(startIndex) || !Number.isInteger(length) || length <= 0) return null;
  if (startIndex < 0 || startIndex + length > days.length) return null;

  const window = days.slice(startIndex, startIndex + length);
  for (let index = 0; index < window.length; index += 1) {
    const day = window[index];
    const valuesAreFinite = Number.isFinite(day.totalRevenue) && Number.isFinite(day.totalUnits);
    const isObserved = day.status === "observed_sale" && valuesAreFinite;
    const isVerifiedEmpty = day.status === "verified_empty"
      && Boolean(expectedScopeKey)
      && day.sourceScopeKey === expectedScopeKey
      && day.totalRevenue === 0
      && day.totalUnits === 0;
    if (!isObserved && !isVerifiedEmpty) return null;
    if (index > 0 && day.date !== addCalendarDays(window[index - 1].date, 1)) return null;
  }

  return window;
}

export function sumCalendarDayWindow(
  window: readonly CalendarDayPoint[] | null,
  field: "totalRevenue" | "totalUnits",
): number | null {
  if (!window || window.length === 0) return null;
  const values = window.map((day) => day[field]);
  if (values.some((value) => value == null || !Number.isFinite(value))) return null;
  return values.reduce<number>((sum, value) => sum + value!, 0);
}

export function averageCalendarDayWindow(
  window: readonly CalendarDayPoint[] | null,
  field: "totalRevenue" | "totalUnits",
): number | null {
  const sum = sumCalendarDayWindow(window, field);
  return sum == null || !window?.length ? null : sum / window.length;
}

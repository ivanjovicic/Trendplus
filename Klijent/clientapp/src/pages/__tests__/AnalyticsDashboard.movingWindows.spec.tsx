import { describe, expect, it } from "vitest";
import { calculateDashboardMovingStats } from "../AnalyticsDashboard";
import type { DailySale } from "../../types/analytics";
import {
  averageCalendarDayWindow,
  buildCalendarDayGrid,
  getCompleteCalendarDayWindow,
} from "../../utils/calendarDayCoverage";

function dayAtOffset(endDate: string, offset: number): string {
  const [year, month, day] = endDate.split("-").map(Number);
  const date = new Date(Date.UTC(year, month - 1, day + offset));
  return date.toISOString().slice(0, 10);
}

function makeDailySales(endDate: string, count: number, missingOffsets: number[] = []): DailySale[] {
  return Array.from({ length: count }, (_, index) => ({
    date: dayAtOffset(endDate, index - count + 1),
    totalRevenue: 100 + index * 10,
    transactionCount: index + 1,
    totalUnits: 10 + index,
  })).filter((_, index) => !missingOffsets.includes(index - count + 1));
}

describe("AnalyticsDashboard moving windows", () => {
  it("does not treat a missing calendar day as a complete seven-day window", () => {
    const fromDate = "2026-09-26T00:00";
    const toDate = "2026-10-16T23:59";
    const dailySales = makeDailySales("2026-10-16", 21, [-5]);

    const stats = calculateDashboardMovingStats(dailySales, fromDate, toDate);

    expect(stats.ma7Revenue).toBeNull();
    expect(stats.momentumPct).toBeNull();
    expect(stats.elasticity).toBeNull();
  });

  it("uses exact complete seven- and thirty-calendar-day windows", () => {
    const dailySales = makeDailySales("2026-10-16", 30);

    const stats = calculateDashboardMovingStats(
      dailySales,
      "2026-09-17T00:00",
      "2026-10-16T23:59",
    );

    expect(stats.ma7Revenue).toBe(360);
    expect(stats.ma30Revenue).toBe(245);
    expect(stats.momentumPct).toBeCloseTo(24.14, 2);
    expect(stats.elasticity).toBeNull();
  });

  it("preserves signed returns instead of clamping a complete-window average", () => {
    const dailySales = makeDailySales("2026-10-16", 7).map((sale) => ({
      ...sale,
      totalRevenue: 100,
      totalUnits: 10,
    }));
    dailySales[6] = { ...dailySales[6], totalRevenue: -100, totalUnits: -1 };

    const stats = calculateDashboardMovingStats(
      dailySales,
      "2026-10-10T00:00",
      "2026-10-16T23:59",
    );

    expect(stats.ma7Revenue).toBeCloseTo(71.43, 2);
  });

  it("keeps windows unavailable when the requested series is short or empty", () => {
    const short = calculateDashboardMovingStats(
      makeDailySales("2026-10-16", 6),
      "2026-10-11",
      "2026-10-16",
    );
    const empty = calculateDashboardMovingStats([], "2026-10-10", "2026-10-16");

    expect(short).toEqual({ ma7Revenue: null, ma30Revenue: null, momentumPct: null, elasticity: null });
    expect(empty).toEqual({ ma7Revenue: null, ma30Revenue: null, momentumPct: null, elasticity: null });
  });

  it("uses an explicit verified-empty marker only for the matching scope and period", () => {
    const dailySales = makeDailySales("2026-10-16", 14, [-3]);
    const missingDate = "2026-10-13";
    const fromDate = "2026-10-03";
    const toDate = "2026-10-16";
    const scopeKey = "store:17/source:batch-42";
    const evidence = {
      scopeKey,
      fromDate,
      toDate,
      days: [{ date: missingDate, status: "verified_empty" as const }],
    };

    const matchingGrid = buildCalendarDayGrid(dailySales, fromDate, toDate, { scopeKey, evidence });
    const matchingWindow = getCompleteCalendarDayWindow(matchingGrid, 7, 7, scopeKey);
    const wrongScopeGrid = buildCalendarDayGrid(dailySales, fromDate, toDate, {
      scopeKey: "store:18/source:batch-42",
      evidence,
    });
    const wrongScopeWindow = getCompleteCalendarDayWindow(wrongScopeGrid, 7, 7, "store:18/source:batch-42");

    expect(matchingGrid.find((day) => day.date === missingDate)).toMatchObject({
      status: "verified_empty",
      totalRevenue: 0,
      totalUnits: 0,
    });
    expect(averageCalendarDayWindow(matchingWindow, "totalRevenue")).toBeCloseTo(1200 / 7, 6);
    expect(wrongScopeGrid.find((day) => day.date === missingDate)?.status).toBe("unknown_day");
    expect(wrongScopeWindow).toBeNull();
  });

  it("keeps partial days out of complete windows and excludes outside-period rows", () => {
    const dailySales = [
      ...makeDailySales("2026-10-16", 7),
      {
        date: "2026-10-17",
        totalRevenue: 9_999,
        transactionCount: 1,
        totalUnits: 99,
      },
    ];
    const scopeKey = "store:17/source:batch-42";
    const grid = buildCalendarDayGrid(dailySales, "2026-10-10", "2026-10-16", {
      scopeKey,
      evidence: {
        scopeKey,
        fromDate: "2026-10-10",
        toDate: "2026-10-16",
        days: [{ date: "2026-10-13", status: "partial" }],
      },
    });

    expect(grid).toHaveLength(7);
    expect(grid.at(-1)?.date).toBe("2026-10-16");
    expect(grid.find((day) => day.date === "2026-10-13")?.status).toBe("partial_day");
    expect(getCompleteCalendarDayWindow(grid, 0, 7, scopeKey)).toBeNull();
  });
});

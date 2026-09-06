import { describe, expect, it } from "vitest";
import { calculateDashboardMovingStats } from "../AnalyticsDashboard";
import {
  deriveAnalyticsDetailMetrics,
  getAnalyticsDetailPeriodDays,
  getTrendDirection,
  isAnalyticsDetailPeriodValid,
  selectTrendRowsByDirection,
} from "../AnalyticsDetails";
import { describePopMetric } from "../ColorSalesStatsPage";
import { calculateAnomalyDeviation, calculateDeltaPct } from "../DailySalesStatsPage";
import { formatPercent } from "../../components/inventory/inventoryUtils";

describe("analytics indicator regression guards", () => {
  it("keeps moving averages unavailable when there is no daily history", () => {
    expect(calculateDashboardMovingStats([])).toMatchObject({
      ma7Revenue: null,
      ma30Revenue: null,
      momentumPct: null,
      elasticity: null,
    });
  });

  it("does not label a partial history as a full MA7 or MA30 window", () => {
    const rows = Array.from({ length: 6 }, (_, index) => ({
      date: `2026-08-${String(index + 1).padStart(2, "0")}`,
      totalRevenue: 1000,
      transactionCount: 1,
      totalUnits: 2,
    }));

    expect(calculateDashboardMovingStats(rows).ma7Revenue).toBeNull();
    expect(calculateDashboardMovingStats(rows).ma30Revenue).toBeNull();
  });

  it("does not turn a missing detail summary into zero per-day KPIs", () => {
    expect(deriveAnalyticsDetailMetrics(null, "2026-08-01T00:00", "2026-08-30T23:59")).toMatchObject({
      days: 30,
      revPerDay: null,
      txPerDay: null,
      unitsPerDay: null,
    });
  });

  it("uses an exact inclusive day count for valid detail periods", () => {
    expect(getAnalyticsDetailPeriodDays("2026-08-01T00:00", "2026-08-01T23:59")).toBe(1);
    expect(getAnalyticsDetailPeriodDays("2026-08-01T00:00", "2026-08-30T23:59")).toBe(30);
  });

  it("fails closed for reversed or invalid detail periods", () => {
    expect(isAnalyticsDetailPeriodValid("2026-08-30T00:00", "2026-08-01T23:59")).toBe(false);
    expect(isAnalyticsDetailPeriodValid("not-a-date", "2026-08-01T23:59")).toBe(false);
    expect(isAnalyticsDetailPeriodValid("2026-02-30T00:00", "2026-03-01T23:59")).toBe(false);
    expect(getAnalyticsDetailPeriodDays("2026-08-30T00:00", "2026-08-01T23:59")).toBeNull();
    expect(deriveAnalyticsDetailMetrics({ totalRevenue: 100, totalTransactions: 2, totalUnits: 3 }, "2026-08-30T00:00", "2026-08-01T23:59")).toMatchObject({
      days: null,
      revPerDay: null,
      txPerDay: null,
      unitsPerDay: null,
    });
  });

  it("does not expose non-finite detail summary values as per-day KPIs", () => {
    expect(deriveAnalyticsDetailMetrics({ totalRevenue: Number.NaN, totalTransactions: Number.POSITIVE_INFINITY, totalUnits: 0 }, "2026-08-01T00:00", "2026-08-01T23:59")).toMatchObject({
      days: 1,
      revPerDay: null,
      txPerDay: null,
      unitsPerDay: 0,
    });
  });

  it("keeps unknown and non-finite trends out of direction and ranking", () => {
    expect(getTrendDirection(null)).toBe("neutral");
    expect(getTrendDirection(undefined)).toBe("neutral");
    expect(getTrendDirection(Number.NaN)).toBe("neutral");
    expect(getTrendDirection(Number.POSITIVE_INFINITY)).toBe("neutral");
    expect(getTrendDirection(0)).toBe("neutral");
    expect(getTrendDirection(4)).toBe("up");
    expect(getTrendDirection(-4)).toBe("down");

    const rows = [
      { productId: 1, trendPct: null },
      { productId: 2, trendPct: Number.NaN },
      { productId: 3, trendPct: Number.POSITIVE_INFINITY },
      { productId: 4, trendPct: 0 },
      { productId: 5, trendPct: 8 },
      { productId: 6, trendPct: -3 },
    ] as never;

    expect(selectTrendRowsByDirection(rows, "up").map((row) => row.productId)).toEqual([5]);
    expect(selectTrendRowsByDirection(rows, "down").map((row) => row.productId)).toEqual([6]);
  });

  it("does not describe an unknown previous-period revenue as zero", () => {
    const result = describePopMetric({
      ukupanPromet: 120000,
      previousPeriodRevenue: null,
      popRevenueChangePct: 12,
    } as never);

    expect(result.title).toContain("Nije dostupno");
    expect(result.title).not.toContain("0 RSD");
  });

  it("keeps a positive-vs-zero baseline change unknown instead of showing 0%", () => {
    expect(calculateDeltaPct(10, 0)).toBeNull();
    expect(calculateAnomalyDeviation(10, 0).deviationPct).toBeNull();
  });

  it("does not turn an empty inventory denominator into a valid percentage", () => {
    expect(formatPercent(null)).toBe("Nije dostupno");
  });
});

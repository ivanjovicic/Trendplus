import { describe, expect, it } from "vitest";
import { calculateDashboardMovingStats } from "../AnalyticsDashboard";
import { deriveAnalyticsDetailMetrics } from "../AnalyticsDetails";
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
      revPerDay: null,
      txPerDay: null,
      unitsPerDay: null,
    });
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

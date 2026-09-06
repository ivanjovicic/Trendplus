import { describe, expect, it } from "vitest";
import {
  buildRollingAverage,
  calculateAnomalyDeviation,
  calculateDeltaPct,
  safeDivide,
  summarizePeriod,
} from "../DailySalesStatsPage";
import type { DailySalesRow, DailySalesTableResponse } from "../../services/dailySalesStatsApi";

function row(overrides: Partial<DailySalesRow> = {}): DailySalesRow {
  return {
    date: "2026-07-01",
    firstShiftTotalItems: 0,
    secondShiftTotalItems: 0,
    totalRevenue: 0,
    topSupplierCounts: [0],
    othersCount: 0,
    totalItemsSold: 0,
    ...overrides,
  };
}

function response(overrides: Partial<DailySalesTableResponse> = {}): DailySalesTableResponse {
  return {
    requestedFrom: "2026-07-01",
    requestedTo: "2026-07-01",
    storeId: null,
    topN: 15,
    dataScope: "all",
    topSuppliers: [],
    topSuppliersOrder: [],
    dateRows: [row()],
    metadata: {
      totalDays: 1,
      uniqueSuppliersInRange: 0,
      unknownSupplierPct: 0,
      unknownSupplierItems: 0,
      offShiftItems: 0,
      offShiftRevenue: 0,
      totalItemsInRange: 0,
      duplicateReceiptGroupCount: 0,
      duplicateReceiptHeaderCount: 0,
      receiptAmountMismatchCount: 0,
      receiptAmountMismatchRevenue: 0,
      nonStandardReceiptCount: 0,
      nonStandardReceiptRevenue: 0,
      debtReceiptCount: 0,
      debtReceiptRevenue: 0,
      minAvailableDate: null,
      maxAvailableDate: null,
      warnings: [],
    },
    ...overrides,
  };
}

describe("Daily Sales numeric evidence states", () => {
  it("keeps a successful empty response distinct from a measured zero", () => {
    const summary = summarizePeriod(response({ dateRows: [] }));

    expect(summary.totalRevenue).toBeNull();
    expect(summary.totalVisibleItems).toBeNull();
    expect(summary.avgRevenuePerDay).toBeNull();
    expect(summary.avgRevenuePerItem).toBeNull();
  });

  it("preserves null and missing row evidence instead of filling rolling averages with zero", () => {
    const rows = [row({ totalRevenue: 100, totalItemsSold: null }), row({ totalRevenue: null })];
    const summary = summarizePeriod(response({ dateRows: rows }));

    expect(buildRollingAverage(rows, 1, (item) => item.totalRevenue)).toBeNull();
    expect(summary.totalRevenue).toBeNull();
    expect(summary.totalVisibleItems).toBeNull();
    expect(calculateDeltaPct(null, 100)).toBeNull();
  });

  it("preserves genuine measured zero values", () => {
    const rows = [row({ totalRevenue: 0, totalItemsSold: 0 })];
    const summary = summarizePeriod(response({ dateRows: rows }));

    expect(safeDivide(0, 10)).toBe(0);
    expect(safeDivide(0, 0)).toBeNull();
    expect(buildRollingAverage(rows, 0, (item) => item.totalRevenue)).toBe(0);
    expect(calculateDeltaPct(0, 0)).toBe(0);
    expect(summary.totalRevenue).toBe(0);
    expect(summary.totalVisibleItems).toBe(0);
    expect(summary.avgRevenuePerItem).toBeNull();
  });

  it("does not convert a missing denominator into a ratio of zero", () => {
    const summary = summarizePeriod(response({
      dateRows: [row({ totalRevenue: 100, totalItemsSold: 0 })],
      metadata: {
        ...response().metadata,
        totalItemsInRange: 0,
      },
    }));

    expect(safeDivide(100, null)).toBeNull();
    expect(safeDivide(100, undefined)).toBeNull();
    expect(safeDivide(100, 0)).toBeNull();
    expect(summary.avgRevenuePerItem).toBeNull();
  });

  it("rejects NaN and Infinity at calculation boundaries", () => {
    const rows = [row({ totalRevenue: Number.NaN }), row({ totalRevenue: Number.POSITIVE_INFINITY })];

    expect(safeDivide(Number.NaN, 10)).toBeNull();
    expect(safeDivide(10, Number.POSITIVE_INFINITY)).toBeNull();
    expect(buildRollingAverage(rows, 1, (item) => item.totalRevenue)).toBeNull();
    expect(calculateAnomalyDeviation(Number.POSITIVE_INFINITY, 100)).toEqual({
      deviationValue: null,
      deviationPct: null,
    });
  });

  it("does not turn partial metadata into trusted zero values", () => {
    const partial = response({
      metadata: {
        ...response().metadata,
        totalDays: undefined,
        totalItemsInRange: undefined,
        offShiftItems: undefined,
        uniqueSuppliersInRange: undefined,
      },
    });

    const summary = summarizePeriod(partial);

    expect(summary.totalDays).toBe(1);
    expect(summary.totalItemsInRange).toBe(0);
    expect(summary.offShiftItems).toBeNull();
    expect(summary.uniqueSuppliersInRange).toBeNull();
  });
});

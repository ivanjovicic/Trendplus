import { describe, expect, it } from "vitest";
import {
  buildRollingAverage,
  calculateAnomalyDeviation,
  calculateDeltaPct,
  buildSupplierConcentration,
  safeDivide,
  sortDailySalesRows,
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

  it("provides one stable sort order for table and chart rows", () => {
    const rows = [
      row({ date: "2026-07-01", totalRevenue: 100 }),
      row({ date: "2026-07-02", totalRevenue: 50 }),
    ];

    expect(sortDailySalesRows(rows, "date", "desc").map((item) => item.date)).toEqual([
      "2026-07-02",
      "2026-07-01",
    ]);
    expect(sortDailySalesRows(rows, "totalRevenue", "asc").map((item) => item.date)).toEqual([
      "2026-07-02",
      "2026-07-01",
    ]);
  });

  it("sorts date-only rows as UTC calendar dates across DST boundaries", () => {
    const rows = [
      row({ date: "2026-10-25" }),
      row({ date: "2026-03-29" }),
      row({ date: "2026-03-28" }),
    ];

    expect(sortDailySalesRows(rows, "date", "asc").map((item) => item.date)).toEqual([
      "2026-03-28",
      "2026-03-29",
      "2026-10-25",
    ]);
    expect(sortDailySalesRows(rows, "date", "desc").map((item) => item.date)).toEqual([
      "2026-10-25",
      "2026-03-29",
      "2026-03-28",
    ]);
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

  it("keeps supplier concentration unavailable when top totals contradict the period totals", () => {
    const inconsistent = response({
      topSuppliers: [{
        supplierId: 1,
        supplierName: "Alfa",
        isUnknown: false,
        totalQty: 25,
        totalRevenue: 12000,
      }],
      topSuppliersOrder: ["Alfa"],
      metadata: { ...response().metadata, totalItemsInRange: 18 },
      dateRows: [row({ totalItemsSold: 18, totalRevenue: 9000 })],
    });

    const concentration = buildSupplierConcentration(inconsistent, 9000);

    expect(concentration.warning).toContain("više komada");
    expect(concentration.top3QtySharePct).toBeNull();
    expect(concentration.top5QtySharePct).toBeNull();
    expect(concentration.suppliersTo80Pct).toBeNull();
    expect(concentration.chartData[0]?.qtySharePct).toBeNull();
    expect(concentration.chartData.find((item) => item.supplierName === "Ostali")).toBeUndefined();
  });

  it("uses topSuppliersOrder for concentration ranking instead of response array order", () => {
    const payload = response({
      topSuppliers: [
        { supplierId: 2, supplierName: "Bravo", isUnknown: false, totalQty: 8, totalRevenue: 800 },
        { supplierId: 1, supplierName: "Alfa", isUnknown: false, totalQty: 52, totalRevenue: 5200 },
        { supplierId: 3, supplierName: "Charlie", isUnknown: false, totalQty: 5, totalRevenue: 500 },
      ],
      topSuppliersOrder: ["Alfa", "Bravo", "Charlie"],
      metadata: { ...response().metadata, totalItemsInRange: 65 },
      dateRows: [row({ totalItemsSold: 65, totalRevenue: 6500 })],
    });

    const concentration = buildSupplierConcentration(payload, 6500);

    expect(concentration.warning).toBeNull();
    expect(concentration.chartData.map((item) => item.supplierName)).toEqual(["Alfa", "Bravo", "Charlie"]);
    expect(concentration.chartData[0]?.qtySharePct).toBeCloseTo((52 / 65) * 100, 5);
    expect(concentration.suppliersTo80Pct).toBe(1);
  });

  it("marks concentration unavailable when supplier order metadata is missing or ambiguous", () => {
    const missingOrder = buildSupplierConcentration(response({
      topSuppliers: [{
        supplierId: 1,
        supplierName: "Alfa",
        isUnknown: false,
        totalQty: 10,
        totalRevenue: 1000,
      }],
      topSuppliersOrder: [],
      metadata: { ...response().metadata, totalItemsInRange: 10 },
    }), 1000);

    expect(missingOrder.warning).toContain("topSuppliersOrder");
    expect(missingOrder.chartData).toEqual([]);
    expect(missingOrder.top3QtySharePct).toBeNull();

    const duplicateOrder = buildSupplierConcentration(response({
      topSuppliers: [
        { supplierId: 1, supplierName: "Alfa", isUnknown: false, totalQty: 10, totalRevenue: 1000 },
        { supplierId: 2, supplierName: "Bravo", isUnknown: false, totalQty: 5, totalRevenue: 500 },
      ],
      topSuppliersOrder: ["Alfa", "Alfa"],
      metadata: { ...response().metadata, totalItemsInRange: 15 },
    }), 1500);

    expect(duplicateOrder.warning).toContain("duplirana imena");
    expect(duplicateOrder.suppliersTo80Pct).toBeNull();
  });

  it("preserves partial shift sums without presenting incomplete period shares as complete", () => {
    const rows = [
      row({ totalItemsSold: 10, firstShiftTotalItems: 4, secondShiftTotalItems: 6 }),
      row({ totalItemsSold: 8, firstShiftTotalItems: 3, secondShiftTotalItems: null }),
    ];
    const summary = summarizePeriod(response({ dateRows: rows }));

    expect(summary.firstShiftItems).toBe(7);
    expect(summary.secondShiftItems).toBe(6);
    expect(summary.firstShiftSharePct).toBeNull();
    expect(summary.secondShiftSharePct).toBeNull();
  });

  it("treats both-null and both-zero sold days as missing shift evidence", () => {
    const bothNull = summarizePeriod(response({
      dateRows: [row({ totalItemsSold: 12, firstShiftTotalItems: null, secondShiftTotalItems: null })],
    }));
    const bothZero = summarizePeriod(response({
      dateRows: [row({ totalItemsSold: 12, firstShiftTotalItems: 0, secondShiftTotalItems: 0 })],
    }));

    expect(bothNull.firstShiftSharePct).toBeNull();
    expect(bothZero.firstShiftSharePct).toBeNull();
    expect(bothNull.secondShiftItems).toBeNull();
    expect(bothZero.secondShiftItems).toBeNull();
  });

  it("keeps complete shift shares when every sold day has both measured shifts", () => {
    const summary = summarizePeriod(response({
      dateRows: [
        row({ totalItemsSold: 10, firstShiftTotalItems: 4, secondShiftTotalItems: 6 }),
        row({ totalItemsSold: 8, firstShiftTotalItems: 2, secondShiftTotalItems: 6 }),
      ],
    }));

    expect(summary.firstShiftItems).toBe(6);
    expect(summary.secondShiftItems).toBe(12);
    expect(summary.firstShiftSharePct).toBeCloseTo(33.333333, 4);
    expect(summary.secondShiftSharePct).toBeCloseTo(66.666667, 4);
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

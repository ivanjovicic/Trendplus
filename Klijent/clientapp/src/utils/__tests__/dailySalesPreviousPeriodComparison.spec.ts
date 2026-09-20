import { describe, expect, it } from "vitest";
import type { DailySalesTableResponse } from "../../services/dailySalesStatsApi";
import {
  DAILY_SALES_PREVIOUS_PERIOD_EMPTY_NOTE,
  formatDailySalesComparisonDelta,
  resolveDailySalesPreviousPeriodComparison,
} from "../dailySalesPreviousPeriodComparison";

function response(overrides: Partial<DailySalesTableResponse> = {}): DailySalesTableResponse {
  return {
    requestedFrom: "2026-03-01",
    requestedTo: "2026-03-30",
    storeId: null,
    topN: 5,
    dataScope: "all",
    topSuppliers: [],
    topSuppliersOrder: [],
    dateRows: [
      {
        date: "2026-03-01",
        firstShiftTotalItems: 10,
        secondShiftTotalItems: 8,
        totalRevenue: 9000,
        topSupplierCounts: [],
        othersCount: 0,
        totalItemsSold: 18,
      },
    ],
    metadata: {
      totalDays: 30,
      uniqueSuppliersInRange: 0,
      unknownSupplierPct: 0,
      unknownSupplierItems: 0,
      offShiftItems: 0,
      offShiftRevenue: 0,
      totalItemsInRange: 18,
      duplicateReceiptGroupCount: 0,
      duplicateReceiptHeaderCount: 0,
      receiptAmountMismatchCount: 0,
      receiptAmountMismatchRevenue: 0,
      nonStandardReceiptCount: 0,
      nonStandardReceiptRevenue: 0,
      debtReceiptCount: 0,
      debtReceiptRevenue: 0,
      minAvailableDate: "2026-01-01",
      maxAvailableDate: "2026-04-30",
      warnings: [],
    },
    meta: {
      success: true,
      generatedAtUtc: "2026-07-01T08:00:00Z",
      lastRefreshAtUtc: "2026-07-01T08:00:00Z",
      dataQualityStatus: "good",
      isPartial: false,
    },
    ...overrides,
  };
}

describe("dailySalesPreviousPeriodComparison", () => {
  it("marks a rejected previous-period request as failed comparison evidence", () => {
    const resolution = resolveDailySalesPreviousPeriodComparison(
      { status: "rejected", reason: new Error("Previous period timeout") },
      (reason) => (reason instanceof Error ? reason.message : "failed"),
    );

    expect(resolution.state).toBe("failed");
    expect(resolution.previousData).toBeNull();
    expect(resolution.warning).toBe("Previous period timeout");
    expect(resolution.emptyBaselineNote).toBeNull();
  });

  it("keeps a successful empty previous baseline distinct from failure", () => {
    const resolution = resolveDailySalesPreviousPeriodComparison(
      { status: "fulfilled", value: response({ dateRows: [] }) },
      () => "failed",
    );

    expect(resolution.state).toBe("empty");
    expect(resolution.warning).toBeNull();
    expect(resolution.emptyBaselineNote).toBe(DAILY_SALES_PREVIOUS_PERIOD_EMPTY_NOTE);
  });

  it("does not label failed comparison deltas as Nova baza or N/A", () => {
    expect(formatDailySalesComparisonDelta(null, 1200, null, "failed")).toBe("Nedostupno");
    expect(formatDailySalesComparisonDelta(null, 1200, 0, "available")).toBe("Nova baza");
    expect(formatDailySalesComparisonDelta(null, 1200, null, "empty")).toBe("N/A");
  });
});

import { describe, expect, it } from "vitest";
import {
  resolvePreviousPeriodComparison,
  resolveSupplierPeriodGrowthPct,
  SUPPLIER_PREVIOUS_PERIOD_EMPTY_NOTE,
} from "../supplierPreviousPeriodComparison";

const previousResponse = {
  totals: {
    postRevenue: 1_000,
    hasComparableSalesWindow: true,
  },
} as Awaited<ReturnType<typeof import("../../services/vendorSalesNivelacijaApi").getVendorSalesNivelacija>>;

describe("supplierPreviousPeriodComparison", () => {
  it("marks a rejected previous-period request as failed comparison evidence", () => {
    const result = resolvePreviousPeriodComparison(
      { status: "rejected", reason: new Error("timeout") },
      (reason) => (reason instanceof Error ? reason.message : String(reason)),
    );

    expect(result.state).toBe("failed");
    expect(result.previousRevenue).toBeNull();
    expect(result.warning).toBe("timeout");
    expect(result.emptyBaselineNote).toBeNull();
  });

  it("keeps a successful empty previous baseline distinct from failure", () => {
    const result = resolvePreviousPeriodComparison(
      {
        status: "fulfilled",
        value: {
          ...previousResponse,
          totals: {
            ...previousResponse.totals,
            postRevenue: 0,
            hasComparableSalesWindow: false,
          },
        },
      },
      () => "unused",
    );

    expect(result.state).toBe("empty");
    expect(result.warning).toBeNull();
    expect(result.emptyBaselineNote).toBe(SUPPLIER_PREVIOUS_PERIOD_EMPTY_NOTE);
  });

  it("does not fabricate period growth when the previous baseline failed", () => {
    expect(resolveSupplierPeriodGrowthPct({
      previousPeriodState: "failed",
      previousRevenue: null,
      totalRevenue: 1_200,
    })).toBeNull();

    expect(resolveSupplierPeriodGrowthPct({
      previousPeriodState: "empty",
      previousRevenue: null,
      totalRevenue: 1_200,
    })).toBeNull();
  });

  it("computes growth only from an available previous baseline", () => {
    expect(resolveSupplierPeriodGrowthPct({
      previousPeriodState: "available",
      previousRevenue: 1_000,
      totalRevenue: 1_200,
    })).toBe(20);
  });
});

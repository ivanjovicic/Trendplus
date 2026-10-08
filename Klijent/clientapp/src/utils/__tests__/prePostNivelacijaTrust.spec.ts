import { describe, expect, it } from "vitest";
import {
  comparablePrePostMetric,
  comparablePrePostTotal,
  baselineAwareRevenueChangePercent,
  hasComparablePrePostEvidence,
  revenueBaselineLabel,
  resolvePostRevenueSharePercent,
} from "../prePostNivelacijaTrust";

describe("prePostNivelacijaTrust", () => {
  it("fails closed for empty, null and unknown evidence flags", () => {
    expect(hasComparablePrePostEvidence(undefined)).toBe(false);
    expect(hasComparablePrePostEvidence({ hasComparableSalesWindow: null })).toBe(false);
    expect(comparablePrePostMetric(120, { hasComparableSalesWindow: false })).toBeNull();
    expect(comparablePrePostTotal(120, undefined)).toBeNull();
  });

  it("preserves a backend-proven valid zero", () => {
    const row = { hasComparableSalesWindow: true };
    expect(comparablePrePostMetric(0, row)).toBe(0);
    expect(comparablePrePostTotal(0, true)).toBe(0);
  });

  it("does not turn a missing denominator into a zero effect", () => {
    expect(comparablePrePostMetric(0, { hasComparableSalesWindow: false })).toBeNull();
    expect(comparablePrePostTotal(0, false)).toBeNull();
  });

  it("rejects NaN and Infinity even when the window is comparable", () => {
    const row = { hasComparableSalesWindow: true };
    expect(comparablePrePostMetric(Number.NaN, row)).toBeNull();
    expect(comparablePrePostMetric(Number.POSITIVE_INFINITY, row)).toBeNull();
    expect(comparablePrePostMetric(Number.NEGATIVE_INFINITY, row)).toBeNull();
  });

  it("does not recompute post revenue share when backend field is missing", () => {
    expect(resolvePostRevenueSharePercent({
      hasComparableSalesWindow: true,
      postRevenueSharePercent: null,
    })).toBeNull();
    expect(resolvePostRevenueSharePercent({
      hasComparableSalesWindow: true,
      postRevenueSharePercent: undefined,
    })).toBeNull();
    expect(resolvePostRevenueSharePercent({
      hasComparableSalesWindow: false,
      postRevenueSharePercent: 100,
    })).toBeNull();
  });

  it("uses backend post revenue share when available", () => {
    expect(resolvePostRevenueSharePercent({
      hasComparableSalesWindow: true,
      postRevenueSharePercent: 42.5,
    })).toBe(42.5);
    expect(resolvePostRevenueSharePercent({
      hasComparableSalesWindow: true,
      postRevenueSharePercent: 0,
    })).toBe(0);
  });

  it("keeps a proven zero trend and hides uplift without a revenue baseline", () => {
    expect(baselineAwareRevenueChangePercent({ hasRevenueBaseline: true, changePercent: 0 })).toBe(0);
    expect(baselineAwareRevenueChangePercent({
      hasRevenueBaseline: false,
      revenueBaselineReason: "no_pre_revenue_baseline_uplift",
      changePercent: 100,
    })).toBeNull();
    expect(revenueBaselineLabel({
      hasRevenueBaseline: false,
      revenueBaselineReason: "no_pre_revenue_baseline_uplift",
    })).toBe("Nova osnova; procenat promene nije uporediv");
  });

  it("preserves legacy trend values for responses without baseline metadata", () => {
    expect(baselineAwareRevenueChangePercent({ changePercent: 12.5 })).toBe(12.5);
    expect(revenueBaselineLabel({ changePercent: 12.5 })).toBeNull();
  });
});

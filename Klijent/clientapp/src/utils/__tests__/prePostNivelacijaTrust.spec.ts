import { describe, expect, it } from "vitest";
import {
  comparablePrePostMetric,
  comparablePrePostTotal,
  hasComparablePrePostEvidence,
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
});

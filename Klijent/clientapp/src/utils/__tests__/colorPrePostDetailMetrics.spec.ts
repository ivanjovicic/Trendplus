import { describe, expect, it } from "vitest";
import {
  formatColorPrePostQuantityMetric,
  formatColorPrePostRevenueMetric,
  resolveColorPrePostQuantityMetric,
  resolveColorPrePostRevenueMetric,
} from "../colorPrePostDetailMetrics";

describe("colorPrePostDetailMetrics", () => {
  it.each([0, 90000, -1500])("keeps finite revenue evidence available (%s)", (value) => {
    expect(resolveColorPrePostRevenueMetric(value)).toBe(value);
    expect(formatColorPrePostRevenueMetric(value)).toMatch(/RSD/);
  });

  it.each([0, 12, -3])("keeps finite quantity evidence available (%s)", (value) => {
    expect(resolveColorPrePostQuantityMetric(value)).toBe(value);
    expect(formatColorPrePostQuantityMetric(value)).toMatch(/kom/);
  });

  it.each([null, undefined, Number.NaN, Number.POSITIVE_INFINITY])(
    "fails closed for invalid revenue evidence (%s)",
    (value) => {
      expect(resolveColorPrePostRevenueMetric(value)).toBeNull();
      expect(formatColorPrePostRevenueMetric(value)).toBe("Nije dostupno");
    },
  );

  it.each([null, undefined, Number.NaN, Number.POSITIVE_INFINITY])(
    "fails closed for invalid quantity evidence (%s)",
    (value) => {
      expect(resolveColorPrePostQuantityMetric(value)).toBeNull();
      expect(formatColorPrePostQuantityMetric(value)).toBe("Nije dostupno");
    },
  );

  it("formats measured zero distinctly from unavailable evidence", () => {
    expect(formatColorPrePostRevenueMetric(0)).toMatch(/0.*RSD/);
    expect(formatColorPrePostQuantityMetric(0)).toMatch(/0.*kom/);
    expect(formatColorPrePostRevenueMetric(null)).toBe("Nije dostupno");
    expect(formatColorPrePostQuantityMetric(null)).toBe("Nije dostupno");
  });
});

import { describe, expect, it } from "vitest";
import {
  formatCategoryPrePostQuantityMetric,
  formatCategoryPrePostRevenueMetric,
  resolveCategoryPrePostRawMetric,
} from "../categoryPrePostDetailMetrics";

describe("categoryPrePostDetailMetrics", () => {
  it.each([0, 90000, -1500])("keeps finite revenue evidence available (%s)", (value) => {
    expect(resolveCategoryPrePostRawMetric(value)).toBe(value);
    expect(formatCategoryPrePostRevenueMetric(value)).toMatch(/RSD/);
  });

  it.each([0, 12, -3])("keeps finite quantity evidence available (%s)", (value) => {
    expect(resolveCategoryPrePostRawMetric(value)).toBe(value);
    expect(formatCategoryPrePostQuantityMetric(value)).toMatch(/kom/);
  });

  it.each([null, undefined, Number.NaN, Number.POSITIVE_INFINITY])(
    "fails closed for invalid raw evidence (%s)",
    (value) => {
      expect(resolveCategoryPrePostRawMetric(value)).toBeNull();
      expect(formatCategoryPrePostRevenueMetric(value)).toBe("Nije dostupno");
      expect(formatCategoryPrePostQuantityMetric(value)).toBe("Nije dostupno");
    },
  );

  it("formats measured zero distinctly from unavailable evidence", () => {
    expect(formatCategoryPrePostRevenueMetric(0)).toMatch(/0.*RSD/);
    expect(formatCategoryPrePostQuantityMetric(0)).toMatch(/0.*kom/);
    expect(formatCategoryPrePostRevenueMetric(null)).toBe("Nije dostupno");
    expect(formatCategoryPrePostQuantityMetric(null)).toBe("Nije dostupno");
  });
});

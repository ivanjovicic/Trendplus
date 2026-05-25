import { describe, expect, it } from "vitest";
import {
  getAnalyticsMetricDefinition,
  getMetricDefinition,
  getMetricFormula,
  getMetricMethodologyItems,
  normalizeMetricKey,
} from "../analyticsMetricDefinitions";

describe("analyticsMetricDefinitions", () => {
  it("returns base metric definitions", () => {
    const revenue = getAnalyticsMetricDefinition("revenue");
    const margin = getAnalyticsMetricDefinition("marginContribution");

    expect(revenue.label).toBe("Prihod");
    expect(revenue.formula).toContain("SUM");
    expect(margin.label).toBe("Maržni doprinos");
  });

  it("handles unknown key gracefully through generic helpers", () => {
    expect(getMetricDefinition("unknown_metric").label).toBe("unknown_metric");
    expect(getMetricFormula("unknown_metric")).toContain("nije dokumentovana");
  });

  it("builds methodology items with fallback for unknown metrics", () => {
    const items = getMetricMethodologyItems(["revenue", "unknown_metric"]);

    expect(items).toHaveLength(2);
    expect(items[0].label).toBe("Prihod");
    expect("isDocumented" in items[1]).toBe(true);
  });

  it("normalizes legacy aliases to canonical keys", () => {
    expect(normalizeMetricKey("totalRevenue")).toBe("revenue");
    expect(getAnalyticsMetricDefinition("totalRevenue").label).toBe("Prihod");
  });
});

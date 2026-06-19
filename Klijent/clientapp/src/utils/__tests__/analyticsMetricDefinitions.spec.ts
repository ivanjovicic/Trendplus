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
    expect(margin.label.toLowerCase()).toContain("doprinos");
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
    expect(normalizeMetricKey("stockUnits")).toBe("onHandUnits");
    expect(getAnalyticsMetricDefinition("stockUnits").label).toBe("Ukupno na stanju");
    expect(normalizeMetricKey("dataQualityStatus")).toBe("dataReadinessScore");
    expect(normalizeMetricKey("confidence")).toBe("confidencePct");
    expect(normalizeMetricKey("reliability")).toBe("reliabilityPct");
  });

  it("contains methodology definitions for rollout metrics", () => {
    expect(getAnalyticsMetricDefinition("revenueWithoutCost").formula).toContain("ukupan_prihod");
    expect(getAnalyticsMetricDefinition("unknownSupplierRevenueShare").label).toBeTruthy();
    expect(getAnalyticsMetricDefinition("blockedRecommendationsCount").label).toContain("Blokirane");
    expect(getAnalyticsMetricDefinition("ignoredRowsCount").label).toContain("Ignorisani");
    expect(getAnalyticsMetricDefinition("grossMarginPct").formula).toContain("prihod");
    expect(getAnalyticsMetricDefinition("inventoryTurnover").formula).toContain("/");
  });

  it("keeps denominator-sensitive metrics blocked when the denominator is missing", () => {
    const sellThrough = getAnalyticsMetricDefinition("sellThrough");
    const stockCoverDays = getAnalyticsMetricDefinition("stockCoverDays");

    expect(sellThrough.formula).toBe("soldUnits / (openingStockUnits + inboundUnits)");
    expect(stockCoverDays.formula).toBe("currentOnHandUnits / avgDailySalesUnits");
    expect(sellThrough.blockedWhen.join(" ")).toContain("openingStockUnits + inboundUnits <= 0");
    expect(stockCoverDays.blockedWhen.join(" ")).toContain("avgDailySalesUnits <= 0");
  });
});

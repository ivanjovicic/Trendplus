import { describe, expect, it } from "vitest";
import { buildTypeInsightChartProjection } from "../SupplierFootwearAnalyticsPage";

describe("Supplier Footwear type insight chart projection", () => {
  it("keeps full-cohort shares and adds Ostali when more than eight categories exist", () => {
    const categoryStats = Array.from({ length: 9 }, (_, index) => ({
      category: `Tip ${index + 1}`,
      postRevenueSharePercent: index === 8 ? 5 : 10,
    }));

    const projection = buildTypeInsightChartProjection({
      typeInsightsAuthoritative: true,
      typeInsightsDenominator: "comparable_post_revenue",
      categoryStats,
    } as never);

    expect(projection.totalCategoryCount).toBe(9);
    expect(projection.chartRows).toHaveLength(9);
    expect(projection.chartRows[7]?.name).toBe("Tip 8");
    expect(projection.chartRows[8]).toEqual({ name: "Ostali", sharePct: 5 });
    expect(projection.excludedSharePct).toBe(5);
    expect(projection.chartRows.reduce((sum, row) => sum + row.sharePct, 0)).toBe(85);
    expect(projection.displayDenominatorLabel).toContain("top 8 plus Ostali");
  });

  it("does not fabricate chart rows when type insights are not authoritative", () => {
    const projection = buildTypeInsightChartProjection({
      typeInsightsAuthoritative: false,
      categoryStats: [{ category: "Patike", postRevenueSharePercent: 100 }],
    } as never);

    expect(projection.chartRows).toEqual([]);
    expect(projection.displayDenominatorLabel).toBeNull();
  });
});

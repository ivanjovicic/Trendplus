import { describe, expect, it } from "vitest";
import {
  isEstimatedCategoryRevenue,
  presentDailyAnalysisMetrics,
  presentReorderV1Summary,
  presentSignedInsightPercent,
  presentSmartReorderProfit,
  presentSupplierMarginBenchmark,
} from "../insightStudioTrustPresentation";

describe("insightStudioTrustPresentation (RQ591)", () => {
  it("does not treat missing daily target as zero revenue day", () => {
    const view = presentDailyAnalysisMetrics({
      analysisDate: "2026-01-07",
      targetDataStatus: "missing",
      targetRevenue: null,
      targetUnits: null,
      meanRevenue: 1000,
      zScore: null,
      isOutlier: false,
      isExtremeOutlier: false,
      outlierLabel: "Nema podataka za ciljni dan",
      dailyData: [],
      top5Articles: [],
    });

    expect(view.hasTargetEvidence).toBe(false);
    expect(view.targetRevenueLabel).toBe("N/D");
    expect(view.zScoreLabel).toBe("N/D");
    expect(view.outlierSummary).toContain("Nema podataka");
  });

  it("surfaces unavailable supplier benchmark without fake margin reference", () => {
    const view = presentSupplierMarginBenchmark({
      marginPct: 22,
      systemBenchmarkAvailable: false,
      systemMarginPct: null,
    });

    expect(view.marginLabel).toContain("22");
    expect(view.benchmarkNote).toContain("nije dostupan");
  });

  it("labels legacy reorder summary as potential revenue not procurement cash", () => {
    const view = presentReorderV1Summary({
      criticalCount: 1,
      urgentCount: 0,
      recommendedCount: 1,
      potentialRevenueRsd: 5000,
      estimatedProcurementCostRsd: 2000,
      costCoveragePct: 100,
      reorderValueBasis: "potential_revenue_at_selling_price",
      totalReorderValue: 5000,
    });

    expect(view.potentialRevenueLabel).toContain("5.000");
    expect(view.procurementCostLabel).toContain("2.000");
    expect(view.basisNote).toContain("potencijalni prihod");
  });

  it("keeps unavailable legacy reorder revenue out of fake zero", () => {
    const view = presentReorderV1Summary({
      criticalCount: 0,
      urgentCount: 0,
      recommendedCount: 0,
      potentialRevenueRsd: null,
      estimatedProcurementCostRsd: null,
      costCoveragePct: null,
      reorderValueBasis: "unavailable",
      totalReorderValue: null,
    } as any);

    expect(view.potentialRevenueLabel).toBe("N/D");
    expect(view.procurementCostLabel).toBe("N/D");
    expect(view.basisNote).toBeNull();
  });

  it("keeps nullable profit lift out of fake zero percent", () => {
    expect(presentSignedInsightPercent(null)).toBe("N/D");
    expect(presentSignedInsightPercent(12.5)).toBe("+12,5%");
  });

  it("keeps smart reorder profit unavailable when backend omits it", () => {
    const view = presentSmartReorderProfit({
      criticalCount: 0,
      urgentCount: 0,
      recommendedCount: 1,
      totalReorderCost: 1000,
      expectedRevenueFromReorder: 2000,
      expectedProfitFromReorder: null,
    });

    expect(view.profitLabel).toBe("N/D");
    expect(view.profitNote).toContain("nije pouzdan");
  });

  it("flags derived category revenue as estimated", () => {
    expect(isEstimatedCategoryRevenue({ estimated: true })).toBe(true);
    expect(isEstimatedCategoryRevenue({ revenueBasis: "estimated_velocity_price" })).toBe(true);
    expect(isEstimatedCategoryRevenue({})).toBe(false);
  });
});

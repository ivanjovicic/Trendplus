import { afterEach, describe, expect, it, vi } from "vitest";
import { getSupplierDecisionSummary } from "../supplierDecisionHubApi";

vi.mock("../../utils/apiUrl", () => ({
  apiUrl: (path: string) => path,
}));

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("supplierDecisionHubApi trust metadata mapping", () => {
  it("maps requestedPeriod aliases and normalizes all_history dataset", async () => {
    const payload = {
      from: "2026-05-01T00:00:00Z",
      to: "2026-05-30T23:59:59Z",
      supplierCount: 1,
      fullPriceRevenueShare: 0.6,
      fullPriceSellthrough: 0.45,
      markdownRevenueShare: 0.2,
      preMarkdownMarginPct: 0.3,
      capitalAtRisk: 1000,
      topGrowSuppliers: [],
      topRiskSuppliers: [],
      keyInsights: [],
      trustMetadata: {
        requestedPeriodFrom: "2026-05-01T00:00:00Z",
        requestedPeriodTo: "2026-05-30T23:59:59Z",
        effectiveFrom: "2026-05-01T00:00:00Z",
        effectiveTo: "2026-05-30T23:59:59Z",
        requestedDataset: "all_history",
        effectiveDataset: "all_history",
        effectivePeriodLabel: "Neograniceno",
        dataCoverageStatus: "good",
        usedFallback: false,
        rowCount: 1,
        ignoredRowCount: 0,
        zeroRevenueRowsExcludedCount: 0,
        missingSupplierNameCount: 0,
        hasData: true,
        hasExplicitDateRange: false,
        recommendationAllowed: true,
        noSilentFallback: true,
        windowDays: 0,
        dataScope: "all",
        coverage: "all_history",
      },
      meta: {
        success: true,
      },
    };

    vi.stubGlobal(
      "fetch",
      vi.fn(() =>
        Promise.resolve(
          new Response(JSON.stringify(payload), {
            status: 200,
            headers: { "Content-Type": "application/json" },
          })
        )
      )
    );

    const result = await getSupplierDecisionSummary({
      fromDate: "2026-05-01",
      toDate: "2026-05-30",
    });

    expect(result.trustMetadata).toBeTruthy();
    expect(result.trustMetadata?.requestedFrom).toBe("2026-05-01T00:00:00Z");
    expect(result.trustMetadata?.requestedTo).toBe("2026-05-30T23:59:59Z");
    expect(result.trustMetadata?.requestedDataset).toBe("all_time");
    expect(result.trustMetadata?.effectiveDataset).toBe("all_time");
  });

  it("rejects HTTP 200 payloads with error meta instead of treating zeros as success", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(() =>
        Promise.resolve(
          new Response(JSON.stringify({
            from: "2026-05-01T00:00:00Z",
            to: "2026-05-30T23:59:59Z",
            supplierCount: 0,
            fullPriceRevenueShare: 0,
            fullPriceSellthrough: 0,
            markdownRevenueShare: 0,
            preMarkdownMarginPct: 0,
            capitalAtRisk: 0,
            topGrowSuppliers: [],
            topRiskSuppliers: [],
            keyInsights: [],
            meta: {
              success: false,
              errorCode: "supplier_decision_unavailable",
              errorMessage: "Skorkarta dobavljača trenutno nije dostupna.",
            },
          }), {
            status: 200,
            headers: { "Content-Type": "application/json" },
          })
        )
      )
    );

    await expect(getSupplierDecisionSummary({
      fromDate: "2026-05-01",
      toDate: "2026-05-30",
    })).rejects.toMatchObject({
      name: "SupplierDecisionApiError",
      errorCode: "supplier_decision_unavailable",
    });
  });
});

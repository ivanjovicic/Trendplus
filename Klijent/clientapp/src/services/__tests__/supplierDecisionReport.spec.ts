import { describe, expect, it } from "vitest";
import { buildSupplierDecisionReportPayload } from "../supplierDecisionReport";

function sampleRow(overrides?: Partial<Parameters<typeof buildSupplierDecisionReportPayload>[0]["rows"][number]>) {
  return {
    supplierId: 1,
    supplierName: "Dobavljac A",
    revenue: 120000,
    sharePct: 0.25,
    preMarkdownMarginPct: 0.31,
    marginContribution: 37200,
    status: "increase_focus",
    statusReason: "Stabilan promet i marza.",
    normalizedConfidence: 83,
    confidenceAvailable: true,
    reliabilityPct: 79,
    reliabilityAvailable: true,
    dataQualityStatus: "good",
    reasonCodes: ["high_share"],
    unsoldStockValue: 6200,
    deadStockRate: 0.08,
    ...overrides,
  };
}

describe("buildSupplierDecisionReportPayload", () => {
  it("builds sectioned report rows and metadata", () => {
    const payload = buildSupplierDecisionReportPayload({
      periodLabel: "90d",
      fromDate: "2026-05-01",
      toDate: "2026-07-30",
      supplierLabel: "Svi dobavljaci",
      dataScopeLabel: "all",
      freshnessStatus: "fresh",
      summary: {
        from: "2026-05-01T00:00:00Z",
        to: "2026-07-30T23:59:59Z",
        supplierCount: 2,
        fullPriceRevenueShare: 0.6,
        fullPriceSellthrough: 0.4,
        markdownRevenueShare: 0.2,
        preMarkdownMarginPct: 0.31,
        capitalAtRisk: 21000,
        topGrowSuppliers: [],
        topRiskSuppliers: [],
        keyInsights: [],
      },
      trustMetadata: {
        requestedPeriodFrom: "2026-05-01T00:00:00Z",
        requestedPeriodTo: "2026-07-30T23:59:59Z",
        requestedFrom: "2026-05-01T00:00:00Z",
        requestedTo: "2026-07-30T23:59:59Z",
        effectiveFrom: "2026-05-01T00:00:00Z",
        effectiveTo: "2026-07-30T23:59:59Z",
        requestedDataset: "90d",
        effectiveDataset: "90d",
        effectivePeriodLabel: "Poslednjih 90 dana",
        dataCoverageStatus: "good",
        usedFallback: false,
        lastRefreshAtUtc: "2026-07-31T05:30:00Z",
        rowCount: 2,
        ignoredRowCount: 0,
        zeroRevenueRowsExcludedCount: 0,
        missingSupplierNameCount: 0,
        hasData: true,
        hasExplicitDateRange: true,
        recommendationAllowed: true,
        noSilentFallback: true,
        windowDays: 90,
        dataScope: "all",
        coverage: "window_90d",
      },
      scorecardMeta: {
        success: true,
        dataQualityStatus: "good",
      },
      totalRevenue: 220000,
      totalMarginContribution: 66000,
      top5SharePct: 0.81,
      supplierCounts: {
        boost: 1,
        keep: 1,
        caution: 0,
        reduce: 0,
        insufficient: 0,
      },
      rows: [sampleRow(), sampleRow({ supplierId: 2, supplierName: "Dobavljac B", revenue: 100000, status: "maintain" })],
    });

    expect(payload.tableKey).toBe("supplier-decision-report");
    expect(payload.documentType).toBe("supplier-decision-report");
    expect(payload.rows.some((row) => row.section === "Header" && (row.item === "Dobavljač" || row.item === "Dobavljac"))).toBe(true);
    expect(payload.rows.some((row) => row.section === "KPI" && row.item === "Prihod")).toBe(true);
    expect(payload.rows.some((row) => row.section === "Kvalitet podataka")).toBe(true);
    expect(payload.rows.some((row) => row.section === "supplier_negotiation_pack" && row.item === "Pojačaj saradnju")).toBe(true);
    expect(payload.rows.some((row) => row.section === "Header" && row.item === "Efektivni dataset")).toBe(true);
    expect(payload.metadata.some((item) => item.key === "effectiveDataset" && item.value === "90d")).toBe(true);
    expect(payload.metadata.some((item) => item.key === "dataFreshness" && String(item.value).toLowerCase().includes("sve"))).toBe(true);
  });

  it("adds warning section for insufficient data and fallback", () => {
    const payload = buildSupplierDecisionReportPayload({
      periodLabel: "30d",
      fromDate: "2026-07-01",
      toDate: "2026-07-30",
      supplierLabel: "Dobavljac #12",
      dataScopeLabel: "supplier",
      freshnessStatus: "stale",
      summary: null,
      trustMetadata: {
        requestedPeriodFrom: "2026-07-01T00:00:00Z",
        requestedPeriodTo: "2026-07-30T23:59:59Z",
        requestedFrom: "2026-07-01T00:00:00Z",
        requestedTo: "2026-07-30T23:59:59Z",
        effectiveFrom: "2026-06-01T00:00:00Z",
        effectiveTo: "2026-07-30T23:59:59Z",
        requestedDataset: "30d",
        effectiveDataset: "90d",
        effectivePeriodLabel: "Fallback 90d",
        dataCoverageStatus: "insufficient_data",
        usedFallback: true,
        fallbackReason: "Nedovoljno transakcija u 30d opsegu",
        lastRefreshAtUtc: "2026-07-31T05:30:00Z",
        rowCount: 1,
        ignoredRowCount: 4,
        hasData: true,
        hasExplicitDateRange: true,
        recommendationAllowed: false,
        noSilentFallback: true,
        windowDays: 90,
        dataScope: "supplier",
        coverage: "window_90d",
      },
      scorecardMeta: {
        success: true,
        dataQualityStatus: "insufficient_data",
        isPartial: true,
        warningMessage: "Koriscen fallback signal.",
      },
      totalRevenue: 120000,
      totalMarginContribution: 28000,
      top5SharePct: 1,
      supplierCounts: {
        boost: 0,
        keep: 0,
        caution: 0,
        reduce: 0,
        insufficient: 1,
      },
      rows: [sampleRow({ status: "insufficient_data", statusReason: "Nedovoljno podataka" })],
    });

    const warningRows = payload.rows.filter((row) => row.section === "Upozorenje");
    expect(warningRows.length).toBeGreaterThan(0);
    expect(warningRows.some((row) => String(row.value).toLowerCase().includes("pomo"))).toBe(true);
    expect(warningRows.some((row) => String(row.item).toLowerCase().includes("fallback"))).toBe(true);
    expect(payload.metadata.some((item) => item.key === "usedFallback" && item.value === true)).toBe(true);
  });
});

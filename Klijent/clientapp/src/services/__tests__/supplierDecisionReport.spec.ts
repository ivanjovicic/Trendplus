import { describe, expect, it } from "vitest";
import { buildSupplierDecisionReportPayload, type SupplierDecisionReportBuildInput } from "../supplierDecisionReport";

function buildInput(overrides: Partial<SupplierDecisionReportBuildInput> = {}): SupplierDecisionReportBuildInput {
  return {
    periodLabel: "Poslednjih 30 dana",
    fromDate: "2026-08-01",
    toDate: "2026-08-31",
    supplierLabel: "Svi dobavljači",
    dataScopeLabel: "Svi podaci",
    freshnessStatus: "fresh",
    lastRefreshAtUtc: "2026-09-03T09:00:00Z",
    summary: {
      from: "2026-08-01T00:00:00Z",
      to: "2026-08-31T23:59:59Z",
      supplierCount: 1,
      fullPriceRevenueShare: 0.4,
      fullPriceSellthrough: 0.2,
      markdownRevenueShare: 0.6,
      preMarkdownMarginPct: 0.3,
      capitalAtRisk: 5_000,
      topGrowSuppliers: [],
      topRiskSuppliers: [],
      keyInsights: [],
      dataNote: "Test note",
    } as never,
    trustMetadata: {
      requestedDataset: "30d",
      effectiveDataset: "30d",
      effectivePeriodLabel: "Poslednjih 30 dana",
      recommendationAllowed: false,
      dataCoverageStatus: "insufficient_data",
      usedFallback: false,
      rowCount: 1,
      ignoredRowCount: 0,
      missingSupplierNameCount: 0,
    },
    scorecardMeta: {
      success: true,
      dataQualityStatus: "insufficient_data",
      message: "Nema dovoljno podataka za finalnu preporuku.",
    },
    totalRevenue: 0,
    totalMarginContribution: 0,
    top5SharePct: null,
    supplierCounts: {
      boost: 0,
      keep: 0,
      caution: 0,
      reduce: 0,
      insufficient: 1,
    },
    rows: [
      {
        supplierId: 1,
        supplierName: "Dobavljač 1",
        revenue: 0,
        units: 0,
        sharePct: null,
        preMarkdownMarginPct: 0.3,
        markdownRevenueShare: null,
        marginContribution: 0,
        status: "insufficient_data",
        statusReason: "Nema dovoljno istorije.",
        normalizedConfidence: null,
        confidenceAvailable: false,
        reliabilityPct: null,
        reliabilityAvailable: false,
        dataQualityStatus: "insufficient_data",
        reasonCodes: ["insufficient_history"],
        unsoldStockValue: 0,
        deadStockRate: 0,
      },
    ],
    ...overrides,
  };
}

describe("buildSupplierDecisionReportPayload", () => {
  it("keeps missing percentage metrics as unavailable in report/export payload rows", () => {
    const payload = buildSupplierDecisionReportPayload(buildInput());

    expect(payload.rows.find((row) => row.section === "KPI" && row.item === "Top 5 udeo")?.value).toBe("Nije dostupno");
    expect(payload.rows.find((row) => row.section === "KPI" && row.item === "Sigurnost signala")?.value).toBe("Nije dostupno");
    expect(payload.rows.find((row) => row.section === "KPI" && row.item === "Pouzdanost signala")?.value).toBe("Nije dostupno");
    expect(payload.rows.find((row) => row.section === "supplier_negotiation_pack" && row.item === "Zavisnost od nivelacija")?.value).toBe("Nije dostupno");
    expect(payload.rows.find((row) => row.section === "Header" && row.item === "Posmatrani period")?.value).toContain("Efektivni opseg");
    expect(payload.metadata.find((row) => row.key === "observedPeriodFromUtc")?.value).toBe("2026-08-01T00:00:00Z");
  });

  it("fails closed when report trust metadata is missing", () => {
    const base = buildInput();
    const payload = buildSupplierDecisionReportPayload({
      ...base,
      trustMetadata: { ...base.trustMetadata, recommendationAllowed: undefined },
      scorecardMeta: { success: true, dataQualityStatus: "good" },
      rows: [{
        ...base.rows[0],
        status: "increase_focus",
        normalizedConfidence: 90,
        confidenceAvailable: true,
        reliabilityPct: 80,
        reliabilityAvailable: true,
      }],
    });

    expect(payload.rows.find((row) => row.section === "KPI" && row.item === "Sigurnost signala")?.value).toBe("Nije dostupno");
    expect(payload.rows.find((row) => row.section === "KPI" && row.item === "Pouzdanost signala")?.value).toBe("Nije dostupno");
    expect(payload.rows.find((row) => row.section === "supplier_negotiation_pack" && row.item === "Finalni savet")?.value)
      .toContain("Pomoćni signal");
  });
});

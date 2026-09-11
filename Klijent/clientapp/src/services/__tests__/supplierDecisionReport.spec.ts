import { describe, expect, it } from "vitest";
import {
  buildSupplierDecisionReportPayload,
  buildSupplierDecisionReportSummaryText,
  type SupplierDecisionReportBuildInput,
} from "../supplierDecisionReport";

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

  it("keeps measured zero units and markdown dependency distinct from unavailable evidence", () => {
    const base = buildInput();
    const payload = buildSupplierDecisionReportPayload({
      ...base,
      totalRevenue: 100,
      trustMetadata: { ...base.trustMetadata, recommendationAllowed: true, dataCoverageStatus: "good" },
      scorecardMeta: { success: true, dataQualityStatus: "good" },
      rows: [{ ...base.rows[0], revenue: 100, units: 0, markdownRevenueShare: 0 }],
    });

    expect(payload.rows.find((row) => row.section === "KPI" && row.item === "Prodate jedinice")?.value).toBe("0 kom");
    expect(payload.rows.find((row) => row.section === "KPI" && row.item === "Zavisnost od nivelacija")?.value).toBe("0,0%");
    expect(payload.metadata.find((row) => row.key === "unitsEvidenceState")?.value).toBe("measured_zero");
    expect(payload.metadata.find((row) => row.key === "markdownDependencyEvidenceState")?.value).toBe("measured_zero");
  });

  it("keeps mixed optional numeric coverage unavailable and removes incomplete markdown evidence from actions", () => {
    const base = buildInput();
    const payload = buildSupplierDecisionReportPayload({
      ...base,
      totalRevenue: 100,
      trustMetadata: { ...base.trustMetadata, recommendationAllowed: true, dataCoverageStatus: "good" },
      scorecardMeta: { success: true, dataQualityStatus: "good" },
      supplierCounts: { boost: 0, keep: 0, caution: 0, reduce: 0, insufficient: 0 },
      rows: [
        { ...base.rows[0], supplierId: 1, supplierName: "Dobavljač 1", revenue: 60, units: 12, markdownRevenueShare: 0.8 },
        { ...base.rows[0], supplierId: 2, supplierName: "Dobavljač 2", revenue: 40, units: undefined, markdownRevenueShare: undefined },
      ],
    });

    expect(payload.rows.find((row) => row.section === "KPI" && row.item === "Prodate jedinice")?.value).toBe("Nije dostupno");
    expect(payload.rows.find((row) => row.section === "KPI" && row.item === "Zavisnost od nivelacija")?.value).toBe("Nije dostupno");
    expect(payload.metadata.find((row) => row.key === "unitsEvidenceState")?.value).toBe("partial");
    expect(payload.metadata.find((row) => row.key === "markdownDependencyEvidenceState")?.value).toBe("partial");
    expect(payload.rows.some((row) => row.section === "Upozorenje" && row.item === "Zavisnost od nivelacija" && row.note?.includes("deo redova"))).toBe(true);
    expect(payload.rows.find((row) => row.section === "supplier_negotiation_pack" && row.item === "Traži rabat za robu koja se prodaje samo kroz sniženje")?.value).toBe("Razmotriti");
    expect(buildSupplierDecisionReportSummaryText(payload)).toContain("deo redova");
  });

  it("fails closed for empty and non-finite optional numeric evidence", () => {
    const base = buildInput();
    const emptyPayload = buildSupplierDecisionReportPayload({ ...base, rows: [], totalRevenue: 0 });
    expect(emptyPayload.rows.find((row) => row.section === "KPI" && row.item === "Prodate jedinice")?.value).toBe("Nije dostupno");
    expect(emptyPayload.metadata.find((row) => row.key === "unitsEvidenceState")?.value).toBe("unavailable");
    expect(emptyPayload.metadata.find((row) => row.key === "markdownDependencyEvidenceState")?.value).toBe("unavailable");

    const nonFinitePayload = buildSupplierDecisionReportPayload({
      ...base,
      totalRevenue: 100,
      trustMetadata: { ...base.trustMetadata, recommendationAllowed: true, dataCoverageStatus: "good" },
      scorecardMeta: { success: true, dataQualityStatus: "good" },
      rows: [{ ...base.rows[0], revenue: 100, units: Number.NaN, markdownRevenueShare: Number.POSITIVE_INFINITY }],
    });

    expect(nonFinitePayload.rows.find((row) => row.section === "KPI" && row.item === "Prodate jedinice")?.value).toBe("Nije dostupno");
    expect(nonFinitePayload.rows.find((row) => row.section === "KPI" && row.item === "Zavisnost od nivelacija")?.value).toBe("Nije dostupno");
    expect(nonFinitePayload.metadata.find((row) => row.key === "unitsEvidenceState")?.value).toBe("non_finite");
    expect(nonFinitePayload.metadata.find((row) => row.key === "markdownDependencyEvidenceState")?.value).toBe("non_finite");
    expect(nonFinitePayload.rows.find((row) => row.section === "supplier_negotiation_pack" && row.item === "Traži rabat za robu koja se prodaje samo kroz sniženje")?.value).toBe("Razmotriti");
    expect(buildSupplierDecisionReportSummaryText(nonFinitePayload)).toContain("NaN/Infinity");
  });

  it("does not zero-fill confidence or reliability when availability flags contain incomplete evidence", () => {
    const base = buildInput();
    const payload = buildSupplierDecisionReportPayload({
      ...base,
      totalRevenue: 100,
      trustMetadata: { ...base.trustMetadata, recommendationAllowed: true, dataCoverageStatus: "good" },
      scorecardMeta: { success: true, dataQualityStatus: "good" },
      rows: [
        {
          ...base.rows[0],
          revenue: 60,
          units: 1,
          markdownRevenueShare: 0,
          normalizedConfidence: 80,
          confidenceAvailable: true,
          reliabilityPct: 70,
          reliabilityAvailable: true,
        },
        {
          ...base.rows[0],
          supplierId: 2,
          supplierName: "Dobavljač 2",
          revenue: 40,
          units: 1,
          markdownRevenueShare: 0,
          normalizedConfidence: null,
          confidenceAvailable: true,
          reliabilityPct: Number.NaN,
          reliabilityAvailable: true,
        },
      ],
    });

    expect(payload.rows.find((row) => row.section === "KPI" && row.item === "Sigurnost signala")?.value).toBe("Nije dostupno");
    expect(payload.rows.find((row) => row.section === "KPI" && row.item === "Pouzdanost signala")?.value).toBe("Nije dostupno");
    expect(payload.metadata.find((row) => row.key === "confidenceEvidenceState")?.value).toBe("partial");
    expect(payload.metadata.find((row) => row.key === "reliabilityEvidenceState")?.value).toBe("non_finite");
    expect(payload.rows.find((row) => row.section === "KPI" && row.item === "Sigurnost signala")?.note).toContain("deo redova");
    expect(payload.rows.find((row) => row.section === "KPI" && row.item === "Pouzdanost signala")?.note).toContain("NaN/Infinity");
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

  it("never emits concrete negotiation recommendations when backend actionability is blocked", () => {
    const base = buildInput();
    const payload = buildSupplierDecisionReportPayload({
      ...base,
      trustMetadata: { ...base.trustMetadata, recommendationAllowed: false },
      supplierCounts: { boost: 3, keep: 1, caution: 2, reduce: 2, insufficient: 0 },
      rows: [{
        ...base.rows[0],
        unsoldStockValue: 10_000,
        markdownRevenueShare: 0.8,
      }],
    });

    const negotiationRows = payload.rows.filter((row) => row.section === "supplier_negotiation_pack" && row.secondary === "Predlog razgovora" && row.item !== "Finalni savet");
    expect(negotiationRows).toHaveLength(6);
    expect(negotiationRows.every((row) => row.value !== "Preporučeno")).toBe(true);
    expect(negotiationRows.every((row) => row.value === "Blokirano — proveriti podatke")).toBe(true);
    expect(negotiationRows.every((row) => row.note?.includes("backend nije dozvolio preporuku"))).toBe(true);
    expect(payload.rows.find((row) => row.section === "supplier_negotiation_pack" && row.item === "Finalni savet")?.value)
      .toContain("Pomoćni signal");
  });

  it("keeps concrete negotiation recommendations when backend actionability is allowed", () => {
    const base = buildInput();
    const payload = buildSupplierDecisionReportPayload({
      ...base,
      trustMetadata: { ...base.trustMetadata, recommendationAllowed: true },
      scorecardMeta: { success: true, dataQualityStatus: "good" },
      supplierCounts: { boost: 3, keep: 1, caution: 2, reduce: 2, insufficient: 0 },
      rows: [{ ...base.rows[0], unsoldStockValue: 10_000, markdownRevenueShare: 0.8 }],
    });

    const negotiationRows = payload.rows.filter((row) => row.section === "supplier_negotiation_pack" && row.secondary === "Predlog razgovora" && row.item !== "Finalni savet");
    expect(negotiationRows.some((row) => row.value === "Preporučeno")).toBe(true);
  });
});

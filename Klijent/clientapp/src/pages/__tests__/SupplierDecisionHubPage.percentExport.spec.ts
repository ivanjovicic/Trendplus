import { describe, expect, it } from "vitest";
import { fmtPct } from "../../utils/analyticsFormatters";
import {
  buildAnalyticsDetailSnapshot,
  resolveAnalyticsTablePayload,
} from "../../services/analyticsTableState";
import {
  decisionColumns,
  toSupplierDecisionMarginPercentUnits,
  type DecisionRow,
} from "../SupplierDecisionHubPage";
import type { RankingItem } from "../../services/supplierDecisionHubApi";

function decisionRow(overrides: Partial<DecisionRow> & Pick<DecisionRow, "supplierId" | "supplierName" | "preMarkdownMarginPct">): DecisionRow {
  const base: RankingItem = {
    supplierId: overrides.supplierId,
    supplierName: overrides.supplierName,
    revenue: 100_000,
    units: 50,
    fullPriceRevenueShare: 0.6,
    fullPriceSellthrough: 0.5,
    preMarkdownMarginPct: overrides.preMarkdownMarginPct,
    markdownRevenueShare: 0.2,
    deadStockRate: 0.1,
    unsoldStockValue: 5_000,
    repeatWinnerRate: 0.4,
    mlSupplierScore: 70,
    supplierQualityIndex: 75,
    recommendationCode: "HOLD",
    confidenceScore: 80,
  };

  return {
    ...base,
    sharePct: 25,
    marginContribution: 35_000,
    qualityTrendPct: 12.5,
    status: "maintain",
    statusReason: "Stabilan signal.",
    normalizedConfidence: 80,
    confidenceAvailable: true,
    reliabilityPct: 78,
    reliabilityAvailable: true,
    dataQualityStatus: "good",
    reasonCodes: [],
    ...overrides,
  };
}

describe("Supplier Decision percent export/detail (RQ40)", () => {
  it("converts margin ratio 0.35 to percent units 35", () => {
    expect(toSupplierDecisionMarginPercentUnits(0.35)).toBe(35);
    expect(fmtPct(toSupplierDecisionMarginPercentUnits(0.35), 2)).toBe(fmtPct(35, 2));
    expect(fmtPct(toSupplierDecisionMarginPercentUnits(0.35), 2)).not.toBe(fmtPct(0.35, 2));
  });

  it("export payload uses percent units for preMarkdownMarginPct, not raw ratio", () => {
    const row = decisionRow({
      supplierId: 1,
      supplierName: "Dobavljač A",
      preMarkdownMarginPct: 0.35,
      sharePct: 40,
      qualityTrendPct: 8,
    });

    const payload = resolveAnalyticsTablePayload({
      tableKey: "supplier-decision-hub",
      tableTitle: "Skorkarta",
      columns: decisionColumns,
      rows: [row],
    });

    expect(payload.rows[0].preMarkdownMarginPct).toBe(35);
    expect(payload.rows[0].sharePct).toBe(40);
    expect(payload.rows[0].qualityTrendPct).toBe(8);
    expect(payload.columns.find((c) => c.key === "preMarkdownMarginPct")?.dataType).toBe("percent");
  });

  it("detail snapshot formats margin as 35.00% for ratio 0.35", () => {
    const row = decisionRow({
      supplierId: 2,
      supplierName: "Dobavljač B",
      preMarkdownMarginPct: 0.35,
    });

    const snapshot = buildAnalyticsDetailSnapshot({
      table: "supplier-decision-hub",
      recordId: "2",
      title: row.supplierName,
      columns: decisionColumns,
      row,
    });

    const marginField = snapshot.fields.find((field) => field.key === "preMarkdownMarginPct");
    expect(marginField?.value).toBe(fmtPct(35, 2));
    expect(marginField?.value).not.toBe("0.35");
    expect(marginField?.value).not.toBe(fmtPct(0.35, 2));
    expect(marginField?.dataType).toBe("percent");
  });
});

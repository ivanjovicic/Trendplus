import { describe, expect, it } from "vitest";
import { fmtSignedPct, fmtSignedPctPoints } from "../analyticsFormatters";
import {
  SUPPLIER_ASSORTMENT_POST_WINDOW_REVENUE_LABEL,
  SUPPLIER_OVERVIEW_TOTAL_REVENUE_LABEL,
  SUPPLIER_SCORECARD_COHORT_REVENUE_LABEL,
} from "../supplierMetricSemantics";
import {
  calculateSupplierFullPriceShareDeltaPctPoints,
  calculateSupplierQualityTrendPct,
} from "../../pages/SupplierDecisionHubPage";

describe("supplier cross-tab metric semantics (RQ498)", () => {
  it("keeps distinct revenue labels per Supplier tab basis", () => {
    const labels = [
      SUPPLIER_OVERVIEW_TOTAL_REVENUE_LABEL,
      SUPPLIER_SCORECARD_COHORT_REVENUE_LABEL,
      SUPPLIER_ASSORTMENT_POST_WINDOW_REVENUE_LABEL,
    ];
    expect(new Set(labels).size).toBe(3);
  });

  it("renders composition gap as percentage points, not a temporal trend percent", () => {
    expect(calculateSupplierQualityTrendPct(0.62, 0.24)).toBe(38);
    expect(fmtSignedPctPoints(38, 1)).toBe("+38,0 pp");
    expect(fmtSignedPct(38, 1)).toBe("+38,0%");
  });

  it("renders full-price share delta as pp, not relative growth", () => {
    expect(calculateSupplierFullPriceShareDeltaPctPoints(0.62, 0.58)).toBe(4);
    expect(fmtSignedPctPoints(4, 1)).toBe("+4,0 pp");
    expect(fmtSignedPct(4, 1)).toBe("+4,0%");
  });
});

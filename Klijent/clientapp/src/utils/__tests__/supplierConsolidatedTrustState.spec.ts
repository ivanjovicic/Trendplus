import { describe, expect, it } from "vitest";
import {
  SUPPLIER_CONSOLIDATED_DATA_QUALITY_LABELS,
  SUPPLIER_TAB_FALLBACK_DATA_SOURCE,
  normalizeSupplierChildDataQualityStatus,
  resolveSupplierConsolidatedContextToneClass,
  resolveSupplierConsolidatedTrustMode,
  resolveSupplierConsolidatedTrustStatusLabel,
  resolveSupplierTabFallbackDataSource,
} from "../supplierConsolidatedTrustState";

describe("supplierConsolidatedTrustState (RQ499)", () => {
  it("maps child error status to critical trust severity", () => {
    expect(normalizeSupplierChildDataQualityStatus("error")).toBe("critical");
    expect(normalizeSupplierChildDataQualityStatus("critical")).toBe("critical");
    expect(resolveSupplierConsolidatedTrustStatusLabel({
      dataQualityStatus: "critical",
    })).toBe(SUPPLIER_CONSOLIDATED_DATA_QUALITY_LABELS.critical);
    expect(resolveSupplierConsolidatedContextToneClass({
      dataQualityStatus: "critical",
      recommendationAllowed: true,
    })).toBe("critical");
  });

  it("uses tab-specific fallback data sources", () => {
    expect(resolveSupplierTabFallbackDataSource("overview")).toBe(SUPPLIER_TAB_FALLBACK_DATA_SOURCE.overview);
    expect(resolveSupplierTabFallbackDataSource("scorecard")).toBe(SUPPLIER_TAB_FALLBACK_DATA_SOURCE.scorecard);
    expect(resolveSupplierTabFallbackDataSource("assortment")).toBe(SUPPLIER_TAB_FALLBACK_DATA_SOURCE.assortment);
    expect(new Set(Object.values(SUPPLIER_TAB_FALLBACK_DATA_SOURCE)).size).toBe(3);
  });

  it("keeps scorecard and assortment in signal mode even when recommendation is allowed", () => {
    expect(resolveSupplierConsolidatedTrustMode("overview")).toBe("recommendation");
    expect(resolveSupplierConsolidatedTrustMode("scorecard")).toBe("signal");
    expect(resolveSupplierConsolidatedTrustMode("assortment")).toBe("signal");
  });

  it("uses neutral pending labels instead of unknown quality", () => {
    expect(resolveSupplierConsolidatedTrustStatusLabel(null)).toMatch(/Učitavanje pouzdanosti/i);
    expect(resolveSupplierConsolidatedContextToneClass(null)).toBe("neutral");
  });
});

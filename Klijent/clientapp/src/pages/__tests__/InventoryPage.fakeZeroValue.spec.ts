import { describe, expect, it } from "vitest";
import {
  buildInventoryRow,
  buildInventoryScreenCsvLines,
  buildSupplierChart,
  formatCurrency,
} from "../../components/inventory/inventoryUtils";
import type { InventoryListItem } from "../../types/analytics";

function makeItem(overrides: Partial<InventoryListItem> = {}): InventoryListItem {
  return {
    id: 42,
    naziv: "Model bez cene",
    plu: "SKU-42",
    kolicina: 12,
    minimalnaKolicina: 3,
    nabavnaCena: null,
    estimatedValue: null,
    idObjekat: 1,
    idDobavljac: 7,
    ...overrides,
  };
}

describe("inventory fake-zero value guardrail", () => {
  it("keeps value unknown when quantity > 0 and cost/estimate are missing", () => {
    const row = buildInventoryRow(makeItem(), [], []);

    expect(row.quantity).toBe(12);
    expect(row.unitCost).toBeNull();
    expect(row.estimatedValueAmount).toBeNull();
    expect(formatCurrency(row.estimatedValueAmount)).toBe("Nije dostupno");
  });

  it("uses backend estimatedValue when present even without unit cost", () => {
    const row = buildInventoryRow(makeItem({ estimatedValue: 15000, nabavnaCena: null }), [], []);

    expect(row.unitCost).toBeNull();
    expect(row.estimatedValueAmount).toBe(15000);
  });

  it("computes value from unit cost when estimate is missing", () => {
    const row = buildInventoryRow(makeItem({ nabavnaCena: 250, estimatedValue: null }), [], []);

    expect(row.unitCost).toBe(250);
    expect(row.estimatedValueAmount).toBe(3000);
  });

  it("treats zero quantity without cost as true zero capital", () => {
    const row = buildInventoryRow(makeItem({ kolicina: 0, nabavnaCena: null, estimatedValue: null }), [], []);

    expect(row.estimatedValueAmount).toBe(0);
  });

  it("keeps null quantity unavailable instead of measured OOS zero", () => {
    const row = buildInventoryRow(makeItem({ kolicina: null, minimalnaKolicina: 3 }), [], []);

    expect(row.quantity).toBeNull();
    expect(row.stockState).toBe("unknown");
    expect(row.stockStateLabel).toBe("Nepoznata zaliha");
    expect(row.estimatedValueAmount).toBeNull();
    expect(row.recommendationAllowed).toBe(false);
    expect(row.dataQualityStatus).toBe("insufficient_data");
  });

  it("does not label positive stock as stable when minimum is missing", () => {
    const row = buildInventoryRow(makeItem({ kolicina: 12, minimalnaKolicina: null }), [], []);

    expect(row.minimum).toBeNull();
    expect(row.stockState).toBe("unknown");
    expect(row.stockStateLabel).toBe("Bez praga");
    expect(row.coverageRatio).toBeNull();
    expect(row.recommendationAllowed).toBe(false);
  });

  it("preserves measured zero quantity as out-of-stock", () => {
    const row = buildInventoryRow(makeItem({ kolicina: 0, minimalnaKolicina: 3 }), [], []);

    expect(row.quantity).toBe(0);
    expect(row.stockState).toBe("critical");
    expect(row.stockStateLabel).toBe("Bez zaliha");
  });

  it("rejects non-finite quantity as unknown", () => {
    const row = buildInventoryRow(makeItem({ kolicina: Number.NaN, minimalnaKolicina: 3 }), [], []);

    expect(row.quantity).toBeNull();
    expect(row.stockState).toBe("unknown");
  });

  it("excludes unknown values from supplier chart totals and screen CSV", () => {
    const known = buildInventoryRow(makeItem({ id: 1, nabavnaCena: 100, estimatedValue: null, idDobavljac: 1 }), [], [
      { supplierId: 1, supplierName: "Known" },
    ]);
    const unknown = buildInventoryRow(makeItem({ id: 2, nabavnaCena: null, estimatedValue: null, idDobavljac: 2 }), [], [
      { supplierId: 2, supplierName: "Unknown" },
    ]);

    const chart = buildSupplierChart([known, unknown]);
    expect(chart).toEqual([{ supplierName: "Known", totalValue: 1200 }]);

    const lines = buildInventoryScreenCsvLines([unknown, known]);
    expect(lines[1].endsWith(";0;;")).toBe(true);
    expect(lines[1]).not.toContain("0.00");
    expect(lines[2]).toContain("100.00");
    expect(lines[2]).toContain("1200.00");
  });
});

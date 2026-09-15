import { describe, expect, it } from "vitest";
import {
  resolveSupplierFilterFallbackState,
  SUPPLIER_FILTER_STALE_LIST_MESSAGE,
} from "../supplierFilterFallbackState";

describe("supplierFilterFallbackState", () => {
  it("returns fresh suppliers when no fallback metadata is present", () => {
    const result = resolveSupplierFilterFallbackState([
      { supplierId: 1, supplierName: "Dobavljač A" },
    ]);

    expect(result).toEqual({
      warning: null,
      isStale: false,
      shouldClearSelection: false,
      suppliers: [{ supplierId: 1, supplierName: "Dobavljač A" }],
    });
  });

  it("keeps the previous supplier list marked stale when fallback metadata is present", () => {
    const previousSuppliers = [{ supplierId: 9, supplierName: "Stari dobavljač" }];
    const response = [{ supplierId: 2, supplierName: "Novi dobavljač" }] as typeof previousSuppliers & {
      meta: { success: true; warningMessage: "Filteri dobavljača trenutno koriste pomoćni signal." };
    };
    response.meta = { success: true, warningMessage: "Filteri dobavljača trenutno koriste pomoćni signal." };

    const result = resolveSupplierFilterFallbackState(response, previousSuppliers);

    expect(result.isStale).toBe(true);
    expect(result.shouldClearSelection).toBe(true);
    expect(result.warning).toBe("Filteri dobavljača trenutno koriste pomoćni signal.");
    expect(result.suppliers).toEqual(previousSuppliers);
    expect(SUPPLIER_FILTER_STALE_LIST_MESSAGE).toMatch(/zastarela/i);
  });

  it("accepts an empty fresh list without marking it stale", () => {
    const result = resolveSupplierFilterFallbackState([]);

    expect(result).toEqual({
      warning: null,
      isStale: false,
      shouldClearSelection: false,
      suppliers: [],
    });
  });
});

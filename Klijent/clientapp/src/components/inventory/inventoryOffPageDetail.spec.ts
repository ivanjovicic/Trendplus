import { describe, expect, it } from "vitest";
import { buildOffPageDetailPlaceholderRow } from "./inventoryUtils";

describe("buildOffPageDetailPlaceholderRow", () => {
  it("keeps quantity and value unknown until detail fetch completes", () => {
    const row = buildOffPageDetailPlaceholderRow(9999, [{ storeId: 1, storeName: "Prodavnica 1" }], [], {
      storeId: 1,
      label: "Off-page artikal",
    });

    expect(row.contextStatus).toBe("loadingContext");
    expect(row.quantity).toBeNull();
    expect(row.minimum).toBeNull();
    expect(row.unitCost).toBeNull();
    expect(row.estimatedValueAmount).toBeNull();
    expect(row.stockState).toBe("unknown");
    expect(row.stockStateLabel).toBe("Nepoznata zaliha");
  });
});

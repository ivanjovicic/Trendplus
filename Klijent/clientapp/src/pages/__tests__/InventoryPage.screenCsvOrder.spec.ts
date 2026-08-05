import { describe, expect, it } from "vitest";
import {
  buildInventoryScreenCsvFilename,
  buildInventoryScreenCsvLines,
  isInventoryPageLocalRiskSort,
} from "../../components/inventory/inventoryUtils";

describe("inventory screen CSV order parity", () => {
  it("exports rows in the given (displayed) order for OOS risk sort", () => {
    const displayedRows = [
      {
        plu: "HIGH",
        naziv: "High OOS risk",
        supplierName: "A",
        storeName: "Store 1",
        stockStateLabel: "Niska zaliha",
        quantity: 2,
        minimum: 5,
        reorderGap: 3,
        unitCost: 10,
        estimatedValueAmount: 20,
      },
      {
        plu: "LOW",
        naziv: "Low OOS risk",
        supplierName: "B",
        storeName: "Store 1",
        stockStateLabel: "Stabilno",
        quantity: 50,
        minimum: 5,
        reorderGap: 0,
        unitCost: 8,
        estimatedValueAmount: 400,
      },
    ];

    // Server quantity order would put LOW first; screen export must keep risk order.
    const quantityOrder = [displayedRows[1], displayedRows[0]];
    expect(quantityOrder.map((row) => row.plu)).toEqual(["LOW", "HIGH"]);

    const lines = buildInventoryScreenCsvLines(displayedRows);
    expect(lines[0]).toContain("PLU");
    expect(lines[1].startsWith("HIGH;")).toBe(true);
    expect(lines[2].startsWith("LOW;")).toBe(true);
    expect(lines[1]).toContain("High OOS risk");
    expect(isInventoryPageLocalRiskSort("oosRisk")).toBe(true);
  });

  it("includes risk sort mode in the screen CSV filename", () => {
    expect(buildInventoryScreenCsvFilename(2, "oosRisk")).toBe("bilans-stanja-strana-2-oosRisk.csv");
    expect(buildInventoryScreenCsvFilename(1, "overstockRisk")).toBe("bilans-stanja-strana-1-overstockRisk.csv");
    expect(buildInventoryScreenCsvFilename(1, "kolicina")).toBe("bilans-stanja-strana-1.csv");
  });
});

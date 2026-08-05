import { describe, expect, it } from "vitest";
import {
  inventoryRiskSortScopeWarning,
  isInventoryPageLocalRiskSort,
} from "../../components/inventory/inventoryUtils";

describe("inventory risk sort scope", () => {
  it("treats oosRisk and overstockRisk as page-local sorts", () => {
    expect(isInventoryPageLocalRiskSort("oosRisk")).toBe(true);
    expect(isInventoryPageLocalRiskSort("overstockRisk")).toBe(true);
    expect(isInventoryPageLocalRiskSort("kolicina")).toBe(false);
    expect(isInventoryPageLocalRiskSort("vrednost")).toBe(false);
  });

  it("warns that risk sort can hide higher-risk SKUs on later pages", () => {
    const warning = inventoryRiskSortScopeWarning("oosRisk", {
      pageSize: 50,
      totalPages: 3,
      totalCount: 120,
    });

    expect(warning).toContain("samo za artikle na trenutnoj strani");
    expect(warning).toContain("3 strana");
    expect(warning).toContain("drugim stranama");
    expect(warning).not.toContain("cela filtrirana lista");
  });

  it("still labels page-local scope when the filtered list fits one page", () => {
    const warning = inventoryRiskSortScopeWarning("overstockRisk", {
      pageSize: 50,
      totalPages: 1,
      totalCount: 12,
    });

    expect(warning).toContain("Overstock rizik");
    expect(warning).toContain("samo za artikle na trenutnoj strani");
    expect(warning).toContain("cela filtrirana lista");
  });

  it("returns no warning for global server sorts", () => {
    expect(inventoryRiskSortScopeWarning("kolicina", { pageSize: 50, totalPages: 4, totalCount: 200 })).toBeNull();
  });
});

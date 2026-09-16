import { describe, expect, it } from "vitest";
import { resolveAuthoritativeTopSuppliers } from "../dailySupplierOrder";
import type { DailySalesSupplierHeader } from "../../services/dailySalesStatsApi";

function supplier(
  name: string,
  totalQty: number,
  totalRevenue = totalQty * 100,
): DailySalesSupplierHeader {
  return {
    supplierId: name.length,
    supplierName: name,
    isUnknown: false,
    totalQty,
    totalRevenue,
  };
}

describe("resolveAuthoritativeTopSuppliers", () => {
  it("reorders top suppliers to match topSuppliersOrder", () => {
    const suppliers = [supplier("Bravo", 20), supplier("Alfa", 50)];
    const resolution = resolveAuthoritativeTopSuppliers(suppliers, ["Alfa", "Bravo"]);

    expect(resolution.warning).toBeNull();
    expect(resolution.suppliers.map((item) => item.supplierName)).toEqual(["Alfa", "Bravo"]);
  });

  it("fails closed when topSuppliersOrder is missing", () => {
    const resolution = resolveAuthoritativeTopSuppliers([supplier("Alfa", 10)], []);

    expect(resolution.suppliers).toEqual([]);
    expect(resolution.warning).toContain("topSuppliersOrder");
  });

  it("fails closed for duplicate order names", () => {
    const resolution = resolveAuthoritativeTopSuppliers(
      [supplier("Alfa", 10), supplier("Bravo", 5)],
      ["Alfa", "Alfa"],
    );

    expect(resolution.suppliers).toEqual([]);
    expect(resolution.warning).toContain("duplirana imena");
  });

  it("fails closed when order references an unknown supplier", () => {
    const resolution = resolveAuthoritativeTopSuppliers(
      [supplier("Alfa", 10)],
      ["Alfa", "Nepostojeci"],
    );

    expect(resolution.suppliers).toEqual([]);
    expect(resolution.warning).toContain("Nepostojeci");
  });

  it("fails closed when topSuppliers contains suppliers missing from order", () => {
    const resolution = resolveAuthoritativeTopSuppliers(
      [supplier("Alfa", 10), supplier("Bravo", 5)],
      ["Alfa"],
    );

    expect(resolution.suppliers).toEqual([]);
    expect(resolution.warning).toContain("nije uskladjen");
  });
});

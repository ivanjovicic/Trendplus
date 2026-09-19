import { describe, expect, it } from "vitest";
import { computeInventorySignalKpis } from "./inventorySignalKpis";
import type { InventoryRow } from "./types";

function row(overrides: Partial<InventoryRow> = {}): InventoryRow {
  return {
    id: 1,
    naziv: "Artikal",
    plu: "PLU-1",
    kolicina: 1,
    minimalnaKolicina: 0,
    nabavnaCena: 100,
    estimatedValue: 100,
    idObjekat: 1,
    idDobavljac: null,
    quantity: 1,
    minimum: 0,
    reorderGap: 0,
    stockState: "ok",
    stockStateLabel: "OK",
    estimatedValueAmount: 100,
    unitCost: 100,
    coverageRatio: null,
    stockCoverDays: 5,
    stockCoverStatus: "low_cover",
    stockCoverStatusLabel: "Niska pokrivenost",
    sellThroughRatio: 0.5,
    sellThroughStatus: "good",
    sellThroughStatusLabel: "Dobar",
    signalConfidencePct: 80,
    recommendationAllowed: true,
    signalText: "OK",
    dataQualityStatus: "good",
    reasonCodes: [],
    ...overrides,
  };
}

describe("computeInventorySignalKpis", () => {
  it("marks KPI scope as filter-wide when the filtered set fits on one page", () => {
    const snapshot = computeInventorySignalKpis([row(), row({ id: 2, stockCoverStatus: "slow_stock", sellThroughStatus: "slow" })], 2, 50);
    expect(snapshot.scope).toBe("filter");
    expect(snapshot.stockCoverRiskCount).toBe(1);
    expect(snapshot.slowStockSkus).toBe(1);
    expect(snapshot.goodSellThroughSkus).toBe(1);
  });

  it("marks KPI scope as page-local when the filtered set spans multiple pages", () => {
    const snapshot = computeInventorySignalKpis([row()], 120, 50);
    expect(snapshot.scope).toBe("page");
  });
});

import { describe, expect, it } from "vitest";
import { buildInventorySignalActionSpec } from "../InventoryPage";
import type { InventoryRow } from "../../components/inventory/types";

function makeRow(overrides: Partial<InventoryRow> = {}): InventoryRow {
  return {
    id: 11,
    naziv: "Model A",
    plu: "SKU-11",
    kolicina: 10,
    minimalnaKolicina: 5,
    nabavnaCena: 1000,
    estimatedValue: 10000,
    idObjekat: 1,
    idDobavljac: 5,
    supplierName: "Dobavljac A",
    storeName: "Prodavnica 1",
    quantity: 10,
    minimum: 5,
    reorderGap: 0,
    stockState: "healthy",
    stockStateLabel: "Stabilno",
    estimatedValueAmount: 10000,
    unitCost: 1000,
    coverageRatio: 2,
    stockCoverDays: 9,
    stockCoverStatus: "healthy",
    stockCoverStatusLabel: "Zdrava pokrivenost",
    sellThroughRatio: 0.45,
    sellThroughStatus: "warning",
    sellThroughStatusLabel: "Sell-through upozorenje",
    signalConfidencePct: 76,
    recommendationAllowed: true,
    signalText: "Prati signal",
    dataQualityStatus: "good",
    reasonCodes: [],
    ...overrides,
  };
}

describe("Inventory signal action mapping", () => {
  it("maps low_cover and out_of_stock_risk to REPLENISH", () => {
    const low = buildInventorySignalActionSpec(makeRow({ stockCoverStatus: "low_cover" }));
    const oos = buildInventorySignalActionSpec(makeRow({ stockCoverStatus: "out_of_stock_risk" }));

    expect(low.recommendationStatus).toBe("REPLENISH");
    expect(oos.recommendationStatus).toBe("REPLENISH");
    expect(new Date(low.dueAtUtc).getTime()).toBeGreaterThan(Date.now());
    expect(new Date(oos.dueAtUtc).getTime()).toBeGreaterThan(Date.now());
  });

  it("maps slow_stock/no_velocity to SLOW_STOCK_REVIEW", () => {
    const slow = buildInventorySignalActionSpec(makeRow({ stockCoverStatus: "slow_stock" }));
    const noVelocity = buildInventorySignalActionSpec(makeRow({ stockCoverStatus: "no_velocity" }));

    expect(slow.recommendationStatus).toBe("SLOW_STOCK_REVIEW");
    expect(noVelocity.recommendationStatus).toBe("SLOW_STOCK_REVIEW");
  });

  it("maps insufficient_data to SIGNAL_REVIEW", () => {
    const spec = buildInventorySignalActionSpec(
      makeRow({
        stockCoverStatus: "insufficient_data",
        stockCoverDays: null,
        sellThroughRatio: null,
      })
    );

    expect(spec.recommendationStatus).toBe("SIGNAL_REVIEW");
  });
});

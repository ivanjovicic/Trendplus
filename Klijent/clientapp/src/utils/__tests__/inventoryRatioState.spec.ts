import { describe, expect, it } from "vitest";
import { projectInventoryRatios } from "../inventoryRatioState";

describe("projectInventoryRatios", () => {
  it.each([
    [{ totalSkuCount: 100, outOfStockCount: 5, lowStockCount: 10 }, { availablePct: 95, redZonePct: 10 }],
    [{ totalSkuCount: 100, outOfStockCount: 0, lowStockCount: 0 }, { availablePct: 100, redZonePct: 0 }],
    [{ totalSkuCount: 100, outOfStockCount: null, lowStockCount: 10 }, { availablePct: null, redZonePct: 10 }],
    [{ totalSkuCount: null, outOfStockCount: 5, lowStockCount: 10 }, { availablePct: null, redZonePct: null }],
    [{ totalSkuCount: 100, outOfStockCount: Number.NaN, lowStockCount: Number.POSITIVE_INFINITY }, { availablePct: null, redZonePct: null }],
  ])("keeps only finite, semantically compatible ratios for %j", (input, expected) => {
    expect(projectInventoryRatios(input)).toEqual(expected);
  });

  it.each([
    [{ totalSkuCount: 0, outOfStockCount: 0, lowStockCount: 0 }, { availablePct: null, redZonePct: null }],
    [{ totalSkuCount: 100, outOfStockCount: -1, lowStockCount: 10 }, { availablePct: null, redZonePct: 10 }],
    [{ totalSkuCount: 100, outOfStockCount: 5, lowStockCount: 101 }, { availablePct: 95, redZonePct: null }],
  ])("does not infer a ratio from an invalid denominator or count: %j", (input, expected) => {
    expect(projectInventoryRatios(input)).toEqual(expected);
  });
});

import { describe, expect, it } from "vitest";
import {
  buildInventoryWorkflowCentralQueueMetadata,
  INVENTORY_EXPOSURE_BASIS,
  INVENTORY_SUGGESTED_ACTION_COST_BASIS,
  resolveInventoryExposureRsd,
  resolveInventoryExposureRsdFromRow,
} from "../inventoryUtils";
import type { InventoryRow } from "../types";

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

describe("inventory impact semantics", () => {
  it("keeps exposure separate from missing-cost and true-zero states", () => {
    expect(resolveInventoryExposureRsd(10000, false)).toBe(10000);
    expect(resolveInventoryExposureRsd(0, false)).toBe(0);
    expect(resolveInventoryExposureRsd(10000, true)).toBeNull();
    expect(resolveInventoryExposureRsd(null, false)).toBeNull();
    expect(resolveInventoryExposureRsd(Number.NaN, false)).toBeNull();
  });

  it("projects row exposure without treating it as expected impact", () => {
    expect(resolveInventoryExposureRsdFromRow(makeRow())).toBe(10000);
    expect(resolveInventoryExposureRsdFromRow(makeRow({ estimatedValueAmount: null, estimatedValue: null, unitCost: 500, quantity: 4 }))).toBe(2000);
    expect(resolveInventoryExposureRsdFromRow(makeRow({ estimatedValueAmount: null, estimatedValue: null, unitCost: null, quantity: 10 }))).toBeNull();
  });

  it("serializes workflow queue metadata with exposure only", () => {
    const metadata = buildInventoryWorkflowCentralQueueMetadata({
      suggestionKey: "dopuna-1",
      actionType: "markdown",
      priority: "high",
      label: "Markdown predlog",
      reason: "Spora zaliha",
      status: "pending",
      artikalId: 501,
      naziv: "Artikal A",
      fromStoreName: "Prodavnica 1",
      toStoreName: null,
      suggestedQty: 3,
      estimatedValue: 25000,
      costMissing: false,
      daysSinceMovement: 90,
    });

    expect(metadata.inventoryExposureRsd).toBe(25000);
    expect(metadata.inventoryExposureBasis).toBe(INVENTORY_EXPOSURE_BASIS);
    expect(metadata).not.toHaveProperty("impactEstimateRsd");
    expect(metadata).not.toHaveProperty("expectedImpactRsd");
  });

  it("keeps forecast action cost separate from current-stock exposure", () => {
    const metadata = buildInventoryWorkflowCentralQueueMetadata({
      suggestionKey: "forecast-1",
      actionType: "dopuna",
      priority: "high",
      label: "Predložena dopuna",
      reason: "Forecast signal",
      status: "pending",
      artikalId: 501,
      naziv: "Artikal A",
      fromStoreName: null,
      toStoreName: "Prodavnica 1",
      suggestedQty: 2,
      forecastDemandQty: 2,
      estimatedValue: 1000,
      estimatedValueBasis: "suggested_action_cost",
      costMissing: false,
      daysSinceMovement: 0,
    });

    expect(metadata.suggestedActionCostRsd).toBe(1000);
    expect(metadata.suggestedActionCostBasis).toBe(INVENTORY_SUGGESTED_ACTION_COST_BASIS);
    expect(metadata).not.toHaveProperty("inventoryExposureRsd");
    expect(metadata).not.toHaveProperty("expectedImpactRsd");
  });

  it("keeps missing forecast action cost unavailable", () => {
    const metadata = buildInventoryWorkflowCentralQueueMetadata({
      suggestionKey: "forecast-1",
      actionType: "dopuna",
      priority: "high",
      label: "Predložena dopuna",
      reason: "Forecast signal",
      status: "pending",
      artikalId: 501,
      naziv: "Artikal A",
      fromStoreName: null,
      toStoreName: "Prodavnica 1",
      suggestedQty: 2,
      forecastDemandQty: 2,
      estimatedValue: null,
      estimatedValueBasis: "suggested_action_cost",
      costMissing: true,
      daysSinceMovement: 0,
    });

    expect(metadata.suggestedActionCostRsd).toBeNull();
    expect(metadata).not.toHaveProperty("inventoryExposureRsd");
    expect(metadata.costMissing).toBe(true);
  });
});

import { describe, expect, it } from "vitest";
import type { ForecastRowDto } from "../../../types/analytics";
import { aggregateInventoryForecastRiskForRow } from "../inventoryUtils";

function forecastRow(partial: Partial<ForecastRowDto> & Pick<ForecastRowDto, "skuId" | "storeId" | "sizeCode">): ForecastRowDto {
  return {
    forecast7d: null,
    forecast14d: null,
    forecast28d: null,
    probabilityOfOOSIn7d: null,
    overstockRisk: null,
    confidenceScore: null,
    explanation: "test",
    ...partial,
  };
}

describe("aggregateInventoryForecastRiskForRow", () => {
  it("uses max risk across sizes for the same sku and store", () => {
    const items = [
      forecastRow({ skuId: 10, storeId: 1, sizeCode: "40", probabilityOfOOSIn7d: 0.2, overstockRisk: 0.1 }),
      forecastRow({ skuId: 10, storeId: 1, sizeCode: "41", probabilityOfOOSIn7d: 0.55, overstockRisk: 0.3 }),
    ];

    const aggregate = aggregateInventoryForecastRiskForRow({ id: 10, idObjekat: 1 }, items);

    expect(aggregate.oosRisk).toBe(0.55);
    expect(aggregate.overstockRisk).toBe(0.3);
    expect(aggregate.basis).toBe("max-across-sizes");
    expect(aggregate.matchedSizeCount).toBe(2);
  });

  it("does not merge forecast rows from another store when the list row is store-specific", () => {
    const items = [
      forecastRow({ skuId: 10, storeId: 1, sizeCode: "40", probabilityOfOOSIn7d: 0.9, overstockRisk: 0.8 }),
      forecastRow({ skuId: 10, storeId: 2, sizeCode: "40", probabilityOfOOSIn7d: 0.1, overstockRisk: 0.05 }),
    ];

    const aggregate = aggregateInventoryForecastRiskForRow({ id: 10, idObjekat: 2 }, items);

    expect(aggregate.oosRisk).toBe(0.1);
    expect(aggregate.overstockRisk).toBe(0.05);
  });

  it("honors the selected store filter for all-location inventory rows", () => {
    const items = [
      forecastRow({ skuId: 10, storeId: 1, sizeCode: "40", probabilityOfOOSIn7d: 0.9, overstockRisk: 0.7 }),
      forecastRow({ skuId: 10, storeId: 2, sizeCode: "40", probabilityOfOOSIn7d: 0.2, overstockRisk: 0.1 }),
    ];

    const aggregate = aggregateInventoryForecastRiskForRow(
      { id: 10, idObjekat: null },
      items,
      { selectedStoreId: 2 },
    );

    expect(aggregate.oosRisk).toBe(0.2);
    expect(aggregate.basis).toBe("max-across-sizes");
  });

  it("refuses cross-store max aggregation for all-location rows without a store filter", () => {
    const items = [
      forecastRow({ skuId: 10, storeId: 1, sizeCode: "40", probabilityOfOOSIn7d: 0.9, overstockRisk: 0.7 }),
      forecastRow({ skuId: 10, storeId: 2, sizeCode: "41", probabilityOfOOSIn7d: 0.2, overstockRisk: 0.1 }),
    ];

    const aggregate = aggregateInventoryForecastRiskForRow({ id: 10, idObjekat: null }, items);

    expect(aggregate.oosRisk).toBeNull();
    expect(aggregate.overstockRisk).toBeNull();
    expect(aggregate.basis).toBe("unavailable-multi-store");
  });

  it("keeps valid zero distinct from missing risk values", () => {
    const items = [
      forecastRow({ skuId: 10, storeId: 1, sizeCode: "40", probabilityOfOOSIn7d: 0, overstockRisk: 0 }),
    ];

    const aggregate = aggregateInventoryForecastRiskForRow({ id: 10, idObjekat: 1 }, items);

    expect(aggregate.oosRisk).toBe(0);
    expect(aggregate.overstockRisk).toBe(0);
  });

  it("marks partial forecast evidence when any matched size lacks a risk value", () => {
    const items = [
      forecastRow({ skuId: 10, storeId: 1, sizeCode: "40", probabilityOfOOSIn7d: 0.4, overstockRisk: null }),
      forecastRow({ skuId: 10, storeId: 1, sizeCode: "41", probabilityOfOOSIn7d: 0.2, overstockRisk: 0.1 }),
    ];

    const aggregate = aggregateInventoryForecastRiskForRow({ id: 10, idObjekat: 1 }, items);

    expect(aggregate.oosRisk).toBe(0.4);
    expect(aggregate.overstockRisk).toBe(0.1);
    expect(aggregate.basis).toBe("partial-missing-risk");
  });
});

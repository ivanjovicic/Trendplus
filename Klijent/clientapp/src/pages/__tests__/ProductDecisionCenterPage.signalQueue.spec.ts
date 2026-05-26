import { describe, expect, it } from "vitest";
import { buildProductQueueSpec } from "../ProductDecisionCenterPage";
import type { ProductDecisionCenterItem } from "../../types/analytics";

function makeRow(overrides: Partial<ProductDecisionCenterItem> = {}): ProductDecisionCenterItem {
  return {
    productId: 101,
    sku: "SKU-101",
    productName: "Model X",
    supplierId: 10,
    supplierName: "Dobavljac A",
    revenue: 100000,
    unitsSold: 40,
    velocityUnitsPerDay: 1.2,
    marginContribution: 24000,
    marginPct: 24,
    marginQualityLabel: "Dobro",
    marginCoveragePct: 92,
    currentStock: 10,
    minStock: 5,
    stockGap: 0,
    trendPct: 3,
    lostSalesEstimate: 15000,
    slowStockCapital: 0,
    stockCoverDays: 5,
    stockCoverStatus: "healthy",
    stockCoverStatusLabel: "Zdrava pokrivenost",
    sellThroughRatio: 0.6,
    sellThroughStatus: "good",
    sellThroughStatusLabel: "Dobar sell-through",
    signalConfidencePct: 82,
    recommendationAllowed: true,
    dataQualityStatus: "good",
    confidencePct: 88,
    reliabilityPct: 80,
    recommendationStatus: "WATCH",
    recommendationLabel: "Prati",
    recommendationReason: "Stabilan signal.",
    reasonCodes: [],
    recommendedAction: "Nastavi pracenje.",
    ...overrides,
  };
}

describe("Product decision signal queue mapping", () => {
  it("maps low_cover/out_of_stock_risk to REPLENISH", () => {
    const low = buildProductQueueSpec(makeRow({ stockCoverStatus: "low_cover" }));
    const oos = buildProductQueueSpec(makeRow({ stockCoverStatus: "out_of_stock_risk" }));

    expect(low.recommendationStatus).toBe("REPLENISH");
    expect(oos.recommendationStatus).toBe("REPLENISH");
    expect(new Date(low.dueAtUtc).getTime()).toBeGreaterThan(Date.now());
    expect(new Date(oos.dueAtUtc).getTime()).toBeGreaterThan(Date.now());
  });

  it("maps slow_stock/no_velocity to SLOW_STOCK_REVIEW", () => {
    const slow = buildProductQueueSpec(makeRow({ stockCoverStatus: "slow_stock" }));
    const noVelocity = buildProductQueueSpec(makeRow({ stockCoverStatus: "no_velocity" }));

    expect(slow.recommendationStatus).toBe("SLOW_STOCK_REVIEW");
    expect(noVelocity.recommendationStatus).toBe("SLOW_STOCK_REVIEW");
  });

  it("maps insufficient_data or recommendationAllowed=false to SIGNAL_REVIEW", () => {
    const insufficient = buildProductQueueSpec(makeRow({ stockCoverStatus: "insufficient_data" }));
    const notAllowed = buildProductQueueSpec(makeRow({ recommendationAllowed: false }));

    expect(insufficient.recommendationStatus).toBe("SIGNAL_REVIEW");
    expect(notAllowed.recommendationStatus).toBe("SIGNAL_REVIEW");
  });
});

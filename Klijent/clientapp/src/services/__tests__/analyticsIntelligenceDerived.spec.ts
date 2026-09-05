import { describe, expect, it } from "vitest";
import { fmtPct } from "../../utils/analyticsFormatters";
import {
  buildAgingResultFromSignals,
  buildCategoryIntelligenceFromSignals,
  buildDepletionResultFromSignals,
  buildPriceSensitivityFromSignals,
  buildSmartReorderFromSignals,
  mergeCategorySignalsAsPrimary,
} from "../analyticsIntelligenceDerived";
import type {
  DemandSignalItem,
  InventoryRiskSignalItem,
  PriceIntelligenceItem,
} from "../analyticsIntelligenceApi";
import type { CategoryIntelligence } from "../insightStudioApi";

function priceItem(
  overrides: Partial<PriceIntelligenceItem> & Pick<PriceIntelligenceItem, "articleId" | "category" | "netPrice">,
): PriceIntelligenceItem {
  return {
    sku: `SKU-${overrides.articleId}`,
    productName: `Artikal ${overrides.articleId}`,
    brandKey: "brand",
    supplierId: 1,
    supplierName: "Dobavljac",
    priceDate: "2026-08-01",
    listPrice: overrides.netPrice,
    cost: overrides.netPrice * 0.5,
    priceIndexVsCategory: null,
    priceIndexVsBrand: null,
    discountDepth: 0,
    marginPct: 0.4,
    ...overrides,
  };
}

function inventoryItem(
  overrides: Partial<InventoryRiskSignalItem> & Pick<InventoryRiskSignalItem, "articleId" | "category" | "avgDailySales30d">,
): InventoryRiskSignalItem {
  return {
    sku: `SKU-${overrides.articleId}`,
    productName: `Artikal ${overrides.articleId}`,
    supplierId: 1,
    supplierName: "Dobavljac",
    date: "2026-08-01",
    stockQty: 10,
    daysOfCover: 20,
    stockTurn: 1,
    stockoutDays: 0,
    lowStockDays: 0,
    deadStockRisk: 0,
    ...overrides,
  };
}

describe("buildCategoryIntelligenceFromSignals (RQ39)", () => {
  it("emits revShare in percent units for a 25%/75% split", () => {
    // Category A: 1 unit/day * 30 * 100 = 3000 revenue → 25%
    // Category B: 3 units/day * 30 * 100 = 9000 revenue → 75%
    const result = buildCategoryIntelligenceFromSignals(
      [
        priceItem({ articleId: 1, category: "A", netPrice: 100 }),
        priceItem({ articleId: 2, category: "B", netPrice: 100 }),
      ],
      [
        inventoryItem({ articleId: 1, category: "A", avgDailySales30d: 1 }),
        inventoryItem({ articleId: 2, category: "B", avgDailySales30d: 3 }),
      ],
      [],
    );

    const a = result.byCategory.find((row) => row.kategorija === "A");
    const b = result.byCategory.find((row) => row.kategorija === "B");

    expect(a?.revShare).toBe(25);
    expect(b?.revShare).toBe(75);
    expect(fmtPct(a?.revShare)).toBe(fmtPct(25));
    expect(fmtPct(b?.revShare)).toBe(fmtPct(75));
    // Must not look like a ratio formatted as percent (0.25% / 0.75%).
    expect(fmtPct(a?.revShare)).not.toBe(fmtPct(0.25));
    expect(a?.revShare).toBeGreaterThan(1);
  });

  it("matches legacy percent-shaped CategoryStat for the same shares", () => {
    const derived = buildCategoryIntelligenceFromSignals(
      [
        priceItem({ articleId: 1, category: "A", netPrice: 100 }),
        priceItem({ articleId: 2, category: "B", netPrice: 100 }),
      ],
      [
        inventoryItem({ articleId: 1, category: "A", avgDailySales30d: 1 }),
        inventoryItem({ articleId: 2, category: "B", avgDailySales30d: 3 }),
      ],
      [],
    );

    const legacy: CategoryIntelligence = {
      byCategory: [
        {
          kategorija: "A",
          totalRevenue: 3000,
          totalUnits: 30,
          marginPct: 40,
          profitLift: 40,
          revShare: 25,
          velocity: 1,
          uniqueSKU: 1,
        },
        {
          kategorija: "B",
          totalRevenue: 9000,
          totalUnits: 90,
          marginPct: 40,
          profitLift: 40,
          revShare: 75,
          velocity: 3,
          uniqueSKU: 1,
        },
      ],
      byGender: [],
    };

    expect(derived.byCategory.find((r) => r.kategorija === "A")?.revShare).toBe(
      legacy.byCategory.find((r) => r.kategorija === "A")?.revShare,
    );
    expect(derived.byCategory.find((r) => r.kategorija === "B")?.revShare).toBe(
      legacy.byCategory.find((r) => r.kategorija === "B")?.revShare,
    );
  });

  it("returns unknown revShare when the revenue denominator is zero", () => {
    const result = buildCategoryIntelligenceFromSignals(
      [priceItem({ articleId: 1, category: "Empty", netPrice: 100 })],
      [inventoryItem({ articleId: 1, category: "Empty", avgDailySales30d: 0 })],
      [],
    );

    expect(result.byCategory).toHaveLength(1);
    expect(result.byCategory[0].revShare).toBeNull();
    expect(result.byCategory[0].totalRevenue).toBe(0);
  });

  it("does not promote frontend-derived signals over the backend result", () => {
    const legacy: CategoryIntelligence = {
      byCategory: [
        {
          kategorija: "LegacyOnly",
          totalRevenue: 1,
          totalUnits: 1,
          marginPct: 0,
          profitLift: 0,
          revShare: 100,
          velocity: 0,
          uniqueSKU: 1,
        },
      ],
      byGender: [{ pol: "M", totalRevenue: 1000, totalUnits: 10, revShare: 100 }],
    };

    const merged = mergeCategorySignalsAsPrimary(
      legacy,
      [
        priceItem({ articleId: 1, category: "A", netPrice: 100 }),
        priceItem({ articleId: 2, category: "B", netPrice: 100 }),
      ],
      [
        inventoryItem({ articleId: 1, category: "A", avgDailySales30d: 1 }),
        inventoryItem({ articleId: 2, category: "B", avgDailySales30d: 3 }),
      ],
      [] as DemandSignalItem[],
    );

    expect(merged).toEqual(legacy);
    expect(merged!.byGender).toEqual(legacy.byGender);
  });

  it("drops non-finite derived revenue instead of converting it to zero", () => {
    const result = buildCategoryIntelligenceFromSignals(
      [priceItem({ articleId: 1, category: "Overflow", netPrice: Number.MAX_VALUE })],
      [inventoryItem({ articleId: 1, category: "Overflow", avgDailySales30d: 30 })],
      [],
    );

    expect(result.byCategory).toEqual([]);
  });

  it("preserves a valid zero velocity and skips invalid price evidence", () => {
    const result = buildPriceSensitivityFromSignals(
      [
        priceItem({ articleId: 1, category: "Zero", netPrice: 100 }),
        priceItem({ articleId: 2, category: "NaN", netPrice: Number.NaN }),
        priceItem({ articleId: 3, category: "Infinity", netPrice: Number.POSITIVE_INFINITY }),
      ],
      [
        inventoryItem({ articleId: 1, category: "Zero", avgDailySales30d: 0, stockQty: 0 }),
        inventoryItem({ articleId: 2, category: "NaN", avgDailySales30d: 1 }),
        inventoryItem({ articleId: 3, category: "Infinity", avgDailySales30d: 1 }),
      ],
    );

    expect(result.bands).toHaveLength(1);
    expect(result.bands[0]).toMatchObject({ totalUnits: 0, totalStock: 0, avgVelocityPerSku: 0 });
  });

  it("does not use demand velocity as a fallback when inventory velocity is missing", () => {
    const result = buildCategoryIntelligenceFromSignals(
      [priceItem({ articleId: 1, category: "Missing inventory rate", netPrice: 100 })],
      [inventoryItem({ articleId: 1, category: "Missing inventory rate", avgDailySales30d: null as unknown as number })],
      [{
        articleId: 1,
        productName: "Fallback candidate",
        category: "Missing inventory rate",
        supplierName: "Dobavljac",
        daysSinceLastSale: 10,
        salesVelocity: 5,
        demandAcceleration: 1,
        storeCoverage: 1,
      }],
    );

    expect(result.byCategory).toEqual([]);
  });

  it("keeps aging evidence when cost is a valid zero and does not use price as a cost fallback", () => {
    const result = buildAgingResultFromSignals(
      [
        inventoryItem({ articleId: 1, category: "Known", avgDailySales30d: 0, stockQty: 4 }),
        inventoryItem({ articleId: 2, category: "Missing", avgDailySales30d: 0, stockQty: 4 }),
      ],
      [
        { articleId: 1, productName: "Zero cost", category: "Known", supplierName: "Dobavljac", daysSinceLastSale: 100, salesVelocity: 0, demandAcceleration: 0, storeCoverage: 1 },
        { articleId: 2, productName: "Missing cost", category: "Missing", supplierName: "Dobavljac", daysSinceLastSale: 100, salesVelocity: 0, demandAcceleration: 0, storeCoverage: 1 },
      ],
      [
        priceItem({ articleId: 1, category: "Known", netPrice: 100, cost: 0 }),
        priceItem({ articleId: 2, category: "Missing", netPrice: 100, cost: null as unknown as number }),
      ],
    );

    expect(result.items).toHaveLength(2);
    expect(result.items.find((item) => item.id === 1)?.stockValue).toBe(0);
    expect(result.items.find((item) => item.id === 2)?.stockValue).toBeNull();
    expect(result.summary.criticalStockValue).toBeNull();
  });

  it("rejects invalid depletion coverage while preserving a measured zero risk", () => {
    const result = buildDepletionResultFromSignals(
      [
        inventoryItem({ articleId: 1, category: "Zero risk", avgDailySales30d: 1, daysOfCover: 14, stockQty: 0 }),
        inventoryItem({ articleId: 2, category: "Negative", avgDailySales30d: 1, daysOfCover: -1 }),
        inventoryItem({ articleId: 3, category: "NaN", avgDailySales30d: 1, daysOfCover: Number.NaN }),
        inventoryItem({ articleId: 4, category: "Null", avgDailySales30d: 1, daysOfCover: null as unknown as number }),
      ],
      [priceItem({ articleId: 1, category: "Zero risk", netPrice: 100 })],
      "2026-08-01T00:00:00Z",
    );

    expect(result.forecasts).toHaveLength(1);
    expect(result.forecasts[0]).toMatchObject({ daysUntilOOS: 14, atRiskRevenue: 0 });
    expect(Number.isFinite(result.totalAtRiskRevenue)).toBe(true);
  });

  it("keeps an empty depletion total unavailable instead of inventing zero risk", () => {
    const result = buildDepletionResultFromSignals(
      [inventoryItem({ articleId: 1, category: "Unknown", avgDailySales30d: 1, daysOfCover: null as unknown as number })],
      [priceItem({ articleId: 1, category: "Unknown", netPrice: 100 })],
      "2026-08-01T00:00:00Z",
    );

    expect(result.forecasts).toEqual([]);
    expect(result.totalAtRiskRevenue).toBeNull();
  });

  it("keeps the derived reorder builder non-actionable", () => {
    const result = buildSmartReorderFromSignals([], [], [], []);

    expect(result.items).toEqual([]);
    expect(result.byCategoryPlan).toEqual([]);
    expect(result.bySupplierPlan).toEqual([]);
  });
});

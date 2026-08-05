import { describe, expect, it } from "vitest";
import { fmtPct } from "../../utils/analyticsFormatters";
import {
  buildCategoryIntelligenceFromSignals,
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

  it("returns 0 revShare when total revenue is zero", () => {
    const result = buildCategoryIntelligenceFromSignals(
      [priceItem({ articleId: 1, category: "Empty", netPrice: 100 })],
      [inventoryItem({ articleId: 1, category: "Empty", avgDailySales30d: 0 })],
      [],
    );

    expect(result.byCategory).toHaveLength(1);
    expect(result.byCategory[0].revShare).toBe(0);
    expect(result.byCategory[0].totalRevenue).toBe(0);
  });

  it("keeps percent units when merge promotes derived over legacy", () => {
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

    expect(merged).not.toBeNull();
    expect(merged!.byCategory.find((r) => r.kategorija === "A")?.revShare).toBe(25);
    expect(merged!.byGender).toEqual(legacy.byGender);
  });
});

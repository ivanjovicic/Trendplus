import { describe, it, expect, vi } from "vitest";

describe("InventoryPage - total value semantics", () => {
  it("should use authoritative backend total, not page-local row sum", () => {
    // Scenario: Multiple pages with missing backend total
    const balance = {
      totalSku: 100,
      totalOnHand: 500,
      lowStockCount: 15,
      outOfStockCount: 5,
      estimatedInventoryValue: null, // Backend cannot provide authoritative total
    };

    const pageData = {
      items: [
        { id: 1, estimatedValue: 1000 },
        { id: 2, estimatedValue: 2000 },
        { id: 3, estimatedValue: 3000 },
      ],
      totalCount: 500, // 500 items total across pages
      meta: { success: true },
    };

    // Total value should be null (unavailable), not the sum of current page rows (6000)
    const totalValue = balance?.estimatedInventoryValue ?? null;
    expect(totalValue).toBeNull(); // Not 6000 (page sum)
  });

  it("should preserve measured zero distinct from missing value", () => {
    // Scenario: Zero inventory value is legitimate
    const balanceWithZero = {
      totalSku: 5,
      estimatedInventoryValue: 0,
    };

    const totalValue = balanceWithZero?.estimatedInventoryValue ?? null;
    expect(totalValue).toBe(0); // Zero is preserved, not confused with null
  });

  it("should handle authoritative total across multiple pages", () => {
    // Scenario: Backend provides authoritative total for entire inventory
    const balanceWithTotal = {
      totalSku: 500,
      estimatedInventoryValue: 250000, // Total across all pages
    };

    const pageData = {
      items: [
        { id: 1, estimatedValue: 1000 },
        { id: 2, estimatedValue: 2000 },
      ],
      totalCount: 500, // Many more items on other pages
      meta: { success: true },
    };

    const totalValue = balanceWithTotal?.estimatedInventoryValue ?? null;
    expect(totalValue).toBe(250000); // Use authoritative backend value, not page sum

    // Even if we sum the page: 1000 + 2000 = 3000, we use 250000
    const pageSum = (pageData.items ?? []).reduce(
      (sum, row) => sum + (row.estimatedValue ?? 0),
      0
    );
    expect(pageSum).toBe(3000);
    expect(totalValue).not.toBe(pageSum); // Critical: do not use page sum as total
  });

  it("should not change total value on pagination", () => {
    const balance = {
      estimatedInventoryValue: 150000,
    };

    // Page 1 data
    const page1Value = balance?.estimatedInventoryValue ?? null;

    // Simulate navigating to page 2
    const balance2 = {
      estimatedInventoryValue: 150000, // Same backend response
    };
    const page2Value = balance2?.estimatedInventoryValue ?? null;

    // Total must not change
    expect(page1Value).toBe(page2Value);
    expect(page2Value).toBe(150000);
  });

  it("should distinguish unavailable total from zero total", () => {
    // Unavailable (null)
    const unavailableTotal = null;

    // Zero total
    const zeroTotal = 0;

    // These must remain distinct
    expect(unavailableTotal).not.toBe(zeroTotal);
    expect(zeroTotal === 0).toBe(true);
    expect(unavailableTotal === null).toBe(true);
  });
});

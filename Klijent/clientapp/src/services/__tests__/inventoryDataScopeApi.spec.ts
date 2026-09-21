import { afterEach, describe, expect, it, vi } from "vitest";
import {
  getInventoryActionSuggestions,
  getInventoryBalance,
  getInventoryInsights,
  getInventoryItemDetail,
  getInventoryList,
  getInventoryStoreComparison,
} from "../analyticsApi";

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("Inventory data-scope API contract", () => {
  it("sends the selected scope to every Inventory endpoint that supports scoped article data", async () => {
    const urls: string[] = [];
    vi.stubGlobal("fetch", vi.fn().mockImplementation(async (input: RequestInfo | URL) => {
      urls.push(String(input));
      return {
        ok: true,
        json: async () => ({
          totalSku: 0,
          totalOnHand: 0,
          lowStockCount: 0,
          outOfStockCount: 0,
          items: [],
          totalCount: 0,
          pageNumber: 1,
          pageSize: 50,
          id: 42,
          estimatedValue: 0,
          updatedAt: "2026-05-26T12:00:00Z",
          movementCount: 0,
          daysSinceMovement: 0,
          signalConfidencePct: 0,
          recommendationAllowed: false,
          history: [],
          totalItems: 0,
          totalEstimatedValue: 0,
          aging: [],
          abc: [],
          topAgedItems: [],
          topCapitalLockedItems: [],
          meta: { success: true, dataQualityStatus: "insufficient_evidence" },
        }),
      };
    }));

    await Promise.all([
      getInventoryBalance(true, 2, 5, "imported"),
      getInventoryList({ dataScope: "imported" }),
      getInventoryInsights({ dataScope: "imported" }),
      getInventoryItemDetail(42, { dataScope: "imported" }),
      getInventoryStoreComparison({ dataScope: "imported" }),
      getInventoryActionSuggestions({ dataScope: "imported" }),
    ]);

    expect(urls).toHaveLength(6);
    for (const url of urls) {
      expect(new URL(url, window.location.origin).searchParams.get("dataScope")).toBe("imported");
    }
  });

  it("propagates abort signals through cached Inventory requests", async () => {
    const controller = new AbortController();
    vi.stubGlobal("fetch", vi.fn().mockImplementation((_input: RequestInfo | URL, init?: RequestInit) => (
      new Promise<Response>((_resolve, reject) => {
        init?.signal?.addEventListener("abort", () => reject(init.signal?.reason), { once: true });
      })
    )));

    const request = getInventoryInsights({ dataScope: "abort-check", signal: controller.signal });
    controller.abort();

    await expect(request).rejects.toMatchObject({ name: "AbortError" });
  });
});

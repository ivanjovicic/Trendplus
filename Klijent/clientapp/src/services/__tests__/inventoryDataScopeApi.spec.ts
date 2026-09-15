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
        json: async () => ({}),
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
});

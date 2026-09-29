import { describe, expect, it } from "vitest";
import type { StoreOption } from "../../types/analytics";
import {
  resolveStoreFilterFallbackState,
  resolveStoreFilterLoadFailure,
  STORE_FILTER_STALE_LIST_MESSAGE,
  STORE_SELECTION_CLEARED_MESSAGE,
} from "../storeFilterFallbackState";

const previousStores: StoreOption[] = [
  { storeId: 1, storeName: "Centar" },
  { storeId: 2, storeName: "Bulevar" },
];

describe("store filter fallback state", () => {
  it("keeps the last-known-good list but blocks selection on fallback metadata", () => {
    const result = resolveStoreFilterFallbackState(
      Object.assign([], { meta: { success: true, warningMessage: "Cache je zastareo." } }),
      previousStores,
      2,
    );

    expect(result.stores).toEqual(previousStores);
    expect(result.isStale).toBe(true);
    expect(result.selectedStoreId).toBeNull();
    expect(result.warning).toContain(STORE_FILTER_STALE_LIST_MESSAGE);
  });

  it("clears a selected store that is absent from a successful scoped response", () => {
    const result = resolveStoreFilterFallbackState(
      [{ storeId: 3, storeName: "Novi Sad" }],
      previousStores,
      2,
    );

    expect(result.stores).toEqual([{ storeId: 3, storeName: "Novi Sad" }]);
    expect(result.isStale).toBe(false);
    expect(result.shouldClearSelection).toBe(true);
    expect(result.selectedStoreId).toBeNull();
    expect(result.warning).toBe(STORE_SELECTION_CLEARED_MESSAGE);
  });

  it("preserves the selected store when the scoped response validates it", () => {
    const result = resolveStoreFilterFallbackState(previousStores, [], 2);

    expect(result.isStale).toBe(false);
    expect(result.shouldClearSelection).toBe(false);
    expect(result.selectedStoreId).toBe(2);
  });

  it("preserves the last-known-good list and clears selection on transport failure", () => {
    const result = resolveStoreFilterLoadFailure(previousStores, 2);

    expect(result.stores).toEqual(previousStores);
    expect(result.isStale).toBe(true);
    expect(result.shouldClearSelection).toBe(true);
    expect(result.selectedStoreId).toBeNull();
  });
});

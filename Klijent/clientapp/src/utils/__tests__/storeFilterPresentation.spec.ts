import { describe, expect, it } from "vitest";
import type { StoreOption } from "../../types/analytics";
import { buildStoreOptionLabel, getDuplicateStoreNames } from "../storeFilterPresentation";

describe("store filter presentation", () => {
  it("adds stable IDs when store names are duplicated", () => {
    const stores: StoreOption[] = [
      { storeId: 10, storeName: "Centar", city: "Beograd" },
      { storeId: 11, storeName: "Centar", city: "Novi Sad" },
      { storeId: 12, storeName: "Bulevar" },
    ];
    const duplicates = getDuplicateStoreNames(stores);

    expect(buildStoreOptionLabel(stores[0], duplicates)).toBe("Centar (Beograd) [ID 10]");
    expect(buildStoreOptionLabel(stores[1], duplicates)).toBe("Centar (Novi Sad) [ID 11]");
    expect(buildStoreOptionLabel(stores[2], duplicates)).toBe("Bulevar");
  });
});

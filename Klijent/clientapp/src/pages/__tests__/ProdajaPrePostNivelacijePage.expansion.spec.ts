import { describe, expect, it } from "vitest";
import { reconcilePrePostExpandedVendorKey } from "../ProdajaPrePostNivelacijePage";

describe("Pre/Post expanded vendor reconciliation", () => {
  it("keeps the expanded vendor when it remains in the refetched result", () => {
    expect(reconcilePrePostExpandedVendorKey("id:7", ["id:7", "id:9"])).toBe("id:7");
  });

  it("clears the expansion when the vendor is no longer present", () => {
    expect(reconcilePrePostExpandedVendorKey("id:7", ["id:9"])).toBeNull();
  });

  it("does not carry a positional row key across refetch because it may identify a different vendor", () => {
    expect(reconcilePrePostExpandedVendorKey("row:3", ["id:7", "row:3"])).toBeNull();
  });

  it("keeps an empty expansion empty", () => {
    expect(reconcilePrePostExpandedVendorKey(null, ["id:7"])).toBeNull();
  });
});

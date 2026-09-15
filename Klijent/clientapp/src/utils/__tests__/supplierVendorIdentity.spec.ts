import { describe, expect, it } from "vitest";
import {
  buildSupplierVendorDetailRecordId,
  buildSupplierVendorKey,
  buildSupplierVendorKeys,
  resolveSupplierArticleVendorKey,
} from "../supplierVendorIdentity";

describe("supplierVendorIdentity", () => {
  it("keeps authoritative vendor IDs as stable keys", () => {
    expect(buildSupplierVendorKey({ vendorId: 42, vendorName: "Alfa" }, 0)).toBe("id:42");
  });

  it("uses row index for null-ID vendors instead of normalized name", () => {
    const vendor = { vendorId: null, vendorName: "Alfa" };
    expect(buildSupplierVendorKey(vendor, 0)).toBe("row:0");
    expect(buildSupplierVendorKey(vendor, 1)).toBe("row:1");
    expect(buildSupplierVendorKey({ vendorId: null, vendorName: "" }, 2)).toBe("row:2");
  });

  it("keeps distinct null-ID vendors with the same or blank name collision-safe", () => {
    const vendors = [
      { vendorId: null, vendorName: "Alfa" },
      { vendorId: null, vendorName: "ALFA" },
      { vendorId: null, vendorName: "" },
      { vendorId: null, vendorName: "   " },
    ];
    expect(buildSupplierVendorKeys(vendors)).toEqual(["row:0", "row:1", "row:2", "row:3"]);
  });

  it("does not collapse distinct backend rows that share a vendor ID", () => {
    const vendors = [
      { vendorId: 5, vendorName: "Alfa" },
      { vendorId: 5, vendorName: "Alfa magacin" },
    ];
    expect(buildSupplierVendorKeys(vendors)).toEqual(["row:0", "row:1"]);
  });

  it("uses collision-safe record IDs for null-ID and duplicate-ID vendor detail snapshots", () => {
    expect(buildSupplierVendorDetailRecordId({ vendorId: 7, vendorName: "Alfa" }, "id:7")).toBe("7");
    expect(buildSupplierVendorDetailRecordId({ vendorId: null, vendorName: "Alfa" }, "row:0")).toBe("row:0");
    expect(buildSupplierVendorDetailRecordId({ vendorId: 5, vendorName: "Alfa" }, "row:0")).toBe("row:0");
  });

  it("resolves article vendor keys by unique ID or unambiguous name match", () => {
    const vendorStats = [
      { vendorId: null, vendorName: "Alfa" },
      { vendorId: null, vendorName: "Alfa" },
    ];
    const vendorKeys = buildSupplierVendorKeys(vendorStats);

    expect(resolveSupplierArticleVendorKey({ vendorId: 9, vendorName: "Beta" }, 0, vendorStats, vendorKeys)).toBe("id:9");
    expect(resolveSupplierArticleVendorKey({ vendorId: null, vendorName: "Gamma" }, 0, [
      { vendorId: null, vendorName: "Gamma" },
    ])).toBe("row:0");
    expect(resolveSupplierArticleVendorKey({ vendorId: null, vendorName: "Alfa" }, 3, vendorStats, vendorKeys)).toBe("article:3");
    expect(resolveSupplierArticleVendorKey({ vendorId: null, vendorName: "" }, 1, [
      { vendorId: null, vendorName: "" },
      { vendorId: null, vendorName: " " },
    ])).toBe("article:1");
  });

  it("does not attribute articles to a guessed vendor when IDs collide", () => {
    const vendorStats = [
      { vendorId: 5, vendorName: "Alfa" },
      { vendorId: 5, vendorName: "Alfa magacin" },
    ];
    const vendorKeys = buildSupplierVendorKeys(vendorStats);

    expect(resolveSupplierArticleVendorKey({ vendorId: 5, vendorName: "Alfa" }, 4, vendorStats, vendorKeys)).toBe("article:4");
  });
});

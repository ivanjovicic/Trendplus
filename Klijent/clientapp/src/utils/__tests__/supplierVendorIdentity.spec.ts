import { describe, expect, it } from "vitest";
import {
  buildSupplierVendorDetailRecordId,
  buildSupplierVendorKey,
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

  it("keeps distinct null-ID vendors with the same name collision-safe", () => {
    const first = buildSupplierVendorKey({ vendorId: null, vendorName: "Alfa" }, 0);
    const second = buildSupplierVendorKey({ vendorId: null, vendorName: "ALFA" }, 1);
    expect(first).not.toBe(second);
  });

  it("uses collision-safe record IDs for null-ID vendor detail snapshots", () => {
    expect(buildSupplierVendorDetailRecordId({ vendorId: 7, vendorName: "Alfa" }, "row:0")).toBe("7");
    expect(buildSupplierVendorDetailRecordId({ vendorId: null, vendorName: "Alfa" }, "row:0")).toBe("row:0");
  });

  it("resolves article vendor keys by ID or unambiguous name match", () => {
    const vendorStats = [
      { vendorId: null, vendorName: "Alfa" },
      { vendorId: null, vendorName: "Alfa" },
    ];

    expect(resolveSupplierArticleVendorKey({ vendorId: 9, vendorName: "Beta" }, 0, vendorStats)).toBe("id:9");
    expect(resolveSupplierArticleVendorKey({ vendorId: null, vendorName: "Gamma" }, 0, [
      { vendorId: null, vendorName: "Gamma" },
    ])).toBe("row:0");
    expect(resolveSupplierArticleVendorKey({ vendorId: null, vendorName: "Alfa" }, 3, vendorStats)).toBe("article:3");
  });
});

import { describe, expect, it } from "vitest";
import { decisionColumns, reconcilePrePostExpandedVendorKey } from "../ProdajaPrePostNivelacijePage";
import { buildAnalyticsDetailSnapshot } from "../../services/analyticsTableState";

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

describe("Pre/Post full-detail export projection", () => {
  it("keeps split metrics unavailable when the vendor row has no comparable window", () => {
    const row = {
      vendorName: "Dobavljač",
      hasComparableSalesWindow: false,
      preRevenue: 1_000,
      postRevenue: 1_200,
      preQty: 10,
      postQty: 12,
      changeRevenue: 200,
      changeQty: 2,
      avgCoveragePre30: 0.8,
      avgCoveragePost30: 0.7,
    };
    const snapshot = buildAnalyticsDetailSnapshot({
      table: "nivelacije-pre-post",
      recordId: "vendor:1",
      title: row.vendorName,
      columns: decisionColumns as never,
      row: row as never,
    });

    expect(snapshot.fields).toEqual(expect.arrayContaining([
      expect.objectContaining({ key: "hasComparableSalesWindow", value: "Ne" }),
      expect.objectContaining({ key: "preRevenue", value: "Nije dostupno" }),
      expect.objectContaining({ key: "postRevenue", value: "Nije dostupno" }),
      expect.objectContaining({ key: "preQty", value: "Nije dostupno" }),
      expect.objectContaining({ key: "postQty", value: "Nije dostupno" }),
      expect.objectContaining({ key: "changeRevenue", value: "Nije dostupno" }),
      expect.objectContaining({ key: "changeQty", value: "Nije dostupno" }),
      expect.objectContaining({ key: "avgCoveragePre30", value: "Nije dostupno" }),
    ]));
  });
});

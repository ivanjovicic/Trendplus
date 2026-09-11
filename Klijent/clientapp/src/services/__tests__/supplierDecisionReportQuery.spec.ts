import { describe, expect, it } from "vitest";
import { buildSupplierDecisionReportHref } from "../supplierDecisionReportQuery";

describe("supplier decision report query contract", () => {
  it("round-trips every material filter and preserves zero, false and explicit empty values", () => {
    const href = buildSupplierDecisionReportHref({
      fromDate: "2026-04-01",
      toDate: "2026-06-30",
      scope: "existing",
      category: "Patike & čizme",
      gender: "Ženski",
      seasonId: 0,
      minRevenue: 0,
      onlyHighConfidence: false,
      excludeOosBeforeMarkdown: true,
      supplierId: 0,
      storeId: 0,
      section: "supplier_negotiation_pack",
    });
    const query = new URL(href, "http://localhost").searchParams;

    expect(query.get("fromDate")).toBe("2026-04-01");
    expect(query.get("toDate")).toBe("2026-06-30");
    expect(query.get("scope")).toBe("existing");
    expect(query.get("category")).toBe("Patike & čizme");
    expect(query.get("gender")).toBe("Ženski");
    expect(query.get("seasonId")).toBe("0");
    expect(query.get("minRevenue")).toBe("0");
    expect(query.get("onlyHighConfidence")).toBe("false");
    expect(query.get("excludeOosBeforeMarkdown")).toBe("true");
    expect(query.get("supplierId")).toBe("0");
    expect(query.get("storeId")).toBe("0");
    expect(query.get("section")).toBe("supplier_negotiation_pack");
    expect(query.has("dataScope")).toBe(false);
  });

  it("keeps an explicit empty value distinct from an absent filter", () => {
    const explicitEmpty = new URL(buildSupplierDecisionReportHref({ category: "" }), "http://localhost").searchParams;
    const absent = new URL(buildSupplierDecisionReportHref({}), "http://localhost").searchParams;

    expect(explicitEmpty.has("category")).toBe(true);
    expect(explicitEmpty.get("category")).toBe("");
    expect(absent.has("category")).toBe(false);
  });
});

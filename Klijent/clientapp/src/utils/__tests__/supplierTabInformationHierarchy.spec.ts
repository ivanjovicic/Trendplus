import { describe, expect, it } from "vitest";
import {
  SUPPLIER_ASSORTMENT_SECONDARY_KPI_TITLE,
  SUPPLIER_SCORECARD_SECONDARY_KPI_TITLE,
  SUPPLIER_TAB_ROLE_CUE,
} from "../supplierTabInformationHierarchy";

describe("supplierTabInformationHierarchy", () => {
  it("defines a distinct role cue for each Supplier tab", () => {
    const cues = Object.values(SUPPLIER_TAB_ROLE_CUE);
    expect(new Set(cues).size).toBe(cues.length);
    expect(SUPPLIER_TAB_ROLE_CUE.overview).toMatch(/konačn/i);
    expect(SUPPLIER_TAB_ROLE_CUE.scorecard).toMatch(/pomoćni signal/i);
    expect(SUPPLIER_TAB_ROLE_CUE.assortment).toMatch(/objašnjenje/i);
  });

  it("labels demoted KPI sections explicitly as secondary context", () => {
    expect(SUPPLIER_SCORECARD_SECONDARY_KPI_TITLE).toMatch(/sekundarno/i);
    expect(SUPPLIER_ASSORTMENT_SECONDARY_KPI_TITLE).toMatch(/sekundarno/i);
  });
});

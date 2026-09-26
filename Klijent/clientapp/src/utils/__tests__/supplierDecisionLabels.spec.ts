import { describe, expect, it } from "vitest";
import {
  supplierDecisionDatasetLabel,
  supplierDecisionFreshnessLabel,
  supplierDecisionProvenanceLabel,
  supplierDecisionReasonText,
} from "../supplierDecisionLabels";

describe("supplierDecisionLabels", () => {
  it("maps technical dataset and provenance identifiers to Serbian user copy", () => {
    expect(supplierDecisionDatasetLabel("90d")).toBe("poslednjih 90 dana");
    expect(supplierDecisionDatasetLabel("mv_supplier_decision_score_cache_90d")).toBe("keš signala odluke dobavljača");
    expect(supplierDecisionProvenanceLabel("mv_supplier_decision_score_cache_180d")).toBe("keš signala odluke dobavljača");
  });

  it("redacts technical terminology without changing internal codes", () => {
    expect(supplierDecisionReasonText("Korišćen fallback dataset; stock-risk signal je upozorenje.")).toBe(
      "Korišćen pomoćni skup podataka; signal rizika zaliha je upozorenje."
    );
    expect(supplierDecisionReasonText("fallback_dataset_used")).toBeNull();
  });

  it("maps freshness statuses to Serbian copy", () => {
    expect(supplierDecisionFreshnessLabel("stale")).toBe("Zastarelo");
    expect(supplierDecisionFreshnessLabel("fresh")).toBe("Sveže");
  });
});

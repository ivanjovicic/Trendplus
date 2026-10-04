import { describe, expect, it } from "vitest";
import {
  getAnalyticsDecisionReadiness,
  getAnalyticsIntegrityState,
  isAnalyticsDecisionActionable,
} from "../analyticsDecisionReadiness";

describe("analyticsDecisionReadiness", () => {
  it("keeps backend categorical readiness authoritative", () => {
    const readiness = getAnalyticsDecisionReadiness({
      success: true,
      recommendationAllowed: false,
      decisionReadiness: {
        state: "blocked",
        surfaceRole: "recommendation",
        recommendationAllowed: false,
        reasonCodes: ["missing_cost_evidence"],
        evidenceReferences: ["supplier.costCoverage"],
        repairPath: "Data Quality",
      },
    });

    expect(readiness.state).toBe("blocked");
    expect(readiness.reasonCodes).toEqual(["missing_cost_evidence"]);
    expect(isAnalyticsDecisionActionable({ success: true, decisionReadiness: readiness })).toBe(false);
  });

  it("preserves a useful signal without presenting it as a final CTA", () => {
    const readiness = getAnalyticsDecisionReadiness({
      success: true,
      decisionReadiness: {
        state: "signal_only",
        surfaceRole: "signal",
        recommendationAllowed: true,
        reasonCodes: [],
        evidenceReferences: ["color.net_sales_signed"],
        repairPath: null,
      },
    }, "signal");

    expect(readiness.state).toBe("signal_only");
    expect(isAnalyticsDecisionActionable({ success: true, decisionReadiness: readiness })).toBe(false);
  });

  it("fails closed when legacy metadata has no readiness contract", () => {
    const readiness = getAnalyticsDecisionReadiness({ success: true }, "report");
    expect(readiness.state).toBe("unavailable");
    expect(readiness.reasonCodes).toContain("decision_readiness_unavailable");
  });

  it("never treats an integrity snapshot as verified outside its bound response context", () => {
    expect(getAnalyticsIntegrityState({
      success: true,
      operationsIntegrityStatus: "verified",
      operationsIntegrityContextMatches: false,
    }, false)).toBe("unverified");
    expect(getAnalyticsIntegrityState({
      success: true,
      operationsIntegrityStatus: "drift_detected",
      operationsIntegrityContextMatches: true,
    }, true)).toBe("drift_detected");
    expect(getAnalyticsIntegrityState({
      success: true,
      operationsIntegrityStatus: "verified",
      operationsIntegrityContextMatches: true,
      operationsIntegrityEvidenceId: null,
    }, true)).toBe("unverified");
    expect(getAnalyticsIntegrityState({ success: true }, false)).toBe("unverified");
  });
});

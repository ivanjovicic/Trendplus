import type {
  AnalyticsDecisionReadiness,
  AnalyticsResponseMeta,
} from "../types/analytics";

export const ANALYTICS_DECISION_READINESS_STATES = [
  "decision_ready",
  "signal_only",
  "blocked",
  "unavailable",
] as const;

export function getAnalyticsDecisionReadiness(
  meta: AnalyticsResponseMeta | null | undefined,
  surfaceRole: AnalyticsDecisionReadiness["surfaceRole"] = "recommendation",
): AnalyticsDecisionReadiness {
  if (meta?.decisionReadiness) return meta.decisionReadiness;

  return {
    state: "unavailable",
    surfaceRole,
    recommendationAllowed: meta?.recommendationAllowed ?? null,
    reasonCodes: ["decision_readiness_unavailable"],
    evidenceReferences: [],
    repairPath: "Proverite izvor podataka i status osvežavanja.",
  };
}

export function isAnalyticsDecisionActionable(
  meta: AnalyticsResponseMeta | null | undefined,
): boolean {
  const readiness = getAnalyticsDecisionReadiness(meta);
  return readiness.state === "decision_ready" && readiness.recommendationAllowed === true;
}

export type AnalyticsIntegrityState = "verified" | "unverified" | "degraded" | "drift_detected" | "unavailable";

export function getAnalyticsIntegrityState(
  meta: AnalyticsResponseMeta | null | undefined,
  contextIsCurrent: boolean,
): AnalyticsIntegrityState {
  if (!contextIsCurrent) return "unverified";
  const status = meta?.operationsIntegrityStatus?.trim().toLowerCase();
  if (status === "verified" || status === "unverified" || status === "degraded" || status === "drift_detected") {
    if (status === "verified" && !meta?.operationsIntegrityEvidenceId?.trim()) return "unverified";
    return status;
  }
  return "unavailable";
}

export function isInsightStudioExperimentalEnabled(): boolean {
  // Build-time visibility gate only; enable it only in an owner-facing deployment.
  return String(import.meta.env.VITE_ENABLE_EXPERIMENTAL_INSIGHT_STUDIO ?? "false").toLowerCase() === "true";
}

export function finiteToolbarCount(value: number | null | undefined): number | null {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

export function resolveToolbarMetricsStatus(value: string | null | undefined): string | null {
  if (value == null) return null;
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

export function formatPrePostAnalysisWindowHint(windowDays: number | null | undefined): string {
  const normalized = finiteToolbarCount(windowDays);
  if (normalized == null) {
    return "Analiza poredjena po nivelacionom prozoru: prozor nije dostupan.";
  }
  return `Analiza poredjena po nivelacionom prozoru od ${normalized} dana.`;
}

import { fmtPct } from "./analyticsFormatters";

export type DataQualityTone = "good" | "warning" | "critical" | "insufficient_data";

export function normalizeDataQualityStatus(value: string | null | undefined): DataQualityTone {
  const normalized = (value ?? "").trim().toLowerCase();
  if (normalized === "good") return "good";
  if (normalized === "warning") return "warning";
  if (normalized === "critical" || normalized === "error") return "critical";
  return "insufficient_data";
}

export function dataQualityStatusLabel(value: string | null | undefined): string {
  const tone = normalizeDataQualityStatus(value);
  if (tone === "good") return "Dobro";
  if (tone === "warning") return "Oprez";
  if (tone === "critical") return "Kriticno / ne veruj";
  return "Nedovoljno podataka";
}

export function normalizePercent(value: number | null | undefined): number | null {
  if (value == null || Number.isNaN(value)) return null;
  return Math.max(0, Math.min(100, value));
}

export function formatConfidence(value: number | null | undefined, digits = 0): string {
  const normalized = normalizePercent(value);
  if (normalized == null) return "Pouzdanost nije dostupna";
  return fmtPct(normalized, digits);
}

export function formatReliability(value: number | null | undefined, digits = 0): string {
  const normalized = normalizePercent(value);
  if (normalized == null) return "Pouzdanost nije dostupna";
  return fmtPct(normalized, digits);
}

export function buildPopUnavailableHint(previousPeriodValue: number | null | undefined): string | null {
  if (previousPeriodValue == null) return "Nema prethodnog perioda za PoP poredenje.";
  if (previousPeriodValue <= 0) return "Nema prethodnog perioda za PoP poredenje.";
  return null;
}

export const QUALITY_EXPLANATIONS = {
  marginEstimated: "Marza je procena jer deo nabavne cene nije istorijski potvrdjen.",
  lowSampleRecommendation: "Preporuka nije pouzdana zbog malog uzorka.",
  missingConfidence: "Pouzdanost nije dostupna.",
} as const;


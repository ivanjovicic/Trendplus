import type { CSSProperties } from "react";

export type CanonicalRecommendationStatus =
  | "increase_focus"
  | "maintain"
  | "review"
  | "do_not_trust"
  | "insufficient_data";

export type RecommendationQualityStatus =
  | "good"
  | "warning"
  | "critical"
  | "insufficient_data";

export const RECOMMENDATION_STATUS_PRIORITY: Record<CanonicalRecommendationStatus, number> = {
  increase_focus: 5,
  maintain: 4,
  review: 3,
  insufficient_data: 2,
  do_not_trust: 1,
};

export const RECOMMENDATION_RELIABILITY_LABEL = "Pouzdanost signala %";
export const RECOMMENDATION_CONFIDENCE_LABEL = "Sigurnost preporuke %";
export const RECOMMENDATION_SIGNAL_UNAVAILABLE =
  "Pouzdanost nije dostupna (backend nije dostavio confidence/reliability signal).";

export const RECOMMENDATION_RELIABILITY_TOOLTIP =
  "Pouzdanost signala % pokazuje koliko je ulazni signal stabilan i podatkovno pokriven. Nije garancija ishoda.";

export const RECOMMENDATION_CONFIDENCE_TOOLTIP =
  "Sigurnost preporuke % pokazuje koliko je backend preporuka upotrebljiva za odluku. Nije garancija poslovnog ishoda.";

export function isCanonicalRecommendationStatus(value: string | null | undefined): value is CanonicalRecommendationStatus {
  return value === "increase_focus"
    || value === "maintain"
    || value === "review"
    || value === "do_not_trust"
    || value === "insufficient_data";
}

export function recommendationStatusLabel(status: CanonicalRecommendationStatus): string {
  if (status === "increase_focus") return "Pojacaj";
  if (status === "maintain") return "Zadrzi";
  if (status === "review") return "Pregledaj";
  if (status === "do_not_trust") return "Ne veruj";
  return "Nedovoljno podataka";
}

export function recommendationStatusTone(status: CanonicalRecommendationStatus): "boost" | "keep" | "review" | "reduce" | "na" {
  if (status === "increase_focus") return "boost";
  if (status === "maintain") return "keep";
  if (status === "review") return "review";
  if (status === "do_not_trust") return "reduce";
  return "na";
}

export function recommendationStatusTooltipBrief(status: CanonicalRecommendationStatus): string {
  if (status === "increase_focus") return "Pozitivan signal; povecati fokus uz standardnu kontrolu rizika.";
  if (status === "maintain") return "Stabilan signal; zadrzati trenutni nivo fokusa.";
  if (status === "review") return "Mesovit signal; potreban rucni pregled pre promene fokusa.";
  if (status === "do_not_trust") return "Signal je nepouzdan za akciju; ne donositi odluku bez dodatne provere.";
  return "Nedovoljno podataka za pouzdanu preporuku.";
}

export function normalizeRecommendationPct(value: number | null | undefined): number | null {
  if (value == null || !Number.isFinite(value)) return null;
  const normalized = value <= 1 ? value * 100 : value;
  return Math.max(0, Math.min(100, normalized));
}

export function normalizeRecommendationQualityStatus(
  value: string | null | undefined
): RecommendationQualityStatus {
  if (value === "good" || value === "warning" || value === "critical") return value;
  return "insufficient_data";
}

export function recommendationQualityLabel(status: RecommendationQualityStatus): string {
  if (status === "good") return "Dobar kvalitet podataka";
  if (status === "warning") return "Upozorenje kvaliteta podataka";
  if (status === "critical") return "Kritican kvalitet podataka";
  return "Nedovoljno podataka";
}

export function recommendationQualityTone(
  status: RecommendationQualityStatus
): "good" | "warning" | "critical" | "insufficient_data" {
  if (status === "good") return "good";
  if (status === "warning") return "warning";
  if (status === "critical") return "critical";
  return "insufficient_data";
}

export function recommendationQualityStyle(status: RecommendationQualityStatus): CSSProperties {
  if (status === "good") {
    return { color: "var(--success, #16a34a)" };
  }
  if (status === "warning") {
    return { color: "var(--warning, #d97706)" };
  }
  if (status === "critical") {
    return { color: "var(--danger, #dc2626)" };
  }
  return { color: "var(--text-muted, #6b7280)" };
}

export function recommendationReasonHintFromCode(code: string): string | null {
  if (code === "missing_cost_coverage") {
    return "Marza je procena jer deo nabavne cene nije istorijski potvrdjen.";
  }
  if (code === "tiny_sample") {
    return "Preporuka nije pouzdana zbog malog uzorka.";
  }
  if (code === "previous_period_missing" || code === "no_previous_baseline" || code === "pop_unavailable") {
    return "Nema prethodnog perioda za PoP poredjenje.";
  }
  return null;
}

export function recommendationReasonHints(reasonCodes: string[]): string[] {
  return Array.from(
    new Set(
      reasonCodes
        .map((code) => recommendationReasonHintFromCode(code))
        .filter((hint): hint is string => Boolean(hint))
    )
  );
}

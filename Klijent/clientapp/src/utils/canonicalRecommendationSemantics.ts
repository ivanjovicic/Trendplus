export type CanonicalRecommendationStatus =
  | "increase_focus"
  | "maintain"
  | "review"
  | "do_not_trust"
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

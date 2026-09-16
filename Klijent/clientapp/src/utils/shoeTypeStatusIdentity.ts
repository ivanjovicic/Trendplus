import type { AnalyticsRecommendation } from "../services/shoeTypeSalesStatsApi";
import {
  type CanonicalRecommendationStatus,
  isCanonicalRecommendationStatus,
  normalizeRecommendationPct,
} from "./canonicalRecommendationSemantics";

export const MISSING_SHOE_TYPE_RECOMMENDATION_REASON =
  "Backend recommendation payload nedostaje; red ostaje informativan bez lokalnog izvodjenja preporuke.";

export type ShoeTypeRecommendationProjection = {
  status: CanonicalRecommendationStatus;
  recommendationAllowed: boolean;
  statusReason: string;
  reliabilityPct: number | null;
  confidencePct: number | null;
  reliabilityAvailable: boolean;
  confidenceAvailable: boolean;
};

export function mapShoeTypeBackendStatus(status?: string | null): CanonicalRecommendationStatus {
  return isCanonicalRecommendationStatus(status) ? status : "insufficient_data";
}

export function buildShoeTypeRecommendationProjection(
  recommendation?: AnalyticsRecommendation | null,
  rowReliabilityPct?: number | null,
): ShoeTypeRecommendationProjection {
  const status = mapShoeTypeBackendStatus(recommendation?.status);
  const recommendationAllowed = recommendation?.recommendationAllowed === true;
  const baseReason = recommendation?.summary ?? MISSING_SHOE_TYPE_RECOMMENDATION_REASON;
  const statusReason = recommendationAllowed
    ? baseReason
    : `Automatska preporuka nije dozvoljena: ${baseReason}`;
  const reliabilityPctValue = recommendationAllowed
    ? normalizeRecommendationPct(recommendation?.reliabilityPct ?? rowReliabilityPct)
    : null;
  const confidencePctValue = recommendationAllowed
    ? normalizeRecommendationPct(recommendation?.confidencePct)
    : null;

  return {
    status,
    recommendationAllowed,
    statusReason,
    reliabilityPct: reliabilityPctValue,
    confidencePct: confidencePctValue,
    reliabilityAvailable: reliabilityPctValue != null,
    confidenceAvailable: confidencePctValue != null,
  };
}

import type { AnalyticsRecommendation } from "../services/shoeTypeSalesStatsApi";
import {
  type CanonicalRecommendationStatus,
  isCanonicalRecommendationStatus,
  normalizeRecommendationPct,
} from "./canonicalRecommendationSemantics";

export const MISSING_SHOE_TYPE_RECOMMENDATION_REASON =
  "Backend recommendation payload nedostaje; red ostaje informativan bez lokalnog izvodjenja preporuke.";

export const UNKNOWN_SHOE_TYPE_RECOMMENDATION_STATUS_REASON =
  "Status preporuke nije prepoznat; red ostaje informativan bez automatske preporuke.";

export type ShoeTypeRecommendationProjection = {
  status: CanonicalRecommendationStatus;
  recommendationAllowed: boolean;
  statusReason: string;
  reliabilityPct: number | null;
  confidencePct: number | null;
  reliabilityAvailable: boolean;
  confidenceAvailable: boolean;
};

export function mapShoeTypeBackendStatus(status?: string | null): CanonicalRecommendationStatus | null {
  return isCanonicalRecommendationStatus(status) ? status : null;
}

export function buildShoeTypeRecommendationProjection(
  recommendation?: AnalyticsRecommendation | null,
  rowReliabilityPct?: number | null,
): ShoeTypeRecommendationProjection {
  const mappedBackendStatus = mapShoeTypeBackendStatus(recommendation?.status);
  const hasSupportedBackendStatus = mappedBackendStatus != null;
  const status = mappedBackendStatus ?? "insufficient_data";
  const recommendationAllowed = hasSupportedBackendStatus && recommendation?.recommendationAllowed === true;
  const statusReason = hasSupportedBackendStatus
    ? recommendation?.summary ?? MISSING_SHOE_TYPE_RECOMMENDATION_REASON
    : UNKNOWN_SHOE_TYPE_RECOMMENDATION_STATUS_REASON;
  const actionabilityReason = recommendationAllowed
    ? statusReason
    : hasSupportedBackendStatus
      ? recommendation?.recommendationAllowed === false
        ? `Backend je blokirao izvrsenje preporuke: ${statusReason}`
        : `Backend nije potvrdio da je preporuka izvrsna: ${statusReason}`
      : statusReason;
  const reliabilityPctValue = recommendationAllowed
    ? normalizeRecommendationPct(recommendation?.reliabilityPct ?? rowReliabilityPct)
    : null;
  const confidencePctValue = recommendationAllowed
    ? normalizeRecommendationPct(recommendation?.confidencePct)
    : null;

  return {
    status,
    recommendationAllowed,
    statusReason: actionabilityReason,
    reliabilityPct: reliabilityPctValue,
    confidencePct: confidencePctValue,
    reliabilityAvailable: reliabilityPctValue != null,
    confidenceAvailable: confidencePctValue != null,
  };
}

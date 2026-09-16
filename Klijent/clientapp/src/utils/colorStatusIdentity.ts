import type { AnalyticsRecommendation } from "../services/colorSalesStatsApi";
import {
  type CanonicalRecommendationStatus,
  isCanonicalRecommendationStatus,
  normalizeRecommendationPct,
} from "./canonicalRecommendationSemantics";

export const MISSING_COLOR_RECOMMENDATION_REASON =
  "Backend preporuka nije dostupna; lokalna heuristika se ne koristi kao odluka.";

export const UNKNOWN_COLOR_RECOMMENDATION_STATUS_REASON =
  "Status preporuke nije prepoznat; red ostaje informativan bez automatske preporuke.";

export type ColorRecommendationProjection = {
  status: CanonicalRecommendationStatus;
  recommendationAllowed: boolean;
  statusReason: string;
  reliabilityPct: number | null;
  confidencePct: number | null;
  reliabilityAvailable: boolean;
  confidenceAvailable: boolean;
};

export function mapColorBackendStatus(status?: string | null): CanonicalRecommendationStatus | null {
  return isCanonicalRecommendationStatus(status) ? status : null;
}

export function buildColorRecommendationProjection(
  recommendation?: AnalyticsRecommendation | null,
  rowReliabilityPct?: number | null,
): ColorRecommendationProjection {
  const recommendationMissing = recommendation == null;
  const mappedBackendStatus = mapColorBackendStatus(recommendation?.status);
  const hasSupportedBackendStatus = mappedBackendStatus != null;
  const status = mappedBackendStatus ?? "insufficient_data";
  const recommendationAllowed = hasSupportedBackendStatus && recommendation?.recommendationAllowed === true;
  const statusReason = recommendationMissing
    ? MISSING_COLOR_RECOMMENDATION_REASON
    : hasSupportedBackendStatus
      ? recommendation?.summary ?? MISSING_COLOR_RECOMMENDATION_REASON
      : UNKNOWN_COLOR_RECOMMENDATION_STATUS_REASON;
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

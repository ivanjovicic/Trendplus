import { describe, expect, it } from "vitest";
import {
  buildColorRecommendationProjection,
  mapColorBackendStatus,
  MISSING_COLOR_RECOMMENDATION_REASON,
  UNKNOWN_COLOR_RECOMMENDATION_STATUS_REASON,
} from "../colorStatusIdentity";

const canonicalStatuses = [
  "increase_focus",
  "maintain",
  "review",
  "do_not_trust",
  "insufficient_data",
] as const;

describe("mapColorBackendStatus", () => {
  it.each(canonicalStatuses)("preserves canonical backend status %s", (status) => {
    expect(mapColorBackendStatus(status)).toBe(status);
  });

  it("returns null for unknown backend codes", () => {
    expect(mapColorBackendStatus("review_hard")).toBeNull();
    expect(mapColorBackendStatus(null)).toBeNull();
    expect(mapColorBackendStatus(undefined)).toBeNull();
  });
});

describe("buildColorRecommendationProjection", () => {
  it.each(canonicalStatuses)(
    "keeps backend status %s visible when recommendationAllowed is false",
    (status) => {
      const projection = buildColorRecommendationProjection({
        status,
        label: "Review",
        summary: "Signal zahteva proveru.",
        confidencePct: 72,
        reliabilityPct: 68,
        dataQualityStatus: "warning",
        recommendationAllowed: false,
        reasonCodes: ["weak_signal"],
      });

      expect(projection.status).toBe(status);
      expect(projection.recommendationAllowed).toBe(false);
      expect(projection.statusReason).toBe("Backend je blokirao izvrsenje preporuke: Signal zahteva proveru.");
      expect(projection.confidencePct).toBeNull();
      expect(projection.reliabilityPct).toBeNull();
    },
  );

  it("uses a distinct reason when permission is missing but status is recognised", () => {
    const projection = buildColorRecommendationProjection({
      status: "do_not_trust",
      label: "Do not trust",
      summary: "Signal zahteva proveru izvora.",
      confidencePct: 40,
      reliabilityPct: 35,
      dataQualityStatus: "critical",
      reasonCodes: ["data_quality_critical"],
    });

    expect(projection.status).toBe("do_not_trust");
    expect(projection.recommendationAllowed).toBe(false);
    expect(projection.statusReason).toBe("Backend nije potvrdio da je preporuka izvrsna: Signal zahteva proveru izvora.");
  });

  it("fails closed for unknown status without exposing raw backend codes", () => {
    const projection = buildColorRecommendationProjection({
      status: "backend_future_status" as "review",
      label: "Review",
      summary: "Ovaj tekst ne sme postati status.",
      confidencePct: 72,
      reliabilityPct: 68,
      dataQualityStatus: "warning",
      recommendationAllowed: true,
      reasonCodes: [],
    });

    expect(projection.status).toBe("insufficient_data");
    expect(projection.recommendationAllowed).toBe(false);
    expect(projection.statusReason).toBe(UNKNOWN_COLOR_RECOMMENDATION_STATUS_REASON);
    expect(projection.confidencePct).toBeNull();
  });

  it("fails closed when recommendation payload is missing", () => {
    const projection = buildColorRecommendationProjection(undefined);

    expect(projection.status).toBe("insufficient_data");
    expect(projection.recommendationAllowed).toBe(false);
    expect(projection.statusReason).toBe(MISSING_COLOR_RECOMMENDATION_REASON);
  });

  it("keeps actionable confidence/reliability when recommendation is allowed", () => {
    const projection = buildColorRecommendationProjection({
      status: "increase_focus",
      label: "Increase focus",
      summary: "Signal je upotrebljiv.",
      confidencePct: 72,
      reliabilityPct: 68,
      dataQualityStatus: "good",
      recommendationAllowed: true,
      reasonCodes: [],
    });

    expect(projection.recommendationAllowed).toBe(true);
    expect(projection.statusReason).toBe("Signal je upotrebljiv.");
    expect(projection.confidencePct).toBe(72);
    expect(projection.reliabilityPct).toBe(68);
  });

  it("uses the missing-summary fallback only for recognised statuses without summary", () => {
    const projection = buildColorRecommendationProjection({
      status: "maintain",
      label: "Maintain",
      summary: undefined as unknown as string,
      confidencePct: 70,
      reliabilityPct: 65,
      dataQualityStatus: "good",
      recommendationAllowed: true,
      reasonCodes: [],
    });

    expect(projection.statusReason).toBe(MISSING_COLOR_RECOMMENDATION_REASON);
  });
});

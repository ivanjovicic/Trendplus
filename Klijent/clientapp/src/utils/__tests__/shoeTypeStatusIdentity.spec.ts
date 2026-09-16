import { describe, expect, it } from "vitest";
import {
  buildShoeTypeRecommendationProjection,
  mapShoeTypeBackendStatus,
  MISSING_SHOE_TYPE_RECOMMENDATION_REASON,
} from "../shoeTypeStatusIdentity";

const canonicalStatuses = [
  "increase_focus",
  "maintain",
  "review",
  "do_not_trust",
  "insufficient_data",
] as const;

describe("mapShoeTypeBackendStatus", () => {
  it.each(canonicalStatuses)("preserves canonical backend status %s", (status) => {
    expect(mapShoeTypeBackendStatus(status)).toBe(status);
  });

  it("fails closed to insufficient_data for unknown backend codes", () => {
    expect(mapShoeTypeBackendStatus("review_hard")).toBe("insufficient_data");
    expect(mapShoeTypeBackendStatus(null)).toBe("insufficient_data");
    expect(mapShoeTypeBackendStatus(undefined)).toBe("insufficient_data");
  });
});

describe("buildShoeTypeRecommendationProjection", () => {
  it.each(canonicalStatuses)(
    "keeps backend status %s visible when recommendationAllowed is false",
    (status) => {
      const projection = buildShoeTypeRecommendationProjection({
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
      expect(projection.statusReason).toBe("Automatska preporuka nije dozvoljena: Signal zahteva proveru.");
      expect(projection.confidencePct).toBeNull();
      expect(projection.reliabilityPct).toBeNull();
      expect(projection.confidenceAvailable).toBe(false);
      expect(projection.reliabilityAvailable).toBe(false);
    },
  );

  it.each(canonicalStatuses)(
    "keeps actionable confidence/reliability when recommendationAllowed is true for %s",
    (status) => {
      const projection = buildShoeTypeRecommendationProjection({
        status,
        label: "Review",
        summary: "Signal je upotrebljiv.",
        confidencePct: 72,
        reliabilityPct: 68,
        dataQualityStatus: "good",
        recommendationAllowed: true,
        reasonCodes: [],
      });

      expect(projection.status).toBe(status);
      expect(projection.recommendationAllowed).toBe(true);
      expect(projection.statusReason).toBe("Signal je upotrebljiv.");
      expect(projection.confidencePct).toBe(72);
      expect(projection.reliabilityPct).toBe(68);
      expect(projection.confidenceAvailable).toBe(true);
      expect(projection.reliabilityAvailable).toBe(true);
    },
  );

  it("treats missing recommendationAllowed as non-actionable without overwriting status", () => {
    const projection = buildShoeTypeRecommendationProjection({
      status: "do_not_trust",
      label: "Do not trust",
      summary: "Podaci nisu pouzdani.",
      confidencePct: 40,
      reliabilityPct: 35,
      dataQualityStatus: "critical",
      reasonCodes: ["data_quality_critical"],
    });

    expect(projection.status).toBe("do_not_trust");
    expect(projection.recommendationAllowed).toBe(false);
    expect(projection.confidencePct).toBeNull();
    expect(projection.reliabilityPct).toBeNull();
  });

  it("uses row reliability fallback only when recommendation is allowed", () => {
    const allowed = buildShoeTypeRecommendationProjection(
      {
        status: "review",
        label: "Review",
        summary: "Proveriti.",
        confidencePct: null,
        reliabilityPct: null,
        dataQualityStatus: "warning",
        recommendationAllowed: true,
        reasonCodes: [],
      },
      81,
    );
    const gated = buildShoeTypeRecommendationProjection(
      {
        status: "review",
        label: "Review",
        summary: "Proveriti.",
        confidencePct: null,
        reliabilityPct: null,
        dataQualityStatus: "warning",
        recommendationAllowed: false,
        reasonCodes: [],
      },
      81,
    );

    expect(allowed.reliabilityPct).toBe(81);
    expect(gated.reliabilityPct).toBeNull();
  });

  it("fails closed when recommendation payload is missing", () => {
    const projection = buildShoeTypeRecommendationProjection(undefined);

    expect(projection.status).toBe("insufficient_data");
    expect(projection.recommendationAllowed).toBe(false);
    expect(projection.statusReason).toBe(
      `Automatska preporuka nije dozvoljena: ${MISSING_SHOE_TYPE_RECOMMENDATION_REASON}`,
    );
    expect(projection.confidencePct).toBeNull();
    expect(projection.reliabilityPct).toBeNull();
  });
});

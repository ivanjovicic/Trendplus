import { describe, it, expect } from "vitest";
import {
  isCanonicalRecommendationStatus,
  recommendationStatusLabel,
  recommendationStatusTone,
  recommendationStatusTooltipBrief,
  recommendationReasonLabel,
  RECOMMENDATION_STATUS_PRIORITY,
} from "../canonicalRecommendationSemantics";

describe("canonicalrecommendationsemantics", () => {
  it("recognizes known statuses", () => {
    expect(isCanonicalRecommendationStatus("increase_focus")).toBe(true);
    expect(isCanonicalRecommendationStatus("unknown_status")).toBe(false);
  });

  it("returns labels and tones for statuses", () => {
    expect(recommendationStatusLabel("increase_focus")).toBe("Pojacaj");
    expect(recommendationStatusTone("maintain")).toBe("keep");
  });

  it("tooltip brief returns a non-empty string", () => {
    expect(recommendationStatusTooltipBrief("review")).toBeTruthy();
  });

  it("priority mapping is ordered", () => {
    expect(RECOMMENDATION_STATUS_PRIORITY.increase_focus).toBeGreaterThan(
      RECOMMENDATION_STATUS_PRIORITY.do_not_trust
    );
  });

  it("never exposes an unknown backend reason code", () => {
    expect(recommendationReasonLabel("unmapped_internal_reason")).toBe(
      "Dodatno ograničenje iz procene."
    );
    expect(recommendationReasonLabel("missing_cost")).toBe("Nedostaje nabavna cena.");
  });
});

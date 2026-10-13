import { describe, expect, it } from "vitest";
import { fmtPct } from "../analyticsFormatters";
import { buildCoverageTooltip, buildMarginDetailNote, buildRecommendationCaveat } from "../marginQuality";

describe("marginQuality unknown coverage", () => {
  it("renders unknown coverage shares as unavailable, not 0%", () => {
    const tooltip = buildCoverageTooltip(null, undefined, null, fmtPct);

    expect(tooltip).not.toMatch(/(^|[^\d])0,0%/);
    expect(tooltip).toContain("Istorijski trošak sa prodajne stavke: Nije dostupno");
    expect(tooltip).toContain("Bez troška: Nije dostupno");
  });

  it("keeps known zero coverage as 0%", () => {
    const tooltip = buildCoverageTooltip(100, 0, 0, fmtPct);

    expect(tooltip).toContain("Istorijski trošak sa prodajne stavke: 100,0%");
    expect(tooltip).toContain("Bez troška: 0,0%");
  });

  it("omits the fallback share from recommendation caveats when it is unknown", () => {
    const estimated = buildRecommendationCaveat("estimated", null, fmtPct);
    const partial = buildRecommendationCaveat("partial", undefined, fmtPct);

    expect(estimated).not.toMatch(/(^|[^\d])0,0%/);
    expect(estimated).toContain("fallback troška");
    expect(partial).not.toMatch(/(^|[^\d])0,0%/);
    expect(buildRecommendationCaveat("partial", 12.5, fmtPct)).toContain("12,5%");
  });

  it("does not claim zero historical or fallback coverage in margin detail notes when unknown", () => {
    const estimated = buildMarginDetailNote("estimated", null, null, fmtPct);
    const partial = buildMarginDetailNote("partial", 30, null, fmtPct);

    expect(estimated).not.toMatch(/(^|[^\d])0,0%/);
    expect(estimated).toContain("Marža je dominantno procenjena");
    expect(partial).not.toMatch(/(^|[^\d])0,0%/);
    expect(partial).toContain("30,0%");
    expect(buildMarginDetailNote("partial", 30, 70, fmtPct)).toContain("70,0% prometa ima istorijski trošak");
  });
});

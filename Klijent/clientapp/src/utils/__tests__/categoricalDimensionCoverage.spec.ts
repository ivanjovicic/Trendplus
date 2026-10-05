import { describe, expect, it } from "vitest";
import {
  DIMENSION_NOT_POPULATED_IN_SOURCE,
  dimensionNotPopulatedMessage,
  isDimensionNotPopulatedInSource,
} from "../categoricalDimensionCoverage";

describe("categoricalDimensionCoverage", () => {
  it("detects not populated coverage state", () => {
    expect(
      isDimensionNotPopulatedInSource({
        knownCoveragePct: 0,
        dimensionCoverageState: DIMENSION_NOT_POPULATED_IN_SOURCE,
      }),
    ).toBe(true);
  });

  it("builds Serbian empty-state copy", () => {
    expect(dimensionNotPopulatedMessage("Boja")).toContain("Boja nije popunjena u izvoru");
  });
});

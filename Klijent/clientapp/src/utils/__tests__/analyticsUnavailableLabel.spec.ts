import { describe, expect, it } from "vitest";
import { ANALYTICS_UNAVAILABLE_LABEL } from "../analyticsConstants";

describe("ANALYTICS_UNAVAILABLE_LABEL", () => {
  it("uses Serbian unavailable copy from the design system (not N/A)", () => {
    expect(ANALYTICS_UNAVAILABLE_LABEL).toBe("Nije dostupno");
  });
});

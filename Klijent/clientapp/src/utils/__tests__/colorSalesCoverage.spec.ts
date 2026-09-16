import { describe, expect, it } from "vitest";
import { resolveColorCoveragePct } from "../colorSalesCoverage";

describe("resolveColorCoveragePct", () => {
  it.each([
    [0, 8],
    [5, 8],
  ])("keeps valid measured coverage (%s / %s)", (numerator, denominator) => {
    expect(resolveColorCoveragePct(numerator, denominator)).toBe((numerator / denominator) * 100);
  });

  it.each([
    [12, 0],
    [12, -5],
    [12, Number.NaN],
    [-1, 10],
    [11, 10],
    [null, 10],
    [10, null],
  ])("rejects incompatible coverage evidence (%s / %s)", (numerator, denominator) => {
    expect(resolveColorCoveragePct(numerator, denominator)).toBeNull();
  });
});

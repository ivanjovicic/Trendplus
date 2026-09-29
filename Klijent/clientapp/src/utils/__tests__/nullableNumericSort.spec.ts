import { describe, expect, it } from "vitest";
import { compareNullableNumbers } from "../nullableNumericSort";

describe("compareNullableNumbers", () => {
  it("keeps unavailable values last in ascending and descending order", () => {
    expect(compareNullableNumbers(null, -25, "asc")).toBeGreaterThan(0);
    expect(compareNullableNumbers(null, -25, "desc")).toBeGreaterThan(0);
    expect(compareNullableNumbers(-25, null, "asc")).toBeLessThan(0);
    expect(compareNullableNumbers(-25, null, "desc")).toBeLessThan(0);
  });

  it("preserves legitimate negative values and direction", () => {
    expect(compareNullableNumbers(-100, -25, "asc")).toBeLessThan(0);
    expect(compareNullableNumbers(-100, -25, "desc")).toBeGreaterThan(0);
  });

  it("treats non-finite values as unavailable", () => {
    expect(compareNullableNumbers(Number.NaN, 0, "asc")).toBeGreaterThan(0);
    expect(compareNullableNumbers(Number.POSITIVE_INFINITY, 0, "desc")).toBeGreaterThan(0);
    expect(compareNullableNumbers(Number.NaN, Number.POSITIVE_INFINITY, "asc")).toBe(0);
  });
});

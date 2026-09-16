import { describe, expect, it } from "vitest";
import {
  resolveColorComplementPercent,
  resolveColorCountValue,
  resolveColorPartSharePct,
  resolveColorPercentValue,
  resolveColorRevenueSharePct,
} from "../colorPercentRange";

describe("resolveColorPercentValue", () => {
  it.each([0, 12.5, 100])("keeps valid measured percentages (%s)", (value) => {
    expect(resolveColorPercentValue(value)).toBe(value);
  });

  it.each([-1, 100.1, Number.NaN, Number.POSITIVE_INFINITY, null, undefined])(
    "fails closed for invalid percentages (%s)",
    (value) => {
      expect(resolveColorPercentValue(value)).toBeNull();
    },
  );
});

describe("resolveColorPartSharePct", () => {
  it("derives valid share percentages from compatible numerator and denominator", () => {
    expect(resolveColorPartSharePct(25, 100)).toBe(25);
    expect(resolveColorRevenueSharePct(120000, 200000)).toBe(60);
    expect(resolveColorPartSharePct(0, 12)).toBe(0);
  });

  it.each([
    [12, 0],
    [12, -5],
    [12, Number.NaN],
    [-1, 10],
    [11, 10],
    [null, 10],
    [10, null],
  ])("rejects incompatible share evidence (%s / %s)", (numerator, denominator) => {
    expect(resolveColorPartSharePct(numerator, denominator)).toBeNull();
  });
});

describe("resolveColorComplementPercent", () => {
  it("keeps valid complements inside the 0-100 range", () => {
    expect(resolveColorComplementPercent(20)).toBe(80);
    expect(resolveColorComplementPercent(0)).toBe(100);
    expect(resolveColorComplementPercent(100)).toBe(0);
  });

  it("fails closed when the source percentage is invalid", () => {
    expect(resolveColorComplementPercent(150)).toBeNull();
    expect(resolveColorComplementPercent(Number.NaN)).toBeNull();
  });
});

describe("resolveColorCountValue", () => {
  it.each([0, 3, 42])("keeps valid non-negative counts (%s)", (value) => {
    expect(resolveColorCountValue(value)).toBe(value);
  });

  it.each([-1, Number.NaN, Number.POSITIVE_INFINITY, null, undefined])(
    "fails closed for invalid counts (%s)",
    (value) => {
      expect(resolveColorCountValue(value)).toBeNull();
    },
  );
});

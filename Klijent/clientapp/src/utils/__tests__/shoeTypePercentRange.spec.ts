import { describe, expect, it } from "vitest";
import {
  resolveShoeTypeComplementPercent,
  resolveShoeTypePartSharePct,
  resolveShoeTypePercentValue,
  resolveShoeTypeQuantitySharePct,
  resolveShoeTypeRevenueSharePct,
} from "../shoeTypePercentRange";

describe("resolveShoeTypePercentValue", () => {
  it.each([0, 12.5, 100])("keeps valid measured percentages (%s)", (value) => {
    expect(resolveShoeTypePercentValue(value)).toBe(value);
  });

  it.each([-1, 100.1, Number.NaN, Number.POSITIVE_INFINITY, null, undefined])(
    "fails closed for invalid percentages (%s)",
    (value) => {
      expect(resolveShoeTypePercentValue(value)).toBeNull();
    },
  );
});

describe("resolveShoeTypePartSharePct", () => {
  it("derives valid share percentages from compatible numerator and denominator", () => {
    expect(resolveShoeTypePartSharePct(25, 100)).toBe(25);
    expect(resolveShoeTypeRevenueSharePct(120000, 200000)).toBe(60);
    expect(resolveShoeTypeQuantitySharePct(0, 12)).toBe(0);
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
    expect(resolveShoeTypePartSharePct(numerator, denominator)).toBeNull();
  });
});

describe("resolveShoeTypeComplementPercent", () => {
  it("keeps valid complements inside the 0-100 range", () => {
    expect(resolveShoeTypeComplementPercent(20)).toBe(80);
    expect(resolveShoeTypeComplementPercent(0)).toBe(100);
    expect(resolveShoeTypeComplementPercent(100)).toBe(0);
  });

  it("fails closed when the source percentage is invalid", () => {
    expect(resolveShoeTypeComplementPercent(150)).toBeNull();
    expect(resolveShoeTypeComplementPercent(Number.NaN)).toBeNull();
  });
});

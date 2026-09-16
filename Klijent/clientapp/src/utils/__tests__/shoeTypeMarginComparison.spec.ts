import { describe, expect, it } from "vitest";
import {
  buildShoeTypeComparisonData,
  formatShoeTypeMarginContributionShare,
  resolveShoeTypeMarginContributionSharePct,
} from "../shoeTypeMarginComparison";

const rows = [
  {
    tipObuceNaziv: "Patike",
    ukupanPromet: 120000,
    marginContribution: -600,
    sharePct: 60,
  },
  {
    tipObuceNaziv: "Čizme",
    ukupanPromet: 80000,
    marginContribution: -400,
    sharePct: 40,
  },
];

describe("shoeTypeMarginComparison", () => {
  it("keeps negative totals comparable and preserves margin share percentages", () => {
    expect(resolveShoeTypeMarginContributionSharePct(-600, -1000)).toBe(60);
    expect(buildShoeTypeComparisonData(rows, -1000)).toEqual([
      { name: "Patike", udeoPrometa: 60, udeoMarznogDoprinosa: 60 },
      { name: "Čizme", udeoPrometa: 40, udeoMarznogDoprinosa: 40 },
    ]);
  });

  it("keeps measured zero totals visible while leaving non-zero margin shares unavailable", () => {
    expect(resolveShoeTypeMarginContributionSharePct(0, 0)).toBe(0);
    expect(resolveShoeTypeMarginContributionSharePct(100, 0)).toBeNull();
    expect(buildShoeTypeComparisonData(rows, 0)).toEqual([
      { name: "Patike", udeoPrometa: 60, udeoMarznogDoprinosa: null },
      { name: "Čizme", udeoPrometa: 40, udeoMarznogDoprinosa: null },
    ]);
  });

  it("suppresses comparison only for missing or non-finite totals", () => {
    expect(buildShoeTypeComparisonData(rows, null)).toEqual([]);
    expect(buildShoeTypeComparisonData(rows, Number.NaN)).toEqual([]);
    expect(buildShoeTypeComparisonData(rows, Number.POSITIVE_INFINITY)).toEqual([]);
    expect(resolveShoeTypeMarginContributionSharePct(100, null)).toBeNull();
  });

  it("formats measured zero share distinctly from unavailable evidence", () => {
    const formatPct = (value: number, digits = 2) => `${value.toFixed(digits)}%`;
    expect(formatShoeTypeMarginContributionShare(0, 0, formatPct)).toBe("0.00%");
    expect(formatShoeTypeMarginContributionShare(100, 0, formatPct)).toBe("Nije dostupno");
    expect(formatShoeTypeMarginContributionShare(-600, -1000, formatPct)).toBe("60.00%");
  });
});

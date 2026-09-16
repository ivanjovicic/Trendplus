import { describe, expect, it } from "vitest";
import {
  buildShoeTypeMarginComparisonProjection,
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
  it("keeps negative totals comparable for detail formatting only", () => {
    expect(resolveShoeTypeMarginContributionSharePct(-600, -1000)).toBe(60);
  });

  it("keeps measured zero totals visible while leaving non-zero margin shares unavailable", () => {
    expect(resolveShoeTypeMarginContributionSharePct(0, 0)).toBe(0);
    expect(resolveShoeTypeMarginContributionSharePct(100, 0)).toBeNull();
  });

  it("suppresses comparison only for missing or non-finite totals", () => {
    expect(buildShoeTypeMarginComparisonProjection(rows, null).mode).toBe("unavailable");
    expect(buildShoeTypeMarginComparisonProjection(rows, Number.NaN).mode).toBe("unavailable");
    expect(buildShoeTypeMarginComparisonProjection(rows, Number.POSITIVE_INFINITY).mode).toBe("unavailable");
    expect(resolveShoeTypeMarginContributionSharePct(100, null)).toBeNull();
  });

  it("formats measured zero share distinctly from unavailable evidence", () => {
    const formatPct = (value: number, digits = 2) => `${value.toFixed(digits)}%`;
    expect(formatShoeTypeMarginContributionShare(0, 0, formatPct)).toBe("0.00%");
    expect(formatShoeTypeMarginContributionShare(100, 0, formatPct)).toBe("Nije dostupno");
    expect(formatShoeTypeMarginContributionShare(-600, -1000, formatPct)).toBe("60.00%");
  });

  it("projects share-mode chart data when total margin is positive", () => {
    expect(buildShoeTypeMarginComparisonProjection(rows, 1000)).toEqual({
      mode: "share",
      data: [
        {
          name: "Patike",
          udeoPrometa: 60,
          udeoMarznogDoprinosa: -60,
          marginContributionRsd: -600,
        },
        {
          name: "Čizme",
          udeoPrometa: 40,
          udeoMarznogDoprinosa: -40,
          marginContributionRsd: -400,
        },
      ],
    });
  });

  it("projects value-mode chart data when total margin is zero or negative", () => {
    expect(buildShoeTypeMarginComparisonProjection(rows, 0)).toEqual({
      mode: "value",
      data: [
        {
          name: "Patike",
          udeoPrometa: 60,
          udeoMarznogDoprinosa: null,
          marginContributionRsd: -600,
        },
        {
          name: "Čizme",
          udeoPrometa: 40,
          udeoMarznogDoprinosa: null,
          marginContributionRsd: -400,
        },
      ],
    });
  });

  it("fails closed on invalid share percentages in chart projection", () => {
    const invalidShareRows = [
      {
        tipObuceNaziv: "Patike",
        ukupanPromet: 120000,
        marginContribution: 600,
        sharePct: 150,
      },
    ];

    expect(buildShoeTypeMarginComparisonProjection(invalidShareRows, 1000, (value) => (
      typeof value === "number" && value >= 0 && value <= 100
    ))).toEqual({
      mode: "unavailable",
      data: [],
    });
  });
});

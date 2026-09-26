import { describe, expect, it } from "vitest";
import {
  classifyDailyShiftSummary,
  hasMissingShiftSummary,
  hasPartialShiftSummary,
  resolveShiftChartValue,
  summarizeShiftItems,
  sumShiftColumn,
} from "../dailyShiftSummary";
import type { DailySalesRow } from "../../services/dailySalesStatsApi";

function row(overrides: Partial<DailySalesRow> = {}): DailySalesRow {
  return {
    date: "2026-07-01",
    firstShiftTotalItems: 0,
    secondShiftTotalItems: 0,
    totalRevenue: 0,
    topSupplierCounts: [0],
    othersCount: 0,
    totalItemsSold: 0,
    ...overrides,
  };
}

describe("dailyShiftSummary", () => {
  it("classifies complete, missing, partial and unavailable shift evidence", () => {
    expect(classifyDailyShiftSummary(row({ totalItemsSold: 10, firstShiftTotalItems: 6, secondShiftTotalItems: 4 }))).toBe("complete");
    expect(classifyDailyShiftSummary(row({ totalItemsSold: 10, firstShiftTotalItems: 0, secondShiftTotalItems: 0 }))).toBe("missing");
    expect(classifyDailyShiftSummary(row({ totalItemsSold: 10, firstShiftTotalItems: null, secondShiftTotalItems: null }))).toBe("missing");
    expect(classifyDailyShiftSummary(row({ totalItemsSold: 10, firstShiftTotalItems: 5, secondShiftTotalItems: null }))).toBe("partial");
    expect(classifyDailyShiftSummary(row({ totalItemsSold: 10, firstShiftTotalItems: null, secondShiftTotalItems: 0 }))).toBe("partial");
  });

  it("keeps measured zero visible while unknown shifts stay unavailable in charts", () => {
    const partial = row({ totalItemsSold: 12, firstShiftTotalItems: 0, secondShiftTotalItems: 12 });

    expect(hasPartialShiftSummary(partial)).toBe(false);
    expect(hasMissingShiftSummary(row({ totalItemsSold: 12, firstShiftTotalItems: 0, secondShiftTotalItems: 0 }))).toBe(true);
    expect(resolveShiftChartValue(partial, "first")).toBe(0);
    expect(resolveShiftChartValue(row({ totalItemsSold: 12, firstShiftTotalItems: null, secondShiftTotalItems: 4 }), "first")).toBeNull();
  });

  it("keeps negative return quantities as measured signed shift evidence", () => {
    const returns = row({
      firstShiftTotalItems: -3,
      secondShiftTotalItems: -2,
      totalItemsSold: -5,
    });

    expect(classifyDailyShiftSummary(returns)).toBe("complete");
    expect(resolveShiftChartValue(returns, "first")).toBe(-3);
    expect(summarizeShiftItems([returns], "second")).toEqual({ value: -2, state: "complete" });
  });

  it("sums available shift values while marking partial period evidence", () => {
    const rows = [
      row({ totalItemsSold: 10, firstShiftTotalItems: 4, secondShiftTotalItems: 6 }),
      row({ totalItemsSold: 8, firstShiftTotalItems: 3, secondShiftTotalItems: null }),
      row({ totalItemsSold: 5, firstShiftTotalItems: 0, secondShiftTotalItems: 0 }),
    ];

    expect(sumShiftColumn(rows, "firstShiftTotalItems")).toEqual({ sum: 7, isPartial: true });
    expect(sumShiftColumn(rows, "secondShiftTotalItems")).toEqual({ sum: 6, isPartial: true });
    expect(summarizeShiftItems(rows, "second")).toEqual({ value: 6, state: "partial" });
  });

  it("treats no-time fallback assignment as unavailable shift shares", () => {
    const fallbackRow = row({
      totalItemsSold: 10,
      firstShiftTotalItems: null,
      secondShiftTotalItems: null,
    });

    expect(classifyDailyShiftSummary(fallbackRow, "no_time_fallback")).toBe("unavailable");
    expect(resolveShiftChartValue(fallbackRow, "first", "no_time_fallback")).toBeNull();
    expect(summarizeShiftItems([fallbackRow], "first", "no_time_fallback")).toEqual({
      value: null,
      state: "unavailable",
    });
    expect(classifyDailyShiftSummary(
      row({ totalItemsSold: 10, firstShiftTotalItems: 6, secondShiftTotalItems: 4 }),
      "partial",
    )).toBe("partial");
  });
});

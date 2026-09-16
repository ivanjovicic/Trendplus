import { describe, expect, it } from "vitest";
import {
  classifyDailyShiftSummary,
  hasMissingShiftSummary,
  hasPartialShiftSummary,
  periodHasIncompleteShiftEvidence,
  resolveShiftChartValue,
  resolveShiftDisplayValue,
  resolveShiftExportValue,
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
    expect(classifyDailyShiftSummary(row({ totalItemsSold: 0, firstShiftTotalItems: null, secondShiftTotalItems: null }))).toBe("unavailable");
    expect(classifyDailyShiftSummary(row({ totalItemsSold: null, firstShiftTotalItems: 5, secondShiftTotalItems: 5 }))).toBe("unavailable");
  });

  it("keeps measured zero visible while unknown shifts stay unavailable", () => {
    const partial = row({ totalItemsSold: 12, firstShiftTotalItems: 0, secondShiftTotalItems: 12 });
    const missing = row({ totalItemsSold: 12, firstShiftTotalItems: 0, secondShiftTotalItems: 0 });

    expect(hasPartialShiftSummary(partial)).toBe(false);
    expect(hasMissingShiftSummary(missing)).toBe(true);
    expect(resolveShiftDisplayValue(partial, "first", (value) => String(value))).toBe("0");
    expect(resolveShiftDisplayValue(partial, "second", (value) => String(value))).toBe("12");
    expect(resolveShiftDisplayValue(row({ totalItemsSold: 12, firstShiftTotalItems: null, secondShiftTotalItems: 4 }), "first", () => "x")).toBe("N/A");
    expect(resolveShiftExportValue(missing, "first", "____")).toBe("____");
    expect(resolveShiftChartValue(partial, "first")).toBe(0);
    expect(resolveShiftChartValue(missing, "first")).toBeNull();
  });

  it("sums available shift values while marking partial period evidence", () => {
    const rows = [
      row({ totalItemsSold: 10, firstShiftTotalItems: 4, secondShiftTotalItems: 6 }),
      row({ totalItemsSold: 8, firstShiftTotalItems: 3, secondShiftTotalItems: null }),
      row({ totalItemsSold: 5, firstShiftTotalItems: 0, secondShiftTotalItems: 0 }),
    ];

    expect(sumShiftColumn(rows, "firstShiftTotalItems")).toEqual({ sum: 7, isPartial: true });
    expect(sumShiftColumn(rows, "secondShiftTotalItems")).toEqual({ sum: 6, isPartial: true });
    expect(periodHasIncompleteShiftEvidence(rows)).toBe(true);
  });

  it("rejects non-finite shift values as absent evidence", () => {
    const partial = row({
      totalItemsSold: 9,
      firstShiftTotalItems: Number.NaN,
      secondShiftTotalItems: 4,
    });

    expect(classifyDailyShiftSummary(partial)).toBe("partial");
    expect(resolveShiftChartValue(partial, "first")).toBeNull();
    expect(resolveShiftChartValue(partial, "second")).toBe(4);
  });
});

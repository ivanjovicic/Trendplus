import { describe, expect, it } from "vitest";
import { createAnalyticsDatasetProjections } from "../analyticsDatasetProjections";

describe("analyticsDatasetProjections", () => {
  it("keeps table sorting separate from chronological chart and global datasets", () => {
    const canonicalRows = [{ id: 1 }, { id: 2 }, { id: 3 }];
    const tableRows = [canonicalRows[2], canonicalRows[0]];
    const chartRows = [canonicalRows[0], canonicalRows[1], canonicalRows[2]];
    const globalTotals = { total: 3 };
    const globalFacets = { categories: ["A", "B"] };

    const projections = createAnalyticsDatasetProjections({
      canonicalRows,
      filteredRows: tableRows,
      tableRows,
      chronologicalChartRows: chartRows,
      globalTotals,
      globalFacets,
      pageRows: tableRows,
    });

    expect(projections.tableRows).toEqual(tableRows);
    expect(projections.chronologicalChartRows).toEqual(chartRows);
    expect(projections.globalTotals).toBe(globalTotals);
    expect(projections.globalFacets).toBe(globalFacets);
    expect(projections.pageRows).toEqual(tableRows);
  });

  it("does not infer global totals or facets from page rows", () => {
    const pageRows = [{ id: 1 }];
    const projections = createAnalyticsDatasetProjections({
      canonicalRows: pageRows,
      pageRows,
      globalTotals: { total: 42 },
      globalFacets: { categories: ["A", "B"] },
    });

    expect(projections.globalTotals).toEqual({ total: 42 });
    expect(projections.globalFacets).toEqual({ categories: ["A", "B"] });
    expect(projections.globalTotals).not.toEqual({ total: pageRows.length });
  });
});

export type AnalyticsProjectionRows<T> = readonly T[];

export interface AnalyticsDatasetProjections<T, TGlobalTotals = unknown, TGlobalFacets = unknown> {
  canonicalRows: AnalyticsProjectionRows<T>;
  filteredRows: AnalyticsProjectionRows<T>;
  tableRows: AnalyticsProjectionRows<T>;
  chronologicalChartRows: AnalyticsProjectionRows<T>;
  exportRows: AnalyticsProjectionRows<T>;
  detailRows: AnalyticsProjectionRows<T>;
  pageRows: AnalyticsProjectionRows<T>;
  globalTotals: TGlobalTotals | null;
  globalFacets: TGlobalFacets | null;
}

export interface CreateAnalyticsDatasetProjectionsInput<T, TGlobalTotals, TGlobalFacets> {
  canonicalRows: AnalyticsProjectionRows<T>;
  filteredRows?: AnalyticsProjectionRows<T>;
  tableRows?: AnalyticsProjectionRows<T>;
  chronologicalChartRows?: AnalyticsProjectionRows<T>;
  exportRows?: AnalyticsProjectionRows<T>;
  detailRows?: AnalyticsProjectionRows<T>;
  pageRows?: AnalyticsProjectionRows<T>;
  globalTotals?: TGlobalTotals | null;
  globalFacets?: TGlobalFacets | null;
}

/**
 * Names every consumer projection so a sorted/paginated table array cannot be
 * mistaken for a chronological chart, export, detail, or global dataset.
 */
export function createAnalyticsDatasetProjections<T, TGlobalTotals, TGlobalFacets>(
  input: CreateAnalyticsDatasetProjectionsInput<T, TGlobalTotals, TGlobalFacets>,
): AnalyticsDatasetProjections<T, TGlobalTotals, TGlobalFacets> {
  const canonicalRows = input.canonicalRows;
  const filteredRows = input.filteredRows ?? canonicalRows;
  const tableRows = input.tableRows ?? filteredRows;

  return {
    canonicalRows,
    filteredRows,
    tableRows,
    chronologicalChartRows: input.chronologicalChartRows ?? canonicalRows,
    exportRows: input.exportRows ?? tableRows,
    detailRows: input.detailRows ?? filteredRows,
    pageRows: input.pageRows ?? canonicalRows,
    globalTotals: input.globalTotals ?? null,
    globalFacets: input.globalFacets ?? null,
  };
}

export type SupplierDecisionReportQuery = {
  fromDate?: string | null;
  toDate?: string | null;
  scope?: string | null;
  dataScope?: string | null;
  category?: string | null;
  gender?: string | null;
  seasonId?: number | null;
  minRevenue?: number | null;
  onlyHighConfidence?: boolean | null;
  excludeOosBeforeMarkdown?: boolean | null;
  supplierId?: number | null;
  storeId?: number | null;
  section?: string | null;
};

function appendIfPresent(params: URLSearchParams, key: string, value: string | number | boolean | null | undefined) {
  if (value !== null && value !== undefined) {
    params.set(key, String(value));
  }
}

/**
 * Canonical durable supplier-report query contract. Null/undefined means absent;
 * an explicitly supplied empty string, zero or false remains explicit.
 */
export function appendSupplierDecisionReportQuery(params: URLSearchParams, query: SupplierDecisionReportQuery) {
  appendIfPresent(params, "fromDate", query.fromDate);
  appendIfPresent(params, "toDate", query.toDate);
  appendIfPresent(params, "scope", query.scope);
  appendIfPresent(params, "dataScope", query.dataScope);
  appendIfPresent(params, "category", query.category);
  appendIfPresent(params, "gender", query.gender);
  appendIfPresent(params, "seasonId", query.seasonId);
  appendIfPresent(params, "minRevenue", query.minRevenue);
  appendIfPresent(params, "onlyHighConfidence", query.onlyHighConfidence);
  appendIfPresent(params, "excludeOosBeforeMarkdown", query.excludeOosBeforeMarkdown);
  appendIfPresent(params, "supplierId", query.supplierId);
  appendIfPresent(params, "storeId", query.storeId);
  appendIfPresent(params, "section", query.section);
}

export function buildSupplierDecisionReportHref(query: SupplierDecisionReportQuery): string {
  const params = new URLSearchParams();
  appendSupplierDecisionReportQuery(params, query);
  return `/analytics/supplier/report?${params.toString()}`;
}

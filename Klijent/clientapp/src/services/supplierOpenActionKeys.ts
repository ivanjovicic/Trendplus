import { getAnalyticsActions } from "./analyticsApi";
import type { AnalyticsActionFilters, AnalyticsActionStatus } from "../types/analytics";

export const SUPPLIER_OPEN_ACTION_PAGE_SIZE = 200;

/** Safety cap: 200 * 50 = 10k open actions per status before stopping pagination. */
export const SUPPLIER_OPEN_ACTION_MAX_PAGES = 50;

export type FetchAnalyticsActionsPage = (
  filters: AnalyticsActionFilters,
) => ReturnType<typeof getAnalyticsActions>;

export async function loadOpenSupplierActionSourceKeys(
  statuses: AnalyticsActionStatus[],
  fetchPage: FetchAnalyticsActionsPage = getAnalyticsActions,
): Promise<Set<string>> {
  const keys = new Set<string>();
  const baseFilter: AnalyticsActionFilters = {
    sourceType: "supplier",
    pageSize: SUPPLIER_OPEN_ACTION_PAGE_SIZE,
  };

  for (const status of statuses) {
    let page = 1;
    let totalPages = 1;

    while (page <= totalPages && page <= SUPPLIER_OPEN_ACTION_MAX_PAGES) {
      const response = await fetchPage({ ...baseFilter, status, page });
      for (const item of response.items) {
        if (item.sourceKey) keys.add(item.sourceKey);
      }
      totalPages = Math.max(1, response.totalPages ?? 1);
      page += 1;
    }
  }

  return keys;
}

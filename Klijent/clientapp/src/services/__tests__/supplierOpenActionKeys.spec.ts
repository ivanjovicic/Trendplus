import { describe, expect, it, vi } from "vitest";
import {
  SUPPLIER_OPEN_ACTION_MAX_PAGES,
  SUPPLIER_OPEN_ACTION_PAGE_SIZE,
  loadOpenSupplierActionSourceKeys,
} from "../supplierOpenActionKeys";
import type { AnalyticsActionListResponse } from "../../types/analytics";

function pagedResponse(
  page: number,
  pageSize: number,
  totalCount: number,
  sourceKeys: string[],
): AnalyticsActionListResponse {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  return {
    items: sourceKeys.map((sourceKey) => ({ sourceKey } as AnalyticsActionListResponse["items"][number])),
    totalCount,
    page,
    pageSize,
    totalPages,
  };
}

describe("loadOpenSupplierActionSourceKeys (RQ48)", () => {
  it("pages through all results for each open status", async () => {
    const fetchPage = vi.fn(async (filters) => {
      if (filters.status === "new") {
        if (filters.page === 1) {
          return pagedResponse(1, SUPPLIER_OPEN_ACTION_PAGE_SIZE, 250, ["new-page-1"]);
        }
        return pagedResponse(2, SUPPLIER_OPEN_ACTION_PAGE_SIZE, 250, ["new-page-2"]);
      }
      if (filters.status === "accepted") {
        return pagedResponse(1, SUPPLIER_OPEN_ACTION_PAGE_SIZE, 1, ["accepted-1"]);
      }
      return pagedResponse(1, SUPPLIER_OPEN_ACTION_PAGE_SIZE, 0, []);
    });

    const keys = await loadOpenSupplierActionSourceKeys(["new", "accepted", "deferred"], fetchPage);

    expect(keys).toEqual(new Set(["new-page-1", "new-page-2", "accepted-1"]));
    expect(fetchPage).toHaveBeenCalledWith(expect.objectContaining({
      sourceType: "supplier",
      status: "new",
      page: 1,
      pageSize: SUPPLIER_OPEN_ACTION_PAGE_SIZE,
    }));
    expect(fetchPage).toHaveBeenCalledWith(expect.objectContaining({ status: "new", page: 2 }));
    expect(fetchPage).toHaveBeenCalledTimes(4);
  });

  it("stops after the configured max pages per status", async () => {
    const fetchPage = vi.fn(async (filters) => {
      return pagedResponse(filters.page ?? 1, SUPPLIER_OPEN_ACTION_PAGE_SIZE, 1_000_000, [`p${filters.page}`]);
    });

    const keys = await loadOpenSupplierActionSourceKeys(["new"], fetchPage);

    expect(fetchPage).toHaveBeenCalledTimes(SUPPLIER_OPEN_ACTION_MAX_PAGES);
    expect(keys.size).toBe(SUPPLIER_OPEN_ACTION_MAX_PAGES);
  });
});

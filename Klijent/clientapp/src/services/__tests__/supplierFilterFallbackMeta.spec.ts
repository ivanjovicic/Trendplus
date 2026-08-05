import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { getSupplierFilters } from "../analyticsApi";

describe("supplier filter fallback meta", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("keeps supplier filter arrays usable while exposing fallback metadata from headers", async () => {
    const fetchMock = vi.fn((_input: RequestInfo | URL, _init?: RequestInit) =>
      Promise.resolve(
        new Response(
          JSON.stringify([
            { supplierId: 101, supplierName: "Dobavljac A" },
            { supplierId: 202, supplierName: "Dobavljac B" },
          ]),
          {
            status: 200,
            headers: {
              "Content-Type": "application/json",
              "X-Analytics-Fallback": "true",
              "X-Analytics-Fallback-Code": "supplier_filters_timeout",
              "X-Analytics-Fallback-Reason": "Filteri dobavljaca trenutno koriste pomocni signal.",
            },
          }
        )
      )
    );

    vi.stubGlobal("fetch", fetchMock);

    const result = await getSupplierFilters("2026-06-01T00:00:00Z", "2026-07-01T00:00:00Z", true, 7);

    expect(fetchMock).toHaveBeenCalled();
    expect(Array.isArray(result)).toBe(true);
    expect(result).toHaveLength(2);
    expect(result[0].supplierName).toBe("Dobavljac A");
    expect("meta" in result).toBe(true);
    expect(Object.keys(result)).not.toContain("meta");
    expect(result.meta).toEqual(
      expect.objectContaining({
        success: true,
        warningCode: "supplier_filters_timeout",
        warningMessage: "Filteri dobavljaca trenutno koriste pomocni signal.",
        dataQualityStatus: "warning",
        isPartial: true,
      }),
    );
  });
});

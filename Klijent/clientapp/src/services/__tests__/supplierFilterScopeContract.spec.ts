import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { getSupplierFilters } from "../analyticsApi";
import { getDataScopeStorageKey } from "../../utils/dataScope";

describe("supplier filter scope contract", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    localStorage.clear();
  });

  it("uses explicit dataScope instead of ambient local storage when provided", async () => {
    localStorage.setItem(getDataScopeStorageKey(), "existing");

    const fetchMock = vi.fn((_input: RequestInfo | URL) =>
      Promise.resolve(
        new Response(JSON.stringify([]), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      ),
    );
    vi.stubGlobal("fetch", fetchMock);

    await getSupplierFilters("2026-06-01", "2026-07-01", true, 7, "imported");

    const requestUrl = new URL(String(fetchMock.mock.calls[0]?.[0]), "http://localhost");
    expect(requestUrl.searchParams.get("dataScope")).toBe("imported");
    expect(requestUrl.searchParams.get("storeId")).toBe("7");
  });

  it("falls back to ambient local storage when explicit dataScope is omitted", async () => {
    localStorage.setItem(getDataScopeStorageKey(), "imported");

    const fetchMock = vi.fn((_input: RequestInfo | URL) =>
      Promise.resolve(
        new Response(JSON.stringify([]), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      ),
    );
    vi.stubGlobal("fetch", fetchMock);

    await getSupplierFilters("2026-06-01", "2026-07-01", true, null);

    const requestUrl = new URL(String(fetchMock.mock.calls[0]?.[0]), "http://localhost");
    expect(requestUrl.searchParams.get("dataScope")).toBe("imported");
  });

  it("normalizes invalid explicit scope values to all", async () => {
    const fetchMock = vi.fn((_input: RequestInfo | URL) =>
      Promise.resolve(
        new Response(JSON.stringify([]), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      ),
    );
    vi.stubGlobal("fetch", fetchMock);

    await getSupplierFilters(undefined, undefined, true, undefined, "unexpected");

    const requestUrl = new URL(String(fetchMock.mock.calls[0]?.[0]), "http://localhost");
    expect(requestUrl.searchParams.get("dataScope")).toBe("all");
  });
});

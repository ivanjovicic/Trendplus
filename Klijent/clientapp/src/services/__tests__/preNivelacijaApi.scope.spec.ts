import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { getPreNivelacijaPrioriteti } from "../preNivelacijaApi";
import { getDataScopeStorageKey } from "../../utils/dataScope";

const responseBody = JSON.stringify({ meta: { success: true, dataQualityStatus: "good" } });

describe("pre-nivelacija API scope contract", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    localStorage.clear();
  });

  it("uses the explicit scope for the request even when ambient storage differs", async () => {
    localStorage.setItem(getDataScopeStorageKey(), "existing");
    const fetchMock = vi.fn(() => Promise.resolve(new Response(responseBody, { status: 200 })));
    vi.stubGlobal("fetch", fetchMock);

    await getPreNivelacijaPrioriteti({ dataScope: "imported", page: 1, pageSize: 60 });

    const requestUrl = new URL(String(fetchMock.mock.calls[0]?.[0]), "http://localhost");
    expect(requestUrl.searchParams.get("dataScope")).toBe("imported");
  });

  it("normalizes invalid explicit scope values to all", async () => {
    const fetchMock = vi.fn(() => Promise.resolve(new Response(responseBody, { status: 200 })));
    vi.stubGlobal("fetch", fetchMock);

    await getPreNivelacijaPrioriteti({ dataScope: "invalid" });

    const requestUrl = new URL(String(fetchMock.mock.calls[0]?.[0]), "http://localhost");
    expect(requestUrl.searchParams.get("dataScope")).toBe("all");
  });
});

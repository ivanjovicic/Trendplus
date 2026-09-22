import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { getPreNivelacijaPrioriteti, PreNivelacijaApiError } from "../preNivelacijaApi";
import { getDataScopeStorageKey } from "../../utils/dataScope";

const responseBody = JSON.stringify({
  generatedAtUtc: "2026-07-01T08:00:00Z",
  formulaVersion: "test",
  formulaDescription: "Test empty response",
  summary: {
    supplierCount: 0,
    candidatesCount: 0,
    highPriorityCount: 0,
    increaseFocusCount: 0,
    maintainCount: 0,
    reviewCount: 0,
    doNotTrustCount: 0,
    insufficientDataCount: 0,
    totalStockAtRisk: 0,
    estimatedAvoidableMarkdownLoss: 0,
    expectedHighlightRevenueUplift: 0,
    averagePreNivelacijaScore: 0,
  },
  supplierLeaderboard: [],
  candidates: [],
  queues: { highlightNow: [], monitor: [], likelyMarkdownSoon: [] },
  alerts: [],
  page: 1,
  pageSize: 20,
  totalCandidates: 0,
  meta: { success: true, dataQualityStatus: "good" },
});

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

  it("returns non-empty guidance for an empty response body", async () => {
    vi.stubGlobal("fetch", vi.fn(() => Promise.resolve(new Response("", { status: 503 }))));

    await expect(getPreNivelacijaPrioriteti({})).rejects.toMatchObject({
      name: "PreNivelacijaApiError",
      message: "Pre-nivelacija prioriteti trenutno nisu dostupni. Proverite status osvežavanja i pokušajte ponovo.",
    });
  });

  it("keeps safe JSON guidance and correlation metadata without leaking the backend code", async () => {
    vi.stubGlobal("fetch", vi.fn(() => Promise.resolve(new Response(JSON.stringify({
      detail: "Servis je privremeno nedostupan.",
      errorCode: "PRE_NIVELACIJA_BACKEND_TIMEOUT",
      correlationId: "corr-295",
    }), { status: 504, headers: { "content-type": "application/problem+json" } }))));

    const failure = await getPreNivelacijaPrioriteti({}).catch((reason) => reason as PreNivelacijaApiError);
    expect(failure.message).toBe("Servis je privremeno nedostupan.");
    expect(failure.message).not.toContain("PRE_NIVELACIJA_BACKEND_TIMEOUT");
    expect(failure.correlationId).toBe("corr-295");
  });

  it("suppresses raw HTML and network/timeout messages", async () => {
    vi.stubGlobal("fetch", vi.fn(() => Promise.resolve(new Response("<html><body>proxy secret</body></html>", {
      status: 502,
      headers: { "content-type": "text/html" },
    }))));
    await expect(getPreNivelacijaPrioriteti({})).rejects.toMatchObject({
      message: "Pre-nivelacija prioriteti trenutno nisu dostupni. Proverite status osvežavanja i pokušajte ponovo.",
    });

    vi.stubGlobal("fetch", vi.fn(() => Promise.reject(new Error("timeout ECONNREFUSED"))));
    await expect(getPreNivelacijaPrioriteti({})).rejects.toMatchObject({
      message: "Pre-nivelacija prioriteti trenutno nisu dostupni. Proverite status osvežavanja i pokušajte ponovo.",
    });
  });

  it("suppresses non-JSON technical response bodies", async () => {
    vi.stubGlobal("fetch", vi.fn(() => Promise.resolve(new Response("database connection failed", {
      status: 503,
      headers: { "content-type": "text/plain" },
    }))));

    await expect(getPreNivelacijaPrioriteti({})).rejects.toMatchObject({
      message: "Pre-nivelacija prioriteti trenutno nisu dostupni. Proverite status osvežavanja i pokušajte ponovo.",
    });
  });
});

import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiHttpError } from "../analyticsHttp";
import { AnalyticsResponseValidationError } from "../../validation/analyticsResponseValidation";
import {
  getVendorSalesNivelacija,
  getVendorSalesNivelacijaOptions,
} from "../vendorSalesNivelacijaApi";

function validResponse(overrides: Record<string, unknown> = {}) {
  return {
    generatedAt: "2026-09-22T08:00:00Z",
    windowDays: 30,
    vendorId: null,
    eventDate: null,
    from: null,
    to: null,
    category: null,
    includeInactive: false,
    storeId: null,
    dataScope: "all",
    scopeApplied: true,
    categories: [],
    vendorStats: [],
    articleStats: [],
    totals: {
      preQty: 0,
      preRevenue: 0,
      postQty: 0,
      postRevenue: 0,
      changeQty: 0,
      changeRevenue: 0,
      changePercent: 0,
      vendorsCount: 0,
      articlesCount: 0,
      activeArticlesCount: 0,
      avgRevenuePerArticlePre: 0,
      avgRevenuePerArticlePost: 0,
      avgPriceChangePercent: 0,
      absoluteChangeRevenue: 0,
      avgCoveragePre30: null,
      avgCoveragePost30: null,
      hasComparableSalesWindow: false,
      comparableRows: 0,
      comparableArticlesCount: 0,
      comparableVendorsCount: 0,
    },
    dataQuality: null,
    categoryStats: [],
    priceDirectionStats: [],
    insights: [],
    avgMomentumRevenue: null,
    avgElasticity: null,
    avgDidRevenue: null,
    avgLostSalesOOS: null,
    oosRate: null,
    metricsStatus: null,
    recommendationAllowed: false,
    meta: { success: true },
    ...overrides,
  };
}

describe("vendor sales nivelacija scope contract", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("forwards store and data scope and accepts only matching backend provenance", async () => {
    const fetchMock = vi.fn(async () => new Response(JSON.stringify({
      ...validResponse({ storeId: 7, dataScope: "existing" }),
    }), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    await expect(getVendorSalesNivelacija({ storeId: 7, dataScope: " EXISTING " }))
      .resolves.toEqual(expect.objectContaining({ storeId: 7, dataScope: "existing", scopeApplied: true }));

    const requestUrl = String(fetchMock.mock.calls[0]?.[0]);
    expect(requestUrl).toContain("storeId=7");
    expect(requestUrl).toContain("dataScope=+EXISTING+");
  });

  it("rejects a response that would relabel unscoped data as scoped", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify({
      ...validResponse(),
    }), { status: 200 })));

    await expect(getVendorSalesNivelacija({ storeId: 7, dataScope: "existing" }))
      .rejects.toThrow("nije potvrdila traženi objekat i opseg podataka");
  });

  it("rejects malformed success payloads before page derivation", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify(
      validResponse({ totals: undefined }),
    ), { status: 200 })));

    await expect(getVendorSalesNivelacija({}))
      .rejects.toBeInstanceOf(AnalyticsResponseValidationError);
  });

  it("rejects non-finite numeric evidence instead of sorting it as a real value", async () => {
    // JSON cannot carry NaN, so exercise the runtime boundary with the parsed
    // object shape used by the fetch mock instead.
    const response = new Response(null, { status: 200 });
    vi.stubGlobal("fetch", vi.fn(async () => ({
      ok: true,
      json: async () => validResponse({ totals: { ...validResponse().totals, changeRevenue: Number.NaN } }),
      headers: response.headers,
      status: 200,
    } as Response)));

    await expect(getVendorSalesNivelacija({}))
      .rejects.toBeInstanceOf(AnalyticsResponseValidationError);
  });

  it("maps provider details to a safe traceable HTTP error", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(
      "Npgsql.PostgresException: relation vw_vendor_sales_nivelacija does not exist",
      { status: 500, headers: { "content-type": "text/plain" } },
    )));

    const failure = await getVendorSalesNivelacija({}).catch((error: unknown) => error);
    expect(failure).toBeInstanceOf(ApiHttpError);
    expect((failure as ApiHttpError).status).toBe(500);
    expect((failure as Error).message).not.toContain("Npgsql");
    expect((failure as Error).message).toContain("Pre/post nivelacija podaci trenutno nisu dostupni.");
  });

  it("keeps the backend correlation ID when a problem response is JSON", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify({
      title: "Pre/post nivelacija nije dostupna.",
      detail: "Npgsql.PostgresException: relation is missing",
      errorCode: "vendor_sales_nivelacija_unavailable",
      correlationId: "corr-rq387",
    }), { status: 503, headers: { "content-type": "application/problem+json" } })));

    const failure = await getVendorSalesNivelacija({}).catch((error: unknown) => error);
    expect(failure).toBeInstanceOf(ApiHttpError);
    expect((failure as ApiHttpError).status).toBe(503);
    expect((failure as ApiHttpError).correlationId).toBe("corr-rq387");
    expect((failure as Error).message).not.toContain("Npgsql");
    expect((failure as Error).message).toContain("corr-rq387");
  });

  it("validates options instead of treating malformed rows as an empty success", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify([
      { eventDate: "2026-09-22T00:00:00Z", eventsCount: null, vendorsCount: 0, articlesCount: 0, activeArticlesCount: 0, hasSalesWindow: false, label: "" },
    ]), { status: 200 })));

    await expect(getVendorSalesNivelacijaOptions({}))
      .rejects.toBeInstanceOf(AnalyticsResponseValidationError);
  });
});

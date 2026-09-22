import { afterEach, describe, expect, it, vi } from "vitest";
import { getVendorSalesNivelacija } from "../vendorSalesNivelacijaApi";

describe("vendor sales nivelacija scope contract", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("forwards store and data scope and accepts only matching backend provenance", async () => {
    const fetchMock = vi.fn(async () => new Response(JSON.stringify({
      storeId: 7,
      dataScope: "existing",
      scopeApplied: true,
      meta: { success: true },
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
      storeId: null,
      dataScope: "all",
      scopeApplied: true,
      meta: { success: true },
    }), { status: 200 })));

    await expect(getVendorSalesNivelacija({ storeId: 7, dataScope: "existing" }))
      .rejects.toThrow("nije potvrdila traženi objekat i opseg podataka");
  });
});

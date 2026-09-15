import { afterEach, describe, expect, it, vi } from "vitest";
import { exportInventoryReport, previewInventoryReport } from "../analyticsApi";

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("inventory export API contract", () => {
  it("sends explicit dataScope in export and preview request bodies", async () => {
    vi.spyOn(window, "prompt").mockReturnValue("secret-admin-key");
    const bodies: unknown[] = [];
    const fetchMock = vi.fn().mockImplementation(async (_url: string, init?: RequestInit) => {
      bodies.push(JSON.parse(String(init?.body ?? "{}")));
      return {
        ok: true,
        json: async () => ({
          documentId: "doc-1",
          status: "completed",
          isAsync: false,
          createdAtUtc: "2026-09-15T00:00:00Z",
        }),
      };
    });
    vi.stubGlobal("fetch", fetchMock);

    await exportInventoryReport({
      format: "pdf",
      search: "nike",
      storeId: 2,
      supplierId: 5,
      sortBy: "naziv",
      dataScope: "imported",
    });
    await previewInventoryReport({
      search: "nike",
      storeId: 2,
      supplierId: 5,
      sortBy: "naziv",
      dataScope: "imported",
    });

    expect(bodies).toHaveLength(2);
    expect(bodies[0]).toMatchObject({
      format: "pdf",
      search: "nike",
      storeId: 2,
      supplierId: 5,
      sortBy: "naziv",
      dataScope: "imported",
    });
    expect(bodies[1]).toMatchObject({
      search: "nike",
      storeId: 2,
      supplierId: 5,
      sortBy: "naziv",
      dataScope: "imported",
    });
  });
});

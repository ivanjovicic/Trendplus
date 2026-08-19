import { afterEach, describe, expect, it, vi } from "vitest";
import { generateExport } from "../exportApi";

vi.mock("../../utils/apiUrl", () => ({
  apiUrl: (path: string) => path,
}));

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

const payload = {
  tableKey: "sales",
  tableTitle: "Sales",
  columns: [],
  rows: [],
  filters: [],
  metadata: [],
};

describe("exportApi admin headers", () => {
  it("sends the admin key when generating an export", async () => {
    vi.spyOn(window, "prompt").mockReturnValue("secret-admin-key");
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ documentId: "d1", status: "completed", isAsync: false, createdAtUtc: "2026-08-13T00:00:00Z" }),
    });
    vi.stubGlobal("fetch", fetchMock);

    await generateExport(payload as never, {
      format: "csv",
      orientation: "landscape",
      includeFiltersAndMetadata: true,
    });

    expect(fetchMock).toHaveBeenCalledWith(
      "/api/documents/generate",
      expect.objectContaining({
        method: "POST",
        headers: expect.objectContaining({ "X-Admin-Key": "secret-admin-key" }),
      })
    );
  });

  it("maps 401 to a missing-key message", async () => {
    vi.spyOn(window, "prompt").mockReturnValue("secret-admin-key");
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
      json: async () => ({}),
    }));

    await expect(generateExport(payload as never, {
      format: "csv",
      orientation: "landscape",
      includeFiltersAndMetadata: true,
    })).rejects.toThrow("Nedostaje admin key za izvoz dokumenata.");
  });
});

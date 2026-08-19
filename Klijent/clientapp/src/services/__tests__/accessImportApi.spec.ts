import { afterEach, describe, expect, it, vi } from "vitest";
import { cancelAccessImportBatch } from "../accessImportApi";
import { getRestoreScript } from "../accessImportRestoreApi";

vi.mock("../../utils/apiUrl", () => ({
  apiUrl: (path: string) => path,
}));

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("access import admin headers", () => {
  it("sends the admin key when cancelling a batch", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ batchId: 42, status: "cancellation-requested" }),
    });
    vi.stubGlobal("fetch", fetchMock);

    await cancelAccessImportBatch(42, "secret-admin-key");

    expect(fetchMock).toHaveBeenCalledWith("/api/access-import/batches/42/cancel", {
      method: "POST",
      headers: { "X-Admin-Key": "secret-admin-key" },
    });
  });

  it("sends the admin key when generating a restore script", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ script: "INSERT INTO ..." }),
    });
    vi.stubGlobal("fetch", fetchMock);

    const script = await getRestoreScript([12, 13], "secret-admin-key");

    expect(script).toBe("INSERT INTO ...");
    expect(fetchMock).toHaveBeenCalledWith("/api/access-import/cleanup/archive/restore-script", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-Admin-Key": "secret-admin-key",
      },
      body: JSON.stringify({ ids: [12, 13] }),
    });
  });
});

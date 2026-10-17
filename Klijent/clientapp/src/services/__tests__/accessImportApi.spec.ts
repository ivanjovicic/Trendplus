import { afterEach, describe, expect, it, vi } from "vitest";
import { cancelAccessImportBatch, runAccessImport } from "../accessImportApi";
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

  it("sends a trimmed source namespace with a routine import request", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ batchId: 42, status: "queued" }),
    });
    vi.stubGlobal("fetch", fetchMock);

    await runAccessImport(new File(["access"], "store.accdb"), {
      sourceNamespace: "  store-logical-uuid  ",
      overwriteExisting: false,
      adminKey: "secret-admin-key",
    });

    const [, options] = fetchMock.mock.calls[0];
    const body = options.body as FormData;
    expect(body.get("sourceNamespace")).toBe("store-logical-uuid");
    expect(body.get("overwriteExisting")).toBe("false");
    expect(options.headers).toEqual({ "X-Admin-Key": "secret-admin-key" });
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

import { afterEach, describe, expect, it, vi } from "vitest";
import { getLogById, getLogs } from "../logsApi";

vi.mock("../../utils/apiUrl", () => ({
  apiUrl: (path: string) => path,
}));

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("logsApi admin headers", () => {
  it("sends the admin key when listing logs", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ logs: [], totalCount: 0, pageNumber: 1, pageSize: 100 }),
    });
    vi.stubGlobal("fetch", fetchMock);

    await getLogs(1, 100, undefined, undefined, undefined, undefined, "secret-admin-key");

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining("/api/logs?pageNumber=1&pageSize=100"),
      expect.objectContaining({
        headers: { "X-Admin-Key": "secret-admin-key" },
      })
    );
  });

  it("sends the admin key when loading a log by id", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ id: 12, timestamp: "2026-08-13T00:00:00Z", level: "Error", message: "x" }),
    });
    vi.stubGlobal("fetch", fetchMock);

    await getLogById(12, "secret-admin-key");

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining("/api/logs/12"),
      expect.objectContaining({
        headers: { "X-Admin-Key": "secret-admin-key" },
      })
    );
  });

  it("maps 401 to a missing-key message", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
      text: async () => "",
    }));

    await expect(getLogs(1, 100)).rejects.toThrow("Nedostaje admin key za pregled logova.");
  });

  it("maps 403 to an invalid-key message", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: false,
      status: 403,
      text: async () => "",
    }));

    await expect(getLogById(12, "wrong")).rejects.toThrow("Admin key nije ispravan za pregled logova.");
  });
});

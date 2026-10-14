import { afterEach, describe, expect, it, vi } from "vitest";
import { findActiveAccessImportBatch, getAccessImportBatches, type AccessImportBatchDto } from "../accessImportApi";

vi.mock("../../utils/apiUrl", () => ({
  apiUrl: (path: string) => path,
}));

afterEach(() => {
  vi.unstubAllGlobals();
});

function batch(overrides: Partial<AccessImportBatchDto>): AccessImportBatchDto {
  return {
    id: 1, sourceSystem: "access", sourceFileName: "a.accdb", queuedAtUtc: "2026-10-14T10:00:00Z", startedAtUtc: "2026-10-14T10:00:00Z",
    completedAtUtc: null, status: "completed", summaryJson: null, errorMessage: null, durationSeconds: null,
    totalImported: 0, totalUpdated: 0, totalErrors: 0, dataOrigin: "access",
    ...overrides,
  };
}

const now = Date.parse("2026-10-14T10:05:00Z");

describe("findActiveAccessImportBatch", () => {
  it("returns null when every batch is terminal", () => {
    expect(findActiveAccessImportBatch([batch({ id: 1 }), batch({ id: 2, status: "failed" })], now)).toBeNull();
  });

  it("prefers the running batch over a queued one and is case-insensitive", () => {
    const result = findActiveAccessImportBatch([
      batch({ id: 3, status: "pending", startedAtUtc: "2026-10-14T10:04:00Z" }),
      batch({ id: 2, status: "Running", lastHeartbeatUtc: "2026-10-14T10:04:30Z" }),
    ], now);
    expect(result?.batch.id).toBe(2);
    expect(result?.stale).toBe(false);
  });

  it("ignores a running row that already has a completion time", () => {
    expect(findActiveAccessImportBatch([batch({ id: 4, status: "running", completedAtUtc: "2026-10-14T10:03:00Z" })], now)).toBeNull();
  });

  it("flags a running batch without a recent heartbeat as stale", () => {
    const result = findActiveAccessImportBatch([batch({ id: 5, status: "running", lastHeartbeatUtc: "2026-10-14T09:40:00Z" })], now);
    expect(result?.stale).toBe(true);
  });

  it("never flags a queued batch as stale", () => {
    const result = findActiveAccessImportBatch([batch({ id: 6, status: "pending", startedAtUtc: "2026-10-14T08:00:00Z" })], now);
    expect(result).toMatchObject({ stale: false });
  });
});

describe("getAccessImportBatches request sharing", () => {
  it("does not reuse a keyless in-flight request for a call that carries the admin key", async () => {
    let resolveFirst: (value: unknown) => void = () => undefined;
    const fetchMock = vi.fn()
      .mockImplementationOnce(() => new Promise((resolve) => { resolveFirst = resolve; }))
      .mockResolvedValueOnce({ ok: true, json: async () => [] });
    vi.stubGlobal("fetch", fetchMock);

    const first = getAccessImportBatches(50, "mount");
    const second = getAccessImportBatches(50, "active-run-check", "secret");
    await second;
    resolveFirst({ ok: true, json: async () => [] });
    await first;

    expect(fetchMock).toHaveBeenCalledTimes(2);
  });
});

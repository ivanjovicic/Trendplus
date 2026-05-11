import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { workerApi } from "../workerApi";

describe("workerApi", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  const mockFetch = (status: number, data: unknown) => {
    global.fetch = vi.fn(() =>
      Promise.resolve({
        ok: status >= 200 && status < 300,
        status,
        statusText: status === 200 ? "OK" : "Error",
        json: () => Promise.resolve(data),
      } as Response)
    );
  };

  it("getWorkersConfiguration calls new configuration endpoint", async () => {
    mockFetch(200, {
      processType: "worker",
      workersEnabledGlobally: true,
      runtimeToggleAllowed: true,
      total: 2,
      workers: [
        { workerName: "AccessImportBackgroundWorker", displayName: "Access Import" },
        { workerName: "SyncWorker", displayName: "Analytics Sync" },
      ],
    });

    const result = await workerApi.getWorkersConfiguration();

    expect(global.fetch).toHaveBeenCalledWith("/api/workers/configuration");
    expect(result.total).toBe(2);
    expect(result.workers).toHaveLength(2);
  });

  it("startWorker posts to worker start endpoint", async () => {
    mockFetch(200, { success: true, message: "started" });

    const result = await workerApi.startWorker("Worker Name 123");

    expect(global.fetch).toHaveBeenCalledWith("/api/workers/Worker%20Name%20123/start", {
      method: "POST",
    });
    expect(result.success).toBe(true);
  });

  it("stopWorker posts to worker stop endpoint", async () => {
    mockFetch(200, { success: true, message: "stopped" });

    const result = await workerApi.stopWorker("SyncWorker");

    expect(global.fetch).toHaveBeenCalledWith("/api/workers/SyncWorker/stop", {
      method: "POST",
    });
    expect(result.success).toBe(true);
  });

  it("restartWorker posts to worker restart endpoint", async () => {
    mockFetch(200, { success: true, message: "restarted" });

    const result = await workerApi.restartWorker("SyncWorker");

    expect(global.fetch).toHaveBeenCalledWith("/api/workers/SyncWorker/restart", {
      method: "POST",
    });
    expect(result.success).toBe(true);
  });

  it("enableSchedule posts to schedule enable endpoint", async () => {
    mockFetch(200, { success: true, message: "schedule enabled" });

    const result = await workerApi.enableSchedule("SyncWorker");

    expect(global.fetch).toHaveBeenCalledWith("/api/workers/SyncWorker/schedule/enable", {
      method: "POST",
    });
    expect(result.success).toBe(true);
  });

  it("disableSchedule posts to schedule disable endpoint", async () => {
    mockFetch(200, { success: true, message: "schedule disabled" });

    const result = await workerApi.disableSchedule("SyncWorker");

    expect(global.fetch).toHaveBeenCalledWith("/api/workers/SyncWorker/schedule/disable", {
      method: "POST",
    });
    expect(result.success).toBe(true);
  });

  it("throws when configuration request fails", async () => {
    mockFetch(500, { error: "Server error" });

    await expect(workerApi.getWorkersConfiguration()).rejects.toThrow();
  });

  it("throws when start request fails and includes body error detail", async () => {
    mockFetch(401, { error: "Unauthorized" });

    await expect(workerApi.startWorker("SyncWorker")).rejects.toThrow("Unauthorized");
  });
});

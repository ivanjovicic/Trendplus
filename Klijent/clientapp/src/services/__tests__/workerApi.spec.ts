import { describe, it, expect, beforeEach, vi, afterEach } from "vitest";
import { workerApi } from "../../../services/workerApi";

describe("workerApi", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  const mockFetch = (status: number, data: any) => {
    global.fetch = vi.fn(() =>
      Promise.resolve({
        ok: status >= 200 && status < 300,
        status,
        statusText: status === 200 ? "OK" : "Error",
        json: () => Promise.resolve(data),
      } as Response)
    );
  };

  it("getWorkersList calls correct endpoint", async () => {
    const mockData = {
      workers: [
        {
          workerName: "TestWorker",
          runtimeStatus: "Running",
          isScheduleEnabled: true,
          isManuallyStopped: false,
        },
      ],
      total: 1,
    };

    mockFetch(200, mockData);

    const result = await workerApi.getWorkersList();

    expect(global.fetch).toHaveBeenCalledWith("/api/admin/workers/list");
    expect(result.total).toBe(1);
    expect(result.workers.length).toBe(1);
  });

  it("getWorkerDetails calls correct endpoint with worker name", async () => {
    const mockWorker = {
      workerName: "TestWorker",
      runtimeStatus: "Running",
      isScheduleEnabled: true,
      isManuallyStopped: false,
      errorCount: 0,
    };

    mockFetch(200, mockWorker);

    const result = await workerApi.getWorkerDetails("TestWorker");

    expect(global.fetch).toHaveBeenCalledWith(
      "/api/admin/workers/TestWorker"
    );
    expect(result.workerName).toBe("TestWorker");
  });

  it("stopWorker sends POST request", async () => {
    const mockResponse = {
      success: true,
      message: "Worker stopped",
    };

    mockFetch(200, mockResponse);

    const result = await workerApi.stopWorker("TestWorker");

    expect(global.fetch).toHaveBeenCalledWith(
      "/api/admin/workers/TestWorker/stop",
      { method: "POST" }
    );
    expect(result.success).toBe(true);
  });

  it("resumeWorker sends POST request", async () => {
    const mockResponse = {
      success: true,
      message: "Worker resumed",
    };

    mockFetch(200, mockResponse);

    const result = await workerApi.resumeWorker("TestWorker");

    expect(global.fetch).toHaveBeenCalledWith(
      "/api/admin/workers/TestWorker/resume",
      { method: "POST" }
    );
    expect(result.success).toBe(true);
  });

  it("enableSchedule sends POST request", async () => {
    const mockResponse = {
      success: true,
      message: "Schedule enabled",
    };

    mockFetch(200, mockResponse);

    const result = await workerApi.enableSchedule("TestWorker");

    expect(global.fetch).toHaveBeenCalledWith(
      "/api/admin/workers/TestWorker/schedule/enable",
      { method: "POST" }
    );
    expect(result.success).toBe(true);
  });

  it("disableSchedule sends POST request", async () => {
    const mockResponse = {
      success: true,
      message: "Schedule disabled",
    };

    mockFetch(200, mockResponse);

    const result = await workerApi.disableSchedule("TestWorker");

    expect(global.fetch).toHaveBeenCalledWith(
      "/api/admin/workers/TestWorker/schedule/disable",
      { method: "POST" }
    );
    expect(result.success).toBe(true);
  });

  it("handles special characters in worker name", async () => {
    const mockWorker = { workerName: "Worker-Name_123" };
    mockFetch(200, mockWorker);

    await workerApi.getWorkerDetails("Worker-Name_123");

    expect(global.fetch).toHaveBeenCalledWith(
      "/api/admin/workers/Worker-Name_123"
    );
  });

  it("throws error when response is not ok", async () => {
    mockFetch(500, { error: "Server error" });

    await expect(workerApi.getWorkersList()).rejects.toThrow(
      "Failed to fetch workers list"
    );
  });

  it("throws error when stop worker fails", async () => {
    mockFetch(400, { error: "Bad request" });

    await expect(workerApi.stopWorker("TestWorker")).rejects.toThrow(
      "Failed to stop worker"
    );
  });

  it("throws error when resume worker fails", async () => {
    mockFetch(500, { error: "Server error" });

    await expect(workerApi.resumeWorker("TestWorker")).rejects.toThrow(
      "Failed to resume worker"
    );
  });

  it("throws error when enable schedule fails", async () => {
    mockFetch(400, { error: "Bad request" });

    await expect(workerApi.enableSchedule("TestWorker")).rejects.toThrow(
      "Failed to enable schedule"
    );
  });

  it("throws error when disable schedule fails", async () => {
    mockFetch(403, { error: "Forbidden" });

    await expect(workerApi.disableSchedule("TestWorker")).rejects.toThrow(
      "Failed to disable schedule"
    );
  });

  it("returns parsed JSON response", async () => {
    const mockData = {
      workers: [
        {
          workerName: "Worker1",
          runtimeStatus: "Running",
          errorCount: 0,
        },
        {
          workerName: "Worker2",
          runtimeStatus: "Stopped",
          errorCount: 2,
        },
      ],
      total: 2,
    };

    mockFetch(200, mockData);

    const result = await workerApi.getWorkersList();

    expect(result).toEqual(mockData);
    expect(result.workers).toHaveLength(2);
  });
});

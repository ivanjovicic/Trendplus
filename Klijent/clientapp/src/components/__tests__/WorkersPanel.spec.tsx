import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { WorkersPanel } from "../WorkersPanel";
import * as workerApiModule from "../../services/workerApi";
import * as analyticsApiModule from "../../services/analyticsApi";

vi.mock("../../services/workerApi", () => ({
  workerApi: {
    getWorkersConfiguration: vi.fn(),
    startWorker: vi.fn(),
    stopWorker: vi.fn(),
    restartWorker: vi.fn(),
    enableSchedule: vi.fn(),
    disableSchedule: vi.fn(),
  },
}));

vi.mock("../../services/analyticsApi", () => ({
  getAnalyticsRefreshStatus: vi.fn(),
  getAnalyticsCacheStatus: vi.fn(),
  clearAnalyticsCache: vi.fn(),
}));

describe("WorkersPanel", () => {
  const mockResponse = {
    processType: "worker",
    workersEnabledGlobally: true,
    runtimeToggleAllowed: true,
    total: 2,
    workers: [
      {
        workerName: "AccessImportBackgroundWorker",
        displayName: "Access Import",
        description: "Queue processor",
        workerType: "import",
        isRuntimeControllable: true,
        isScheduleControllable: true,
        runtimeControlReason: null,
        scheduleControlReason: null,
        status: "Running",
        scheduleEnabled: true,
        isManuallyStopped: false,
        isRegisteredInCurrentProcess: true,
        isConfiguredButNotRunning: false,
        lastHeartbeat: "2026-05-07T10:00:00Z",
        lastRunAt: "2026-05-07T10:00:00Z",
        nextRunAt: "2026-05-07T10:05:00Z",
        lastSuccessAt: "2026-05-07T10:00:00Z",
        lastFailureAt: null,
        lastError: null,
      },
      {
        workerName: "ReadinessWarmupHostedService",
        displayName: "Readiness Warmup",
        description: "Startup warmup",
        workerType: "startup",
        isRuntimeControllable: false,
        isScheduleControllable: false,
        runtimeControlReason: "Startup-only service.",
        scheduleControlReason: "Startup-only service.",
        status: "ConfiguredButNotRunning",
        scheduleEnabled: false,
        isManuallyStopped: false,
        isRegisteredInCurrentProcess: false,
        isConfiguredButNotRunning: true,
        lastHeartbeat: null,
        lastRunAt: null,
        nextRunAt: null,
        lastSuccessAt: null,
        lastFailureAt: null,
        lastError: null,
      },
    ],
  };

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(workerApiModule.workerApi.getWorkersConfiguration).mockResolvedValue(mockResponse as any);
    vi.mocked(workerApiModule.workerApi.startWorker).mockResolvedValue({
      success: true,
      message: "Pokrenut",
    });
    vi.mocked(workerApiModule.workerApi.stopWorker).mockResolvedValue({
      success: true,
      message: "Zaustavljen",
    });
    vi.mocked(workerApiModule.workerApi.restartWorker).mockResolvedValue({
      success: true,
      message: "Restartovan",
    });
    vi.mocked(workerApiModule.workerApi.enableSchedule).mockResolvedValue({
      success: true,
      message: "Omogucen raspored",
    });
    vi.mocked(workerApiModule.workerApi.disableSchedule).mockResolvedValue({
      success: true,
      message: "Onemogucen raspored",
    });
    vi.mocked(analyticsApiModule.getAnalyticsRefreshStatus).mockResolvedValue({
      lastSuccessfulRefreshAtUtc: null,
      lastAttemptAtUtc: null,
      lastFailureAtUtc: null,
      isRunning: false,
      lastErrorMessage: null,
      currentStep: null,
      refreshedObjects: [],
      failedObjects: [],
      durationSeconds: null,
      dataFreshnessStatus: "unknown",
      processMode: "worker",
      processType: "worker",
      workersEnabled: true,
      workerWarning: null,
      workerProcessWarning: null,
      generatedAtUtc: new Date().toISOString(),
      jobs: [],
      recentRuns: [],
    } as any);
    vi.mocked(analyticsApiModule.getAnalyticsCacheStatus).mockResolvedValue({
      provider: "Memory",
      redisAvailable: false,
      redisEnabled: false,
      isShared: false,
      isDistributed: false,
      cacheMode: "in-memory",
      environment: "Development",
      cacheType: "In-Memory only",
      message: "Cache nije distribuiran",
      warning: null,
      lastClearAtUtc: null,
      lastClearFamily: null,
      lastAnalyticsCacheClearAtUtc: null,
      lastReportCacheClearAtUtc: null,
      reportCacheVersion: 1,
      clearStateStorage: "memory",
    } as any);
    vi.mocked(analyticsApiModule.clearAnalyticsCache).mockResolvedValue({
      success: true,
      message: "Analytics cache i report cache su očišćeni.",
      lastClearAtUtc: new Date().toISOString(),
      lastClearFamily: "all",
      lastAnalyticsCacheClearAtUtc: new Date().toISOString(),
      lastReportCacheClearAtUtc: new Date().toISOString(),
      reportCacheVersion: 2,
      isShared: false,
      warning: null,
      storage: "memory",
    } as any);
  });

  it("renders all workers returned by backend", async () => {
    render(<WorkersPanel />);

    expect(await screen.findByText("Access Import")).toBeInTheDocument();
    expect(await screen.findByText("Readiness Warmup")).toBeInTheDocument();
    expect(screen.getByText("2 radnik(a)")).toBeInTheDocument();
    expect(screen.getByText("Auto-osvezavanje svaki 5s.")).toBeInTheDocument();
  });

  it("uses Serbian labels and keeps one refresh button", async () => {
    render(<WorkersPanel />);

    await screen.findByText("Access Import");

    expect(screen.getByText("Naziv")).toBeInTheDocument();
    expect(screen.getByText("Status")).toBeInTheDocument();
    expect(screen.getByText("Raspored")).toBeInTheDocument();
    expect(screen.getByText("Heartbeat")).toBeInTheDocument();
    expect(screen.getByText("Poslednje pokretanje")).toBeInTheDocument();
    expect(screen.getByText("Sledece pokretanje")).toBeInTheDocument();
    expect(screen.getByText("Greska")).toBeInTheDocument();
    expect(screen.getByText("Akcije")).toBeInTheDocument();

    expect(screen.getAllByRole("button", { name: "Osvezi" })).toHaveLength(1);
    expect(screen.getByRole("button", { name: "Očisti analytics cache" })).toBeInTheDocument();
    expect(screen.queryByText("Worker Management")).not.toBeInTheDocument();
    expect(screen.queryByText("Refresh")).not.toBeInTheDocument();
  });

  it("disables unsupported runtime actions", async () => {
    render(<WorkersPanel />);

    const workerNameCell = await screen.findByText("ReadinessWarmupHostedService");
    const row = workerNameCell.closest("tr");
    expect(row).not.toBeNull();

    const startButton = within(row as HTMLElement).getByRole("button", { name: "Pokreni odmah" });
    expect(startButton).toBeDisabled();
    expect(startButton).toHaveAttribute("title", "Startup-only service.");
  });

  it("starts runtime-controllable worker", async () => {
    render(<WorkersPanel />);

    const workerNameCell = await screen.findByText("AccessImportBackgroundWorker");
    const row = workerNameCell.closest("tr");
    expect(row).not.toBeNull();

    const startButton = within(row as HTMLElement).getByRole("button", { name: "Pokreni odmah" });
    fireEvent.click(startButton);

    await waitFor(() => {
      expect(workerApiModule.workerApi.startWorker).toHaveBeenCalledWith("AccessImportBackgroundWorker");
    });
  });

  it("renders durable analytics refresh history details", async () => {
    vi.mocked(analyticsApiModule.getAnalyticsRefreshStatus).mockResolvedValueOnce({
      lastSuccessfulRefreshAtUtc: null,
      lastAttemptAtUtc: null,
      lastFailureAtUtc: null,
      isRunning: false,
      lastErrorMessage: null,
      currentStep: null,
      refreshedObjects: [],
      failedObjects: [],
      durationSeconds: null,
      dataFreshnessStatus: "unknown",
      processMode: "worker",
      processType: "worker",
      workersEnabled: true,
      workerWarning: null,
      workerProcessWarning: null,
      generatedAtUtc: new Date().toISOString(),
      jobs: [],
      recentRuns: [
        {
          id: 42,
          jobKey: "nightly_analytics_refresh",
          jobName: "Nightly analytics refresh",
          status: "partial",
          startedAtUtc: "2026-05-25T05:00:00Z",
          finishedAtUtc: "2026-05-25T05:04:00Z",
          durationSeconds: 240,
          refreshedObjects: ["sales_facts_mv"],
          failedObjects: ["mv_inventory_recommendations"],
          errorCode: "partial_refresh",
          errorMessage: "Jedan view nije osvezen.",
          correlationId: "corr-123",
          triggeredBy: "nightly",
          processMode: "worker",
          workerName: "NightlyAnalyticsRefreshWorker",
          createdAtUtc: "2026-05-25T05:00:00Z",
        },
      ],
    } as any);

    render(<WorkersPanel />);

    expect(await screen.findByText("Istorija analytics osvezavanja")).toBeInTheDocument();
    expect(await screen.findByText("Početak")).toBeInTheDocument();
    expect(await screen.findByText("Završetak")).toBeInTheDocument();
    expect(await screen.findByText("Osveženi objekti")).toBeInTheDocument();
    expect(await screen.findByText("Neuspešni objekti")).toBeInTheDocument();
    expect(await screen.findByText("sales_facts_mv")).toBeInTheDocument();
    expect(await screen.findByText("mv_inventory_recommendations")).toBeInTheDocument();
  });
});

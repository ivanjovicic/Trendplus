import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { WorkersPanel } from "../WorkersPanel";
import * as workerApiModule from "../../services/workerApi";

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
      message: "Omogućen raspored",
    });
    vi.mocked(workerApiModule.workerApi.disableSchedule).mockResolvedValue({
      success: true,
      message: "Onemogućen raspored",
    });
  });

  it("renders all workers returned by backend", async () => {
    render(<WorkersPanel />);

    expect(await screen.findByText("Access Import")).toBeInTheDocument();
    expect(await screen.findByText("Readiness Warmup")).toBeInTheDocument();
    expect(screen.getByText("2 radnik(a)")).toBeInTheDocument();
    expect(screen.getByText("Auto-osvežavanje svaki 5s.")).toBeInTheDocument();
  });

  it("uses Serbian labels and keeps one refresh button", async () => {
    render(<WorkersPanel />);

    await screen.findByText("Access Import");

    expect(screen.getByText("Naziv")).toBeInTheDocument();
    expect(screen.getByText("Status")).toBeInTheDocument();
    expect(screen.getByText("Raspored")).toBeInTheDocument();
    expect(screen.getByText("Heartbeat")).toBeInTheDocument();
    expect(screen.getByText("Poslednje pokretanje")).toBeInTheDocument();
    expect(screen.getByText("Sledeće pokretanje")).toBeInTheDocument();
    expect(screen.getByText("Greška")).toBeInTheDocument();
    expect(screen.getByText("Akcije")).toBeInTheDocument();

    expect(screen.getAllByRole("button", { name: "Osveži" })).toHaveLength(1);
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
});

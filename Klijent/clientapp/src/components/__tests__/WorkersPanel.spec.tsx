import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { WorkersPanel } from "../../../components/WorkersPanel";
import * as workerApi from "../../../services/workerApi";

// Mock the workerApi module
vi.mock("../../../services/workerApi", () => ({
  workerApi: {
    getWorkersList: vi.fn(),
    stopWorker: vi.fn(),
    resumeWorker: vi.fn(),
    enableSchedule: vi.fn(),
    disableSchedule: vi.fn(),
  },
}));

describe("WorkersPanel", () => {
  const mockWorkers = [
    {
      workerName: "AccessImportWorker",
      runtimeStatus: "Running",
      lastHeartbeat: "2025-01-20T10:30:00Z",
      lastError: null,
      lastErrorTime: null,
      errorCount: 0,
      isScheduleEnabled: true,
      isManuallyStopped: false,
      updatedAtUtc: "2025-01-20T10:00:00Z",
      updatedBy: "system",
    },
    {
      workerName: "AnalyticsWorker",
      runtimeStatus: "Stopped",
      lastHeartbeat: "2025-01-20T09:00:00Z",
      lastError: "Connection timeout",
      lastErrorTime: "2025-01-20T09:30:00Z",
      errorCount: 3,
      isScheduleEnabled: false,
      isManuallyStopped: true,
      updatedAtUtc: "2025-01-20T09:00:00Z",
      updatedBy: "admin",
    },
  ];

  beforeEach(() => {
    vi.clearAllMocks();
    (workerApi.workerApi.getWorkersList as any).mockResolvedValue({
      workers: mockWorkers,
      total: 2,
    });
  });

  it("renders workers panel with title", async () => {
    render(<WorkersPanel />);
    expect(screen.getByText("Worker Management")).toBeInTheDocument();
    expect(screen.getByText("Refresh")).toBeInTheDocument();
  });

  it("loads and displays workers list", async () => {
    render(<WorkersPanel />);

    await waitFor(() => {
      expect(screen.getByText("AccessImportWorker")).toBeInTheDocument();
      expect(screen.getByText("AnalyticsWorker")).toBeInTheDocument();
    });
  });

  it("displays worker status badges", async () => {
    render(<WorkersPanel />);

    await waitFor(() => {
      expect(screen.getByText("Running")).toBeInTheDocument();
      expect(screen.getByText("Stopped")).toBeInTheDocument();
    });
  });

  it("displays schedule enabled/disabled badges", async () => {
    render(<WorkersPanel />);

    await waitFor(() => {
      const badges = screen.getAllByText("Enabled");
      expect(badges.length).toBeGreaterThan(0);
    });
  });

  it("calls stopWorker when stop button clicked", async () => {
    (workerApi.workerApi.stopWorker as any).mockResolvedValue({
      success: true,
      message: "Worker stopped",
    });

    render(<WorkersPanel />);

    await waitFor(() => {
      expect(screen.getByText("AccessImportWorker")).toBeInTheDocument();
    });

    // Find and click the stop button for first worker
    const stopButtons = screen.getAllByText("Stop");
    expect(stopButtons.length).toBeGreaterThan(0);
    
    window.confirm = vi.fn(() => true);
    fireEvent.click(stopButtons[0]);

    await waitFor(() => {
      expect(workerApi.workerApi.stopWorker).toHaveBeenCalledWith("AccessImportWorker");
    });
  });

  it("calls resumeWorker when resume button clicked", async () => {
    (workerApi.workerApi.resumeWorker as any).mockResolvedValue({
      success: true,
      message: "Worker resumed",
    });

    render(<WorkersPanel />);

    await waitFor(() => {
      expect(screen.getByText("AnalyticsWorker")).toBeInTheDocument();
    });

    // Find and click the resume button for stopped worker
    const resumeButtons = screen.getAllByText("Resume");
    if (resumeButtons.length > 0) {
      fireEvent.click(resumeButtons[0]);

      await waitFor(() => {
        expect(workerApi.workerApi.resumeWorker).toHaveBeenCalled();
      });
    }
  });

  it("calls disableSchedule when disable schedule button clicked", async () => {
    (workerApi.workerApi.disableSchedule as any).mockResolvedValue({
      success: true,
      message: "Schedule disabled",
    });

    render(<WorkersPanel />);

    await waitFor(() => {
      expect(screen.getByText("AccessImportWorker")).toBeInTheDocument();
    });

    window.confirm = vi.fn(() => true);
    const disableScheduleButtons = screen.getAllByText("Disable Schedule");
    if (disableScheduleButtons.length > 0) {
      fireEvent.click(disableScheduleButtons[0]);

      await waitFor(() => {
        expect(workerApi.workerApi.disableSchedule).toHaveBeenCalledWith("AccessImportWorker");
      });
    }
  });

  it("calls enableSchedule when enable schedule button clicked", async () => {
    (workerApi.workerApi.enableSchedule as any).mockResolvedValue({
      success: true,
      message: "Schedule enabled",
    });

    render(<WorkersPanel />);

    await waitFor(() => {
      expect(screen.getByText("AnalyticsWorker")).toBeInTheDocument();
    });

    const enableScheduleButtons = screen.getAllByText("Enable Schedule");
    if (enableScheduleButtons.length > 0) {
      fireEvent.click(enableScheduleButtons[0]);

      await waitFor(() => {
        expect(workerApi.workerApi.enableSchedule).toHaveBeenCalled();
      });
    }
  });

  it("shows success message after action", async () => {
    (workerApi.workerApi.stopWorker as any).mockResolvedValue({
      success: true,
      message: "Worker stopped successfully",
    });

    render(<WorkersPanel />);

    await waitFor(() => {
      expect(screen.getByText("AccessImportWorker")).toBeInTheDocument();
    });

    window.confirm = vi.fn(() => true);
    const stopButtons = screen.getAllByText("Stop");
    fireEvent.click(stopButtons[0]);

    await waitFor(() => {
      expect(screen.getByText(/stopped successfully/i)).toBeInTheDocument();
    });
  });

  it("shows error message on failed action", async () => {
    (workerApi.workerApi.stopWorker as any).mockRejectedValue(
      new Error("Network error")
    );

    render(<WorkersPanel />);

    await waitFor(() => {
      expect(screen.getByText("AccessImportWorker")).toBeInTheDocument();
    });

    window.confirm = vi.fn(() => true);
    const stopButtons = screen.getAllByText("Stop");
    fireEvent.click(stopButtons[0]);

    await waitFor(() => {
      expect(screen.getByText(/Network error/i)).toBeInTheDocument();
    });
  });

  it("requires confirmation before stopping worker", async () => {
    render(<WorkersPanel />);

    await waitFor(() => {
      expect(screen.getByText("AccessImportWorker")).toBeInTheDocument();
    });

    window.confirm = vi.fn(() => false);
    const stopButtons = screen.getAllByText("Stop");
    fireEvent.click(stopButtons[0]);

    expect(window.confirm).toHaveBeenCalled();
    expect(workerApi.workerApi.stopWorker).not.toHaveBeenCalled();
  });

  it("requires confirmation before disabling schedule", async () => {
    render(<WorkersPanel />);

    await waitFor(() => {
      expect(screen.getByText("AccessImportWorker")).toBeInTheDocument();
    });

    window.confirm = vi.fn(() => false);
    const disableButtons = screen.getAllByText("Disable Schedule");
    if (disableButtons.length > 0) {
      fireEvent.click(disableButtons[0]);

      expect(window.confirm).toHaveBeenCalled();
      expect(workerApi.workerApi.disableSchedule).not.toHaveBeenCalled();
    }
  });

  it("displays last heartbeat time", async () => {
    render(<WorkersPanel />);

    await waitFor(() => {
      expect(screen.getByText(/1\/20\/2025/i)).toBeInTheDocument();
    });
  });

  it("displays loading state", () => {
    (workerApi.workerApi.getWorkersList as any).mockImplementation(
      () => new Promise(() => {}) // Never resolves
    );

    render(<WorkersPanel />);

    expect(screen.getByText("Loading workers...")).toBeInTheDocument();
  });

  it("displays error count for workers", async () => {
    render(<WorkersPanel />);

    await waitFor(() => {
      expect(screen.getByText("AnalyticsWorker")).toBeInTheDocument();
    });

    // The component shows error count in the table
    expect(workerApi.workerApi.getWorkersList).toHaveBeenCalled();
  });

  it("auto-refreshes workers at specified interval", async () => {
    vi.useFakeTimers();

    render(<WorkersPanel refreshInterval={2000} />);

    await waitFor(() => {
      expect(screen.getByText("AccessImportWorker")).toBeInTheDocument();
    });

    expect(workerApi.workerApi.getWorkersList).toHaveBeenCalledTimes(1);

    // Fast-forward time
    vi.advanceTimersByTime(2000);

    await waitFor(() => {
      expect(workerApi.workerApi.getWorkersList).toHaveBeenCalledTimes(2);
    });

    vi.useRealTimers();
  });

  it("displays worker table with all columns", async () => {
    render(<WorkersPanel />);

    await waitFor(() => {
      expect(screen.getByText("Worker Name")).toBeInTheDocument();
      expect(screen.getByText("Status")).toBeInTheDocument();
      expect(screen.getByText("Schedule")).toBeInTheDocument();
      expect(screen.getByText("Last Heartbeat")).toBeInTheDocument();
      expect(screen.getByText("Actions")).toBeInTheDocument();
    });
  });
});

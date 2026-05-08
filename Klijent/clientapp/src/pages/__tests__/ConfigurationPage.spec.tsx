import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { rest } from "msw";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ToastProvider } from "../../components/Toast";
import { PingControlProvider } from "../../context/PingControlContext";
import { ThemeProvider } from "../../context/ThemeContext";
import { server } from "../../mocks/server";
import ConfigurationPage from "../ConfigurationPage";
import * as workerApiModule from "../../services/workerApi";

vi.mock("../../utils/fetchWithTimeout", () => ({
  fetchWithTimeout: (input: RequestInfo | URL, init?: RequestInit) => {
    if (typeof input === "string" && input.startsWith("/")) {
      return fetch(`http://localhost${input}`, init);
    }
    if (input instanceof URL && input.pathname.startsWith("/")) {
      return fetch(new URL(input.pathname + input.search, "http://localhost"), init);
    }
    return fetch(input, init);
  },
}));

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

function renderPage() {
  return render(
    <ThemeProvider>
      <ToastProvider>
        <PingControlProvider>
          <ConfigurationPage />
        </PingControlProvider>
      </ToastProvider>
    </ThemeProvider>
  );
}

describe("ConfigurationPage", () => {
  const host = "http://localhost";

  beforeEach(() => {
    vi.mocked(workerApiModule.workerApi.getWorkersConfiguration).mockResolvedValue({
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
          workerName: "SyncWorker",
          displayName: "Analytics Sync",
          description: "Sync worker",
          workerType: "sync",
          isRuntimeControllable: true,
          isScheduleControllable: true,
          status: "Unknown",
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
    } as any);
    vi.mocked(workerApiModule.workerApi.startWorker).mockResolvedValue({ success: true, message: "ok" });
    vi.mocked(workerApiModule.workerApi.stopWorker).mockResolvedValue({ success: true, message: "ok" });
    vi.mocked(workerApiModule.workerApi.restartWorker).mockResolvedValue({ success: true, message: "ok" });
    vi.mocked(workerApiModule.workerApi.enableSchedule).mockResolvedValue({ success: true, message: "ok" });
    vi.mocked(workerApiModule.workerApi.disableSchedule).mockResolvedValue({ success: true, message: "ok" });

    server.use(
      rest.get(`${host}/api/workers/control`, (_req, res, ctx) =>
        res(ctx.status(200), ctx.json({ isEnabled: true, workersEnabledSource: "config" }))
      ),
      rest.get(`${host}/api/admin/health-check`, (_req, res, ctx) =>
        res(ctx.status(200), ctx.json({ workerGlobalEnabled: true, databaseConnected: true, databaseMessage: "OK" }))
      ),
      rest.get(`${host}/api/redis/status`, (_req, res, ctx) =>
        res(ctx.status(200), ctx.json({ enabled: true, available: true }))
      ),
      rest.get(`${host}/api/admin/backend-routing`, (_req, res, ctx) =>
        res(
          ctx.status(200),
          ctx.json({
            primaryProvider: "render",
            fallbackEnabled: true,
            fallbackProvider: "fly",
            updatedAtUtc: "2026-05-01T10:00:00Z",
            updatedBy: "test",
          })
        )
      ),
      rest.post(`${host}/api/admin/backend-routing`, async (req, res, ctx) => {
        const body = await req.json();
        return res(ctx.status(200), ctx.json(body));
      }),
      rest.get(`${host}/api/admin/backend-routing/ping/:provider`, (req, res, ctx) => {
        const provider = req.params.provider as string;
        if (provider === "render") {
          return res(
            ctx.status(200),
            ctx.json({
              provider: "render",
              success: true,
              statusCode: 200,
              latencyMs: 120,
              checkedAtUtc: "2026-05-07T10:00:00Z",
              message: "Ready",
            })
          );
        }

        return res(
          ctx.status(200),
          ctx.json({
            provider: "fly",
            success: false,
            statusCode: 503,
            latencyMs: 210,
            checkedAtUtc: "2026-05-07T10:00:05Z",
            message: "HTTP 503",
          })
        );
      }),
      rest.get("*/api/admin/backend-routing/ping/render", (_req, res, ctx) =>
        res(
          ctx.status(200),
          ctx.json({
            provider: "render",
            success: true,
            statusCode: 200,
            latencyMs: 120,
            checkedAtUtc: "2026-05-07T10:00:00Z",
            message: "Ready",
          })
        )
      ),
      rest.get(`${host}/api/admin/backend-routing/ping/render`, (_req, res, ctx) =>
        res(
          ctx.status(200),
          ctx.json({
            provider: "render",
            success: true,
            statusCode: 200,
            latencyMs: 120,
            checkedAtUtc: "2026-05-07T10:00:00Z",
            message: "Ready",
          })
        )
      )
    );
  });

  it("renders configuration page and backend panel", async () => {
    renderPage();

    expect(screen.getByText(/Konfiguracija i nadzor/i)).toBeInTheDocument();
    await waitFor(() => {
      expect(screen.getByText(/Backend provider i failover/i)).toBeInTheDocument();
    });
  });

  it("uses render as default backend provider", async () => {
    renderPage();

    const selects = await screen.findAllByRole("combobox");
    expect((selects[0] as HTMLSelectElement).value).toBe("render");
  });

  it("supports fallback toggle behavior", async () => {
    renderPage();

    const fallbackCheckbox = await screen.findByRole("checkbox", { name: /fallback/i });
    expect((fallbackCheckbox as HTMLInputElement).checked).toBe(true);

    fireEvent.click(fallbackCheckbox);
    expect((fallbackCheckbox as HTMLInputElement).checked).toBe(false);
  });

  it("shows API ping results for providers", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByText(/OK \(120ms\)/i)).toBeInTheDocument();
    });
    expect(screen.getByText(/HTTP 503/i)).toBeInTheDocument();
  });

  it("workers panel shows full list and removes duplicate global controls", async () => {
    renderPage();

    fireEvent.click(await screen.findByRole("button", { name: "Radnici" }));

    expect(await screen.findByText("Access Import")).toBeInTheDocument();
    expect(screen.getByText("Analytics Sync")).toBeInTheDocument();

    expect(screen.queryByText("Uključi radnike")).not.toBeInTheDocument();
    expect(screen.queryByText("Isključi radnike")).not.toBeInTheDocument();
    expect(screen.queryByText("Worker Management")).not.toBeInTheDocument();
    expect(screen.queryByText("Refresh")).not.toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: "Osveži" })).toHaveLength(1);
  });
});

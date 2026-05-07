import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { rest } from "msw";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ToastProvider } from "../../components/Toast";
import { PingControlProvider } from "../../context/PingControlContext";
import { ThemeProvider } from "../../context/ThemeContext";
import { server } from "../../mocks/server";
import ConfigurationPage from "../ConfigurationPage";

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
});

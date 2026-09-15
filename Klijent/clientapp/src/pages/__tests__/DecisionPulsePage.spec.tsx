import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter } from "react-router-dom";
import * as decisionPulseApi from "../../services/decisionPulseApi";
import DecisionPulsePage from "../DecisionPulsePage";

describe("DecisionPulsePage", () => {
  beforeEach(() => {
    vi.spyOn(decisionPulseApi, "getDecisionPulse").mockResolvedValue({
      generatedAtUtc: "2026-08-20T12:00:00Z",
      periodFromUtc: null,
      periodToUtc: null,
      tenantScope: "n/a_dedicated",
      suppressedCount: 2,
      items: [],
      meta: {
        success: true,
        emptyReason: "no_pulse_items",
        message: "Nema actionable Pulse stavki.",
      },
    });
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it("renders empty as non-error and does not invent KPI zeros", async () => {
    render(
      <MemoryRouter>
        <DecisionPulsePage />
      </MemoryRouter>,
    );

    expect(await screen.findByText(/Prazan rezultat nije greška/i)).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(screen.queryByText(/0 RSD/i)).not.toBeInTheDocument();
  });

  it("does not render raw metadata when the source fails", async () => {
    vi.spyOn(decisionPulseApi, "getDecisionPulse").mockResolvedValue({
      generatedAtUtc: "2026-08-20T12:00:00Z",
      periodFromUtc: null,
      periodToUtc: null,
      tenantScope: "n/a_dedicated",
      suppressedCount: 0,
      items: [],
      meta: {
        success: false,
        errorCode: "source_error",
        errorMessage: "SqlException: password=super-secret; connection_string=private",
      },
    });

    render(
      <MemoryRouter>
        <DecisionPulsePage />
      </MemoryRouter>,
    );

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Podaci trenutno nisu dostupni");
    expect(alert).not.toHaveTextContent(/SqlException|super-secret|connection_string/i);
  });

  it("sanitizes a rejected request error", async () => {
    vi.spyOn(decisionPulseApi, "getDecisionPulse").mockRejectedValue(
      new Error("Decision Pulse HTTP 500: NpgsqlException at Database.Query()"),
    );

    render(
      <MemoryRouter>
        <DecisionPulsePage />
      </MemoryRouter>,
    );

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Podaci trenutno nisu dostupni");
    expect(alert).not.toHaveTextContent(/HTTP 500|NpgsqlException|Database\.Query/i);
  });

  it("does not try to render malformed empty metadata", async () => {
    vi.spyOn(decisionPulseApi, "getDecisionPulse").mockResolvedValue({
      generatedAtUtc: "2026-08-20T12:00:00Z",
      periodFromUtc: null,
      periodToUtc: null,
      tenantScope: "n/a_dedicated",
      suppressedCount: 0,
      items: [],
      meta: {
        success: true,
        emptyReason: "no_pulse_items",
        message: { unexpected: "object" } as never,
      },
    });

    render(
      <MemoryRouter>
        <DecisionPulsePage />
      </MemoryRouter>,
    );

    expect(await screen.findByText(/Prazan rezultat nije greška/i)).toBeInTheDocument();
    expect(screen.queryByText("[object Object]")).not.toBeInTheDocument();
  });
});

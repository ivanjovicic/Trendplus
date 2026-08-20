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
});

import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { getForecastBaselineBacktest } from "../../services/analyticsApi";
import TrendModelList from "./TrendModelList";

vi.mock("../../services/analyticsApi", () => ({
  getForecastBaselineBacktest: vi.fn(),
}));

const mockGetForecastBaselineBacktest = vi.mocked(getForecastBaselineBacktest);

function buildUnavailablePayload() {
  return {
    generatedAtUtc: "2026-09-04T10:00:00Z",
    evaluationStatus: "unavailable",
    isAuthoritativeMeasurement: false,
    comparisonWindowStatus: "unavailable",
    evaluationFreshnessStatus: "unknown",
    lastEvaluatedAtUtc: null,
    windowStartUtc: null,
    windowEndUtc: null,
    horizonDays: 14,
    primaryBaselineId: "naive_last_period",
    primaryBaselineLabel: "Naivni poslednji period",
    allowedBaselineIds: ["naive_last_period", "seasonal_naive"],
    allowedMetricIds: ["wape", "bias", "mae"],
    allowedCohortIds: ["sufficient_history"],
    missingEvidenceReasons: ["missing_authoritative_evaluation_snapshot"],
    metrics: [
      { metricId: "wape", label: "WAPE", displayKind: "percent", unitLabel: "%", value: null, isAvailable: false, limitation: "N/A" },
      { metricId: "bias", label: "Bias", displayKind: "signed_percent", unitLabel: "%", value: null, isAvailable: false, limitation: "N/A" },
      { metricId: "mae", label: "MAE", displayKind: "number", unitLabel: "jed.", value: null, isAvailable: false, limitation: "N/A" },
    ],
    cohorts: [],
    aggregates: null,
    warning: "No authoritative evaluation snapshot",
  };
}

describe("TrendModelList", () => {
  beforeEach(() => {
    mockGetForecastBaselineBacktest.mockReset();
  });

  it("keeps unavailable evaluation fail-closed without numeric accuracy", async () => {
    mockGetForecastBaselineBacktest.mockResolvedValue(buildUnavailablePayload());

    render(<TrendModelList />);

    expect(screen.getByRole("heading", { name: "Trend modeli" })).toBeInTheDocument();

    await waitFor(() =>
      expect(screen.getByText("Tačnost modela: nije dostupna")).toBeInTheDocument(),
    );

    expect(screen.getByText(/Nema autoritativnog evaluacionog snapshot-a/i)).toBeInTheDocument();
    expect(screen.queryByText("84")).not.toBeInTheDocument();
    expect(screen.queryByText("+4,2%")).not.toBeInTheDocument();
  });

  it("explains the missing evaluation evidence through an accessible tooltip", async () => {
    mockGetForecastBaselineBacktest.mockResolvedValue(buildUnavailablePayload());

    render(<TrendModelList />);

    await waitFor(() => expect(mockGetForecastBaselineBacktest).toHaveBeenCalled());

    const infoButtons = screen.getAllByRole("button", { name: "Više informacija" });
    expect(infoButtons.length).toBe(1);

    fireEvent.click(infoButtons[0]);

    expect(await screen.findByRole("tooltip")).toHaveTextContent(/potvrđena evaluacija/i);
  });

  it("shows measured metrics only for ready authoritative fresh payloads", async () => {
    mockGetForecastBaselineBacktest.mockResolvedValue({
      ...buildUnavailablePayload(),
      evaluationStatus: "ready",
      isAuthoritativeMeasurement: true,
      evaluationFreshnessStatus: "fresh",
      lastEvaluatedAtUtc: "2026-09-04T09:30:00Z",
      windowStartUtc: "2026-08-01T00:00:00Z",
      windowEndUtc: "2026-08-28T00:00:00Z",
      aggregates: { sampleCount: 128, wape: 12.4, bias: -3.1, mae: 42.3 },
      missingEvidenceReasons: [],
      metrics: [
        { metricId: "wape", label: "WAPE", displayKind: "percent", unitLabel: "%", value: 12.4, isAvailable: true, limitation: null },
        { metricId: "bias", label: "Bias", displayKind: "signed_percent", unitLabel: "%", value: -3.1, isAvailable: true, limitation: null },
        { metricId: "mae", label: "MAE", displayKind: "number", unitLabel: "jed.", value: 42.3, isAvailable: true, limitation: null },
      ],
    });

    render(<TrendModelList />);

    await waitFor(() =>
      expect(screen.getByText("Tačnost modela: dostupna")).toBeInTheDocument(),
    );

    expect(screen.getByText("12,4%")).toBeInTheDocument();
    expect(screen.getByText("-3,1%")).toBeInTheDocument();
    expect(screen.getByText("42,3 jed.")).toBeInTheDocument();
    expect(screen.getByText(/potvrđene evaluacije/i)).toBeInTheDocument();
  });

  it("hides numeric scores when evaluation freshness is stale", async () => {
    mockGetForecastBaselineBacktest.mockResolvedValue({
      ...buildUnavailablePayload(),
      evaluationStatus: "ready",
      isAuthoritativeMeasurement: true,
      evaluationFreshnessStatus: "stale",
      lastEvaluatedAtUtc: "2026-08-01T09:30:00Z",
      missingEvidenceReasons: [],
      metrics: [
        { metricId: "wape", label: "WAPE", displayKind: "percent", unitLabel: "%", value: 11.2, isAvailable: true, limitation: null },
        { metricId: "bias", label: "Bias", displayKind: "signed_percent", unitLabel: "%", value: -2.5, isAvailable: true, limitation: null },
        { metricId: "mae", label: "MAE", displayKind: "number", unitLabel: "jed.", value: 38.8, isAvailable: true, limitation: null },
      ],
    });

    render(<TrendModelList />);

    await waitFor(() =>
      expect(screen.getByText("Tačnost modela: nije dostupna")).toBeInTheDocument(),
    );

    expect(screen.getAllByText(/Poslednja evaluacija je zastarela|Zastarela evaluacija nije dovoljno sveža/i).length).toBeGreaterThan(0);
    expect(screen.queryByText("11,2%")).not.toBeInTheDocument();
    expect(screen.queryByText("-2,5%")).not.toBeInTheDocument();
  });

  it("fails closed on malformed metric values instead of showing fake numbers", async () => {
    mockGetForecastBaselineBacktest.mockResolvedValue({
      ...buildUnavailablePayload(),
      evaluationStatus: "ready",
      isAuthoritativeMeasurement: true,
      evaluationFreshnessStatus: "fresh",
      missingEvidenceReasons: [],
      metrics: [
        { metricId: "wape", label: "WAPE", displayKind: "percent", unitLabel: "%", value: Number.NaN, isAvailable: true, limitation: null },
        { metricId: "bias", label: "Bias", displayKind: "signed_percent", unitLabel: "%", value: Number.POSITIVE_INFINITY, isAvailable: true, limitation: null },
        { metricId: "mae", label: "MAE", displayKind: "number", unitLabel: "jed.", value: null, isAvailable: true, limitation: "malformed" },
      ],
    });

    render(<TrendModelList />);

    await waitFor(() =>
      expect(screen.getByText("Tačnost modela: nije dostupna")).toBeInTheDocument(),
    );

    expect(screen.queryByText("NaN%")).not.toBeInTheDocument();
    expect(screen.queryByText("Infinity%")).not.toBeInTheDocument();
    expect(screen.getByText(/Podaci o evaluaciji nisu validni za numerički prikaz/i)).toBeInTheDocument();
  });
});

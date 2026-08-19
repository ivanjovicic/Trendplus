import { fireEvent, render, screen, within } from "@testing-library/react";
import type { ComponentProps } from "react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import RecommendationMeasurementStatisticsReview from "../RecommendationMeasurementStatisticsReview";
import type {
  AnalyticsActionOutcomeSummaryResponse,
  RecommendationMeasurementStatistics,
} from "../../types/analytics";

function stats(overrides: Partial<RecommendationMeasurementStatistics> = {}): RecommendationMeasurementStatistics {
  return {
    success: true,
    issuedCount: 8,
    acceptedCount: 4,
    rejectedCount: 1,
    ignoredCount: 1,
    executedCount: 3,
    measuredCount: 2,
    notMeasuredCount: 1,
    successCount: 1,
    neutralCount: 0,
    negativeCount: 1,
    pendingCount: 0,
    acceptanceRate: 0.5,
    rejectionRate: 0.125,
    ignoredRate: 0.125,
    executionRate: 0.75,
    measurementCoverageRate: null,
    notMeasuredShare: 0.3333,
    positiveOutcomeRate: 0.25,
    neutralOutcomeRate: null,
    negativeOutcomeRate: 0.5,
    warningCodes: ["small_measured_sample"],
    emptyReason: null,
    ...overrides,
  };
}

function summary(
  overrides: Partial<AnalyticsActionOutcomeSummaryResponse> = {},
): AnalyticsActionOutcomeSummaryResponse {
  return {
    meta: {
      success: true,
      periodMode: "created",
      createdFrom: "2026-03-17T00:00:00Z",
      createdTo: "2026-06-15T00:00:00Z",
      generatedAtUtc: "2026-06-15T00:00:00Z",
      sampleSize: 8,
      measuredSampleSize: 2,
      warnings: [],
      emptyReason: null,
    },
    totals: {
      createdCount: 8,
      closedCount: 4,
      openCount: 4,
      measuredCount: 2,
      pendingOutcomeCount: 0,
      successCount: 7,
      neutralCount: 0,
      negativeCount: 1,
      notMeasuredCount: 0,
      outcomeCoverageRate: 0.9,
      positiveOutcomeRate: 0.9,
      negativeOutcomeRate: 0.1,
    },
    impact: {
      measuredImpactSampleCount: 2,
    },
    bySourceType: [],
    byPriority: [],
    byOutcomeStatus: [],
    byDataQuality: [],
    byConfidenceBucket: [],
    byReliabilityBucket: [],
    measurementStatistics: stats(),
    ...overrides,
  };
}

function renderReview(
  props: Partial<ComponentProps<typeof RecommendationMeasurementStatisticsReview>> = {},
) {
  return render(
    <MemoryRouter>
      <RecommendationMeasurementStatisticsReview
        loading={false}
        loadError={null}
        summary={summary()}
        {...props}
      />
    </MemoryRouter>,
  );
}

describe("RecommendationMeasurementStatisticsReview", () => {
  it("binds funnel and outcome rates to measurementStatistics, not totals success", () => {
    renderReview();

    const panel = screen.getByTestId("measurement-statistics-review");
    expect(within(panel).getByText("Izdato")).toBeInTheDocument();
    expect(within(panel).getByText("Prihvaćeno")).toBeInTheDocument();
    expect(within(panel).getByText("Izvršeno")).toBeInTheDocument();
    expect(within(panel).getByText(/Stopa prihvatanja 50% · nije uspeh/)).toBeInTheDocument();
    expect(within(panel).getByText(/Stopa pozitivnih ishoda 25%/)).toBeInTheDocument();
    expect(within(panel).queryByText("90%")).not.toBeInTheDocument();
    expect(within(panel).getByText(/Obim toka, nije uspeh/)).toBeInTheDocument();
  });

  it("renders EmptyState for no_rows without KPI zeros or 0% rates", () => {
    renderReview({
      summary: summary({
        measurementStatistics: stats({
          issuedCount: 0,
          acceptedCount: 0,
          rejectedCount: 0,
          ignoredCount: 0,
          executedCount: 0,
          measuredCount: 0,
          notMeasuredCount: 0,
          successCount: 0,
          emptyReason: "no_rows",
          acceptanceRate: null,
          positiveOutcomeRate: null,
          measurementCoverageRate: null,
          warningCodes: [],
        }),
      }),
    });

    expect(screen.getByText("Nema izdatih preporuka za izabrani period.")).toBeInTheDocument();
    expect(screen.getByText("no_rows")).toBeInTheDocument();
    expect(screen.queryByText("0%")).not.toBeInTheDocument();
    expect(screen.queryByText("Stopa pozitivnih ishoda")).not.toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("does not invent percentages when measurementStatistics is missing", () => {
    renderReview({
      summary: summary({ measurementStatistics: null }),
    });

    const alert = screen.getByRole("alert");
    expect(alert).toHaveTextContent("Polje measurementStatistics nedostaje");
    expect(alert).toHaveTextContent("missing_statistics");
    expect(screen.queryByText("90%")).not.toBeInTheDocument();
    expect(screen.queryByText("Stopa pozitivnih ishoda")).not.toBeInTheDocument();
  });

  it("hides rates on load error", () => {
    renderReview({
      loadError: "summary unavailable",
      summary: null,
    });

    expect(screen.getByRole("alert")).toHaveTextContent("summary unavailable");
    expect(screen.queryByText("Stopa pozitivnih ishoda")).not.toBeInTheDocument();
    expect(screen.queryByText("0%")).not.toBeInTheDocument();
  });

  it("keeps a null coverage rate as Nije dostupno instead of 0%", () => {
    renderReview();

    const coverage = screen.getByTestId("rms-coverage-rate");
    expect(coverage).toHaveTextContent("Nije dostupno");
    expect(coverage).not.toHaveTextContent("0%");
  });

  it("fails export gracefully without writing a zeros CSV", () => {
    const createObjectURL = vi.fn();
    vi.stubGlobal("URL", { ...URL, createObjectURL, revokeObjectURL: vi.fn() });

    renderReview({
      summary: summary({
        measurementStatistics: stats({ emptyReason: "no_rows", warningCodes: [] }),
      }),
    });

    fireEvent.click(screen.getByRole("button", { name: "Izvezi CSV" }));

    expect(screen.getByText(/Izvoz nije sačuvan/)).toBeInTheDocument();
    expect(createObjectURL).not.toHaveBeenCalled();
    expect(screen.getByText("Nema izdatih preporuka za izabrani period.")).toBeInTheDocument();
  });
});

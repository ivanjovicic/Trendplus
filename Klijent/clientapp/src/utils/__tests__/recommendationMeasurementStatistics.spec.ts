import { describe, expect, it } from "vitest";
import {
  buildMeasurementStatisticsExportCsv,
  canExportMeasurementStatistics,
  formatMeasurementRate,
  MEASUREMENT_RATE_UNAVAILABLE,
  resolveMeasurementStatisticsView,
} from "../recommendationMeasurementStatistics";
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
    measurementCoverageRate: 0.6667,
    notMeasuredShare: 0.3333,
    positiveOutcomeRate: 0.25,
    neutralOutcomeRate: 0,
    negativeOutcomeRate: 0.5,
    warningCodes: ["small_sample"],
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
      successCount: 3,
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

describe("recommendationMeasurementStatistics", () => {
  it("formats a null rate as unavailable, not 0%", () => {
    expect(formatMeasurementRate(null)).toBe(MEASUREMENT_RATE_UNAVAILABLE);
    expect(formatMeasurementRate(undefined)).toBe(MEASUREMENT_RATE_UNAVAILABLE);
    expect(formatMeasurementRate(null)).not.toBe("0%");
    expect(formatMeasurementRate(0)).toBe("0%");
  });

  it("does not treat totals rates as the measurement success view", () => {
    const view = resolveMeasurementStatisticsView({
      loading: false,
      summary: summary(),
    });

    expect(view.kind).toBe("ready");
    if (view.kind !== "ready") {
      return;
    }

    expect(view.stats.positiveOutcomeRate).toBe(0.25);
    expect(view.stats.positiveOutcomeRate).not.toBe(0.9);
    expect(formatMeasurementRate(view.stats.positiveOutcomeRate)).toBe("25%");
    expect(formatMeasurementRate(view.stats.positiveOutcomeRate)).not.toBe("90%");
  });

  it("uses EmptyState signal for no_rows without exposing rates", () => {
    const view = resolveMeasurementStatisticsView({
      loading: false,
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
          neutralCount: 0,
          negativeCount: 0,
          pendingCount: 0,
          acceptanceRate: null,
          rejectionRate: null,
          ignoredRate: null,
          executionRate: null,
          measurementCoverageRate: null,
          notMeasuredShare: null,
          positiveOutcomeRate: null,
          neutralOutcomeRate: null,
          negativeOutcomeRate: null,
          warningCodes: [],
          emptyReason: "no_rows",
        }),
      }),
    });

    expect(view).toEqual({ kind: "empty", emptyReason: "no_rows" });
    expect(canExportMeasurementStatistics(view)).toBe(false);
  });

  it("does not compute local percentages when measurementStatistics is missing", () => {
    const view = resolveMeasurementStatisticsView({
      loading: false,
      summary: summary({ measurementStatistics: null }),
    });

    expect(view.kind).toBe("error");
    if (view.kind !== "error") {
      return;
    }

    expect(view.code).toBe("missing_statistics");
    expect(view.message).not.toMatch(/90%/);
    expect(canExportMeasurementStatistics(view)).toBe(false);
  });

  it("hides rates on load failure", () => {
    const view = resolveMeasurementStatisticsView({
      loading: false,
      loadError: "summary unavailable",
      summary: null,
    });

    expect(view).toMatchObject({ kind: "error", code: "load_failed" });
    expect(canExportMeasurementStatistics(view)).toBe(false);
  });

  it("copies backend counts and nullable rates into CSV without recomputing success", () => {
    const csv = buildMeasurementStatisticsExportCsv(stats({
      positiveOutcomeRate: null,
      acceptanceRate: 0.5,
    }));

    expect(csv).toContain("issuedCount,acceptedCount,rejectedCount,ignoredCount,executedCount");
    expect(csv).toContain("successCount");
    expect(csv).toContain(",0.5,");
    expect(csv).toMatch(/,1,0,1,0,0.5,/);
    expect(csv.split("\n")[1]).toContain(",,");
    expect(csv).not.toContain("0.9");
  });
});

import { describe, expect, it } from "vitest";
import {
  formatMetricDisplayValue,
  normalizeMetricNumber,
} from "../utils/analyticsMetricValue";

export type AnalyticsContractMetaFixture = {
  success?: boolean | null;
  emptyReason?: string | null;
  dataQualityStatus?: string | null;
};

export type AnalyticsContractFixture = {
  metric: unknown;
  meta: AnalyticsContractMetaFixture;
  backendValue: unknown;
  projectedValue: unknown;
  chartRows: readonly { observedAtUtc: string }[];
  visibleRows: readonly unknown[];
  totalCount: number;
};

export type AnalyticsReliabilityContractAdapter<TResponse> = {
  name: string;
  responseFactory: (fixture: AnalyticsContractFixture) => TResponse;
  selectMetric: (response: TResponse) => unknown;
  selectMeta: (response: TResponse) => AnalyticsContractMetaFixture;
  selectBackendValue: (response: TResponse) => unknown;
  selectProjectedValue: (response: TResponse) => unknown;
  selectChartRows: (response: TResponse) => readonly { observedAtUtc: string }[];
  selectCounts: (response: TResponse) => { visibleCount: number; totalCount: number };
};

export const ANALYTICS_RELIABILITY_NUMERIC_CASES = [
  { name: "missing", input: null, normalized: null },
  { name: "undefined", input: undefined, normalized: null },
  { name: "NaN", input: Number.NaN, normalized: null },
  { name: "Infinity", input: Number.POSITIVE_INFINITY, normalized: null },
  { name: "malformed", input: "not-a-number", normalized: null },
  { name: "valid zero", input: 0, normalized: 0 },
  { name: "valid positive", input: 1250.5, normalized: 1250.5 },
  { name: "valid negative delta", input: -12.5, normalized: -12.5 },
  { name: "out-of-range raw value", input: 101, normalized: 101 },
] as const;

function fixture(overrides: Partial<AnalyticsContractFixture> = {}): AnalyticsContractFixture {
  return {
    metric: null,
    meta: { success: true, emptyReason: null, dataQualityStatus: "good" },
    backendValue: "backend-authoritative",
    projectedValue: "backend-authoritative",
    chartRows: [
      { observedAtUtc: "2026-09-02T00:00:00Z" },
      { observedAtUtc: "2026-09-01T00:00:00Z" },
    ],
    visibleRows: ["visible-1", "visible-2"],
    totalCount: 10,
    ...overrides,
  };
}

/**
 * Registers the shared analytics invariant suite for a page adapter.
 *
 * The adapter owns only response construction and selectors. This helper
 * intentionally contains no page scoring, recommendation thresholds or
 * endpoint-specific business rules.
 */
export function registerAnalyticsReliabilityContractSuite<TResponse>(
  adapter: AnalyticsReliabilityContractAdapter<TResponse>,
): void {
  describe(`${adapter.name} analytics reliability contract`, () => {
    it.each(ANALYTICS_RELIABILITY_NUMERIC_CASES)(
      "normalizes $name without turning unknown into zero",
      ({ input, normalized }) => {
        const response = adapter.responseFactory(fixture({ metric: input }));
        expect(normalizeMetricNumber(adapter.selectMetric(response) as number | string | null | undefined))
          .toBe(normalized);
      },
    );

    it("keeps valid zero visible while unavailable values fail closed", () => {
      const zeroResponse = adapter.responseFactory(fixture({ metric: 0 }));
      const unknownResponse = adapter.responseFactory(fixture({ metric: null }));

      expect(formatMetricDisplayValue({
        value: normalizeMetricNumber(adapter.selectMetric(zeroResponse) as number | string | null | undefined),
        kind: "number",
        fallback: "N/A",
      })).toBe("0");
      expect(formatMetricDisplayValue({
        value: normalizeMetricNumber(adapter.selectMetric(unknownResponse) as number | string | null | undefined),
        kind: "number",
        fallback: "N/A",
      })).toBe("N/A");
    });

    it("keeps successful empty distinct from error", () => {
      const emptyResponse = adapter.responseFactory(fixture({
        meta: { success: true, emptyReason: "no_data_in_period", dataQualityStatus: "insufficient_data" },
        visibleRows: [],
        totalCount: 0,
      }));
      const errorResponse = adapter.responseFactory(fixture({
        meta: { success: false, emptyReason: null, dataQualityStatus: "error" },
      }));

      expect(adapter.selectMeta(emptyResponse)).toMatchObject({
        success: true,
        emptyReason: "no_data_in_period",
      });
      expect(adapter.selectMeta(errorResponse)).toMatchObject({
        success: false,
        dataQualityStatus: "error",
      });
    });

    it("preserves backend authority instead of reconstructing a row value", () => {
      const response = adapter.responseFactory(fixture({
        backendValue: 87,
        projectedValue: 87,
      }));

      expect(adapter.selectProjectedValue(response)).toBe(adapter.selectBackendValue(response));
    });

    it("keeps chart chronology independent from table ordering", () => {
      const response = adapter.responseFactory(fixture());
      const dates = adapter.selectChartRows(response).map((row) => Date.parse(row.observedAtUtc));

      expect(dates).toEqual([...dates].sort((left, right) => left - right));
    });

    it("keeps visible-page counts distinct from global totals", () => {
      const response = adapter.responseFactory(fixture({
        visibleRows: ["page-row"],
        totalCount: 250,
      }));

      expect(adapter.selectCounts(response)).toEqual({ visibleCount: 1, totalCount: 250 });
    });
  });
}

export function createFixtureAdapter(name: string): AnalyticsReliabilityContractAdapter<AnalyticsContractFixture> {
  return {
    name,
    responseFactory: (value) => value,
    selectMetric: (response) => response.metric,
    selectMeta: (response) => response.meta,
    selectBackendValue: (response) => response.backendValue,
    selectProjectedValue: (response) => response.projectedValue,
    selectChartRows: (response) => [...response.chartRows].sort(
      (left, right) => Date.parse(left.observedAtUtc) - Date.parse(right.observedAtUtc),
    ),
    selectCounts: (response) => ({
      visibleCount: response.visibleRows.length,
      totalCount: response.totalCount,
    }),
  };
}

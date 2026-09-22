import { describe, expect, it } from "vitest";
import type { AnalyticsResponseMeta } from "../../types/analytics";
import {
  isAuthoritativeAnalyticsMetric,
  readAnalyticsMetricProvenance,
} from "../analyticsMetricProvenance";

describe("analyticsMetricProvenance", () => {
  const meta: AnalyticsResponseMeta = {
    success: true,
    metricProvenance: {
      revenueShare: {
        kind: "authoritative_backend_aggregate",
        authority: "authoritative",
        actionability: "informational",
        unit: "ratio",
        denominator: "period_total_revenue",
      },
      margin: {
        kind: "observed_row_value",
        authority: "observed",
        actionability: "informational",
        unit: "RSD",
      },
      confidence: {
        kind: "modeled_estimated",
        authority: "modeled",
        actionability: "actionable",
        unit: "ratio",
        denominator: "eligible_signal_count",
      },
      reliability: {
        kind: "unknown",
        authority: "unknown",
        actionability: "blocked",
      },
      counts: {
        kind: "frontend_display_derivation",
        authority: "derived",
        actionability: "informational",
        unit: "items",
      },
    },
  };

  it("preserves source, authority, actionability, unit, and denominator", () => {
    expect(readAnalyticsMetricProvenance(meta, "revenueShare")).toEqual({
      kind: "authoritative_backend_aggregate",
      authority: "authoritative",
      actionability: "informational",
      unit: "ratio",
      denominator: "period_total_revenue",
    });
    expect(readAnalyticsMetricProvenance(meta, "margin")?.unit).toBe("RSD");
    expect(readAnalyticsMetricProvenance(meta, "confidence")?.denominator).toBe("eligible_signal_count");
    expect(readAnalyticsMetricProvenance(meta, "reliability")?.actionability).toBe("blocked");
    expect(readAnalyticsMetricProvenance(meta, "counts")?.kind).toBe("frontend_display_derivation");
  });

  it("identifies only an explicit backend aggregate as authoritative", () => {
    expect(isAuthoritativeAnalyticsMetric(meta, "revenueShare")).toBe(true);
    expect(isAuthoritativeAnalyticsMetric(meta, "margin")).toBe(false);
    expect(isAuthoritativeAnalyticsMetric(meta, "counts")).toBe(false);
  });

  it("preserves the same provenance across table, detail, chart, export, and action projections", () => {
    const surfaces = ["table", "detail", "chart", "export", "action"];
    const expected = readAnalyticsMetricProvenance(meta, "revenueShare");

    for (const surface of surfaces) {
      const projectedMeta = { ...meta, surface };
      expect(readAnalyticsMetricProvenance(projectedMeta, "revenueShare"), surface).toEqual(expected);
      expect(isAuthoritativeAnalyticsMetric(projectedMeta, "revenueShare")).toBe(true);
    }
  });

  it("fails closed for missing or contradictory provenance", () => {
    expect(readAnalyticsMetricProvenance(null, "margin")).toBeNull();
    expect(readAnalyticsMetricProvenance({ metricProvenance: {} }, "margin")).toBeNull();
    expect(readAnalyticsMetricProvenance({
      metricProvenance: {
        margin: {
          kind: "authoritative_backend_aggregate",
          authority: "derived",
          actionability: "informational",
        },
      },
    }, "margin")).toBeNull();
    expect(readAnalyticsMetricProvenance({
      metricProvenance: {
        margin: {
          kind: "frontend_display_derivation",
          authority: "authoritative",
          actionability: "informational",
        },
      },
    }, "margin")).toBeNull();
  });
});

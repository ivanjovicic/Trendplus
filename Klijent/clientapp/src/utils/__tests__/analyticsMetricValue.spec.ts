import { describe, expect, it } from "vitest";
import {
  formatMetricDisplayValue,
  metricStatusLabel,
  normalizeMetricNumber,
  resolveMetricValueStatus,
} from "../analyticsMetricValue";
import { fmtNumber, fmtPct, fmtRsd, fmtSignedPct } from "../analyticsFormatters";

describe("analyticsMetricValue", () => {
  it("does not turn missing currency into 0 RSD", () => {
    expect(formatMetricDisplayValue({ value: null, kind: "currency" })).toBe("Nije dostupno");
    expect(formatMetricDisplayValue({ value: undefined, kind: "percent" })).toBe("Nije dostupno");
  });

  it("keeps valid zero distinct from unavailable", () => {
    expect(resolveMetricValueStatus({ value: 0 })).toBe("valid_zero");
    expect(formatMetricDisplayValue({ value: 0, kind: "currency" })).toBe("0 RSD");
  });

  it("renders semantic fallback states for insufficient and stale metrics", () => {
    expect(metricStatusLabel("insufficient_data")).toBe("Nedovoljno podataka");
    expect(formatMetricDisplayValue({ value: null, kind: "percent", status: "stale" })).toBe("Zastarelo");
    expect(formatMetricDisplayValue({ value: null, kind: "percent", status: "not_applicable" })).toBe("Nije primenljivo");
  });

  it("normalizes numeric text and rejects invalid input", () => {
    expect(normalizeMetricNumber("1 234,5")).toBe(1234.5);
    expect(normalizeMetricNumber("")).toBeNull();
    expect(normalizeMetricNumber("abc")).toBeNull();
  });

  it("does not format NaN or Infinity as analytics values", () => {
    expect(fmtNumber(Number.NaN)).toBe("N/A");
    expect(fmtNumber(Number.POSITIVE_INFINITY)).toBe("N/A");
    expect(fmtRsd(Number.NEGATIVE_INFINITY)).toBe("N/A");
    expect(fmtPct(Number.POSITIVE_INFINITY)).toBe("N/A");
    expect(fmtSignedPct(Number.NaN)).toBe("N/A");
  });
});

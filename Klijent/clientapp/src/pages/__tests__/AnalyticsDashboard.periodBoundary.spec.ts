import { describe, expect, it } from "vitest";
import {
  calculateDashboardPeriodDays,
  resolveDashboardPeriodDays,
} from "../AnalyticsDashboard";

describe("AnalyticsDashboard UTC period divisor", () => {
  it("counts spring DST boundary days from backend UTC timestamps", () => {
    expect(
      calculateDashboardPeriodDays(
        "2026-03-28T00:00:00Z",
        "2026-03-30T00:00:00Z",
      ),
    ).toBe(3);
  });

  it("counts autumn DST boundary days from backend UTC timestamps", () => {
    expect(
      calculateDashboardPeriodDays(
        "2026-10-24T00:00:00Z",
        "2026-10-26T00:00:00Z",
      ),
    ).toBe(3);
  });

  it("fails closed for missing or reversed backend periods", () => {
    expect(calculateDashboardPeriodDays(null, "2026-08-01T00:00:00Z")).toBe(1);
    expect(
      calculateDashboardPeriodDays(
        "2026-08-02T00:00:00Z",
        "2026-08-01T00:00:00Z",
      ),
    ).toBe(1);
  });

  it("prefers a complete requested backend period over effective and filter fallbacks", () => {
    expect(
      resolveDashboardPeriodDays(
        {
          requestedPeriodFromUtc: "2026-03-28T00:00:00Z",
          requestedPeriodToUtc: "2026-03-30T00:00:00Z",
          effectivePeriodFromUtc: "2026-04-01T00:00:00Z",
          effectivePeriodToUtc: "2026-04-02T00:00:00Z",
        },
        "2026-05-01",
        "2026-05-10",
      ),
    ).toBe(3);
  });

  it("uses a complete effective period when requested metadata is incomplete", () => {
    expect(
      resolveDashboardPeriodDays(
        {
          requestedPeriodFromUtc: "2026-08-01T00:00:00Z",
          requestedPeriodToUtc: null,
          effectivePeriodFromUtc: "2026-10-24T00:00:00Z",
          effectivePeriodToUtc: "2026-10-26T00:00:00Z",
        },
        "2026-08-01",
        "2026-08-02",
      ),
    ).toBe(3);
  });

  it("falls back to the selected filter when backend period metadata is invalid", () => {
    expect(
      resolveDashboardPeriodDays(
        {
          requestedPeriodFromUtc: "not-a-date",
          requestedPeriodToUtc: "2026-08-03T00:00:00Z",
          effectivePeriodFromUtc: "2026-08-04T00:00:00Z",
          effectivePeriodToUtc: "2026-08-01T00:00:00Z",
        },
        "2026-08-01",
        "2026-08-02",
      ),
    ).toBe(2);
  });
});

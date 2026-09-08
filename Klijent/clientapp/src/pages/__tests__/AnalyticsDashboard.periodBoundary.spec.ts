import { describe, expect, it } from "vitest";
import { calculateDashboardPeriodDays } from "../AnalyticsDashboard";

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
});

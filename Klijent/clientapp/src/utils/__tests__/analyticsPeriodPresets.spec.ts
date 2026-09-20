import { afterEach, describe, expect, it, vi } from "vitest";
import {
  getAnalyticsPeriodPresetRange,
  resolvePresetFilterRange,
} from "../analyticsPeriodPresets";

describe("analyticsPeriodPresets", () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it("recomputes preset ranges from the current calendar day", () => {
    const april = new Date("2026-04-01T12:00:00Z");
    const may = new Date("2026-05-01T12:00:00Z");

    expect(getAnalyticsPeriodPresetRange("30d", april)).toEqual({
      fromDate: "2026-03-03",
      toDate: "2026-04-01",
    });
    expect(getAnalyticsPeriodPresetRange("30d", may)).toEqual({
      fromDate: "2026-04-02",
      toDate: "2026-05-01",
    });
  });

  it("keeps custom draft dates while preset ranges refresh on apply", () => {
    expect(resolvePresetFilterRange("custom", "2026-01-01", "2026-01-31")).toEqual({
      fromDate: "2026-01-01",
      toDate: "2026-01-31",
    });
    expect(resolvePresetFilterRange("30d", "2026-01-01", "2026-01-31", new Date("2026-05-01T12:00:00Z"))).toEqual({
      fromDate: "2026-04-02",
      toDate: "2026-05-01",
    });
  });
});

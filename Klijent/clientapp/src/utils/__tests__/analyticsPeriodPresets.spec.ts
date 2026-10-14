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

  it.each([
    ["00:30 CEST, still the previous UTC day", "2026-10-13T22:30:00Z", "2026-09-15", "2026-10-14"],
    ["23:30 CEST", "2026-10-14T21:30:00Z", "2026-09-15", "2026-10-14"],
    ["00:30 CET in winter", "2026-01-15T23:30:00Z", "2025-12-18", "2026-01-16"],
    ["23:30 CET in winter", "2026-01-16T22:30:00Z", "2025-12-18", "2026-01-16"],
    ["spring-forward day, 00:30 CET", "2026-03-28T23:30:00Z", "2026-02-28", "2026-03-29"],
    ["spring-forward day, 23:30 CEST", "2026-03-29T21:30:00Z", "2026-02-28", "2026-03-29"],
    ["fall-back day, 00:30 CEST", "2026-10-24T22:30:00Z", "2026-09-26", "2026-10-25"],
    ["fall-back day, 23:30 CET", "2026-10-25T22:30:00Z", "2026-09-26", "2026-10-25"],
  ])("ends the 30-day preset on the Europe/Belgrade business day (%s)", (_label, now, fromDate, toDate) => {
    expect(getAnalyticsPeriodPresetRange("30d", new Date(now))).toEqual({ fromDate, toDate });
  });

  it("counts every preset length in Belgrade calendar days across DST changes", () => {
    const afterMidnight = new Date("2026-10-13T22:30:00Z");

    expect(getAnalyticsPeriodPresetRange("90d", afterMidnight)).toEqual({ fromDate: "2026-07-17", toDate: "2026-10-14" });
    expect(getAnalyticsPeriodPresetRange("180d", afterMidnight)).toEqual({ fromDate: "2026-04-18", toDate: "2026-10-14" });
    expect(getAnalyticsPeriodPresetRange("365d", afterMidnight)).toEqual({ fromDate: "2025-10-15", toDate: "2026-10-14" });
  });

  it("falls back to the Belgrade business day for an unknown select value instead of throwing", () => {
    const unknown = "" as unknown as Parameters<typeof getAnalyticsPeriodPresetRange>[0];

    expect(getAnalyticsPeriodPresetRange(unknown, new Date("2026-10-13T22:30:00Z"))).toEqual({
      fromDate: "2026-10-14",
      toDate: "2026-10-14",
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

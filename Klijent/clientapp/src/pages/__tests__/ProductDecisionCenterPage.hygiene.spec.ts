import { beforeEach, describe, expect, it } from "vitest";
import {
  applyPeriodPreset,
  compareNullable,
  defaultPeriodRange,
} from "../ProductDecisionCenterPage";

describe("ProductDecisionCenterPage hygiene contracts", () => {
  beforeEach(() => {
    window.history.replaceState(null, "", "/");
  });

  it("uses the Belgrade calendar day for period defaults and presets", () => {
    const now = new Date("2026-09-30T22:30:00.000Z");

    expect(defaultPeriodRange(now)).toEqual({ fromDate: "2026-09-02", toDate: "2026-10-01" });
    expect(applyPeriodPreset("last90", now)).toEqual({ fromDate: "2026-07-04", toDate: "2026-10-01" });
  });

  it("keeps null values last in both sort directions", () => {
    const compareNumbers = (left: number, right: number) => left - right;

    expect(compareNullable(null, 3, compareNumbers, "asc")).toBeGreaterThan(0);
    expect(compareNullable(null, 3, compareNumbers, "desc")).toBeGreaterThan(0);
    expect(compareNullable(3, null, compareNumbers, "asc")).toBeLessThan(0);
    expect(compareNullable(3, null, compareNumbers, "desc")).toBeLessThan(0);
  });
});

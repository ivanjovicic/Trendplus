import { describe, expect, it } from "vitest";
import { buildPeriodLineageLabel, resolveLineagePeriod } from "../analyticsPeriodLineage";

describe("analyticsPeriodLineage", () => {
  it("adds observed period only when it differs from the effective range", () => {
    expect(buildPeriodLineageLabel({
      effectiveFromUtc: "2026-06-01T00:00:00Z",
      effectiveToUtc: "2026-06-30T00:00:00Z",
      observedFromUtc: "2026-01-01T00:00:00Z",
      observedToUtc: "2026-06-30T00:00:00Z",
    })).toContain("Posmatrani podaci");
  });

  it("prefers requested bounds when available", () => {
    expect(resolveLineagePeriod(
      "2026-06-01T00:00:00Z",
      "2026-06-30T00:00:00Z",
      "2026-01-01T00:00:00Z",
      "2026-06-30T00:00:00Z",
      null,
      null,
    )).toEqual({
      periodFrom: "2026-06-01T00:00:00Z",
      periodTo: "2026-06-30T00:00:00Z",
    });
  });
});

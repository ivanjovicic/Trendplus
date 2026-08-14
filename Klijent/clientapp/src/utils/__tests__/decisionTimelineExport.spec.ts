import { describe, expect, it } from "vitest";
import { assertHonestDecisionTimelineExportCsv } from "../decisionTimelineExport";

describe("decisionTimelineExport", () => {
  it("rejects a failed export instead of downloading zero rates", () => {
    const csv = [
      "# success=false",
      "# requestedPeriodFromUtc=2026-08-01",
      "# effectivePeriodFromUtc=2026-08-01",
      "# emptyReason=",
      "# errorCode=ANALYTICS_UNEXPECTED_ERROR",
    ].join("\n");

    expect(() => assertHonestDecisionTimelineExportCsv(csv)).toThrow(/nije dostupan/i);
  });

  it("keeps an empty period export without fabricating rates", () => {
    const csv = [
      "# success=true",
      "# requestedPeriodFromUtc=2026-08-01",
      "# requestedPeriodToUtc=2026-08-11",
      "# effectivePeriodFromUtc=2026-08-01",
      "# effectivePeriodToUtc=2026-08-11",
      "# emptyReason=outside_period",
      "# successRate=",
      "timelineId,actionId",
    ].join("\n");

    expect(assertHonestDecisionTimelineExportCsv(csv)).toContain("emptyReason=outside_period");
    expect(csv).not.toMatch(/successRate=0/);
    expect(csv).not.toContain("0%");
  });
});

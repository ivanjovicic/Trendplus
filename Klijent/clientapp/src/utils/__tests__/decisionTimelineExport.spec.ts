import { describe, expect, it } from "vitest";
import { assertHonestDecisionTimelineExportCsv } from "../decisionTimelineExport";
import {
  timelineEmptyReasonLabel,
  timelineEventTypeLabel,
  timelineGapReasonLabel,
} from "../decisionTimelineLabels";

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

describe("decisionTimelineLabels Slice-5 parity", () => {
  it("keeps rejected distinct from done and not_measured distinct from success or failure", () => {
    expect(timelineEventTypeLabel("action_rejected")).toBe("Akcija odbijena");
    expect(timelineEventTypeLabel("action_executed")).toBe("Akcija izvršena");
    expect(timelineEventTypeLabel("outcome_not_measured")).toBe("Ishod nije izmeren");
    expect(timelineEventTypeLabel("outcome_measured")).toBe("Ishod izmeren");

    expect(timelineEventTypeLabel("action_rejected")).not.toMatch(/izvršen|done|završen/i);
    expect(timelineEventTypeLabel("outcome_not_measured")).not.toMatch(/uspeh|success|failure|negativ/i);
    expect(timelineEventTypeLabel("action_rejected")).not.toBe(timelineEventTypeLabel("action_executed"));
    expect(timelineEventTypeLabel("outcome_not_measured")).not.toBe(timelineEventTypeLabel("outcome_measured"));
  });

  it("maps the same Slice-2 event and gap codes the export CSV copies", () => {
    const csvEventTypes = "recommendation_issued|action_rejected|action_executed|outcome_not_measured";
    const labels = csvEventTypes.split("|").map(timelineEventTypeLabel);

    expect(labels).toEqual([
      "Preporuka izdata",
      "Akcija odbijena",
      "Akcija izvršena",
      "Ishod nije izmeren",
    ]);
    expect(labels.join(" → ")).not.toMatch(/done|success|0%/i);
    expect(timelineGapReasonLabel("no_measurement_evidence")).toBe("Nema merenog dokaza");
    expect(timelineGapReasonLabel("no_execution_proof")).toBe("Nema dokaza o izvršenju");
    expect(timelineEmptyReasonLabel("outside_period")).toBe("Nema događaja u izabranom periodu.");
    expect(timelineEmptyReasonLabel("no_measurement")).not.toMatch(/0%/);
  });
});

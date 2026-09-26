import { describe, expect, it } from "vitest";
import { toInclusiveCalendarDate, toUtcDateOnlyExclusive } from "../analyticsDateRanges";

describe("analytics whole-day ranges", () => {
  it("serializes the next UTC midnight as an exclusive upper bound", () => {
    expect(toUtcDateOnlyExclusive("2026-07-07")).toBe("2026-07-08T00:00:00.000Z");
  });

  it("renders an exclusive upper bound as the last included calendar date", () => {
    expect(toInclusiveCalendarDate("2026-07-08T00:00:00Z")).toBe("2026-07-07");
    expect(toInclusiveCalendarDate("2026-07-08")).toBe("2026-07-08");
  });
});

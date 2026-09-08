import { describe, expect, it } from "vitest";
import { createScheduleDraft, validateScheduleDraft } from "./inventoryUtils";

describe("validateScheduleDraft", () => {
  it("accepts the default schedule when required values are filled", () => {
    expect(validateScheduleDraft({
      ...createScheduleDraft(),
      name: "Dnevni report",
      recipientsCsv: "manager@example.com;retail@example.com",
    })).toBeNull();
  });

  it("rejects malformed recipients and times", () => {
    expect(validateScheduleDraft({
      ...createScheduleDraft(),
      name: "Dnevni report",
      recipientsCsv: "manager@example.com;bad",
    })).toContain("validne email");

    expect(validateScheduleDraft({
      ...createScheduleDraft(),
      name: "Dnevni report",
      recipientsCsv: "manager@example.com",
      runAtLocalTime: "25:99",
    })).toContain("HH:mm");
  });

  it("rejects invalid time zones and missing weekly days", () => {
    expect(validateScheduleDraft({
      ...createScheduleDraft(),
      name: "Nedeljni report",
      recipientsCsv: "manager@example.com",
      timeZoneId: "Not/AZone",
    })).toContain("Vremenska zona");

    expect(validateScheduleDraft({
      ...createScheduleDraft(),
      name: "Nedeljni report",
      recipientsCsv: "manager@example.com",
      frequency: "weekly",
      dayOfWeek: null,
    })).toContain("nedeljni");
  });
});

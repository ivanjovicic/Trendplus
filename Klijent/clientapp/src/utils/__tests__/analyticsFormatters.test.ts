import { describe, it, expect } from "vitest";
import { fmtRsd, fmtPct, fmtNumber, formatDate } from "../analyticsFormatters";

describe("analyticsFormatters", () => {
  it("fmtRsd returns fallback for null", () => {
    expect(fmtRsd(null)).toBe("Nije dostupno");
  });

  it("fmtRsd formats 1234 in sr-RS style and appends RSD", () => {
    expect(fmtRsd(1234)).toBe("1.234 RSD");
  });

  it("fmtNumber returns fallback for null", () => {
    expect(fmtNumber(null)).toBe("Nije dostupno");
  });

  it("fmtPct returns fallback for null", () => {
    expect(fmtPct(null)).toBe("Nije dostupno");
  });

  it("fmtPct appends percent sign for numeric values", () => {
    const out = fmtPct(12.34);
    expect(out).toContain("%");
    expect(out).not.toBe("Nije dostupno");
  });

  it("formats ISO dates as stable Serbian calendar dates in UTC", () => {
    expect(formatDate("2026-03-18T23:30:00-05:00")).toBe("19. 3. 2026.");
  });
});

import { describe, it, expect } from "vitest";
import { fmtRsd, fmtPct, fmtNumber } from "../analyticsFormatters";

describe("analyticsFormatters", () => {
  it("fmtRsd returns fallback for null", () => {
    expect(fmtRsd(null)).toBe("N/A");
  });

  it("fmtRsd formats 1234 in sr-RS style and appends RSD", () => {
    expect(fmtRsd(1234)).toBe("1.234 RSD");
  });

  it("fmtNumber returns fallback for null", () => {
    expect(fmtNumber(null)).toBe("N/A");
  });

  it("fmtPct returns fallback for null", () => {
    expect(fmtPct(null)).toBe("N/A");
  });

  it("fmtPct appends percent sign for numeric values", () => {
    const out = fmtPct(12.34);
    expect(out).toContain("%");
    expect(out).not.toBe("N/A");
  });
});

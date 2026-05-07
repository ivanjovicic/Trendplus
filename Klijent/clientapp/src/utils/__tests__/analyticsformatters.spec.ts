import { describe, it, expect } from "vitest";
import { fmtRsd, fmtRsdShort, fmtPct, fmtSignedPct, fmtQty } from "../analyticsFormatters";

describe("analyticsformatters", () => {
  it("formats RSD and includes currency", () => {
    expect(fmtRsd(123)).toContain("RSD");
    expect(fmtRsdShort(123)).toContain("RSD");
  });

  it("returns N/A for null/undefined percentages", () => {
    expect(fmtPct(null)).toBe("N/A");
    expect(fmtPct(undefined)).toBe("N/A");
  });

  it("fmtSignedPct shows + for positives and N/A for null", () => {
    expect(fmtSignedPct(2.5)).toContain("+");
    expect(fmtSignedPct(-1.2)).not.toContain("+");
    expect(fmtSignedPct(null)).toBe("N/A");
  });

  it("fmtQty appends unit", () => {
    expect(fmtQty(5)).toContain("kom");
  });
});

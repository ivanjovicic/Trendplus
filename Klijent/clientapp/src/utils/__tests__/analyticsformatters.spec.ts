import { describe, it, expect } from "vitest";
import { fmtRsd, fmtRsdShort, fmtPct, fmtSignedPct, fmtQty, fmtNumber, fmtPctFromRatio, getPresetRange } from "../analyticsFormatters";

describe("analyticsformatters", () => {
  it("formats RSD values and includes currency suffix", () => {
    expect(fmtRsd(1234)).toContain("RSD");
    expect(fmtRsdShort(1234)).toContain("RSD");
    expect(fmtRsd(1234)).toContain("1.234");
  });

  it("formats percentages and signed percentages", () => {
    expect(fmtPct(12.34, 1)).toBe("12,3%");
    expect(fmtSignedPct(2.5)).toContain("+");
    expect(fmtSignedPct(-1.2)).not.toContain("+");
  });

  it("returns N/A for null/undefined values", () => {
    expect(fmtPct(null)).toBe("N/A");
    expect(fmtPct(undefined)).toBe("N/A");
    expect(fmtSignedPct(null)).toBe("N/A");
    expect(fmtRsd(undefined)).toBe("N/A");
    expect(fmtNumber(undefined)).toBe("N/A");
    expect(fmtQty(null)).toBe("N/A");
  });

  it("fmtPctFromRatio keeps missing ratios as fallback and formats real ratios", () => {
    expect(fmtPctFromRatio(null, 1, "-")).toBe("-");
    expect(fmtPctFromRatio(undefined, 1, "-")).toBe("-");
    expect(fmtPctFromRatio(0.125, 1, "-")).toBe("12,5%");
  });

  it("keeps the caller's Serbian unavailable label for missing percentages", () => {
    expect(fmtPct(null, 1, "Nije dostupno")).toBe("Nije dostupno");
    expect(fmtPctFromRatio(null, 1, "Nije dostupno")).toBe("Nije dostupno");
  });

  it("fmtQty appends unit", () => {
    expect(fmtQty(5)).toContain("kom");
  });

  it("returns comparable ranges for standard presets", () => {
    const now = new Date(Date.UTC(2026, 4, 20, 12, 0, 0));

    expect(getPresetRange("30d", now)).toEqual({ fromDate: "2026-04-20", toDate: "2026-05-20" });
    expect(getPresetRange("90d", now)).toEqual({ fromDate: "2026-02-19", toDate: "2026-05-20" });
    expect(getPresetRange("180d", now)).toEqual({ fromDate: "2025-11-21", toDate: "2026-05-20" });
    expect(getPresetRange("365d", now)).toEqual({ fromDate: "2025-05-20", toDate: "2026-05-20" });
  });
});

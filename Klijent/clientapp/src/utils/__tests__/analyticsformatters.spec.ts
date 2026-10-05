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

  it("returns Nije dostupno for null/undefined values", () => {
    expect(fmtPct(null)).toBe("Nije dostupno");
    expect(fmtPct(undefined)).toBe("Nije dostupno");
    expect(fmtSignedPct(null)).toBe("Nije dostupno");
    expect(fmtRsd(undefined)).toBe("Nije dostupno");
    expect(fmtNumber(undefined)).toBe("Nije dostupno");
    expect(fmtQty(null)).toBe("Nije dostupno");
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

    expect(getPresetRange("30d", now)).toEqual({ fromDate: "2026-04-21", toDate: "2026-05-20" });
    expect(getPresetRange("90d", now)).toEqual({ fromDate: "2026-02-20", toDate: "2026-05-20" });
    expect(getPresetRange("180d", now)).toEqual({ fromDate: "2025-11-22", toDate: "2026-05-20" });
    expect(getPresetRange("365d", now)).toEqual({ fromDate: "2025-05-21", toDate: "2026-05-20" });
  });
});

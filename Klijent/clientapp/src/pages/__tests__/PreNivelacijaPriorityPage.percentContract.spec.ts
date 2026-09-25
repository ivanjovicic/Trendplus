import { describe, expect, it } from "vitest";
import {
  normalizePreNivelacijaPercentagePoints,
  normalizePreNivelacijaSignedPercentagePoints,
} from "../PreNivelacijaPriorityPage";
import { fmtPct } from "../../utils/analyticsFormatters";

describe("Pre-Nivelacija percentage contract", () => {
  it("treats percentage-point values literally so 1 means 1%", () => {
    expect(normalizePreNivelacijaPercentagePoints(1)).toBe(1);
    expect(fmtPct(normalizePreNivelacijaPercentagePoints(1), 2)).toBe(fmtPct(1, 2));
    expect(fmtPct(normalizePreNivelacijaPercentagePoints(1), 2)).not.toBe(fmtPct(100, 2));
  });

  it("accepts valid percentage points and rejects ambiguous or invalid magnitudes", () => {
    expect(normalizePreNivelacijaPercentagePoints(0)).toBe(0);
    expect(normalizePreNivelacijaPercentagePoints(0.5)).toBe(0.5);
    expect(normalizePreNivelacijaPercentagePoints(100)).toBe(100);
    expect(normalizePreNivelacijaPercentagePoints(null)).toBeNull();
    expect(normalizePreNivelacijaPercentagePoints(-1)).toBeNull();
    expect(normalizePreNivelacijaPercentagePoints(101)).toBeNull();
  });

  it("keeps signed WoW percentage-point decreases instead of clamping them away", () => {
    expect(normalizePreNivelacijaSignedPercentagePoints(-12.5)).toBe(-12.5);
    expect(normalizePreNivelacijaSignedPercentagePoints(0)).toBe(0);
    expect(normalizePreNivelacijaSignedPercentagePoints(4)).toBe(4);
    expect(normalizePreNivelacijaSignedPercentagePoints(null)).toBeNull();
    expect(normalizePreNivelacijaSignedPercentagePoints(Number.NaN)).toBeNull();
    expect(fmtPct(normalizePreNivelacijaSignedPercentagePoints(-12.5), 1)).toBe(fmtPct(-12.5, 1));
  });
});

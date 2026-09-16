import { describe, expect, it } from "vitest";
import {
  finiteToolbarCount,
  formatPrePostAnalysisWindowHint,
  resolveToolbarMetricsStatus,
} from "../prePostToolbarMetadata";

describe("prePostToolbarMetadata", () => {
  it("keeps measured zero while rejecting missing and non-finite counts", () => {
    expect(finiteToolbarCount(0)).toBe(0);
    expect(finiteToolbarCount(12)).toBe(12);
    expect(finiteToolbarCount(null)).toBeNull();
    expect(finiteToolbarCount(undefined)).toBeNull();
    expect(finiteToolbarCount(Number.NaN)).toBeNull();
    expect(finiteToolbarCount(Number.POSITIVE_INFINITY)).toBeNull();
  });

  it("preserves authoritative metrics status without defaulting to OK", () => {
    expect(resolveToolbarMetricsStatus("OK")).toBe("OK");
    expect(resolveToolbarMetricsStatus(" WARN ")).toBe("WARN");
    expect(resolveToolbarMetricsStatus(null)).toBeNull();
    expect(resolveToolbarMetricsStatus("")).toBeNull();
    expect(resolveToolbarMetricsStatus("   ")).toBeNull();
  });

  it("formats analysis-window hints for available and missing values", () => {
    expect(formatPrePostAnalysisWindowHint(30)).toBe(
      "Analiza poredjena po nivelacionom prozoru od 30 dana.",
    );
    expect(formatPrePostAnalysisWindowHint(0)).toBe(
      "Analiza poredjena po nivelacionom prozoru od 0 dana.",
    );
    expect(formatPrePostAnalysisWindowHint(null)).toBe(
      "Analiza poredjena po nivelacionom prozoru: prozor nije dostupan.",
    );
  });
});

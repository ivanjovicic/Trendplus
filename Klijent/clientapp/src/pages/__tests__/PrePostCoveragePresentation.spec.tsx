import { describe, expect, it } from "vitest";
import { describeNivelacijaImpactMetric as describeSupplierImpactMetric, describeNivelacijaUnitsImpactMetric } from "../SupplierSalesStatsPage";
import { describeNivelacijaImpactMetric as describeColorImpactMetric } from "../ColorSalesStatsPage";
import { describeNivelacijaImpactMetric as describeShoeTypeImpactMetric } from "../ShoeTypeSalesStatsPage";

const unknownCoverageValues = [null, undefined, Number.NaN, Number.POSITIVE_INFINITY, Number.NEGATIVE_INFINITY];
const invalidImpactValues = [Number.NaN, Number.POSITIVE_INFINITY, Number.NEGATIVE_INFINITY];

function supplierMetric(overrides: Record<string, unknown> = {}) {
  return {
    prePostNivelacijaRevenueImpactPct: null,
    prePostNivelacijaUnitsImpactPct: null,
    prePostNivelacijaRevenueCoveragePct: null,
    prePostSignalNote: null,
    preNivelacijePromet: 100,
    posleNivelacijePromet: 100,
    preNivelacijeKolicina: 10,
    posleNivelacijeKolicina: 10,
    ...overrides,
  };
}

function categoryMetric(overrides: Record<string, unknown> = {}) {
  return {
    prePostNivelacijaRevenueImpactPct: null,
    prePostNivelacijaRevenueCoveragePct: null,
    prePostSignalNote: null,
    preNivelacijePromet: 100,
    posleNivelacijePromet: 100,
    ...overrides,
  };
}

describe("pre/post coverage presentation guardrails", () => {
  it.each(unknownCoverageValues)("does not turn supplier coverage %s into a measured zero", (coverage) => {
    const result = describeSupplierImpactMetric(supplierMetric({ prePostNivelacijaRevenueCoveragePct: coverage }));

    expect(result.label).toBe("N/A");
    expect(result.title).toContain("nije dostupno");
  });

  it("keeps a genuine supplier zero coverage distinct from unknown coverage", () => {
    const result = describeSupplierImpactMetric(supplierMetric({ prePostNivelacijaRevenueCoveragePct: 0 }));

    expect(result.label).toBe("0% pokriće");
    expect(result.title).toContain("izmereno kao 0%");
  });

  it("preserves valid supplier impact and protects the units metric from unknown coverage", () => {
    expect(describeSupplierImpactMetric(supplierMetric({
      prePostNivelacijaRevenueImpactPct: -12.5,
      prePostNivelacijaRevenueCoveragePct: 75,
    })).label).toBe("-12,50%");

    for (const coverage of unknownCoverageValues) {
      expect(describeNivelacijaUnitsImpactMetric(supplierMetric({
        prePostNivelacijaRevenueCoveragePct: coverage,
      })).title).toContain("nije dostupno");
    }
  });

  it.each([
    ["Color", describeColorImpactMetric],
    ["Shoe Type", describeShoeTypeImpactMetric],
  ] as const)("keeps %s coverage states trustworthy", (_name, describeMetric) => {
    for (const coverage of unknownCoverageValues) {
      const result = describeMetric(categoryMetric({ prePostNivelacijaRevenueCoveragePct: coverage }));

      expect(result.label).toBe("N/A");
      expect(result.title).toContain("nije dostupno");
    }

    expect(describeMetric(categoryMetric({ prePostNivelacijaRevenueCoveragePct: 0 })).label).toBe("0% pokriće");
    expect(describeMetric(categoryMetric({
      prePostNivelacijaRevenueImpactPct: 8.25,
      prePostNivelacijaRevenueCoveragePct: 75,
    })).label).toBe("+8,25%");
  });

  it.each(invalidImpactValues)("does not expose invalid impact %s", (impact) => {
    const results = [
      describeSupplierImpactMetric(supplierMetric({
        prePostNivelacijaRevenueImpactPct: impact,
        prePostNivelacijaRevenueCoveragePct: 75,
      })),
      describeNivelacijaUnitsImpactMetric(supplierMetric({
        prePostNivelacijaUnitsImpactPct: impact,
        prePostNivelacijaRevenueCoveragePct: 75,
      })),
      describeColorImpactMetric(categoryMetric({
        prePostNivelacijaRevenueImpactPct: impact,
        prePostNivelacijaRevenueCoveragePct: 75,
      })),
      describeShoeTypeImpactMetric(categoryMetric({
        prePostNivelacijaRevenueImpactPct: impact,
        prePostNivelacijaRevenueCoveragePct: 75,
      })),
    ];

    for (const result of results) {
      expect(result.label).not.toMatch(/NaN|Infinity/);
      expect(result.label).toBe("N/A");
    }
  });
});

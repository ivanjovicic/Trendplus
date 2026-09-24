import { describe, expect, it } from "vitest";
import type { SupplierSalesStat, SupplierSalesStatsResponse } from "../../services/supplierSalesStatsApi";
import { buildDecisionSuppliers } from "../SupplierSalesStatsPage";

function buildSupplier(overrides: Partial<SupplierSalesStat> = {}): SupplierSalesStat {
  return {
    dobavljacId: 1,
    dobavljacNaziv: "Alfa",
    isUnknown: false,
    preNivelacijePromet: 0,
    preNivelacijeKolicina: 0,
    posleNivelacijePromet: 10000,
    posleNivelacijeKolicina: 10,
    ukupanPromet: 10000,
    ukupnaKolicina: 10,
    previousPeriodRevenue: 8000,
    previousPeriodUnits: 8,
    brojArtikalaSaNivelacijom: 0,
    brojArtikalaUkupno: 2,
    revenueWithCost: 10000,
    estimatedCostRevenue: 0,
    marginContribution: 4000,
    marginDataCoveragePct: 100,
    fallbackCostCoveragePct: 0,
    marginPct: 40,
    popRevenueChangePct: 25,
    popUnitsChangePct: 25,
    prePostNivelacijaRevenueImpactPct: null,
    prePostNivelacijaUnitsImpactPct: null,
    prePostNivelacijaRevenueCoveragePct: null,
    recommendation: {
      status: "maintain",
      label: "Maintain",
      summary: "Stabilan partner.",
      confidencePct: 80,
      reliabilityPct: 75,
      dataQualityStatus: "good",
      recommendationAllowed: true,
      reasonCodes: ["stable_margin"],
    },
    footwearBreakdown: [],
    ...overrides,
  };
}

function buildResponse(
  suppliers: SupplierSalesStat[],
  totals: Partial<SupplierSalesStatsResponse["totals"]> = {},
): SupplierSalesStatsResponse {
  return {
    generatedAt: "2026-07-01T08:00:00Z",
    fromDate: "2026-06-01",
    toDate: "2026-06-30",
    dataWindowFrom: null,
    dataWindowTo: null,
    sezonaId: null,
    storeId: null,
    suppliers,
    totals: {
      ukupanPromet: 10000,
      ukupanMarzniDoprinos: 4000,
      prosecnaMarza: 40,
      prePromet: 0,
      poslePromet: 10000,
      ukupnaKolicina: 10,
      preKolicina: 0,
      posleKolicina: 10,
      previousPeriodRevenue: 8000,
      previousPeriodUnits: 8,
      brojDobavljaca: suppliers.length,
      popRevenueChangePct: 25,
      popUnitsChangePct: 25,
      prePostNivelacijaRevenueImpactPct: null,
      prePostNivelacijaUnitsImpactPct: null,
      ...totals,
    },
    dataQuality: {
      missingCostQty: 0,
      missingCostRevenue: 0,
      missingCostRevenueSharePct: 0,
      unknownSupplierRevenue: 0,
      unknownSupplierRevenueSharePct: 0,
      revenueWithNivelacijaSplit: 0,
      revenueWithNivelacijaSplitSharePct: 0,
    },
    sezone: [],
  };
}

describe("buildDecisionSuppliers", () => {
  it("recomputes share-of-margin when only the total margin contribution changes", () => {
    const suppliers = [buildSupplier()];
    const baseline = buildDecisionSuppliers(buildResponse(suppliers, { ukupanMarzniDoprinos: 4000 }));
    const updated = buildDecisionSuppliers(buildResponse(suppliers, { ukupanMarzniDoprinos: 8000 }));

    expect(baseline[0]?.shareOfMarginContribution).toBe(100);
    expect(updated[0]?.shareOfMarginContribution).toBe(50);
  });

  it("recomputes share-of-units when only the total unit count changes", () => {
    const suppliers = [buildSupplier()];
    const baseline = buildDecisionSuppliers(buildResponse(suppliers, { ukupnaKolicina: 10 }));
    const updated = buildDecisionSuppliers(buildResponse(suppliers, { ukupnaKolicina: 20 }));

    expect(baseline[0]?.shareOfUnits).toBe(100);
    expect(updated[0]?.shareOfUnits).toBe(50);
  });

  it("recomputes recommendation projection when the supplier recommendation changes under the same supplier array", () => {
    const suppliers = [buildSupplier()];
    const baseline = buildDecisionSuppliers(buildResponse(suppliers));
    suppliers[0] = buildSupplier({
      recommendation: {
        status: "review",
        label: "Review",
        summary: "Proveriti maržu.",
        confidencePct: null,
        reliabilityPct: null,
        dataQualityStatus: "warning",
        recommendationAllowed: false,
        reasonCodes: ["margin_warning"],
      },
    });
    const updated = buildDecisionSuppliers(buildResponse(suppliers));

    expect(baseline[0]?.status).toBe("maintain");
    expect(baseline[0]?.recommendationAllowed).toBe(true);
    expect(updated[0]?.status).toBe("review");
    expect(updated[0]?.statusLabel).toBe("Oprez");
    expect(updated[0]?.recommendationAllowed).toBe(false);
    expect(updated[0]?.statusReason).toContain("Backend je blokirao izvrsenje preporuke");
  });

  it.each([
    ["increase_focus", "Pojačaj"],
    ["maintain", "Zadrži"],
    ["review", "Oprez"],
    ["do_not_trust", "Smanji / Ne veruj"],
    ["insufficient_data", "Nedovoljno podataka"],
  ] as const)("preserves backend %s status when recommendationAllowed is false", (backendStatus, expectedLabel) => {
    const suppliers = [
      buildSupplier({
        recommendation: {
          status: backendStatus,
          label: backendStatus,
          summary: "Test razlog.",
          confidencePct: null,
          reliabilityPct: null,
          dataQualityStatus: "warning",
          recommendationAllowed: false,
          reasonCodes: ["margin_warning"],
        },
      }),
    ];
    const rows = buildDecisionSuppliers(buildResponse(suppliers));

    expect(rows[0]?.status).toBe(backendStatus);
    expect(rows[0]?.statusLabel).toBe(expectedLabel);
    expect(rows[0]?.recommendationAllowed).toBe(false);
    expect(rows[0]?.statusReason).toContain("Backend je blokirao izvrsenje preporuke");
  });

  it("treats missing recommendationAllowed as blocked actionability without rewriting backend status", () => {
    const suppliers = [
      buildSupplier({
        recommendation: {
          status: "do_not_trust",
          label: "Do not trust",
          summary: "Signal zahteva proveru izvora.",
          confidencePct: null,
          reliabilityPct: null,
          dataQualityStatus: "critical",
          recommendationAllowed: undefined,
          reasonCodes: ["unknown_entity"],
        },
      }),
    ];
    const rows = buildDecisionSuppliers(buildResponse(suppliers));

    expect(rows[0]?.status).toBe("do_not_trust");
    expect(rows[0]?.statusLabel).toBe("Smanji / Ne veruj");
    expect(rows[0]?.recommendationAllowed).toBe(false);
    expect(rows[0]?.statusReason).toContain("Backend nije potvrdio da je preporuka izvrsna");
  });

  it("keeps zero totals as valid zero shares instead of unavailable values", () => {
    const suppliers = [buildSupplier({ ukupnaKolicina: 0, marginContribution: 0, ukupanPromet: 0 })];
    const rows = buildDecisionSuppliers(buildResponse(suppliers, {
      ukupanPromet: 0,
      ukupanMarzniDoprinos: 0,
      ukupnaKolicina: 0,
    }));

    expect(rows[0]?.sharePct).toBeNull();
    expect(rows[0]?.shareOfMarginContribution).toBeNull();
    expect(rows[0]?.shareOfUnits).toBeNull();
  });
});

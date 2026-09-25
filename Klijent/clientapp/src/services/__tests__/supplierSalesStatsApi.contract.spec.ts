import { afterEach, describe, expect, it, vi } from "vitest";
import { AnalyticsResponseValidationError } from "../../validation/analyticsResponseValidation";
import { getSupplierSalesStats } from "../supplierSalesStatsApi";

function validResponse(overrides: Record<string, unknown> = {}) {
  const recommendation = {
    status: "review",
    label: "Pregledati",
    summary: "Marža je ispod očekivanja.",
    confidencePct: 72,
    reliabilityPct: 68,
    dataQualityStatus: "warning",
    recommendationAllowed: true,
    reasonCodes: ["margin_quality_warning"],
  };
  const footwearBreakdown = {
    tipObuceId: 4,
    tipObuceNaziv: "Patike",
    ukupanPromet: 100,
    ukupnaKolicina: 2,
    brojArtikala: 1,
    totalCost: 120,
    marginContribution: -20,
    marginPct: -20,
    shareOfSupplierRevenuePct: 100,
    shareOfSupplierMarginContributionPct: null,
    previousPeriodRevenue: null,
    previousPeriodUnits: null,
    popRevenueChangePct: null,
    popUnitsChangePct: null,
    historicalCostRevenue: 100,
    historicalCostCoveragePct: 100,
    estimatedCostRevenue: 0,
    estimatedCostCoveragePct: 0,
    snapshotCostRevenue: 0,
    snapshotCostCoveragePct: 0,
    noCostRevenue: 0,
    noCostCoveragePct: 0,
    marginQualityLabel: "Upozorenje",
    marginQualityTier: "warning",
    marginQualityShortLabel: "Upozorenje",
    marginQualityTooltip: "Trošak je potvrđen, ali marža je negativna.",
  };
  const supplier = {
    dobavljacId: 7,
    dobavljacNaziv: "Dobavljač 7",
    isUnknown: false,
    preNivelacijePromet: 0,
    preNivelacijeKolicina: 0,
    posleNivelacijePromet: 100,
    posleNivelacijeKolicina: 2,
    ukupanPromet: 100,
    ukupnaKolicina: 2,
    brojArtikalaSaNivelacijom: 1,
    brojArtikalaUkupno: 1,
    revenueWithCost: 100,
    estimatedCostRevenue: 0,
    marginContribution: -20,
    marginDataCoveragePct: 100,
    fallbackCostCoveragePct: 0,
    marginPct: -20,
    totalCost: 120,
    historicalCostRevenue: 100,
    historicalCostCoveragePct: 100,
    estimatedCostCoveragePct: 0,
    snapshotCostRevenue: 0,
    snapshotCostCoveragePct: 0,
    noCostRevenue: 0,
    noCostCoveragePct: 0,
    isEstimatedMargin: false,
    marginQualityLabel: "Upozorenje",
    marginQualityTier: "warning",
    marginQualityShortLabel: "Upozorenje",
    marginQualityTooltip: "Trošak je potvrđen, ali marža je negativna.",
    revenueWithNivelacijaSplit: 100,
    comparableRevenueWithNivelacijaSplit: 100,
    previousPeriodRevenue: null,
    previousPeriodUnits: null,
    popRevenueChangePct: null,
    popUnitsChangePct: null,
    prePostNivelacijaRevenueImpactPct: null,
    prePostNivelacijaUnitsImpactPct: null,
    prePostNivelacijaRevenueCoveragePct: null,
    prePostSignalNote: null,
    prePostComparableArticleCount: 0,
    primaryFootwearType: "Patike",
    primaryFootwearTypeSharePct: 100,
    footwearTypeCount: 1,
    footwearBreakdown: [footwearBreakdown],
    sharePct: 100,
    shareOfMarginContribution: null,
    shareOfProfit: null,
    shareOfUnits: 100,
    reliabilityPct: 68,
    recommendation,
    promenaPrometa: null,
    promenaKolicine: null,
  };

  return {
    generatedAt: "2026-09-25T08:00:00Z",
    meta: {
      success: true,
      generatedAtUtc: "2026-09-25T08:00:00Z",
      dataQualityStatus: "warning",
      recommendationAllowed: true,
    },
    fromDate: "2026-09-01T00:00:00Z",
    toDate: "2026-09-25T23:59:59Z",
    dataWindowFrom: "2026-09-01T00:00:00Z",
    dataWindowTo: "2026-09-25T23:59:59Z",
    sezonaId: null,
    storeId: null,
    dataScope: "all",
    provenanceBasis: "live_query",
    recommendationAllowed: true,
    recommendationReferenceCohort: {
      scope: "all_response_suppliers",
      supplierCount: 1,
      includesUnknown: true,
      basis: "backend_supplier_response",
    },
    suppliers: [supplier],
    totals: {
      ukupanPromet: 100,
      ukupanMarzniDoprinos: -20,
      ukupanTrosak: 120,
      prosecnaMarza: -20,
      weightedMarginRevenue: 100,
      weightedMarginContribution: -20,
      marginBenchmarkBasis: "known_supplier_covered_revenue_weighted",
      historicalCostCoveragePct: 100,
      estimatedCostCoveragePct: 0,
      noCostCoveragePct: 0,
      snapshotCostRevenue: 0,
      snapshotCostCoveragePct: 0,
      isSnapshotActive: false,
      snapshotGeneratedAtUtc: null,
      isEstimatedMargin: false,
      marginQualityLabel: "Upozorenje",
      marginQualityTier: "warning",
      marginQualityShortLabel: "Upozorenje",
      marginQualityTooltip: "Trošak je potvrđen, ali marža je negativna.",
      prePromet: 0,
      poslePromet: 100,
      ukupnaKolicina: 2,
      preKolicina: 0,
      posleKolicina: 2,
      previousPeriodRevenue: null,
      previousPeriodUnits: null,
      brojDobavljaca: 1,
      brojDobavljacTipObuceKombinacija: 1,
      popRevenueChangePct: null,
      popUnitsChangePct: null,
      prePostNivelacijaRevenueImpactPct: null,
      prePostNivelacijaUnitsImpactPct: null,
      recommendationSummary: {
        increaseFocus: 0,
        maintain: 0,
        review: 1,
        doNotTrust: 0,
        insufficientData: 0,
      },
      promenaPrometaPct: null,
    },
    dataQuality: {
      missingCostQty: 0,
      missingCostRevenue: 0,
      missingCostRevenueSharePct: 0,
      noCostRevenue: 0,
      noCostRevenueSharePct: 0,
      costCoveredRevenue: 100,
      costCoveredRevenueSharePct: 100,
      historicalCostRevenue: 100,
      historicalCostRevenueSharePct: 100,
      snapshotCostRevenue: 0,
      snapshotCostRevenueSharePct: 0,
      estimatedCostRevenue: 0,
      estimatedCostRevenueSharePct: 0,
      costSourceBasis: "historical_sale_line_then_snapshot_then_product_fallback_then_unavailable",
      unknownSupplierRevenue: 0,
      unknownSupplierRevenueSharePct: 0,
      revenueWithNivelacijaSplit: 100,
      revenueWithNivelacijaSplitSharePct: 100,
    },
    sezone: [],
    ...overrides,
  };
}

describe("supplier sales runtime contract", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("accepts a valid negative margin contribution without weakening the payload boundary", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify(validResponse()), { status: 200 })));

    await expect(getSupplierSalesStats()).resolves.toMatchObject({
      suppliers: [expect.objectContaining({ marginContribution: -20 })],
      totals: expect.objectContaining({ ukupanMarzniDoprinos: -20 }),
    });
  });

  it("rejects a success payload without authoritative totals", async () => {
    const { totals: _totals, ...withoutTotals } = validResponse();
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify(withoutTotals), { status: 200 })));

    await expect(getSupplierSalesStats()).rejects.toBeInstanceOf(AnalyticsResponseValidationError);
  });

  it("rejects an out-of-range recommendation confidence before page derivation", async () => {
    const malformed = validResponse() as any;
    malformed.suppliers[0].recommendation.confidencePct = 101;
    vi.stubGlobal("fetch", vi.fn(async () => ({
      ok: true,
      status: 200,
      headers: new Headers({ "content-type": "application/json" }),
      json: async () => malformed,
    } as Response)));

    await expect(getSupplierSalesStats()).rejects.toBeInstanceOf(AnalyticsResponseValidationError);
  });
});

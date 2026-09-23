import { describe, expect, it } from "vitest";
import {
  colorSalesStatsResponseSchema,
  dailySalesTableResponseSchema,
  preNivelacijaPriorityResponseSchema,
  shoeTypeSalesStatsResponseSchema,
} from "../analyticsResponseSchemas";
import { AnalyticsResponseValidationError, validateAnalyticsResponse } from "../analyticsResponseValidation";

const colorRow = {
  boja: "Crna",
  preNivelacijePromet: 0,
  preNivelacijeKolicina: 0,
  posleNivelacijePromet: 10,
  posleNivelacijeKolicina: 1,
  ukupanPromet: 10,
  ukupnaKolicina: 1,
  previousPeriodRevenue: null,
  previousPeriodUnits: null,
  brojArtikalaSaNivelacijom: 0,
  brojArtikalaUkupno: 1,
  revenueWithCost: 10,
  estimatedCostRevenue: 0,
  marginContribution: 10,
  marginDataCoveragePct: null,
  fallbackCostCoveragePct: null,
  marginPct: 100,
  revenueWithNivelacijaSplit: 10,
  comparablePreRevenue: 0,
  comparablePostRevenue: 0,
  comparablePreQuantity: 0,
  comparablePostQuantity: 0,
  popRevenueChangePct: null,
  popUnitsChangePct: null,
  prePostNivelacijaRevenueImpactPct: null,
  prePostNivelacijaUnitsImpactPct: null,
  prePostNivelacijaRevenueCoveragePct: null,
  prePostSignalNote: "Nema artikala sa prodajom i pre i posle prve nivelacije.",
  prePostComparableArticleCount: 0,
  sharePct: 100,
  reliabilityPct: null,
  recommendation: {
    status: "insufficient_data",
    label: "Nedovoljno podataka",
    summary: "Nema dovoljno potvrđenih podataka za preporuku.",
    confidencePct: null,
    reliabilityPct: null,
    dataQualityStatus: "insufficient_data",
    recommendationAllowed: false,
    reasonCodes: ["prepost_comparable_cohort_unavailable"],
  },
};

const validColorResponse = {
  generatedAt: "2026-07-01T08:00:00Z",
  fromDate: "2026-06-01T00:00:00Z",
  toDate: "2026-07-01T00:00:00Z",
  dataWindowFrom: "2026-06-01T00:00:00Z",
  dataWindowTo: "2026-07-01T00:00:00Z",
  sezonaId: null,
  storeId: null,
  dataScope: "all",
  lineage: {
    storeId: null,
    dataScope: "all",
    eventCount: 0,
    eventArticleCount: 0,
    salesArticleCount: 1,
    salesArticlesWithMatchingNivelacija: 0,
    storePolicy: "all_stores_allowed",
    originPolicy: "all_origins_allowed",
  },
  colors: [colorRow],
  totals: {
    ukupanPromet: 10,
    ukupanMarzniDoprinos: 10,
    weightedKnownMarginPct: 100,
    weightedKnownMarginRevenue: 10,
    prePromet: 0,
    poslePromet: 10,
    ukupnaKolicina: 1,
    preKolicina: 0,
    posleKolicina: 1,
    comparablePreRevenue: 0,
    comparablePostRevenue: 0,
    comparablePreQuantity: 0,
    comparablePostQuantity: 0,
    comparableArticleCount: 0,
    comparableRevenueCoveragePct: null,
    prePostSignalNote: "Nema artikala sa prodajom i pre i posle prve nivelacije.",
    observedPreRevenue: 0,
    observedPostRevenue: 10,
    observedPreQuantity: 0,
    observedPostQuantity: 1,
    previousPeriodRevenue: null,
    previousPeriodUnits: null,
    popRevenueChangePct: null,
    popUnitsChangePct: null,
    prePostNivelacijaRevenueImpactPct: null,
    prePostNivelacijaUnitsImpactPct: null,
    recommendationSummary: {
      increaseFocus: 0,
      maintain: 0,
      review: 0,
      doNotTrust: 0,
      insufficientData: 1,
    },
  },
  dataQuality: {
    missingCostRevenue: 0,
    missingCostRevenueSharePct: null,
    unknownColorRevenue: 0,
    unknownColorRevenueSharePct: null,
    revenueWithNivelacijaSplit: 10,
    revenueWithNivelacijaSplitSharePct: 100,
    observedRevenueWithNivelacijaSplit: 10,
    observedRevenueWithNivelacijaSplitSharePct: 100,
    weightedKnownMarginPct: 100,
    weightedKnownMarginRevenue: 10,
  },
  sezone: [],
  meta: {
    success: true,
    generatedAtUtc: "2026-07-01T08:00:00Z",
    dataQualityStatus: "insufficient_data",
    recommendationAllowed: false,
  },
};

const validShoeResponse = {
  generatedAt: "2026-07-01T08:00:00Z",
  fromDate: "2026-06-01T00:00:00Z",
  toDate: "2026-07-01T00:00:00Z",
  dataWindowFrom: "2026-06-01T00:00:00Z",
  dataWindowTo: "2026-07-01T00:00:00Z",
  sezonaId: null,
  storeId: null,
  shoeTypes: [{
    tipObuceId: 1,
    tipObuceNaziv: "Patike",
    isUnknown: false,
    preNivelacijePromet: 0,
    preNivelacijeKolicina: 0,
    posleNivelacijePromet: 100,
    posleNivelacijeKolicina: 2,
    ukupanPromet: 100,
    ukupnaKolicina: 2,
    previousPeriodRevenue: null,
    previousPeriodUnits: null,
    brojArtikalaSaNivelacijom: 0,
    brojArtikalaUkupno: 1,
    revenueWithCost: 100,
    costCoveredRevenue: 100,
    costCoveredRevenueSharePct: 100,
    estimatedCostRevenue: 0,
    marginContribution: 40,
    marginDataCoveragePct: 100,
    fallbackCostCoveragePct: 0,
    marginPct: 40,
    totalCost: 60,
    historicalCostRevenue: 100,
    historicalCostCoveragePct: 100,
    estimatedCostCoveragePct: 0,
    snapshotCostRevenue: 0,
    snapshotCostCoveragePct: 0,
    noCostRevenue: 0,
    noCostCoveragePct: 0,
    isEstimatedMargin: false,
    marginQualityLabel: "Istorijski potvrđena",
    marginQualityTier: "confirmed",
    marginQualityShortLabel: "Potvrđena",
    marginQualityTooltip: "Pouzdan signal.",
    comparableRevenueWithNivelacijaSplit: 100,
    prePostSignalNote: null,
    prePostComparableArticleCount: 0,
    comparablePreRevenue: 0,
    comparablePostRevenue: 0,
    comparablePreQuantity: 0,
    comparablePostQuantity: 0,
    promenaPrometa: null,
    promenaKolicine: null,
    revenueWithNivelacijaSplit: 100,
    popRevenueChangePct: null,
    popUnitsChangePct: null,
    prePostNivelacijaRevenueImpactPct: null,
    prePostNivelacijaUnitsImpactPct: null,
    prePostNivelacijaRevenueCoveragePct: null,
    recommendation: {
      status: "insufficient_data",
      label: "Nedovoljno podataka",
      summary: "Nema dovoljno podataka.",
      confidencePct: null,
      reliabilityPct: null,
      dataQualityStatus: "critical",
      recommendationAllowed: false,
      reasonCodes: ["tiny_sample"],
    },
  }],
  totals: {
    ukupanPromet: 100,
    ukupanMarzniDoprinos: 40,
    ukupanTrosak: 60,
    prosecnaMarza: 40,
    weightedMarginRevenue: 100,
    prePromet: 0,
    poslePromet: 100,
    ukupnaKolicina: 2,
    preKolicina: 0,
    posleKolicina: 2,
    comparablePreRevenue: 0,
    comparablePostRevenue: 0,
    comparablePreQuantity: 0,
    comparablePostQuantity: 0,
    comparableArticleCount: 0,
    comparableRevenueCoveragePct: 0,
    observedPreRevenue: 0,
    observedPostRevenue: 100,
    observedPreQuantity: 0,
    observedPostQuantity: 2,
    previousPeriodRevenue: null,
    previousPeriodUnits: null,
    popRevenueChangePct: null,
    popUnitsChangePct: null,
    prePostNivelacijaRevenueImpactPct: null,
    prePostNivelacijaUnitsImpactPct: null,
    brojTipovaObuce: 1,
    historicalCostCoveragePct: 100,
    estimatedCostCoveragePct: 0,
    noCostCoveragePct: 0,
    snapshotCostRevenue: 0,
    snapshotCostCoveragePct: 0,
    isSnapshotActive: false,
    snapshotGeneratedAtUtc: null,
    isEstimatedMargin: false,
    marginQualityLabel: "Istorijski potvrđena",
    marginQualityTier: "confirmed",
    marginQualityShortLabel: "Potvrđena",
    marginQualityTooltip: "Pouzdan signal.",
  },
  dataQuality: {
    costCoveredRevenue: 100,
    costCoveredRevenueSharePct: 100,
    missingCostRevenue: 0,
    missingCostRevenueSharePct: 0,
    noCostRevenue: 0,
    noCostRevenueSharePct: 0,
    historicalCostRevenue: 100,
    historicalCostRevenueSharePct: 100,
    snapshotCostRevenue: 0,
    snapshotCostRevenueSharePct: 0,
    estimatedCostRevenue: 0,
    estimatedCostRevenueSharePct: 0,
    unknownTypeRevenue: 0,
    unknownTypeRevenueSharePct: 0,
    revenueWithNivelacijaSplit: 100,
    revenueWithNivelacijaSplitSharePct: 100,
  },
  sezone: [],
};

describe("analytics response schemas", () => {
  it("accepts valid zero, positive, and nullable unknown values", () => {
    expect(colorSalesStatsResponseSchema.safeParse(validColorResponse).success).toBe(true);
    expect(dailySalesTableResponseSchema.safeParse({
      requestedFrom: "2026-06-01",
      requestedTo: "2026-07-01",
      storeId: null,
      topN: 0,
      dataScope: "all",
      topSuppliers: [],
      topSuppliersOrder: [],
      dateRows: [],
      metadata: {
        totalDays: 0,
        uniqueSuppliersInRange: 0,
        unknownSupplierPct: 0,
        unknownSupplierItems: 0,
        offShiftItems: 0,
        offShiftRevenue: 0,
        totalItemsInRange: 0,
        duplicateReceiptGroupCount: 0,
        duplicateReceiptHeaderCount: 0,
        receiptAmountMismatchCount: 0,
        receiptAmountMismatchRevenue: 0,
        nonStandardReceiptCount: 0,
        nonStandardReceiptRevenue: 0,
        debtReceiptCount: 0,
        debtReceiptRevenue: 0,
        minAvailableDate: null,
        maxAvailableDate: null,
      },
    }).success).toBe(true);
  });

  it("validates Color decision scores as finite percentages on rows and totals", () => {
    expect(colorSalesStatsResponseSchema.safeParse({
      ...validColorResponse,
      colors: [{ ...colorRow, decisionScore: 42.5 }],
      totals: { ...validColorResponse.totals, decisionScore: 42.5 },
    }).success).toBe(true);

    for (const invalidScore of [-1, 101, Number.NaN, Number.POSITIVE_INFINITY]) {
      expect(colorSalesStatsResponseSchema.safeParse({
        ...validColorResponse,
        colors: [{ ...colorRow, decisionScore: invalidScore }],
      }).success).toBe(false);
      expect(colorSalesStatsResponseSchema.safeParse({
        ...validColorResponse,
        totals: { ...validColorResponse.totals, decisionScore: invalidScore },
      }).success).toBe(false);
    }
  });

  it("accepts signed Daily Sales quantities and revenues while keeping counters non-negative", () => {
    const result = dailySalesTableResponseSchema.safeParse({
      requestedFrom: "2026-06-01",
      requestedTo: "2026-07-01",
      storeId: null,
      topN: 15,
      dataScope: "all",
      topSuppliers: [],
      topSuppliersOrder: [],
      dateRows: [],
      metadata: {
        totalDays: 1,
        uniqueSuppliersInRange: 2,
        unknownSupplierPct: -25,
        unknownSupplierItems: -3,
        offShiftItems: -2,
        offShiftRevenue: -200,
        totalItemsInRange: -5,
        duplicateReceiptGroupCount: 0,
        duplicateReceiptHeaderCount: 0,
        receiptAmountMismatchCount: 0,
        receiptAmountMismatchRevenue: 0,
        nonStandardReceiptCount: 1,
        nonStandardReceiptRevenue: -50,
        debtReceiptCount: 1,
        debtReceiptRevenue: -150,
        minAvailableDate: null,
        maxAvailableDate: null,
      },
    });

    expect(result.success).toBe(true);
    expect(dailySalesTableResponseSchema.safeParse({
      ...result.success ? result.data : {},
      metadata: {
        ...(result.success ? result.data.metadata : {}),
        totalDays: -1,
      },
    }).success).toBe(false);
  });

  it("validates Shoe Type decision fields and preserves unavailable margin as null", () => {
    expect(shoeTypeSalesStatsResponseSchema.safeParse(validShoeResponse).success).toBe(true);
    expect(shoeTypeSalesStatsResponseSchema.safeParse({
      ...validShoeResponse,
      shoeTypes: [{
        ...validShoeResponse.shoeTypes[0],
        marginPct: null,
        recommendation: {
          ...validShoeResponse.shoeTypes[0].recommendation,
          recommendationAllowed: undefined,
        },
      }],
    }).success).toBe(false);
    expect(shoeTypeSalesStatsResponseSchema.safeParse({
      ...validShoeResponse,
      totals: { ...validShoeResponse.totals, prosecnaMarza: Number.NaN },
    }).success).toBe(false);
  });

  it("accepts signed Color revenue, quantity and cost evidence while keeping counters and percentages bounded", () => {
    const result = colorSalesStatsResponseSchema.safeParse({
      ...validColorResponse,
      colors: [{
        ...colorRow,
        preNivelacijePromet: -25,
        preNivelacijeKolicina: -2,
        posleNivelacijePromet: 5,
        posleNivelacijeKolicina: 1,
        ukupanPromet: -20,
        ukupnaKolicina: -1,
        revenueWithCost: -20,
        estimatedCostRevenue: -5,
        marginContribution: -15,
        marginDataCoveragePct: null,
        fallbackCostCoveragePct: null,
        marginPct: -75,
        revenueWithNivelacijaSplit: -20,
        sharePct: null,
      }],
      totals: {
        ...validColorResponse.totals,
        ukupanPromet: -20,
        ukupanMarzniDoprinos: -15,
        prePromet: -25,
        poslePromet: 5,
        ukupnaKolicina: -1,
        preKolicina: -2,
        posleKolicina: 1,
      },
      dataQuality: {
        ...validColorResponse.dataQuality,
        missingCostRevenue: 0,
        estimatedCostRevenue: -5,
        unknownColorRevenue: -20,
        revenueWithNivelacijaSplit: -20,
        missingCostRevenueSharePct: null,
        estimatedCostRevenueSharePct: null,
        unknownColorRevenueSharePct: null,
        revenueWithNivelacijaSplitSharePct: null,
        costQualityDenominatorStatus: "unavailable_non_positive_net_revenue",
      },
    });

    expect(result.success).toBe(true);
  });

  it.each([
    ["negative count", { ...colorRow, brojArtikalaUkupno: -1 }],
    ["percentage above 100", { ...colorRow, sharePct: 101 }],
    ["margin percentage above 100", { ...colorRow, marginPct: 101 }],
    ["NaN", { ...colorRow, marginContribution: Number.NaN }],
    ["Infinity", { ...colorRow, marginContribution: Number.POSITIVE_INFINITY }],
  ])("rejects %s instead of repairing it", (_caseName, invalidRow) => {
    const result = colorSalesStatsResponseSchema.safeParse({
      ...validColorResponse,
      colors: [invalidRow],
    });
    expect(result.success).toBe(false);
  });

  it("accepts signed Pre-Nivelacija sales evidence and explicit window provenance", () => {
    const result = preNivelacijaPriorityResponseSchema.safeParse({
      generatedAtUtc: "2026-07-01T08:00:00Z",
      formulaVersion: "pre_nivelacija_v3",
      formulaDescription: "Potpisani neto signal",
      summary: {
        supplierCount: 1,
        candidatesCount: 1,
        highPriorityCount: 1,
        increaseFocusCount: 0,
        maintainCount: 0,
        reviewCount: 0,
        doNotTrustCount: 0,
        insufficientDataCount: 1,
        totalStockAtRisk: 10,
        estimatedAvoidableMarkdownLoss: 0,
        expectedHighlightRevenueUplift: 0,
        averagePreNivelacijaScore: 75,
      },
      supplierLeaderboard: [{
        supplierId: 1,
        supplierName: "Dobavljač 1",
        highPrioritySkuCount: 1,
        candidateSkuCount: 1,
        stockUnitsAtRisk: 10,
        estimatedAvoidableMarkdownLoss: 0,
        expectedHighlightRevenueUplift: 0,
        actionScore: 10,
        weekOverWeekRiskDeltaPct: null,
        weekOverWeekEvidenceStatus: "unavailable_non_positive_denominator",
      }],
      filterFacets: {
        seasons: [{ id: 1, label: "Leto" }],
        footwearTypes: [{ id: 2, label: "Patike" }],
      },
      candidates: [{
        artikalId: 1,
        sku: "SKU-1",
        supplierId: 1,
        seasonId: 1,
        footwearTypeId: 2,
        supplierName: "Dobavljač 1",
        category: "Obuća",
        footwearType: "Patike",
        season: "Leto",
        stockUnits: 10,
        units180: -2,
        positiveUnits180: 3,
        negativeUnits180: -5,
        velocity180: -0.0111,
        daysSinceLastSale: 5,
        markdownEvents: 1,
        avgMarkdownPct: 10,
        grossMarginPctEst: 35,
        seasonRecencyBoost: 20,
        preNivelacijaScore: 75,
        scoreBreakdown: {
          stockPressure: 80,
          velocityRisk: 100,
          recencyRisk: 5,
          markdownOpportunity: 70,
          marginPotential: 58,
          seasonRecencyBoost: 20,
        },
        scenarioHighlightNow: { expectedUnits30d: 0, expectedRevenue30d: 0, expectedMargin30d: 0, effectivePrice: 1000 },
        scenarioMarkdownNow: { expectedUnits30d: 0, expectedRevenue30d: 0, expectedMargin30d: 0, effectivePrice: 900 },
        marginDeltaHighlightVsMarkdown: 0,
        revenueDeltaHighlightVsMarkdown: 0,
        hasCompleteEvidence: false,
        evidenceReason: "signed_sales_non_positive",
        reliabilityPct: 35,
        decisionScore: 50,
        priorityBand: "high",
        confidence: "Low",
        recommendationAllowed: false,
        salesEvidenceStatus: "non_positive_net_with_returns",
        salesEvidenceReason: "signed_sales_non_positive",
        recommendation: {
          status: "insufficient_data",
          label: "Nedovoljno podataka",
          summary: "Signal nije dovoljno jak za pouzdanu preporuku.",
          confidencePct: 20,
          reliabilityPct: 35,
          dataQualityStatus: "insufficient_data",
          recommendationAllowed: false,
          reasonCodes: ["signed_sales_non_positive"],
        },
      }],
      queues: { highlightNow: [], monitor: [], likelyMarkdownSoon: [] },
      alerts: [],
      page: 1,
      pageSize: 20,
      totalCandidates: 1,
      recommendationAllowed: false,
      evidenceWindow: {
        salesWindowFromUtc: "2026-01-02T08:00:00Z",
        salesWindowToUtc: "2026-07-01T08:00:00Z",
        markdownWindowFromUtc: "2026-01-02T08:00:00Z",
        markdownWindowToUtc: "2026-07-01T08:00:00Z",
        timezone: "UTC",
        salesQuantityPolicy: "signed_net_quantity_preserved",
        nonPositiveNetPolicy: "recommendation_unavailable",
        previousWeekDenominatorPolicy: "unavailable_when_non_positive",
        candidatesWithReturns: 1,
        candidatesWithNonPositiveNetSales: 1,
        candidatesWithoutSalesInWindow: 0,
        suppliersWithUnavailablePreviousWeekDenominator: 1,
      },
      meta: { success: true, dataQualityStatus: "insufficient_data" },
    });

    expect(result.success).toBe(true);

    expect(preNivelacijaPriorityResponseSchema.safeParse({
      ...result.success ? result.data : {},
      candidates: result.success
        ? [{
            ...result.data.candidates[0],
            recommendation: { ...result.data.candidates[0].recommendation, status: "unknown" },
          }]
        : [],
    }).success).toBe(false);

    expect(preNivelacijaPriorityResponseSchema.safeParse({
      ...result.success ? result.data : {},
      candidates: result.success
        ? [{
            ...result.data.candidates[0],
            recommendation: { ...result.data.candidates[0].recommendation, recommendationAllowed: undefined },
          }]
        : [],
    }).success).toBe(false);

    expect(preNivelacijaPriorityResponseSchema.safeParse({
      ...result.success ? result.data : {},
      candidates: result.success
        ? [{ ...result.data.candidates[0], preNivelacijaScore: 101 }]
        : [],
    }).success).toBe(false);

    expect(preNivelacijaPriorityResponseSchema.safeParse({
      ...result.success ? result.data : {},
      candidates: result.success
        ? [{
            ...result.data.candidates[0],
            recommendation: { ...result.data.candidates[0].recommendation, reasonCodes: [null] },
          }]
        : [],
    }).success).toBe(false);

    expect(preNivelacijaPriorityResponseSchema.safeParse({
      ...result.success ? result.data : {},
      queues: {
        highlightNow: [{ artikalId: 1, sku: "SKU-1" }],
        monitor: [],
        likelyMarkdownSoon: [],
      },
    }).success).toBe(false);

    expect(preNivelacijaPriorityResponseSchema.safeParse({
      ...result.success ? result.data : {},
      alerts: [{ type: "bad", severity: "warning", message: "", supplierName: null, artikalId: null }],
    }).success).toBe(false);

    expect(preNivelacijaPriorityResponseSchema.safeParse({
      ...result.success ? result.data : {},
      evidenceWindow: result.success
        ? { ...result.data.evidenceWindow, salesWindowFromUtc: "not-a-date" }
        : null,
    }).success).toBe(false);
  });

  it("rejects malformed dates and missing required response sections", () => {
    expect(colorSalesStatsResponseSchema.safeParse({
      ...validColorResponse,
      generatedAt: "not-a-date",
    }).success).toBe(false);
    expect(colorSalesStatsResponseSchema.safeParse({
      ...validColorResponse,
      totals: undefined,
    }).success).toBe(false);
  });

  it("requires the backend recommendation and trust context before page derivation", () => {
    expect(colorSalesStatsResponseSchema.safeParse({
      ...validColorResponse,
      colors: [{ ...colorRow, recommendation: undefined }],
    }).success).toBe(false);
    expect(colorSalesStatsResponseSchema.safeParse({
      ...validColorResponse,
      colors: [{
        ...colorRow,
        recommendation: { ...colorRow.recommendation, recommendationAllowed: "false" },
      }],
    }).success).toBe(false);
    expect(colorSalesStatsResponseSchema.safeParse({
      ...validColorResponse,
      meta: undefined,
    }).success).toBe(false);
    expect(colorSalesStatsResponseSchema.safeParse({
      ...validColorResponse,
      lineage: { ...validColorResponse.lineage, salesArticleCount: -1 },
    }).success).toBe(false);
  });

  it("raises a controlled validation error without producing fake zero values", () => {
    expect(() => validateAnalyticsResponse(
      { ...validColorResponse, colors: [{ ...colorRow, brojArtikalaUkupno: -1 }] },
      colorSalesStatsResponseSchema,
      "Color sales",
    )).toThrow(AnalyticsResponseValidationError);
  });
});

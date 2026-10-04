import { describe, expect, it } from "vitest";
import {
  analyticsResponseMetaSchema,
  colorSalesStatsResponseSchema,
  dailySalesTableResponseSchema,
  inventoryInsightsResponseSchema,
  preNivelacijaPriorityResponseSchema,
  shoeTypeSalesStatsResponseSchema,
  supplierSalesStatsResponseSchema,
  vendorSalesNivelacijaResponseSchema,
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

describe("analytics response trust metadata", () => {
  it("accepts backend readiness plus context-bound Operations integrity evidence", () => {
    const meta = analyticsResponseMetaSchema.parse({
      success: true,
      decisionReadiness: {
        state: "blocked",
        surfaceRole: "recommendation",
        recommendationAllowed: false,
        reasonCodes: ["data_quality_blocks_decision"],
        evidenceReferences: ["inventory.stock"],
        repairPath: "Inventory refresh status",
      },
      operationsIntegrityStatus: "drift_detected",
      operationsIntegrityCheckedAtUtc: "2026-10-04T13:00:00Z",
      operationsIntegrityEvidenceId: "inventory-evidence-1",
      operationsIntegrityFamily: "inventory",
      operationsIntegrityContextFingerprint: "integrity-context-1",
      operationsIntegritySourceGeneration: "generation-1",
      operationsIntegrityContextMatches: true,
    });

    expect(meta.decisionReadiness?.state).toBe("blocked");
    expect(meta.operationsIntegrityEvidenceId).toBe("inventory-evidence-1");
    expect(meta.operationsIntegrityContextMatches).toBe(true);
  });
});

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

const validSupplierResponse = {
  generatedAt: "2026-07-01T08:00:00Z",
  meta: { success: true, dataQualityStatus: "warning", recommendationAllowed: true },
  fromDate: "2026-06-01T00:00:00Z",
  toDate: "2026-07-01T00:00:00Z",
  dataWindowFrom: "2026-01-01T00:00:00Z",
  dataWindowTo: "2026-07-01T00:00:00Z",
  sezonaId: 1,
  storeId: null,
  dataScope: "all",
  provenanceBasis: "live_query",
  recommendationAllowed: true,
  recommendationReferenceCohort: {
    scope: "all_response_suppliers",
    supplierCount: 1,
    includesUnknown: false,
    basis: "backend_supplier_response",
  },
  suppliers: [{
    dobavljacId: 7,
    dobavljacNaziv: "Dobavljač A",
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
    footwearBreakdown: [{
      tipObuceId: 3,
      tipObuceNaziv: "Patike",
      ukupanPromet: 100,
      ukupnaKolicina: 2,
      brojArtikala: 1,
      totalCost: 60,
      marginContribution: 40,
      marginPct: 40,
      shareOfSupplierRevenuePct: 100,
      shareOfSupplierMarginContributionPct: 100,
      previousPeriodRevenue: null,
      previousPeriodUnits: null,
      popRevenueChangePct: null,
      popUnitsChangePct: null,
    }],
    sharePct: 100,
    shareOfMarginContribution: 100,
    shareOfProfit: 100,
    shareOfUnits: 100,
    reliabilityPct: 90,
    recommendation: {
      status: "maintain",
      label: "Zadrži",
      summary: "Signal je dovoljno pouzdan.",
      confidencePct: 80,
      reliabilityPct: 90,
      dataQualityStatus: "good",
      recommendationAllowed: true,
      reasonCodes: [],
    },
    promenaKolicine: null,
  }],
  totals: {
    ukupanPromet: 100,
    ukupanMarzniDoprinos: 40,
    ukupanTrosak: 60,
    prosecnaMarza: 40,
    weightedMarginRevenue: 100,
    weightedMarginContribution: 40,
    marginBenchmarkBasis: "known_supplier_covered_revenue_weighted",
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
      maintain: 1,
      review: 0,
      doNotTrust: 0,
      insufficientData: 0,
    },
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
    revenueWithNivelacijaSplit: 0,
    revenueWithNivelacijaSplitSharePct: null,
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

  it("accepts negative Access supplier identifiers for Daily Sales, PreNivelacija and inventory insights", () => {
    const daily = dailySalesTableResponseSchema.safeParse({
      requestedFrom: "2026-07-06",
      requestedTo: "2026-08-05",
      storeId: null,
      topN: 15,
      dataScope: "all",
      topSuppliers: [
        {
          supplierId: -2122024036,
          supplierName: "BIS",
          isUnknown: false,
          unknownReason: null,
          attributionBasis: "frozen_current_master_backfill",
          totalQty: 10,
          totalRevenue: 1000,
        },
        {
          supplierId: 0,
          supplierName: "Zero",
          isUnknown: false,
          totalQty: 1,
          totalRevenue: 100,
        },
        {
          supplierId: null,
          supplierName: "Nepoznat dobavljač",
          isUnknown: true,
          unknownReason: "missing_attribution",
          totalQty: 2,
          totalRevenue: 200,
        },
      ],
      topSuppliersOrder: ["BIS", "Zero", "Nepoznat dobavljač"],
      dateRows: [],
      metadata: {
        totalDays: 1,
        uniqueSuppliersInRange: 3,
        unknownSupplierPct: 10,
        unknownSupplierItems: 2,
        offShiftItems: 0,
        offShiftRevenue: 0,
        totalItemsInRange: 13,
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
    });
    expect(daily.success).toBe(true);

    for (const invalid of [1.5, Number.NaN, Number.POSITIVE_INFINITY]) {
      expect(dailySalesTableResponseSchema.safeParse({
        requestedFrom: "2026-07-06",
        requestedTo: "2026-08-05",
        storeId: null,
        topN: 1,
        dataScope: "all",
        topSuppliers: [{
          supplierId: invalid,
          supplierName: "X",
          isUnknown: false,
          totalQty: 1,
          totalRevenue: 1,
        }],
        topSuppliersOrder: ["X"],
        dateRows: [],
        metadata: {
          totalDays: 1,
          uniqueSuppliersInRange: 1,
          unknownSupplierPct: 0,
          unknownSupplierItems: 0,
          offShiftItems: 0,
          offShiftRevenue: 0,
          totalItemsInRange: 1,
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

  it("accepts negative Access shoe type, store and season identifiers on Color, Shoe Type and Supplier stats", () => {
    const negativeSeason = { id: -7, naziv: "Arhivska sezona", datumOd: "2025-01-01", datumDo: "2025-06-30" };
    const shoe = shoeTypeSalesStatsResponseSchema.safeParse({
      ...validShoeResponse,
      storeId: -598733481,
      sezone: [negativeSeason],
      shoeTypes: [{ ...validShoeResponse.shoeTypes[0], tipObuceId: -2004188974, tipObuceNaziv: "Ž.Cipela" }],
    });
    expect(shoe.success).toBe(true);

    expect(colorSalesStatsResponseSchema.safeParse({
      ...validColorResponse,
      storeId: -598733481,
      sezonaId: -7,
      lineage: { ...validColorResponse.lineage, storeId: -598733481 },
      sezone: [negativeSeason],
    }).success).toBe(true);

    expect(supplierSalesStatsResponseSchema.safeParse({
      ...validSupplierResponse,
      storeId: -598733481,
      sezone: [negativeSeason],
    }).success).toBe(true);

    expect(shoeTypeSalesStatsResponseSchema.safeParse({
      ...validShoeResponse,
      shoeTypes: [{ ...validShoeResponse.shoeTypes[0], tipObuceId: -2004188974.5 }],
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

  it("accepts older Shoe Type payloads without row-level margin presentation fields", () => {
    const { marginQualityTier, marginQualityShortLabel, marginQualityTooltip, ...legacyRow } = validShoeResponse.shoeTypes[0];
    const result = shoeTypeSalesStatsResponseSchema.safeParse({
      ...validShoeResponse,
      shoeTypes: [legacyRow],
    });

    expect(result.success).toBe(true);
  });

  it("validates Supplier Sales decision and cost-quality fields without hiding signed margin evidence", () => {
    expect(supplierSalesStatsResponseSchema.safeParse(validSupplierResponse).success).toBe(true);

    const signedSupplierResponse = {
      ...validSupplierResponse,
      suppliers: [{
        ...validSupplierResponse.suppliers[0],
        ukupanPromet: -20,
        ukupnaKolicina: -1,
        revenueWithCost: -20,
        marginContribution: -150,
        marginPct: -750,
      }],
      totals: {
        ...validSupplierResponse.totals,
        ukupanPromet: -20,
        ukupanMarzniDoprinos: -150,
        prosecnaMarza: null,
        weightedMarginRevenue: -20,
        weightedMarginContribution: -150,
      },
    };
    expect(supplierSalesStatsResponseSchema.safeParse(signedSupplierResponse).success).toBe(true);

    expect(supplierSalesStatsResponseSchema.safeParse({
      ...validSupplierResponse,
      totals: { ...validSupplierResponse.totals, weightedMarginRevenue: Number.NaN },
    }).success).toBe(false);
    expect(supplierSalesStatsResponseSchema.safeParse({
      ...validSupplierResponse,
      suppliers: [{
        ...validSupplierResponse.suppliers[0],
        recommendation: {
          ...validSupplierResponse.suppliers[0].recommendation,
          confidencePct: 101,
        },
      }],
    }).success).toBe(false);
    expect(supplierSalesStatsResponseSchema.safeParse({
      ...validSupplierResponse,
      dataQuality: { ...validSupplierResponse.dataQuality, costSourceBasis: "" },
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

  it("accepts signed Color net-sales shares with explicit basis metadata", () => {
    const result = colorSalesStatsResponseSchema.safeParse({
      ...validColorResponse,
      colors: [{
        ...colorRow,
        sharePct: -25,
        sharePctBasis: "net_sales_signed",
        sharePctNumerator: -500,
        sharePctDenominator: 2000,
        sharePctUnavailableReason: null,
      }],
    });

    expect(result.success).toBe(true);
  });

  it("accepts signed Pre-Nivelacija sales evidence and explicit window provenance", () => {
    const result = preNivelacijaPriorityResponseSchema.safeParse({
      generatedAtUtc: "2026-07-01T08:00:00Z",
      formulaVersion: "pre_nivelacija_v3",
      formulaDescription: "Potpisani neto signal",
      modelEvidence: {
        scoreBasis: "cohort_relative_max_ratio",
        scoreReferencePopulation: "base_candidate_universe_before_dimension_filters",
        scoreNormalization: "stock_and_velocity_divided_by_reference_population_max",
        scenarioBasis: "heuristic_uncalibrated",
        scenarioParameterVersion: "pre_nivelacija_scenario_v1",
        scenarioDisclaimer: "heuristic_estimate_not_causal_or_calibrated_uplift",
      },
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
        totalStockAtRiskCoverageEligible: 1,
        totalStockAtRiskCoverageTotal: 1,
        estimatedAvoidableMarkdownLoss: null,
        estimatedAvoidableMarkdownLossCoverageEligible: 0,
        estimatedAvoidableMarkdownLossCoverageTotal: 1,
        expectedHighlightRevenueUplift: null,
        expectedHighlightRevenueUpliftCoverageEligible: 0,
        expectedHighlightRevenueUpliftCoverageTotal: 1,
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
        storeId: 1,
        storeName: "Objekat #1",
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
        receiptPopulationPolicy: "certified_retail_excludes_trimmed_case_insensitive_dug_korekcija",
        salesQuantityPolicy: "signed_net_quantity_preserved",
        signedReturnPolicy: "included_in_signed_net_positive_net_remains_actionable",
        lastSaleRecencyPolicy: "latest_positive_retail_sale_only",
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

    if (result.success) {
      const negativeIds = preNivelacijaPriorityResponseSchema.safeParse({
        ...result.data,
        filterFacets: {
          seasons: [{ id: -7, label: "Arhivska sezona" }],
          footwearTypes: [{ id: -2004188974, label: "Ž.Cipela" }],
          stores: [{ id: -598733481, label: "Objekat sa negativnim ID" }],
          suppliers: [{ id: -2122024036, label: "BIS" }],
        },
        candidates: [{
          ...result.data.candidates[0],
          storeId: -598733481,
          supplierId: -2122024036,
          seasonId: -7,
          footwearTypeId: -2004188974,
        }],
        queues: {
          highlightNow: [{
            artikalId: 1,
            sku: "SKU-1",
            storeId: -598733481,
            storeName: "Objekat sa negativnim ID",
            supplierName: "BIS",
            preNivelacijaScore: 75,
            priorityBand: "high",
            owner: "merch",
            status: "open",
            dueDateUtc: "2026-07-08T08:00:00Z",
          }],
          monitor: [],
          likelyMarkdownSoon: [],
        },
      });
      expect(negativeIds.success).toBe(true);

      expect(preNivelacijaPriorityResponseSchema.safeParse({
        ...result.data,
        candidates: [{ ...result.data.candidates[0], footwearTypeId: -1.5 }],
      }).success).toBe(false);
    }

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

  it("accepts an explicit analytics context fingerprint and unavailable state", () => {
    const withContext = {
      ...validColorResponse,
      meta: {
        ...validColorResponse.meta,
        context: {
          contractVersion: "analytics_context_v1",
          state: "available",
          fingerprint: `sha256:${"a".repeat(64)}`,
          dateBoundaryConvention: "half_open_utc",
          requestedPeriodFromUtc: "2026-06-01T00:00:00Z",
          requestedPeriodToUtc: "2026-07-01T00:00:00Z",
          effectivePeriodFromUtc: "2026-06-01T00:00:00Z",
          effectivePeriodToUtc: "2026-07-01T00:00:00Z",
          populationKey: "certified_retail_sales",
          populationFilters: { storeId: null },
          sourceDataset: "certified_sales_rows",
          sourceGeneration: "sales_header_origin_v1",
          formulaVersion: "sales_context_v1",
          materializerGeneration: "live_query",
          rowLimitSemantics: "all_filtered_rows",
        },
      },
    };

    expect(colorSalesStatsResponseSchema.safeParse(withContext).success).toBe(true);
    expect(colorSalesStatsResponseSchema.safeParse({
      ...withContext,
      meta: {
        ...withContext.meta,
        context: {
          contractVersion: "analytics_context_v1",
          state: "unavailable",
          fingerprint: null,
          unavailableReason: "missing_context_fields:sourceGeneration",
        },
      },
    }).success).toBe(true);
  });

  it("accepts unknown vendor-sales-nivelacija change percent instead of requiring a fake zero", () => {
    const driverMetrics = {
      momentumRevenue: { mean: 0, median: 0, sampleCount: 2, meanWeighting: "unweighted" },
      elasticity: { mean: 0.5, median: 0.4, sampleCount: 2, meanWeighting: "post_revenue_weighted" },
      didRevenue: { mean: null, median: null, sampleCount: 0, meanWeighting: "unweighted" },
      lostSalesOOS: { mean: null, median: null, sampleCount: 0, meanWeighting: "unweighted" },
    };
    const vendor = {
      vendorId: 203,
      vendorName: "Gama",
      preQty: 0,
      preRevenue: 0,
      postQty: 0,
      postRevenue: 0,
      changeQty: 0,
      changeRevenue: 0,
      changePercent: null,
      absoluteChangeRevenue: 0,
      changeSharePercent: 0,
      postRevenueSharePercent: 0,
      avgCoveragePre30: null,
      avgCoveragePost30: null,
      hasComparableSalesWindow: false,
      semanticChangePercentRevenue: null,
      articleCount: 0,
      activeArticlesCount: 0,
      increasedPriceArticlesCount: 0,
      decreasedPriceArticlesCount: 0,
      reliabilityPct: 35,
      driverMetrics,
    };
    const response = {
      generatedAt: "2026-10-01T08:00:00Z",
      windowDays: 30,
      vendorId: null,
      eventDate: null,
      from: null,
      to: null,
      category: null,
      includeInactive: false,
      storeId: null,
      dataScope: "all",
      scopeApplied: true,
      categories: [],
      vendorStats: [vendor],
      articleStats: [],
      totals: {
        preQty: 0,
        preRevenue: 0,
        postQty: 0,
        postRevenue: 0,
        changeQty: 0,
        changeRevenue: 0,
        changePercent: null,
        vendorsCount: 0,
        articlesCount: 0,
        activeArticlesCount: 0,
        avgRevenuePerArticlePre: 0,
        avgRevenuePerArticlePost: 0,
        avgPriceChangePercent: null,
        absoluteChangeRevenue: 0,
        avgCoveragePre30: null,
        avgCoveragePost30: null,
        hasComparableSalesWindow: false,
        comparableRows: 0,
        comparableArticlesCount: 0,
        comparableVendorsCount: 0,
      },
      categoryStats: [{
        category: "Patike",
        articlesCount: 0,
        vendorsCount: 0,
        preQty: 0,
        preRevenue: 0,
        postQty: 0,
        postRevenue: 0,
        changeQty: 0,
        changeRevenue: 0,
        changePercent: null,
        hasComparableSalesWindow: false,
        comparableArticleCount: 0,
      }],
      priceDirectionStats: [{
        segment: "Cena nije dostupna",
        articlesCount: 0,
        vendorsCount: 0,
        avgPriceChangePercent: null,
        changeRevenue: 0,
        changePercent: null,
        hasComparableSalesWindow: false,
        comparableArticleCount: 0,
      }],
      insights: [],
      driverMetrics,
    };

    expect(vendorSalesNivelacijaResponseSchema.safeParse(response).success).toBe(true);
    expect(vendorSalesNivelacijaResponseSchema.safeParse({
      ...response,
      vendorStats: [{ ...vendor, changePercent: Number.NaN }],
    }).success).toBe(false);
    expect(vendorSalesNivelacijaResponseSchema.safeParse({
      ...response,
      driverMetrics: {
        ...response.driverMetrics,
        didRevenue: { ...response.driverMetrics.didRevenue, sampleCount: -1 },
      },
    }).success).toBe(false);
  });
});

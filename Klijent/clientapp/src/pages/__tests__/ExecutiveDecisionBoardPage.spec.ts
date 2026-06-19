import { describe, expect, it } from "vitest";
import { buildExecutiveDecisionBoardModel } from "../ExecutiveDecisionBoardPage";

function createBasePayload(): Parameters<typeof buildExecutiveDecisionBoardModel>[0] {
  return {
    refreshStatus: {
      lastSuccessfulRefreshAtUtc: "2026-06-19T09:00:00Z",
      lastAttemptAtUtc: "2026-06-19T09:05:00Z",
      lastFailureAtUtc: null,
      isRunning: false,
      lastErrorMessage: null,
      currentStep: null,
      refreshedObjects: [],
      failedObjects: [],
      durationSeconds: 21,
      dataFreshnessStatus: "fresh",
      processMode: "worker",
      processType: "worker",
      workersEnabled: true,
      generatedAtUtc: "2026-06-19T09:05:00Z",
      jobs: [],
    },
    dashboard: null,
    dataQualityHealth: {
      generatedAt: "2026-06-19T09:00:00Z",
      lookbackDays: 30,
      windowFrom: "2026-05-20T00:00:00Z",
      windowTo: "2026-06-19T00:00:00Z",
      orphanArticleCount: 0,
      totalRevenue: 100000,
      missingCostRevenue: 0,
      missingCostRevenueSharePct: 0,
      unknownSupplierRevenue: 0,
      unknownSupplierRevenueSharePct: 0,
      score: 92,
      scoreStatus: "good",
      scoreSummary: "Kvalitet podataka je dobar.",
      thresholds: {
        orphanArticleCount: 10,
        missingCostRevenueSharePct: 5,
        unknownSupplierRevenueSharePct: 5,
      },
      meta: { success: true, dataQualityStatus: "good" },
    },
    pilotIntake: {
      generatedAtUtc: "2026-06-19T09:00:00Z",
      periodFromUtc: "2026-05-20T00:00:00Z",
      periodToUtc: "2026-06-19T00:00:00Z",
      dataScope: "all",
      storeId: null,
      supplierId: null,
      lastImportAtUtc: "2026-06-18T09:00:00Z",
      lastRefreshAtUtc: "2026-06-19T09:00:00Z",
      readinessStatus: "good",
      readinessLabel: "Spremno",
      readinessScore: 88,
      loadedData: {
        articlesCount: 10,
        saleItemsCount: 20,
        receiptsCount: 6,
        suppliersCount: 4,
        storesCount: 2,
        firstSaleDate: "2026-05-20T00:00:00Z",
        lastSaleDate: "2026-06-19T00:00:00Z",
      },
      issues: {
        missingSupplierCount: 0,
        missingCostCount: 0,
        missingCategoryCount: 0,
        saleWithoutArticleCount: 0,
        zeroOrNegativePriceCount: 0,
        missingSupplierNameCount: 0,
      },
      impact: {
        revenueWithoutCostPercent: 0,
        articlesWithoutSupplierPercent: 0,
        recommendationsBlockedCount: 0,
        ignoredRowsCount: 0,
        insufficientSignalCount: 0,
      },
      recommendedActions: [],
      meta: { success: true, dataQualityStatus: "good" },
    },
    productDecisionCenter: {
      generatedAtUtc: "2026-06-19T09:00:00Z",
      periodFromUtc: "2026-06-01T00:00:00Z",
      periodToUtc: "2026-06-19T00:00:00Z",
      totalRows: 1,
      analyzedRows: 1,
      ignoredRowsCount: 0,
      summary: {
        replenishCount: 1,
        markdownCount: 0,
        highPotentialCount: 1,
        badDataCount: 0,
        lostSalesEstimate: 120000,
        slowStockCapital: 0,
      },
      rows: [
        {
          productId: 1,
          recommendationId: "rec-1",
          sourceType: "product",
          sourceKey: "product:1",
          recommendationType: "decision",
          sku: "SKU-1",
          productName: "Patike X",
          supplierId: 7,
          supplierName: "Dobavljač A",
          category: "Patike",
          tipObuce: "Sportske",
          color: "Crna",
          size: "42",
          revenue: 200000,
          unitsSold: 50,
          velocityUnitsPerDay: 1.7,
          marginContribution: 50000,
          marginPct: 0.4,
          marginQualityLabel: "Validna",
          marginCoveragePct: 95,
          currentStock: 4,
          minStock: 10,
          stockGap: 6,
          daysSinceLastSale: 4,
          trendPct: 12,
          lostSalesEstimate: 120000,
          slowStockCapital: 0,
          dataQualityStatus: "good",
          confidenceLevel: "high",
          confidenceScore: 88,
          confidencePct: 88,
          reliabilityPct: 92,
          recommendationStatus: "REPLENISH",
          recommendationLabel: "Dopuni",
          recommendationReason: "Brza prodaja i niska zaliha.",
          reasonCodes: ["high_velocity", "low_stock"],
          warningCodes: ["stock_risk"],
          primaryDrivers: ["sales_velocity", "stock_risk"],
          expectedImpactRsd: 120000,
          impactWindowDays: 14,
          riskIfIgnored: "Moguća rasprodaja.",
          explainabilityText: "Brza prodaja i nizak nivo zalihe znače da treba dopuniti.",
          inputFreshnessStatus: "fresh",
          recommendedAction: "Dopuni odmah.",
        },
      ],
      meta: { success: true, dataQualityStatus: "good" },
    },
    inventoryInsights: {
      totalItems: 1,
      totalEstimatedValue: 10000,
      aging: [],
      abc: [],
      topAgedItems: [
        {
          id: 11,
          plu: "PLU-11",
          naziv: "Zaliha X",
          supplierName: "Dobavljač A",
          storeName: "Prodavnica 1",
          quantity: 2,
          minimum: 8,
          reorderGap: 6,
          estimatedValue: 10000,
          daysSinceMovement: 60,
          agingBucket: "60+",
          agingLabel: "Stara",
          abcClass: "A",
          stockState: "critical",
        },
      ],
      topCapitalLockedItems: [],
      meta: { success: true, dataQualityStatus: "good" },
    },
    inventoryRows: [
      {
        id: 11,
        naziv: "Zaliha X",
        plu: "PLU-11",
        kolicina: 2,
        minimalnaKolicina: 8,
        nabavnaCena: 5000,
        estimatedValue: 10000,
        idObjekat: 1,
        idDobavljac: 7,
        velicina: "42",
        velicinaGroup: "42",
        stockCoverDays: 3,
        stockCoverStatus: "low_cover",
        stockCoverStatusLabel: "Niska pokrivenost",
        sellThroughRatio: 0.3,
        sellThroughStatus: "warning",
        sellThroughStatusLabel: "Upozorenje",
        signalConfidencePct: 81,
        recommendationAllowed: true,
        reasonCodes: ["low_cover"],
        dataQualityStatus: "warning",
        supplierName: "Dobavljač A",
        storeName: "Prodavnica 1",
        quantity: 2,
        minimum: 8,
        reorderGap: 6,
        stockState: "critical",
        stockStateLabel: "Bez zaliha",
        estimatedValueAmount: 10000,
        unitCost: 5000,
        coverageRatio: 0.25,
        signalText: "Dopuni",
      } as Parameters<typeof buildExecutiveDecisionBoardModel>[0]["inventoryRows"][number],
    ],
    stores: [
      { storeId: 1, storeName: "Prodavnica 1", city: "Beograd", region: "RS" },
    ],
    suppliers: [
      { supplierId: 7, supplierName: "Dobavljač A" },
    ],
    supplierSummary: {
      from: "2026-06-01T00:00:00Z",
      to: "2026-06-19T00:00:00Z",
      supplierCount: 1,
      fullPriceRevenueShare: 0.8,
      fullPriceSellthrough: 0.9,
      markdownRevenueShare: 0.1,
      preMarkdownMarginPct: 0.34,
      capitalAtRisk: 10000,
      topGrowSuppliers: [
        {
          supplierId: 7,
          supplierName: "Dobavljač A",
          revenue: 50000,
          mlSupplierScore: 88,
          supplierQualityIndex: 82,
          recommendationCode: "EXPAND",
          confidenceScore: 84,
        },
      ],
      topRiskSuppliers: [],
      keyInsights: [],
      dataNote: null,
      trustMetadata: {
        dataCoverageStatus: "good",
        dataScope: "all",
        recommendationAllowed: true,
        usedFallback: false,
      },
      meta: { success: true, dataQualityStatus: "good" },
    },
    actions: {
      items: [
        {
          id: 1,
          sourceType: "product",
          sourceKey: "product:1",
          sourceId: 1,
          title: "Dopuni: Patike X",
          description: "Brza prodaja i niska zaliha.",
          recommendationStatus: "REPLENISH",
          priority: "P1",
          impactEstimateRsd: 120000,
          dueAtUtc: "2026-07-01T00:00:00Z",
          expectedImpactRsd: 120000,
          measuredImpactRsd: null,
          outcomeStatus: "pending",
          outcomeMeasuredAtUtc: null,
          outcomeNotes: null,
          confidencePct: 88,
          reliabilityPct: 92,
          dataQualityStatus: "good",
          status: "new",
          actionUrl: "/analytics/products",
          metadataJson: null,
          createdAtUtc: "2026-06-19T09:00:00Z",
          updatedAtUtc: "2026-06-19T09:05:00Z",
          resolvedAtUtc: null,
          createdByUserId: "user-1",
          updatedByUserId: "user-1",
          updatedByUserName: "Ivan",
          notes: [],
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    },
    actionOutcomeSummary: {
      meta: {
        success: true,
        periodMode: "created",
        createdFrom: "2026-06-01T00:00:00Z",
        createdTo: "2026-06-19T00:00:00Z",
        resolvedFrom: null,
        resolvedTo: null,
        measuredFrom: null,
        measuredTo: null,
        generatedAtUtc: "2026-06-19T09:05:00Z",
        sampleSize: 1,
        measuredSampleSize: 1,
        warnings: [],
        emptyReason: null,
      },
      totals: {
        createdCount: 1,
        closedCount: 1,
        openCount: 1,
        measuredCount: 1,
        pendingOutcomeCount: 0,
        successCount: 1,
        neutralCount: 0,
        negativeCount: 0,
        notMeasuredCount: 0,
        outcomeCoverageRate: 1,
        positiveOutcomeRate: 1,
        negativeOutcomeRate: 0,
      },
      impact: {
        expectedImpactRsd: 120000,
        measuredImpactRsd: 100000,
        realizationRatio: 0.83,
        measuredImpactSampleCount: 1,
      },
      bySourceType: [],
      byPriority: [],
      byOutcomeStatus: [],
      byDataQuality: [],
      byConfidenceBucket: [],
      byReliabilityBucket: [],
    },
    errors: [],
  };
}

describe("ExecutiveDecisionBoardPage model", () => {
  it("composes sections from existing analytics sources", () => {
    const model = buildExecutiveDecisionBoardModel(createBasePayload());
    const allCards = model.sections.flatMap((section) => section.cards);

    expect(model.hasData).toBe(true);
    expect(model.isPartial).toBe(false);
    expect(model.sections.map((section) => section.key)).toEqual([
      "urgent",
      "impact",
      "stockRisk",
      "supplierRisk",
      "blockers",
      "actionsDecision",
      "actionsOutcome",
    ]);

    expect(allCards.some((card) => card.kind === "product")).toBe(true);
    expect(allCards.some((card) => card.kind === "inventory")).toBe(true);
    expect(allCards.some((card) => card.kind === "supplier")).toBe(true);
    expect(allCards.some((card) => card.kind === "action")).toBe(true);
    expect(allCards.some((card) => card.kind === "outcome")).toBe(true);
  });

  it("keeps insufficient data and missing impact visible instead of inventing confidence or 0 RSD", () => {
    const payload = createBasePayload();
    const productDecisionCenter = payload.productDecisionCenter!;
    payload.productDecisionCenter = {
      ...productDecisionCenter,
      rows: [
        {
          ...productDecisionCenter.rows[0],
          confidenceLevel: "insufficient_data",
          confidenceScore: null,
          confidencePct: 0,
          expectedImpactRsd: undefined as unknown as number,
          lostSalesEstimate: undefined as unknown as number,
        },
      ],
    };

    const model = buildExecutiveDecisionBoardModel(payload);
    const productCard = model.sections.flatMap((section) => section.cards).find((card) => card.kind === "product");

    expect(productCard?.confidenceTone).toBe("insufficient");
    expect(productCard?.confidenceLabel).toContain("Nedovoljno podataka");
    expect(productCard?.expectedImpactRsd).toBeNull();
  });

  it("keeps missing action and supplier confidence at insufficient instead of helper language", () => {
    const payload = createBasePayload();

    payload.actions = {
      ...payload.actions!,
      items: [
        {
          ...payload.actions!.items[0],
          confidencePct: null,
        } as Parameters<typeof buildExecutiveDecisionBoardModel>[0]["actions"]["items"][number],
      ],
    };

    payload.supplierSummary = {
      ...payload.supplierSummary!,
      topGrowSuppliers: [
        {
          ...payload.supplierSummary!.topGrowSuppliers[0],
          confidenceScore: null,
        } as Parameters<typeof buildExecutiveDecisionBoardModel>[0]["supplierSummary"]["topGrowSuppliers"][number],
      ],
    };

    const model = buildExecutiveDecisionBoardModel(payload);
    const actionCard = model.sections.flatMap((section) => section.cards).find((card) => card.kind === "action");
    const supplierCard = model.sections.flatMap((section) => section.cards).find((card) => card.kind === "supplier");

    expect(actionCard?.confidenceTone).toBe("insufficient");
    expect(actionCard?.confidenceLabel).toBe("Nedovoljno podataka");
    expect(supplierCard?.confidenceTone).toBe("insufficient");
    expect(supplierCard?.confidenceLabel).toBe("Nedovoljno podataka");
  });
});

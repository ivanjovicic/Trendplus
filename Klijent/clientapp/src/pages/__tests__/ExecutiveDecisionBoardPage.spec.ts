import { describe, expect, it } from "vitest";
import {
  buildExecutiveDecisionBoardModel,
  buildExecutiveFallbackProductCards,
  buildExecutiveFallbackSupplierCards,
  buildInventoryCards,
} from "../ExecutiveDecisionBoardPage";
import type { InventoryRow } from "../../components/inventory/types";
import type { SummaryResponse } from "../../services/supplierDecisionHubApi";
import type {
  DecisionBoardAggregateResponse,
  DecisionBoardCard,
  DecisionBoardMetric,
  DecisionBoardSection,
  DecisionBoardSourceState,
  InventoryInsightItem,
  InventoryInsights,
  ProductDecisionCenterItem,
  ProductDecisionCenterResponse,
  ProductDecisionRecommendationStatus,
} from "../../types/analytics";

function baseCard(overrides: Partial<DecisionBoardCard> & Pick<DecisionBoardCard, "id" | "kind" | "sectionKey" | "sourceModule" | "title" | "riskIfIgnored" | "recommendedNextAction" | "actionHref" | "dataQualityStatus" | "priorityScore" | "impactScore">): DecisionBoardCard {
  return {
    sourceType: "product",
    sourceKey: overrides.id,
    summary: "Brza prodaja i niska zaliha.",
    confidenceLevel: "high",
    confidenceScore: 88,
    reliabilityPct: 92,
    expectedImpactRsd: 120000,
    measuredImpactRsd: null,
    realizationRatio: null,
    alreadyInAction: false,
    alreadyClosed: false,
    warningCodes: ["stock_risk"],
    generatedAtUtc: "2026-06-19T09:05:00Z",
    ...overrides,
  };
}

function baseSection(key: string, cards: DecisionBoardCard[]): DecisionBoardSection {
  return {
    key,
    title: `${key} sekcija`,
    description: `${key} opis`,
    sourceLink: `/analytics/${key}`,
    emptyMessage: `${key} je prazan`,
    warnings: cards.flatMap((card) => card.warningCodes),
    cards,
  };
}

function baseAggregate(overrides: Partial<DecisionBoardAggregateResponse> = {}): DecisionBoardAggregateResponse {
  const urgentCard = baseCard({
    id: "product:1",
    kind: "product",
    sectionKey: "urgent",
    sourceModule: "Odluke o proizvodima",
    title: "Patike X",
    summary: "Brza prodaja i niska zaliha.",
    riskIfIgnored: "Moguća rasprodaja.",
    recommendedNextAction: "Dopuni odmah.",
    actionHref: "/analytics/products",
    dataQualityStatus: "good",
    priorityScore: 280,
    impactScore: 120000,
  });

  const blockerCard = baseCard({
    id: "blocker-1",
    kind: "blocker",
    sectionKey: "blockers",
    sourceModule: "Kvalitet podataka",
    title: "Data quality health traži proveru",
    summary: "Nedostaju ključni signalni izvori.",
    confidenceLevel: "warning",
    confidenceScore: 54,
    expectedImpactRsd: null,
    riskIfIgnored: "Signal ostaje slab.",
    recommendedNextAction: "Otvori kvalitet podataka.",
    actionHref: "/analytics/data-quality",
    dataQualityStatus: "warning",
    priorityScore: 240,
    impactScore: 0,
  });

  const actionCard = baseCard({
    id: "action-1",
    kind: "action",
    sectionKey: "actionsDecision",
    sourceModule: "Centralne akcije",
    sourceType: "product",
    sourceKey: "product:1",
    title: "Dopuni: Patike X",
    summary: "Akcija je već u toku.",
    confidenceLevel: "high",
    confidenceScore: 88,
    expectedImpactRsd: 120000,
    riskIfIgnored: "Akcija može ostati nezatvorena.",
    recommendedNextAction: "Prati izvršenje.",
    actionHref: "/analytics/actions",
    alreadyInAction: true,
    dataQualityStatus: "good",
    priorityScore: 200,
    impactScore: 120000,
  });

  const outcomeCard = baseCard({
    id: "outcome-1",
    kind: "outcome",
    sectionKey: "actionsOutcome",
    sourceModule: "Sažetak ishoda",
    sourceType: "product",
    sourceKey: "product:1",
    title: "Realizacija očekivanog uticaja",
    summary: "Feedback je delimičan.",
    confidenceLevel: "medium",
    confidenceScore: 61,
    expectedImpactRsd: 120000,
    measuredImpactRsd: 100000,
    realizationRatio: 0.83,
    riskIfIgnored: "Nećemo znati da li je akcija uspela.",
    recommendedNextAction: "Uporedi očekivani i ostvareni efekat.",
    actionHref: "/analytics/actions",
    dataQualityStatus: "warning",
    priorityScore: 150,
    impactScore: 120000,
  });

  const sections: DecisionBoardSection[] = [
    baseSection("urgent", [urgentCard]),
    baseSection("impact", [urgentCard]),
    baseSection("stockRisk", []),
    baseSection("supplierRisk", []),
    baseSection("blockers", [blockerCard]),
    baseSection("actionsDecision", [actionCard]),
    baseSection("actionsOutcome", [outcomeCard]),
  ];

  return {
    generatedAtUtc: "2026-06-19T09:05:00Z",
    periodFromUtc: "2026-06-01T00:00:00Z",
    periodToUtc: "2026-06-19T00:00:00Z",
    lastRefreshAtUtc: "2026-06-19T09:00:00Z",
    overallDataQualityStatus: "good",
    recommendationNote: "Backend ostaje izvor istine; board samo kompozira postojeće signale.",
    warnings: [],
    metrics: [
      { label: "Urgentne odluke", value: "1", tone: "critical" },
      { label: "Visok uticaj", value: "1", tone: "warning" },
      { label: "Blokatori", value: "1", tone: "critical" },
    ] satisfies DecisionBoardMetric[],
    sourceStates: [
      {
        sourceKey: "refresh-status",
        displayName: "Refresh status",
        status: "fresh",
        generatedAtUtc: "2026-06-19T09:00:00Z",
        warningCodes: [],
        sourceLink: "/analytics/pilot-readiness",
      } satisfies DecisionBoardSourceState,
      {
        sourceKey: "data-quality-health",
        displayName: "Data quality health",
        status: "good",
        generatedAtUtc: "2026-06-19T09:00:00Z",
        warningCodes: [],
        sourceLink: "/analytics/data-quality",
      } satisfies DecisionBoardSourceState,
    ],
    sections,
    meta: {
      success: true,
      dataQualityStatus: "good",
    },
    ...overrides,
  };
}

function baseInventoryRow(overrides: Partial<InventoryRow> & Pick<InventoryRow, "id" | "naziv">): InventoryRow {
  return {
    id: overrides.id,
    naziv: overrides.naziv,
    plu: "SKU-1",
    kolicina: 10,
    minimalnaKolicina: 4,
    nabavnaCena: 1200,
    estimatedValue: 12000,
    idObjekat: 1,
    idDobavljac: 2,
    velicina: "42",
    velicinaGroup: "42",
    stockCoverDays: 3,
    stockCoverStatus: "insufficient_data",
    stockCoverStatusLabel: "Nedovoljno podataka",
    sellThroughRatio: 0.2,
    sellThroughStatus: "insufficient_data",
    sellThroughStatusLabel: "Nedovoljno podataka",
    signalConfidencePct: 41,
    recommendationAllowed: false,
    reasonCodes: ["insufficient_signal"],
    dataQualityStatus: "warning",
    supplierName: "Dobavljač X",
    storeName: "Prodavnica Y",
    quantity: 10,
    minimum: 4,
    reorderGap: 2,
    stockState: "warning",
    stockStateLabel: "Upozorenje",
    estimatedValueAmount: 12000,
    unitCost: 1200,
    coverageRatio: 0.3,
    signalText: "Signal nije dovoljan za potvrđenu preporuku.",
    ...overrides,
  };
}

function baseInventoryInsight(overrides: Partial<InventoryInsightItem> & Pick<InventoryInsightItem, "id" | "naziv">): InventoryInsightItem {
  return {
    id: overrides.id,
    naziv: overrides.naziv,
    supplierName: "Dobavljač X",
    storeName: "Prodavnica Y",
    quantity: 10,
    minimum: 4,
    reorderGap: 2,
    estimatedValue: 12000,
    daysSinceMovement: 18,
    agingBucket: "aged",
    agingLabel: "Staro",
    abcClass: "A",
    stockState: "warning",
    stockCoverDays: 3,
    stockCoverStatus: "insufficient_data",
    stockCoverStatusLabel: "Nedovoljno podataka",
    sellThroughRatio: 0.2,
    sellThroughStatus: "insufficient_data",
    sellThroughStatusLabel: "Nedovoljno podataka",
    signalConfidencePct: 41,
    recommendationAllowed: false,
    dataQualityStatus: "warning",
    reasonCodes: ["insufficient_signal"],
    ...overrides,
  };
}

function baseInventoryInsights(overrides: Partial<InventoryInsights> = {}): InventoryInsights {
  const topItem = baseInventoryInsight({ id: 201, naziv: "Patike sa slabim signalom" });

  return {
    totalItems: 1,
    totalEstimatedValue: 12000,
    aging: [] as InventoryInsights["aging"],
    abc: [] as InventoryInsights["abc"],
    topAgedItems: [topItem],
    topCapitalLockedItems: [topItem],
    meta: null,
    ...overrides,
  };
}

describe("ExecutiveDecisionBoardPage model", () => {
  it("maps aggregate sections and metrics into the board model", () => {
    const model = buildExecutiveDecisionBoardModel(baseAggregate());
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
    expect(model.metrics).toHaveLength(3);
    expect(allCards.some((card) => card.kind === "product")).toBe(true);
    expect(allCards.some((card) => card.kind === "blocker")).toBe(true);
    expect(allCards.some((card) => card.kind === "action")).toBe(true);
    expect(allCards.some((card) => card.kind === "outcome")).toBe(true);
    expect(model.recommendationNote).toContain("Backend ostaje izvor istine");
  });

  it("keeps missing expected impact visible instead of inventing 0 RSD", () => {
    const payload = baseAggregate({
      sections: [
        baseSection("urgent", [
          baseCard({
            id: "product:2",
            kind: "product",
            sectionKey: "urgent",
            sourceModule: "Odluke o proizvodima",
            title: "Patike Y",
            summary: "Signal je slab.",
            confidenceLevel: "insufficient_data",
            confidenceScore: null,
            expectedImpactRsd: null,
            riskIfIgnored: "Nema pouzdanog signala.",
            recommendedNextAction: "Sačekaj novi signal.",
            actionHref: "/analytics/products",
            dataQualityStatus: "insufficient_data",
            priorityScore: 40,
            impactScore: 0,
          }),
        ]),
        ...baseAggregate().sections.slice(1),
      ],
      metrics: baseAggregate().metrics,
    });

    const model = buildExecutiveDecisionBoardModel(payload);
    const productCard = model.sections.flatMap((section) => section.cards).find((card) => card.title === "Patike Y");

    expect(productCard?.confidenceTone).toBe("insufficient");
    expect(productCard?.confidenceLabel).toContain("Nedovoljno podataka");
    expect(productCard?.expectedImpactRsd).toBeNull();
  });

  it("does not crash when a compatibility aggregate omits the warnings array", () => {
    const payload = baseAggregate({ warnings: undefined as unknown as string[] });

    expect(() => buildExecutiveDecisionBoardModel(payload)).not.toThrow();
  });

  it("keeps weak inventory signal exposure out of expected impact on executive inventory cards", () => {
    const rows = [
      baseInventoryRow({
        id: 201,
        naziv: "Patike sa slabim signalom",
        plu: "SKU-201",
        estimatedValue: 98000,
        estimatedValueAmount: 98000,
        stockCoverStatus: "insufficient_data",
        stockCoverStatusLabel: "Nedovoljno podataka",
        sellThroughStatus: "insufficient_data",
        sellThroughStatusLabel: "Nedovoljno podataka",
        recommendationAllowed: false,
        signalConfidencePct: 41,
        signalText: "Signal nije dovoljan za potvrđenu preporuku.",
      }),
    ];

    const cards = buildInventoryCards(baseInventoryInsights(), rows, new Map());

    expect(cards).toHaveLength(1);
    expect(cards[0].recommendedNextAction).toBe("Proveri signal zalihe: Patike sa slabim signalom");
    expect(cards[0].expectedImpactRsd).toBeNull();
    expect(cards[0].impactScore).toBe(0);
  });

  it("still preserves expected impact for actionable inventory rows", () => {
    const row = baseInventoryInsight({
      id: 202,
      naziv: "Patike za dopunu",
      stockCoverStatus: "out_of_stock_risk",
      stockCoverStatusLabel: "Rizik rasprodaje",
      sellThroughStatus: "healthy",
      sellThroughStatusLabel: "Zdravo",
      recommendationAllowed: true,
      signalConfidencePct: 86,
      reasonCodes: ["out_of_stock_risk"],
    });
    const rows = [
      baseInventoryRow({
        id: 202,
        naziv: "Patike za dopunu",
        plu: "SKU-202",
        estimatedValue: 75000,
        estimatedValueAmount: 75000,
        stockCoverStatus: "out_of_stock_risk",
        stockCoverStatusLabel: "Rizik rasprodaje",
        sellThroughStatus: "healthy",
        sellThroughStatusLabel: "Zdravo",
        recommendationAllowed: true,
        signalConfidencePct: 86,
        signalText: "Rizik rasprodaje.",
        reasonCodes: ["out_of_stock_risk"],
      }),
    ];

    const cards = buildInventoryCards(
      {
        ...baseInventoryInsights(),
        topAgedItems: [row],
        topCapitalLockedItems: [row],
      },
      rows,
      new Map(),
    );

    expect(cards).toHaveLength(1);
    expect(cards[0].expectedImpactRsd).toBe(75000);
    expect(cards[0].recommendedNextAction).toBe("Dopuni artikal: Patike za dopunu");
  });

  it("keeps stale or warning source states visible in the board model", () => {
    const payload = baseAggregate({
      overallDataQualityStatus: "warning",
      warnings: ["BOARD_PARTIAL"],
      meta: {
        success: true,
        dataQualityStatus: "warning",
        warningCode: "BOARD_PARTIAL",
        isPartial: true,
      },
      sourceStates: [
        {
          sourceKey: "refresh-status",
          displayName: "Refresh status",
          status: "stale",
          generatedAtUtc: "2026-06-19T09:00:00Z",
          warningCodes: ["stale"],
          sourceLink: "/analytics/pilot-readiness",
        },
        {
          sourceKey: "data-quality-health",
          displayName: "Data quality health",
          status: "warning",
          generatedAtUtc: "2026-06-19T09:00:00Z",
          warningCodes: ["missing_cost"],
          sourceLink: "/analytics/data-quality",
        },
      ],
    });

    const model = buildExecutiveDecisionBoardModel(payload);

    expect(model.isPartial).toBe(true);
    expect(model.overallDataQualityStatus).toBe("warning");
    expect(model.periodFrom).toBe("2026-06-01T00:00:00Z");
    expect(model.periodTo).toBe("2026-06-19T00:00:00Z");
    expect(model.lastRefreshAt).toBe("2026-06-19T09:00:00Z");
  });

  it("returns an empty model when payload is missing", () => {
    const model = buildExecutiveDecisionBoardModel(null);

    expect(model.hasData).toBe(false);
    expect(model.sections).toHaveLength(0);
    expect(model.metrics).toHaveLength(0);
  });
});

function productRow(
  overrides: Partial<ProductDecisionCenterItem> & {
    productId: number;
    productName: string;
    recommendationStatus: ProductDecisionRecommendationStatus;
    expectedImpactRsd?: number | null;
    lostSalesEstimate?: number;
  },
): ProductDecisionCenterItem {
  return {
    sku: `SKU-${overrides.productId}`,
    revenue: 10_000,
    unitsSold: 5,
    velocityUnitsPerDay: 0.5,
    marginContribution: 2_000,
    marginQualityLabel: "ok",
    marginCoveragePct: 100,
    currentStock: 2,
    minStock: 5,
    stockGap: 3,
    lostSalesEstimate: overrides.lostSalesEstimate ?? 0,
    dataQualityStatus: "good",
    confidencePct: 80,
    reliabilityPct: 80,
    recommendationLabel: overrides.recommendationStatus,
    recommendationReason: "Test razlog.",
    reasonCodes: [],
    recommendedAction: "Proveri.",
    ...overrides,
  };
}

function productCenter(rows: ProductDecisionCenterItem[]): ProductDecisionCenterResponse {
  return {
    generatedAtUtc: "2026-08-05T08:00:00Z",
    periodFromUtc: "2026-07-01T00:00:00Z",
    periodToUtc: "2026-08-05T00:00:00Z",
    totalRows: rows.length,
    summary: {
      replenishCount: 0,
      markdownCount: 0,
      highPotentialCount: 0,
      badDataCount: 0,
      lostSalesEstimate: rows.reduce((sum, row) => sum + row.lostSalesEstimate, 0),
      slowStockCapital: 0,
    },
    rows,
  };
}

describe("buildExecutiveFallbackProductCards (RQ72)", () => {
  it("preserves recommendationAllowed and confidenceSource for product cards", () => {
    const cards = buildExecutiveFallbackProductCards(
      productCenter([
        productRow({
          productId: 0,
          productName: "Blokiran signal",
          recommendationStatus: "FIX_DATA",
          recommendationAllowed: false,
          expectedImpactRsd: null,
          dataQualityStatus: "insufficient_data",
          confidenceLevel: "insufficient_data",
          confidencePct: 20,
        }),
        productRow({
          productId: 1,
          productName: "Otvoren signal",
          recommendationStatus: "REPLENISH",
          recommendationAllowed: true,
          expectedImpactRsd: 90_000,
          dataQualityStatus: "good",
          confidenceLevel: "high",
          confidencePct: 82,
        }),
      ]),
    );

    expect(cards).toHaveLength(2);
    expect(cards[0].title).toBe("Otvoren signal");
    expect(cards[0].recommendationAllowed).toBe(true);
    expect(cards[0].confidenceSource).toBe("signal");
    expect(cards[1].title).toBe("Blokiran signal");
    expect(cards[1].recommendationAllowed).toBe(false);
    expect(cards[1].confidenceSource).toBe("workflow_status_only");
  });

  it("does not map lostSalesEstimate into expectedImpact when PDC left it null", () => {
    const cards = buildExecutiveFallbackProductCards(
      productCenter([
        productRow({
          productId: 1,
          productName: "FIX_DATA bez impact",
          recommendationStatus: "FIX_DATA",
          expectedImpactRsd: null,
          lostSalesEstimate: 250_000,
          dataQualityStatus: "warning",
          confidenceLevel: "low",
          confidencePct: 40,
        }),
        productRow({
          productId: 2,
          productName: "INSUFFICIENT_DATA bez impact",
          recommendationStatus: "INSUFFICIENT_DATA",
          expectedImpactRsd: null,
          lostSalesEstimate: 180_000,
          dataQualityStatus: "insufficient_data",
          confidenceLevel: "insufficient_data",
          confidencePct: 20,
          confidenceScore: null,
        }),
      ]),
    );

    expect(cards).toHaveLength(2);
    for (const card of cards) {
      expect(card.expectedImpactRsd).toBeNull();
      expect(card.impactScore).toBe(0);
    }
  });

  it("preserves PDC expectedImpactRsd when present", () => {
    const cards = buildExecutiveFallbackProductCards(
      productCenter([
        productRow({
          productId: 3,
          productName: "REPLENISH sa impact",
          recommendationStatus: "REPLENISH",
          expectedImpactRsd: 90_000,
          lostSalesEstimate: 90_000,
        }),
      ]),
    );

    expect(cards).toHaveLength(1);
    expect(cards[0].expectedImpactRsd).toBe(90_000);
    expect(cards[0].impactScore).toBe(90_000);
  });

  it("does not promote missing-impact rows by lost-sales when sorting", () => {
    const cards = buildExecutiveFallbackProductCards(
      productCenter([
        productRow({
          productId: 10,
          productName: "Veliki lost sales bez impact",
          recommendationStatus: "FIX_DATA",
          expectedImpactRsd: null,
          lostSalesEstimate: 500_000,
          confidencePct: 90,
          confidenceScore: 90,
        }),
        productRow({
          productId: 11,
          productName: "Mali expected impact",
          recommendationStatus: "REPLENISH",
          expectedImpactRsd: 40_000,
          lostSalesEstimate: 1_000,
          confidencePct: 70,
          confidenceScore: 70,
        }),
      ]),
    );

    expect(cards[0].title).toBe("Mali expected impact");
    expect(cards[0].expectedImpactRsd).toBe(40_000);
    expect(cards[1].title).toBe("Veliki lost sales bez impact");
    expect(cards[1].expectedImpactRsd).toBeNull();
  });
});

describe("buildExecutiveFallbackSupplierCards", () => {
  it("blocks a supplier signal when compatibility payload omits trust metadata", () => {
    const summary: SummaryResponse = {
      from: "2026-06-01T00:00:00Z",
      to: "2026-06-30T23:59:59Z",
      supplierCount: 1,
      fullPriceRevenueShare: 0.8,
      fullPriceSellthrough: 0.6,
      markdownRevenueShare: 0.2,
      preMarkdownMarginPct: 0.3,
      capitalAtRisk: 10_000,
      topGrowSuppliers: [{
        supplierId: 78,
        supplierName: "Dobavljač bez trust metapodataka",
        revenue: 900_000,
        mlSupplierScore: 80,
        supplierQualityIndex: 75,
        recommendationCode: "EXPAND",
        confidenceScore: 95,
        reliabilityPct: 95,
        dataQualityStatus: "good",
        statusReason: "Signal je inače jak.",
        reasonCodes: ["supplier_grow"],
      }],
      topRiskSuppliers: [],
      keyInsights: [],
      trustMetadata: null,
    };

    const cards = buildExecutiveFallbackSupplierCards(summary);

    expect(cards).toHaveLength(1);
    expect(cards[0]).toMatchObject({
      recommendationAllowed: false,
      confidenceTone: "insufficient",
      dataQualityStatus: "insufficient_data",
      impactScore: 0,
    });
    expect(cards[0].priorityScore).toBeLessThanOrEqual(40);
    expect(cards[0].warningCodes).toContain("supplier_recommendation_blocked");
    expect(cards[0].sourceKey).toContain("signal_check");
    expect(cards[0].recommendedNextAction).toContain("Proveri pouzdanost");
  });
});

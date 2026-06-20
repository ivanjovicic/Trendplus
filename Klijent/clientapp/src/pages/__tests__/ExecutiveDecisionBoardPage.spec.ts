import { describe, expect, it } from "vitest";
import { buildExecutiveDecisionBoardModel } from "../ExecutiveDecisionBoardPage";
import type {
  DecisionBoardAggregateResponse,
  DecisionBoardCard,
  DecisionBoardMetric,
  DecisionBoardSection,
  DecisionBoardSourceState,
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

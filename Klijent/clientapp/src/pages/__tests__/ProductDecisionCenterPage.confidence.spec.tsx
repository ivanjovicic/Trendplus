import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import ProductDecisionCenterPage from "../ProductDecisionCenterPage";

vi.mock("react-router-dom", async () => ({
  Link: ({ children }: { children: ReactNode }) => <a>{children}</a>,
}));

const getStoresMock = vi.fn();
const getSupplierFiltersMock = vi.fn();
const getProductDecisionCenterMock = vi.fn();
const getProductDecisionTimelineMock = vi.fn();
const getAnalyticsActionSourceStatusesMock = vi.fn();
const upsertAnalyticsActionWithResultMock = vi.fn();

vi.mock("../../services/analyticsApi", () => ({
  AnalyticsMetaError: class extends Error {},
  getStores: (...args: unknown[]) => getStoresMock(...args),
  getSupplierFilters: (...args: unknown[]) => getSupplierFiltersMock(...args),
  getProductDecisionCenter: (...args: unknown[]) => getProductDecisionCenterMock(...args),
  getProductDecisionTimeline: (...args: unknown[]) => getProductDecisionTimelineMock(...args),
  getAnalyticsActionSourceStatuses: (...args: unknown[]) => getAnalyticsActionSourceStatusesMock(...args),
  upsertAnalyticsActionWithResult: (...args: unknown[]) => upsertAnalyticsActionWithResultMock(...args),
}));

vi.mock("../../components/analytics/AnalyticsTrustHeader", () => ({ default: () => null }));
vi.mock("../../components/analytics/AnalyticsTableToolbar", () => ({ default: () => null }));
vi.mock("../../components/analytics/AnalyticsEmptyState", () => ({ default: () => null }));
vi.mock("../../components/analytics/AnalyticsErrorState", () => ({
  default: ({
    title,
    message,
  }: {
    title: string;
    message: string;
  }) => (
    <div role="alert">
      <strong>{title}</strong>
      <span>{message}</span>
    </div>
  ),
}));
vi.mock("../../components/analytics/KpiExplainButton", () => ({ default: () => null }));
vi.mock("../../components/ui/InfoTip", () => ({ default: () => null }));

function makeRow(overrides: Record<string, unknown> = {}) {
  const row = {
    productId: 101,
    recommendationId: "product:101:REPLENISH:20260528:20260626",
    sourceType: "product",
    sourceKey: "product:101",
    recommendationType: "REPLENISH",
    sku: "SKU-101",
    productName: "Model X",
    supplierId: 77,
    supplierName: "Supplier A",
    revenue: 120000,
    unitsSold: 40,
    velocityUnitsPerDay: 1.2,
    marginContribution: 24000,
    marginPct: 24,
    marginQualityLabel: "Dobro",
    marginCoveragePct: 90,
    currentStock: 10,
    minStock: 5,
    stockGap: 0,
    trendPct: 3,
    lostSalesEstimate: 25000,
    slowStockCapital: 0,
    stockCoverDays: 5,
    stockCoverStatus: "low_cover",
    stockCoverStatusLabel: "Niska pokrivenost",
    sellThroughRatio: 0.6,
    sellThroughStatus: "good",
    sellThroughStatusLabel: "Dobar sell-through",
    signalConfidencePct: 82,
    recommendationAllowed: true,
    dataQualityStatus: "good",
    confidenceLevel: "high",
    confidenceScore: 88,
    confidencePct: 88,
    reliabilityPct: 80,
    recommendationStatus: "REPLENISH",
    recommendationLabel: "Dopuni",
    recommendationReason: "Brza prodaja i nizak stock cover.",
    warningCodes: [],
    primaryDrivers: ["sales_velocity", "stock_risk", "margin"],
    reasonCodes: ["high_velocity", "low_stock"],
    expectedImpactRsd: 25000,
    impactWindowDays: 14,
    riskIfIgnored: "Rizik je izgubljena prodaja i pad dostupnosti na polici.",
    explainabilityText: "Brza prodaja i nizak stock cover.",
    inputFreshnessStatus: "fresh",
    confidenceBreakdown: [
      {
        category: "confidence",
        code: "confidence_score",
        label: "Ocena pouzdanosti",
        valueText: "Visoka sigurnost · 88%",
        sourceFields: ["ConfidenceLevel", "ConfidenceScore", "ConfidencePct"],
        isMissing: false,
        detail: "Ocena kombinuje snagu signala i dostupnost ulaza.",
      },
      {
        category: "confidence",
        code: "evidence_coverage",
        label: "Pokrivenost signala",
        valueText: "Široka",
        sourceFields: ["UnitsSold", "VelocityUnitsPerDay", "MarginPct", "TrendPct", "DaysSinceLastSale", "WarningCodes", "ReasonCodes"],
        isMissing: false,
        detail: "Više nezavisnih signala je prisutno: prodaja, marža, zaliha i trend.",
      },
      {
        category: "confidence",
        code: "reliability_signal",
        label: "Pouzdanost signala",
        valueText: "80%",
        sourceFields: ["ReliabilityPct", "SignalConfidencePct"],
        isMissing: false,
        detail: "SignalConfidence 82%",
      },
      {
        category: "confidence",
        code: "freshness_signal",
        label: "Svežina ulaza",
        valueText: "Sveže",
        sourceFields: ["InputFreshnessStatus", "DataQualityStatus"],
        isMissing: false,
        detail: "Kvalitet podataka dobar",
      },
      {
        category: "confidence",
        code: "data_quality_signal",
        label: "Kvalitet podataka",
        valueText: "dobar",
        sourceFields: ["DataQualityStatus", "WarningCodes"],
        isMissing: false,
        detail: "Podaci su konzistentni.",
      },
    ],
    recommendedAction: "Dopuni zalihe",
    daysSinceLastSale: 12,
    ...overrides,
  };
  const reasonCodes = Array.isArray(row.reasonCodes) ? row.reasonCodes : [];
  const primaryDrivers = Array.isArray(row.primaryDrivers) ? row.primaryDrivers : [];
  const warningCodes = Array.isArray(row.warningCodes) ? row.warningCodes : [];
  const confidenceBreakdown = Array.isArray(row.confidenceBreakdown) ? row.confidenceBreakdown : [];
  const alternativeRecommendations = Array.isArray(row.alternativeRecommendations) ? row.alternativeRecommendations : [];
  const evidenceChain = Array.isArray(row.evidenceChain) ? row.evidenceChain : [];
  const decisionTree = Array.isArray((row as { decisionTree?: unknown }).decisionTree)
    ? ((row as { decisionTree?: Array<Record<string, unknown>> }).decisionTree ?? [])
    : [
        {
          category: "decision",
          code: "selected_recommendation",
          label: "Odabrana preporuka",
          valueText: row.recommendationLabel,
          sourceFields: ["RecommendationStatus", "RecommendationLabel", "RecommendationReason"],
          isSelected: true,
          detail: row.explainabilityText,
        },
        {
          category: "gate",
          code: "data_quality_gate",
          label: "Kvalitet podataka",
          valueText: row.dataQualityStatus === "good" ? "Prolazi dalje" : "Blokira granu",
          sourceFields: ["DataQualityStatus", "WarningCodes", "ReasonCodes"],
          isSelected: row.dataQualityStatus === "good" || row.dataQualityStatus === "warning",
          detail: "Deterministički uslov iz backend grane.",
        },
        {
          category: "gate",
          code: "freshness_gate",
          label: "Svežina ulaza",
          valueText: String(row.inputFreshnessStatus ?? "unknown"),
          sourceFields: ["InputFreshnessStatus", "DataQualityStatus"],
          isSelected: row.inputFreshnessStatus !== "critical",
          detail: "Svežina ulaza se prikazuje bez lokalnog preračunavanja.",
        },
        {
          category: "branch",
          code: "selected_branch",
          label: row.recommendationLabel,
          valueText: row.recommendedAction,
          sourceFields: ["RecommendationStatus", "RecommendedAction", "ReasonCodes", "PrimaryDrivers"],
          isSelected: true,
          detail: row.recommendationReason,
        },
      ];

  return {
    ...row,
    whyPanel: overrides.whyPanel ?? {
      recommendationStatus: row.recommendationStatus,
      recommendationLabel: row.recommendationLabel,
      recommendationReason: row.recommendationReason,
      recommendedAction: row.recommendedAction,
      explainabilityText: row.explainabilityText,
      summarySource: row.recommendationReason ? "recommendation_reason" : "backend_composed",
      summaryFallbackUsed: !row.recommendationReason,
      summaryFallbackReason: !row.recommendationReason ? "recommendation_reason_missing" : null,
      reasonCodes: [...reasonCodes],
      primaryDrivers: [...primaryDrivers],
      warningCodes: [...warningCodes],
      confidenceLevel: row.confidenceLevel,
      confidenceScore: row.confidenceScore,
      confidencePct: row.confidencePct,
      reliabilityPct: row.reliabilityPct,
      dataQualityStatus: row.dataQualityStatus,
      inputFreshnessStatus: row.inputFreshnessStatus,
      recommendationAllowed: row.recommendationAllowed,
      expectedImpactRsd: row.expectedImpactRsd,
      impactWindowDays: row.impactWindowDays,
      riskIfIgnored: row.riskIfIgnored,
      confidenceBreakdown: [...confidenceBreakdown],
      alternativeRecommendations: [...alternativeRecommendations],
      evidenceChain: [...evidenceChain],
      decisionTree: [...(decisionTree as Array<Record<string, unknown>>)],
    },
  };
}

function buildResponse(rows: Array<Record<string, unknown>>, dataQualityStatus: string) {
  return {
    rows,
    summary: {
      lostSalesEstimate: 25000,
      slowStockCapital: 0,
    },
    totalRows: rows.length,
    generatedAtUtc: "2026-05-26T12:00:00Z",
    periodFromUtc: "2026-04-27",
    periodToUtc: "2026-05-26",
    meta: {
      success: true,
      dataQualityStatus,
    },
  };
}

beforeEach(() => {
  vi.clearAllMocks();

  getStoresMock.mockResolvedValue([]);
  getSupplierFiltersMock.mockResolvedValue([]);
  getAnalyticsActionSourceStatusesMock.mockResolvedValue({ items: [] });
  upsertAnalyticsActionWithResultMock.mockResolvedValue({
    item: { id: 1, sourceKey: "product:101:replenish:2026-05-28:2026-06-26:all:all" },
    created: true,
    existing: false,
    status: "new",
    sourceKey: "product:101:replenish:2026-05-28:2026-06-26:all:all",
  });
  getProductDecisionCenterMock.mockResolvedValue(buildResponse([makeRow()], "good"));
  getProductDecisionTimelineMock.mockResolvedValue({
    scope: {
      sourceType: "product",
      sourceKey: "product:101",
      productId: 101,
      recommendationType: "REPLENISH",
      periodFromUtc: "2026-04-27",
      periodToUtc: "2026-05-26",
      scopeExplanation: "Entitet: product:101 · Porodica: REPLENISH · Period: 2026-04-27 – 2026-05-26",
    },
    emptyReason: "no_events",
    timelines: [],
    matchedActionCount: 0,
    matchedEventCount: 0,
    warningCodes: [],
    meta: { success: true, dataQualityStatus: "insufficient_data" },
  });
});

describe("ProductDecisionCenterPage confidence contract", () => {
  it("explains Decision Timeline filter scope and keeps empty results explicit", async () => {
    render(<ProductDecisionCenterPage />);

    expect(await screen.findByText(/Visoka sigurnost/i)).toBeInTheDocument();
    fireEvent.click(screen.getAllByRole("button", { name: /Za.*\?/i })[0]);

    expect(await screen.findByTestId("decision-timeline-panel")).toBeInTheDocument();
    await waitFor(() => {
      expect(getProductDecisionTimelineMock).toHaveBeenCalled();
    });
    expect(getProductDecisionTimelineMock).toHaveBeenCalledWith(
      expect.objectContaining({
        sourceType: "product",
        sourceKey: "product:101",
        productId: 101,
        recommendationType: "REPLENISH",
      }),
    );
    expect(screen.getByTestId("decision-timeline-scope")).toHaveTextContent(/Porodica: REPLENISH/i);
    expect(screen.getByTestId("decision-timeline-empty")).toHaveTextContent(/no_events/i);
  });

  it("renders strong recommendations with explicit estimated impact wording", async () => {
    render(<ProductDecisionCenterPage />);

    expect(await screen.findByText(/Visoka sigurnost/i)).toBeInTheDocument();
    expect(screen.getByText("Dopuni zalihe")).toBeInTheDocument();
    expect(screen.getByText(/Procena uticaja:/i)).toBeInTheDocument();

    fireEvent.click(screen.getAllByRole("button", { name: /Za.*\?/i })[0]);

    expect(screen.getByText(/Zašto ova preporuka\?/i)).toBeInTheDocument();
    expect(screen.getByText(/Izvor objašnjenja: direktan razlog preporuke/i)).toBeInTheDocument();
    expect(screen.getByText(/Glavni pokreta/i)).toBeInTheDocument();
    expect(screen.getByText("Brzina prodaje", { selector: ".reason-chip" })).toBeInTheDocument();
    expect(screen.getByText("Rizik zalihe", { selector: ".reason-chip" })).toBeInTheDocument();
    expect(screen.getByText(/Očekivani uticaj:/i)).toBeInTheDocument();

    fireEvent.click(screen.getAllByRole("button", { name: "Dodaj u akcije" })[0]);

    await waitFor(() => {
      expect(upsertAnalyticsActionWithResultMock).toHaveBeenCalledTimes(1);
    });

    expect(upsertAnalyticsActionWithResultMock).toHaveBeenCalledWith(
      expect.objectContaining({
        expectedImpactRsd: 25000,
        impactEstimateRsd: 25000,
        confidencePct: 88,
      }),
    );
  });

  it("renders a structured evidence chain in the Why panel", async () => {
    getProductDecisionCenterMock.mockResolvedValueOnce(
      buildResponse([
        makeRow({
          productId: 606,
          recommendationId: "product:606:REPLENISH:20260528:20260626",
          sourceKey: "product:606",
          productName: "Model Evidence",
          sku: "SKU-606",
          revenue: 144000,
          unitsSold: 42,
          velocityUnitsPerDay: 1.4,
          marginContribution: 28800,
          marginPct: 26,
          marginCoveragePct: 88,
          currentStock: 8,
          minStock: 4,
          stockGap: 1,
          trendPct: 6,
          confidenceLevel: "high",
          confidenceScore: 90,
          confidencePct: 90,
          reliabilityPct: 82,
          recommendationReason: "Brza prodaja i nizak stock cover.",
          explainabilityText: "Brza prodaja i nizak stock cover.",
          warningCodes: [],
          primaryDrivers: ["sales_velocity", "stock_risk", "margin"],
          expectedImpactRsd: 28800,
          impactWindowDays: 14,
          riskIfIgnored: "Rizik je gubitka prodaje.",
          inputFreshnessStatus: "fresh",
          evidenceChain: [
            {
              category: "decision",
              code: "selected_recommendation",
              label: "Odabrana preporuka",
              valueText: "Dopuni",
              sourceFields: ["RecommendationStatus", "RecommendationLabel", "RecommendationReason"],
              isMissing: false,
              detail: "Brza prodaja i nizak stock cover.",
            },
            {
              category: "evidence",
              code: "sales_signal",
              label: "Signal prodaje",
              valueText: "1.4 kom/dan | 42 kom",
              sourceFields: ["VelocityUnitsPerDay", "UnitsSold", "Revenue"],
              isMissing: false,
              detail: "Prihod 144000 RSD",
            },
            {
              category: "confidence",
              code: "freshness_signal",
              label: "Svezina ulaza",
              valueText: "fresh",
              sourceFields: ["InputFreshnessStatus", "DataQualityStatus"],
              isMissing: false,
              detail: "Kvalitet podataka good",
            },
            {
              category: "impact",
              code: "expected_impact",
              label: "Ocekivani uticaj",
              valueText: "28800 RSD u 14 dana",
              sourceFields: ["ExpectedImpactRsd", "ImpactWindowDays", "RiskIfIgnored"],
              isMissing: false,
              detail: "Rizik je gubitka prodaje.",
            },
          ],
        }),
      ], "good"),
    );

    render(<ProductDecisionCenterPage />);

    fireEvent.click(await screen.findByRole("button", { name: /Za.*\?/i }));

    expect(screen.getByText("Lanac dokaza:")).toBeInTheDocument();
    expect(screen.getAllByText("Odabrana preporuka", { selector: ".evidence-chain-label" }).length).toBeGreaterThan(0);
    expect(screen.getByText("Signal prodaje", { selector: ".evidence-chain-label" })).toBeInTheDocument();
    expect(screen.getByText("28800 RSD u 14 dana")).toBeInTheDocument();
    expect(
      screen.getByText((_, element) => element?.textContent === "Izvor: VelocityUnitsPerDay · UnitsSold · Revenue"),
    ).toBeInTheDocument();
  });

  it("renders a deterministic decision tree path in the Why panel", async () => {
    render(<ProductDecisionCenterPage />);

    fireEvent.click(await screen.findByRole("button", { name: /Za.*\?/i }));

    expect(screen.getByText("Put odluke:")).toBeInTheDocument();
    expect(screen.getByText("Deterministički uslov iz backend grane.")).toBeInTheDocument();
    expect(screen.getAllByText("izabrana grana").length).toBeGreaterThan(0);
  });

  it("renders a structured confidence breakdown in the Why panel", async () => {
    getProductDecisionCenterMock.mockResolvedValueOnce(
      buildResponse([
        makeRow({
          productId: 707,
          recommendationId: "product:707:REPLENISH:20260528:20260626",
          sourceKey: "product:707",
          productName: "Model Confidence",
          sku: "SKU-707",
          confidenceScore: 90,
          confidencePct: 90,
          reliabilityPct: 82,
          confidenceBreakdown: [
            {
              category: "confidence",
              code: "confidence_score",
              label: "Ocena pouzdanosti",
              valueText: "Visoka sigurnost · 90%",
              sourceFields: ["ConfidenceLevel", "ConfidenceScore", "ConfidencePct"],
              isMissing: false,
              detail: "Ocena kombinuje snagu signala i dostupnost ulaza.",
            },
            {
              category: "confidence",
              code: "evidence_coverage",
              label: "Pokrivenost signala",
              valueText: "Široka",
              sourceFields: ["UnitsSold", "VelocityUnitsPerDay", "MarginPct", "TrendPct", "DaysSinceLastSale", "WarningCodes", "ReasonCodes"],
              isMissing: false,
              detail: "Više nezavisnih signala je prisutno: prodaja, marža, zaliha i trend.",
            },
            {
              category: "confidence",
              code: "reliability_signal",
              label: "Pouzdanost signala",
              valueText: "82%",
              sourceFields: ["ReliabilityPct", "SignalConfidencePct"],
              isMissing: false,
              detail: "SignalConfidence 82%",
            },
            {
              category: "confidence",
              code: "freshness_signal",
              label: "Svežina ulaza",
              valueText: "Sveže",
              sourceFields: ["InputFreshnessStatus", "DataQualityStatus"],
              isMissing: false,
              detail: "Kvalitet podataka dobar",
            },
            {
              category: "confidence",
              code: "data_quality_signal",
              label: "Kvalitet podataka",
              valueText: "dobar",
              sourceFields: ["DataQualityStatus", "WarningCodes"],
              isMissing: false,
              detail: "Podaci su konzistentni.",
            },
          ],
        }),
      ], "good"),
    );

    render(<ProductDecisionCenterPage />);

    fireEvent.click(await screen.findByRole("button", { name: /Za.*\?/i }));

    expect(screen.getByText("Raspodela pouzdanosti:")).toBeInTheDocument();
    expect(screen.getByText("Ocena pouzdanosti", { selector: ".evidence-chain-label" })).toBeInTheDocument();
    expect(screen.getByText("Pokrivenost signala", { selector: ".evidence-chain-label" })).toBeInTheDocument();
    expect(screen.getByText("Široka")).toBeInTheDocument();
    expect(screen.getByText("82%", { selector: ".evidence-chain-value" })).toBeInTheDocument();
    expect(screen.getByText("Sveže")).toBeInTheDocument();
    expect(screen.getByText("dobar")).toBeInTheDocument();
  });

  it("renders alternative recommendations in the Why panel", async () => {
    getProductDecisionCenterMock.mockResolvedValueOnce(
      buildResponse([
        makeRow({
          productId: 808,
          recommendationId: "product:808:REPLENISH:20260528:20260626",
          sourceKey: "product:808",
          productName: "Model Alternative",
          sku: "SKU-808",
          alternativeRecommendations: [
            {
              rank: 1,
              recommendationStatus: "BOOST",
              recommendationLabel: "Pojačaj",
              recommendedAction: "Pojačaj vidljivost i planiraj brzu dopunu.",
              reason: "Trend 7.0%, marža 26.0%, velocity 1.40/dan i gap zalihe 1.",
              reasonCodes: ["high_velocity", "low_stock"],
              confidenceLevel: "medium",
              confidenceScore: 64,
              reliabilityPct: 58,
              dataQualityStatus: "good",
              whyLowerRanked: "Dopuna ima neposredniji signal od širenja potražnje, jer je stock gap već vidljiv.",
            },
            {
              rank: 2,
              recommendationStatus: "WATCH",
              recommendationLabel: "Prati",
              recommendedAction: "Nastavi praćenje bez hitne intervencije.",
              reason: "Stabilan signal bez hitne akcije.",
              reasonCodes: ["insufficient_history"],
              confidenceLevel: "low",
              confidenceScore: 34,
              reliabilityPct: 29,
              dataQualityStatus: "good",
              whyLowerRanked: "Čekanje bi odložilo odgovor na postojeći manjak zalihe.",
            },
          ],
        }),
      ], "good"),
    );

    render(<ProductDecisionCenterPage />);

    fireEvent.click(await screen.findByRole("button", { name: /Za.*\?/i }));

    expect(screen.getByText("Alternativne preporuke:")).toBeInTheDocument();
    expect(screen.getByText("Alternativa 1", { selector: ".evidence-chain-category" })).toBeInTheDocument();
    expect(screen.getByText("Pojačaj", { selector: ".evidence-chain-label" })).toBeInTheDocument();
    expect(screen.getByText("Srednja sigurnost · 64%", { selector: ".confidence-pill" })).toBeInTheDocument();
    expect(screen.getByText("Pojačaj vidljivost i planiraj brzu dopunu.")).toBeInTheDocument();
    expect(screen.getByText("Zašto niže: Dopuna ima neposredniji signal od širenja potražnje, jer je stock gap već vidljiv.")).toBeInTheDocument();
    expect(screen.getByText("Prati", { selector: ".evidence-chain-label" })).toBeInTheDocument();
    expect(
      screen.getByText((_, element) =>
        ((element?.classList.contains("evidence-chain-source") ?? false)
          && (element.textContent?.includes("Čekanje bi odložilo odgovor na postojeći manjak zalihe.") ?? false)),
      ),
    ).toBeInTheDocument();
  });

  it("uses the backend Why panel bundle when present", async () => {
    getProductDecisionCenterMock.mockResolvedValueOnce(
      buildResponse([
        makeRow({
          productId: 818,
          recommendationStatus: "BOOST",
          recommendationLabel: "Pojačaj",
          recommendationReason: "Redovi bi i dalje nudili staru lokalnu vrednost.",
          explainabilityText: "Panel explanation from backend bundle.",
          recommendedAction: "Pojačaj vidljivost",
          reasonCodes: ["positive_trend"],
          primaryDrivers: ["trend"],
          warningCodes: ["not_reasonable"],
          confidenceLevel: "medium",
          confidenceScore: 64,
          confidencePct: 64,
          reliabilityPct: 61,
          dataQualityStatus: "good",
          inputFreshnessStatus: "fresh",
          expectedImpactRsd: 42000,
          impactWindowDays: 7,
          riskIfIgnored: "Panel risk from backend bundle.",
          whyPanel: {
            recommendationStatus: "BOOST",
            recommendationLabel: "Pojačaj",
            recommendationReason: "Panel reason from backend bundle.",
            recommendedAction: "Pojačaj vidljivost",
            explainabilityText: "Panel explanation from backend bundle.",
            summarySource: "recommendation_reason",
            summaryFallbackUsed: false,
            summaryFallbackReason: null,
            reasonCodes: ["positive_trend"],
            primaryDrivers: ["trend"],
            warningCodes: ["not_reasonable"],
            confidenceLevel: "medium",
            confidenceScore: 64,
            confidencePct: 64,
            reliabilityPct: 61,
            dataQualityStatus: "good",
            inputFreshnessStatus: "fresh",
            recommendationAllowed: true,
            expectedImpactRsd: 42000,
            impactWindowDays: 7,
            riskIfIgnored: "Panel risk from backend bundle.",
            confidenceBreakdown: [
              {
                category: "confidence",
                code: "confidence_score",
                label: "Ocena pouzdanosti",
                valueText: "Srednja sigurnost · 64%",
                sourceFields: ["ConfidenceLevel", "ConfidenceScore", "ConfidencePct"],
                isMissing: false,
                detail: "Uzeto iz backend bundle-a.",
              },
            ],
            alternativeRecommendations: [],
            evidenceChain: [
              {
                category: "decision",
                code: "selected_recommendation",
                label: "Odabrana preporuka",
                valueText: "Pojačaj",
                sourceFields: ["RecommendationStatus", "RecommendationLabel", "RecommendationReason"],
                isMissing: false,
                detail: "Panel explanation from backend bundle.",
              },
            ],
          },
        }),
      ], "good"),
    );

    render(<ProductDecisionCenterPage />);

    fireEvent.click(await screen.findByRole("button", { name: /Za.*\?/i }));

    expect(screen.getByText(/Izvor objašnjenja: direktan razlog preporuke/i)).toBeInTheDocument();
    expect(
      screen.getByText((_, element) =>
        element?.tagName === "DIV"
        && element.classList.contains("reason-block")
        && (element.querySelector("strong")?.textContent?.includes("Zašto ova preporuka?") ?? false),
      ),
    ).toHaveTextContent("Panel explanation from backend bundle.");
    expect(screen.getByText("Pojačaj vidljivost")).toBeInTheDocument();
    expect(screen.queryByText("Redovi bi i dalje nudili staru lokalnu vrednost.")).not.toBeInTheDocument();
  });

  it("shows explicit fallback state when the recommendation reason is missing", async () => {
    getProductDecisionCenterMock.mockResolvedValueOnce(
      buildResponse([
        makeRow({
          productId: 909,
          recommendationId: "product:909:REPLENISH:20260528:20260626",
          sourceKey: "product:909",
          recommendationReason: "",
          explainabilityText: "Kompovano iz backend signala.",
          riskIfIgnored: "Rizik je gubitak prodaje.",
        }),
      ], "good"),
    );

    render(<ProductDecisionCenterPage />);

    fireEvent.click(await screen.findByRole("button", { name: /Za.*\?/i }));

    expect(screen.getByText(/Izvor objašnjenja: backend kompozicija signala/i)).toBeInTheDocument();
    expect(screen.getByText(/Fallback: RecommendationReason nije bio dostupan/i)).toBeInTheDocument();
    const fallbackBlock = screen.getByText(/Zašto ova preporuka\?/i).closest(".reason-block");
    expect(fallbackBlock).not.toBeNull();
    if (!fallbackBlock) {
      throw new Error("Fallback blok nije pronađen.");
    }

    expect(fallbackBlock.textContent).toContain("Kompovano iz backend signala.");
  });

  it("keeps confident recommendations honest when expected impact is missing", async () => {
    getProductDecisionCenterMock.mockResolvedValueOnce(
      buildResponse([
        makeRow({
          productId: 303,
          recommendationId: "product:303:REPLENISH:20260528:20260626",
          sourceKey: "product:303",
          productName: "Model Z",
          sku: "SKU-303",
          revenue: 180000,
          unitsSold: 52,
          velocityUnitsPerDay: 1.8,
          marginContribution: 36000,
          marginPct: 28,
          marginCoveragePct: 91,
          currentStock: 3,
          minStock: 10,
          stockGap: 7,
          trendPct: 14,
          lostSalesEstimate: 25000,
          stockCoverDays: 2,
          stockCoverStatus: "low_cover",
          sellThroughRatio: 0.5,
          sellThroughStatus: "warning",
          recommendationAllowed: true,
          confidenceLevel: "high",
          confidenceScore: 91,
          confidencePct: 91,
          reliabilityPct: 87,
          recommendationReason: "Brza prodaja i niska zaliha.",
          warningCodes: ["expected_impact_denominator_missing"],
          primaryDrivers: ["sales_velocity", "stock_risk"],
          reasonCodes: ["high_velocity", "low_stock", "expected_impact_denominator_missing"],
          expectedImpactRsd: null,
          impactWindowDays: 14,
          riskIfIgnored: "Moguca rasprodaja.",
          explainabilityText: "Brza prodaja i niska zaliha.",
          inputFreshnessStatus: "fresh",
          confidenceBreakdown: [
            {
              category: "confidence",
              code: "confidence_score",
              label: "Ocena pouzdanosti",
              valueText: "Nedovoljno podataka",
              sourceFields: ["ConfidenceLevel", "ConfidenceScore", "ConfidencePct"],
              isMissing: true,
              detail: "Ocena kombinuje snagu signala i dostupnost ulaza.",
            },
            {
              category: "confidence",
              code: "evidence_coverage",
              label: "Pokrivenost signala",
              valueText: "Nedovoljna",
              sourceFields: ["UnitsSold", "VelocityUnitsPerDay", "MarginPct", "TrendPct", "DaysSinceLastSale", "WarningCodes", "ReasonCodes"],
              isMissing: true,
              detail: "Obavezni signali nisu kompletni.",
            },
            {
              category: "confidence",
              code: "freshness_signal",
              label: "Svežina ulaza",
              valueText: "Kritično",
              sourceFields: ["InputFreshnessStatus", "DataQualityStatus"],
              isMissing: false,
              detail: "Kvalitet podataka kritičan",
            },
            {
              category: "confidence",
              code: "data_quality_signal",
              label: "Kvalitet podataka",
              valueText: "nedovoljno podataka",
              sourceFields: ["DataQualityStatus", "WarningCodes"],
              isMissing: true,
              detail: "Nedovoljno signala za stabilnu preporuku.",
            },
          ],
          evidenceChain: [
            {
              category: "impact",
              code: "expected_impact",
              label: "Ocekivani uticaj",
              valueText: "Nije dostupno",
              sourceFields: ["ExpectedImpactRsd", "ImpactWindowDays", "RiskIfIgnored"],
              isMissing: true,
              detail: "Moguca rasprodaja.",
            },
          ],
          recommendedAction: "Dopuni odmah.",
        }),
      ], "warning"),
    );

    render(<ProductDecisionCenterPage />);

    expect(await screen.findByText(/Visoka sigurnost/i)).toBeInTheDocument();
    expect(screen.getByText("Procena uticaja nije dostupna.")).toBeInTheDocument();
    expect(screen.getByText("Upozorenje: nedostaje ulaz za procenu uticaja.")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /Za.*\?/i }));
    expect(screen.getByText("Raspodela pouzdanosti:")).toBeInTheDocument();
    expect(screen.getByText("Nedovoljno podataka", { selector: ".evidence-chain-missing" })).toBeInTheDocument();
    expect(screen.getByText("Nije dostupno", { selector: ".evidence-chain-missing" })).toBeInTheDocument();

    fireEvent.click(screen.getAllByRole("button", { name: "Dodaj u akcije" })[0]);

    await waitFor(() => {
      expect(upsertAnalyticsActionWithResultMock).toHaveBeenCalledTimes(1);
    });

    expect(upsertAnalyticsActionWithResultMock).toHaveBeenCalledWith(
      expect.objectContaining({
        expectedImpactRsd: undefined,
        impactEstimateRsd: undefined,
        confidencePct: 91,
      }),
    );
  });

  it("uses backend warning codes without backfilling from reason codes", async () => {
    getProductDecisionCenterMock.mockResolvedValueOnce(
      buildResponse([
        makeRow({
          productId: 505,
          recommendationId: "product:505:REPLENISH:20260528:20260626",
          sourceKey: "product:505",
          productName: "Model Warning",
          sku: "SKU-505",
          currentStock: 4,
          minStock: 8,
          stockGap: 4,
          warningCodes: [],
          reasonCodes: ["missing_cost"],
          primaryDrivers: null,
          confidenceScore: null,
          confidencePct: 89,
          expectedImpactRsd: null,
          inputFreshnessStatus: null,
          daysSinceLastSale: 90,
          recommendationReason: "Brza prodaja i nizak stock cover.",
          explainabilityText: "Brza prodaja i nizak stock cover.",
          confidenceLevel: "high",
          reliabilityPct: 66,
          recommendationAllowed: true,
        }),
      ], "warning"),
    );

    render(<ProductDecisionCenterPage />);

    expect(await screen.findByText("Model Warning")).toBeInTheDocument();
    expect(screen.getByText("Visoka sigurnost")).toBeInTheDocument();
    expect(screen.queryByText(/89%/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Upozorenja:/i)).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /Za.*\?/i }));

    expect(screen.getByText("Nema dodatnih upozorenja.")).toBeInTheDocument();
    expect(screen.getByText("Nedostaje nabavna cena.")).toBeInTheDocument();
    expect(screen.getByText("Procena uticaja nije dostupna.")).toBeInTheDocument();
    expect(screen.getByText(/Svežina ulaza: Nije poznato/i)).toBeInTheDocument();
    expect(screen.queryByText("Brzina prodaje", { selector: ".reason-chip" })).not.toBeInTheDocument();
  });

  it("shows insufficient-data confidence and avoids fake zero impact", async () => {
    getProductDecisionCenterMock.mockResolvedValueOnce(
      buildResponse([
        makeRow({
          productId: 202,
          recommendationId: "product:202:INSUFFICIENT_DATA:20260528:20260626",
          sourceKey: "product:202",
          recommendationType: "INSUFFICIENT_DATA",
          productName: "Model Y",
          sku: "SKU-202",
          revenue: 0,
          unitsSold: 0,
          velocityUnitsPerDay: 0,
          marginContribution: 0,
          marginPct: null,
          marginQualityLabel: "Nedovoljno",
          marginCoveragePct: 0,
          currentStock: 0,
          minStock: 0,
          stockGap: 0,
          trendPct: null,
          lostSalesEstimate: 0,
          slowStockCapital: 0,
          stockCoverDays: null,
          stockCoverStatus: "insufficient_data",
          stockCoverStatusLabel: "Nedovoljno podataka",
          sellThroughRatio: null,
          sellThroughStatus: "insufficient_data",
          sellThroughStatusLabel: "Nedovoljno podataka",
          recommendationAllowed: false,
          confidenceLevel: "insufficient_data",
          confidenceScore: null,
          confidencePct: 32,
          reliabilityPct: 24,
          recommendationStatus: "INSUFFICIENT_DATA",
          recommendationLabel: "Nedovoljno podataka",
          recommendationReason: "Nedovoljno signala za pouzdanu preporuku.",
          warningCodes: ["insufficient_history", "missing_cost", "expected_impact_denominator_missing"],
          primaryDrivers: ["sparse_sales", "missing_cost"],
          reasonCodes: ["insufficient_history", "missing_cost"],
          expectedImpactRsd: null,
          impactWindowDays: null,
          riskIfIgnored: "Rizik je da odluka ostane zasnovana na slabom signalu.",
          explainabilityText: "Nedovoljno signala za pouzdanu preporuku.",
          inputFreshnessStatus: "critical",
          recommendedAction: "Sacekaj dodatne podatke pre poslovne odluke.",
        }),
      ], "insufficient_data"),
    );

    render(<ProductDecisionCenterPage />);

    expect(await screen.findByText("Nedovoljno podataka", { selector: ".confidence-pill" })).toBeInTheDocument();
    expect(screen.getByText("Procena uticaja nije dostupna.")).toBeInTheDocument();
    expect(screen.getByText("Upozorenje: nedostaje ulaz za procenu uticaja.")).toBeInTheDocument();
    expect(screen.queryByText(/Visoka sigurnost/i)).not.toBeInTheDocument();
  });

  it("keeps stale stock freshness visible on expanded replenishment rows", async () => {
    getProductDecisionCenterMock.mockResolvedValueOnce(
      buildResponse([
        makeRow({
          productId: 404,
          recommendationId: "product:404:REPLENISH:20260528:20260626",
          sourceKey: "product:404",
          productName: "Model Stale",
          daysSinceLastSale: 12,
          inputFreshnessStatus: "stale",
          dataQualityStatus: "warning",
          recommendationReason: "Signal dopune postoji, ali ulaz nije svez.",
        }),
      ], "warning"),
    );

    render(<ProductDecisionCenterPage />);

    fireEvent.click(await screen.findByRole("button", { name: /Za.*\?/i }));
    expect(screen.getByText(/Svežina ulaza: Zastarelo/i)).toBeInTheDocument();
  });
});

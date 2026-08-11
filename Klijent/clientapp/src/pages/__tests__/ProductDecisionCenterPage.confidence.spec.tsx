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
const getAnalyticsActionSourceStatusesMock = vi.fn();
const upsertAnalyticsActionWithResultMock = vi.fn();

vi.mock("../../services/analyticsApi", () => ({
  AnalyticsMetaError: class extends Error {},
  getStores: (...args: unknown[]) => getStoresMock(...args),
  getSupplierFilters: (...args: unknown[]) => getSupplierFiltersMock(...args),
  getProductDecisionCenter: (...args: unknown[]) => getProductDecisionCenterMock(...args),
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
  return {
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
    recommendedAction: "Dopuni zalihe",
    daysSinceLastSale: 12,
    ...overrides,
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
});

describe("ProductDecisionCenterPage confidence contract", () => {
  it("renders strong recommendations with explicit estimated impact wording", async () => {
    render(<ProductDecisionCenterPage />);

    expect(await screen.findByText(/Visoka sigurnost/i)).toBeInTheDocument();
    expect(screen.getByText("Dopuni zalihe")).toBeInTheDocument();
    expect(screen.getByText(/Procena uticaja:/i)).toBeInTheDocument();

    fireEvent.click(screen.getAllByRole("button", { name: /Za.*\?/i })[0]);

    expect(screen.getByText(/Zašto ova preporuka\?/i)).toBeInTheDocument();
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
    expect(screen.getByText("Odabrana preporuka", { selector: ".evidence-chain-label" })).toBeInTheDocument();
    expect(screen.getByText("Signal prodaje", { selector: ".evidence-chain-label" })).toBeInTheDocument();
    expect(screen.getByText("28800 RSD u 14 dana")).toBeInTheDocument();
    expect(
      screen.getByText((_, element) => element?.textContent === "Izvor: VelocityUnitsPerDay · UnitsSold · Revenue"),
    ).toBeInTheDocument();
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

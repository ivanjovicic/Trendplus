import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import ProductDecisionCenterPage from "../ProductDecisionCenterPage";

vi.mock("react-router-dom", async () => {
  return {
    Link: ({ children }: { children: ReactNode }) => <a>{children}</a>,
  };
  it("keeps confident recommendations honest when expected impact is missing", async () => {
    getProductDecisionCenterMock.mockResolvedValueOnce({
      rows: [
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
          lostSalesEstimate: 0,
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
          riskIfIgnored: "Moguća rasprodaja.",
          explainabilityText: "Brza prodaja i niska zaliha.",
          inputFreshnessStatus: "fresh",
          recommendedAction: "Dopuni odmah.",
        }),
      ],
      summary: {
        lostSalesEstimate: 0,
        slowStockCapital: 0,
      },
      totalRows: 1,
      generatedAtUtc: "2026-05-26T12:00:00Z",
      periodFromUtc: "2026-04-27",
      periodToUtc: "2026-05-26",
      meta: {
        success: true,
        dataQualityStatus: "warning",
      },
    });

    render(<ProductDecisionCenterPage />);

    expect(await screen.findByText("Visoka sigurnost Â· 91%")).toBeInTheDocument();
    expect(screen.getByText("Procena uticaja nije dostupna.")).toBeInTheDocument();
    expect(screen.getByText("Upozorenje: nedostaje ulaz za procenu uticaja.")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Dodaj u akcije" }));

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
});

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
    ...overrides,
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
  getProductDecisionCenterMock.mockResolvedValue({
    rows: [makeRow()],
    summary: {
      lostSalesEstimate: 25000,
      slowStockCapital: 0,
    },
    totalRows: 1,
    generatedAtUtc: "2026-05-26T12:00:00Z",
    periodFromUtc: "2026-04-27",
    periodToUtc: "2026-05-26",
    meta: {
      success: true,
      dataQualityStatus: "good",
    },
  });
});

describe("ProductDecisionCenterPage confidence contract", () => {
  it("renders confidence, explanation and drivers for a strong recommendation", async () => {
    render(<ProductDecisionCenterPage />);

    expect(await screen.findByText("Visoka sigurnost · 88%")).toBeInTheDocument();
    expect(screen.getByText("Dopuni zalihe")).toBeInTheDocument();

    fireEvent.click(screen.getAllByRole("button", { name: "Zašto?" })[0]);

    expect(screen.getByText("Zašto ova preporuka?")).toBeInTheDocument();
    expect(screen.getByText("Glavni pokretači:")).toBeInTheDocument();
    expect(screen.getByText("Brzina prodaje", { selector: ".reason-chip" })).toBeInTheDocument();
    expect(screen.getByText("Rizik zalihe", { selector: ".reason-chip" })).toBeInTheDocument();
    expect(screen.getByText("Očekivani uticaj:")).toBeInTheDocument();

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

  it("shows insufficient-data confidence and avoids fake zero impact", async () => {
    getProductDecisionCenterMock.mockResolvedValueOnce({
      rows: [
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
          recommendedAction: "Sačekaj dodatne podatke pre poslovne odluke.",
        }),
      ],
      summary: {
        lostSalesEstimate: 0,
        slowStockCapital: 0,
      },
      totalRows: 1,
      generatedAtUtc: "2026-05-26T12:00:00Z",
      periodFromUtc: "2026-04-27",
      periodToUtc: "2026-05-26",
      meta: {
        success: true,
        dataQualityStatus: "insufficient_data",
      },
    });

    render(<ProductDecisionCenterPage />);

    expect(await screen.findByText("Nedovoljno podataka", { selector: ".confidence-pill" })).toBeInTheDocument();
    expect(screen.getByText("Procena uticaja nije dostupna.")).toBeInTheDocument();
    expect(screen.getByText("Upozorenje: nedostaje ulaz za procenu uticaja.")).toBeInTheDocument();
    expect(screen.queryByText("Visoka sigurnost · 88%")).not.toBeInTheDocument();
  });
});

import { render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import ProductDecisionCenterPage from "../ProductDecisionCenterPage";

vi.mock("react-router-dom", async () => {
  return {
    Link: ({ children }: { children: ReactNode }) => <a>{children}</a>,
  };
});

const getStoresMock = vi.fn();
const getSupplierFiltersMock = vi.fn();
const getProductDecisionCenterMock = vi.fn();
const getAnalyticsActionSourceStatusesMock = vi.fn();

vi.mock("../../services/analyticsApi", () => ({
  AnalyticsMetaError: class extends Error {},
  getStores: (...args: unknown[]) => getStoresMock(...args),
  getSupplierFilters: (...args: unknown[]) => getSupplierFiltersMock(...args),
  getProductDecisionCenter: (...args: unknown[]) => getProductDecisionCenterMock(...args),
  getProductDecisionTimelineExportCsv: vi.fn(),
  getAnalyticsActionSourceStatuses: (...args: unknown[]) => getAnalyticsActionSourceStatusesMock(...args),
  upsertAnalyticsActionWithResult: vi.fn(),
}));

vi.mock("../../components/analytics/AnalyticsTrustHeader", () => ({ default: () => null }));
vi.mock("../../components/analytics/AnalyticsTableToolbar", () => ({ default: () => null }));
vi.mock("../../components/analytics/AnalyticsEmptyState", () => ({
  default: ({
    message,
    variant,
  }: {
    message?: string;
    variant?: string;
  }) => (
    <div data-testid="analytics-empty-state" data-variant={variant}>
      {message ? <span>{message}</span> : null}
    </div>
  ),
}));
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

describe("ProductDecisionCenterPage action status fallback", () => {
  beforeEach(() => {
    vi.clearAllMocks();

    getStoresMock.mockResolvedValue([]);
    getSupplierFiltersMock.mockResolvedValue([]);
    getAnalyticsActionSourceStatusesMock.mockResolvedValue({ items: [] });
    getProductDecisionCenterMock.mockResolvedValue({
      rows: [
        {
          productId: 101,
          productName: "Model X",
          sku: "SKU-101",
          category: "Sneakers",
          tipObuce: "Patike",
          supplierName: "Supplier A",
          supplierId: 77,
          revenue: 120000,
          unitsSold: 40,
          velocityUnitsPerDay: 1.2,
          marginPct: 24,
          marginQualityLabel: "Dobro",
          marginCoveragePct: 90,
          currentStock: 10,
          minStock: 5,
          stockGap: 0,
          trendPct: 3,
          confidencePct: 88,
          confidenceLevel: "high",
          confidenceScore: 88,
          reliabilityPct: 79,
          recommendationStatus: "REPLENISH",
          recommendationLabel: "Dopuni",
          recommendedAction: "Dopuni zalihe",
          recommendationReason: "Brza prodaja i nizak stock cover.",
          reasonCodes: ["high_velocity"],
          warningCodes: [],
          primaryDrivers: ["sales_velocity", "stock_risk"],
          lostSalesEstimate: 25000,
          expectedImpactRsd: 25000,
          impactWindowDays: 14,
          riskIfIgnored: "Rizik je gubitka prodaje.",
          explainabilityText: "Brza prodaja i nizak stock cover.",
          inputFreshnessStatus: "fresh",
          dataQualityStatus: "good",
          stockCoverDays: 5,
          stockCoverStatus: "low_cover",
          sellThroughRatio: 0.6,
          sellThroughStatus: "good",
        },
      ],
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

  it("keeps product recommendations visible when optional action status lookup fails", async () => {
    getAnalyticsActionSourceStatusesMock.mockRejectedValueOnce(new Error("404 Not Found"));

    render(<ProductDecisionCenterPage />);

    expect(await screen.findByText("Model X")).toBeInTheDocument();
    expect(await screen.findByText("Status akcija trenutno nije dostupan.")).toBeInTheDocument();
    expect(screen.getByText("Dopuni zalihe")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Dodaj u akcije" })).toHaveAttribute(
      "title",
      "Dodaj u centralni red akcija. Status postojećih akcija trenutno nije dostupan.",
    );
    expect(screen.queryByRole("button", { name: "U akcijama" })).not.toBeInTheDocument();
  });

  it("keeps blocking error state when the main product decision endpoint fails", async () => {
    getProductDecisionCenterMock.mockRejectedValueOnce(
      new Error("Product Decision Center podaci trenutno nisu dostupni."),
    );

    render(<ProductDecisionCenterPage />);

    expect(await screen.findByRole("alert")).toBeInTheDocument();
    expect(screen.getByText("Podaci trenutno nisu dostupni")).toBeInTheDocument();
    expect(screen.getByText("Product Decision Center podaci trenutno nisu dostupni.")).toBeInTheDocument();
    expect(screen.queryByText("Status akcija trenutno nije dostupan.")).not.toBeInTheDocument();
    expect(screen.queryByText("Model X")).not.toBeInTheDocument();
  });

  it("renders the shared empty state and hides KPI cards when no candidates are returned", async () => {
    getProductDecisionCenterMock.mockResolvedValue({
      rows: [],
      summary: {
        replenishCount: 0,
        markdownCount: 0,
        highPotentialCount: 0,
        badDataCount: 0,
        lostSalesEstimate: 0,
        slowStockCapital: 0,
      },
      totalRows: 0,
      generatedAtUtc: "2026-05-26T12:00:00Z",
      periodFromUtc: "2026-04-27",
      periodToUtc: "2026-05-26",
      meta: {
        success: true,
        emptyReason: "no_candidates",
        dataQualityStatus: "insufficient_data",
      },
    });

    render(<ProductDecisionCenterPage />);

    expect(await screen.findByTestId("analytics-empty-state")).toHaveAttribute("data-variant", "insufficient_data");
    expect(screen.getByTestId("analytics-empty-state")).toHaveTextContent("Ne prikazujemo automatsku preporuku jer signal nije dovoljno jak.");
    expect(document.querySelector(".product-decision-kpis")).toBeNull();
    expect(document.querySelector(".product-decision-table-wrap")).toBeNull();
  });
});

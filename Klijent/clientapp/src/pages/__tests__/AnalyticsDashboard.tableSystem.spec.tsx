import React from "react";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  checkAnalyticsHealth,
  getAnalyticsRefreshStatus,
  getDashboardBootstrap,
  getStores,
  upsertAnalyticsAction,
} from "../../services/analyticsApi";
import AnalyticsDashboard from "../AnalyticsDashboard";

vi.mock("../../components/analytics/AnalyticsDashboardCharts", () => ({
  default: () => <div data-testid="charts-stub" />,
}));

vi.mock("../../services/analyticsApi", async () => {
  const actual = await vi.importActual<typeof import("../../services/analyticsApi")>(
    "../../services/analyticsApi",
  );

  return {
    ...actual,
    checkAnalyticsHealth: vi.fn(),
    getAnalyticsRefreshStatus: vi.fn(),
    getDashboardBootstrap: vi.fn(),
    getStores: vi.fn(),
    upsertAnalyticsAction: vi.fn(),
  };
});

const topRows = [
  {
    productId: 101,
    sku: "SKU-101",
    productName: "Runner 101",
    revenue: 125000,
    units: 12,
    velocityUnitsPerDay: 1.5,
    marginImpact: 34000,
    stockStatus: "good",
    trendPct: 12.4,
    marginQualityLabel: "Margin signal dostupan",
    marginQualityTier: "good",
    marginQualityShortLabel: "Dostupno",
    marginQualityTooltip: "Margin impact je izračunat iz dostupne nabavne cene.",
    dataQualityStatus: "good",
    statusReason: "Margin signal je potvrđen na osnovu dostupne nabavne cene.",
    reasonCodes: ["margin_available"],
  },
  {
    productId: 102,
    sku: "SKU-102",
    productName: "Runner 102",
    revenue: 98000,
    units: 9,
    velocityUnitsPerDay: 1.1,
    marginImpact: 22000,
    stockStatus: "warning",
    trendPct: -4.8,
    marginQualityLabel: "Nedovoljno podataka",
    marginQualityTier: "insufficient_data",
    marginQualityShortLabel: "Nedostaje dokaz",
    marginQualityTooltip: "Nabavna cena nije dostupna, pa margin signal nije potvrđen.",
    dataQualityStatus: "insufficient_data",
    statusReason: "Nabavna cena nije dostupna za ovaj artikal.",
    reasonCodes: ["missing_cost"],
  },
];

const unknownTrendRows = [
  {
    productId: 201,
    sku: "SKU-201",
    productName: "Unknown Trend Product",
    revenue: 50000,
    units: 5,
    velocityUnitsPerDay: 0.8,
    marginImpact: 15000,
    stockStatus: "good",
    trendPct: null, // Unknown trend
    marginQualityLabel: "Margin signal dostupan",
    marginQualityTier: "good",
    marginQualityShortLabel: "Dostupno",
    marginQualityTooltip: "Margin impact je izračunat iz dostupne nabavne cene.",
    dataQualityStatus: "good",
    statusReason: "Margin signal je potvrđen na osnovu dostupne nabavne cene.",
    reasonCodes: ["margin_available"],
  },
  {
    productId: 202,
    sku: "SKU-202",
    productName: "NaN Trend Product",
    revenue: 35000,
    units: 3,
    velocityUnitsPerDay: 0.5,
    marginImpact: 10000,
    stockStatus: "warning",
    trendPct: NaN, // Invalid non-finite trend
    marginQualityLabel: "Nedovoljno podataka",
    marginQualityTier: "insufficient_data",
    marginQualityShortLabel: "Nedostaje dokaz",
    marginQualityTooltip: "Nabavna cena nije dostupna, pa margin signal nije potvrđen.",
    dataQualityStatus: "insufficient_data",
    statusReason: "Nabavna cena nije dostupna za ovaj artikal.",
    reasonCodes: ["missing_cost"],
  },
  {
    productId: 203,
    sku: "SKU-203",
    productName: "Infinity Trend Product",
    revenue: 20000,
    units: 2,
    velocityUnitsPerDay: 0.3,
    marginImpact: 5000,
    stockStatus: "critical",
    trendPct: Infinity, // Non-finite trend
    marginQualityLabel: "Nedovoljno podataka",
    marginQualityTier: "insufficient_data",
    marginQualityShortLabel: "Nedostaje dokaz",
    marginQualityTooltip: "Nabavna cena nije dostupna, pa margin signal nije potvrđen.",
    dataQualityStatus: "insufficient_data",
    statusReason: "Nabavna cena nije dostupna za ovaj artikal.",
    reasonCodes: ["missing_cost"],
  },
  {
    productId: 204,
    sku: "SKU-204",
    productName: "Zero Trend Product (Measured)",
    revenue: 15000,
    units: 1,
    velocityUnitsPerDay: 0.2,
    marginImpact: 3000,
    stockStatus: "good",
    trendPct: 0, // Genuine measured zero trend
    marginQualityLabel: "Margin signal dostupan",
    marginQualityTier: "good",
    marginQualityShortLabel: "Dostupno",
    marginQualityTooltip: "Margin impact je izračunat iz dostupne nabavne cene.",
    dataQualityStatus: "good",
    statusReason: "Margin signal je potvrđen na osnovu dostupne nabavne cene.",
    reasonCodes: ["margin_available"],
  },
];

describe("AnalyticsDashboard table system", () => {
  beforeEach(() => {
    vi.mocked(getDashboardBootstrap).mockResolvedValue({
      summary: {
        totalRevenue: 12345,
        totalTransactions: 12,
        totalUnits: 8,
      },
      inventory: { totalSkuCount: 100, outOfStockCount: 5, lowStockCount: 10 },
      dailySales: [],
      categoryData: [],
      genderData: [],
      supplierData: [],
      supplierOptions: [{ supplierId: 77, supplierName: "Alfa Shoes" }],
      weekdayData: [],
      hourData: [],
      paymentData: [],
      quickInsights: null,
      transactionStats: null,
      advanced: null,
      topAdvanced: {
        byRevenue: topRows,
        byUnits: topRows,
        byVelocity: topRows,
        byMarginImpact: topRows,
        marginAvailable: true,
        marginMessage: "Margin data available.",
      },
      validationCompleteness: null,
      validationFreshness: null,
      validationLostSales: null,
      executive: null,
      decisionActions: [],
      errors: [],
      meta: { success: true, dataQualityStatus: "good" },
    });
    vi.mocked(getStores).mockResolvedValue([{ storeId: 5, storeName: "Delta" }]);
    vi.mocked(getAnalyticsRefreshStatus).mockResolvedValue({
      isRunning: false,
      currentStep: null,
      dataFreshnessStatus: "good",
      lastSuccessfulRefreshAtUtc: "2026-08-05T10:00:00Z",
      jobs: [],
    });
    vi.mocked(checkAnalyticsHealth).mockResolvedValue({
      status: "ok",
      tables: { salesFacts: 10, salesLineFacts: 20, productsDim: 5 },
      message: "ok",
    });
    vi.mocked(upsertAnalyticsAction).mockResolvedValue({} as never);
  });

  it("keeps top-table render and export row counts in sync through the shared table surface", async () => {
    render(
      <MemoryRouter>
        <AnalyticsDashboard />
      </MemoryRouter>,
    );

    fireEvent.click(
      await screen.findByRole("button", { name: /detaljnu analizu/i }),
    );

    const tableSurface = await screen.findByTestId("analytics-data-table");
    const table = within(tableSurface).getByRole("table");
    const rows = within(table).getAllByRole("row");

    expect(rows).toHaveLength(3);
    expect(
      within(tableSurface).getByText("Prikazano: 2 redova"),
    ).toBeInTheDocument();
    expect(within(tableSurface).getByText("Redova: 2")).toBeInTheDocument();
    expect(
      within(tableSurface).queryByText(
        "Prikaz je ograni\u010Den na prvih 10 redova za izabrani KPI pogled.",
      ),
    ).not.toBeInTheDocument();
    expect(
      within(table).getByRole("columnheader", { name: /Promet/i }),
    ).toHaveClass("analytics-data-table__numeric");
    expect(within(table).getByText("SKU-101")).toBeInTheDocument();
    expect(within(table).getByText("Dostupno")).toBeInTheDocument();
    expect(
      within(table).getByTitle("Margin impact je izračunat iz dostupne nabavne cene."),
    ).toBeInTheDocument();
    expect(within(table).getByText("Nedostaje dokaz")).toBeInTheDocument();
    expect(
      within(table).getByTitle("Nabavna cena nije dostupna, pa margin signal nije potvrđen."),
    ).toBeInTheDocument();
  });

  it("excludes unknown/null trends from top gainers list", async () => {
    // FAILING-FIRST TEST: Currently this fails because null trend is coalesced to 0 and included
    vi.mocked(getDashboardBootstrap).mockResolvedValue({
      summary: {
        totalRevenue: 12345,
        totalTransactions: 12,
        totalUnits: 8,
      },
      inventory: { totalSkuCount: 100, outOfStockCount: 5, lowStockCount: 10 },
      dailySales: [],
      categoryData: [],
      genderData: [],
      supplierData: [],
      supplierOptions: [{ supplierId: 77, supplierName: "Alfa Shoes" }],
      weekdayData: [],
      hourData: [],
      paymentData: [],
      quickInsights: null,
      transactionStats: null,
      advanced: null,
      topAdvanced: {
        byRevenue: [
          // positive trend (should be in gainers)
          { ...topRows[0], trendPct: 12.4 },
          // null trend (should NOT be in gainers)
          { ...unknownTrendRows[0], trendPct: null },
          // negative trend (should be in losers, not gainers)
          { ...topRows[1], trendPct: -4.8 },
          // genuine zero trend (should NOT be in gainers)
          { ...unknownTrendRows[3], trendPct: 0 },
        ],
        byUnits: [],
        byVelocity: [],
        byMarginImpact: [],
        marginAvailable: true,
        marginMessage: "Margin data available.",
      },
      validationCompleteness: null,
      validationFreshness: null,
      validationLostSales: null,
      executive: null,
      decisionActions: [],
      errors: [],
      meta: { success: true, dataQualityStatus: "good" },
    });
    vi.mocked(getAnalyticsRefreshStatus).mockResolvedValue({
      isRunning: false,
      currentStep: null,
      dataFreshnessStatus: "good",
      lastSuccessfulRefreshAtUtc: "2026-08-05T10:00:00Z",
      jobs: [],
    });
    vi.mocked(checkAnalyticsHealth).mockResolvedValue({
      status: "ok",
      tables: { salesFacts: 10, salesLineFacts: 20, productsDim: 5 },
      message: "ok",
    });

    render(
      <MemoryRouter>
        <AnalyticsDashboard />
      </MemoryRouter>,
    );
    // Find the top gainers section
    fireEvent.click(
      await screen.findByRole("button", { name: /prikaži detaljnu analizu/i }),
    );
    const gainersSection = await screen.findByTestId("top-gainers-section");

    // Should only see Runner 101 (trendPct: 12.4) in gainers
    expect(within(gainersSection).getByText("Runner 101")).toBeInTheDocument();

    // Should NOT see SKU-201 (null trend) even though it should be filtered out
    // This will fail with current implementation because null is coalesced to 0 and filtered as neutral
    expect(within(gainersSection).queryByText("Unknown Trend Product")).not.toBeInTheDocument();

    // Should NOT see SKU-204 (genuine zero trend, measured neutral)
    expect(within(gainersSection).queryByText("Zero Trend Product (Measured)")).not.toBeInTheDocument();
    expect(screen.getByText("Unknown Trend Product")).toBeInTheDocument();
    expect(screen.getByText("Unknown Trend Product").closest("tr")?.textContent).toContain("Nema trenda");
  });

  it("excludes unknown/non-finite trends from top losers list", async () => {
    // FAILING-FIRST TEST: Currently this fails because NaN/Infinity trends are coalesced to 0 and filtered
    vi.mocked(getDashboardBootstrap).mockResolvedValue({
      summary: {
        totalRevenue: 12345,
        totalTransactions: 12,
        totalUnits: 8,
      },
      inventory: { totalSkuCount: 100, outOfStockCount: 5, lowStockCount: 10 },
      dailySales: [],
      categoryData: [],
      genderData: [],
      supplierData: [],
      supplierOptions: [{ supplierId: 77, supplierName: "Alfa Shoes" }],
      weekdayData: [],
      hourData: [],
      paymentData: [],
      quickInsights: null,
      transactionStats: null,
      advanced: null,
      topAdvanced: {
        byRevenue: [
          // negative trend (should be in losers)
          { ...topRows[1], trendPct: -4.8 },
          // NaN trend (should NOT be in losers)
          { ...unknownTrendRows[1], trendPct: NaN },
          // positive trend (should be in gainers, not losers)
          { ...topRows[0], trendPct: 12.4 },
          // Infinity trend (should NOT be in losers)
          { ...unknownTrendRows[2], trendPct: Infinity },
        ],
        byUnits: [],
        byVelocity: [],
        byMarginImpact: [],
        marginAvailable: true,
        marginMessage: "Margin data available.",
      },
      validationCompleteness: null,
      validationFreshness: null,
      validationLostSales: null,
      executive: null,
      decisionActions: [],
      errors: [],
      meta: { success: true, dataQualityStatus: "good" },
    });
    vi.mocked(getAnalyticsRefreshStatus).mockResolvedValue({
      isRunning: false,
      currentStep: null,
      dataFreshnessStatus: "good",
      lastSuccessfulRefreshAtUtc: "2026-08-05T10:00:00Z",
      jobs: [],
    });
    vi.mocked(checkAnalyticsHealth).mockResolvedValue({
      status: "ok",
      tables: { salesFacts: 10, salesLineFacts: 20, productsDim: 5 },
      message: "ok",
    });

    render(
      <MemoryRouter>
        <AnalyticsDashboard />
      </MemoryRouter>,
    );

    // Find the top losers section
    fireEvent.click(
      await screen.findByRole("button", { name: /prikaži detaljnu analizu/i }),
    );
    const losersSection = await screen.findByTestId("top-losers-section");

    // Should only see Runner 102 (trendPct: -4.8) in losers
    expect(within(losersSection).getByText("Runner 102")).toBeInTheDocument();

    // Should NOT see SKU-202 (NaN trend) or SKU-203 (Infinity trend)
    // This will fail with current implementation because NaN/Infinity are coalesced to 0 and filtered as neutral
    expect(within(losersSection).queryByText("NaN Trend Product")).not.toBeInTheDocument();
    expect(within(losersSection).queryByText("Infinity Trend Product")).not.toBeInTheDocument();
    expect(screen.getByText("NaN Trend Product").closest("tr")?.textContent).toContain("Nema trenda");
    expect(screen.getByText("Infinity Trend Product").closest("tr")?.textContent).toContain("Nema trenda");
  });

  it("preserves genuine zero trend as measured neutral (not in gainers or losers)", async () => {
    // FAILING-FIRST TEST: Verify that genuine zero trend is distinct from unknown
    vi.mocked(getDashboardBootstrap).mockResolvedValue({
      summary: {
        totalRevenue: 12345,
        totalTransactions: 12,
        totalUnits: 8,
      },
      inventory: { totalSkuCount: 100, outOfStockCount: 5, lowStockCount: 10 },
      dailySales: [],
      categoryData: [],
      genderData: [],
      supplierData: [],
      supplierOptions: [{ supplierId: 77, supplierName: "Alfa Shoes" }],
      weekdayData: [],
      hourData: [],
      paymentData: [],
      quickInsights: null,
      transactionStats: null,
      advanced: null,
      topAdvanced: {
        byRevenue: [
          // genuine zero trend
          { ...unknownTrendRows[3], trendPct: 0 },
          // positive trend
          { ...topRows[0], trendPct: 12.4 },
          // negative trend
          { ...topRows[1], trendPct: -4.8 },
        ],
        byUnits: [],
        byVelocity: [],
        byMarginImpact: [],
        marginAvailable: true,
        marginMessage: "Margin data available.",
      },
      validationCompleteness: null,
      validationFreshness: null,
      validationLostSales: null,
      executive: null,
      decisionActions: [],
      errors: [],
      meta: { success: true, dataQualityStatus: "good" },
    });
    vi.mocked(getAnalyticsRefreshStatus).mockResolvedValue({
      isRunning: false,
      currentStep: null,
      dataFreshnessStatus: "good",
      lastSuccessfulRefreshAtUtc: "2026-08-05T10:00:00Z",
      jobs: [],
    });
    vi.mocked(checkAnalyticsHealth).mockResolvedValue({
      status: "ok",
      tables: { salesFacts: 10, salesLineFacts: 20, productsDim: 5 },
      message: "ok",
    });

    render(
      <MemoryRouter>
        <AnalyticsDashboard />
      </MemoryRouter>,
    );

    // Find the top gainers section
    fireEvent.click(
      await screen.findByRole("button", { name: /prikaži detaljnu analizu/i }),
    );
    const gainersSection = await screen.findByTestId("top-gainers-section");

    // SKU-204 (zero trend) should NOT be in gainers
    expect(within(gainersSection).queryByText("Zero Trend Product (Measured)")).not.toBeInTheDocument();

    // Find the top losers section
    const losersSection = await screen.findByTestId("top-losers-section");

    // SKU-204 (zero trend) should NOT be in losers
    expect(within(losersSection).queryByText("Zero Trend Product (Measured)")).not.toBeInTheDocument();
    expect(screen.getByText("Zero Trend Product (Measured)").closest("tr")?.textContent).toContain("Bez promene");
  });
});

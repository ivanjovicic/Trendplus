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
});

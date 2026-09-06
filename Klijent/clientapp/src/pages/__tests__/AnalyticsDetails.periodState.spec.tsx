import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter } from "react-router-dom";
import AnalyticsDetails from "../AnalyticsDetails";

const apiMocks = vi.hoisted(() => ({
  checkAnalyticsHealth: vi.fn(),
  getSalesSummary: vi.fn(),
  getDailySales: vi.fn(),
  getInventoryStatus: vi.fn(),
  getTopProductsAdvanced: vi.fn(),
  getDashboardAdvanced: vi.fn(),
  getValidationCompleteness: vi.fn(),
  getValidationFreshness: vi.fn(),
  getValidationLostSales: vi.fn(),
  getValidationNegativeQty: vi.fn(),
}));

vi.mock("../../services/analyticsApi", () => apiMocks);

describe("AnalyticsDetails period state", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    apiMocks.checkAnalyticsHealth.mockResolvedValue({ tables: { salesFacts: 0, salesLineFacts: 0, productsDim: 0 } });
    apiMocks.getSalesSummary.mockResolvedValue({ totalRevenue: 0, totalTransactions: 0, totalUnits: 0 });
    apiMocks.getDailySales.mockResolvedValue([]);
    apiMocks.getInventoryStatus.mockResolvedValue({ totalSkuCount: 0, lowStockCount: 0, outOfStockCount: 0 });
    apiMocks.getTopProductsAdvanced.mockResolvedValue({ byRevenue: [], byUnits: [], byVelocity: [], byMarginImpact: [] });
    apiMocks.getDashboardAdvanced.mockResolvedValue({ cards: [], insights: [], actions: [], validations: [] });
    apiMocks.getValidationCompleteness.mockResolvedValue({ status: "unknown", score: null, affectedSku: null });
    apiMocks.getValidationFreshness.mockResolvedValue({ status: "unknown", freshnessHours: null });
    apiMocks.getValidationLostSales.mockResolvedValue({ status: "unknown", lostSalesEstimate: null });
    apiMocks.getValidationNegativeQty.mockResolvedValue({ status: "unknown", negativeQtyCount: null, totalRows: null });
  });

  it("does not fetch or render KPIs for a reversed custom period", async () => {
    render(
      <MemoryRouter>
        <AnalyticsDetails />
      </MemoryRouter>,
    );

    await waitFor(() => expect(screen.getByText("Promet/dan")).toBeInTheDocument());
    await new Promise((resolve) => setTimeout(resolve, 0));
    vi.clearAllMocks();

    fireEvent.change(screen.getByRole("combobox"), { target: { value: "custom" } });
    fireEvent.change(screen.getByLabelText("Od"), { target: { value: "2026-08-30T00:00" } });
    await waitFor(() => expect(apiMocks.getSalesSummary).toHaveBeenCalled());
    vi.clearAllMocks();
    fireEvent.change(screen.getByLabelText("Do"), { target: { value: "2026-08-01T23:59" } });

    expect(await screen.findByTestId("analytics-details-invalid-period")).toHaveTextContent("Period nije validan");
    expect(screen.getByTestId("analytics-details-invalid-period")).toHaveTextContent("Podaci nisu učitani za ovaj period.");
    expect(screen.queryByText("Promet/dan")).not.toBeInTheDocument();
    expect(apiMocks.getSalesSummary).not.toHaveBeenCalled();
  });
});

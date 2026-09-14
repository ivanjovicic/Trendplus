import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
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
  const getRiskCard = (label: string) => screen.getByText(label).closest("article") as HTMLElement;

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

  it("requests inventory status for the selected period", async () => {
    render(
      <MemoryRouter>
        <AnalyticsDetails />
      </MemoryRouter>,
    );

    await waitFor(() => expect(apiMocks.getInventoryStatus).toHaveBeenCalled());

    expect(apiMocks.getInventoryStatus).toHaveBeenCalledWith(
      2,
      true,
      expect.stringMatching(/T00:00$/),
      expect.stringMatching(/T23:59$/),
    );
  });

  it("ignores a stale analytics load after the period changes", async () => {
    const healthRequests: Array<{ resolve: (value: unknown) => void }> = [];
    apiMocks.checkAnalyticsHealth.mockImplementation(() => new Promise((resolve) => {
      healthRequests.push({ resolve });
    }));

    render(
      <MemoryRouter>
        <AnalyticsDetails />
      </MemoryRouter>,
    );

    await waitFor(() => expect(healthRequests).toHaveLength(1));
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "7d" } });
    await waitFor(() => expect(healthRequests).toHaveLength(2));

    healthRequests[1].resolve({ tables: { salesFacts: 2, salesLineFacts: 2, productsDim: 2 } });
    await waitFor(() => expect(screen.getByText("Analytics baza: 2 prodaja, 2 stavki, 2 proizvoda.")).toBeInTheDocument());

    healthRequests[0].resolve({ tables: { salesFacts: 1, salesLineFacts: 1, productsDim: 1 } });
    await waitFor(() => {
      expect(screen.getByText("Analytics baza: 2 prodaja, 2 stavki, 2 proizvoda.")).toBeInTheDocument();
      expect(screen.queryByText("Analytics baza: 1 prodaja, 1 stavki, 1 proizvoda.")).not.toBeInTheDocument();
    });
  });

  it("preserves valid zero inventory ratios and uses the shared projection", async () => {
    apiMocks.getInventoryStatus.mockResolvedValue({ totalSkuCount: 100, lowStockCount: 0, outOfStockCount: 0 });

    render(
      <MemoryRouter>
        <AnalyticsDetails />
      </MemoryRouter>,
    );

    await waitFor(() => expect(within(getRiskCard("In-stock %")).getByText("100,0%")).toBeInTheDocument());
    expect(within(getRiskCard("Red zone SKU %")).getByText("0,0%")).toBeInTheDocument();
    expect(getRiskCard("In-stock %")).toHaveClass("good");
    expect(getRiskCard("Red zone SKU %")).toHaveClass("good");
  });

  it.each([
    [{ totalSkuCount: 100, lowStockCount: 10 }, "Nije dostupno", "10,0%", "neutral", "warning"],
    [{ totalSkuCount: 100, lowStockCount: 10, outOfStockCount: Number.NaN }, "Nije dostupno", "10,0%", "neutral", "warning"],
    [{ totalSkuCount: 100, lowStockCount: Number.POSITIVE_INFINITY, outOfStockCount: 5 }, "95,0%", "Nije dostupno", "good", "neutral"],
    [null, "Nije dostupno", "Nije dostupno", "neutral", "neutral"],
  ])("renders unavailable ratios without a misleading critical tone for %#", async (inventory, available, red, availableTone, redTone) => {
    apiMocks.getInventoryStatus.mockResolvedValue(inventory);

    render(
      <MemoryRouter>
        <AnalyticsDetails />
      </MemoryRouter>,
    );

    await waitFor(() => expect(within(getRiskCard("In-stock %")).getByText(available)).toBeInTheDocument());
    expect(within(getRiskCard("Red zone SKU %")).getByText(red)).toBeInTheDocument();
    expect(getRiskCard("In-stock %")).toHaveClass(availableTone);
    expect(getRiskCard("Red zone SKU %")).toHaveClass(redTone);
    expect(getRiskCard("In-stock %")).not.toHaveClass("critical");
    expect(getRiskCard("Red zone SKU %")).not.toHaveClass("critical");
  });

  it("keeps the error state separate from unavailable inventory ratios", async () => {
    apiMocks.getInventoryStatus.mockRejectedValue(new Error("inventory unavailable"));

    render(
      <MemoryRouter>
        <AnalyticsDetails />
      </MemoryRouter>,
    );

    expect(await screen.findByText("Greske pri ucitavanju")).toBeInTheDocument();
    await waitFor(() => expect(within(getRiskCard("In-stock %")).getByText("Nije dostupno")).toBeInTheDocument());
    expect(screen.getByText(/inventory unavailable/)).toBeInTheDocument();
  });
});

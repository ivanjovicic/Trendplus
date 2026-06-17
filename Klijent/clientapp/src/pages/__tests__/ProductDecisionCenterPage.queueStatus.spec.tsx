import { fireEvent, render, screen, waitFor } from "@testing-library/react";
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

describe("ProductDecisionCenterPage queue status sync", () => {
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
          reliabilityPct: 79,
          recommendationStatus: "REPLENISH",
          recommendationLabel: "Dopuni",
          recommendedAction: "Dopuni zalihe",
          recommendationReason: "Brza prodaja i nizak stock cover.",
          reasonCodes: ["high_velocity"],
          lostSalesEstimate: 25000,
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

  it("uses batch source status endpoint for visible rows", async () => {
    const { unmount } = render(<ProductDecisionCenterPage />);

    await waitFor(() => {
      expect(getAnalyticsActionSourceStatusesMock).toHaveBeenCalled();
    });

    expect(getAnalyticsActionSourceStatusesMock).toHaveBeenCalledWith(
      expect.objectContaining({
        items: expect.arrayContaining([
          expect.objectContaining({ sourceType: "product" }),
        ]),
      }),
    );

    unmount();
  });

  it("keeps explicit UI labels but does not rewrite backend reason or action text", async () => {
    getProductDecisionCenterMock.mockResolvedValue({
      rows: [
        {
          productId: 202,
          productName: "Model Y",
          sku: "SKU-202",
          category: "Sneakers",
          tipObuce: "Patike",
          supplierName: "Supplier B",
          supplierId: 88,
          revenue: 95000,
          unitsSold: 22,
          velocityUnitsPerDay: 0.8,
          marginPct: 18,
          marginQualityLabel: "Visok kvalitet",
          marginCoveragePct: 91,
          currentStock: 6,
          minStock: 4,
          stockGap: 0,
          trendPct: 5,
          confidencePct: 82,
          reliabilityPct: 70,
          recommendationStatus: "BOOST",
          recommendationLabel: "Pojacaj",
          recommendedAction: "Nastavi pracenje.",
          recommendationReason: "Marza je ispod zeljenog nivoa.",
          reasonCodes: ["poor_margin"],
          lostSalesEstimate: 10000,
          dataQualityStatus: "good",
          stockCoverDays: 7,
          stockCoverStatus: "healthy",
          sellThroughRatio: 0.7,
          sellThroughStatus: "good",
        },
      ],
      summary: {
        lostSalesEstimate: 10000,
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

    render(<ProductDecisionCenterPage />);

    expect(await screen.findByText("Pojačaj")).toBeInTheDocument();
    expect(screen.queryByText("Pojacaj")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Zašto?" })).toHaveAttribute("title", "Marza je ispod zeljenog nivoa.");
    expect(screen.getByText("Nastavi pracenje.")).toBeInTheDocument();
  });

  it("ignores immediate duplicate add-to-queue clicks for the same row", async () => {
    let resolveUpsert: ((value: unknown) => void) | null = null;
    upsertAnalyticsActionWithResultMock.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          resolveUpsert = resolve;
        }),
    );

    render(<ProductDecisionCenterPage />);

    const addButtons = await screen.findAllByRole("button", { name: "Dodaj u akcije" });
    fireEvent.click(addButtons[0]);
    fireEvent.click(addButtons[0]);

    expect(upsertAnalyticsActionWithResultMock).toHaveBeenCalledTimes(1);

    resolveUpsert?.({
      item: { id: 1, sourceKey: "product:101:replenish:2026-05-28:2026-06-26:all:all" },
      created: true,
      existing: false,
      status: "new",
      sourceKey: "product:101:replenish:2026-05-28:2026-06-26:all:all",
    });

    await waitFor(() => {
      expect(screen.getAllByRole("button", { name: "U akcijama" }).length).toBeGreaterThan(0);
    });
  });

  it("keeps product recommendations visible when optional action status lookup fails", async () => {
    getAnalyticsActionSourceStatusesMock.mockRejectedValueOnce(new Error("404 Not Found"));

    render(<ProductDecisionCenterPage />);

    expect(await screen.findByText("Model X")).toBeInTheDocument();
    expect(await screen.findByText("Status akcija trenutno nije dostupan.")).toBeInTheDocument();
    expect(screen.getByText("Dopuni zalihe")).toBeInTheDocument();
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
});

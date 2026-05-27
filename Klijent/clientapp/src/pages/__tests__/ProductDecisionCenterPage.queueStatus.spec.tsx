import { render, waitFor } from "@testing-library/react";
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
  getAnalyticsActionSourceStatuses: (...args: unknown[]) => getAnalyticsActionSourceStatusesMock(...args),
  upsertAnalyticsActionWithResult: vi.fn(),
}));

vi.mock("../../components/analytics/AnalyticsTrustHeader", () => ({ default: () => null }));
vi.mock("../../components/analytics/AnalyticsTableToolbar", () => ({ default: () => null }));
vi.mock("../../components/analytics/AnalyticsEmptyState", () => ({ default: () => null }));
vi.mock("../../components/analytics/AnalyticsErrorState", () => ({ default: () => null }));
vi.mock("../../components/analytics/KpiExplainButton", () => ({ default: () => null }));
vi.mock("../../components/ui/InfoTip", () => ({ default: () => null }));

describe("ProductDecisionCenterPage queue status sync", () => {
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
});

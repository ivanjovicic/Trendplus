import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import InventoryPage from "../InventoryPage";

const getAnalyticsActionSourceStatusesMock = vi.fn();
const getStoresMock = vi.fn();
const getSupplierFiltersMock = vi.fn();
const getInventoryBalanceMock = vi.fn();
const getInventoryListMock = vi.fn();
const getInventoryInsightsMock = vi.fn();
const getInventoryStoreComparisonMock = vi.fn();
const getInventoryActionSuggestionsMock = vi.fn();
const getForecastMock = vi.fn();
const getInventoryAlertsMock = vi.fn();
const getRebalanceSuggestionsMock = vi.fn();
const getInventoryReportSchedulesMock = vi.fn();

vi.mock("../../services/analyticsApi", () => ({
  AnalyticsMetaError: class extends Error {},
  getAnalyticsActionSourceStatuses: (...args: unknown[]) => getAnalyticsActionSourceStatusesMock(...args),
  getStores: (...args: unknown[]) => getStoresMock(...args),
  getSupplierFilters: (...args: unknown[]) => getSupplierFiltersMock(...args),
  getInventoryBalance: (...args: unknown[]) => getInventoryBalanceMock(...args),
  getInventoryList: (...args: unknown[]) => getInventoryListMock(...args),
  getInventoryInsights: (...args: unknown[]) => getInventoryInsightsMock(...args),
  getInventoryStoreComparison: (...args: unknown[]) => getInventoryStoreComparisonMock(...args),
  getInventoryActionSuggestions: (...args: unknown[]) => getInventoryActionSuggestionsMock(...args),
  getForecast: (...args: unknown[]) => getForecastMock(...args),
  getInventoryAlerts: (...args: unknown[]) => getInventoryAlertsMock(...args),
  getRebalanceSuggestions: (...args: unknown[]) => getRebalanceSuggestionsMock(...args),
  getInventoryReportSchedules: (...args: unknown[]) => getInventoryReportSchedulesMock(...args),
  createInventoryReportSchedule: vi.fn(),
  exportInventoryReport: vi.fn(),
  getInventoryItemDetail: vi.fn(),
  getSizeCurve: vi.fn(),
  previewInventoryReport: vi.fn(),
  printBlankInventoryForm: vi.fn(),
  runInventoryReportScheduleNow: vi.fn(),
  saveInventoryActionDecision: vi.fn(),
  upsertAnalyticsActionWithResult: vi.fn(),
}));

vi.mock("../../components/analytics/AnalyticsTrustHeader", () => ({ default: () => null }));
vi.mock("../../components/analytics/AnalyticsEmptyState", () => ({ default: () => null }));
vi.mock("../../components/analytics/AnalyticsErrorState", () => ({ default: () => null }));
vi.mock("../../components/analytics/KpiExplainButton", () => ({ default: () => null }));
vi.mock("../../components/inventory/ActionWorkflowPanel", () => ({ ActionWorkflowPanel: () => null }));
vi.mock("../../components/inventory/DecisionSummaryBar", () => ({ DecisionSummaryBar: () => null }));
vi.mock("../../components/inventory/DemandForecastPanel", () => ({ DemandForecastPanel: () => null }));
vi.mock("../../components/inventory/ExportSchedulerPanel", () => ({ ExportSchedulerPanel: () => null }));
vi.mock("../../components/inventory/InventoryAlertsFeed", () => ({ InventoryAlertsFeed: () => null }));
vi.mock("../../components/inventory/InventoryInsightPanels", () => ({ InventoryInsightPanels: () => null }));
vi.mock("../../components/inventory/InventoryItemsTable", () => ({
  InventoryItemsTable: ({ rows, isRowQueued }: { rows: Array<Record<string, unknown>>; isRowQueued: (row: Record<string, unknown>) => boolean }) => (
    <div
      data-testid="inventory-items-table"
      data-queued={rows.length > 0 ? String(isRowQueued(rows[0])) : "false"}
    />
  ),
}));
vi.mock("../../components/inventory/InventoryKPICards", () => ({ InventoryKPICards: () => null }));
vi.mock("../../components/inventory/InventoryPriorityPanels", () => ({ InventoryPriorityPanels: () => null }));
vi.mock("../../components/inventory/MailSchedulerPanel", () => ({ MailSchedulerPanel: () => null }));
vi.mock("../../components/inventory/RebalancingTable", () => ({ RebalancingTable: () => null }));
vi.mock("../../components/inventory/SKUDetailModal", () => ({ SKUDetailModal: () => null }));
vi.mock("../../components/inventory/SizeCurvePanel", () => ({ SizeCurvePanel: () => null }));
vi.mock("../../components/inventory/StoreComparisonPanel", () => ({ StoreComparisonPanel: () => null }));
vi.mock("../../components/ErrorBoundary", () => ({ ErrorBoundary: ({ children }: { children: ReactNode }) => <>{children}</> }));

describe("InventoryPage queue status sync", () => {
  beforeEach(() => {
    vi.clearAllMocks();

    getStoresMock.mockResolvedValue([{ storeId: 1, storeName: "Prodavnica 1" }]);
    getSupplierFiltersMock.mockResolvedValue([]);
    getInventoryReportSchedulesMock.mockResolvedValue([]);
    getInventoryBalanceMock.mockResolvedValue({
      totalSku: 1,
      totalOnHand: 10,
      outOfStockCount: 0,
      lowStockCount: 0,
      estimatedInventoryValue: 1000,
      meta: { success: true, dataQualityStatus: "good" },
    });
    getInventoryListMock.mockResolvedValue({
      items: [
        {
          id: 501,
          naziv: "Artikal A",
          plu: "PLU-501",
          kolicina: 10,
          minimalnaKolicina: 3,
          nabavnaCena: 100,
          estimatedValue: 1000,
          idObjekat: 1,
          idDobavljac: null,
          stockCoverDays: 4,
          stockCoverStatus: "low_cover",
          sellThroughRatio: 0.5,
          sellThroughStatus: "good",
        },
      ],
      totalCount: 1,
      pageNumber: 1,
      pageSize: 50,
      meta: { success: true, dataQualityStatus: "good" },
    });
    getInventoryInsightsMock.mockResolvedValue({ meta: { success: true, dataQualityStatus: "good" } });
    getInventoryStoreComparisonMock.mockResolvedValue({ generatedAtUtc: "2026-05-26T12:00:00Z", meta: { success: true, dataQualityStatus: "good" } });
    getInventoryActionSuggestionsMock.mockResolvedValue({ generatedAtUtc: "2026-05-26T12:00:00Z", pendingCount: 0, approvedCount: 0, deferredCount: 0, closedCount: 0, items: [], meta: { success: true, dataQualityStatus: "good" } });
    getForecastMock.mockResolvedValue({ generatedAtUtc: "2026-05-26T12:00:00Z", items: [] });
    getInventoryAlertsMock.mockResolvedValue({ generatedAtUtc: "2026-05-26T12:00:00Z", items: [] });
    getRebalanceSuggestionsMock.mockResolvedValue({ generatedAtUtc: "2026-05-26T12:00:00Z", items: [] });
    getAnalyticsActionSourceStatusesMock.mockResolvedValue({ items: [] });
  });

  it("uses batch source status endpoint for visible inventory rows", async () => {
    render(<InventoryPage />);

    await waitFor(() => {
      expect(getAnalyticsActionSourceStatusesMock).toHaveBeenCalled();
    });

    expect(getAnalyticsActionSourceStatusesMock).toHaveBeenCalledWith(
      expect.objectContaining({
        items: expect.arrayContaining([
          expect.objectContaining({ sourceType: "inventory" }),
        ]),
      }),
    );
  });

  it("keeps queued inventory suggestions visible when source status lookup fails", async () => {
    const refreshedInventoryList = {
      items: [
        {
          id: 501,
          naziv: "Artikal A",
          plu: "PLU-501",
          kolicina: 10,
          minimalnaKolicina: 3,
          nabavnaCena: 100,
          estimatedValue: 1000,
          idObjekat: 1,
          idDobavljac: null,
          stockCoverDays: 4,
          stockCoverStatus: "low_cover",
          sellThroughRatio: 0.5,
          sellThroughStatus: "good",
        },
      ],
      totalCount: 1,
      pageNumber: 1,
      pageSize: 50,
      meta: { success: true, dataQualityStatus: "good" },
    };
    const refreshedActionWorkflow = {
      generatedAtUtc: "2026-05-26T12:00:00Z",
      pendingCount: 0,
      approvedCount: 0,
      deferredCount: 0,
      closedCount: 0,
      items: [],
      meta: { success: true, dataQualityStatus: "good" },
    };

    getAnalyticsActionSourceStatusesMock
      .mockImplementationOnce(({ items }: { items: Array<{ sourceKey: string }> }) => Promise.resolve({
        items: items.map(({ sourceKey }) => ({ sourceKey, exists: true })),
      }))
      .mockRejectedValueOnce(new Error("status lookup failed"));
    getInventoryListMock.mockImplementationOnce(async () => refreshedInventoryList);
    getInventoryActionSuggestionsMock.mockImplementationOnce(async () => refreshedActionWorkflow);

    render(<InventoryPage />);

    await waitFor(() => {
      expect(screen.getByTestId("inventory-items-table").getAttribute("data-queued")).toBe("true");
    });

    fireEvent.change(screen.getByLabelText("Filter po prodavnici"), { target: { value: "1" } });

    await waitFor(() => {
      expect(getAnalyticsActionSourceStatusesMock).toHaveBeenCalledTimes(2);
    });

    await waitFor(() => {
      expect(screen.getByTestId("inventory-items-table").getAttribute("data-queued")).toBe("true");
    });
  });
});

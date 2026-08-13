import { render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { MemoryRouter } from "react-router-dom";
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
vi.mock("../../components/analytics/KpiExplainButton", () => ({ default: () => null }));
vi.mock("../../components/inventory/ActionWorkflowPanel", () => ({ ActionWorkflowPanel: () => null }));
vi.mock("../../components/inventory/DecisionSummaryBar", () => ({ DecisionSummaryBar: () => null }));
vi.mock("../../components/inventory/DemandForecastPanel", () => ({ DemandForecastPanel: () => null }));
vi.mock("../../components/inventory/ExportSchedulerPanel", () => ({ ExportSchedulerPanel: () => null }));
vi.mock("../../components/inventory/InventoryAlertsFeed", () => ({ InventoryAlertsFeed: () => null }));
vi.mock("../../components/inventory/InventoryInsightPanels", () => ({ InventoryInsightPanels: () => null }));
vi.mock("../../components/inventory/InventoryItemsTable", () => ({ InventoryItemsTable: () => null }));
vi.mock("../../components/inventory/InventoryKPICards", () => ({
  InventoryKPICards: () => <div data-testid="inventory-kpi-cards">KPI</div>,
}));
vi.mock("../../components/inventory/InventoryPriorityPanels", () => ({ InventoryPriorityPanels: () => null }));
vi.mock("../../components/inventory/MailSchedulerPanel", () => ({ MailSchedulerPanel: () => null }));
vi.mock("../../components/inventory/RebalancingTable", () => ({ RebalancingTable: () => null }));
vi.mock("../../components/inventory/SKUDetailModal", () => ({ SKUDetailModal: () => null }));
vi.mock("../../components/inventory/SizeCurvePanel", () => ({ SizeCurvePanel: () => null }));
vi.mock("../../components/inventory/StoreComparisonPanel", () => ({ StoreComparisonPanel: () => null }));
vi.mock("../../components/ErrorBoundary", () => ({ ErrorBoundary: ({ children }: { children: ReactNode }) => <>{children}</> }));

describe("InventoryPage partial load failure", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getStoresMock.mockResolvedValue([]);
    getSupplierFiltersMock.mockResolvedValue([]);
    getInventoryReportSchedulesMock.mockResolvedValue([]);
    getAnalyticsActionSourceStatusesMock.mockResolvedValue({ items: [] });
    getInventoryInsightsMock.mockResolvedValue({ meta: { success: true } });
    getInventoryStoreComparisonMock.mockResolvedValue({ generatedAtUtc: "2026-08-13T10:00:00Z", meta: { success: true } });
    getInventoryActionSuggestionsMock.mockResolvedValue({
      generatedAtUtc: "2026-08-13T10:00:00Z",
      pendingCount: 0,
      approvedCount: 0,
      deferredCount: 0,
      closedCount: 0,
      items: [],
      meta: { success: true },
    });
    getForecastMock.mockResolvedValue({ items: [], generatedAtUtc: "2026-08-13T10:00:00Z" });
    getInventoryAlertsMock.mockResolvedValue({ items: [] });
    getRebalanceSuggestionsMock.mockResolvedValue({ items: [] });
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
      meta: { success: true },
    });
  });

  it("blocks KPI render when balance fails even if the list succeeds", async () => {
    getInventoryBalanceMock.mockRejectedValue(new Error("balance down"));

    render(
      <MemoryRouter>
        <InventoryPage />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(screen.getByRole("alert")).toHaveTextContent("Podaci trenutno nisu dostupni");
    });
    expect(screen.queryByTestId("inventory-kpi-cards")).not.toBeInTheDocument();
    await waitFor(() => {
      expect(getInventoryBalanceMock).toHaveBeenCalled();
    });
  });
});

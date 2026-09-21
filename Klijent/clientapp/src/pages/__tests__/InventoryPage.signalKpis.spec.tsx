import { render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter } from "react-router-dom";
import InventoryPage from "../InventoryPage";
import { INVENTORY_SIGNAL_KPI_PAGE_SCOPE_NOTE } from "../../components/inventory/inventorySignalKpis";

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
  getInventoryReportSchedules: (...args: unknown[]) => getInventoryReportSchedulesMock([]),
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
  InventoryItemsTable: () => <div data-testid="inventory-items-table" />,
}));
vi.mock("../../components/inventory/InventoryKPICards", () => ({ InventoryKPICards: () => null }));
vi.mock("../../components/inventory/InventoryPriorityPanels", () => ({ InventoryPriorityPanels: () => null }));
vi.mock("../../components/inventory/MailSchedulerPanel", () => ({ MailSchedulerPanel: () => null }));
vi.mock("../../components/inventory/RebalancingTable", () => ({ RebalancingTable: () => null }));
vi.mock("../../components/inventory/SKUDetailModal", () => ({ SKUDetailModal: () => null }));
vi.mock("../../components/inventory/SizeCurvePanel", () => ({ SizeCurvePanel: () => null }));
vi.mock("../../components/inventory/StoreComparisonPanel", () => ({ StoreComparisonPanel: () => null }));
vi.mock("../../components/ErrorBoundary", () => ({ ErrorBoundary: ({ children }: { children: ReactNode }) => <>{children}</> }));

function inventoryItem(id: number, stockCoverStatus: string) {
  return {
    id,
    naziv: `Artikal ${id}`,
    plu: `PLU-${id}`,
    kolicina: 10,
    minimalnaKolicina: 3,
    nabavnaCena: 100,
    estimatedValue: 1000,
    idObjekat: 1,
    idDobavljac: null,
    stockCoverDays: 4,
    stockCoverStatus,
    sellThroughRatio: 0.5,
    sellThroughStatus: "good",
  };
}

function seedInventoryMocks(listResponse: { items: ReturnType<typeof inventoryItem>[]; totalCount: number }) {
  getStoresMock.mockResolvedValue([{ storeId: 1, storeName: "Prodavnica 1" }]);
  getSupplierFiltersMock.mockResolvedValue([]);
  getInventoryReportSchedulesMock.mockResolvedValue([]);
  getAnalyticsActionSourceStatusesMock.mockResolvedValue({ items: [] });
  getInventoryBalanceMock.mockResolvedValue({
    totalSku: listResponse.totalCount,
    totalOnHand: 10,
    outOfStockCount: 0,
    lowStockCount: 0,
    estimatedInventoryValue: 1000,
    meta: { success: true, dataQualityStatus: "good" },
  });
  getInventoryListMock.mockResolvedValue({
    ...listResponse,
    pageNumber: 1,
    pageSize: 50,
    meta: { success: true, dataQualityStatus: "good" },
  });
  getInventoryInsightsMock.mockResolvedValue({ meta: { success: true, dataQualityStatus: "good" } });
  getInventoryStoreComparisonMock.mockResolvedValue({ generatedAtUtc: "2026-05-26T12:00:00Z", meta: { success: true } });
  getInventoryActionSuggestionsMock.mockResolvedValue({ generatedAtUtc: "2026-05-26T12:00:00Z", pendingCount: 0, approvedCount: 0, deferredCount: 0, closedCount: 0, items: [], meta: { success: true } });
  getForecastMock.mockResolvedValue({ items: [], generatedAtUtc: "2026-05-26T12:00:00Z" });
  getInventoryAlertsMock.mockResolvedValue({ items: [] });
  getRebalanceSuggestionsMock.mockResolvedValue({ items: [] });
}

describe("InventoryPage signal KPI scope", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("shows page-local scope note when filtered inventory spans multiple pages", async () => {
    seedInventoryMocks({
      items: [inventoryItem(501, "low_cover")],
      totalCount: 120,
    });

    render(
      <MemoryRouter>
        <InventoryPage />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(screen.getByTestId("inventory-signal-kpi-scope-note")).toHaveTextContent(INVENTORY_SIGNAL_KPI_PAGE_SCOPE_NOTE);
      expect(screen.getByTestId("inventory-signal-kpi-scope-note")).toHaveTextContent(/1 od 120 artikala/);
    });
  });

  it("hides page-local scope note when the filtered set fits on one page", async () => {
    seedInventoryMocks({
      items: [inventoryItem(501, "low_cover"), inventoryItem(502, "slow_stock")],
      totalCount: 2,
    });

    render(
      <MemoryRouter>
        <InventoryPage />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(screen.getByText("Rizik pokrivenosti zalihe")).toBeInTheDocument();
    });
    expect(screen.queryByText(new RegExp(INVENTORY_SIGNAL_KPI_PAGE_SCOPE_NOTE))).not.toBeInTheDocument();
  });
});

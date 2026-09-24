import { render, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import InventoryPage, { parseInventoryAlertSeverity } from "../InventoryPage";

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
vi.mock("../../components/inventory/InventoryItemsTable", () => ({ InventoryItemsTable: () => null }));
vi.mock("../../components/inventory/InventoryKPICards", () => ({ InventoryKPICards: () => null }));
vi.mock("../../components/inventory/InventoryPriorityPanels", () => ({ InventoryPriorityPanels: () => null }));
vi.mock("../../components/inventory/MailSchedulerPanel", () => ({ MailSchedulerPanel: () => null }));
vi.mock("../../components/inventory/RebalancingTable", () => ({ RebalancingTable: () => null }));
vi.mock("../../components/inventory/SKUDetailModal", () => ({ SKUDetailModal: () => null }));
vi.mock("../../components/inventory/SizeCurvePanel", () => ({ SizeCurvePanel: () => null }));
vi.mock("../../components/inventory/StoreComparisonPanel", () => ({ StoreComparisonPanel: () => null }));
vi.mock("../../components/ErrorBoundary", () => ({ ErrorBoundary: ({ children }: { children: ReactNode }) => <>{children}</> }));

function renderInventoryPage(initialEntry = "/analytics/inventory") {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <Routes>
        <Route path="/analytics/inventory" element={<InventoryPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

function mockInventorySnapshot() {
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
    items: [],
    totalCount: 0,
    pageNumber: 1,
    pageSize: 50,
    meta: { success: true, dataQualityStatus: "good" },
  });
  getInventoryInsightsMock.mockResolvedValue({ meta: { success: true, dataQualityStatus: "good" } });
  getInventoryStoreComparisonMock.mockResolvedValue({ generatedAtUtc: "2026-05-26T12:00:00Z", meta: { success: true, dataQualityStatus: "good" } });
  getInventoryActionSuggestionsMock.mockResolvedValue({ generatedAtUtc: "2026-05-26T12:00:00Z", pendingCount: 0, approvedCount: 0, deferredCount: 0, closedCount: 0, items: [], meta: { success: true, dataQualityStatus: "good" } });
  getForecastMock.mockResolvedValue({ generatedAtUtc: "2026-05-26T12:00:00Z", items: [] });
  getRebalanceSuggestionsMock.mockResolvedValue({ generatedAtUtc: "2026-05-26T12:00:00Z", items: [] });
  getAnalyticsActionSourceStatusesMock.mockResolvedValue({ items: [] });
  getInventoryAlertsMock.mockResolvedValue({ generatedAtUtc: "2026-05-26T12:00:00Z", items: [] });
}

describe("parseInventoryAlertSeverity", () => {
  it("accepts supported severity values", () => {
    expect(parseInventoryAlertSeverity("critical")).toBe("critical");
    expect(parseInventoryAlertSeverity("warning")).toBe("warning");
    expect(parseInventoryAlertSeverity("info")).toBe("info");
  });

  it("falls back to all alerts for invalid or missing values", () => {
    expect(parseInventoryAlertSeverity(null)).toBe("");
    expect(parseInventoryAlertSeverity("")).toBe("");
    expect(parseInventoryAlertSeverity("urgent")).toBe("");
  });
});

describe("InventoryPage alert severity filter", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockInventorySnapshot();
  });

  it("requests all severities by default", async () => {
    renderInventoryPage();

    await waitFor(() => {
      expect(getInventoryAlertsMock).toHaveBeenCalled();
    });

    expect(getInventoryAlertsMock).toHaveBeenLastCalledWith(
      expect.objectContaining({ severity: undefined }),
    );
  });

  it("requests backend severity filtering from the URL", async () => {
    renderInventoryPage("/analytics/inventory?alertSeverity=critical");

    await waitFor(() => {
      expect(getInventoryAlertsMock).toHaveBeenLastCalledWith(
        expect.objectContaining({ severity: "critical" }),
      );
    });
  });

  it("refetches alerts when the URL severity changes", async () => {
    renderInventoryPage("/analytics/inventory?alertSeverity=warning");

    await waitFor(() => {
      expect(getInventoryAlertsMock).toHaveBeenLastCalledWith(
        expect.objectContaining({ severity: "warning" }),
      );
    });

    const baseline = getInventoryAlertsMock.mock.calls.length;
    renderInventoryPage("/analytics/inventory?alertSeverity=critical");

    await waitFor(() => {
      expect(getInventoryAlertsMock.mock.calls.length).toBeGreaterThan(baseline);
      expect(getInventoryAlertsMock).toHaveBeenLastCalledWith(
        expect.objectContaining({ severity: "critical" }),
      );
    });
  });
});

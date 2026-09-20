import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter } from "react-router-dom";
import InventoryPage from "../InventoryPage";
import { setDataScope } from "../../utils/dataScope";

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

function seedInventoryMocks() {
  getStoresMock.mockResolvedValue([{ storeId: 1, storeName: "Prodavnica 1" }]);
  getSupplierFiltersMock.mockResolvedValue([]);
  getInventoryReportSchedulesMock.mockResolvedValue([]);
  getAnalyticsActionSourceStatusesMock.mockResolvedValue({ items: [] });
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
  getInventoryActionSuggestionsMock.mockResolvedValue({
    generatedAtUtc: "2026-05-26T12:00:00Z",
    pendingCount: 0,
    approvedCount: 0,
    deferredCount: 0,
    closedCount: 0,
    items: [],
    meta: { success: true, dataQualityStatus: "good" },
  });
  getForecastMock.mockResolvedValue({ items: [], generatedAtUtc: "2026-05-26T12:00:00Z" });
  getInventoryAlertsMock.mockResolvedValue({ items: [] });
  getRebalanceSuggestionsMock.mockResolvedValue({ items: [] });
}

describe("InventoryPage signal window refresh", () => {
  let currentTime = new Date("2026-09-18T12:00:00.000Z");
  const RealDate = Date;

  beforeEach(() => {
    vi.clearAllMocks();
    setDataScope("all");
    seedInventoryMocks();
    currentTime = new Date("2026-09-18T12:00:00.000Z");

    class MockDate extends RealDate {
      constructor(...args: [] | [string | number | Date]) {
        if (args.length === 0) {
          super(currentTime.getTime());
          return;
        }
        super(...args);
      }

      static now() {
        return currentTime.getTime();
      }
    }

    vi.stubGlobal("Date", MockDate as DateConstructor);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("recomputes the 30-day signal window when reload generation advances", async () => {
    render(
      <MemoryRouter>
        <InventoryPage />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(getInventoryListMock).toHaveBeenCalled();
    });

    const initialCall = getInventoryListMock.mock.calls[0]?.[0] as { fromDate: string; toDate: string };
    expect(initialCall.toDate).toBe("2026-09-18T12:00:00.000Z");
    expect(initialCall.fromDate).toBe("2026-08-19T12:00:00.000Z");

    currentTime = new Date("2026-09-19T12:00:00.000Z");
    act(() => {
      window.dispatchEvent(new Event("trendplus:data-scope-changed"));
    });

    await waitFor(() => {
      const lastCall = getInventoryListMock.mock.calls.at(-1)?.[0] as { fromDate: string; toDate: string };
      expect(lastCall.toDate).toBe("2026-09-19T12:00:00.000Z");
      expect(lastCall.fromDate).toBe("2026-08-20T12:00:00.000Z");
    });
  });

  it("refreshes inventory in place without a full page reload", async () => {
    render(
      <MemoryRouter>
        <InventoryPage />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(getInventoryListMock).toHaveBeenCalledTimes(1);
    });

    fireEvent.click(screen.getByRole("button", { name: "Osveži" }));

    await waitFor(() => {
      expect(getInventoryListMock).toHaveBeenCalledTimes(2);
    });
  });

  it("recomputes the signal window when data scope changes", async () => {
    currentTime = new Date("2026-09-18T08:00:00.000Z");

    render(
      <MemoryRouter>
        <InventoryPage />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(getInventoryListMock).toHaveBeenCalled();
    });

    currentTime = new Date("2026-09-20T08:00:00.000Z");
    setDataScope("existing");
    act(() => {
      window.dispatchEvent(new Event("trendplus:data-scope-changed"));
    });

    await waitFor(() => {
      const lastCall = getInventoryListMock.mock.calls.at(-1)?.[0] as { fromDate: string; toDate: string; dataScope: string };
      expect(lastCall.dataScope).toBe("existing");
      expect(lastCall.toDate).toBe("2026-09-20T08:00:00.000Z");
      expect(lastCall.fromDate).toBe("2026-08-21T08:00:00.000Z");
    });
  });
});

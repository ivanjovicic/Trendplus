import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
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
const getInventoryItemDetailMock = vi.fn();

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
  getInventoryItemDetail: (...args: unknown[]) => getInventoryItemDetailMock(...args),
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
vi.mock("../../components/inventory/InventoryInsightPanels", () => ({ InventoryInsightPanels: () => null }));
vi.mock("../../components/inventory/InventoryItemsTable", () => ({
  InventoryItemsTable: () => <div data-testid="inventory-items-table" />,
}));
vi.mock("../../components/inventory/InventoryKPICards", () => ({ InventoryKPICards: () => null }));
vi.mock("../../components/inventory/InventoryPriorityPanels", () => ({ InventoryPriorityPanels: () => null }));
vi.mock("../../components/inventory/MailSchedulerPanel", () => ({ MailSchedulerPanel: () => null }));
vi.mock("../../components/inventory/RebalancingTable", () => ({ RebalancingTable: () => null }));
vi.mock("../../components/inventory/SizeCurvePanel", () => ({ SizeCurvePanel: () => null }));
vi.mock("../../components/inventory/StoreComparisonPanel", () => ({ StoreComparisonPanel: () => null }));
vi.mock("../../components/ErrorBoundary", () => ({ ErrorBoundary: ({ children }: { children: ReactNode }) => <>{children}</> }));

const offPageAlert = {
  alertType: "low_stock",
  skuId: 9999,
  storeId: 1,
  title: "Off-page alert artikal",
  message: "Artikal nije na trenutnoj stranici.",
  severity: "critical" as const,
  actionability: {
    recommendationAllowed: false,
    dataQualityStatus: "insufficient_data",
  },
};

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
    items: [{
      id: 501,
      naziv: "Artikal na stranici",
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
    }],
    totalCount: 1,
    pageNumber: 1,
    pageSize: 50,
    meta: { success: true, dataQualityStatus: "good" },
  });
  getInventoryInsightsMock.mockResolvedValue({
    totalItems: 1,
    totalEstimatedValue: 1000,
    aging: [],
    abc: [],
    topAgedItems: [],
    topCapitalLockedItems: [],
    meta: { success: true, dataQualityStatus: "good" },
  });
  getInventoryStoreComparisonMock.mockResolvedValue({ generatedAtUtc: "2026-05-26T12:00:00Z", meta: { success: true } });
  getInventoryActionSuggestionsMock.mockResolvedValue({ generatedAtUtc: "2026-05-26T12:00:00Z", pendingCount: 0, approvedCount: 0, deferredCount: 0, closedCount: 0, items: [], meta: { success: true } });
  getForecastMock.mockResolvedValue({ items: [], generatedAtUtc: "2026-05-26T12:00:00Z" });
  getRebalanceSuggestionsMock.mockResolvedValue({ items: [] });
  getInventoryAlertsMock.mockResolvedValue({
    generatedAtUtc: "2026-05-26T12:00:00Z",
    totalCount: 1,
    returnedCount: 1,
    totalMatchingCount: 1,
    isTruncated: false,
    snapshotAvailable: true,
    items: [offPageAlert],
  });
}

describe("InventoryPage off-page SKU detail", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    seedInventoryMocks();
  });

  it("opens alert detail without fake zero quantity or value while context loads", async () => {
    getInventoryItemDetailMock.mockImplementation(
      () => new Promise(() => {
        /* keep loading */
      }),
    );

    render(
      <MemoryRouter>
        <InventoryPage />
      </MemoryRouter>,
    );

    const alertButton = await screen.findByRole("button", { name: /Otvori detalj artikla za alert Off-page alert artikal/i });
    fireEvent.click(alertButton);

    await waitFor(() => {
      expect(screen.getByText("Učitavam kontekst artikla...")).toBeInTheDocument();
    });

    expect(screen.getAllByText("Nije dostupno").length).toBeGreaterThan(0);
    expect(screen.queryByText("Bez zaliha")).not.toBeInTheDocument();
    expect(screen.queryByText(/0\s*RSD/)).not.toBeInTheDocument();
  });

  it("shows fetched detail values after off-page alert navigation succeeds", async () => {
    getInventoryItemDetailMock.mockResolvedValue({
      id: 9999,
      naziv: "Off-page alert artikal",
      kolicina: 12,
      minimalnaKolicina: 4,
      nabavnaCena: 250,
      estimatedValue: 3000,
      storeName: "Prodavnica 1",
      supplierName: "Dobavljac A",
      history: [],
    });

    render(
      <MemoryRouter>
        <InventoryPage />
      </MemoryRouter>,
    );

    const alertButton = await screen.findByRole("button", { name: /Otvori detalj artikla za alert Off-page alert artikal/i });
    fireEvent.click(alertButton);

    await waitFor(() => {
      expect(getInventoryItemDetailMock).toHaveBeenCalledWith(9999, expect.any(Object));
    });

    await waitFor(() => {
      expect(screen.getByText("12")).toBeInTheDocument();
      expect(screen.getByText(/3\.000\s*RSD/)).toBeInTheDocument();
    });
  });

  it("marks missing context after off-page detail fetch fails", async () => {
    getInventoryItemDetailMock.mockRejectedValue(new Error("Artikal nije pronađen u detaljnom kontekstu."));

    render(
      <MemoryRouter>
        <InventoryPage />
      </MemoryRouter>,
    );

    const alertButton = await screen.findByRole("button", { name: /Otvori detalj artikla za alert Off-page alert artikal/i });
    fireEvent.click(alertButton);

    await waitFor(() => {
      expect(screen.getByText("Kontekst artikla nije pronađen.")).toBeInTheDocument();
      expect(screen.getByText("Artikal nije pronađen u detaljnom kontekstu.")).toBeInTheDocument();
    });

    expect(screen.queryByText(/0\s*RSD/)).not.toBeInTheDocument();
    expect(screen.queryByText("Bez zaliha")).not.toBeInTheDocument();
  });

  it("aborts the detail request when the active data scope changes", async () => {
    getInventoryItemDetailMock.mockImplementation(
      () => new Promise(() => {
        /* keep loading until the scope changes */
      }),
    );

    render(
      <MemoryRouter>
        <InventoryPage />
      </MemoryRouter>,
    );

    const alertButton = await screen.findByRole("button", { name: /Otvori detalj artikla za alert Off-page alert artikal/i });
    fireEvent.click(alertButton);

    await waitFor(() => {
      expect(getInventoryItemDetailMock).toHaveBeenCalled();
    });
    const detailSignal = getInventoryItemDetailMock.mock.calls.at(-1)?.[1]?.signal as AbortSignal;
    expect(detailSignal).toBeInstanceOf(AbortSignal);

    setDataScope("existing");
    fireEvent(window, new Event("trendplus:data-scope-changed"));

    await waitFor(() => {
      expect(detailSignal.aborted).toBe(true);
    });
  });
});

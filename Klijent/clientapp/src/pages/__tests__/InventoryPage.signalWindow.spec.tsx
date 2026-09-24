import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter, useLocation } from "react-router-dom";
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
  InventoryItemsTable: ({
    pageNumber,
    onNextPage,
  }: {
    pageNumber: number;
    onNextPage: () => void;
  }) => (
    <div data-testid="inventory-items-table">
      <span>{`page=${pageNumber}`}</span>
      <button type="button" onClick={onNextPage}>Sledeća strana</button>
    </div>
  ),
}));
vi.mock("../../components/inventory/InventoryKPICards", () => ({ InventoryKPICards: () => null }));
vi.mock("../../components/inventory/InventoryPriorityPanels", () => ({ InventoryPriorityPanels: () => null }));
vi.mock("../../components/inventory/MailSchedulerPanel", () => ({ MailSchedulerPanel: () => null }));
vi.mock("../../components/inventory/RebalancingTable", () => ({ RebalancingTable: () => null }));
vi.mock("../../components/inventory/SKUDetailModal", () => ({ SKUDetailModal: () => null }));
vi.mock("../../components/inventory/SizeCurvePanel", () => ({ SizeCurvePanel: () => null }));
vi.mock("../../components/inventory/StoreComparisonPanel", () => ({
  StoreComparisonPanel: ({
    compareStoreIds,
    onToggleStore,
  }: {
    compareStoreIds: number[];
    onToggleStore: (storeId: number) => void;
  }) => (
    <div data-testid="store-comparison-panel">
      <span>{compareStoreIds.join(",")}</span>
      <button type="button" onClick={() => onToggleStore(1)}>Toggle store 1</button>
    </div>
  ),
}));
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

function LocationProbe() {
  const location = useLocation();
  return <output data-testid="location-search">{location.search}</output>;
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
    expect(initialCall.toDate).toBe("2026-09-18");
    expect(initialCall.fromDate).toBe("2026-08-20");

    currentTime = new Date("2026-09-19T12:00:00.000Z");
    act(() => {
      window.dispatchEvent(new Event("trendplus:data-scope-changed"));
    });

    await waitFor(() => {
      const lastCall = getInventoryListMock.mock.calls.at(-1)?.[0] as { fromDate: string; toDate: string };
      expect(lastCall.toDate).toBe("2026-09-19");
      expect(lastCall.fromDate).toBe("2026-08-21");
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
    const initialCallCount = getInventoryListMock.mock.calls.length;

    fireEvent.click(screen.getByRole("button", { name: "Osveži" }));

    await waitFor(() => {
      expect(getInventoryListMock.mock.calls.length).toBeGreaterThan(initialCallCount);
    });
  });

  it("passes one lifecycle signal to every secondary request and aborts it on scope refresh", async () => {
    render(
      <MemoryRouter>
        <InventoryPage />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(getInventoryListMock).toHaveBeenCalled();
      expect(getInventoryInsightsMock).toHaveBeenCalled();
      expect(getInventoryStoreComparisonMock).toHaveBeenCalled();
      expect(getInventoryActionSuggestionsMock).toHaveBeenCalled();
      expect(getForecastMock).toHaveBeenCalled();
      expect(getInventoryAlertsMock).toHaveBeenCalled();
      expect(getRebalanceSuggestionsMock).toHaveBeenCalled();
    });

    const lifecycleSignal = getInventoryListMock.mock.calls.at(-1)?.[0]?.signal as AbortSignal;
    expect(lifecycleSignal).toBeInstanceOf(AbortSignal);
    expect(getInventoryBalanceMock.mock.calls.at(-1)?.[4]).toBe(lifecycleSignal);
    expect(getInventoryInsightsMock.mock.calls.at(-1)?.[0]?.signal).toBe(lifecycleSignal);
    expect(getInventoryStoreComparisonMock.mock.calls.at(-1)?.[0]?.signal).toBe(lifecycleSignal);
    expect(getInventoryActionSuggestionsMock.mock.calls.at(-1)?.[0]?.signal).toBe(lifecycleSignal);
    expect(getForecastMock.mock.calls.at(-1)?.[0]?.signal).toBe(lifecycleSignal);
    expect(getInventoryAlertsMock.mock.calls.at(-1)?.[0]?.signal).toBe(lifecycleSignal);
    expect(getRebalanceSuggestionsMock.mock.calls.at(-1)?.[0]?.signal).toBe(lifecycleSignal);

    setDataScope("existing");
    act(() => {
      window.dispatchEvent(new Event("trendplus:data-scope-changed"));
    });

    await waitFor(() => {
      const nextSignal = getInventoryListMock.mock.calls.at(-1)?.[0]?.signal as AbortSignal;
      expect(nextSignal).not.toBe(lifecycleSignal);
      expect(lifecycleSignal.aborted).toBe(true);
    });
  });

  it("restores Inventory pagination, search, page size, and compare stores from URL", async () => {
    render(
      <MemoryRouter initialEntries={["/analytics/inventory?page=3&pageSize=100&search=patika&compareStores=1,2"]}>
        <LocationProbe />
        <InventoryPage />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(getInventoryListMock).toHaveBeenCalledWith(expect.objectContaining({
        pageNumber: 3,
        pageSize: 100,
        search: "patika",
      }));
      expect(getInventoryStoreComparisonMock).toHaveBeenCalledWith(expect.objectContaining({
        compareStoreIds: [1, 2],
      }));
    });

    expect(screen.getByRole("searchbox", { name: "Pretraga artikala" })).toHaveValue("patika");
    expect(screen.getByTestId("inventory-items-table")).toHaveTextContent("page=3");
    expect(screen.getByLabelText("Veličina strane tabele artikala")).toHaveValue("100");
    expect(screen.getByTestId("store-comparison-panel")).toHaveTextContent("1,2");
    expect(screen.getByTestId("location-search")).toHaveTextContent("page=3");
  });

  it("restores a custom period from URL and sends it to supported signal requests", async () => {
    render(
      <MemoryRouter initialEntries={["/analytics/inventory?periodPreset=custom&fromDate=2026-01-05&toDate=2026-02-15"]}>
        <LocationProbe />
        <InventoryPage />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(getInventoryListMock).toHaveBeenCalledWith(expect.objectContaining({
        fromDate: "2026-01-05",
        toDate: "2026-02-15",
      }));
    });

    expect(screen.getByLabelText("Period signala zaliha")).toHaveValue("custom");
    expect(screen.getByLabelText("Početak perioda signala")).toHaveValue("2026-01-05");
    expect(screen.getByLabelText("Kraj perioda signala")).toHaveValue("2026-02-15");
    expect(screen.getByTestId("inventory-period-lineage")).toHaveTextContent("2026-01-05 → 2026-02-15");
    expect(screen.getByTestId("location-search")).toHaveTextContent("periodPreset=custom");
  });

  it("fails invalid period URL values closed to the deterministic 30-day preset", async () => {
    render(
      <MemoryRouter initialEntries={["/analytics/inventory?periodPreset=custom&fromDate=not-a-date&toDate=2026-01-01"]}>
        <InventoryPage />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(getInventoryListMock).toHaveBeenCalledWith(expect.objectContaining({
        fromDate: "2026-08-20",
        toDate: "2026-09-18",
      }));
    });

    expect(screen.getByLabelText("Period signala zaliha")).toHaveValue("30d");
    expect(screen.getByTestId("inventory-period-lineage")).toHaveTextContent("2026-08-20 → 2026-09-18");
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
      expect(lastCall.toDate).toBe("2026-09-20");
      expect(lastCall.fromDate).toBe("2026-08-22");
    });
  });
});

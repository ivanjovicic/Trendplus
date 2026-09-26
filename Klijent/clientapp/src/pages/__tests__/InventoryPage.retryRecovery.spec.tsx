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
vi.mock("../../components/analytics/AnalyticsEmptyState", () => ({
  default: ({
    onRetry,
    actions,
    variant,
  }: {
    onRetry?: () => void;
    actions?: Array<{ label: string; onClick?: () => void }>;
    variant?: string;
  }) => (
    <div data-testid="analytics-empty-state" data-variant={variant ?? ""}>
      <button type="button" data-testid="analytics-empty-retry" onClick={() => onRetry?.()}>
        Ponovi učitavanje
      </button>
      {(actions ?? []).map((action) => (
        <button key={action.label} type="button" onClick={() => action.onClick?.()}>
          {action.label}
        </button>
      ))}
    </div>
  ),
}));
vi.mock("../../components/analytics/AnalyticsErrorState", () => ({
  default: ({ onRetry }: { onRetry?: () => void }) => (
    <button type="button" data-testid="analytics-error-retry" onClick={() => onRetry?.()}>
      Pokušaj ponovo
    </button>
  ),
}));
vi.mock("../../components/analytics/KpiExplainButton", () => ({ default: () => null }));
vi.mock("../../components/inventory/ActionWorkflowPanel", () => ({ ActionWorkflowPanel: () => null }));
vi.mock("../../components/inventory/DecisionSummaryBar", () => ({ DecisionSummaryBar: () => null }));
vi.mock("../../components/inventory/DemandForecastPanel", () => ({ DemandForecastPanel: () => null }));
vi.mock("../../components/inventory/ExportSchedulerPanel", () => ({ ExportSchedulerPanel: () => null }));
vi.mock("../../components/inventory/InventoryAlertsFeed", () => ({ InventoryAlertsFeed: () => null }));
vi.mock("../../components/inventory/InventoryInsightPanels", () => ({ InventoryInsightPanels: () => null }));
vi.mock("../../components/inventory/InventoryItemsTable", () => ({
  InventoryItemsTable: ({ rows }: { rows: Array<Record<string, unknown>> }) => (
    <div data-testid="inventory-items-table" data-row-count={String(rows.length)} />
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

const inventoryRow = {
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
};

const filledInventoryList = {
  items: [inventoryRow],
  totalCount: 1,
  pageNumber: 1,
  pageSize: 50,
  meta: { success: true, dataQualityStatus: "good" },
};

const emptyInventoryList = {
  items: [],
  totalCount: 0,
  pageNumber: 1,
  pageSize: 50,
  meta: { success: true, dataQualityStatus: "good" },
};

function renderPage(initialEntry = "/analytics/inventory") {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <InventoryPage />
    </MemoryRouter>,
  );
}

function expectControlsVisible() {
  expect(screen.getByTestId("analytics-control-bar")).toBeInTheDocument();
  expect(screen.getByRole("searchbox", { name: "Pretraga artikala" })).toBeInTheDocument();
  expect(screen.getByLabelText("Filter po prodavnici")).toBeInTheDocument();
  expect(screen.getByLabelText("Filter po dobavljaču")).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Osveži" })).toBeInTheDocument();
}

async function settle() {
  // Let any trailing effects flush before counting requests.
  await new Promise((resolve) => setTimeout(resolve, 50));
}

describe("InventoryPage retry recovery and persistent controls (RQ427)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    setDataScope("all");

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
    getInventoryListMock.mockResolvedValue(filledInventoryList);
    getInventoryInsightsMock.mockResolvedValue({ meta: { success: true, dataQualityStatus: "good" } });
    getInventoryStoreComparisonMock.mockResolvedValue({ generatedAtUtc: "2026-05-26T12:00:00Z", meta: { success: true, dataQualityStatus: "good" } });
    getInventoryActionSuggestionsMock.mockResolvedValue({ generatedAtUtc: "2026-05-26T12:00:00Z", pendingCount: 0, approvedCount: 0, deferredCount: 0, closedCount: 0, items: [], meta: { success: true, dataQualityStatus: "good" } });
    getForecastMock.mockResolvedValue({ generatedAtUtc: "2026-05-26T12:00:00Z", items: [] });
    getInventoryAlertsMock.mockResolvedValue({ generatedAtUtc: "2026-05-26T12:00:00Z", items: [] });
    getRebalanceSuggestionsMock.mockResolvedValue({ generatedAtUtc: "2026-05-26T12:00:00Z", items: [] });
    getAnalyticsActionSourceStatusesMock.mockResolvedValue({ items: [] });
  });

  it("keeps controls visible on error and retry sends exactly one new list request that recovers the table", async () => {
    getInventoryListMock.mockRejectedValue(new Error("Lista zaliha nije dostupna."));

    renderPage();

    await screen.findByTestId("analytics-error-retry");
    await screen.findByRole("option", { name: "Prodavnica 1" });
    await settle();

    expectControlsVisible();
    expect(screen.queryByTestId("inventory-items-table")).not.toBeInTheDocument();

    getInventoryListMock.mockResolvedValue(filledInventoryList);
    const callsBeforeRetry = getInventoryListMock.mock.calls.length;

    fireEvent.click(screen.getByTestId("analytics-error-retry"));

    await waitFor(() => {
      expect(screen.getByTestId("inventory-items-table")).toHaveAttribute("data-row-count", "1");
    });
    await settle();

    expect(getInventoryListMock.mock.calls.length - callsBeforeRetry).toBe(1);
    expect(screen.queryByTestId("analytics-error-retry")).not.toBeInTheDocument();
    expectControlsVisible();
  });

  it("keeps the same search input mounted from the error state through reload and recovery", async () => {
    getInventoryListMock.mockRejectedValue(new Error("Lista zaliha nije dostupna."));

    renderPage();

    await screen.findByTestId("analytics-error-retry");
    await screen.findByRole("option", { name: "Prodavnica 1" });
    await settle();
    const searchbox = screen.getByRole("searchbox", { name: "Pretraga artikala" });

    getInventoryListMock.mockResolvedValue(filledInventoryList);
    fireEvent.change(searchbox, { target: { value: "Artikal" } });

    await waitFor(() => {
      expect(getInventoryListMock).toHaveBeenLastCalledWith(expect.objectContaining({ search: "Artikal" }));
      expect(screen.getByTestId("inventory-items-table")).toBeInTheDocument();
    });
    // Same DOM node: the control bar was not remounted across error -> reload -> data.
    expect(screen.getByRole("searchbox", { name: "Pretraga artikala" })).toBe(searchbox);
    expect(searchbox).toHaveValue("Artikal");
  });

  it("keeps controls visible in the filtered empty state and retry sends exactly one new list request", async () => {
    getInventoryListMock.mockResolvedValue(emptyInventoryList);

    renderPage("/analytics/inventory?search=nema");

    const emptyState = await screen.findByTestId("analytics-empty-state");
    await screen.findByRole("option", { name: "Prodavnica 1" });
    await settle();

    expect(emptyState).toHaveAttribute("data-variant", "filtered_out");
    expectControlsVisible();
    expect(screen.getByRole("searchbox", { name: "Pretraga artikala" })).toHaveValue("nema");
    expect(screen.getByRole("button", { name: "Poništi filtere" })).toBeInTheDocument();

    getInventoryListMock.mockResolvedValue(filledInventoryList);
    const callsBeforeRetry = getInventoryListMock.mock.calls.length;

    fireEvent.click(screen.getByTestId("analytics-empty-retry"));

    await waitFor(() => {
      expect(screen.getByTestId("inventory-items-table")).toHaveAttribute("data-row-count", "1");
    });
    await settle();

    expect(getInventoryListMock.mock.calls.length - callsBeforeRetry).toBe(1);
    expect(getInventoryListMock).toHaveBeenLastCalledWith(expect.objectContaining({ search: "nema" }));
  });

  it("clears the search from the empty state and reloads unfiltered data", async () => {
    getInventoryListMock.mockImplementation(async ({ search }: { search?: string }) => (
      search ? emptyInventoryList : filledInventoryList
    ));

    renderPage("/analytics/inventory?search=nema");

    await screen.findByTestId("analytics-empty-state");
    fireEvent.click(screen.getByRole("button", { name: "Poništi filtere" }));

    await waitFor(() => {
      expect(screen.getByTestId("inventory-items-table")).toHaveAttribute("data-row-count", "1");
    });

    expect(getInventoryListMock).toHaveBeenLastCalledWith(expect.objectContaining({ search: undefined, storeId: null, supplierId: null, pageNumber: 1 }));
    expect(screen.getByRole("searchbox", { name: "Pretraga artikala" })).toHaveValue("");
    expect(screen.queryByTestId("analytics-empty-state")).not.toBeInTheDocument();
  });

  it("does not offer a filter reset when the empty result has no active filters", async () => {
    getInventoryListMock.mockResolvedValue(emptyInventoryList);

    renderPage();

    const emptyState = await screen.findByTestId("analytics-empty-state");

    expect(emptyState).toHaveAttribute("data-variant", "no_data");
    expect(screen.queryByRole("button", { name: "Poništi filtere" })).not.toBeInTheDocument();
    expectControlsVisible();
  });

  it("control bar refresh re-runs the primary request once without dropping the loaded table", async () => {
    renderPage();

    await waitFor(() => {
      expect(screen.getByTestId("inventory-items-table")).toHaveAttribute("data-row-count", "1");
    });
    await screen.findByRole("option", { name: "Prodavnica 1" });
    await settle();

    const callsBeforeRefresh = getInventoryListMock.mock.calls.length;
    fireEvent.click(screen.getByRole("button", { name: "Osveži" }));

    await waitFor(() => {
      expect(getInventoryListMock.mock.calls.length).toBe(callsBeforeRefresh + 1);
    });
    await settle();

    expect(getInventoryListMock.mock.calls.length - callsBeforeRefresh).toBe(1);
    expect(screen.getByTestId("inventory-items-table")).toBeInTheDocument();
  });
});

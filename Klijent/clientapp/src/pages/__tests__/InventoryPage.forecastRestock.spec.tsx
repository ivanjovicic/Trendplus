import type { ReactNode } from "react";
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import InventoryPage from "../InventoryPage";
import { buildForecastRestockSuggestion, buildInventoryRow } from "../../components/inventory/inventoryUtils";

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
const forecastPanelClickMock = vi.fn();

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

vi.mock("../../components/analytics/AnalyticsTrustHeader", () => ({
  default: ({ dataQualityStatus, isPartial }: { dataQualityStatus?: string | null; isPartial?: boolean }) => (
    <div data-testid="trust-header" data-quality={dataQualityStatus ?? ""} data-partial={String(Boolean(isPartial))} />
  ),
}));
vi.mock("../../components/analytics/AnalyticsEmptyState", () => ({ default: () => null }));
vi.mock("../../components/analytics/AnalyticsErrorState", () => ({ default: () => null }));
vi.mock("../../components/analytics/KpiExplainButton", () => ({ default: () => null }));
vi.mock("../../components/inventory/ActionWorkflowPanel", () => ({
  ActionWorkflowPanel: ({ actionWorkflow }: { actionWorkflow: { items?: Array<{ estimatedValue?: number | null; costMissing?: boolean | null }> } | null }) => {
    const item = actionWorkflow?.items?.[0] ?? null;
    const valueText = item?.costMissing
      ? "Nije dostupno (nedostaje nabavna cena)"
      : item?.estimatedValue == null
        ? "Nije dostupno"
        : String(item.estimatedValue);
    return (
      <div
        data-testid="workflow-count"
        data-first-estimated-value={item?.estimatedValue == null ? "" : String(item.estimatedValue)}
        data-first-cost-missing={String(Boolean(item?.costMissing))}
      >
        {actionWorkflow?.items?.length ?? 0}
        {item ? ` | Vrednost: ${valueText}` : ""}
      </div>
    );
  },
}));
vi.mock("../../components/inventory/DecisionSummaryBar", () => ({ DecisionSummaryBar: () => null }));
vi.mock("../../components/inventory/DemandForecastPanel", () => ({
  DemandForecastPanel: ({ forecast, onSuggestRestock }: { forecast: { items: Array<{ skuId: number; storeId: number; sizeCode: string }> } | null; onSuggestRestock: (item: { skuId: number; storeId: number; sizeCode: string }) => void }) => (
    <div>
      <button type="button" onClick={() => { forecastPanelClickMock(); if (forecast?.items[0]) onSuggestRestock(forecast.items[0]); }}>
        Predlozi prvu prognozu
      </button>
    </div>
  ),
}));
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

describe("InventoryPage forecast restock trust states", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    forecastPanelClickMock.mockClear();

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
          id: 999,
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
    getForecastMock.mockResolvedValue({
      generatedAtUtc: "2026-05-26T12:00:00Z",
      totalCount: 1,
      snapshotAvailable: true,
      items: [
        {
          skuId: 999,
          storeId: 1,
          sizeCode: "42",
          forecast7d: 2.5,
          forecast14d: 4.5,
          forecast28d: 8.5,
          probabilityOfOOSIn7d: 0.82,
          overstockRisk: 0.1,
          confidenceScore: 0.76,
          explanation: "Visok OOS signal.",
        },
      ],
    });
    getInventoryAlertsMock.mockResolvedValue({ generatedAtUtc: "2026-05-26T12:00:00Z", items: [] });
    getRebalanceSuggestionsMock.mockResolvedValue({ generatedAtUtc: "2026-05-26T12:00:00Z", items: [] });
    getAnalyticsActionSourceStatusesMock.mockResolvedValue({ items: [] });
  });

  it("does not create a forecast restock action without a loaded stock baseline", async () => {
    render(<MemoryRouter><InventoryPage /></MemoryRouter>);

    const button = await screen.findByRole("button", { name: /Predlozi prvu prognozu/i });
    await waitFor(() => {
      expect(getInventoryListMock).toHaveBeenCalled();
      expect(getForecastMock).toHaveBeenCalled();
    });
    await act(async () => {
      fireEvent.click(button);
    });

    expect(forecastPanelClickMock).toHaveBeenCalledTimes(1);
    expect(screen.getByTestId("workflow-count")).toHaveTextContent("0");
  });

  it("shows that signal panels do not follow text search", async () => {
    render(<MemoryRouter><InventoryPage /></MemoryRouter>);

    const searchbox = await screen.findByRole("searchbox", { name: /Pretraga artikala/i });
    fireEvent.change(searchbox, { target: { value: "Patike" } });

    await waitFor(() => {
      expect(screen.getByTestId("signal-lineage-note")).toHaveTextContent("tekst pretraga ne utiče na prognozu, upozorenja i redistribuciju");
    });
  });

  it("marks forecast workflow value as missing when unit cost is zero", () => {
    const row = buildInventoryRow({
      id: 999,
      naziv: "Artikal A",
      plu: "PLU-999",
      kolicina: 10,
      minimalnaKolicina: 3,
      nabavnaCena: 0,
      estimatedValue: 1000,
      idObjekat: 1,
      idDobavljac: null,
      stockCoverDays: 4,
      stockCoverStatus: "low_cover",
      sellThroughRatio: 0.5,
      sellThroughStatus: "good",
    }, [{ storeId: 1, storeName: "Prodavnica 1" }], []);

    const suggestion = buildForecastRestockSuggestion(row, {
      skuId: 999,
      storeId: 1,
      sizeCode: "42",
      forecast7d: 2.5,
      probabilityOfOOSIn7d: 0.82,
    }, [{ storeId: 1, storeName: "Prodavnica 1" }], 4);

    expect(suggestion.costMissing).toBe(true);
    expect(suggestion.estimatedValue).toBeNull();
    expect(suggestion.suggestedQty).toBe(3);
    expect(suggestion.forecastDemandQty).toBe(3);
  });

  it("does not create a forecast restock action when forecast evidence is missing", async () => {
    getForecastMock.mockResolvedValue({
      generatedAtUtc: "2026-05-26T12:00:00Z",
      totalCount: 1,
      snapshotAvailable: true,
      items: [
        {
          skuId: 999,
          storeId: 1,
          sizeCode: "42",
          forecast7d: null,
          forecast14d: null,
          forecast28d: null,
          probabilityOfOOSIn7d: 0.82,
          overstockRisk: 0.1,
          confidenceScore: null,
          explanation: "Nepotpuna evidencija.",
        },
      ],
    });

    render(<MemoryRouter><InventoryPage /></MemoryRouter>);

    const button = await screen.findByRole("button", { name: /Predlozi prvu prognozu/i });
    await act(async () => {
      fireEvent.click(button);
    });

    expect(forecastPanelClickMock).toHaveBeenCalledTimes(1);
    expect(screen.getByTestId("workflow-count")).toHaveTextContent("0");
  });

  it("keeps stale inventory meta visible instead of looking green", async () => {
    getInventoryBalanceMock.mockResolvedValue({
      totalSku: 1,
      totalOnHand: 10,
      outOfStockCount: 0,
      lowStockCount: 0,
      estimatedInventoryValue: 1000,
      meta: { success: true, dataQualityStatus: "stale", warningCode: "STALE_CACHE", warningMessage: "Podaci su zastareli." },
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
      meta: { success: true, dataQualityStatus: "stale", warningCode: "STALE_CACHE", warningMessage: "Podaci su zastareli." },
    });
    getInventoryInsightsMock.mockResolvedValue({ meta: { success: true, dataQualityStatus: "stale", warningCode: "STALE_CACHE", warningMessage: "Podaci su zastareli." } });
    getInventoryStoreComparisonMock.mockResolvedValue({ generatedAtUtc: "2026-05-26T12:00:00Z", meta: { success: true, dataQualityStatus: "stale", warningCode: "STALE_CACHE", warningMessage: "Podaci su zastareli." } });
    getInventoryActionSuggestionsMock.mockResolvedValue({ generatedAtUtc: "2026-05-26T12:00:00Z", pendingCount: 0, approvedCount: 0, deferredCount: 0, closedCount: 0, items: [], meta: { success: true, dataQualityStatus: "stale", warningCode: "STALE_CACHE", warningMessage: "Podaci su zastareli." } });
    getForecastMock.mockResolvedValue({
      generatedAtUtc: "2026-05-26T12:00:00Z",
      totalCount: 0,
      snapshotAvailable: false,
      warning: "Prognoza koristi zastarele ulaze.",
      items: [],
    });
    getInventoryAlertsMock.mockResolvedValue({ generatedAtUtc: "2026-05-26T12:00:00Z", items: [] });
    getRebalanceSuggestionsMock.mockResolvedValue({ generatedAtUtc: "2026-05-26T12:00:00Z", items: [] });

    render(<MemoryRouter><InventoryPage /></MemoryRouter>);

    const trustHeader = await screen.findByTestId("trust-header");
    expect(trustHeader).toHaveAttribute("data-quality", "stale");
    expect(trustHeader).toHaveAttribute("data-partial", "true");
  });
});

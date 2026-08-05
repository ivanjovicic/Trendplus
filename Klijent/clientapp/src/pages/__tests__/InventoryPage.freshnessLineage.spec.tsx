import { render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import InventoryPage from "../InventoryPage";
import { formatDateTime } from "../../utils/analyticsFormatters";

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

let capturedTrustHeaderProps: { lastRefreshAt?: string | null; dataFreshnessStatus?: string | null } | null = null;

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
  default: (props: { lastRefreshAt?: string | null; dataFreshnessStatus?: string | null }) => {
    capturedTrustHeaderProps = props;
    return (
      <div
        data-testid="trust-header"
        data-last-refresh={props.lastRefreshAt ?? ""}
        data-freshness={props.dataFreshnessStatus ?? ""}
      />
    );
  },
}));
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

describe("InventoryPage freshness lineage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    capturedTrustHeaderProps = null;

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
    getInventoryStoreComparisonMock.mockResolvedValue({ generatedAtUtc: "2026-08-05T10:15:00Z", meta: { success: true, dataQualityStatus: "good" } });
    getInventoryActionSuggestionsMock.mockResolvedValue({ generatedAtUtc: "2026-08-05T10:20:00Z", pendingCount: 0, approvedCount: 0, deferredCount: 0, closedCount: 0, items: [], meta: { success: true, dataQualityStatus: "good" } });
    getForecastMock.mockResolvedValue({ generatedAtUtc: "2026-08-05T10:45:00Z", items: [] });
    getInventoryAlertsMock.mockResolvedValue({ generatedAtUtc: "2026-08-05T10:30:00Z", items: [] });
    getRebalanceSuggestionsMock.mockResolvedValue({ generatedAtUtc: "2026-08-05T10:35:00Z", items: [] });
    getAnalyticsActionSourceStatusesMock.mockResolvedValue({ items: [] });
  });

  it("does not promote secondary panel timestamps to the inventory header", async () => {
    render(<InventoryPage />);

    const note = await screen.findByRole("note");
    expect(note).toHaveTextContent("Sekundarni paneli imaju zasebnu svežinu");
    expect(note).toHaveTextContent(formatDateTime("2026-08-05T10:45:00Z"));

    await waitFor(() => {
      expect(capturedTrustHeaderProps).not.toBeNull();
    });

    expect(capturedTrustHeaderProps?.lastRefreshAt).toBeNull();
    expect(screen.getByTestId("trust-header")).toHaveAttribute("data-last-refresh", "");
  });
});

import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import PilotReadinessPage from "../PilotReadinessPage";
import {
  getAnalyticsActionCounts,
  getAnalyticsRefreshStatus,
  getInventoryAlerts,
  getPilotDataQualityIntakeReport,
  getPilotIntakeDurableReport,
  getSupplierDecisionDurableReport,
} from "../../services/analyticsApi";

vi.mock("../../services/analyticsApi", async () => {
  const actual = await vi.importActual<typeof import("../../services/analyticsApi")>("../../services/analyticsApi");
  return {
    ...actual,
    getAnalyticsActionCounts: vi.fn(),
    getAnalyticsRefreshStatus: vi.fn(),
    getInventoryAlerts: vi.fn(),
    getPilotDataQualityIntakeReport: vi.fn(),
    getPilotIntakeDurableReport: vi.fn(),
    getSupplierDecisionDurableReport: vi.fn(),
  };
});

const mockedGetPilotDataQualityIntakeReport = vi.mocked(getPilotDataQualityIntakeReport);
const mockedGetAnalyticsRefreshStatus = vi.mocked(getAnalyticsRefreshStatus);
const mockedGetAnalyticsActionCounts = vi.mocked(getAnalyticsActionCounts);
const mockedGetInventoryAlerts = vi.mocked(getInventoryAlerts);
const mockedGetPilotIntakeDurableReport = vi.mocked(getPilotIntakeDurableReport);
const mockedGetSupplierDecisionDurableReport = vi.mocked(getSupplierDecisionDurableReport);

function renderPage() {
  render(
    <MemoryRouter initialEntries={["/analytics/pilot-readiness"]}>
      <Routes>
        <Route path="/analytics/pilot-readiness" element={<PilotReadinessPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe("PilotReadinessPage", () => {
  it("renders readiness links and warnings when core signals are available", async () => {
    mockedGetPilotDataQualityIntakeReport.mockResolvedValue({
      periodFromUtc: "2026-05-01T00:00:00Z",
      periodToUtc: "2026-05-31T23:59:59Z",
      dataScope: "all",
      lastImportAtUtc: "2026-06-01T06:00:00Z",
      lastImportStatus: "succeeded",
      lastRefreshAtUtc: "2026-06-01T07:00:00Z",
      dataFreshnessStatus: "fresh",
      readinessStatus: "ready_with_warnings",
      readinessLabel: "Spremno uz upozorenja",
      readinessScore: 82,
      loadedData: {
        articlesCount: 120,
        saleItemsCount: 500,
        receiptsCount: 40,
        suppliersCount: 8,
        storesCount: 3,
        firstSaleDate: "2026-05-01T00:00:00Z",
        lastSaleDate: "2026-05-31T23:59:59Z",
      },
      issues: {
        missingSupplierCount: 2,
        missingCostCount: 3,
        missingCategoryCount: 1,
        missingColorCount: 0,
        missingSizeCount: 0,
        saleWithoutArticleCount: 0,
        zeroOrNegativePriceCount: 0,
        duplicateSkuCount: 0,
        missingSupplierNameCount: 0,
      },
      impact: {
        revenueWithoutCostPercent: 0.03,
        articlesWithoutSupplierPercent: 0.05,
        recommendationsBlockedCount: 4,
        ignoredRowsCount: 2,
        insufficientSignalCount: 6,
      },
      recommendedActions: ["Proverite nabavne cene"],
      meta: { success: true },
    } as any);

    mockedGetAnalyticsRefreshStatus.mockResolvedValue({
      lastSuccessfulRefreshAtUtc: "2026-06-01T07:00:00Z",
      lastAttemptAtUtc: "2026-06-01T07:30:00Z",
      lastFailureAtUtc: null,
      isRunning: false,
      lastErrorMessage: null,
      currentStep: null,
      refreshedObjects: ["dashboard"],
      failedObjects: [],
      durationSeconds: 14,
      dataFreshnessStatus: "fresh",
      processMode: "worker",
      processType: "worker",
      workersEnabled: true,
      generatedAtUtc: "2026-06-01T07:30:00Z",
      jobs: [],
    } as any);

    mockedGetAnalyticsActionCounts.mockResolvedValue({ new: 2, accepted: 1, deferred: 0, rejected: 0, done: 1, p1Open: 1 } as any);
    mockedGetInventoryAlerts.mockResolvedValue({ generatedAtUtc: "2026-06-01T07:30:00Z", totalCount: 2, snapshotAvailable: true, items: [{ severity: "warning" }, { severity: "critical" }] } as any);
    mockedGetPilotIntakeDurableReport.mockResolvedValue({ reportId: "pilot", stableQueryUrl: "/analytics/reports/pilot-intake", payload: { tableKey: "pilot", tableTitle: "Pilot", documentType: "pilot", templateName: "default", locale: "sr-RS", columns: [], rows: [], filters: [], metadata: [] }, rows: [], sections: [], generatedAtUtc: "2026-06-01T07:30:00Z", period: { fromUtc: "2026-05-01T00:00:00Z", toUtc: "2026-05-31T23:59:59Z", label: "Pilot" }, dataQualityStatus: "good", recommendationAllowed: true, usedFallback: false, methodology: { summary: "x", notes: [] } } as any);
    mockedGetSupplierDecisionDurableReport.mockResolvedValue({ reportId: "supplier", stableQueryUrl: "/analytics/supplier/report", payload: { tableKey: "supplier", tableTitle: "Supplier", documentType: "supplier", templateName: "default", locale: "sr-RS", columns: [], rows: [], filters: [], metadata: [] }, rows: [], sections: [], generatedAtUtc: "2026-06-01T07:30:00Z", period: { fromUtc: "2026-05-01T00:00:00Z", toUtc: "2026-05-31T23:59:59Z", label: "Supplier" }, dataQualityStatus: "good", recommendationAllowed: true, usedFallback: false, methodology: { summary: "x", notes: [] } } as any);

    renderPage();

    const summary = await screen.findByRole("region", { name: /Status pilota/i });
    expect(summary).toHaveTextContent(/Spremno uz upozorenja/i);
    expect(screen.getAllByRole("link", { name: /Otvori dashboard/i })[0]).toHaveAttribute("href", "/analytics");
    expect(screen.getAllByRole("link", { name: /Otvori import/i })[0]).toHaveAttribute("href", "/access-import");
    expect(screen.getAllByRole("link", { name: /Pilot intake report/i }).length).toBeGreaterThan(0);
    expect(screen.getAllByRole("link", { name: /Supplier report/i }).length).toBeGreaterThan(0);
  });

  it("shows unknown status when core signals cannot be confirmed", async () => {
    mockedGetPilotDataQualityIntakeReport.mockRejectedValue(new Error("unavailable"));
    mockedGetAnalyticsRefreshStatus.mockRejectedValue(new Error("unavailable"));
    mockedGetAnalyticsActionCounts.mockRejectedValue(new Error("unavailable"));
    mockedGetInventoryAlerts.mockRejectedValue(new Error("unavailable"));
    mockedGetPilotIntakeDurableReport.mockRejectedValue(new Error("unavailable"));
    mockedGetSupplierDecisionDurableReport.mockRejectedValue(new Error("unavailable"));

    renderPage();

    const summary = await screen.findByRole("region", { name: /Status pilota/i });
    expect(summary).toHaveTextContent(/Nepoznato/i);
    expect(summary).toHaveTextContent(/Nije moguće potvrditi da su podaci učitani|Nije moguće potvrditi dostupnost reportova/i);
  });
});

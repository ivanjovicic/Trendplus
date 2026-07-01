import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import DataQualityPage from "./DataQualityPage";
import {
  getAnalyticsDataQualityHealth,
  getAnalyticsDataQualityTrend,
  getAnalyticsRefreshStatus,
  getDataQualityIssues,
  getDataQualityTopOffenders,
  getPilotDataQualityIntakeReport,
  getPilotIntakeDurableReport,
} from "../services/analyticsApi";
import type {
  AnalyticsDataQualityHealth,
  AnalyticsRefreshStatus,
  DataQualityIssueItem,
  DataQualityIssueListResult,
  DataQualityTopOffendersResult,
  DataQualityTrendResult,
  PilotDataQualityIntakeReport,
  PilotIntakeDurableReport,
} from "../types/analytics";

vi.mock("../components/analytics/AnalyticsTrustHeader", () => ({
  default: ({ title, dataQualityStatus }: { title: string; dataQualityStatus?: string | null }) => (
    <div data-testid="analytics-trust-header">
      {title} | status: {dataQualityStatus ?? "n/a"}
    </div>
  ),
}));

vi.mock("../components/analytics/AnalyticsRefreshStatusBanner", () => ({
  default: ({ error }: { error?: string | null }) => <div data-testid="refresh-banner">{error ?? "refresh-ok"}</div>,
}));

vi.mock("../components/analytics/AnalyticsTableToolbar", () => ({
  default: ({ tableKey, rows }: { tableKey: string; rows: unknown[] }) => (
    <div data-testid="analytics-toolbar">
      {tableKey}: {rows.length} rows
    </div>
  ),
}));

vi.mock("../components/analytics/KpiExplainButton", () => ({
  default: ({ ariaLabel }: { ariaLabel: string }) => <button type="button" aria-label={ariaLabel}>?</button>,
}));

vi.mock("../components/analytics/PilotDataQualityIntakeReport", () => ({
  default: ({ error }: { error?: string | null }) => <div data-testid="pilot-intake-panel">{error ?? "pilot-intake-ok"}</div>,
}));

vi.mock("../components/analytics/PilotImportReadinessCard", () => ({
  default: () => <div data-testid="pilot-readiness-card">pilot readiness</div>,
}));

vi.mock("../components/ui/InfoTip", () => ({
  default: ({ text }: { text: string }) => <span data-testid="info-tip">{text}</span>,
}));

vi.mock("../services/analyticsApi", async () => {
  const actual = await vi.importActual<typeof import("../services/analyticsApi")>("../services/analyticsApi");
  return {
    ...actual,
    getAnalyticsDataQualityHealth: vi.fn(),
    getAnalyticsDataQualityTrend: vi.fn(),
    getAnalyticsRefreshStatus: vi.fn(),
    getDataQualityIssues: vi.fn(),
    getDataQualityTopOffenders: vi.fn(),
    getPilotDataQualityIntakeReport: vi.fn(),
    getPilotIntakeDurableReport: vi.fn(),
  };
});

function issue(overrides: Partial<DataQualityIssueItem> = {}): DataQualityIssueItem {
  return {
    sku: "SKU-101",
    productId: "101",
    name: "Premium sandala",
    supplierId: null,
    supplierName: null,
    shoeTypeId: "12",
    shoeTypeName: "Sandala",
    issueType: "missingSupplier",
    sales30d: 72000,
    stock: 8,
    lastUpdated: "2026-07-01T08:00:00Z",
    ...overrides,
  };
}

function issues(overrides: Partial<DataQualityIssueListResult> = {}): DataQualityIssueListResult {
  const items = overrides.items ?? [issue()];
  return {
    page: 1,
    pageSize: 25,
    total: items.length,
    items,
    meta: {
      success: true,
      generatedAtUtc: "2026-07-01T08:00:00Z",
      lastRefreshAtUtc: "2026-07-01T08:00:00Z",
      dataQualityStatus: "warning",
      isPartial: false,
    },
    ...overrides,
  };
}

function health(overrides: Partial<AnalyticsDataQualityHealth> = {}): AnalyticsDataQualityHealth {
  return {
    generatedAt: "2026-07-01T08:00:00Z",
    lookbackDays: 30,
    windowFrom: "2026-06-01T00:00:00Z",
    windowTo: "2026-07-01T00:00:00Z",
    orphanArticleCount: 14,
    totalRevenue: 1_200_000,
    missingCostRevenue: 220_000,
    missingCostRevenueSharePct: 18.3,
    unknownSupplierRevenue: 180_000,
    unknownSupplierRevenueSharePct: 15,
    score: 63,
    scoreStatus: "warning",
    scoreSummary: "Postoje problemi koji blokiraju deo preporuka.",
    thresholds: {
      orphanArticleCount: 10,
      missingCostRevenueSharePct: 5,
      unknownSupplierRevenueSharePct: 3,
    },
    meta: {
      success: true,
      generatedAtUtc: "2026-07-01T08:00:00Z",
      lastRefreshAtUtc: "2026-07-01T08:00:00Z",
      dataQualityStatus: "warning",
    },
    ...overrides,
  };
}

function refresh(overrides: Partial<AnalyticsRefreshStatus> = {}): AnalyticsRefreshStatus {
  return {
    lastSuccessfulRefreshAtUtc: "2026-07-01T07:55:00Z",
    lastAttemptAtUtc: "2026-07-01T07:55:00Z",
    lastFailureAtUtc: null,
    isRunning: false,
    lastErrorMessage: null,
    currentStep: null,
    refreshedObjects: ["data_quality"],
    failedObjects: [],
    durationSeconds: 12,
    dataFreshnessStatus: "fresh",
    processMode: "worker",
    processType: "worker",
    workersEnabled: true,
    workerWarning: null,
    workerProcessWarning: null,
    cacheMode: "redis",
    isDistributed: true,
    generatedAtUtc: "2026-07-01T08:00:00Z",
    jobs: [],
    recentRuns: [],
    ...overrides,
  };
}

function topOffenders(overrides: Partial<DataQualityTopOffendersResult> = {}): DataQualityTopOffendersResult {
  return {
    issueType: "missingSupplier",
    limit: 10,
    count: 1,
    items: [
      {
        sku: "SKU-101",
        productId: "101",
        name: "Premium sandala",
        supplierName: null,
        shoeTypeName: "Sandala",
        sales30d: 72000,
        revenueImpactRsd: 72000,
        revenueImpactPct: 6,
        actionUrl: "/artikli/101/edit",
      },
    ],
    meta: { success: true },
    ...overrides,
  };
}

function trend(): DataQualityTrendResult {
  return {
    days: 7,
    dataScope: "all",
    points: [
      { date: "2026-06-25T00:00:00Z", missingCostRevenueSharePct: 20, unknownSupplierRevenueSharePct: 17, orphanArticleCount: 18 },
      { date: "2026-07-01T00:00:00Z", missingCostRevenueSharePct: 18.3, unknownSupplierRevenueSharePct: 15, orphanArticleCount: 14 },
    ],
    meta: { success: true },
  };
}

function intake(): PilotDataQualityIntakeReport {
  return {
    generatedAtUtc: "2026-07-01T08:00:00Z",
    periodFromUtc: "2026-06-01T00:00:00Z",
    periodToUtc: "2026-07-01T00:00:00Z",
    dataScope: "all",
    storeId: null,
    supplierId: null,
    lastImportAtUtc: "2026-07-01T07:30:00Z",
    lastRefreshAtUtc: "2026-07-01T07:55:00Z",
    readinessStatus: "warning",
    readinessLabel: "Potrebne korekcije",
    readinessScore: 63,
    loadedData: {
      articlesCount: 1000,
      saleItemsCount: 5000,
      receiptsCount: 800,
      suppliersCount: 40,
      storesCount: 2,
      firstSaleDate: "2024-01-01T00:00:00Z",
      lastSaleDate: "2026-07-01T00:00:00Z",
    },
    issues: {
      missingSupplierCount: 14,
      missingCostCount: 22,
      missingCategoryCount: 6,
      saleWithoutArticleCount: 0,
      zeroOrNegativePriceCount: 0,
      missingSupplierNameCount: 3,
    },
    impact: {
      revenueWithoutCostPercent: 18.3,
      articlesWithoutSupplierPercent: 1.4,
      recommendationsBlockedCount: 7,
      ignoredRowsCount: 11,
      insufficientSignalCount: 9,
    },
    recommendedActions: ["Dopuniti dobavljače za artikle sa prometom."],
    meta: { success: true },
  };
}

function renderPage(initialEntry = "/analytics/data-quality") {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <Routes>
        <Route path="/analytics/data-quality" element={<DataQualityPage />} />
        <Route path="/artikli/:id/edit" element={<div>Article edit route</div>} />
      </Routes>
    </MemoryRouter>,
  );
}

describe("DataQualityPage", () => {
  beforeEach(() => {
    vi.mocked(getDataQualityIssues).mockResolvedValue(issues());
    vi.mocked(getAnalyticsDataQualityHealth).mockResolvedValue(health());
    vi.mocked(getAnalyticsRefreshStatus).mockResolvedValue(refresh());
    vi.mocked(getPilotDataQualityIntakeReport).mockResolvedValue(intake());
    vi.mocked(getPilotIntakeDurableReport).mockResolvedValue({} as PilotIntakeDurableReport);
    vi.mocked(getDataQualityTopOffenders).mockResolvedValue(topOffenders());
    vi.mocked(getAnalyticsDataQualityTrend).mockResolvedValue(trend());
  });

  it("renders warning health, issue table, top offenders, trend and export context", async () => {
    renderPage();

    expect(screen.getByTestId("analytics-trust-header")).toHaveTextContent("Provera kvaliteta podataka");
    await screen.findByText("Problematični artikli");

    expect(screen.getByText("Skor kvaliteta podataka")).toBeInTheDocument();
    expect(screen.getByText("Postoje problemi koji blokiraju deo preporuka.")).toBeInTheDocument();
    expect(screen.getAllByText("Artikli bez dobavljača").length).toBeGreaterThan(0);
    expect(screen.getByText("Promet bez nabavne cene")).toBeInTheDocument();
    expect(screen.getByText("Blokirane preporuke")).toBeInTheDocument();
    expect(screen.getByText("Trend kvaliteta podataka")).toBeInTheDocument();
    expect(screen.getByText("Top problemi")).toBeInTheDocument();
    expect(screen.getAllByText("Premium sandala").length).toBeGreaterThan(0);
    expect(screen.getAllByText("SKU-101").length).toBeGreaterThan(0);
    expect(screen.getByTestId("analytics-toolbar")).toHaveTextContent("data-quality-missingSupplier: 1 rows");
  });

  it("preserves context query parameters and calls APIs with dataScope", async () => {
    renderPage("/analytics/data-quality?originTable=color-sales-stats&dataScope=imported&fromDate=2026-06-01T00:00:00Z&toDate=2026-06-30T23:59:59Z&storeId=2&supplierId=7&returnTo=/analytics/color-sales-stats");

    await screen.findByText("Problematični artikli");

    expect(getDataQualityIssues).toHaveBeenCalledWith(expect.objectContaining({ dataScope: "imported" }));
    expect(getAnalyticsDataQualityHealth).toHaveBeenCalledWith(undefined, "imported");
    expect(getDataQualityTopOffenders).toHaveBeenCalledWith("missingSupplier", 10, "imported");
    expect(screen.getByText(/Otvoreno iz analytics tabele:/)).toBeInTheDocument();
    expect(screen.getByText("color-sales-stats")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Nazad na izvorni kontekst" })).toHaveAttribute("href", "/analytics/color-sales-stats");
  });

  it("updates issue type and search query through URL-driven filters", async () => {
    renderPage();
    await screen.findByText("Problematični artikli");

    fireEvent.click(screen.getByRole("tab", { name: "Nedostajući tip obuće" }));
    await waitFor(() => expect(getDataQualityIssues).toHaveBeenLastCalledWith(expect.objectContaining({
      type: "missingShoeType",
      page: 1,
    })));

    fireEvent.change(screen.getByPlaceholderText("Pretraga po SKU, artiklu, dobavljaču, tipu..."), { target: { value: "sandala" } });
    fireEvent.click(screen.getByRole("button", { name: "Pretraži" }));

    await waitFor(() => expect(getDataQualityIssues).toHaveBeenLastCalledWith(expect.objectContaining({
      type: "missingShoeType",
      q: "sandala",
      page: 1,
    })));
  });

  it("shows intake view without losing read-only report context", async () => {
    renderPage();
    await screen.findByText("Problematični artikli");

    fireEvent.click(screen.getByRole("tab", { name: "Pilot intake izveštaj" }));

    expect(await screen.findByTestId("pilot-intake-panel")).toHaveTextContent("pilot-intake-ok");
    expect(getPilotDataQualityIntakeReport).toHaveBeenCalled();
    expect(getPilotIntakeDurableReport).toHaveBeenCalled();
  });
});

import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import PilotReadinessPage from "./PilotReadinessPage";
import {
  getAnalyticsActionCounts,
  getAnalyticsActionOutcomeSummary,
  getAnalyticsDataQualityHealth,
  getAnalyticsRefreshStatus,
  getDashboardBootstrap,
  getPilotDataQualityIntakeReport,
  getPilotIntakeDurableReport,
  getProductDecisionCenter,
  getSupplierDecisionDurableReport,
} from "../services/analyticsApi";
import type {
  AnalyticsActionCounts,
  AnalyticsActionOutcomeSummaryResponse,
  AnalyticsDashboardBootstrap,
  AnalyticsRefreshStatus,
  PilotDataQualityIntakeReport,
  PilotIntakeDurableReport,
  ProductDecisionCenterResponse,
  SupplierDecisionDurableReport,
} from "../types/analytics";

vi.mock("../components/analytics/AnalyticsTrustHeader", () => ({
  default: ({
    title,
    dataQualityStatus,
    isPartial,
    emptyStateReason,
  }: {
    title: string;
    dataQualityStatus?: string | null;
    isPartial?: boolean;
    emptyStateReason?: string | null;
  }) => (
    <div data-testid="analytics-trust-header">
      {title} | status: {dataQualityStatus ?? "n/a"} | partial: {String(Boolean(isPartial))} | summary: {emptyStateReason ?? "-"}
    </div>
  ),
}));

vi.mock("../components/analytics/AnalyticsRefreshStatusBanner", () => ({
  default: ({ loading, error }: { loading?: boolean; error?: string | null }) => (
    <div data-testid="refresh-banner">{loading ? "loading" : error ?? "refresh-ok"}</div>
  ),
}));

vi.mock("../components/analytics/AnalyticsEmptyState", () => ({
  default: ({
    title,
    message,
    actions,
  }: {
    title?: string;
    message?: string;
    actions?: Array<{ label: string; onClick?: () => void }>;
  }) => (
    <div data-testid="analytics-empty-state">
      <strong>{title}</strong>
      <span>{message}</span>
      {actions?.map((action) => action.onClick ? (
        <button key={action.label} type="button" onClick={action.onClick}>{action.label}</button>
      ) : null)}
    </div>
  ),
}));

vi.mock("../components/analytics/AnalyticsErrorState", () => ({
  default: ({ title, message, onRetry }: { title: string; message: string; onRetry?: () => void }) => (
    <div data-testid="analytics-error-state">
      <strong>{title}</strong>
      <span>{message}</span>
      <button type="button" onClick={onRetry}>Ponovo proveri</button>
    </div>
  ),
}));

vi.mock("../services/analyticsApi", () => ({
  getAnalyticsActionCounts: vi.fn(),
  getAnalyticsActionOutcomeSummary: vi.fn(),
  getAnalyticsDataQualityHealth: vi.fn(),
  getAnalyticsRefreshStatus: vi.fn(),
  getDashboardBootstrap: vi.fn(),
  getPilotDataQualityIntakeReport: vi.fn(),
  getPilotIntakeDurableReport: vi.fn(),
  getProductDecisionCenter: vi.fn(),
  getSupplierDecisionDurableReport: vi.fn(),
}));

function currentIso(): string {
  return new Date().toISOString();
}

function readyBootstrap(): AnalyticsDashboardBootstrap {
  return {
    summary: {
      totalRevenue: 240_000,
      totalTransactions: 48,
      totalUnits: 96,
      avgBasketValue: 5_000,
      avgItemPrice: 2_500,
    },
    inventory: {
      totalSkuCount: 24,
      totalOnHand: 180,
      lowStockCount: 2,
      outOfStockCount: 1,
    },
    dailySales: [{ date: "2026-07-01", totalRevenue: 240_000, transactionCount: 48, totalUnits: 96 }],
    categoryData: [],
    genderData: [],
    supplierData: [{ dobavljacId: 7, dobavljacNaziv: "Dobavljač Premium", totalRevenue: 180_000, totalUnits: 70, transactionCount: 30 }],
    supplierOptions: [{ supplierId: 7, supplierName: "Dobavljač Premium" }],
    paymentData: [],
    weekdayData: [],
    hourData: [],
    quickInsights: null,
    transactionStats: null,
    advanced: null,
    topAdvanced: null,
    validationCompleteness: null,
    validationFreshness: null,
    validationLostSales: null,
    decisionActions: [],
    executive: {
      totalMarginContributionRsd: 70_000,
      inventoryDangerValueRsd: 15_000,
      topSuppliers: [{ supplierId: 7, supplierName: "Dobavljač Premium", revenue: 180_000, marginContribution: 54_000, link: "/analytics/supplier" }],
      topMarginProducts: [],
      topMarginCategories: [],
      negativeSignals: [],
      dataQualitySummary: {
        missingSupplierCount: 0,
        missingCostCount: 0,
        insufficientSignalCount: 0,
        ignoredRowsCount: 0,
        freshnessStatus: "fresh",
      },
    },
    errors: [],
    meta: { success: true, dataQualityStatus: "good", generatedAtUtc: currentIso(), lastRefreshAtUtc: currentIso() },
  } as AnalyticsDashboardBootstrap;
}

function readyRefresh(): AnalyticsRefreshStatus {
  return {
    lastSuccessfulRefreshAtUtc: currentIso(),
    lastAttemptAtUtc: currentIso(),
    lastFailureAtUtc: null,
    isRunning: false,
    lastErrorMessage: null,
    currentStep: null,
    refreshedObjects: ["dashboard/bootstrap", "product-decision-center"],
    failedObjects: [],
    durationSeconds: 15,
    dataFreshnessStatus: "fresh",
    processMode: "worker",
    processType: "worker",
    workersEnabled: true,
    workerWarning: null,
    workerProcessWarning: null,
    cacheMode: "redis",
    isDistributed: true,
    generatedAtUtc: currentIso(),
    jobs: [],
    recentRuns: [],
  };
}

function readyIntake(overrides: Partial<PilotDataQualityIntakeReport> = {}): PilotDataQualityIntakeReport {
  return {
    generatedAtUtc: currentIso(),
    periodFromUtc: "2026-06-01T00:00:00Z",
    periodToUtc: "2026-07-01T23:59:59Z",
    dataScope: "all",
    storeId: null,
    supplierId: null,
    lastImportAtUtc: currentIso(),
    lastRefreshAtUtc: currentIso(),
    readinessStatus: "good",
    readinessLabel: "Spremno",
    readinessScore: 96,
    loadedData: {
      articlesCount: 1000,
      saleItemsCount: 5000,
      receiptsCount: 800,
      suppliersCount: 40,
      storesCount: 2,
      firstSaleDate: "2024-01-01",
      lastSaleDate: "2026-07-01",
    },
    issues: {
      missingSupplierCount: 0,
      missingCostCount: 0,
      missingCategoryCount: 0,
      missingColorCount: 0,
      missingSizeCount: 0,
      saleWithoutArticleCount: 0,
      zeroOrNegativePriceCount: 0,
      duplicateSkuCount: 0,
      missingSupplierNameCount: 0,
    },
    impact: {
      revenueWithoutCostPercent: 0,
      articlesWithoutSupplierPercent: 0,
      recommendationsBlockedCount: 0,
      ignoredRowsCount: 0,
      insufficientSignalCount: 0,
    },
    recommendedActions: [],
    meta: { success: true, dataQualityStatus: "good", generatedAtUtc: currentIso() },
    ...overrides,
  };
}

function readyProducts(): ProductDecisionCenterResponse {
  return {
    generatedAtUtc: currentIso(),
    periodFromUtc: "2026-06-01T00:00:00Z",
    periodToUtc: "2026-07-01T23:59:59Z",
    totalRows: 1,
    analyzedRows: 1,
    ignoredRowsCount: 0,
    summary: {
      replenishCount: 1,
      markdownCount: 0,
      highPotentialCount: 0,
      badDataCount: 0,
      lostSalesEstimate: 25_000,
      slowStockCapital: 0,
    },
    rows: [{} as ProductDecisionCenterResponse["rows"][number]],
    meta: { success: true, dataQualityStatus: "good", generatedAtUtc: currentIso() },
  };
}

function readyCounts(): AnalyticsActionCounts {
  return { new: 1, accepted: 1, deferred: 0, rejected: 0, done: 1, p1Open: 1 };
}

function readyOutcome(): AnalyticsActionOutcomeSummaryResponse {
  return {
    meta: {
      success: true,
      periodMode: "created",
      createdFrom: null,
      createdTo: null,
      resolvedFrom: null,
      resolvedTo: null,
      measuredFrom: null,
      measuredTo: null,
      generatedAtUtc: currentIso(),
      sampleSize: 3,
      measuredSampleSize: 1,
      warnings: [],
      emptyReason: null,
    },
    totals: {
      createdCount: 3,
      closedCount: 1,
      openCount: 2,
      measuredCount: 1,
      pendingOutcomeCount: 0,
      successCount: 1,
      neutralCount: 0,
      negativeCount: 0,
      notMeasuredCount: 0,
      outcomeCoverageRate: 1,
      positiveOutcomeRate: 1,
      negativeOutcomeRate: 0,
    },
    impact: { expectedImpactRsd: 25_000, measuredImpactRsd: 22_000, realizationRatio: 0.88, measuredImpactSampleCount: 1 },
    bySourceType: [],
    byPriority: [],
    byOutcomeStatus: [],
    byDataQuality: [],
    byConfidenceBucket: [],
    byReliabilityBucket: [],
  };
}

function readyPilotReport(): PilotIntakeDurableReport {
  return {
    reportId: "pilot-report",
    stableQueryUrl: "/analytics/reports/pilot-intake",
    reportTitle: "Pilot intake",
    reportType: "pilot-intake",
    generatedAtUtc: currentIso(),
    periodFrom: "2026-06-01",
    periodTo: "2026-07-01",
    period: { fromUtc: "2026-06-01T00:00:00Z", toUtc: "2026-07-01T23:59:59Z", label: "Jun 2026" },
    lastRefreshAtUtc: currentIso(),
    dataFreshnessStatus: "fresh",
    dataQualityStatus: "good",
    recommendationAllowed: true,
    usedFallback: false,
    warnings: [],
    methodology: "Pilot readiness methodology.",
    rows: [{ section: "Sažetak", item: "Status", value: "Spremno" }],
    sections: [{ key: "summary", title: "Sažetak", rowCount: 1, description: "Sažetak", emptyMessage: null, columns: [], rows: [{ section: "Sažetak", item: "Status", value: "Spremno" }] }],
    payload: { tableKey: "pilot-intake", tableTitle: "Pilot intake", documentType: "pilot-intake", templateName: "analytics-table-default", locale: "sr-RS", columns: [], rows: [], filters: [], metadata: [], templateVersion: 1 },
    meta: { success: true, dataQualityStatus: "good", generatedAtUtc: currentIso() },
  } as PilotIntakeDurableReport;
}

function readySupplierReport(): SupplierDecisionDurableReport {
  return {
    reportId: "supplier-report",
    stableQueryUrl: "/analytics/reports/supplier-decision",
    reportTitle: "Supplier decision",
    reportType: "supplier-decision",
    generatedAtUtc: currentIso(),
    periodFrom: "2026-06-01",
    periodTo: "2026-07-01",
    period: { fromUtc: "2026-06-01T00:00:00Z", toUtc: "2026-07-01T23:59:59Z", label: "Jun 2026" },
    lastRefreshAtUtc: currentIso(),
    dataFreshnessStatus: "fresh",
    dataQualityStatus: "good",
    recommendationAllowed: true,
    usedFallback: false,
    fallbackReason: null,
    warnings: [],
    methodology: "Supplier methodology.",
    rows: [{ section: "Sažetak", item: "Status", value: "Spremno" }],
    sections: [{ key: "summary", title: "Sažetak", rowCount: 1, description: "Sažetak", emptyMessage: null, columns: [], rows: [{ section: "Sažetak", item: "Status", value: "Spremno" }] }],
    kpis: [],
    recommendedActions: [],
    methodologySummary: "Supplier methodology.",
    payload: { tableKey: "supplier-decision", tableTitle: "Supplier decision", documentType: "supplier-decision", templateName: "analytics-table-default", locale: "sr-RS", columns: [], rows: [], filters: [], metadata: [], templateVersion: 1 },
    meta: { success: true, dataQualityStatus: "good", generatedAtUtc: currentIso() },
  } as SupplierDecisionDurableReport;
}

function configureReady(overrides: { intake?: PilotDataQualityIntakeReport } = {}) {
  vi.mocked(getDashboardBootstrap).mockResolvedValue(readyBootstrap());
  vi.mocked(getAnalyticsRefreshStatus).mockResolvedValue(readyRefresh());
  vi.mocked(getAnalyticsDataQualityHealth).mockResolvedValue(null as never);
  vi.mocked(getPilotDataQualityIntakeReport).mockResolvedValue(overrides.intake ?? readyIntake());
  vi.mocked(getProductDecisionCenter).mockResolvedValue(readyProducts());
  vi.mocked(getAnalyticsActionCounts).mockResolvedValue(readyCounts());
  vi.mocked(getAnalyticsActionOutcomeSummary).mockResolvedValue(readyOutcome());
  vi.mocked(getPilotIntakeDurableReport).mockResolvedValue(readyPilotReport());
  vi.mocked(getSupplierDecisionDurableReport).mockResolvedValue(readySupplierReport());
}

function renderPage() {
  return render(
    <MemoryRouter>
      <PilotReadinessPage />
    </MemoryRouter>,
  );
}

describe("PilotReadinessPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    configureReady();
  });

  it("renders a fully ready nine-step checklist from confirmed sources", async () => {
    renderPage();

    expect(screen.getByTestId("refresh-banner")).toHaveTextContent("loading");
    expect(await screen.findByRole("heading", { name: "Spremno za demo" })).toBeInTheDocument();

    expect(getDashboardBootstrap).toHaveBeenCalledWith(undefined, undefined, true);
    expect(getProductDecisionCenter).toHaveBeenCalledWith({ top: 100 });
    expect(getPilotDataQualityIntakeReport).toHaveBeenCalledWith({});
    expect(getPilotIntakeDurableReport).toHaveBeenCalledWith({});
    expect(getSupplierDecisionDurableReport).toHaveBeenCalledWith({});

    const checklist = screen.getByRole("region", { name: "Pilot readiness checklist" });
    expect(within(checklist).getAllByRole("article")).toHaveLength(9);
    expect(within(checklist).getAllByText("Spremno")).toHaveLength(9);
    expect(screen.getByTestId("analytics-trust-header")).toHaveTextContent("status: good");
    expect(screen.getByTestId("analytics-trust-header")).toHaveTextContent("partial: false");
    expect(screen.queryByTestId("analytics-empty-state")).not.toBeInTheDocument();
    expect(screen.queryByTestId("analytics-error-state")).not.toBeInTheDocument();
  });

  it("renders blocked readiness when data quality blocks recommendations", async () => {
    configureReady({
      intake: readyIntake({
        readinessStatus: "critical",
        readinessLabel: "Blokirano",
        readinessScore: 38,
        issues: {
          ...readyIntake().issues,
          missingCostCount: 25,
        },
        impact: {
          ...readyIntake().impact,
          recommendationsBlockedCount: 12,
        },
        meta: { success: true, dataQualityStatus: "critical", generatedAtUtc: currentIso() },
      }),
    });

    renderPage();

    expect(await screen.findByRole("heading", { name: "Pilot nije spreman" })).toBeInTheDocument();
    expect(screen.getByTestId("analytics-trust-header")).toHaveTextContent("status: critical");

    const qualityCard = screen.getByText("Kvalitet podataka proveren").closest("article");
    expect(qualityCard).not.toBeNull();
    expect(within(qualityCard as HTMLElement).getByText("Blokirano")).toBeInTheDocument();
    expect(within(qualityCard as HTMLElement).getByText(/Blokirane preporuke: 12/i)).toBeInTheDocument();
  });

  it("keeps partial source failures visible without collapsing confirmed cards", async () => {
    vi.mocked(getSupplierDecisionDurableReport).mockRejectedValueOnce(new Error("Supplier report timeout"));

    renderPage();

    expect(await screen.findByRole("heading", { name: "Spremno uz upozorenja" })).toBeInTheDocument();
    expect(screen.getByText("Dostupni su delimični signali.")).toBeInTheDocument();
    expect(screen.getByText(/Unknown ostaje unknown/i)).toBeInTheDocument();
    expect(screen.getByTestId("analytics-trust-header")).toHaveTextContent("status: warning");
    expect(screen.getByTestId("analytics-trust-header")).toHaveTextContent("partial: true");
    expect(screen.queryByTestId("analytics-error-state")).not.toBeInTheDocument();

    const loadedDataCard = screen.getByText("Podaci učitani").closest("article");
    expect(loadedDataCard).not.toBeNull();
    expect(within(loadedDataCard as HTMLElement).getByText("Spremno")).toBeInTheDocument();
  });

  it("shows an explicit unknown empty state when every endpoint returns no confirmed source", async () => {
    vi.mocked(getDashboardBootstrap).mockResolvedValue(null as never);
    vi.mocked(getAnalyticsRefreshStatus).mockResolvedValue(null as never);
    vi.mocked(getAnalyticsDataQualityHealth).mockResolvedValue(null as never);
    vi.mocked(getPilotDataQualityIntakeReport).mockResolvedValue(null as never);
    vi.mocked(getProductDecisionCenter).mockResolvedValue(null as never);
    vi.mocked(getAnalyticsActionCounts).mockResolvedValue(null as never);
    vi.mocked(getAnalyticsActionOutcomeSummary).mockResolvedValue(null as never);
    vi.mocked(getPilotIntakeDurableReport).mockResolvedValue(null as never);
    vi.mocked(getSupplierDecisionDurableReport).mockResolvedValue(null as never);

    renderPage();

    expect(await screen.findByRole("heading", { name: "Spremnost nije potvrđena" })).toBeInTheDocument();
    expect(screen.getByTestId("analytics-empty-state")).toHaveTextContent("Nema potvrđenih readiness signala");
    expect(screen.getByText(/Unknown nikad ne znači zeleno/i)).toBeInTheDocument();
    expect(screen.getByTestId("analytics-trust-header")).toHaveTextContent("status: insufficient_data");
  });

  it("shows global error when every source fails and retries all readiness calls", async () => {
    const mocks = [
      vi.mocked(getDashboardBootstrap),
      vi.mocked(getAnalyticsRefreshStatus),
      vi.mocked(getAnalyticsDataQualityHealth),
      vi.mocked(getPilotDataQualityIntakeReport),
      vi.mocked(getProductDecisionCenter),
      vi.mocked(getAnalyticsActionCounts),
      vi.mocked(getAnalyticsActionOutcomeSummary),
      vi.mocked(getPilotIntakeDurableReport),
      vi.mocked(getSupplierDecisionDurableReport),
    ];

    mocks.forEach((mock) => mock.mockRejectedValueOnce(new Error("Readiness source unavailable")));
    configureReady();

    renderPage();

    expect(await screen.findByTestId("analytics-error-state")).toHaveTextContent("Readiness source unavailable");
    fireEvent.click(screen.getByRole("button", { name: "Ponovo proveri" }));

    expect(await screen.findByRole("heading", { name: "Spremno za demo" })).toBeInTheDocument();
    await waitFor(() => mocks.forEach((mock) => expect(mock).toHaveBeenCalledTimes(2)));
  });
});

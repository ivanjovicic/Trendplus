import { describe, expect, it } from "vitest";
import type {
  AnalyticsActionCounts,
  AnalyticsActionOutcomeSummaryResponse,
  AnalyticsDashboardBootstrap,
  AnalyticsRefreshStatus,
  AnalyticsResponseMeta,
  PilotDataQualityIntakeReport,
  PilotIntakeDurableReport,
  ProductDecisionCenterResponse,
  SupplierDecisionDurableReport,
} from "../../types/analytics";
import { buildPilotReadinessCards, type ReadinessPayload } from "../PilotReadinessPage";

function makeMeta(overrides: Partial<AnalyticsResponseMeta> = {}): AnalyticsResponseMeta {
  return {
    success: true,
    generatedAtUtc: "2026-06-16T09:00:00Z",
    dataQualityStatus: "good",
    ...overrides,
  };
}

const emptyPayload: ReadinessPayload = {
  bootstrap: null,
  refreshStatus: null,
  dataQualityHealth: null,
  intakeReport: null,
  productDecisionCenter: null,
  actionCounts: null,
  actionOutcomeSummary: null,
  pilotReport: null,
  supplierReport: null,
  errors: [],
};

const readyBootstrap = {
  summary: {
    totalRevenue: 120_000,
    totalTransactions: 24,
    totalUnits: 48,
    avgBasketValue: 5_000,
    avgItemPrice: 2_500,
  },
  inventory: {
    totalSkuCount: 12,
    totalOnHand: 80,
    lowStockCount: 0,
    outOfStockCount: 0,
    estimatedInventoryValue: 500_000,
    meta: makeMeta(),
  },
  dailySales: [
    {
      date: "2026-06-15",
      totalRevenue: 120_000,
      transactionCount: 24,
      totalUnits: 48,
    },
  ],
  categoryData: [],
  genderData: [],
  supplierData: [
    {
      dobavljacId: 1,
      dobavljacNaziv: "Dobavljač A",
      totalRevenue: 72_000,
      totalUnits: 30,
      transactionCount: 12,
    },
  ],
  supplierOptions: [
    {
      supplierId: 1,
      supplierName: "Dobavljač A",
    },
  ],
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
    totalMarginContributionRsd: 25_000,
    inventoryDangerValueRsd: 0,
    topSuppliers: [
      {
        supplierId: 1,
        supplierName: "Dobavljač A",
        revenue: 72_000,
        marginContribution: 15_000,
        link: "/analytics/supplier",
      },
    ],
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
  meta: makeMeta(),
} as AnalyticsDashboardBootstrap;

const readyRefreshStatus = {
  lastSuccessfulRefreshAtUtc: "2026-06-15T10:00:00Z",
  lastAttemptAtUtc: "2026-06-15T10:00:00Z",
  lastFailureAtUtc: null,
  isRunning: false,
  lastErrorMessage: null,
  currentStep: null,
  refreshedObjects: ["dashboard/bootstrap"],
  failedObjects: [],
  durationSeconds: 42,
  dataFreshnessStatus: "fresh",
  processMode: "worker",
  processType: "worker",
  workersEnabled: true,
  workerWarning: null,
  workerProcessWarning: null,
  cacheMode: "redis",
  isDistributed: true,
  lastAnalyticsCacheClearAtUtc: null,
  lastReportCacheClearAtUtc: null,
  cacheWarning: null,
  generatedAtUtc: "2026-06-15T10:00:00Z",
  jobs: [],
  recentRuns: [],
} as AnalyticsRefreshStatus;

const readyIntakeReport = {
  generatedAtUtc: "2026-06-15T10:00:00Z",
  periodFromUtc: "2026-06-01T00:00:00Z",
  periodToUtc: "2026-06-15T23:59:59Z",
  dataScope: "all",
  storeId: null,
  supplierId: null,
  lastImportAtUtc: "2026-06-15T09:30:00Z",
  lastImportStatus: "completed",
  lastImportScope: "global",
  lastRefreshAtUtc: "2026-06-15T10:00:00Z",
  readinessStatus: "good",
  readinessLabel: "Spremno",
  readinessScore: 95,
  loadedData: {
    articlesCount: 12,
    saleItemsCount: 24,
    receiptsCount: 8,
    suppliersCount: 1,
    storesCount: 1,
    firstSaleDate: "2026-06-01",
    lastSaleDate: "2026-06-15",
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
  meta: makeMeta(),
} as PilotDataQualityIntakeReport;

const readyProductDecisionCenter = {
  generatedAtUtc: "2026-06-15T10:00:00Z",
  periodFromUtc: "2026-06-01T00:00:00Z",
  periodToUtc: "2026-06-15T23:59:59Z",
  totalRows: 1,
  analyzedRows: 1,
  ignoredRowsCount: 0,
  summary: {
    replenishCount: 1,
    markdownCount: 0,
    highPotentialCount: 0,
    badDataCount: 0,
    lostSalesEstimate: 0,
    slowStockCapital: 0,
  },
  rows: [
    {
      productId: 1,
      sku: "SKU-1",
      productName: "Model 1",
      supplierId: 1,
      supplierName: "Dobavljač A",
      category: "Obuća",
      tipObuce: "Patike",
      color: "Crna",
      size: "42",
      revenue: 1_000,
      unitsSold: 4,
      velocityUnitsPerDay: 0.5,
      marginContribution: 400,
      marginPct: 0.4,
      marginQualityLabel: "good",
      marginCoveragePct: 100,
      currentStock: 10,
      minStock: 4,
      stockGap: 0,
      daysSinceLastSale: 2,
      trendPct: 10,
      lostSalesEstimate: 0,
      slowStockCapital: 0,
      dataQualityStatus: "good",
      confidencePct: 90,
      reliabilityPct: 95,
      recommendationStatus: "REPLENISH",
      recommendationLabel: "Dopuni",
      recommendationReason: "Spremno",
      reasonCodes: [],
      recommendedAction: "Dopuni",
    },
  ],
  meta: makeMeta(),
} as ProductDecisionCenterResponse;

const readyActionCounts: AnalyticsActionCounts = {
  new: 1,
  accepted: 0,
  deferred: 0,
  rejected: 0,
  done: 0,
  p1Open: 1,
};

const readyActionOutcomeSummary = {
  meta: {
    success: true,
    periodMode: "created",
    createdFrom: null,
    createdTo: null,
    resolvedFrom: null,
    resolvedTo: null,
    measuredFrom: null,
    measuredTo: null,
    generatedAtUtc: "2026-06-15T10:00:00Z",
    sampleSize: 1,
    measuredSampleSize: 1,
    warnings: [],
    emptyReason: null,
  },
  totals: {
    createdCount: 1,
    closedCount: 0,
    openCount: 1,
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
  impact: {
    expectedImpactRsd: 1_000,
    measuredImpactRsd: 900,
    realizationRatio: 0.9,
    measuredImpactSampleCount: 1,
  },
  bySourceType: [],
  byPriority: [],
  byOutcomeStatus: [],
  byDataQuality: [],
  byConfidenceBucket: [],
  byReliabilityBucket: [],
} as AnalyticsActionOutcomeSummaryResponse;

const readyPilotReport = {
  reportId: "pilot-report-1",
  stableQueryUrl: "/analytics/reports/pilot-intake",
  reportTitle: "Pilot intake",
  reportType: "pilot-intake",
  generatedAtUtc: "2026-06-15T10:00:00Z",
  periodFrom: "2026-06-01",
  periodTo: "2026-06-15",
  period: {
    fromUtc: "2026-06-01T00:00:00Z",
    toUtc: "2026-06-15T23:59:59Z",
    label: "June 2026",
  },
  lastRefreshAtUtc: "2026-06-15T10:00:00Z",
  dataFreshnessStatus: "fresh",
  dataQualityStatus: "good",
  recommendationAllowed: true,
  usedFallback: false,
  warnings: [],
  methodology: "Metodologija readiness reporta.",
  rows: [
    {
      section: "Sažetak",
      item: "Status",
      value: "Spremno",
    },
  ],
  sections: [
    {
      key: "summary",
      title: "Sažetak",
      rowCount: 1,
      description: "Summary section",
      emptyMessage: null,
      columns: [],
      rows: [
        {
          section: "Sažetak",
          item: "Status",
          value: "Spremno",
        },
      ],
    },
  ],
  payload: {
    tableKey: "pilot-intake",
    tableTitle: "Pilot intake",
    documentType: "pilot-intake",
    templateName: "analytics-table-default",
    locale: "sr-RS",
    columns: [],
    rows: [
      {
        section: "Sažetak",
        item: "Status",
        value: "Spremno",
      },
    ],
    filters: [],
    metadata: [],
    templateVersion: 1,
  },
  meta: makeMeta(),
} as PilotIntakeDurableReport;

const readySupplierReport = {
  reportId: "supplier-report-1",
  stableQueryUrl: "/analytics/reports/supplier-decision",
  reportTitle: "Supplier decision",
  reportType: "supplier-decision",
  generatedAtUtc: "2026-06-15T10:00:00Z",
  periodFrom: "2026-06-01",
  periodTo: "2026-06-15",
  period: {
    fromUtc: "2026-06-01T00:00:00Z",
    toUtc: "2026-06-15T23:59:59Z",
    label: "June 2026",
  },
  lastRefreshAtUtc: "2026-06-15T10:00:00Z",
  dataFreshnessStatus: "fresh",
  dataQualityStatus: "good",
  recommendationAllowed: true,
  usedFallback: false,
  fallbackReason: null,
  warnings: [],
  methodology: "Metodologija supplier reporta.",
  rows: [
    {
      section: "Sažetak",
      item: "Status",
      value: "Spremno",
    },
  ],
  sections: [
    {
      key: "summary",
      title: "Sažetak",
      rowCount: 1,
      description: "Summary section",
      emptyMessage: null,
      columns: [],
      rows: [
        {
          section: "Sažetak",
          item: "Status",
          value: "Spremno",
        },
      ],
    },
  ],
  kpis: [],
  recommendedActions: [],
  methodologySummary: "Supplier readiness methodology.",
  payload: {
    tableKey: "supplier-decision",
    tableTitle: "Supplier decision",
    documentType: "supplier-decision",
    templateName: "analytics-table-default",
    locale: "sr-RS",
    columns: [],
    rows: [
      {
        section: "Sažetak",
        item: "Status",
        value: "Spremno",
      },
    ],
    filters: [],
    metadata: [],
    templateVersion: 1,
  },
  meta: makeMeta(),
} as SupplierDecisionDurableReport;

const readyPayload: ReadinessPayload = {
  bootstrap: readyBootstrap,
  refreshStatus: readyRefreshStatus,
  dataQualityHealth: null,
  intakeReport: readyIntakeReport,
  productDecisionCenter: readyProductDecisionCenter,
  actionCounts: readyActionCounts,
  actionOutcomeSummary: readyActionOutcomeSummary,
  pilotReport: readyPilotReport,
  supplierReport: readySupplierReport,
  errors: [],
};

function collectStatuses(payload: ReadinessPayload) {
  return Object.fromEntries(buildPilotReadinessCards(payload).map((card) => [card.key, card.status]));
}

describe("buildPilotReadinessCards", () => {
  it("keeps every checklist item unknown when all sources are missing", () => {
    expect(collectStatuses(emptyPayload)).toEqual({
      "data-quality": "unknown",
      refresh: "unknown",
      sales: "unknown",
      products: "unknown",
      supplier: "unknown",
      inventory: "unknown",
      actions: "unknown",
      reports: "unknown",
    });
  });

  it("marks the full pilot signal set as ready", () => {
    expect(collectStatuses(readyPayload)).toEqual({
      "data-quality": "ready",
      refresh: "ready",
      sales: "ready",
      products: "ready",
      supplier: "ready",
      inventory: "ready",
      actions: "ready",
      reports: "ready",
    });
  });
});

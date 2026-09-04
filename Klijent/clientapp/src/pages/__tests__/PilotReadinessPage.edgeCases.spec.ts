import { describe, expect, it } from "vitest";
import { buildPilotReadinessCards, resolveReadinessHeaderContext, type ReadinessPayload } from "../PilotReadinessPage";
import type {
  AnalyticsActionCounts,
  AnalyticsDataQualityHealth,
  AnalyticsRefreshStatus,
  PilotDataQualityIntakeReport,
  PilotIntakeDurableReport,
  ProductDecisionCenterResponse,
  SupplierDecisionDurableReport,
} from "../../types/analytics";

function payload(overrides: Partial<ReadinessPayload> = {}): ReadinessPayload {
  return {
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
    ...overrides,
  };
}

function findCard(value: ReadinessPayload, key: string) {
  const card = buildPilotReadinessCards(value).find((item) => item.key === key);
  expect(card).toBeDefined();
  return card!;
}

function intake(overrides: Partial<PilotDataQualityIntakeReport> = {}): PilotDataQualityIntakeReport {
  return {
    generatedAtUtc: "2026-07-01T08:00:00Z",
    periodFromUtc: "2026-06-01T00:00:00Z",
    periodToUtc: "2026-07-01T00:00:00Z",
    dataScope: "all",
    storeId: null,
    supplierId: null,
    lastImportAtUtc: "2026-07-01T07:30:00Z",
    lastImportStatus: "completed",
    lastImportScope: "global",
    lastRefreshAtUtc: "2026-07-01T08:00:00Z",
    readinessStatus: "good",
    readinessLabel: "Spremno",
    readinessScore: 95,
    loadedData: {
      articlesCount: 100,
      saleItemsCount: 500,
      receiptsCount: 80,
      suppliersCount: 10,
      storesCount: 2,
      firstSaleDate: "2026-01-01",
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
    meta: { success: true, dataQualityStatus: "good" },
    ...overrides,
  };
}

function refresh(overrides: Partial<AnalyticsRefreshStatus> = {}): AnalyticsRefreshStatus {
  return {
    lastSuccessfulRefreshAtUtc: "2026-07-01T08:00:00Z",
    lastAttemptAtUtc: "2026-07-01T08:00:00Z",
    lastFailureAtUtc: null,
    isRunning: false,
    lastErrorMessage: null,
    currentStep: null,
    refreshedObjects: [],
    failedObjects: [],
    durationSeconds: 10,
    dataFreshnessStatus: "fresh",
    processMode: "worker",
    processType: "worker",
    workersEnabled: true,
    workerWarning: null,
    workerProcessWarning: null,
    generatedAtUtc: "2026-07-01T08:00:00Z",
    jobs: [],
    recentRuns: [],
    ...overrides,
  };
}

function product(overrides: Partial<ProductDecisionCenterResponse> = {}): ProductDecisionCenterResponse {
  return {
    generatedAtUtc: "2026-07-01T08:00:00Z",
    periodFromUtc: "2026-06-01T00:00:00Z",
    periodToUtc: "2026-07-01T00:00:00Z",
    totalRows: 0,
    analyzedRows: 0,
    ignoredRowsCount: 0,
    summary: {
      replenishCount: 0,
      markdownCount: 0,
      highPotentialCount: 0,
      badDataCount: 0,
      lostSalesEstimate: 0,
      slowStockCapital: 0,
    },
    rows: [],
    meta: { success: true, dataQualityStatus: "good" },
    ...overrides,
  };
}

function pilotReport(overrides: Partial<PilotIntakeDurableReport> = {}): PilotIntakeDurableReport {
  return {
    reportId: "pilot",
    stableQueryUrl: "/analytics/reports/pilot-intake",
    reportTitle: "Pilot report",
    reportType: "pilot-intake",
    generatedAtUtc: "2026-07-01T08:00:00Z",
    periodFrom: "2026-06-01",
    periodTo: "2026-07-01",
    period: { fromUtc: "2026-06-01T00:00:00Z", toUtc: "2026-07-01T00:00:00Z", label: "Jun" },
    lastRefreshAtUtc: "2026-07-01T08:00:00Z",
    dataFreshnessStatus: "fresh",
    dataQualityStatus: "good",
    recommendationAllowed: true,
    usedFallback: false,
    warnings: [],
    methodology: "method",
    rows: [{ section: "summary", item: "status", value: "ready" }],
    sections: [{ key: "summary", title: "Summary", rowCount: 1, description: null, emptyMessage: null, columns: [], rows: [] }],
    payload: { tableKey: "pilot", tableTitle: "Pilot", columns: [], rows: [], filters: [], metadata: [] },
    meta: { success: true, dataQualityStatus: "good" },
    ...overrides,
  } as PilotIntakeDurableReport;
}

function supplierReport(overrides: Partial<SupplierDecisionDurableReport> = {}): SupplierDecisionDurableReport {
  return {
    reportId: "supplier",
    stableQueryUrl: "/analytics/reports/supplier-decision",
    reportTitle: "Supplier report",
    reportType: "supplier-decision",
    generatedAtUtc: "2026-07-01T08:00:00Z",
    periodFrom: "2026-06-01",
    periodTo: "2026-07-01",
    period: { fromUtc: "2026-06-01T00:00:00Z", toUtc: "2026-07-01T00:00:00Z", label: "Jun" },
    lastRefreshAtUtc: "2026-07-01T08:00:00Z",
    dataFreshnessStatus: "fresh",
    dataQualityStatus: "good",
    recommendationAllowed: true,
    usedFallback: false,
    fallbackReason: null,
    warnings: [],
    methodology: "method",
    rows: [{ section: "summary", item: "status", value: "ready" }],
    sections: [{ key: "summary", title: "Summary", rowCount: 1, description: null, emptyMessage: null, columns: [], rows: [] }],
    kpis: [],
    recommendedActions: [],
    methodologySummary: "method",
    payload: { tableKey: "supplier", tableTitle: "Supplier", columns: [], rows: [], filters: [], metadata: [] },
    meta: { success: true, dataQualityStatus: "good" },
    ...overrides,
  } as SupplierDecisionDurableReport;
}

describe("Pilot readiness edge-state mapping", () => {
  it("blocks data quality when issues actively block recommendations", () => {
    const report = intake({
      readinessStatus: "warning",
      issues: { ...intake().issues, missingCostCount: 4 },
      impact: { ...intake().impact, recommendationsBlockedCount: 2 },
    });

    const card = findCard(payload({ intakeReport: report }), "data-quality");

    expect(card.status).toBe("blocked");
    expect(card.reason).toContain("2 preporuka je blokirano");
  });

  it("does not call a perfect score ready when the decision gate blocks recommendations", () => {
    const report = intake({
      readinessStatus: "good",
      readinessLabel: "Spremno",
      readinessScore: 100,
      issues: { ...intake().issues, missingCostCount: 1087, missingCategoryCount: 12422 },
      impact: { ...intake().impact, recommendationsBlockedCount: 1087, insufficientSignalCount: 12402 },
    });

    const card = findCard(payload({ intakeReport: report }), "data-quality");

    expect(card.status).toBe("blocked");
    expect(card.reason).toContain("Preporuke nisu bezbedne");
    expect(card.reason).toContain("1.087");
    expect(card.reason).not.toContain("Readiness: Spremno (100)");
  });

  it("keeps the intake decision score instead of replacing it with the traffic health score", () => {
    const report = intake({ readinessScore: 42, readinessStatus: "critical", readinessLabel: "Kritično" });
    const health = {
      generatedAt: "2026-07-01T08:00:00Z",
      lookbackDays: 90,
      windowFrom: "2026-04-01T00:00:00Z",
      windowTo: "2026-07-01T00:00:00Z",
      orphanArticleCount: 0,
      totalRevenue: 100,
      missingCostRevenue: 0,
      missingCostRevenueSharePct: 0,
      unknownSupplierRevenue: 50,
      unknownSupplierRevenueSharePct: 10,
      score: 70,
      scoreStatus: "warning",
      scoreSummary: "Health signal",
      thresholds: { orphanArticleCount: 10, missingCostRevenueSharePct: 5, unknownSupplierRevenueSharePct: 3 },
      meta: { success: true, dataQualityStatus: "warning" },
    } as AnalyticsDataQualityHealth;

    const card = findCard(payload({ intakeReport: report, dataQualityHealth: health }), "data-quality");

    expect(card.reason).toContain("skor 42");
    expect(card.reason).not.toContain("(70)");
  });

  it("explains report quality in Serbian instead of exposing the backend code", () => {
    const card = findCard(payload({
      pilotReport: pilotReport({ dataQualityStatus: "warning" }),
      supplierReport: supplierReport(),
    }), "reports");

    expect(card.meta).toContain("Kvalitet: Oprez");
    expect(card.meta).not.toContain("warning");
  });

  it("blocks refresh when the last successful refresh is critically old", () => {
    const card = findCard(payload({
      refreshStatus: refresh({ lastSuccessfulRefreshAtUtc: "2000-01-01T00:00:00Z" }),
    }), "refresh");

    expect(card.status).toBe("blocked");
    expect(card.reason).toContain("Poslednji uspešan refresh");
  });

  it("warns when workers are disabled even if freshness is reported as fresh", () => {
    const card = findCard(payload({
      refreshStatus: refresh({ workersEnabled: false, workerWarning: "Workers disabled" }),
    }), "refresh");

    expect(card.status).toBe("warning");
    expect(card.meta).toContain("Workers disabled");
  });

  it("blocks an empty product decision response instead of treating zero rows as healthy", () => {
    const card = findCard(payload({
      productDecisionCenter: product({
        meta: {
          success: true,
          dataQualityStatus: "insufficient_data",
          emptyReason: "Nema dovoljno istorije prodaje.",
        },
      }),
    }), "products");

    expect(card.status).toBe("blocked");
    expect(card.reason).toContain("Nema dovoljno istorije prodaje");
  });

  it("distinguishes unavailable action data from a confirmed empty action queue", () => {
    const unavailable = findCard(payload(), "actions");
    const emptyCounts: AnalyticsActionCounts = { new: 0, accepted: 0, deferred: 0, rejected: 0, done: 0, p1Open: 0 };
    const confirmedEmpty = findCard(payload({ actionCounts: emptyCounts }), "actions");

    expect(unavailable.status).toBe("unknown");
    expect(confirmedEmpty.status).toBe("warning");
    expect(confirmedEmpty.reason).toContain("Red akcija je prazan");
  });

  it("warns on report fallback and blocks critical report quality", () => {
    const fallback = findCard(payload({
      pilotReport: pilotReport({ usedFallback: true }),
      supplierReport: supplierReport(),
    }), "reports");
    const critical = findCard(payload({
      pilotReport: pilotReport({ dataQualityStatus: "critical" }),
      supplierReport: supplierReport(),
    }), "reports");

    expect(fallback.status).toBe("warning");
    expect(critical.status).toBe("blocked");
  });

  it("keeps requested readiness period and separately labels observed data lineage", () => {
    const context = resolveReadinessHeaderContext(payload({
      intakeReport: intake({
        periodFromUtc: "2026-06-01T00:00:00Z",
        periodToUtc: "2026-06-30T00:00:00Z",
        loadedData: {
          ...intake().loadedData,
          firstSaleDate: "2026-01-10T00:00:00Z",
          lastSaleDate: "2026-06-29T00:00:00Z",
        },
      }),
    }));

    expect(context.periodFrom).toBe("2026-06-01T00:00:00Z");
    expect(context.periodTo).toBe("2026-06-30T00:00:00Z");
    expect(context.effectivePeriodLabel).toContain("Posmatrani podaci");
    expect(context.effectivePeriodLabel).toContain("Efektivni");
  });
});

import { describe, expect, it } from "vitest";
import type { AnalyticsRefreshStatus, PilotDataQualityIntakeReport } from "../../types/analytics";
import { computePilotImportReadiness } from "../pilotImportReadiness";

function buildReport(overrides: Partial<PilotDataQualityIntakeReport> = {}): PilotDataQualityIntakeReport {
  return {
    generatedAtUtc: "2026-06-14T10:00:00Z",
    periodFromUtc: "2026-06-01T00:00:00Z",
    periodToUtc: "2026-06-14T23:59:59Z",
    dataScope: "all",
    storeId: null,
    supplierId: null,
    lastImportAtUtc: "2026-06-14T09:00:00Z",
    lastImportStatus: "completed",
    lastImportScope: "global",
    lastImportBatchId: 7,
    lastRefreshAtUtc: "2026-06-14T09:30:00Z",
    readinessStatus: "excellent",
    readinessLabel: "Spremno za pouzdanu analitiku",
    readinessScore: 95,
    loadedData: {
      articlesCount: 120,
      saleItemsCount: 340,
      receiptsCount: 210,
      suppliersCount: 18,
      storesCount: 3,
      firstSaleDate: "2026-06-01T08:00:00Z",
      lastSaleDate: "2026-06-14T18:00:00Z",
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

function buildRefreshStatus(overrides: Partial<AnalyticsRefreshStatus> = {}): AnalyticsRefreshStatus {
  return {
    lastSuccessfulRefreshAtUtc: "2026-06-14T09:30:00Z",
    lastAttemptAtUtc: "2026-06-14T09:30:00Z",
    lastFailureAtUtc: null,
    isRunning: false,
    lastErrorMessage: null,
    currentStep: null,
    refreshedObjects: ["analytics"],
    failedObjects: [],
    durationSeconds: 42,
    dataFreshnessStatus: "fresh",
    processMode: "worker",
    processType: "worker",
    workersEnabled: true,
    cacheMode: "redis",
    isDistributed: true,
    generatedAtUtc: "2026-06-14T09:31:00Z",
    jobs: [],
    ...overrides,
  };
}

describe("computePilotImportReadiness", () => {
  it("marks a healthy report as ready", () => {
    const result = computePilotImportReadiness(buildReport(), buildRefreshStatus());

    expect(result.status).toBe("ready");
    expect(result.label).toBe("Spremno");
    expect(result.reasons).toHaveLength(0);
  });

  it("downgrades stale freshness to ready_with_warnings", () => {
    const result = computePilotImportReadiness(
      buildReport({ readinessStatus: "good", readinessLabel: "Upotrebljivo uz upozorenja" }),
      buildRefreshStatus({ dataFreshnessStatus: "stale" })
    );

    expect(result.status).toBe("ready_with_warnings");
    expect(result.reasons.some((reason) => reason.includes("zastarelo"))).toBe(true);
  });

  it("marks critical backend readiness as not ready", () => {
    const result = computePilotImportReadiness(
      buildReport({ readinessStatus: "critical", readinessLabel: "Prvo srediti podatke" }),
      buildRefreshStatus()
    );

    expect(result.status).toBe("not_ready");
    expect(result.label).toBe("Nije spremno");
  });

  it("returns unknown when report is missing", () => {
    const result = computePilotImportReadiness(null, buildRefreshStatus());

    expect(result.status).toBe("unknown");
    expect(result.label).toBe("Nepoznato");
    expect(result.nextActions.some((action) => action.includes("import"))).toBe(true);
  });

  it("marks failed latest import as not_ready even when timestamp exists", () => {
    const result = computePilotImportReadiness(
      buildReport({ lastImportStatus: "failed" }),
      buildRefreshStatus()
    );

    expect(result.status).toBe("not_ready");
    expect(result.reasons.some((reason) => reason.includes("nije uspeo"))).toBe(true);
  });

  it("marks cancelled latest import as not_ready", () => {
    const result = computePilotImportReadiness(
      buildReport({ lastImportStatus: "cancelled" }),
      buildRefreshStatus()
    );

    expect(result.status).toBe("not_ready");
  });

  it("marks running import as ready_with_warnings", () => {
    const result = computePilotImportReadiness(
      buildReport({ lastImportStatus: "running" }),
      buildRefreshStatus()
    );

    expect(result.status).toBe("ready_with_warnings");
    expect(result.reasons.some((reason) => reason.includes("nije potvrdio"))).toBe(true);
  });

  it("treats timestamp without status as warning, not ready", () => {
    const result = computePilotImportReadiness(
      buildReport({ lastImportStatus: null }),
      buildRefreshStatus()
    );

    expect(result.status).toBe("ready_with_warnings");
    expect(result.reasons.some((reason) => reason.includes("nije poznat"))).toBe(true);
  });

  it("reads lastImportStatus from report when third argument is omitted", () => {
    const result = computePilotImportReadiness(
      buildReport({ lastImportStatus: "failed" }),
      buildRefreshStatus()
    );

    expect(result.status).toBe("not_ready");
  });

  it("warns when import scope is global under store filter", () => {
    const result = computePilotImportReadiness(
      buildReport({ storeId: "12", lastImportScope: "global", lastImportStatus: "completed" }),
      buildRefreshStatus()
    );

    expect(result.status).toBe("ready_with_warnings");
    expect(result.reasons.some((reason) => reason.includes("globalan"))).toBe(true);
  });
});

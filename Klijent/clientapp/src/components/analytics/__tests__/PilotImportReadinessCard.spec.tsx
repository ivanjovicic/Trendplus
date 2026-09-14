import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import type { AnalyticsRefreshStatus, PilotDataQualityIntakeReport } from "../../../types/analytics";
import PilotImportReadinessCard from "../PilotImportReadinessCard";

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
    lastImportBatchId: 11,
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

function buildRefresh(): AnalyticsRefreshStatus {
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
  };
}

describe("PilotImportReadinessCard", () => {
  it("shows import status and scope signals", () => {
    render(
      <MemoryRouter>
        <PilotImportReadinessCard report={buildReport()} refreshStatus={buildRefresh()} />
      </MemoryRouter>
    );

    expect(screen.getByText(/Status importa: Završen/i)).toBeInTheDocument();
    expect(screen.getByText(/Scope importa: Svi podaci/i)).toBeInTheDocument();
    expect(screen.queryByText(/completed|global/i)).not.toBeInTheDocument();
    expect(screen.getByText("Spremno")).toBeInTheDocument();
  });

  it("does not present failed import as ready", () => {
    render(
      <MemoryRouter>
        <PilotImportReadinessCard report={buildReport({ lastImportStatus: "failed" })} refreshStatus={buildRefresh()} />
      </MemoryRouter>
    );

    expect(screen.getByText("Nije spremno")).toBeInTheDocument();
    expect(screen.getByText(/Status importa: Neuspešan/i)).toBeInTheDocument();
    expect(screen.getByText(/Poslednji import nije uspeo/i)).toBeInTheDocument();
  });

  it("maps missing and future values without exposing backend tokens", () => {
    render(
      <MemoryRouter>
        <PilotImportReadinessCard
          report={buildReport({ lastImportStatus: "future_status_v2", lastImportScope: null, readinessStatus: "future_readiness_v2" })}
          refreshStatus={buildRefresh()}
        />
      </MemoryRouter>
    );

    expect(screen.getByText(/Status importa: Nije mapirano/i)).toBeInTheDocument();
    expect(screen.getByText(/Scope importa: Nepoznato/i)).toBeInTheDocument();
    expect(screen.getByText(/Oznaka spremnosti: Nije mapirano/i)).toBeInTheDocument();
    expect(screen.queryByText(/future_status_v2|future_readiness_v2/i)).not.toBeInTheDocument();
  });

  it("shows the same unavailable impact state used by readiness reasoning", () => {
    render(
      <MemoryRouter>
        <PilotImportReadinessCard
          report={buildReport({
            impact: {
              ...buildReport().impact,
              articlesWithoutSupplierPercent: Number.NaN,
            },
          })}
          refreshStatus={buildRefresh()}
        />
      </MemoryRouter>
    );

    expect(screen.getByText("Artikli bez dobavljača: Nije dostupno")).toBeInTheDocument();
    expect(screen.queryByText(/Artikli bez dobavljača: 0%/i)).not.toBeInTheDocument();
  });
});

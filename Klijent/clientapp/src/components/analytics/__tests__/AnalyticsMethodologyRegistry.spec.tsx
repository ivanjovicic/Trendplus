import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { MemoryRouter } from "react-router-dom";
import SupplierDecisionReport from "../SupplierDecisionReport";
import PilotDataQualityIntakeReport, { impactSignalState, issueSignalState } from "../PilotDataQualityIntakeReport";
import type { ResolvedAnalyticsTablePayload } from "../../../types/analyticsTable";
import type { PilotDataQualityIntakeReport as PilotReport } from "../../../types/analytics";

function createSupplierPayload(): ResolvedAnalyticsTablePayload {
  return {
    tableKey: "supplier-report",
    tableTitle: "Supplier report",
    columns: [],
    filters: [],
    metadata: [{ key: "dataQualityStatus", label: "DQ", value: "good" }],
    rows: [
      { section: "Header", item: "Dobavljač", value: "Test Dobavljač" },
      { section: "Header", item: "Period", value: "2026-05-01 - 2026-05-25" },
      { section: "KPI", item: "Prihod", value: "1.000.000" },
    ],
    locale: "sr-RS",
  };
}

function createSupplierAsciiSectionPayload(): ResolvedAnalyticsTablePayload {
  return {
    tableKey: "supplier-report-ascii",
    tableTitle: "Supplier report ascii",
    columns: [],
    filters: [],
    metadata: [{ key: "dataQualityStatus", label: "DQ", value: "good" }],
    rows: [
      { section: "Header", item: "Dobavljac", value: "ASCII Dobavljac" },
      { section: "Header", item: "Period", value: "2026-05-01 - 2026-05-25" },
      { section: "KPI", item: "Prihod", value: "1.000.000" },
      { section: "Top artikli / dobavljaci", item: "Top Supplier", value: "500.000 RSD", secondary: "Marza 35,0%" },
      { section: "Pojacaj", item: "Signal Supplier", value: "400.000 RSD", note: "Jak signal" },
    ],
    locale: "sr-RS",
  };
}

function createPilotReport(overrides?: Partial<PilotReport>): PilotReport {
  const base: PilotReport = {
    generatedAtUtc: "2026-05-25T08:00:00Z",
    periodFromUtc: "2026-04-25T00:00:00Z",
    periodToUtc: "2026-05-25T23:59:59Z",
    dataScope: "all",
    lastImportAtUtc: "2026-05-25T06:00:00Z",
    lastRefreshAtUtc: "2026-05-25T07:30:00Z",
    readinessStatus: "warning",
    readinessLabel: "Upotrebljivo uz upozorenja",
    readinessScore: 76,
    loadedData: {
      articlesCount: 1000,
      saleItemsCount: 5000,
      receiptsCount: 1200,
      suppliersCount: 35,
      storesCount: 8,
      firstSaleDate: "2025-01-01T00:00:00Z",
      lastSaleDate: "2026-05-24T00:00:00Z",
    },
    issues: {
      missingSupplierCount: 10,
      missingCostCount: 40,
      missingCategoryCount: 12,
      missingColorCount: 9,
      missingSizeCount: 14,
      saleWithoutArticleCount: 2,
      zeroOrNegativePriceCount: 1,
      duplicateSkuCount: 3,
      missingSupplierNameCount: 4,
    },
    impact: {
      revenueWithoutCostPercent: 0.12,
      articlesWithoutSupplierPercent: 0.05,
      recommendationsBlockedCount: 55,
      ignoredRowsCount: 11,
      insufficientSignalCount: 17,
    },
    recommendedActions: ["Povežite dobavljače", "Dopunite nabavne cene"],
    meta: null,
  };

  return {
    ...base,
    ...overrides,
    loadedData: {
      ...base.loadedData,
      ...overrides?.loadedData,
    },
    issues: {
      ...base.issues,
      ...overrides?.issues,
    },
    impact: {
      ...base.impact,
      ...overrides?.impact,
    },
  };
}

describe("Analytics methodology registry usage", () => {
  it("renders supplier methodology from metric registry", () => {
    render(
      <MemoryRouter>
        <SupplierDecisionReport payload={createSupplierPayload()} />
      </MemoryRouter>
    );

    expect(screen.getByText("Metodologija")).toBeInTheDocument();
    expect(screen.getByText("Metodologija metrika")).toBeInTheDocument();
    expect(screen.getByText(/SUM\(prodajna_vrednost_stavke\)/i)).toBeInTheDocument();
    expect(screen.getByText(/Sales facts analytics/i)).toBeInTheDocument();
  });

  it("renders supplier sections from ascii payload aliases", () => {
    render(
      <MemoryRouter>
        <SupplierDecisionReport payload={createSupplierAsciiSectionPayload()} />
      </MemoryRouter>
    );

    expect(screen.getByText("Top Supplier")).toBeInTheDocument();
    expect(screen.getByText("Signal Supplier")).toBeInTheDocument();
    expect(screen.getByText("ASCII Dobavljac")).toBeInTheDocument();
  });

  it("renders pilot methodology from metric registry", () => {
    render(
      <MemoryRouter>
        <PilotDataQualityIntakeReport
          report={createPilotReport()}
          loading={false}
          error={null}
          filters={[]}
          durableReport={null}
          onRetry={() => {}}
        />
      </MemoryRouter>
    );

    expect(screen.getByText("Metodologija metrika")).toBeInTheDocument();
    expect(screen.getAllByText("Spremnost podataka").length).toBeGreaterThan(0);
    expect(screen.getByText(/Ponderisani skor kvaliteta master i transakcionih podataka/i)).toBeInTheDocument();
    expect(screen.getAllByText(/Data quality checks/i).length).toBeGreaterThan(0);
  });

  it("shows positive empty copy only when all issue and impact fields are explicitly zero", () => {
    const report = createPilotReport({
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
      meta: { success: true, dataQualityStatus: "good" },
    });

    expect(issueSignalState(report)).toBe("clear");
    expect(impactSignalState(report)).toBe("clear");
  });

  it("does not present missing or partial issue signals as a clean state", () => {
    const report = createPilotReport({
      issues: {
        missingSupplierCount: 0,
        missingCostCount: 0,
        missingCategoryCount: 0,
        missingColorCount: null as unknown as number,
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
        insufficientSignalCount: null as unknown as number,
      },
      meta: { success: true, dataQualityStatus: "warning", isPartial: true, warningCode: "partial_payload" },
    });

    expect(issueSignalState(report)).toBe("partial");
    expect(impactSignalState(report)).toBe("partial");
  });
});

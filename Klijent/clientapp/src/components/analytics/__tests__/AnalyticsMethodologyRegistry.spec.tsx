import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { MemoryRouter } from "react-router-dom";
import SupplierDecisionReport from "../SupplierDecisionReport";
import PilotDataQualityIntakeReport from "../PilotDataQualityIntakeReport";
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

function createPilotReport(): PilotReport {
  return {
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
}

describe("Analytics methodology registry usage", () => {
  it("renders supplier methodology from metric registry", () => {
    render(
      <MemoryRouter>
        <SupplierDecisionReport payload={createSupplierPayload()} />
      </MemoryRouter>
    );

    expect(screen.getByText("Metodologija")).toBeInTheDocument();
    expect(screen.getByText(/Suma polja ukupna_cena kroz sve prodajne stavke/i)).toBeInTheDocument();
    expect(screen.getByText(/MV: sales_facts_mv/i)).toBeInTheDocument();
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

    fireEvent.click(screen.getByRole("button", { name: "Kako čitati ovaj izveštaj?" }));
    expect(screen.getByText(/Definicije ključnih KPI-jeva se čitaju iz centralnog analytics registry-ja/i)).toBeInTheDocument();
    expect(screen.getByText(/Ponderisani skor kvaliteta signala/i)).toBeInTheDocument();
    expect(screen.getAllByText(/MV: analytics_data_quality_history/i).length).toBeGreaterThan(0);
  });
});

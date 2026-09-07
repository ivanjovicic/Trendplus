import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import type { PilotDataQualityIntakeReport } from "../../../types/analytics";
import PilotDataQualityIntakeReportPanel from "../PilotDataQualityIntakeReport";

function emptyIntakeReport(): PilotDataQualityIntakeReport {
  return {
    generatedAtUtc: "2026-09-07T08:00:00Z",
    periodFromUtc: "2026-09-01T00:00:00Z",
    periodToUtc: "2026-09-07T00:00:00Z",
    dataScope: "all",
    storeId: null,
    supplierId: null,
    lastImportAtUtc: "2026-09-07T07:30:00Z",
    lastImportStatus: "completed",
    lastImportScope: "global",
    lastImportBatchId: 42,
    lastRefreshAtUtc: "2026-09-07T07:45:00Z",
    readinessStatus: "insufficient_data",
    readinessLabel: "Nema dovoljno podataka za readiness procenu",
    readinessScore: 0,
    loadedData: {
      articlesCount: 0,
      saleItemsCount: 0,
      receiptsCount: 0,
      suppliersCount: 0,
      storesCount: 0,
      firstSaleDate: null,
      lastSaleDate: null,
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
      revenueWithoutCostPercent: null,
      articlesWithoutSupplierPercent: 0,
      recommendationsBlockedCount: 0,
      ignoredRowsCount: 0,
      insufficientSignalCount: 0,
    },
    recommendedActions: [],
    meta: {
      success: true,
      emptyReason: "no_intake_evidence",
      dataQualityStatus: "insufficient_data",
      message: "Nema dovoljno ucitanih artikala ili import redova za readiness procenu.",
    },
  };
}

describe("PilotDataQualityIntakeReport", () => {
  it("renders an insufficient-data state without a numeric empty score", () => {
    render(
      <MemoryRouter>
        <PilotDataQualityIntakeReportPanel
          report={emptyIntakeReport()}
          loading={false}
          error={null}
          filters={[]}
          onRetry={vi.fn()}
        />
      </MemoryRouter>,
    );

    expect(screen.getByRole("heading", { name: "Pilot intake izveštaj nema dovoljno podataka" })).toBeInTheDocument();
    expect(screen.getByText("Nema dovoljno ucitanih artikala ili import redova za readiness procenu.")).toBeInTheDocument();
    expect(screen.queryByText("0/100")).not.toBeInTheDocument();
  });
});

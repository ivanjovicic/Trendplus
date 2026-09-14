import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import type { PilotDataQualityIntakeReport } from "../../../types/analytics";
import PilotDataQualityIntakeReportPanel, {
  buildCsv,
  buildExportPayload,
  buildSummary,
} from "../PilotDataQualityIntakeReport";

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

  it("uses the same safe readiness/import labels in report, copied summary and exports", () => {
    const report = emptyIntakeReport();
    report.readinessStatus = "warning";
    report.lastImportStatus = "completed";
    report.lastImportScope = "global";

    const csv = buildCsv(report);
    const summary = buildSummary(report);
    const exportPayload = buildExportPayload(report, []);
    const exportText = JSON.stringify(exportPayload);

    for (const text of [csv, summary, exportText]) {
      expect(text).toContain("Upozorenje");
      expect(text).toContain("Završen");
      expect(text).toContain("Svi podaci");
      expect(text).not.toContain("warning");
      expect(text).not.toContain("completed");
      expect(text).not.toContain("global");
    }
  });

  it("keeps unknown and future values visibly unknown in every export surface", () => {
    const report = emptyIntakeReport();
    report.readinessStatus = "future_readiness_v2";
    report.lastImportStatus = "future_status_v2";
    report.lastImportScope = null;

    const surfaces = [
      buildCsv(report),
      buildSummary(report),
      JSON.stringify(buildExportPayload(report, [])),
    ];

    for (const text of surfaces) {
      expect(text).toContain("Nije mapirano");
      expect(text).toContain("Nepoznato");
      expect(text).not.toContain("future_readiness_v2");
      expect(text).not.toContain("future_status_v2");
    }
  });

  it("does not export a fake supplier zero for an empty intake", () => {
    const report = emptyIntakeReport();
    const surfaces = [
      buildCsv(report),
      buildSummary(report),
      JSON.stringify(buildExportPayload(report, [])),
    ];

    for (const text of surfaces) {
      expect(text).toContain("Nije dostupno");
      expect(text).not.toContain("Artikli bez dobavljača,0%");
      expect(text).not.toContain("artikli bez dobavljača 0%");
    }
  });

  it("exports the measured supplier zero consistently when the denominator is positive", () => {
    const report = emptyIntakeReport();
    report.loadedData.articlesCount = 10;
    report.impact.articlesWithoutSupplierPercent = 0;
    const surfaces = [
      buildCsv(report),
      buildSummary(report),
      JSON.stringify(buildExportPayload(report, [])),
    ];

    for (const text of surfaces) {
      expect(text).toContain("0%");
      expect(text).not.toContain("Artikli bez dobavljača,Nije dostupno");
      expect(text).not.toContain("artikli bez dobavljača Nije dostupno");
    }
  });
});

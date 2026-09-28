import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import type { PilotDataQualityIntakeReport, PilotIntakeDurableReport } from "../../../types/analytics";
import PilotDataQualityIntakeReportPanel, {
  buildCsv,
  buildDurableCsv,
  buildExportPayload,
  buildPilotCsvFilename,
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
    readinessLabel: "Nema dovoljno podataka za procenu spremnosti",
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
      message: "Nema dovoljno učitanih artikala ili redova uvoza za procenu spremnosti.",
    },
  };
}

function durableIntakeReport(overrides: Partial<PilotIntakeDurableReport> = {}): PilotIntakeDurableReport {
  return {
    reportId: "pilot-durable-1",
    stableQueryUrl: "/analytics/reports/pilot-intake?fromDate=2026-04-01&toDate=2026-06-30",
    reportTitle: "Trendplus pilot izveštaj kvaliteta podataka",
    reportType: "pilot-intake",
    generatedAtUtc: "2026-06-30T12:00:00Z",
    periodFrom: "2026-04-01",
    periodTo: "2026-06-30",
    period: { fromUtc: "2026-04-01", toUtc: "2026-06-30", label: "Pilot intake", scope: "all" },
    lastRefreshAtUtc: "2026-06-30T11:00:00Z",
    dataFreshnessStatus: "fresh",
    dataQualityStatus: "good",
    recommendationAllowed: true,
    usedFallback: false,
    warnings: ["Dopuni nabavne cene"],
    kpis: [
      { key: "readinessScore", label: "Spremnost za preporuke", value: 82, unit: "/100", tone: "positive" },
      { key: "articlesCount", label: "Artikli", value: 1234 },
    ],
    recommendedActions: [{ title: "Dopuni nabavne cene", description: "Proveri troškove.", href: "/analytics/data-quality", priority: "high" }],
    methodology: { summary: "Test metodologija", notes: ["Readiness test"] },
    rows: [{ section: "Učitano", item: "Artikli", value: "1234" }],
    sections: [{
      key: "loaded-counts",
      title: "Učitano",
      rowCount: 1,
      description: "Obuhvaćeni podaci.",
      columns: [{ key: "metric", label: "Metrika" }, { key: "value", label: "Vrednost", dataType: "number" }],
      rows: [{ metric: "Artikli", value: 1234 }],
    }],
    payload: {
      tableKey: "pilot-data-quality-intake",
      tableTitle: "Trendplus pilot izveštaj kvaliteta podataka",
      documentType: "pilot-intake",
      templateName: "analytics-table-default",
      locale: "sr-RS",
      columns: [],
      rows: [{ section: "Učitano", item: "Artikli", value: "1234" }],
      filters: [],
      metadata: [],
    },
    meta: { success: true, dataQualityStatus: "good" },
    ...overrides,
  };
}

describe("PilotDataQualityIntakeReport", () => {
  it("renders backend durable KPIs, sections and actions instead of the permanent empty state", () => {
    const report = durableIntakeReport();
    render(
      <MemoryRouter>
        <PilotDataQualityIntakeReportPanel
          report={null}
          durableReport={report}
          loading={false}
          error={null}
          filters={[]}
          onRetry={vi.fn()}
        />
      </MemoryRouter>,
    );

    expect(screen.getByTestId("pilot-durable-report")).toBeInTheDocument();
    expect(screen.getByText("Spremnost za preporuke")).toBeInTheDocument();
    expect(screen.getByText("82/100")).toBeInTheDocument();
    expect(screen.getByText("Učitano")).toBeInTheDocument();
    expect(screen.getAllByText("1.234").length).toBeGreaterThan(0);
    expect(screen.getByRole("link", { name: "Dopuni nabavne cene" })).toHaveAttribute("href", "/analytics/data-quality");
    expect(screen.queryByText(/redova/)).not.toBeInTheDocument();
  });

  it("renders durable empty state only when backend marks the report empty", () => {
    const report = durableIntakeReport({
      kpis: [],
      recommendedActions: [],
      sections: [{ key: "report-status", title: "Status reporta", rowCount: 1, rows: [{ status: "no_data_in_period", message: "Nema podataka u periodu" }] }],
      meta: { success: true, emptyReason: "no_data_in_period", message: "Nema podataka u periodu", dataQualityStatus: "insufficient_data" },
    });

    render(
      <MemoryRouter>
        <PilotDataQualityIntakeReportPanel
          report={null}
          durableReport={report}
          loading={false}
          error={null}
          filters={[]}
          onRetry={vi.fn()}
        />
      </MemoryRouter>,
    );

    expect(screen.getByRole("heading", { name: "Pilot izveštaj nema dovoljno podataka" })).toBeInTheDocument();
    expect(screen.queryByTestId("pilot-durable-report")).not.toBeInTheDocument();
  });

  it("exports durable rows with a UTF-8 BOM and no synthetic duplicate readiness row", () => {
    const csv = buildDurableCsv(durableIntakeReport());
    expect(csv.charCodeAt(0)).toBe(0xFEFF);
    expect(csv).toContain("Učitano,Artikli,1234");
    expect(csv.match(/Spremnost za preporuke/g) ?? []).toHaveLength(0);
    expect(buildPilotCsvFilename(null, durableIntakeReport())).toBe("pilot-intake-2026-06-30_2026-04-01_2026-06-30.csv");
  });

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

    expect(screen.getByRole("heading", { name: "Pilot izveštaj nema dovoljno podataka" })).toBeInTheDocument();
    expect(screen.getByText("Nema dovoljno učitanih artikala ili redova uvoza za procenu spremnosti.")).toBeInTheDocument();
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

  it("exports signal coverage and the explicit period anchor without turning unsold articles into a blocker", () => {
    const report = emptyIntakeReport();
    report.loadedData.articlesCount = 100;
    report.impact.insufficientSignalCount = 95;
    report.periodAnchorCode = "import_business_date_fallback";
    report.periodAnchorMessage = "Za izabrani filter nema prodaje; period je usidren na poslednji poslovni datum u uvezenom skupu podataka.";

    const surfaces = [
      buildCsv(report),
      buildSummary(report),
      JSON.stringify(buildExportPayload(report, [])),
    ];

    for (const text of surfaces) {
      expect(text).toContain("Pokrivenost poslovnim signalom");
      expect(text).toMatch(/5(?:[,.]0)?%/);
      expect(text).toContain("import_business_date_fallback");
    }
    expect(surfaces[1]).toContain("ne menja skor spremnosti");
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

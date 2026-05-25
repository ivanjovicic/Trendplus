import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import type { AnalyticsNamedValue } from "../../types/analyticsTable";
import type { PilotDataQualityIntakeReport, PilotIntakeDurableReport } from "../../types/analytics";
import { resolveAnalyticsTablePayload } from "../../services/analyticsTableState";
import { downloadExport, generateExport, waitForExport } from "../../services/exportApi";
import {
  getAnalyticsMetricDefinition,
  type AnalyticsMetricKey,
} from "../../utils/analyticsMetricDefinitions";
import {
  fmtNumber,
  fmtPct,
  formatDate,
  formatDateTime,
} from "../../utils/analyticsFormatters";
import KpiExplainButton from "./KpiExplainButton";
import AnalyticsEmptyState from "./AnalyticsEmptyState";
import AnalyticsErrorState from "./AnalyticsErrorState";
import "./PilotDataQualityIntakeReport.css";

type Props = {
  report: PilotDataQualityIntakeReport | null;
  loading: boolean;
  error: string | null;
  filters: AnalyticsNamedValue[];
  durableReport?: PilotIntakeDurableReport | null;
  onRetry: () => void;
};

function formatPercentFromRatio(value: number | null | undefined, digits = 1): string {
  if (value == null || Number.isNaN(value)) return "-";
  return fmtPct(value * 100, digits);
}

function readinessTone(status: string): "excellent" | "good" | "warning" | "critical" {
  if (status === "excellent") return "excellent";
  if (status === "good") return "good";
  if (status === "warning") return "warning";
  return "critical";
}

function mapActionHref(action: string): string {
  const normalized = action.toLowerCase();
  if (normalized.includes("dobavlj")) return "/analytics/supplier";
  if (normalized.includes("cena") || normalized.includes("kategor") || normalized.includes("map")) return "/analytics/data-quality";
  if (normalized.includes("osvez")) return "/admin/configuration?panel=workers";
  return "/analytics/data-quality";
}

function buildCsv(report: PilotDataQualityIntakeReport): string {
  const rows = [
    ["Sekcija", "Stavka", "Vrednost"],
    ["Skor", "Status spremnosti", report.readinessStatus],
    ["Skor", "Oznaka spremnosti", report.readinessLabel],
    ["Skor", "Skor spremnosti", String(report.readinessScore)],
    ["Učitano", "Artikli", String(report.loadedData.articlesCount)],
    ["Učitano", "Stavke prodaje", String(report.loadedData.saleItemsCount)],
    ["Učitano", "Računi", String(report.loadedData.receiptsCount)],
    ["Učitano", "Dobavljači", String(report.loadedData.suppliersCount)],
    ["Učitano", "Prodajni objekti", String(report.loadedData.storesCount)],
    ["Učitano", "Prva prodaja", report.loadedData.firstSaleDate ?? ""],
    ["Učitano", "Poslednja prodaja", report.loadedData.lastSaleDate ?? ""],
    ["Problemi", "Bez dobavljača", String(report.issues.missingSupplierCount)],
    ["Problemi", "Bez nabavne cene", String(report.issues.missingCostCount)],
    ["Problemi", "Bez kategorije", String(report.issues.missingCategoryCount)],
    ["Problemi", "Bez boje", String(report.issues.missingColorCount ?? 0)],
    ["Problemi", "Bez veličine", String(report.issues.missingSizeCount ?? 0)],
    ["Problemi", "Prodaja bez artikla", String(report.issues.saleWithoutArticleCount)],
    ["Problemi", "Nulta/negativna cena", String(report.issues.zeroOrNegativePriceCount)],
    ["Problemi", "Dupliran SKU", String(report.issues.duplicateSkuCount ?? 0)],
    ["Problemi", "Dobavljač bez naziva", String(report.issues.missingSupplierNameCount)],
    ["Uticaj", "Prihod bez cene", formatPercentFromRatio(report.impact.revenueWithoutCostPercent)],
    ["Uticaj", "Artikli bez dobavljača", formatPercentFromRatio(report.impact.articlesWithoutSupplierPercent)],
    ["Uticaj", "Blokirane preporuke", String(report.impact.recommendationsBlockedCount)],
    ["Uticaj", "Ignorisani redovi", String(report.impact.ignoredRowsCount)],
    ["Uticaj", "Nedovoljni signali", String(report.impact.insufficientSignalCount)],
  ];

  for (const action of report.recommendedActions) {
    rows.push(["Akcije", "Preporučena akcija", action]);
  }

  return rows
    .map((row) => row.map((value) => {
      if (/[",\n;]/.test(value)) return `"${value.replace(/"/g, '""')}"`;
      return value;
    }).join(","))
    .join("\n");
}

function buildSummary(report: PilotDataQualityIntakeReport): string {
  return [
    `Trendplus pilot izveštaj kvaliteta podataka`,
    `Skor spremnosti: ${report.readinessLabel} (${report.readinessScore}/100)`,
    `Učitano: ${fmtNumber(report.loadedData.articlesCount, 0, "-")} artikala, ${fmtNumber(report.loadedData.saleItemsCount, 0, "-")} stavki prodaje, ${fmtNumber(report.loadedData.receiptsCount, 0, "-")} računa`,
    `Top problemi: bez dobavljača ${fmtNumber(report.issues.missingSupplierCount, 0, "-")}, bez nabavne cene ${fmtNumber(report.issues.missingCostCount, 0, "-")}, bez kategorije ${fmtNumber(report.issues.missingCategoryCount, 0, "-")}`,
    `Uticaj: prihod bez cene ${formatPercentFromRatio(report.impact.revenueWithoutCostPercent)}, artikli bez dobavljača ${formatPercentFromRatio(report.impact.articlesWithoutSupplierPercent)}, blokirane preporuke ${fmtNumber(report.impact.recommendationsBlockedCount, 0, "-")}`,
    `Preporučene akcije: ${report.recommendedActions.join("; ")}`,
  ].join("\n");
}

function buildExportPayload(report: PilotDataQualityIntakeReport, filters: AnalyticsNamedValue[]) {
  const rows: Array<{ section: string; item: string; value: string }> = [
    { section: "Skor", item: "Status spremnosti", value: report.readinessStatus },
    { section: "Skor", item: "Oznaka spremnosti", value: report.readinessLabel },
    { section: "Skor", item: "Skor spremnosti", value: String(report.readinessScore) },
    { section: "Učitano", item: "Artikli", value: String(report.loadedData.articlesCount) },
    { section: "Učitano", item: "Stavke prodaje", value: String(report.loadedData.saleItemsCount) },
    { section: "Učitano", item: "Računi", value: String(report.loadedData.receiptsCount) },
    { section: "Učitano", item: "Dobavljači", value: String(report.loadedData.suppliersCount) },
    { section: "Učitano", item: "Prodajni objekti", value: String(report.loadedData.storesCount) },
    { section: "Učitano", item: "Prva prodaja", value: report.loadedData.firstSaleDate ?? "-" },
    { section: "Učitano", item: "Poslednja prodaja", value: report.loadedData.lastSaleDate ?? "-" },
    { section: "Problemi", item: "Bez dobavljača", value: String(report.issues.missingSupplierCount) },
    { section: "Problemi", item: "Bez nabavne cene", value: String(report.issues.missingCostCount) },
    { section: "Problemi", item: "Bez kategorije", value: String(report.issues.missingCategoryCount) },
    { section: "Problemi", item: "Bez boje", value: String(report.issues.missingColorCount ?? 0) },
    { section: "Problemi", item: "Bez veličine", value: String(report.issues.missingSizeCount ?? 0) },
    { section: "Problemi", item: "Prodaja bez artikla", value: String(report.issues.saleWithoutArticleCount) },
    { section: "Problemi", item: "Nulta/negativna cena", value: String(report.issues.zeroOrNegativePriceCount) },
    { section: "Problemi", item: "Dupliran SKU", value: String(report.issues.duplicateSkuCount ?? 0) },
    { section: "Problemi", item: "Dobavljač bez naziva", value: String(report.issues.missingSupplierNameCount) },
    { section: "Uticaj", item: "Prihod bez cene", value: formatPercentFromRatio(report.impact.revenueWithoutCostPercent) },
    { section: "Uticaj", item: "Artikli bez dobavljača", value: formatPercentFromRatio(report.impact.articlesWithoutSupplierPercent) },
    { section: "Uticaj", item: "Blokirane preporuke", value: String(report.impact.recommendationsBlockedCount) },
    { section: "Uticaj", item: "Ignorisani redovi", value: String(report.impact.ignoredRowsCount) },
    { section: "Uticaj", item: "Nedovoljni signali", value: String(report.impact.insufficientSignalCount) },
  ];

  for (const action of report.recommendedActions) {
    rows.push({ section: "Preporučene akcije", item: "Akcija", value: action });
  }

  return resolveAnalyticsTablePayload({
    tableKey: "pilot-data-quality-intake",
    tableTitle: "Trendplus pilot izveštaj kvaliteta podataka",
    documentType: "pilot-data-quality-intake",
    templateName: "analytics-table-default",
    columns: [
      { key: "section", header: "Sekcija", dataType: "text" as const },
      { key: "item", header: "Stavka", dataType: "text" as const },
      { key: "value", header: "Vrednost", dataType: "text" as const },
    ],
    rows,
    filters,
    metadata: [
      { key: "generatedAtUtc", label: "Generisano", value: report.generatedAtUtc },
      { key: "lastImportAtUtc", label: "Poslednji import", value: report.lastImportAtUtc ?? null },
      { key: "lastRefreshAtUtc", label: "Poslednje osveženje", value: report.lastRefreshAtUtc ?? null },
      { key: "dataScope", label: "Opseg podataka", value: report.dataScope },
    ],
    locale: "sr-RS",
  });
}

function normalizeColumnType(value: string | undefined) {
  return value === "number"
    || value === "currency"
    || value === "percent"
    || value === "date"
    || value === "datetime"
    || value === "text"
    ? value
    : "text";
}

function formatDurableValue(value: unknown): string {
  if (value == null) return "-";
  if (typeof value === "number") return fmtNumber(value, 0, "-");
  if (typeof value === "boolean") return value ? "Da" : "Ne";
  return String(value);
}

function durableMethodologySummary(report: PilotIntakeDurableReport): string {
  if (typeof report.methodology === "string") return report.methodology;
  return report.methodologySummary ?? report.methodology.summary;
}

function durableSectionRowCount(section: PilotIntakeDurableReport["sections"][number]): number {
  if (typeof section.rowCount === "number") return section.rowCount;
  return Array.isArray(section.rows) ? section.rows.length : 0;
}

function buildDurableCsv(report: PilotIntakeDurableReport): string {
  const rows = [
    ["Sekcija", "Stavka", "Vrednost"],
    ["Izveštaj", "Naslov", report.reportTitle ?? "Trendplus pilot izveštaj kvaliteta podataka"],
    ["Izveštaj", "Tip", report.reportType ?? "pilot-intake"],
    ["Izveštaj", "Generisano", report.generatedAtUtc],
    ["Izveštaj", "Period od", report.periodFrom ?? report.period?.fromUtc ?? "-"],
    ["Izveštaj", "Period do", report.periodTo ?? report.period?.toUtc ?? "-"],
    ["Izveštaj", "Poslednji refresh", report.lastRefreshAtUtc ?? "-"],
    ["Izveštaj", "Status kvaliteta podataka", report.dataQualityStatus],
    ["Izveštaj", "Preporuke dozvoljene", report.recommendationAllowed == null ? "-" : report.recommendationAllowed ? "Da" : "Ne"],
    ["Izveštaj", "Korišćen fallback", report.usedFallback == null ? "-" : report.usedFallback ? "Da" : "Ne"],
  ];

  for (const warning of report.warnings ?? []) {
    rows.push(["Upozorenja", "Upozorenje", warning]);
  }

  for (const section of report.sections) {
    rows.push(["Sekcije", section.title || section.key, String(durableSectionRowCount(section))]);
  }

  for (const row of report.rows) {
    rows.push([
      row.section || "Podaci",
      row.item,
      formatDurableValue(row.value),
    ]);
  }

  return rows
    .map((row) => row.map((value) => {
      if (/[",\n;]/.test(value)) return `"${value.replace(/"/g, '""')}"`;
      return value;
    }).join(","))
    .join("\n");
}

function buildDurableSummary(report: PilotIntakeDurableReport): string {
  const warnings = report.warnings && report.warnings.length > 0
    ? report.warnings.join("; ")
    : "nema";

  return [
    report.reportTitle ?? "Trendplus pilot izveštaj kvaliteta podataka",
    `Tip: ${report.reportType ?? "pilot-intake"}`,
    `Period: ${report.periodFrom ?? report.period?.fromUtc ?? "-"} - ${report.periodTo ?? report.period?.toUtc ?? "-"}`,
    `Poslednji refresh: ${report.lastRefreshAtUtc ?? "-"}`,
    `Status kvaliteta podataka: ${report.dataQualityStatus}`,
    `Preporuke dozvoljene: ${report.recommendationAllowed == null ? "-" : report.recommendationAllowed ? "Da" : "Ne"}`,
    `Korišćen fallback: ${report.usedFallback == null ? "-" : report.usedFallback ? "Da" : "Ne"}`,
    `Upozorenja: ${warnings}`,
    `Metodologija: ${durableMethodologySummary(report)}`,
  ].join("\n");
}

export default function PilotDataQualityIntakeReport({ report, loading, error, filters, durableReport, onRetry }: Props) {
  const [exportState, setExportState] = useState<string | null>(null);
  const [showMethodology, setShowMethodology] = useState(false);
  const methodologyKeys: AnalyticsMetricKey[] = [
    "dataReadiness",
    "revenueWithoutCost",
    "revenueUnknownSupplier",
    "totalRevenue",
    "marginContribution",
  ];

  const readiness = useMemo(() => readinessTone(report?.readinessStatus ?? "critical"), [report?.readinessStatus]);

  if (error && !durableReport && !report) {
    return (
      <AnalyticsErrorState
        title="Pilot intake report trenutno nije dostupan"
        message={error}
        suggestions={[
          "Proverite da li je import završen.",
          "Pokrenite osvežavanje analytics podataka.",
          "Pokušajte ponovo za nekoliko trenutaka.",
        ]}
        onRetry={onRetry}
        helpHref="/analytics/data-quality"
      />
    );
  }

  if (loading && !report && !durableReport) {
    return <div className="data-quality-loading">Učitavam pilot intake izveštaj...</div>;
  }

  if (!report && !durableReport) {
    return (
      <AnalyticsEmptyState
        variant="no_data"
        message="Pilot intake izveštaj nije moguće generisati za trenutni opseg."
        reasons={[
          "Import nije završen ili nema podataka u periodu.",
          "Filter opseg je previše uzak.",
        ]}
        actions={[
          { label: "Proširite period." },
          { label: "Proverite Data Quality", href: "/analytics/data-quality" },
          { label: "Proverite worker status", href: "/admin/configuration?panel=workers" },
        ]}
        dataQualityHref="/analytics/data-quality"
        refreshStatusHref="/admin/configuration?panel=workers"
      />
    );
  }

  const handlePrint = () => {
    window.print();
    setExportState("Otvoren je browser pregled za štampu.");
  };

  const handleCsv = () => {
    try {
      const csv = durableReport ? buildDurableCsv(durableReport) : buildCsv(report!);
      const blob = new Blob([csv], { type: "text/csv;charset=utf-8" });
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      const generatedAt = durableReport?.generatedAtUtc ?? report?.generatedAtUtc ?? new Date().toISOString();
      anchor.download = `pilot-data-quality-intake-${generatedAt.slice(0, 10)}.csv`;
      anchor.click();
      URL.revokeObjectURL(url);
      setExportState("CSV izvoz je preuzet.");
    } catch (reason) {
      setExportState(reason instanceof Error ? reason.message : "CSV izvoz nije uspeo.");
    }
  };

  const handleExcel = async () => {
    try {
      setExportState("Pripremam Excel izvoz...");
      const payload = durableReport
        ? resolveAnalyticsTablePayload({
            tableKey: durableReport.payload.tableKey,
            tableTitle: durableReport.payload.tableTitle,
            documentType: durableReport.payload.documentType,
            templateName: durableReport.payload.templateName,
            locale: durableReport.payload.locale,
            columns: durableReport.payload.columns.map((column) => ({
              ...column,
              dataType: normalizeColumnType(column.dataType),
            })),
            rows: durableReport.payload.rows,
            filters: durableReport.payload.filters,
            metadata: durableReport.payload.metadata,
          })
        : buildExportPayload(report!, filters);
      const result = await generateExport(payload, {
        format: "xlsx",
        orientation: "landscape",
        includeFiltersAndMetadata: true,
      });

      if (result.isAsync) {
        const completed = await waitForExport(result.documentId);
        if (completed.downloadUrl) {
          downloadExport(completed.downloadUrl, completed.fileName);
        }
      } else if (result.downloadUrl) {
        downloadExport(result.downloadUrl, result.fileName);
      }

      setExportState("Excel izvoz je preuzet.");
    } catch (reason) {
      setExportState(reason instanceof Error ? reason.message : "Excel izvoz nije uspeo.");
    }
  };

  const handleCopySummary = async () => {
    try {
      const summary = durableReport ? buildDurableSummary(durableReport) : buildSummary(report!);
      await navigator.clipboard.writeText(summary);
      setExportState("Sažetak je kopiran.");
    } catch {
      setExportState("Kopiranje nije uspelo.");
    }
  };

  const handleCopyLink = async () => {
    try {
      const targetUrl = durableReport?.stableQueryUrl
        ? new URL(durableReport.stableQueryUrl, window.location.origin).toString()
        : window.location.href;
      await navigator.clipboard.writeText(targetUrl);
      setExportState("Link ka izveštaju je kopiran.");
    } catch {
      setExportState("Kopiranje linka nije uspelo.");
    }
  };

  return (
    <section className="pilot-intake-report">
      <div className="pilot-intake-head">
        <div>
          <h2>{durableReport?.reportTitle ?? "Trendplus pilot izveštaj kvaliteta podataka"}</h2>
          <p>Učitavanje podataka za analitiku pre otvaranja dashboard-a.</p>
        </div>
        <div className="pilot-intake-actions no-print">
          {durableReport?.stableQueryUrl ? (
            <Link to={durableReport.stableQueryUrl} className="pilot-intake-action-link">Otvori trajni report</Link>
          ) : null}
          <button type="button" onClick={handlePrint}>Štampaj izveštaj</button>
          <button type="button" onClick={handleCsv}>Izvezi CSV</button>
          <button type="button" onClick={() => void handleExcel()}>Izvezi Excel</button>
          <button type="button" onClick={() => void handleCopySummary()}>Kopiraj sažetak</button>
          <button type="button" onClick={() => void handleCopyLink()}>Kopiraj link</button>
          <span className="pilot-intake-muted">PDF izvoz trenutno nije dostupan. Koristite štampu ili Excel.</span>
        </div>
      </div>

      {exportState ? <div className="pilot-intake-state no-print">{exportState}</div> : null}

      {report ? (
        <>
          <article className={`pilot-intake-score ${readiness}`}>
            <div>
              <span>Skor spremnosti</span>
              <strong>{fmtNumber(report.readinessScore, 0, "-")}/100</strong>
              <p>{report.readinessLabel}</p>
            </div>
            <div className="pilot-intake-thresholds">
              <span>90-100: Spremno za pouzdanu analitiku</span>
              <span>70-89: Upotrebljivo uz upozorenja</span>
              <span>40-69: Pilot može, ali preporuke ograničene</span>
              <span>&lt;40: Prvo srediti podatke</span>
            </div>
          </article>

          <div className="pilot-intake-grid">
            <section className="pilot-card">
              <h3>Učitano</h3>
              <ul>
                <li>Artikli: {fmtNumber(report.loadedData.articlesCount, 0, "-")}</li>
                <li>Stavke prodaje: {fmtNumber(report.loadedData.saleItemsCount, 0, "-")}</li>
                <li>Računi: {fmtNumber(report.loadedData.receiptsCount, 0, "-")}</li>
                <li>Dobavljači: {fmtNumber(report.loadedData.suppliersCount, 0, "-")}</li>
                <li>Objekti: {fmtNumber(report.loadedData.storesCount, 0, "-")}</li>
                <li>Prva prodaja: {formatDate(report.loadedData.firstSaleDate)}</li>
                <li>Poslednja prodaja: {formatDate(report.loadedData.lastSaleDate)}</li>
                <li>Poslednji import: {formatDateTime(report.lastImportAtUtc, "-")}</li>
                <li>Poslednje osveženje analitike: {formatDateTime(report.lastRefreshAtUtc, "-")}</li>
              </ul>
            </section>

            <section className="pilot-card">
              <h3>Problemi</h3>
              <ul>
                <li className="critical">Bez dobavljača: {fmtNumber(report.issues.missingSupplierCount, 0, "-")}</li>
                <li className="critical">Bez nabavne cene: {fmtNumber(report.issues.missingCostCount, 0, "-")}</li>
                <li className="warning">Bez kategorije: {fmtNumber(report.issues.missingCategoryCount, 0, "-")}</li>
                <li className="warning">Bez boje: {fmtNumber(report.issues.missingColorCount ?? 0, 0, "-")}</li>
                <li className="warning">Bez veličine: {fmtNumber(report.issues.missingSizeCount ?? 0, 0, "-")}</li>
                <li className="critical">Prodaja bez artikla: {fmtNumber(report.issues.saleWithoutArticleCount, 0, "-")}</li>
                <li className="critical">Nulta/negativna cena: {fmtNumber(report.issues.zeroOrNegativePriceCount, 0, "-")}</li>
                <li className="warning">Dupliran SKU: {fmtNumber(report.issues.duplicateSkuCount ?? 0, 0, "-")}</li>
                <li className="warning">Dobavljač bez naziva: {fmtNumber(report.issues.missingSupplierNameCount, 0, "-")}</li>
              </ul>
            </section>

            <section className="pilot-card">
              <h3>Uticaj</h3>
              <ul>
                <li>Prihod bez nabavne cene: {formatPercentFromRatio(report.impact.revenueWithoutCostPercent)}</li>
                <li>Artikli bez dobavljača: {formatPercentFromRatio(report.impact.articlesWithoutSupplierPercent)}</li>
                <li>Blokirane preporuke: {fmtNumber(report.impact.recommendationsBlockedCount, 0, "-")}</li>
                <li>Ignorisani redovi: {fmtNumber(report.impact.ignoredRowsCount, 0, "-")}</li>
                <li>Nedovoljni signali: {fmtNumber(report.impact.insufficientSignalCount, 0, "-")}</li>
              </ul>
              <p className="pilot-card-note">Visok procenat prihoda bez cene ili veliki broj blokiranih preporuka direktno smanjuje pouzdanost maržnih odluka.</p>
            </section>
          </div>

          <section className="pilot-card">
            <h3>Preporučene akcije</h3>
            {report.recommendedActions.length === 0 ? (
              <p className="pilot-card-note">Nema preporučenih akcija za trenutni opseg. Proverite detalje kvaliteta podataka i osvežavanje.</p>
            ) : (
              <div className="pilot-actions-list">
                {report.recommendedActions.map((action) => (
                  <Link key={action} to={mapActionHref(action)} className="pilot-action-item">
                    <strong>{action}</strong>
                    <span>Otvori povezani ekran</span>
                  </Link>
                ))}
              </div>
            )}
          </section>
        </>
      ) : null}

      {durableReport ? (
        <section className="pilot-card">
          <h3>Trajni izveštaj</h3>
          <ul>
            <li>ID izveštaja: {durableReport.reportId}</li>
            <li>Tip izveštaja: {durableReport.reportType ?? "pilot-intake"}</li>
            <li>Generisano: {formatDateTime(durableReport.generatedAtUtc, "-")}</li>
            <li>Period od: {formatDate(durableReport.periodFrom ?? durableReport.period?.fromUtc, "-")}</li>
            <li>Period do: {formatDate(durableReport.periodTo ?? durableReport.period?.toUtc, "-")}</li>
            <li>Poslednji refresh: {formatDateTime(durableReport.lastRefreshAtUtc, "-")}</li>
            <li>Status kvaliteta podataka: {durableReport.dataQualityStatus}</li>
            <li>Preporuke dozvoljene: {durableReport.recommendationAllowed == null ? "-" : durableReport.recommendationAllowed ? "Da" : "Ne"}</li>
            <li>Korišćen fallback: {durableReport.usedFallback == null ? "-" : durableReport.usedFallback ? "Da" : "Ne"}</li>
          </ul>
          {durableReport.warnings && durableReport.warnings.length > 0 ? (
            <div className="pilot-card-note">
              <strong>Upozorenja:</strong> {durableReport.warnings.join(" | ")}
            </div>
          ) : null}
          <p className="pilot-card-note"><strong>Metodologija:</strong> {durableMethodologySummary(durableReport)}</p>
          {durableReport.meta?.message ? <p className="pilot-card-note">{durableReport.meta.message}</p> : null}
          {durableReport.sections.length > 0 ? (
            <ul>
              {durableReport.sections.map((section) => (
                <li key={section.key}>{section.title || section.key}: {fmtNumber(durableSectionRowCount(section), 0, "-")}</li>
              ))}
            </ul>
          ) : null}
        </section>
      ) : null}

      <section className="pilot-methodology no-print">
        <button
          type="button"
          onClick={() => setShowMethodology((prev) => !prev)}
          aria-expanded={showMethodology}
          aria-controls="pilot-methodology-panel"
        >
          Kako čitati ovaj izveštaj?
        </button>
        {showMethodology ? (
          <div id="pilot-methodology-panel" className="pilot-methodology-list">
            <p>
              Ovaj izveštaj prikazuje koliko podataka je učitano i koji nedostaci utiču na pouzdanost analitike.
              Definicije ključnih KPI-jeva se čitaju iz centralnog analytics registry-ja.
            </p>
            {methodologyKeys.map((metricKey) => {
              const definition = getAnalyticsMetricDefinition(metricKey);
              return (
                <article key={metricKey} className="pilot-methodology-item">
                  <div className="pilot-methodology-head">
                    <strong>{definition.title}</strong>
                    <KpiExplainButton metricKey={metricKey} ariaLabel={`Kako je izračunat KPI: ${definition.title}`} />
                  </div>
                  <p>{definition.description}</p>
                  <p><strong>Formula:</strong> {definition.formula}</p>
                  <p><strong>Izvor:</strong> {definition.source}</p>
                  {definition.qualityNote ? <p className="pilot-card-note">{definition.qualityNote}</p> : null}
                </article>
              );
            })}
          </div>
        ) : null}
      </section>
    </section>
  );
}



import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import type { AnalyticsNamedValue } from "../../types/analyticsTable";
import type {
  AnalyticsRefreshStatus,
  PilotDataQualityIntakeReport,
  PilotIntakeDurableReport,
} from "../../types/analytics";
import { resolveAnalyticsTablePayload } from "../../services/analyticsTableState";
import { downloadExport, generateExport, waitForExport } from "../../services/exportApi";
import {
  findAnalyticsMetricKeyByLabel,
  type AnalyticsMetricKey,
} from "../../utils/analyticsMetricDefinitions";
import {
  fmtNumber,
  fmtPctFromRatio,
  formatDate,
  formatDateTime,
} from "../../utils/analyticsFormatters";
import { isAnalyticsMetaWarning } from "../../utils/analyticsResponseMeta";
import AnalyticsEmptyState from "./AnalyticsEmptyState";
import AnalyticsErrorState from "./AnalyticsErrorState";
import KpiExplainButton from "./KpiExplainButton";
import MetricMethodologyPanel from "./MetricMethodologyPanel";
import PilotImportReadinessCard from "./PilotImportReadinessCard";
import "./PilotDataQualityIntakeReport.css";

type Props = {
  report: PilotDataQualityIntakeReport | null;
  loading: boolean;
  error: string | null;
  filters: AnalyticsNamedValue[];
  durableReport?: PilotIntakeDurableReport | null;
  refreshStatus?: AnalyticsRefreshStatus | null;
  onRetry: () => void;
};

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

function formatOptionalCount(value: number | null | undefined): string {
  return value == null ? "-" : String(value);
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
    ["Problemi", "Bez boje", formatOptionalCount(report.issues.missingColorCount)],
    ["Problemi", "Bez veličine", formatOptionalCount(report.issues.missingSizeCount)],
    ["Problemi", "Prodaja bez artikla", String(report.issues.saleWithoutArticleCount)],
    ["Problemi", "Nulta/negativna cena", String(report.issues.zeroOrNegativePriceCount)],
    ["Problemi", "Dupliran SKU", formatOptionalCount(report.issues.duplicateSkuCount)],
    ["Problemi", "Dobavljač bez naziva", String(report.issues.missingSupplierNameCount)],
    ["Uticaj", "Prihod bez cene", fmtPctFromRatio(report.impact.revenueWithoutCostPercent, 1, "-")],
    ["Uticaj", "Artikli bez dobavljača", fmtPctFromRatio(report.impact.articlesWithoutSupplierPercent, 1, "-")],
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
    `Uticaj: prihod bez cene ${fmtPctFromRatio(report.impact.revenueWithoutCostPercent, 1, "-")}, artikli bez dobavljača ${fmtPctFromRatio(report.impact.articlesWithoutSupplierPercent, 1, "-")}, blokirane preporuke ${fmtNumber(report.impact.recommendationsBlockedCount, 0, "-")}`,
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
    { section: "Problemi", item: "Bez boje", value: formatOptionalCount(report.issues.missingColorCount) },
    { section: "Problemi", item: "Bez veličine", value: formatOptionalCount(report.issues.missingSizeCount) },
    { section: "Problemi", item: "Prodaja bez artikla", value: String(report.issues.saleWithoutArticleCount) },
    { section: "Problemi", item: "Nulta/negativna cena", value: String(report.issues.zeroOrNegativePriceCount) },
    { section: "Problemi", item: "Dupliran SKU", value: formatOptionalCount(report.issues.duplicateSkuCount) },
    { section: "Problemi", item: "Dobavljač bez naziva", value: String(report.issues.missingSupplierNameCount) },
    { section: "Uticaj", item: "Prihod bez cene", value: fmtPctFromRatio(report.impact.revenueWithoutCostPercent, 1, "-") },
    { section: "Uticaj", item: "Artikli bez dobavljača", value: fmtPctFromRatio(report.impact.articlesWithoutSupplierPercent, 1, "-") },
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
      { key: "lastImportStatus", label: "Status importa", value: report.lastImportStatus ?? null },
      { key: "lastImportScope", label: "Scope importa", value: report.lastImportScope ?? null },
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

function normalizeText(value: string | null | undefined): string {
  return (value ?? "")
    .toLowerCase()
    .normalize("NFKD")
    .replace(/[^\w\s]/g, "")
    .replace(/\s+/g, " ")
    .trim();
}

type TrustSignalState = "clear" | "partial" | "issues";

function isFiniteNumber(value: number | null | undefined): value is number {
  return typeof value === "number" && Number.isFinite(value);
}

function resolveTrustSignalState(
  values: Array<number | null | undefined>,
  options?: { hasMetaWarning?: boolean },
): TrustSignalState {
  const hasPositiveValue = values.some((value) => isFiniteNumber(value) && value > 0);
  if (hasPositiveValue) return "issues";

  const hasMissingValue = values.some((value) => !isFiniteNumber(value));
  if (options?.hasMetaWarning || hasMissingValue) return "partial";

  return "clear";
}

function issueSignalState(report: PilotDataQualityIntakeReport): TrustSignalState {
  return resolveTrustSignalState(
    [
      report.issues.missingSupplierCount,
      report.issues.missingCostCount,
      report.issues.missingCategoryCount,
      report.issues.missingColorCount,
      report.issues.missingSizeCount,
      report.issues.saleWithoutArticleCount,
      report.issues.zeroOrNegativePriceCount,
      report.issues.duplicateSkuCount,
      report.issues.missingSupplierNameCount,
    ],
    { hasMetaWarning: isAnalyticsMetaWarning(report.meta) },
  );
}

function impactSignalState(report: PilotDataQualityIntakeReport): TrustSignalState {
  return resolveTrustSignalState(
    [
      report.impact.revenueWithoutCostPercent,
      report.impact.articlesWithoutSupplierPercent,
      report.impact.recommendationsBlockedCount,
      report.impact.ignoredRowsCount,
      report.impact.insufficientSignalCount,
    ],
    { hasMetaWarning: isAnalyticsMetaWarning(report.meta) },
  );
}

function signalStateLabel(state: TrustSignalState): string {
  if (state === "issues") return "Potrebna korekcija";
  if (state === "partial") return "Nedovoljno potvrđeno";
  return "Bez otvorenih signala";
}

function signalStateTone(state: TrustSignalState): string {
  if (state === "issues") return "warning";
  if (state === "partial") return "partial";
  return "clear";
}

export default function PilotDataQualityIntakeReportPanel({ report, loading, error, filters, durableReport, refreshStatus, onRetry }: Props) {
  const [exportBusy, setExportBusy] = useState(false);
  const [exportStatus, setExportStatus] = useState<string | null>(null);
  const [methodologyKey, setMethodologyKey] = useState<AnalyticsMetricKey | null>(null);

  const durableSections = durableReport?.sections ?? [];
  const durableSummary = durableReport ? durableMethodologySummary(durableReport) : null;
  const durableWarnings = durableReport?.warnings ?? [];
  const durableGeneratedAt = durableReport?.generatedAtUtc ?? report?.generatedAtUtc ?? null;
  const generatedAtLabel = durableGeneratedAt ? formatDateTime(durableGeneratedAt, "Nije dostupno") : "Nije dostupno";
  const metaWarning = isAnalyticsMetaWarning(report?.meta) || isAnalyticsMetaWarning(durableReport?.meta);

  const reportText = useMemo(() => (report ? buildSummary(report) : ""), [report]);
  const exportPayload = useMemo(() => (report ? buildExportPayload(report, filters) : null), [filters, report]);

  async function runTextExport() {
    if (!report) return;
    const blob = new Blob([buildCsv(report)], { type: "text/csv;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `pilot-intake-${formatDate(report.generatedAtUtc)}.csv`;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
  }

  async function runServerExport(format: "pdf" | "xlsx" | "csv") {
    if (!exportPayload || exportBusy) return;
    try {
      setExportBusy(true);
      setExportStatus("Server priprema dokument...");
      const result = await generateExport(exportPayload, {
        format,
        orientation: "portrait",
        includeFiltersAndMetadata: true,
      });
      if (result.isAsync) {
        setExportStatus("Dokument je u redu čekanja...");
        const completed = await waitForExport(result.documentId);
        if (completed.downloadUrl) downloadExport(completed.downloadUrl, completed.fileName);
      } else if (result.downloadUrl) {
        downloadExport(result.downloadUrl, result.fileName);
      }
      setExportStatus("Eksport je spreman.");
    } catch (reason) {
      setExportStatus(reason instanceof Error ? reason.message : "Eksport nije uspeo.");
    } finally {
      setExportBusy(false);
    }
  }

  if (loading) {
    return <div className="pilot-intake-loading">Učitavam pilot intake izveštaj...</div>;
  }

  if (error) {
    return (
      <AnalyticsErrorState
        title="Pilot intake izveštaj nije dostupan"
        message={error}
        onRetry={onRetry}
        helpHref="/admin/configuration?panel=workers"
      />
    );
  }

  if (!report) {
    return (
      <AnalyticsEmptyState
        variant="insufficient_data"
        title="Pilot intake izveštaj nema podatke"
        message="Nema dovoljno učitanih podataka da bi se izračunao readiness score."
        reasons={["Nema import batch-a ili prodajnih redova u izabranom periodu."]}
      />
    );
  }

  const tone = readinessTone(report.readinessStatus);
  const issueState = issueSignalState(report);
  const impactState = impactSignalState(report);

  return (
    <section className={`pilot-intake-card tone-${tone}`}>
      <div className="pilot-intake-head">
        <div>
          <h2>Pilot intake izveštaj</h2>
          <p>Spremnost podataka za pouzdan demo, analitiku i preporuke.</p>
        </div>
        <div className="pilot-intake-score">
          <span>{report.readinessLabel}</span>
          <strong>{report.readinessScore}/100</strong>
        </div>
      </div>

      <PilotImportReadinessCard report={report} refreshStatus={refreshStatus} />

      {metaWarning ? (
        <div className="pilot-intake-warning" role="status">
          {report.meta?.message ?? durableReport?.meta?.message ?? "Izveštaj ima upozorenja kvaliteta podataka."}
        </div>
      ) : null}

      {durableReport ? (
        <div className="pilot-intake-durable-note">
          <strong>Durable report:</strong> {durableReport.reportTitle ?? durableReport.title ?? "Pilot intake"} · {generatedAtLabel}
          {durableWarnings.length > 0 ? <span> · {durableWarnings.length} upozorenja</span> : null}
          <p>{durableSummary}</p>
        </div>
      ) : null}

      <div className="pilot-intake-grid">
        <article>
          <span>Učitano</span>
          <strong>{fmtNumber(report.loadedData.articlesCount, 0, "-")} artikala</strong>
          <p>{fmtNumber(report.loadedData.saleItemsCount, 0, "-")} stavki prodaje · {fmtNumber(report.loadedData.receiptsCount, 0, "-")} računa</p>
        </article>
        <article className={`state-${signalStateTone(issueState)}`}>
          <span>Problemi podataka</span>
          <strong>{signalStateLabel(issueState)}</strong>
          <p>Dobavljač {fmtNumber(report.issues.missingSupplierCount, 0, "-")} · cena {fmtNumber(report.issues.missingCostCount, 0, "-")} · kategorija {fmtNumber(report.issues.missingCategoryCount, 0, "-")}</p>
        </article>
        <article className={`state-${signalStateTone(impactState)}`}>
          <span>Uticaj na preporuke</span>
          <strong>{signalStateLabel(impactState)}</strong>
          <p>{fmtPctFromRatio(report.impact.revenueWithoutCostPercent, 1, "-")} prihoda bez cene · {fmtNumber(report.impact.recommendationsBlockedCount, 0, "-")} blokiranih preporuka</p>
        </article>
      </div>

      <div className="pilot-intake-meta">
        <span>Period: {formatDate(report.periodFromUtc)} - {formatDate(report.periodToUtc)}</span>
        <span>Scope: {report.dataScope}</span>
        <span>Import: {formatDateTime(report.lastImportAtUtc, "Nije dostupan")}</span>
        <span>Refresh: {formatDateTime(report.lastRefreshAtUtc, "Nije dostupan")}</span>
      </div>

      {durableSections.length > 0 ? (
        <div className="pilot-intake-durable-sections">
          {durableSections.map((section) => (
            <article key={section.key}>
              <span>{section.title ?? section.key}</span>
              <strong>{fmtNumber(durableSectionRowCount(section), 0, "-")} redova</strong>
              {section.description ? <p>{section.description}</p> : null}
            </article>
          ))}
        </div>
      ) : null}

      <div className="pilot-intake-actions">
        {report.recommendedActions.map((action) => {
          const metricKey = findAnalyticsMetricKeyByLabel(action);
          return (
            <div key={action} className="pilot-intake-action-row">
              <Link to={mapActionHref(action)}>{action}</Link>
              {metricKey ? (
                <button type="button" onClick={() => setMethodologyKey(metricKey)}>
                  Kako se meri?
                </button>
              ) : null}
            </div>
          );
        })}
      </div>

      <div className="pilot-intake-export">
        <button type="button" onClick={runTextExport}>Preuzmi CSV</button>
        <button type="button" disabled={exportBusy || !exportPayload} onClick={() => void runServerExport("pdf")}>PDF</button>
        <button type="button" disabled={exportBusy || !exportPayload} onClick={() => void runServerExport("xlsx")}>XLSX</button>
        <button type="button" disabled={exportBusy || !exportPayload} onClick={() => void navigator.clipboard?.writeText(reportText)}>Kopiraj sažetak</button>
        {exportStatus ? <span>{exportStatus}</span> : null}
      </div>

      {methodologyKey ? (
        <MetricMethodologyPanel
          metricKey={methodologyKey}
          onClose={() => setMethodologyKey(null)}
        />
      ) : null}
    </section>
  );
}

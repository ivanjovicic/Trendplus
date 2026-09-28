import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import type { AnalyticsNamedValue, ResolvedAnalyticsTablePayload } from "../../types/analyticsTable";
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
  formatPilotImpactPercentage,
  getPilotImportScopeLabel,
  getPilotImportStatusLabel,
  getPilotReadinessStatusLabel,
  resolvePilotIntakeImpact,
} from "../../utils/pilotImportReadiness";
import {
  fmtNumber,
  fmtPct,
  fmtPctFromRatio,
  fmtRsd,
  formatDate,
  formatDateTime,
} from "../../utils/analyticsFormatters";
import { isAnalyticsMetaEmpty, isAnalyticsMetaError, isAnalyticsMetaWarning } from "../../utils/analyticsResponseMeta";
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

function resolveSignalCoverage(report: PilotDataQualityIntakeReport): number | null {
  if (report.loadedData.articlesCount <= 0) return null;
  const covered = Math.max(
    0,
    Math.min(
      report.loadedData.articlesCount,
      report.loadedData.articlesCount - report.impact.insufficientSignalCount,
    ),
  );
  return covered / report.loadedData.articlesCount;
}

export function buildCsv(report: PilotDataQualityIntakeReport): string {
  const impact = resolvePilotIntakeImpact(report);
  const signalCoverage = resolveSignalCoverage(report);
  const rows = [
    ["Sekcija", "Stavka", "Vrednost"],
    ["Skor", "Status spremnosti", getPilotReadinessStatusLabel(report.readinessStatus)],
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
    ["Uticaj", "Prihod bez cene", formatPilotImpactPercentage(impact.revenueWithoutCost)],
    ["Uticaj", "Artikli bez dobavljača", formatPilotImpactPercentage(impact.articlesWithoutSupplier)],
    ["Uticaj", "Blokirani artikli (jedinstveni)", String(report.impact.recommendationsBlockedCount)],
    ["Uticaj", "Ignorisani redovi", String(report.impact.ignoredRowsCount)],
    ["Uticaj", "Pokrivenost poslovnim signalom", signalCoverage == null ? "nije dostupno" : fmtPctFromRatio(signalCoverage, 1, "nije dostupno")],
    ["Period", "Anchor perioda", report.periodAnchorCode ?? "nije potvrđen"],
    ["Period", "Napomena za anchor", report.periodAnchorMessage ?? ""],
    ["Uvoz", "Status uvoza", getPilotImportStatusLabel(report.lastImportStatus)],
    ["Uvoz", "Opseg uvoza", getPilotImportScopeLabel(report.lastImportScope)],
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

export function buildSummary(report: PilotDataQualityIntakeReport): string {
  const impact = resolvePilotIntakeImpact(report);
  const signalCoverage = resolveSignalCoverage(report);
  return [
    `Trendplus pilot izveštaj kvaliteta podataka`,
    `Status spremnosti: ${getPilotReadinessStatusLabel(report.readinessStatus)}`,
    `Skor spremnosti: ${getPilotReadinessStatusLabel(report.readinessStatus)} (${report.readinessScore}/100)`,
    `Učitano: ${fmtNumber(report.loadedData.articlesCount, 0, "-")} artikala, ${fmtNumber(report.loadedData.saleItemsCount, 0, "-")} stavki prodaje, ${fmtNumber(report.loadedData.receiptsCount, 0, "-")} računa`,
    `Top problemi: bez dobavljača ${fmtNumber(report.issues.missingSupplierCount, 0, "-")}, bez nabavne cene ${fmtNumber(report.issues.missingCostCount, 0, "-")}, bez kategorije ${fmtNumber(report.issues.missingCategoryCount, 0, "-")}`,
    `Uticaj: prihod bez cene ${formatPilotImpactPercentage(impact.revenueWithoutCost)}, artikli bez dobavljača ${formatPilotImpactPercentage(impact.articlesWithoutSupplier)}, blokirani artikli ${fmtNumber(report.impact.recommendationsBlockedCount, 0, "-")}`,
    `Pokrivenost poslovnim signalom: ${signalCoverage == null ? "nije dostupna" : fmtPctFromRatio(signalCoverage, 1, "nije dostupna")} (informativno, ne menja skor spremnosti)`,
    `Anchor perioda: ${report.periodAnchorCode ?? "nije potvrđen"}${report.periodAnchorMessage ? ` — ${report.periodAnchorMessage}` : ""}`,
    `Status uvoza: ${getPilotImportStatusLabel(report.lastImportStatus)}`,
    `Opseg uvoza: ${getPilotImportScopeLabel(report.lastImportScope)}`,
    `Preporučene akcije: ${report.recommendedActions.join("; ")}`,
  ].join("\n");
}

export function buildExportPayload(report: PilotDataQualityIntakeReport, filters: AnalyticsNamedValue[]) {
  const impact = resolvePilotIntakeImpact(report);
  const signalCoverage = resolveSignalCoverage(report);
  const rows: Array<{ section: string; item: string; value: string }> = [
    { section: "Skor", item: "Status spremnosti", value: getPilotReadinessStatusLabel(report.readinessStatus) },
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
    { section: "Uticaj", item: "Prihod bez cene", value: formatPilotImpactPercentage(impact.revenueWithoutCost) },
    { section: "Uticaj", item: "Artikli bez dobavljača", value: formatPilotImpactPercentage(impact.articlesWithoutSupplier) },
    { section: "Uticaj", item: "Blokirani artikli (jedinstveni)", value: String(report.impact.recommendationsBlockedCount) },
    { section: "Uticaj", item: "Ignorisani redovi", value: String(report.impact.ignoredRowsCount) },
    { section: "Uticaj", item: "Pokrivenost poslovnim signalom", value: signalCoverage == null ? "nije dostupno" : fmtPctFromRatio(signalCoverage, 1, "nije dostupno") },
    { section: "Period", item: "Anchor perioda", value: report.periodAnchorCode ?? "nije potvrđen" },
    { section: "Period", item: "Napomena za anchor", value: report.periodAnchorMessage ?? "-" },
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
      { key: "lastImportAtUtc", label: "Poslednji uvoz", value: report.lastImportAtUtc ?? null },
      { key: "lastImportStatus", label: "Status uvoza", value: getPilotImportStatusLabel(report.lastImportStatus) },
      { key: "lastImportScope", label: "Opseg uvoza", value: getPilotImportScopeLabel(report.lastImportScope) },
      { key: "lastRefreshAtUtc", label: "Poslednje osveženje", value: report.lastRefreshAtUtc ?? null },
      { key: "dataScope", label: "Opseg podataka", value: report.dataScope },
      { key: "periodAnchorCode", label: "Anchor perioda", value: report.periodAnchorCode ?? null },
      { key: "periodAnchorMessage", label: "Napomena za anchor", value: report.periodAnchorMessage ?? null },
    ],
    locale: "sr-RS",
  });
}

function durableMethodologySummary(report: PilotIntakeDurableReport): string {
  if (typeof report.methodology === "string") return report.methodology;
  return report.methodologySummary ?? report.methodology.summary;
}

function durableKpiNumber(value: unknown): number | null {
  if (typeof value === "number") return Number.isFinite(value) ? value : null;
  if (typeof value !== "string" || !value.trim()) return null;
  const parsed = Number(value.replace(/\s/g, "").replace(",", "."));
  return Number.isFinite(parsed) ? parsed : null;
}

function formatDurableKpiValue(kpi: NonNullable<PilotIntakeDurableReport["kpis"]>[number]): string {
  if (["error", "insufficient_data", "stale"].includes(kpi.valueStatus ?? "")) {
    return kpi.valueReason ?? "Nije dostupno";
  }
  const value = durableKpiNumber(kpi.value);
  if (value == null) return kpi.valueReason ?? "Nije dostupno";
  if (kpi.unit === "ratio") return fmtPctFromRatio(value, 1, "Nije dostupno");
  if (kpi.unit === "%") return fmtPct(value, 1, "Nije dostupno");
  if (kpi.unit === "/100") return `${fmtNumber(value, 0, "Nije dostupno")}/100`;
  if (kpi.unit?.toUpperCase() === "RSD") return fmtRsd(value, 0, "Nije dostupno");
  return fmtNumber(value, 0, "Nije dostupno");
}

function formatDurableCell(value: unknown, dataType?: string): string {
  if (value == null || value === "") return "Nije dostupno";
  if (dataType === "date") return formatDate(String(value), "Nije dostupno");
  if (dataType === "datetime") return formatDateTime(String(value), "Nije dostupno");
  if (dataType === "number" || dataType === "currency") {
    const numeric = durableKpiNumber(value);
    return numeric == null ? "Nije dostupno" : dataType === "currency" ? fmtRsd(numeric, 0, "Nije dostupno") : fmtNumber(numeric, 0, "Nije dostupno");
  }
  if (typeof value === "boolean") return value ? "Da" : "Ne";
  return String(value);
}

function durableFreshnessLabel(value: string | null | undefined): string {
  switch (value?.trim().toLowerCase()) {
    case "fresh": return "Sveže";
    case "stale": return "Zastarelo";
    case "critical": return "Kritično";
    case "warning": return "Oprez";
    default: return "Nije dostupna";
  }
}

function durableReportIsEmpty(report: PilotIntakeDurableReport): boolean {
  if (isAnalyticsMetaEmpty(report.meta)) return true;
  return (report.kpis?.length ?? 0) === 0 && report.sections.some((section) => section.key === "report-status");
}

function toResolvedPayload(report: PilotIntakeDurableReport): ResolvedAnalyticsTablePayload {
  return {
    tableKey: report.payload.tableKey,
    tableTitle: report.payload.tableTitle,
    documentType: report.payload.documentType,
    templateName: report.payload.templateName,
    templateVersion: report.payload.templateVersion,
    locale: report.payload.locale,
    columns: report.payload.columns,
    rows: report.payload.rows.map((row) => ({
      section: row.section,
      item: row.item,
      value: row.value,
      secondary: row.secondary,
      note: row.note,
    })),
    filters: report.payload.filters,
    metadata: report.payload.metadata,
  };
}

function csvCell(value: unknown): string {
  const text = String(value ?? "");
  return /[",\n;]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}

export function buildDurableCsv(report: PilotIntakeDurableReport): string {
  const rows = [
    ["Sekcija", "Stavka", "Vrednost"],
    ...report.rows.map((row) => [row.section, row.item, row.value]),
  ];
  return `\uFEFF${rows.map((row) => row.map(csvCell).join(",")).join("\n")}`;
}

function buildDurableSummary(report: PilotIntakeDurableReport): string {
  const kpis = (report.kpis ?? []).map((kpi) => `${kpi.label}: ${formatDurableKpiValue(kpi)}`);
  const actions = (report.recommendedActions ?? []).map((action) => action.title);
  return [
    report.reportTitle ?? report.title ?? "Trendplus pilot izveštaj kvaliteta podataka",
    ...kpis,
    actions.length > 0 ? `Preporučene akcije: ${actions.join("; ")}` : null,
  ].filter((line): line is string => Boolean(line)).join("\n");
}

function csvDate(value: string | null | undefined): string {
  const raw = value?.slice(0, 10) ?? "";
  return /^\d{4}-\d{2}-\d{2}$/.test(raw) ? raw : "unknown-date";
}

export function buildPilotCsvFilename(report: PilotDataQualityIntakeReport | null, durableReport: PilotIntakeDurableReport | null | undefined): string {
  const generatedAt = report?.generatedAtUtc ?? durableReport?.generatedAtUtc;
  const from = durableReport?.periodFrom ?? report?.periodFromUtc;
  const to = durableReport?.periodTo ?? report?.periodToUtc;
  const periodSuffix = from && to ? `_${csvDate(from)}_${csvDate(to)}` : "";
  return `pilot-intake-${csvDate(generatedAt)}${periodSuffix}.csv`;
}

type DurableReportContentProps = {
  report: PilotIntakeDurableReport;
  exportBusy: boolean;
  exportStatus: string | null;
  onTextExport: () => void;
  onServerExport: (format: "pdf" | "xlsx" | "csv") => void;
  onCopy: () => void;
};

function DurableReportContent({ report, exportBusy, exportStatus, onTextExport, onServerExport, onCopy }: DurableReportContentProps) {
  const kpis = report.kpis ?? [];
  const actions = report.recommendedActions ?? [];
  const warnings = report.warnings ?? [];

  return (
    <section className="pilot-intake-card tone-warning" data-testid="pilot-durable-report">
      <div className="pilot-intake-head">
        <div>
          <h2>{report.reportTitle ?? report.title ?? "Pilot izveštaj"}</h2>
          <p>Trajni izveštaj kvaliteta podataka iz backend izvora.</p>
        </div>
        <div className="pilot-intake-score">
          <span>{report.recommendationAllowed ? "Preporuke dozvoljene" : "Preporuke ograničene"}</span>
          <strong>{getPilotReadinessStatusLabel(report.dataQualityStatus)}</strong>
        </div>
      </div>

      {warnings.length > 0 ? (
        <div className="pilot-intake-warning" role="status">
          {warnings.join(" · ")}
        </div>
      ) : null}

      {kpis.length > 0 ? (
        <div className="pilot-intake-grid" data-testid="pilot-durable-kpis">
          {kpis.map((kpi) => (
            <article key={kpi.key}>
              <span>{kpi.label}</span>
              <strong>{formatDurableKpiValue(kpi)}</strong>
              {kpi.note || kpi.valueReason ? <p>{kpi.note ?? kpi.valueReason}</p> : null}
            </article>
          ))}
        </div>
      ) : null}

      <div className="pilot-intake-meta">
        <span>Period: {formatDate(report.periodFrom ?? report.period?.fromUtc, "Nije dostupan")} - {formatDate(report.periodTo ?? report.period?.toUtc, "Nije dostupan")}</span>
        <span>Opseg podataka: {getPilotImportScopeLabel(report.period?.scope)}</span>
        <span>Generisano: {formatDateTime(report.generatedAtUtc, "Nije dostupno")}</span>
        <span>Svežina: {durableFreshnessLabel(report.dataFreshnessStatus)}</span>
      </div>

      <div className="pilot-intake-durable-sections" data-testid="pilot-durable-sections">
        {report.sections.map((section) => {
          const rows: Array<Record<string, unknown>> = section.rows ?? report.rows
            .filter((row) => row.section === section.key)
            .map((row) => ({ item: row.item, value: row.value, note: row.note }));
          const columns: Array<{ key: string; label: string; dataType?: string }> = section.columns ?? (rows.length > 0
            ? Object.keys(rows[0]).map((key) => ({ key, label: key }))
            : []);

          return (
            <article key={section.key} className="pilot-intake-durable-section">
              <h3>{section.title ?? section.key}</h3>
              {section.description ? <p>{section.description}</p> : null}
              {rows.length > 0 && columns.length > 0 ? (
                <div className="pilot-intake-durable-table-wrap">
                  <table className="pilot-intake-durable-table">
                    <thead><tr>{columns.map((column) => <th key={column.key}>{column.label}</th>)}</tr></thead>
                    <tbody>
                      {rows.map((row, rowIndex) => (
                        <tr key={`${section.key}-${rowIndex}`}>
                          {columns.map((column) => <td key={column.key}>{formatDurableCell(row[column.key], column.dataType)}</td>)}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : <p className="pilot-card-note">{section.emptyMessage ?? "Nema stavki za ovaj opseg."}</p>}
            </article>
          );
        })}
      </div>

      <div className="pilot-intake-actions" data-testid="pilot-durable-actions">
        {actions.length > 0 ? actions.map((action) => (
          <div key={`${action.title}-${action.href}`} className="pilot-intake-action-row">
            <Link to={action.href || "/analytics/data-quality"}>{action.title}</Link>
            <span>{action.description}</span>
          </div>
        )) : <p className="pilot-card-note">Nema preporučenih akcija za traženi opseg.</p>}
      </div>

      <div className="pilot-intake-export">
        <button type="button" onClick={onTextExport}>Preuzmi CSV</button>
        <button type="button" disabled={exportBusy} onClick={() => onServerExport("pdf")}>PDF</button>
        <button type="button" disabled={exportBusy} onClick={() => onServerExport("xlsx")}>XLSX</button>
        <button type="button" onClick={onCopy}>Kopiraj sažetak</button>
        {exportStatus ? <span>{exportStatus}</span> : null}
      </div>
    </section>
  );
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

export function issueSignalState(report: PilotDataQualityIntakeReport): TrustSignalState {
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

export function impactSignalState(report: PilotDataQualityIntakeReport): TrustSignalState {
  const impact = resolvePilotIntakeImpact(report);
  return resolveTrustSignalState(
    [
      impact.revenueWithoutCost.percentage,
      impact.articlesWithoutSupplier.percentage,
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

  const durableSummary = durableReport ? durableMethodologySummary(durableReport) : null;
  const durableWarnings = durableReport?.warnings ?? [];
  const durableGeneratedAt = durableReport?.generatedAtUtc ?? report?.generatedAtUtc ?? null;
  const generatedAtLabel = durableGeneratedAt ? formatDateTime(durableGeneratedAt, "Nije dostupno") : "Nije dostupno";
  const metaWarning = isAnalyticsMetaWarning(report?.meta) || isAnalyticsMetaWarning(durableReport?.meta);

  const reportText = useMemo(() => {
    if (report) return buildSummary(report);
    return durableReport ? buildDurableSummary(durableReport) : "";
  }, [durableReport, report]);
  const exportPayload = useMemo(() => {
    if (report) return buildExportPayload(report, filters);
    return durableReport ? toResolvedPayload(durableReport) : null;
  }, [durableReport, filters, report]);

  async function runTextExport() {
    if (!report && !durableReport) return;
    const csv = report ? buildCsv(report) : buildDurableCsv(durableReport!);
    const blob = new Blob([csv], { type: "text/csv;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = buildPilotCsvFilename(report, durableReport);
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
    return <div className="pilot-intake-loading">Učitavam pilot izveštaj…</div>;
  }

  if (error) {
    return (
      <AnalyticsErrorState
        title="Pilot izveštaj nije dostupan"
        message={error}
        onRetry={onRetry}
        helpHref="/admin/configuration?panel=workers"
      />
    );
  }

  if (durableReport && isAnalyticsMetaError(durableReport.meta)) {
    return (
      <AnalyticsErrorState
        title="Pilot izveštaj nije dostupan"
        message={durableReport.meta?.errorMessage ?? durableReport.meta?.message ?? "Pilot izveštaj nije dostupan."}
        onRetry={onRetry}
        helpHref="/analytics/data-quality"
      />
    );
  }

  if (durableReport && durableReportIsEmpty(durableReport)) {
    return (
      <AnalyticsEmptyState
        variant="insufficient_data"
        title="Pilot izveštaj nema dovoljno podataka"
        message={durableReport.meta?.message ?? "Nema dovoljno učitanih podataka da bi se izračunao skor spremnosti."}
        reasons={durableReport.meta?.emptyReason ? [durableReport.meta.emptyReason] : ["Nema import batch-a ili prodajnih redova u izabranom periodu."]}
      />
    );
  }

  if (!report && durableReport) {
    return (
      <DurableReportContent
        report={durableReport}
        exportBusy={exportBusy}
        exportStatus={exportStatus}
        onTextExport={() => void runTextExport()}
        onServerExport={(format) => void runServerExport(format)}
        onCopy={() => void navigator.clipboard?.writeText(reportText)}
      />
    );
  }

  if (!report || isAnalyticsMetaEmpty(report.meta)) {
    return (
      <AnalyticsEmptyState
        variant="insufficient_data"
        title="Pilot izveštaj nema dovoljno podataka"
        message={report?.meta?.message ?? "Nema dovoljno učitanih podataka da bi se izračunao skor spremnosti."}
        reasons={["Nema import batch-a ili prodajnih redova u izabranom periodu."]}
      />
    );
  }

  const tone = readinessTone(report.readinessStatus);
  const issueState = issueSignalState(report);
  const impactState = impactSignalState(report);
  const impact = resolvePilotIntakeImpact(report);

  return (
    <section className={`pilot-intake-card tone-${tone}`}>
      <div className="pilot-intake-head">
        <div>
          <h2>Pilot izveštaj</h2>
          <p>Spremnost podataka za bezbedne preporuke, uz jasno označene rupe u katalogu i signalu.</p>
        </div>
        <div className="pilot-intake-score">
          <span>{getPilotReadinessStatusLabel(report.readinessStatus)}</span>
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
          <strong>Trajni izveštaj:</strong> {durableReport.reportTitle ?? durableReport.title ?? "Pilot izveštaj"} · {generatedAtLabel}
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
          <p>{formatPilotImpactPercentage(impact.revenueWithoutCost)} prihoda bez cene · {formatPilotImpactPercentage(impact.articlesWithoutSupplier)} artikala bez dobavljača · {fmtNumber(report.impact.recommendationsBlockedCount, 0, "-")} blokiranih artikala</p>
        </article>
      </div>

      <div className="pilot-intake-meta">
        <span>Period: {formatDate(report.periodFromUtc)} - {formatDate(report.periodToUtc)}</span>
        <span>Pokrivenost poslovnim signalom: {resolveSignalCoverage(report) == null ? "nije dostupna" : fmtPctFromRatio(resolveSignalCoverage(report), 1, "nije dostupna")}</span>
        <span>Anchor perioda: {report.periodAnchorCode ?? "nije potvrđen"}</span>
        <span>Opseg podataka: {getPilotImportScopeLabel(report.dataScope)}</span>
        <span>Uvoz: {formatDateTime(report.lastImportAtUtc, "Nije dostupan")}</span>
        <span>Osvežavanje: {formatDateTime(report.lastRefreshAtUtc, "Nije dostupan")}</span>
      </div>

      {report.periodAnchorMessage ? (
        <div className="pilot-intake-warning" role="status">
          {report.periodAnchorMessage}
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
      ) : (
        <MetricMethodologyPanel metricKeys={["dataReadinessScore"]} />
      )}
    </section>
  );
}

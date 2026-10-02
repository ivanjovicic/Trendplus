import type { AnalyticsNamedValue } from "../../types/analyticsTable";
import type { PilotDataQualityIntakeReport, PilotIntakeDurableReport } from "../../types/analytics";
import { resolveAnalyticsTablePayload } from "../../services/analyticsTableState";
import {
  formatPilotImpactPercentage,
  getPilotImportScopeLabel,
  getPilotImportStatusLabel,
  getPilotReadinessStatusLabel,
  resolvePilotIntakeImpact,
} from "../../utils/pilotImportReadiness";
import { fmtNumber, fmtPctFromRatio } from "../../utils/analyticsFormatters";
import { isAnalyticsMetaWarning } from "../../utils/analyticsResponseMeta";

export type TrustSignalState = "clear" | "partial" | "issues";

function formatOptionalCount(value: number | null | undefined): string {
  return value == null ? "-" : String(value);
}

export function resolveSignalCoverage(report: PilotDataQualityIntakeReport): number | null {
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

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
    { section: "Problemi", item: "Bez boje", value: String(report.issues.missingColorCount ?? 0) },
    { section: "Problemi", item: "Bez veličine", value: String(report.issues.missingSizeCount ?? 0) },
    { section: "Problemi", item: "Prodaja bez artikla", value: String(report.issues.saleWithoutArticleCount) },
    { section: "Problemi", item: "Nulta/negativna cena", value: String(report.issues.zeroOrNegativePriceCount) },
    { section: "Problemi", item: "Dupliran SKU", value: String(report.issues.duplicateSkuCount ?? 0) },
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

function parseNumberFromValue(value: unknown): number | null {
  if (typeof value === "number" && Number.isFinite(value)) return value;
  if (typeof value !== "string") return null;
  const normalized = value.replace(/\./g, "").replace(",", ".").replace(/[^\d.-]/g, "");
  if (!normalized) return null;
  const parsed = Number(normalized);
  return Number.isFinite(parsed) ? parsed : null;
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

export default function PilotDataQualityIntakeReport({
  report,
  loading,
  error,
  filters,
  durableReport,
  refreshStatus,
  onRetry,
}: Props) {
  const [exportState, setExportState] = useState<string | null>(null);
  const methodologyKeys = useMemo<Array<AnalyticsMetricKey | string>>(() => {
    const fallbackKeys: AnalyticsMetricKey[] = [
      "dataReadinessScore",
      "missingCostCount",
      "missingSupplierCount",
      "revenueWithoutCost",
      "unknownSupplierRevenueShare",
      "blockedRecommendationsCount",
      "ignoredRowsCount",
    ];

    const durableMethodologyKeys =
      durableReport && typeof durableReport.methodology === "object" && Array.isArray(durableReport.methodology.metricKeys)
        ? durableReport.methodology.metricKeys
        : [];

    const payloadMethodologyKeys =
      (durableReport?.payload as { methodologyMetricKeys?: string[] } | undefined)?.methodologyMetricKeys ?? [];

    return Array.from(new Set([...durableMethodologyKeys, ...payloadMethodologyKeys, ...fallbackKeys]));
  }, [durableReport]);

  const readiness = useMemo(() => readinessTone(report?.readinessStatus ?? "critical"), [report?.readinessStatus]);
  const durableRows = durableReport?.rows ?? [];
  const durableActions = durableReport?.recommendedActions ?? [];
  const reportPeriodFrom = report?.periodFromUtc ?? durableReport?.periodFrom ?? durableReport?.period?.fromUtc ?? null;
  const reportPeriodTo = report?.periodToUtc ?? durableReport?.periodTo ?? durableReport?.period?.toUtc ?? null;
  const reportGeneratedAt = report?.generatedAtUtc ?? durableReport?.generatedAtUtc ?? null;
  const reportLastRefreshAt = report?.lastRefreshAtUtc ?? durableReport?.lastRefreshAtUtc ?? null;
  const reportDataQualityStatus = report?.meta?.dataQualityStatus ?? durableReport?.dataQualityStatus ?? null;
  const reportIssueSignalState = report ? issueSignalState(report) : "issues";
  const reportImpactSignalState = report ? impactSignalState(report) : "issues";

  const groupedDurableRows = useMemo(() => {
    const groups = {
      loaded: [] as typeof durableRows,
      issues: [] as typeof durableRows,
      impact: [] as typeof durableRows,
      readiness: [] as typeof durableRows,
      recommended: [] as typeof durableRows,
    };

    for (const row of durableRows) {
      const section = normalizeText(row.section);
      if (section.includes("ucit") || section.includes("loaded")) groups.loaded.push(row);
      if (section.includes("problem") || section.includes("issue")) groups.issues.push(row);
      if (section.includes("uticaj") || section.includes("impact")) groups.impact.push(row);
      if (section.includes("spremnost") || section.includes("readiness") || section.includes("skor")) groups.readiness.push(row);
      if (section.includes("preporuc") || section.includes("action")) groups.recommended.push(row);
    }

    return groups;
  }, [durableRows]);

  const durableReadinessScore = useMemo(() => {
    if (!durableReport) return null;
    const kpiScore = durableReport.kpis?.find((kpi) => normalizeText(kpi.key).includes("readiness") || normalizeText(kpi.label).includes("spremnost"));
    const fromKpi = parseNumberFromValue(kpiScore?.value);
    if (fromKpi != null) return fromKpi;

    const fromRows = groupedDurableRows.readiness.find((row) => normalizeText(row.item).includes("skor"));
    return parseNumberFromValue(fromRows?.value);
  }, [durableReport, groupedDurableRows.readiness]);

  const durableReadinessLabel = useMemo(() => {
    if (!durableReport) return null;
    const kpiLabel = durableReport.kpis?.find((kpi) => normalizeText(kpi.key).includes("readiness") || normalizeText(kpi.label).includes("spremnost"))?.note;
    if (kpiLabel) return kpiLabel;
    const rowLabel = groupedDurableRows.readiness.find((row) => normalizeText(row.item).includes("oznaka") || normalizeText(row.item).includes("label"));
    if (rowLabel?.value) return String(rowLabel.value);
    if (durableReadinessScore == null) return null;
    if (durableReadinessScore >= 90) return "Spremno za pouzdanu analitiku";
    if (durableReadinessScore >= 70) return "Upotrebljivo uz upozorenja";
    if (durableReadinessScore >= 40) return "Pilot može, preporuke ograničene";
    return "Prvo srediti podatke";
  }, [durableReport, groupedDurableRows.readiness, durableReadinessScore]);

  const durableReadinessTone = useMemo(() => {
    if (durableReadinessScore == null) return "warning";
    if (durableReadinessScore >= 90) return "excellent";
    if (durableReadinessScore >= 70) return "good";
    if (durableReadinessScore >= 40) return "warning";
    return "critical";
  }, [durableReadinessScore]);

  const intakeKpiCards = useMemo(() => {
    if (!report) return [];
    return [
      {
        label: "Spremnost podataka",
        value: `${fmtNumber(report.readinessScore, 0, "-")}/100`,
        metricKey: "dataReadinessScore" as const,
      },
      {
        label: "Artikli bez dobavljača",
        value: fmtNumber(report.issues.missingSupplierCount, 0, "-"),
        metricKey: "missingSupplierCount" as const,
      },
      {
        label: "Redovi bez nabavne cene",
        value: fmtNumber(report.issues.missingCostCount, 0, "-"),
        metricKey: "missingCostCount" as const,
      },
      {
        label: "Prihod bez nabavne cene",
        value: fmtPctFromRatio(report.impact.revenueWithoutCostPercent, 1, "-"),
        metricKey: "revenueWithoutCost" as const,
      },
      {
        label: "Blokirane preporuke",
        value: fmtNumber(report.impact.recommendationsBlockedCount, 0, "-"),
        metricKey: "blockedRecommendationsCount" as const,
      },
      {
        label: "Ignorisani redovi",
        value: fmtNumber(report.impact.ignoredRowsCount, 0, "-"),
        metricKey: "ignoredRowsCount" as const,
      },
    ];
  }, [report]);

  const durableKpiCards = useMemo(() => {
    if (!durableReport?.kpis || durableReport.kpis.length === 0) return [];
    return durableReport.kpis.map((kpi) => {
      const resolvedMetricKey =
        findAnalyticsMetricKeyByLabel(kpi.label)
        ?? findAnalyticsMetricKeyByLabel(kpi.key)
        ?? kpi.key
        ?? kpi.label;

      return {
        label: kpi.label,
        value: formatDurableValue(kpi.value),
        metricKey: resolvedMetricKey,
      };
    });
  }, [durableReport]);

  const renderDurableSection = (title: string, rows: typeof durableRows, emptyMessage: string) => (
    <section className="pilot-card">
      <h3>{title}</h3>
      {rows.length === 0 ? (
        <p className="pilot-card-note">{emptyMessage}</p>
      ) : (
        <ul>
          {rows.map((row, index) => (
            <li key={`${title}-${row.item}-${index}`}>
              {row.item}: {formatDurableValue(row.value)}
              {row.secondary ? <span className="pilot-list-secondary"> ({row.secondary})</span> : null}
            </li>
          ))}
        </ul>
      )}
    </section>
  );

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
          { label: "Proverite kvalitet podataka", href: "/analytics/data-quality" },
          { label: "Proverite status osvežavanja", href: "/admin/configuration?panel=workers" },
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
          <p>Prodajni/onboarding pregled spremnosti podataka pre prezentacije dashboard-a.</p>
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

      <PilotImportReadinessCard report={report} refreshStatus={refreshStatus} />

      <section className="pilot-card">
        <h3>Trendplus pilot izveštaj kvaliteta podataka</h3>
        <ul>
          <li>Period od: {formatDate(reportPeriodFrom, "-")}</li>
          <li>Period do: {formatDate(reportPeriodTo, "-")}</li>
          <li>Generisano: {formatDateTime(reportGeneratedAt, "-")}</li>
          <li>Poslednje osveženje: {formatDateTime(reportLastRefreshAt, "-")}</li>
          <li>Skor spremnosti podataka: {report ? `${fmtNumber(report.readinessScore, 0, "-")}/100` : durableReadinessScore == null ? "-" : `${fmtNumber(durableReadinessScore, 0, "-")}/100`}</li>
          <li>Status kvaliteta podataka: {reportDataQualityStatus ?? "-"}</li>
        </ul>
      </section>

      {report ? (
        <>
          <section className="pilot-card">
            <h3>Ključni KPI signali</h3>
            <div className="pilot-kpi-grid">
              {intakeKpiCards.map((kpi) => (
                <article key={kpi.label} className="pilot-kpi-card">
                  <span>{kpi.label}</span>
                  <strong>{kpi.value}</strong>
                  <KpiExplainButton
                    metricKey={kpi.metricKey}
                    ariaLabel={`Kako je izračunato: ${kpi.label}`}
                  />
                </article>
              ))}
            </div>
          </section>

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
              {reportIssueSignalState === "clear" ? (
                <p className="pilot-card-note">Nema otvorenih problema u ovom payload-u. Ovo je validno prazno stanje, ne greška.</p>
              ) : reportIssueSignalState === "partial" ? (
                <p className="pilot-card-note">Nije moguće potvrditi da nema problema jer deo signala nije dostupan.</p>
              ) : (
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
              )}
            </section>

            <section className="pilot-card">
              <h3>Uticaj</h3>
              {reportImpactSignalState === "clear" ? (
                <p className="pilot-card-note">Nema dodatnog negativnog uticaja u ovom opsegu. Ovo znači da trenutno nema blokiranih preporuka ni izdvojenih impact signala.</p>
              ) : reportImpactSignalState === "partial" ? (
                <p className="pilot-card-note">Nije moguće potvrditi da nema problema jer deo signala nije dostupan.</p>
              ) : (
                <>
                  <ul>
                    <li>Prihod bez nabavne cene: {fmtPctFromRatio(report.impact.revenueWithoutCostPercent, 1, "-")}</li>
                    <li>Artikli bez dobavljača: {fmtPctFromRatio(report.impact.articlesWithoutSupplierPercent, 1, "-")}</li>
                    <li>Blokirane preporuke: {fmtNumber(report.impact.recommendationsBlockedCount, 0, "-")}</li>
                    <li>Ignorisani redovi: {fmtNumber(report.impact.ignoredRowsCount, 0, "-")}</li>
                    <li>Nedovoljni signali: {fmtNumber(report.impact.insufficientSignalCount, 0, "-")}</li>
                  </ul>
                  <p className="pilot-card-note">Visok procenat prihoda bez cene ili veliki broj blokiranih preporuka direktno smanjuje pouzdanost maržnih odluka.</p>
                </>
              )}
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

      {!report && durableReport ? (
        <>
          {durableKpiCards.length > 0 ? (
            <section className="pilot-card">
              <h3>Ključni KPI signali</h3>
              <div className="pilot-kpi-grid">
                {durableKpiCards.map((kpi, index) => (
                  <article key={`${kpi.label}-${index}`} className="pilot-kpi-card">
                    <span>{kpi.label}</span>
                    <strong>{kpi.value}</strong>
                    <KpiExplainButton
                      metricKey={kpi.metricKey}
                      ariaLabel={`Kako je izračunato: ${kpi.label}`}
                    />
                  </article>
                ))}
              </div>
            </section>
          ) : null}

          <article className={`pilot-intake-score ${durableReadinessTone}`}>
            <div>
              <span>Skor spremnosti</span>
              <strong>{durableReadinessScore == null ? "-" : `${fmtNumber(durableReadinessScore, 0, "-")}/100`}</strong>
              <p>{durableReadinessLabel ?? "Nije dostupno"}</p>
            </div>
            <div className="pilot-intake-thresholds">
              <span>90-100: Spremno za pouzdanu analitiku</span>
              <span>70-89: Upotrebljivo uz upozorenja</span>
              <span>40-69: Pilot može, preporuke ograničene</span>
              <span>&lt;40: Prvo srediti podatke</span>
            </div>
          </article>

          <div className="pilot-intake-grid">
            {renderDurableSection("Učitano", groupedDurableRows.loaded, "Detalji o učitanim podacima nisu dostupni u ovom payload-u.")}
            {renderDurableSection("Problemi", groupedDurableRows.issues, "Nema eksplicitnih problema u trajnom payload-u za ovaj period.")}
            {renderDurableSection("Uticaj", groupedDurableRows.impact, "Uticaj nije eksplicitno opisan u trajnom payload-u.")}
          </div>

          <section className="pilot-card">
            <h3>Preporučene akcije</h3>
            {durableActions.length === 0 ? (
              <p className="pilot-card-note">Nema preporučenih akcija za trenutni opseg. Proverite detalje kvaliteta podataka i osvežavanje.</p>
            ) : (
              <div className="pilot-actions-list">
                {durableActions.map((action, index) => (
                  <Link key={`${action.title}-${index}`} to={action.href || mapActionHref(action.title)} className="pilot-action-item">
                    <strong>{action.title}</strong>
                    <span>{action.description || "Otvori povezani ekran"}</span>
                  </Link>
                ))}
              </div>
            )}
          </section>
        </>
      ) : null}

      {durableReport?.warnings && durableReport.warnings.length > 0 ? (
        <section className="pilot-card">
          <h3>Upozorenja iz trajnog reporta</h3>
          <p className="pilot-card-note">{durableReport.warnings.join(" | ")}</p>
        </section>
      ) : null}

      <details className="pilot-methodology no-print">
        <summary>Metodologija</summary>
        <MetricMethodologyPanel metricKeys={methodologyKeys} dataQualityHref="/analytics/data-quality" />
      </details>

      {durableReport ? (
        <p className="pilot-card-note no-print">
          <strong>Metodologija backend payload-a:</strong> {durableMethodologySummary(durableReport)}
        </p>
      ) : null}
    </section>
  );
}

export { impactSignalState, issueSignalState, resolveTrustSignalState };



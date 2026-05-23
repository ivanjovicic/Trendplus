import {
  downloadExport,
  generateExport,
  requestPrintPreview,
  resolveApiUrl,
  waitForExport,
  type ExportOrientation,
} from "./exportApi";
import { resolveAnalyticsTablePayload } from "./analyticsTableState";
import type { AnalyticsNamedValue, ResolvedAnalyticsTablePayload } from "../types/analyticsTable";
import type { AnalyticsFreshnessStatus, AnalyticsResponseMeta } from "../types/analytics";
import type { ScorecardTrustMetadata, SummaryResponse } from "./supplierDecisionHubApi";
import { dataQualityStatusLabel, normalizeDataQualityStatus } from "../utils/analyticsQuality";
import { fmtPct, fmtRsd } from "../utils/analyticsFormatters";

export type SupplierDecisionReportRow = {
  supplierId: number;
  supplierName: string;
  revenue: number;
  sharePct: number;
  preMarkdownMarginPct: number;
  marginContribution: number;
  status: string;
  statusReason: string;
  normalizedConfidence: number;
  confidenceAvailable: boolean;
  reliabilityPct: number;
  reliabilityAvailable: boolean;
  dataQualityStatus: string;
  reasonCodes: string[];
  unsoldStockValue: number;
  deadStockRate: number;
};

export type SupplierDecisionReportBuildInput = {
  periodLabel: string;
  fromDate: string;
  toDate: string;
  supplierLabel: string;
  dataScopeLabel: string;
  freshnessStatus?: AnalyticsFreshnessStatus | string | null;
  lastRefreshAtUtc?: string | null;
  summary: SummaryResponse | null;
  trustMetadata: ScorecardTrustMetadata | null;
  scorecardMeta: AnalyticsResponseMeta | null;
  totalRevenue: number;
  totalMarginContribution: number;
  top5SharePct: number;
  supplierCounts: {
    boost: number;
    keep: number;
    caution: number;
    reduce: number;
    insufficient: number;
  };
  rows: SupplierDecisionReportRow[];
};

function normalizeFreshnessLabel(value: string | null | undefined): string {
  const normalized = (value ?? "").trim().toLowerCase();
  if (normalized === "fresh") return "Sveze";
  if (normalized === "stale") return "Zastarelo";
  if (normalized === "critical") return "Kriticno";
  return "Nije poznato";
}

function safeDate(value: string | null | undefined): string {
  if (!value) return "n/a";
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return value;
  return parsed.toLocaleString("sr-RS");
}

function buildSectionRow(section: string, item: string, value: string, secondary = "", note = "") {
  return { section, item, value, secondary, note };
}

export function buildSupplierDecisionReportPayload(input: SupplierDecisionReportBuildInput): ResolvedAnalyticsTablePayload {
  const nowUtc = new Date().toISOString();
  const trust = input.trustMetadata;
  const meta = input.scorecardMeta;
  const topRevenueRows = [...input.rows].sort((a, b) => b.revenue - a.revenue).slice(0, 5);
  const riskRows = [...input.rows]
    .sort((a, b) => (b.unsoldStockValue + b.deadStockRate * 1000) - (a.unsoldStockValue + a.deadStockRate * 1000))
    .slice(0, 5);
  const reduceRows = input.rows.filter((row) => row.status === "do_not_trust").slice(0, 5);
  const boostRows = input.rows.filter((row) => row.status === "increase_focus").slice(0, 5);

  const detailRows = [
    buildSectionRow("Header", "Report", "Trendplus Supplier Decision Report", "", ""),
    buildSectionRow("Header", "Dobavljac", input.supplierLabel, "", ""),
    buildSectionRow("Header", "Period", `${input.fromDate} - ${input.toDate}`, input.periodLabel, ""),
    buildSectionRow("Header", "Data scope", input.dataScopeLabel, "", ""),
    buildSectionRow("Header", "Datum izvestaja", safeDate(nowUtc), "", ""),
    buildSectionRow("Header", "Poslednji refresh", safeDate(input.lastRefreshAtUtc ?? trust?.lastRefreshAtUtc), normalizeFreshnessLabel(input.freshnessStatus), ""),
    buildSectionRow("Header", "Data quality", dataQualityStatusLabel(meta?.dataQualityStatus), trust?.dataCoverageStatus ?? "", ""),
    buildSectionRow("KPI", "Prihod", fmtRsd(input.totalRevenue), "", ""),
    buildSectionRow("KPI", "Marzni doprinos", fmtRsd(input.totalMarginContribution), "", ""),
    buildSectionRow("KPI", "Dobavljaca", String(input.summary?.supplierCount ?? input.rows.length), "", ""),
    buildSectionRow("KPI", "Top 5 udeo", fmtPct(input.top5SharePct, 1), "", ""),
    buildSectionRow(
      "Preporuke",
      "Raspodela",
      `Pojacaj ${input.supplierCounts.boost} | Zadrzi ${input.supplierCounts.keep} | Oprez ${input.supplierCounts.caution} | Smanji ${input.supplierCounts.reduce} | Nedovoljno ${input.supplierCounts.insufficient}`,
      "",
      ""
    ),
  ];

  for (const row of topRevenueRows) {
    detailRows.push(buildSectionRow("Top artikli / dobavljaci", row.supplierName, fmtRsd(row.revenue), `Marza ${fmtPct(row.preMarkdownMarginPct * 100, 1)}`, row.statusReason));
  }

  for (const row of riskRows) {
    detailRows.push(buildSectionRow("Rizik zalihe", row.supplierName, fmtRsd(row.unsoldStockValue), `Dead stock ${fmtPct(row.deadStockRate * 100, 1)}`, row.reasonCodes.join(", ")));
  }

  for (const row of boostRows) {
    detailRows.push(buildSectionRow("Pojacaj", row.supplierName, fmtRsd(row.revenue), `Pouzdanost ${row.reliabilityAvailable ? fmtPct(row.reliabilityPct, 0) : "n/a"}`, row.statusReason));
  }

  for (const row of reduceRows) {
    detailRows.push(buildSectionRow("Smanji", row.supplierName, fmtRsd(row.revenue), `Confidence ${row.confidenceAvailable ? fmtPct(row.normalizedConfidence, 0) : "n/a"}`, row.statusReason));
  }

  detailRows.push(
    buildSectionRow("Data quality", "Missing supplier count", String(trust?.missingSupplierNameCount ?? 0), "", ""),
    buildSectionRow("Data quality", "Ignored rows", String(trust?.ignoredRowCount ?? 0), "", ""),
    buildSectionRow("Data quality", "Row count", String(trust?.rowCount ?? input.rows.length), "", ""),
    buildSectionRow("Data quality", "Coverage", trust?.dataCoverageStatus ?? normalizeDataQualityStatus(meta?.dataQualityStatus), trust?.effectivePeriodLabel ?? "", ""),
    buildSectionRow(
      "Methodology",
      "Opis",
      "Preporuka kombinuje promet, marzni doprinos, zavisnost od nivelacija, rizik zaliha i pouzdanost signala.",
      "",
      "Videti docs/ANALYTICS_SEMANTIC_GUARDRAILS.md"
    )
  );

  if (meta?.dataQualityStatus === "insufficient_data" || trust?.recommendationAllowed === false) {
    detailRows.push(
      buildSectionRow(
        "Upozorenje",
        "Nedovoljno podataka",
        "Sistem ne daje finalnu preporuku za izabrani opseg.",
        "",
        meta?.message ?? trust?.dataNote ?? "Nedovoljno podataka za pouzdanu preporuku."
      )
    );
  }

  if (meta?.isPartial || trust?.usedFallback) {
    detailRows.push(
      buildSectionRow(
        "Upozorenje",
        "Delimicni/fallback podaci",
        "Prikazani su delimicni ili fallback podaci.",
        trust?.effectivePeriodLabel ?? "",
        meta?.warningMessage ?? meta?.message ?? trust?.fallbackReason ?? ""
      )
    );
  }

  const filters: AnalyticsNamedValue[] = [
    { key: "supplier", label: "Dobavljac", value: input.supplierLabel },
    { key: "period", label: "Period", value: `${input.fromDate} - ${input.toDate}` },
    { key: "periodLabel", label: "Period label", value: input.periodLabel },
    { key: "dataScope", label: "Data scope", value: input.dataScopeLabel },
  ];

  const metadata: AnalyticsNamedValue[] = [
    { key: "generatedAtUtc", label: "Generated", value: nowUtc },
    { key: "lastRefreshAtUtc", label: "Last refresh", value: input.lastRefreshAtUtc ?? trust?.lastRefreshAtUtc ?? null },
    { key: "dataFreshness", label: "Freshness", value: normalizeFreshnessLabel(input.freshnessStatus) },
    { key: "dataQualityStatus", label: "Data quality", value: dataQualityStatusLabel(meta?.dataQualityStatus) },
    { key: "recommendationAllowed", label: "Recommendation allowed", value: trust?.recommendationAllowed ?? false },
  ];

  return resolveAnalyticsTablePayload({
    tableKey: "supplier-decision-report",
    tableTitle: "Trendplus Supplier Decision Report",
    documentType: "supplier-decision-report",
    templateName: "analytics-table-default",
    columns: [
      { key: "section", header: "Sekcija", dataType: "text" },
      { key: "item", header: "Stavka", dataType: "text" },
      { key: "value", header: "Vrednost", dataType: "text" },
      { key: "secondary", header: "Kontekst", dataType: "text" },
      { key: "note", header: "Napomena", dataType: "text" },
    ],
    rows: detailRows,
    filters,
    metadata,
    locale: "sr-RS",
  });
}

async function runDocumentExport(payload: ResolvedAnalyticsTablePayload, format: "pdf" | "xlsx") {
  const result = await generateExport(payload, {
    format,
    orientation: "landscape",
    includeFiltersAndMetadata: true,
  });

  if (result.isAsync) {
    const completed = await waitForExport(result.documentId);
    if (completed.downloadUrl) {
      downloadExport(completed.downloadUrl, completed.fileName);
    }
    return;
  }

  if (result.downloadUrl) {
    downloadExport(result.downloadUrl, result.fileName);
  }
}

export async function exportSupplierDecisionReportPdf(payload: ResolvedAnalyticsTablePayload): Promise<void> {
  await runDocumentExport(payload, "pdf");
}

export async function exportSupplierDecisionReportExcel(payload: ResolvedAnalyticsTablePayload): Promise<void> {
  await runDocumentExport(payload, "xlsx");
}

export async function openSupplierDecisionPrintPreview(
  payload: ResolvedAnalyticsTablePayload,
  orientation: ExportOrientation = "landscape"
): Promise<void> {
  const previewResult = await requestPrintPreview(payload, {
    format: "pdf",
    orientation,
    includeFiltersAndMetadata: true,
    preview: true,
    forceAsync: false,
  });

  if (previewResult.printUrl) {
    window.open(resolveApiUrl(previewResult.printUrl), "_blank", "noopener");
  }
}

function escapeCsv(value: unknown): string {
  if (value == null) return "";
  const text = String(value);
  if (/[",\n;]/.test(text)) {
    return `"${text.replace(/"/g, "\"\"")}"`;
  }
  return text;
}

function downloadTextFile(fileName: string, content: string, mimeType: string) {
  const blob = new Blob([content], { type: `${mimeType};charset=utf-8` });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}

export function exportSupplierDecisionReportCsv(payload: ResolvedAnalyticsTablePayload, fileName?: string) {
  const columns = payload.columns.map((c) => c.key);
  const header = payload.columns.map((c) => c.header || c.key);
  const lines = [
    header.map(escapeCsv).join(","),
    ...payload.rows.map((row) => columns.map((key) => escapeCsv((row as Record<string, unknown>)[key])).join(",")),
  ];

  const safeName = (fileName ?? `supplier-decision-report-${new Date().toISOString().slice(0, 10)}.csv`)
    .replace(/[^\w.-]+/g, "_");

  downloadTextFile(safeName, lines.join("\n"), "text/csv");
}

export function buildSupplierDecisionReportSummaryText(payload: ResolvedAnalyticsTablePayload): string {
  const get = (section: string, item: string) => {
    const found = payload.rows.find((row) => String(row.section) === section && String(row.item) === item);
    if (!found) return null;
    const value = found.value == null ? "" : String(found.value);
    return value.trim() ? value : null;
  };

  const supplier = get("Header", "Dobavljac") ?? payload.filters.find((f) => f.key === "supplier")?.value ?? "Dobavljac";
  const period = get("Header", "Period") ?? payload.filters.find((f) => f.key === "period")?.value ?? "";
  const revenue = get("KPI", "Prihod");
  const margin = get("KPI", "Marzni doprinos");
  const top5 = get("KPI", "Top 5 udeo");
  const distribution = get("Preporuke", "Raspodela");

  const dataQuality = payload.metadata.find((m) => m.key === "dataQualityStatus")?.value ?? null;
  const freshness = payload.metadata.find((m) => m.key === "dataFreshness")?.value ?? null;
  const recommendationAllowed = payload.metadata.find((m) => m.key === "recommendationAllowed")?.value ?? null;

  const lines = [
    `Supplier Decision Report`,
    `Dobavljac: ${String(supplier)}`,
    period ? `Period: ${String(period)}` : null,
    revenue ? `Prihod: ${revenue}` : null,
    margin ? `Marzni doprinos: ${margin}` : null,
    top5 ? `Top 5 udeo: ${top5}` : null,
    distribution ? `Preporuke (raspodela): ${distribution}` : null,
    dataQuality != null ? `Data quality: ${String(dataQuality)}` : null,
    freshness != null ? `Freshness: ${String(freshness)}` : null,
    recommendationAllowed != null ? `Preporuke dozvoljene: ${String(recommendationAllowed)}` : null,
  ].filter((line): line is string => Boolean(line && line.trim()));

  return lines.join("\n");
}

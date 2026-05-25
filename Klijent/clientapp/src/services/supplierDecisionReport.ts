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
  units?: number;
  sharePct: number;
  preMarkdownMarginPct: number;
  markdownRevenueShare?: number;
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
  if (normalized === "fresh") return "Sveže";
  if (normalized === "stale") return "Zastarelo";
  if (normalized === "critical") return "Kritično";
  return "Nije poznato";
}

function safeDate(value: string | null | undefined): string {
  if (!value) return "nije dostupno";
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
  const totalUnits = input.rows.reduce((sum, row) => sum + (row.units ?? 0), 0);
  const totalStockRisk = input.rows.reduce((sum, row) => sum + row.unsoldStockValue, 0);
  const weightedMarkdownDependencyPct = input.totalRevenue > 0
    ? input.rows.reduce((sum, row) => sum + ((row.markdownRevenueShare ?? 0) * row.revenue), 0) / input.totalRevenue
    : 0;
  const reliabilityRows = input.rows.filter((row) => row.reliabilityAvailable);
  const avgReliabilityPct = reliabilityRows.length > 0
    ? reliabilityRows.reduce((sum, row) => sum + row.reliabilityPct, 0) / reliabilityRows.length
    : null;
  const topRevenueRows = [...input.rows].sort((a, b) => b.revenue - a.revenue).slice(0, 5);
  const riskRows = [...input.rows]
    .sort((a, b) => (b.unsoldStockValue + b.deadStockRate * 1000) - (a.unsoldStockValue + a.deadStockRate * 1000))
    .slice(0, 5);
  const reduceRows = input.rows.filter((row) => row.status === "do_not_trust").slice(0, 5);
  const boostRows = input.rows.filter((row) => row.status === "increase_focus").slice(0, 5);

  const detailRows = [
    buildSectionRow("Header", "Naziv izveštaja", "Trendplus izveštaj dobavljača", "", ""),
    buildSectionRow("Header", "Dobavljač", input.supplierLabel, "", ""),
    buildSectionRow("Header", "Period", `${input.fromDate} - ${input.toDate}`, input.periodLabel, ""),
    buildSectionRow("Header", "Opseg podataka", input.dataScopeLabel, "", ""),
    buildSectionRow("Header", "Datum izveštaja", safeDate(nowUtc), "", ""),
    buildSectionRow("Header", "Poslednje osveženje", safeDate(input.lastRefreshAtUtc ?? trust?.lastRefreshAtUtc), normalizeFreshnessLabel(input.freshnessStatus), ""),
    buildSectionRow("Header", "Kvalitet podataka", dataQualityStatusLabel(meta?.dataQualityStatus), trust?.dataCoverageStatus ?? "", ""),
    buildSectionRow("Header", "Traženi period", `${safeDate(trust?.requestedPeriodFrom ?? trust?.requestedFrom)} - ${safeDate(trust?.requestedPeriodTo ?? trust?.requestedTo)}`, trust?.requestedDataset ?? "nije dostupno", ""),
    buildSectionRow("Header", "Efektivni dataset", trust?.effectiveDataset ?? "nije dostupno", trust?.effectivePeriodLabel ?? "", ""),
    buildSectionRow("Header", "Korišćen fallback", trust?.usedFallback ? "Da" : "Ne", trust?.fallbackReason ?? "", ""),
    buildSectionRow("Header", "Preporuka dozvoljena", trust?.recommendationAllowed ? "Da" : "Ne", trust?.dataCoverageStatus ?? "", ""),
    buildSectionRow("KPI", "Prihod", fmtRsd(input.totalRevenue), "", ""),
    buildSectionRow("KPI", "Maržni doprinos", fmtRsd(input.totalMarginContribution), "", ""),
    buildSectionRow("KPI", "Broj dobavljača", String(input.summary?.supplierCount ?? input.rows.length), "", ""),
    buildSectionRow("KPI", "Prodate jedinice", totalUnits.toLocaleString("sr-RS"), "", ""),
    buildSectionRow("KPI", "Rizik zaliha", fmtRsd(totalStockRisk), "", ""),
    buildSectionRow("KPI", "Zavisnost od nivelacija", fmtPct(weightedMarkdownDependencyPct * 100, 1), "", ""),
    buildSectionRow("KPI", "Pouzdanost signala", avgReliabilityPct == null ? "nije dostupno" : fmtPct(avgReliabilityPct, 1), "", ""),
    buildSectionRow("KPI", "Top 5 udeo", fmtPct(input.top5SharePct, 1), "", ""),
    buildSectionRow(
      "Preporuke",
      "Raspodela",
      `Pojačaj ${input.supplierCounts.boost} | Zadrži ${input.supplierCounts.keep} | Oprez ${input.supplierCounts.caution} | Smanji ${input.supplierCounts.reduce} | Nedovoljno ${input.supplierCounts.insufficient}`,
      "",
      ""
    ),
    buildSectionRow(
      "Preporuke",
      "Preporuka",
      trust?.recommendationAllowed === false
        ? "Finalna preporuka je blokirana; prikazan je pomoćni scorecard signal."
        : "Finalna preporuka aktivna",
      trust?.dataCoverageStatus ?? "",
      trust?.fallbackReason ?? trust?.dataNote ?? ""
    ),
  ];

  for (const row of topRevenueRows) {
    detailRows.push(buildSectionRow("Top artikli / dobavljači", row.supplierName, fmtRsd(row.revenue), `Marža ${fmtPct(row.preMarkdownMarginPct * 100, 1)}`, row.statusReason));
  }

  for (const row of riskRows) {
    detailRows.push(buildSectionRow("Rizik zalihe", row.supplierName, fmtRsd(row.unsoldStockValue), `Dead stock ${fmtPct(row.deadStockRate * 100, 1)}`, row.reasonCodes.join(", ")));
  }

  for (const row of boostRows) {
    detailRows.push(buildSectionRow("Pojačaj", row.supplierName, fmtRsd(row.revenue), `Pouzdanost ${row.reliabilityAvailable ? fmtPct(row.reliabilityPct, 0) : "nije dostupno"}`, row.statusReason));
  }

  for (const row of reduceRows) {
    detailRows.push(buildSectionRow("Smanji", row.supplierName, fmtRsd(row.revenue), `Sigurnost ${row.confidenceAvailable ? fmtPct(row.normalizedConfidence, 0) : "nije dostupno"}`, row.statusReason));
  }

  detailRows.push(
    buildSectionRow("Kvalitet podataka", "Nedostajući dobavljači", String(trust?.missingSupplierNameCount ?? 0), "", ""),
    buildSectionRow("Kvalitet podataka", "Ignorisani redovi", String(trust?.ignoredRowCount ?? 0), "", ""),
    buildSectionRow("Kvalitet podataka", "Broj redova", String(trust?.rowCount ?? input.rows.length), "", ""),
    buildSectionRow("Kvalitet podataka", "Status pokrivenosti", trust?.dataCoverageStatus ?? normalizeDataQualityStatus(meta?.dataQualityStatus), trust?.effectivePeriodLabel ?? "", ""),
    buildSectionRow(
      "Metodologija",
      "Opis",
      "Preporuka kombinuje promet, maržni doprinos, zavisnost od nivelacija, rizik zaliha i pouzdanost signala.",
      "",
      "Kako čitati ovaj izveštaj: /analytics/data-quality"
    )
  );

  if (!trust) {
    detailRows.push(
      buildSectionRow(
        "Kvalitet podataka",
        "Detaljan sažetak",
        "Detaljan sažetak kvaliteta podataka nije dostupan u ovom report payload-u. Otvorite Data Quality ekran za detalje.",
        "",
        ""
      )
    );
  }

  if (meta?.dataQualityStatus === "insufficient_data" || trust?.recommendationAllowed === false) {
    detailRows.push(
      buildSectionRow(
        "Upozorenje",
        "Nedovoljno podataka",
        "Report prikazuje pomoćni scorecard signal, ne finalnu preporuku.",
        "",
        meta?.message ?? trust?.dataNote ?? "Nedovoljno podataka za pouzdanu preporuku."
      )
    );
  }

  if (trust?.recommendationAllowed === false) {
    detailRows.push(
      buildSectionRow(
        "Upozorenje",
        "Pomoćni scorecard signal",
        "Report prikazuje pomoćni scorecard signal, ne finalnu preporuku.",
        trust?.effectivePeriodLabel ?? trust?.effectiveDataset ?? "",
        trust?.fallbackReason ?? ""
      )
    );
  }

  if (meta?.isPartial || trust?.usedFallback) {
    detailRows.push(
      buildSectionRow(
        "Upozorenje",
        "Delimični/fallback podaci",
        "Prikazani su delimični ili fallback podaci.",
        trust?.effectivePeriodLabel ?? "",
        meta?.warningMessage ?? meta?.message ?? trust?.fallbackReason ?? ""
      )
    );
  }

  if (trust?.usedFallback) {
    detailRows.push(
      buildSectionRow(
        "Upozorenje",
        "Korišćen širi period",
        `Korišćen je širi period (${trust.effectiveDataset}) zbog nedostatka podataka za traženi period.`,
        trust.fallbackReasonCode ?? "",
        trust.fallbackReason ?? ""
      )
    );
  }

  const filters: AnalyticsNamedValue[] = [
    { key: "supplier", label: "Dobavljač", value: input.supplierLabel },
    { key: "period", label: "Period", value: `${input.fromDate} - ${input.toDate}` },
    { key: "periodLabel", label: "Oznaka perioda", value: input.periodLabel },
    { key: "dataScope", label: "Opseg podataka", value: input.dataScopeLabel },
  ];

  const metadata: AnalyticsNamedValue[] = [
    { key: "generatedAtUtc", label: "Generisano", value: nowUtc },
    { key: "lastRefreshAtUtc", label: "Poslednje osveženje", value: input.lastRefreshAtUtc ?? trust?.lastRefreshAtUtc ?? null },
    { key: "dataFreshness", label: "Svežina podataka", value: normalizeFreshnessLabel(input.freshnessStatus) },
    { key: "dataQualityStatus", label: "Kvalitet podataka", value: dataQualityStatusLabel(meta?.dataQualityStatus) },
    { key: "requestedDataset", label: "Traženi dataset", value: trust?.requestedDataset ?? null },
    { key: "effectiveDataset", label: "Efektivni dataset", value: trust?.effectiveDataset ?? null },
    { key: "usedFallback", label: "Korišćen fallback", value: trust?.usedFallback ?? false },
    { key: "fallbackReason", label: "Razlog fallback-a", value: trust?.fallbackReason ?? null },
    { key: "recommendationAllowed", label: "Preporuka dozvoljena", value: trust?.recommendationAllowed ?? false },
  ];

  return resolveAnalyticsTablePayload({
    tableKey: "supplier-decision-report",
    tableTitle: "Trendplus izveštaj dobavljača",
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

  const safeName = (fileName ?? `trendplus-izvestaj-dobavljaca-${new Date().toISOString().slice(0, 10)}.csv`)
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

  const supplier = get("Header", "Dobavljač")
    ?? get("Header", "Dobavljac")
    ?? payload.filters.find((f) => f.key === "supplier")?.value
    ?? "Dobavljač";
  const period = get("Header", "Period") ?? payload.filters.find((f) => f.key === "period")?.value ?? "";
  const revenue = get("KPI", "Prihod");
  const margin = get("KPI", "Maržni doprinos") ?? get("KPI", "Marzni doprinos");
  const top5 = get("KPI", "Top 5 udeo");
  const distribution = get("Preporuke", "Raspodela");

  const dataQuality = payload.metadata.find((m) => m.key === "dataQualityStatus")?.value ?? null;
  const freshness = payload.metadata.find((m) => m.key === "dataFreshness")?.value ?? null;
  const effectiveDataset = payload.metadata.find((m) => m.key === "effectiveDataset")?.value ?? null;
  const usedFallback = payload.metadata.find((m) => m.key === "usedFallback")?.value ?? null;
  const fallbackReason = payload.metadata.find((m) => m.key === "fallbackReason")?.value ?? null;
  const recommendationAllowed = payload.metadata.find((m) => m.key === "recommendationAllowed")?.value ?? null;

  const lines = [
    `Trendplus izveštaj dobavljača`,
    `Dobavljač: ${String(supplier)}`,
    period ? `Period: ${String(period)}` : null,
    revenue ? `Prihod: ${revenue}` : null,
    margin ? `Maržni doprinos: ${margin}` : null,
    top5 ? `Top 5 udeo: ${top5}` : null,
    distribution ? `Preporuke (raspodela): ${distribution}` : null,
    dataQuality != null ? `Kvalitet podataka: ${String(dataQuality)}` : null,
    freshness != null ? `Svežina podataka: ${String(freshness)}` : null,
    effectiveDataset != null ? `Efektivni dataset: ${String(effectiveDataset)}` : null,
    usedFallback != null ? `Fallback aktivan: ${String(usedFallback)}` : null,
    fallbackReason != null && String(fallbackReason).trim() ? `Fallback razlog: ${String(fallbackReason)}` : null,
    recommendationAllowed != null ? `Preporuke dozvoljene: ${String(recommendationAllowed)}` : null,
    recommendationAllowed === false ? "Report prikazuje pomoćni scorecard signal, ne finalnu preporuku." : null,
  ].filter((line): line is string => Boolean(line && line.trim()));

  return lines.join("\n");
}

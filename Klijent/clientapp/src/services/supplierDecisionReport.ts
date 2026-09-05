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
import type { SummaryResponse } from "./supplierDecisionHubApi";
import { dataQualityStatusLabel, normalizeDataQualityStatus } from "../utils/analyticsQuality";
import { fmtPct, fmtRsd } from "../utils/analyticsFormatters";
import { buildPeriodLineageLabel } from "../utils/analyticsPeriodLineage";
import { formatMetricDisplayValue } from "../utils/analyticsMetricValue";
import { recommendationReasonLabel } from "../utils/canonicalRecommendationSemantics";

type ScorecardTrustMetadata = {
  lastRefreshAtUtc?: string | null;
  requestedPeriodFrom?: string | null;
  requestedPeriodTo?: string | null;
  requestedFrom?: string | null;
  requestedTo?: string | null;
  requestedDataset?: string | null;
  effectiveDataset?: string | null;
  effectivePeriodLabel?: string | null;
  provenanceBasis?: string | null;
  usedFallback?: boolean;
  fallbackReason?: string | null;
  fallbackReasonCode?: string | null;
  recommendationAllowed?: boolean;
  dataCoverageStatus?: string | null;
  dataNote?: string | null;
  missingSupplierNameCount?: number;
  ignoredRowCount?: number;
  rowCount?: number;
};

export type SupplierDecisionReportRow = {
  supplierId: number;
  supplierName: string;
  revenue: number;
  units?: number;
  sharePct: number | null;
  preMarkdownMarginPct: number;
  markdownRevenueShare?: number;
  marginContribution: number;
  status: string;
  statusReason: string;
  normalizedConfidence: number | null;
  confidenceAvailable: boolean;
  reliabilityPct: number | null;
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
  top5SharePct: number | null;
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
  const recommendationAllowed = trust?.recommendationAllowed === true;
  const totalUnits = input.rows.reduce((sum, row) => sum + (row.units ?? 0), 0);
  const totalStockRisk = input.rows.reduce((sum, row) => sum + row.unsoldStockValue, 0);
  const weightedMarkdownDependencyPct = input.totalRevenue > 0
    ? input.rows.reduce((sum, row) => sum + ((row.markdownRevenueShare ?? 0) * row.revenue), 0) / input.totalRevenue
    : null;
  const confidenceRows = recommendationAllowed ? input.rows.filter((row) => row.confidenceAvailable) : [];
  const avgConfidencePct = confidenceRows.length > 0
    ? confidenceRows.reduce((sum, row) => sum + (row.normalizedConfidence ?? 0), 0) / confidenceRows.length
    : null;
  const reliabilityRows = recommendationAllowed ? input.rows.filter((row) => row.reliabilityAvailable) : [];
  const avgReliabilityPct = reliabilityRows.length > 0
    ? reliabilityRows.reduce((sum, row) => sum + (row.reliabilityPct ?? 0), 0) / reliabilityRows.length
    : null;
  const reasonCodePreview = Array.from(new Set(input.rows.flatMap((row) => row.reasonCodes ?? []).filter((code) => Boolean(String(code).trim()))))
    .slice(0, 8)
    .map(recommendationReasonLabel);
  const topRevenueRows = [...input.rows].sort((a, b) => b.revenue - a.revenue).slice(0, 5);
  const riskRows = [...input.rows]
    .sort((a, b) => (b.unsoldStockValue + b.deadStockRate * 1000) - (a.unsoldStockValue + a.deadStockRate * 1000))
    .slice(0, 5);
  const reduceRows = input.rows.filter((row) => row.status === "do_not_trust").slice(0, 5);
  const boostRows = input.rows.filter((row) => row.status === "increase_focus").slice(0, 5);
  const observedFromUtc = input.summary?.from ?? null;
  const observedToUtc = input.summary?.to ?? null;
  const requestedFromUtc = trust?.requestedPeriodFrom ?? trust?.requestedFrom ?? `${input.fromDate}T00:00:00Z`;
  const requestedToUtc = trust?.requestedPeriodTo ?? trust?.requestedTo ?? `${input.toDate}T00:00:00Z`;
  const effectiveFromUtc = input.summary?.from ?? null;
  const effectiveToUtc = input.summary?.to ?? null;
  const periodLineageLabel = buildPeriodLineageLabel({
    effectivePeriodLabel: trust?.effectivePeriodLabel ?? input.periodLabel,
    effectiveFromUtc,
    effectiveToUtc,
    observedFromUtc,
    observedToUtc,
  });

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
    buildSectionRow("Header", "Posmatrani period", periodLineageLabel ?? "nije dostupno", "", ""),
    buildSectionRow("Header", "Korišćen fallback", trust?.usedFallback ? "Da" : "Ne", trust?.fallbackReason ?? "", ""),
    buildSectionRow("Header", "Preporuka dozvoljena", trust?.recommendationAllowed ? "Da" : "Ne", trust?.dataCoverageStatus ?? "", ""),
    buildSectionRow("KPI", "Prihod", fmtRsd(input.totalRevenue), "", ""),
    buildSectionRow("KPI", "Maržni doprinos", fmtRsd(input.totalMarginContribution), "", ""),
    buildSectionRow("KPI", "Broj dobavljača", String(input.summary?.supplierCount ?? input.rows.length), "", ""),
    buildSectionRow("KPI", "Prodate jedinice", totalUnits.toLocaleString("sr-RS"), "", ""),
    buildSectionRow("KPI", "Rizik zaliha", fmtRsd(totalStockRisk), "", ""),
    buildSectionRow("KPI", "Zavisnost od nivelacija", formatMetricDisplayValue({ value: weightedMarkdownDependencyPct, kind: "ratioPercent" }), "", ""),
    buildSectionRow("KPI", "Sigurnost signala", formatMetricDisplayValue({ value: avgConfidencePct, kind: "percent" }), "", ""),
    buildSectionRow("KPI", "Pouzdanost signala", formatMetricDisplayValue({ value: avgReliabilityPct, kind: "percent" }), "", ""),
    buildSectionRow("KPI", "Top 5 udeo", formatMetricDisplayValue({ value: input.top5SharePct, kind: "percent" }), "", ""),
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
      !recommendationAllowed
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
    detailRows.push(buildSectionRow("Rizik zalihe", row.supplierName, fmtRsd(row.unsoldStockValue), `Dead stock ${fmtPct(row.deadStockRate * 100, 1)}`, row.reasonCodes.map(recommendationReasonLabel).join(", ")));
  }

  for (const row of boostRows) {
    detailRows.push(buildSectionRow("Pojačaj", row.supplierName, fmtRsd(row.revenue), `Pouzdanost ${row.reliabilityAvailable ? formatMetricDisplayValue({ value: row.reliabilityPct, kind: "percent", digits: 0, fallback: "nije dostupno" }) : "nije dostupno"}`, row.statusReason));
  }

  for (const row of reduceRows) {
    detailRows.push(buildSectionRow("Smanji", row.supplierName, fmtRsd(row.revenue), `Sigurnost ${row.confidenceAvailable ? formatMetricDisplayValue({ value: row.normalizedConfidence, kind: "percent", digits: 0, fallback: "nije dostupno" }) : "nije dostupno"}`, row.statusReason));
  }

  const topMarginRows = [...input.rows]
    .sort((a, b) => b.marginContribution - a.marginContribution)
    .slice(0, 3);
  const markdownDependentRows = input.rows
    .filter((row) => (row.markdownRevenueShare ?? 0) >= 0.5)
    .sort((a, b) => (b.markdownRevenueShare ?? 0) - (a.markdownRevenueShare ?? 0))
    .slice(0, 3);
  const slowStockRows = [...input.rows]
    .sort((a, b) => b.unsoldStockValue - a.unsoldStockValue)
    .slice(0, 3);
  const replenishRows = input.rows
    .filter((row) => row.status === "increase_focus" && row.deadStockRate <= 0.2)
    .sort((a, b) => b.revenue - a.revenue)
    .slice(0, 3);
  const missingCostSignalDetected = input.rows.some((row) => row.reasonCodes.some((code) => code.toLowerCase().includes("missing_cost")));

  detailRows.push(
    buildSectionRow("supplier_negotiation_pack", "Dobavljač", input.supplierLabel, "Sažetak", ""),
    buildSectionRow("supplier_negotiation_pack", "Prihod", fmtRsd(input.totalRevenue), "Sažetak", ""),
    buildSectionRow("supplier_negotiation_pack", "Maržni doprinos", fmtRsd(input.totalMarginContribution), "Sažetak", ""),
    buildSectionRow("supplier_negotiation_pack", "Prodate jedinice", totalUnits.toLocaleString("sr-RS"), "Sažetak", ""),
    buildSectionRow("supplier_negotiation_pack", "Lager u riziku", fmtRsd(totalStockRisk), "Sažetak", ""),
    buildSectionRow("supplier_negotiation_pack", "Zavisnost od nivelacija", formatMetricDisplayValue({ value: weightedMarkdownDependencyPct, kind: "ratioPercent" }), "Sažetak", ""),
    buildSectionRow("supplier_negotiation_pack", "Preporuka dozvoljena", trust?.recommendationAllowed ? "Da" : "Ne", "Sažetak", ""),
    buildSectionRow("supplier_negotiation_pack", "Korišćen fallback", trust?.usedFallback ? "Da" : "Ne", "Sažetak", trust?.effectivePeriodLabel ?? ""),
    buildSectionRow("supplier_negotiation_pack", "Status kvaliteta podataka", trust?.dataCoverageStatus ?? normalizeDataQualityStatus(meta?.dataQualityStatus), "Sažetak", "")
  );

  if (topMarginRows.length > 0) {
    detailRows.push(buildSectionRow("supplier_negotiation_pack", "Najbolji artikli po maržnom doprinosu", topMarginRows.map((row) => row.supplierName).join(", "), "Argumenti", "Top 3 po maržnom doprinosu"));
  }
  if (slowStockRows.length > 0) {
    detailRows.push(buildSectionRow("supplier_negotiation_pack", "Artikli sa sporom zalihom", slowStockRows.map((row) => row.supplierName).join(", "), "Argumenti", "Visok lager u riziku"));
  }
  if (markdownDependentRows.length > 0) {
    detailRows.push(buildSectionRow("supplier_negotiation_pack", "Artikli zavisni od sniženja", markdownDependentRows.map((row) => row.supplierName).join(", "), "Argumenti", "Visoka markdown zavisnost"));
  }
  if (replenishRows.length > 0) {
    detailRows.push(buildSectionRow("supplier_negotiation_pack", "Artikli za dopunu", replenishRows.map((row) => row.supplierName).join(", "), "Argumenti", "Stabilna tražnja i nizak dead stock"));
  }
  if (reduceRows.length > 0) {
    detailRows.push(buildSectionRow("supplier_negotiation_pack", "Artikli za smanjenje narudžbine", reduceRows.map((row) => row.supplierName).join(", "), "Argumenti", "Signal za smanjenje fokusa"));
  }
  if ((trust?.missingSupplierNameCount ?? 0) > 0) {
    detailRows.push(buildSectionRow("supplier_negotiation_pack", "Artikli sa missing supplier problemom", String(trust?.missingSupplierNameCount ?? 0), "Argumenti", "Nedostaje dobavljač i signal je manje pouzdan"));
  }
  if (missingCostSignalDetected) {
    detailRows.push(buildSectionRow("supplier_negotiation_pack", "Artikli sa missing cost problemom", "Maržni doprinos može biti nepouzdan", "Argumenti", "Detektovan missing_cost signal u reasonCodes."));
  }

  detailRows.push(
    buildSectionRow("supplier_negotiation_pack", "Pojačaj saradnju", input.supplierCounts.boost > input.supplierCounts.reduce ? "Preporučeno" : "Razmotriti", "Predlog razgovora", "Fokus na artikle sa stabilnim signalom rasta."),
    buildSectionRow("supplier_negotiation_pack", "Zadrži", input.supplierCounts.keep > 0 ? "Razmotriti" : "Nije prioritet", "Predlog razgovora", "Stabilan učinak bez eskalacije."),
    buildSectionRow("supplier_negotiation_pack", "Pregovaraj bolje uslove", input.supplierCounts.caution > 0 ? "Preporučeno" : "Razmotriti", "Predlog razgovora", "Marža i markdown signal ukazuju na prostor za pregovor."),
    buildSectionRow("supplier_negotiation_pack", "Smanji narednu narudžbinu", input.supplierCounts.reduce > 0 ? "Preporučeno" : "Razmotriti", "Predlog razgovora", "Signal upozorava na rizičan asortiman."),
    buildSectionRow("supplier_negotiation_pack", "Traži zamenu/povrat spore robe", slowStockRows.length > 0 ? "Preporučeno" : "Razmotriti", "Predlog razgovora", "Spor obrt i visok lager u riziku."),
    buildSectionRow("supplier_negotiation_pack", "Traži rabat za robu koja se prodaje samo kroz sniženje", markdownDependentRows.length > 0 ? "Preporučeno" : "Razmotriti", "Predlog razgovora", "Zavisnost od sniženja smanjuje kvalitet marže."),
    buildSectionRow(
      "supplier_negotiation_pack",
      "Finalni savet",
      !recommendationAllowed
        ? "Pomoćni signal - proveriti podatke pre odluke"
        : (input.supplierCounts.boost >= input.supplierCounts.reduce ? "Pojačaj saradnju" : "Smanji narednu narudžbinu"),
      "Predlog razgovora",
      !recommendationAllowed
        ? "Finalni savet je blokiran jer recommendationAllowed=false."
        : "Koristiti kao polazni predlog razgovora sa dobavljačem."
    )
  );

  if (!recommendationAllowed) {
    detailRows.push(buildSectionRow("supplier_negotiation_pack", "Pomoćni signal", "Pomoćni signal - proveriti podatke pre odluke", "Upozorenja", "Finalna preporuka nije dozvoljena za ovaj signal."));
  }
  if (trust?.usedFallback) {
    const fallbackContext = [
      trust?.effectiveDataset ? `dataset: ${trust.effectiveDataset}` : null,
      trust?.effectivePeriodLabel ? `period: ${trust.effectivePeriodLabel}` : null,
      trust?.fallbackReason ? trust.fallbackReason : null,
    ].filter((part): part is string => Boolean(part)).join(" | ");
    detailRows.push(buildSectionRow("supplier_negotiation_pack", "Korišćen fallback dataset", "Da", "Upozorenja", fallbackContext || "Korišćen fallback dataset"));
  }
  if ((meta?.dataQualityStatus ?? trust?.dataCoverageStatus) && normalizeDataQualityStatus(meta?.dataQualityStatus ?? trust?.dataCoverageStatus) !== "good") {
    detailRows.push(buildSectionRow("supplier_negotiation_pack", "Kvalitet podataka nije idealan", dataQualityStatusLabel(meta?.dataQualityStatus), "Upozorenja", meta?.warningMessage ?? meta?.message ?? ""));
    if (missingCostSignalDetected) {
      detailRows.push(buildSectionRow("supplier_negotiation_pack", "Maržni doprinos je procena", "Da", "Upozorenja", "Marža je procena jer deo nabavne cene nije istorijski potvrđen."));
    }
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

  if (meta?.dataQualityStatus === "insufficient_data" || !recommendationAllowed) {
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

  if (!recommendationAllowed) {
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
    { key: "confidencePct", label: "Sigurnost signala", value: avgConfidencePct },
    { key: "reliabilityPct", label: "Pouzdanost signala", value: avgReliabilityPct },
    { key: "requestedDataset", label: "Traženi dataset", value: trust?.requestedDataset ?? null },
    { key: "effectiveDataset", label: "Efektivni dataset", value: trust?.effectiveDataset ?? null },
    { key: "requestedPeriodFromUtc", label: "Traženi period od", value: requestedFromUtc },
    { key: "requestedPeriodToUtc", label: "Traženi period do", value: requestedToUtc },
    { key: "effectivePeriodFromUtc", label: "Efektivni period od", value: effectiveFromUtc },
    { key: "effectivePeriodToUtc", label: "Efektivni period do", value: effectiveToUtc },
    { key: "observedPeriodFromUtc", label: "Posmatrani period od", value: observedFromUtc },
    { key: "observedPeriodToUtc", label: "Posmatrani period do", value: observedToUtc },
    { key: "effectivePeriodLabel", label: "Efektivni period", value: trust?.effectivePeriodLabel ?? input.periodLabel },
    { key: "provenanceBasis", label: "Osnova generisanja", value: trust?.provenanceBasis ?? null },
    { key: "usedFallback", label: "Korišćen fallback", value: trust?.usedFallback ?? false },
    { key: "fallbackReason", label: "Razlog fallback-a", value: trust?.fallbackReason ?? null },
    { key: "recommendationAllowed", label: "Preporuka dozvoljena", value: trust?.recommendationAllowed ?? false },
    { key: "reasonCodesPreview", label: "Šifarnici razloga", value: reasonCodePreview.join(" | ") || null },
  ];

  return resolveAnalyticsTablePayload({
    tableKey: "supplier-decision-report",
    tableTitle: "Trendplus izveštaj dobavljača",
    documentType: "supplier-decision-report",
    templateName: "analytics-table-default",
    methodologyMetricKeys: [
      "revenue",
      "marginContribution",
      "unitsSold",
      "stockAtRisk",
      "markdownDependency",
      "confidencePct",
      "reliabilityPct",
    ],
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
  const recommendationAllowedValue = payload.metadata.find((m) => m.key === "recommendationAllowed")?.value ?? null;
  const recommendationAllowed = recommendationAllowedValue === true
    || ["true", "da"].includes(String(recommendationAllowedValue).trim().toLowerCase());

  const lines = [
    `Trendplus izveštaj dobavljača`,
    `Dobavljač: ${String(supplier)}`,
    period ? `Period: ${String(period)}` : null,
    revenue ? `Prihod: ${revenue}` : null,
    margin ? `Maržni doprinos: ${margin}` : null,
    top5 ? `Top 5 udeo: ${top5}` : null,
    distribution ? `Preporuke (raspodela): ${distribution}` : null,
    ...payload.rows
      .filter((row) => String(row.section) === "supplier_negotiation_pack")
      .map((row) => `${String(row.secondary ?? row.section)} - ${String(row.item)}: ${String(row.value)}`),
    dataQuality != null ? `Kvalitet podataka: ${String(dataQuality)}` : null,
    freshness != null ? `Svežina podataka: ${String(freshness)}` : null,
    effectiveDataset != null ? `Efektivni dataset: ${String(effectiveDataset)}` : null,
    usedFallback != null ? `Fallback aktivan: ${String(usedFallback)}` : null,
    fallbackReason != null && String(fallbackReason).trim() ? `Fallback razlog: ${String(fallbackReason)}` : null,
    recommendationAllowedValue != null ? `Preporuke dozvoljene: ${String(recommendationAllowedValue)}` : null,
    !recommendationAllowed
      ? "Report prikazuje pomoćni scorecard signal, ne finalnu preporuku."
      : null,
  ].filter((line): line is string => Boolean(line && line.trim()));

  return lines.join("\n");
}

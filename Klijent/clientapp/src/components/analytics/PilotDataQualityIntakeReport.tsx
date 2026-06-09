import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import type { AnalyticsNamedValue } from "../../types/analyticsTable";
import type { AnalyticsRefreshStatus, PilotDataQualityIntakeReport, PilotIntakeDurableReport } from "../../types/analytics";
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
import AnalyticsEmptyState from "./AnalyticsEmptyState";
import AnalyticsErrorState from "./AnalyticsErrorState";
import KpiExplainButton from "./KpiExplainButton";
import MetricMethodologyPanel from "./MetricMethodologyPanel";
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

export type PilotImportReadinessStatus = "ready" | "ready_with_warnings" | "not_ready" | "unknown";

type PilotStatusLink = {
  label: string;
  href: string;
};

export type PilotStatusAssessment = {
  status: PilotImportReadinessStatus;
  label: string;
  summary: string;
  tone: "good" | "warning" | "critical";
  reasons: string[];
  nextActions: string[];
  links: PilotStatusLink[];
};

const PILOT_STATUS_LABELS: Record<PilotImportReadinessStatus, string> = {
  ready: "Spremno",
  ready_with_warnings: "Spremno uz upozorenja",
  not_ready: "Nije spremno",
  unknown: "Nepoznato",
};

const PILOT_STATUS_LINKS: PilotStatusLink[] = [
  { label: "Kvalitet podataka", href: "/analytics/data-quality" },
  { label: "Status osvežavanja", href: "/admin/configuration?panel=workers" },
  { label: "Uvoz iz Accessa", href: "/access-import" },
];

const READINESS_WARNING_SHARE_THRESHOLD = 0.02;

function appendUnique(items: string[], value: string | null | undefined) {
  if (!value) return;
  if (!items.includes(value)) {
    items.push(value);
  }
}

function hasUsableSalePeriod(firstSaleDate: string | null | undefined, lastSaleDate: string | null | undefined): boolean {
  if (!firstSaleDate || !lastSaleDate) return false;
  const firstSale = Date.parse(firstSaleDate);
  const lastSale = Date.parse(lastSaleDate);
  return !Number.isNaN(firstSale) && !Number.isNaN(lastSale) && firstSale <= lastSale;
}

function hasRefreshFailure(refreshStatus: AnalyticsRefreshStatus | null, freshnessStatus: string): boolean {
  if (freshnessStatus === "critical") {
    return true;
  }

  const failureAt = Date.parse(refreshStatus?.lastFailureAtUtc ?? "");
  const successAt = Date.parse(refreshStatus?.lastSuccessfulRefreshAtUtc ?? "");

  if (!Number.isNaN(failureAt) && Number.isNaN(successAt)) {
    return true;
  }

  return !Number.isNaN(failureAt) && !Number.isNaN(successAt) && failureAt >= successAt;
}

function buildReadyReasons(report: PilotDataQualityIntakeReport, refreshAtUtc: string | null | undefined): string[] {
  const reasons = [
    "Artikli i stavke prodaje postoje za pilot dashboard.",
    `Prodajni period je upotrebljiv: ${formatDate(report.loadedData.firstSaleDate, "-")} - ${formatDate(report.loadedData.lastSaleDate, "-")}.`,
  ];

  if (report.lastImportStatus) {
    reasons.push(`Poslednji import je u statusu ${report.lastImportStatus}.`);
  } else if (report.lastImportAtUtc) {
    reasons.push(`Poslednji import je zabeležen ${formatDateTime(report.lastImportAtUtc, "-")}.`);
  }

  if (refreshAtUtc) {
    reasons.push(`Poslednje uspešno osvežavanje analitike: ${formatDateTime(refreshAtUtc, "-")}.`);
  }

  reasons.push("Nedostajući trošak ili dobavljač nisu na nivou koji blokira pilot prikaz.");
  return reasons;
}

function buildWarningActions(report: PilotDataQualityIntakeReport, refreshStatus: AnalyticsRefreshStatus | null): string[] {
  const actions: string[] = [];

  for (const action of report.recommendedActions) {
    appendUnique(actions, action);
  }

  if (refreshStatus?.isRunning || (refreshStatus?.dataFreshnessStatus ?? report.dataFreshnessStatus ?? "").toLowerCase() === "stale") {
    appendUnique(actions, "Proverite status osvežavanja pre prikaza dashboard-a.");
  }

  if (["partial", "queued", "running"].includes(normalizeImportStatus(report.lastImportStatus))) {
    appendUnique(actions, "Proverite poslednji batch u Uvozu iz Accessa pre prikaza dashboard-a.");
  }

  if (actions.length === 0) {
    appendUnique(actions, "Otvorite Kvalitet podataka i rešite upozorenja koja utiču na preporuke.");
  }

  return actions;
}

function readinessTone(status: string): "excellent" | "good" | "warning" | "critical" {
  const normalized = status.trim().toLowerCase();
  if (normalized === "excellent" || normalized === "ready") return "excellent";
  if (normalized === "good" || normalized === "ready_with_warnings") return "good";
  if (normalized === "warning" || normalized === "unknown") return "warning";
  return "critical";
}

function normalizeImportStatus(value: string | null | undefined): "succeeded" | "failed" | "partial" | "running" | "queued" | "unknown" {
  const normalized = (value ?? "").trim().toLowerCase();
  if (!normalized) return "unknown";
  if (["succeeded", "success", "completed", "complete", "done", "ok"].includes(normalized)) return "succeeded";
  if (["failed", "error", "faulted", "aborted"].includes(normalized)) return "failed";
  if (["partial", "warning", "warned"].includes(normalized)) return "partial";
  if (["running", "in_progress", "processing", "started"].includes(normalized)) return "running";
  if (["queued", "pending", "waiting"].includes(normalized)) return "queued";
  return "unknown";
}

function ageInHours(value: string | null | undefined): number | null {
  if (!value) return null;
  const parsed = Date.parse(value);
  if (Number.isNaN(parsed)) return null;
  return Math.max(0, (Date.now() - parsed) / (1000 * 60 * 60));
}

export function assessPilotImportReadiness(
  report: PilotDataQualityIntakeReport | null,
  refreshStatus: AnalyticsRefreshStatus | null,
  intakeError?: string | null,
): PilotStatusAssessment {
  if (!report) {
    return {
      status: "unknown",
      label: PILOT_STATUS_LABELS.unknown,
      summary: "Status importa ili osvežavanja nije potvrđen, zato dashboard ne treba prikazivati kao spreman.",
      tone: "warning",
      reasons: [
        intakeError ? `Pilot intake izveštaj nije dostupan: ${intakeError}` : "Pilot intake izveštaj nije dostupan.",
        "Nije moguće potvrditi da su podaci spremni za prikaz dashboard-a.",
      ],
      nextActions: [
        "Proverite poslednji import u Uvozu iz Accessa.",
        "Proverite Status osvežavanja i worker proces.",
        "Osvežite pilot intake izveštaj pre prikaza dashboard-a.",
      ],
      links: PILOT_STATUS_LINKS,
    };
  }

  const reasons: string[] = [];
  const nextActions: string[] = [];
  const links = PILOT_STATUS_LINKS;

  const articleCount = report.loadedData.articlesCount;
  const saleLineCount = report.loadedData.saleItemsCount;
  const receiptCount = report.loadedData.receiptsCount;
  const supplierCount = report.loadedData.suppliersCount;
  const firstSaleDate = report.loadedData.firstSaleDate;
  const lastSaleDate = report.loadedData.lastSaleDate;
  const missingCostShare = report.impact.revenueWithoutCostPercent;
  const missingSupplierShare = report.impact.articlesWithoutSupplierPercent;
  const insufficientSignalCount = report.impact.insufficientSignalCount;
  const readinessStatus = (report.readinessStatus ?? "").toLowerCase();
  const importStatus = normalizeImportStatus(report.lastImportStatus);
  const freshnessStatus = (refreshStatus?.dataFreshnessStatus ?? report.dataFreshnessStatus ?? "").toLowerCase();
  const refreshAtUtc = refreshStatus?.lastSuccessfulRefreshAtUtc ?? report.lastRefreshAtUtc;
  const refreshAgeHours = ageInHours(refreshAtUtc);
  const usableDateRange = hasUsableSalePeriod(firstSaleDate, lastSaleDate);
  const refreshFailed = hasRefreshFailure(refreshStatus, freshnessStatus);
  const importStatusKnown = importStatus !== "unknown" || Boolean(report.lastImportAtUtc);
  const refreshStatusKnown = freshnessStatus !== "unknown" || Boolean(refreshAtUtc || refreshStatus?.lastFailureAtUtc);
  const recommendationImpactDetected =
    report.impact.recommendationsBlockedCount > 0
    || missingCostShare >= READINESS_WARNING_SHARE_THRESHOLD
    || missingSupplierShare >= READINESS_WARNING_SHARE_THRESHOLD
    || report.issues.missingCategoryCount > 0
    || (report.issues.missingColorCount ?? 0) > 0
    || (report.issues.missingSizeCount ?? 0) > 0
    || insufficientSignalCount > 0
    || supplierCount === 0;

  if (!articleCount) {
    appendUnique(reasons, "Nema artikala u importovanom skupu.");
  }

  if (!saleLineCount) {
    appendUnique(reasons, "Nema stavki prodaje u izabranom periodu.");
  }

  if (!usableDateRange) {
    appendUnique(reasons, "Ne postoji upotrebljiv period prodaje za pilot prikaz.");
  }

  if (importStatus === "failed") {
    appendUnique(reasons, "Poslednji import je neuspešan.");
  }

  if (refreshFailed) {
    appendUnique(reasons, "Poslednje osvežavanje analitike je neuspešno ili kritično zastarelo.");
  }

  let status: PilotImportReadinessStatus;
  let tone: PilotStatusAssessment["tone"];

  if (reasons.length > 0) {
    status = "not_ready";
    tone = "critical";

    if (!articleCount || !saleLineCount) {
      appendUnique(nextActions, "Ponovite import tako da artikli i prodaja budu učitani pre prikaza dashboard-a.");
    }

    if (!usableDateRange) {
      appendUnique(nextActions, "Proverite mapiranje datuma i proširite period da postoji upotrebljiv raspon prodaje.");
    }

    if (importStatus === "failed") {
      appendUnique(nextActions, "Otvorite Uvoz iz Accessa i proverite poslednji neuspešan batch.");
    }

    if (refreshFailed) {
      appendUnique(nextActions, "Otvorite Status osvežavanja i pokrenite refresh kada je bezbedno.");
    }
  } else if (!importStatusKnown || !refreshStatusKnown) {
    status = "unknown";
    tone = "warning";

    if (!importStatusKnown) {
      appendUnique(reasons, "Status poslednjeg importa nije dostupan.");
      appendUnique(nextActions, "Proverite poslednji import batch u Uvozu iz Accessa.");
    }

    if (!refreshStatusKnown) {
      appendUnique(reasons, "Status poslednjeg osvežavanja analitike nije dostupan.");
      appendUnique(nextActions, "Proverite Status osvežavanja i worker proces.");
    }

    appendUnique(reasons, "Sistem trenutno ne može pouzdano da proceni da li je dashboard bezbedan za prikaz.");
    appendUnique(nextActions, "Osvežite pilot intake izveštaj pre prikaza dashboard-a.");
  } else {
    const warningReasons: string[] = [];

    if (!receiptCount) {
      appendUnique(warningReasons, "Nema računa u izabranom periodu.");
    }

    if (!supplierCount) {
      appendUnique(warningReasons, "Nema dobavljača u importu, pa dobavljačke preporuke nisu pouzdane.");
    }

    if (importStatus === "partial" || importStatus === "running" || importStatus === "queued") {
      appendUnique(warningReasons, `Poslednji import je u statusu ${report.lastImportStatus}.`);
    }

    if (freshnessStatus === "stale") {
      appendUnique(warningReasons, "Osvežavanje je zastarelo i treba ga potvrditi pre demo prikaza.");
    }

    if (refreshStatus?.isRunning) {
      appendUnique(warningReasons, "Refresh je trenutno u toku.");
    }

    if (refreshAgeHours != null && refreshAgeHours > 72) {
      appendUnique(warningReasons, "Poslednje uspešno osvežavanje je starije od 72h.");
    }

    if (missingCostShare >= READINESS_WARNING_SHARE_THRESHOLD) {
      appendUnique(warningReasons, `Prihod bez nabavne cene: ${fmtPctFromRatio(missingCostShare, 1, "-")}.`);
    }

    if (missingSupplierShare >= READINESS_WARNING_SHARE_THRESHOLD) {
      appendUnique(warningReasons, `Artikli bez dobavljača: ${fmtPctFromRatio(missingSupplierShare, 1, "-")}.`);
    }

    if (report.issues.missingCategoryCount > 0) {
      appendUnique(warningReasons, `Artikli bez kategorije: ${fmtNumber(report.issues.missingCategoryCount, 0, "-")}.`);
    }

    if ((report.issues.missingColorCount ?? 0) > 0 || (report.issues.missingSizeCount ?? 0) > 0) {
      appendUnique(warningReasons, "Nedostaju atributi boje ili veličine koji utiču na deo preporuka.");
    }

    if (report.impact.recommendationsBlockedCount > 0) {
      appendUnique(warningReasons, `Blokirane preporuke: ${fmtNumber(report.impact.recommendationsBlockedCount, 0, "-")}.`);
    }

    if (insufficientSignalCount > 0) {
      appendUnique(warningReasons, `Nedovoljni signali: ${fmtNumber(insufficientSignalCount, 0, "-")}.`);
    }

    if ((readinessStatus === "warning" || readinessStatus === "critical") && !recommendationImpactDetected) {
      appendUnique(warningReasons, `Ukupan readiness skor traži ručnu proveru: ${report.readinessLabel}.`);
    }

    if (warningReasons.length > 0 || recommendationImpactDetected) {
      status = "ready_with_warnings";
      tone = "warning";
      reasons.push(...warningReasons);
      nextActions.push(...buildWarningActions(report, refreshStatus));
    } else {
      status = "ready";
      tone = "good";
      reasons.push(...buildReadyReasons(report, refreshAtUtc));
      appendUnique(nextActions, "Možete otvoriti dashboard za pilot prikaz.");
      appendUnique(nextActions, "Pratite Kvalitet podataka i Status osvežavanja tokom demo sesije.");
    }
  }

  if (report.lastImportAtUtc && status !== "ready") {
    appendUnique(reasons, `Poslednji import: ${formatDateTime(report.lastImportAtUtc, "-")}.`);
  }

  if (report.lastImportStatus && status !== "ready") {
    appendUnique(reasons, `Status importa: ${report.lastImportStatus}.`);
  }

  if (refreshAtUtc && status !== "ready") {
    appendUnique(reasons, `Poslednje uspešno osvežavanje: ${formatDateTime(refreshAtUtc, "-")}.`);
  }

  if (freshnessStatus !== "unknown" && status !== "ready") {
    appendUnique(reasons, `Status osvežavanja: ${refreshStatus?.dataFreshnessStatus ?? report.dataFreshnessStatus}.`);
  }

  const summary = status === "ready"
    ? "Dashboard je bezbedan za pilot prikaz."
    : status === "ready_with_warnings"
      ? "Dashboard može da se prikaže, ali preporuke i poverenje treba objasniti kroz upozorenja."
      : status === "not_ready"
        ? "Pilot nije bezbedan za prikaz dok se ne reše osnovni import ili refresh blokeri."
        : "Status nije dovoljno jasan da bi dashboard izgledao potvrđeno uspešno.";

  return {
    status,
    label: PILOT_STATUS_LABELS[status],
    summary,
    tone,
    reasons,
    nextActions,
    links,
  };
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
    ["Učitano", "Poslednji import status", report.lastImportStatus ?? "-"],
    ["Učitano", "Svežina podataka", report.dataFreshnessStatus ?? "-"],
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
    `Poslednji import status: ${report.lastImportStatus ?? "-"}`,
    `Svežina podataka: ${report.dataFreshnessStatus ?? "-"}`,
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
    { section: "Učitano", item: "Poslednji import status", value: report.lastImportStatus ?? "-" },
    { section: "Učitano", item: "Svežina podataka", value: report.dataFreshnessStatus ?? "-" },
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
      { key: "lastImportStatus", label: "Poslednji import status", value: report.lastImportStatus ?? null },
      { key: "lastRefreshAtUtc", label: "Poslednje osveženje", value: report.lastRefreshAtUtc ?? null },
      { key: "dataFreshnessStatus", label: "Svežina podataka", value: report.dataFreshnessStatus ?? null },
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

export default function PilotDataQualityIntakeReport({ report, loading, error, filters, durableReport, refreshStatus, onRetry }: Props) {
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
  const pilotImportAssessment = useMemo(() => assessPilotImportReadiness(report, refreshStatus ?? null, error), [error, refreshStatus, report]);
  const durableRows = durableReport?.rows ?? [];
  const durableActions = durableReport?.recommendedActions ?? [];
  const reportPeriodFrom = report?.periodFromUtc ?? durableReport?.periodFrom ?? durableReport?.period?.fromUtc ?? null;
  const reportPeriodTo = report?.periodToUtc ?? durableReport?.periodTo ?? durableReport?.period?.toUtc ?? null;
  const reportGeneratedAt = report?.generatedAtUtc ?? durableReport?.generatedAtUtc ?? null;
  const reportLastRefreshAt = report?.lastRefreshAtUtc ?? durableReport?.lastRefreshAtUtc ?? null;
  const reportDataQualityStatus = report?.meta?.dataQualityStatus ?? durableReport?.dataQualityStatus ?? null;

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

      <section aria-label="Status pilota">
        <article className={`pilot-intake-score ${pilotImportAssessment.tone}`}>
          <div>
            <span>Status pilota</span>
            <strong>{pilotImportAssessment.label}</strong>
            <p>{pilotImportAssessment.summary}</p>
          </div>
          <div className="pilot-intake-thresholds">
            {pilotImportAssessment.reasons.slice(0, 3).map((reason, index) => (
              <span key={`pilot-status-highlight-${index}`}>{reason}</span>
            ))}
          </div>
        </article>
        <div className="pilot-intake-grid" style={{ gridTemplateColumns: "repeat(2, minmax(220px, 1fr))", marginTop: 10 }}>
          <div className="pilot-card">
            <h3>Razlozi</h3>
            <ul>
              {pilotImportAssessment.reasons.map((reason, index) => (
                <li key={`pilot-reason-${index}`}>{reason}</li>
              ))}
            </ul>
          </div>
          <div className="pilot-card">
            <h3>Sledeći koraci</h3>
            <ul>
              {pilotImportAssessment.nextActions.map((action) => (
                <li key={action}>{action}</li>
              ))}
            </ul>
          </div>
        </div>
        <div className="pilot-actions-list no-print" style={{ marginTop: 10 }}>
          {pilotImportAssessment.links.map((link) => (
            <Link key={link.href} to={link.href} className="pilot-action-item">
              <strong>{link.label}</strong>
              <span>Otvori ekran</span>
            </Link>
          ))}
        </div>
      </section>

      <section className="pilot-card">
        <h3>Trendplus pilot izveštaj kvaliteta podataka</h3>
        <ul>
          <li>Period od: {formatDate(reportPeriodFrom, "-")}</li>
          <li>Period do: {formatDate(reportPeriodTo, "-")}</li>
          <li>Generisano: {formatDateTime(reportGeneratedAt, "-")}</li>
          <li>Poslednje osveženje: {formatDateTime(reportLastRefreshAt, "-")}</li>
          <li>Poslednji import status: {report?.lastImportStatus ?? "-"}</li>
          <li>Skor spremnosti podataka: {report ? `${fmtNumber(report.readinessScore, 0, "-")}/100` : durableReadinessScore == null ? "-" : `${fmtNumber(durableReadinessScore, 0, "-")}/100`}</li>
          <li>Status kvaliteta podataka: {reportDataQualityStatus ?? "-"}</li>
          <li>Svežina podataka: {report?.dataFreshnessStatus ?? "-"}</li>
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
                <li>Prihod bez nabavne cene: {fmtPctFromRatio(report.impact.revenueWithoutCostPercent, 1, "-")}</li>
                <li>Artikli bez dobavljača: {fmtPctFromRatio(report.impact.articlesWithoutSupplierPercent, 1, "-")}</li>
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



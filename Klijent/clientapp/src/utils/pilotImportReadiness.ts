import type { AnalyticsRefreshStatus, PilotDataQualityIntakeReport } from "../types/analytics";

export type PilotImportReadinessStatus = "ready" | "ready_with_warnings" | "not_ready" | "unknown";

export interface PilotImportReadinessResult {
  status: PilotImportReadinessStatus;
  label: string;
  tone: "excellent" | "warning" | "critical";
  summary: string;
  reasons: string[];
  nextActions: string[];
}

const STATUS_LABELS: Record<PilotImportReadinessStatus, string> = {
  ready: "Spremno",
  ready_with_warnings: "Spremno uz upozorenja",
  not_ready: "Nije spremno",
  unknown: "Nepoznato",
};

const STATUS_SUMMARIES: Record<PilotImportReadinessStatus, string> = {
  ready: "Pilot može da se pokaže kao stabilan i dovoljno pouzdan za razgovor sa kupcem.",
  ready_with_warnings: "Pilot može da se pokaže, ali ima upozorenja koja treba objasniti pre prezentacije.",
  not_ready: "Pilot još nije bezbedan za prikaz kao finalni dashboard bez korekcija.",
  unknown: "Nedovoljno signala je dostupno da bismo procenili spremnost pilota.",
};

const NUMBER_FORMAT = new Intl.NumberFormat("sr-RS", { maximumFractionDigits: 0 });
const PERCENT_FORMAT = new Intl.NumberFormat("sr-RS", { maximumFractionDigits: 1 });

function formatCount(value: number): string {
  return NUMBER_FORMAT.format(value);
}

function formatPercent(value: number): string {
  return `${PERCENT_FORMAT.format(value)}%`;
}

function normalizeStatus(value: string | null | undefined): string {
  return value?.trim().toLowerCase() ?? "";
}

function dedupe(values: string[]): string[] {
  return Array.from(new Set(values.filter((value) => value.trim().length > 0)));
}

function isFailedImportStatus(value: string): boolean {
  return value === "failed" || value === "error" || value === "blocked" || value === "cancelled" || value === "canceled";
}

function isWarningImportStatus(value: string): boolean {
  return value === "partial" || value === "warning" || value === "running" || value === "queued" || value === "in_progress";
}

function buildUnknownResult(reason: string): PilotImportReadinessResult {
  return {
    status: "unknown",
    label: STATUS_LABELS.unknown,
    tone: "critical",
    summary: STATUS_SUMMARIES.unknown,
    reasons: [reason],
    nextActions: [
      "Završi pilot import ili ponovo učitaj report.",
      "Otvori kvalitet podataka.",
      "Otvori status osvežavanja.",
      "Otvori import.",
    ],
  };
}

export function computePilotImportReadiness(
  report: PilotDataQualityIntakeReport | null,
  refreshStatus?: AnalyticsRefreshStatus | null,
  lastImportStatus?: string | null,
): PilotImportReadinessResult {
  if (!report) {
    return buildUnknownResult("Pilot intake report još nije dostupan, pa readiness ne može da se proceni.");
  }

  const baseStatus = normalizeStatus(report.readinessStatus);
  const freshnessStatus = normalizeStatus(refreshStatus?.dataFreshnessStatus);
  const importStatus = normalizeStatus(lastImportStatus);
  const reasons: string[] = [];
  const nextActions: string[] = [];

  const articleCount = report.loadedData.articlesCount;
  const saleLineCount = report.loadedData.saleItemsCount;
  const receiptCount = report.loadedData.receiptsCount;
  const supplierCount = report.loadedData.suppliersCount;
  const firstSaleDate = report.loadedData.firstSaleDate;
  const lastSaleDate = report.loadedData.lastSaleDate;
  const missingCostRevenueSharePct = Math.max(0, report.impact.revenueWithoutCostPercent * 100);
  const missingSupplierSharePct = Math.max(0, report.impact.articlesWithoutSupplierPercent * 100);

  const hardBlockers = [
    articleCount <= 0 ? "Nema artikala u pilot paketu." : null,
    saleLineCount <= 0 ? "Nema stavki prodaje u pilot paketu." : null,
    receiptCount <= 0 ? "Nema računa u pilot paketu." : null,
    !firstSaleDate || !lastSaleDate ? "Nedostaje prvi ili poslednji datum prodaje." : null,
    baseStatus === "critical" ? "Backend readiness je označen kao critical." : null,
    freshnessStatus === "critical" ? "Poslednje osvežavanje je kritično zastarelo." : null,
    isFailedImportStatus(importStatus) ? "Poslednji import nije uspeo." : null,
  ].filter((value): value is string => value !== null);

  const warningSignals = [
    baseStatus === "warning" ? `Backend readiness je ${report.readinessLabel}.` : null,
    supplierCount <= 0 ? "Nema dobavljača u pilot paketu." : null,
    report.issues.missingSupplierCount > 0 ? `${formatCount(report.issues.missingSupplierCount)} artikala nema dobavljača.` : null,
    report.issues.missingCostCount > 0 ? `${formatCount(report.issues.missingCostCount)} stavki nema nabavnu cenu.` : null,
    report.impact.revenueWithoutCostPercent > 0 ? `${formatPercent(missingCostRevenueSharePct)} prihoda nema nabavnu cenu.` : null,
    report.impact.articlesWithoutSupplierPercent > 0 ? `${formatPercent(missingSupplierSharePct)} artikala nema dobavljača.` : null,
    report.impact.insufficientSignalCount > 0 ? `${formatCount(report.impact.insufficientSignalCount)} artikala nema dovoljno signala.` : null,
    report.impact.ignoredRowsCount > 0 ? `${formatCount(report.impact.ignoredRowsCount)} redova je ignorisano pri importu.` : null,
    !report.lastImportAtUtc ? "Poslednji import nije dostupan." : null,
    !refreshStatus ? "Status osvežavanja nije dostupan." : null,
    freshnessStatus === "stale" ? "Poslednje osvežavanje je zastarelo." : null,
    refreshStatus?.isRunning ? "Osvežavanje analitike je trenutno u toku." : null,
    isWarningImportStatus(importStatus) ? "Poslednji import još nije potvrdio stabilno stanje." : null,
    report.meta?.dataQualityStatus === "insufficient_data" ? "Kvalitet podataka je označen kao nedovoljan." : null,
  ].filter((value): value is string => value !== null);

  if (hardBlockers.length === 0 && warningSignals.length === 0 && baseStatus !== "good" && baseStatus !== "excellent") {
    warningSignals.push(`Backend readiness status nije standardno prepoznat: ${report.readinessStatus}.`);
  }

  reasons.push(...hardBlockers, ...warningSignals);

  if (hardBlockers.length > 0) {
    nextActions.push(
      "Proveri da li su artikli, prodaja i računi kompletno učitani.",
      "Ponovo pokreni Access import ili popravi mapu kolona.",
    );
  }

  if (report.issues.missingCostCount > 0 || report.issues.missingSupplierCount > 0) {
    nextActions.push("Dopuni nabavne cene i dobavljače.");
  }

  if (freshnessStatus === "stale" || freshnessStatus === "critical" || refreshStatus?.isRunning) {
    nextActions.push("Pokreni ili prati osvežavanje analitike.");
  }

  if (report.impact.insufficientSignalCount > 0 || report.meta?.dataQualityStatus === "insufficient_data") {
    nextActions.push("Pregledaj signalni period pre prezentacije.");
  }

  if (!report.lastImportAtUtc) {
    nextActions.push("Proveri poslednji import.");
  }

  nextActions.push("Otvori kvalitet podataka.", "Otvori status osvežavanja.", "Otvori import.");

  const status: PilotImportReadinessStatus = hardBlockers.length > 0
    ? "not_ready"
    : baseStatus === "warning" || warningSignals.length > 0
      ? "ready_with_warnings"
      : baseStatus === "good" || baseStatus === "excellent"
        ? "ready"
        : "ready_with_warnings";

  return {
    status,
    label: STATUS_LABELS[status],
    tone: status === "ready" ? "excellent" : status === "ready_with_warnings" ? "warning" : "critical",
    summary: STATUS_SUMMARIES[status],
    reasons: dedupe(reasons),
    nextActions: dedupe(nextActions),
  };
}

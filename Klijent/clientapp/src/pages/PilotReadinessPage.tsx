import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import AnalyticsEmptyState from "../components/analytics/AnalyticsEmptyState";
import AnalyticsErrorState from "../components/analytics/AnalyticsErrorState";
import AnalyticsRefreshStatusBanner from "../components/analytics/AnalyticsRefreshStatusBanner";
import AnalyticsTrustHeader from "../components/analytics/AnalyticsTrustHeader";
import {
  getAnalyticsActionCounts,
  getAnalyticsActionOutcomeSummary,
  getAnalyticsDataQualityHealth,
  getAnalyticsRefreshStatus,
  getDashboardBootstrap,
  getPilotDataQualityIntakeReport,
  getPilotIntakeDurableReport,
  getProductDecisionCenter,
  getSupplierDecisionDurableReport,
} from "../services/analyticsApi";
import type {
  AnalyticsActionCounts,
  AnalyticsActionOutcomeSummaryResponse,
  AnalyticsDashboardBootstrap,
  AnalyticsDataQualityHealth,
  AnalyticsRefreshStatus,
  PilotDataQualityIntakeReport,
  PilotIntakeDurableReport,
  ProductDecisionCenterResponse,
  SupplierDecisionDurableReport,
} from "../types/analytics";
import { fmtNumber, fmtPct, fmtPctFromRatio, fmtRsd, formatDateTime } from "../utils/analyticsFormatters";
import { dataQualityStatusLabel } from "../utils/analyticsQuality";
import { getAnalyticsMetaMessage, isAnalyticsMetaWarning } from "../utils/analyticsResponseMeta";
import "./PilotReadinessPage.css";

type ReadinessStatus = "ready" | "warning" | "blocked" | "unknown";

type ReadinessCard = {
  key: string;
  index: string;
  title: string;
  status: ReadinessStatus;
  reason: string;
  actionLabel: string;
  href: string;
  meta?: string | null;
};

type LoadError = {
  key: string;
  message: string;
  errorCode?: string | null;
  correlationId?: string | null;
};

type ReadinessPayload = {
  bootstrap: AnalyticsDashboardBootstrap | null;
  refreshStatus: AnalyticsRefreshStatus | null;
  dataQualityHealth: AnalyticsDataQualityHealth | null;
  intakeReport: PilotDataQualityIntakeReport | null;
  productDecisionCenter: ProductDecisionCenterResponse | null;
  actionCounts: AnalyticsActionCounts | null;
  actionOutcomeSummary: AnalyticsActionOutcomeSummaryResponse | null;
  pilotReport: PilotIntakeDurableReport | null;
  supplierReport: SupplierDecisionDurableReport | null;
  errors: LoadError[];
};

type LoadTask = {
  key: string;
  request: Promise<unknown>;
  assign: (value: unknown) => void;
  fallback: string;
};

const EMPTY_PAYLOAD: ReadinessPayload = {
  bootstrap: null,
  refreshStatus: null,
  dataQualityHealth: null,
  intakeReport: null,
  productDecisionCenter: null,
  actionCounts: null,
  actionOutcomeSummary: null,
  pilotReport: null,
  supplierReport: null,
  errors: [],
};

const STATUS_LABELS: Record<ReadinessStatus, string> = {
  ready: "Spremno",
  warning: "Upozorenje",
  blocked: "Blokirano",
  unknown: "Nepoznato",
};

const STATUS_TONES: Record<ReadinessStatus, string> = {
  ready: "ready",
  warning: "warning",
  blocked: "blocked",
  unknown: "unknown",
};

function normalizeMessage(value: string | null | undefined, fallback: string): string {
  const trimmed = value?.trim();
  return trimmed ? trimmed : fallback;
}

function normalizeLoadError(key: string, reason: unknown, fallback: string): LoadError {
  if (reason && typeof reason === "object") {
    const maybeError = reason as {
      message?: string;
      errorCode?: string | null;
      correlationId?: string | null;
    };

    if (typeof maybeError.message === "string" && maybeError.message.trim().length > 0) {
      return {
        key,
        message: maybeError.message,
        errorCode: maybeError.errorCode ?? null,
        correlationId: maybeError.correlationId ?? null,
      };
    }
  }

  if (reason instanceof Error && reason.message.trim().length > 0) {
    return { key, message: reason.message };
  }

  if (typeof reason === "string" && reason.trim().length > 0) {
    return { key, message: reason };
  }

  return { key, message: fallback };
}

function statusLabel(status: ReadinessStatus): string {
  return STATUS_LABELS[status];
}

function statusToneClass(status: ReadinessStatus): string {
  return `pilot-readiness-card-${STATUS_TONES[status]}`;
}

function isTruthyNumber(value: number | null | undefined): boolean {
  return typeof value === "number" && Number.isFinite(value) && value > 0;
}

function hasSupplierSignals(bootstrap: AnalyticsDashboardBootstrap | null): boolean {
  if (!bootstrap) return false;
  return bootstrap.supplierData.length > 0 || (bootstrap.executive?.topSuppliers?.length ?? 0) > 0;
}

function hasSalesSignals(bootstrap: AnalyticsDashboardBootstrap | null): boolean {
  if (!bootstrap) return false;
  return isTruthyNumber(bootstrap.summary?.totalRevenue)
    || isTruthyNumber(bootstrap.summary?.totalTransactions)
    || isTruthyNumber(bootstrap.summary?.totalUnits)
    || bootstrap.dailySales.length > 0;
}

function hasInventorySignals(bootstrap: AnalyticsDashboardBootstrap | null): boolean {
  if (!bootstrap) return false;
  return isTruthyNumber(bootstrap.inventory?.totalSkuCount)
    || isTruthyNumber(bootstrap.inventory?.totalOnHand)
    || isTruthyNumber(bootstrap.inventory?.lowStockCount)
    || isTruthyNumber(bootstrap.inventory?.outOfStockCount);
}

function formatLoadCount(value: number | null | undefined): string {
  return fmtNumber(value, 0, "-");
}

function deriveOverallStatus(items: ReadinessCard[]): ReadinessStatus {
  if (items.some((item) => item.status === "blocked")) return "blocked";
  if (items.some((item) => item.status === "warning")) return "warning";
  if (items.some((item) => item.status === "unknown")) return "unknown";
  return "ready";
}

function buildDataQualityCard(intakeReport: PilotDataQualityIntakeReport | null, health: AnalyticsDataQualityHealth | null): ReadinessCard {
  if (!intakeReport && !health) {
    return {
      key: "data-quality",
      index: "01",
      title: "Kvalitet podataka proveren",
      status: "unknown",
      reason: "Nije stigao pilot intake niti data quality health, pa kvalitet podataka ne možemo da potvrdimo.",
      actionLabel: "Otvori Kvalitet podataka",
      href: "/analytics/data-quality",
      meta: "Izvor: data quality intake i health",
    };
  }

  const readinessStatus = intakeReport?.readinessStatus?.trim().toLowerCase() ?? "";
  const healthStatus = health?.scoreStatus ?? "";
  const intakeMetaWarning = Boolean(intakeReport?.meta?.isPartial || isAnalyticsMetaWarning(intakeReport?.meta));
  const healthMetaWarning = Boolean(health?.meta?.isPartial || isAnalyticsMetaWarning(health?.meta));
  const blockedRecommendations = intakeReport?.impact.recommendationsBlockedCount ?? 0;
  const hasBlockingIssue = blockedRecommendations > 0;
  const isCritical = readinessStatus === "critical" || healthStatus === "critical" || hasBlockingIssue;
  const isWarning = readinessStatus === "warning" || healthStatus === "warning" || intakeMetaWarning || healthMetaWarning;
  // Intake readiness is the decision gate for this screen. Health score is a
  // separate traffic-quality signal and must not silently replace it.
  const score = intakeReport?.readinessScore ?? health?.score ?? null;
  const summary = intakeReport?.readinessLabel ?? health?.scoreSummary ?? "Kvalitet podataka je dostupan.";
  const qualityReason = isCritical && intakeReport && blockedRecommendations > 0
    ? `Preporuke nisu bezbedne za pilot: ${formatLoadCount(blockedRecommendations)} preporuka je blokirano zbog kvaliteta ulaznih podataka. Skor kvaliteta (${formatLoadCount(score)}) ne otključava preporuke.`
    : intakeReport
      ? `Kvalitet podataka: ${summary} (skor ${formatLoadCount(score)}). Trenutno je blokirano ${formatLoadCount(blockedRecommendations)} preporuka; ignorisano je ${formatLoadCount(intakeReport.impact.ignoredRowsCount)} redova.`
      : `Health score: ${formatLoadCount(score)}. ${health?.scoreSummary ?? "Data quality health je učitan."}`;

  return {
    key: "data-quality",
    index: "01",
    title: "Kvalitet podataka proveren",
    status: isCritical ? "blocked" : isWarning ? "warning" : "ready",
    reason: qualityReason,
    actionLabel: "Otvori Kvalitet podataka",
    href: "/analytics/data-quality",
    meta: intakeReport
      ? `Bez dobavljača: ${formatLoadCount(intakeReport.issues.missingSupplierCount)} · bez nabavne cene: ${formatLoadCount(intakeReport.issues.missingCostCount)} · bez kategorije: ${formatLoadCount(intakeReport.issues.missingCategoryCount)} · nedovoljni signali: ${formatLoadCount(intakeReport.impact.insufficientSignalCount)} · ignorisani redovi: ${formatLoadCount(intakeReport.impact.ignoredRowsCount)}`
      : null,
  };
}

function buildRefreshCard(refreshStatus: AnalyticsRefreshStatus | null): ReadinessCard {
  if (!refreshStatus) {
    return {
      key: "refresh",
      index: "02",
      title: "Svežina analytics podataka",
      status: "unknown",
      reason: "Status osvežavanja nije dostupan, pa ne možemo da potvrdimo svežinu signala.",
      actionLabel: "Otvori worker panel",
      href: "/admin/configuration?panel=workers",
      meta: "Izvor: refresh status",
    };
  }

  const freshness = (refreshStatus.dataFreshnessStatus ?? "unknown").trim().toLowerCase();
  const lastSuccess = refreshStatus.lastSuccessfulRefreshAtUtc ? new Date(refreshStatus.lastSuccessfulRefreshAtUtc) : null;
  const referenceTime = refreshStatus.generatedAtUtc
    ? new Date(refreshStatus.generatedAtUtc)
    : refreshStatus.lastAttemptAtUtc
      ? new Date(refreshStatus.lastAttemptAtUtc)
      : null;
  const hoursSinceSuccess = lastSuccess && referenceTime && !Number.isNaN(lastSuccess.getTime()) && !Number.isNaN(referenceTime.getTime())
    ? (referenceTime.getTime() - lastSuccess.getTime()) / (1000 * 60 * 60)
    : null;
  const hasCriticalAge = hoursSinceSuccess != null && hoursSinceSuccess > 72;
  const hasWorkerProblem = refreshStatus.workersEnabled === false || refreshStatus.processType === "worker" && refreshStatus.processMode === "web";
  const isCritical = freshness === "critical" || hasCriticalAge || (!lastSuccess && !refreshStatus.isRunning);
  const isWarning = freshness === "stale" || refreshStatus.isRunning || Boolean(refreshStatus.lastFailureAtUtc) || Boolean(refreshStatus.workerWarning || refreshStatus.workerProcessWarning) || hasWorkerProblem;
  const hasAttemptHistory = Boolean(refreshStatus.lastAttemptAtUtc || refreshStatus.lastSuccessfulRefreshAtUtc || refreshStatus.recentRuns?.length);

  return {
    key: "refresh",
    index: "02",
    title: "Svežina analytics podataka",
    status: isCritical ? "blocked" : isWarning ? "warning" : "ready",
    reason: refreshStatus.isRunning
      ? `Osvežavanje je u toku${refreshStatus.currentStep ? ` (${refreshStatus.currentStep})` : ""}.`
      : freshness === "fresh"
        ? `Poslednji uspešan refresh je ${formatDateTime(refreshStatus.lastSuccessfulRefreshAtUtc, "-")}.`
      : freshness === "stale"
        ? `Refresh je zastareo od ${formatDateTime(refreshStatus.lastSuccessfulRefreshAtUtc, "-")}.`
        : !hasAttemptHistory && !refreshStatus.isRunning
          ? "Nema zabeleženog uspešnog osvežavanja niti pokušaja u istoriji; svežina podataka nije potvrđena."
          : `Stanje osvežavanja nije potvrđeno. Poslednji uspešan refresh: ${formatDateTime(refreshStatus.lastSuccessfulRefreshAtUtc, "-")}.`,
    actionLabel: "Otvori worker panel",
    href: "/admin/configuration?panel=workers",
    meta: refreshStatus.workerWarning
      ?? refreshStatus.workerProcessWarning
      ?? (refreshStatus.processMode === "web" ? "Proces: web (automatsko osvežavanje nije potvrđeno)" : null),
  };
}

function buildSalesCard(bootstrap: AnalyticsDashboardBootstrap | null): ReadinessCard {
  if (!bootstrap) {
    return {
      key: "sales",
      index: "03",
      title: "Pregled prodaje dostupan",
      status: "unknown",
      reason: "Dashboard bootstrap nije dostupan, pa prodajni pregled ne možemo da potvrdimo.",
      actionLabel: "Otvori Trendplus pregled",
      href: "/analytics",
      meta: "Izvor: dashboard bootstrap",
    };
  }

  const summary = bootstrap.summary;
  const hasSales = hasSalesSignals(bootstrap);
  const hasWarnings = bootstrap.errors.length > 0 || Boolean(bootstrap.meta?.isPartial);

  if (!hasSales) {
    return {
      key: "sales",
      index: "03",
      title: "Pregled prodaje dostupan",
      status: "blocked",
      reason: "Bootstrap je stigao, ali nema potvrđenih prodajnih signala za pregled prodaje.",
      actionLabel: "Otvori Trendplus pregled",
      href: "/analytics",
      meta: "Izvor: dashboard bootstrap",
    };
  }

  return {
    key: "sales",
    index: "03",
    title: "Pregled prodaje dostupan",
    status: hasWarnings ? "warning" : "ready",
    reason: `Prihod ${fmtRsd(summary?.totalRevenue ?? null, 0, "-")}, ${formatLoadCount(summary?.totalTransactions)} transakcija i ${formatLoadCount(summary?.totalUnits)} komada su dostupni u dashboard bootstrap-u.`,
    actionLabel: "Otvori Trendplus pregled",
    href: "/analytics",
    meta: bootstrap.dailySales.length > 0 ? `${formatLoadCount(bootstrap.dailySales.length)} dnevnih tačaka` : "bez dnevne serije",
  };
}

function buildProductsCard(productDecisionCenter: ProductDecisionCenterResponse | null): ReadinessCard {
  if (!productDecisionCenter) {
    return {
      key: "products",
      index: "04",
      title: "Odluke o proizvodima dostupne",
      status: "unknown",
      reason: "Product Decision Center nije dostupan, pa odluke o proizvodima ne možemo da potvrdimo.",
      actionLabel: "Otvori Odluke o proizvodima",
      href: "/analytics/products",
      meta: "Izvor: product decision center",
    };
  }

  const rows = productDecisionCenter.rows.length;
  const meta = productDecisionCenter.meta;
  const metaMessage = getAnalyticsMetaMessage(meta);
  const isPartial = Boolean(meta?.isPartial || isAnalyticsMetaWarning(meta));
  const isBlocked = rows === 0 || meta?.dataQualityStatus === "insufficient_data";
  const isWarning = isPartial || (productDecisionCenter.summary.badDataCount ?? 0) > 0;

  return {
    key: "products",
    index: "04",
    title: "Odluke o proizvodima dostupne",
    status: isBlocked ? "blocked" : isWarning ? "warning" : "ready",
    reason: rows > 0
      ? `Nađeno je ${formatLoadCount(rows)} redova; replenish ${formatLoadCount(productDecisionCenter.summary.replenishCount)}, markdown ${formatLoadCount(productDecisionCenter.summary.markdownCount)} i high-potential ${formatLoadCount(productDecisionCenter.summary.highPotentialCount)}.`
      : meta?.emptyReason ?? metaMessage ?? "Product Decision Center je stigao bez redova za trenutni opseg.",
    actionLabel: "Otvori Odluke o proizvodima",
    href: "/analytics/products",
    meta: `Problematični redovi: ${formatLoadCount(productDecisionCenter.summary.badDataCount)} · procena izgubljene prodaje: ${fmtRsd(productDecisionCenter.summary.lostSalesEstimate, 0, "-")}`,
  };
}

function buildSupplierCard(bootstrap: AnalyticsDashboardBootstrap | null): ReadinessCard {
  if (!bootstrap) {
    return {
      key: "supplier",
      index: "05",
      title: "Pregled dobavljača dostupan",
      status: "unknown",
      reason: "Dashboard bootstrap nije dostupan, pa ne možemo da potvrdimo dobavljačke signale.",
      actionLabel: "Otvori Pregled dobavljača",
      href: "/analytics/supplier",
      meta: "Izvor: dashboard bootstrap",
    };
  }

  const supplierRows = bootstrap.supplierData.length;
  const topSuppliers = bootstrap.executive?.topSuppliers?.length ?? 0;
  const hasSupplierData = hasSupplierSignals(bootstrap);
  const hasWarnings = bootstrap.errors.length > 0 || Boolean(bootstrap.meta?.isPartial);
  const topSupplier = bootstrap.executive?.topSuppliers?.[0];

  if (!hasSupplierData) {
    return {
      key: "supplier",
      index: "05",
      title: "Pregled dobavljača dostupan",
      status: "blocked",
      reason: "Bootstrap je stigao, ali ne postoji potvrđen dobavljački signal za pregled dobavljača.",
      actionLabel: "Otvori Pregled dobavljača",
      href: "/analytics/supplier",
      meta: "Izvor: dashboard bootstrap",
    };
  }

  return {
    key: "supplier",
    index: "05",
    title: "Pregled dobavljača dostupan",
    status: hasWarnings ? "warning" : "ready",
    reason: topSupplier
      ? `Top dobavljač je ${topSupplier.supplierName} sa ${fmtRsd(topSupplier.revenue, 0, "-")} i maržnim doprinosom ${fmtRsd(topSupplier.marginContribution, 0, "-")}.`
      : `Bootstrap ima ${formatLoadCount(supplierRows)} dobavljačkih redova i ${formatLoadCount(topSuppliers)} top signala.`,
    actionLabel: "Otvori Pregled dobavljača",
    href: "/analytics/supplier",
    meta: supplierRows > 0 ? `${formatLoadCount(supplierRows)} dobavljačkih redova` : `${formatLoadCount(topSuppliers)} top signala`,
  };
}

function buildInventoryCard(bootstrap: AnalyticsDashboardBootstrap | null): ReadinessCard {
  if (!bootstrap) {
    return {
      key: "inventory",
      index: "06",
      title: "Lager rizici dostupni",
      status: "unknown",
      reason: "Dashboard bootstrap nije dostupan, pa ne možemo da potvrdimo inventory rizike.",
      actionLabel: "Otvori Zalihe i dopuna",
      href: "/analytics/inventory",
      meta: "Izvor: dashboard bootstrap",
    };
  }

  const inventory = bootstrap.inventory;
  const hasInventory = hasInventorySignals(bootstrap);
  const lowStock = inventory?.lowStockCount ?? 0;
  const outOfStock = inventory?.outOfStockCount ?? 0;
  const dangerValue = bootstrap.executive?.inventoryDangerValueRsd ?? null;
  const hasRisk = lowStock > 0 || outOfStock > 0 || isTruthyNumber(dangerValue);
  const hasWarnings = bootstrap.errors.length > 0 || Boolean(bootstrap.meta?.isPartial);

  if (!hasInventory) {
    return {
      key: "inventory",
      index: "06",
      title: "Lager rizici dostupni",
      status: "blocked",
      reason: "Bootstrap ne sadrži potvrđen inventory signal, pa lager rizici nisu proverljivi.",
      actionLabel: "Otvori Zalihe i dopuna",
      href: "/analytics/inventory",
      meta: "Izvor: dashboard bootstrap",
    };
  }

  return {
    key: "inventory",
    index: "06",
    title: "Lager rizici dostupni",
    status: hasWarnings ? "warning" : "ready",
    reason: hasRisk
      ? `Low stock ${formatLoadCount(lowStock)}, out-of-stock ${formatLoadCount(outOfStock)} i rizik vrednosti ${fmtRsd(dangerValue, 0, "-")} su dostupni.`
      : `Inventar je učitan (${formatLoadCount(inventory?.totalSkuCount)} SKU), ali nema aktivnih rizika za demo priču.`,
    actionLabel: "Otvori Zalihe i dopuna",
    href: "/analytics/inventory",
    meta: `${formatLoadCount(inventory?.totalSkuCount)} SKU / ${formatLoadCount(inventory?.totalOnHand)} na stanju`,
  };
}

function buildActionsCard(
  actionCounts: AnalyticsActionCounts | null,
  outcomeSummary: AnalyticsActionOutcomeSummaryResponse | null,
): ReadinessCard {
  if (!actionCounts && !outcomeSummary) {
    return {
      key: "actions",
      index: "07",
      title: "Akcije kreirane/aktivne",
      status: "unknown",
      reason: "Red akcija nije dostupan, pa ne možemo da potvrdimo kreirane ili aktivne akcije.",
      actionLabel: "Otvori Centralne akcije",
      href: "/analytics/actions",
      meta: "Izvor: action counts i outcome summary",
    };
  }

  const newCount = actionCounts?.new ?? 0;
  const acceptedCount = actionCounts?.accepted ?? 0;
  const deferredCount = actionCounts?.deferred ?? 0;
  const rejectedCount = actionCounts?.rejected ?? 0;
  const doneCount = actionCounts?.done ?? 0;
  const openCount = newCount + acceptedCount + deferredCount;
  const totalCount = newCount + acceptedCount + deferredCount + rejectedCount + doneCount;
  const measuredCount = outcomeSummary?.totals.measuredCount ?? 0;
  const sampleSize = outcomeSummary?.meta.sampleSize ?? 0;
  const measuredSampleSize = outcomeSummary?.meta.measuredSampleSize ?? 0;
  const hasActions = totalCount > 0 || sampleSize > 0;
  const isWarning = hasActions && (openCount === 0 || measuredSampleSize === 0 || (outcomeSummary?.meta.warnings?.length ?? 0) > 0);

  return {
    key: "actions",
    index: "07",
    title: "Akcije kreirane/aktivne",
    status: hasActions ? (isWarning ? "warning" : "ready") : "warning",
    reason: hasActions
      ? `Ukupno ${formatLoadCount(totalCount)} akcija, otvorenih ${formatLoadCount(openCount)}, završenih ${formatLoadCount(doneCount)}. Ishodi su mereni na ${formatLoadCount(measuredCount)} akcija.`
      : "Red akcija je prazan, pa nema radnih akcija za pilot demo.",
    actionLabel: "Otvori Centralne akcije",
    href: "/analytics/actions",
    meta: outcomeSummary
      ? `Pokrivenost ishoda: ${fmtPctFromRatio(outcomeSummary.totals.outcomeCoverageRate, 0, "nije izračunata")}`
      : `${formatLoadCount(totalCount)} ukupno`,
  };
}

function buildReportsCard(
  pilotReport: PilotIntakeDurableReport | null,
  supplierReport: SupplierDecisionDurableReport | null,
): ReadinessCard {
  if (!pilotReport && !supplierReport) {
    return {
      key: "reports",
      index: "08",
      title: "Izveštaji spremni",
      status: "unknown",
      reason: "Nijedan report endpoint nije stigao, pa readiness izveštaja ne možemo da potvrdimo.",
      actionLabel: "Otvori pilot izveštaj",
      href: "/analytics/reports/pilot-intake",
      meta: "Izvor: pilot i supplier report",
    };
  }

  const hasPilot = Boolean(pilotReport);
  const hasSupplier = Boolean(supplierReport);
  const pilotQualityStatus = pilotReport?.dataQualityStatus?.trim().toLowerCase() ?? "";
  const supplierQualityStatus = supplierReport?.dataQualityStatus?.trim().toLowerCase() ?? "";
  const pilotWarning = Boolean(pilotReport && (pilotReport.usedFallback || pilotReport.recommendationAllowed === false || pilotQualityStatus === "warning" || isAnalyticsMetaWarning(pilotReport.meta)));
  const supplierWarning = Boolean(supplierReport && (supplierReport.usedFallback || supplierReport.recommendationAllowed === false || supplierQualityStatus === "warning" || isAnalyticsMetaWarning(supplierReport.meta)));
  const pilotReady = Boolean(pilotReport && (pilotReport.rows.length > 0 || pilotReport.sections.length > 0));
  const supplierReady = Boolean(supplierReport && (supplierReport.rows.length > 0 || supplierReport.sections.length > 0));
  const isCritical = Boolean(
    pilotQualityStatus === "critical" || pilotQualityStatus === "insufficient_data"
      || supplierQualityStatus === "critical" || supplierQualityStatus === "insufficient_data",
  );
  const status: ReadinessStatus = !hasPilot || !hasSupplier
    ? "warning"
    : isCritical
      ? "blocked"
      : pilotWarning || supplierWarning
        ? "warning"
        : pilotReady && supplierReady
          ? "ready"
          : "unknown";
  const qualityStatus = pilotReport?.dataQualityStatus ?? supplierReport?.dataQualityStatus ?? null;
  const qualityLabel = dataQualityStatusLabel(qualityStatus);
  const hasFallback = Boolean(pilotReport?.usedFallback || supplierReport?.usedFallback);

  return {
    key: "reports",
    index: "08",
    title: "Izveštaji spremni",
    status,
    reason: pilotReport && supplierReport
      ? status === "blocked" || status === "warning"
        ? `Pilot i supplier izveštaj postoje, ali kvalitet podataka je ${qualityLabel.toLowerCase()}${hasFallback ? " i korišćen je rezervni izvor" : ""}. ${status === "blocked" ? "Preporuke iz izveštaja ostaju blokirane." : "Proverite ih pre pilot prezentacije."}`
        : `Pilot report (${pilotReport.reportTitle ?? pilotReport.title ?? "pilot-intake"}) i supplier report (${supplierReport.reportTitle ?? supplierReport.title ?? "supplier-decision"}) su dostupni.`
      : pilotReport
        ? `Pilot report je dostupan, ali supplier report još nije potvrđen.`
        : `Supplier report je dostupan, ali pilot report još nije potvrđen.`,
    actionLabel: "Otvori pilot izveštaj",
    href: "/analytics/reports/pilot-intake",
    meta: qualityStatus
      ? `Kvalitet: ${qualityLabel}${hasFallback ? " · rezervni izvor" : ""}`
      : null,
  };
}

export function buildPilotReadinessCards(payload: ReadinessPayload): ReadinessCard[] {
  return [
    buildDataQualityCard(payload.intakeReport, payload.dataQualityHealth),
    buildRefreshCard(payload.refreshStatus),
    buildSalesCard(payload.bootstrap),
    buildProductsCard(payload.productDecisionCenter),
    buildSupplierCard(payload.bootstrap),
    buildInventoryCard(payload.bootstrap),
    buildActionsCard(payload.actionCounts, payload.actionOutcomeSummary),
    buildReportsCard(payload.pilotReport, payload.supplierReport),
  ];
}

function getHeaderDataQualityStatus(overallStatus: ReadinessStatus): "good" | "warning" | "critical" | "insufficient_data" {
  if (overallStatus === "ready") return "good";
  if (overallStatus === "warning") return "warning";
  if (overallStatus === "blocked") return "critical";
  return "insufficient_data";
}

function getSummarySummary(payload: ReadinessPayload, cards: ReadinessCard[]): string {
  const blocked = cards.filter((card) => card.status === "blocked").length;
  const warning = cards.filter((card) => card.status === "warning").length;
  const unknown = cards.filter((card) => card.status === "unknown").length;
  const ready = cards.filter((card) => card.status === "ready").length;
  const errors = payload.errors.length + (payload.bootstrap?.errors.length ?? 0);

  return `Spremno ${ready} od ${cards.length}; upozorenja ${warning}, blokirano ${blocked}, nepoznato ${unknown}${errors > 0 ? `, signali sa greškom ${errors}` : ""}.`;
}

export default function PilotReadinessPage() {
  const [payload, setPayload] = useState<ReadinessPayload>(EMPTY_PAYLOAD);
  const [loading, setLoading] = useState(true);
  const [reloadTick, setReloadTick] = useState(0);

  const loadSignals = useCallback(async (isCancelled?: () => boolean) => {
    setLoading(true);

    const nextPayload: ReadinessPayload = {
      ...EMPTY_PAYLOAD,
      errors: [],
    };

    const tasks: LoadTask[] = [
      { key: "bootstrap", request: getDashboardBootstrap(undefined, undefined, true), assign: (value) => { nextPayload.bootstrap = value as AnalyticsDashboardBootstrap; }, fallback: "Dashboard bootstrap nije dostupan." },
      { key: "refreshStatus", request: getAnalyticsRefreshStatus(), assign: (value) => { nextPayload.refreshStatus = value as AnalyticsRefreshStatus; }, fallback: "Status osvežavanja nije dostupan." },
      { key: "dataQualityHealth", request: getAnalyticsDataQualityHealth(), assign: (value) => { nextPayload.dataQualityHealth = value as AnalyticsDataQualityHealth; }, fallback: "Data quality health nije dostupan." },
      { key: "intakeReport", request: getPilotDataQualityIntakeReport({}), assign: (value) => { nextPayload.intakeReport = value as PilotDataQualityIntakeReport; }, fallback: "Pilot intake report nije dostupan." },
      { key: "productDecisionCenter", request: getProductDecisionCenter({ top: 100 }), assign: (value) => { nextPayload.productDecisionCenter = value as ProductDecisionCenterResponse; }, fallback: "Product Decision Center nije dostupan." },
      { key: "actionCounts", request: getAnalyticsActionCounts(), assign: (value) => { nextPayload.actionCounts = value as AnalyticsActionCounts; }, fallback: "Action counts nisu dostupni." },
      { key: "actionOutcomeSummary", request: getAnalyticsActionOutcomeSummary(), assign: (value) => { nextPayload.actionOutcomeSummary = value as AnalyticsActionOutcomeSummaryResponse; }, fallback: "Action outcome summary nije dostupan." },
      { key: "pilotReport", request: getPilotIntakeDurableReport({}), assign: (value) => { nextPayload.pilotReport = value as PilotIntakeDurableReport; }, fallback: "Pilot report nije dostupan." },
      { key: "supplierReport", request: getSupplierDecisionDurableReport({}), assign: (value) => { nextPayload.supplierReport = value as SupplierDecisionDurableReport; }, fallback: "Supplier report nije dostupan." },
    ];

    const results = await Promise.allSettled(tasks.map((task) => task.request));

    if (isCancelled?.()) {
      return;
    }

    results.forEach((result, index) => {
      const task = tasks[index];
      if (result.status === "fulfilled") {
        task.assign(result.value);
        return;
      }

      nextPayload.errors.push(normalizeLoadError(task.key, result.reason, task.fallback));
    });

    if (isCancelled?.()) {
      return;
    }

    setPayload(nextPayload);

    if (isCancelled?.()) {
      return;
    }

    setLoading(false);
  }, []);

  useEffect(() => {
    let cancelled = false;

    void loadSignals(() => cancelled);

    return () => {
      cancelled = true;
    };
  }, [loadSignals, reloadTick]);

  const cards = useMemo(() => buildPilotReadinessCards(payload), [payload]);
  const overallStatus = useMemo(() => deriveOverallStatus(cards), [cards]);
  const overallDataQualityStatus = getHeaderDataQualityStatus(overallStatus);
  const summaryMessage = useMemo(() => getSummarySummary(payload, cards), [cards, payload]);
  const hasAnySuccess = payload.bootstrap != null
    || payload.refreshStatus != null
    || payload.dataQualityHealth != null
    || payload.intakeReport != null
    || payload.productDecisionCenter != null
    || payload.actionCounts != null
    || payload.actionOutcomeSummary != null
    || payload.pilotReport != null
    || payload.supplierReport != null;
  const globalError = !loading && !hasAnySuccess && payload.errors.length > 0 ? payload.errors[0] : null;
  const allUnknown = cards.every((card) => card.status === "unknown");
  const overallDataQualitySummary = payload.intakeReport
    ? {
      missingSupplierCount: payload.intakeReport.issues.missingSupplierCount,
      missingCostCount: payload.intakeReport.issues.missingCostCount,
      missingCategoryCount: payload.intakeReport.issues.missingCategoryCount,
      insufficientSignalCount: payload.intakeReport.impact.insufficientSignalCount,
      ignoredRowsCount: payload.intakeReport.impact.ignoredRowsCount,
    }
    : payload.bootstrap?.executive?.dataQualitySummary
      ? {
        missingSupplierCount: payload.bootstrap.executive.dataQualitySummary.missingSupplierCount,
        missingCostCount: payload.bootstrap.executive.dataQualitySummary.missingCostCount,
        missingCategoryCount: null,
        insufficientSignalCount: payload.bootstrap.executive.dataQualitySummary.insufficientSignalCount,
        ignoredRowsCount: payload.bootstrap.executive.dataQualitySummary.ignoredRowsCount,
      }
      : undefined;
  const lastRefreshAt = payload.refreshStatus?.lastSuccessfulRefreshAtUtc
    ?? payload.intakeReport?.lastRefreshAtUtc
    ?? payload.pilotReport?.lastRefreshAtUtc
    ?? payload.bootstrap?.meta?.lastRefreshAtUtc
    ?? null;
  const periodFrom = payload.pilotReport?.periodFrom
    ?? payload.productDecisionCenter?.periodFromUtc
    ?? null;
  const periodTo = payload.pilotReport?.periodTo
    ?? payload.productDecisionCenter?.periodToUtc
    ?? null;
  const refreshErrors = payload.errors.filter((item) => item.key === "refreshStatus" || item.key === "bootstrap");
  const hasPartialSignals = payload.errors.length > 0
    || (payload.bootstrap?.errors.length ?? 0) > 0
    || Boolean(payload.bootstrap?.meta?.isPartial)
    || Boolean(payload.intakeReport?.meta?.isPartial)
    || Boolean(payload.dataQualityHealth?.meta?.isPartial)
    || Boolean(payload.productDecisionCenter?.meta?.isPartial)
    || Boolean(payload.pilotReport?.meta?.isPartial)
    || Boolean(payload.supplierReport?.meta?.isPartial)
    || payload.refreshStatus?.dataFreshnessStatus === "stale"
    || payload.refreshStatus?.dataFreshnessStatus === "critical";

  return (
    <div className="pilot-readiness-page">
      <AnalyticsTrustHeader
        title="Pilot spremnost"
        description="Kontrolni ekran pokazuje da li su ključni analytics signali spremni za demo ili pilot upotrebu."
        mode="report"
        periodFrom={periodFrom}
        periodTo={periodTo}
        lastRefreshAt={lastRefreshAt}
        dataFreshnessStatus={payload.refreshStatus?.dataFreshnessStatus ?? "unknown"}
        refreshIsRunning={payload.refreshStatus?.isRunning ?? false}
        refreshCurrentStep={payload.refreshStatus?.currentStep ?? null}
        isPartial={hasPartialSignals}
        dataSource="Postojeći analytics endpointi"
        dataQualityStatus={overallDataQualityStatus}
        dataQualitySummary={overallDataQualitySummary}
        dataQualityHref="/analytics/data-quality"
        refreshStatusHref="/admin/configuration?panel=workers"
        methodologyHref="/analytics/data-quality"
        methodologyLabel="Kako čitati readiness"
        emptyStateReason={summaryMessage}
        compact
      />

      <AnalyticsRefreshStatusBanner
        status={payload.refreshStatus}
        loading={loading && !payload.refreshStatus}
        error={refreshErrors[0]?.message ?? null}
      />

      <section className={`pilot-readiness-overview pilot-readiness-overview-${overallStatus}`}>
        <div className="pilot-readiness-overview-copy">
          <p className="pilot-readiness-overline">Pilot readiness checklist</p>
          <h2>{overallStatus === "ready" ? "Spremno za demo" : overallStatus === "warning" ? "Spremno uz upozorenja" : overallStatus === "blocked" ? "Pilot nije spreman" : "Spremnost nije potvrđena"}</h2>
          <p>
            {summaryMessage} Nepoznato nikad ne znači zeleno.
          </p>
        </div>

        <div className="pilot-readiness-overview-metrics" aria-label="Sažetak readiness statusa">
          {[
            { label: "Spremno", value: cards.filter((card) => card.status === "ready").length, tone: "ready" },
            { label: "Upozorenja", value: cards.filter((card) => card.status === "warning").length, tone: "warning" },
            { label: "Blokirano", value: cards.filter((card) => card.status === "blocked").length, tone: "blocked" },
            { label: "Nepoznato", value: cards.filter((card) => card.status === "unknown").length, tone: "unknown" },
          ].map((item) => (
            <article key={item.label} className={`pilot-readiness-metric pilot-readiness-metric-${item.tone}`}>
              <span>{item.label}</span>
              <strong>{formatLoadCount(item.value)}</strong>
            </article>
          ))}
        </div>

        <div className="pilot-readiness-overview-actions">
          <button type="button" className="pilot-readiness-action" onClick={() => setReloadTick((value) => value + 1)}>
            Ponovo proveri readiness
          </button>
          <Link to="/analytics/data-quality" className="pilot-readiness-action">
            Kvalitet podataka
          </Link>
          <Link to="/admin/configuration?panel=workers" className="pilot-readiness-action">
            Worker panel
          </Link>
        </div>
      </section>

      {globalError ? (
        <AnalyticsErrorState
          title="Pilot readiness trenutno nije dostupan"
          message={globalError.message}
          errorCode={globalError.errorCode ?? undefined}
          correlationId={globalError.correlationId ?? undefined}
          suggestions={[
            "Proverite da li worker i refresh status odgovaraju.",
            "Otvorte Kvalitet podataka i proverite import.",
            "Pokušajte ponovo nakon osvežavanja.",
          ]}
          onRetry={() => setReloadTick((value) => value + 1)}
          helpHref="/admin/configuration?panel=workers"
          helpLabel="Otvori worker panel"
        />
      ) : null}

      {!loading && allUnknown && !globalError ? (
        <AnalyticsEmptyState
          variant="insufficient_data"
          title="Nema potvrđenih readiness signala"
          message="Stranica je učitana, ali nijedan signalni izvor nije potvrdio da je pilot spreman."
          reasons={[
            "Proverite refresh status i worker panel.",
            "Otvorite Kvalitet podataka i potvrdite da import radi.",
            "Ponovo proverite readiness nakon što se bootstrap i report endpointi vrate.",
          ]}
          actions={[
            { label: "Ponovo proveri", onClick: () => setReloadTick((value) => value + 1) },
            { label: "Otvori Kvalitet podataka", href: "/analytics/data-quality" },
            { label: "Otvori worker panel", href: "/admin/configuration?panel=workers" },
          ]}
          dataQualityHref="/analytics/data-quality"
          refreshStatusHref="/admin/configuration?panel=workers"
          showDefaultLinks={false}
        />
      ) : null}

      {!loading && hasPartialSignals && !globalError ? (
        <section className="pilot-readiness-note" role="status">
          <strong>Dostupni su delimični signali.</strong>
          <span>
            Prikazujemo samo potvrđene stavke. Nepoznato ostaje nepoznato dok API ne vrati validan signal.
          </span>
        </section>
      ) : null}

      {loading && cards.every((card) => card.status === "unknown") ? (
        <section className="pilot-readiness-loading" aria-live="polite">
          Učitavanje pilot readiness signala...
        </section>
      ) : null}

      <section className="pilot-readiness-checklist" aria-label="Pilot readiness checklist">
        {cards.map((card) => (
          <article key={card.key} className={`pilot-readiness-card ${statusToneClass(card.status)}`}>
            <div className="pilot-readiness-card-head">
              <div className="pilot-readiness-card-title">
                <span className="pilot-readiness-card-index">{card.index}</span>
                <h3>{card.title}</h3>
              </div>
              <span className={`pilot-readiness-badge pilot-readiness-badge-${STATUS_TONES[card.status]}`}>
                {statusLabel(card.status)}
              </span>
            </div>

            <p className="pilot-readiness-card-reason">{normalizeMessage(card.reason, "Signal nije potvrđen.")}</p>

            {card.meta ? <p className="pilot-readiness-card-meta">{card.meta}</p> : null}

            <div className="pilot-readiness-card-footer">
              <Link className="pilot-readiness-card-action" to={card.href}>
                {card.actionLabel}
              </Link>
            </div>
          </article>
        ))}
      </section>

      <details className="pilot-readiness-help">
        <summary>Kako čitati checklist</summary>
        <div className="pilot-readiness-help-content">
          <p>Ready znači da je signal potvrđen i da postoji dovoljno podataka za demo ili pilot upotrebu.</p>
          <p>Warning znači da signal postoji, ali je delimičan, zastareo ili bez dovoljno potpunog uzorka.</p>
          <p>Blocked znači da ključni signal nije dovoljan za pouzdano predstavljanje ili da je dataset prazan.</p>
          <p>Nepoznato znači da API nije potvrdio stanje. To nikad ne treba tumačiti kao uspeh.</p>
        </div>
      </details>
    </div>
  );
}

export type { ReadinessCard, ReadinessStatus, ReadinessPayload };

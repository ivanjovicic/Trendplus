import { useId, useState } from "react";
import { Link } from "react-router-dom";
import { formatDate, formatDateTime } from "../../utils/analyticsFormatters";
import { getSafeAnalyticsErrorMessage } from "../../utils/analyticsErrorMessages";
import { supplierDecisionDatasetLabel, supplierDecisionProvenanceLabel, supplierDecisionReasonText } from "../../utils/supplierDecisionLabels";
import { getAnalyticsDecisionReadiness, getAnalyticsIntegrityState } from "../../utils/analyticsDecisionReadiness";
import type { AnalyticsResponseMeta } from "../../types/analytics";
import "./AnalyticsTrustHeader.css";

type AnalyticsTrustHeaderProps = {
  title: string;
  description: string;
  periodFrom?: string | null;
  periodTo?: string | null;
  requestedPeriodFrom?: string | null;
  requestedPeriodTo?: string | null;
  effectivePeriodFrom?: string | null;
  effectivePeriodTo?: string | null;
  observedPeriodFrom?: string | null;
  observedPeriodTo?: string | null;
  lastRefreshAt?: string | null;
  dataFreshnessStatus?: "fresh" | "stale" | "critical" | "unknown" | string | null;
  refreshIsRunning?: boolean;
  refreshCurrentStep?: string | null;
  isPartial?: boolean;
  dataSource?: string | null;
  dataQualityStatus?: "good" | "warning" | "critical" | "insufficient_data" | string | null;
  dataQualitySummary?: {
    missingSupplierCount?: number | null;
    missingCostCount?: number | null;
    missingCategoryCount?: number | null;
    insufficientSignalCount?: number | null;
    ignoredRowsCount?: number | null;
  };
  mode: "recommendation" | "signal" | "report";
  recommendationNote?: string;
  emptyStateReason?: string | null;
  methodologyHref?: string;
  methodologyLabel?: string;
  dataQualityHref?: string;
  refreshStatusHref?: string;
  compact?: boolean;
  requestedDataset?: string | null;
  effectiveDataset?: string | null;
  effectivePeriodLabel?: string | null;
  provenanceBasis?: string | null;
  usedFallback?: boolean;
  fallbackReason?: string | null;
  fallbackReasonCode?: string | null;
  recommendationAllowed?: boolean | null;
  trustPending?: boolean;
  meta?: AnalyticsResponseMeta | null;
  showOperationsTrust?: boolean;
  showTitle?: boolean;
};

const MODE_LABELS: Record<AnalyticsTrustHeaderProps["mode"], string> = {
  recommendation: "Preporuka sistema",
  signal: "Analitički signal",
  report: "Izveštaj",
};

const STATUS_LABELS: Record<string, string> = {
  good: "Podaci deluju pouzdano",
  warning: "Postoje upozorenja",
  critical: "Podaci nisu pouzdani",
  insufficient_data: "Nedovoljno podataka",
};

const FRESHNESS_LABELS: Record<string, string> = {
  fresh: "Sveže",
  stale: "Zastarelo",
  critical: "Kritično",
  unknown: "Nije poznato",
};

const FRESHNESS_REASON_LABELS: Record<string, string> = {
  source_import_recent_success: "Potvrđeno poslednjim uspešnim importom",
  source_import_older_than_24h: "Poslednji uspešan import je stariji od 24 sata",
  source_import_older_than_72h: "Poslednji uspešan import je stariji od 72 sata",
  source_import_failure_after_success: "Zabeležen je neuspešan import nakon poslednjeg uspeha",
  source_import_evidence_missing: "Nema trajnog dokaza o uspešnom importu",
  source_import_not_store_scoped: "Import dokaz nije vezan za izabranu prodavnicu",
  source_watermark_unavailable_for_existing_data: "Trajni izvorni watermark nije dostupan za postojeće podatke",
  source_scope_contains_unwatermarked_rows: "Izabrani skup sadrži redove bez zajedničkog trajnog watermark-a",
  source_freshness_context_unavailable: "Svežina nije vezana za potpuni kontekst ovog upita",
  source_freshness_evidence_unavailable: "Dokaz o svežini trenutno nije dostupan",
};

const REFRESH_STEP_LABELS: Record<string, string> = {
  sales_facts_refresh: "osvežavanje prodajnih činjenica",
  product_dim_refresh: "osvežavanje proizvoda",
  supplier_decision_mvs: "osvežavanje signala dobavljača",
  product_decision_snapshot: "osvežavanje odluka za proizvode",
  inventory_recommendations: "osvežavanje preporuka zaliha",
};

const FALLBACK_REASON_LABELS: Record<string, string> = {
  no_mv_30d: "Nema dovoljno zapisa u traženom periodu",
  no_window_rows: "Nema dovoljno zapisa u traženom periodu",
  range_uses_all_time: "Korišćen je širi istorijski skup podataka",
  fallback_dataset_used: "Korišćen je pomoćni skup podataka",
  missing_post_observation: "Nedostaje deo post-nivelacija podataka",
};

function renderLink(href: string, label: string, className: string) {
  if (href.startsWith("/")) {
    return <Link to={href} className={className}>{label}</Link>;
  }

  return <a href={href} className={className}>{label}</a>;
}

function formatBelgradeDateTime(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return formatDateTime(value);
  return date.toLocaleString("sr-RS", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
    timeZone: "Europe/Belgrade",
  });
}

function normalizeFreshness(value: string | null | undefined): "fresh" | "stale" | "critical" | "unknown" {
  const normalized = typeof value === "string" ? value.trim().toLowerCase() : null;
  if (normalized === "fresh" || normalized === "stale" || normalized === "critical") {
    return normalized;
  }

  return "unknown";
}

function normalizeToken(value: string | null | undefined): string | null {
  const normalized = value?.trim().toLowerCase().replace(/[\s-]+/g, "_");
  return normalized || null;
}

function safeRefreshStepLabel(value: string | null | undefined): string | null {
  const normalized = normalizeToken(value);
  if (!normalized) return null;
  return REFRESH_STEP_LABELS[normalized] ?? "Obrada podataka";
}

function safeFallbackReasonLabel(value: string | null | undefined): string | null {
  const normalized = normalizeToken(value);
  if (!normalized) return null;
  return FALLBACK_REASON_LABELS[normalized] ?? "Dodatni razlog pomoćnog skupa nije naveden.";
}

function normalizeStatus(value: string | null | undefined): "good" | "warning" | "critical" | "insufficient_data" | null {
  if (typeof value !== "string" || !value.trim()) {
    return null;
  }

  const normalized = value.trim().toLowerCase();
  if (normalized === "good" || normalized === "warning" || normalized === "critical" || normalized === "insufficient_data") {
    return normalized;
  }

  return null;
}

function statusTone(status: ReturnType<typeof normalizeStatus>): "good" | "warning" | "critical" | "insufficient" | "neutral" {
  if (status === "good") return "good";
  if (status === "warning") return "warning";
  if (status === "critical") return "critical";
  if (status === "insufficient_data") return "insufficient";
  return "neutral";
}

function renderSummaryValue(value: number | null | undefined): string {
  if (value == null || !Number.isFinite(value)) {
    return "-";
  }

  return value.toLocaleString("sr-RS");
}

function hasSummaryValues(
  summary: AnalyticsTrustHeaderProps["dataQualitySummary"],
): summary is NonNullable<AnalyticsTrustHeaderProps["dataQualitySummary"]> {
  if (!summary) {
    return false;
  }

  return [
    summary.missingSupplierCount,
    summary.missingCostCount,
    summary.missingCategoryCount,
    summary.insufficientSignalCount,
    summary.ignoredRowsCount,
  ].some((value) => value != null && Number.isFinite(value));
}

export default function AnalyticsTrustHeader({
  title,
  description,
  periodFrom,
  periodTo,
  requestedPeriodFrom,
  requestedPeriodTo,
  effectivePeriodFrom,
  effectivePeriodTo,
  observedPeriodFrom,
  observedPeriodTo,
  lastRefreshAt,
  dataFreshnessStatus,
  refreshIsRunning,
  refreshCurrentStep,
  isPartial,
  dataSource,
  dataQualityStatus,
  dataQualitySummary,
  mode,
  recommendationNote,
  emptyStateReason,
  methodologyHref,
  methodologyLabel,
  dataQualityHref,
  refreshStatusHref,
  compact = false,
  requestedDataset,
  effectiveDataset,
  effectivePeriodLabel,
  provenanceBasis,
  usedFallback,
  fallbackReason,
  fallbackReasonCode,
  recommendationAllowed,
  trustPending = false,
  meta = null,
  showOperationsTrust = false,
  showTitle = true,
}: AnalyticsTrustHeaderProps) {
  const normalizedStatus = trustPending ? null : normalizeStatus(dataQualityStatus);
  const tone = trustPending ? "neutral" : statusTone(normalizedStatus);
  const statusLabel = trustPending
    ? "Učitavanje pouzdanosti"
    : (normalizedStatus ? STATUS_LABELS[normalizedStatus] : "Status kvaliteta nije dostupan");
  const freshness = normalizeFreshness(dataFreshnessStatus);
  const freshnessReasonCode = meta?.dataFreshnessReasonCode?.trim() || null;
  const safePeriodFrom = typeof periodFrom === "string" ? periodFrom.trim() : null;
  const safePeriodTo = typeof periodTo === "string" ? periodTo.trim() : null;
  const safeRequestedFrom = typeof requestedPeriodFrom === "string" ? requestedPeriodFrom.trim() : safePeriodFrom;
  const safeRequestedTo = typeof requestedPeriodTo === "string" ? requestedPeriodTo.trim() : safePeriodTo;
  const safeEffectiveFrom = typeof effectivePeriodFrom === "string" ? effectivePeriodFrom.trim() : null;
  const safeEffectiveTo = typeof effectivePeriodTo === "string" ? effectivePeriodTo.trim() : null;
  const safeObservedFrom = typeof observedPeriodFrom === "string" ? observedPeriodFrom.trim() : null;
  const safeObservedTo = typeof observedPeriodTo === "string" ? observedPeriodTo.trim() : null;
  const hasPeriod = Boolean(safeRequestedFrom && safeRequestedTo);
  const hasEffectivePeriod = Boolean(safeEffectiveFrom && safeEffectiveTo);
  const hasObservedPeriod = Boolean(safeObservedFrom && safeObservedTo);
  const hasSummary = hasSummaryValues(dataQualitySummary);
  const normalizedRequestedDataset = supplierDecisionDatasetLabel(requestedDataset);
  const normalizedEffectiveDataset = supplierDecisionDatasetLabel(effectiveDataset);
  const hasDataset = Boolean(normalizedRequestedDataset || normalizedEffectiveDataset);
  const datasetValue = normalizedRequestedDataset && normalizedEffectiveDataset
    ? `${normalizedRequestedDataset} -> ${normalizedEffectiveDataset}`
    : (normalizedEffectiveDataset ?? normalizedRequestedDataset);
  const effectiveLabel = typeof effectivePeriodLabel === "string" ? effectivePeriodLabel.trim() || null : null;
  const provenanceLabel = supplierDecisionProvenanceLabel(provenanceBasis);
  const dataSourceLabel = typeof dataSource === "string" ? dataSource.trim() || null : null;
  const recommendationNoteText = typeof recommendationNote === "string" ? recommendationNote.trim() || null : null;
  const emptyStateReasonText = typeof emptyStateReason === "string" ? emptyStateReason.trim() || null : null;
  const refreshStepLabel = safeRefreshStepLabel(refreshCurrentStep);
  const fallbackReasonLabel = safeFallbackReasonLabel(fallbackReasonCode);
  const fallbackReasonText = fallbackReason
    ? supplierDecisionReasonText(getSafeAnalyticsErrorMessage(fallbackReason, fallbackReasonCode, "Dodatni razlog pomoćnog skupa nije naveden."))
    : null;
  const showFallbackBanner = Boolean(usedFallback);
  const showGatedBanner = !trustPending && mode === "recommendation" && recommendationAllowed !== true && !showFallbackBanner;
  const showPartialBanner = Boolean(isPartial) || freshness === "stale" || freshness === "critical";
  const resolvedDataQualityHref = dataQualityHref || "/analytics/data-quality";
  const resolvedRefreshStatusHref = refreshStatusHref || "/admin/configuration?panel=workers";
  const readiness = trustPending
    ? null
    : getAnalyticsDecisionReadiness(meta, mode === "recommendation" ? "recommendation" : mode === "signal" ? "signal" : "report");
  const readinessState = trustPending ? "unavailable" : readiness?.state;
  const readinessStateIsKnown = readinessState === "decision_ready"
    || readinessState === "signal_only"
    || readinessState === "blocked"
    || readinessState === "unavailable";
  const resolvedReadinessState = readinessStateIsKnown ? readinessState : "unavailable";
  const evidenceId = !trustPending ? meta?.operationsIntegrityEvidenceId?.trim() || null : null;
  const integrityContextMatches = !trustPending
    && meta?.operationsIntegrityContextMatches === true
    && evidenceId !== null;
  const integrityState = trustPending
    ? "unavailable"
    : getAnalyticsIntegrityState(meta, integrityContextMatches);
  const rawIntegrityState = meta?.operationsIntegrityStatus?.trim().toLowerCase() ?? null;
  const readinessLabels: Record<string, string> = {
    decision_ready: "Spremno za odluku",
    signal_only: "Samo signal",
    blocked: "Blokirano",
    unavailable: "Nije dostupno",
  } as const;
  const integrityLabels: Record<string, string> = {
    verified: "Provereno za ovaj kontekst",
    unverified: meta?.operationsIntegrityFamily === "nivelacija" && !evidenceId
      ? "Nije nezavisno provereno"
        : integrityContextMatches
          ? "Nije provereno"
          : meta?.operationsIntegrityContextMatches === true && !evidenceId
            ? "Nije provereno — dokaz nije dostupan"
            : "Nije provereno za izabrani kontekst",
    degraded: "Provera je degradirana",
    drift_detected: "Odstupanje je otkriveno",
    unavailable: "Dokaz integriteta nije dostupan",
  } as const;
  const evidenceContext = !trustPending ? meta?.operationsIntegrityContextFingerprint?.trim() || null : null;
  const responseContext = !trustPending ? meta?.context?.fingerprint?.trim() || null : null;
  const readinessReason = readiness?.reasonCodes?.filter(Boolean).join(", ") || null;
  const evidenceHref = evidenceId
    ? `/api/analytics/operations-integrity/evidence/${encodeURIComponent(evidenceId)}`
    : null;

  const [detailsExpanded, setDetailsExpanded] = useState(false);
  const periodSummary = hasPeriod
    ? `${formatDate(safeRequestedFrom)} - ${formatDate(safeRequestedTo)}`
    : "Period nije definisan";
  const effectivePeriodSummary = hasEffectivePeriod
    ? `${formatDate(safeEffectiveFrom)} - ${formatDate(safeEffectiveTo)}`
    : "Nije dostupno";
  const readinessSummaryLabel = showOperationsTrust
    ? resolvedReadinessState === "blocked" ? readinessLabels.blocked : normalizedStatus === "critical" ? statusLabel : readinessLabels[resolvedReadinessState]
    : statusLabel;
  const readinessSummaryTone = normalizedStatus === "critical" || freshness === "critical"
    ? "critical"
    : showOperationsTrust
      ? resolvedReadinessState === "blocked" ? "critical" : resolvedReadinessState === "decision_ready" ? "good" : resolvedReadinessState === "signal_only" ? "warning" : "neutral"
      : tone;
  const detailsId = useId();

  return (
    <section
      className={`analytics-trust-header${detailsExpanded ? " analytics-trust-header--expanded" : " analytics-trust-header--collapsed"}${compact ? " analytics-trust-header--compact" : ""}`}
      aria-label="Kontekst pouzdanosti analitike"
    >
      <div className="ath-summary-bar" data-testid="analytics-trust-summary">
        <span className={`ath-summary-readiness ath-summary-readiness-${readinessSummaryTone}`} data-testid="analytics-trust-summary-readiness">
          {readinessSummaryLabel}
        </span>
        {showTitle ? <h1 className="ath-title ath-summary-title">{title}</h1> : null}
        <span className="ath-summary-fact ath-summary-period-fact"><span>Efektivni period</span><strong>{effectivePeriodSummary}</strong></span>
        {hasObservedPeriod ? (
          <span className="ath-summary-fact"><span>Podaci do</span><strong>{formatDate(safeObservedTo)}</strong></span>
        ) : null}
        <span className={`ath-freshness-badge ath-freshness-${freshness}`}>{FRESHNESS_LABELS[freshness]}</span>
        <button
          type="button"
          className="ath-details-toggle"
          aria-expanded={detailsExpanded}
          aria-controls={detailsId}
          data-testid="analytics-trust-details-toggle"
          onClick={() => setDetailsExpanded((open) => !open)}
        >
          {detailsExpanded ? "Sakrij detalje" : "Detalji pouzdanosti"}
        </button>
      </div>
      <div id={detailsId} className="ath-full-details" hidden={!detailsExpanded}>
      <div className="ath-main">
        <div className="ath-main-copy">
          <p className="ath-overline">{MODE_LABELS[mode]}</p>
          <p className={`ath-description${detailsExpanded ? "" : " ath-description--clamp"}`}>{description}</p>
          {refreshIsRunning ? (
            <p className="ath-live">Osvežavanje je u toku{refreshStepLabel ? ` (${refreshStepLabel})` : ""}</p>
          ) : null}
        </div>
        <div className={`ath-status ath-status-${tone}`}>
          <span className="ath-status-label">{statusLabel}</span>
        </div>
      </div>

      <div className="ath-context-strip" data-testid="analytics-trust-context-strip">
        <div className="ath-context-item">
          <span className="ath-context-key">Period</span>
          <strong className="ath-context-value">{periodSummary}</strong>
        </div>
        <div className="ath-context-item">
          <span className="ath-context-key">Svežina</span>
          <span className={`ath-freshness-badge ath-freshness-${freshness}`}>{FRESHNESS_LABELS[freshness]}</span>
        </div>
        {hasEffectivePeriod && (safeEffectiveFrom !== safeRequestedFrom || safeEffectiveTo !== safeRequestedTo) ? (
          <div className="ath-context-item">
            <span className="ath-context-key">Efektivni period</span>
            <strong className="ath-context-value">{formatDate(safeEffectiveFrom)} - {formatDate(safeEffectiveTo)}</strong>
          </div>
        ) : null}
      </div>

      {showFallbackBanner ? (
        <div className="ath-banner ath-banner-warning" role="status">
          <strong>Pomoćni skup je aktivan.</strong>{" "}
          Za traženi period nema dovoljno podataka. Korišćen je skup podataka {effectiveLabel ?? normalizedEffectiveDataset ?? "Nije dostupno"} kao pomoćni signal.
          {fallbackReasonText ? ` ${fallbackReasonText}` : null}
          {fallbackReasonLabel ? <span className="ath-banner-code"> ({fallbackReasonLabel})</span> : null}
        </div>
      ) : null}

      {showGatedBanner ? (
        <div className="ath-banner ath-banner-neutral" role="status">
          <strong>Preporuka nije dostupna.</strong> Sistem ne prikazuje konačnu preporuku jer nema dovoljno pouzdanih podataka za izabrani period.
        </div>
      ) : null}

      {showPartialBanner ? (
        <div className="ath-banner ath-banner-warning" role="status">
          <strong>Upozorenje:</strong> Prikaz može biti delimičan ili zastareo.
        </div>
      ) : null}

      {recommendationNoteText ? <p className="ath-note">{recommendationNoteText}</p> : null}
      {emptyStateReasonText ? <p className="ath-empty-reason">{emptyStateReasonText}</p> : null}

      <div className="ath-details-panel" data-testid="analytics-trust-details-panel" hidden={!detailsExpanded}>
        {detailsExpanded ? (
          <>
        <div className="ath-meta-grid">
        {showOperationsTrust ? <>
        <div className="ath-meta-item" data-testid="analytics-trust-readiness">
          <span className="ath-meta-key">Spremnost odluke</span>
          <strong className={`ath-trust-state ath-trust-state-${resolvedReadinessState}`} data-testid="analytics-trust-readiness-state">
            {trustPending ? "Provera u toku" : readinessLabels[resolvedReadinessState]}
          </strong>
          {readinessReason ? <span className="ath-meta-subtle">{readinessReason}</span> : null}
        </div>
        <div className="ath-meta-item" data-testid="analytics-trust-integrity">
          <span className="ath-meta-key">Integritet operacija</span>
          <strong className={`ath-trust-state ath-trust-state-${integrityState}`} data-testid="analytics-trust-integrity-state">
            {trustPending ? "Provera u toku" : integrityLabels[integrityState]}
          </strong>
          {rawIntegrityState && evidenceId && !integrityContextMatches && !trustPending ? (
            <span className="ath-meta-subtle">Poslednji nalaz: {rawIntegrityState}</span>
          ) : null}
          {meta?.operationsIntegrityCheckedAtUtc && !trustPending ? (
            <span className="ath-meta-subtle">Provereno: {formatBelgradeDateTime(meta.operationsIntegrityCheckedAtUtc)}</span>
          ) : null}
          {evidenceId ? <span className="ath-integrity-evidence-id">ID dokaza: {evidenceId}</span> : null}
          {evidenceContext ? <span className="ath-integrity-context">Kontekst dokaza: {evidenceContext}</span> : null}
          {responseContext && evidenceContext && responseContext !== evidenceContext ? (
            <span className="ath-integrity-context">Kontekst upita: {responseContext}</span>
          ) : null}
          {evidenceHref ? <a className="ath-integrity-evidence-link" href={evidenceHref} target="_blank" rel="noreferrer">Pregledaj dokaz</a> : null}
        </div>
        </> : null}
        <div className="ath-meta-item">
          <span className="ath-meta-key">Period</span>
          <strong className="ath-meta-value">
            {hasPeriod ? `${formatDate(safeRequestedFrom)} - ${formatDate(safeRequestedTo)}` : "Period nije definisan"}
          </strong>
        </div>
        {hasEffectivePeriod ? (
          <div className="ath-meta-item">
            <span className="ath-meta-key">Efektivni period</span>
            <strong className="ath-meta-value">{formatDate(safeEffectiveFrom)} - {formatDate(safeEffectiveTo)}</strong>
            {effectiveLabel ? <span className="ath-meta-subtle">{effectiveLabel}</span> : null}
          </div>
        ) : null}
        {hasObservedPeriod ? (
          <div className="ath-meta-item">
            <span className="ath-meta-key">Posmatrani period</span>
            <strong className="ath-meta-value">{formatDate(safeObservedFrom)} - {formatDate(safeObservedTo)}</strong>
          </div>
        ) : null}
        <div className="ath-meta-item">
          <span className="ath-meta-key">Poslednje osveženje</span>
          <strong className="ath-meta-value">
            {lastRefreshAt ? formatBelgradeDateTime(lastRefreshAt) : "Vreme osveženja nije dostupno"}
          </strong>
          <span className={`ath-freshness-badge ath-freshness-${freshness}`}>
            {FRESHNESS_LABELS[freshness]}
          </span>
          {freshnessReasonCode ? (
            <span className="ath-meta-subtle">
              {FRESHNESS_REASON_LABELS[freshnessReasonCode] ?? "Razlog stanja svežine nije mapiran."}
            </span>
          ) : null}
          {meta?.dataFreshnessEvidenceId ? (
            <span className="ath-meta-subtle">ID dokaza: {meta.dataFreshnessEvidenceId}</span>
          ) : null}
        </div>
        <div className="ath-meta-item">
          <span className="ath-meta-key">Izvor podataka</span>
          <strong className="ath-meta-value">
            {dataSourceLabel || "Izvor podataka nije naveden"}
          </strong>
        </div>
        {provenanceLabel ? (
          <div className="ath-meta-item">
            <span className="ath-meta-key">Osnova generisanja</span>
            <strong className="ath-meta-value">{provenanceLabel}</strong>
          </div>
        ) : null}
        {hasDataset ? (
          <div className="ath-meta-item">
            <span className="ath-meta-key">Skup podataka</span>
            <strong className="ath-meta-value">{datasetValue ?? "-"}</strong>
            {effectiveLabel ? <span className="ath-meta-subtle">{effectiveLabel}</span> : null}
          </div>
        ) : null}
      </div>

        <div className={`ath-summary ${compact ? "ath-summary-compact" : ""}`}>
          {compact ? null : <h2>Sažetak kvaliteta podataka</h2>}
          {hasSummary ? (
            <div className={compact ? "ath-summary-chips" : "ath-summary-grid"}>
              <div><span>Artikli bez dobavljača</span><strong>{renderSummaryValue(dataQualitySummary.missingSupplierCount)}</strong></div>
              <div><span>Redovi bez nabavne cene</span><strong>{renderSummaryValue(dataQualitySummary.missingCostCount)}</strong></div>
              <div><span>Artikli bez kategorije</span><strong>{renderSummaryValue(dataQualitySummary.missingCategoryCount)}</strong></div>
              <div><span>Nedovoljni signali</span><strong>{renderSummaryValue(dataQualitySummary.insufficientSignalCount)}</strong></div>
              <div><span>Ignorisani redovi (skriveno zbog limita)</span><strong>{renderSummaryValue(dataQualitySummary.ignoredRowsCount)}</strong></div>
            </div>
          ) : (
            <p className={`ath-summary-missing ${compact ? "ath-summary-missing-compact" : ""}`}>Detaljan kvalitet podataka nije dostupan za ovaj ekran.</p>
          )}
        </div>

        <div className="ath-footer">
          {renderLink(resolvedDataQualityHref, "Kvalitet podataka", "ath-footer-link")}
          {renderLink(resolvedRefreshStatusHref, "Status osvežavanja", "ath-footer-link")}
          {methodologyHref ? renderLink(methodologyHref, methodologyLabel ?? "Metodologija i tumačenje signala", "ath-footer-link") : null}
        </div>
          </>
        ) : null}
      </div>
      </div>
    </section>
  );
}

export type { AnalyticsTrustHeaderProps };


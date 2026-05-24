import { formatDate, formatDateTime } from "../../utils/analyticsFormatters";
import "./AnalyticsTrustHeader.css";

type AnalyticsTrustHeaderProps = {
  title: string;
  description: string;
  periodFrom?: string | null;
  periodTo?: string | null;
  lastRefreshAt?: string | null;
  dataFreshnessStatus?: "fresh" | "stale" | "critical" | "unknown" | string | null;
  refreshIsRunning?: boolean;
  refreshCurrentStep?: string | null;
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
  requestedDataset?: string | null;
  effectiveDataset?: string | null;
  effectivePeriodLabel?: string | null;
  usedFallback?: boolean;
  fallbackReason?: string | null;
  fallbackReasonCode?: string | null;
  recommendationAllowed?: boolean | null;
};

const MODE_LABELS: Record<AnalyticsTrustHeaderProps["mode"], string> = {
  recommendation: "Preporuka sistema",
  signal: "Analiticki signal",
  report: "Izvestaj",
};

const STATUS_LABELS: Record<string, string> = {
  good: "Podaci deluju pouzdano",
  warning: "Postoje upozorenja",
  critical: "Podaci nisu pouzdani",
  insufficient_data: "Nedovoljno podataka",
};

const FRESHNESS_LABELS: Record<string, string> = {
  fresh: "Sveze",
  stale: "Zastarelo",
  critical: "Kriticno",
  unknown: "Nije poznato",
};

function normalizeFreshness(value: string | null | undefined): "fresh" | "stale" | "critical" | "unknown" {
  if (value === "fresh" || value === "stale" || value === "critical") {
    return value;
  }

  return "unknown";
}

function normalizeStatus(value: string | null | undefined): "good" | "warning" | "critical" | "insufficient_data" | null {
  if (!value) {
    return null;
  }

  const normalized = value.trim().toLowerCase();
  if (normalized === "good" || normalized === "warning" || normalized === "critical" || normalized === "insufficient_data") {
    return normalized;
  }

  return null;
}

function statusTone(status: ReturnType<typeof normalizeStatus>): "good" | "warning" | "critical" | "neutral" {
  if (status === "good") return "good";
  if (status === "warning") return "warning";
  if (status === "critical") return "critical";
  return "neutral";
}

function renderSummaryValue(value: number | null | undefined): string {
  if (value == null) {
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
  ].some((value) => value != null);
}

export default function AnalyticsTrustHeader({
  title,
  description,
  periodFrom,
  periodTo,
  lastRefreshAt,
  dataFreshnessStatus,
  refreshIsRunning,
  refreshCurrentStep,
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
  requestedDataset,
  effectiveDataset,
  effectivePeriodLabel,
  usedFallback,
  fallbackReason,
  fallbackReasonCode,
  recommendationAllowed,
}: AnalyticsTrustHeaderProps) {
  const normalizedStatus = normalizeStatus(dataQualityStatus);
  const tone = statusTone(normalizedStatus);
  const statusLabel = normalizedStatus ? STATUS_LABELS[normalizedStatus] : "Status kvaliteta nije dostupan";
  const freshness = normalizeFreshness(dataFreshnessStatus);
  const hasPeriod = Boolean(periodFrom && periodTo);
  const hasSummary = hasSummaryValues(dataQualitySummary);
  const hasDataset = Boolean((requestedDataset && requestedDataset.trim()) || (effectiveDataset && effectiveDataset.trim()));
  const normalizedRequestedDataset = requestedDataset?.trim() || null;
  const normalizedEffectiveDataset = effectiveDataset?.trim() || null;
  const datasetValue = normalizedRequestedDataset && normalizedEffectiveDataset
    ? `${normalizedRequestedDataset} → ${normalizedEffectiveDataset}`
    : (normalizedEffectiveDataset ?? normalizedRequestedDataset);
  const effectiveLabel = effectivePeriodLabel?.trim() || null;
  const showFallbackBanner = Boolean(usedFallback);
  const showGatedBanner = recommendationAllowed === false && !showFallbackBanner;

  return (
    <section className="analytics-trust-header" aria-label="Kontekst pouzdanosti analitike">
      <div className="ath-main">
        <div className="ath-main-copy">
          <p className="ath-overline">{MODE_LABELS[mode]}</p>
          <h1 className="ath-title">{title}</h1>
          <p className="ath-description">{description}</p>
          {refreshIsRunning ? (
            <p className="ath-live">Osvezavanje je u toku{refreshCurrentStep ? ` (${refreshCurrentStep})` : ""}</p>
          ) : null}
        </div>
        <div className={`ath-status ath-status-${tone}`}>
          <span className="ath-status-label">{statusLabel}</span>
        </div>
      </div>

      <div className="ath-meta-grid">
        <div className="ath-meta-item">
          <span className="ath-meta-key">Period</span>
          <strong className="ath-meta-value">
            {hasPeriod ? `${formatDate(periodFrom)} - ${formatDate(periodTo)}` : "Period nije definisan"}
          </strong>
        </div>
        <div className="ath-meta-item">
          <span className="ath-meta-key">Poslednje osvezenje</span>
          <strong className="ath-meta-value">
            {lastRefreshAt ? formatDateTime(lastRefreshAt) : "Vreme osvezenja nije dostupno"}
          </strong>
          <span className={`ath-freshness-badge ath-freshness-${freshness}`}>
            {FRESHNESS_LABELS[freshness]}
          </span>
        </div>
        <div className="ath-meta-item">
          <span className="ath-meta-key">Izvor podataka</span>
          <strong className="ath-meta-value">
            {dataSource?.trim() || "Izvor podataka nije naveden"}
          </strong>
        </div>
        {hasDataset ? (
          <div className="ath-meta-item">
            <span className="ath-meta-key">Dataset</span>
            <strong className="ath-meta-value">{datasetValue ?? "-"}</strong>
            {effectiveLabel ? <span className="ath-meta-subtle">{effectiveLabel}</span> : null}
          </div>
        ) : null}
      </div>

      {showFallbackBanner ? (
        <div className="ath-banner ath-banner-warning" role="note">
          <strong>Fallback aktiviran.</strong>{" "}
          Za trazeni period nema dovoljno podataka. Koriscen je dataset {effectiveLabel ?? normalizedEffectiveDataset ?? "n/a"} kao pomocni signal.
          {fallbackReason ? ` ${fallbackReason}` : null}
          {fallbackReasonCode ? <span className="ath-banner-code"> ({fallbackReasonCode})</span> : null}
        </div>
      ) : null}

      {showGatedBanner ? (
        <div className="ath-banner ath-banner-neutral" role="note">
          <strong>Preporuka je gated.</strong> Sistem ne prikazuje konacnu preporuku jer nema dovoljno pouzdanih podataka za izabrani period.
        </div>
      ) : null}

      {recommendationNote ? <p className="ath-note">{recommendationNote}</p> : null}
      {emptyStateReason ? <p className="ath-empty-reason">{emptyStateReason}</p> : null}

      <div className="ath-summary">
        <h2>Sazetak kvaliteta podataka</h2>
        {hasSummary ? (
          <div className="ath-summary-grid">
            <div><span>Artikli bez dobavljaca</span><strong>{renderSummaryValue(dataQualitySummary.missingSupplierCount)}</strong></div>
            <div><span>Redovi bez nabavne cene</span><strong>{renderSummaryValue(dataQualitySummary.missingCostCount)}</strong></div>
            <div><span>Artikli bez kategorije</span><strong>{renderSummaryValue(dataQualitySummary.missingCategoryCount)}</strong></div>
            <div><span>Nedovoljni signali</span><strong>{renderSummaryValue(dataQualitySummary.insufficientSignalCount)}</strong></div>
            <div><span>Ignorisani redovi</span><strong>{renderSummaryValue(dataQualitySummary.ignoredRowsCount)}</strong></div>
          </div>
        ) : (
          <p className="ath-summary-missing">Detaljan kvalitet podataka nije dostupan za ovaj ekran.</p>
        )}
      </div>

      {(methodologyHref || dataQualityHref || refreshStatusHref) ? (
        <div className="ath-footer">
          {methodologyHref ? <a href={methodologyHref}>{methodologyLabel ?? "Metodologija i tumacenje signala"}</a> : null}
          {dataQualityHref ? <a href={dataQualityHref}>Kvalitet podataka</a> : null}
          {refreshStatusHref ? <a href={refreshStatusHref}>Status osvezavanja</a> : null}
        </div>
      ) : null}
    </section>
  );
}

export type { AnalyticsTrustHeaderProps };

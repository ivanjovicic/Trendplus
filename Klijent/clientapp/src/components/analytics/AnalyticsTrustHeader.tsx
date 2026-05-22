import { formatDate, formatDateTime } from "../../utils/analyticsFormatters";
import "./AnalyticsTrustHeader.css";

type AnalyticsTrustHeaderProps = {
  title: string;
  description: string;
  periodFrom?: string | null;
  periodTo?: string | null;
  lastRefreshAt?: string | null;
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

function normalizeStatus(value: string | null | undefined): "good" | "warning" | "critical" | "insufficient_data" | null {
  if (!value) return null;
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
  if (status === "insufficient_data") return "neutral";
  return "neutral";
}

function renderSummaryValue(value: number | null | undefined): string {
  if (value == null) return "-";
  return value.toLocaleString("sr-RS");
}

export default function AnalyticsTrustHeader({
  title,
  description,
  periodFrom,
  periodTo,
  lastRefreshAt,
  dataSource,
  dataQualityStatus,
  dataQualitySummary,
  mode,
  recommendationNote,
  emptyStateReason,
  methodologyHref,
}: AnalyticsTrustHeaderProps) {
  const normalizedStatus = normalizeStatus(dataQualityStatus);
  const tone = statusTone(normalizedStatus);
  const statusLabel = normalizedStatus ? STATUS_LABELS[normalizedStatus] : "Status kvaliteta nije dostupan";
  const hasPeriod = Boolean(periodFrom && periodTo);
  const hasSummary = Boolean(dataQualitySummary);

  return (
    <section className="analytics-trust-header" aria-label="Kontekst pouzdanosti analitike">
      <div className="ath-main">
        <div className="ath-main-copy">
          <p className="ath-overline">{MODE_LABELS[mode]}</p>
          <h1 className="ath-title">{title}</h1>
          <p className="ath-description">{description}</p>
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
        </div>
        <div className="ath-meta-item">
          <span className="ath-meta-key">Izvor podataka</span>
          <strong className="ath-meta-value">
            {dataSource?.trim() || "Izvor podataka nije naveden"}
          </strong>
        </div>
      </div>

      {recommendationNote ? (
        <p className="ath-note">{recommendationNote}</p>
      ) : null}

      {emptyStateReason ? (
        <p className="ath-empty-reason">{emptyStateReason}</p>
      ) : null}

      <div className="ath-summary">
        <h2>Sažetak kvaliteta podataka</h2>
        {hasSummary ? (
          <div className="ath-summary-grid">
            <div><span>Artikli bez dobavljaca</span><strong>{renderSummaryValue(dataQualitySummary?.missingSupplierCount)}</strong></div>
            <div><span>Redovi bez nabavne cene</span><strong>{renderSummaryValue(dataQualitySummary?.missingCostCount)}</strong></div>
            <div><span>Artikli bez kategorije</span><strong>{renderSummaryValue(dataQualitySummary?.missingCategoryCount)}</strong></div>
            <div><span>Nedovoljni signali</span><strong>{renderSummaryValue(dataQualitySummary?.insufficientSignalCount)}</strong></div>
            <div><span>Ignorisani redovi</span><strong>{renderSummaryValue(dataQualitySummary?.ignoredRowsCount)}</strong></div>
          </div>
        ) : (
          <p className="ath-summary-missing">Detaljan kvalitet podataka nije dostupan za ovaj ekran.</p>
        )}
      </div>

      {methodologyHref ? (
        <div className="ath-footer">
          <a href={methodologyHref}>Metodologija i tumačenje signala</a>
        </div>
      ) : null}
    </section>
  );
}

export type { AnalyticsTrustHeaderProps };

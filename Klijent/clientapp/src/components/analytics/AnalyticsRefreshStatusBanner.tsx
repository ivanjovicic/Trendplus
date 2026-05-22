import { Link } from "react-router-dom";
import type { AnalyticsRefreshStatus } from "../../types/analytics";
import { formatDateTime } from "../../utils/analyticsFormatters";
import "./AnalyticsRefreshStatusBanner.css";

type AnalyticsRefreshStatusBannerProps = {
  status: AnalyticsRefreshStatus | null;
  loading?: boolean;
  error?: string | null;
  adminHref?: string;
};

function normalizeFreshness(value: string | null | undefined): "fresh" | "stale" | "critical" | "unknown" {
  if (value === "fresh" || value === "stale" || value === "critical") return value;
  return "unknown";
}

function freshnessLabel(value: "fresh" | "stale" | "critical" | "unknown"): string {
  if (value === "fresh") return "Sveze";
  if (value === "stale") return "Zastarelo";
  if (value === "critical") return "Kriticno";
  return "Nepoznato";
}

export default function AnalyticsRefreshStatusBanner({
  status,
  loading = false,
  error,
  adminHref = "/admin/configuration?panel=workers",
}: AnalyticsRefreshStatusBannerProps) {
  const freshness = normalizeFreshness(status?.dataFreshnessStatus);

  if (loading && !status) {
    return (
      <section className="analytics-refresh-banner" aria-live="polite">
        <span className="arb-loading">Ucitavam status osvezavanja analitike...</span>
      </section>
    );
  }

  if (!status) {
    return (
      <section className="analytics-refresh-banner analytics-refresh-banner-unknown" aria-live="polite">
        <div className="arb-main">
          <strong>Status osvezavanja nije dostupan.</strong>
          {error ? <span>{error}</span> : null}
        </div>
        <Link to={adminHref} className="arb-link">Otvori worker panel</Link>
      </section>
    );
  }

  const failedJobs = status.jobs.filter((job) => normalizeFreshness(job.dataFreshnessStatus) === "critical");

  return (
    <section className={`analytics-refresh-banner analytics-refresh-banner-${freshness}`} aria-live="polite">
      <div className="arb-main">
        <div className="arb-row">
          <strong>Poslednji uspesan refresh:</strong>
          <span>{status.lastSuccessfulRefreshAtUtc ? formatDateTime(status.lastSuccessfulRefreshAtUtc) : "Nije zabelezen"}</span>
          <span className={`arb-badge arb-badge-${freshness}`}>{freshnessLabel(freshness)}</span>
        </div>
        <div className="arb-row">
          <strong>Poslednji pokusaj:</strong>
          <span>{status.lastAttemptAtUtc ? formatDateTime(status.lastAttemptAtUtc) : "Nema pokusaja u istoriji"}</span>
        </div>
        {status.isRunning ? <div className="arb-row"><strong>Refresh:</strong><span>U toku</span></div> : null}
        {status.lastFailureAtUtc ? (
          <div className="arb-row">
            <strong>Poslednji pad:</strong>
            <span>{formatDateTime(status.lastFailureAtUtc)}</span>
          </div>
        ) : null}
        {status.lastErrorMessage ? (
          <div className="arb-row arb-error">
            <strong>Greska:</strong>
            <span>{status.lastErrorMessage}</span>
          </div>
        ) : null}
        {status.workerProcessWarning ? (
          <div className="arb-row arb-warning">
            <strong>Napomena:</strong>
            <span>{status.workerProcessWarning}</span>
          </div>
        ) : null}
        {failedJobs.length > 0 ? (
          <div className="arb-row arb-error">
            <strong>Poslovi sa greskom:</strong>
            <span>{failedJobs.map((job) => job.displayName).join(", ")}</span>
          </div>
        ) : null}
        {error ? <div className="arb-row arb-warning"><strong>Upozorenje:</strong><span>{error}</span></div> : null}
      </div>
      <Link to={adminHref} className="arb-link">Otvori worker panel</Link>
    </section>
  );
}

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

  const processMode = (status.processMode || status.processType || "unknown").toLowerCase();
  const workerWarning = status.workerWarning ?? status.workerProcessWarning;
  const failedJobs = status.jobs.filter((job) => normalizeFreshness(job.dataFreshnessStatus) === "critical");
  const refreshedObjects = status.refreshedObjects ?? [];
  const failedObjects = status.failedObjects ?? [];

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
        <div className="arb-row">
          <strong>Proces:</strong>
          <span>{processMode}</span>
        </div>
        {status.isRunning ? (
          <div className="arb-row">
            <strong>Refresh:</strong>
            <span>Osvezavanje u toku{status.currentStep ? ` (${status.currentStep})` : ""}</span>
          </div>
        ) : null}
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
        {status.durationSeconds != null ? (
          <div className="arb-row">
            <strong>Trajanje:</strong>
            <span>{Math.round(status.durationSeconds)} s</span>
          </div>
        ) : null}
        {refreshedObjects.length > 0 ? (
          <div className="arb-row">
            <strong>Osvezeni objekti:</strong>
            <span>{refreshedObjects.join(", ")}</span>
          </div>
        ) : null}
        {failedObjects.length > 0 ? (
          <div className="arb-row arb-error">
            <strong>Neuspesni objekti:</strong>
            <span>{failedObjects.join(", ")}</span>
          </div>
        ) : null}
        {workerWarning ? (
          <div className="arb-row arb-warning">
            <strong>Napomena:</strong>
            <span>{workerWarning}</span>
          </div>
        ) : null}
        {!workerWarning && processMode === "web" && status.workersEnabled ? (
          <div className="arb-row arb-warning">
            <strong>Upozorenje:</strong>
            <span>Automatsko osvezavanje nije aktivno u web procesu. Potrebna je deployacija radnika (worker).</span>
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

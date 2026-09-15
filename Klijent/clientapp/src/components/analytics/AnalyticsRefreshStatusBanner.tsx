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
  const normalized = typeof value === "string" ? value.trim().toLowerCase() : null;
  if (normalized === "fresh" || normalized === "stale" || normalized === "critical") return normalized;
  return "unknown";
}

const PROCESS_MODE_LABELS: Record<string, string> = {
  web: "Web proces",
  worker: "Radni proces",
};

const REFRESH_STEP_LABELS: Record<string, string> = {
  sales_facts_refresh: "osvežavanje prodajnih činjenica",
  product_dim_refresh: "osvežavanje proizvoda",
  supplier_decision_mvs: "osvežavanje signala dobavljača",
  product_decision_snapshot: "osvežavanje odluka za proizvode",
  inventory_recommendations: "osvežavanje preporuka zaliha",
  data_quality_snapshot: "osvežavanje kvaliteta podataka",
};

const ANALYTICS_OBJECT_LABELS: Record<string, string> = {
  sales_facts_mv: "prodajne činjenice",
  product_dim_mv: "proizvodi",
  supplier_decision_mv: "signali dobavljača",
  mv_supplier_decision_score_cache: "signali dobavljača",
  mv_supplier_decision_score_cache_90d: "signali dobavljača",
  mv_supplier_decision_score_cache_180d: "signali dobavljača",
  mv_product_decision_snapshot: "odluke za proizvode",
  mv_inventory_recommendations: "preporuke zaliha",
  analytics_data_quality_history: "kvalitet podataka",
};

function normalizeToken(value: string | null | undefined): string | null {
  const normalized = typeof value === "string" ? value.trim().toLowerCase().replace(/[\s-]+/g, "_") : null;
  return normalized || null;
}

function processModeLabel(value: string | null | undefined): string {
  const key = normalizeToken(value);
  return (key && PROCESS_MODE_LABELS[key]) || "Nepoznat proces";
}

function refreshStepLabel(value: string | null | undefined): string | null {
  const key = normalizeToken(value);
  return key ? REFRESH_STEP_LABELS[key] || "Obrada podataka" : null;
}

function analyticsObjectLabel(value: string): string {
  const key = normalizeToken(value);
  return (key && ANALYTICS_OBJECT_LABELS[key]) || "objekat analitike";
}

function analyticsJobLabel(value: string | null | undefined): string {
  const key = normalizeToken(value);
  return (key && REFRESH_STEP_LABELS[key]) || "posao analitike";
}

function safeWorkerWarning(value: string | null | undefined): string | null {
  const normalized = typeof value === "string" ? value.trim().toLowerCase() : null;
  if (!normalized) return null;
  if (normalized.includes("worker nije aktivan")) return "Worker nije aktivan u ovom procesu. Automatsko osvežavanje nije aktivno.";
  if (normalized.includes("worker nije registrovan")) return "Automatsko osvežavanje radnika nije aktivno u ovom procesu.";
  return "Automatsko osvežavanje nije potvrđeno. Proverite worker panel.";
}

function normalizeDurationSeconds(value: number | null | undefined): number | null {
  return typeof value === "number" && Number.isFinite(value) && value >= 0 ? value : null;
}

function freshnessLabel(value: "fresh" | "stale" | "critical" | "unknown"): string {
  if (value === "fresh") return "Sveže";
  if (value === "stale") return "Zastarelo";
  if (value === "critical") return "Kritično";
  return "Nepoznato";
}

export default function AnalyticsRefreshStatusBanner({
  status,
  loading = false,
  error,
  adminHref = "/admin/configuration?panel=workers",
}: AnalyticsRefreshStatusBannerProps) {
  const freshness = normalizeFreshness(status?.dataFreshnessStatus);
  const recentRuns = Array.isArray(status?.recentRuns) ? status.recentRuns : [];
  const latestCorrelationId = typeof recentRuns[0]?.correlationId === "string"
    ? recentRuns[0].correlationId.trim() || null
    : null;
  const shouldShowCorrelationId = Boolean(
    latestCorrelationId && (error || status?.lastErrorMessage || freshness === "stale" || freshness === "critical")
  );

  if (loading && !status) {
    return (
      <section className="analytics-refresh-banner" aria-live="polite">
        <span className="arb-loading">Učitavam status osvežavanja analitike...</span>
      </section>
    );
  }

  if (!status) {
    return (
      <section className="analytics-refresh-banner analytics-refresh-banner-unknown" aria-live="polite">
        <div className="arb-main">
          <strong>Status osvežavanja nije dostupan.</strong>
          {error ? <span>Detalji greške nisu dostupni. Proverite worker panel.</span> : null}
        </div>
        <Link to={adminHref} className="arb-link">Otvori worker panel</Link>
      </section>
    );
  }

  const processModeKey = normalizeToken(status.processMode || status.processType);
  const processMode = processModeLabel(status.processMode || status.processType);
  const currentStep = refreshStepLabel(status.currentStep);
  const workerWarning = safeWorkerWarning(status.workerWarning ?? status.workerProcessWarning);
  const hasPartialPayload = !Array.isArray(status.jobs)
    || !Array.isArray(status.refreshedObjects)
    || !Array.isArray(status.failedObjects);
  const jobs = Array.isArray(status.jobs) ? status.jobs : [];
  const failedJobs = jobs.filter((job) => job && normalizeFreshness(job.dataFreshnessStatus) === "critical");
  const refreshedObjects = Array.isArray(status.refreshedObjects) ? status.refreshedObjects : [];
  const failedObjects = Array.isArray(status.failedObjects) ? status.failedObjects : [];
  const displayedFreshness = hasPartialPayload && freshness === "fresh" ? "unknown" : freshness;
  const showCriticalCopy = displayedFreshness === "critical";
  const durationSeconds = normalizeDurationSeconds(status.durationSeconds);
  const hasRecordedAttempt = Boolean(status.lastAttemptAtUtc || status.lastSuccessfulRefreshAtUtc || recentRuns.length);

  return (
    <section className={`analytics-refresh-banner analytics-refresh-banner-${displayedFreshness}`} aria-live="polite">
      <div className="arb-main">
        <div className="arb-row">
          <strong>Poslednji uspešan refresh:</strong>
          <span>{status.lastSuccessfulRefreshAtUtc ? formatDateTime(status.lastSuccessfulRefreshAtUtc) : "Nije zabeležen"}</span>
          <span className={`arb-badge arb-badge-${displayedFreshness}`}>{freshnessLabel(displayedFreshness)}</span>
        </div>
        {showCriticalCopy ? (
          <div className="arb-row arb-error">
            <strong>Upozorenje:</strong>
            <span>Podaci su kritično zastareli. Ne preporučuje se donošenje odluka bez provere osvežavanja.</span>
          </div>
        ) : null}
        <div className="arb-row">
          <strong>Poslednji pokušaj:</strong>
          <span>{status.lastAttemptAtUtc ? formatDateTime(status.lastAttemptAtUtc) : "Nema pokušaja u istoriji"}</span>
        </div>
        <div className="arb-row">
          <strong>Proces:</strong>
          <span>{processMode}</span>
        </div>
        {status.isRunning ? (
          <div className="arb-row">
            <strong>Refresh:</strong>
            <span>Osvežavanje u toku{currentStep ? ` (${currentStep})` : ""}</span>
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
            <strong>Greška:</strong>
            <span>Osvežavanje nije uspešno završeno.</span>
          </div>
        ) : null}
        {shouldShowCorrelationId ? (
          <div className="arb-row">
            <strong>Correlation ID:</strong>
            <span>{latestCorrelationId}</span>
          </div>
        ) : null}
        {durationSeconds != null && hasRecordedAttempt && !status.isRunning ? (
          <div className="arb-row">
            <strong>Trajanje:</strong>
            <span>{Math.round(durationSeconds)} s</span>
          </div>
        ) : null}
        {refreshedObjects.length > 0 ? (
          <div className="arb-row">
            <strong>Osveženi objekti:</strong>
            <span>{refreshedObjects.map(analyticsObjectLabel).join(", ")}</span>
          </div>
        ) : null}
        {failedObjects.length > 0 ? (
          <div className="arb-row arb-error">
            <strong>Neuspešni objekti:</strong>
            <span>{failedObjects.map(analyticsObjectLabel).join(", ")}</span>
          </div>
        ) : null}
        {workerWarning ? (
          <div className="arb-row arb-warning">
            <strong>Napomena:</strong>
            <span>{workerWarning}</span>
          </div>
        ) : null}
        {!workerWarning && processModeKey === "web" && status.workersEnabled ? (
          <div className="arb-row arb-warning">
            <strong>Upozorenje:</strong>
            <span>Automatsko osvežavanje nije aktivno u web procesu. Potrebna je deployacija radnika (worker).</span>
          </div>
        ) : null}
        {failedJobs.length > 0 ? (
          <div className="arb-row arb-error">
            <strong>Poslovi sa greškom:</strong>
            <span>{failedJobs.map((job) => analyticsJobLabel(job.key)).join(", ")}</span>
          </div>
        ) : null}
        {error ? <div className="arb-row arb-warning"><strong>Upozorenje:</strong><span>Osvežavanje statusa nije moguće potvrditi. Proverite worker panel.</span></div> : null}
      </div>
      <Link to={adminHref} className="arb-link">Otvori worker panel</Link>
    </section>
  );
}



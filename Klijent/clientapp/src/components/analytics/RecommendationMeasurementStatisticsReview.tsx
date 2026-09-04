import { useMemo, useState } from "react";
import AnalyticsEmptyState from "./AnalyticsEmptyState";
import AnalyticsErrorState from "./AnalyticsErrorState";
import AnalyticsTrustHeader from "./AnalyticsTrustHeader";
import { fmtNumber, formatDateTime } from "../../utils/analyticsFormatters";
import {
  buildMeasurementStatisticsExportCsv,
  canExportMeasurementStatistics,
  downloadMeasurementStatisticsCsv,
  formatMeasurementRate,
  formatMeasurementWarning,
  resolveMeasurementStatisticsView,
} from "../../utils/recommendationMeasurementStatistics";
import type { AnalyticsActionOutcomeSummaryResponse } from "../../types/analytics";
import "./RecommendationMeasurementStatisticsReview.css";

type RecommendationMeasurementStatisticsReviewProps = {
  loading: boolean;
  loadError?: string | null;
  summary: AnalyticsActionOutcomeSummaryResponse | null;
  onRetry?: () => void;
};

function periodBounds(summary: AnalyticsActionOutcomeSummaryResponse | null): {
  from?: string | null;
  to?: string | null;
} {
  if (!summary) {
    return {};
  }

  return {
    from: summary.meta.createdFrom ?? summary.meta.resolvedFrom ?? summary.meta.measuredFrom,
    to: summary.meta.createdTo ?? summary.meta.resolvedTo ?? summary.meta.measuredTo,
  };
}

export default function RecommendationMeasurementStatisticsReview({
  loading,
  loadError,
  summary,
  onRetry,
}: RecommendationMeasurementStatisticsReviewProps) {
  const view = useMemo(
    () => resolveMeasurementStatisticsView({ loading, loadError, summary }),
    [loading, loadError, summary],
  );
  const [exportWarning, setExportWarning] = useState<string | null>(null);
  const period = periodBounds(summary);
  const warningCodes = view.kind === "ready"
    ? Array.from(new Set([...(view.stats.warningCodes ?? []), ...(summary?.meta.warnings ?? [])]))
    : [];

  function handleExport() {
    setExportWarning(null);
    if (!canExportMeasurementStatistics(view)) {
      setExportWarning("Izvoz nije sačuvan jer statistika merenja nije spremna. Pregled na ekranu ostaje nepromenjen.");
      return;
    }

    try {
      downloadMeasurementStatisticsCsv(
        "recommendation-measurement-statistics.csv",
        buildMeasurementStatisticsExportCsv(view.stats),
      );
    } catch {
      setExportWarning("Izvoz CSV fajla nije uspeo. Pregled na ekranu ostaje nepromenjen.");
    }
  }

  function handlePrint() {
    setExportWarning(null);
    try {
      window.print();
    } catch {
      setExportWarning("Štampa nije uspela. Pregled na ekranu ostaje nepromenjen.");
    }
  }

  return (
    <section
      className="rms-panel"
      data-testid="measurement-statistics-review"
      aria-labelledby="rms-title"
    >
      <h2 id="rms-title" className="rms-section-title">Statistika merenja preporuka</h2>
      <p className="rms-note">
        Funnel, pokrivenost merenjem i raspodela ishoda dolaze samo iz polja measurementStatistics.
        Prihvatanje i izvršenje nisu uspeh.
      </p>

      {view.kind === "loading" ? (
        <div className="rms-loading">Učitavanje statistike merenja...</div>
      ) : null}

      {view.kind === "error" ? (
        <AnalyticsErrorState
          title="Statistika merenja nije dostupna"
          message={view.message}
          errorCode={view.code}
          onRetry={onRetry}
          helpHref="/analytics/data-quality"
          helpLabel="Otvori kvalitet podataka"
        />
      ) : null}

      {view.kind !== "loading" ? (
        <div className="rms-header-actions">
          <button type="button" onClick={handleExport}>
            Izvezi CSV
          </button>
          <button type="button" onClick={handlePrint}>
            Štampaj
          </button>
        </div>
      ) : null}

      {view.kind === "empty" ? (
        <AnalyticsEmptyState
          variant="no_data"
          title="Nema izdatih preporuka za izabrani period."
          message="Prazan uzorak nije greška i ne prikazuje stope kao 0%."
          emptyReason={view.emptyReason}
          onRetry={onRetry}
        />
      ) : null}

      {view.kind === "ready" && summary ? (
        <>
          <AnalyticsTrustHeader
            title="Pregled statistike merenja"
            description={`Izdato: ${fmtNumber(view.stats.issuedCount, 0, "0")}. Obim toka (kreirano): ${fmtNumber(summary.meta.sampleSize, 0, "0")}. Period: ${summary.meta.periodMode}.`}
            periodFrom={period.from}
            periodTo={period.to}
            lastRefreshAt={null}
            dataSource="GET /api/analytics/actions/outcomes/summary measurementStatistics"
            mode="report"
            emptyStateReason={view.stats.emptyReason ?? summary.meta.emptyReason ?? null}
            dataQualityHref="/analytics/data-quality"
            refreshStatusHref="/admin/configuration?panel=workers"
            compact
          />

          {warningCodes.length > 0 ? (
            <div className="rms-warnings" aria-label="Upozorenja statistike merenja">
              {warningCodes.map((code) => (
                <span key={code} className="rms-warning-chip">
                  {formatMeasurementWarning(code)}
                </span>
              ))}
            </div>
          ) : null}

          <h3 className="rms-section-title">Životni ciklus</h3>
          <p className="rms-note">Prihvaćeno i izvršeno nisu uspeh preporuke.</p>
          <div className="rms-grid">
            <div className="rms-card">
              <span className="rms-card-label">Izdato</span>
              <strong className="rms-card-value">{fmtNumber(view.stats.issuedCount, 0, "0")}</strong>
            </div>
            <div className="rms-card">
              <span className="rms-card-label">Prihvaćeno</span>
              <strong className="rms-card-value">{fmtNumber(view.stats.acceptedCount, 0, "0")}</strong>
              <span className="rms-card-note">Stopa prihvatanja {formatMeasurementRate(view.stats.acceptanceRate)} · nije uspeh</span>
            </div>
            <div className="rms-card">
              <span className="rms-card-label">Odbijeno</span>
              <strong className="rms-card-value">{fmtNumber(view.stats.rejectedCount, 0, "0")}</strong>
              <span className="rms-card-note">Stopa odbijanja {formatMeasurementRate(view.stats.rejectionRate)}</span>
            </div>
            <div className="rms-card">
              <span className="rms-card-label">Ignorisano</span>
              <strong className="rms-card-value">{fmtNumber(view.stats.ignoredCount, 0, "0")}</strong>
              <span className="rms-card-note">Stopa ignorisanja {formatMeasurementRate(view.stats.ignoredRate)}</span>
            </div>
            <div className="rms-card">
              <span className="rms-card-label">Izvršeno</span>
              <strong className="rms-card-value">{fmtNumber(view.stats.executedCount, 0, "0")}</strong>
              <span className="rms-card-note">Stopa izvršenja {formatMeasurementRate(view.stats.executionRate)} · nije uspeh</span>
            </div>
          </div>

          <h3 className="rms-section-title">Pokrivenost merenjem</h3>
          <div className="rms-grid">
            <div className="rms-card">
              <span className="rms-card-label">Izmereno</span>
              <strong className="rms-card-value">{fmtNumber(view.stats.measuredCount, 0, "0")}</strong>
            </div>
            <div className="rms-card">
              <span className="rms-card-label">Nije izmereno</span>
              <strong className="rms-card-value">{fmtNumber(view.stats.notMeasuredCount, 0, "0")}</strong>
              <span className="rms-card-note">Udeo bez merenja {formatMeasurementRate(view.stats.notMeasuredShare)}</span>
            </div>
            <div className="rms-card">
              <span className="rms-card-label">Čeka merenje</span>
              <strong className="rms-card-value">{fmtNumber(view.stats.pendingCount, 0, "0")}</strong>
            </div>
            <div className="rms-card" data-testid="rms-coverage-rate">
              <span className="rms-card-label">Pokrivenost merenjem</span>
              <strong className="rms-card-value">{formatMeasurementRate(view.stats.measurementCoverageRate)}</strong>
              <span className="rms-card-note">Izmereno / izvršeno</span>
            </div>
          </div>

          <h3 className="rms-section-title">Raspodela ishoda</h3>
          <p className="rms-note">Stopa pozitivnih ishoda meri se samo nad izmerenim redovima, ne nad izdatim, prihvaćenim ili izvršenim.</p>
          <div className="rms-grid">
            <div className="rms-card">
              <span className="rms-card-label">Pozitivan ishod</span>
              <strong className="rms-card-value">{fmtNumber(view.stats.successCount, 0, "0")}</strong>
              <span className="rms-card-note">Stopa pozitivnih ishoda {formatMeasurementRate(view.stats.positiveOutcomeRate)}</span>
            </div>
            <div className="rms-card">
              <span className="rms-card-label">Neutralan ishod</span>
              <strong className="rms-card-value">{fmtNumber(view.stats.neutralCount, 0, "0")}</strong>
              <span className="rms-card-note">Stopa neutralnih ishoda {formatMeasurementRate(view.stats.neutralOutcomeRate)}</span>
            </div>
            <div className="rms-card">
              <span className="rms-card-label">Negativan ishod</span>
              <strong className="rms-card-value">{fmtNumber(view.stats.negativeCount, 0, "0")}</strong>
              <span className="rms-card-note">Stopa negativnih ishoda {formatMeasurementRate(view.stats.negativeOutcomeRate)}</span>
            </div>
          </div>

          <div className="rms-volume" aria-label="Obim toka">
            <span>Obim toka, nije uspeh.</span>
            <span>Kreirano: {fmtNumber(summary.totals.createdCount, 0, "0")}</span>
            <span>Zatvoreno: {fmtNumber(summary.totals.closedCount, 0, "0")}</span>
            <span>Otvoreno: {fmtNumber(summary.totals.openCount, 0, "0")}</span>
            <span>Poslednji uspešan refresh: nije potvrđen za ovaj izvor</span>
          </div>
        </>
      ) : null}

      {exportWarning ? (
        <p className="rms-export-warning" role="status">{exportWarning}</p>
      ) : null}
    </section>
  );
}

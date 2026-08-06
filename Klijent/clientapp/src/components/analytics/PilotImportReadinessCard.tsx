import { Link } from "react-router-dom";
import type { AnalyticsRefreshStatus, PilotDataQualityIntakeReport } from "../../types/analytics";
import { formatDate, formatDateTime, fmtNumber } from "../../utils/analyticsFormatters";
import { computePilotImportReadiness } from "../../utils/pilotImportReadiness";
import "./PilotDataQualityIntakeReport.css";

type Props = {
  report: PilotDataQualityIntakeReport | null;
  refreshStatus?: AnalyticsRefreshStatus | null;
  dataQualityHref?: string;
  refreshStatusHref?: string;
  importHref?: string;
};

function renderListItem(value: string, index: number) {
  return <li key={`${index}-${value}`}>{value}</li>;
}

export default function PilotImportReadinessCard({
  report,
  refreshStatus,
  dataQualityHref = "/analytics/data-quality",
  refreshStatusHref = "/admin/configuration?panel=workers",
  importHref = "/access-import",
}: Props) {
  const readiness = computePilotImportReadiness(report, refreshStatus);

  return (
    <section className={`pilot-card pilot-import-readiness pilot-import-readiness-${readiness.status}`}>
      <div className="pilot-import-readiness-header">
        <div>
          <h3>Status pilota</h3>
          <p className="pilot-card-note">{readiness.summary}</p>
        </div>
        <div className={`pilot-import-readiness-status pilot-import-readiness-status-${readiness.status}`}>
          {readiness.label}
        </div>
      </div>

      <div className="pilot-import-readiness-grid">
        <section className="pilot-import-readiness-section">
          <h4>Ulazni signali</h4>
          {report ? (
            <ul>
              <li>Skor spremnosti: {fmtNumber(report.readinessScore, 0, "-")}/100</li>
              <li>Oznaka spremnosti: {report.readinessLabel}</li>
              <li>Artikli: {fmtNumber(report.loadedData.articlesCount, 0, "-")}</li>
              <li>Stavke prodaje: {fmtNumber(report.loadedData.saleItemsCount, 0, "-")}</li>
              <li>Računi: {fmtNumber(report.loadedData.receiptsCount, 0, "-")}</li>
              <li>Dobavljači: {fmtNumber(report.loadedData.suppliersCount, 0, "-")}</li>
              <li>Prva prodaja: {formatDate(report.loadedData.firstSaleDate, "-")}</li>
              <li>Poslednja prodaja: {formatDate(report.loadedData.lastSaleDate, "-")}</li>
              <li>Poslednji import: {formatDateTime(report.lastImportAtUtc, "-")}</li>
              <li>Status importa: {report.lastImportStatus?.trim() || "unknown"}</li>
              <li>Scope importa: {report.lastImportScope?.trim() || "-"}</li>
              <li>Poslednje osveženje: {formatDateTime(report.lastRefreshAtUtc, "-")}</li>
              <li>Status osvežavanja: {refreshStatus?.dataFreshnessStatus ?? "-"}</li>
            </ul>
          ) : (
            <p className="pilot-card-note">Pilot intake report još nije dostupan.</p>
          )}
        </section>

        <section className="pilot-import-readiness-section">
          <h4>Razlozi</h4>
          {readiness.reasons.length > 0 ? (
            <ul>
              {readiness.reasons.map(renderListItem)}
            </ul>
          ) : (
            <p className="pilot-card-note">Nema otvorenih razloga koji bi degradirali readiness.</p>
          )}
        </section>

        <section className="pilot-import-readiness-section">
          <h4>Sledeći koraci</h4>
          {readiness.nextActions.length > 0 ? (
            <ul>
              {readiness.nextActions.map(renderListItem)}
            </ul>
          ) : (
            <p className="pilot-card-note">Nema dodatnih koraka za ovaj kontekst.</p>
          )}
        </section>
      </div>

      <div className="pilot-import-readiness-links no-print">
        <Link className="pilot-intake-action-link" to={dataQualityHref}>Otvori kvalitet podataka</Link>
        <Link className="pilot-intake-action-link" to={refreshStatusHref}>Status osvežavanja</Link>
        <Link className="pilot-intake-action-link" to={importHref}>Otvori import</Link>
      </div>
    </section>
  );
}

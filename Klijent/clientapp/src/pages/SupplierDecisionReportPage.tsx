import { useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import SupplierDecisionReport from "../components/analytics/SupplierDecisionReport";
import SupplierDecisionReportActions from "../components/analytics/SupplierDecisionReportActions";
import AnalyticsEmptyState from "../components/analytics/AnalyticsEmptyState";
import AnalyticsErrorState from "../components/analytics/AnalyticsErrorState";
import { getPrintPayload } from "../services/analyticsTableState";
import "./SupplierDecisionReportPage.css";

export default function SupplierDecisionReportPage() {
  // TODO(report-api): Replace temporary stateKey/localStorage preview flow with
  // GET /api/analytics/suppliers/report?... endpoint for shareable, durable URLs.
  const [searchParams] = useSearchParams();
  const stateKey = searchParams.get("stateKey");
  const [exportError, setExportError] = useState<string | null>(null);

  const payload = useMemo(() => getPrintPayload(stateKey), [stateKey]);

  if (!payload) {
    return (
      <div className="supplier-decision-report-page">
        <AnalyticsEmptyState
          title="Report nije pronađen"
          message="Privremeni podaci su istekli (preview traje oko 10 minuta). Pregled izveštaja je privremen. Za trajni izvoz koristite PDF/Excel."
          actions={[
            { label: "Vrati se na dobavljače", href: "/analytics/supplier" },
            { label: "Ponovo generiši report", href: "/analytics/supplier" },
            { label: "Otvori scorecard", href: "/analytics/supplier?tab=scorecard" },
          ]}
          refreshStatusHref="/admin/configuration?panel=workers"
          dataQualityHref="/analytics/data-quality"
          variant="filtered_out"
        />
      </div>
    );
  }

  return (
    <div className="supplier-decision-report-page">
      {exportError ? (
        <AnalyticsErrorState
          title="Izvoz izveštaja nije uspeo"
          message={exportError}
          suggestions={[
            "Proverite refresh status.",
            "Proverite kvalitet podataka.",
            "Pokušajte ponovo.",
          ]}
          helpHref="/analytics/data-quality"
        />
      ) : null}

      <header className="sdrp-head no-print">
        <div>
          <h1>Trendplus izveštaj dobavljača</h1>
          <p>Pregled izveštaja u HTML formi (print-friendly). Preview se čuva privremeno (oko 10 minuta).</p>
        </div>
        <div className="sdrp-actions">
          <Link to="/analytics/supplier" className="sdrp-back">Nazad</Link>
          <SupplierDecisionReportActions payload={payload} onError={setExportError} />
          <button type="button" className="sdrp-print" onClick={() => window.print()}>Štampaj (browser)</button>
        </div>
      </header>

      <SupplierDecisionReport payload={payload} />
    </div>
  );
}


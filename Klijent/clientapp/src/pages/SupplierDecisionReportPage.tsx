import { useMemo } from "react";
import { Link, useSearchParams } from "react-router-dom";
import SupplierDecisionReport from "../components/analytics/SupplierDecisionReport";
import SupplierDecisionReportActions from "../components/analytics/SupplierDecisionReportActions";
import AnalyticsEmptyState from "../components/analytics/AnalyticsEmptyState";
import { getPrintPayload } from "../services/analyticsTableState";
import "./SupplierDecisionReportPage.css";

export default function SupplierDecisionReportPage() {
  const [searchParams] = useSearchParams();
  const stateKey = searchParams.get("stateKey");

  const payload = useMemo(() => getPrintPayload(stateKey), [stateKey]);

  if (!payload) {
    return (
      <div className="supplier-decision-report-page">
        <AnalyticsEmptyState
          title="Report preview nije dostupan"
          message="Report preview se cuva kratko (oko 15 minuta) i vezan je za poslednji generisani report."
          actions={[
            { label: "Vrati se na dobavljace", href: "/analytics/supplier" },
          ]}
          dataQualityHref="/analytics/data-quality"
          variant="filtered_out"
        />
      </div>
    );
  }

  return (
    <div className="supplier-decision-report-page">
      <header className="sdrp-head">
        <div>
          <h1>Supplier Decision Report</h1>
          <p>Pregled izvestaja u HTML formi (print-friendly).</p>
        </div>
        <div className="sdrp-actions">
          <Link to="/analytics/supplier" className="sdrp-back">Nazad</Link>
          <SupplierDecisionReportActions payload={payload} />
          <button type="button" className="sdrp-print" onClick={() => window.print()}>Stampaj (browser)</button>
        </div>
      </header>

      <SupplierDecisionReport payload={payload} />
    </div>
  );
}


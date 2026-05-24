import { useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import SupplierDecisionReport from "../components/analytics/SupplierDecisionReport";
import SupplierDecisionReportActions from "../components/analytics/SupplierDecisionReportActions";
import AnalyticsEmptyState from "../components/analytics/AnalyticsEmptyState";
import AnalyticsErrorState from "../components/analytics/AnalyticsErrorState";
import { getPrintPayload } from "../services/analyticsTableState";
import { getSupplierDecisionDurableReport } from "../services/analyticsApi";
import { resolveAnalyticsTablePayload } from "../services/analyticsTableState";
import type { ResolvedAnalyticsTablePayload } from "../types/analyticsTable";
import "./SupplierDecisionReportPage.css";

function normalizeColumnType(value: string | undefined) {
  return value === "number"
    || value === "currency"
    || value === "percent"
    || value === "date"
    || value === "datetime"
    || value === "text"
    ? value
    : "text";
}

export default function SupplierDecisionReportPage() {
  const [searchParams] = useSearchParams();
  const stateKey = searchParams.get("stateKey");
  const fromDate = searchParams.get("fromDate");
  const toDate = searchParams.get("toDate");
  const scope = searchParams.get("scope");
  const supplierId = searchParams.get("supplierId");
  const storeId = searchParams.get("storeId");

  const [backendPayload, setBackendPayload] = useState<ResolvedAnalyticsTablePayload | null>(null);
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [exportError, setExportError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    const hasDurableParams = Boolean(fromDate || toDate || scope || supplierId || storeId);

    if (!hasDurableParams) {
      setBackendPayload(null);
      setLoadError(null);
      setLoading(false);
      return;
    }

    setLoading(true);
    setLoadError(null);

    void (async () => {
      try {
        const response = await getSupplierDecisionDurableReport({
          fromDate,
          toDate,
          scope,
          supplierId: supplierId ? Number(supplierId) : null,
          storeId: storeId ? Number(storeId) : null,
        });

        if (cancelled) return;

        const payload = resolveAnalyticsTablePayload({
          tableKey: response.payload.tableKey,
          tableTitle: response.payload.tableTitle,
          documentType: response.payload.documentType,
          templateName: response.payload.templateName,
          locale: response.payload.locale,
          columns: response.payload.columns.map((column) => ({
            ...column,
            dataType: normalizeColumnType(column.dataType),
          })),
          rows: response.payload.rows,
          filters: response.payload.filters,
          metadata: response.payload.metadata,
        });

        setBackendPayload(payload);
      } catch (reason) {
        if (cancelled) return;
        setLoadError(reason instanceof Error ? reason.message : "Report nije dostupan.");
        setBackendPayload(null);
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [fromDate, toDate, scope, supplierId, storeId]);

  const legacyPayload = useMemo(() => getPrintPayload(stateKey), [stateKey]);
  const payload = backendPayload ?? legacyPayload;
  const durableReportHref = useMemo(() => {
    const params = new URLSearchParams();
    if (fromDate) params.set("fromDate", fromDate);
    if (toDate) params.set("toDate", toDate);
    if (scope) params.set("scope", scope);
    if (supplierId) params.set("supplierId", supplierId);
    if (storeId) params.set("storeId", storeId);
    return params.toString() ? `/analytics/supplier/report?${params.toString()}` : null;
  }, [fromDate, scope, storeId, supplierId, toDate]);

  if (loading && !payload) {
    return (
      <div className="supplier-decision-report-page">
        <div className="data-quality-loading">Ucitavam trajni supplier report...</div>
      </div>
    );
  }

  if (loadError && !payload) {
    return (
      <div className="supplier-decision-report-page">
        <AnalyticsErrorState
          title="Report nije dostupan"
          message={loadError}
          suggestions={[
            "Proverite da li je period validan.",
            "Proverite refresh status.",
            "Pokusajte ponovo iz Supplier pregleda.",
          ]}
          helpHref="/analytics/data-quality"
        />
      </div>
    );
  }

  if (!payload) {
    return (
      <div className="supplier-decision-report-page">
        <AnalyticsEmptyState
          title="Report nije pronađen"
          message="Trajni report nije pronadjen za trazeni query, a preview payload je istekao. Ponovo generisite izvestaj iz Supplier pregleda."
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
          <p>Pregled izvestaja u HTML formi (print-friendly). Ako je otvoren sa query parametrima, koristi trajni backend report payload.</p>
        </div>
        <div className="sdrp-actions">
          <Link to="/analytics/supplier" className="sdrp-back">Nazad</Link>
          <SupplierDecisionReportActions payload={payload} durableReportHref={durableReportHref} onError={setExportError} />
          <button type="button" className="sdrp-print" onClick={() => window.print()}>Štampaj (browser)</button>
        </div>
      </header>

      <SupplierDecisionReport payload={payload} />
    </div>
  );
}


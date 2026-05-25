import { useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import SupplierDecisionReport from "../components/analytics/SupplierDecisionReport";
import SupplierDecisionReportActions from "../components/analytics/SupplierDecisionReportActions";
import AnalyticsEmptyState from "../components/analytics/AnalyticsEmptyState";
import AnalyticsErrorState from "../components/analytics/AnalyticsErrorState";
import { getPrintPayload, resolveAnalyticsTablePayload } from "../services/analyticsTableState";
import { AnalyticsMetaError, getSupplierDecisionDurableReport } from "../services/analyticsApi";
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

type ReportLoadError = {
  message: string;
  errorCode?: string | null;
  correlationId?: string | null;
};

export default function SupplierDecisionReportPage() {
  const [searchParams] = useSearchParams();
  const stateKey = searchParams.get("stateKey");
  const fromDate = searchParams.get("fromDate");
  const toDate = searchParams.get("toDate");
  const scope = searchParams.get("scope");
  const supplierId = searchParams.get("supplierId");
  const storeId = searchParams.get("storeId");

  const [backendPayload, setBackendPayload] = useState<ResolvedAnalyticsTablePayload | null>(null);
  const [backendError, setBackendError] = useState<ReportLoadError | null>(null);
  const [loading, setLoading] = useState(false);
  const [reloadTick, setReloadTick] = useState(0);
  const [exportError, setExportError] = useState<string | null>(null);

  const hasDurableParams = Boolean(fromDate || toDate || scope || supplierId || storeId);

  useEffect(() => {
    let cancelled = false;

    if (!hasDurableParams) {
      setBackendPayload(null);
      setBackendError(null);
      setLoading(false);
      return;
    }

    setLoading(true);
    setBackendError(null);

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

        if (reason instanceof AnalyticsMetaError) {
          setBackendError({
            message: reason.message,
            errorCode: reason.errorCode,
            correlationId: reason.correlationId,
          });
        } else {
          setBackendError({
            message: reason instanceof Error ? reason.message : "Report trenutno nije dostupan.",
          });
        }

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
  }, [fromDate, hasDurableParams, reloadTick, scope, storeId, supplierId, toDate]);

  const legacyPayload = useMemo(() => getPrintPayload(stateKey), [stateKey]);
  const hasLegacyFallback = Boolean(legacyPayload);
  const showLegacyWarning = Boolean(backendError && hasDurableParams && hasLegacyFallback);
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
        <div className="data-quality-loading">Učitavam trajni izveštaj dobavljača...</div>
      </div>
    );
  }

  if (backendError && !payload) {
    return (
      <div className="supplier-decision-report-page">
        <AnalyticsErrorState
          title="Podaci trenutno nisu dostupni."
          message={backendError.message}
          errorCode={backendError.errorCode}
          correlationId={backendError.correlationId}
          suggestions={[
            "Proverite period.",
            "Proverite refresh status.",
            "Otvorite kvalitet podataka.",
          ]}
          onRetry={() => setReloadTick((prev) => prev + 1)}
          helpHref="/analytics/data-quality"
        />
      </div>
    );
  }

  if (!payload) {
    return (
      <div className="supplier-decision-report-page">
        <AnalyticsEmptyState
          title="Izveštaj nije dostupan"
          message="Pregled izveštaja je istekao jer se čuva privremeno u browseru."
          reasons={[
            "Za trajni dokument koristite PDF/Excel export ili ponovo generišite report iz pregleda dobavljača.",
          ]}
          actions={[
            { label: "Vrati se na dobavljače", href: "/analytics/supplier" },
            { label: "Ponovo generiši report", href: "/analytics/supplier" },
            { label: "Otvori Scorecard", href: "/analytics/supplier?tab=scorecard" },
          ]}
          refreshStatusHref="/admin/configuration?panel=workers"
          dataQualityHref="/analytics/data-quality"
          variant="filtered_out"
        />
        <div className="sdrp-expired-actions sdrp-actions no-print">
          <Link to="/analytics/supplier?tab=scorecard" className="sdrp-back">Otvori Scorecard</Link>
          <button type="button" className="sdrp-print" onClick={() => setReloadTick((prev) => prev + 1)}>Ponovo učitaj</button>
        </div>
        <p className="sdrp-expired-note">
          Za trajni dokument koristite PDF/Excel export ili ponovo generišite report iz pregleda dobavljača.
        </p>
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

      {showLegacyWarning ? (
        <div className="sdrp-warning-banner no-print" role="status">
          Backend report trenutno nije dostupan. Prikazujemo privremeni browser preview.
        </div>
      ) : null}

      <header className="sdrp-head no-print">
        <div>
          <h1>Trendplus izveštaj dobavljača</h1>
          <p>Pregled izveštaja u HTML formi spremnoj za štampu i izvoz. Ako je otvoren sa query parametrima, koristi trajni backend report payload.</p>
        </div>
        <div className="sdrp-actions">
          <Link to="/analytics/supplier" className="sdrp-back">Vrati se na dobavljače</Link>
          <Link to="/analytics/supplier" className="sdrp-back">Ponovo generiši report</Link>
          <Link to="/analytics/supplier?tab=scorecard" className="sdrp-back">Otvori Scorecard</Link>
          <SupplierDecisionReportActions payload={payload} durableReportHref={durableReportHref} onError={setExportError} />
          <button type="button" className="sdrp-print" onClick={() => window.print()}>Štampaj iz pregleda</button>
        </div>
      </header>

      <SupplierDecisionReport payload={payload} />
    </div>
  );
}

import { useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import AnalyticsEmptyState from "../components/analytics/AnalyticsEmptyState";
import AnalyticsErrorState from "../components/analytics/AnalyticsErrorState";
import AnalyticsRefreshStatusBanner from "../components/analytics/AnalyticsRefreshStatusBanner";
import AnalyticsTrustHeader from "../components/analytics/AnalyticsTrustHeader";
import PilotDataQualityIntakeReport from "../components/analytics/PilotDataQualityIntakeReport";
import {
  AnalyticsMetaError,
  getAnalyticsRefreshStatus,
  getPilotIntakeDurableReport,
} from "../services/analyticsApi";
import { getPrintPayload } from "../services/analyticsTableState";
import type {
  AnalyticsRefreshStatus,
  DurableReportRow,
  PilotIntakeDurableReport,
} from "../types/analytics";
import type { ResolvedAnalyticsTablePayload } from "../types/analyticsTable";
import "./PilotIntakeReportPage.css";

type ReportLoadError = {
  message: string;
  errorCode?: string | null;
  correlationId?: string | null;
};

function asString(value: unknown): string {
  if (value == null) return "";
  return String(value);
}

function asBool(value: string | null): boolean | undefined {
  if (!value) return undefined;
  const normalized = value.trim().toLowerCase();
  if (normalized === "true") return true;
  if (normalized === "false") return false;
  return undefined;
}

function mapLegacyPayloadToDurable(
  payload: ResolvedAnalyticsTablePayload,
  stateKey: string | null,
  query: {
    fromDate: string | null;
    toDate: string | null;
    scope: string | null;
    storeId: string | null;
    supplierId: string | null;
  }
): PilotIntakeDurableReport {
  const metadataMap = new Map(
    (payload.metadata ?? []).map((item) => [item.key, item.value == null ? null : String(item.value)])
  );

  const rows: DurableReportRow[] = payload.rows.map((row) => ({
    section: asString(row.section),
    item: asString(row.item),
    value: asString(row.value),
    secondary: asString(row.secondary) || null,
    note: asString(row.note) || null,
  }));

  const sectionCount = new Map<string, number>();
  for (const row of rows) {
    const sectionName = row.section || "Podaci";
    sectionCount.set(sectionName, (sectionCount.get(sectionName) ?? 0) + 1);
  }

  const sections = Array.from(sectionCount.entries()).map(([key, rowCount]) => ({
    key,
    title: key,
    rowCount,
  }));

  const periodFrom = query.fromDate ?? metadataMap.get("periodFromUtc") ?? metadataMap.get("periodFrom") ?? null;
  const periodTo = query.toDate ?? metadataMap.get("periodToUtc") ?? metadataMap.get("periodTo") ?? null;
  const usedFallback = asBool(metadataMap.get("usedFallback") ?? null);
  const recommendationAllowed = asBool(metadataMap.get("recommendationAllowed") ?? null);
  const stableParams = new URLSearchParams();
  if (query.fromDate) stableParams.set("fromDate", query.fromDate);
  if (query.toDate) stableParams.set("toDate", query.toDate);
  if (query.scope) stableParams.set("scope", query.scope);
  if (query.storeId) stableParams.set("storeId", query.storeId);
  if (query.supplierId) stableParams.set("supplierId", query.supplierId);

  return {
    reportId: metadataMap.get("reportId") ?? `pilot-intake-legacy-${stateKey ?? "preview"}`,
    stableQueryUrl: stableParams.toString()
      ? `/analytics/data-quality/pilot-intake-report?${stableParams.toString()}`
      : "/analytics/data-quality/pilot-intake-report",
    reportTitle: payload.tableTitle || "Trendplus pilot izveštaj kvaliteta podataka",
    reportType: payload.documentType || "pilot-intake",
    generatedAtUtc: metadataMap.get("generatedAtUtc") ?? new Date().toISOString(),
    periodFrom: periodFrom ?? undefined,
    periodTo: periodTo ?? undefined,
    period: {
      fromUtc: periodFrom ?? new Date().toISOString(),
      toUtc: periodTo ?? new Date().toISOString(),
      label: "Pilot intake",
    },
    lastRefreshAtUtc: metadataMap.get("lastRefreshAtUtc") ?? null,
    dataQualityStatus: metadataMap.get("dataQualityStatus") ?? "warning",
    recommendationAllowed,
    usedFallback,
    warnings: rows
      .filter((row) => row.section.toLowerCase().includes("upozoren"))
      .map((row) => row.value)
      .filter((row) => row.trim().length > 0),
    methodology: metadataMap.get("methodology") ?? "Metodologija nije dostupna u privremenom preview payload-u.",
    rows,
    sections,
    payload: {
      tableKey: payload.tableKey,
      tableTitle: payload.tableTitle,
      documentType: payload.documentType ?? "pilot-intake",
      templateName: payload.templateName ?? "analytics-table-default",
      locale: payload.locale ?? "sr-RS",
      columns: payload.columns.map((column) => ({
        key: column.key,
        header: column.header,
        dataType: column.dataType,
      })),
      rows: rows.map((row) => ({
        section: row.section,
        item: row.item,
        value: row.value,
        secondary: row.secondary,
        note: row.note,
      })),
      filters: (payload.filters ?? []).map((item) => ({
        key: item.key,
        label: item.label,
        value: item.value == null ? "" : String(item.value),
      })),
      metadata: (payload.metadata ?? []).map((item) => ({
        key: item.key,
        label: item.label,
        value: item.value == null ? "" : String(item.value),
      })),
      templateVersion: payload.templateVersion,
    },
    meta: null,
  };
}

export default function PilotIntakeReportPage() {
  const [searchParams] = useSearchParams();
  const stateKey = searchParams.get("stateKey");
  const fromDate = searchParams.get("fromDate");
  const toDate = searchParams.get("toDate");
  const scope = searchParams.get("scope");
  const storeId = searchParams.get("storeId");
  const supplierId = searchParams.get("supplierId");

  const [durableReport, setDurableReport] = useState<PilotIntakeDurableReport | null>(null);
  const [backendError, setBackendError] = useState<ReportLoadError | null>(null);
  const [refreshStatus, setRefreshStatus] = useState<AnalyticsRefreshStatus | null>(null);
  const [refreshStatusError, setRefreshStatusError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [reloadTick, setReloadTick] = useState(0);

  const hasDurableParams = Boolean(fromDate || toDate || scope || storeId || supplierId);
  const legacyPayload = useMemo(() => getPrintPayload(stateKey), [stateKey]);
  const legacyDurableReport = useMemo(
    () => (legacyPayload ? mapLegacyPayloadToDurable(legacyPayload, stateKey, { fromDate, toDate, scope, storeId, supplierId }) : null),
    [fromDate, legacyPayload, scope, stateKey, storeId, supplierId, toDate]
  );

  useEffect(() => {
    let cancelled = false;

    setLoading(true);
    setBackendError(null);

    void (async () => {
      const refreshTask = getAnalyticsRefreshStatus();
      const reportTask = hasDurableParams
        ? getPilotIntakeDurableReport({
            fromDate,
            toDate,
            scope,
            storeId: storeId ? Number(storeId) : null,
            supplierId: supplierId ? Number(supplierId) : null,
          })
        : Promise.resolve(null);

      const [refreshResult, reportResult] = await Promise.allSettled([refreshTask, reportTask]);
      if (cancelled) return;

      if (refreshResult.status === "fulfilled") {
        setRefreshStatus(refreshResult.value);
        setRefreshStatusError(null);
      } else {
        setRefreshStatus(null);
        setRefreshStatusError(
          refreshResult.reason instanceof Error
            ? refreshResult.reason.message
            : "Status osvežavanja analitike nije dostupan."
        );
      }

      if (!hasDurableParams) {
        setDurableReport(null);
      } else if (reportResult.status === "fulfilled") {
        setDurableReport(reportResult.value);
      } else if (reportResult.status === "rejected") {
        setDurableReport(null);
        if (reportResult.reason instanceof AnalyticsMetaError) {
          setBackendError({
            message: reportResult.reason.message,
            errorCode: reportResult.reason.errorCode,
            correlationId: reportResult.reason.correlationId,
          });
        } else {
          setBackendError({
            message: reportResult.reason instanceof Error
              ? reportResult.reason.message
              : "Pilot intake report trenutno nije dostupan.",
          });
        }
      }

      setLoading(false);
    })();

    return () => {
      cancelled = true;
    };
  }, [fromDate, hasDurableParams, reloadTick, scope, storeId, supplierId, toDate]);

  const resolvedReport = durableReport ?? legacyDurableReport;
  const showLegacyWarning = Boolean(backendError && hasDurableParams && legacyDurableReport);

  if (loading && !resolvedReport) {
    return (
      <div className="pilot-intake-report-page">
        <div className="data-quality-loading">Učitavam pilot intake izveštaj...</div>
      </div>
    );
  }

  if (backendError && !resolvedReport) {
    return (
      <div className="pilot-intake-report-page">
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

  if (!resolvedReport) {
    return (
      <div className="pilot-intake-report-page">
        <AnalyticsEmptyState
          title="Izveštaj nije dostupan"
          message="Pregled izveštaja je istekao jer se čuva privremeno u browseru."
          reasons={[
            "Za trajni dokument koristite print/CSV/Excel export ili ponovo generišite report iz Data Quality ekrana.",
          ]}
          actions={[
            { label: "Vrati se na Data Quality", href: "/analytics/data-quality" },
            { label: "Ponovo generiši report", href: "/analytics/data-quality" },
          ]}
          variant="filtered_out"
          dataQualityHref="/analytics/data-quality"
          refreshStatusHref="/admin/configuration?panel=workers"
        />
      </div>
    );
  }

  return (
    <div className="pilot-intake-report-page">
      <AnalyticsTrustHeader
        title="Pilot intake izveštaj"
        description="Trajni pregled spremnosti podataka za pouzdanu analitiku i preporuke."
        mode="report"
        periodFrom={resolvedReport.periodFrom ?? resolvedReport.period?.fromUtc ?? null}
        periodTo={resolvedReport.periodTo ?? resolvedReport.period?.toUtc ?? null}
        lastRefreshAt={resolvedReport.lastRefreshAtUtc ?? refreshStatus?.lastSuccessfulRefreshAtUtc ?? null}
        dataFreshnessStatus={refreshStatus?.dataFreshnessStatus ?? null}
        refreshIsRunning={refreshStatus?.isRunning ?? false}
        refreshCurrentStep={refreshStatus?.currentStep ?? null}
        dataSource="Data quality checks"
        dataQualityStatus={resolvedReport.dataQualityStatus}
        dataQualityHref="/analytics/data-quality"
        refreshStatusHref="/admin/configuration?panel=workers"
        methodologyHref="/analytics/data-quality"
        methodologyLabel="Metodologija intake izveštaja"
        isPartial={resolvedReport.meta?.isPartial ?? false}
        emptyStateReason={resolvedReport.meta?.message ?? null}
      />

      <AnalyticsRefreshStatusBanner status={refreshStatus} error={refreshStatusError} />

      {showLegacyWarning ? (
        <div className="pirp-warning-banner no-print" role="status">
          Backend report trenutno nije dostupan. Prikazujemo privremeni browser preview.
        </div>
      ) : null}

      <div className="pirp-actions no-print">
        <Link to="/analytics/data-quality" className="pirp-back-link">Vrati se na Data Quality</Link>
        <button type="button" className="pirp-retry-btn" onClick={() => setReloadTick((prev) => prev + 1)}>Ponovo generiši report</button>
      </div>

      <PilotDataQualityIntakeReport
        report={null}
        durableReport={resolvedReport}
        loading={loading}
        error={backendError?.message ?? null}
        filters={resolvedReport.payload.filters}
        onRetry={() => setReloadTick((prev) => prev + 1)}
      />
    </div>
  );
}

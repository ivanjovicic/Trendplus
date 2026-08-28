import { useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import SupplierDecisionReport from "../components/analytics/SupplierDecisionReport";
import SupplierDecisionReportActions from "../components/analytics/SupplierDecisionReportActions";
import AnalyticsEmptyState from "../components/analytics/AnalyticsEmptyState";
import AnalyticsErrorState from "../components/analytics/AnalyticsErrorState";
import {
  ANALYTICS_BROWSER_PREVIEW_TTL_MS,
  getBrowserPreviewSnapshot,
  resolveAnalyticsTablePayload,
  type BrowserPreviewSnapshot,
} from "../services/analyticsTableState";
import { AnalyticsMetaError, getSupplierDecisionDurableReport } from "../services/analyticsApi";
import type { ResolvedAnalyticsTablePayload } from "../types/analyticsTable";
import { formatDateTime } from "../utils/analyticsFormatters";
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

function parseOptionalNumber(value: string | null): number | null {
  if (!value) return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

function parseOptionalBoolean(value: string | null): boolean | null {
  if (!value) return null;
  const normalized = value.trim().toLowerCase();
  if (normalized === "true") return true;
  if (normalized === "false") return false;
  return null;
}

function formatTtlMinutes(ttlMs: number): string {
  const minutes = Math.max(1, Math.round(ttlMs / 60_000));
  return `${minutes} min`;
}

const SUPPLIER_REPORT_METHODOLOGY_KEYS = [
  "revenue",
  "marginContribution",
  "markdownDependency",
  "stockAtRisk",
  "confidencePct",
  "reliabilityPct",
  "sellThrough",
];

export default function SupplierDecisionReportPage() {
  const [searchParams] = useSearchParams();
  const stateKey = searchParams.get("stateKey");
  const fromDate = searchParams.get("fromDate");
  const toDate = searchParams.get("toDate");
  const scope = searchParams.get("scope");
  const dataScope = searchParams.get("dataScope");
  const supplierId = searchParams.get("supplierId");
  const storeId = searchParams.get("storeId");
  const category = searchParams.get("category");
  const gender = searchParams.get("gender");
  const seasonId = searchParams.get("seasonId");
  const minRevenue = searchParams.get("minRevenue");
  const onlyHighConfidence = searchParams.get("onlyHighConfidence");
  const excludeOosBeforeMarkdown = searchParams.get("excludeOosBeforeMarkdown");
  const section = searchParams.get("section");
  const previewMode = searchParams.get("preview");

  const parsedSupplierId = useMemo(() => parseOptionalNumber(supplierId), [supplierId]);
  const parsedStoreId = useMemo(() => parseOptionalNumber(storeId), [storeId]);
  const parsedSeasonId = useMemo(() => parseOptionalNumber(seasonId), [seasonId]);
  const parsedMinRevenue = useMemo(() => parseOptionalNumber(minRevenue), [minRevenue]);
  const parsedOnlyHighConfidence = useMemo(() => parseOptionalBoolean(onlyHighConfidence), [onlyHighConfidence]);
  const parsedExcludeOosBeforeMarkdown = useMemo(() => parseOptionalBoolean(excludeOosBeforeMarkdown), [excludeOosBeforeMarkdown]);

  const [backendPayload, setBackendPayload] = useState<ResolvedAnalyticsTablePayload | null>(null);
  const [backendError, setBackendError] = useState<ReportLoadError | null>(null);
  const [loading, setLoading] = useState(false);
  const [reloadTick, setReloadTick] = useState(0);
  const [exportError, setExportError] = useState<string | null>(null);

  const hasDurableQueryValues = Boolean(
    fromDate
    || toDate
    || scope
    || dataScope
    || supplierId
    || storeId
    || category
    || gender
    || seasonId
    || minRevenue
    || onlyHighConfidence
    || excludeOosBeforeMarkdown
    || section
  );
  // A stateKey only represents an intentionally opened browser preview. Durable URLs
  // always reload the backend payload, including the default report URL with no filters.
  const isBrowserPreview = Boolean(stateKey) && (previewMode === "browser" || !hasDurableQueryValues);

  useEffect(() => {
    let cancelled = false;

    if (isBrowserPreview) {
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
          dataScope,
          supplierId: parsedSupplierId,
          storeId: parsedStoreId,
          category,
          gender,
          seasonId: parsedSeasonId,
          minRevenue: parsedMinRevenue,
          onlyHighConfidence: parsedOnlyHighConfidence,
          excludeOosBeforeMarkdown: parsedExcludeOosBeforeMarkdown,
          section,
        });

        if (cancelled) return;

        const payload = resolveAnalyticsTablePayload({
          tableKey: response.payload.tableKey,
          tableTitle: response.payload.tableTitle,
          documentType: response.payload.documentType,
          templateName: response.payload.templateName,
          locale: response.payload.locale,
          methodologyMetricKeys: SUPPLIER_REPORT_METHODOLOGY_KEYS,
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
  }, [
    category,
    dataScope,
    excludeOosBeforeMarkdown,
    fromDate,
    gender,
    isBrowserPreview,
    minRevenue,
    onlyHighConfidence,
    parsedExcludeOosBeforeMarkdown,
    parsedMinRevenue,
    parsedOnlyHighConfidence,
    parsedSeasonId,
    parsedStoreId,
    parsedSupplierId,
    reloadTick,
    section,
    scope,
    seasonId,
    storeId,
    supplierId,
    toDate,
  ]);

  const browserPreviewSnapshot = useMemo((): BrowserPreviewSnapshot | null => (
    isBrowserPreview ? getBrowserPreviewSnapshot(stateKey) : null
  ), [isBrowserPreview, stateKey]);
  const browserPreviewPayload = browserPreviewSnapshot?.payload ?? null;
  const payload = isBrowserPreview ? browserPreviewPayload : backendPayload;

  const durableReportHref = useMemo(() => {
    const params = new URLSearchParams();
    if (fromDate) params.set("fromDate", fromDate);
    if (toDate) params.set("toDate", toDate);
    if (scope) params.set("scope", scope);
    if (dataScope) params.set("dataScope", dataScope);
    if (supplierId) params.set("supplierId", supplierId);
    if (storeId) params.set("storeId", storeId);
    if (category) params.set("category", category);
    if (gender) params.set("gender", gender);
    if (seasonId) params.set("seasonId", seasonId);
    if (minRevenue) params.set("minRevenue", minRevenue);
    if (onlyHighConfidence) params.set("onlyHighConfidence", onlyHighConfidence);
    if (excludeOosBeforeMarkdown) params.set("excludeOosBeforeMarkdown", excludeOosBeforeMarkdown);
    if (section) params.set("section", section);
    return params.toString() ? `/analytics/supplier/report?${params.toString()}` : null;
  }, [
    category,
    dataScope,
    excludeOosBeforeMarkdown,
    fromDate,
    gender,
    minRevenue,
    onlyHighConfidence,
    section,
    scope,
    seasonId,
    storeId,
    supplierId,
    toDate,
  ]);

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

  if (isBrowserPreview && !payload) {
    return (
      <div className="supplier-decision-report-page">
        <AnalyticsEmptyState
          title="Pregled izveštaja je istekao"
          message="Pregled izveštaja je istekao jer se čuva privremeno u browseru."
          reasons={[
            "Za trajni dokument koristite Excel/Print ili ponovo generišite report.",
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
      </div>
    );
  }

  if (!payload) {
    return (
      <div className="supplier-decision-report-page">
        <AnalyticsEmptyState
          title="Trajni izveštaj nema podatke"
          message="Backend nije vratio podatke za traženi kontekst izveštaja."
          reasons={["Proverite period i aktivne filtere, pa ponovo učitajte report."]}
          actions={[
            { label: "Vrati se na dobavljače", href: "/analytics/supplier" },
            { label: "Otvori Scorecard", href: "/analytics/supplier?tab=scorecard" },
          ]}
          refreshStatusHref="/admin/configuration?panel=workers"
          dataQualityHref="/analytics/data-quality"
          variant="insufficient_data"
        />
      </div>
    );
  }

  const previewSavedAtLabel = browserPreviewSnapshot
    ? formatDateTime(browserPreviewSnapshot.savedAtUtc)
    : null;
  const previewExpiresAtLabel = browserPreviewSnapshot
    ? formatDateTime(browserPreviewSnapshot.expiresAtUtc)
    : null;
  const previewTtlLabel = formatTtlMinutes(browserPreviewSnapshot?.ttlMs ?? ANALYTICS_BROWSER_PREVIEW_TTL_MS);

  return (
    <div className={`supplier-decision-report-page${isBrowserPreview ? " sdrp-local-preview" : ""}`}>
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

      {isBrowserPreview ? (
        <div className="sdrp-local-preview-banner" role="status" data-testid="local-preview-banner">
          <strong className="sdrp-local-preview-badge">LOKALNI BROWSER PREVIEW</strong>
          <p>
            Ovo nije trajni backend izveštaj. Snapshot je sačuvan u browseru
             {previewSavedAtLabel ? <> u <time dateTime={browserPreviewSnapshot!.savedAtUtc}>{previewSavedAtLabel}</time></> : null}
            {" "}(TTL {previewTtlLabel}
            {previewExpiresAtLabel ? <>, ističe {previewExpiresAtLabel}</> : null}).
            Izvoz i štampa su onemogućeni dok ne otvorite trajni report.
          </p>
        </div>
      ) : null}

      <header className="sdrp-head no-print">
        <div>
          <h1>Trendplus izveštaj dobavljača</h1>
          <p>
            {isBrowserPreview
              ? "Privremeni browser snapshot — nije potvrđen kao trenutni backend izveštaj."
              : "Pregled izveštaja u HTML formi spremnoj za štampu i izvoz. Trajni backend payload se ponovo učitava pri svakom otvaranju."}
          </p>
          {isBrowserPreview && previewSavedAtLabel ? (
            <p className="sdrp-preview-meta" data-testid="local-preview-meta">
              Sačuvano: {previewSavedAtLabel} · TTL: {previewTtlLabel}
              {previewExpiresAtLabel ? ` · Ističe: ${previewExpiresAtLabel}` : ""}
            </p>
          ) : null}
        </div>
        <div className="sdrp-actions">
          <Link to="/analytics/supplier" className="sdrp-back">Vrati se na dobavljače</Link>
          <Link to="/analytics/supplier" className="sdrp-back">Ponovo generiši report</Link>
          <Link to="/analytics/supplier?tab=scorecard" className="sdrp-back">Otvori Scorecard</Link>
          {isBrowserPreview ? (
            <p className="sdrp-export-disabled" data-testid="local-preview-export-disabled">
              Izvoz/štampa onemogućeni za lokalni preview. Otvorite trajni report preko Scorecard akcija.
            </p>
          ) : (
            <>
              <SupplierDecisionReportActions payload={payload} durableReportHref={durableReportHref} onError={setExportError} />
              <button type="button" className="sdrp-print" onClick={() => window.print()}>Štampaj iz pregleda</button>
            </>
          )}
        </div>
      </header>

      <SupplierDecisionReport payload={payload} />
    </div>
  );
}

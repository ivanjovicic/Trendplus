import { useCallback, useEffect, useMemo, useState, type FormEvent } from "react";
import { Link, useSearchParams } from "react-router-dom";
import AnalyticsEmptyState from "../components/analytics/AnalyticsEmptyState";
import AnalyticsErrorState from "../components/analytics/AnalyticsErrorState";
import AnalyticsRefreshStatusBanner from "../components/analytics/AnalyticsRefreshStatusBanner";
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import AnalyticsTrustHeader from "../components/analytics/AnalyticsTrustHeader";
import KpiExplainButton from "../components/analytics/KpiExplainButton";
import PilotDataQualityIntakeReportPanel from "../components/analytics/PilotDataQualityIntakeReport";
import InfoTip from "../components/ui/InfoTip";
import {
  AnalyticsMetaError,
  getAnalyticsRefreshStatus,
  getAnalyticsDataQualityHealth,
  getAnalyticsDataQualityTrend,
  getPilotDataQualityIntakeReport,
  getPilotIntakeDurableReport,
  getDataQualityIssues,
  getDataQualityTopOffenders,
} from "../services/analyticsApi";
import type {
  AnalyticsRefreshStatus,
  AnalyticsDataQualityHealth,
  DataQualityIssueItem,
  DataQualityIssueListResult,
  DataQualityIssueType,
  DataQualitySortBy,
  DataQualitySortDir,
  PilotDataQualityIntakeReport as PilotDataQualityIntakeReportDto,
  PilotIntakeDurableReport,
  DataQualityTopOffendersResult,
  DataQualityTrendPoint,
  DataQualityTrendResult,
} from "../types/analytics";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import { fmtNumber, fmtPct, fmtRsd, formatDate, formatDateTime } from "../utils/analyticsFormatters";
import {
  getAnalyticsMetaMessage,
  isAnalyticsMetaEmpty,
  isAnalyticsMetaWarning,
} from "../utils/analyticsResponseMeta";
import "./DataQualityPage.css";

const ISSUE_TABS: Array<{ key: DataQualityIssueType; label: string; tone: "danger" | "warning" | "neutral" }> = [
  { key: "missingSupplier", label: "Nedostajući dobavljač", tone: "danger" },
  { key: "missingShoeType", label: "Nedostajući tip obuće", tone: "warning" },
];

const LOW_PRIORITY_TABS: Array<{ key: DataQualityIssueType; label: string; tone: "danger" | "warning" | "neutral" }> = [
  { key: "invalidName", label: "Neispravni nazivi", tone: "neutral" },
];

const VIEW_TABS = [
  { key: "issues", label: "Detalji problema" },
  { key: "intake", label: "Pilot intake izveštaj" },
] as const;

const analyticsColumns: AnalyticsTableColumn<DataQualityIssueItem>[] = [
  { key: "sku", header: "SKU", dataType: "text" },
  { key: "productId", header: "Artikal ID", dataType: "text" },
  { key: "name", header: "Naziv artikla", dataType: "text" },
  { key: "supplierName", header: "Dobavljač", dataType: "text" },
  { key: "shoeTypeName", header: "Tip obuće", dataType: "text" },
  { key: "sales30d", header: "Pogođeni promet 30d", dataType: "currency" },
  { key: "stock", header: "Stanje", dataType: "number" },
  { key: "lastUpdated", header: "Ažurirano", dataType: "datetime" },
  { key: "issueType", header: "Problem", dataType: "text" },
];

function normalizeIssueType(value: string | null): DataQualityIssueType {
  if (value === "missingShoeType" || value === "invalidName") return value;
  return "missingSupplier";
}

function normalizeSortBy(value: string | null): DataQualitySortBy {
  if (value === "lastUpdated" || value === "stock" || value === "name") return value;
  return "sales30d";
}

function normalizeSortDir(value: string | null): DataQualitySortDir {
  return value === "asc" ? "asc" : "desc";
}

function parsePositiveInt(value: string | null, fallback: number): number {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? Math.floor(parsed) : fallback;
}

function issueLabel(issueType: DataQualityIssueType): string {
  return [...ISSUE_TABS, ...LOW_PRIORITY_TABS].find((item) => item.key === issueType)?.label ?? issueType;
}

function rowTone(issueType: DataQualityIssueType): string {
  if (issueType === "missingSupplier") return "badge-danger";
  if (issueType === "missingShoeType") return "badge-warning";
  return "badge-neutral";
}

function scoreTone(status: AnalyticsDataQualityHealth["scoreStatus"] | undefined): string {
  switch (status) {
    case "excellent":
      return "excellent";
    case "good":
      return "good";
    case "warning":
      return "warning";
    case "critical":
      return "critical";
    default:
      return "warning";
  }
}

function buildLinePath(points: DataQualityTrendPoint[], selector: (point: DataQualityTrendPoint) => number, width: number, height: number) {
  const padding = 14;
  const values = points.map(selector);
  const max = Math.max(...values, 1);
  const min = Math.min(...values, 0);
  const range = Math.max(max - min, 1);

  return points
    .map((point, index) => {
      const x = points.length === 1
        ? width / 2
        : padding + ((width - padding * 2) * index) / (points.length - 1);
      const value = selector(point);
      const y = height - padding - ((value - min) / range) * (height - padding * 2);
      return `${index === 0 ? "M" : "L"} ${x.toFixed(2)} ${y.toFixed(2)}`;
    })
    .join(" ");
}

function trendTone(points: DataQualityTrendPoint[], selector: (point: DataQualityTrendPoint) => number): "improving" | "worsening" {
  if (points.length < 2) return "improving";
  return selector(points[points.length - 1]) <= selector(points[0]) ? "improving" : "worsening";
}

function TopOffendersPanel({ issueType, dataScope }: { issueType: DataQualityIssueType; dataScope?: string | null }) {
  const [result, setResult] = useState<DataQualityTopOffendersResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let isCancelled = false;

    const load = async () => {
      try {
        const response = await getDataQualityTopOffenders(issueType, 10, dataScope);
        if (!isCancelled) {
          setResult(response);
          setError(null);
        }
      } catch (reason) {
        if (!isCancelled) {
          setResult(null);
          setError(reason instanceof Error ? reason.message : "Top problemi nisu dostupni.");
        }
      }
    };

    void load();
    return () => {
      isCancelled = true;
    };
  }, [dataScope, issueType]);

  if (error) {
    return <div className="data-quality-inline-error">{error}</div>;
  }

  if (!result || result.items.length === 0) {
    return null;
  }

  return (
    <section className="data-quality-top-offenders">
      <div className="data-quality-section-head">
        <div>
          <h2>Top offenders</h2>
          <p>Rangirano po pogođenom prometu unutar aktivnog tipa problema.</p>
        </div>
        <span className="data-quality-top-offenders-meta">Top {result.count}</span>
      </div>

      <div className="data-quality-table-wrap">
        <table className="data-quality-table data-quality-table-compact">
          <thead>
            <tr>
              <th>SKU</th>
              <th>Artikal</th>
              <th>Dobavljač</th>
              <th className="align-right">Promet 30d</th>
              <th className="align-right">Impact (RSD)</th>
              <th className="align-right">Impact (%)</th>
              <th>Akcija</th>
            </tr>
          </thead>
          <tbody>
            {result.items.map((item) => (
              <tr key={`${item.productId}-${item.sku ?? "nosku"}`}>
                <td>{item.sku || "-"}</td>
                <td>
                  <div className="data-quality-name">
                    <strong>{item.name || "Naziv nedostaje"}</strong>
                    <span>ID: {item.productId}</span>
                  </div>
                </td>
                <td>{item.supplierName || item.shoeTypeName || "-"}</td>
                <td className="align-right">{fmtRsd(item.sales30d, 2)}</td>
                <td className="align-right">{fmtRsd(item.revenueImpactRsd, 2)}</td>
                <td className="align-right">{fmtPct(item.revenueImpactPct, 1)}</td>
                <td>
                  <Link className="data-quality-action" to={item.actionUrl || `/artikli/${item.productId}/edit`}>
                    Otvori artikal
                  </Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function DataQualityTrendChart({ dataScope }: { dataScope?: string | null }) {
  const [trend, setTrend] = useState<DataQualityTrendResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let isCancelled = false;

    const load = async () => {
      try {
        const response = await getAnalyticsDataQualityTrend(7, dataScope);
        if (!isCancelled) {
          setTrend(response);
          setError(null);
        }
      } catch (reason) {
        if (!isCancelled) {
          setTrend(null);
          setError(reason instanceof Error ? reason.message : "Trend nije dostupan.");
        }
      }
    };

    void load();
    return () => {
      isCancelled = true;
    };
  }, [dataScope]);

  const chart = useMemo(() => {
    if (!trend || trend.points.length === 0) return null;

    const width = 360;
    const height = 120;
    return {
      width,
      height,
      missingCostPath: buildLinePath(trend.points, (point) => point.missingCostRevenueSharePct, width, height),
      unknownSupplierPath: buildLinePath(trend.points, (point) => point.unknownSupplierRevenueSharePct, width, height),
      missingCostTone: trendTone(trend.points, (point) => point.missingCostRevenueSharePct),
      unknownSupplierTone: trendTone(trend.points, (point) => point.unknownSupplierRevenueSharePct),
    };
  }, [trend]);

  if (error) {
    return <div className="data-quality-inline-error">{error}</div>;
  }

  if (!trend || trend.points.length === 0 || !chart) {
    return null;
  }

  return (
    <section className="data-quality-trend-card">
      <div className="data-quality-section-head">
        <div>
          <h2>Data Quality Trend</h2>
          <p>Posljednjih {trend.days} dana za missing cost i unknown supplier pokazatelje.</p>
        </div>
      </div>

      <div className="data-quality-trend-stage">
        <svg viewBox={`0 0 ${chart.width} ${chart.height}`} role="img" aria-label="Data quality trend chart">
          <path className={`trend-line ${chart.missingCostTone}`} d={chart.missingCostPath} />
          <path className={`trend-line ${chart.unknownSupplierTone}`} d={chart.unknownSupplierPath} />
        </svg>
      </div>

      <div className="data-quality-trend-legend">
        <span className={`legend-chip ${chart.missingCostTone}`}>Missing cost %</span>
        <span className={`legend-chip ${chart.unknownSupplierTone}`}>Unknown supplier %</span>
      </div>

      <div className="data-quality-trend-labels">
        {trend.points.map((point) => (
          <span key={point.date}>{formatDate(point.date)}</span>
        ))}
      </div>
    </section>
  );
}

export default function DataQualityPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [data, setData] = useState<DataQualityIssueListResult | null>(null);
  const [health, setHealth] = useState<AnalyticsDataQualityHealth | null>(null);
  const [intakeReport, setIntakeReport] = useState<PilotDataQualityIntakeReportDto | null>(null);
  const [durableIntakeReport, setDurableIntakeReport] = useState<PilotIntakeDurableReport | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<{ message: string; errorCode?: string | null; correlationId?: string | null } | null>(null);
  const [healthError, setHealthError] = useState<string | null>(null);
  const [intakeReportError, setIntakeReportError] = useState<string | null>(null);
  const [refreshStatus, setRefreshStatus] = useState<AnalyticsRefreshStatus | null>(null);
  const [refreshStatusError, setRefreshStatusError] = useState<string | null>(null);
  const [searchDraft, setSearchDraft] = useState(searchParams.get("q") ?? "");

  const issueType = normalizeIssueType(searchParams.get("type"));
  const page = parsePositiveInt(searchParams.get("page"), 1);
  const pageSize = parsePositiveInt(searchParams.get("pageSize"), 25);
  const sortBy = normalizeSortBy(searchParams.get("sortBy"));
  const sortDir = normalizeSortDir(searchParams.get("sortDir"));
  const q = searchParams.get("q") ?? "";
  const totalPages = Math.max(1, Math.ceil((data?.total ?? 0) / pageSize));
  const originTable = searchParams.get("originTable");
  const contextFromDate = searchParams.get("fromDate");
  const contextToDate = searchParams.get("toDate");
  const contextSezonaId = searchParams.get("sezonaId");
  const contextStoreId = searchParams.get("storeId");
  const contextDataScope = searchParams.get("dataScope");
  const contextScope = searchParams.get("scope");
  const contextIncludeUnknown = searchParams.get("includeUnknown");
  const contextFocus = searchParams.get("focus");
  const contextSupplierId = searchParams.get("supplierId");
  const returnTo = searchParams.get("returnTo");
  const viewMode = searchParams.get("view") === "intake" ? "intake" : "issues";

  const supplierContextQuery = useMemo(() => {
    const params = new URLSearchParams();
    if (contextFromDate) params.set("fromDate", contextFromDate);
    if (contextToDate) params.set("toDate", contextToDate);
    if (contextSezonaId) params.set("sezonaId", contextSezonaId);
    if (contextStoreId) params.set("storeId", contextStoreId);
    if (contextDataScope) params.set("dataScope", contextDataScope);
    if (contextIncludeUnknown) params.set("includeUnknown", contextIncludeUnknown);
    if (contextSupplierId) params.set("supplierId", contextSupplierId);
    params.set("focus", contextFocus || "data-quality");
    return params.toString();
  }, [
    contextDataScope,
    contextFocus,
    contextFromDate,
    contextIncludeUnknown,
    contextSezonaId,
    contextStoreId,
    contextSupplierId,
    contextToDate,
  ]);

  const pilotIntakeReportHref = useMemo(() => {
    const params = new URLSearchParams();
    if (contextFromDate) params.set("fromDate", contextFromDate);
    if (contextToDate) params.set("toDate", contextToDate);
    if (contextStoreId) params.set("storeId", contextStoreId);
    if (contextSupplierId) params.set("supplierId", contextSupplierId);
    const scopeValue = contextScope ?? contextDataScope;
    if (scopeValue) {
      params.set("scope", scopeValue);
    }
    if (contextDataScope) {
      params.set("dataScope", contextDataScope);
    }
    const query = params.toString();
    return query ? `/analytics/reports/pilot-intake?${query}` : "/analytics/reports/pilot-intake";
  }, [contextDataScope, contextFromDate, contextScope, contextStoreId, contextSupplierId, contextToDate]);

  useEffect(() => {
    setSearchDraft(q);
  }, [q]);

  const updateParams = useCallback((changes: Record<string, string | number | null | undefined>) => {
    const next = new URLSearchParams(searchParams);

    for (const [key, value] of Object.entries(changes)) {
      if (value == null || value === "") {
        next.delete(key);
      } else {
        next.set(key, String(value));
      }
    }

    setSearchParams(next, { replace: false });
  }, [searchParams, setSearchParams]);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    setHealthError(null);
    setIntakeReportError(null);

    const [issuesResult, healthResult, refreshResult, intakeResult, durableIntakeResult] = await Promise.allSettled([
      getDataQualityIssues({
        type: issueType,
        page,
        pageSize,
        q,
        sortBy,
        sortDir,
        dataScope: contextDataScope,
      }),
      getAnalyticsDataQualityHealth(undefined, contextDataScope),
      getAnalyticsRefreshStatus(),
      getPilotDataQualityIntakeReport({
        fromDate: contextFromDate,
        toDate: contextToDate,
        storeId: contextStoreId ? Number(contextStoreId) : null,
        supplierId: contextSupplierId ? Number(contextSupplierId) : null,
        dataScope: contextDataScope,
      }),
      getPilotIntakeDurableReport({
        fromDate: contextFromDate,
        toDate: contextToDate,
        storeId: contextStoreId ? Number(contextStoreId) : null,
        supplierId: contextSupplierId ? Number(contextSupplierId) : null,
        dataScope: contextDataScope,
      }),
    ]);

    if (issuesResult.status === "fulfilled") {
      setData(issuesResult.value);
    } else {
      setData(null);
      if (issuesResult.reason instanceof AnalyticsMetaError) {
        setError({
          message: issuesResult.reason.message,
          errorCode: issuesResult.reason.errorCode,
          correlationId: issuesResult.reason.correlationId,
        });
      } else {
        setError({
          message: issuesResult.reason instanceof Error
            ? issuesResult.reason.message
            : "Data quality podaci nisu dostupni.",
        });
      }
    }

    if (healthResult.status === "fulfilled") {
      setHealth(healthResult.value);
    } else {
      setHealth(null);
      setHealthError(
        healthResult.reason instanceof Error
          ? healthResult.reason.message
          : "Health snapshot nije dostupan."
      );
    }

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

    if (durableIntakeResult.status === "fulfilled") {
      setDurableIntakeReport(durableIntakeResult.value);
      if (intakeResult.status === "fulfilled") {
        setIntakeReport(intakeResult.value);
      } else {
        setIntakeReport(null);
      }
      setIntakeReportError(null);
    } else if (intakeResult.status === "fulfilled") {
      setDurableIntakeReport(null);
      setIntakeReport(intakeResult.value);
      setIntakeReportError(null);
    } else {
      setDurableIntakeReport(null);
      setIntakeReport(null);
      setIntakeReportError(
        durableIntakeResult.reason instanceof Error
          ? durableIntakeResult.reason.message
          : intakeResult.reason instanceof Error
            ? intakeResult.reason.message
            : "Pilot intake report nije dostupan."
      );
    }

    setLoading(false);
  }, [contextDataScope, contextFromDate, contextStoreId, contextSupplierId, contextToDate, issueType, page, pageSize, q, sortBy, sortDir]);

  useEffect(() => {
    void load();
  }, [load]);

  const toolbarFilters = useMemo<AnalyticsNamedValue[]>(() => {
    const values: AnalyticsNamedValue[] = [
      { key: "type", label: "Tip problema", value: issueLabel(issueType) },
      { key: "page", label: "Strana", value: page },
      { key: "pageSize", label: "Po strani", value: pageSize },
      { key: "sortBy", label: "Sort", value: `${sortBy} ${sortDir}` },
    ];

    if (q) values.push({ key: "q", label: "Pretraga", value: q });
    if (contextFromDate || contextToDate) {
      values.push({
        key: "contextPeriod",
        label: "Kontekst period",
        value: `${formatDate(contextFromDate)} - ${formatDate(contextToDate)}`,
      });
    }
    if (contextStoreId) values.push({ key: "contextStoreId", label: "Objekat", value: contextStoreId });
    if (contextDataScope) values.push({ key: "contextDataScope", label: "Scope", value: contextDataScope });
    if (originTable) values.push({ key: "originTable", label: "Otvoreno iz", value: originTable });

    return values;
  }, [contextDataScope, contextFromDate, contextStoreId, contextToDate, issueType, originTable, page, pageSize, q, sortBy, sortDir]);

  const toolbarMetadata = useMemo<AnalyticsNamedValue[]>(() => [
    { key: "total", label: "Ukupno problema", value: data?.total ?? 0 },
    { key: "issueType", label: "Issue type", value: issueType },
  ], [data?.total, issueType]);

  const issuesMeta = data?.meta ?? null;
  const issuesMetaMessage = getAnalyticsMetaMessage(issuesMeta);
  const showIssuesMetaWarning = viewMode === "issues" && !loading && !error && isAnalyticsMetaWarning(issuesMeta);
  const showEmptyState = !loading && !error && (data?.items.length ?? 0) === 0;
  const emptyStateVariant = useMemo<"no_data" | "insufficient_data" | "filtered_out" | null>(() => {
    if (!showEmptyState) return null;
    if (issuesMeta?.dataQualityStatus === "insufficient_data") return "insufficient_data";
    if (isAnalyticsMetaEmpty(issuesMeta) && issuesMeta?.dataQualityStatus === "insufficient_data") return "insufficient_data";
    if (q.trim().length > 0) return "filtered_out";
    return "no_data";
  }, [issuesMeta, issuesMeta?.dataQualityStatus, q, showEmptyState]);

  const healthStatus = useMemo(() => {
    if (!health) return { label: "Snapshot nije dostupan", tone: "neutral" as const };

    const hasWarning =
      health.orphanArticleCount >= health.thresholds.orphanArticleCount ||
      (health.missingCostRevenueSharePct ?? 0) >= health.thresholds.missingCostRevenueSharePct ||
      (health.unknownSupplierRevenueSharePct ?? 0) >= health.thresholds.unknownSupplierRevenueSharePct;

    return hasWarning
      ? { label: "Potrebna je korekcija podataka", tone: "warning" as const }
      : { label: "Podaci su u zelenoj zoni", tone: "ok" as const };
  }, [health]);

  const changeTab = (nextType: DataQualityIssueType) => {
    updateParams({ type: nextType, page: 1 });
  };

  const submitSearch = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    updateParams({ q: searchDraft.trim(), page: 1 });
  };

  const showAdvancedContext = Boolean(contextIncludeUnknown || contextFocus || contextSupplierId || contextSezonaId);
  const trustDataQualityStatus = durableIntakeReport?.dataQualityStatus
    ?? (intakeReport
      ? intakeReport.readinessStatus === "critical"
      ? "critical"
      : intakeReport.readinessStatus === "excellent" || intakeReport.readinessStatus === "good"
        ? "good"
        : "warning"
      : health?.scoreStatus === "critical"
      ? "critical"
      : health?.scoreStatus === "warning"
        ? "warning"
        : health?.scoreStatus === "good" || health?.scoreStatus === "excellent"
          ? "good"
          : health?.meta?.dataQualityStatus ?? null);

  const trustSummary = intakeReport
    ? {
        missingSupplierCount: intakeReport.issues.missingSupplierCount,
        missingCostCount: intakeReport.issues.missingCostCount,
        missingCategoryCount: intakeReport.issues.missingCategoryCount,
        insufficientSignalCount: intakeReport.impact.insufficientSignalCount,
        ignoredRowsCount: intakeReport.impact.ignoredRowsCount,
      }
    : {
        missingSupplierCount: health?.orphanArticleCount ?? null,
      };

  const changeView = (nextView: "issues" | "intake") => {
    updateParams({ view: nextView, page: 1 });
  };

  return (
    <div className="data-quality-page">
      <AnalyticsTrustHeader
        title="Provera kvaliteta podataka"
        description="Centralni pregled problema koji direktno uticu na pouzdanost analitike i preporuka."
        periodFrom={contextFromDate ?? health?.windowFrom ?? null}
        periodTo={contextToDate ?? health?.windowTo ?? null}
        lastRefreshAt={refreshStatus?.lastSuccessfulRefreshAtUtc ?? health?.meta?.lastRefreshAtUtc ?? health?.generatedAt ?? null}
        dataFreshnessStatus={refreshStatus?.dataFreshnessStatus ?? null}
        refreshIsRunning={refreshStatus?.isRunning ?? false}
        refreshCurrentStep={refreshStatus?.currentStep ?? null}
        dataSource="Data quality checks"
        dataQualityStatus={trustDataQualityStatus}
        dataQualitySummary={trustSummary}
        mode="report"
        emptyStateReason={
          viewMode === "issues"
            ? (!loading && !error && data?.items.length === 0 ? (issuesMetaMessage ?? "Nema otvorenih data quality problema za izabrani filter.") : null)
            : durableIntakeReport?.meta?.message ?? intakeReport?.meta?.message ?? null
        }
        methodologyHref="/analytics/data-quality"
        dataQualityHref="/analytics/data-quality"
        refreshStatusHref="/admin/configuration?panel=workers"
        compact
      />
      <header className="data-quality-header">
        <div>
          <h1 className="data-quality-title">Provera kvaliteta podataka <InfoTip text="Problemi koji utiču na analytics." /></h1>
          <p className="data-quality-subtitle">
            Fokus na kritične greške: dobavljač, kategorija, naziv artikla.
          </p>
        </div>
        <div className="data-quality-header-side">
          {health ? (
            <section className={`data-quality-score-card ${scoreTone(health.scoreStatus)}`} aria-label="Data quality score">
              <span className="data-quality-score-label">Data quality score</span>
              <strong>{health.score}</strong>
              <span className="data-quality-score-status">{health.scoreStatus}</span>
              <p>{health.scoreSummary}</p>
              <KpiExplainButton metricKey="dataReadiness" ariaLabel="Kako je izračunat data quality score" />
            </section>
          ) : null}
          <div className="data-quality-meta">
            <span>Signal filter: samo artikli sa više od 1.000 RSD prometa u 30 dana</span>
            <Link to={pilotIntakeReportHref}>Otvori pilot intake report</Link>
          </div>
        </div>
      </header>

      <AnalyticsRefreshStatusBanner
        status={refreshStatus}
        loading={loading}
        error={refreshStatusError}
      />

      <div className="data-quality-tabs" role="tablist" aria-label="Data quality views">
        {VIEW_TABS.map((tab) => (
          <button
            key={tab.key}
            type="button"
            role="tab"
            aria-selected={viewMode === tab.key}
            className={`data-quality-tab ${viewMode === tab.key ? "active" : ""} neutral`}
            onClick={() => changeView(tab.key)}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {viewMode === "intake" ? (
        <PilotDataQualityIntakeReportPanel
          report={intakeReport}
          loading={loading}
          error={intakeReportError}
          filters={toolbarFilters}
          durableReport={durableIntakeReport}
          onRetry={() => {
            void load();
          }}
        />
      ) : null}

      {showIssuesMetaWarning ? (
        <div className="data-quality-loading" role="status">
          Prikazani podaci su delimični. {issuesMetaMessage ?? "Proverite analytics refresh status."}
        </div>
      ) : null}

      {viewMode === "issues" && health ? (
        <section className="data-quality-health-grid">
          <article className={`data-quality-health-card ${healthStatus.tone}`}>
            <span className="data-quality-health-label">Health status</span>
            <strong>{healthStatus.label}</strong>
            <p>
              Prozor: {formatDateTime(health.windowFrom)} - {formatDateTime(health.windowTo)} | Lookback {health.lookbackDays} dana
            </p>
          </article>

          <article className="data-quality-health-card">
            <span className="data-quality-health-label">Artikli bez dobavljača</span>
            <strong>{fmtNumber(health.orphanArticleCount)}</strong>
            <p>Warning threshold: {health.thresholds.orphanArticleCount}</p>
          </article>

          <article className="data-quality-health-card">
            <span className="data-quality-health-label">Promet bez nabavne cene</span>
            <strong>{fmtPct(health.missingCostRevenueSharePct, 1)}</strong>
            <p>{fmtRsd(health.missingCostRevenue, 2)} bez pouzdane marze</p>
            <KpiExplainButton metricKey="revenueWithoutCost" ariaLabel="Kako je izračunat promet bez nabavne cene" />
          </article>

          <article className="data-quality-health-card">
            <span className="data-quality-health-label">Promet nepoznatog dobavljača</span>
            <strong>{fmtPct(health.unknownSupplierRevenueSharePct, 1)}</strong>
            <p>{fmtRsd(health.unknownSupplierRevenue, 2)} u unknown bucket-u</p>
            <KpiExplainButton metricKey="revenueUnknownSupplier" ariaLabel="Kako je izračunat promet nepoznatog dobavljača" />
          </article>
        </section>
      ) : null}

      {viewMode === "issues" ? <DataQualityTrendChart dataScope={contextDataScope} /> : null}

      {viewMode === "issues" && health ? (
        <section className="data-quality-quick-actions">
          <button type="button" onClick={() => changeTab("missingSupplier")}>
            Artikli bez dobavljača
          </button>
          <Link to={`/analytics/supplier${supplierContextQuery ? `?tab=overview&${supplierContextQuery}` : "?tab=overview"}`}>
            Otvori supplier analitiku
          </Link>
          {returnTo ? <Link to={returnTo}>Nazad na izvorni kontekst</Link> : null}
        </section>
      ) : null}

      {viewMode === "issues" ? <div className="data-quality-tabs" role="tablist" aria-label="Data quality issue tabs">
        {ISSUE_TABS.map((tab) => (
          <button
            key={tab.key}
            type="button"
            role="tab"
            aria-selected={issueType === tab.key}
            className={`data-quality-tab ${issueType === tab.key ? "active" : ""} ${tab.tone}`}
            onClick={() => changeTab(tab.key)}
          >
            {tab.label}
          </button>
        ))}
      </div> : null}

      {viewMode === "issues" ? <details className="data-quality-low-priority" open={issueType === "invalidName"}>
        <summary>Low priority issues</summary>
        <div className="data-quality-tabs" role="tablist" aria-label="Low priority issue tabs">
          {LOW_PRIORITY_TABS.map((tab) => (
            <button
              key={tab.key}
              type="button"
              role="tab"
              aria-selected={issueType === tab.key}
              className={`data-quality-tab ${issueType === tab.key ? "active" : ""} ${tab.tone}`}
              onClick={() => changeTab(tab.key)}
            >
              {tab.label}
            </button>
          ))}
        </div>
      </details> : null}

      {viewMode === "issues" ? <section className="data-quality-controls">
        <form className="data-quality-search" onSubmit={submitSearch}>
          <input
            type="search"
            value={searchDraft}
            onChange={(event) => setSearchDraft(event.target.value)}
            placeholder="Pretraga po SKU, artiklu, dobavljaču, tipu..."
          />
          <button type="submit">Pretrazi</button>
        </form>

        <div className="data-quality-selects">
          <label>
            <span>Sort by</span>
            <select value={sortBy} onChange={(event) => updateParams({ sortBy: event.target.value, page: 1 })}>
              <option value="sales30d">Affected revenue 30d</option>
              <option value="lastUpdated">Last updated</option>
              <option value="stock">Stock</option>
              <option value="name">Name</option>
            </select>
          </label>

          <label>
            <span>Smer</span>
            <select value={sortDir} onChange={(event) => updateParams({ sortDir: event.target.value, page: 1 })}>
              <option value="desc">DESC</option>
              <option value="asc">ASC</option>
            </select>
          </label>

          <label>
            <span>Po strani</span>
            <select value={pageSize} onChange={(event) => updateParams({ pageSize: event.target.value, page: 1 })}>
              <option value="10">10</option>
              <option value="25">25</option>
              <option value="50">50</option>
              <option value="100">100</option>
            </select>
          </label>
        </div>
      </section> : null}

      {viewMode === "issues" && originTable ? (
        <div className="data-quality-origin">
          <div className="data-quality-origin-main">
            <span>Otvoreno iz analytics tabele: <strong>{originTable}</strong></span>
            {(contextFromDate || contextToDate) ? (
              <span>Kontekst perioda: <strong>{formatDate(contextFromDate)} - {formatDate(contextToDate)}</strong></span>
            ) : null}
            {contextStoreId ? <span>Objekat: <strong>{contextStoreId}</strong></span> : null}
            {contextDataScope ? <span>Scope: <strong>{contextDataScope}</strong></span> : null}
          </div>

          {showAdvancedContext ? (
            <details className="data-quality-origin-advanced">
              <summary>Napredni kontekst</summary>
              <div className="data-quality-origin-main">
                {contextSezonaId ? <span>Sezona: <strong>{contextSezonaId}</strong></span> : null}
                {contextIncludeUnknown ? <span>Include unknown: <strong>{contextIncludeUnknown}</strong></span> : null}
                {contextFocus ? <span>Focus: <strong>{contextFocus}</strong></span> : null}
                {contextSupplierId ? <span>Supplier: <strong>{contextSupplierId}</strong></span> : null}
              </div>
            </details>
          ) : null}
        </div>
      ) : null}

      {viewMode === "issues" && error ? (
        <AnalyticsErrorState
          title="Podaci trenutno nisu dostupni"
          message={error.message || "Ne prikazujemo nule jer nije potvrđeno da je period stvarno prazan."}
          errorCode={error.errorCode ?? undefined}
          correlationId={error.correlationId ?? undefined}
          onRetry={() => {
            void load();
          }}
          helpHref="/analytics/data-quality"
        />
      ) : null}

      {viewMode === "issues" && showEmptyState ? (
        <AnalyticsEmptyState
          variant={emptyStateVariant ?? "no_data"}
          message={
            emptyStateVariant === "insufficient_data"
              ? "Ne prikazujemo automatsku preporuku jer signal nije dovoljno jak."
              : emptyStateVariant === "filtered_out"
                ? "Promenite filtere ili proširite period."
                : (issuesMetaMessage ?? "Nije bilo prodaje u izabranom periodu.")
          }
          reasons={[
            "U izabranom periodu nema problema koji prolaze prag signalnog prometa.",
            "Scope ili tip problema je suzio rezultat na prazan skup.",
          ]}
          actions={[
            { label: "Proverite drugi tip problema." },
            { label: "Proširite period i osvežite listu." },
            { label: "Pokrenite analytics refresh i pokušajte ponovo." },
          ]}
          dataQualityHref="/analytics/data-quality"
          refreshStatusHref="/admin/configuration?panel=workers"
          emptyReason={issuesMeta?.emptyReason ?? issuesMetaMessage ?? null}
        />
      ) : null}
      {viewMode === "issues" && healthError ? <div className="data-quality-loading">{healthError}</div> : null}
      {viewMode === "issues" && loading ? <div className="data-quality-loading">Učitavam data quality probleme...</div> : null}

      {viewMode === "issues" && !loading && data ? <TopOffendersPanel issueType={issueType} dataScope={contextDataScope} /> : null}

      {viewMode === "issues" && !loading && data && !showEmptyState ? (
        <section className="data-quality-card">
          <div className="data-quality-table-head">
            <div>
              <h2>Problematični artikli</h2>
              <span className="data-quality-table-meta">
                Ukupno: {data.total} | Strana {page} / {totalPages}
              </span>
            </div>

            <AnalyticsTableToolbar
              tableKey={`data-quality-${issueType}`}
              tableTitle={`Data quality - ${issueLabel(issueType)}`}
              columns={analyticsColumns}
              rows={data.items}
              filters={toolbarFilters}
              metadata={toolbarMetadata}
              defaultOrientation="landscape"
            />
          </div>

          <div className="data-quality-table-wrap">
            <table className="data-quality-table">
              <thead>
                <tr>
                  <th>SKU</th>
                  <th>Artikal</th>
                  <th>Dobavljač</th>
                  <th>Tip obuće</th>
                  <th className="align-right">Pogođeni promet 30d</th>
                  <th className="align-right">Stanje</th>
                  <th>Ažurirano</th>
                  <th>Problem</th>
                  <th>Akcija</th>
                </tr>
              </thead>
              <tbody>
                {data.items.length === 0 ? (
                  <tr>
                    <td colSpan={9} className="data-quality-empty">
                      Nema artikala za izabrani data-quality filter.
                    </td>
                  </tr>
                ) : (
                  data.items.map((item) => (
                    <tr key={`${item.issueType}-${item.productId}`}>
                      <td>{item.sku || "-"}</td>
                      <td>
                        <div className="data-quality-name">
                          <strong>{item.name || "Naziv nedostaje"}</strong>
                          <span>ID: {item.productId}</span>
                        </div>
                      </td>
                      <td>{item.supplierName || "-"}</td>
                      <td>{item.shoeTypeName || "-"}</td>
                      <td className="align-right">{fmtRsd(item.sales30d, 2)}</td>
                      <td className="align-right">{fmtNumber(item.stock)}</td>
                      <td>{formatDateTime(item.lastUpdated)}</td>
                      <td>
                        <span className={`data-quality-badge ${rowTone(item.issueType)}`}>
                          {issueLabel(item.issueType)}
                        </span>
                      </td>
                      <td>
                        <Link className="data-quality-action" to={`/artikli/${item.productId}/edit`}>
                          Otvori artikal
                        </Link>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          <div className="data-quality-pagination">
            <button type="button" onClick={() => updateParams({ page: Math.max(1, page - 1) })} disabled={page <= 1}>
              Prethodna
            </button>
            <span>
              {page} / {totalPages}
            </span>
            <button
              type="button"
              onClick={() => updateParams({ page: Math.min(totalPages, page + 1) })}
              disabled={page >= totalPages}
            >
              Sledeca
            </button>
          </div>
        </section>
      ) : null}
    </div>
  );
}



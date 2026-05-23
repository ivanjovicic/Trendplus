import { useCallback, useEffect, useMemo, useState, type FormEvent } from "react";
import { Link, useSearchParams } from "react-router-dom";
import AnalyticsEmptyState from "../components/analytics/AnalyticsEmptyState";
import AnalyticsErrorState from "../components/analytics/AnalyticsErrorState";
import AnalyticsRefreshStatusBanner from "../components/analytics/AnalyticsRefreshStatusBanner";
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import AnalyticsTrustHeader from "../components/analytics/AnalyticsTrustHeader";
import InfoTip from "../components/ui/InfoTip";
import {
  AnalyticsMetaError,
  getAnalyticsRefreshStatus,
  getAnalyticsDataQualityHealth,
  getAnalyticsDataQualityTrend,
  getPilotDataQualityIntakeReport,
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
  PilotDataQualityIntakeReport,
  DataQualityTopOffendersResult,
  DataQualityTrendPoint,
  DataQualityTrendResult,
} from "../types/analytics";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import "./DataQualityPage.css";

const ISSUE_TABS: Array<{ key: DataQualityIssueType; label: string; tone: "danger" | "warning" | "neutral" }> = [
  { key: "missingSupplier", label: "Nedostajuci dobavljac", tone: "danger" },
  { key: "missingShoeType", label: "Nedostajuci tip obuce", tone: "warning" },
];

const LOW_PRIORITY_TABS: Array<{ key: DataQualityIssueType; label: string; tone: "danger" | "warning" | "neutral" }> = [
  { key: "invalidName", label: "Neispravni nazivi", tone: "neutral" },
];

const VIEW_TABS = [
  { key: "issues", label: "Detalji problema" },
  { key: "intake", label: "Pilot intake report" },
] as const;

const analyticsColumns: AnalyticsTableColumn<DataQualityIssueItem>[] = [
  { key: "sku", header: "SKU", dataType: "text" },
  { key: "productId", header: "Artikal ID", dataType: "text" },
  { key: "name", header: "Naziv artikla", dataType: "text" },
  { key: "supplierName", header: "Dobavljac", dataType: "text" },
  { key: "shoeTypeName", header: "Tip obuce", dataType: "text" },
  { key: "sales30d", header: "Pogodjeni promet 30d", dataType: "currency" },
  { key: "stock", header: "Stanje", dataType: "number" },
  { key: "lastUpdated", header: "Azurirano", dataType: "datetime" },
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

function formatCurrency(value: number): string {
  return `${value.toLocaleString("sr-RS", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} RSD`;
}

function formatPercent(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return "N/A";
  return `${value.toLocaleString("sr-RS", { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`;
}

function formatDateTime(value: string): string {
  return new Date(value).toLocaleString("sr-RS");
}

function formatDateOnly(value: string | null): string {
  if (!value) return "-";
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return value;
  return parsed.toLocaleDateString("sr-RS");
}

function formatShortDate(value: string): string {
  return new Date(value).toLocaleDateString("sr-RS", { day: "2-digit", month: "2-digit" });
}

function formatCount(value: number): string {
  return value.toLocaleString("sr-RS");
}

function escapeCsv(value: string | number | null | undefined): string {
  const text = value == null ? "" : String(value);
  if (/[",\n;]/.test(text)) {
    return `"${text.replace(/"/g, '""')}"`;
  }

  return text;
}

function downloadTextFile(fileName: string, content: string, mimeType: string) {
  const blob = new Blob([content], { type: `${mimeType};charset=utf-8` });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}

function buildReportCsv(report: PilotDataQualityIntakeReport): string {
  const rows = [
    ["Field", "Value"],
    ["Readiness status", report.readinessStatus],
    ["Readiness label", report.readinessLabel],
    ["Readiness score", report.readinessScore],
    ["Summary", report.summary],
    ["Last import", report.lastImportAtUtc ?? ""],
    ["Articles", report.loadedData.articleCount],
    ["Suppliers", report.loadedData.supplierCount],
    ["Stores", report.loadedData.storeCount],
    ["Sales receipts", report.loadedData.salesReceiptCount],
    ["Sales lines", report.loadedData.salesLineCount],
    ["Missing supplier", report.issues.missingSupplierCount],
    ["Missing cost", report.issues.missingCostCount],
    ["Missing category", report.issues.missingCategoryCount],
    ["Missing shoe type", report.issues.missingShoeTypeCount],
    ["Missing size", report.issues.missingSizeCount],
    ["Missing color", report.issues.missingColorCount],
    ["Invalid names", report.issues.invalidNameCount],
    ["Duplicate PLU", report.issues.duplicateSkuCount],
  ];

  return rows.map((row) => row.map(escapeCsv).join(",")).join("\n");
}

function buildReportSummary(report: PilotDataQualityIntakeReport): string {
  return [
    `Pilot intake report: ${report.readinessLabel} (${report.readinessScore}/100)`,
    `Last import: ${report.lastImportAtUtc ? formatDateTime(report.lastImportAtUtc) : "-"}`,
    `Loaded articles: ${formatCount(report.loadedData.articleCount)}`,
    `Missing supplier: ${formatCount(report.issues.missingSupplierCount)}`,
    `Missing cost: ${formatCount(report.issues.missingCostCount)}`,
    `Blocked recommendations: ${formatCount(report.issues.blockedRecommendationsCount)}`,
    `Revenue at risk: ${formatCurrency(report.impact.revenueAtRiskRsd)}`,
  ].join("\n");
}

function PilotIntakeReportPanel({
  report,
  loading,
  error,
  onPrint,
  onExportCsv,
  onCopy,
}: {
  report: PilotDataQualityIntakeReport | null;
  loading: boolean;
  error: string | null;
  onPrint: () => void;
  onExportCsv: () => void;
  onCopy: () => void;
}) {
  if (error) {
    return <div className="data-quality-inline-error">{error}</div>;
  }

  if (loading && !report) {
    return <div className="data-quality-loading">Ucitavam pilot intake report...</div>;
  }

  if (!report) {
    return null;
  }

  return (
    <section className="data-quality-intake-report">
      <div className="data-quality-section-head">
        <div>
          <h2>Pilot Data Quality Intake Report</h2>
          <p>{report.summary}</p>
        </div>
        <div className="data-quality-intake-actions">
          <button type="button" onClick={onPrint}>Stampaj</button>
          <button type="button" onClick={onExportCsv}>Izvezi CSV</button>
          <button type="button" onClick={onCopy}>Kopiraj sazetak</button>
        </div>
      </div>

      <div className={`data-quality-score-card ${report.readinessStatus === "Ready" ? "excellent" : report.readinessStatus === "FixDataFirst" ? "critical" : "warning"}`}>
        <span className="data-quality-score-label">Readiness</span>
        <strong>{report.readinessScore}</strong>
        <span className="data-quality-score-status">{report.readinessLabel}</span>
        <p>{report.summary}</p>
      </div>

      <div className="data-quality-health-grid">
        <article className="data-quality-health-card ok">
          <span className="data-quality-health-label">Ucitanо</span>
          <strong>{formatCount(report.loadedData.articleCount)} artikala</strong>
          <p>{formatCount(report.loadedData.supplierCount)} dobavljaca, {formatCount(report.loadedData.storeCount)} objekata</p>
        </article>
        <article className="data-quality-health-card">
          <span className="data-quality-health-label">Prodaja u prozoru</span>
          <strong>{formatCount(report.loadedData.salesReceiptCount)} racuna</strong>
          <p>{formatCount(report.loadedData.salesLineCount)} stavki</p>
        </article>
        <article className="data-quality-health-card">
          <span className="data-quality-health-label">Poslednji import</span>
          <strong>{report.lastImportAtUtc ? formatDateTime(report.lastImportAtUtc) : "-"}</strong>
          <p>{report.loadedData.lastImportSourceFile ?? report.loadedData.lastImportSourcePath ?? "Nema ucitanog batch-a"}</p>
        </article>
        <article className="data-quality-health-card warning">
          <span className="data-quality-health-label">Ignorisani redovi</span>
          <strong>{formatCount(report.loadedData.ignoredRows)}</strong>
          <p>{formatCount(report.loadedData.totalErrors)} total errors</p>
        </article>
      </div>

      <div className="data-quality-intake-grid">
        <section className="data-quality-card">
          <div className="data-quality-section-head">
            <div><h3>Issues</h3><p>Sta nedostaje i sta blokira pilot.</p></div>
          </div>
          <div className="data-quality-intake-list">
            {report.issues.items.map((item) => (
              <article key={item.key} className={`data-quality-intake-chip ${item.severity}`}>
                <strong>{item.label}</strong>
                <span>{formatCount(item.count)}</span>
                <p>{item.impact}</p>
              </article>
            ))}
          </div>
        </section>

        <section className="data-quality-card">
          <div className="data-quality-section-head">
            <div><h3>Uticaj</h3><p>Kako problemi uticu na pouzdanost.</p></div>
          </div>
          <div className="data-quality-intake-list">
            {report.impact.items.map((item) => (
              <article key={item.key} className="data-quality-intake-chip neutral">
                <strong>{item.label}</strong>
                <span>{item.value}</span>
                <p>{item.description}</p>
              </article>
            ))}
          </div>
        </section>

        <section className="data-quality-card">
          <div className="data-quality-section-head">
            <div><h3>Preporucene akcije</h3><p>Sta treba uraditi pre prikaza glavnih dashboarda.</p></div>
          </div>
          <div className="data-quality-report-actions">
            {report.recommendedActions.items.map((action) => (
              <article key={`${action.priority}-${action.title}`}>
                <strong>{action.priority} {action.title}</strong>
                <p>{action.reason}</p>
                <span>{action.nextStep}</span>
              </article>
            ))}
          </div>
        </section>
      </div>
    </section>
  );
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
          <p>Rangirano po pogodjenom prometu unutar aktivnog tipa problema.</p>
        </div>
        <span className="data-quality-top-offenders-meta">Top {result.count}</span>
      </div>

      <div className="data-quality-table-wrap">
        <table className="data-quality-table data-quality-table-compact">
          <thead>
            <tr>
              <th>SKU</th>
              <th>Artikal</th>
              <th>Dobavljac</th>
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
                <td className="align-right">{formatCurrency(item.sales30d)}</td>
                <td className="align-right">{formatCurrency(item.revenueImpactRsd)}</td>
                <td className="align-right">{formatPercent(item.revenueImpactPct)}</td>
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
          <span key={point.date}>{formatShortDate(point.date)}</span>
        ))}
      </div>
    </section>
  );
}

export default function DataQualityPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [data, setData] = useState<DataQualityIssueListResult | null>(null);
  const [health, setHealth] = useState<AnalyticsDataQualityHealth | null>(null);
  const [intakeReport, setIntakeReport] = useState<PilotDataQualityIntakeReport | null>(null);
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

    const [issuesResult, healthResult, refreshResult, intakeResult] = await Promise.allSettled([
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
          : "Status osvezavanja analitike nije dostupan."
      );
    }

    if (intakeResult.status === "fulfilled") {
      setIntakeReport(intakeResult.value);
      setIntakeReportError(null);
    } else {
      setIntakeReport(null);
      setIntakeReportError(
        intakeResult.reason instanceof Error
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
        value: `${formatDateOnly(contextFromDate)} - ${formatDateOnly(contextToDate)}`,
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
  const trustDataQualityStatus = intakeReport
    ? intakeReport.readinessStatus === "FixDataFirst"
      ? "critical"
      : intakeReport.readinessStatus === "Ready"
        ? "good"
        : "warning"
    : health?.scoreStatus === "critical"
      ? "critical"
      : health?.scoreStatus === "warning"
        ? "warning"
        : health?.scoreStatus === "good" || health?.scoreStatus === "excellent"
          ? "good"
          : health?.meta?.dataQualityStatus ?? null;

  const trustSummary = intakeReport
    ? {
        missingSupplierCount: intakeReport.issues.missingSupplierCount,
        missingCostCount: intakeReport.issues.missingCostCount,
        missingCategoryCount: intakeReport.issues.missingCategoryCount,
        insufficientSignalCount: intakeReport.issues.blockedRecommendationsCount,
        ignoredRowsCount: intakeReport.loadedData.ignoredRows,
      }
    : {
        missingSupplierCount: health?.orphanArticleCount ?? null,
      };

  const reportClipboardText = intakeReport ? buildReportSummary(intakeReport) : "";

  const handlePrintReport = () => window.print();
  const handleExportReportCsv = () => {
    if (!intakeReport) return;
    downloadTextFile(`pilot-data-quality-intake-${intakeReport.generatedAtUtc.slice(0, 10)}.csv`, buildReportCsv(intakeReport), "text/csv");
  };
  const handleCopyReport = async () => {
    if (!reportClipboardText) return;
    await navigator.clipboard.writeText(reportClipboardText);
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
        dataSource={contextDataScope ? `Data quality (${contextDataScope})` : "Data quality read model"}
        dataQualityStatus={trustDataQualityStatus}
        dataQualitySummary={trustSummary}
        mode="report"
        emptyStateReason={!loading && !error && data?.items.length === 0 ? (data?.meta?.message ?? "Nema otvorenih data quality problema za izabrani filter.") : null}
        methodologyHref="/analytics/data-quality"
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
            </section>
          ) : null}
          <div className="data-quality-meta">
            <span>Signal filter: samo artikli sa vise od 1.000 RSD prometa u 30 dana</span>
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
        <PilotIntakeReportPanel
          report={intakeReport}
          loading={loading}
          error={intakeReportError}
          onPrint={handlePrintReport}
          onExportCsv={handleExportReportCsv}
          onCopy={handleCopyReport}
        />
      ) : null}

      {!loading && !error && data?.meta?.isPartial ? (
        <div className="data-quality-loading" role="status">
          Prikazani podaci su delimični. {data.meta.warningMessage ?? data.meta.message ?? "Proverite analytics refresh status."}
        </div>
      ) : null}

      {health ? (
        <section className="data-quality-health-grid">
          <article className={`data-quality-health-card ${healthStatus.tone}`}>
            <span className="data-quality-health-label">Health status</span>
            <strong>{healthStatus.label}</strong>
            <p>
              Prozor: {formatDateTime(health.windowFrom)} - {formatDateTime(health.windowTo)} | Lookback {health.lookbackDays} dana
            </p>
          </article>

          <article className="data-quality-health-card">
            <span className="data-quality-health-label">Artikli bez dobavljaca</span>
            <strong>{health.orphanArticleCount.toLocaleString("sr-RS")}</strong>
            <p>Warning threshold: {health.thresholds.orphanArticleCount}</p>
          </article>

          <article className="data-quality-health-card">
            <span className="data-quality-health-label">Promet bez nabavne cene</span>
            <strong>{formatPercent(health.missingCostRevenueSharePct)}</strong>
            <p>{formatCurrency(health.missingCostRevenue)} bez pouzdane marze</p>
          </article>

          <article className="data-quality-health-card">
            <span className="data-quality-health-label">Promet nepoznatog dobavljaca</span>
            <strong>{formatPercent(health.unknownSupplierRevenueSharePct)}</strong>
            <p>{formatCurrency(health.unknownSupplierRevenue)} u unknown bucket-u</p>
          </article>
        </section>
      ) : null}

      <DataQualityTrendChart dataScope={contextDataScope} />

      {health ? (
        <section className="data-quality-quick-actions">
          <button type="button" onClick={() => changeTab("missingSupplier")}>
            Artikli bez dobavljaca
          </button>
          <Link to={`/analytics/supplier${supplierContextQuery ? `?tab=overview&${supplierContextQuery}` : "?tab=overview"}`}>
            Otvori supplier analitiku
          </Link>
          {returnTo ? <Link to={returnTo}>Nazad na izvorni kontekst</Link> : null}
        </section>
      ) : null}

      <div className="data-quality-tabs" role="tablist" aria-label="Data quality issue tabs">
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
      </div>

      <details className="data-quality-low-priority" open={issueType === "invalidName"}>
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
      </details>

      <section className="data-quality-controls">
        <form className="data-quality-search" onSubmit={submitSearch}>
          <input
            type="search"
            value={searchDraft}
            onChange={(event) => setSearchDraft(event.target.value)}
            placeholder="Pretraga po SKU, artiklu, dobavljacu, tipu..."
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
      </section>

      {originTable ? (
        <div className="data-quality-origin">
          <div className="data-quality-origin-main">
            <span>Otvoreno iz analytics tabele: <strong>{originTable}</strong></span>
            {(contextFromDate || contextToDate) ? (
              <span>Kontekst perioda: <strong>{formatDateOnly(contextFromDate)} - {formatDateOnly(contextToDate)}</strong></span>
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

      {error ? (
        <AnalyticsErrorState
          title="Data quality podaci trenutno nisu dostupni"
          message={error.message}
          errorCode={error.errorCode ?? undefined}
          correlationId={error.correlationId ?? undefined}
          suggestions={[
            "Proverite konekciju sa analytics bazom i pokrenite refresh.",
            "Probajte ponovo za nekoliko trenutaka.",
          ]}
          onRetry={() => {
            void load();
          }}
          helpHref="/analytics/data-quality"
        />
      ) : null}

      {!loading && !error && data?.items.length === 0 && data?.meta?.dataQualityStatus === "insufficient_data" ? (
        <AnalyticsEmptyState
          variant="insufficient_data"
          message={data.meta.message ?? "Nema otvorenih data quality problema u izabranom opsegu."}
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
        />
      ) : null}
      {healthError ? <div className="data-quality-loading">{healthError}</div> : null}
      {loading ? <div className="data-quality-loading">Ucitavam data quality probleme...</div> : null}

      {!loading && data ? <TopOffendersPanel issueType={issueType} dataScope={contextDataScope} /> : null}

      {!loading && data ? (
        <section className="data-quality-card">
          <div className="data-quality-table-head">
            <div>
              <h2>Problematicni artikli</h2>
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
                  <th>Dobavljac</th>
                  <th>Tip obuce</th>
                  <th className="align-right">Pogodjeni promet 30d</th>
                  <th className="align-right">Stanje</th>
                  <th>Azurirano</th>
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
                      <td className="align-right">{formatCurrency(item.sales30d)}</td>
                      <td className="align-right">{item.stock.toLocaleString("sr-RS")}</td>
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

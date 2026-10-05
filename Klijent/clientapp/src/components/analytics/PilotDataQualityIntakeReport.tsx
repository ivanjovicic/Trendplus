import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import type { AnalyticsNamedValue, ResolvedAnalyticsTablePayload } from "../../types/analyticsTable";
import type {
  AnalyticsRefreshStatus,
  PilotDataQualityIntakeReport,
  PilotIntakeDurableReport,
} from "../../types/analytics";
import { downloadExport, generateExport, waitForExport } from "../../services/exportApi";
import {
  findAnalyticsMetricKeyByLabel,
  type AnalyticsMetricKey,
} from "../../utils/analyticsMetricDefinitions";
import {
  formatPilotImpactPercentage,
  getPilotImportScopeLabel,
  getPilotReadinessStatusLabel,
  resolvePilotIntakeImpact,
} from "../../utils/pilotImportReadiness";
import {
  fmtNumber,
  fmtPct,
  fmtPctFromRatio,
  fmtRsd,
  formatDate,
  formatDateTime,
} from "../../utils/analyticsFormatters";
import { isAnalyticsMetaEmpty, isAnalyticsMetaError, isAnalyticsMetaWarning } from "../../utils/analyticsResponseMeta";
import AnalyticsEmptyState from "./AnalyticsEmptyState";
import AnalyticsErrorState from "./AnalyticsErrorState";
import MetricMethodologyPanel from "./MetricMethodologyPanel";
import {
  buildCsv,
  buildDurableCsv,
  buildExportPayload,
  buildPilotCsvFilename,
  buildSummary,
  impactSignalState,
  issueSignalState,
  resolveSignalCoverage,
  type TrustSignalState,
} from "./pilotDataQualityIntakeReportHelpers";
import PilotImportReadinessCard from "./PilotImportReadinessCard";
import "./PilotDataQualityIntakeReport.css";

type Props = {
  report: PilotDataQualityIntakeReport | null;
  loading: boolean;
  error: string | null;
  filters: AnalyticsNamedValue[];
  durableReport?: PilotIntakeDurableReport | null;
  refreshStatus?: AnalyticsRefreshStatus | null;
  onRetry: () => void;
};

function readinessTone(status: string): "excellent" | "good" | "warning" | "critical" {
  if (status === "excellent") return "excellent";
  if (status === "good") return "good";
  if (status === "warning") return "warning";
  return "critical";
}

function mapActionHref(action: string): string {
  const normalized = action.toLowerCase();
  if (normalized.includes("dobavlj")) return "/analytics/supplier";
  if (normalized.includes("cena") || normalized.includes("kategor") || normalized.includes("map")) return "/analytics/data-quality";
  if (normalized.includes("osvez")) return "/admin/configuration?panel=workers";
  return "/analytics/data-quality";
}

function durableMethodologySummary(report: PilotIntakeDurableReport): string {
  if (typeof report.methodology === "string") return report.methodology;
  return report.methodologySummary ?? report.methodology.summary;
}

function durableKpiNumber(value: unknown): number | null {
  if (typeof value === "number") return Number.isFinite(value) ? value : null;
  if (typeof value !== "string" || !value.trim()) return null;
  const parsed = Number(value.replace(/\s/g, "").replace(",", "."));
  return Number.isFinite(parsed) ? parsed : null;
}

function formatDurableKpiValue(kpi: NonNullable<PilotIntakeDurableReport["kpis"]>[number]): string {
  if (["error", "insufficient_data", "stale"].includes(kpi.valueStatus ?? "")) {
    return kpi.valueReason ?? "Nije dostupno";
  }
  const value = durableKpiNumber(kpi.value);
  if (value == null) return kpi.valueReason ?? "Nije dostupno";
  if (kpi.unit === "ratio") return fmtPctFromRatio(value, 1, "Nije dostupno");
  if (kpi.unit === "%") return fmtPct(value, 1, "Nije dostupno");
  if (kpi.unit === "/100") return `${fmtNumber(value, 0, "Nije dostupno")}/100`;
  if (kpi.unit?.toUpperCase() === "RSD") return fmtRsd(value, 0, "Nije dostupno");
  return fmtNumber(value, 0, "Nije dostupno");
}

function formatDurableCell(value: unknown, dataType?: string): string {
  if (value == null || value === "") return "Nije dostupno";
  if (dataType === "date") return formatDate(String(value), "Nije dostupno");
  if (dataType === "datetime") return formatDateTime(String(value), "Nije dostupno");
  if (dataType === "number" || dataType === "currency") {
    const numeric = durableKpiNumber(value);
    return numeric == null ? "Nije dostupno" : dataType === "currency" ? fmtRsd(numeric, 0, "Nije dostupno") : fmtNumber(numeric, 0, "Nije dostupno");
  }
  if (typeof value === "boolean") return value ? "Da" : "Ne";
  return String(value);
}

function durableFreshnessLabel(value: string | null | undefined): string {
  switch (value?.trim().toLowerCase()) {
    case "fresh": return "Sveže";
    case "stale": return "Zastarelo";
    case "critical": return "Kritično";
    case "warning": return "Oprez";
    default: return "Nije dostupna";
  }
}

function durableReportIsEmpty(report: PilotIntakeDurableReport): boolean {
  if (isAnalyticsMetaEmpty(report.meta)) return true;
  return (report.kpis?.length ?? 0) === 0 && report.sections.some((section) => section.key === "report-status");
}

function toResolvedPayload(report: PilotIntakeDurableReport): ResolvedAnalyticsTablePayload {
  return {
    tableKey: report.payload.tableKey,
    tableTitle: report.payload.tableTitle,
    documentType: report.payload.documentType,
    templateName: report.payload.templateName,
    templateVersion: report.payload.templateVersion,
    locale: report.payload.locale,
    columns: report.payload.columns,
    rows: report.payload.rows.map((row) => ({
      section: row.section,
      item: row.item,
      value: row.value,
      secondary: row.secondary,
      note: row.note,
    })),
    filters: report.payload.filters,
    metadata: report.payload.metadata,
  };
}

function buildDurableSummary(report: PilotIntakeDurableReport): string {
  const kpis = (report.kpis ?? []).map((kpi) => `${kpi.label}: ${formatDurableKpiValue(kpi)}`);
  const actions = (report.recommendedActions ?? []).map((action) => action.title);
  return [
    report.reportTitle ?? report.title ?? "Trendplus pilot izveštaj kvaliteta podataka",
    ...kpis,
    actions.length > 0 ? `Preporučene akcije: ${actions.join("; ")}` : null,
  ].filter((line): line is string => Boolean(line)).join("\n");
}

type DurableReportContentProps = {
  report: PilotIntakeDurableReport;
  exportBusy: boolean;
  exportStatus: string | null;
  onTextExport: () => void;
  onServerExport: (format: "pdf" | "xlsx" | "csv") => void;
  onCopy: () => void;
};

function DurableReportContent({ report, exportBusy, exportStatus, onTextExport, onServerExport, onCopy }: DurableReportContentProps) {
  const kpis = report.kpis ?? [];
  const actions = report.recommendedActions ?? [];
  const warnings = report.warnings ?? [];

  return (
    <section className="pilot-intake-card tone-warning" data-testid="pilot-durable-report">
      <div className="pilot-intake-head">
        <div>
          <h2>{report.reportTitle ?? report.title ?? "Pilot izveštaj"}</h2>
          <p>Trajni izveštaj kvaliteta podataka iz backend izvora.</p>
        </div>
        <div className="pilot-intake-score">
          <span>{report.recommendationAllowed ? "Preporuke dozvoljene" : "Preporuke ograničene"}</span>
          <strong>{getPilotReadinessStatusLabel(report.dataQualityStatus)}</strong>
        </div>
      </div>

      {warnings.length > 0 ? (
        <div className="pilot-intake-warning" role="status">
          {warnings.join(" · ")}
        </div>
      ) : null}

      {kpis.length > 0 ? (
        <div className="pilot-intake-grid" data-testid="pilot-durable-kpis">
          {kpis.map((kpi) => (
            <article key={kpi.key}>
              <span>{kpi.label}</span>
              <strong>{formatDurableKpiValue(kpi)}</strong>
              {kpi.note || kpi.valueReason ? <p>{kpi.note ?? kpi.valueReason}</p> : null}
            </article>
          ))}
        </div>
      ) : null}

      <div className="pilot-intake-meta">
        <span>Period: {formatDate(report.periodFrom ?? report.period?.fromUtc, "Nije dostupan")} - {formatDate(report.periodTo ?? report.period?.toUtc, "Nije dostupan")}</span>
        <span>Opseg podataka: {getPilotImportScopeLabel(report.period?.scope)}</span>
        <span>Generisano: {formatDateTime(report.generatedAtUtc, "Nije dostupno")}</span>
        <span>Svežina: {durableFreshnessLabel(report.dataFreshnessStatus)}</span>
      </div>

      <div className="pilot-intake-durable-sections" data-testid="pilot-durable-sections">
        {report.sections.map((section) => {
          const rows: Array<Record<string, unknown>> = section.rows ?? report.rows
            .filter((row) => row.section === section.key)
            .map((row) => ({ item: row.item, value: row.value, note: row.note }));
          const columns: Array<{ key: string; label: string; dataType?: string }> = section.columns ?? (rows.length > 0
            ? Object.keys(rows[0]).map((key) => ({ key, label: key }))
            : []);

          return (
            <article key={section.key} className="pilot-intake-durable-section">
              <h3>{section.title ?? section.key}</h3>
              {section.description ? <p>{section.description}</p> : null}
              {rows.length > 0 && columns.length > 0 ? (
                <>
                  <p className="pilot-intake-durable-table-hint" role="note">
                    Tabela se pomera vodoravno — skrolujte za ostale kolone.
                  </p>
                  <div
                    className="pilot-intake-durable-table-wrap"
                    role="region"
                    tabIndex={0}
                    aria-label={`${section.title ?? section.key}: tabela sa vodoravnim pomeranjem`}
                  >
                  <table className="pilot-intake-durable-table">
                    <thead><tr>{columns.map((column) => <th key={column.key}>{column.label}</th>)}</tr></thead>
                    <tbody>
                      {rows.map((row, rowIndex) => (
                        <tr key={`${section.key}-${rowIndex}`}>
                          {columns.map((column) => (
                            <td
                              key={column.key}
                              className={["number", "currency", "percent", "ratio"].includes(column.dataType ?? "")
                                ? "pilot-intake-durable-table__numeric"
                                : undefined}
                            >
                              {formatDurableCell(row[column.key], column.dataType)}
                            </td>
                          ))}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                  </div>
                </>
              ) : <p className="pilot-card-note">{section.emptyMessage ?? "Nema stavki za ovaj opseg."}</p>}
            </article>
          );
        })}
      </div>

      <div className="pilot-intake-actions" data-testid="pilot-durable-actions">
        {actions.length > 0 ? actions.map((action) => (
          <div key={`${action.title}-${action.href}`} className="pilot-intake-action-row">
            <Link to={action.href || "/analytics/data-quality"}>{action.title}</Link>
            <span>{action.description}</span>
          </div>
        )) : <p className="pilot-card-note">Nema preporučenih akcija za traženi opseg.</p>}
      </div>

      <div className="pilot-intake-export">
        <button type="button" onClick={onTextExport}>Preuzmi CSV</button>
        <button type="button" disabled={exportBusy} onClick={() => onServerExport("pdf")}>PDF</button>
        <button type="button" disabled={exportBusy} onClick={() => onServerExport("xlsx")}>XLSX</button>
        <button type="button" onClick={onCopy}>Kopiraj sažetak</button>
        {exportStatus ? <span>{exportStatus}</span> : null}
      </div>
    </section>
  );
}

function signalStateLabel(state: TrustSignalState): string {
  if (state === "issues") return "Potrebna korekcija";
  if (state === "partial") return "Nedovoljno potvrđeno";
  return "Bez otvorenih signala";
}

function signalStateTone(state: TrustSignalState): string {
  if (state === "issues") return "warning";
  if (state === "partial") return "partial";
  return "clear";
}

export default function PilotDataQualityIntakeReportPanel({ report, loading, error, filters, durableReport, refreshStatus, onRetry }: Props) {
  const [exportBusy, setExportBusy] = useState(false);
  const [exportStatus, setExportStatus] = useState<string | null>(null);
  const [methodologyKey, setMethodologyKey] = useState<AnalyticsMetricKey | null>(null);

  const durableSummary = durableReport ? durableMethodologySummary(durableReport) : null;
  const durableWarnings = durableReport?.warnings ?? [];
  const durableGeneratedAt = durableReport?.generatedAtUtc ?? report?.generatedAtUtc ?? null;
  const generatedAtLabel = durableGeneratedAt ? formatDateTime(durableGeneratedAt, "Nije dostupno") : "Nije dostupno";
  const metaWarning = isAnalyticsMetaWarning(report?.meta) || isAnalyticsMetaWarning(durableReport?.meta);

  const reportText = useMemo(() => {
    if (report) return buildSummary(report);
    return durableReport ? buildDurableSummary(durableReport) : "";
  }, [durableReport, report]);
  const exportPayload = useMemo(() => {
    if (report) return buildExportPayload(report, filters);
    return durableReport ? toResolvedPayload(durableReport) : null;
  }, [durableReport, filters, report]);

  async function runTextExport() {
    if (!report && !durableReport) return;
    const csv = report ? buildCsv(report) : buildDurableCsv(durableReport!);
    const blob = new Blob([csv], { type: "text/csv;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = buildPilotCsvFilename(report, durableReport);
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
  }

  async function runServerExport(format: "pdf" | "xlsx" | "csv") {
    if (!exportPayload || exportBusy) return;
    try {
      setExportBusy(true);
      setExportStatus("Server priprema dokument...");
      const result = await generateExport(exportPayload, {
        format,
        orientation: "portrait",
        includeFiltersAndMetadata: true,
      });
      if (result.isAsync) {
        setExportStatus("Dokument je u redu čekanja...");
        const completed = await waitForExport(result.documentId);
        if (completed.downloadUrl) downloadExport(completed.downloadUrl, completed.fileName);
      } else if (result.downloadUrl) {
        downloadExport(result.downloadUrl, result.fileName);
      }
      setExportStatus("Eksport je spreman.");
    } catch (reason) {
      setExportStatus(reason instanceof Error ? reason.message : "Eksport nije uspeo.");
    } finally {
      setExportBusy(false);
    }
  }

  if (loading) {
    return <div className="pilot-intake-loading">Učitavam pilot izveštaj…</div>;
  }

  if (error) {
    return (
      <AnalyticsErrorState
        title="Pilot izveštaj nije dostupan"
        message={error}
        onRetry={onRetry}
        helpHref="/admin/configuration?panel=workers"
      />
    );
  }

  if (durableReport && isAnalyticsMetaError(durableReport.meta)) {
    return (
      <AnalyticsErrorState
        title="Pilot izveštaj nije dostupan"
        message={durableReport.meta?.errorMessage ?? durableReport.meta?.message ?? "Pilot izveštaj nije dostupan."}
        onRetry={onRetry}
        helpHref="/analytics/data-quality"
      />
    );
  }

  if (durableReport && durableReportIsEmpty(durableReport)) {
    return (
      <AnalyticsEmptyState
        variant="insufficient_data"
        title="Pilot izveštaj nema dovoljno podataka"
        message={durableReport.meta?.message ?? "Nema dovoljno učitanih podataka da bi se izračunao skor spremnosti."}
        reasons={durableReport.meta?.emptyReason ? [durableReport.meta.emptyReason] : ["Nema import batch-a ili prodajnih redova u izabranom periodu."]}
      />
    );
  }

  if (!report && durableReport) {
    return (
      <DurableReportContent
        report={durableReport}
        exportBusy={exportBusy}
        exportStatus={exportStatus}
        onTextExport={() => void runTextExport()}
        onServerExport={(format) => void runServerExport(format)}
        onCopy={() => void navigator.clipboard?.writeText(reportText)}
      />
    );
  }

  if (!report || isAnalyticsMetaEmpty(report.meta)) {
    return (
      <AnalyticsEmptyState
        variant="insufficient_data"
        title="Pilot izveštaj nema dovoljno podataka"
        message={report?.meta?.message ?? "Nema dovoljno učitanih podataka da bi se izračunao skor spremnosti."}
        reasons={["Nema import batch-a ili prodajnih redova u izabranom periodu."]}
      />
    );
  }

  const tone = readinessTone(report.readinessStatus);
  const issueState = issueSignalState(report);
  const impactState = impactSignalState(report);
  const impact = resolvePilotIntakeImpact(report);

  return (
    <section className={`pilot-intake-card tone-${tone}`}>
      <div className="pilot-intake-head">
        <div>
          <h2>Pilot izveštaj</h2>
          <p>Spremnost podataka za bezbedne preporuke, uz jasno označene rupe u katalogu i signalu.</p>
        </div>
        <div className="pilot-intake-score">
          <span>{getPilotReadinessStatusLabel(report.readinessStatus)}</span>
          <strong>{report.readinessScore}/100</strong>
        </div>
      </div>

      <PilotImportReadinessCard report={report} refreshStatus={refreshStatus} />

      {metaWarning ? (
        <div className="pilot-intake-warning" role="status">
          {report.meta?.message ?? durableReport?.meta?.message ?? "Izveštaj ima upozorenja kvaliteta podataka."}
        </div>
      ) : null}

      {durableReport ? (
        <div className="pilot-intake-durable-note">
          <strong>Trajni izveštaj:</strong> {durableReport.reportTitle ?? durableReport.title ?? "Pilot izveštaj"} · {generatedAtLabel}
          {durableWarnings.length > 0 ? <span> · {durableWarnings.length} upozorenja</span> : null}
          <p>{durableSummary}</p>
        </div>
      ) : null}

      <div className="pilot-intake-grid">
        <article>
          <span>Učitano</span>
          <strong>{fmtNumber(report.loadedData.articlesCount, 0, "-")} artikala</strong>
          <p>{fmtNumber(report.loadedData.saleItemsCount, 0, "-")} stavki prodaje · {fmtNumber(report.loadedData.receiptsCount, 0, "-")} računa</p>
        </article>
        <article className={`state-${signalStateTone(issueState)}`}>
          <span>Problemi podataka</span>
          <strong>{signalStateLabel(issueState)}</strong>
          <p>Dobavljač {fmtNumber(report.issues.missingSupplierCount, 0, "-")} · cena {fmtNumber(report.issues.missingCostCount, 0, "-")} · kategorija {fmtNumber(report.issues.missingCategoryCount, 0, "-")}</p>
        </article>
        <article className={`state-${signalStateTone(impactState)}`}>
          <span>Uticaj na preporuke</span>
          <strong>{signalStateLabel(impactState)}</strong>
          <p>{formatPilotImpactPercentage(impact.revenueWithoutCost)} prihoda bez cene · {formatPilotImpactPercentage(impact.articlesWithoutSupplier)} artikala bez dobavljača · {fmtNumber(report.impact.recommendationsBlockedCount, 0, "-")} blokiranih artikala</p>
        </article>
      </div>

      <div className="pilot-intake-meta">
        <span>Period: {formatDate(report.periodFromUtc)} - {formatDate(report.periodToUtc)}</span>
        <span>Pokrivenost poslovnim signalom: {resolveSignalCoverage(report) == null ? "nije dostupna" : fmtPctFromRatio(resolveSignalCoverage(report), 1, "nije dostupna")}</span>
        <span>Anchor perioda: {report.periodAnchorCode ?? "nije potvrđen"}</span>
        <span>Opseg podataka: {getPilotImportScopeLabel(report.dataScope)}</span>
        <span>Uvoz: {formatDateTime(report.lastImportAtUtc, "Nije dostupan")}</span>
        <span>Osvežavanje: {formatDateTime(report.lastRefreshAtUtc, "Nije dostupan")}</span>
      </div>

      {report.periodAnchorMessage ? (
        <div className="pilot-intake-warning" role="status">
          {report.periodAnchorMessage}
        </div>
      ) : null}

      <div className="pilot-intake-actions">
        {report.recommendedActions.map((action) => {
          const metricKey = findAnalyticsMetricKeyByLabel(action);
          return (
            <div key={action} className="pilot-intake-action-row">
              <Link to={mapActionHref(action)}>{action}</Link>
              {metricKey ? (
                <button type="button" onClick={() => setMethodologyKey(metricKey)}>
                  Kako se meri?
                </button>
              ) : null}
            </div>
          );
        })}
      </div>

      <div className="pilot-intake-export">
        <button type="button" onClick={runTextExport}>Preuzmi CSV</button>
        <button type="button" disabled={exportBusy || !exportPayload} onClick={() => void runServerExport("pdf")}>PDF</button>
        <button type="button" disabled={exportBusy || !exportPayload} onClick={() => void runServerExport("xlsx")}>XLSX</button>
        <button type="button" disabled={exportBusy || !exportPayload} onClick={() => void navigator.clipboard?.writeText(reportText)}>Kopiraj sažetak</button>
        {exportStatus ? <span>{exportStatus}</span> : null}
      </div>

      {methodologyKey ? (
        <MetricMethodologyPanel
          metricKey={methodologyKey}
          onClose={() => setMethodologyKey(null)}
        />
      ) : (
        <MetricMethodologyPanel metricKeys={["dataReadinessScore"]} />
      )}
    </section>
  );
}

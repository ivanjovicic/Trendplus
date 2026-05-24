import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import type { AnalyticsNamedValue } from "../../types/analyticsTable";
import type { PilotDataQualityIntakeReport, PilotIntakeDurableReport } from "../../types/analytics";
import { resolveAnalyticsTablePayload } from "../../services/analyticsTableState";
import { downloadExport, generateExport, waitForExport } from "../../services/exportApi";
import AnalyticsEmptyState from "./AnalyticsEmptyState";
import AnalyticsErrorState from "./AnalyticsErrorState";
import "./PilotDataQualityIntakeReport.css";

type Props = {
  report: PilotDataQualityIntakeReport | null;
  loading: boolean;
  error: string | null;
  filters: AnalyticsNamedValue[];
  durableReport?: PilotIntakeDurableReport | null;
  onRetry: () => void;
};

function formatDateTime(value: string | null | undefined): string {
  if (!value) return "-";
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return value;
  return parsed.toLocaleString("sr-RS");
}

function formatDate(value: string | null | undefined): string {
  if (!value) return "-";
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return value;
  return parsed.toLocaleDateString("sr-RS");
}

function formatNumber(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return "-";
  return value.toLocaleString("sr-RS");
}

function formatPercentFromRatio(value: number | null | undefined, digits = 1): string {
  if (value == null || Number.isNaN(value)) return "-";
  return `${(value * 100).toLocaleString("sr-RS", { minimumFractionDigits: digits, maximumFractionDigits: digits })}%`;
}

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

function buildCsv(report: PilotDataQualityIntakeReport): string {
  const rows = [
    ["Sekcija", "Stavka", "Vrednost"],
    ["Skor", "Readiness status", report.readinessStatus],
    ["Skor", "Readiness label", report.readinessLabel],
    ["Skor", "Readiness score", String(report.readinessScore)],
    ["Ucitano", "Artikli", String(report.loadedData.articlesCount)],
    ["Ucitano", "Stavke prodaje", String(report.loadedData.saleItemsCount)],
    ["Ucitano", "Racuni", String(report.loadedData.receiptsCount)],
    ["Ucitano", "Dobavljači", String(report.loadedData.suppliersCount)],
    ["Ucitano", "Prodajni objekti", String(report.loadedData.storesCount)],
    ["Ucitano", "Prva prodaja", report.loadedData.firstSaleDate ?? ""],
    ["Ucitano", "Poslednja prodaja", report.loadedData.lastSaleDate ?? ""],
    ["Problemi", "Bez dobavljača", String(report.issues.missingSupplierCount)],
    ["Problemi", "Bez nabavne cene", String(report.issues.missingCostCount)],
    ["Problemi", "Bez kategorije", String(report.issues.missingCategoryCount)],
    ["Problemi", "Bez boje", String(report.issues.missingColorCount ?? 0)],
    ["Problemi", "Bez velicine", String(report.issues.missingSizeCount ?? 0)],
    ["Problemi", "Prodaja bez artikla", String(report.issues.saleWithoutArticleCount)],
    ["Problemi", "Nulta/negativna cena", String(report.issues.zeroOrNegativePriceCount)],
    ["Problemi", "Dupliran SKU", String(report.issues.duplicateSkuCount ?? 0)],
    ["Problemi", "Dobavljač bez naziva", String(report.issues.missingSupplierNameCount)],
    ["Uticaj", "Prihod bez cene", String(report.impact.revenueWithoutCostPercent)],
    ["Uticaj", "Artikli bez dobavljača", String(report.impact.articlesWithoutSupplierPercent)],
    ["Uticaj", "Blokirane preporuke", String(report.impact.recommendationsBlockedCount)],
    ["Uticaj", "Ignorisani redovi", String(report.impact.ignoredRowsCount)],
    ["Uticaj", "Nedovoljni signali", String(report.impact.insufficientSignalCount)],
  ];

  for (const action of report.recommendedActions) {
    rows.push(["Akcije", "Preporucena akcija", action]);
  }

  return rows
    .map((row) => row.map((value) => {
      if (/[",\n;]/.test(value)) return `"${value.replace(/"/g, '""')}"`;
      return value;
    }).join(","))
    .join("\n");
}

function buildSummary(report: PilotDataQualityIntakeReport): string {
  return [
    `Trendplus pilot izveštaj kvaliteta podataka`,
    `Skor spremnosti: ${report.readinessLabel} (${report.readinessScore}/100)`,
    `Ucitano: ${formatNumber(report.loadedData.articlesCount)} artikala, ${formatNumber(report.loadedData.saleItemsCount)} stavki prodaje, ${formatNumber(report.loadedData.receiptsCount)} računa`,
    `Top problemi: bez dobavljača ${formatNumber(report.issues.missingSupplierCount)}, bez nabavne cene ${formatNumber(report.issues.missingCostCount)}, bez kategorije ${formatNumber(report.issues.missingCategoryCount)}`,
    `Uticaj: prihod bez cene ${formatPercentFromRatio(report.impact.revenueWithoutCostPercent)}, artikli bez dobavljača ${formatPercentFromRatio(report.impact.articlesWithoutSupplierPercent)}, blokirane preporuke ${formatNumber(report.impact.recommendationsBlockedCount)}`,
    `Preporucene akcije: ${report.recommendedActions.join("; ")}`,
  ].join("\n");
}

function buildExportPayload(report: PilotDataQualityIntakeReport, filters: AnalyticsNamedValue[]) {
  const rows: Array<{ section: string; item: string; value: string }> = [
    { section: "Skor", item: "Readiness status", value: report.readinessStatus },
    { section: "Skor", item: "Readiness label", value: report.readinessLabel },
    { section: "Skor", item: "Readiness score", value: String(report.readinessScore) },
    { section: "Ucitano", item: "Artikli", value: String(report.loadedData.articlesCount) },
    { section: "Ucitano", item: "Stavke prodaje", value: String(report.loadedData.saleItemsCount) },
    { section: "Ucitano", item: "Racuni", value: String(report.loadedData.receiptsCount) },
    { section: "Ucitano", item: "Dobavljači", value: String(report.loadedData.suppliersCount) },
    { section: "Ucitano", item: "Prodajni objekti", value: String(report.loadedData.storesCount) },
    { section: "Ucitano", item: "Prva prodaja", value: report.loadedData.firstSaleDate ?? "-" },
    { section: "Ucitano", item: "Poslednja prodaja", value: report.loadedData.lastSaleDate ?? "-" },
    { section: "Problemi", item: "Bez dobavljača", value: String(report.issues.missingSupplierCount) },
    { section: "Problemi", item: "Bez nabavne cene", value: String(report.issues.missingCostCount) },
    { section: "Problemi", item: "Bez kategorije", value: String(report.issues.missingCategoryCount) },
    { section: "Problemi", item: "Bez boje", value: String(report.issues.missingColorCount ?? 0) },
    { section: "Problemi", item: "Bez velicine", value: String(report.issues.missingSizeCount ?? 0) },
    { section: "Problemi", item: "Prodaja bez artikla", value: String(report.issues.saleWithoutArticleCount) },
    { section: "Problemi", item: "Nulta/negativna cena", value: String(report.issues.zeroOrNegativePriceCount) },
    { section: "Problemi", item: "Dupliran SKU", value: String(report.issues.duplicateSkuCount ?? 0) },
    { section: "Problemi", item: "Dobavljač bez naziva", value: String(report.issues.missingSupplierNameCount) },
    { section: "Uticaj", item: "Prihod bez cene", value: formatPercentFromRatio(report.impact.revenueWithoutCostPercent) },
    { section: "Uticaj", item: "Artikli bez dobavljača", value: formatPercentFromRatio(report.impact.articlesWithoutSupplierPercent) },
    { section: "Uticaj", item: "Blokirane preporuke", value: String(report.impact.recommendationsBlockedCount) },
    { section: "Uticaj", item: "Ignorisani redovi", value: String(report.impact.ignoredRowsCount) },
    { section: "Uticaj", item: "Nedovoljni signali", value: String(report.impact.insufficientSignalCount) },
  ];

  for (const action of report.recommendedActions) {
    rows.push({ section: "Preporucene akcije", item: "Akcija", value: action });
  }

  return resolveAnalyticsTablePayload({
    tableKey: "pilot-data-quality-intake",
    tableTitle: "Trendplus pilot izveštaj kvaliteta podataka",
    documentType: "pilot-data-quality-intake",
    templateName: "analytics-table-default",
    columns: [
      { key: "section", header: "Sekcija", dataType: "text" as const },
      { key: "item", header: "Stavka", dataType: "text" as const },
      { key: "value", header: "Vrednost", dataType: "text" as const },
    ],
    rows,
    filters,
    metadata: [
      { key: "generatedAtUtc", label: "Generisano", value: report.generatedAtUtc },
      { key: "lastImportAtUtc", label: "Poslednji import", value: report.lastImportAtUtc ?? null },
      { key: "lastRefreshAtUtc", label: "Poslednje osveženje", value: report.lastRefreshAtUtc ?? null },
      { key: "dataScope", label: "Scope", value: report.dataScope },
    ],
    locale: "sr-RS",
  });
}

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

export default function PilotDataQualityIntakeReport({ report, loading, error, filters, durableReport, onRetry }: Props) {
  const [exportState, setExportState] = useState<string | null>(null);
  const [showMethodology, setShowMethodology] = useState(false);

  const readiness = useMemo(() => readinessTone(report?.readinessStatus ?? "critical"), [report?.readinessStatus]);

  if (error) {
    return (
      <AnalyticsErrorState
        title="Pilot intake report trenutno nije dostupan"
        message={error}
        suggestions={[
          "Proverite da li je import zavrsen.",
          "Pokrenite osvežavanje analytics podataka.",
          "Pokušajte ponovo za nekoliko trenutaka.",
        ]}
        onRetry={onRetry}
        helpHref="/analytics/data-quality"
      />
    );
  }

  if (loading && !report) {
    return <div className="data-quality-loading">Ucitavam pilot intake report...</div>;
  }

  if (!report) {
    return (
      <AnalyticsEmptyState
        variant="no_data"
        message="Pilot intake report nije moguce generisati za trenutni opseg."
        reasons={[
          "Import nije zavrsen ili nema podataka u periodu.",
          "Filter opseg je previše uzak.",
        ]}
        actions={[
          { label: "Proširite period." },
          { label: "Proverite Data Quality", href: "/analytics/data-quality" },
          { label: "Proverite worker status", href: "/admin/configuration?panel=workers" },
        ]}
        dataQualityHref="/analytics/data-quality"
        refreshStatusHref="/admin/configuration?panel=workers"
      />
    );
  }

  const handlePrint = () => {
    window.print();
  };

  const handleCsv = () => {
    const csv = buildCsv(report);
    const blob = new Blob([csv], { type: "text/csv;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `pilot-data-quality-intake-${report.generatedAtUtc.slice(0, 10)}.csv`;
    anchor.click();
    URL.revokeObjectURL(url);
  };

  const handleExcel = async () => {
    try {
      setExportState("Pripremam Excel izvoz...");
      const payload = durableReport
        ? resolveAnalyticsTablePayload({
            tableKey: durableReport.payload.tableKey,
            tableTitle: durableReport.payload.tableTitle,
            documentType: durableReport.payload.documentType,
            templateName: durableReport.payload.templateName,
            locale: durableReport.payload.locale,
            columns: durableReport.payload.columns.map((column) => ({
              ...column,
              dataType: normalizeColumnType(column.dataType),
            })),
            rows: durableReport.payload.rows,
            filters: durableReport.payload.filters,
            metadata: durableReport.payload.metadata,
          })
        : buildExportPayload(report, filters);
      const result = await generateExport(payload, {
        format: "xlsx",
        orientation: "landscape",
        includeFiltersAndMetadata: true,
      });

      if (result.isAsync) {
        const completed = await waitForExport(result.documentId);
        if (completed.downloadUrl) {
          downloadExport(completed.downloadUrl, completed.fileName);
        }
      } else if (result.downloadUrl) {
        downloadExport(result.downloadUrl, result.fileName);
      }

      setExportState("Excel izvoz je preuzet.");
    } catch (reason) {
      setExportState(reason instanceof Error ? reason.message : "Excel izvoz nije uspeo.");
    }
  };

  const handleCopySummary = async () => {
    try {
      await navigator.clipboard.writeText(buildSummary(report));
      setExportState("Sazetak je kopiran.");
    } catch {
      setExportState("Kopiranje nije uspelo.");
    }
  };

  const handleCopyLink = async () => {
    try {
      const targetUrl = durableReport?.stableQueryUrl
        ? new URL(durableReport.stableQueryUrl, window.location.origin).toString()
        : window.location.href;
      await navigator.clipboard.writeText(targetUrl);
      setExportState("Link ka izveštaju je kopiran.");
    } catch {
      setExportState("Kopiranje linka nije uspelo.");
    }
  };

  return (
    <section className="pilot-intake-report">
      <div className="pilot-intake-head">
        <div>
          <h2>Trendplus pilot izveštaj kvaliteta podataka</h2>
          <p>Ucitavanje podataka za analitiku pre otvaranja dashboard-a.</p>
        </div>
        <div className="pilot-intake-actions no-print">
          {durableReport?.stableQueryUrl ? (
            <Link to={durableReport.stableQueryUrl} className="pilot-intake-action-link">Otvori trajni report</Link>
          ) : null}
          <button type="button" onClick={handlePrint}>Stampaj izveštaj</button>
          <button type="button" onClick={handleCsv}>Izvezi CSV</button>
          <button type="button" onClick={() => void handleExcel()}>Izvezi Excel</button>
          <button type="button" onClick={() => void handleCopySummary()}>Kopiraj sazetak</button>
          <button type="button" onClick={() => void handleCopyLink()}>Kopiraj link</button>
          <span className="pilot-intake-muted">PDF export nije dostupan u ovoj fazi.</span>
        </div>
      </div>

      {exportState ? <div className="pilot-intake-state no-print">{exportState}</div> : null}

      <article className={`pilot-intake-score ${readiness}`}>
        <div>
          <span>Skor spremnosti</span>
          <strong>{report.readinessScore}/100</strong>
          <p>{report.readinessLabel}</p>
        </div>
        <div className="pilot-intake-thresholds">
          <span>90-100: Spremno za pouzdanu analitiku</span>
          <span>70-89: Upotrebljivo uz upozorenja</span>
          <span>40-69: Pilot moze, ali preporuke ogranicene</span>
          <span>&lt;40: Prvo srediti podatke</span>
        </div>
      </article>

      <div className="pilot-intake-grid">
        <section className="pilot-card">
          <h3>Ucitano</h3>
          <ul>
            <li>Artikli: {formatNumber(report.loadedData.articlesCount)}</li>
            <li>Stavke prodaje: {formatNumber(report.loadedData.saleItemsCount)}</li>
            <li>Racuni: {formatNumber(report.loadedData.receiptsCount)}</li>
            <li>Dobavljači: {formatNumber(report.loadedData.suppliersCount)}</li>
            <li>Objekti: {formatNumber(report.loadedData.storesCount)}</li>
            <li>Prva prodaja: {formatDate(report.loadedData.firstSaleDate)}</li>
            <li>Poslednja prodaja: {formatDate(report.loadedData.lastSaleDate)}</li>
            <li>Poslednji import: {formatDateTime(report.lastImportAtUtc)}</li>
            <li>Poslednje osveženje analitike: {formatDateTime(report.lastRefreshAtUtc)}</li>
          </ul>
        </section>

        <section className="pilot-card">
          <h3>Problemi</h3>
          <ul>
            <li className="critical">Bez dobavljača: {formatNumber(report.issues.missingSupplierCount)}</li>
            <li className="critical">Bez nabavne cene: {formatNumber(report.issues.missingCostCount)}</li>
            <li className="warning">Bez kategorije: {formatNumber(report.issues.missingCategoryCount)}</li>
            <li className="warning">Bez boje: {formatNumber(report.issues.missingColorCount ?? 0)}</li>
            <li className="warning">Bez velicine: {formatNumber(report.issues.missingSizeCount ?? 0)}</li>
            <li className="critical">Prodaja bez artikla: {formatNumber(report.issues.saleWithoutArticleCount)}</li>
            <li className="critical">Nulta/negativna cena: {formatNumber(report.issues.zeroOrNegativePriceCount)}</li>
            <li className="warning">Dupliran SKU: {formatNumber(report.issues.duplicateSkuCount ?? 0)}</li>
            <li className="warning">Dobavljač bez naziva: {formatNumber(report.issues.missingSupplierNameCount)}</li>
          </ul>
        </section>

        <section className="pilot-card">
          <h3>Uticaj</h3>
          <ul>
            <li>Prihod bez nabavne cene: {formatPercentFromRatio(report.impact.revenueWithoutCostPercent)}</li>
            <li>Artikli bez dobavljača: {formatPercentFromRatio(report.impact.articlesWithoutSupplierPercent)}</li>
            <li>Blokirane preporuke: {formatNumber(report.impact.recommendationsBlockedCount)}</li>
            <li>Ignorisani redovi: {formatNumber(report.impact.ignoredRowsCount)}</li>
            <li>Nedovoljni signali: {formatNumber(report.impact.insufficientSignalCount)}</li>
          </ul>
          <p className="pilot-card-note">Visok procenat prihoda bez cene ili veliki broj blokiranih preporuka direktno smanjuje pouzdanost marznih odluka.</p>
        </section>
      </div>

      <section className="pilot-card">
        <h3>Preporucene akcije</h3>
        <div className="pilot-actions-list">
          {report.recommendedActions.map((action) => (
            <Link key={action} to={mapActionHref(action)} className="pilot-action-item">
              <strong>{action}</strong>
              <span>Otvori povezani ekran</span>
            </Link>
          ))}
        </div>
      </section>

      <section className="pilot-methodology no-print">
        <button type="button" onClick={() => setShowMethodology((prev) => !prev)} aria-expanded={showMethodology}>
          Kako citati ovaj izveštaj?
        </button>
        {showMethodology ? (
          <p>
            Ovaj izveštaj prikazuje koliko podataka je ucitano i koji nedostaci uticu na pouzdanost analitike. Preporuke su zasnovane na kvalitetu signala,
            marznom doprinosu, zalihama i zavisnosti od nivoa cene.
          </p>
        ) : null}
      </section>
    </section>
  );
}



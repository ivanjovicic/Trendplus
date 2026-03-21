import React from "react";
import { ChevronDown, Download, Printer } from "lucide-react";
import Modal from "../Modal";
import {
  downloadExport,
  generateExport,
  requestPrintPreview,
  resolveApiUrl,
  SYNC_ROW_LIMIT,
  waitForExport,
  type ExportFormat,
  type ExportOrientation,
} from "../../services/exportApi";
import { resolveAnalyticsTablePayload, savePrintPayload } from "../../services/analyticsTableState";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../../types/analyticsTable";

function formatLabel(format: ExportFormat): string {
  if (format === "pdf") return "PDF";
  if (format === "xlsx") return "Excel";
  return "CSV";
}

export default function AnalyticsTableToolbar<Row>(props: {
  tableKey: string;
  tableTitle: string;
  columns: AnalyticsTableColumn<Row>[];
  rows: Row[];
  filters?: AnalyticsNamedValue[];
  metadata?: AnalyticsNamedValue[];
  defaultOrientation?: ExportOrientation;
  locale?: string;
  documentType?: string;
  templateName?: string;
  templateVersion?: number;
}) {
  const [menuOpen, setMenuOpen] = React.useState(false);
  const [modalOpen, setModalOpen] = React.useState(false);
  const [format, setFormat] = React.useState<ExportFormat>("pdf");
  const [orientation, setOrientation] = React.useState<ExportOrientation>(props.defaultOrientation ?? "landscape");
  const [includeFilters, setIncludeFilters] = React.useState(true);
  const [preview, setPreview] = React.useState(false);
  const [submitting, setSubmitting] = React.useState(false);
  const [statusText, setStatusText] = React.useState<string | null>(null);

  const payload = React.useMemo(
    () =>
      resolveAnalyticsTablePayload({
        tableKey: props.tableKey,
        tableTitle: props.tableTitle,
        columns: props.columns,
        rows: props.rows,
        filters: props.filters,
        metadata: props.metadata,
        locale: props.locale,
        documentType: props.documentType,
        templateName: props.templateName,
        templateVersion: props.templateVersion,
      }),
    [props.columns, props.documentType, props.filters, props.locale, props.metadata, props.rows, props.tableKey, props.tableTitle, props.templateName, props.templateVersion]
  );

  const openExportModal = (selectedFormat: ExportFormat) => {
    setFormat(selectedFormat);
    setPreview(selectedFormat === "pdf");
    setModalOpen(true);
    setMenuOpen(false);
  };

  const handlePrint = () => {
    const stateKey = savePrintPayload(payload);
    window.open(`/print/analytics/${encodeURIComponent(props.tableKey)}?stateKey=${encodeURIComponent(stateKey)}`, "_blank", "noopener");
  };

  const handleExport = async () => {
    setSubmitting(true);
    setStatusText(null);

    try {
      console.info("Export triggered", { table: props.tableKey, format, rowCount: payload.rows.length });
      if (preview && format === "pdf") {
        const previewResult = await requestPrintPreview(payload, {
          orientation,
          includeFiltersAndMetadata: includeFilters,
          preview: true,
        });

        if (previewResult.printUrl) {
          window.open(resolveApiUrl(previewResult.printUrl), "_blank", "noopener");
          setStatusText("Print preview je otvoren u novom tabu.");
        }

        setModalOpen(false);
        return;
      }

      const result = await generateExport(payload, {
        format,
        orientation,
        includeFiltersAndMetadata: includeFilters,
      });

      if (result.isAsync) {
        setStatusText("Veliki eksport je stavljen u red. Cekam da dokument bude spreman...");
        const completed = await waitForExport(result.documentId);
        if (completed.downloadUrl) {
          downloadExport(completed.downloadUrl, completed.fileName);
        }
        setStatusText("Eksport je zavrsen i preuzet.");
      } else if (result.downloadUrl) {
        downloadExport(result.downloadUrl, result.fileName);
        setStatusText("Eksport je preuzet.");
      } else {
        setStatusText("Eksport je pokrenut.");
      }

      setModalOpen(false);
    } catch (reason) {
      setStatusText(reason instanceof Error ? reason.message : "Eksport nije uspeo.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <div className="flex flex-wrap items-center gap-2">
        <div className="relative">
          <button
            type="button"
            onClick={() => setMenuOpen((current) => !current)}
            className="inline-flex items-center gap-2 rounded-xl border border-primary bg-primary px-3 py-2 text-xs font-semibold text-white"
          >
            <Download size={14} />
            Export
            <ChevronDown size={14} />
          </button>

          {menuOpen ? (
            <div className="absolute right-0 z-20 mt-2 min-w-[180px] rounded-xl border border-border bg-surface p-1 shadow-[0_12px_30px_-12px_rgba(0,0,0,0.9)]">
              {(["pdf", "xlsx", "csv"] as ExportFormat[]).map((option) => (
                <button
                  key={option}
                  type="button"
                  onClick={() => openExportModal(option)}
                  className="flex w-full items-center justify-between rounded-lg px-3 py-2 text-left text-sm text-muted hover:bg-surface-elevated"
                >
                  <span>{formatLabel(option)}</span>
                  <span className="text-xs text-muted">
                    {option === "pdf" ? "Print layout" : option === "xlsx" ? "Spreadsheet" : "Flat file"}
                  </span>
                </button>
              ))}
            </div>
          ) : null}
        </div>

        <button
          type="button"
          onClick={handlePrint}
          className="inline-flex items-center gap-2 rounded-xl border border-border bg-surface px-3 py-2 text-xs font-semibold text-muted"
        >
          <Printer size={14} />
          Print
        </button>

        <span className="text-xs text-muted">Redova: {payload.rows.length}</span>
        {statusText ? <span className="text-xs text-accent-success">{statusText}</span> : null}
      </div>

      <Modal
        isOpen={modalOpen}
        onClose={() => !submitting && setModalOpen(false)}
        title={`Export ${props.tableTitle}`}
        size="md"
      >
        <div className="space-y-4 text-sm text-muted">
          <div className="grid gap-3 sm:grid-cols-2">
            <label className="space-y-1">
              <span className="block text-xs uppercase tracking-wide text-muted">Format</span>
              <select
                value={format}
                onChange={(e) => setFormat(e.target.value as ExportFormat)}
                className="w-full rounded-lg border border-border bg-surface px-3 py-2"
              >
                <option value="pdf">PDF</option>
                <option value="xlsx">Excel</option>
                <option value="csv">CSV</option>
              </select>
            </label>

            <label className="space-y-1">
              <span className="block text-xs uppercase tracking-wide text-muted">Orijentacija</span>
              <select
                value={orientation}
                onChange={(e) => setOrientation(e.target.value as ExportOrientation)}
                className="w-full rounded-lg border border-border bg-surface px-3 py-2"
              >
                <option value="landscape">Landscape</option>
                <option value="portrait">Portrait</option>
              </select>
            </label>
          </div>

          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={includeFilters} onChange={(e) => setIncludeFilters(e.target.checked)} />
            Ukljuci filtere i metadata
          </label>

          <label className={`flex items-center gap-2 text-sm ${format !== "pdf" ? "opacity-50" : ""}`}>
            <input
              type="checkbox"
              checked={preview}
              disabled={format !== "pdf"}
              onChange={(e) => setPreview(e.target.checked)}
            />
            Otvori preview pre eksportovanja (samo PDF)
          </label>

          <div className="rounded-xl border border-border bg-surface p-3 text-xs text-muted">
            Manji setovi se generisu odmah. Vece tabele preko {SYNC_ROW_LIMIT.toLocaleString("sr-RS")} redova automatski prelaze u async queue.
          </div>

          <div className="flex justify-end gap-2">
            <button
              type="button"
              onClick={() => setModalOpen(false)}
              className="rounded-lg border border-border bg-surface px-3 py-2 text-xs font-semibold text-muted"
              disabled={submitting}
            >
              Otkazi
            </button>
            <button
              type="button"
              onClick={() => void handleExport()}
              className="rounded-lg border border-primary bg-primary px-3 py-2 text-xs font-semibold text-white"
              disabled={submitting}
            >
              {submitting ? "Generisem..." : preview && format === "pdf" ? "Otvori preview" : "Pokreni export"}
            </button>
          </div>
        </div>
      </Modal>
    </>
  );
}

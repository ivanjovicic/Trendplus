import React from "react";
import { CheckCircle2, ChevronDown, Download, FileSpreadsheet, FileText, Printer, ShieldCheck } from "lucide-react";
import Modal from "../Modal";
import InfoTip from "../ui/InfoTip";
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

function formatDescription(format: ExportFormat): string {
  if (format === "pdf") return "Izveštaj za menadžment i štampu";
  if (format === "xlsx") return "Tabela za dalju analizu";
  return "Brz flat-file izvoz";
}

function formatIcon(format: ExportFormat) {
  if (format === "xlsx") return FileSpreadsheet;
  if (format === "csv") return FileText;
  return Printer;
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
  extraActions?: React.ReactNode;
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
        setStatusText("Veliki eksport je stavljen u red. Čekam da dokument bude spreman...");
        const completed = await waitForExport(result.documentId);
        if (completed.downloadUrl) {
          downloadExport(completed.downloadUrl, completed.fileName);
        }
        setStatusText("Eksport je završen i preuzet.");
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
      <div className="flex flex-wrap items-center gap-2 rounded-2xl border border-[var(--border-default)] bg-[var(--surface-elevated)]/90 px-3 py-2 shadow-[0_16px_36px_-30px_rgba(0,0,0,0.9)]">
        <div className="relative">
          <button
            type="button"
            onClick={() => setMenuOpen((current) => !current)}
            className="inline-flex items-center gap-2 rounded-xl border border-primary bg-primary px-3 py-2 text-xs font-semibold text-[var(--primary-text)] shadow-[0_12px_24px_-18px_var(--info)] transition hover:translate-y-[-1px]"
          >
            <Download size={14} />
            Izvoz
            <InfoTip text="Izvezi tabelu u PDF, Excel ili CSV format sa filterima i metapodacima." />
            <ChevronDown size={14} />
          </button>

          {menuOpen ? (
            <div className="absolute right-0 z-20 mt-2 min-w-[260px] rounded-2xl border border-border bg-surface p-2 shadow-[0_24px_54px_-20px_rgba(0,0,0,0.9)]">
              {(["pdf", "xlsx", "csv"] as ExportFormat[]).map((option) => {
                const OptionIcon = formatIcon(option);
                return (
                  <button
                    key={option}
                    type="button"
                    onClick={() => openExportModal(option)}
                    className="flex w-full items-start gap-3 rounded-xl px-3 py-2.5 text-left text-sm text-muted transition hover:bg-surface-elevated hover:text-contrast"
                  >
                    <span className="mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-xl border border-border bg-[var(--surface-light)]">
                      <OptionIcon size={15} />
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="block font-semibold text-contrast">{formatLabel(option)}</span>
                      <span className="block text-xs text-muted">{formatDescription(option)}</span>
                    </span>
                  </button>
                );
              })}
            </div>
          ) : null}
        </div>

        <button
          type="button"
          onClick={handlePrint}
          className="inline-flex items-center gap-2 rounded-xl border border-border bg-surface px-3 py-2 text-xs font-semibold text-muted transition hover:border-[var(--info)] hover:text-contrast"
          title="Otvori prozor za štampu/printer"
        >
          <Printer size={14} />
          Štampaj
          <InfoTip text="Otvori print layout sa podešavanjima za izveštaj." />
        </button>

        {props.extraActions ?? null}
        <span className="inline-flex items-center gap-1 rounded-full border border-border bg-[var(--surface-light)] px-2.5 py-1 text-xs font-semibold text-muted">
          <ShieldCheck size={13} />
          Redova: {payload.rows.length.toLocaleString("sr-RS")}
        </span>
        {statusText ? (
          <span className="inline-flex items-center gap-1 rounded-full border border-[var(--success)]/40 bg-success-soft px-2.5 py-1 text-xs font-semibold text-[var(--success)]">
            <CheckCircle2 size={13} />
            {statusText}
          </span>
        ) : null}
      </div>

      <Modal
        isOpen={modalOpen}
        onClose={() => !submitting && setModalOpen(false)}
        title={`Export ${props.tableTitle}`}
        size="md"
      >
        <div className="space-y-4 text-sm text-muted">
          <div className="rounded-2xl border border-border bg-[var(--surface-light)] p-3">
            <div className="flex items-start gap-3">
              <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl border border-[var(--info)]/50 bg-[var(--info)]/10 text-[var(--info)]">
                <ShieldCheck size={17} />
              </span>
              <div>
                <p className="m-0 text-sm font-semibold text-contrast">Premium analytics export</p>
                <p className="m-0 mt-1 text-xs leading-relaxed text-muted">
                  Dokument koristi iste kolone, redove, filtere i metadata kao tabela na ekranu. Za velike setove eksport prelazi u async queue.
                </p>
              </div>
            </div>
          </div>

          <div className="grid gap-3 sm:grid-cols-2">
            <label className="space-y-1">
              <span className="block text-xs uppercase tracking-wide text-muted">Format</span>
              <select
                value={format}
                onChange={(e) => setFormat(e.target.value as ExportFormat)}
                className="w-full rounded-xl border border-border bg-surface px-3 py-2"
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
                className="w-full rounded-xl border border-border bg-surface px-3 py-2"
              >
                <option value="landscape">Landscape</option>
                <option value="portrait">Portrait</option>
              </select>
            </label>
          </div>

          <label className="flex items-center gap-2 rounded-xl border border-border bg-[var(--surface-light)] px-3 py-2 text-sm">
            <input type="checkbox" checked={includeFilters} onChange={(e) => setIncludeFilters(e.target.checked)} />
            Uključi filtere i metadata
          </label>

          <label className={`flex items-center gap-2 rounded-xl border border-border bg-[var(--surface-light)] px-3 py-2 text-sm ${format !== "pdf" ? "opacity-50" : ""}`}>
            <input
              type="checkbox"
              checked={preview}
              disabled={format !== "pdf"}
              onChange={(e) => setPreview(e.target.checked)}
            />
            Otvori preview pre eksportovanja (samo PDF)
          </label>

          <div className="rounded-xl border border-border bg-surface p-3 text-xs text-muted">
            Manji setovi se generišu odmah. Veće tabele preko {SYNC_ROW_LIMIT.toLocaleString("sr-RS")} redova automatski prelaze u async queue.
          </div>

          <div className="flex justify-end gap-2">
            <button
              type="button"
              onClick={() => setModalOpen(false)}
              className="rounded-xl border border-border bg-surface px-3 py-2 text-xs font-semibold text-muted transition hover:text-contrast"
              disabled={submitting}
            >
              Otkaži
            </button>
            <button
              type="button"
              onClick={() => void handleExport()}
              className="rounded-xl border border-primary bg-primary px-3 py-2 text-xs font-semibold text-[var(--primary-text)] shadow-[0_12px_24px_-18px_var(--info)]"
              disabled={submitting}
            >
              {submitting ? "Generišem..." : preview && format === "pdf" ? "Otvori preview" : "Pokreni export"}
            </button>
          </div>
        </div>
      </Modal>
    </>
  );
}

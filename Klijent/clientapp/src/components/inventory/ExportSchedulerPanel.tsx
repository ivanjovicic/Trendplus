import { ChevronDown, Download, FileSpreadsheet, FileText, MailIcon, Printer, RefreshCw } from "lucide-react";
import { useState } from "react";
import type { InventoryReportSchedule, InventoryReportScheduleInput } from "../../types/analytics";

type ExportSchedulerPanelProps = {
  isOpen?: boolean;
  printOrientation: "landscape" | "portrait";
  onPrintOrientationChange: (orientation: "landscape" | "portrait") => void;
  onPrintPreview: () => void;
  onPrintBlank: () => void;
  onExportCsv: () => void;
  onExportCsvFiltered: () => void;
  onExportExcel: () => void;
  onExportPdf: () => void;
  onRefresh: () => void;
  schedules: InventoryReportSchedule[];
  scheduleDraft: InventoryReportScheduleInput;
  setScheduleDraft: (draft: InventoryReportScheduleInput) => void;
  schedulerBusy: boolean;
  schedulerMessage: string | null;
  onCopyCurrentFilters: () => void;
  onSaveSchedule: () => void;
  onRunScheduleNow: (id: number) => void;
  exportBusy: boolean;
  totalCount: number;
  rowsLength: number;
  exportStatus: string | null;
};

export function ExportSchedulerPanel({
  isOpen: initialOpen,
  printOrientation,
  onPrintOrientationChange,
  onPrintPreview,
  onPrintBlank,
  onExportCsv,
  onExportCsvFiltered,
  onExportExcel,
  onExportPdf,
  onRefresh,
  schedules,
  scheduleDraft,
  setScheduleDraft,
  schedulerBusy,
  schedulerMessage,
  onCopyCurrentFilters,
  onSaveSchedule,
  onRunScheduleNow,
  exportBusy,
  totalCount,
  rowsLength,
  exportStatus,
}: ExportSchedulerPanelProps) {
  const [isOpen, setIsOpen] = useState(initialOpen ?? false);

  return (
    <section className="rounded-[28px] border border-border bg-surface p-5 shadow-lg">
      <button
        type="button"
        onClick={() => setIsOpen(!isOpen)}
        className="w-full flex items-center justify-between gap-3 hover:opacity-80 transition-opacity"
      >
        <div className="flex items-start gap-3 text-left">
          <div>
            <h2 className="text-lg font-semibold text-foreground">Izvoz i raspored izveštaja</h2>
            <p className="text-sm text-muted">Pripremi dokumente za deljenje, zapiši raspored, ili pošalji timu.</p>
          </div>
        </div>
        <ChevronDown
          size={20}
          className={`shrink-0 transition-transform duration-200 ${isOpen ? "rotate-180" : ""}`}
        />
      </button>

      {isOpen && (
        <div className="mt-5 space-y-4 border-t border-border pt-4">
          {/* Print & Export Section */}
          <div>
            <h3 className="text-sm font-semibold text-foreground mb-3">Štampa i izvoz</h3>
            <div className="flex flex-wrap items-center gap-2 mb-3">
              <span className="inline-flex rounded-xl border border-border overflow-hidden text-xs font-semibold" role="group" aria-label="Orijentacija štampe">
                <button
                  type="button"
                  aria-pressed={printOrientation === "landscape"}
                  onClick={() => onPrintOrientationChange("landscape")}
                  className={`px-3 py-2 transition-colors duration-150 ${printOrientation === "landscape" ? "bg-info text-white" : "bg-surface text-foreground hover:bg-surface-darker"}`}
                  title="Horizontalno (A4 landscape)"
                >
                  ↔ Hor.
                </button>
                <button
                  type="button"
                  aria-pressed={printOrientation === "portrait"}
                  onClick={() => onPrintOrientationChange("portrait")}
                  className={`px-3 py-2 border-l border-border transition-colors duration-150 ${printOrientation === "portrait" ? "bg-info text-white" : "bg-surface text-foreground hover:bg-surface-darker"}`}
                  title="Vertikalno (A4 portrait)"
                >
                  ↕ Ver.
                </button>
              </span>
            </div>

            <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-2">
              <button
                type="button"
                aria-label="Otvori print preview filtriranog izvestaja"
                onClick={onPrintPreview}
                disabled={exportBusy || totalCount === 0}
                className="inline-flex items-center justify-center gap-1 rounded-xl border border-border bg-surface px-3 py-2 text-xs font-semibold text-foreground transition-all duration-200 hover:border-info hover:shadow-md disabled:cursor-not-allowed disabled:opacity-60"
              >
                <Printer size={13} />
                <span className="hidden sm:inline">Preview</span>
              </button>
              <button
                type="button"
                aria-label="Odštampaj prazan obrazac bilansa stanja"
                onClick={onPrintBlank}
                disabled={exportBusy}
                className="inline-flex items-center justify-center gap-1 rounded-xl border border-border bg-surface px-3 py-2 text-xs font-semibold text-foreground transition-all duration-200 hover:border-warning hover:shadow-md disabled:cursor-not-allowed disabled:opacity-60"
              >
                <Printer size={13} />
                <span className="hidden sm:inline">Prazan</span>
              </button>
              <button
                type="button"
                aria-label="Izvezi CSV za trenutni ekran"
                onClick={onExportCsv}
                disabled={rowsLength === 0}
                className="inline-flex items-center justify-center gap-1 rounded-xl border border-border bg-surface px-3 py-2 text-xs font-semibold text-info transition-all duration-200 hover:border-info hover:shadow-md disabled:cursor-not-allowed disabled:opacity-60"
              >
                <Download size={13} />
                <span className="hidden sm:inline">CSV</span>
              </button>
              <button
                type="button"
                aria-label="Izvezi CSV filtrirano"
                onClick={onExportCsvFiltered}
                disabled={exportBusy || totalCount === 0}
                className="inline-flex items-center justify-center gap-1 rounded-xl border border-border bg-surface px-3 py-2 text-xs font-semibold text-info transition-all duration-200 hover:border-info hover:shadow-md disabled:cursor-not-allowed disabled:opacity-60"
              >
                <Download size={13} />
                <span className="hidden sm:inline">CSV fil</span>
              </button>
              <button
                type="button"
                aria-label="Izvezi Excel filtrirano"
                onClick={onExportExcel}
                disabled={exportBusy || totalCount === 0}
                className="inline-flex items-center justify-center gap-1 rounded-xl border border-border bg-surface px-3 py-2 text-xs font-semibold text-success transition-all duration-200 hover:border-success hover:shadow-md disabled:cursor-not-allowed disabled:opacity-60"
              >
                <FileSpreadsheet size={13} />
                <span className="hidden sm:inline">Excel</span>
              </button>
              <button
                type="button"
                aria-label="Izvezi PDF filtrirano"
                onClick={onExportPdf}
                disabled={exportBusy || totalCount === 0}
                className="inline-flex items-center justify-center gap-1 rounded-xl border border-border bg-surface px-3 py-2 text-xs font-semibold text-error transition-all duration-200 hover:border-error hover:shadow-md disabled:cursor-not-allowed disabled:opacity-60"
              >
                <FileText size={13} />
                <span className="hidden sm:inline">PDF</span>
              </button>
              <button
                type="button"
                aria-label="Osvezi stranicu bilansa stanja"
                onClick={onRefresh}
                className="inline-flex items-center justify-center gap-1 rounded-xl border border-border bg-surface px-3 py-2 text-xs font-semibold text-foreground transition-all duration-200 hover:border-secondary hover:shadow-md col-span-2 sm:col-span-1"
              >
                <RefreshCw size={13} />
                <span className="hidden sm:inline">Osvezi</span>
              </button>
            </div>

            {exportStatus ? (
              <div className="mt-2 rounded-2xl border border-info bg-surface-darker px-3 py-2 text-xs text-info">
                {exportStatus}
              </div>
            ) : null}
          </div>

          {/* Scheduler Section */}
          <div className="border-t border-border pt-4">
            <h3 className="text-sm font-semibold text-foreground mb-3 flex items-center gap-2">
              <MailIcon size={14} /> Raspored izveštaja
            </h3>

            {schedulerMessage ? (
              <div className="mb-3 rounded-2xl border border-warning bg-surface-darker px-3 py-2 text-xs text-warning">
                {schedulerMessage}
              </div>
            ) : null}

            {schedules.length > 0 && (
              <div className="mb-4 space-y-2">
                <p className="text-xs font-semibold text-muted">Postojeći rasporedi:</p>
                {schedules.map((schedule) => (
                  <div
                    key={schedule.id}
                    className="flex items-center justify-between rounded-xl border border-border bg-surface-darker p-3 text-xs"
                  >
                    <div>
                      <div className="font-semibold text-foreground">{schedule.name}</div>
                      <div className="text-muted">
                        {schedule.isEnabled ? "Aktivan" : "Neaktivan"} · {schedule.frequency}
                      </div>
                    </div>
                    <button
                      type="button"
                      onClick={() => onRunScheduleNow(schedule.id)}
                      disabled={schedulerBusy}
                      className="rounded-lg border border-border bg-info px-2 py-1 text-white transition-opacity hover:opacity-80 disabled:cursor-not-allowed disabled:opacity-60"
                    >
                      Pokreni
                    </button>
                  </div>
                ))}
              </div>
            )}

            <div className="space-y-2">
              <button
                type="button"
                onClick={onCopyCurrentFilters}
                className="w-full rounded-xl border border-border bg-surface px-3 py-2 text-xs font-semibold text-foreground transition-colors hover:bg-surface-darker"
              >
                Kopiraj trenutne filtere u raspored
              </button>
              <button
                type="button"
                onClick={onSaveSchedule}
                disabled={schedulerBusy}
                className="w-full rounded-xl border border-success bg-surface px-3 py-2 text-xs font-semibold text-success transition-colors hover:bg-surface-darker disabled:cursor-not-allowed disabled:opacity-60"
              >
                {schedulerBusy ? "Čuvanje..." : "Sačuvaj novi raspored"}
              </button>
            </div>
          </div>
        </div>
      )}
    </section>
  );
}

import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { ExportSchedulerPanel } from "./ExportSchedulerPanel";
import { createScheduleDraft } from "./inventoryUtils";

describe("ExportSchedulerPanel schedule labels", () => {
  it("uses safe labels for unknown frequency and format values", () => {
    const noop = vi.fn();
    render(
      <ExportSchedulerPanel
        isOpen
        printOrientation="landscape"
        onPrintOrientationChange={noop}
        onPrintPreview={noop}
        onPrintBlank={noop}
        onExportCsv={noop}
        onExportCsvFiltered={noop}
        onExportExcel={noop}
        onExportPdf={noop}
        onRefresh={noop}
        schedules={[{
          id: 3,
          name: "Raspored bez poznatih oznaka",
          isEnabled: true,
          frequency: "future_frequency",
          dayOfWeek: null,
          runAtLocalTime: "08:00",
          timeZoneId: "Europe/Belgrade",
          format: "future_format",
          orientation: "landscape",
          includeFiltersAndMetadata: true,
          recipientsCsv: "manager@example.com",
          subject: null,
          search: null,
          storeId: null,
          supplierId: null,
          sortBy: null,
          lastRunAtUtc: null,
          lastRunStatus: null,
          lastError: null,
          lastDocumentId: null,
        }]}
        scheduleDraft={createScheduleDraft()}
        setScheduleDraft={noop}
        schedulerBusy={false}
        schedulerMessage={null}
        onCopyCurrentFilters={noop}
        onSaveSchedule={noop}
        onRunScheduleNow={noop}
        exportBusy={false}
        totalCount={1}
        rowsLength={1}
        exportStatus={null}
      />,
    );

    expect(screen.getByText("Raspored bez poznatih oznaka").parentElement).toHaveTextContent("Nepoznato");
    expect(screen.queryByText(/future_frequency/)).not.toBeInTheDocument();
    expect(screen.queryByText(/future_format/)).not.toBeInTheDocument();
  });

  it("sanitizes technical export and scheduler messages", () => {
    const noop = vi.fn();
    render(
      <ExportSchedulerPanel
        isOpen
        printOrientation="landscape"
        onPrintOrientationChange={noop}
        onPrintPreview={noop}
        onPrintBlank={noop}
        onExportCsv={noop}
        onExportCsvFiltered={noop}
        onExportExcel={noop}
        onExportPdf={noop}
        onRefresh={noop}
        schedules={[]}
        scheduleDraft={createScheduleDraft()}
        setScheduleDraft={noop}
        schedulerBusy={false}
        schedulerMessage="NpgsqlException: scheduler provider failure"
        onCopyCurrentFilters={noop}
        onSaveSchedule={noop}
        onRunScheduleNow={noop}
        exportBusy={false}
        totalCount={1}
        rowsLength={1}
        exportStatus="System.InvalidOperationException: export provider failure"
      />,
    );

    // Each surface uses its own safe, user-facing fallback instead of the raw technical message.
    expect(screen.getByText("Rasporedi izveštaja trenutno nisu dostupni.")).toBeInTheDocument();
    expect(screen.getByText("Operacija izvoza trenutno nije uspela.")).toBeInTheDocument();
    expect(screen.queryByText(/NpgsqlException|InvalidOperationException/)).not.toBeInTheDocument();
  });
});

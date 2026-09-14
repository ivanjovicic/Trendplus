import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { MailSchedulerPanel } from "./MailSchedulerPanel";
import { createScheduleDraft } from "./inventoryUtils";

describe("MailSchedulerPanel status labels", () => {
  it("does not expose unknown scheduler tokens or imply a successful run", () => {
    render(
      <MailSchedulerPanel
        scheduleDraft={createScheduleDraft()}
        setScheduleDraft={vi.fn()}
        schedules={[{
          id: 1,
          name: "Nepoznat raspored",
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
          lastRunAtUtc: "2026-08-10T10:00:00Z",
          lastRunStatus: "future_status",
          lastError: null,
          lastDocumentId: null,
        }]}
        schedulerBusy={false}
        schedulerMessage={null}
        onCopyCurrentFilters={vi.fn()}
        onSaveSchedule={vi.fn()}
        onRunScheduleNow={vi.fn()}
      />,
    );

    expect(screen.getByText(/Nepoznato u 08:00/)).toBeInTheDocument();
    expect(screen.getByText("Nepoznato | manager@example.com")).toBeInTheDocument();
    expect(screen.getByText(/status: Nepoznato/)).toBeInTheDocument();
    expect(screen.queryByText("future_frequency")).not.toBeInTheDocument();
    expect(screen.queryByText("future_format")).not.toBeInTheDocument();
    expect(screen.queryByText(/future_status/)).not.toBeInTheDocument();
    expect(screen.getByText(/status: Nepoznato/)).toBeInTheDocument();
  });

  it("labels a schedule with no previous run explicitly", () => {
    render(
      <MailSchedulerPanel
        scheduleDraft={createScheduleDraft()}
        setScheduleDraft={vi.fn()}
        schedules={[{
          id: 2,
          name: "Dnevni raspored",
          isEnabled: false,
          frequency: "daily",
          dayOfWeek: null,
          runAtLocalTime: "08:00",
          timeZoneId: "Europe/Belgrade",
          format: "pdf",
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
        schedulerBusy={false}
        schedulerMessage={null}
        onCopyCurrentFilters={vi.fn()}
        onSaveSchedule={vi.fn()}
        onRunScheduleNow={vi.fn()}
      />,
    );

    expect(screen.getByText(/status: Nije pokrenuto/)).toBeInTheDocument();
  });
});

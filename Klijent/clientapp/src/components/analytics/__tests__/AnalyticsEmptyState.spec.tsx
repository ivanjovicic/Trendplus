import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { act, fireEvent, render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import AnalyticsEmptyState from "../AnalyticsEmptyState";

const emptyStateStyles = readFileSync(resolve(process.cwd(), "src/components/analytics/AnalyticsEmptyState.css"), "utf8");

function renderEmptyState(overrides: Partial<React.ComponentProps<typeof AnalyticsEmptyState>> = {}) {
  return render(
    <MemoryRouter>
      <AnalyticsEmptyState
        variant="no_data"
        emptyReason="no_data_in_period"
        {...overrides}
      />
    </MemoryRouter>,
  );
}

describe("AnalyticsEmptyState", () => {
  it("keeps insufficient-data surfaces on the active theme surface", () => {
    expect(emptyStateStyles).toMatch(/\.analytics-empty-state\s*\{[^}]*background:\s*var\(--surface-elevated/);
    const insufficientDataStyles = emptyStateStyles.match(/\.aes-insufficient-data\s*\{([^}]*)\}/)?.[1] ?? "";
    expect(insufficientDataStyles).not.toMatch(/background\s*:/);
  });

  it("maps known empty reason codes to Serbian copy", () => {
    renderEmptyState();

    expect(screen.getAllByText("Nema podataka za izabrani period.")).toHaveLength(1);
    expect(screen.queryByText("no_data_in_period")).not.toBeInTheDocument();
  });

  it("preserves safe contextual copy separately from the reason code", () => {
    renderEmptyState({
      message: "Izabrani period je van dostupnog raspona prodaje (01.01.2026 - 31.03.2026).",
      emptyReason: "no_data_in_period",
    });

    expect(screen.getByText(/van dostupnog raspona prodaje/)).toBeInTheDocument();
    expect(screen.getAllByText("Nema podataka za izabrani period.")).toHaveLength(1);
  });

  it("fails closed for unknown or malicious-looking backend codes", () => {
    renderEmptyState({ code: "<script>alert('backend-code')</script>" });

    expect(screen.getByText("Sačuvajte tehnički kod iz detalja i kontaktirajte podršku.")).toBeInTheDocument();
    expect(screen.getByText(/<script>/i).closest("details")).not.toBeNull();
  });

  it("keeps an unknown backend code in an accessible details disclosure", () => {
    renderEmptyState({ code: "FUTURE_EMPTY_REASON" });

    expect(screen.getByRole("heading", { name: "Prikaz nije dostupan." })).toBeInTheDocument();
    expect(screen.getByText("FUTURE_EMPTY_REASON")).toBeInTheDocument();
    expect(screen.getByText("FUTURE_EMPTY_REASON").closest("details")).not.toBeNull();
  });

  it("does not turn an error response meta into a successful empty state", () => {
    const { container } = renderEmptyState({
      meta: { success: false, errorCode: "MISSING_OBJECT" },
    });

    expect(container.querySelector(".analytics-empty-state")).toBeNull();
  });

  it("shows a slow-loading recovery state only after the configured delay", () => {
    const onRetry = vi.fn();
    const onCancel = vi.fn();
    vi.useFakeTimers();
    try {
      renderEmptyState({ loading: true, loadingDelayMs: 1000, onRetry, onCancel });

      expect(screen.getByRole("heading", { name: "Učitavanje podataka…" })).toBeInTheDocument();
      act(() => { vi.advanceTimersByTime(1000); });
      expect(screen.getByRole("heading", { name: "Učitavanje traje duže nego obično." })).toBeInTheDocument();
      fireEvent.click(screen.getByRole("button", { name: "Otkaži" }));
      fireEvent.click(screen.getByRole("button", { name: "Pokušaj ponovo" }));
      expect(onRetry).toHaveBeenCalledOnce();
      expect(onCancel).toHaveBeenCalledOnce();
    } finally {
      vi.useRealTimers();
    }
  });

  it("sanitizes technical empty-state messages", () => {
    renderEmptyState({ message: "sql_timeout_v2 at internal_table" });

    expect(
      screen.getByText("Sistem nije pronašao zapise koji odgovaraju trenutnim filterima."),
    ).toBeInTheDocument();
    expect(screen.queryByText(/sql_timeout_v2|internal_table/i)).not.toBeInTheDocument();
  });

  it("keeps blank and null reasons safe and does not invent a raw reason", () => {
    const { rerender } = renderEmptyState({ emptyReason: "   " });
    expect(screen.getByText("Nije specificirano")).toBeInTheDocument();

    rerender(
      <MemoryRouter>
        <AnalyticsEmptyState variant="no_data" emptyReason={null} />
      </MemoryRouter>,
    );

    expect(screen.queryByText("Nije specificirano")).not.toBeInTheDocument();
    expect(screen.queryByText(/emptyReason/i)).not.toBeInTheDocument();
  });

  it("keeps only executable entries under the action heading", () => {
    renderEmptyState({
      actions: [
        { label: "Samo smernica bez akcije" },
        { label: "Otvori kvalitet podataka", href: "/analytics/data-quality" },
      ],
    });

    const actionHeading = screen.getByRole("heading", { name: "Predlog akcija" });
    const actionSection = actionHeading.parentElement as HTMLElement;
    const actionItems = within(actionSection).getAllByRole("listitem");

    expect(actionItems).toHaveLength(1);
    expect(within(actionItems[0]).getByRole("link", { name: "Otvori kvalitet podataka" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Smernice" })).toBeInTheDocument();
    expect(screen.getByText("Samo smernica bez akcije")).toBeInTheDocument();
  });

  it("does not advertise the default period guidance as a non-action", () => {
    renderEmptyState({ actions: undefined });

    const actionHeading = screen.getByRole("heading", { name: "Predlog akcija" });
    const actionSection = actionHeading.parentElement as HTMLElement;

    expect(within(actionSection).queryByText("Proširi period")).not.toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Smernice" })).toBeInTheDocument();
    expect(screen.getByText("Proširi period")).toBeInTheDocument();
  });
});

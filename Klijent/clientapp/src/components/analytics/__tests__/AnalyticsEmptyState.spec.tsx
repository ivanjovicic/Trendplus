import { render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import AnalyticsEmptyState from "../AnalyticsEmptyState";

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
  it("maps known empty reason codes to Serbian copy", () => {
    renderEmptyState();

    expect(screen.getAllByText("Nema podataka za izabrani period.")).toHaveLength(2);
    expect(screen.queryByText("no_data_in_period")).not.toBeInTheDocument();
  });

  it("fails closed for unknown or malicious-looking empty reasons", () => {
    renderEmptyState({ emptyReason: "<script>alert('backend-code')</script>" });

    expect(screen.getByText("Nema podataka za izabrani opseg.")).toBeInTheDocument();
    expect(screen.queryByText(/backend-code|<script>/i)).not.toBeInTheDocument();
  });

  it("sanitizes technical empty-state messages", () => {
    renderEmptyState({ message: "sql_timeout_v2 at internal_table" });

    expect(screen.getByText("Podaci trenutno nisu dostupni. Proverite kvalitet podataka i pokušajte ponovo.")).toBeInTheDocument();
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

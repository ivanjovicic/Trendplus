import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import AnalyticsErrorState from "../AnalyticsErrorState";

function renderError(overrides: Partial<React.ComponentProps<typeof AnalyticsErrorState>> = {}) {
  return render(
    <MemoryRouter>
      <AnalyticsErrorState
        title="Analitika nije dostupna"
        message="Podaci trenutno nisu dostupni."
        {...overrides}
      />
    </MemoryRouter>,
  );
}

describe("AnalyticsErrorState", () => {
  it("hides a known backend code while preserving a clear Serbian message", () => {
    renderError({
      message: "Provera analitike trenutno nije dostupna.",
      errorCode: "ANALYTICS_DB_UNAVAILABLE",
    });

    const alert = screen.getByRole("alert");
    expect(alert).toHaveTextContent("Provera analitike trenutno nije dostupna.");
    expect(alert).not.toHaveTextContent("ANALYTICS_DB_UNAVAILABLE");
    expect(alert).not.toHaveTextContent("Šifra greške");
  });

  it("replaces unknown and technical backend text with safe Serbian guidance", () => {
    renderError({
      message: "System.InvalidOperationException: FUTURE_ANALYTICS_ERROR_V2",
      errorCode: "FUTURE_ANALYTICS_ERROR_V2",
    });

    const alert = screen.getByRole("alert");
    expect(alert).toHaveTextContent(
      "Podaci trenutno nisu dostupni. Proverite kvalitet podataka i pokušajte ponovo.",
    );
    expect(alert).not.toHaveTextContent("FUTURE_ANALYTICS_ERROR_V2");
    expect(alert).not.toHaveTextContent("System.InvalidOperationException");
  });

  it("suppresses malformed technical codes and keeps correlation as support metadata", () => {
    renderError({
      message: "SQL timeout (sql_timeout_v2)",
      errorCode: "sql_timeout_v2",
      correlationId: "corr-253",
    });

    const alert = screen.getByRole("alert");
    expect(alert).toHaveTextContent(
      "Podaci trenutno nisu dostupni. Proverite kvalitet podataka i pokušajte ponovo.",
    );
    expect(alert).toHaveTextContent("Correlation ID: corr-253");
    expect(alert).not.toHaveTextContent("sql_timeout_v2");
  });

  it("does not pass a technical page message through when no code is provided", () => {
    renderError({ message: "TypeError: Failed to fetch analytics payload" });

    const alert = screen.getByRole("alert");
    expect(alert).toHaveTextContent(
      "Podaci trenutno nisu dostupni. Proverite kvalitet podataka i pokušajte ponovo.",
    );
    expect(alert).not.toHaveTextContent("TypeError: Failed to fetch analytics payload");
  });

  it("does not render a code row when the code is empty and preserves the empty-message fallback", () => {
    renderError({ message: "", errorCode: "" });

    const alert = screen.getByRole("alert");
    expect(alert).toHaveTextContent("Ne prikazujemo nule jer nije potvrđeno da je period stvarno prazan.");
    expect(alert).not.toHaveTextContent("Šifra greške");
  });

  it("preserves retry and help actions", () => {
    const onRetry = vi.fn();
    renderError({ onRetry, helpHref: "/analytics/data-quality", helpLabel: "Otvori kvalitet podataka" });

    fireEvent.click(screen.getByRole("button", { name: "Pokušaj ponovo" }));

    expect(onRetry).toHaveBeenCalledOnce();
    expect(screen.getByRole("link", { name: "Otvori kvalitet podataka" })).toHaveAttribute(
      "href",
      "/analytics/data-quality",
    );
  });
});

import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import AnalyticsErrorState from "../AnalyticsErrorState";
import { ANALYTICS_EMPTY_ERROR_FALLBACK_MESSAGE } from "../../../utils/analyticsErrorMessages";

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
    expect(screen.getByText("FUTURE_ANALYTICS_ERROR_V2").closest("details")).not.toBeNull();
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
    expect(screen.getByText("sql_timeout_v2").closest("details")).not.toBeNull();
  });

  it("does not pass a technical page message through when no code is provided", () => {
    renderError({ message: "TypeError: Failed to fetch analytics payload" });

    const alert = screen.getByRole("alert");
    expect(alert).toHaveTextContent(ANALYTICS_EMPTY_ERROR_FALLBACK_MESSAGE);
    expect(alert).not.toHaveTextContent("TypeError: Failed to fetch analytics payload");
  });

  it("suppresses a lowercase technical code even when no separate error code is provided", () => {
    renderError({ message: "sql_timeout_v2" });

    const alert = screen.getByRole("alert");
    expect(alert).toHaveTextContent(ANALYTICS_EMPTY_ERROR_FALLBACK_MESSAGE);
    expect(alert).not.toHaveTextContent("sql_timeout_v2");
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

  it("maps contract errors, offers retry, and makes the correlation ID copyable", async () => {
    const onRetry = vi.fn();
    const writeText = vi.fn().mockResolvedValue(undefined);
    const originalClipboard = Object.getOwnPropertyDescriptor(navigator, "clipboard");
    Object.defineProperty(navigator, "clipboard", {
      configurable: true,
      value: { writeText },
    });

    try {
      renderError({
        code: "MISSING_OBJECT",
        meta: { success: false, errorCode: "MISSING_OBJECT", correlationId: "corr-contract-42" },
        onRetry,
      });

      expect(screen.getByRole("heading", { name: "Server je vratio neočekivan format podataka." })).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Pokušaj ponovo" })).toBeInTheDocument();
      fireEvent.click(screen.getByRole("button", { name: "Kopiraj ID" }));
      expect(writeText).toHaveBeenCalledWith("corr-contract-42");
      expect(await screen.findByText("ID je kopiran.")).toBeInTheDocument();
      fireEvent.click(screen.getByRole("button", { name: "Pokušaj ponovo" }));
      expect(onRetry).toHaveBeenCalledOnce();
    } finally {
      if (originalClipboard) Object.defineProperty(navigator, "clipboard", originalClipboard);
      else Reflect.deleteProperty(navigator, "clipboard");
    }
  });

  it("puts an unknown code in a keyboard-accessible disclosure and keeps the main copy safe", () => {
    renderError({ code: "FUTURE_ANALYTICS_ERROR_V3" });

    expect(screen.getByRole("heading", { name: "Analitika nije dostupna" })).toBeInTheDocument();
    expect(screen.getByText("FUTURE_ANALYTICS_ERROR_V3")).toBeInTheDocument();
    expect(screen.getByText("FUTURE_ANALYTICS_ERROR_V3").closest("details")).not.toBeNull();
    expect(screen.queryByRole("button", { name: "Pokušaj ponovo" })).not.toBeInTheDocument();
  });
});

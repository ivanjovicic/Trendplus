import React from "react";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { MemoryRouter } from "react-router-dom";
import HeaderStatus from "../HeaderStatus";
import { BackendStatusContext } from "../../../context/BackendStatusContext";
import { PingControlProvider } from "../../../context/PingControlContext";

const backendStatusValue = {
  status: "up" as const,
  online: true,
  checking: false,
  lastCheckedAt: Date.now(),
  lastReachableAt: Date.now(),
  lastUnavailableAt: null,
  lastError: null,
  hadConfirmedOutage: false,
  recoveryNoticeVisible: false,
  recoveryNoticeAt: null,
  providerState: {
    phase: "primary_ready" as const,
    activeHost: "primary" as const,
    reason: null,
    updatedAt: Date.now(),
    retryAfterMs: null,
  },
};

function renderHeader(pathname = "/analytics/products/123/edit") {
  return render(
    <MemoryRouter initialEntries={[pathname]}>
      <PingControlProvider>
        <BackendStatusContext.Provider value={backendStatusValue}>
          <HeaderStatus onOpenMobileNav={() => {}} />
        </BackendStatusContext.Provider>
      </PingControlProvider>
    </MemoryRouter>
  );
}

describe("HeaderStatus", () => {
  it("renders route-aware breadcrumbs and command center buttons", () => {
    renderHeader();

    expect(screen.getByRole("button", { name: /Komande/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Obaveštenja/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Kontekst/i })).toBeInTheDocument();
    expect(screen.getAllByRole("link", { name: /^Teme$/i })).toHaveLength(1);
    expect(screen.getByText("Analitika")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Odluke o proizvodima" })).toHaveAttribute(
      "href",
      "/analytics/products"
    );
    expect(screen.queryByRole("heading", { level: 1, name: "Odluke o proizvodima" })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Odluke o proizvodima" })).toBeInTheDocument();
    expect(screen.getByText("123")).toBeInTheDocument();
    expect(screen.getByText("Edit")).toBeInTheDocument();
  });

  it("keeps passive operational status in the header and links to its admin surface", () => {
    renderHeader();

    expect(screen.getAllByText(/Workeri:/i).length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText(/Redis:/i).length).toBeGreaterThanOrEqual(1);
    expect(screen.queryByRole("button", { name: /Start|Stop|uključi Redis|isključi Redis/i })).not.toBeInTheDocument();
    expect(screen.queryByText(/^API (ON|OFF)$/i)).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Status i operacije/i })).toHaveAttribute("href", "/admin/configuration?panel=workers");
    expect(screen.getAllByRole("link", { name: /^Teme$/i })).toHaveLength(1);
  });

  it("opens the command launcher with searchable routes", () => {
    renderHeader();

    fireEvent.click(screen.getByRole("button", { name: /Komande/i }));

    expect(screen.getByRole("heading", { level: 2, name: /^Komande$/i })).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 3, name: /Brze veze i akcije/i })).toBeInTheDocument();
    expect(screen.getByPlaceholderText(/Pretraži stranice i akcije/i)).toBeInTheDocument();
    expect(screen.getAllByRole("link", { name: /Pilot spremnost/i }).length).toBeGreaterThanOrEqual(1);
  });

  it("keeps compact header actions reachable through the mobile tools surface", () => {
    window.matchMedia = vi.fn().mockImplementation((query: string) => ({
      matches: query.includes("max-width"),
      media: query,
      onchange: null,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    }));

    renderHeader();

    fireEvent.click(screen.getByRole("button", { name: /Otvori dodatne kontrole/i }));
    const toolsDialog = screen.getByRole("dialog", { name: /Dodatne kontrole zaglavlja/i });
    fireEvent.click(within(toolsDialog).getByRole("button", { name: /^Komande$/i }));
    expect(screen.getByRole("heading", { level: 2, name: /^Komande$/i })).toBeInTheDocument();
  });

  it("keeps every header action reachable from the small-laptop overflow menu and restores focus after Escape", async () => {
    window.matchMedia = vi.fn().mockImplementation((query: string) => ({
      matches: query.includes("max-width: 1023px"),
      media: query,
      onchange: null,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    }));

    renderHeader();
    const moreButton = screen.getByRole("button", { name: /Otvori dodatne kontrole/i });
    moreButton.focus();
    fireEvent.click(moreButton);

    const toolsDialog = screen.getByRole("dialog", { name: /Dodatne kontrole zaglavlja/i });
    await waitFor(() => expect(toolsDialog).toHaveFocus());
    expect(within(toolsDialog).getByRole("button", { name: /^Komande$/i })).toBeInTheDocument();
    expect(within(toolsDialog).getByRole("button", { name: /^Obaveštenja$/i })).toBeInTheDocument();
    expect(within(toolsDialog).getByRole("button", { name: /^Kontekst$/i })).toBeInTheDocument();
    expect(within(toolsDialog).getByRole("combobox")).toBeInTheDocument();
    expect(within(toolsDialog).getByRole("link", { name: /^Teme$/i })).toBeInTheDocument();
    expect(within(toolsDialog).getByRole("button", { name: /^Osveži$/i })).toBeInTheDocument();
    expect(within(toolsDialog).getByRole("link", { name: /Otvori status i operacije/i })).toHaveAttribute("href", "/admin/configuration?panel=workers");
    expect(within(toolsDialog).queryByRole("button", { name: /Start|Stop/i })).not.toBeInTheDocument();

    fireEvent.keyDown(document, { key: "Escape" });
    expect(screen.queryByRole("dialog", { name: /Dodatne kontrole zaglavlja/i })).not.toBeInTheDocument();
    expect(moreButton).toHaveFocus();
  });

  it("opens the notification inbox with backend and analytics signals", () => {
    renderHeader();

    fireEvent.click(screen.getByRole("button", { name: /Obaveštenja/i }));

    expect(screen.getByRole("heading", { level: 2, name: /^Obaveštenja$/i })).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 3, name: /Signali i akcije/i })).toBeInTheDocument();
    expect(screen.getByText(/Backend je potvrđen/i)).toBeInTheDocument();
    expect(screen.getByText(/Analitički signalni centar/i)).toBeInTheDocument();
  });
});

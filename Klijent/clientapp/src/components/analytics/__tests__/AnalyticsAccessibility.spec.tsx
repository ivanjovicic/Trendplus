import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { useState } from "react";
import { describe, expect, it, vi } from "vitest";
import Modal from "../../Modal";
import AnalyticsTableToolbar from "../AnalyticsTableToolbar";
import type { AnalyticsTableColumn } from "../../../types/analyticsTable";

vi.mock("../../ui/InfoTip", () => ({
  default: ({ text }: { text: string }) => <span title={text}>i</span>,
}));

type Row = { name: string; revenue: number };

const columns: AnalyticsTableColumn<Row>[] = [
  { key: "name", header: "Naziv", dataType: "text" },
  { key: "revenue", header: "Prihod", dataType: "currency" },
];

function ModalHarness() {
  const [open, setOpen] = useState(false);

  return (
    <div>
      <button type="button" onClick={() => setOpen(true)}>
        Otvori modal
      </button>
      <Modal
        isOpen={open}
        onClose={() => setOpen(false)}
        title={"Analytics pode\u0161avanja"}
      >
        <button type="button">Prva akcija</button>
        <input aria-label={"Naziv izve\u0161taja"} />
        <button type="button">Poslednja akcija</button>
      </Modal>
    </div>
  );
}

function renderToolbar() {
  return render(
    <AnalyticsTableToolbar
      tableKey="accessibility-table"
      tableTitle="Accessibility tabela"
      columns={columns}
      rows={[{ name: "Model A", revenue: 120000 }]}
    />,
  );
}

describe("analytics modal accessibility", () => {
  it("labels the dialog, focuses close, traps Tab and restores trigger focus", async () => {
    render(<ModalHarness />);

    const trigger = screen.getByRole("button", { name: "Otvori modal" });
    trigger.focus();
    fireEvent.click(trigger);

    const dialog = screen.getByRole("dialog", {
      name: "Analytics pode\u0161avanja",
    });
    expect(dialog).toHaveAttribute("aria-modal", "true");
    expect(document.body.style.overflow).toBe("hidden");

    const closeButton = screen.getByRole("button", { name: "Zatvori" });
    await waitFor(() => expect(closeButton).toHaveFocus());

    const lastButton = screen.getByRole("button", { name: "Poslednja akcija" });
    lastButton.focus();
    fireEvent.keyDown(document, { key: "Tab" });
    expect(closeButton).toHaveFocus();

    closeButton.focus();
    fireEvent.keyDown(document, { key: "Tab", shiftKey: true });
    expect(lastButton).toHaveFocus();

    fireEvent.keyDown(document, { key: "Escape" });
    await waitFor(() =>
      expect(
        screen.queryByRole("dialog", { name: "Analytics pode\u0161avanja" }),
      ).not.toBeInTheDocument(),
    );
    expect(trigger).toHaveFocus();
    expect(document.body.style.overflow).toBe("unset");
  });

  it("closes from the backdrop without exposing the backdrop as interactive content", async () => {
    const { container } = render(<ModalHarness />);
    fireEvent.click(screen.getByRole("button", { name: "Otvori modal" }));
    await screen.findByRole("dialog", { name: "Analytics pode\u0161avanja" });

    const backdrop = container.querySelector(".modal-backdrop");
    expect(backdrop).toHaveAttribute("aria-hidden", "true");
    fireEvent.click(backdrop as HTMLElement);

    await waitFor(() =>
      expect(
        screen.queryByRole("dialog", { name: "Analytics pode\u0161avanja" }),
      ).not.toBeInTheDocument(),
    );
  });
});

describe("analytics export menu accessibility", () => {
  it("uses menu semantics and supports Arrow/Home/End/Escape keyboard navigation", async () => {
    renderToolbar();

    const trigger = screen.getByRole("button", { name: /Izvoz/i });
    expect(trigger).toHaveAttribute("aria-haspopup", "menu");
    expect(trigger).toHaveAttribute("aria-expanded", "false");

    fireEvent.click(trigger);

    const menu = screen.getByRole("menu", { name: "Formati izvoza" });
    expect(trigger).toHaveAttribute("aria-expanded", "true");
    expect(trigger).toHaveAttribute("aria-controls", menu.id);

    const pdf = screen.getByRole("menuitem", { name: "Izvezi kao PDF" });
    const excel = screen.getByRole("menuitem", { name: "Izvezi kao Excel" });
    const csv = screen.getByRole("menuitem", { name: "Izvezi kao CSV" });
    await waitFor(() => expect(pdf).toHaveFocus());

    fireEvent.keyDown(document, { key: "ArrowDown" });
    expect(excel).toHaveFocus();

    fireEvent.keyDown(document, { key: "End" });
    expect(csv).toHaveFocus();

    fireEvent.keyDown(document, { key: "Home" });
    expect(pdf).toHaveFocus();

    fireEvent.keyDown(document, { key: "ArrowUp" });
    expect(csv).toHaveFocus();

    fireEvent.keyDown(document, { key: "Escape" });
    expect(
      screen.queryByRole("menu", { name: "Formati izvoza" }),
    ).not.toBeInTheDocument();
    expect(trigger).toHaveAttribute("aria-expanded", "false");
    expect(trigger).toHaveFocus();
  });

  it("opens an accessibly named export dialog with labelled controls", async () => {
    renderToolbar();

    fireEvent.click(screen.getByRole("button", { name: /Izvoz/i }));
    fireEvent.click(screen.getByRole("menuitem", { name: "Izvezi kao PDF" }));

    const dialog = screen.getByRole("dialog", {
      name: "Export Accessibility tabela",
    });
    expect(dialog).toBeInTheDocument();
    expect(screen.getByLabelText("Format")).toHaveValue("pdf");
    expect(screen.getByLabelText("Orijentacija")).toHaveValue("landscape");
    expect(
      screen.getByRole("checkbox", {
        name: "Uklju\u010Di filtere i metadata",
      }),
    ).toBeChecked();
    expect(
      screen.getByRole("checkbox", {
        name: "Otvori preview pre eksportovanja (samo PDF)",
      }),
    ).toBeEnabled();
    await waitFor(() =>
      expect(screen.getByRole("button", { name: "Zatvori" })).toHaveFocus(),
    );
  });

  it("closes the open export menu when clicking outside", async () => {
    renderToolbar();

    fireEvent.click(screen.getByRole("button", { name: /Izvoz/i }));
    await screen.findByRole("menu", { name: "Formati izvoza" });
    fireEvent.mouseDown(document.body);

    expect(
      screen.queryByRole("menu", { name: "Formati izvoza" }),
    ).not.toBeInTheDocument();
  });
});

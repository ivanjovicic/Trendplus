import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import CreateProdajaForm from "./CreateProdajaForm";
import { ToastProvider } from "../Toast";

describe("CreateProdajaForm mobile workflow contract", () => {
  it("searches, adds and edits an item, then submits the unchanged numeric payload", async () => {
    vi.useFakeTimers();
    const onSubmit = vi.fn().mockResolvedValue(undefined);

    try {
      render(
        <ToastProvider>
          <CreateProdajaForm
            artikli={[{ id: 1, naziv: "Patike A", cena: 20 }]}
            onSubmit={onSubmit}
          />
        </ToastProvider>,
      );

      fireEvent.change(screen.getByPlaceholderText("Broj racuna"), { target: { value: " POS-001 " } });
      const search = screen.getByPlaceholderText("Pretrazi artikle po nazivu...");
      fireEvent.change(search, { target: { value: "Patike" } });
      await act(async () => {
        vi.advanceTimersByTime(260);
      });
      vi.useRealTimers();

      fireEvent.click(await screen.findByRole("button", { name: /Patike A/ }));
      const quantities = screen.getAllByRole("spinbutton");
      fireEvent.change(quantities[2], { target: { value: "2" } });
      fireEvent.change(quantities[3], { target: { value: "17.5" } });
      fireEvent.click(screen.getByRole("button", { name: "Sacuvaj prodaju" }));

      await waitFor(() => expect(onSubmit).toHaveBeenCalledWith({
        brojRacuna: "POS-001",
        idObjekat: 1,
        nacinPlacanja: "Gotovina",
        stavke: [
          { idArtikal: 1, kolicina: 1, cena: 20 },
          { idArtikal: 1, kolicina: 2, cena: 17.5 },
        ],
      }));
    } finally {
      vi.useRealTimers();
    }
  });
});

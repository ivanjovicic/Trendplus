import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import PovracajPage from "../PovracajPage";
import { getPovracaji } from "../../services/povracajApi";

vi.mock("../../services/povracajApi", () => ({ getPovracaji: vi.fn() }));

describe("PovracajPage responsive list", () => {
  beforeEach(() => {
    vi.mocked(getPovracaji).mockResolvedValue({
      items: [{
        id: 1, brojZapisnika: "P-1", datumPovracaja: "2026-10-01T10:00:00Z", dobavljacId: 4,
        dobavljacNaziv: "Dobavljač", status: "Kreiran", ukupanIznos: 2400,
        datumKreiranja: "2026-10-01T10:00:00Z", brojStavki: 2,
      }], totalCount: 1, pageNumber: 1, pageSize: 25,
    });
  });

  it("keeps search ahead of a keyboard-focusable table with the return number pinned", async () => {
    render(<PovracajPage />);

    const search = await screen.findByRole("textbox", { name: "Pretraga povraćaja po broju zapisnika ili dobavljaču" });
    const region = screen.getByRole("region", { name: "Lista povraćaja" });
    expect(search.compareDocumentPosition(region) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(region).toHaveAttribute("tabindex", "0");
    expect(region.querySelector("tbody td")).toHaveClass("sticky", "left-0");
    expect(screen.getByRole("button", { name: "+ Novi povracaj" })).toBeInTheDocument();
  });
});

import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import DnevnikPromenaPage from "../DnevnikPromenaPage";
import { getDnevnikPromena } from "../../services/dnevnikPromenaApi";

vi.mock("../../services/dnevnikPromenaApi", () => ({
  getDnevnikPromena: vi.fn(),
  getDnevnikPromenaById: vi.fn(),
}));

describe("DnevnikPromenaPage responsive table", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true, json: async () => ["Promena cene"] }));
    vi.mocked(getDnevnikPromena).mockResolvedValue({
      items: [{
        id: 1, tipPromene: "Promena cene", datum: "2026-10-01T10:00:00Z", iznos: 10,
        brojRacuna: "R-1", artikalId: 7, artikalNaziv: "Sintetička patika", dobavljacId: 2,
        dobavljacNaziv: "Dobavljač", staraProdajnaCena: 100, novaProdajnaCena: 110,
        komentar: "Pilot zapis", korisnikIme: "Fixture",
      }], totalCount: 1, pageNumber: 1, pageSize: 50, sortBy: "datum", sortDir: "desc",
    });
  });

  afterEach(() => vi.unstubAllGlobals());

  it("keeps the key date column and details action reachable in the contained list", async () => {
    render(<MemoryRouter initialEntries={["/dnevnik-promena"]}><DnevnikPromenaPage /></MemoryRouter>);

    const region = await screen.findByRole("region", { name: "Tabela dnevnika promena" });
    expect(region).toHaveAttribute("tabindex", "0");
    expect(region.querySelector("tbody td")).toHaveClass("sticky", "left-0");
    expect(screen.getByRole("button", { name: "Detalji" })).toHaveClass("min-h-11");
    expect(screen.getByRole("button", { name: /Filteri/ })).toBeInTheDocument();
  });
});

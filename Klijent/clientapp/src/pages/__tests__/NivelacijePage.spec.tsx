import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import NivelacijePage from "../NivelacijePage";
import { getNivelacije } from "../../services/artikliApi";

vi.mock("../../services/artikliApi", () => ({
  getNivelacije: vi.fn(),
}));

describe("NivelacijePage", () => {
  beforeEach(() => {
    vi.mocked(getNivelacije).mockResolvedValue({
      items: [
        {
          id: 1,
          datum: "2026-10-03T10:00:00Z",
          tipPromene: "Nivelacija",
          artikalId: 14,
          idObjekat: null,
          artikalNaziv: "Patike",
          staraProdajnaCena: 100,
          novaProdajnaCena: 90,
          komentar: null,
          korisnikIme: "operator",
        },
      ],
      totalCount: 1,
      pageNumber: 1,
      pageSize: 50,
      sortBy: "datum",
      sortDir: "desc",
    });
  });

  it("labels imported changes and chain-wide events and uses calendar-day filters", async () => {
    render(
      <MemoryRouter initialEntries={["/nivelacije?fromDate=2026-01-01&artikalId=14"]}>
        <NivelacijePage />
      </MemoryRouter>,
    );

    expect(await screen.findByText("Uvezena nivelacija")).toBeInTheDocument();
    expect(screen.getByText("Sve prodavnice (lančano)")).toBeInTheDocument();
    expect(screen.getByText("Promena cene bez izabrane prodavnice je lančana i važi za sve prodavnice.")).toBeInTheDocument();
    expect(screen.getByLabelText("Od datuma (Beograd)")).toHaveAttribute("type", "date");
    expect(screen.getByLabelText("Do datuma, uključujući ceo dan")).toHaveAttribute("type", "date");
    expect(screen.getByLabelText("Artikal ID")).toHaveValue("14");
    expect(screen.getByRole("region", { name: "Tabela nivelacija" })).toHaveAttribute("tabindex", "0");
  });

  it("surfaces load errors with alert semantics and retry", async () => {
    vi.mocked(getNivelacije).mockRejectedValueOnce(new Error("mrežna greška"));

    render(
      <MemoryRouter initialEntries={["/nivelacije"]}>
        <NivelacijePage />
      </MemoryRouter>,
    );

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("mrežna greška");
    expect(screen.getByRole("button", { name: "Pokušaj ponovo" })).toBeInTheDocument();
  });
});

import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
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
    render(<NivelacijePage />);

    expect(await screen.findByText("Uvezena nivelacija")).toBeInTheDocument();
    expect(screen.getByText("Sve prodavnice (lančano)")).toBeInTheDocument();
    expect(screen.getByText("Promena cene bez izabrane prodavnice je lančana i važi za sve prodavnice.")).toBeInTheDocument();
    expect(screen.getByLabelText("Od datuma (Beograd)")).toHaveAttribute("type", "date");
    expect(screen.getByLabelText("Do datuma, uključujući ceo dan")).toHaveAttribute("type", "date");
  });
});

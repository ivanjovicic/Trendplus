import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import ArtikliListPage from "./ArtikliListPage";
import { getArtikliPaged } from "../services/artikliApi";
import { getDobavljaci } from "../services/dobavljaciApi";
import { getSezone } from "../services/sezoneApi";

vi.mock("../services/artikliApi", () => ({ getArtikliPaged: vi.fn() }));
vi.mock("../services/dobavljaciApi", () => ({ getDobavljaci: vi.fn() }));
vi.mock("../services/sezoneApi", () => ({ getSezone: vi.fn() }));
vi.mock("../utils/dataScope", () => ({
  getDataScope: () => "all",
  setDataScope: vi.fn(),
}));

const article = {
  id: 701,
  naziv: "Sintetičke patike",
  prodajnaCena: 7490,
  nabavnaCena: 4200,
  kolicina: 8,
  dobavljacId: 21,
  dobavljacNaziv: "Sintetički dobavljač",
};

function renderPage() {
  return render(<MemoryRouter><ArtikliListPage /></MemoryRouter>);
}

describe("ArtikliListPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    sessionStorage.clear();
    localStorage.clear();
    vi.mocked(getArtikliPaged).mockResolvedValue({ items: [article], totalCount: 51, pageNumber: 1, pageSize: 50 });
    vi.mocked(getSezone).mockResolvedValue([]);
    vi.mocked(getDobavljaci).mockResolvedValue([]);
  });

  it("keeps every article field in a keyboard-scrollable table region", async () => {
    renderPage();
    const region = await screen.findByRole("region", { name: "Tabela sa vodoravnim pomeranjem" });

    expect(region).toHaveAttribute("tabindex", "0");
    expect(within(region).getByRole("columnheader", { name: /ID/ })).toBeInTheDocument();
    expect(within(region).getByRole("columnheader", { name: /Naziv/ })).toBeInTheDocument();
    expect(within(region).getByRole("columnheader", { name: /Prodajna cena/ })).toBeInTheDocument();
    expect(within(region).getByRole("columnheader", { name: /Nabavna cena/ })).toBeInTheDocument();
    expect(within(region).getByRole("columnheader", { name: /Količina/ })).toBeInTheDocument();
    expect(within(region).getByRole("columnheader", { name: /Dobavljač/ })).toBeInTheDocument();
    expect(within(region).getByRole("columnheader", { name: "Akcija" })).toBeInTheDocument();
    expect(screen.getByRole("note")).toHaveTextContent("Tabela se pomera vodoravno");
  });

  it("preserves server paging and sort parameters while responsive filters are used", async () => {
    renderPage();
    await screen.findByText("Sintetičke patike");
    expect(getArtikliPaged).toHaveBeenCalledWith(1, 50, expect.objectContaining({ sortBy: "naziv", sortDir: "asc" }));

    fireEvent.click(screen.getByRole("button", { name: /Filteri/ }));
    fireEvent.change(screen.getByRole("textbox", { name: "Filter po nazivu" }), { target: { value: "Sintetičke" } });
    await waitFor(() => expect(getArtikliPaged).toHaveBeenCalledWith(1, 50, expect.objectContaining({
      naziv: "Sintetičke",
      sortBy: "naziv",
      sortDir: "asc",
    })));

    fireEvent.change(screen.getByRole("combobox", { name: "Broj artikala po strani" }), { target: { value: "25" } });
    await waitFor(() => expect(getArtikliPaged).toHaveBeenCalledWith(1, 25, expect.objectContaining({ naziv: "Sintetičke" })));
    fireEvent.click(screen.getByRole("button", { name: "Sledeća strana" }));
    await waitFor(() => expect(getArtikliPaged).toHaveBeenCalledWith(2, 25, expect.objectContaining({
      naziv: "Sintetičke",
      sortBy: "naziv",
      sortDir: "asc",
    })));

    fireEvent.click(screen.getByRole("button", { name: "Sortiraj po Prodajna cena" }));
    await waitFor(() => expect(getArtikliPaged).toHaveBeenCalledWith(1, 25, expect.objectContaining({
      naziv: "Sintetičke",
      sortBy: "prodajnaCena",
      sortDir: "asc",
    })));
  });
});

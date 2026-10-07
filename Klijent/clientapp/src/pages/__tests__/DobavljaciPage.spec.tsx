import { fireEvent, render, screen, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import DobavljaciPage from "../DobavljaciPage";
import { getDobavljaci } from "../../services/dobavljaciApi";

vi.mock("../../services/dobavljaciApi", () => ({
  createDobavljac: vi.fn(),
  getDobavljaci: vi.fn(),
}));

const suppliers = Array.from({ length: 8 }, (_, index) => ({
  id: index + 1,
  naziv: `Dobavljač ${index + 1}`,
  adresa: `Adresa ${index + 1}`,
  telefon: `011-100-${index}`,
  napomena: "Pilot zapis",
}));

describe("DobavljaciPage responsive list", () => {
  beforeEach(() => {
    vi.mocked(getDobavljaci).mockResolvedValue(suppliers);
  });

  it("puts supplier search before the first five rows and exposes coarse-pointer actions", async () => {
    render(<DobavljaciPage />);

    const search = await screen.findByRole("searchbox", { name: "Pretraži dobavljače" });
    const list = screen.getByRole("region", { name: "Tabela dobavljača" });
    expect(search.compareDocumentPosition(list) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(within(list).getAllByRole("row")).toHaveLength(9);

    const actionMenu = screen.getByLabelText("Akcije za Dobavljač 1");
    fireEvent.click(actionMenu);
    const details = actionMenu.closest("details");
    expect(details).not.toBeNull();
    expect(within(details as HTMLElement).getByRole("button", { name: "Izmeni", hidden: true })).toBeInTheDocument();
    expect(within(details as HTMLElement).getByRole("button", { name: "Obriši", hidden: true })).toBeInTheDocument();
  });

  it("filters by contact fields without changing the loaded supplier set", async () => {
    render(<DobavljaciPage />);

    fireEvent.change(await screen.findByRole("searchbox", { name: "Pretraži dobavljače" }), {
      target: { value: "011-100-5" },
    });
    const list = screen.getByRole("region", { name: "Tabela dobavljača" });
    expect(await within(list).findByText("Dobavljač 6")).toBeInTheDocument();
    expect(within(list).queryByText("Dobavljač 1")).not.toBeInTheDocument();
    expect(getDobavljaci).toHaveBeenCalled();
  });
});

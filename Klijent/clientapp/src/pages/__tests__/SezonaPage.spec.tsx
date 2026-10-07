import { fireEvent, render, screen, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import SezonaPage from "../SezonaPage";
import { getSezone } from "../../services/sezoneApi";

vi.mock("../../services/sezoneApi", () => ({
  createSezona: vi.fn(),
  getSezone: vi.fn(),
}));

describe("SezonaPage responsive list", () => {
  beforeEach(() => {
    vi.mocked(getSezone).mockResolvedValue([
      { id: 1, naziv: "Proleće/Leto 2026", datumOd: "2026-03-01", datumDo: "2026-08-31" },
      { id: 2, naziv: "Jesen/Zima 2026/2027", datumOd: "2026-09-01", datumDo: "2027-02-28" },
    ]);
  });

  it("shows search before the scrollable key-column table and filters locally", async () => {
    render(<SezonaPage />);

    const search = await screen.findByRole("searchbox", { name: "Pretraži sezone" });
    expect(screen.getByRole("region", { name: "Tabela sezona" })).toBeInTheDocument();
    fireEvent.change(search, { target: { value: "zima" } });
    const region = screen.getByRole("region", { name: "Tabela sezona" });
    expect(within(region).getByText("Jesen/Zima 2026/2027")).toBeInTheDocument();
    expect(within(region).queryByText("Proleće/Leto 2026")).not.toBeInTheDocument();
  });
});

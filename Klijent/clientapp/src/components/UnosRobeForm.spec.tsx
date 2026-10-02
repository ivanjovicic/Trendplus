import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { describe, expect, it } from "vitest";
import UnosRobeForm from "./UnosRobeForm";

function NavigationState() {
  const location = useLocation();
  return <output data-testid="navigation-state">{JSON.stringify(location.state)}</output>;
}

describe("UnosRobeForm workflow contract", () => {
  it("selects a supplier and continues with the trimmed receipt and existing route state", () => {
    render(
      <MemoryRouter>
        <UnosRobeForm dobavljaci={[{ id: 7, naziv: "Dobavljac A", adresa: "Novi Sad" }]} />
        <NavigationState />
      </MemoryRouter>,
    );

    fireEvent.change(screen.getByPlaceholderText("Npr. PR-2026-001"), { target: { value: " INV-7 " } });
    fireEvent.change(screen.getByPlaceholderText("Naziv, adresa ili telefon..."), { target: { value: "Dobavljac" } });
    fireEvent.click(screen.getByRole("button", { name: /Dobavljac A/ }));
    fireEvent.click(screen.getByRole("button", { name: /Nastavi na unos artikala/ }));

    expect(screen.getByTestId("navigation-state")).toHaveTextContent(JSON.stringify({
      dobavljacId: 7,
      dobavljacNaziv: "Dobavljac A",
      brojRacuna: "INV-7",
    }));
  });
});

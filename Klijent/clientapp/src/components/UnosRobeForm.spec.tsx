import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import UnosRobeForm from "./UnosRobeForm";
import { getStores } from "../services/analyticsApi";

vi.mock("../services/analyticsApi", async (importOriginal) => ({
  ...await importOriginal<typeof import("../services/analyticsApi")>(),
  getStores: vi.fn(),
}));

function NavigationState() {
  const location = useLocation();
  return <output data-testid="navigation-state">{JSON.stringify(location.state)}</output>;
}

describe("UnosRobeForm workflow contract", () => {
  beforeEach(() => vi.mocked(getStores).mockResolvedValue([{ storeId: -4, storeName: "Magacin" }]));

  it("requires a supplier, reference and physical location before navigating with the receipt header", async () => {
    render(
      <MemoryRouter>
        <UnosRobeForm dobavljaci={[{ id: 7, naziv: "Dobavljac A", adresa: "Novi Sad" }]} />
        <NavigationState />
      </MemoryRouter>,
    );

    fireEvent.change(screen.getByLabelText(/Dobavljač/), { target: { value: "7" } });
    fireEvent.change(screen.getByLabelText(/Broj računa ili otpremnice/), { target: { value: " INV-7 " } });
    fireEvent.change(await screen.findByLabelText(/Prodavnica ili skladište prijema/), { target: { value: "-4" } });
    fireEvent.click(screen.getByRole("button", { name: /Nastavi na stavke/ }));

    await waitFor(() => expect(JSON.parse(screen.getByTestId("navigation-state").textContent ?? "null")).toMatchObject({
      receiptHeader: {
        supplierId: 7,
        supplierName: "Dobavljac A",
        documentNumber: "INV-7",
        documentType: "invoice",
        storeId: -4,
        storeName: "Magacin",
      },
    }));
    expect(JSON.parse(screen.getByTestId("navigation-state").textContent ?? "null").receiptHeader.requestKey).toMatch(/^[0-9a-f-]{36}$/i);
  });
});

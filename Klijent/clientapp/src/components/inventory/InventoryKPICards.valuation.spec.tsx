import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { InventoryKPICards } from "./InventoryKPICards";

describe("InventoryKPICards valuation copy", () => {
  it("labels estimated valuation basis in the KPI title", () => {
    render(
      <InventoryKPICards
        totalSku={10}
        totalOnHand={100}
        lowStockCount={2}
        lowStockShare={0.2}
        avgUnitsPerSku={10}
        totalValue={50000}
        valuationIsEstimated
        valueCoveragePct={82.5}
      />,
    );

    expect(screen.getByText("Procena vrednosti (procena)")).toBeInTheDocument();
    expect(screen.getByText(/Procena zasnovana na poslednjoj prodajnoj nabavnoj ceni/i)).toBeInTheDocument();
  });
});

import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import SupplierRecommendationRail from "./SupplierRecommendationRail";

describe("SupplierRecommendationRail trust rendering", () => {
  it("surfaces trust and status reason on supplier recommendation cards", () => {
    render(
      <SupplierRecommendationRail
        topGrowSuppliers={[
          {
            supplierId: 11,
            supplierName: "Dobavljač 11",
            revenue: 12000,
            mlSupplierScore: 76,
            supplierQualityIndex: 81,
            recommendationCode: "EXPAND",
            confidenceScore: 88,
            reliabilityPct: 82,
            dataQualityStatus: "warning",
            statusReason: "Signal je dobar, ali uz upozorenje na pokrivenost.",
            reasonCodes: ["coverage_gap"],
          },
        ]}
        topRiskSuppliers={[]}
        onSelectSupplier={vi.fn()}
      />
    );

    expect(screen.getByText("Trust signala")).toBeInTheDocument();
    expect(screen.getByText("Pouzdanost: 82% · Kvalitet: Oprez")).toBeInTheDocument();
    expect(screen.getByText("Signal je dobar, ali uz upozorenje na pokrivenost.")).toBeInTheDocument();
  });
});

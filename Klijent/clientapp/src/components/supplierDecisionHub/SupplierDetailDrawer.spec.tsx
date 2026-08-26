import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import SupplierDetailDrawer from "./SupplierDetailDrawer";
import type { SupplierDecisionDetailsResponse } from "../../services/supplierDecisionHubApi";

function details(): SupplierDecisionDetailsResponse {
  return {
    supplierHeader: {
      supplierId: 42,
      supplierName: "Dobavljač 42",
      periodFrom: "2026-04-03T00:00:00Z",
      periodTo: "2026-07-01T00:00:00Z",
      mlSupplierScore: 81.5,
      aiExplanation: "Stabilan signal.",
      topFeature1: "margin",
      topFeature2: "sellthrough",
      topFeature3: "stock",
      supplierQualityIndex: 78,
      recommendationCode: "EXPAND",
      confidenceScore: 88,
      reliabilityPct: 82,
      dataQualityStatus: "warning",
      statusReason: "Signal je dobar, ali uz upozorenje na pokrivenost.",
      reasonCodes: ["coverage_gap"],
    },
    kpis: {
      revenue: 12000,
      units: 180,
      fullPriceRevenueShare: 0.62,
      fullPriceSellthrough: 0.48,
      markdownRevenueShare: 0.24,
      preMarkdownMarginPct: 0.34,
      deadStockRate: 0.08,
      unsoldStockValue: 1200,
      repeatWinnerRate: 0.42,
      capitalAtRisk: 2400,
    },
    categoryBreakdown: [],
    winningArticles: [],
    markdownDependentArticles: [],
    blockedByOosArticles: [],
    recommendationHistory: [],
  };
}

describe("SupplierDetailDrawer trust rendering", () => {
  it("shows backend-owned reliability, data quality and status reason in the drawer header", () => {
    render(
      <SupplierDetailDrawer
        open
        details={details()}
        onClose={vi.fn()}
      />
    );

    expect(screen.getByRole("heading", { name: "Dobavljač 42" })).toBeInTheDocument();
    expect(screen.getByText("Trust signala: 82%")).toBeInTheDocument();
    expect(screen.getByText("Kvalitet podataka: Oprez")).toBeInTheDocument();
    expect(screen.getByText("Razlog signala: Signal je dobar, ali uz upozorenje na pokrivenost.")).toBeInTheDocument();
  });
});

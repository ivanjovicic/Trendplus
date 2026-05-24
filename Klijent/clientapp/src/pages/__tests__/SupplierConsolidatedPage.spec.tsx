import { render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import React, { useEffect } from "react";
import { MemoryRouter } from "react-router-dom";
import SupplierConsolidatedPage from "../SupplierConsolidatedPage";

vi.mock("../../services/analyticsApi", () => ({
  getStores: vi.fn().mockResolvedValue([]),
  getSupplierFilters: vi.fn().mockResolvedValue([]),
}));

vi.mock("../SupplierSalesStatsPage", () => ({
  default: function MockSupplierSalesStatsPage(props: any) {
    useEffect(() => {
      props.onTrustMetadataChange?.({
        requestedDataset: "30d",
        effectiveDataset: "90d",
        effectivePeriodLabel: "Poslednjih 90 dana",
        usedFallback: true,
        fallbackReason: "Trazeni 30d nema zaseban scorecard dataset.",
        fallbackReasonCode: "no_mv_30d",
        dataQualityStatus: "warning",
        recommendationAllowed: false,
        recommendationNote: "Mock overview tab",
      });
    }, [props.onTrustMetadataChange]);

    return <div data-testid="mock-overview">Overview</div>;
  },
}));

vi.mock("../SupplierDecisionHubPage", () => ({
  default: function MockSupplierDecisionHubPage() {
    return <div data-testid="mock-scorecard">Scorecard</div>;
  },
}));

vi.mock("../SupplierFootwearAnalyticsPage", () => ({
  default: function MockSupplierFootwearAnalyticsPage() {
    return <div data-testid="mock-assortment">Assortment</div>;
  },
}));

describe("SupplierConsolidatedPage", () => {
  it("renders consolidated trust header and shows fallback banner when child reports fallback", async () => {
    render(
      <MemoryRouter initialEntries={["/analytics/supplier"]}>
        <SupplierConsolidatedPage />
      </MemoryRouter>
    );

    expect(screen.getByText("Dobavljaci")).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText("Dataset")).toBeInTheDocument();
      expect(screen.getByText(/30d → 90d/)).toBeInTheDocument();
      expect(screen.getByText(/Fallback aktiviran\./)).toBeInTheDocument();
    });
  });
});

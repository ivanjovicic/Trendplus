import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import React, { useEffect } from "react";
import { MemoryRouter } from "react-router-dom";
import SupplierConsolidatedPage from "../SupplierConsolidatedPage";
import { getSupplierFilters } from "../../services/analyticsApi";

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
        provenanceBasis: "mv_supplier_decision_score_cache_90d",
        usedFallback: true,
        fallbackReason: "no_data_30d",
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

    expect(screen.getAllByText("Dobavljači").length).toBeGreaterThan(0);
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("heading", { level: 1, name: "Dobavljači" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 2, name: "Dobavljači" })).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText("Dataset")).toBeInTheDocument();
      expect(screen.getByText(/30d\s*(→|->)\s*90d/)).toBeInTheDocument();
      expect(screen.getByText("mv_supplier_decision_score_cache_90d")).toBeInTheDocument();
      expect(screen.getByText(/Fallback aktiviran\./)).toBeInTheDocument();
      expect(screen.queryByText(/no_data_30d/i)).not.toBeInTheDocument();
    });
  });

  it("explains an Operations legacy source while preserving the canonical tab", () => {
    render(
      <MemoryRouter initialEntries={["/analytics/supplier?tab=assortment&legacySource=operations-supplier-footwear&dataScope=imported"]}>
        <SupplierConsolidatedPage />
      </MemoryRouter>,
    );

    expect(screen.getByTestId("supplier-legacy-context")).toHaveTextContent(
      "Kompatibilna veza iz Operacija otvorila je canonical Pregled dobavljača, tab Asortiman",
    );
    expect(screen.getByRole("button", { name: /Asortiman/i })).toHaveAttribute("aria-selected", "true");
  });

  it.each([
    ["overview", "mock-overview", "Pregled"],
    ["scorecard", "mock-scorecard", "Skorkarta"],
    ["assortment", "mock-assortment", "Asortiman"],
  ] as const)("keeps one title and one filter owner on the %s tab", async (tab, testId, tabLabel) => {
    render(
      <MemoryRouter initialEntries={[`/analytics/supplier?tab=${tab}`]}>
        <SupplierConsolidatedPage />
      </MemoryRouter>,
    );

    expect(await screen.findByTestId(testId)).toBeInTheDocument();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("heading", { level: 1, name: "Dobavljači" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 2, name: "Dobavljači" })).toBeInTheDocument();
    expect(screen.getAllByLabelText("Supplier filteri")).toHaveLength(1);
    expect(screen.getByRole("button", { name: new RegExp(tabLabel, "i") })).toHaveAttribute("aria-selected", "true");
    expect(screen.queryByText("Opseg i filteri")).not.toBeInTheDocument();
    expect(screen.queryByText("Kontrole asortimana")).not.toBeInTheDocument();
  });

  it("requests supplier filters with canonical dataScope and clears invalid supplier selection", async () => {
    vi.mocked(getSupplierFilters).mockResolvedValue([
      { supplierId: 202, supplierName: "Dobavljač B" },
    ] as Awaited<ReturnType<typeof getSupplierFilters>>);

    render(
      <MemoryRouter initialEntries={["/analytics/supplier?dataScope=imported&supplierId=101"]}>
        <SupplierConsolidatedPage />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(getSupplierFilters).toHaveBeenCalledWith(
        expect.any(String),
        expect.any(String),
        true,
        null,
        "imported",
      );
    });

    await waitFor(() => {
      expect(screen.getByLabelText("Dobavljač")).toHaveValue("");
    });
  });

  it("does not duplicate filter owners when switching tabs", async () => {
    render(
      <MemoryRouter initialEntries={["/analytics/supplier?tab=overview"]}>
        <SupplierConsolidatedPage />
      </MemoryRouter>,
    );

    expect(await screen.findByTestId("mock-overview")).toBeInTheDocument();
    expect(screen.getAllByLabelText("Supplier filteri")).toHaveLength(1);

    fireEvent.click(screen.getByRole("button", { name: /Skorkarta/i }));
    expect(await screen.findByTestId("mock-scorecard")).toBeInTheDocument();
    expect(screen.getAllByLabelText("Supplier filteri")).toHaveLength(1);
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);

    fireEvent.click(screen.getByRole("button", { name: /Asortiman/i }));
    expect(await screen.findByTestId("mock-assortment")).toBeInTheDocument();
    expect(screen.getAllByLabelText("Supplier filteri")).toHaveLength(1);
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
  });
});

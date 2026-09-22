import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import React, { useEffect } from "react";
import { MemoryRouter, useLocation } from "react-router-dom";
import SupplierConsolidatedPage from "../SupplierConsolidatedPage";
import { getSupplierFilters } from "../../services/analyticsApi";

vi.mock("../../services/analyticsApi", () => ({
  getStores: vi.fn().mockResolvedValue([]),
  getSupplierFilters: vi.fn().mockResolvedValue([]),
}));

vi.mock("../../services/sezoneApi", () => ({
  getSezone: vi.fn().mockResolvedValue([
    { id: 7, naziv: "Proleće 2026", datumOd: "2026-03-01", datumDo: "2026-05-31" },
  ]),
}));

function LocationProbe() {
  const location = useLocation();
  return <output data-testid="location-search">{location.search}</output>;
}

vi.mock("../SupplierSalesStatsPage", () => ({
  default: function MockSupplierSalesStatsPage(props: any) {
    useEffect(() => {
      props.onTrustMetadataChange?.({
        lastRefreshAt: "2026-07-01T07:55:00Z",
        dataFreshnessStatus: "fresh",
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
      expect(screen.getByText("Skup podataka")).toBeInTheDocument();
      expect(screen.getByText(/30d\s*(→|->)\s*90d/)).toBeInTheDocument();
      expect(screen.getByText("mv_supplier_decision_score_cache_90d")).toBeInTheDocument();
      expect(screen.getByText(/Pomoćni skup je aktivan\./)).toBeInTheDocument();
      expect(screen.queryByText(/no_data_30d/i)).not.toBeInTheDocument();
      expect(screen.getByText("Sveže")).toBeInTheDocument();
    });
  });

  it("explains an Operations legacy source while preserving the canonical tab", () => {
    render(
      <MemoryRouter initialEntries={["/analytics/supplier?tab=assortment&legacySource=operations-supplier-footwear&dataScope=imported"]}>
        <SupplierConsolidatedPage />
      </MemoryRouter>,
    );

    expect(screen.getByTestId("supplier-legacy-context")).toHaveTextContent(
      "Kompatibilna veza iz Operacija otvorila je glavni Pregled dobavljača, tab Asortiman",
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
    expect(screen.getAllByLabelText("Filteri dobavljača")).toHaveLength(1);
    expect(screen.getByRole("button", { name: new RegExp(tabLabel, "i") })).toHaveAttribute("aria-selected", "true");
    expect(screen.queryByText("Opseg i filteri")).not.toBeInTheDocument();
    expect(screen.queryByText("Kontrole asortimana")).not.toBeInTheDocument();
  });

  it("marks retained supplier options stale and blocks selection when filter fallback metadata is returned", async () => {
    vi.mocked(getSupplierFilters)
      .mockResolvedValueOnce([
        { supplierId: 101, supplierName: "Dobavljač A" },
      ] as Awaited<ReturnType<typeof getSupplierFilters>>)
      .mockResolvedValueOnce(Object.assign(
        [{ supplierId: 202, supplierName: "Dobavljač B" }],
        {
          meta: {
            success: true,
            warningMessage: "Filteri dobavljača trenutno koriste pomoćni signal.",
            dataQualityStatus: "warning",
            isPartial: true,
          },
        },
      ) as Awaited<ReturnType<typeof getSupplierFilters>>);

    render(
      <MemoryRouter initialEntries={["/analytics/supplier"]}>
        <SupplierConsolidatedPage />
      </MemoryRouter>,
    );

    const supplierSelect = await screen.findByRole("combobox", { name: /Dobavljač/i });
    await waitFor(() => {
      expect(screen.getByRole("option", { name: "Dobavljač A" })).toBeInTheDocument();
    });

    fireEvent.change(screen.getByDisplayValue("Svi podaci"), { target: { value: "imported" } });

    await waitFor(() => {
      expect(screen.getByText("Zastarela lista")).toBeInTheDocument();
      expect(supplierSelect).toBeDisabled();
      expect(supplierSelect).toHaveValue("");
      expect(screen.getByText(/Filteri dobavljača trenutno koriste pomoćni signal/i)).toBeInTheDocument();
      expect(screen.getByText(/Lista dobavljača je zastarela/i)).toBeInTheDocument();
      expect(screen.getByRole("option", { name: "Dobavljač A" })).toBeDisabled();
    });
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
      expect(screen.getByRole("combobox", { name: /Dobavljač/i })).toHaveValue("");
    });
  });

  it("does not duplicate filter owners when switching tabs", async () => {
    render(
      <MemoryRouter initialEntries={["/analytics/supplier?tab=overview"]}>
        <SupplierConsolidatedPage />
      </MemoryRouter>,
    );

    expect(await screen.findByTestId("mock-overview")).toBeInTheDocument();
    expect(screen.getAllByLabelText("Filteri dobavljača")).toHaveLength(1);

    fireEvent.click(screen.getByRole("button", { name: /Skorkarta/i }));
    expect(await screen.findByTestId("mock-scorecard")).toBeInTheDocument();
    expect(screen.getAllByLabelText("Filteri dobavljača")).toHaveLength(1);
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);

    fireEvent.click(screen.getByRole("button", { name: /Asortiman/i }));
    expect(await screen.findByTestId("mock-assortment")).toBeInTheDocument();
    expect(screen.getAllByLabelText("Filteri dobavljača")).toHaveLength(1);
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
  });

  it("round-trips scorecard-only filters through the canonical URL owner", async () => {
    render(
      <MemoryRouter initialEntries={["/analytics/supplier?tab=scorecard&category=Patike&gender=Mu%C5%A1ko&seasonId=7&minRevenue=5000&onlyHighConfidence=true&excludeOosBeforeMarkdown=true"]}>
        <LocationProbe />
        <SupplierConsolidatedPage />
      </MemoryRouter>,
    );

    expect(await screen.findByDisplayValue("Patike")).toBeInTheDocument();
    expect(screen.getByDisplayValue("Muško")).toBeInTheDocument();
    expect(screen.getByDisplayValue("Proleće 2026")).toBeInTheDocument();
    expect(screen.getByDisplayValue("5000")).toBeInTheDocument();
    expect(screen.getByLabelText("Samo visoka pouzdanost skorkarte")).toBeChecked();
    expect(screen.getByLabelText("Isključi artikle bez zaliha pre sniženja iz skorkarte")).toBeChecked();

    fireEvent.change(screen.getByDisplayValue("Patike"), { target: { value: "Čizme" } });

    await waitFor(() => {
      expect(screen.getByTestId("location-search")).toHaveTextContent("category=%C4%8Cizme");
      expect(screen.getByTestId("location-search")).toHaveTextContent("gender=Mu%C5%A1ko");
      expect(screen.getByTestId("location-search")).toHaveTextContent("seasonId=7");
      expect(screen.getByTestId("location-search")).toHaveTextContent("onlyHighConfidence=true");
      expect(screen.getByTestId("location-search")).toHaveTextContent("excludeOosBeforeMarkdown=true");
    });
  });

  it("removes invalid scorecard filters from the canonical URL instead of sending them to the backend", async () => {
    render(
      <MemoryRouter initialEntries={["/analytics/supplier?tab=scorecard&gender=nepoznato&seasonId=-1&minRevenue=-20&onlyHighConfidence=maybe&excludeOosBeforeMarkdown=false"]}>
        <LocationProbe />
        <SupplierConsolidatedPage />
      </MemoryRouter>,
    );

    await waitFor(() => {
      const search = screen.getByTestId("location-search").textContent ?? "";
      expect(search).not.toContain("gender=");
      expect(search).not.toContain("seasonId=");
      expect(search).not.toContain("minRevenue=");
      expect(search).not.toContain("onlyHighConfidence=");
      expect(search).not.toContain("excludeOosBeforeMarkdown=");
    });
  });

  it("blocks retained supplier options when the filter request itself fails", async () => {
    vi.mocked(getSupplierFilters)
      .mockReset()
      .mockResolvedValueOnce([
        { supplierId: 101, supplierName: "Dobavljač A" },
      ] as Awaited<ReturnType<typeof getSupplierFilters>>)
      .mockRejectedValueOnce(new Error("supplier filters unavailable"));

    render(
      <MemoryRouter initialEntries={["/analytics/supplier"]}>
        <SupplierConsolidatedPage />
      </MemoryRouter>,
    );

    const supplierSelect = await screen.findByRole("combobox", { name: /Dobavljač/i });
    await waitFor(() => {
      expect(screen.getByRole("option", { name: "Dobavljač A" })).toBeInTheDocument();
    });

    fireEvent.change(screen.getByDisplayValue("Svi podaci"), { target: { value: "imported" } });

    await waitFor(() => {
      expect(supplierSelect).toBeDisabled();
      expect(screen.getByText(/Lista dobavljača nije osvežena/i)).toBeInTheDocument();
      expect(screen.getByText(/Lista dobavljača je zastarela/i)).toBeInTheDocument();
      expect(screen.getByRole("option", { name: "Dobavljač A" })).toBeDisabled();
    });
  });
});

import React from "react";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import SupplierFootwearAnalyticsPage from "../SupplierFootwearAnalyticsPage";
import { getVendorSalesNivelacija } from "../../services/vendorSalesNivelacijaApi";
import { buildSupplierSalesDisplayProjection } from "../SupplierSalesStatsPage";

vi.mock("../../services/vendorSalesNivelacijaApi", async () => {
  const actual = await vi.importActual<typeof import("../../services/vendorSalesNivelacijaApi")>(
    "../../services/vendorSalesNivelacijaApi",
  );
  return {
    ...actual,
    getVendorSalesNivelacija: vi.fn(actual.getVendorSalesNivelacija),
    getVendorSalesNivelacijaOptions: vi.fn().mockResolvedValue([]),
  };
});

vi.mock("../../services/dobavljaciApi", () => ({
  getDobavljaci: vi.fn().mockResolvedValue([]),
}));

vi.mock("recharts", () => ({
  Bar: () => <div />,
  BarChart: ({ children }: { children?: React.ReactNode }) => <div>{children}</div>,
  CartesianGrid: () => <div />,
  ResponsiveContainer: ({ children }: { children?: React.ReactNode }) => <div>{children}</div>,
  Tooltip: () => <div />,
  XAxis: () => <div />,
  YAxis: () => <div />,
}));

describe("supplier frontend residuals (RQ523)", () => {
  it("keeps declared-population share when only one supplier row is projected", () => {
    const projection = buildSupplierSalesDisplayProjection(
      [{ ukupanPromet: 10_000, isUnknown: false, sharePct: 40 } as never],
      { positiveNetRevenueDenominator: 15_000 },
    );

    expect(projection.rows[0]?.sharePct).toBeCloseTo(66.6667, 3);
  });

  it("loads assortment category from the URL and shows applied chips after apply", async () => {
    render(
      <MemoryRouter initialEntries={["/analytics/supplier-footwear?category=Patike"]}>
        <SupplierFootwearAnalyticsPage />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(getVendorSalesNivelacija).toHaveBeenCalled();
    });

    const categoryField = await screen.findByLabelText("Kategorija");
    expect(categoryField).toHaveValue("Patike");

    fireEvent.click(screen.getByRole("button", { name: "Primeni filtere" }));

    const controlBar = await screen.findByTestId("analytics-control-bar");
    expect(within(controlBar).getByText(/Patike/)).toBeInTheDocument();
    expect(within(controlBar).queryByText(/^good$/i)).not.toBeInTheDocument();
  });

  it("syncs embedded assortment category from shared canonical filters", async () => {
    render(
      <MemoryRouter>
        <SupplierFootwearAnalyticsPage
          embedded
          sharedFilters={{
            periodPreset: "30d",
            fromDate: "2026-07-13",
            toDate: "2026-08-11",
            dataScope: "all",
            storeId: null,
            supplierId: null,
            category: "Cipele",
          }}
          onTrustMetadataChange={vi.fn()}
        />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(getVendorSalesNivelacija).toHaveBeenCalledWith(
        expect.objectContaining({ category: "Cipele" }),
      );
    });
  });
});

import { render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import SupplierSalesStatsPage from "../SupplierSalesStatsPage";
import { getStores } from "../../services/analyticsApi";
import { getSupplierSalesStats } from "../../services/supplierSalesStatsApi";

vi.mock("../../components/analytics/AnalyticsTrustHeader", () => ({
  default: ({ title }: { title: string }) => <div data-testid="analytics-trust-header">{title}</div>,
}));

vi.mock("../../services/analyticsApi", async () => {
  const actual = await vi.importActual<typeof import("../../services/analyticsApi")>("../../services/analyticsApi");
  return {
    ...actual,
    getStores: vi.fn(),
  };
});

vi.mock("../../services/supplierSalesStatsApi", () => ({
  getSupplierSalesStats: vi.fn(),
}));

describe("SupplierSalesStatsPage premium controls", () => {
  beforeEach(() => {
    vi.mocked(getStores).mockResolvedValue([]);
    vi.mocked(getSupplierSalesStats).mockResolvedValue({
      fromDate: "2026-06-01",
      toDate: "2026-06-30",
      generatedAt: "2026-07-01T08:00:00Z",
      sezone: [],
      suppliers: [
        {
          dobavljacId: 1,
          dobavljacNaziv: "Alfa",
          isUnknown: false,
          preNivelacijePromet: 0,
          preNivelacijeKolicina: 0,
          posleNivelacijePromet: 10000,
          posleNivelacijeKolicina: 5,
          ukupanPromet: 10000,
          ukupnaKolicina: 5,
          previousPeriodRevenue: 8000,
          previousPeriodUnits: 4,
          brojArtikalaSaNivelacijom: 0,
          brojArtikalaUkupno: 2,
          revenueWithCost: 10000,
          estimatedCostRevenue: 0,
          marginContribution: 4000,
          marginDataCoveragePct: 100,
          fallbackCostCoveragePct: 0,
          marginPct: 40,
          totalCost: 6000,
          popRevenueChangePct: 25,
          popUnitsChangePct: 25,
          prePostNivelacijaRevenueImpactPct: null,
          prePostNivelacijaUnitsImpactPct: null,
          prePostNivelacijaRevenueCoveragePct: null,
          recommendation: {
            status: "maintain",
            label: "Maintain",
            summary: "Stabilan partner.",
            confidencePct: 80,
            reliabilityPct: 75,
            dataQualityStatus: "good",
            reasonCodes: ["stable_margin"],
          },
          footwearBreakdown: [],
        },
      ],
      totals: {
        ukupanPromet: 10000,
        ukupnaKolicina: 5,
        marginContribution: 4000,
        marginPct: 40,
        missingCostRevenueSharePct: 0,
        unknownSupplierRevenueSharePct: 0,
        marginQualityTier: "good",
        isSnapshotActive: false,
        snapshotCostCoveragePct: null,
      },
      dataQuality: {
        missingCostRevenueSharePct: 0,
        unknownSupplierRevenueSharePct: 0,
      },
      meta: { success: true, dataQualityStatus: "good" },
    } as never);
  });

  it("uses shared control bar and analytics data table without changing recommendation labels", async () => {
    render(
      <MemoryRouter initialEntries={["/analytics/supplier-sales-stats"]}>
        <SupplierSalesStatsPage />
      </MemoryRouter>,
    );

    expect(screen.getByTestId("analytics-trust-header")).toHaveTextContent("Dobavljači: Pregled");
    const controlBar = await screen.findByTestId("analytics-control-bar");
    expect(within(controlBar).getByRole("heading", { name: "Opseg i filteri" })).toBeInTheDocument();
    expect(within(controlBar).getByLabelText("Period")).toBeInTheDocument();
    expect(within(controlBar).getByLabelText("Objekat")).toBeInTheDocument();
    expect(within(controlBar).getByRole("link", { name: "Kvalitet podataka" })).toHaveAttribute(
      "href",
      "/analytics/data-quality",
    );

    await waitFor(() => {
      expect(screen.getByTestId("supplier-sales-stats-data-table")).toBeInTheDocument();
    });
    expect(screen.getByText("Alfa")).toBeInTheDocument();
    expect(screen.getByText("Prioritetna lista dobavljača")).toBeInTheDocument();
  });
});

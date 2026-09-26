import { act, render, screen, waitFor } from "@testing-library/react";
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

function buildResponse(scope: string) {
  return {
    fromDate: "2026-06-01",
    toDate: "2026-06-30",
    generatedAt: "2026-07-01T08:00:00Z",
    dataScope: scope,
    meta: {
      success: true,
      lastRefreshAtUtc: "2026-07-01T07:55:00Z",
      dataQualityStatus: "good",
      isPartial: false,
    },
    provenanceBasis: "live_query",
    sezone: [],
    suppliers: [
      {
        dobavljacId: 1,
        dobavljacNaziv: scope === "imported" ? "Uvezeni Alfa" : "Postojeci Alfa",
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
          recommendationAllowed: true,
          reasonCodes: ["stable_margin"],
        },
        footwearBreakdown: [],
      },
    ],
    totals: {
      ukupanPromet: 10000,
      ukupnaKolicina: 5,
      ukupanMarzniDoprinos: 4000,
      marginContribution: 4000,
      marginPct: 40,
      missingCostRevenueSharePct: 0,
      unknownSupplierRevenueSharePct: 0,
      marginQualityTier: "confirmed",
      isSnapshotActive: false,
      snapshotCostCoveragePct: null,
      brojDobavljaca: 1,
    },
    dataQuality: {
      missingCostRevenueSharePct: 0,
      unknownSupplierRevenueSharePct: 0,
    },
  } as never;
}

describe("SupplierSalesStatsPage scope reload", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    vi.mocked(getStores).mockResolvedValue([]);
    vi.mocked(getSupplierSalesStats).mockImplementation(async (params) => buildResponse(params?.dataScope ?? "all"));
  });

  it("reloads standalone supplier sales when the global data scope changes", async () => {
    localStorage.setItem("trendplus:dataScope", "imported");

    render(
      <MemoryRouter initialEntries={["/analytics/supplier-sales-stats"]}>
        <SupplierSalesStatsPage />
      </MemoryRouter>,
    );

    expect(await screen.findByText("Uvezeni Alfa")).toBeInTheDocument();
    expect(getSupplierSalesStats).toHaveBeenCalledWith(expect.objectContaining({ dataScope: "imported" }));

    localStorage.setItem("trendplus:dataScope", "existing");
    await act(async () => {
      window.dispatchEvent(new Event("trendplus:data-scope-changed"));
    });

    expect(await screen.findByText("Postojeci Alfa")).toBeInTheDocument();
    await waitFor(() => {
      expect(getSupplierSalesStats).toHaveBeenLastCalledWith(expect.objectContaining({ dataScope: "existing" }));
    });
  });

  it("ignores a late response from the previous scope after a scope switch", async () => {
    localStorage.setItem("trendplus:dataScope", "imported");
    let resolveImported: ((value: unknown) => void) | undefined;
    vi.mocked(getSupplierSalesStats)
      .mockImplementationOnce(async () => new Promise((resolve) => {
        resolveImported = resolve;
      }))
      .mockImplementationOnce(async (params) => buildResponse(params?.dataScope ?? "all"));

    render(
      <MemoryRouter initialEntries={["/analytics/supplier-sales-stats"]}>
        <SupplierSalesStatsPage />
      </MemoryRouter>,
    );

    localStorage.setItem("trendplus:dataScope", "existing");
    await act(async () => {
      window.dispatchEvent(new Event("trendplus:data-scope-changed"));
    });

    await waitFor(() => {
      expect(getSupplierSalesStats).toHaveBeenLastCalledWith(expect.objectContaining({ dataScope: "existing" }));
    });

    await act(async () => {
      resolveImported?.(buildResponse("imported"));
    });

    expect(await screen.findByText("Postojeci Alfa")).toBeInTheDocument();
    expect(screen.queryByText("Uvezeni Alfa")).not.toBeInTheDocument();
  });

  it("uses shared parent scope in embedded mode instead of the global event", async () => {
    const onTrustMetadataChange = vi.fn();

    const { rerender } = render(
      <MemoryRouter initialEntries={["/analytics/supplier-sales-stats"]}>
        <SupplierSalesStatsPage
          embedded
          sharedFilters={{
            periodPreset: "30d",
            fromDate: "2026-06-01",
            toDate: "2026-06-30",
            dataScope: "imported",
            storeId: null,
            supplierId: null,
          }}
          onTrustMetadataChange={onTrustMetadataChange}
        />
      </MemoryRouter>,
    );

    expect(await screen.findByText("Uvezeni Alfa")).toBeInTheDocument();
    expect(getSupplierSalesStats).toHaveBeenCalledWith(expect.objectContaining({ dataScope: "imported" }));

    localStorage.setItem("trendplus:dataScope", "existing");
    await act(async () => {
      window.dispatchEvent(new Event("trendplus:data-scope-changed"));
    });

    expect(getSupplierSalesStats).toHaveBeenCalledTimes(1);

    rerender(
      <MemoryRouter initialEntries={["/analytics/supplier-sales-stats"]}>
        <SupplierSalesStatsPage
          embedded
          sharedFilters={{
            periodPreset: "30d",
            fromDate: "2026-06-01",
            toDate: "2026-06-30",
            dataScope: "existing",
            storeId: null,
            supplierId: null,
          }}
          onTrustMetadataChange={onTrustMetadataChange}
        />
      </MemoryRouter>,
    );

    expect(await screen.findByText("Postojeci Alfa")).toBeInTheDocument();
    expect(getSupplierSalesStats).toHaveBeenLastCalledWith(expect.objectContaining({ dataScope: "existing" }));
  });
});

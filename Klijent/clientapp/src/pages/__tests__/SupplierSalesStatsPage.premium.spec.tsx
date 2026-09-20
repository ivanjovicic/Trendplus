import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import SupplierSalesStatsPage, {
  buildSupplierConcentrationData,
  calculateTopSupplierRevenueShare,
  describePopMetric,
  describePopUnitsMetric,
} from "../SupplierSalesStatsPage";
import { getStores } from "../../services/analyticsApi";
import { getSupplierSalesStats } from "../../services/supplierSalesStatsApi";

const AnalyticsTrustHeaderMock = vi.hoisted(() =>
  vi.fn((props: { title: string }) => <div data-testid="analytics-trust-header">{props.title}</div>)
);

vi.mock("../../components/analytics/AnalyticsTrustHeader", () => ({
  default: AnalyticsTrustHeaderMock,
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
  it("uses the same visible known-supplier population for concentration KPI and chart", () => {
    const rows = [
      { dobavljacNaziv: "Alfa", ukupanPromet: 100 },
      { dobavljacNaziv: "Beta", ukupanPromet: 50 },
      { dobavljacNaziv: "Gamma", ukupanPromet: 25 },
      { dobavljacNaziv: "Delta", ukupanPromet: 10 },
      { dobavljacNaziv: "Epsilon", ukupanPromet: 5 },
      { dobavljacNaziv: "Zeta", ukupanPromet: 10 },
    ];

    expect(calculateTopSupplierRevenueShare(rows)).toBe(97.5);
    expect(calculateTopSupplierRevenueShare([rows[0]])).toBe(100);
    expect(buildSupplierConcentrationData(rows)).toEqual([
      { name: "Alfa", sharePct: 50 },
      { name: "Beta", sharePct: 25 },
      { name: "Gamma", sharePct: 12.5 },
      { name: "Delta", sharePct: 5 },
      { name: "Zeta", sharePct: 5 },
      { name: "Epsilon", sharePct: 2.5 },
    ]);
  });

  it.each([
    [[]],
    [[{ dobavljacNaziv: "Zero", ukupanPromet: 0 }]],
    [[{ dobavljacNaziv: "Unknown", ukupanPromet: Number.NaN }]],
  ])("keeps unavailable concentration evidence distinct for %j", (rows) => {
    expect(calculateTopSupplierRevenueShare(rows)).toBeNull();
    expect(buildSupplierConcentrationData(rows)).toEqual([]);
  });

  it("keeps non-finite PoP revenue and units unavailable while preserving zero", () => {
    expect(describePopMetric({ popRevenueChangePct: Number.POSITIVE_INFINITY, previousPeriodRevenue: 100, ukupanPromet: 200 }).label).toBe("N/A");
    expect(describePopMetric({ popRevenueChangePct: 0, previousPeriodRevenue: 100, ukupanPromet: 200 }).label).toBe("0,00%");
    expect(describePopUnitsMetric({ popUnitsChangePct: Number.NEGATIVE_INFINITY, previousPeriodUnits: 100, ukupnaKolicina: 200 }).label).toBe("N/A");
    expect(describePopUnitsMetric({ popUnitsChangePct: 0, previousPeriodUnits: 100, ukupnaKolicina: 200 }).label).toBe("0,00%");
  });

  beforeEach(() => {
    vi.mocked(getStores).mockResolvedValue([]);
    vi.mocked(getSupplierSalesStats).mockResolvedValue({
      fromDate: "2026-06-01",
      toDate: "2026-06-30",
      generatedAt: "2026-07-01T08:00:00Z",
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

  it("forwards supplier trust lineage and effective period into the shared trust header", async () => {
    vi.mocked(getSupplierSalesStats).mockResolvedValue({
      fromDate: "2026-06-01",
      toDate: "2026-06-30",
      generatedAt: "2026-07-01T08:00:00Z",
      meta: {
        success: true,
        lastRefreshAtUtc: "2026-07-01T07:50:00Z",
        dataQualityStatus: "insufficient_data",
        emptyReason: "no_supplier_sales",
        message: "Nema podataka za prodaju po dobavljaču.",
        isPartial: false,
      },
      dataWindowFrom: "2024-01-01T00:00:00Z",
      dataWindowTo: "2026-06-30T23:59:59Z",
      provenanceBasis: "live_query",
      sezone: [],
      suppliers: [],
      totals: {
        ukupanPromet: 0,
        ukupnaKolicina: 0,
        marginContribution: 0,
        marginPct: 0,
        missingCostRevenueSharePct: 0,
        unknownSupplierRevenueSharePct: 0,
        marginQualityTier: "insufficient_data",
        isSnapshotActive: false,
        snapshotCostCoveragePct: null,
      },
      dataQuality: {
        missingCostRevenueSharePct: 0,
        unknownSupplierRevenueSharePct: 0,
      },
    } as never);

    render(
      <MemoryRouter initialEntries={["/analytics/supplier-sales-stats"]}>
        <SupplierSalesStatsPage />
      </MemoryRouter>,
    );

    await screen.findByRole("heading", { name: /Nema (podataka|dovoljno podataka)/i });

    const trustHeaderProps = AnalyticsTrustHeaderMock.mock.calls.at(-1)?.[0] as {
      title?: string;
      dataSource?: string | null;
      provenanceBasis?: string | null;
      requestedDataset?: string | null;
      effectiveDataset?: string | null;
      effectivePeriodLabel?: string | null;
      lastRefreshAt?: string | null;
      dataQualityStatus?: string | null;
      emptyStateReason?: string | null;
    };

    expect(trustHeaderProps).toEqual(expect.objectContaining({
      title: "Dobavljači: Pregled",
      dataSource: "Supplier sales stats (scope: Svi podaci)",
      provenanceBasis: "live_query",
      requestedDataset: "Svi podaci",
      effectiveDataset: "Svi podaci",
      dataFreshnessStatus: "unknown",
    }));
    expect(trustHeaderProps.effectivePeriodLabel).toContain("2024");
    expect(trustHeaderProps.effectivePeriodLabel).toContain("2026");
  });

  it("projects fresh for a non-empty response with a valid refresh timestamp", async () => {
    render(
      <MemoryRouter initialEntries={["/analytics/supplier-sales-stats"]}>
        <SupplierSalesStatsPage />
      </MemoryRouter>,
    );

    await waitFor(() => {
      const trustHeaderProps = AnalyticsTrustHeaderMock.mock.calls.at(-1)?.[0] as {
        dataFreshnessStatus?: string | null;
      };
      expect(trustHeaderProps.dataFreshnessStatus).toBe("fresh");
    });
  });

  it("forwards freshness provenance to the embedded trust metadata owner", async () => {
    const onTrustMetadataChange = vi.fn();

    render(
      <MemoryRouter initialEntries={["/analytics/supplier-sales-stats"]}>
        <SupplierSalesStatsPage embedded onTrustMetadataChange={onTrustMetadataChange} />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(onTrustMetadataChange).toHaveBeenCalledWith(expect.objectContaining({
        dataFreshnessStatus: "fresh",
        lastRefreshAt: "2026-07-01T07:55:00Z",
      }));
    });
  });

  it("keeps embedded and standalone trust freshness aligned for partial payloads", async () => {
    vi.mocked(getSupplierSalesStats).mockResolvedValue({
      ...(await getSupplierSalesStats({} as never)),
      meta: {
        success: true,
        lastRefreshAtUtc: "2026-07-01T07:55:00Z",
        dataQualityStatus: "warning",
        isPartial: true,
        warningCode: "partial_payload",
      },
    } as never);

    render(
      <MemoryRouter initialEntries={["/analytics/supplier-sales-stats"]}>
        <SupplierSalesStatsPage />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(AnalyticsTrustHeaderMock).toHaveBeenCalled();
    });

    const standaloneTrustHeaderProps = AnalyticsTrustHeaderMock.mock.calls.at(-1)?.[0] as {
      dataFreshnessStatus?: string | null;
      lastRefreshAt?: string | null;
      isPartial?: boolean;
    };

    const onTrustMetadataChange = vi.fn();
    render(
      <MemoryRouter initialEntries={["/analytics/supplier-sales-stats"]}>
        <SupplierSalesStatsPage embedded onTrustMetadataChange={onTrustMetadataChange} />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(onTrustMetadataChange).toHaveBeenCalledWith(expect.objectContaining({
        dataFreshnessStatus: standaloneTrustHeaderProps.dataFreshnessStatus,
        lastRefreshAt: standaloneTrustHeaderProps.lastRefreshAt,
      }));
    });

    expect(standaloneTrustHeaderProps.dataFreshnessStatus).toBe("stale");
    expect(standaloneTrustHeaderProps.isPartial).toBe(true);
  });

  it("keeps embedded trust metadata unknown when refresh timestamp is missing", async () => {
    vi.mocked(getSupplierSalesStats).mockResolvedValue({
      ...(await getSupplierSalesStats({} as never)),
      generatedAt: "2026-07-01T08:00:00Z",
      meta: {
        success: true,
        lastRefreshAtUtc: null,
        dataQualityStatus: "good",
        isPartial: false,
      },
    } as never);

    const onTrustMetadataChange = vi.fn();
    render(
      <MemoryRouter initialEntries={["/analytics/supplier-sales-stats"]}>
        <SupplierSalesStatsPage embedded onTrustMetadataChange={onTrustMetadataChange} />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(onTrustMetadataChange).toHaveBeenCalledWith(expect.objectContaining({
        dataFreshnessStatus: "unknown",
        lastRefreshAt: null,
      }));
    });
  });

  it("matches standalone and embedded freshness projection for the same payload", async () => {
    const onTrustMetadataChange = vi.fn();

    const { unmount } = render(
      <MemoryRouter initialEntries={["/analytics/supplier-sales-stats"]}>
        <SupplierSalesStatsPage />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(AnalyticsTrustHeaderMock).toHaveBeenCalled();
    });

    const standaloneTrustHeaderProps = AnalyticsTrustHeaderMock.mock.calls.at(-1)?.[0] as {
      dataFreshnessStatus?: string | null;
      lastRefreshAt?: string | null;
    };

    unmount();
    onTrustMetadataChange.mockClear();

    render(
      <MemoryRouter initialEntries={["/analytics/supplier-sales-stats"]}>
        <SupplierSalesStatsPage embedded onTrustMetadataChange={onTrustMetadataChange} />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(onTrustMetadataChange).toHaveBeenCalledWith(expect.objectContaining({
        dataFreshnessStatus: standaloneTrustHeaderProps.dataFreshnessStatus,
        lastRefreshAt: standaloneTrustHeaderProps.lastRefreshAt,
      }));
    });
  });

  it("hides standalone trust header and filter surface when embedded", async () => {
    render(
      <MemoryRouter initialEntries={["/analytics/supplier-sales-stats"]}>
        <SupplierSalesStatsPage embedded onTrustMetadataChange={vi.fn()} />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(screen.queryByTestId("analytics-trust-header")).not.toBeInTheDocument();
    });
    expect(screen.queryByText("Opseg i filteri")).not.toBeInTheDocument();
    expect(screen.queryAllByRole("heading", { level: 1 })).toHaveLength(0);
    expect(screen.getByRole("region", { name: "Pregled dobavljača" })).toBeInTheDocument();
  });

  it("error hides KPI zeros when supplier sales fails", async () => {
    vi.mocked(getSupplierSalesStats).mockRejectedValue(new Error("backend down"));

    render(
      <MemoryRouter initialEntries={["/analytics/supplier-sales-stats"]}>
        <SupplierSalesStatsPage />
      </MemoryRouter>,
    );

    expect(await screen.findByRole("alert")).toHaveTextContent(/Podaci trenutno nisu dostupni/i);
    expect(screen.queryByText("Ukupan promet")).not.toBeInTheDocument();
    expect(screen.queryByText("Prioritetna lista dobavljača")).not.toBeInTheDocument();
  });

  it("clears embedded trust metadata on supplier sales error instead of promoting generated time", async () => {
    vi.mocked(getSupplierSalesStats).mockRejectedValue(new Error("backend down"));
    const onTrustMetadataChange = vi.fn();

    render(
      <MemoryRouter initialEntries={["/analytics/supplier-sales-stats"]}>
        <SupplierSalesStatsPage embedded onTrustMetadataChange={onTrustMetadataChange} />
      </MemoryRouter>,
    );

    expect(await screen.findByRole("alert")).toHaveTextContent(/Podaci trenutno nisu dostupni/i);
    await waitFor(() => {
      expect(onTrustMetadataChange).toHaveBeenCalledWith(null);
    });
  });

  it("keeps embedded empty payloads unknown without dropping a valid refresh timestamp", async () => {
    vi.mocked(getSupplierSalesStats).mockResolvedValue({
      fromDate: "2026-06-01",
      toDate: "2026-06-30",
      generatedAt: "2026-07-01T08:00:00Z",
      meta: {
        success: true,
        lastRefreshAtUtc: "2026-07-01T07:55:00Z",
        dataQualityStatus: "insufficient_data",
        emptyReason: "no_supplier_sales",
        isPartial: false,
      },
      sezone: [],
      suppliers: [],
      totals: {
        ukupanPromet: 0,
        ukupnaKolicina: 0,
        marginContribution: 0,
        marginPct: 0,
        missingCostRevenueSharePct: 0,
        unknownSupplierRevenueSharePct: 0,
        marginQualityTier: "insufficient_data",
        isSnapshotActive: false,
        snapshotCostCoveragePct: null,
      },
      dataQuality: {
        missingCostRevenueSharePct: 0,
        unknownSupplierRevenueSharePct: 0,
      },
    } as never);

    const onTrustMetadataChange = vi.fn();
    render(
      <MemoryRouter initialEntries={["/analytics/supplier-sales-stats"]}>
        <SupplierSalesStatsPage embedded onTrustMetadataChange={onTrustMetadataChange} />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(onTrustMetadataChange).toHaveBeenCalledWith(expect.objectContaining({
        dataFreshnessStatus: "unknown",
        lastRefreshAt: "2026-07-01T07:55:00Z",
      }));
    });
  });

  it("shows unavailable comparable article count in detail when backend omits the field", async () => {
    vi.mocked(getSupplierSalesStats).mockResolvedValueOnce({
      fromDate: "2026-06-01",
      toDate: "2026-06-30",
      generatedAt: "2026-07-01T08:00:00Z",
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
          prePostComparableArticleCount: null,
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
    } as never);

    render(
      <MemoryRouter initialEntries={["/analytics/supplier-sales-stats"]}>
        <SupplierSalesStatsPage />
      </MemoryRouter>,
    );

    await screen.findByText("Alfa");
    fireEvent.click(screen.getByRole("button", { name: "Detalji" }));

    const comparableArticles = await screen.findByText("Uporedivi artikli");
    const articleCard = comparableArticles.closest("article");
    expect(articleCard).not.toBeNull();
    expect(within(articleCard!).getByText("Nije dostupno")).toBeInTheDocument();
    expect(within(articleCard!).queryByText("0")).not.toBeInTheDocument();
  });

  it("empty is not error when supplier sales returns no rows", async () => {
    vi.mocked(getSupplierSalesStats).mockResolvedValue({
      fromDate: "2026-06-01",
      toDate: "2026-06-30",
      generatedAt: "2026-07-01T08:00:00Z",
      meta: {
        success: true,
        lastRefreshAtUtc: "2026-07-01T08:00:00Z",
        dataQualityStatus: "insufficient_data",
        emptyReason: "no_supplier_sales",
      },
      dataWindowFrom: "2024-01-01T00:00:00Z",
      dataWindowTo: "2026-06-30T23:59:59Z",
      provenanceBasis: "live_query",
      sezone: [],
      suppliers: [],
      totals: {
        ukupanPromet: 0,
        ukupnaKolicina: 0,
        marginContribution: 0,
        marginPct: 0,
        missingCostRevenueSharePct: 0,
        unknownSupplierRevenueSharePct: 0,
        marginQualityTier: "insufficient_data",
        isSnapshotActive: false,
        snapshotCostCoveragePct: null,
      },
      dataQuality: {
        missingCostRevenueSharePct: 0,
        unknownSupplierRevenueSharePct: 0,
      },
    } as never);

    render(
      <MemoryRouter initialEntries={["/analytics/supplier-sales-stats"]}>
        <SupplierSalesStatsPage />
      </MemoryRouter>,
    );

    expect(await screen.findByRole("heading", { name: /Nema (podataka|dovoljno podataka)/i })).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(screen.queryByText("Ukupan promet")).not.toBeInTheDocument();
    expect(screen.queryByText("Ukupan maržni doprinos")).not.toBeInTheDocument();
  });
});

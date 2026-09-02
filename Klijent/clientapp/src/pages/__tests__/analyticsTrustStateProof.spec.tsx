import { render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import DailySalesStatsPage from "../DailySalesStatsPage";
import ShoeTypeSalesStatsPage from "../ShoeTypeSalesStatsPage";
import SupplierSalesStatsPage from "../SupplierSalesStatsPage";
import AnalyticsActionsPage from "../AnalyticsActionsPage";
import { getStores } from "../../services/analyticsApi";
import { getDailySalesStats } from "../../services/dailySalesStatsApi";
import { getShoeTypeSalesStats } from "../../services/shoeTypeSalesStatsApi";
import { getSupplierSalesStats } from "../../services/supplierSalesStatsApi";

vi.mock("recharts", () => ({
  Bar: () => null,
  BarChart: ({ children }: { children?: ReactNode }) => <div>{children}</div>,
  CartesianGrid: () => null,
  ComposedChart: ({ children }: { children?: ReactNode }) => <div>{children}</div>,
  Legend: () => null,
  Line: () => null,
  LineChart: ({ children }: { children?: ReactNode }) => <div>{children}</div>,
  ResponsiveContainer: ({ children }: { children?: ReactNode }) => <div>{children}</div>,
  Tooltip: () => null,
  XAxis: () => null,
  YAxis: () => null,
}));

vi.mock("../../components/ui/InfoTip", () => ({
  default: ({ text }: { text: string }) => <span>{text}</span>,
}));

vi.mock("../../services/analyticsApi", async () => {
  const actual = await vi.importActual<typeof import("../../services/analyticsApi")>("../../services/analyticsApi");
  return {
    ...actual,
    getStores: vi.fn(),
    getAnalyticsActions: vi.fn(),
    getAnalyticsActionCounts: vi.fn(),
    getAnalyticsActionOutcomeSummary: vi.fn(),
    getAnalyticsActionById: vi.fn(),
    updateAnalyticsActionOutcome: vi.fn(),
    updateAnalyticsActionStatus: vi.fn(),
  };
});

vi.mock("../../services/dailySalesStatsApi", () => ({
  getDailySalesStats: vi.fn(),
}));

vi.mock("../../services/shoeTypeSalesStatsApi", async () => {
  const actual = await vi.importActual<typeof import("../../services/shoeTypeSalesStatsApi")>("../../services/shoeTypeSalesStatsApi");
  return {
    ...actual,
    getShoeTypeSalesStats: vi.fn(),
  };
});

vi.mock("../../services/supplierSalesStatsApi", () => ({
  getSupplierSalesStats: vi.fn(),
}));

describe("analytics trust-state header proof", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.setItem("trendplus:dataScope", "all");
    vi.mocked(getStores).mockResolvedValue([]);
  });

  it("Daily Sales mounts the real AnalyticsTrustHeader", async () => {
    vi.mocked(getDailySalesStats).mockRejectedValue(new Error("backend down"));

    render(
      <MemoryRouter initialEntries={["/analytics/daily-sales"]}>
        <Routes>
          <Route path="/analytics/daily-sales" element={<DailySalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(screen.getByRole("region", { name: "Kontekst pouzdanosti analitike" })).toBeInTheDocument();
    expect(screen.getByText("Analitički signal")).toBeInTheDocument();
    expect(await screen.findByRole("alert")).toHaveTextContent(/Dnevna prodaja trenutno nije dostupna/i);
  });

  it("Shoe Type mounts the real AnalyticsTrustHeader", async () => {
    vi.mocked(getShoeTypeSalesStats).mockRejectedValue(new Error("backend down"));

    render(
      <MemoryRouter initialEntries={["/analitika/shoe-type-sales-stats"]}>
        <Routes>
          <Route path="/analitika/shoe-type-sales-stats" element={<ShoeTypeSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(screen.getByRole("region", { name: "Kontekst pouzdanosti analitike" })).toBeInTheDocument();
    expect(screen.getByText("Analitički signal")).toBeInTheDocument();
    expect(await screen.findByRole("alert")).toBeInTheDocument();
  });

  it("Supplier sales mounts the real AnalyticsTrustHeader", async () => {
    vi.mocked(getSupplierSalesStats).mockRejectedValue(new Error("backend down"));

    render(
      <MemoryRouter initialEntries={["/analytics/supplier-sales-stats"]}>
        <SupplierSalesStatsPage />
      </MemoryRouter>,
    );

    expect(screen.getByRole("region", { name: "Kontekst pouzdanosti analitike" })).toBeInTheDocument();
    expect(screen.getByText("Preporuka sistema")).toBeInTheDocument();
    expect(await screen.findByRole("alert")).toBeInTheDocument();
  });

  it("Supplier sales exposes provenance basis when data loads successfully", async () => {
    vi.mocked(getSupplierSalesStats).mockResolvedValue({
      generatedAt: "2026-07-01T08:00:00Z",
      fromDate: "2026-06-01",
      toDate: "2026-06-30",
      dataWindowFrom: "2026-06-01T00:00:00Z",
      dataWindowTo: "2026-06-30T23:59:59Z",
      sezonaId: null,
      storeId: null,
      dataScope: "all",
      provenanceBasis: "live_query",
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
      sezone: [],
    } as never);

    render(
      <MemoryRouter initialEntries={["/analytics/supplier-sales-stats"]}>
        <SupplierSalesStatsPage />
      </MemoryRouter>,
    );

    expect(screen.getByRole("region", { name: "Kontekst pouzdanosti analitike" })).toBeInTheDocument();
    expect(screen.getByText("Supplier sales stats (scope: Svi podaci)")).toBeInTheDocument();
    expect(screen.getByText("Svi podaci -> Svi podaci")).toBeInTheDocument();
    expect(await screen.findByText("Osnova generisanja")).toBeInTheDocument();
    expect(screen.getByText("live_query")).toBeInTheDocument();
  });

  it("Analytics Actions mounts the real AnalyticsTrustHeader", async () => {
    const analyticsApi = await import("../../services/analyticsApi");
    vi.mocked(analyticsApi.getAnalyticsActions).mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 50,
      totalPages: 0,
    } as never);
    vi.mocked(analyticsApi.getAnalyticsActionCounts).mockResolvedValue({
      new: 0,
      accepted: 0,
      deferred: 0,
      rejected: 0,
      done: 0,
      p1Open: 0,
    } as never);
    vi.mocked(analyticsApi.getAnalyticsActionOutcomeSummary).mockResolvedValue({
      meta: {
        success: true,
        periodMode: "created",
        createdFrom: null,
        createdTo: null,
        resolvedFrom: null,
        resolvedTo: null,
        measuredFrom: null,
        measuredTo: null,
        generatedAtUtc: "2026-08-13T00:00:00Z",
        sampleSize: 0,
        measuredSampleSize: 0,
        warnings: [],
        emptyReason: "no_measured_closed_outcomes",
      },
      totals: {
        createdCount: 0,
        closedCount: 0,
        openCount: 0,
        measuredCount: 0,
        measuredOutcomeCount: 0,
        pendingOutcomeCount: 0,
        successCount: 0,
        neutralCount: 0,
        negativeCount: 0,
        notMeasuredCount: 0,
        outcomeCoverageRate: null,
        positiveOutcomeRate: null,
        negativeOutcomeRate: null,
        closedOutcomeCoverageRate: null,
        measuredPositiveOutcomeRate: null,
        measuredNegativeOutcomeRate: null,
      },
      impact: {
        expectedImpactRsd: null,
        measuredImpactRsd: null,
        realizationRatio: null,
        measuredImpactSampleCount: 0,
      },
      bySourceType: [],
      byPriority: [],
      byOutcomeStatus: [],
      byDataQuality: [],
      byConfidenceBucket: [],
      byReliabilityBucket: [],
      measurementStatistics: {
        success: true,
        issuedCount: 0,
        acceptedCount: 0,
        rejectedCount: 0,
        ignoredCount: 0,
        executedCount: 0,
        measuredCount: 0,
        notMeasuredCount: 0,
        successCount: 0,
        neutralCount: 0,
        negativeCount: 0,
        pendingCount: 0,
        acceptanceRate: null,
        rejectionRate: null,
        ignoredRate: null,
        executionRate: null,
        measurementCoverageRate: null,
        notMeasuredShare: null,
        positiveOutcomeRate: null,
        neutralOutcomeRate: null,
        negativeOutcomeRate: null,
        warningCodes: [],
        emptyReason: "no_rows",
      },
    } as never);

    render(
      <MemoryRouter initialEntries={["/analytics/actions"]}>
        <AnalyticsActionsPage />
      </MemoryRouter>,
    );

    expect(screen.getByRole("region", { name: "Kontekst pouzdanosti analitike" })).toBeInTheDocument();
    expect(screen.getByText("Izveštaj")).toBeInTheDocument();
    expect(await screen.findByText("Analiza ishoda još nije spremna")).toBeInTheDocument();
    expect(screen.getByText(/nema zatvorenih akcija sa izmerenim ishodom/i)).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(screen.queryByText("Izmereni uticaj")).not.toBeInTheDocument();
  });
});

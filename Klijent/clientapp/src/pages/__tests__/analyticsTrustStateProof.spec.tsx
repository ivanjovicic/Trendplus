import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import DailySalesStatsPage from "../DailySalesStatsPage";
import ShoeTypeSalesStatsPage from "../ShoeTypeSalesStatsPage";
import SupplierSalesStatsPage from "../SupplierSalesStatsPage";
import AnalyticsActionsPage from "../AnalyticsActionsPage";
import ColorSalesStatsPage from "../ColorSalesStatsPage";
import ProdajaPrePostNivelacijePage from "../ProdajaPrePostNivelacijePage";
import PreNivelacijaPriorityPage from "../PreNivelacijaPriorityPage";
import InventoryPage from "../InventoryPage";
import { getStores } from "../../services/analyticsApi";
import { getDailySalesStats } from "../../services/dailySalesStatsApi";
import { getShoeTypeSalesStats } from "../../services/shoeTypeSalesStatsApi";
import { getSupplierSalesStats } from "../../services/supplierSalesStatsApi";
import { ApiHttpError } from "../../services/analyticsHttp";

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
    getSupplierFilters: vi.fn(),
    getInventoryReportSchedules: vi.fn(),
    getInventoryBalance: vi.fn(),
    getInventoryList: vi.fn(),
    getInventoryInsights: vi.fn(),
    getInventoryStoreComparison: vi.fn(),
    getInventoryActionSuggestions: vi.fn(),
    getForecast: vi.fn(),
    getInventoryAlerts: vi.fn(),
    getRebalanceSuggestions: vi.fn(),
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

vi.mock("../../services/colorSalesStatsApi", () => ({ getColorSalesStats: vi.fn() }));
vi.mock("../../services/vendorSalesNivelacijaApi", () => ({ getVendorSalesNivelacija: vi.fn() }));
vi.mock("../../services/dobavljaciApi", () => ({ getDobavljaci: vi.fn().mockResolvedValue([]) }));
vi.mock("../../services/preNivelacijaApi", () => ({
  getPreNivelacijaPrioriteti: vi.fn(),
  PreNivelacijaApiError: class extends Error {},
}));

function expandTrustDetails() {
  const toggle = screen.queryByTestId("analytics-trust-details-toggle");
  if (toggle && toggle.getAttribute("aria-expanded") !== "true") {
    fireEvent.click(toggle);
  }
}

describe("analytics trust-state header proof", () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    localStorage.setItem("trendplus:dataScope", "all");
    vi.mocked(getStores).mockResolvedValue([]);
    const analyticsApi = await import("../../services/analyticsApi");
    vi.mocked(analyticsApi.getSupplierFilters).mockResolvedValue([]);
    vi.mocked(analyticsApi.getInventoryReportSchedules).mockResolvedValue([]);
    vi.mocked(analyticsApi.getInventoryInsights).mockResolvedValue({
      totalItems: 0,
      totalEstimatedValue: 0,
      aging: [],
      abc: [],
      topAgedItems: [],
      topCapitalLockedItems: [],
      meta: { success: true },
    } as never);
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
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(await screen.findByRole("alert")).toHaveTextContent(/Dnevna prodaja trenutno nije dostupna/i);
    expandTrustDetails();
    expect(screen.getByTestId("analytics-trust-readiness-state")).toHaveTextContent("Nije dostupno");
    expect(screen.getByTestId("analytics-trust-integrity-state")).toHaveTextContent("Nije provereno za izabrani kontekst");
  });

  it("Shoe Type mounts the real AnalyticsTrustHeader", async () => {
    vi.mocked(getShoeTypeSalesStats).mockRejectedValue(new Error("backend down"));

    render(
      <MemoryRouter initialEntries={["/analytics/shoe-type-sales-stats"]}>
        <Routes>
          <Route path="/analytics/shoe-type-sales-stats" element={<ShoeTypeSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(screen.getByRole("region", { name: "Kontekst pouzdanosti analitike" })).toBeInTheDocument();
    expect(screen.getByText("Analitički signal")).toBeInTheDocument();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(await screen.findByRole("alert")).toBeInTheDocument();
    expandTrustDetails();
    expect(screen.getByTestId("analytics-trust-readiness-state")).toHaveTextContent("Nije dostupno");
    expect(screen.getByTestId("analytics-trust-integrity-state")).toHaveTextContent("Nije provereno za izabrani kontekst");
  });

  it.each([
    { title: "Color", route: "/analytics/color-sales-stats", Page: ColorSalesStatsPage, service: "color" },
    { title: "Pre/Post Nivelacija", route: "/analytics/vendor-sales-nivelacija", Page: ProdajaPrePostNivelacijePage, service: "prepost" },
    { title: "Pre-Nivelacija Priorities", route: "/analytics/pre-nivelacija-priorities", Page: PreNivelacijaPriorityPage, service: "priority" },
    { title: "Inventory", route: "/analytics/inventory", Page: InventoryPage, service: "inventory" },
  ])("$title mounts the shared trust header and exposes fail-closed state", async ({ route, Page, service }) => {
    const analyticsApi = await import("../../services/analyticsApi");
    if (service === "color") {
      const api = await import("../../services/colorSalesStatsApi");
      vi.mocked(api.getColorSalesStats).mockRejectedValue(new Error("backend down"));
    } else if (service === "prepost") {
      const api = await import("../../services/vendorSalesNivelacijaApi");
      vi.mocked(api.getVendorSalesNivelacija).mockRejectedValue(new Error("backend down"));
    } else if (service === "priority") {
      const api = await import("../../services/preNivelacijaApi");
      vi.mocked(api.getPreNivelacijaPrioriteti).mockRejectedValue(new Error("backend down"));
    } else {
      vi.mocked(analyticsApi.getInventoryBalance).mockResolvedValue({
        totalSku: 1, totalOnHand: 10, outOfStockCount: 0, lowStockCount: 0, estimatedInventoryValue: 1000,
        meta: { success: true },
      } as never);
      vi.mocked(analyticsApi.getInventoryList).mockResolvedValue({
        items: [{ id: 501, naziv: "Artikal A", plu: "PLU-501", kolicina: 10, minimalnaKolicina: 3, nabavnaCena: 100, estimatedValue: 1000, idObjekat: 1, idDobavljac: null }],
        totalCount: 1,
        pageNumber: 1,
        pageSize: 50,
        meta: {
          success: true,
          operationsIntegrityStatus: "verified",
          operationsIntegrityContextMatches: true,
          operationsIntegrityEvidenceId: "inventory-evidence-1",
          operationsIntegrityContextFingerprint: "inventory-context-1",
        },
      } as never);
      vi.mocked(analyticsApi.getInventoryInsights).mockResolvedValue({
        meta: {
          success: true,
          decisionReadiness: {
            state: "decision_ready",
            surfaceRole: "recommendation",
            recommendationAllowed: true,
            reasonCodes: [],
            evidenceReferences: ["inventory.snapshot"],
          },
        },
      } as never);
      vi.mocked(analyticsApi.getInventoryStoreComparison).mockRejectedValue(new Error("backend down"));
      vi.mocked(analyticsApi.getInventoryActionSuggestions).mockRejectedValue(new Error("backend down"));
      vi.mocked(analyticsApi.getForecast).mockRejectedValue(new Error("backend down"));
      vi.mocked(analyticsApi.getInventoryAlerts).mockRejectedValue(new Error("backend down"));
      vi.mocked(analyticsApi.getRebalanceSuggestions).mockRejectedValue(new Error("backend down"));
      vi.mocked(analyticsApi.getSupplierFilters).mockResolvedValue([]);
      vi.mocked(analyticsApi.getInventoryReportSchedules).mockResolvedValue([]);
    }

    render(
      <MemoryRouter initialEntries={[route]}>
        <Routes>
          <Route path={route} element={<Page />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByRole("region", { name: "Kontekst pouzdanosti analitike" })).toBeInTheDocument();
    expandTrustDetails();
    if (service === "inventory") {
      await waitFor(() => {
        expect(screen.getByTestId("analytics-trust-readiness-state")).toHaveTextContent("Spremno za odluku");
      });
      expect(screen.getByTestId("analytics-trust-integrity-state")).toHaveTextContent("Provereno za ovaj kontekst");
      expect(screen.getByRole("link", { name: "Pregledaj dokaz" })).toHaveAttribute(
        "href",
        "/api/analytics/operations-integrity/evidence/inventory-evidence-1",
      );
    } else {
      expect(screen.getByTestId("analytics-trust-readiness-state")).toHaveTextContent(/Nije dostupno|Provera u toku/);
      expect(screen.getByTestId("analytics-trust-integrity-state")).toHaveTextContent(/Nije provereno|Provera u toku|nije dostupno/i);
      expect(screen.queryByRole("link", { name: "Pregledaj dokaz" })).not.toBeInTheDocument();
    }
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
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(await screen.findByRole("alert")).toBeInTheDocument();
  });

  it("Supplier sales preserves backend error identity and retry state", async () => {
    vi.mocked(getSupplierSalesStats).mockRejectedValue(
      new ApiHttpError(
        503,
        "Analitička šema za pregled dobavljača nije kompatibilna sa aktivnim ugovorom.",
        "SUPPLIER_OVERVIEW_SCHEMA_INVALID",
        "supplier-correlation-1",
      ),
    );

    render(
      <MemoryRouter initialEntries={["/analytics/supplier-sales-stats"]}>
        <Routes>
          <Route path="/analytics/supplier-sales-stats" element={<SupplierSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Analitička šema za pregled dobavljača");
    expect(alert).toHaveTextContent("supplier-correlation-1");
    expect(screen.getByRole("button", { name: "Pokušaj ponovo" })).toBeInTheDocument();
    expect(screen.queryByText("Nema dovoljno podataka za izabrani period")).not.toBeInTheDocument();
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
    expect(screen.getByRole("heading", { name: "Dobavljači: Pregled" })).toBeInTheDocument();
    await waitFor(() => {
      expandTrustDetails();
      expect(screen.getByText("Osnova generisanja")).toBeInTheDocument();
    });
    expect(screen.getByText("Prodaja po dobavljačima (opseg: Svi podaci)")).toBeInTheDocument();
    expect(screen.getByText("Svi podaci -> Svi podaci")).toBeInTheDocument();
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

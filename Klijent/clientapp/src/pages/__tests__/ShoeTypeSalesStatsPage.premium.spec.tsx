import { render, screen, waitFor, within } from "@testing-library/react";
import type { ReactNode } from "react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import ShoeTypeSalesStatsPage from "../ShoeTypeSalesStatsPage";
import { getStores } from "../../services/analyticsApi";
import { getShoeTypeSalesStats } from "../../services/shoeTypeSalesStatsApi";
import type { ShoeTypeSalesStat, ShoeTypeSalesStatsResponse } from "../../services/shoeTypeSalesStatsApi";

vi.mock("recharts", () => ({
  Bar: () => null,
  BarChart: ({ children }: { children?: ReactNode }) => <div data-testid="bar-chart">{children}</div>,
  CartesianGrid: () => null,
  Legend: () => null,
  ResponsiveContainer: ({ children }: { children?: ReactNode }) => <div data-testid="responsive-container">{children}</div>,
  Tooltip: () => null,
  XAxis: () => null,
  YAxis: () => null,
}));

vi.mock("../../components/analytics/AnalyticsTrustHeader", () => ({
  default: ({ title }: { title: string }) => <div data-testid="analytics-trust-header">{title}</div>,
}));

vi.mock("../../components/ui/InfoTip", () => ({
  default: ({ text }: { text: string }) => <span data-testid="info-tip">{text}</span>,
}));

vi.mock("../../services/analyticsApi", async () => {
  const actual = await vi.importActual<typeof import("../../services/analyticsApi")>("../../services/analyticsApi");
  return {
    ...actual,
    getStores: vi.fn(),
  };
});

vi.mock("../../services/shoeTypeSalesStatsApi", async () => {
  const actual = await vi.importActual<typeof import("../../services/shoeTypeSalesStatsApi")>("../../services/shoeTypeSalesStatsApi");
  return {
    ...actual,
    getShoeTypeSalesStats: vi.fn(),
  };
});

function shoeType(overrides: Partial<ShoeTypeSalesStat> = {}): ShoeTypeSalesStat {
  return {
    tipObuceId: 1,
    tipObuceNaziv: "Patike",
    preNivelacijePromet: 90000,
    preNivelacijeKolicina: 9,
    posleNivelacijePromet: 30000,
    posleNivelacijeKolicina: 3,
    ukupanPromet: 120000,
    ukupnaKolicina: 12,
    previousPeriodRevenue: 80000,
    previousPeriodUnits: 8,
    brojArtikalaSaNivelacijom: 5,
    brojArtikalaUkupno: 8,
    revenueWithCost: 100000,
    estimatedCostRevenue: 20000,
    marginContribution: 46000,
    marginDataCoveragePct: 83.3,
    fallbackCostCoveragePct: 16.7,
    marginPct: 38.3,
    totalCost: 74000,
    revenueWithNivelacijaSplit: 100000,
    popRevenueChangePct: 50,
    popUnitsChangePct: 20,
    prePostNivelacijaRevenueImpactPct: -12.5,
    prePostNivelacijaUnitsImpactPct: -10,
    prePostNivelacijaRevenueCoveragePct: 75,
    sharePct: 100,
    reliabilityPct: 82,
    recommendation: {
      status: "increase_focus",
      label: "Increase focus",
      summary: "Jak rast.",
      confidencePct: 88,
      reliabilityPct: 82,
      dataQualityStatus: "good",
      reasonCodes: [],
    },
    ...overrides,
  };
}

function response(overrides: Partial<ShoeTypeSalesStatsResponse> = {}): ShoeTypeSalesStatsResponse {
  return {
    generatedAt: "2026-07-01T08:30:00Z",
    fromDate: "2026-06-01T00:00:00Z",
    toDate: "2026-06-30T23:59:59Z",
    dataWindowFrom: "2024-01-01T00:00:00Z",
    dataWindowTo: "2026-06-30T23:59:59Z",
    sezonaId: null,
    storeId: null,
    dataScope: "all",
    shoeTypes: [shoeType()],
    totals: {
      ukupanPromet: 120000,
      ukupanMarzniDoprinos: 46000,
      prePromet: 90000,
      poslePromet: 30000,
      brojTipovaObuce: 1,
      snapshotCostCoveragePct: 0,
      isSnapshotActive: false,
    },
    dataQuality: {
      missingCostRevenue: 0,
      missingCostRevenueSharePct: 10,
      unknownTypeRevenue: 0,
      unknownTypeRevenueSharePct: 0,
      revenueWithNivelacijaSplit: 100000,
      revenueWithNivelacijaSplitSharePct: 75,
    },
    sezone: [],
    ...overrides,
  };
}

describe("ShoeTypeSalesStatsPage premium controls", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.setItem("trendplus:dataScope", "all");
    vi.mocked(getStores).mockResolvedValue([]);
    vi.mocked(getShoeTypeSalesStats).mockResolvedValue(response());
  });

  it("uses shared control bar and analytics data table without changing recommendation labels", async () => {
    render(
      <MemoryRouter initialEntries={["/analitika/shoe-type-sales-stats"]}>
        <Routes>
          <Route path="/analitika/shoe-type-sales-stats" element={<ShoeTypeSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(screen.getByTestId("analytics-trust-header")).toHaveTextContent("Prodaja po tipu obuće");
    const controlBar = await screen.findByTestId("analytics-control-bar");
    expect(within(controlBar).getByRole("heading", { name: "Opseg i filteri" })).toBeInTheDocument();
    expect(within(controlBar).getByLabelText("Period")).toBeInTheDocument();
    expect(within(controlBar).getByLabelText("Objekat")).toBeInTheDocument();
    expect(within(controlBar).getByRole("link", { name: "Kvalitet podataka" })).toHaveAttribute(
      "href",
      "/analytics/data-quality",
    );

    await waitFor(() => {
      expect(screen.getByTestId("shoe-type-sales-stats-data-table")).toBeInTheDocument();
    });
    expect(screen.getByText("Patike")).toBeInTheDocument();
    expect(screen.getByText("Prioritetna lista tipova obuće")).toBeInTheDocument();
  });

  it("error hides KPI zeros when shoe type sales fails", async () => {
    vi.mocked(getShoeTypeSalesStats).mockRejectedValue(new Error("backend down"));

    render(
      <MemoryRouter initialEntries={["/analitika/shoe-type-sales-stats"]}>
        <Routes>
          <Route path="/analitika/shoe-type-sales-stats" element={<ShoeTypeSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByRole("alert")).toHaveTextContent(/Podaci trenutno nisu dostupni/i);
    expect(screen.queryByText("Ukupan promet")).not.toBeInTheDocument();
    expect(screen.queryByText("Prioritetna lista tipova obuće")).not.toBeInTheDocument();
  });

  it("empty is not error when shoe type sales returns no rows", async () => {
    vi.mocked(getShoeTypeSalesStats).mockResolvedValue(response({
      shoeTypes: [],
      totals: {
        ukupanPromet: 0,
        ukupanMarzniDoprinos: 0,
        prePromet: 0,
        poslePromet: 0,
        brojTipovaObuce: 0,
        snapshotCostCoveragePct: 0,
        isSnapshotActive: false,
      },
      dataQuality: {
        missingCostRevenue: 0,
        missingCostRevenueSharePct: 0,
        unknownTypeRevenue: 0,
        unknownTypeRevenueSharePct: 0,
        revenueWithNivelacijaSplit: 0,
        revenueWithNivelacijaSplitSharePct: 0,
      },
    }));

    render(
      <MemoryRouter initialEntries={["/analitika/shoe-type-sales-stats"]}>
        <Routes>
          <Route path="/analitika/shoe-type-sales-stats" element={<ShoeTypeSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByRole("heading", { name: /Nema (podataka|dovoljno podataka)/i })).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(screen.queryByText("Ukupan promet")).not.toBeInTheDocument();
    expect(screen.queryByText("Ukupan maržni doprinos")).not.toBeInTheDocument();
  });
});

import { render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import ShoeTypeSalesStatsPage from "../ShoeTypeSalesStatsPage";
import { getStores } from "../../services/analyticsApi";
import { getShoeTypeSalesStats } from "../../services/shoeTypeSalesStatsApi";
import type { ShoeTypeSalesStat, ShoeTypeSalesStatsResponse } from "../../services/shoeTypeSalesStatsApi";

vi.mock("recharts", () => ({
  Bar: () => null,
  BarChart: ({ children }: { children?: ReactNode }) => <div>{children}</div>,
  CartesianGrid: () => null,
  Legend: () => null,
  ResponsiveContainer: ({ children }: { children?: ReactNode }) => <div>{children}</div>,
  Tooltip: () => null,
  XAxis: () => null,
  YAxis: () => null,
}));

vi.mock("../../components/analytics/AnalyticsTrustHeader", () => ({
  default: ({ periodFrom, periodTo }: { periodFrom?: string | null; periodTo?: string | null }) => (
    <div data-testid="trust-period">{`${periodFrom ?? "-"}|${periodTo ?? "-"}`}</div>
  ),
}));

vi.mock("../../components/analytics/AnalyticsTableToolbar", () => ({
  default: () => <div data-testid="analytics-toolbar" />,
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

function renderPage() {
  return render(
    <MemoryRouter initialEntries={["/analitika/shoe-type-sales-stats"]}>
      <Routes>
        <Route path="/analitika/shoe-type-sales-stats" element={<ShoeTypeSalesStatsPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

function setupMocks(payload: ShoeTypeSalesStatsResponse) {
  vi.clearAllMocks();
  localStorage.setItem("trendplus:dataScope", "all");
  vi.mocked(getStores).mockResolvedValue([
    { storeId: 1, storeName: "Centar", city: "Beograd", region: "BG" },
  ]);
  vi.mocked(getShoeTypeSalesStats).mockResolvedValue(payload);
}

// RQ445: the trust header formats period bounds with local-time `formatDate`, so a raw
// `...T23:59:59Z` end rendered as the next calendar day in Europe/Belgrade. The page must
// pass the calendar dates of the backend effective range instead.
describe("ShoeTypeSalesStatsPage trust header period (RQ445)", () => {
  beforeEach(() => {
    setupMocks(response());
  });

  it("passes calendar dates of the effective range instead of raw UTC timestamps", async () => {
    renderPage();

    const period = await screen.findByTestId("trust-period");
    await screen.findByText("Prioritetna lista tipova obuće");
    expect(period).toHaveTextContent("2026-06-01|2026-06-30");
    expect(period.textContent).not.toContain("T23:59:59");
  });

  it("keeps the season end date for a tick-precision end-of-day bound", async () => {
    setupMocks(response({
      fromDate: "2026-03-01T00:00:00Z",
      toDate: "2026-05-31T23:59:59.9999999Z",
      sezonaId: 7,
    }));
    renderPage();

    await screen.findByText("Prioritetna lista tipova obuće");
    expect(screen.getByTestId("trust-period")).toHaveTextContent("2026-03-01|2026-05-31");
  });
});

import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import type { ReactNode } from "react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import ColorSalesStatsPage from "../ColorSalesStatsPage";
import { getStores } from "../../services/analyticsApi";
import { getColorSalesStats } from "../../services/colorSalesStatsApi";
import type { ColorSalesStat, ColorSalesStatsResponse } from "../../services/colorSalesStatsApi";

vi.mock("recharts", () => ({
  Bar: () => null,
  BarChart: ({ children }: { children?: ReactNode }) => <div data-testid="bar-chart">{children}</div>,
  CartesianGrid: () => null,
  ResponsiveContainer: ({ children }: { children?: ReactNode }) => <div data-testid="responsive-container">{children}</div>,
  Tooltip: () => null,
  XAxis: () => null,
  YAxis: () => null,
}));

vi.mock("../../components/analytics/AnalyticsDataTable", () => ({
  default: ({ toolbar, children, testId }: { toolbar?: ReactNode; children?: ReactNode; testId?: string }) => (
    <section data-testid={testId ?? "analytics-data-table"}>
      {toolbar}
      {children}
    </section>
  ),
}));

vi.mock("../../components/analytics/AnalyticsTableToolbar", () => ({
  default: ({ tableKey, rows }: { tableKey: string; rows: unknown[] }) => (
    <div data-testid="analytics-toolbar">
      {tableKey}: {rows.length} rows
    </div>
  ),
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

vi.mock("../../services/colorSalesStatsApi", async () => {
  const actual = await vi.importActual<typeof import("../../services/colorSalesStatsApi")>("../../services/colorSalesStatsApi");
  return {
    ...actual,
    getColorSalesStats: vi.fn(),
  };
});

function color(overrides: Partial<ColorSalesStat> = {}): ColorSalesStat {
  return {
    boja: "Crna",
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
    historicalCostRevenue: 100000,
    historicalCostCoveragePct: 83.3,
    estimatedCostCoveragePct: 16.7,
    noCostRevenue: 0,
    noCostCoveragePct: 0,
    snapshotCostRevenue: 100000,
    snapshotCostCoveragePct: 83.3,
    isEstimatedMargin: false,
    marginQualityLabel: "Dobra pokrivenost",
    marginQualityTier: "good",
    marginQualityShortLabel: "Good",
    marginQualityTooltip: "Većina prometa ima poznatu nabavnu cenu.",
    revenueWithNivelacijaSplit: 100000,
    popRevenueChangePct: 50,
    popUnitsChangePct: 20,
    prePostNivelacijaRevenueImpactPct: -12.5,
    prePostNivelacijaUnitsImpactPct: -10,
    prePostNivelacijaRevenueCoveragePct: 75,
    prePostSignalNote: "Dovoljna pre/post pokrivenost.",
    prePostComparableArticleCount: 5,
    sharePct: 60,
    reliabilityPct: 82,
    isUnknown: false,
    recommendation: {
      status: "increase_focus",
      label: "Increase focus",
      summary: "Jak rast i zdrava marža.",
      confidencePct: 88,
      reliabilityPct: 82,
      dataQualityStatus: "good",
      reasonCodes: ["strong_pop_growth"],
    },
    ...overrides,
  };
}

function response(overrides: Partial<ColorSalesStatsResponse> = {}): ColorSalesStatsResponse {
  const colors = overrides.colors ?? [
    color({
      boja: "Crna",
      ukupanPromet: 120000,
      marginContribution: 46000,
      recommendation: {
        status: "increase_focus",
        label: "Increase focus",
        summary: "Jak rast i zdrava marža.",
        confidencePct: 88,
        reliabilityPct: 82,
        dataQualityStatus: "good",
        reasonCodes: ["strong_pop_growth"],
      },
    }),
    color({
      boja: "Bež",
      ukupanPromet: 45000,
      marginContribution: 12000,
      popRevenueChangePct: -20,
      recommendation: {
        status: "review",
        label: "Review",
        summary: "Pad i slabiji doprinos.",
        confidencePct: 61,
        reliabilityPct: 70,
        dataQualityStatus: "warning",
        reasonCodes: ["weak_pop"],
      },
    }),
  ];

  return {
    generatedAt: "2026-07-01T08:30:00Z",
    fromDate: "2026-06-01T00:00:00Z",
    toDate: "2026-06-30T23:59:59Z",
    dataWindowFrom: "2024-01-01T00:00:00Z",
    dataWindowTo: "2026-06-30T23:59:59Z",
    sezonaId: null,
    storeId: null,
    dataScope: "all",
    colors,
    totals: {
      ukupanPromet: colors.reduce((sum, item) => sum + item.ukupanPromet, 0),
      ukupanMarzniDoprinos: colors.reduce((sum, item) => sum + item.marginContribution, 0),
      ukupanTrosak: 107000,
      prosecnaMarza: 35,
      historicalCostCoveragePct: 78,
      estimatedCostCoveragePct: 15,
      noCostCoveragePct: 7,
      snapshotCostRevenue: 100000,
      snapshotCostCoveragePct: 78,
      isSnapshotActive: true,
      snapshotGeneratedAtUtc: "2026-07-01T08:00:00Z",
      isEstimatedMargin: false,
      marginQualityLabel: "Dobra pokrivenost",
      marginQualityTier: "good",
      marginQualityShortLabel: "Good",
      marginQualityTooltip: "Dovoljna pokrivenost nabavnom cenom.",
      prePromet: 120000,
      poslePromet: 45000,
      ukupnaKolicina: 17,
      preKolicina: 12,
      posleKolicina: 5,
      previousPeriodRevenue: 110000,
      previousPeriodUnits: 11,
      brojBoja: colors.length,
      popRevenueChangePct: 30,
      popUnitsChangePct: 18,
      prePostNivelacijaRevenueImpactPct: -8,
      prePostNivelacijaUnitsImpactPct: -6,
      recommendationSummary: {
        increaseFocus: colors.filter((item) => item.recommendation?.status === "increase_focus").length,
        maintain: colors.filter((item) => item.recommendation?.status === "maintain").length,
        review: colors.filter((item) => item.recommendation?.status === "review").length,
        doNotTrust: 0,
        insufficientData: 0,
      },
    },
    dataQuality: {
      missingCostRevenue: 15000,
      missingCostRevenueSharePct: 10,
      estimatedCostRevenue: 20000,
      estimatedCostRevenueSharePct: 12,
      unknownColorRevenue: 5000,
      unknownColorRevenueSharePct: 3,
      revenueWithNivelacijaSplit: 100000,
      revenueWithNivelacijaSplitSharePct: 45,
    },
    sezone: [
      { id: 3, naziv: "Leto 2026", datumOd: "2026-06-01T00:00:00Z", datumDo: "2026-08-31T23:59:59Z" },
    ],
    ...overrides,
  };
}

describe("ColorSalesStatsPage premium controls", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.setItem("trendplus:dataScope", "all");
    vi.mocked(getStores).mockResolvedValue([
      { storeId: 1, storeName: "Centar", city: "Beograd", region: "BG" },
      { storeId: 2, storeName: "Novi Beograd", city: "Beograd", region: "BG" },
    ]);
    vi.mocked(getColorSalesStats).mockResolvedValue(response());
  });

  it("uses shared trust header, control bar and analytics data table without changing ranking labels", async () => {
    render(
      <MemoryRouter initialEntries={["/analitika/color-sales-stats"]}>
        <Routes>
          <Route path="/analitika/color-sales-stats" element={<ColorSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(screen.getByRole("heading", { name: "Prodaja po boji artikla" })).toBeInTheDocument();
    const controlBar = await screen.findByTestId("analytics-control-bar");
    expect(within(controlBar).getByRole("heading", { name: "Opseg i filteri" })).toBeInTheDocument();
    expect(within(controlBar).getByLabelText("Period")).toBeInTheDocument();
    expect(within(controlBar).getByLabelText("Objekat")).toBeInTheDocument();
    expect(within(controlBar).getByRole("button", { name: "Primeni filtere" })).toBeInTheDocument();
    expect(within(controlBar).getByRole("link", { name: "Kvalitet podataka" })).toHaveAttribute(
      "href",
      "/analytics/data-quality",
    );

    await waitFor(() => {
      expect(screen.getByText("Crna")).toBeInTheDocument();
    });
    expect(screen.getByText("Prioritetna lista boja")).toBeInTheDocument();
  });

  it("shows fresh only when the response provides a valid refresh timestamp", async () => {
    vi.mocked(getColorSalesStats).mockResolvedValue(response({
      meta: {
        success: true,
        dataQualityStatus: "good",
        isPartial: false,
        lastRefreshAtUtc: "2026-07-01T08:30:00Z",
      },
    }));

    render(
      <MemoryRouter initialEntries={["/analitika/color-sales-stats"]}>
        <Routes>
          <Route path="/analitika/color-sales-stats" element={<ColorSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(screen.getByText("Sveže")).toBeInTheDocument();
    });
  });

  it("keeps valid raw pre/post detail metrics visible when impact percent is unavailable", async () => {
    vi.mocked(getColorSalesStats).mockResolvedValue(response({
      colors: [
        color({
          boja: "Crna",
          preNivelacijePromet: 90000,
          posleNivelacijePromet: 30000,
          preNivelacijeKolicina: 9,
          posleNivelacijeKolicina: 3,
          prePostNivelacijaRevenueImpactPct: null,
          prePostNivelacijaUnitsImpactPct: null,
          prePostNivelacijaRevenueCoveragePct: 75,
        }),
      ],
    }));

    render(
      <MemoryRouter initialEntries={["/analitika/color-sales-stats"]}>
        <Routes>
          <Route path="/analitika/color-sales-stats" element={<ColorSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    const table = await screen.findByTestId("analytics-data-table");
    const row = within(table).getAllByRole("row").find((candidate) => candidate.textContent?.includes("Crna"));
    expect(row).toBeDefined();
    fireEvent.click(within(row!).getByRole("button", { name: "Detalji" }));

    const detailHeading = await screen.findByRole("heading", { name: "Detalj odluke: Crna" });
    const detailPanel = detailHeading.closest("section");
    expect(detailPanel).not.toBeNull();
    expect(within(detailPanel!).getByText("Pre nivelacije promet").parentElement).toHaveTextContent(/90\.000/);
    expect(within(detailPanel!).getByText("Posle nivelacije promet").parentElement).toHaveTextContent(/30\.000/);
    expect(within(detailPanel!).getByText("Pre nivo kolicina").parentElement).toHaveTextContent(/9.*kom/);
    expect(within(detailPanel!).getByText("Posle nivo kolicina").parentElement).toHaveTextContent(/3.*kom/);
    expect(within(detailPanel!).getByText("Nivelacija impact prometa").parentElement).toHaveTextContent("N/A");
  });

  it("keeps raw pre/post detail metrics independently unavailable when individual evidence is missing", async () => {
    vi.mocked(getColorSalesStats).mockResolvedValue(response({
      colors: [
        color({
          boja: "Teget",
          preNivelacijePromet: 50000,
          posleNivelacijePromet: Number.NaN,
          preNivelacijeKolicina: 5,
          posleNivelacijeKolicina: Number.POSITIVE_INFINITY,
          prePostNivelacijaRevenueImpactPct: -8,
        }),
      ],
    }));

    render(
      <MemoryRouter initialEntries={["/analitika/color-sales-stats"]}>
        <Routes>
          <Route path="/analitika/color-sales-stats" element={<ColorSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    const table = await screen.findByTestId("analytics-data-table");
    const row = within(table).getAllByRole("row").find((candidate) => candidate.textContent?.includes("Teget"));
    expect(row).toBeDefined();
    fireEvent.click(within(row!).getByRole("button", { name: "Detalji" }));

    const detailHeading = await screen.findByRole("heading", { name: "Detalj odluke: Teget" });
    const detailPanel = detailHeading.closest("section");
    expect(detailPanel).not.toBeNull();
    expect(within(detailPanel!).getByText("Pre nivelacije promet").parentElement).toHaveTextContent(/50\.000/);
    expect(within(detailPanel!).getByText("Posle nivelacije promet").parentElement).toHaveTextContent("Nije dostupno");
    expect(within(detailPanel!).getByText("Pre nivo kolicina").parentElement).toHaveTextContent(/5.*kom/);
    expect(within(detailPanel!).getByText("Posle nivo kolicina").parentElement).toHaveTextContent("Nije dostupno");
    expect(within(detailPanel!).getByText("Nivelacija impact prometa").parentElement).toHaveTextContent(/-8,00%/);
  });
});

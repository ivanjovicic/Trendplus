import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import type { ReactNode } from "react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import ColorSalesStatsPage from "../ColorSalesStatsPage";
import { getStores } from "../../services/analyticsApi";
import { getColorSalesStats } from "../../services/colorSalesStatsApi";
import type { ColorSalesStat, ColorSalesStatsResponse } from "../../services/colorSalesStatsApi";
import { RECOMMENDATION_SIGNAL_UNAVAILABLE } from "../../utils/canonicalRecommendationSemantics";

vi.mock("recharts", () => ({
  Bar: () => null,
  BarChart: ({ data, children }: { data?: unknown[]; children?: ReactNode }) => (
    <div data-testid="bar-chart" data-chart-data={JSON.stringify(data ?? [])}>{children}</div>
  ),
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
  default: ({ tableKey, rows, metadata }: {
    tableKey: string;
    rows: unknown[];
    metadata?: Array<{ label: string; value: unknown }>;
  }) => (
    <div data-testid="analytics-toolbar">
      {tableKey}: {rows.length} rows
      {metadata?.map((item) => (
        <span key={item.label}>{item.label}: {String(item.value)}</span>
      ))}
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
    comparablePreRevenue: 90000,
    comparablePostRevenue: 30000,
    comparablePreQuantity: 9,
    comparablePostQuantity: 3,
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
      comparablePreRevenue: 120000,
      comparablePostRevenue: 45000,
      comparablePreQuantity: 12,
      comparablePostQuantity: 5,
      comparableArticleCount: 10,
      comparableRevenueCoveragePct: 75,
      prePostSignalNote: null,
      observedPreRevenue: 120000,
      observedPostRevenue: 45000,
      observedPreQuantity: 12,
      observedPostQuantity: 5,
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
    expect(within(detailPanel!).getByText("Uporedivo pre nivelacije promet").parentElement).toHaveTextContent(/90\.000/);
    expect(within(detailPanel!).getByText("Uporedivo posle nivelacije promet").parentElement).toHaveTextContent(/30\.000/);
    expect(within(detailPanel!).getByText("Uporedivo pre nivelacije količina").parentElement).toHaveTextContent(/9.*kom/);
    expect(within(detailPanel!).getByText("Uporedivo posle nivelacije količina").parentElement).toHaveTextContent(/3.*kom/);
    expect(within(detailPanel!).getByText("Uticaj nivelacije na promet").parentElement).toHaveTextContent("N/A");
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
          comparablePreRevenue: 50000,
          comparablePostRevenue: Number.NaN,
          comparablePreQuantity: 5,
          comparablePostQuantity: Number.POSITIVE_INFINITY,
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
    expect(within(detailPanel!).getByText("Uporedivo pre nivelacije promet").parentElement).toHaveTextContent(/50\.000/);
    expect(within(detailPanel!).getByText("Uporedivo posle nivelacije promet").parentElement).toHaveTextContent("Nije dostupno");
    expect(within(detailPanel!).getByText("Uporedivo pre nivelacije količina").parentElement).toHaveTextContent(/5.*kom/);
    expect(within(detailPanel!).getByText("Uporedivo posle nivelacije količina").parentElement).toHaveTextContent("Nije dostupno");
    expect(within(detailPanel!).getByText("Uticaj nivelacije na promet").parentElement).toHaveTextContent(/-8,00%/);
  });

  it("keeps measured zero pre/post evidence visible when impact percent is unavailable", async () => {
    vi.mocked(getColorSalesStats).mockResolvedValue(response({
      colors: [
        color({
          boja: "Bordo",
          preNivelacijePromet: 0,
          posleNivelacijePromet: 25000,
          preNivelacijeKolicina: 0,
          posleNivelacijeKolicina: 4,
          comparablePreRevenue: 0,
          comparablePostRevenue: 25000,
          comparablePreQuantity: 0,
          comparablePostQuantity: 4,
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
    const row = within(table).getAllByRole("row").find((candidate) => candidate.textContent?.includes("Bordo"));
    expect(row).toBeDefined();
    fireEvent.click(within(row!).getByRole("button", { name: "Detalji" }));

    const detailHeading = await screen.findByRole("heading", { name: "Detalj odluke: Bordo" });
    const detailPanel = detailHeading.closest("section");
    expect(detailPanel).not.toBeNull();
    expect(within(detailPanel!).getByText("Uporedivo pre nivelacije promet").parentElement).toHaveTextContent(/0.*RSD/);
    expect(within(detailPanel!).getByText("Uporedivo posle nivelacije promet").parentElement).toHaveTextContent(/25\.000/);
    expect(within(detailPanel!).getByText("Uticaj nivelacije na promet").parentElement).toHaveTextContent("Bez baze");
  });

  it("preserves gated color status identity across row, KPI and detail surfaces", async () => {
    const baseRecommendation = color().recommendation!;
    vi.mocked(getColorSalesStats).mockResolvedValue(response({
      colors: [
        color({
          boja: "Dozvoljena crna",
          recommendation: { ...baseRecommendation, recommendationAllowed: true },
        }),
        color({
          boja: "Crna za pregled",
          recommendation: {
            ...baseRecommendation,
            status: "review",
            summary: "Potrebna je rucna provera.",
            recommendationAllowed: false,
          },
        }),
        color({
          boja: "Nepouzdan signal",
          recommendation: {
            ...baseRecommendation,
            status: "do_not_trust",
            summary: "Signal zahteva proveru izvora.",
            recommendationAllowed: undefined,
          },
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
    const allowedRow = within(table).getAllByRole("row").find((candidate) => candidate.textContent?.includes("Dozvoljena crna"));
    const reviewRow = within(table).getAllByRole("row").find((candidate) => candidate.textContent?.includes("Crna za pregled"));
    const doNotTrustRow = within(table).getAllByRole("row").find((candidate) => candidate.textContent?.includes("Nepouzdan signal"));
    expect(allowedRow).toBeDefined();
    expect(reviewRow).toBeDefined();
    expect(doNotTrustRow).toBeDefined();
    if (!allowedRow || !reviewRow || !doNotTrustRow) throw new Error("Expected recommendation rows were not rendered");

    expect(within(allowedRow).getByLabelText(/Pojacaj:/)).toBeInTheDocument();
    expect(within(allowedRow).queryByText("Akcija blokirana")).not.toBeInTheDocument();
    expect(within(reviewRow).getByLabelText(/Pregledaj: Backend je blokirao izvrsenje preporuke/)).toBeInTheDocument();
    expect(within(reviewRow).getByText("Akcija blokirana")).toBeInTheDocument();
    expect(within(doNotTrustRow).getByLabelText(/Ne veruj: Backend nije potvrdio da je preporuka izvrsna/)).toBeInTheDocument();
    expect(within(doNotTrustRow).getByText("Akcija blokirana")).toBeInTheDocument();

    const tableHead = screen.getByText("Prioritetna lista boja").parentElement;
    expect(tableHead).not.toBeNull();
    expect(within(tableHead!).getByText(/Pojacaj: 1/)).toBeInTheDocument();
    expect(within(tableHead!).getByText(/Pregledaj: 1/)).toBeInTheDocument();
    expect(within(tableHead!).getByText(/Ne veruj: 1/)).toBeInTheDocument();
    expect(within(tableHead!).getByText(/Nedovoljno podataka: 0/)).toBeInTheDocument();

    fireEvent.click(within(reviewRow).getByRole("button", { name: "Detalji" }));
    expect(await screen.findByRole("heading", { name: "Detalj odluke: Crna za pregled" })).toBeInTheDocument();
    expect(screen.getByText("Razlog preporuke:").parentElement).toHaveTextContent("Backend je blokirao izvrsenje preporuke: Potrebna je rucna provera.");
    expect(screen.getAllByText(RECOMMENDATION_SIGNAL_UNAVAILABLE)).toHaveLength(1);
  });

  it("fails closed for unknown or missing recommendations without exposing raw status codes", async () => {
    const baseRecommendation = color().recommendation!;
    vi.mocked(getColorSalesStats).mockResolvedValue(response({
      colors: [
        color({
          boja: "Nepoznat status",
          recommendation: {
            ...baseRecommendation,
            status: "backend_future_status" as typeof baseRecommendation.status,
            summary: "Ovaj tekst ne sme postati status.",
            recommendationAllowed: true,
          },
        }),
        color({
          boja: "Bez preporuke",
          recommendation: undefined,
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
    const unknownRow = within(table).getAllByRole("row").find((candidate) => candidate.textContent?.includes("Nepoznat status"));
    const missingRow = within(table).getAllByRole("row").find((candidate) => candidate.textContent?.includes("Bez preporuke"));
    expect(unknownRow).toBeDefined();
    expect(missingRow).toBeDefined();
    if (!unknownRow || !missingRow) throw new Error("Expected unavailable recommendation rows were not rendered");

    expect(within(unknownRow).getByLabelText(/Nedovoljno podataka: Status preporuke nije prepoznat/)).toBeInTheDocument();
    expect(within(unknownRow).getByText("Akcija blokirana")).toBeInTheDocument();
    expect(within(missingRow).getByLabelText(/Nedovoljno podataka: Backend preporuka nije dostupna/)).toBeInTheDocument();
    expect(screen.queryByText("backend_future_status")).not.toBeInTheDocument();
    const tableHead = screen.getByText("Prioritetna lista boja").parentElement;
    expect(tableHead).not.toBeNull();
    expect(within(tableHead!).getByText(/Nedovoljno podataka: 2/)).toBeInTheDocument();
  });

  it("keeps missing color count unavailable instead of fabricating zero in toolbar metadata", async () => {
    vi.mocked(getColorSalesStats).mockResolvedValue(response({
      totals: {
        ...response().totals,
        brojBoja: undefined as unknown as number,
      },
    }));

    render(
      <MemoryRouter initialEntries={["/analitika/color-sales-stats"]}>
        <Routes>
          <Route path="/analitika/color-sales-stats" element={<ColorSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    const toolbar = await screen.findByTestId("analytics-toolbar");
    expect(within(toolbar).getByText("Broj boja: N/A")).toBeInTheDocument();
    expect(within(toolbar).queryByText("Broj boja: 0")).not.toBeInTheDocument();
  });

  it("keeps concentration chart from inventing invalid Ostale share percentages", async () => {
    vi.mocked(getColorSalesStats).mockResolvedValue(response({
      colors: [
        ...Array.from({ length: 6 }, (_, index) => color({
          boja: `Boja ${index + 1}`,
          ukupanPromet: 12000 - index,
          sharePct: 60,
        })),
        color({
          boja: "Boja 7",
          ukupanPromet: 5000,
          sharePct: 55,
        }),
        color({
          boja: "Boja 8",
          ukupanPromet: 4000,
          sharePct: 55,
        }),
      ],
      totals: {
        ...response().totals,
        ukupanPromet: 80000,
        brojBoja: 8,
      },
    }));

    render(
      <MemoryRouter initialEntries={["/analitika/color-sales-stats"]}>
        <Routes>
          <Route path="/analitika/color-sales-stats" element={<ColorSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    const chartData = JSON.parse(
      (await screen.findByTestId("bar-chart")).getAttribute("data-chart-data") ?? "[]",
    ) as Array<{ name: string; sharePct: number }>;
    expect(chartData.some((entry) => entry.name === "Ostale")).toBe(false);
    expect(chartData).toHaveLength(6);
  });

  it("keeps valid zero and 100 percentages visible across surfaces", async () => {
    vi.mocked(getColorSalesStats).mockResolvedValue(response({
      colors: [color({
        boja: "Crvena",
        ukupanPromet: 120000,
        brojArtikalaSaNivelacijom: 0,
        brojArtikalaUkupno: 8,
        sharePct: 100,
        prePostNivelacijaRevenueCoveragePct: 0,
        marginDataCoveragePct: 0,
      })],
      totals: {
        ...response().totals,
        ukupanPromet: 120000,
        brojBoja: 1,
      },
    }));

    render(
      <MemoryRouter initialEntries={["/analitika/color-sales-stats"]}>
        <Routes>
          <Route path="/analitika/color-sales-stats" element={<ColorSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    const table = await screen.findByTestId("analytics-data-table");
    const row = within(table).getAllByRole("row").find((candidate) => candidate.textContent?.includes("Crvena"));
    expect(row).toBeDefined();
    expect(row).toHaveTextContent("100,00%");

    fireEvent.click(within(row!).getByRole("button", { name: "Detalji" }));
    const detailHeading = await screen.findByRole("heading", { name: "Detalj odluke: Crvena" });
    const detailPanel = detailHeading.closest("section");
    expect(detailPanel).not.toBeNull();
    expect(within(detailPanel!).getByText("Pokrice marze").parentElement).toHaveTextContent("0,0%");
    expect(within(detailPanel!).getByText("Pre/post pokriće uporedive kohorte").parentElement).toHaveTextContent("0,0%");
  });

  it("does not recompute share when the backend omits it", async () => {
    vi.mocked(getColorSalesStats).mockResolvedValue(response({
      colors: [color({ boja: "Bez udela", sharePct: null, ukupanPromet: 120000 })],
    }));

    render(
      <MemoryRouter initialEntries={["/analitika/color-sales-stats"]}>
        <Routes>
          <Route path="/analitika/color-sales-stats" element={<ColorSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    const table = await screen.findByTestId("analytics-data-table");
    const row = within(table).getAllByRole("row").find((candidate) => candidate.textContent?.includes("Bez udela"));
    expect(row).toBeDefined();
    expect(row).not.toHaveTextContent("100,00%");
    expect(row).toHaveTextContent("N/A");
  });

  it("fails closed on invalid share and coverage percentages in the table", async () => {
    vi.mocked(getColorSalesStats).mockResolvedValue(response({
      colors: [color({
        boja: "Nevalidna",
        sharePct: 150,
        prePostNivelacijaRevenueCoveragePct: 130,
        brojArtikalaSaNivelacijom: 12,
        brojArtikalaUkupno: 8,
      })],
    }));

    render(
      <MemoryRouter initialEntries={["/analitika/color-sales-stats"]}>
        <Routes>
          <Route path="/analitika/color-sales-stats" element={<ColorSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    const table = await screen.findByTestId("analytics-data-table");
    const row = within(table).getAllByRole("row").find((candidate) => candidate.textContent?.includes("Nevalidna"));
    expect(row).toBeDefined();
    expect(row).not.toHaveTextContent("150,00%");
    expect(row).not.toHaveTextContent("130,00%");
    expect(row).toHaveTextContent("N/A");
  });
});

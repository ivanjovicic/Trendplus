import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import type { ReactNode } from "react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import ShoeTypeSalesStatsPage from "../ShoeTypeSalesStatsPage";
import { getStores } from "../../services/analyticsApi";
import { getShoeTypeSalesStats } from "../../services/shoeTypeSalesStatsApi";
import { resolveShoeTypeCoveragePct } from "../../utils/shoeTypeSalesCoverage";
import { RECOMMENDATION_SIGNAL_UNAVAILABLE } from "../../utils/canonicalRecommendationSemantics";
import type { ShoeTypeSalesStat, ShoeTypeSalesStatsResponse } from "../../services/shoeTypeSalesStatsApi";

vi.mock("recharts", () => ({
  Bar: () => null,
  BarChart: ({ data, children }: { data?: unknown[]; children?: ReactNode }) => (
    <div data-testid="bar-chart" data-chart-data={JSON.stringify(data ?? [])}>{children}</div>
  ),
  CartesianGrid: () => null,
  Legend: () => null,
  ResponsiveContainer: ({ children }: { children?: ReactNode }) => <div data-testid="responsive-container">{children}</div>,
  Tooltip: () => null,
  XAxis: () => null,
  YAxis: () => null,
}));

vi.mock("../../components/analytics/AnalyticsTrustHeader", () => ({
  default: ({ title, periodFrom, periodTo, lastRefreshAt, dataFreshnessStatus, mode, recommendationAllowed }: {
    title: string;
    periodFrom?: string | null;
    periodTo?: string | null;
    lastRefreshAt?: string | null;
    dataFreshnessStatus?: string | null;
    mode?: string;
    recommendationAllowed?: boolean | null;
  }) => (
    <div
      data-testid="analytics-trust-header"
      data-period-from={periodFrom ?? ""}
      data-period-to={periodTo ?? ""}
      data-last-refresh-at={lastRefreshAt ?? ""}
      data-freshness={dataFreshnessStatus ?? ""}
      data-mode={mode ?? ""}
      data-recommendation-allowed={recommendationAllowed == null ? "" : String(recommendationAllowed)}
    >
      {title}
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
    meta: {
      success: true,
      dataQualityStatus: "good",
      lastRefreshAtUtc: "2026-07-01T08:30:00Z",
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

  it.each([
    [0, 0],
    [1, 0],
    [0, null],
    [0, undefined],
    [undefined, 10],
    [0, Number.NaN],
    [0, Number.POSITIVE_INFINITY],
    [Number.NaN, 10],
    [Number.POSITIVE_INFINITY, 10],
    [9, 8],
  ])("keeps shoe-type coverage unavailable when evidence is not measurable (%s / %s)", (numerator, denominator) => {
    expect(resolveShoeTypeCoveragePct(numerator, denominator)).toBeNull();
  });

  it("preserves a genuine finite zero coverage with a positive denominator", () => {
    expect(resolveShoeTypeCoveragePct(0, 8)).toBe(0);
    expect(resolveShoeTypeCoveragePct(5, 8)).toBe(62.5);
  });

  it("keeps undefined shoe-type coverage unavailable in table, tooltip and detail", async () => {
    vi.mocked(getShoeTypeSalesStats).mockResolvedValue(response({
      shoeTypes: [shoeType({ brojArtikalaSaNivelacijom: 0, brojArtikalaUkupno: 0 })],
    }));

    render(
      <MemoryRouter initialEntries={["/analitika/shoe-type-sales-stats"]}>
        <Routes>
          <Route path="/analitika/shoe-type-sales-stats" element={<ShoeTypeSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    const table = await screen.findByTestId("shoe-type-sales-stats-data-table");
    const row = within(table).getAllByRole("row").find((candidate) => within(candidate).queryByText("Patike"));
    expect(row).toBeDefined();
    if (!row) throw new Error("Shoe type data row was not rendered");
    expect(row).toHaveTextContent("N/A");
    const status = within(row).getByLabelText(/Nivelacija artikala N\/A/);
    expect(status).toBeInTheDocument();
    expect(status).not.toHaveAttribute("aria-label", expect.stringContaining("Nivelacija artikala 0%"));

    within(row).getByRole("button", { name: "Detalji" }).click();
    const detailLabel = await screen.findByText("Pokriće artikala sa nivelacijom");
    expect(detailLabel.parentElement).toHaveTextContent("N/A");
    expect(detailLabel.parentElement).not.toHaveTextContent("0%");
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
    expect(screen.getByTestId("analytics-trust-header")).toHaveAttribute("data-mode", "signal");
    expect(screen.getByTestId("analytics-trust-header")).toHaveAttribute("data-recommendation-allowed", "");
    await waitFor(() => {
      expect(screen.getByTestId("analytics-trust-header")).toHaveAttribute("data-period-from", "2026-06-01T00:00:00Z");
      expect(screen.getByTestId("analytics-trust-header")).toHaveAttribute("data-period-to", "2026-06-30T23:59:59Z");
      expect(screen.getByTestId("analytics-trust-header")).toHaveAttribute("data-last-refresh-at", "2026-07-01T08:30:00Z");
      expect(screen.getByTestId("analytics-trust-header")).toHaveAttribute("data-freshness", "fresh");
    });
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

  it("keeps known backend statuses visible while false or missing permission blocks actionability", async () => {
    const baseRecommendation = shoeType().recommendation!;
    vi.mocked(getShoeTypeSalesStats).mockResolvedValue(response({
      shoeTypes: [
        shoeType({
          tipObuceId: 1,
          tipObuceNaziv: "Dozvoljene patike",
          recommendation: { ...baseRecommendation, recommendationAllowed: true },
        }),
        shoeType({
          tipObuceId: 2,
          tipObuceNaziv: "Patike za pregled",
          recommendation: {
            ...baseRecommendation,
            status: "review",
            summary: "Potrebna je rucna provera.",
            recommendationAllowed: false,
          },
        }),
        shoeType({
          tipObuceId: 3,
          tipObuceNaziv: "Nepouzdan signal",
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
      <MemoryRouter initialEntries={["/analitika/shoe-type-sales-stats"]}>
        <Routes>
          <Route path="/analitika/shoe-type-sales-stats" element={<ShoeTypeSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    const table = await screen.findByTestId("shoe-type-sales-stats-data-table");
    const allowedRow = within(table).getAllByRole("row").find((candidate) => within(candidate).queryByText("Dozvoljene patike"));
    const reviewRow = within(table).getAllByRole("row").find((candidate) => within(candidate).queryByText("Patike za pregled"));
    const doNotTrustRow = within(table).getAllByRole("row").find((candidate) => within(candidate).queryByText("Nepouzdan signal"));
    expect(allowedRow).toBeDefined();
    expect(reviewRow).toBeDefined();
    expect(doNotTrustRow).toBeDefined();
    if (!allowedRow || !reviewRow || !doNotTrustRow) throw new Error("Expected recommendation rows were not rendered");

    expect(within(allowedRow).getByLabelText(/Pojacaj: Jak rast/)).toBeInTheDocument();
    expect(within(allowedRow).queryByText("Akcija blokirana")).not.toBeInTheDocument();
    expect(within(reviewRow).getByLabelText(/Pregledaj: Backend je blokirao izvrsenje preporuke/)).toBeInTheDocument();
    expect(within(reviewRow).getByText("Akcija blokirana")).toBeInTheDocument();
    expect(within(reviewRow).getByLabelText(/Pouzdanost nije dostupna/)).toBeInTheDocument();
    expect(within(doNotTrustRow).getByLabelText(/Ne veruj: Backend nije potvrdio da je preporuka izvrsna/)).toBeInTheDocument();
    expect(within(doNotTrustRow).getByText("Akcija blokirana")).toBeInTheDocument();

    expect(screen.getByLabelText("Raspodela preporuka")).toHaveTextContent("Pojacaj 1");
    expect(screen.getByLabelText("Raspodela preporuka")).toHaveTextContent("Pregledaj 1");
    expect(screen.getByLabelText("Raspodela preporuka")).toHaveTextContent("Ne veruj 1");
    expect(screen.getByLabelText("Raspodela preporuka")).toHaveTextContent("Nedovoljno podataka 0");

    fireEvent.click(within(reviewRow).getByRole("button", { name: "Detalji" }));
    expect(await screen.findByRole("heading", { name: "Detalj odluke: Patike za pregled" })).toBeInTheDocument();
    expect(screen.getByText("Razlog preporuke:").parentElement).toHaveTextContent("Backend je blokirao izvrsenje preporuke: Potrebna je rucna provera.");
    expect(screen.getAllByText(RECOMMENDATION_SIGNAL_UNAVAILABLE)).toHaveLength(2);
  });

  it("fails closed for unknown or missing recommendations without exposing raw status codes", async () => {
    const baseRecommendation = shoeType().recommendation!;
    vi.mocked(getShoeTypeSalesStats).mockResolvedValue(response({
      shoeTypes: [
        shoeType({
          tipObuceId: 1,
          tipObuceNaziv: "Nepoznat status",
          recommendation: {
            ...baseRecommendation,
            status: "backend_future_status" as unknown as typeof baseRecommendation.status,
            summary: "Ovaj tekst ne sme postati status.",
            recommendationAllowed: true,
          },
        }),
        shoeType({
          tipObuceId: 2,
          tipObuceNaziv: "Bez preporuke",
          recommendation: undefined,
        }),
      ],
    }));

    render(
      <MemoryRouter initialEntries={["/analitika/shoe-type-sales-stats"]}>
        <Routes>
          <Route path="/analitika/shoe-type-sales-stats" element={<ShoeTypeSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    const table = await screen.findByTestId("shoe-type-sales-stats-data-table");
    const unknownRow = within(table).getAllByRole("row").find((candidate) => within(candidate).queryByText("Nepoznat status"));
    const missingRow = within(table).getAllByRole("row").find((candidate) => within(candidate).queryByText("Bez preporuke"));
    expect(unknownRow).toBeDefined();
    expect(missingRow).toBeDefined();
    if (!unknownRow || !missingRow) throw new Error("Expected unavailable recommendation rows were not rendered");

    expect(within(unknownRow).getByLabelText(/Nedovoljno podataka: Status preporuke nije prepoznat/)).toBeInTheDocument();
    expect(within(unknownRow).getByText("Akcija blokirana")).toBeInTheDocument();
    expect(within(unknownRow).getByLabelText(/Pouzdanost nije dostupna/)).toBeInTheDocument();
    expect(within(missingRow).getByLabelText(/Nedovoljno podataka: Status preporuke nije prepoznat/)).toBeInTheDocument();
    expect(screen.queryByText("backend_future_status")).not.toBeInTheDocument();
    expect(screen.getByLabelText("Raspodela preporuka")).toHaveTextContent("Nedovoljno podataka 2");

    fireEvent.click(within(unknownRow).getByRole("button", { name: "Detalji" }));
    expect((await screen.findByText("Razlog preporuke:")).parentElement).toHaveTextContent("Status preporuke nije prepoznat; red ostaje informativan bez automatske preporuke.");
  });

  it("fails closed to unknown when the refresh timestamp is missing", async () => {
    vi.mocked(getShoeTypeSalesStats).mockResolvedValue(response({
      meta: {
        success: true,
        dataQualityStatus: "good",
        isPartial: false,
        lastRefreshAtUtc: null,
      },
    }));

    render(
      <MemoryRouter initialEntries={["/analitika/shoe-type-sales-stats"]}>
        <Routes>
          <Route path="/analitika/shoe-type-sales-stats" element={<ShoeTypeSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(screen.getByTestId("analytics-trust-header")).toHaveAttribute("data-freshness", "unknown");
    });
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

  it("keeps a positive total margin as percentage-share comparison data", async () => {
    render(
      <MemoryRouter initialEntries={["/analitika/shoe-type-sales-stats"]}>
        <Routes>
          <Route path="/analitika/shoe-type-sales-stats" element={<ShoeTypeSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByTestId("shoe-type-margin-share-chart")).toBeInTheDocument();
    expect(screen.queryByTestId("shoe-type-margin-value-chart")).not.toBeInTheDocument();
  });

  it.each([
    {
      label: "measured zero",
      totalMarginContribution: 0,
      rows: [
        shoeType({ tipObuceNaziv: "Patike", ukupanPromet: 70000, marginContribution: 0 }),
        shoeType({ tipObuceId: 2, tipObuceNaziv: "Cipele", ukupanPromet: 50000, marginContribution: Number.NaN }),
      ],
      excludedChartName: "Cipele",
    },
    {
      label: "negative total",
      totalMarginContribution: -1000,
      rows: [
        shoeType({ tipObuceNaziv: "Patike", ukupanPromet: 70000, marginContribution: -1500 }),
        shoeType({ tipObuceId: 2, tipObuceNaziv: "Cipele", ukupanPromet: 50000, marginContribution: 500 }),
      ],
    },
  ])("keeps $label margin evidence visible without an undefined margin-share percentage", async ({ totalMarginContribution, rows, excludedChartName }) => {
    vi.mocked(getShoeTypeSalesStats).mockResolvedValue(response({
      shoeTypes: rows,
      totals: {
        ukupanPromet: 120000,
        ukupanMarzniDoprinos: totalMarginContribution,
        prePromet: 90000,
        poslePromet: 30000,
        brojTipovaObuce: rows.length,
        snapshotCostCoveragePct: 0,
        isSnapshotActive: false,
      },
    }));

    render(
      <MemoryRouter initialEntries={["/analitika/shoe-type-sales-stats"]}>
        <Routes>
          <Route path="/analitika/shoe-type-sales-stats" element={<ShoeTypeSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    const chart = await screen.findByTestId("shoe-type-margin-value-chart");
    expect(screen.getByRole("heading", { name: /Maržni doprinos po tipu obuće/ })).toBeInTheDocument();
    expect(chart).toHaveTextContent("Ukupan maržni doprinos je");
    const chartData = within(chart).getByTestId("bar-chart");
    if (excludedChartName) {
      expect(chartData).not.toHaveAttribute("data-chart-data", expect.stringContaining(excludedChartName));
    } else {
      expect(chartData).toHaveAttribute("data-chart-data", expect.stringContaining("Cipele"));
    }
    expect(screen.queryByText("Nema podataka za poređenja.")).not.toBeInTheDocument();
  });

  it.each([null, Number.NaN, Number.POSITIVE_INFINITY])("keeps a non-finite or missing total margin unavailable (%s)", async (totalMarginContribution) => {
    vi.mocked(getShoeTypeSalesStats).mockResolvedValue(response({
      totals: {
        ukupanPromet: 120000,
        ukupanMarzniDoprinos: totalMarginContribution as unknown as number,
        prePromet: 90000,
        poslePromet: 30000,
        brojTipovaObuce: 1,
        snapshotCostCoveragePct: 0,
        isSnapshotActive: false,
      },
    }));

    render(
      <MemoryRouter initialEntries={["/analitika/shoe-type-sales-stats"]}>
        <Routes>
          <Route path="/analitika/shoe-type-sales-stats" element={<ShoeTypeSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByText("Nema podataka za poređenja.")).toBeInTheDocument();
    expect(screen.queryByTestId("shoe-type-margin-share-chart")).not.toBeInTheDocument();
    expect(screen.queryByTestId("shoe-type-margin-value-chart")).not.toBeInTheDocument();
  });

  it("fails closed on malformed backend share percentages in table, KPI and detail", async () => {
    vi.mocked(getShoeTypeSalesStats).mockResolvedValue(response({
      shoeTypes: [
        shoeType({
          tipObuceId: 1,
          tipObuceNaziv: "Patike",
          ukupanPromet: 120000,
          sharePct: 150,
          prePostNivelacijaRevenueCoveragePct: 130,
        }),
        shoeType({
          tipObuceId: 2,
          tipObuceNaziv: "Čizme",
          ukupanPromet: 80000,
          sharePct: -5,
        }),
      ],
      totals: {
        ukupanPromet: 200000,
        ukupanMarzniDoprinos: 46000,
        prePromet: 150000,
        poslePromet: 50000,
        brojTipovaObuce: 2,
        ukupnaKolicina: 20,
        snapshotCostCoveragePct: 0,
        isSnapshotActive: false,
      },
      dataQuality: {
        missingCostRevenue: 0,
        missingCostRevenueSharePct: 140,
        unknownTypeRevenue: 0,
        unknownTypeRevenueSharePct: 0,
        revenueWithNivelacijaSplit: 100000,
        revenueWithNivelacijaSplitSharePct: 75,
      },
    }));

    render(
      <MemoryRouter initialEntries={["/analitika/shoe-type-sales-stats"]}>
        <Routes>
          <Route path="/analitika/shoe-type-sales-stats" element={<ShoeTypeSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    const table = await screen.findByTestId("shoe-type-sales-stats-data-table");
    const patikeRow = within(table).getAllByRole("row").find((candidate) => candidate.textContent?.includes("Patike"));
    const cizmeRow = within(table).getAllByRole("row").find((candidate) => candidate.textContent?.includes("Čizme"));
    expect(patikeRow).toBeDefined();
    expect(cizmeRow).toBeDefined();
    expect(patikeRow).toHaveTextContent("60,00%");
    expect(cizmeRow).toHaveTextContent("40,00%");

    const top5Kpi = screen.getByText("Udeo top 5 tipova").closest("article");
    expect(top5Kpi).not.toBeNull();
    expect(top5Kpi).toHaveTextContent("100,0%");

    fireEvent.click(within(patikeRow!).getByRole("button", { name: "Detalji" }));
    const detailHeading = await screen.findByRole("heading", { name: "Detalj odluke: Patike" });
    const detailPanel = detailHeading.closest("section");
    expect(detailPanel).not.toBeNull();
    expect(within(detailPanel!).getByText("Udeo u prometu").parentElement).toHaveTextContent("60,00%");
    expect(within(detailPanel!).getByText("Pre/post pokrice prometa").parentElement).toHaveTextContent("N/A");
  });

  it("shows unavailable margin-share detail when total margin is zero", async () => {
    vi.mocked(getShoeTypeSalesStats).mockResolvedValue(response({
      shoeTypes: [
        shoeType({
          tipObuceNaziv: "Patike",
          ukupanPromet: 120000,
          marginContribution: 15000,
        }),
      ],
      totals: {
        ukupanPromet: 120000,
        ukupanMarzniDoprinos: 0,
        prePromet: 90000,
        poslePromet: 30000,
        brojTipovaObuce: 1,
        snapshotCostCoveragePct: 0,
        isSnapshotActive: false,
      },
    }));

    render(
      <MemoryRouter initialEntries={["/analitika/shoe-type-sales-stats"]}>
        <Routes>
          <Route path="/analitika/shoe-type-sales-stats" element={<ShoeTypeSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    const table = await screen.findByTestId("shoe-type-sales-stats-data-table");
    const row = within(table).getAllByRole("row").find((candidate) => candidate.textContent?.includes("Patike"));
    expect(row).toBeDefined();
    fireEvent.click(within(row!).getByRole("button", { name: "Detalji" }));

    const detailHeading = await screen.findByRole("heading", { name: "Detalj odluke: Patike" });
    const detailPanel = detailHeading.closest("section");
    expect(detailPanel).not.toBeNull();
    expect(within(detailPanel!).getByText("Udeo u maržnom doprinosu").parentElement).toHaveTextContent("Nije dostupno");
    expect(within(detailPanel!).getByText("Maržni doprinos").parentElement).toHaveTextContent(/15\.000/);
  });

  it("keeps valid raw pre/post detail metrics visible when impact percent is unavailable", async () => {
    vi.mocked(getShoeTypeSalesStats).mockResolvedValue(response({
      shoeTypes: [
        shoeType({
          tipObuceNaziv: "Patike",
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
      <MemoryRouter initialEntries={["/analitika/shoe-type-sales-stats"]}>
        <Routes>
          <Route path="/analitika/shoe-type-sales-stats" element={<ShoeTypeSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    const table = await screen.findByTestId("shoe-type-sales-stats-data-table");
    const row = within(table).getAllByRole("row").find((candidate) => candidate.textContent?.includes("Patike"));
    expect(row).toBeDefined();
    fireEvent.click(within(row!).getByRole("button", { name: "Detalji" }));

    const detailHeading = await screen.findByRole("heading", { name: "Detalj odluke: Patike" });
    const detailPanel = detailHeading.closest("section");
    expect(detailPanel).not.toBeNull();
    expect(within(detailPanel!).getByText("Pre nivelacije promet").parentElement).toHaveTextContent(/90\.000/);
    expect(within(detailPanel!).getByText("Posle nivelacije promet").parentElement).toHaveTextContent(/30\.000/);
    expect(within(detailPanel!).getByText("Pre nivo količina").parentElement).toHaveTextContent(/9.*kom/);
    expect(within(detailPanel!).getByText("Posle nivo količina").parentElement).toHaveTextContent(/3.*kom/);
    expect(within(detailPanel!).getByText("Nivelacija impact prometa").parentElement).toHaveTextContent("N/A");
  });

  it("keeps concentration chart from inventing invalid Ostali share percentages", async () => {
    vi.mocked(getShoeTypeSalesStats).mockResolvedValue(response({
      shoeTypes: [
        ...Array.from({ length: 6 }, (_, index) => shoeType({
          tipObuceId: index + 1,
          tipObuceNaziv: `Tip ${index + 1}`,
          ukupanPromet: 12000 - index,
          sharePct: 60,
        })),
        shoeType({
          tipObuceId: 7,
          tipObuceNaziv: "Tip 7",
          ukupanPromet: 5000,
          sharePct: 55,
        }),
        shoeType({
          tipObuceId: 8,
          tipObuceNaziv: "Tip 8",
          ukupanPromet: 4000,
          sharePct: 55,
        }),
      ],
      totals: {
        ukupanPromet: 80000,
        ukupanMarzniDoprinos: 20000,
        prePromet: 60000,
        poslePromet: 20000,
        brojTipovaObuce: 8,
        snapshotCostCoveragePct: 0,
        isSnapshotActive: false,
      },
    }));

    render(
      <MemoryRouter initialEntries={["/analitika/shoe-type-sales-stats"]}>
        <Routes>
          <Route path="/analitika/shoe-type-sales-stats" element={<ShoeTypeSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    const concentrationChart = await screen.findByTestId("shoe-type-concentration-chart");
    const chartData = JSON.parse(within(concentrationChart).getByTestId("bar-chart").getAttribute("data-chart-data") ?? "[]") as Array<{ name: string; sharePct: number }>;
    expect(chartData.some((entry) => entry.name === "Ostali")).toBe(false);
    expect(chartData).toHaveLength(6);
  });

  it("keeps valid zero and 100 percentages visible across surfaces", async () => {
    vi.mocked(getShoeTypeSalesStats).mockResolvedValue(response({
      shoeTypes: [shoeType({
        tipObuceNaziv: "Patike",
        ukupanPromet: 120000,
        ukupnaKolicina: 0,
        brojArtikalaSaNivelacijom: 0,
        brojArtikalaUkupno: 8,
        sharePct: 100,
        prePostNivelacijaRevenueCoveragePct: 0,
      })],
      totals: {
        ukupanPromet: 120000,
        ukupanMarzniDoprinos: 46000,
        prePromet: 90000,
        poslePromet: 30000,
        brojTipovaObuce: 1,
        ukupnaKolicina: 12,
        snapshotCostCoveragePct: 0,
        isSnapshotActive: false,
      },
    }));

    render(
      <MemoryRouter initialEntries={["/analitika/shoe-type-sales-stats"]}>
        <Routes>
          <Route path="/analitika/shoe-type-sales-stats" element={<ShoeTypeSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    const table = await screen.findByTestId("shoe-type-sales-stats-data-table");
    const row = within(table).getAllByRole("row").find((candidate) => candidate.textContent?.includes("Patike"));
    expect(row).toBeDefined();
    expect(row).toHaveTextContent("100,00%");

    fireEvent.click(within(row!).getByRole("button", { name: "Detalji" }));
    const detailHeading = await screen.findByRole("heading", { name: "Detalj odluke: Patike" });
    const detailPanel = detailHeading.closest("section");
    expect(detailPanel).not.toBeNull();
    expect(within(detailPanel!).getByText("Udeo u količini").parentElement).toHaveTextContent("0,00%");
    expect(within(detailPanel!).getByText("Pre/post pokrice prometa").parentElement).toHaveTextContent("0,0%");
  });
});

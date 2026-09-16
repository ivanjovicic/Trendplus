import { render, screen, waitFor, within } from "@testing-library/react";
import type { ReactNode } from "react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import ShoeTypeSalesStatsPage from "../ShoeTypeSalesStatsPage";
import { getStores } from "../../services/analyticsApi";
import { getShoeTypeSalesStats } from "../../services/shoeTypeSalesStatsApi";
import { RECOMMENDATION_SIGNAL_UNAVAILABLE } from "../../utils/canonicalRecommendationSemantics";
import { resolveShoeTypeCoveragePct } from "../../utils/shoeTypeSalesCoverage";
import { resolveShoeTypePercentValue } from "../../utils/shoeTypePercentRange";
import type { ShoeTypeSalesStat, ShoeTypeSalesStatsResponse } from "../../services/shoeTypeSalesStatsApi";

vi.mock("recharts", () => ({
  Bar: () => null,
  BarChart: () => <div data-testid="bar-chart" />,
  CartesianGrid: () => null,
  Legend: () => null,
  ResponsiveContainer: ({ children }: { children?: ReactNode }) => <div data-testid="responsive-container">{children}</div>,
  Tooltip: () => null,
  XAxis: () => null,
  YAxis: () => null,
}));

vi.mock("../../components/analytics/AnalyticsTrustHeader", () => ({
  default: ({ title, periodFrom, periodTo, lastRefreshAt, dataFreshnessStatus }: {
    title: string;
    periodFrom?: string | null;
    periodTo?: string | null;
    lastRefreshAt?: string | null;
    dataFreshnessStatus?: string | null;
  }) => (
    <div
      data-testid="analytics-trust-header"
      data-period-from={periodFrom ?? ""}
      data-period-to={periodTo ?? ""}
      data-last-refresh-at={lastRefreshAt ?? ""}
      data-freshness={dataFreshnessStatus ?? ""}
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

  it("keeps negative margin comparison visible instead of hiding the chart", async () => {
    vi.mocked(getShoeTypeSalesStats).mockResolvedValue(response({
      shoeTypes: [
        shoeType({
          tipObuceId: 1,
          tipObuceNaziv: "Patike",
          ukupanPromet: 120000,
          marginContribution: -600,
          sharePct: 60,
        }),
        shoeType({
          tipObuceId: 2,
          tipObuceNaziv: "Čizme",
          ukupanPromet: 80000,
          marginContribution: -400,
          sharePct: 40,
        }),
      ],
      totals: {
        ukupanPromet: 200000,
        ukupanMarzniDoprinos: -1000,
        prePromet: 150000,
        poslePromet: 50000,
        brojTipovaObuce: 2,
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

    expect(await screen.findByText("Promet vs Maržni doprinos")).toBeInTheDocument();
    expect(screen.getByText(/Ukupan maržni doprinos je negativan/i)).toBeInTheDocument();
    expect(screen.queryByText("Nema podataka za poređenja.")).not.toBeInTheDocument();
    expect(screen.getAllByTestId("bar-chart").length).toBeGreaterThanOrEqual(1);

    const table = await screen.findByTestId("shoe-type-sales-stats-data-table");
    const patikeRow = within(table).getByText("Patike").closest("tr");
    expect(patikeRow).not.toBeNull();
    within(patikeRow!).getByRole("button", { name: "Detalji" }).click();
    const marginShareLabel = await screen.findByText("Udeo u maržnom doprinosu");
    const detailArticle = marginShareLabel.closest("article");
    expect(detailArticle).not.toBeNull();
    expect(within(detailArticle!).getByText("60,00%")).toBeInTheDocument();
  });

  it("keeps measured zero margin totals visible without treating them as missing data", async () => {
    vi.mocked(getShoeTypeSalesStats).mockResolvedValue(response({
      shoeTypes: [
        shoeType({
          tipObuceId: 1,
          tipObuceNaziv: "Patike",
          ukupanPromet: 120000,
          marginContribution: 0,
          sharePct: 100,
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

    expect(await screen.findByText(/Izmereni ukupan maržni doprinos je 0 RSD/i)).toBeInTheDocument();
    expect(screen.queryByText("Nema podataka za poređenja.")).not.toBeInTheDocument();

    const table = await screen.findByTestId("shoe-type-sales-stats-data-table");
    const patikeRow = within(table).getByText("Patike").closest("tr");
    expect(patikeRow).not.toBeNull();
    within(patikeRow!).getByRole("button", { name: "Detalji" }).click();
    const marginShareLabel = await screen.findByText("Udeo u maržnom doprinosu");
    const detailArticle = marginShareLabel.closest("article");
    expect(detailArticle).not.toBeNull();
    expect(within(detailArticle!).getByText("0,00%")).toBeInTheDocument();
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

  it("fails closed on malformed backend share percentages in table, KPI and export metadata", async () => {
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

    within(patikeRow!).getByRole("button", { name: "Detalji" }).click();
    const detailHeading = await screen.findByRole("heading", { name: "Detalj odluke: Patike" });
    const detailPanel = detailHeading.closest("section");
    expect(detailPanel).not.toBeNull();
    expect(within(detailPanel!).getByText("Udeo u prometu").parentElement).toHaveTextContent("60,00%");
    expect(within(detailPanel!).getByText("Pokriće artikala sa nivelacijom").parentElement).toHaveTextContent("62,5%");
    expect(within(detailPanel!).getByText("Pre/post pokrice prometa").parentElement).toHaveTextContent("N/A");
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

    within(row!).getByRole("button", { name: "Detalji" }).click();
    const detailHeading = await screen.findByRole("heading", { name: "Detalj odluke: Patike" });
    const detailPanel = detailHeading.closest("section");
    expect(detailPanel).not.toBeNull();
    expect(within(detailPanel!).getByText("Udeo u količini").parentElement).toHaveTextContent("0,00%");
    expect(within(detailPanel!).getByText("Pre/post pokrice prometa").parentElement).toHaveTextContent("0,0%");
    expect(resolveShoeTypePercentValue(100)).toBe(100);
    expect(resolveShoeTypePercentValue(0)).toBe(0);
  });

  it("preserves gated review status instead of collapsing it to insufficient data", async () => {
    vi.mocked(getShoeTypeSalesStats).mockResolvedValue(response({
      shoeTypes: [shoeType({
        tipObuceNaziv: "Sandale",
        recommendation: {
          status: "review",
          label: "Review",
          summary: "Mesovit signal zahteva rucni pregled.",
          confidencePct: 55,
          reliabilityPct: 48,
          dataQualityStatus: "warning",
          recommendationAllowed: false,
          reasonCodes: ["weak_signal"],
        },
      })],
    }));

    render(
      <MemoryRouter initialEntries={["/analitika/shoe-type-sales-stats"]}>
        <Routes>
          <Route path="/analitika/shoe-type-sales-stats" element={<ShoeTypeSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    await screen.findByText("Prioritetna lista tipova obuće");
    expect(screen.getByLabelText("Raspodela preporuka")).toHaveTextContent("Pregledaj 1");
    expect(screen.getByLabelText("Raspodela preporuka")).toHaveTextContent("Nedovoljno podataka 0");

    const table = screen.getByTestId("shoe-type-sales-stats-data-table");
    const row = within(table).getAllByRole("row").find((candidate) => candidate.textContent?.includes("Sandale"));
    expect(row).toBeDefined();
    expect(row).toHaveTextContent("Pregledaj");
    expect(row).not.toHaveTextContent("Nedovoljno podataka");

    within(row!).getByRole("button", { name: "Detalji" }).click();
    const detailSection = await screen.findByRole("heading", { name: "Detalj odluke: Sandale" });
    const detailPanel = detailSection.closest("section");
    expect(detailPanel).not.toBeNull();
    expect(within(detailPanel!).getByText(/Automatska preporuka nije dozvoljena: Mesovit signal zahteva rucni pregled/i)).toBeInTheDocument();
    expect(within(detailPanel!).getAllByText(RECOMMENDATION_SIGNAL_UNAVAILABLE).length).toBeGreaterThanOrEqual(2);
  });

  it("preserves gated do-not-trust status and keeps confidence unavailable", async () => {
    vi.mocked(getShoeTypeSalesStats).mockResolvedValue(response({
      shoeTypes: [shoeType({
        tipObuceNaziv: "Cipele",
        recommendation: {
          status: "do_not_trust",
          label: "Do not trust",
          summary: "Signal nije pouzdan za akciju.",
          confidencePct: 20,
          reliabilityPct: 15,
          dataQualityStatus: "critical",
          recommendationAllowed: false,
          reasonCodes: ["data_quality_critical"],
        },
      })],
    }));

    render(
      <MemoryRouter initialEntries={["/analitika/shoe-type-sales-stats"]}>
        <Routes>
          <Route path="/analitika/shoe-type-sales-stats" element={<ShoeTypeSalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    await screen.findByText("Prioritetna lista tipova obuće");
    expect(screen.getByLabelText("Raspodela preporuka")).toHaveTextContent("Ne veruj 1");
    expect(screen.getByLabelText("Raspodela preporuka")).toHaveTextContent("Nedovoljno podataka 0");

    const table = screen.getByTestId("shoe-type-sales-stats-data-table");
    const row = within(table).getAllByRole("row").find((candidate) => candidate.textContent?.includes("Cipele"));
    expect(row).toBeDefined();
    expect(row).toHaveTextContent("Ne veruj");
    expect(row).not.toHaveTextContent("Nedovoljno podataka");

    within(row!).getByRole("button", { name: "Detalji" }).click();
    const detailSection = await screen.findByRole("heading", { name: "Detalj odluke: Cipele" });
    const detailPanel = detailSection.closest("section");
    expect(detailPanel).not.toBeNull();
    expect(within(detailPanel!).getByText(/Automatska preporuka nije dozvoljena: Signal nije pouzdan za akciju/i)).toBeInTheDocument();
  });
});

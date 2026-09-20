import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import type { ReactNode } from "react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import ColorSalesStatsPage from "../ColorSalesStatsPage";
import { getStores } from "../../services/analyticsApi";
import { getAnalyticsDetailSnapshot } from "../../services/analyticsTableState";
import { getColorSalesStats } from "../../services/colorSalesStatsApi";
import type { ColorSalesStat, ColorSalesStatsResponse } from "../../services/colorSalesStatsApi";
import { RECOMMENDATION_SIGNAL_UNAVAILABLE } from "../../utils/canonicalRecommendationSemantics";

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
  const base: ColorSalesStat = {
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
      recommendationAllowed: true,
      reasonCodes: ["strong_pop_growth"],
    },
  };
  return {
    ...base,
    ...overrides,
    recommendation: overrides.recommendation
      ? { recommendationAllowed: true, ...overrides.recommendation }
      : overrides.recommendation,
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

function renderPage() {
  return render(
    <MemoryRouter initialEntries={["/analitika/color-sales-stats"]}>
      <Routes>
        <Route path="/analitika/color-sales-stats" element={<ColorSalesStatsPage />} />
        <Route path="/analitika/color-sales-stats/:color" element={<div>Color detail route</div>} />
      </Routes>
    </MemoryRouter>,
  );
}

function getDecisionTable() {
  return screen.getByRole("table");
}

describe("ColorSalesStatsPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.setItem("trendplus:dataScope", "all");
    vi.mocked(getStores).mockResolvedValue([
      { storeId: 1, storeName: "Centar", city: "Beograd", region: "BG" },
      { storeId: 2, storeName: "Novi Beograd", city: "Beograd", region: "BG" },
    ]);
    vi.mocked(getColorSalesStats).mockResolvedValue(response());
  });

  it("renders premium chrome with shared control bar and shared data table", async () => {
    renderPage();

    expect(screen.getByRole("heading", { name: "Prodaja po boji artikla" })).toBeInTheDocument();
    const controlBar = await screen.findByTestId("analytics-control-bar");
    expect(within(controlBar).getByRole("heading", { name: "Opseg i filteri" })).toBeInTheDocument();
    expect(within(controlBar).getByLabelText("Period")).toBeInTheDocument();
    expect(within(controlBar).getByLabelText("Od")).toBeInTheDocument();
    expect(within(controlBar).getByLabelText("Do")).toBeInTheDocument();
    expect(within(controlBar).getByLabelText("Sezona")).toBeInTheDocument();
    expect(within(controlBar).getByLabelText("Objekat")).toBeInTheDocument();
    expect(within(controlBar).getByRole("link", { name: "Kvalitet podataka" })).toHaveAttribute(
      "href",
      "/analytics/data-quality",
    );

    await screen.findByText("Crna");
    expect(screen.getByText("Koncentracija prometa po bojama")).toBeInTheDocument();
    expect(screen.getByText("Prioritetna lista boja")).toBeInTheDocument();
    expect(screen.getByText("Pojacaj")).toBeInTheDocument();
    expect(screen.getByText("Pregledaj")).toBeInTheDocument();
  });

  it("includes dataScope in list calls, reloads on scope change, and keeps detail navigation aligned", async () => {
    localStorage.setItem("trendplus:dataScope", "imported");
    renderPage();
    await screen.findByText("Prioritetna lista boja");

    expect(getColorSalesStats).toHaveBeenCalledWith(expect.objectContaining({ dataScope: "imported" }));

    localStorage.setItem("trendplus:dataScope", "existing");
    await act(async () => {
      window.dispatchEvent(new Event("trendplus:data-scope-changed"));
    });

    await waitFor(() => {
      expect(getColorSalesStats).toHaveBeenLastCalledWith(expect.objectContaining({ dataScope: "existing" }));
    });

    fireEvent.click(screen.getAllByRole("button", { name: "Detalji" })[0]);
    fireEvent.click(await screen.findByRole("button", { name: "Otvori puni detalj" }));

    expect(await screen.findByText("Color detail route")).toBeInTheDocument();
    const snapshot = getAnalyticsDetailSnapshot("color-sales-stats", encodeURIComponent("Crna"));
    expect(snapshot?.metadata.some((field) => field.key === "dataScope" && field.value === "existing")).toBe(true);
  });

  it("blocks invalid date ranges before issuing a new analytics request", async () => {
    renderPage();
    await screen.findByText("Prioritetna lista boja");
    expect(getColorSalesStats).toHaveBeenCalledTimes(1);

    const [fromInput, toInput] = Array.from(document.querySelectorAll<HTMLInputElement>('input[type="date"]'));
    fireEvent.change(fromInput, { target: { value: "2026-07-10" } });
    fireEvent.change(toInput, { target: { value: "2026-07-01" } });
    fireEvent.click(screen.getByRole("button", { name: "Primeni filtere" }));

    expect(screen.getByText("Datum „Od” ne može biti posle datuma „Do”.")).toBeInTheDocument();
    expect(getColorSalesStats).toHaveBeenCalledTimes(1);
  });

  it("applies season and store filters using backend query semantics", async () => {
    renderPage();
    await screen.findByText("Prioritetna lista boja");

    const comboBoxes = screen.getAllByRole("combobox");
    const seasonSelect = comboBoxes[1];
    const storeSelect = comboBoxes[2];

    fireEvent.change(seasonSelect, { target: { value: "3" } });
    fireEvent.change(storeSelect, { target: { value: "2" } });
    fireEvent.click(screen.getByRole("button", { name: "Primeni filtere" }));

    await waitFor(() => expect(getColorSalesStats).toHaveBeenCalledTimes(2));
    expect(getColorSalesStats).toHaveBeenLastCalledWith(expect.objectContaining({
      fromDate: "2026-06-01T00:00:00Z",
      toDate: "2026-08-31T23:59:59Z",
      sezonaId: 3,
      storeId: 2,
    }));
  });

  it("sorts visible table rows without changing the source export row count", async () => {
    vi.mocked(getColorSalesStats).mockResolvedValue(response({
      colors: [
        color({
          boja: "Crna",
          ukupanPromet: 120000,
          marginContribution: 46000,
          recommendation: {
            status: "increase_focus",
            label: "Increase focus",
            summary: "Jak rast.",
            confidencePct: 88,
            reliabilityPct: 82,
            dataQualityStatus: "good",
            reasonCodes: [],
          },
        }),
        color({
          boja: "Bela",
          ukupanPromet: 220000,
          marginContribution: 72000,
          recommendation: {
            status: "maintain",
            label: "Maintain",
            summary: "Stabilno.",
            confidencePct: 70,
            reliabilityPct: 80,
            dataQualityStatus: "good",
            reasonCodes: [],
          },
        }),
      ],
    }));

    renderPage();
    await screen.findByText("Prioritetna lista boja");
    await waitFor(() => {
      expect(screen.getByTestId("analytics-toolbar")).toBeInTheDocument();
    }, { timeout: 5000 });

    const table = getDecisionTable();
    const revenueButton = within(table).getAllByRole("button").find((button) => button.textContent?.startsWith("Promet"));
    expect(revenueButton).toBeDefined();
    fireEvent.click(revenueButton as HTMLButtonElement);

    const rows = within(table).getAllByRole("row");
    expect(rows[1]).toHaveTextContent("Bela");
    expect(rows[2]).toHaveTextContent("Crna");
    await waitFor(() => {
      expect(screen.getByTestId("analytics-toolbar")).toHaveTextContent("color-sales-stats: 2 rows");
    }, { timeout: 5000 });
  });

  it("preserves backend insufficient_data as Nedovoljno podataka instead of Zadrži", async () => {
    vi.mocked(getColorSalesStats).mockResolvedValue(response({
      colors: [
        color({
          boja: "Siva",
          ukupanPromet: 8000,
          marginContribution: 500,
          recommendation: {
            status: "insufficient_data",
            label: "Insufficient data",
            summary: "Nedovoljno signala.",
            confidencePct: 20,
            reliabilityPct: 35,
            dataQualityStatus: "insufficient_data",
            reasonCodes: ["insufficient_data"],
          },
        }),
        color({
          boja: "Crna",
          ukupanPromet: 120000,
          marginContribution: 46000,
          recommendation: {
            status: "increase_focus",
            label: "Increase focus",
            summary: "Jak rast.",
            confidencePct: 88,
            reliabilityPct: 82,
            dataQualityStatus: "good",
            reasonCodes: [],
          },
        }),
      ],
    }));

    renderPage();
    await screen.findByText("Prioritetna lista boja");

    expect(screen.getByText(/Nedovoljno podataka: 1/)).toBeInTheDocument();
    expect(screen.getByText(/Zadrzi: 0/)).toBeInTheDocument();

    const table = getDecisionTable();
    const sivaRow = within(table).getAllByRole("row").find((row) => row.textContent?.includes("Siva"));
    expect(sivaRow).toBeDefined();
    expect(sivaRow).toHaveTextContent("Nedovoljno podataka");
    expect(sivaRow).not.toHaveTextContent("Zadrzi");
  });

  it("hides an action when the backend recommendation is not allowed", async () => {
    vi.mocked(getColorSalesStats).mockResolvedValue(response({
      colors: [color({
        recommendation: {
          status: "increase_focus",
          label: "Increase focus",
          summary: "Pre/post signal nije uporediv.",
          confidencePct: null,
          reliabilityPct: null,
          dataQualityStatus: "warning",
          recommendationAllowed: false,
          reasonCodes: ["insufficient_data"],
        },
      })],
    }));

    renderPage();
    await screen.findByText("Prioritetna lista boja");

    expect(screen.getByText(/Pojacaj: 1/)).toBeInTheDocument();
    expect(screen.getByText(/Nedovoljno podataka: 0/)).toBeInTheDocument();
    const table = getDecisionTable();
    const row = within(table).getAllByRole("row").find((candidate) => candidate.textContent?.includes("Crna"));
    expect(row).toBeDefined();
    expect(row).toHaveTextContent("Pojacaj");
    expect(row).toHaveTextContent("Akcija blokirana");
    expect(row).not.toHaveTextContent("Nedovoljno podataka");
  });

  it("does not invent a business recommendation when backend recommendation is missing", async () => {
    vi.mocked(getColorSalesStats).mockResolvedValue(response({
      colors: [
        color({
          boja: "Teget",
          ukupanPromet: 250000,
          marginContribution: 90000,
          sharePct: 70,
          popRevenueChangePct: 80,
          recommendation: undefined,
        }),
        color({
          boja: "Crna",
          ukupanPromet: 120000,
          marginContribution: 46000,
          recommendation: {
            status: "increase_focus",
            label: "Increase focus",
            summary: "Jak rast.",
            confidencePct: 88,
            reliabilityPct: 82,
            dataQualityStatus: "good",
            reasonCodes: [],
          },
        }),
      ],
    }));

    renderPage();
    await screen.findByText("Prioritetna lista boja");

    expect(screen.getByText(/Nedovoljno podataka: 1/)).toBeInTheDocument();
    expect(screen.getByText(/Pojacaj: 1/)).toBeInTheDocument();
    expect(screen.getByText(/Zadrzi: 0/)).toBeInTheDocument();
    expect(screen.getByText(/Pregledaj: 0/)).toBeInTheDocument();
    expect(screen.getByText(/Ne veruj: 0/)).toBeInTheDocument();

    const table = getDecisionTable();
    const tegetRow = within(table).getAllByRole("row").find((row) => row.textContent?.includes("Teget"));
    expect(tegetRow).toBeDefined();
    expect(tegetRow).toHaveTextContent("Nedovoljno podataka");
    expect(tegetRow).not.toHaveTextContent("Pojacaj");
    expect(tegetRow).not.toHaveTextContent("Zadrzi");
    expect(tegetRow).not.toHaveTextContent("Pregledaj");
    expect(tegetRow).not.toHaveTextContent("Ne veruj");

    fireEvent.click(within(tegetRow as HTMLElement).getByRole("button", { name: "Detalji" }));
    expect((await screen.findByText("Razlog preporuke:")).parentElement).toHaveTextContent(/Backend preporuka nije dostupna/i);
  });

  it("error hides KPI zeros when color sales fails", async () => {
    vi.mocked(getColorSalesStats).mockRejectedValue(new Error("backend down"));

    renderPage();

    expect(await screen.findByRole("alert")).toHaveTextContent(/Boje trenutno nisu dostupne/i);
    expect(screen.getByRole("alert")).toHaveTextContent("backend down");
    expect(screen.queryByText("Ukupan promet")).not.toBeInTheDocument();
    expect(screen.queryByText("Prioritetna lista boja")).not.toBeInTheDocument();
  });

  it("keeps the newest filter result when an older response arrives late", async () => {
    type Deferred<T> = {
      promise: Promise<T>;
      resolve: (value: T) => void;
    };
    const createDeferred = <T,>(): Deferred<T> => {
      let resolve!: (value: T) => void;
      const promise = new Promise<T>((nextResolve) => {
        resolve = nextResolve;
      });
      return { promise, resolve };
    };

    const firstPayload = createDeferred<ReturnType<typeof response>>();
    const secondPayload = createDeferred<ReturnType<typeof response>>();

    vi.mocked(getColorSalesStats)
      .mockImplementationOnce(() => firstPayload.promise)
      .mockImplementationOnce(() => secondPayload.promise);

    renderPage();
    await waitFor(() => {
      expect(getColorSalesStats).toHaveBeenCalledTimes(1);
    });

    localStorage.setItem("trendplus:dataScope", "existing");
    await act(async () => {
      window.dispatchEvent(new Event("trendplus:data-scope-changed"));
    });

    await waitFor(() => {
      expect(getColorSalesStats).toHaveBeenCalledTimes(2);
    });

    secondPayload.resolve(response({
      colors: [color({ boja: "Plava" })],
      dataScope: "existing",
    }));

    await waitFor(() => {
      expect(screen.getByText("Plava")).toBeInTheDocument();
    });

    firstPayload.resolve(response({
      colors: [color({ boja: "Crvena" })],
    }));

    await waitFor(() => {
      expect(screen.getByText("Plava")).toBeInTheDocument();
    });
    expect(screen.queryByText("Crvena")).not.toBeInTheDocument();
  });

  it("shows stale overlay and keeps prior table data when a refetch fails after a successful load", async () => {
    vi.mocked(getColorSalesStats)
      .mockResolvedValueOnce(response())
      .mockRejectedValueOnce(new Error("Network error on refetch"));

    renderPage();
    await screen.findByText("Prioritetna lista boja");
    expect(screen.getByText("Crna")).toBeInTheDocument();

    localStorage.setItem("trendplus:dataScope", "existing");
    await act(async () => {
      window.dispatchEvent(new Event("trendplus:data-scope-changed"));
    });

    await waitFor(() => {
      expect(screen.getByTestId("color-stale-refetch-warning")).toHaveTextContent(
        "Prikazujemo prethodno učitane podatke. Novi upit nije uspeo.",
      );
    });
    expect(screen.getByText("Crna")).toBeInTheDocument();
    expect(screen.getByText("Prioritetna lista boja")).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("empty is not error when color sales returns no rows", async () => {
    vi.mocked(getColorSalesStats).mockResolvedValue(response({
      colors: [],
      dataQuality: {
        missingCostRevenue: 0,
        missingCostRevenueSharePct: 0,
        estimatedCostRevenue: 0,
        estimatedCostRevenueSharePct: 0,
        unknownColorRevenue: 0,
        unknownColorRevenueSharePct: 0,
        revenueWithNivelacijaSplit: 0,
        revenueWithNivelacijaSplitSharePct: 0,
      },
    }));

    renderPage();

    expect(await screen.findByRole("heading", { name: /Nema (podataka|dovoljno podataka)/i })).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(screen.queryByText("Ukupan promet")).not.toBeInTheDocument();
    expect(screen.queryByText("Ukupan marzni doprinos")).not.toBeInTheDocument();
  });

  it("mounts the real AnalyticsTrustHeader, not a mocked placeholder", async () => {
    renderPage();

    expect(screen.getByRole("region", { name: "Kontekst pouzdanosti analitike" })).toBeInTheDocument();
    expect(screen.getByText("Preporuka sistema")).toBeInTheDocument();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("heading", { level: 1, name: "Prodaja po boji artikla" })).toBeInTheDocument();
    await screen.findByText("Crna");
  });

  it("does not invent decision score 0 or reliability from margin coverage", async () => {
    vi.mocked(getColorSalesStats).mockResolvedValue(response({
      colors: [
        color({
          boja: "Crvena",
          ukupanPromet: 150000,
          marginContribution: 50000,
          reliabilityPct: undefined,
          marginDataCoveragePct: 83.3,
          recommendation: {
            status: "increase_focus",
            label: "Increase focus",
            summary: "Jak rast.",
            confidencePct: undefined as unknown as number,
            reliabilityPct: undefined as unknown as number,
            dataQualityStatus: "good",
            reasonCodes: [],
          },
        }),
      ],
    }));

    renderPage();
    await screen.findByText("Prioritetna lista boja");

    fireEvent.click(screen.getAllByRole("button", { name: "Detalji" })[0]);
    const decisionScore = await screen.findByText("Decision score");
    expect(decisionScore.closest("article")).toHaveTextContent("N/A");
    expect(decisionScore.closest("article")).not.toHaveTextContent(/Decision score\s*0/);

    const reliability = screen.getByText("Pouzdanost podataka");
    expect(reliability.closest("article")).toHaveTextContent(RECOMMENDATION_SIGNAL_UNAVAILABLE);
    expect(reliability.closest("article")).not.toHaveTextContent("83,3");
  });

  it("expands a color row and saves a detail snapshot before navigating to the detail route", async () => {
    renderPage();
    await screen.findByText("Prioritetna lista boja");

    fireEvent.click(screen.getAllByRole("button", { name: "Detalji" })[0]);
    expect(await screen.findByRole("heading", { name: /Detalj odluke:/i })).toBeInTheDocument();
    expect(screen.getByText("PoP trend prometa")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Otvori puni detalj" }));

    expect(await screen.findByText("Color detail route")).toBeInTheDocument();
    const snapshot = getAnalyticsDetailSnapshot("color-sales-stats", encodeURIComponent("Crna"));
    expect(snapshot).toEqual(expect.objectContaining({
      table: "color-sales-stats",
      recordId: "Crna",
      title: "Crna",
    }));
    expect(snapshot?.fields.some((field) => field.key === "ukupanPromet" && field.value === "120.000 RSD")).toBe(true);
  });

  it("keeps export snapshot pre/post fields aligned when impact percent is unavailable", async () => {
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
        }),
      ],
    }));

    renderPage();
    await screen.findByText("Prioritetna lista boja");

    fireEvent.click(screen.getAllByRole("button", { name: "Detalji" })[0]);
    fireEvent.click(screen.getByRole("button", { name: "Otvori puni detalj" }));

    expect(await screen.findByText("Color detail route")).toBeInTheDocument();
    const snapshot = getAnalyticsDetailSnapshot("color-sales-stats", encodeURIComponent("Crna"));
    expect(snapshot?.fields.some((field) => field.key === "preNivelacijePromet" && field.value === "90.000 RSD")).toBe(true);
    expect(snapshot?.fields.some((field) => field.key === "posleNivelacijePromet" && field.value === "30.000 RSD")).toBe(true);
    expect(snapshot?.fields.some((field) => field.key === "preNivelacijeKolicina" && field.value === "9 kom")).toBe(true);
    expect(snapshot?.fields.some((field) => field.key === "posleNivelacijeKolicina" && field.value === "3 kom")).toBe(true);
    expect(snapshot?.fields.some((field) => field.key === "prePostNivelacijaRevenueImpactPct" && field.value === "N/A")).toBe(true);
  });
});

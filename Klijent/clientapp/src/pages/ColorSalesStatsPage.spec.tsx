import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import type { ReactNode } from "react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import ColorSalesStatsPage from "./ColorSalesStatsPage";
import { getStores } from "../services/analyticsApi";
import { getAnalyticsDetailSnapshot } from "../services/analyticsTableState";
import { getColorSalesStats } from "../services/colorSalesStatsApi";
import type { ColorSalesStat, ColorSalesStatsResponse } from "../services/colorSalesStatsApi";

vi.mock("recharts", () => ({
  Bar: () => null,
  BarChart: ({ children }: { children?: ReactNode }) => <div data-testid="bar-chart">{children}</div>,
  CartesianGrid: () => null,
  ResponsiveContainer: ({ children }: { children?: ReactNode }) => <div data-testid="responsive-container">{children}</div>,
  Tooltip: () => null,
  XAxis: () => null,
  YAxis: () => null,
}));

vi.mock("../components/analytics/AnalyticsTableToolbar", () => ({
  default: ({ tableKey, rows }: { tableKey: string; rows: unknown[] }) => (
    <div data-testid="analytics-toolbar">
      {tableKey}: {rows.length} rows
    </div>
  ),
}));

vi.mock("../components/ui/InfoTip", () => ({
  default: ({ text }: { text: string }) => <span data-testid="info-tip">{text}</span>,
}));

vi.mock("../services/analyticsApi", async () => {
  const actual = await vi.importActual<typeof import("../services/analyticsApi")>("../services/analyticsApi");
  return {
    ...actual,
    getStores: vi.fn(),
  };
});

vi.mock("../services/colorSalesStatsApi", async () => {
  const actual = await vi.importActual<typeof import("../services/colorSalesStatsApi")>("../services/colorSalesStatsApi");
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
    color({ boja: "Crna", ukupanPromet: 120000, marginContribution: 46000, recommendation: { status: "increase_focus", label: "Increase focus", summary: "Jak rast i zdrava marža.", confidencePct: 88, reliabilityPct: 82, dataQualityStatus: "good", reasonCodes: ["strong_pop_growth"] } }),
    color({ boja: "Bež", ukupanPromet: 45000, marginContribution: 12000, popRevenueChangePct: -20, recommendation: { status: "review", label: "Review", summary: "Pad i slabiji doprinos.", confidencePct: 61, reliabilityPct: 70, dataQualityStatus: "warning", reasonCodes: ["weak_pop"] } }),
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
    vi.mocked(getStores).mockResolvedValue([
      { storeId: 1, storeName: "Centar", city: "Beograd", region: "BG" },
      { storeId: 2, storeName: "Novi Beograd", city: "Beograd", region: "BG" },
    ]);
    vi.mocked(getColorSalesStats).mockResolvedValue(response());
  });

  it("loads premium color analytics with KPIs, quality notes, table and export context", async () => {
    renderPage();

    expect(screen.getByRole("heading", { name: "Prodaja po boji artikla" })).toBeInTheDocument();
    await screen.findByText("Prioritetna lista boja");

    expect(getColorSalesStats).toHaveBeenCalledWith(expect.objectContaining({
      fromDate: expect.stringMatching(/^\d{4}-\d{2}-\d{2}T00:00:00Z$/),
      toDate: expect.stringMatching(/^\d{4}-\d{2}-\d{2}T23:59:59Z$/),
      sezonaId: null,
      storeId: null,
    }));
    expect(screen.getByText("Ukupan promet")).toBeInTheDocument();
    expect(screen.getByText("Udeo top 5 boja")).toBeInTheDocument();
    expect(screen.getByText("Koncentracija prometa po bojama")).toBeInTheDocument();
    expect(screen.getByText(/Kvalitet podataka:/)).toBeInTheDocument();
    expect(screen.getByText(/Pre\/posle nivelacije trenutno pokriva/i)).toBeInTheDocument();
    expect(screen.getByText("Crna")).toBeInTheDocument();
    expect(screen.getByText("Bež")).toBeInTheDocument();
    expect(screen.getByText("Pojačaj")).toBeInTheDocument();
    expect(screen.getByText("Smanji")).toBeInTheDocument();
    expect(screen.getByTestId("analytics-toolbar")).toHaveTextContent("color-sales-stats: 2 rows");
  });

  it("blocks invalid date ranges before issuing a new analytics request", async () => {
    renderPage();
    await screen.findByText("Prioritetna lista boja");
    expect(getColorSalesStats).toHaveBeenCalledTimes(1);

    const [fromInput, toInput] = Array.from(document.querySelectorAll<HTMLInputElement>('input[type="date"]'));
    fireEvent.change(fromInput, { target: { value: "2026-07-10" } });
    fireEvent.change(toInput, { target: { value: "2026-07-01" } });
    fireEvent.click(screen.getByRole("button", { name: "Primeni" }));

    expect(screen.getByText("Datum od ne moze biti posle datuma do.")).toBeInTheDocument();
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
    fireEvent.click(screen.getByRole("button", { name: "Primeni" }));

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
        color({ boja: "Crna", ukupanPromet: 120000, marginContribution: 46000, recommendation: { status: "increase_focus", label: "Increase focus", summary: "Jak rast.", confidencePct: 88, reliabilityPct: 82, dataQualityStatus: "good", reasonCodes: [] } }),
        color({ boja: "Bela", ukupanPromet: 220000, marginContribution: 72000, recommendation: { status: "maintain", label: "Maintain", summary: "Stabilno.", confidencePct: 70, reliabilityPct: 80, dataQualityStatus: "good", reasonCodes: [] } }),
      ],
    }));

    renderPage();
    await screen.findByText("Prioritetna lista boja");

    const table = getDecisionTable();
    const revenueButton = within(table).getAllByRole("button").find((button) => button.textContent?.startsWith("Promet"));
    expect(revenueButton).toBeDefined();
    fireEvent.click(revenueButton as HTMLButtonElement);

    const rows = within(table).getAllByRole("row");
    expect(rows[1]).toHaveTextContent("Bela");
    expect(rows[2]).toHaveTextContent("Crna");
    expect(screen.getByTestId("analytics-toolbar")).toHaveTextContent("color-sales-stats: 2 rows");
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
    expect(snapshot?.fields.some((field) => field.key === "ukupanPromet" && field.value === "120000")).toBe(true);
  });
});

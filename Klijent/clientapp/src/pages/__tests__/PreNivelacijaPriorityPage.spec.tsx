import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router-dom";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import PreNivelacijaPriorityPage, { decisionColumns } from "../PreNivelacijaPriorityPage";

vi.mock("recharts", () => ({
  BarChart: ({ children }: { children?: ReactNode }) => <div>{children}</div>,
  ResponsiveContainer: ({ children }: { children?: ReactNode }) => <div>{children}</div>,
  CartesianGrid: () => <div />,
  XAxis: () => <div />,
  YAxis: () => <div />,
  Tooltip: () => <div />,
  Bar: () => <div />,
}));

vi.mock("../../components/analytics/AnalyticsTrustHeader", () => ({
  default: ({ title }: { title: string }) => <h1 data-testid="analytics-trust-header">{title}</h1>,
}));
vi.mock("../../components/analytics/AnalyticsTableToolbar", () => ({ default: () => null }));
vi.mock("../../components/analytics/AnalyticsErrorState", () => ({
  default: ({ title, message }: { title: string; message: string }) => (
    <div role="alert">
      <strong>{title}</strong>
      <span>{message}</span>
    </div>
  ),
}));
vi.mock("../../components/ui/InfoTip", () => ({ default: () => null }));

const getPreNivelacijaPrioritetiMock = vi.fn();

vi.mock("../../services/preNivelacijaApi", () => ({
  getPreNivelacijaPrioriteti: (...args: unknown[]) => getPreNivelacijaPrioritetiMock(...args),
}));

function makeCandidate(overrides: Record<string, unknown> = {}) {
  return {
    artikalId: 101,
    sku: "SKU-101",
    supplierId: 11,
    seasonId: 7,
    footwearTypeId: 4,
    supplierName: "Dobavljac A",
    category: "Patike",
    footwearType: "Sneaker",
    season: "Prolece/Leto",
    stockUnits: 12,
    units180: 24,
    velocity180: 0.8,
    daysSinceLastSale: 45,
    markdownEvents: 1,
    avgMarkdownPct: 12,
    grossMarginPctEst: 34,
    seasonRecencyBoost: 18,
    preNivelacijaScore: 86,
    priorityBand: "high",
    scoreBreakdown: {
      stockPressure: 71,
      velocityRisk: 63,
      recencyRisk: 54,
      markdownOpportunity: 42,
      marginPotential: 61,
      seasonRecencyBoost: 18,
    },
    scenarioHighlightNow: {
      expectedUnits30d: 16,
      expectedRevenue30d: 124000,
      expectedMargin30d: 36000,
      effectivePrice: 7990,
    },
    scenarioMarkdownNow: {
      expectedUnits30d: 18,
      expectedRevenue30d: 117000,
      expectedMargin30d: 33000,
      effectivePrice: 7190,
    },
    marginDeltaHighlightVsMarkdown: 3000,
    revenueDeltaHighlightVsMarkdown: 7000,
    confidence: "High",
    reliabilityPct: 82,
    decisionScore: 88,
    recommendation: {
      status: "insufficient_data",
      label: "Nedovoljno podataka",
      summary: "Signal nije dovoljno jak za brzu odluku.",
      confidencePct: 22,
      reliabilityPct: 28,
      dataQualityStatus: "insufficient_data",
      reasonCodes: ["insufficient_history"],
    },
    ...overrides,
  };
}

function makeResponse(candidates = [makeCandidate(), makeCandidate({
  artikalId: 102,
  sku: "SKU-102",
  supplierName: "Dobavljac B",
  category: "Sandale",
  footwearType: "Open Toe",
  season: "Jesen/Zima",
  priorityBand: "medium",
  stockUnits: 20,
  daysSinceLastSale: 12,
  preNivelacijaScore: 63,
  recommendation: {
    status: "review",
    label: "Pregled",
    summary: "Signal trazi rucnu proveru.",
    confidencePct: 64,
    reliabilityPct: 61,
    dataQualityStatus: "warning",
    reasonCodes: ["sparse_sales"],
  },
})]) {
  return {
    generatedAtUtc: "2026-06-19T10:00:00Z",
    formulaVersion: "1.0",
    formulaDescription: "Rule-based markdown scenario support.",
    summary: {
      supplierCount: 1,
      candidatesCount: candidates.length,
      highPriorityCount: 1,
      totalStockAtRisk: 12,
      estimatedAvoidableMarkdownLoss: 12500,
      expectedHighlightRevenueUplift: 18000,
      averagePreNivelacijaScore: 74,
    },
    supplierLeaderboard: [
      {
        supplierId: 11,
        supplierName: "Dobavljac A",
        highPrioritySkuCount: 1,
        candidateSkuCount: candidates.length,
        stockUnitsAtRisk: 12,
        estimatedAvoidableMarkdownLoss: 12500,
        expectedHighlightRevenueUplift: 18000,
        actionScore: 92,
        weekOverWeekRiskDeltaPct: 4,
      },
    ],
    candidates,
    queues: {
      highlightNow: [
        {
          artikalId: 101,
          sku: "SKU-101",
          supplierName: "Dobavljac A",
          preNivelacijaScore: 86,
          priorityBand: "high",
          owner: "Ana",
          status: "insufficient_data",
          dueDateUtc: "2026-06-20T00:00:00Z",
        },
      ],
      monitor: [],
      likelyMarkdownSoon: [],
    },
    alerts: [],
    page: 1,
    pageSize: 60,
    totalCandidates: candidates.length,
    meta: {
      success: true,
      dataQualityStatus: "good",
    },
  };
}

function LocationProbe() {
  const location = useLocation();
  return <output data-testid="location-search">{location.pathname}{location.search}</output>;
}

describe("PreNivelacijaPriorityPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    getPreNivelacijaPrioritetiMock.mockResolvedValue(makeResponse());
  });

  it("does not rank insufficient_data candidates as high priority", async () => {
    render(
      <MemoryRouter initialEntries={["/analytics/pre-nivelacija-prioriteti"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    expect(await screen.findByRole("tab", { name: /Visok prioritet \(0\)/i })).toBeInTheDocument();
    expect(screen.queryByText(/SKU traži brzu proveru/i)).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("tab", { name: /Visok prioritet \(0\)/i }));

    expect(await screen.findByRole("heading", { name: "Nema rezultata za trenutne filtere." })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Vrati prikaz svih prioriteta." })).toBeInTheDocument();
    expect(screen.queryByText("SKU-101")).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Vrati prikaz svih prioriteta." }));
    expect((await screen.findAllByText("SKU-101")).length).toBeGreaterThan(0);
  });

  it("renders shared control bar and data table chrome", async () => {
    render(
      <MemoryRouter initialEntries={["/analytics/pre-nivelacija-prioriteti"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    expect(await screen.findByTestId("analytics-control-bar")).toBeInTheDocument();
    expect(await screen.findByTestId("pre-nivelacija-prioriteti-data-table")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Primeni filtere/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Reset filtera/i })).toBeInTheDocument();
  });

  it("keeps the trust header as the only page-level h1", async () => {
    render(
      <MemoryRouter initialEntries={["/analytics/pre-nivelacija-prioriteti"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    await screen.findByTestId("pre-nivelacija-prioriteti-data-table");

    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("heading", { level: 1, name: "Prioriteti pre-nivelacije" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 2, name: "Prioriteti pre-nivelacije" })).toBeInTheDocument();
  });

  it("shows backend reliability as a percent instead of local Visoko/Srednje/Nisko bands", async () => {
    getPreNivelacijaPrioritetiMock.mockResolvedValueOnce(makeResponse([
      makeCandidate({
        recommendation: {
          status: "review",
          label: "Pregled",
          summary: "Signal zahteva proveru.",
          confidencePct: 22,
          reliabilityPct: 28,
          dataQualityStatus: "warning",
          recommendationAllowed: true,
          reasonCodes: ["review_signal"],
        },
      }),
      makeCandidate({
        artikalId: 102,
        sku: "SKU-102",
        recommendation: {
          status: "maintain",
          label: "Zadrži",
          summary: "Signal je upotrebljiv.",
          confidencePct: 64,
          reliabilityPct: 61,
          dataQualityStatus: "good",
          recommendationAllowed: true,
          reasonCodes: ["stable_signal"],
        },
      }),
    ]));

    render(
      <MemoryRouter initialEntries={["/analytics/pre-nivelacija-prioriteti"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    expect(await screen.findByText("28%")).toBeInTheDocument();
    expect(screen.getByText("61%")).toBeInTheDocument();
    expect(screen.queryByText("Visoko")).not.toBeInTheDocument();
    expect(screen.queryByText("Srednje")).not.toBeInTheDocument();
    expect(screen.queryByText("Nisko")).not.toBeInTheDocument();
  });

  it.each([
    [0, "signal-weak"],
    [55, "signal-watch"],
    [100, "signal-strong"],
  ])("uses a valid reliability tone for %s%% instead of unavailable styling", async (reliabilityPct, expectedClass) => {
    getPreNivelacijaPrioritetiMock.mockResolvedValueOnce(makeResponse([
      makeCandidate({
        recommendation: {
          status: "review",
          label: "Pregled",
          summary: "Validan reliability signal.",
          confidencePct: 64,
          reliabilityPct,
          dataQualityStatus: "good",
          recommendationAllowed: true,
          reasonCodes: ["review_signal"],
        },
      }),
    ]));

    render(
      <MemoryRouter initialEntries={["/analytics/pre-nivelacija-prioriteti"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    const reliability = await screen.findByText(`${reliabilityPct}%`);
    expect(reliability).toHaveClass(expectedClass);
    expect(reliability).not.toHaveClass("signal-na");
  });

  it.each([null, Number.NaN, Number.POSITIVE_INFINITY, "55"])("keeps malformed reliability %s unavailable", async (reliabilityPct) => {
    getPreNivelacijaPrioritetiMock.mockResolvedValueOnce(makeResponse([
      makeCandidate({
        reliabilityPct,
        recommendation: {
          status: "review",
          label: "Pregled",
          summary: "Reliability signal nije pouzdan.",
          confidencePct: 64,
          reliabilityPct,
          dataQualityStatus: "warning",
          recommendationAllowed: true,
          reasonCodes: ["review_signal"],
        },
      }),
    ]));

    render(
      <MemoryRouter initialEntries={["/analytics/pre-nivelacija-prioriteti"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    const unavailable = (await screen.findAllByText("Nije dostupno")).find((element) =>
      element.className.includes("signal-na"),
    );
    expect(unavailable).toBeTruthy();
  });

  it("exports reliability as a percent while preserving null as unavailable", () => {
    const reliabilityColumn = decisionColumns.find((column) => column.key === "reliabilityPct");

    expect(reliabilityColumn).toMatchObject({
      dataType: "percent",
    });
    expect(reliabilityColumn?.getValue?.({ reliabilityAvailable: true, reliabilityPct: 0 } as never)).toBe(0);
    expect(reliabilityColumn?.getValue?.({ reliabilityAvailable: false, reliabilityPct: null } as never)).toBeNull();
  });

  it("keeps markdown copy scenario-oriented and blocks margin signal without cost", async () => {
    getPreNivelacijaPrioritetiMock.mockResolvedValueOnce(
      makeResponse([
        makeCandidate({
          artikalId: 301,
          sku: "SKU-301",
          recommendation: {
            status: "review",
            label: "Pregled",
            summary: "Nedostaje trosak za sigurnu marznu procenu.",
            confidencePct: 58,
            reliabilityPct: 55,
            dataQualityStatus: "warning",
            reasonCodes: ["missing_cost"],
          },
        }),
      ]),
    );

    render(
      <MemoryRouter initialEntries={["/analytics/pre-nivelacija-prioriteti"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    expect(await screen.findByText("Procena povećanja prihoda")).toBeInTheDocument();
    expect(screen.getByText(/Verovatni markdown signal/i)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Detalji" }));

    expect(screen.getByText("Procenjena delta marže")).toBeInTheDocument();
    expect(screen.getByText("Nije dostupno bez troška")).toBeInTheDocument();
    expect(screen.getByText(/Maržni scenario nije dostupan bez pouzdanog troška/i)).toBeInTheDocument();
  });

  it("keeps sparse-sales candidates in additional-check mode even when other scores look strong", async () => {
    getPreNivelacijaPrioritetiMock.mockResolvedValueOnce(
      makeResponse([
        makeCandidate({
          artikalId: 401,
          sku: "SKU-401",
          recommendation: {
            status: "increase_focus",
            label: "Pojacaj",
            summary: "Signal deluje obecavajuce, ali uz slab uzorak prodaje.",
            confidencePct: 87,
            reliabilityPct: 82,
            dataQualityStatus: "good",
            reasonCodes: ["sparse_sales"],
          },
        }),
      ]),
    );

    render(
      <MemoryRouter initialEntries={["/analytics/pre-nivelacija-prioriteti"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    fireEvent.click(await screen.findByRole("button", { name: "Detalji" }));

    expect(screen.getByText("Potrebna je dodatna provera")).toBeInTheDocument();
    expect(screen.getByText(/Signal ima mali ili redak prodajni uzorak/i)).toBeInTheDocument();
  });

  it("shows unavailable reliability instead of a weak signal when the backend omits it", async () => {
    getPreNivelacijaPrioritetiMock.mockResolvedValueOnce(
      makeResponse([
        makeCandidate({
          artikalId: 501,
          sku: "SKU-501",
          reliabilityPct: null,
          recommendation: {
            status: "review",
            label: "Pregledaj",
            summary: "Reliability signal nije dostupan.",
            confidencePct: 61,
            reliabilityPct: null,
            dataQualityStatus: "warning",
            reasonCodes: ["missing_cost"],
          },
        }),
      ]),
    );

    render(
      <MemoryRouter initialEntries={["/analytics/pre-nivelacija-prioriteti"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    const unavailablePill = (await screen.findAllByText("Nije dostupno")).find((element) =>
      element.className.includes("signal-na"),
    );

    expect(unavailablePill).toBeTruthy();
    expect(unavailablePill).toHaveAttribute("title", expect.stringContaining("Pouzdanost nije dostupna"));
    expect(screen.queryByText("Nisko")).not.toBeInTheDocument();
  });

  it.each([
    ["false", false, 88],
    ["missing", undefined, 0],
  ])("keeps a %s recommendation status informative and gates its score", async (_label, recommendationAllowed, decisionScore) => {
    getPreNivelacijaPrioritetiMock.mockResolvedValueOnce(makeResponse([
      makeCandidate({
        decisionScore,
        recommendation: {
          status: "increase_focus",
          label: "Pojačaj fokus",
          summary: "Status dolazi iz backend signala.",
          confidencePct: 91,
          reliabilityPct: 88,
          dataQualityStatus: "good",
          reasonCodes: ["review_signal"],
          ...(recommendationAllowed === undefined ? {} : { recommendationAllowed }),
        },
      }),
    ]));

    render(
      <MemoryRouter initialEntries={["/analytics/pre-nivelacija-prioriteti"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    expect((await screen.findAllByText("SKU-101")).length).toBeGreaterThan(0);
    expect(screen.getByText("Pojacaj")).toBeInTheDocument();
    expect(screen.getByText("Preporuka je blokirana; proveri podatke pre odluke.")).toBeInTheDocument();
    expect(screen.getByTitle(/Preporuka je blokirana; status je informativan/i)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Detalji" }));

    expect(screen.getByText("Ocena preporuke")).toBeInTheDocument();
    expect(screen.getAllByText(/Pouzdanost nije dostupna/).length).toBeGreaterThan(0);
    expect(screen.queryByText("Pojačaj izlaganje i proveri dopunu pre nivelacije.")).not.toBeInTheDocument();
  });

  it.each([Number.NaN, Number.POSITIVE_INFINITY, Number.NEGATIVE_INFINITY])("fails closed for non-finite allowed decision score (%s)", async (decisionScore) => {
    getPreNivelacijaPrioritetiMock.mockResolvedValueOnce(makeResponse([
      makeCandidate({
        decisionScore,
        recommendation: {
          status: "review",
          label: "Pregled",
          summary: "Validan status, ali score nije dostupan.",
          confidencePct: 64,
          reliabilityPct: 61,
          dataQualityStatus: "good",
          recommendationAllowed: true,
          reasonCodes: ["review_signal"],
        },
      }),
    ]));

    render(
      <MemoryRouter initialEntries={["/analytics/pre-nivelacija-prioriteti"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    fireEvent.click(await screen.findByRole("button", { name: "Detalji" }));

    expect(screen.getByText("Ocena preporuke")).toBeInTheDocument();
    expect(screen.getAllByText(/Pouzdanost nije dostupna/).length).toBeGreaterThan(0);
  });

  it("keeps empty-state copy tied to the SKU priority filters, not a sales period", async () => {
    getPreNivelacijaPrioritetiMock.mockResolvedValueOnce(makeResponse([]));

    render(
      <MemoryRouter initialEntries={["/analytics/pre-nivelacija-prioriteti"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    expect(await screen.findByText("Nema kandidata za pre-nivelaciju.")).toBeInTheDocument();
    expect(screen.getByText("Nema kandidata koji ispunjavaju trenutne filtere za pre-nivelacioni prioritet.")).toBeInTheDocument();
    expect(screen.getByText("Promenite filtere dobavljača, sezone ili tipa obuće.")).toBeInTheDocument();
    expect(screen.getByText("Proverite kvalitet podataka.")).toBeInTheDocument();
    expect(screen.queryByText(/period/i)).not.toBeInTheDocument();
    expect(document.querySelector(".pnp-decision-kpis")).toBeNull();
  });

  it("shows an error alert and hides KPI cards when the priority load fails", async () => {
    getPreNivelacijaPrioritetiMock.mockRejectedValueOnce(new Error("Pre-nivelacija API timeout"));

    render(
      <MemoryRouter initialEntries={["/analytics/pre-nivelacija-prioriteti"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Pre-nivelacija API timeout");
    expect(document.querySelector(".pnp-decision-kpis")).toBeNull();
    expect(screen.queryByText("Nisko")).not.toBeInTheDocument();
  });

  it("restores validated filters, focus, page and scope from a shared URL", async () => {
    render(
      <MemoryRouter initialEntries={["/analitika/pre-nivelacija-prioriteti?supplierId=11&seasonId=7&footwearTypeId=4&minScore=72&noSaleDaysMin=21&focus=review&page=2&dataScope=imported"]}>
        <LocationProbe />
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    expect(await screen.findByTestId("pre-nivelacija-prioriteti-data-table")).toBeInTheDocument();
    expect(getPreNivelacijaPrioritetiMock).toHaveBeenCalledWith(expect.objectContaining({
      supplierId: 11,
      seasonId: 7,
      footwearTypeId: 4,
      minScore: 72,
      noSaleDaysMin: 21,
      page: 2,
      dataScope: "imported",
    }));
    expect(screen.getByLabelText("Dobavljač")).toHaveValue("11");
    expect(screen.getByLabelText("Sezona")).toHaveValue("7");
    expect(screen.getByLabelText("Tip obuće")).toHaveValue("4");
    expect(screen.getByLabelText("Min. skor")).toHaveValue(72);
    expect(screen.getByLabelText("Min. dana bez prodaje")).toHaveValue(21);
    expect(screen.getByRole("tab", { name: /Pregledaj/i })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByText("Strana 2")).toBeInTheDocument();
    expect(screen.getByTestId("location-search")).toHaveTextContent("supplierId=11");
    expect(screen.getByTestId("location-search")).toHaveTextContent("focus=review");
  });

  it("fails safely to defaults and canonicalizes invalid query values", async () => {
    render(
      <MemoryRouter initialEntries={["/analitika/pre-nivelacija-prioriteti?supplierId=bad&seasonId=-4&footwearTypeId=0&minScore=101&noSaleDaysMin=-1&focus=unknown&page=0&dataScope=unknown"]}>
        <LocationProbe />
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    expect(await screen.findByTestId("pre-nivelacija-prioriteti-data-table")).toBeInTheDocument();
    expect(getPreNivelacijaPrioritetiMock).toHaveBeenLastCalledWith(expect.objectContaining({
      supplierId: undefined,
      seasonId: undefined,
      footwearTypeId: undefined,
      minScore: 40,
      noSaleDaysMin: 14,
      page: 1,
      dataScope: "all",
    }));
    expect(screen.getByLabelText("Dobavljač")).toHaveValue("");
    expect(screen.getByLabelText("Min. skor")).toHaveValue(40);
    expect(screen.getByLabelText("Min. dana bez prodaje")).toHaveValue(14);
    expect(screen.getByRole("tab", { name: /Sve/i })).toHaveAttribute("aria-selected", "true");
    await waitFor(() => expect(screen.getByTestId("location-search")).toHaveTextContent("minScore=40"));
    expect(screen.getByTestId("location-search")).not.toHaveTextContent("focus=unknown");
    expect(screen.getByTestId("location-search")).not.toHaveTextContent("supplierId=bad");
  });

  it("keeps URL context and snapshot metadata through focus and detail navigation", async () => {
    render(
      <MemoryRouter initialEntries={["/analitika/pre-nivelacija-prioriteti?supplierId=11&seasonId=7&footwearTypeId=4&minScore=72&noSaleDaysMin=21&focus=review&page=1&dataScope=imported"]}>
        <LocationProbe />
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    fireEvent.click(await screen.findByRole("button", { name: "Detalji" }));
    fireEvent.click(screen.getByRole("button", { name: "Otvori puni detalj" }));

    expect(screen.getByTestId("location-search")).toHaveTextContent("/analitika/pre-nivelacija-prioriteti/102");
    expect(screen.getByTestId("location-search")).toHaveTextContent("supplierId=11");
    expect(screen.getByTestId("location-search")).toHaveTextContent("focus=review");
    expect(screen.getByTestId("location-search")).toHaveTextContent("dataScope=imported");

    const snapshot = JSON.parse(sessionStorage.getItem("analytics-detail:pre-nivelacija-prioriteti:102") ?? "null") as {
      metadata?: Array<{ key: string; value: string }>;
    } | null;
    expect(snapshot?.metadata).toEqual(expect.arrayContaining([
      expect.objectContaining({ key: "focus", value: "review" }),
      expect.objectContaining({ key: "minScore", value: "72" }),
    ]));
  });

  it("uses a direct URL scope and normalizes an invalid scope to all", async () => {
    const { unmount } = render(
      <MemoryRouter initialEntries={["/analytics/pre-nivelacija-prioriteti?dataScope=imported"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    await screen.findByTestId("pre-nivelacija-prioriteti-data-table");
    expect(getPreNivelacijaPrioritetiMock).toHaveBeenCalledWith(expect.objectContaining({ dataScope: "imported" }));

    unmount();
    getPreNivelacijaPrioritetiMock.mockClear();

    render(
      <MemoryRouter initialEntries={["/analytics/pre-nivelacija-prioriteti?dataScope=unknown"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    await screen.findByTestId("pre-nivelacija-prioriteti-data-table");
    expect(getPreNivelacijaPrioritetiMock).toHaveBeenCalledWith(expect.objectContaining({ dataScope: "all" }));
  });

  it("reloads exactly once when the global scope event changes the mounted page", async () => {
    render(
      <MemoryRouter initialEntries={["/analytics/pre-nivelacija-prioriteti"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    await screen.findByTestId("pre-nivelacija-prioriteti-data-table");
    expect(getPreNivelacijaPrioritetiMock).toHaveBeenCalledTimes(1);

    localStorage.setItem("trendplus:dataScope", "imported");
    window.dispatchEvent(new Event("trendplus:data-scope-changed"));

    await waitFor(() => expect(getPreNivelacijaPrioritetiMock).toHaveBeenCalledTimes(2));
    expect(getPreNivelacijaPrioritetiMock).toHaveBeenLastCalledWith(expect.objectContaining({ dataScope: "imported" }));
  });

  it("does not let the old-scope response replace the newer scope rows", async () => {
    let resolveAll: ((response: ReturnType<typeof makeResponse>) => void) | undefined;
    let resolveImported: ((response: ReturnType<typeof makeResponse>) => void) | undefined;
    getPreNivelacijaPrioritetiMock.mockImplementation(({ dataScope }: { dataScope?: string }) => {
      if (dataScope === "imported") {
        return new Promise((resolve) => { resolveImported = resolve; });
      }
      return new Promise((resolve) => { resolveAll = resolve; });
    });

    render(
      <MemoryRouter initialEntries={["/analytics/pre-nivelacija-prioriteti"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );
    await waitFor(() => expect(getPreNivelacijaPrioritetiMock).toHaveBeenCalledTimes(1));

    localStorage.setItem("trendplus:dataScope", "imported");
    window.dispatchEvent(new Event("trendplus:data-scope-changed"));
    await waitFor(() => expect(getPreNivelacijaPrioritetiMock).toHaveBeenCalledTimes(2));

    resolveImported?.(makeResponse([makeCandidate({ artikalId: 901, sku: "SKU-IMPORTED" })]));
    expect(await screen.findByText("SKU-IMPORTED")).toBeInTheDocument();

    resolveAll?.(makeResponse([makeCandidate({ artikalId: 902, sku: "SKU-OLD-SCOPE" })]));
    await waitFor(() => expect(screen.queryByText("SKU-OLD-SCOPE")).not.toBeInTheDocument());
  });
});

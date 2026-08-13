import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import PreNivelacijaPriorityPage from "../PreNivelacijaPriorityPage";

vi.mock("recharts", () => ({
  BarChart: ({ children }: { children?: ReactNode }) => <div>{children}</div>,
  ResponsiveContainer: ({ children }: { children?: ReactNode }) => <div>{children}</div>,
  CartesianGrid: () => <div />,
  XAxis: () => <div />,
  YAxis: () => <div />,
  Tooltip: () => <div />,
  Bar: () => <div />,
}));

vi.mock("../../components/analytics/AnalyticsTrustHeader", () => ({ default: () => null }));
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

describe("PreNivelacijaPriorityPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
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

    expect(await screen.findByText("Nema podataka za izabrane filtere.")).toBeInTheDocument();
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

  it("shows backend reliability as a percent instead of local Visoko/Srednje/Nisko bands", async () => {
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
});

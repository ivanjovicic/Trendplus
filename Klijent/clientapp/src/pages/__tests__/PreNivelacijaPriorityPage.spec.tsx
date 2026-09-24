import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter, useLocation, useNavigate } from "react-router-dom";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import PreNivelacijaPriorityPage from "../PreNivelacijaPriorityPage";
import { decisionColumns } from "../preNivelacijaDecision";
import { PreNivelacijaApiError } from "../../services/preNivelacijaApi";
import { AnalyticsResponseValidationError } from "../../validation/analyticsResponseValidation";

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
vi.mock("../../components/analytics/AnalyticsTableToolbar", () => ({
  default: ({ rows }: { rows: Array<{ sku: string }> }) => (
    <output data-testid="export-row-skus" data-skus={rows.map((row) => row.sku).join(",")} />
  ),
}));
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

vi.mock("../../services/preNivelacijaApi", async () => {
  const actual = await vi.importActual<typeof import("../../services/preNivelacijaApi")>("../../services/preNivelacijaApi");
  return {
    ...actual,
    getPreNivelacijaPrioriteti: (...args: unknown[]) => getPreNivelacijaPrioritetiMock(...args),
  };
});

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

function buildFilterFacetsFromCandidates(candidates: Array<ReturnType<typeof makeCandidate>>) {
  const seasons = new Map<number, string>();
  const footwearTypes = new Map<number, string>();

  candidates.forEach((candidate) => {
    if (candidate.seasonId != null && candidate.season && candidate.season !== "N/A") {
      seasons.set(candidate.seasonId, candidate.season);
    }
    if (candidate.footwearTypeId != null && candidate.footwearType && candidate.footwearType !== "N/A") {
      footwearTypes.set(candidate.footwearTypeId, candidate.footwearType);
    }
  });

  return {
    seasons: [...seasons.entries()].map(([id, label]) => ({ id, label })).sort((a, b) => a.label.localeCompare(b.label, "sr")),
    footwearTypes: [...footwearTypes.entries()].map(([id, label]) => ({ id, label })).sort((a, b) => a.label.localeCompare(b.label, "sr")),
  };
}

function buildSupplierActionShareFromLeaderboard(
  suppliers: Array<{
    supplierId?: number | null;
    supplierName: string;
    actionScore: number;
    weekOverWeekRiskDeltaPct?: number | null;
  }>,
) {
  const scored = [...suppliers]
    .filter((supplier) => supplier.actionScore > 0)
    .sort((left, right) => right.actionScore - left.actionScore);
  const totalActionScore = scored.reduce((sum, supplier) => sum + supplier.actionScore, 0);
  if (totalActionScore <= 0) {
    return {
      shareUnit: "percentage_points",
      weekOverWeekRiskDeltaUnit: "percentage_points",
      denominatorPolicy: "leaderboard_action_score_full_population_top_seven_plus_other",
      denominatorLabel: "Nema pozitivnog action score-a u leaderboard-u; udeo u akciji nije dostupan.",
      leaderboardSupplierCount: 0,
      visibleSupplierCount: 0,
      totalActionScore: 0,
      includedActionScore: 0,
      otherActionScore: 0,
      otherSharePct: null,
      segments: [],
    };
  }

  const visibleSuppliers = scored.slice(0, 7);
  const includedActionScore = visibleSuppliers.reduce((sum, supplier) => sum + supplier.actionScore, 0);
  const otherActionScore = totalActionScore - includedActionScore;
  const segments = visibleSuppliers.map((supplier) => ({
    supplierId: supplier.supplierId ?? null,
    supplierName: supplier.supplierName,
    actionSharePct: Number(((supplier.actionScore / totalActionScore) * 100).toFixed(2)),
    weekOverWeekRiskDeltaPct: supplier.weekOverWeekRiskDeltaPct ?? null,
    weekOverWeekRiskDeltaUnit: "percentage_points",
    isOther: false,
  }));

  let otherSharePct: number | null = null;
  if (otherActionScore > 0) {
    otherSharePct = Number(((otherActionScore / totalActionScore) * 100).toFixed(2));
    segments.push({
      supplierId: null,
      supplierName: "Ostali",
      actionSharePct: otherSharePct,
      weekOverWeekRiskDeltaPct: null,
      weekOverWeekRiskDeltaUnit: "percentage_points",
      isOther: true,
    });
  }

  return {
    shareUnit: "percentage_points",
    weekOverWeekRiskDeltaUnit: "percentage_points",
    denominatorPolicy: "leaderboard_action_score_full_population_top_seven_plus_other",
    denominatorLabel: "Udeo u akciji u odnosu na ukupan action score svih dobavljača u leaderboard-u; prikaz top 7 plus Ostali kada postoji preostali udeo.",
    leaderboardSupplierCount: scored.length,
    visibleSupplierCount: visibleSuppliers.length,
    totalActionScore,
    includedActionScore,
    otherActionScore,
    otherSharePct,
    segments,
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
  const supplierLeaderboard = [
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
  ];

  return {
    generatedAtUtc: "2026-06-19T10:00:00Z",
    formulaVersion: "1.0",
    formulaDescription: "Rule-based markdown scenario support.",
    summary: {
      supplierCount: 1,
      candidatesCount: candidates.length,
      highPriorityCount: candidates.filter((candidate) => candidate.priorityBand.toLowerCase() === "high").length,
      increaseFocusCount: candidates.filter((candidate) => candidate.recommendation.status === "increase_focus").length,
      maintainCount: candidates.filter((candidate) => candidate.recommendation.status === "maintain").length,
      reviewCount: candidates.filter((candidate) => candidate.recommendation.status === "review").length,
      doNotTrustCount: candidates.filter((candidate) => candidate.recommendation.status === "do_not_trust").length,
      insufficientDataCount: candidates.filter((candidate) => candidate.recommendation.status === "insufficient_data").length,
      totalStockAtRisk: 12,
      estimatedAvoidableMarkdownLoss: 12500,
      expectedHighlightRevenueUplift: 18000,
      averagePreNivelacijaScore: 74,
    },
    supplierLeaderboard,
    supplierActionShare: buildSupplierActionShareFromLeaderboard(supplierLeaderboard),
    candidates,
    filterFacets: buildFilterFacetsFromCandidates(candidates),
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

function HistoryControls() {
  const navigate = useNavigate();
  return (
    <div>
      <button type="button" onClick={() => navigate(-1)}>Go back</button>
      <button type="button" onClick={() => navigate(1)}>Go forward</button>
    </div>
  );
}

function buildPagedFocusResponse(query: { focus?: string; page?: number; pageSize?: number } = {}) {
  const allCandidates = [
    makeCandidate(),
    makeCandidate({
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
    }),
  ];
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? 60;
  const filteredCandidates = query.focus === "review"
    ? allCandidates.filter((candidate) => candidate.recommendation.status === "review")
    : query.focus === "highPriority"
      ? allCandidates.filter((candidate) => candidate.priorityBand.toLowerCase() === "high")
      : allCandidates;
  const pagedCandidates = filteredCandidates.slice((page - 1) * pageSize, page * pageSize);

  return {
    ...makeResponse(allCandidates),
    candidates: pagedCandidates,
    page,
    pageSize,
    totalCandidates: filteredCandidates.length,
  };
}

describe("PreNivelacijaPriorityPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    getPreNivelacijaPrioritetiMock.mockImplementation(async (query) => buildPagedFocusResponse(query));
  });

  it("keeps the high-priority band stable even when the recommendation is insufficient_data", async () => {
    render(
      <MemoryRouter initialEntries={["/analytics/pre-nivelacija-prioriteti"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    expect(await screen.findByRole("tab", { name: /Visok prioritet \(1\)/i })).toBeInTheDocument();
    expect(screen.queryByText(/SKU traži brzu proveru/i)).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("tab", { name: /Visok prioritet \(1\)/i }));

    expect((await screen.findAllByText("SKU-101")).length).toBeGreaterThan(0);
    expect(screen.queryByRole("heading", { name: "Nema rezultata za trenutne filtere." })).not.toBeInTheDocument();
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

  it("fails closed for malformed numeric rows and keeps valid evidence ahead when sorting", async () => {
    getPreNivelacijaPrioritetiMock.mockResolvedValueOnce(makeResponse([
      makeCandidate({
        artikalId: 701,
        sku: "SKU-701",
        preNivelacijaScore: "bad",
        stockUnits: "12",
        daysSinceLastSale: Number.POSITIVE_INFINITY,
        revenueDeltaHighlightVsMarkdown: Number.NaN,
        marginDeltaHighlightVsMarkdown: "bad",
        decisionScore: Number.NaN,
        reliabilityPct: "55",
        scoreBreakdown: {
          stockPressure: Number.NaN,
          velocityRisk: "bad",
          recencyRisk: null,
          markdownOpportunity: 40,
          marginPotential: 30,
          seasonRecencyBoost: Number.POSITIVE_INFINITY,
        },
        recommendation: {
          status: "review",
          label: "Pregled",
          summary: "Numerički signal nije pouzdan.",
          confidencePct: "bad",
          reliabilityPct: "55",
          dataQualityStatus: "warning",
          recommendationAllowed: true,
          reasonCodes: ["review_signal"],
        },
      }),
      makeCandidate({
        artikalId: 702,
        sku: "SKU-702",
        preNivelacijaScore: 90,
        recommendation: {
          status: "review",
          label: "Pregled",
          summary: "Validan signal.",
          confidencePct: 70,
          reliabilityPct: 80,
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

    const table = await screen.findByTestId("pre-nivelacija-prioriteti-data-table");
    expect(table).toHaveTextContent("Nije dostupno");
    expect(table).not.toHaveTextContent(/NaN|Infinity|undefined/);

    fireEvent.click(screen.getByRole("button", { name: /Skor/ }));
    const sortedRows = table.querySelectorAll("tbody tr");
    expect(sortedRows[0]).toHaveTextContent("SKU-702");

    const malformedRow = within(table).getByText("SKU-701").closest("tr");
    expect(malformedRow).not.toBeNull();
    fireEvent.click(within(malformedRow as HTMLElement).getByRole("button", { name: "Detalji" }));
    expect(screen.getByText("Detalj odluke: SKU-701")).toBeInTheDocument();
    expect(screen.getAllByText("Nije dostupno").length).toBeGreaterThan(0);
  });

  it("preserves measured zero and valid negative delta values", async () => {
    getPreNivelacijaPrioritetiMock.mockResolvedValueOnce(makeResponse([
      makeCandidate({
        preNivelacijaScore: 0,
        stockUnits: 0,
        daysSinceLastSale: 0,
        revenueDeltaHighlightVsMarkdown: -1500,
        marginDeltaHighlightVsMarkdown: -500,
        recommendation: {
          status: "review",
          label: "Pregled",
          summary: "Validan negativan scenario signal.",
          confidencePct: 70,
          reliabilityPct: 80,
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

    const table = await screen.findByTestId("pre-nivelacija-prioriteti-data-table");
    expect(table).toHaveTextContent("0,0");
    expect(table).toHaveTextContent("-1.500 RSD");
    expect(table).not.toHaveTextContent("Nije dostupno");
    const deltaCell = within(table).getByText("-1.500 RSD");
    expect(deltaCell).toHaveClass("trend-down");
    expect(deltaCell).not.toHaveClass("trend-up");
  });

  it("exports reliability as a percent while preserving null as unavailable", () => {
    const reliabilityColumn = decisionColumns.find((column) => column.key === "reliabilityPct");

    expect(reliabilityColumn).toMatchObject({
      dataType: "percent",
    });
    expect(reliabilityColumn?.getValue?.({ reliabilityAvailable: true, reliabilityPct: 0 } as never)).toBe(0);
    expect(reliabilityColumn?.getValue?.({ reliabilityAvailable: false, reliabilityPct: null } as never)).toBeNull();
  });

  it("exports recommendation-gated score and delta as unavailable while preserving measured zero", () => {
    const scoreColumn = decisionColumns.find((column) => column.key === "decisionScore");
    const deltaColumn = decisionColumns.find((column) => column.key === "revenueDelta");

    expect(scoreColumn?.getValue?.({ recommendationAllowed: false, decisionScoreAvailable: false, decisionScore: 88 } as never)).toBeNull();
    expect(deltaColumn?.getValue?.({ recommendationAllowed: false, revenueDelta: 7000 } as never)).toBeNull();
    expect(scoreColumn?.getValue?.({ recommendationAllowed: true, decisionScoreAvailable: true, decisionScore: 0 } as never)).toBe(0);
    expect(deltaColumn?.getValue?.({ recommendationAllowed: true, revenueDelta: 0 } as never)).toBe(0);
  });

  it("renders supplier action-share with an explicit Ostali bucket when an eighth supplier exists", async () => {
    const leaderboard = Array.from({ length: 8 }, (_, index) => ({
      supplierId: index + 1,
      supplierName: `Dobavljac ${index + 1}`,
      highPrioritySkuCount: 1,
      candidateSkuCount: 1,
      stockUnitsAtRisk: 1,
      estimatedAvoidableMarkdownLoss: 100,
      expectedHighlightRevenueUplift: 100,
      actionScore: index + 1,
      weekOverWeekRiskDeltaPct: 1,
    }));

    getPreNivelacijaPrioritetiMock.mockResolvedValueOnce({
      ...makeResponse(),
      supplierLeaderboard: leaderboard,
      supplierActionShare: buildSupplierActionShareFromLeaderboard(leaderboard),
    });

    render(
      <MemoryRouter initialEntries={["/analitika/pre-nivelacija-prioriteti"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    expect(await screen.findByText(/top 7 plus Ostali/i)).toBeInTheDocument();
    expect(buildSupplierActionShareFromLeaderboard(leaderboard).segments.some((segment) => segment.isOther)).toBe(true);
  });

  it("exports the same focus-filtered rows that the table displays", async () => {
    render(
      <MemoryRouter initialEntries={["/analitika/pre-nivelacija-prioriteti"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    await waitFor(() => expect(screen.getByTestId("export-row-skus")).toHaveAttribute("data-skus", "SKU-102,SKU-101"));

    fireEvent.click(screen.getByRole("tab", { name: /Pregledaj \(1\)/i }));

    await waitFor(() => {
      expect(getPreNivelacijaPrioritetiMock).toHaveBeenLastCalledWith(expect.objectContaining({ focus: "review", page: 1 }));
      expect(screen.getByTestId("export-row-skus")).toHaveAttribute("data-skus", "SKU-102");
    });
  });

  it("requests server-side focus filtering so matching candidates outside the current page are still visible", async () => {
    getPreNivelacijaPrioritetiMock.mockImplementation(async (query) => buildPagedFocusResponse({ ...query, pageSize: 1 }));

    render(
      <MemoryRouter initialEntries={["/analitika/pre-nivelacija-prioriteti?page=2"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    await waitFor(() => expect(getPreNivelacijaPrioritetiMock).toHaveBeenCalledWith(expect.objectContaining({ page: 2, pageSize: 60 })));

    fireEvent.click(screen.getByRole("tab", { name: /Pregledaj \(1\)/i }));

    await waitFor(() => {
      expect(getPreNivelacijaPrioritetiMock).toHaveBeenLastCalledWith(expect.objectContaining({ focus: "review", page: 1, pageSize: 60 }));
      expect(screen.getByTestId("export-row-skus")).toHaveAttribute("data-skus", "SKU-102");
    });
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
    const status = screen.getByTitle(/Preporuka je blokirana; status je informativan/i);
    expect(status).toBeInTheDocument();
    expect(status.getAttribute("title")).not.toMatch(/7\.000/);

    const table = screen.getByTestId("pre-nivelacija-prioriteti-data-table");
    const vendorRow = within(table).getByText("SKU-101").closest("tr");
    expect(vendorRow).not.toBeNull();
    expect(within(vendorRow as HTMLElement).queryByText("7.000 RSD")).not.toBeInTheDocument();
    const deltaCell = (vendorRow as HTMLElement).querySelectorAll("td")[5];
    expect(deltaCell.className).not.toMatch(/trend-up|trend-down/);

    fireEvent.click(screen.getByRole("button", { name: "Detalji" }));

    const scoreArticle = screen.getByText("Ocena preporuke").closest("article");
    expect(scoreArticle).toHaveTextContent(/Pouzdanost nije dostupna/);
    expect(scoreArticle).not.toHaveTextContent("88");
    expect(screen.getAllByText(/Pouzdanost nije dostupna/).length).toBeGreaterThan(0);
    expect(screen.queryByText("Pojačaj izlaganje i proveri dopunu pre nivelacije.")).not.toBeInTheDocument();
    expect(screen.queryByText("Signal je upotrebljiv za odluku")).not.toBeInTheDocument();
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

  it("shows safe guidance and hides KPI cards when the priority load fails", async () => {
    getPreNivelacijaPrioritetiMock.mockRejectedValueOnce(
      new PreNivelacijaApiError(
        "Servis je privremeno nedostupan.",
        "PRE_NIVELACIJA_BACKEND_TIMEOUT",
        "corr-pnp-295",
      ),
    );

    render(
      <MemoryRouter initialEntries={["/analytics/pre-nivelacija-prioriteti"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Servis je privremeno nedostupan.");
    expect(alert).not.toHaveTextContent("PRE_NIVELACIJA_BACKEND_TIMEOUT");
    expect(document.querySelector(".pnp-decision-kpis")).toBeNull();
    expect(screen.queryByText("Nisko")).not.toBeInTheDocument();
  });

  it("does not expose raw technical errors from unexpected fetch failures", async () => {
    getPreNivelacijaPrioritetiMock.mockRejectedValueOnce(new Error("timeout ECONNREFUSED"));

    render(
      <MemoryRouter initialEntries={["/analytics/pre-nivelacija-prioriteti"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Pre-nivelacija prioriteti trenutno nisu dostupni. Proverite status osvežavanja i pokušajte ponovo.");
    expect(alert).not.toHaveTextContent("ECONNREFUSED");
  });

  it("shows controlled guidance when the decision payload fails runtime validation", async () => {
    getPreNivelacijaPrioritetiMock.mockRejectedValueOnce(
      new AnalyticsResponseValidationError("Pre-nivelacija prioriteti", ["queues.highlightNow.sku"]),
    );

    render(
      <MemoryRouter initialEntries={["/analytics/pre-nivelacija-prioriteti"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Pre-nivelacija prioriteti trenutno nisu dostupni. Proverite status osvežavanja i pokušajte ponovo.");
    expect(alert).not.toHaveTextContent("queues.highlightNow.sku");
    expect(document.querySelector(".pnp-decision-kpis")).toBeNull();
  });

  it("restores validated filters, focus, page and scope from a shared URL", async () => {
    render(
      <MemoryRouter initialEntries={["/analitika/pre-nivelacija-prioriteti?supplierId=11&seasonId=7&footwearTypeId=4&minScore=72&noSaleDaysMin=21&focus=review&page=1&dataScope=imported"]}>
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
      focus: "review",
      page: 1,
      dataScope: "imported",
    }));
    expect(screen.getByLabelText("Dobavljač")).toHaveValue("11");
    expect(screen.getByLabelText("Sezona")).toHaveValue("7");
    expect(screen.getByLabelText("Tip obuće")).toHaveValue("4");
    expect(screen.getByLabelText("Min. skor")).toHaveValue(72);
    expect(screen.getByLabelText("Min. dana bez prodaje")).toHaveValue(21);
    expect(screen.getByRole("tab", { name: /Pregledaj/i })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByText("Strana 1")).toBeInTheDocument();
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

  it("resets filters, focus and URL to documented defaults", async () => {
    render(
      <MemoryRouter initialEntries={["/analitika/pre-nivelacija-prioriteti?supplierId=11&seasonId=7&footwearTypeId=4&minScore=72&noSaleDaysMin=21&focus=review&page=2&dataScope=imported"]}>
        <LocationProbe />
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    expect(await screen.findByLabelText("Dobavljač")).toHaveValue("11");
    fireEvent.click(screen.getByRole("button", { name: "Reset filtera" }));

    await waitFor(() => expect(screen.getByLabelText("Dobavljač")).toHaveValue(""));
    expect(screen.getByLabelText("Min. skor")).toHaveValue(40);
    expect(screen.getByLabelText("Min. dana bez prodaje")).toHaveValue(14);
    expect(screen.getByRole("tab", { name: /Sve/i })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByTestId("location-search")).toHaveTextContent("minScore=40");
    expect(screen.getByTestId("location-search")).not.toHaveTextContent("supplierId=11");
    expect(screen.getByTestId("location-search")).not.toHaveTextContent("focus=review");
    expect(getPreNivelacijaPrioritetiMock).toHaveBeenLastCalledWith(expect.objectContaining({
      supplierId: undefined,
      minScore: 40,
      noSaleDaysMin: 14,
      page: 1,
      dataScope: "imported",
    }));
  });

  it("restores focus through browser back and forward history", async () => {
    render(
      <MemoryRouter
        initialEntries={[
          "/analitika/pre-nivelacija-prioriteti?minScore=40&noSaleDaysMin=14&dataScope=all",
          "/analitika/pre-nivelacija-prioriteti?minScore=40&noSaleDaysMin=14&focus=review&dataScope=all",
        ]}
        initialIndex={1}
      >
        <HistoryControls />
        <LocationProbe />
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    expect(await screen.findByRole("tab", { name: /Pregledaj/i })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByTestId("location-search")).toHaveTextContent("focus=review");

    fireEvent.click(screen.getByRole("button", { name: "Go back" }));
    await waitFor(() => expect(screen.getByRole("tab", { name: /Sve/i })).toHaveAttribute("aria-selected", "true"));
    expect(screen.getByTestId("location-search")).not.toHaveTextContent("focus=review");

    fireEvent.click(screen.getByRole("button", { name: "Go forward" }));
    await waitFor(() => expect(screen.getByRole("tab", { name: /Pregledaj/i })).toHaveAttribute("aria-selected", "true"));
    expect(screen.getByTestId("location-search")).toHaveTextContent("focus=review");
  });

  it("includes season and footwear filter options from full universe while viewing page 1", async () => {
    const pageOneCandidate = makeCandidate({
      artikalId: 101,
      sku: "SKU-101",
      seasonId: 7,
      season: "Prolece/Leto",
      footwearTypeId: 4,
      footwearType: "Sneaker",
    });
    const pageTwoCandidate = makeCandidate({
      artikalId: 202,
      sku: "SKU-202",
      supplierId: 22,
      supplierName: "Dobavljac B",
      seasonId: 8,
      season: "Jesen/Zima",
      footwearTypeId: 5,
      footwearType: "Boot",
    });

    getPreNivelacijaPrioritetiMock.mockResolvedValueOnce({
      ...makeResponse([pageOneCandidate]),
      page: 1,
      pageSize: 1,
      totalCandidates: 2,
      filterFacets: buildFilterFacetsFromCandidates([pageOneCandidate, pageTwoCandidate]),
    });

    render(
      <MemoryRouter initialEntries={["/analitika/pre-nivelacija-prioriteti"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    const seasonSelect = await screen.findByLabelText("Sezona");
    expect(within(seasonSelect).getByRole("option", { name: "Jesen/Zima" })).toBeInTheDocument();
    expect(within(seasonSelect).getByRole("option", { name: "Prolece/Leto" })).toBeInTheDocument();

    const footwearSelect = screen.getByLabelText("Tip obuće");
    expect(within(footwearSelect).getByRole("option", { name: "Boot" })).toBeInTheDocument();
    expect(within(footwearSelect).getByRole("option", { name: "Sneaker" })).toBeInTheDocument();
  });

  it("keeps expanded detail visible across pagination when the same artikal remains in results", async () => {
    getPreNivelacijaPrioritetiMock
      .mockResolvedValueOnce({
        ...makeResponse([
          makeCandidate({ artikalId: 101, sku: "SKU-101" }),
          makeCandidate({ artikalId: 102, sku: "SKU-102" }),
        ]),
        page: 1,
        pageSize: 1,
        totalCandidates: 2,
      })
      .mockResolvedValueOnce({
        ...makeResponse([makeCandidate({ artikalId: 101, sku: "SKU-101" })]),
        page: 2,
        pageSize: 1,
        totalCandidates: 2,
      });

    render(
      <MemoryRouter initialEntries={["/analitika/pre-nivelacija-prioriteti"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    const table = await screen.findByTestId("pre-nivelacija-prioriteti-data-table");
    const firstRow = within(table).getByText("SKU-101").closest("tr");
    fireEvent.click(within(firstRow as HTMLElement).getByRole("button", { name: "Detalji" }));
    expect(screen.getByText("Detalj odluke: SKU-101")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Sledeća" }));

    await waitFor(() => expect(getPreNivelacijaPrioritetiMock).toHaveBeenLastCalledWith(expect.objectContaining({ page: 2 })));
    expect(await screen.findByText("Detalj odluke: SKU-101")).toBeInTheDocument();
  });

  it("keeps global focus tab counts separate from the visible page slice", async () => {
    const pageOneCandidate = makeCandidate({ artikalId: 301, sku: "SKU-PAGE-1" });
    const pageTwoCandidate = makeCandidate({ artikalId: 302, sku: "SKU-PAGE-2" });
    const allCandidates = [pageOneCandidate, pageTwoCandidate];
    const globalSummary = {
      increaseFocusCount: 0,
      maintainCount: 0,
      reviewCount: 0,
      doNotTrustCount: 0,
      insufficientDataCount: 2,
      highPriorityCount: 2,
      candidatesCount: 2,
    };

    getPreNivelacijaPrioritetiMock.mockImplementation(async (query) => {
      const pageSize = query.pageSize ?? 60;
      const page = query.page ?? 1;
      const pagedCandidates = allCandidates.slice((page - 1) * pageSize, page * pageSize);
      return {
        ...makeResponse(allCandidates),
        summary: { ...makeResponse(allCandidates).summary, ...globalSummary },
        candidates: pagedCandidates,
        page,
        pageSize,
        totalCandidates: allCandidates.length,
      };
    });

    render(
      <MemoryRouter initialEntries={["/analitika/pre-nivelacija-prioriteti"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    const firstKpi = await screen.findByText("Visok prioritet");
    expect(firstKpi.parentElement).toHaveTextContent("2");
    expect(screen.getByRole("tab", { name: /Visok prioritet \(2\)/i })).toBeInTheDocument();
    expect(screen.getByText(/Vidljiva strana:/i)).toHaveTextContent("Visok prioritet: 2");
  });

  it("hides inline detail when the active focus filter excludes the selected row", async () => {
    render(
      <MemoryRouter initialEntries={["/analitika/pre-nivelacija-prioriteti"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    const table = await screen.findByTestId("pre-nivelacija-prioriteti-data-table");
    const reviewRow = within(table).getByText("SKU-102").closest("tr");
    fireEvent.click(within(reviewRow as HTMLElement).getByRole("button", { name: "Detalji" }));
    expect(screen.getByText("Detalj odluke: SKU-102")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("tab", { name: /Pojacaj/i }));
    expect(screen.queryByText("Detalj odluke: SKU-102")).not.toBeInTheDocument();
  });

  it("does not let a blocked recommendation outrank allowed delta evidence", async () => {
    getPreNivelacijaPrioritetiMock.mockResolvedValueOnce(makeResponse([
      makeCandidate({
        artikalId: 801,
        sku: "SKU-BLOCKED",
        revenueDeltaHighlightVsMarkdown: 90000,
        recommendation: {
          status: "increase_focus",
          label: "Pojačaj fokus",
          summary: "Blokiran signal.",
          confidencePct: 91,
          reliabilityPct: 88,
          dataQualityStatus: "good",
          recommendationAllowed: false,
          reasonCodes: ["review_signal"],
        },
      }),
      makeCandidate({
        artikalId: 802,
        sku: "SKU-ALLOWED",
        revenueDeltaHighlightVsMarkdown: 1000,
        recommendation: {
          status: "review",
          label: "Pregled",
          summary: "Dozvoljen signal.",
          confidencePct: 70,
          reliabilityPct: 80,
          dataQualityStatus: "good",
          recommendationAllowed: true,
          reasonCodes: ["review_signal"],
        },
      }),
    ]));

    render(
      <MemoryRouter initialEntries={["/analitika/pre-nivelacija-prioriteti"]}>
        <PreNivelacijaPriorityPage />
      </MemoryRouter>,
    );

    const table = await screen.findByTestId("pre-nivelacija-prioriteti-data-table");
    fireEvent.click(screen.getByRole("button", { name: /Isticanje vs sniženje/ }));
    const sortedRows = table.querySelectorAll("tbody tr");
    expect(sortedRows[0]).toHaveTextContent("SKU-ALLOWED");
    expect(sortedRows[1]).toHaveTextContent("SKU-BLOCKED");
  });
});

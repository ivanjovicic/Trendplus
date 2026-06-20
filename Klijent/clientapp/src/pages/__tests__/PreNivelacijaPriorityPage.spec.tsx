import { render, screen, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import type { ReactNode } from "react";
import { describe, expect, it, vi, beforeEach } from "vitest";
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
vi.mock("../../components/analytics/AnalyticsErrorState", () => ({ default: () => null }));
vi.mock("../../components/analytics/AnalyticsEmptyState", () => ({ default: () => null }));
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
    supplierName: "Dobavljač A",
    category: "Patike",
    footwearType: "Sneaker",
    season: "Proleće/Leto",
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

function makeResponse() {
  return {
    generatedAtUtc: "2026-06-19T10:00:00Z",
    formulaVersion: "1.0",
    formulaDescription: "Rule-based markdown scenario support.",
    summary: {
      supplierCount: 1,
      candidatesCount: 2,
      highPriorityCount: 1,
      totalStockAtRisk: 12,
      estimatedAvoidableMarkdownLoss: 12500,
      expectedHighlightRevenueUplift: 18000,
      averagePreNivelacijaScore: 74,
    },
    supplierLeaderboard: [
      {
        supplierId: 11,
        supplierName: "Dobavljač A",
        highPrioritySkuCount: 1,
        candidateSkuCount: 2,
        stockUnitsAtRisk: 12,
        estimatedAvoidableMarkdownLoss: 12500,
        expectedHighlightRevenueUplift: 18000,
        actionScore: 92,
        weekOverWeekRiskDeltaPct: 4,
      },
    ],
    candidates: [
      makeCandidate(),
      makeCandidate({
        artikalId: 102,
        sku: "SKU-102",
        supplierName: "Dobavljač B",
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
          summary: "Signal traži ručnu proveru.",
          confidencePct: 64,
          reliabilityPct: 61,
          dataQualityStatus: "warning",
          reasonCodes: ["sparse_sales"],
        },
      }),
    ],
    queues: {
      highlightNow: [
        {
          artikalId: 101,
          sku: "SKU-101",
          supplierName: "Dobavljač A",
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
    totalCandidates: 2,
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
      </MemoryRouter>
    );

    expect(await screen.findByRole("tab", { name: /Visok prioritet \(0\)/i })).toBeInTheDocument();
    expect(screen.queryByText(/SKU traži brzu proveru/i)).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("tab", { name: /Visok prioritet \(0\)/i }));

    expect(await screen.findByText("Nema podataka za izabrane filtere.")).toBeInTheDocument();
  });
});

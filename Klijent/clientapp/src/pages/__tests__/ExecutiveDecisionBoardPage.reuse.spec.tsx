import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import ExecutiveDecisionBoardPage from "../ExecutiveDecisionBoardPage";
import type { DecisionBoardAggregateResponse } from "../../types/analytics";

const getDecisionBoardAggregateMock = vi.fn();

vi.mock("../../services/analyticsApi", () => ({
  getDecisionBoardAggregate: (...args: unknown[]) => getDecisionBoardAggregateMock(...args),
}));

vi.mock("../../components/analytics/AnalyticsTrustHeader", () => ({ default: () => null }));
vi.mock("../../components/analytics/AnalyticsRefreshStatusBanner", () => ({ default: () => null }));
vi.mock("../../components/analytics/AnalyticsEmptyState", () => ({ default: () => null }));
vi.mock("../../components/analytics/AnalyticsErrorState", () => ({ default: () => null }));

function buildAggregate(): DecisionBoardAggregateResponse {
  return {
    generatedAtUtc: "2026-08-14T08:30:00Z",
    periodFromUtc: "2026-08-01T00:00:00Z",
    periodToUtc: "2026-08-14T00:00:00Z",
    lastRefreshAtUtc: "2026-08-14T08:15:00Z",
    overallDataQualityStatus: "good",
    recommendationNote: "Board prikazuje backend-led evidence.",
    warnings: [],
    metrics: [],
    sourceStates: [],
    sections: [
      {
        key: "urgent",
        title: "Urgent",
        description: "Urgent decisions",
        sourceLink: "/analytics/products",
        emptyMessage: "No data",
        warnings: [],
        cards: [
          {
            id: "product:1",
            kind: "product",
            sectionKey: "urgent",
            sourceModule: "Odluke o proizvodima",
            sourceType: "product",
            sourceKey: "product:1",
            title: "Patike X",
            summary: "Signal dolazi iz backend aggregate-a.",
            confidenceLevel: "high",
            confidenceScore: 82,
            reliabilityPct: 90,
            expectedImpactRsd: 120000,
            measuredImpactRsd: null,
            realizationRatio: null,
            riskIfIgnored: "Signal ostaje neiskorišćen.",
            recommendedNextAction: "Dopuni odmah.",
            actionHref: "/analytics/products",
            alreadyInAction: false,
            alreadyClosed: false,
            warningCodes: ["missing_cost"],
            reasonCodes: ["replenish_needed"],
            recommendationAllowed: null,
            dataQualityStatus: "good",
            generatedAtUtc: "2026-08-14T08:25:00Z",
            priorityScore: 240,
            impactScore: 120000,
            confidenceSource: "signal",
          },
        ],
      },
    ],
    meta: {
      success: true,
      dataQualityStatus: "good",
    },
  };
}

describe("ExecutiveDecisionBoardPage reuse runtime", () => {
  it("renders warning and reason codes as separate backend evidence groups", async () => {
    getDecisionBoardAggregateMock.mockResolvedValueOnce(buildAggregate());

    render(
      <MemoryRouter initialEntries={["/analytics/decision-board"]}>
        <ExecutiveDecisionBoardPage />
      </MemoryRouter>,
    );

    expect(await screen.findByText("Upozorenja")).toBeInTheDocument();
    expect(screen.getByText("Razlozi")).toBeInTheDocument();
    expect(screen.getByText("Nedostaje nabavna cena")).toBeInTheDocument();
    expect(screen.getByText("replenish needed")).toBeInTheDocument();
  });
});

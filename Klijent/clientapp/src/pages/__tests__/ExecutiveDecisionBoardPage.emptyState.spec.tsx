import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import ExecutiveDecisionBoardPage from "../ExecutiveDecisionBoardPage";

const getDecisionBoardAggregateMock = vi.fn();

vi.mock("../../services/analyticsApi", () => ({
  getDecisionBoardAggregate: (...args: unknown[]) => getDecisionBoardAggregateMock(...args),
}));

vi.mock("../../components/analytics/AnalyticsTrustHeader", () => ({ default: () => null }));
vi.mock("../../components/analytics/AnalyticsRefreshStatusBanner", () => ({ default: () => null }));
vi.mock("../../components/analytics/AnalyticsEmptyState", () => ({
  default: ({ title, message }: { title: string; message: string }) => (
    <div data-testid="analytics-empty-state">
      <strong>{title}</strong>
      <span>{message}</span>
    </div>
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

describe("ExecutiveDecisionBoardPage empty and error chrome", () => {
  it("renders shared empty state without summary chrome when the aggregate has no signals", async () => {
    getDecisionBoardAggregateMock.mockResolvedValueOnce({
      generatedAtUtc: "2026-08-14T08:30:00Z",
      overallDataQualityStatus: "insufficient_data",
      recommendationNote: "Nema signala.",
      warnings: [],
      metrics: [],
      sourceStates: [],
      sections: [],
      meta: {
        success: true,
        emptyReason: "no_signals",
        dataQualityStatus: "insufficient_data",
      },
    });

    render(<ExecutiveDecisionBoardPage />);

    expect(await screen.findByTestId("analytics-empty-state")).toHaveTextContent("Nema dovoljno signala za izvršni board");
    expect(document.querySelector(".decision-board-summary-grid")).toBeNull();
    expect(document.querySelector(".decision-board-sections")).toBeNull();
  });

  it("renders shared error state without summary chrome when the aggregate load fails", async () => {
    getDecisionBoardAggregateMock.mockRejectedValueOnce(new Error("Board API down"));

    render(<ExecutiveDecisionBoardPage />);

    expect(await screen.findByRole("alert")).toHaveTextContent("Izvršni board trenutno nije dostupan");
    expect(screen.getByText("Board API down")).toBeInTheDocument();
    expect(document.querySelector(".decision-board-summary-grid")).toBeNull();
    expect(document.querySelector(".decision-board-sections")).toBeNull();
  });
});

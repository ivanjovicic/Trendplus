import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { rest } from "msw";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { server } from "../../mocks/server";
import * as analyticsApi from "../../services/analyticsApi";
import AnalyticsDashboard from "../AnalyticsDashboard";

vi.mock("../../components/analytics/AnalyticsDashboardCharts", () => ({
  default: () => <div data-testid="charts-stub" />,
}));

describe("AnalyticsDashboard operational fallback", () => {
  beforeEach(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (globalThis as any).AbortSignal = window.AbortSignal;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (globalThis as any).AbortController = window.AbortController;

    vi.spyOn(analyticsApi, "getDashboardBootstrap").mockResolvedValue({
      summary: {
        totalRevenue: 12345,
        totalTransactions: 12,
        totalUnits: 8,
      },
      inventory: {
        totalSkuCount: 100,
        totalOnHand: 40,
        outOfStockCount: 5,
        lowStockCount: 10,
        usedOperationalFallback: true,
      },
      dailySales: [],
      categoryData: [],
      genderData: [],
      supplierData: [],
      supplierOptions: [],
      weekdayData: [],
      hourData: [],
      paymentData: [],
      quickInsights: null,
      transactionStats: null,
      advanced: null,
      topAdvanced: null,
      validationCompleteness: null,
      validationFreshness: null,
      validationLostSales: null,
      executive: null,
      decisionActions: [],
      errors: [],
      meta: {
        success: true,
        isPartial: true,
        warningCode: "inventory_status_operational_fallback",
        warningMessage:
          "Status zaliha je učitan iz operativne tabele Artikli jer analytics relacija nije dostupna.",
        dataQualityStatus: "warning",
      },
    });

    server.use(
      rest.get("/api/analytics/cached/filters/stores", (_req, res, ctx) =>
        res(ctx.status(200), ctx.json([])),
      ),
      rest.get("/api/analytics/refresh-status", (_req, res, ctx) =>
        res(
          ctx.status(200),
          ctx.json({
            isRunning: false,
            currentStep: null,
            dataFreshnessStatus: "good",
            lastSuccessfulRefreshAtUtc: "2026-08-05T10:00:00Z",
            jobs: [],
          }),
        ),
      ),
    );
  });

  it("shows inventory Artikli fallback as a warning, not a trusted healthy count", async () => {
    render(
      <MemoryRouter>
        <AnalyticsDashboard />
      </MemoryRouter>,
    );

    const warnings = await screen.findAllByText(/Status zaliha je učitan iz operativne tabele Artikli/i);
    expect(warnings.length).toBeGreaterThan(0);
    expect(screen.getByText("Postoje upozorenja")).toBeInTheDocument();
    expect(screen.queryByText("Nema kritičnih count signala u ovom preseku.")).not.toBeInTheDocument();
  });
});

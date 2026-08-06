import React from "react";
import { render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { rest } from "msw";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { server } from "../../mocks/server";
import AnalyticsDashboard from "../AnalyticsDashboard";

vi.mock("../../components/analytics/AnalyticsDashboardCharts", () => ({
  default: () => <div data-testid="charts-stub" />,
}));

describe("AnalyticsDashboard control bar", () => {
  beforeEach(() => {
    // Align fetch signal checks with the browser AbortSignal used by app hooks.
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (globalThis as any).AbortSignal = window.AbortSignal;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (globalThis as any).AbortController = window.AbortController;

    server.use(
      rest.get("/api/analytics/cached/dashboard/bootstrap", (_req, res, ctx) =>
        res(
          ctx.status(200),
          ctx.json({
            summary: {
              totalRevenue: 12345,
              totalTransactions: 12,
              totalUnits: 8,
            },
            inventory: { totalSkuCount: 100, outOfStockCount: 5, lowStockCount: 10 },
            dailySales: [],
            categoryData: [],
            genderData: [],
            supplierData: [],
            supplierOptions: [{ supplierId: 77, supplierName: "Alfa Shoes" }],
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
            meta: { success: true, dataQualityStatus: "good" },
          }),
        ),
      ),
      rest.get("/api/analytics/cached/filters/stores", (_req, res, ctx) =>
        res(
          ctx.status(200),
          ctx.json([{ storeId: 5, storeName: "Delta" }]),
        ),
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
      rest.get("/api/analytics/health", (_req, res, ctx) =>
        res(
          ctx.status(200),
          ctx.json({
            status: "ok",
            tables: { salesFacts: 10, salesLineFacts: 20, productsDim: 5 },
            message: "ok",
          }),
        ),
      ),
    );
  });

  it("renders the shared premium control bar with dashboard filters", async () => {
    render(
      <MemoryRouter>
        <AnalyticsDashboard />
      </MemoryRouter>,
    );

    const controlBar = await screen.findByTestId("analytics-control-bar");

    expect(controlBar).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Opseg i filteri" })).toBeInTheDocument();
    expect(within(controlBar).getByLabelText("Period")).toBeInTheDocument();
    expect(within(controlBar).getByLabelText("Prodavnica")).toBeInTheDocument();
    expect(within(controlBar).getByLabelText(/Dobavlja/i)).toBeInTheDocument();
    expect(within(controlBar).getByRole("button", { name: /dashboard/i })).toBeInTheDocument();
    expect(within(controlBar).getByRole("link", { name: "Kvalitet podataka" })).toHaveAttribute(
      "href",
      "/analytics/data-quality",
    );
  });
});

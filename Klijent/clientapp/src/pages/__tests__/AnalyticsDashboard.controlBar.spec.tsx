import React from "react";
import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { rest } from "msw";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { server } from "../../mocks/server";
import * as analyticsApi from "../../services/analyticsApi";
import AnalyticsDashboard from "../AnalyticsDashboard";

vi.mock("../../components/analytics/AnalyticsDashboardCharts", () => ({
  default: () => <div data-testid="charts-stub" />,
}));

function buildBootstrapResponse(actionTitle: string) {
  return {
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
    executive: {
      dataQualitySummary: {
        missingSupplierCount: 0,
        missingCostCount: 0,
        insufficientSignalCount: 0,
        ignoredRowsCount: 0,
        zeroRevenueRowsCount: 0,
        freshnessStatus: "good",
      },
      inventoryDangerValueRsd: 0,
      totalMarginContributionRsd: 0,
      topMarginCategories: [],
      topMarginProducts: [],
      topSuppliers: [],
      negativeSignals: [],
    },
    decisionActions: [
      {
        sourceType: "dashboard",
        priority: "P1",
        title: actionTitle,
        description: actionTitle,
        reason: actionTitle,
        statusReason: "Signal ready.",
        recommendationStatus: "READY",
        expectedImpact: "1 RSD",
        impactEstimateRsd: 1000,
        confidencePct: 0.9,
        reliabilityPct: 0.8,
        recommendationAllowed: true,
        dataQualityStatus: "good",
        actionUrl: "/analytics/inventory",
        metadata: {},
        link: "/analytics/inventory",
        linkLabel: "Otvori inventar",
      },
    ],
    errors: [],
    meta: { success: true, dataQualityStatus: "good" },
  } as never;
}

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

  it("renders the executive command center with trust and risk highlights", async () => {
    vi.spyOn(analyticsApi, "getDashboardBootstrap").mockResolvedValue({
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
      executive: {
        dataQualitySummary: {
          missingSupplierCount: 2,
          missingCostCount: 1,
          insufficientSignalCount: 1,
          ignoredRowsCount: 0,
          zeroRevenueRowsCount: 0,
          freshnessStatus: "warning",
        },
        inventoryDangerValueRsd: 1234,
        totalMarginContributionRsd: 4567,
        topMarginCategories: [],
        topMarginProducts: [],
        topSuppliers: [],
        negativeSignals: [],
      },
      decisionActions: [
        {
          sourceType: "dashboard",
          priority: "P1",
          title: "Otvori inventar",
          description: "Prioritetna operativna akcija.",
          reason: "Rizična zaliha zahteva pregled.",
          statusReason: "Signal je validiran.",
          recommendationStatus: "READY",
          expectedImpact: "12.000 RSD",
          impactEstimateRsd: 12000,
          confidencePct: 0.92,
          reliabilityPct: 0.88,
          recommendationAllowed: true,
          dataQualityStatus: "good",
          actionUrl: "/analytics/inventory",
          metadata: {},
          link: "/analytics/inventory",
          linkLabel: "Otvori inventar",
        },
      ],
      errors: [],
      meta: { success: true, dataQualityStatus: "good" },
    } as never);
    vi.spyOn(analyticsApi, "getStores").mockResolvedValue([{ storeId: 5, storeName: "Delta" }] as never);
    vi.spyOn(analyticsApi, "getAnalyticsRefreshStatus").mockResolvedValue({
      isRunning: false,
      currentStep: null,
      dataFreshnessStatus: "good",
      lastSuccessfulRefreshAtUtc: "2026-08-05T10:00:00Z",
      jobs: [],
    } as never);
    vi.spyOn(analyticsApi, "checkAnalyticsHealth").mockResolvedValue({
      status: "ok",
      tables: { salesFacts: 10, salesLineFacts: 20, productsDim: 5 },
      message: "ok",
    } as never);

    render(
      <MemoryRouter>
        <AnalyticsDashboard />
      </MemoryRouter>,
    );

    await screen.findByRole("heading", { name: "U 30 sekundi: prodaja, marža, rizici i prioriteti" });
    const commandCenter = screen.getByTestId("analytics-command-center");

    expect(within(commandCenter).getByRole("heading", { name: "U 30 sekundi: prodaja, marža, rizici i prioriteti" })).toBeInTheDocument();
    expect(within(commandCenter).getByRole("heading", { name: "Ključni KPI strip" })).toBeInTheDocument();
    expect(within(commandCenter).getByRole("heading", { name: "Šta treba uraditi ove nedelje?" })).toBeInTheDocument();
    expect(within(commandCenter).getByRole("link", { name: "Centralne akcije" })).toHaveAttribute(
      "href",
      "/analytics/actions",
    );
    expect(within(commandCenter).getByRole("heading", { name: "Kvalitet podataka i svežina" })).toBeInTheDocument();
    expect(within(commandCenter).getByRole("heading", { name: "Gde gubimo novac?" })).toBeInTheDocument();
    expect(within(commandCenter).getByText(/Kapital blokiran u rizičnoj i sporoj zalihi\./i)).toBeInTheDocument();
  });

  it("keeps the newest dashboard bootstrap when filter changes trigger overlapping requests", async () => {
    const bootstrapResolvers: Array<(value: never) => void> = [];

    vi.spyOn(analyticsApi, "getDashboardBootstrap").mockImplementation(
      () =>
        new Promise((resolve) => {
          bootstrapResolvers.push(resolve as (value: never) => void);
        }),
    );
    vi.spyOn(analyticsApi, "getStores").mockResolvedValue([
      { storeId: 5, storeName: "Delta" },
      { storeId: 6, storeName: "Epsilon" },
    ] as never);
    vi.spyOn(analyticsApi, "getAnalyticsRefreshStatus").mockResolvedValue({
      isRunning: false,
      currentStep: null,
      dataFreshnessStatus: "good",
      lastSuccessfulRefreshAtUtc: "2026-08-05T10:00:00Z",
      jobs: [],
    } as never);
    vi.spyOn(analyticsApi, "checkAnalyticsHealth").mockResolvedValue({
      status: "ok",
      tables: { salesFacts: 10, salesLineFacts: 20, productsDim: 5 },
      message: "ok",
    } as never);

    render(
      <MemoryRouter>
        <AnalyticsDashboard />
      </MemoryRouter>,
    );

    await screen.findByRole("option", { name: "Epsilon" });
    await waitFor(() => expect(bootstrapResolvers).toHaveLength(1));

    fireEvent.change(screen.getByLabelText("Prodavnica"), {
      target: { value: "6" },
    });

    await waitFor(() => expect(bootstrapResolvers).toHaveLength(2));

    await act(async () => {
      bootstrapResolvers[1](buildBootstrapResponse("Novi signal"));
    });
    expect(await screen.findByText("Novi signal", { selector: "strong" })).toBeInTheDocument();

    await act(async () => {
      bootstrapResolvers[0](buildBootstrapResponse("Stari signal"));
    });

    await waitFor(() =>
      expect(screen.queryByText("Stari signal", { selector: "strong" })).not.toBeInTheDocument(),
    );
    expect(screen.getByText("Novi signal", { selector: "strong" })).toBeInTheDocument();
  });
});

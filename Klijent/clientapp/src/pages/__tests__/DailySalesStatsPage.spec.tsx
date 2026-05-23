import React from "react";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { rest } from "msw";
import { describe, it, expect, beforeEach, vi } from "vitest";
import { server } from "../../mocks/server";
import DailySalesStatsPage from "../DailySalesStatsPage";

// Mock the chart components
vi.mock("recharts", () => ({
  BarChart: ({ children }: any) => <div>{children}</div>,
  LineChart: ({ children }: any) => <div>{children}</div>,
  ComposedChart: ({ children }: any) => <div>{children}</div>,
  ResponsiveContainer: ({ children }: any) => <div>{children}</div>,
  CartesianGrid: () => <div />,
  XAxis: () => <div />,
  YAxis: () => <div />,
  Tooltip: () => <div />,
  Bar: () => <div />,
  Line: () => <div />,
  Legend: () => <div />,
}));

describe("DailySalesStatsPage (integration)", () => {
  const storesResponse = [
    { storeId: 1, storeName: "Store 1" },
    { storeId: 2, storeName: "Store 2" },
  ];

  const dailySalesResponse = {
    status: "ok",
    fromDate: "2026-04-01",
    toDate: "2026-04-30",
    summary: {
      totalRevenue: 250000,
      totalVisibleItems: 500,
      totalItemsInRange: 500,
      totalDays: 30,
      avgRevenuePerDay: 8333,
      avgItemsPerDay: 16.67,
      avgRevenuePerItem: 500,
      firstShiftItems: 250,
      secondShiftItems: 200,
      firstShiftSharePct: 50,
      secondShiftSharePct: 40,
      offShiftItems: 50,
      offShiftRevenue: 25000,
      offShiftSharePct: 10,
      unknownSupplierPct: 5,
      uniqueSuppliersInRange: 10,
    },
    rows: [
      {
        date: "2026-04-01",
        firstShiftTotalItems: 10,
        secondShiftTotalItems: 8,
        totalRevenue: 9000,
        othersCount: 2,
        totalItemsSold: 20,
        suppliers: [],
      },
    ],
    message: "OK",
  };

  beforeEach(() => {
    server.use(
      rest.get("/api/analytics/cached/filters/stores", (_req, res, ctx) =>
        res(ctx.status(200), ctx.json(storesResponse))
      ),
      rest.get("/api/analytics/daily-sales", (_req, res, ctx) =>
        res(ctx.status(200), ctx.json(dailySalesResponse))
      )
    );
  });

  it("renders page title", () => {
    render(
      <MemoryRouter initialEntries={["/analytics/daily-sales"]}>
        <Routes>
          <Route
            path="/analytics/daily-sales"
            element={<DailySalesStatsPage />}
          />
        </Routes>
      </MemoryRouter>
    );

    // Page now renders the title in both AnalyticsTrustHeader and the local page header.
    const titles = screen.getAllByRole("heading", { name: /Prodaja po smeni/i });
    expect(titles.length).toBeGreaterThan(0);
  });

  it("renders filter controls", () => {
    render(
      <MemoryRouter initialEntries={["/analytics/daily-sales"]}>
        <Routes>
          <Route
            path="/analytics/daily-sales"
            element={<DailySalesStatsPage />}
          />
        </Routes>
      </MemoryRouter>
    );

    const periodLabels = screen.getAllByText("Period");
    expect(periodLabels.length).toBeGreaterThan(0);
  });
});

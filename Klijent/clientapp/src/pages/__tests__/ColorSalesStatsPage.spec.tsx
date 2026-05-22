import React from "react";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { rest } from "msw";
import { describe, it, expect, beforeEach, vi } from "vitest";
import { server } from "../../mocks/server";
import ColorSalesStatsPage from "../ColorSalesStatsPage";

vi.mock("react", async () => {
  const actual = await vi.importActual<typeof import("react")>("react");
  return actual;
});

// Mock the chart components
vi.mock("recharts", () => ({
  PieChart: ({ children }: any) => <div>{children}</div>,
  BarChart: ({ children }: any) => <div>{children}</div>,
  ResponsiveContainer: ({ children }: any) => <div>{children}</div>,
  CartesianGrid: () => <div />,
  XAxis: () => <div />,
  YAxis: () => <div />,
  Tooltip: () => <div />,
  Bar: () => <div />,
  Pie: () => <div />,
  Cell: () => <div />,
  Legend: () => <div />,
}));

describe("ColorSalesStatsPage (integration)", () => {
  const colorSalesResponse = {
    status: "ok",
    fromDate: "2026-04-01",
    toDate: "2026-04-30",
    summary: {
      totalRevenue: 150000,
      totalItems: 300,
      uniqueColors: 25,
      topColor: "Black",
      topColorRevenue: 50000,
      avgRevenuePerColor: 6000,
      topColorSharePct: 33.33,
    },
    colorRows: [
      {
        colorId: 1,
        colorName: "Black",
        revenue: 50000,
        items: 100,
        sharePct: 33.33,
        margin: 15000,
      },
      {
        colorId: 2,
        colorName: "White",
        revenue: 40000,
        items: 80,
        sharePct: 26.67,
        margin: 12000,
      },
    ],
    message: "OK",
  };

  const storesResponse = [
    { storeId: 1, storeName: "Store 1" },
    { storeId: 2, storeName: "Store 2" },
  ];

  beforeEach(() => {
    server.use(
      rest.get("/api/analytics/cached/filters/stores", (_req, res, ctx) =>
        res(ctx.status(200), ctx.json(storesResponse))
      ),
      rest.get("/api/analytics/color-sales", (_req, res, ctx) =>
        res(ctx.status(200), ctx.json(colorSalesResponse))
      )
    );
  });

  it("renders page title", () => {
    render(
      <MemoryRouter initialEntries={["/analytics/color-sales"]}>
        <Routes>
          <Route
            path="/analytics/color-sales"
            element={<ColorSalesStatsPage />}
          />
        </Routes>
      </MemoryRouter>
    );

    const title = screen.getByText(/Prodaja po boji/i);
    expect(title).toBeInTheDocument();
  });

  it("renders filter controls", () => {
    render(
      <MemoryRouter initialEntries={["/analytics/color-sales"]}>
        <Routes>
          <Route
            path="/analytics/color-sales"
            element={<ColorSalesStatsPage />}
          />
        </Routes>
      </MemoryRouter>
    );

    const periodLabel = screen.getByText("Period");
    expect(periodLabel).toBeInTheDocument();
  });
});

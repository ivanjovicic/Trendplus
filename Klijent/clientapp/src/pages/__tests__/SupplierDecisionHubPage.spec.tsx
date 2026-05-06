import React from "react";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { rest } from "msw";
import { describe, it, expect, beforeEach, vi } from "vitest";
import { server } from "../../mocks/server";
import SupplierDecisionHubPage from "../SupplierDecisionHubPage";

// Mock the lazy-loaded and chart components
vi.mock("recharts", () => ({
  BarChart: ({ children }: any) => <div>{children}</div>,
  LineChart: ({ children }: any) => <div>{children}</div>,
  ResponsiveContainer: ({ children }: any) => <div>{children}</div>,
  CartesianGrid: () => <div />,
  XAxis: () => <div />,
  YAxis: () => <div />,
  Tooltip: () => <div />,
  Bar: () => <div />,
  Line: () => <div />,
  Legend: () => <div />,
}));

describe("SupplierDecisionHubPage (integration)", () => {
  const summaryResponse = {
    status: "ok",
    fromDate: "2026-01-01",
    toDate: "2026-04-30",
    totalRevenue: 500000,
    totalItems: 1000,
    supplierCount: 25,
    filteredSupplierCount: 25,
    topRecommendation: "review",
  };

  const rankingResponse = {
    status: "ok",
    items: [
      {
        supplierId: 1,
        supplierName: "Nike",
        revenue: 100000,
        items: 200,
        preMarkdownMarginPct: 45.5,
        qualityScore: 8.5,
        qualityRank: 1,
      },
      {
        supplierId: 2,
        supplierName: "Adidas",
        revenue: 80000,
        items: 150,
        preMarkdownMarginPct: 42.0,
        qualityScore: 7.8,
        qualityRank: 2,
      },
    ],
    totalCount: 25,
    page: 1,
    pageSize: 25,
  };

  beforeEach(() => {
    server.use(
      rest.get(
        "/api/analytics/suppliers/decision-hub/summary",
        (_req, res, ctx) =>
          res(ctx.status(200), ctx.json(summaryResponse))
      ),
      rest.get(
        "/api/analytics/suppliers/decision-hub/ranking",
        (_req, res, ctx) =>
          res(ctx.status(200), ctx.json(rankingResponse))
      ),
      rest.get("/api/sezone", (_req, res, ctx) =>
        res(ctx.status(200), ctx.json([]))
      )
    );
  });

  it("renders page title", () => {
    render(
      <MemoryRouter initialEntries={["/analytics/supplier-decision-hub"]}>
        <Routes>
          <Route
            path="/analytics/supplier-decision-hub"
            element={<SupplierDecisionHubPage />}
          />
        </Routes>
      </MemoryRouter>
    );

    const title = screen.getByText(/Prioriteti dobavljača/i);
    expect(title).toBeInTheDocument();
  });

  it("renders filter controls", () => {
    render(
      <MemoryRouter initialEntries={["/analytics/supplier-decision-hub"]}>
        <Routes>
          <Route
            path="/analytics/supplier-decision-hub"
            element={<SupplierDecisionHubPage />}
          />
        </Routes>
      </MemoryRouter>
    );

    const periodLabel = screen.getByText("Period");
    expect(periodLabel).toBeInTheDocument();
  });
});

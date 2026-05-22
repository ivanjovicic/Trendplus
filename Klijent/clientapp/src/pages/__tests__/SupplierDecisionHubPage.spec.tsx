import React from "react";
import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, describe, it, expect, vi } from "vitest";
import SupplierDecisionHubPage from "../SupplierDecisionHubPage";

vi.mock("../../utils/apiUrl", () => ({
  apiUrl: (path: string) => path,
}));

vi.mock("recharts", () => ({
  BarChart: ({ children }: { children?: React.ReactNode }) => <div>{children}</div>,
  ResponsiveContainer: ({ children }: { children?: React.ReactNode }) => <div>{children}</div>,
  CartesianGrid: () => <div />,
  XAxis: () => <div />,
  YAxis: () => <div />,
  Tooltip: () => <div />,
  Bar: () => <div />,
}));

function rankingItem(id: number, revenue = 100_000) {
  return {
    supplierId: id,
    supplierName: `Dobavljač ${id}`,
    revenue,
    units: 120,
    fullPriceRevenueShare: 0.62,
    fullPriceSellthrough: 0.48,
    preMarkdownMarginPct: 0.34,
    markdownRevenueShare: 0.24,
    deadStockRate: 0.08,
    unsoldStockValue: 12_000,
    repeatWinnerRate: 0.42,
    mlSupplierScore: 68,
    supplierQualityIndex: 72,
    recommendationCode: "EXPAND_SELECTIVELY",
    confidenceScore: 74,
  };
}

const summaryResponse = {
  from: "2026-04-13T00:00:00Z",
  to: "2026-05-12T00:00:00Z",
  supplierCount: 2,
  fullPriceRevenueShare: 0.62,
  fullPriceSellthrough: 0.48,
  markdownRevenueShare: 0.24,
  preMarkdownMarginPct: 0.34,
  capitalAtRisk: 24_000,
  topGrowSuppliers: [],
  topRiskSuppliers: [],
  keyInsights: [],
  dataNote: "Metrike su izračunate na osnovu nivelacija iz poslednjih 90 dana.",
};

function jsonResponse(body: unknown, status = 200) {
  return Promise.resolve(
    new Response(JSON.stringify(body), {
      status,
      headers: { "Content-Type": "application/json" },
    })
  );
}

function requestUrl(input: RequestInfo | URL) {
  if (input instanceof Request) {
    return new URL(input.url);
  }

  return new URL(String(input), "http://localhost");
}

function installFetchMock(rankingHandler?: (url: URL) => unknown) {
  const fetchMock = vi.fn((input: RequestInfo | URL) => {
    const url = requestUrl(input);

    if (url.pathname === "/api/analytics/suppliers/decision-hub/summary") {
      return jsonResponse(summaryResponse);
    }

    if (url.pathname === "/api/analytics/suppliers/decision-hub/ranking") {
      return jsonResponse(
        rankingHandler?.(url) ?? {
          page: 1,
          pageSize: 100,
          totalCount: 2,
          items: [rankingItem(1, 100_000), rankingItem(2, 80_000)],
          dataNote: summaryResponse.dataNote,
        }
      );
    }

    if (url.pathname === "/api/sezone") {
      return jsonResponse([]);
    }

    return jsonResponse({ message: `Unhandled test request: ${url.pathname}` }, 404);
  });

  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

function renderPage() {
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
}

describe("SupplierDecisionHubPage", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("renders clear Serbian scorecard explanation and filter controls", async () => {
    installFetchMock();

    renderPage();

    expect(screen.getAllByText(/Skorkarta dobavljac/i).length).toBeGreaterThan(0);
    expect(screen.getByText(/meri Skorkarta/i)).toBeInTheDocument();
    expect(screen.getAllByText("Period").length).toBeGreaterThan(0);
    expect(await screen.findByText(/Koncentracija prihoda/i)).toBeInTheDocument();
    expect(screen.getAllByText(/Trend pune cene/i).length).toBeGreaterThan(0);
  });

  it("loads every ranking page before deriving table and KPI data", async () => {
    const requestedPages: string[] = [];
    const firstPageItems = Array.from({ length: 100 }, (_, index) => rankingItem(index + 1));

    installFetchMock((url) => {
      const page = url.searchParams.get("page") ?? "1";
      requestedPages.push(page);
      return {
        page: Number(page),
        pageSize: 100,
        totalCount: 101,
        items: page === "1" ? firstPageItems : [rankingItem(101, 50_000)],
        dataNote: summaryResponse.dataNote,
      };
    });

    renderPage();

    await waitFor(() => {
      expect(requestedPages).toContain("1");
      expect(requestedPages).toContain("2");
    });
    expect(await screen.findByText("Dobavljač 101")).toBeInTheDocument();
  });
});

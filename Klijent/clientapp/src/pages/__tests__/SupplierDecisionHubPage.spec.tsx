import React from "react";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
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

function rankingItemWithOverrides(id: number, revenue = 100_000, overrides: Record<string, unknown> = {}) {
  return {
    ...rankingItem(id, revenue),
    ...overrides,
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

    expect(screen.getAllByText(/Skorkarta dobavljač/i).length).toBeGreaterThan(0);
    expect(screen.getByText(/meri Skorkarta/i)).toBeInTheDocument();
    expect(screen.getAllByText("Period").length).toBeGreaterThan(0);
    expect(await screen.findByText(/Koncentracija prihoda/i)).toBeInTheDocument();
    expect(screen.getAllByText(/Trend pune cene/i).length).toBeGreaterThan(0);
    expect(await screen.findByTestId("supplier-decision-hub-data-table")).toBeInTheDocument();
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

  it("shows explicit no-silent-fallback empty state when trust metadata says requested range has no rows", async () => {
    const trustMetadata = {
      requestedFrom: "2026-05-01T00:00:00Z",
      requestedTo: "2026-05-12T00:00:00Z",
      effectiveFrom: "2026-05-01T00:00:00Z",
      effectiveTo: "2026-05-12T00:00:00Z",
      requestedDataset: "30d",
      effectiveDataset: "30d",
      effectivePeriodLabel: "Poslednjih 30 dana",
      dataCoverageStatus: "insufficient_data",
      usedFallback: false,
      fallbackReason: null,
      lastRefreshAtUtc: "2026-05-12T00:00:00Z",
      rowCount: 0,
      ignoredRowCount: 0,
      zeroRevenueRowsExcludedCount: 0,
      missingSupplierNameCount: 0,
      hasData: false,
      hasExplicitDateRange: true,
      recommendationAllowed: false,
      noSilentFallback: true,
      windowDays: 90,
      dataScope: "all",
      coverage: "window_90d",
      dataNote: "Metrike su izračunate za traženi period od 30 dana, uz striktan opseg bez tihog fallback-a.",
    };

    const summaryWithTrust = {
      ...summaryResponse,
      supplierCount: 0,
      trustMetadata,
    };

    const rankingWithTrust = {
      page: 1,
      pageSize: 100,
      totalCount: 0,
      items: [],
      dataNote: summaryResponse.dataNote,
      trustMetadata,
    };

    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = requestUrl(input);

      if (url.pathname === "/api/analytics/suppliers/decision-hub/summary") {
        return jsonResponse(summaryWithTrust);
      }

      if (url.pathname === "/api/analytics/suppliers/decision-hub/ranking") {
        return jsonResponse(rankingWithTrust);
      }

      if (url.pathname === "/api/sezone") {
        return jsonResponse([]);
      }

      return jsonResponse({ message: `Unhandled test request: ${url.pathname}` }, 404);
    });

    vi.stubGlobal("fetch", fetchMock);

    renderPage();

    const messages = await screen.findAllByText(/Sistem nije koristio širi period kao fallback/i);
    expect(messages.length).toBeGreaterThan(0);
    expect(screen.getByText(/Proširite period na 90d ili 180d/i)).toBeInTheDocument();
  });

  it("shows fallback banner and helper signal label when recommendation is gated", async () => {
    const trustMetadata = {
      requestedFrom: "2026-05-01T00:00:00Z",
      requestedTo: "2026-05-12T00:00:00Z",
      requestedPeriodFrom: "2026-05-01T00:00:00Z",
      requestedPeriodTo: "2026-05-12T00:00:00Z",
      effectiveFrom: "2026-02-01T00:00:00Z",
      effectiveTo: "2026-05-12T00:00:00Z",
      requestedDataset: "30d",
      effectiveDataset: "90d",
      effectivePeriodLabel: "Poslednjih 90 dana",
      dataCoverageStatus: "warning",
      usedFallback: true,
      fallbackReason: "no_data_30d",
      lastRefreshAtUtc: "2026-05-12T00:00:00Z",
      rowCount: 1,
      ignoredRowCount: 0,
      zeroRevenueRowsExcludedCount: 0,
      missingSupplierNameCount: 0,
      hasData: true,
      hasExplicitDateRange: true,
      recommendationAllowed: false,
      noSilentFallback: true,
      windowDays: 90,
      dataScope: "all",
      coverage: "window_90d",
    };

    const summaryWithTrust = {
      ...summaryResponse,
      supplierCount: 1,
      trustMetadata,
    };

    const rankingWithTrust = {
      page: 1,
      pageSize: 100,
      totalCount: 1,
      items: [rankingItem(1, 100_000)],
      dataNote: summaryResponse.dataNote,
      trustMetadata,
    };

    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = requestUrl(input);

      if (url.pathname === "/api/analytics/suppliers/decision-hub/summary") {
        return jsonResponse(summaryWithTrust);
      }

      if (url.pathname === "/api/analytics/suppliers/decision-hub/ranking") {
        return jsonResponse(rankingWithTrust);
      }

      if (url.pathname === "/api/sezone") {
        return jsonResponse([]);
      }

      return jsonResponse({ message: `Unhandled test request: ${url.pathname}` }, 404);
    });

    vi.stubGlobal("fetch", fetchMock);

    renderPage();

    expect(await screen.findByText(/Prikazan je pomoćni dataset: Poslednjih 90 dana. Finalna preporuka je blokirana./i)).toBeInTheDocument();
    expect(screen.getAllByText("Pomoćni signal").length).toBeGreaterThan(0);
  });
  it("keeps missing supplier confidence unavailable instead of inventing a 0% value", async () => {
    installFetchMock((url) => ({
      page: Number(url.searchParams.get("page") ?? "1"),
      pageSize: 100,
      totalCount: 1,
      items: [
        rankingItemWithOverrides(1, 100_000, {
          confidenceScore: undefined,
          recommendationCode: "HOLD",
        }),
      ],
      dataNote: summaryResponse.dataNote,
    }));

    renderPage();

    await waitFor(() => {
      expect(screen.getAllByRole("button", { name: "Detalji" }).length).toBeGreaterThan(0);
    });
    fireEvent.click(screen.getAllByRole("button", { name: "Detalji" })[0]);

    expect((await screen.findAllByText(/backend nije dostavio confidence\/reliability signal/i)).length).toBeGreaterThan(0);
  });

  it("uses the shared premium table shell and numeric alignment on the ranking table", async () => {
    installFetchMock();

    renderPage();

    const tableSurface = await screen.findByTestId("supplier-decision-hub-data-table");
    const table = within(tableSurface).getByRole("table");

    expect(within(tableSurface).getByText("Prikazano: 2 redova")).toBeInTheDocument();
    expect(within(table).getByRole("columnheader", { name: /Prihod/i })).toHaveClass("analytics-data-table__numeric");
    expect(within(table).getByText(/100.000/).closest("td")).toHaveClass("analytics-data-table__numeric");
  });

  it("renders the supplier explainability snapshot on the hub", async () => {
    installFetchMock();

    renderPage();

    expect(await screen.findAllByTestId("supplier-explainability-snapshot")).toHaveLength(1);
    expect(screen.getByText("Supplier explainability snapshot")).toBeInTheDocument();
  });

  it("shows error state instead of zero KPIs when scorecard meta fails", async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = requestUrl(input);

      if (url.pathname === "/api/analytics/suppliers/decision-hub/summary") {
        return jsonResponse({
          ...summaryResponse,
          supplierCount: 0,
          fullPriceRevenueShare: 0,
          fullPriceSellthrough: 0,
          markdownRevenueShare: 0,
          preMarkdownMarginPct: 0,
          capitalAtRisk: 0,
          meta: {
            success: false,
            errorCode: "supplier_decision_unavailable",
            errorMessage: "Skorkarta dobavljača trenutno nije dostupna.",
          },
        });
      }

      if (url.pathname === "/api/analytics/suppliers/decision-hub/ranking") {
        return jsonResponse({
          page: 1,
          pageSize: 100,
          totalCount: 0,
          items: [],
          meta: {
            success: false,
            errorCode: "supplier_decision_unavailable",
            errorMessage: "Skorkarta dobavljača trenutno nije dostupna.",
          },
        });
      }

      if (url.pathname === "/api/sezone") {
        return jsonResponse([]);
      }

      return jsonResponse({ message: `Unhandled test request: ${url.pathname}` }, 404);
    });

    vi.stubGlobal("fetch", fetchMock);
    renderPage();

    expect(await screen.findByText("Podaci trenutno nisu dostupni")).toBeInTheDocument();
    expect(screen.queryByText("Ukupan prihod")).not.toBeInTheDocument();
  });
});

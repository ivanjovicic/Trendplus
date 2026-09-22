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
      return jsonResponse([{ id: 7, naziv: "Proleće 2026", datumOd: "2026-03-01", datumDo: "2026-05-31" }]);
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
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
  });

  it("hides standalone title, trust header and filters when embedded", async () => {
    installFetchMock();

    render(
      <MemoryRouter>
        <SupplierDecisionHubPage
          embedded
          sharedFilters={{
            periodPreset: "30d",
            fromDate: "2026-04-13",
            toDate: "2026-05-12",
            dataScope: "all",
            storeId: null,
            supplierId: null,
          }}
          onTrustMetadataChange={vi.fn()}
        />
      </MemoryRouter>,
    );

    expect(await screen.findByTestId("supplier-decision-hub-data-table")).toBeInTheDocument();
    expect(screen.queryAllByRole("heading", { level: 1 })).toHaveLength(0);
    expect(screen.queryByRole("button", { name: "Primeni" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Poništi filtere" })).not.toBeInTheDocument();
    expect(screen.getByRole("region", { name: "Skorkarta dobavljača" })).toBeInTheDocument();
  });

  it("sends every supported decision filter when the standalone form is applied", async () => {
    const fetchMock = installFetchMock();

    renderPage();
    await screen.findByTestId("supplier-decision-hub-data-table");

    fireEvent.change(screen.getByLabelText("Kategorija"), { target: { value: "Patike" } });
    fireEvent.change(screen.getByLabelText("Pol"), { target: { value: "Muško" } });
    fireEvent.change(screen.getByLabelText("Sezona"), { target: { value: "7" } });
    fireEvent.change(screen.getByLabelText("Min prihod"), { target: { value: "5000" } });
    fireEvent.click(screen.getByRole("checkbox", { name: /Samo visoka pouzdanost/i }));
    fireEvent.click(screen.getByRole("checkbox", { name: /bez zaliha pre sniženja/i }));
    fireEvent.click(screen.getByRole("button", { name: "Primeni" }));

    await waitFor(() => {
      const requestUrls = fetchMock.mock.calls.map(([input]) => requestUrl(input));
      const decisionRequest = requestUrls.find((url) => url.pathname.endsWith("/decision-hub/summary") && url.searchParams.get("category") === "Patike");
      expect(decisionRequest?.searchParams.get("category")).toBe("Patike");
      expect(decisionRequest?.searchParams.get("gender")).toBe("Muško");
      expect(decisionRequest?.searchParams.get("seasonId")).toBe("7");
      expect(decisionRequest?.searchParams.get("minRevenue")).toBe("5000");
      expect(decisionRequest?.searchParams.get("onlyHighConfidence")).toBe("true");
      expect(decisionRequest?.searchParams.get("excludeOosBeforeMarkdown")).toBe("true");
    });
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

  it("fails closed when backend reason fields have malformed runtime types", async () => {
    installFetchMock(() => ({
      page: 1,
      pageSize: 100,
      totalCount: 1,
      items: [rankingItemWithOverrides(1, 100_000, { statusReason: {}, reasonCodes: {} })],
      trustMetadata: { recommendationAllowed: true },
      dataNote: summaryResponse.dataNote,
    }));

    renderPage();

    expect(await screen.findByText("Dobavljač 1")).toBeInTheDocument();
    fireEvent.click(screen.getByText("Dobavljač 1").closest("tr")!.querySelector("button")!);
    expect(screen.getAllByText(/Server nije dostavio obrazloženje za ovaj signal skorkarte/i).length).toBeGreaterThan(0);
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
      provenanceBasis: "mv_supplier_decision_score_cache_90d",
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

    const messages = await screen.findAllByText(/Sistem nije koristio širi period kao pomoćni skup podataka/i);
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
      provenanceBasis: "mv_supplier_decision_score_cache_90d",
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

    expect(await screen.findByText(/Prikazan je pomoćni skup podataka: Poslednjih 90 dana. Konačna preporuka je blokirana./i)).toBeInTheDocument();
    expect(screen.queryByText(/no_data_30d/i)).not.toBeInTheDocument();
    expect(screen.getAllByText("Pomoćni signal").length).toBeGreaterThan(0);
    expect(screen.getAllByText("keš signala odluke dobavljača").length).toBeGreaterThan(0);

    fireEvent.click((await screen.findByText("Dobavljač 1")).closest("tr")!.querySelector("button")!);

    const detail = screen.getByRole("heading", { name: /Detalj signala skorkarte/i }).closest("section");
    expect(detail).not.toBeNull();
    expect(within(detail!).getByText(/Akcija nije dostupna: konačna preporuka nije dozvoljena/i)).toBeInTheDocument();
    expect(within(detail!).queryByRole("button", { name: "Dodaj u akcije" })).not.toBeInTheDocument();
    expect(within(detail!).getByRole("link", { name: "Kvalitet podataka" })).toHaveAttribute("href", "/analytics/data-quality");
    const postBodies = fetchMock.mock.calls
      .filter((call) => (call[1] as RequestInit | undefined)?.method === "POST")
      .map((call) => JSON.parse(String((call[1] as RequestInit).body)));
    expect(postBodies.some((body) => body.title || body.recommendationStatus)).toBe(false);
  });

  it("keeps the action CTA and write path only for an explicitly allowed recommendation", async () => {
    const trustMetadata = {
      requestedFrom: "2026-05-01T00:00:00Z",
      requestedTo: "2026-05-12T00:00:00Z",
      effectiveFrom: "2026-05-01T00:00:00Z",
      effectiveTo: "2026-05-12T00:00:00Z",
      requestedDataset: "30d",
      effectiveDataset: "30d",
      effectivePeriodLabel: "Poslednjih 30 dana",
      provenanceBasis: "mv_supplier_decision_score_cache_90d",
      dataCoverageStatus: "good",
      usedFallback: false,
      fallbackReason: null,
      lastRefreshAtUtc: "2026-05-12T00:00:00Z",
      rowCount: 1,
      ignoredRowCount: 0,
      zeroRevenueRowsExcludedCount: 0,
      missingSupplierNameCount: 0,
      hasData: true,
      hasExplicitDateRange: true,
      recommendationAllowed: true,
      noSilentFallback: true,
      windowDays: 90,
      dataScope: "all",
      coverage: "window_90d",
    };

    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = requestUrl(input);

      if (url.pathname === "/api/analytics/suppliers/decision-hub/summary") {
        return jsonResponse({ ...summaryResponse, supplierCount: 1, trustMetadata });
      }

      if (url.pathname === "/api/analytics/suppliers/decision-hub/ranking") {
        return jsonResponse({
          page: 1,
          pageSize: 100,
          totalCount: 1,
          items: [rankingItem(1, 100_000)],
          dataNote: summaryResponse.dataNote,
          trustMetadata,
        });
      }

      if (url.pathname === "/api/analytics/actions" && init?.method === "POST") {
        return jsonResponse({
          item: { sourceKey: "supplier:negotiation:1:2026-04-13:2026-05-12:all:all" },
          created: true,
          existing: false,
          status: "open",
          sourceKey: "supplier:negotiation:1:2026-04-13:2026-05-12:all:all",
        });
      }

      if (url.pathname === "/api/analytics/actions") {
        return jsonResponse({ items: [] });
      }

      if (url.pathname === "/api/sezone") {
        return jsonResponse([]);
      }

      return jsonResponse({ message: `Unhandled test request: ${url.pathname}` }, 404);
    });

    vi.stubGlobal("fetch", fetchMock);
    renderPage();

    fireEvent.click((await screen.findByText("Dobavljač 1")).closest("tr")!.querySelector("button")!);
    const detail = screen.getByRole("heading", { name: /Detalj signala skorkarte/i }).closest("section");
    expect(detail).not.toBeNull();
    const actionButton = within(detail!).getByRole("button", { name: "Dodaj u akcije" });
    fireEvent.click(actionButton);

    await waitFor(() => {
      expect(fetchMock.mock.calls.some((call) => (call[1] as RequestInit | undefined)?.method === "POST")).toBe(true);
    });
    const postCall = fetchMock.mock.calls.find((call) => {
      if ((call[1] as RequestInit | undefined)?.method !== "POST") return false;
      const body = JSON.parse(String((call[1] as RequestInit).body));
      return Boolean(body.title && body.recommendationStatus);
    });
    const postBody = JSON.parse(String((postCall?.[1] as RequestInit).body));
    expect(postBody).toMatchObject({ recommendationStatus: "increase_focus" });
    const actionUrl = new URL(postBody.actionUrl, "http://localhost");
    expect(actionUrl.pathname).toBe("/analytics/supplier");
    expect(actionUrl.searchParams.get("tab")).toBe("scorecard");
    expect(actionUrl.searchParams.get("supplierId")).toBe("1");
    expect(actionUrl.searchParams.get("fromDate")).toBeTruthy();
    expect(actionUrl.searchParams.get("toDate")).toBeTruthy();
    expect(actionUrl.searchParams.get("onlyHighConfidence")).toBe("false");
    expect(actionUrl.searchParams.get("excludeOosBeforeMarkdown")).toBe("false");
    expect(JSON.parse(postBody.metadataJson)).toMatchObject({
      recommendationAllowed: true,
      requestedPeriodFrom: "2026-05-01T00:00:00Z",
      requestedPeriodTo: "2026-05-12T00:00:00Z",
      effectivePeriodFrom: "2026-05-01T00:00:00Z",
      effectivePeriodTo: "2026-05-12T00:00:00Z",
      observedPeriodFrom: summaryResponse.from,
      observedPeriodTo: summaryResponse.to,
      effectiveDataset: "30d",
    });
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

    expect((await screen.findAllByText(/server nije dostavio signal sigurnosti\/pouzdanosti/i)).length).toBeGreaterThan(0);
  });

  it("keeps missing share and reliability unavailable across KPI, chart, table, tooltip, and details", async () => {
    installFetchMock((url) => ({
      page: Number(url.searchParams.get("page") ?? "1"),
      pageSize: 100,
      totalCount: 1,
      items: [
        rankingItemWithOverrides(1, 0, {
          confidenceScore: null,
          reliabilityPct: null,
          recommendationCode: "HOLD",
        }),
      ],
      dataNote: summaryResponse.dataNote,
    }));

    renderPage();

    const top5Article = (await screen.findByText("Udeo top 5 dobavljača")).closest("article");
    expect(top5Article).not.toBeNull();
    expect(within(top5Article!).getByText("Nije dostupno")).toBeInTheDocument();

    expect(screen.getByText("Nema podataka za grafikon koncentracije.")).toBeInTheDocument();

    const supplierRow = (await screen.findByText("Dobavljač 1")).closest("tr");
    expect(supplierRow).not.toBeNull();
    expect(within(supplierRow!).getByText("Nije dostupno")).toBeInTheDocument();
    expect(within(supplierRow!).getByLabelText(/Udeo Nije dostupno/i)).toBeInTheDocument();

    fireEvent.click(within(supplierRow!).getByRole("button", { name: "Detalji" }));

    const confidenceArticle = (await screen.findByText("Sigurnost signala")).closest("article");
    const reliabilityArticle = screen.getByText("Pouzdanost signala").closest("article");
    expect(confidenceArticle).not.toBeNull();
    expect(reliabilityArticle).not.toBeNull();
    expect(within(confidenceArticle!).getByText(/nije dostup/i)).toBeInTheDocument();
    expect(within(reliabilityArticle!).getByText(/nije dostup/i)).toBeInTheDocument();
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
    expect(screen.getByText("Sažetak objašnjenja signala")).toBeInTheDocument();
  });

  it("opens rich backend details with the active filter and trust provenance", async () => {
    const detailUrls: URL[] = [];
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = requestUrl(input);

      if (url.pathname === "/api/analytics/suppliers/decision-hub/summary") {
        return jsonResponse(summaryResponse);
      }

      if (url.pathname === "/api/analytics/suppliers/decision-hub/ranking") {
        return jsonResponse({
          page: 1,
          pageSize: 100,
          totalCount: 2,
          items: [rankingItem(1), rankingItem(2, 80_000)],
          dataNote: summaryResponse.dataNote,
        });
      }

      if (url.pathname === "/api/analytics/suppliers/decision-hub/1/details") {
        detailUrls.push(url);
        return jsonResponse({
          supplierHeader: {
            supplierId: 1,
            supplierName: "Dobavljač 1",
            periodFrom: "2026-04-13T00:00:00Z",
            periodTo: "2026-05-12T00:00:00Z",
            mlSupplierScore: 68,
            aiExplanation: "Stabilan signal.",
            topFeature1: "margin",
            topFeature2: "sellthrough",
            topFeature3: "stock",
            supplierQualityIndex: 72,
            recommendationCode: "EXPAND_SELECTIVELY",
            confidenceScore: 74,
            reliabilityPct: 78,
            dataQualityStatus: "warning",
            statusReason: "Signal zahteva proveru pokrivenosti.",
            reasonCodes: ["coverage_gap"],
          },
          kpis: {
            revenue: 100000,
            units: 120,
            fullPriceRevenueShare: 0.62,
            fullPriceSellthrough: 0.48,
            markdownRevenueShare: 0.24,
            preMarkdownMarginPct: 0.34,
            deadStockRate: 0.08,
            unsoldStockValue: 12000,
            repeatWinnerRate: 0.42,
            capitalAtRisk: 12000,
          },
          categoryBreakdown: [],
          winningArticles: [],
          markdownDependentArticles: [],
          blockedByOosArticles: [],
          recommendationHistory: [],
          trustMetadata: {
            requestedFrom: "2026-05-01T00:00:00Z",
            requestedTo: "2026-05-12T00:00:00Z",
            requestedPeriodFrom: "2026-05-01T00:00:00Z",
            requestedPeriodTo: "2026-05-12T00:00:00Z",
            requestedDataset: "30d",
            effectiveDataset: "90d",
            effectivePeriodLabel: "Poslednjih 90 dana",
            recommendationAllowed: false,
            dataCoverageStatus: "warning",
            usedFallback: true,
          },
          dataNote: "Koristi se pomoćni skup podataka.",
          meta: {
            success: true,
            recommendationAllowed: false,
            isPartial: true,
            dataQualityStatus: "warning",
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

    const row = (await screen.findByText("Dobavljač 1")).closest("tr");
    expect(row).not.toBeNull();
    fireEvent.click(within(row!).getByRole("button", { name: "Detalji" }));
    fireEvent.click(await screen.findByRole("button", { name: "Otvori puni detalj" }));

    expect(await screen.findByRole("heading", { name: "Dobavljač 1" })).toBeInTheDocument();
    const drawer = screen.getByRole("complementary", { name: "Detalji dobavljača" });
    expect(within(drawer).getByText("Skup podataka: 90d")).toBeInTheDocument();
    expect(within(drawer).getByText("Traženi period: 01.05.2026. - 12.05.2026.")).toBeInTheDocument();
    expect(within(drawer).getByText("Efektivni period: Poslednjih 90 dana")).toBeInTheDocument();
    expect(within(drawer).getByText(/Konačna preporuka nije dozvoljena/i)).toBeInTheDocument();
    expect(detailUrls).toHaveLength(1);
    expect(detailUrls[0].searchParams.get("fromDate")).toBeTruthy();
    expect(detailUrls[0].searchParams.get("toDate")).toBeTruthy();
    expect(detailUrls[0].searchParams.get("dataScope")).toBe("all");
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

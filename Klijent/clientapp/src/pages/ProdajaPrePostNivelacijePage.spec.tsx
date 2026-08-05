import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import ProdajaPrePostNivelacijePage from "./ProdajaPrePostNivelacijePage";
import { getStores } from "../services/analyticsApi";
import { getDobavljaci } from "../services/dobavljaciApi";
import { getVendorSalesNivelacija } from "../services/vendorSalesNivelacijaApi";
import type { VendorSalesNivelacijaResponse, VendorSalesNivelacijaVendorStat } from "../services/vendorSalesNivelacijaApi";

vi.mock("recharts", () => ({
  Bar: () => null,
  BarChart: ({ children }: { children?: ReactNode }) => <div data-testid="bar-chart">{children}</div>,
  CartesianGrid: () => null,
  Cell: () => null,
  ResponsiveContainer: ({ children }: { children?: ReactNode }) => <div data-testid="responsive-container">{children}</div>,
  Tooltip: () => null,
  XAxis: () => null,
  YAxis: () => null,
}));

vi.mock("../components/analytics/AnalyticsTableToolbar", () => ({
  default: ({ tableKey, rows }: { tableKey: string; rows: unknown[] }) => (
    <div data-testid="analytics-toolbar">{tableKey}: {rows.length} rows</div>
  ),
}));

vi.mock("../components/analytics/AnalyticsTrustHeader", () => ({
  default: ({ dataSource }: { dataSource?: string | null }) => (
    <div data-testid="analytics-trust-header">{dataSource}</div>
  ),
}));

vi.mock("../components/ui/InfoTip", () => ({
  default: ({ text }: { text: string }) => <span data-testid="info-tip">{text}</span>,
}));

vi.mock("../services/analyticsApi", async () => {
  const actual = await vi.importActual<typeof import("../services/analyticsApi")>("../services/analyticsApi");
  return { ...actual, getStores: vi.fn() };
});

vi.mock("../services/dobavljaciApi", async () => {
  const actual = await vi.importActual<typeof import("../services/dobavljaciApi")>("../services/dobavljaciApi");
  return { ...actual, getDobavljaci: vi.fn() };
});

vi.mock("../services/vendorSalesNivelacijaApi", async () => {
  const actual = await vi.importActual<typeof import("../services/vendorSalesNivelacijaApi")>("../services/vendorSalesNivelacijaApi");
  return { ...actual, getVendorSalesNivelacija: vi.fn() };
});

function vendor(overrides: Partial<VendorSalesNivelacijaVendorStat> = {}): VendorSalesNivelacijaVendorStat {
  return {
    vendorId: 10,
    vendorName: "Vendor A",
    preQty: 10,
    preRevenue: 80000,
    postQty: 12,
    postRevenue: 100000,
    changeQty: 2,
    changeRevenue: 20000,
    changePercent: 25,
    absoluteChangeRevenue: 20000,
    changeSharePercent: 100,
    postRevenueSharePercent: 100,
    avgCoveragePre30: 0.8,
    avgCoveragePost30: 0.9,
    articleCount: 4,
    activeArticlesCount: 4,
    increasedPriceArticlesCount: 2,
    decreasedPriceArticlesCount: 1,
    reliabilityPct: 80,
    recommendation: {
      status: "increase_focus",
      label: "Increase focus",
      summary: "Jak signal.",
      confidencePct: 85,
      reliabilityPct: 80,
      dataQualityStatus: "good",
      reasonCodes: [],
    },
    ...overrides,
  };
}

function response(overrides: Partial<VendorSalesNivelacijaResponse> = {}): VendorSalesNivelacijaResponse {
  return {
    generatedAt: "2026-07-01T08:30:00Z",
    windowDays: 30,
    vendorId: null,
    eventDate: null,
    from: "2026-06-01T00:00:00Z",
    to: "2026-06-30T23:59:59Z",
    category: null,
    includeInactive: false,
    categories: ["Obuca"],
    vendorStats: [vendor()],
    articleStats: [],
    totals: {
      vendorsCount: 1,
      articlesCount: 4,
      activeArticlesCount: 4,
      preRevenue: 80000,
      postRevenue: 100000,
      changeRevenue: 20000,
      absoluteChangeRevenue: 20000,
      preQty: 10,
      postQty: 12,
      changeQty: 2,
      changePercent: 25,
      avgRevenuePerArticlePre: 20000,
      avgRevenuePerArticlePost: 25000,
      avgPriceChangePercent: 5,
      avgCoveragePre30: 0.8,
      avgCoveragePost30: 0.9,
    },
    dataQuality: {
      rawRows: 4,
      deduplicatedRows: 4,
      duplicateRowsRemoved: 0,
      inactiveRows: 0,
      unchangedPriceRows: 0,
      analyzedRows: 4,
      analyzedSharePercent: 95,
      lowPostCoverageRows: 0,
      avgCoveragePre30: 0.8,
      avgCoveragePost30: 0.9,
    },
    categoryStats: [],
    priceDirectionStats: [],
    insights: [],
    metricsStatus: "OK",
    meta: { success: true, dataQualityStatus: "good" } as VendorSalesNivelacijaResponse["meta"],
    ...overrides,
  };
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={["/analitika/nivelacije-pre-post"]}>
      <Routes>
        <Route path="/analitika/nivelacije-pre-post" element={<ProdajaPrePostNivelacijePage />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe("ProdajaPrePostNivelacijePage scope lineage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.setItem("trendplus:dataScope", "all");
    vi.mocked(getDobavljaci).mockResolvedValue([{ id: 10, naziv: "Vendor A" } as never]);
    vi.mocked(getStores).mockResolvedValue([
      { storeId: 2, storeName: "Novi Beograd", city: "Beograd", region: "BG" },
    ]);
    vi.mocked(getVendorSalesNivelacija).mockResolvedValue(response());
  });

  it("passes dataScope and storeId to current and previous period requests", async () => {
    localStorage.setItem("trendplus:dataScope", "imported");
    renderPage();

    await screen.findByText("Prioritetna lista dobavljača");
    expect(screen.getByTestId("analytics-trust-header")).toHaveTextContent("scope: imported");

    expect(getVendorSalesNivelacija).toHaveBeenCalled();
    const initialCalls = vi.mocked(getVendorSalesNivelacija).mock.calls.map((call) => call[0]);
    expect(initialCalls.length).toBeGreaterThanOrEqual(2);
    expect(initialCalls.every((query) => query.dataScope === "imported")).toBe(true);
    expect(initialCalls.every((query) => query.storeId == null)).toBe(true);

    fireEvent.change(screen.getByDisplayValue("Svi objekti"), { target: { value: "2" } });
    fireEvent.click(screen.getByRole("button", { name: "Primeni" }));

    await waitFor(() => {
      const latestCalls = vi.mocked(getVendorSalesNivelacija).mock.calls.slice(-2).map((call) => call[0]);
      expect(latestCalls).toHaveLength(2);
      expect(latestCalls.every((query) => query.storeId === 2 && query.dataScope === "imported")).toBe(true);
    });

    expect(screen.getByTestId("analytics-trust-header")).toHaveTextContent("store: 2");
  });

  it("reloads both period requests when global dataScope changes", async () => {
    renderPage();
    await screen.findByText("Prioritetna lista dobavljača");
    const callsBefore = vi.mocked(getVendorSalesNivelacija).mock.calls.length;

    localStorage.setItem("trendplus:dataScope", "existing");
    window.dispatchEvent(new Event("trendplus:data-scope-changed"));

    await waitFor(() => {
      expect(vi.mocked(getVendorSalesNivelacija).mock.calls.length).toBeGreaterThan(callsBefore);
      const latestCalls = vi.mocked(getVendorSalesNivelacija).mock.calls.slice(-2).map((call) => call[0]);
      expect(latestCalls.every((query) => query.dataScope === "existing")).toBe(true);
    });
  });

  it("warns when previous-period request fails and does not label it as Nova baza", async () => {
    vi.mocked(getVendorSalesNivelacija)
      .mockResolvedValueOnce(response())
      .mockRejectedValueOnce(new Error("Previous period timeout"));

    renderPage();
    await screen.findByText("Prioritetna lista dobavljača");

    const warning = await screen.findByTestId("previous-comparison-warning");
    expect(warning).toHaveTextContent("Previous period timeout");
    expect(warning).toHaveTextContent("greške zahteva");
    expect(screen.getAllByText("Nedostupno").length).toBeGreaterThanOrEqual(2);
    expect(screen.queryByText("Nova baza")).not.toBeInTheDocument();
  });
});

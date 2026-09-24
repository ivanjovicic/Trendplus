import { render, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import SupplierFootwearAnalyticsPage from "../SupplierFootwearAnalyticsPage";
import { getVendorSalesNivelacija } from "../../services/vendorSalesNivelacijaApi";
import { getDataScopeStorageKey } from "../../utils/dataScope";

vi.mock("recharts", () => ({
  Bar: ({ children }: { children?: React.ReactNode }) => <div>{children}</div>,
  BarChart: ({ children }: { children?: React.ReactNode }) => <div>{children}</div>,
  CartesianGrid: () => <div />,
  ResponsiveContainer: ({ children }: { children?: React.ReactNode }) => <div>{children}</div>,
  Tooltip: () => <div />,
  XAxis: () => <div />,
  YAxis: () => <div />,
}));

vi.mock("../../components/analytics/AnalyticsTableToolbar", () => ({
  default: () => <div data-testid="analytics-table-toolbar" />,
}));

vi.mock("../../components/analytics/AnalyticsTrustHeader", () => ({
  default: ({ title }: { title: string }) => <h1>{title}</h1>,
}));

vi.mock("../../services/dobavljaciApi", () => ({
  getDobavljaci: vi.fn().mockResolvedValue([]),
}));

vi.mock("../../services/vendorSalesNivelacijaApi", () => ({
  getVendorSalesNivelacija: vi.fn().mockResolvedValue({
    generatedAt: "2026-08-11T10:00:00Z",
    windowDays: 30,
    dataScope: "imported",
    vendorStats: [],
    articleStats: [],
    totals: {
      preQty: 0,
      preRevenue: 0,
      postQty: 0,
      postRevenue: 0,
      changeQty: 0,
      changeRevenue: 0,
      changePercent: 0,
      vendorsCount: 0,
      articlesCount: 0,
      activeArticlesCount: 0,
      avgRevenuePerArticlePre: 0,
      avgRevenuePerArticlePost: 0,
      avgPriceChangePercent: 0,
      absoluteChangeRevenue: 0,
      avgCoveragePre30: 0,
      avgCoveragePost30: 0,
      hasComparableSalesWindow: false,
    },
    categories: [],
    meta: { success: true, dataQualityStatus: "insufficient_data" },
  }),
  getVendorSalesNivelacijaOptions: vi.fn().mockResolvedValue([]),
}));

describe("SupplierFootwearAnalyticsPage scope contract", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  afterEach(() => {
    localStorage.clear();
  });

  it("uses the standalone global data scope on the first request instead of implicit all", async () => {
    localStorage.setItem(getDataScopeStorageKey(), "imported");

    render(
      <MemoryRouter initialEntries={["/analytics/dobavljaci-tipovi-obuce"]}>
        <SupplierFootwearAnalyticsPage />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(getVendorSalesNivelacija).toHaveBeenCalledWith(expect.objectContaining({ dataScope: "imported" }));
    });
  });

  it("reloads with the new scope when the global data scope changes", async () => {
    localStorage.setItem(getDataScopeStorageKey(), "existing");

    render(
      <MemoryRouter initialEntries={["/analytics/dobavljaci-tipovi-obuce"]}>
        <SupplierFootwearAnalyticsPage />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(getVendorSalesNivelacija).toHaveBeenCalledWith(expect.objectContaining({ dataScope: "existing" }));
    });

    localStorage.setItem(getDataScopeStorageKey(), "imported");
    window.dispatchEvent(new Event("trendplus:data-scope-changed"));

    await waitFor(() => {
      expect(getVendorSalesNivelacija).toHaveBeenLastCalledWith(expect.objectContaining({ dataScope: "imported" }));
    });
  });

  it("keeps embedded shared scope authoritative over the standalone global scope", async () => {
    localStorage.setItem(getDataScopeStorageKey(), "imported");

    render(
      <MemoryRouter>
        <SupplierFootwearAnalyticsPage
          embedded
          sharedFilters={{
            periodPreset: "30d",
            fromDate: "2026-07-13",
            toDate: "2026-08-11",
            dataScope: "existing",
            storeId: null,
            supplierId: null,
          }}
          onTrustMetadataChange={vi.fn()}
        />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(getVendorSalesNivelacija).toHaveBeenCalledWith(expect.objectContaining({ dataScope: "existing" }));
    });
  });
});

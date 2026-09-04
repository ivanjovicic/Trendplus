import { render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import SupplierFootwearAnalyticsPage from "../SupplierFootwearAnalyticsPage";
import { getVendorSalesNivelacija } from "../../services/vendorSalesNivelacijaApi";

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
  default: function MockAnalyticsTableToolbar() {
    return <div data-testid="analytics-table-toolbar" />;
  },
}));

vi.mock("../../services/dobavljaciApi", () => ({
  getDobavljaci: vi.fn().mockResolvedValue([
    { id: 1, naziv: "Dobavljač 1" },
  ]),
}));

vi.mock("../../services/vendorSalesNivelacijaApi", () => ({
  getVendorSalesNivelacija: vi.fn().mockResolvedValue({
    generatedAt: "2026-08-11T10:00:00Z",
    windowDays: 30,
    vendorId: null,
    eventDate: null,
    from: "2026-07-13T00:00:00Z",
    to: "2026-08-11T23:59:59Z",
    category: null,
    includeInactive: false,
    categories: ["Patike"],
    vendorStats: [
      {
        vendorId: 1,
        vendorName: "Dobavljač 1",
        preQty: 10,
        preRevenue: 1_000,
        postQty: 12,
        postRevenue: 1_200,
        changeQty: 2,
        changeRevenue: 200,
        changePercent: 20,
        absoluteChangeRevenue: 200,
        changeSharePercent: 50,
        postRevenueSharePercent: 100,
        avgCoveragePre30: 0.8,
        avgCoveragePost30: 0.7,
        articleCount: 1,
        activeArticlesCount: 1,
        increasedPriceArticlesCount: 0,
        decreasedPriceArticlesCount: 0,
        reliabilityPct: 80,
        recommendation: {
          status: "increase_focus",
          label: "Pojačaj fokus",
          summary: "Signal je jak.",
          confidencePct: 75,
          reliabilityPct: 80,
          dataQualityStatus: "good",
          recommendationAllowed: true,
          reasonCodes: ["high_signal"],
        },
      },
    ],
    articleStats: [
      {
        eventDate: "2026-08-01T00:00:00Z",
        vendorId: 1,
        vendorName: "Dobavljač 1",
        sku: "SKU-1",
        articleName: "Patika 1",
        category: "Patike",
        oldPrice: 100,
        newPrice: 120,
        preQty: 10,
        preRevenue: 1_000,
        postQty: 12,
        postRevenue: 1_200,
        changeQty: 2,
        changeRevenue: 200,
        changePercent: 20,
        coveragePre30: 0.8,
        coveragePost30: 0.7,
        hasSalesWindow: true,
        priceChanged: true,
        priceChangePercent: 20,
      },
    ],
    totals: {
      preQty: 10,
      preRevenue: 1_000,
      postQty: 12,
      postRevenue: 1_200,
      changeQty: 2,
      changeRevenue: 200,
      changePercent: 20,
      vendorsCount: 1,
      articlesCount: 1,
      activeArticlesCount: 1,
      avgRevenuePerArticlePre: 1_000,
      avgRevenuePerArticlePost: 1_200,
      avgPriceChangePercent: 20,
      absoluteChangeRevenue: 200,
      avgCoveragePre30: 0.8,
      avgCoveragePost30: 0.7,
    },
    dataQuality: {
      rawRows: 1,
      deduplicatedRows: 1,
      duplicateRowsRemoved: 0,
      inactiveRows: 0,
      unchangedPriceRows: 0,
      analyzedRows: 1,
      analyzedSharePercent: 100,
      lowPostCoverageRows: 0,
      avgCoveragePre30: 0.8,
      avgCoveragePost30: 0.7,
    },
    categoryStats: [],
    priceDirectionStats: [],
    insights: [],
    recommendationAllowed: true,
    meta: {
      success: true,
      dataQualityStatus: "good",
      lastRefreshAtUtc: "2026-08-11T10:00:00Z",
      recommendationAllowed: true,
    },
  }),
  getVendorSalesNivelacijaOptions: vi.fn().mockResolvedValue([]),
}));

describe("SupplierFootwearAnalyticsPage", () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it("renders shared trust header, control bar, and data table chrome", async () => {
    render(
      <MemoryRouter>
        <SupplierFootwearAnalyticsPage />
      </MemoryRouter>
    );

    expect(await screen.findByText("Analitički signal")).toBeInTheDocument();
    expect(screen.getByText("Kontrole asortimana")).toBeInTheDocument();
    expect(await screen.findByTestId("analytics-control-bar")).toBeInTheDocument();
    expect(await screen.findByTestId("supplier-footwear-analytics-data-table")).toBeInTheDocument();
    expect(screen.getByText("Primeni filtere")).toBeInTheDocument();
    expect(screen.getByText("Reset filtera")).toBeInTheDocument();
    expect(within(await screen.findByTestId("analytics-control-bar")).getByText("Kvalitet podataka")).toBeInTheDocument();
    expect(await screen.findByText(/Prikazano:\s*1 red/i)).toBeInTheDocument();
  });

  it("publishes trust metadata for the embedded consolidated page", async () => {
    const onTrustMetadataChange = vi.fn();

    render(
      <MemoryRouter>
        <SupplierFootwearAnalyticsPage
          embedded
          sharedFilters={{
            periodPreset: "30d",
            fromDate: "2026-07-13",
            toDate: "2026-08-11",
            dataScope: "all",
            storeId: null,
            supplierId: null,
          }}
          onTrustMetadataChange={onTrustMetadataChange}
        />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(onTrustMetadataChange).toHaveBeenLastCalledWith(expect.objectContaining({
        periodFrom: "2026-07-13",
        periodTo: "2026-08-11",
        dataSource: "Supplier sales nivelacija po dobavljaču i tipu obuće",
        dataQualityStatus: "good",
        recommendationAllowed: true,
      }));
    });
  });

  it("keeps missing share and confidence metrics unavailable across KPI, chart, table, tooltip, and details", async () => {
    vi.mocked(getVendorSalesNivelacija).mockResolvedValueOnce({
      generatedAt: "2026-08-11T10:00:00Z",
      windowDays: 30,
      vendorId: null,
      eventDate: null,
      from: "2026-07-13T00:00:00Z",
      to: "2026-08-11T23:59:59Z",
      category: null,
      includeInactive: false,
      categories: ["Patike"],
      vendorStats: [
        {
          vendorId: 1,
          vendorName: "Dobavljač 1",
          preQty: 0,
          preRevenue: 0,
          postQty: 0,
          postRevenue: 0,
          changeQty: 0,
          changeRevenue: 0,
          changePercent: 0,
          absoluteChangeRevenue: 0,
          changeSharePercent: 0,
          postRevenueSharePercent: null,
          avgCoveragePre30: 0,
          avgCoveragePost30: 0,
          articleCount: 1,
          activeArticlesCount: 0,
          increasedPriceArticlesCount: 0,
          decreasedPriceArticlesCount: 0,
          reliabilityPct: null,
          recommendation: {
            status: "review_data",
            label: "Proveri podatke",
            summary: "Nema dovoljno signala.",
            confidencePct: null,
            reliabilityPct: null,
            dataQualityStatus: "insufficient_data",
            reasonCodes: ["insufficient_history"],
          },
        },
      ],
      articleStats: [],
      totals: {
        preQty: 0,
        preRevenue: 0,
        postQty: 0,
        postRevenue: 0,
        changeQty: 0,
        changeRevenue: 0,
        changePercent: 0,
        vendorsCount: 1,
        articlesCount: 1,
        activeArticlesCount: 0,
        avgRevenuePerArticlePre: 0,
        avgRevenuePerArticlePost: 0,
        avgPriceChangePercent: 0,
        absoluteChangeRevenue: 0,
        avgCoveragePre30: 0,
        avgCoveragePost30: 0,
      },
      dataQuality: {
        rawRows: 1,
        deduplicatedRows: 1,
        duplicateRowsRemoved: 0,
        inactiveRows: 0,
        unchangedPriceRows: 0,
        analyzedRows: 1,
        analyzedSharePercent: 0,
        lowPostCoverageRows: 1,
        avgCoveragePre30: 0,
        avgCoveragePost30: 0,
      },
      categoryStats: [],
      priceDirectionStats: [],
      insights: [],
      meta: {
        success: true,
        dataQualityStatus: "insufficient_data",
        lastRefreshAtUtc: "2026-08-11T10:00:00Z",
      },
    } as never);

    render(
      <MemoryRouter>
        <SupplierFootwearAnalyticsPage />
      </MemoryRouter>
    );

    const top5Article = (await screen.findByText("Udeo top 5 dobavljaca")).closest("article");
    expect(top5Article).not.toBeNull();
    expect(within(top5Article!).getByText("Nije dostupno")).toBeInTheDocument();

    expect(screen.getByText("Nema podataka za grafikon tipova obuce.")).toBeInTheDocument();

    const tableSurface = await screen.findByTestId("supplier-footwear-analytics-data-table");
    const vendorRow = within(tableSurface).getByText("Dobavljač 1").closest("tr");
    expect(vendorRow).not.toBeNull();
    expect(within(vendorRow!).getByText("Nije dostupno")).toBeInTheDocument();
    expect(within(vendorRow!).getByLabelText(/Udeo Nije dostupno/i)).toBeInTheDocument();

    within(vendorRow!).getByRole("button", { name: "Detalji" }).click();

    const reliabilityArticle = (await screen.findByText("Pouzdanost signala")).closest("article");
    const confidenceArticle = screen.getByText("Poverenje preporuke").closest("article");
    expect(reliabilityArticle).not.toBeNull();
    expect(confidenceArticle).not.toBeNull();
    expect(within(reliabilityArticle!).getByText("Nije dostupno")).toBeInTheDocument();
    expect(within(confidenceArticle!).getByText("Nije dostupno")).toBeInTheDocument();
  });
});

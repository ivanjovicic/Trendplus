import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import SupplierFootwearAnalyticsPage, { decisionColumns } from "../SupplierFootwearAnalyticsPage";
import { getVendorSalesNivelacija, getVendorSalesNivelacijaOptions } from "../../services/vendorSalesNivelacijaApi";
import { buildAnalyticsDetailSnapshot, resolveAnalyticsTablePayload, saveAnalyticsDetailSnapshot } from "../../services/analyticsTableState";
import * as analyticsTableState from "../../services/analyticsTableState";
import { AnalyticsMetaError } from "../../utils/analyticsResponseMeta";

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
    default: function MockAnalyticsTableToolbar({ metadata = [] }: {
      metadata?: Array<{ label: string; value: unknown }>;
    }) {
    return (
      <div data-testid="analytics-table-toolbar">
        {metadata.map((item) => <span key={item.label}>{item.label}: {String(item.value)}</span>)}
      </div>
    );
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
          status: "effective",
          label: "Efekat pozitivan",
          summary: "Promet posle promene cene raste 20% u odnosu na uporediv pre prozor (descriptive signal, ne PoP).",
          confidencePct: 75,
          reliabilityPct: 80,
          dataQualityStatus: "good",
          recommendationAllowed: false,
          reasonCodes: [],
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

  it("exports unavailable toolbar metadata instead of fake zero counts", async () => {
    const baseResponse = await getVendorSalesNivelacija({});
    vi.mocked(getVendorSalesNivelacija).mockResolvedValueOnce({
      ...baseResponse,
      windowDays: null,
      totals: {
        ...baseResponse.totals,
        vendorsCount: null,
        articlesCount: null,
      },
    });

    render(
      <MemoryRouter>
        <SupplierFootwearAnalyticsPage />
      </MemoryRouter>,
    );

    const toolbar = await screen.findByTestId("analytics-table-toolbar");
    expect(toolbar).toHaveTextContent("Dobavljača: Nije dostupno");
    expect(toolbar).toHaveTextContent("Artikala: Nije dostupno");
    expect(toolbar).toHaveTextContent("Prozor (dani): Nije dostupno");
    expect(toolbar).not.toHaveTextContent("Dobavljača: 0");
    expect(toolbar).not.toHaveTextContent("Artikala: 0");
    expect(toolbar).not.toHaveTextContent("Prozor (dani): 0");
  });

  it("renders explicit unavailable size-curve and controlled-markdown evidence on Assortment", async () => {
    render(
      <MemoryRouter>
        <SupplierFootwearAnalyticsPage />
      </MemoryRouter>,
    );

    const panel = await screen.findByTestId("supplier-assortment-rq532-evidence");
    expect(within(panel).getByTestId("supplier-assortment-size-curve-evidence")).toHaveTextContent(
      "Raspodela veličina po dobavljaču × tip obuće nije dostupna",
    );
    expect(within(panel).getByTestId("supplier-assortment-controlled-markdown-evidence")).toHaveTextContent(
      "Kontrolisani efekat nivelacije (DiD) nije prikazan kao uzročan uplift",
    );
    expect(panel).toHaveTextContent("recommendationAllowed=false");
    expect(panel).not.toHaveTextContent("0 RSD");
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
    expect(screen.getByText("Poništi filtere")).toBeInTheDocument();
    expect(within(await screen.findByTestId("analytics-control-bar")).getByText("Kvalitet podataka")).toBeInTheDocument();
    expect(await screen.findByText(/Prikazano:\s*1 red/i)).toBeInTheDocument();
    expect(within(screen.getByTestId("analytics-trust-summary")).getByText("Sveže")).toBeInTheDocument();
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("heading", { level: 1, name: "Dobavljači i tipovi obuće" })).toBeInTheDocument();
  });

  it("promotes type and coverage metrics in the primary KPI band", async () => {
    render(
      <MemoryRouter>
        <SupplierFootwearAnalyticsPage />
      </MemoryRouter>,
    );

    const primary = await screen.findByTestId("supplier-assortment-primary-kpis");
    expect(within(primary).getByText("Dominantan tip obuće")).toBeInTheDocument();
    expect(within(primary).getByText("Uporediva kohorta")).toBeInTheDocument();
    expect(within(primary).queryByText(/Post-prozor promet/i)).not.toBeInTheDocument();

    const secondary = screen.getByTestId("supplier-assortment-secondary-kpis");
    expect(within(secondary).getByText(/Post-prozor promet/i)).toBeInTheDocument();
  });

  it("does not derive type insights from truncated article detail", async () => {
    const baseResponse = await getVendorSalesNivelacija({});
    vi.mocked(getVendorSalesNivelacija).mockResolvedValueOnce({
      ...baseResponse,
      typeInsightsAuthoritative: false,
      categoryStats: [{
        category: "Cipele",
        articlesCount: 1,
        vendorsCount: 1,
        preQty: 1,
        preRevenue: 10,
        postQty: 2,
        postRevenue: 100,
        changeQty: 1,
        changeRevenue: 90,
        changePercent: 900,
        hasComparableSalesWindow: true,
        comparableArticleCount: 1,
        postRevenueSharePercent: 90.91,
        avgElasticity: -1.2,
      }],
      dataQuality: {
        ...baseResponse.dataQuality!,
        returnedRows: 1,
        analyzedRows: 2,
        truncatedRows: 1,
        isDetailTruncated: true,
      },
    });

    render(
      <MemoryRouter>
        <SupplierFootwearAnalyticsPage />
      </MemoryRouter>,
    );

    expect(await screen.findByText(/Detalj prikazuje 1 od 2 analiziranih redova/)).toBeInTheDocument();
    expect(screen.getByText("Nema podataka za grafikon tipova obuće.")).toBeInTheDocument();
    const tableSurface = await screen.findByTestId("supplier-footwear-analytics-data-table");
    expect(within(tableSurface).getAllByText("Nije dostupno").length).toBeGreaterThan(0);
  });

  it("uses backend full-cohort type insights in row, detail and export metadata", async () => {
    const baseResponse = await getVendorSalesNivelacija({});
    vi.mocked(getVendorSalesNivelacija).mockResolvedValueOnce({
      ...baseResponse,
      typeInsightsAuthoritative: true,
      typeInsightsSource: "full_comparable_cohort",
      typeInsightsDenominator: "comparable_post_revenue",
      vendorStats: [{
        ...baseResponse.vendorStats[0],
        primaryFootwearType: "Cipele",
        primaryFootwearTypeSharePercent: 90.91,
        primaryFootwearTypeAvgElasticity: -1.2,
        typeInsightsAuthoritative: true,
      }],
      categoryStats: [{
        category: "Cipele",
        articlesCount: 2,
        vendorsCount: 1,
        preQty: 10,
        preRevenue: 100,
        postQty: 12,
        postRevenue: 1_000,
        changeQty: 2,
        changeRevenue: 900,
        changePercent: 900,
        hasComparableSalesWindow: true,
        comparableArticleCount: 2,
        postRevenueSharePercent: 90.91,
        avgElasticity: -1.2,
      }],
      dataQuality: {
        ...baseResponse.dataQuality!,
        returnedRows: 2,
        analyzedRows: 2,
        isDetailTruncated: false,
      },
    });

    const saveSpy = vi.spyOn(analyticsTableState, "saveAnalyticsDetailSnapshot");
    render(
      <MemoryRouter>
        <SupplierFootwearAnalyticsPage />
      </MemoryRouter>,
    );

    expect(await screen.findByText(/Cipele \(90,9%\)/)).toBeInTheDocument();
    const tableSurface = await screen.findByTestId("supplier-footwear-analytics-data-table");
    expect(within(tableSurface).getByText("Cipele")).toBeInTheDocument();
    within(tableSurface).getByRole("button", { name: "Detalji" }).click();
    const detailPanel = (await screen.findByText("Glavni tip obuće")).closest("section");
    expect(detailPanel).not.toBeNull();
    expect(detailPanel).toHaveTextContent("Cipele (90,9%)");
    expect(within(detailPanel!).getByText("-1,20")).toBeInTheDocument();
    within(detailPanel!).getByRole("button", { name: "Otvori puni detalj" }).click();
    expect(saveSpy).toHaveBeenCalledWith(expect.objectContaining({
      metadata: expect.arrayContaining([
        expect.objectContaining({ key: "typeInsightDenominator", value: "comparable_post_revenue" }),
      ]),
    }));
    saveSpy.mockRestore();
  });

  it("does not infer fresh supplier footwear data from response generated time", async () => {
    const generatedOnlyResponse = await getVendorSalesNivelacija({});
    vi.mocked(getVendorSalesNivelacija).mockResolvedValueOnce({
      ...generatedOnlyResponse,
      generatedAt: "2026-08-11T10:00:00Z",
      meta: {
        ...generatedOnlyResponse.meta,
        success: true,
        lastRefreshAtUtc: null,
        warningCode: null,
        isPartial: false,
      },
    });

    render(
      <MemoryRouter>
        <SupplierFootwearAnalyticsPage />
      </MemoryRouter>
    );

    expect(within(screen.getByTestId("analytics-trust-summary")).getByText("Nije poznato")).toBeInTheDocument();
    expect(screen.queryByText("Sveže")).not.toBeInTheDocument();
  });

  it("keeps partial supplier footwear data visibly stale even with a refresh timestamp", async () => {
    const partialResponse = await getVendorSalesNivelacija({});
    vi.mocked(getVendorSalesNivelacija).mockResolvedValueOnce({
      ...partialResponse,
      meta: {
        ...partialResponse.meta,
        success: true,
        lastRefreshAtUtc: "2026-08-11T09:00:00Z",
        warningCode: "partial_payload",
        isPartial: true,
      },
    });

    render(
      <MemoryRouter>
        <SupplierFootwearAnalyticsPage />
      </MemoryRouter>
    );

    expect(await screen.findByTestId("analytics-trust-summary-availability")).toHaveTextContent("Prikaz delimičan");
    expect(within(screen.getByTestId("analytics-trust-summary")).getByText("Zastarelo")).toBeInTheDocument();
  });

  it("keeps an empty supplier footwear response distinct from an error", async () => {
    const emptyResponse = await getVendorSalesNivelacija({});
    vi.mocked(getVendorSalesNivelacija).mockResolvedValueOnce({
      ...emptyResponse,
      vendorStats: [],
      articleStats: [],
      totals: {
        ...emptyResponse.totals,
        vendorsCount: 0,
        articlesCount: 0,
        activeArticlesCount: 0,
      },
      dataQuality: {
        ...emptyResponse.dataQuality,
        rawRows: 0,
        deduplicatedRows: 0,
        analyzedRows: 0,
        inactiveRows: 0,
      },
      meta: {
        success: true,
        emptyReason: "no_data_in_period",
        dataQualityStatus: "insufficient_data",
        lastRefreshAtUtc: null,
      },
    });

    render(
      <MemoryRouter>
        <SupplierFootwearAnalyticsPage />
      </MemoryRouter>
    );

    expect(await screen.findByText("Nema podataka za izabrane filtere.")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Nema podataka za izabrani period." })).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("shows degraded comparison evidence when only the previous-period request fails", async () => {
    const currentResponse = await getVendorSalesNivelacija({});
    vi.mocked(getVendorSalesNivelacija)
      .mockResolvedValueOnce({
        ...currentResponse,
        totals: {
          ...currentResponse.totals,
          postRevenue: 1_200,
          changePercent: 20,
          hasComparableSalesWindow: true,
        },
      })
      .mockRejectedValueOnce(new Error("Prethodni period nije dostupan."));

    render(
      <MemoryRouter>
        <SupplierFootwearAnalyticsPage />
      </MemoryRouter>
    );

    expect(await screen.findByText("Post-prozor promet (uporediva kohorta)")).toBeInTheDocument();
    expect(screen.getByText("Prethodni period nije dostupan.")).toBeInTheDocument();
    expect(screen.getByText("Rast/pad u odnosu na prethodni period")).toBeInTheDocument();
    expect(screen.queryByText("+20,00%")).not.toBeInTheDocument();
    expect(screen.queryByText("20,00%")).not.toBeInTheDocument();
  });

  it("keeps a successful empty previous baseline distinct from a failed comparison", async () => {
    const currentResponse = await getVendorSalesNivelacija({});
    vi.mocked(getVendorSalesNivelacija)
      .mockResolvedValueOnce({
        ...currentResponse,
        totals: {
          ...currentResponse.totals,
          postRevenue: 1_200,
          changePercent: 20,
          hasComparableSalesWindow: true,
        },
      })
      .mockResolvedValueOnce({
        ...currentResponse,
        vendorStats: [],
        articleStats: [],
        totals: {
          ...currentResponse.totals,
          postRevenue: 0,
          changePercent: 0,
          hasComparableSalesWindow: false,
        },
      });

    render(
      <MemoryRouter>
        <SupplierFootwearAnalyticsPage />
      </MemoryRouter>
    );

    expect(await screen.findByText("Prethodni uporedivi period nema dovoljno podataka za poređenje.")).toBeInTheDocument();
    expect(screen.queryByText("Prethodni period nije dostupan.")).not.toBeInTheDocument();
    expect(screen.queryByText("+20,00%")).not.toBeInTheDocument();
  });

  it("keeps a supplier footwear fallback error unavailable and not fresh", async () => {
    vi.mocked(getVendorSalesNivelacija).mockRejectedValueOnce(new Error("Pre/post nivelacija nije dostupna."));

    render(
      <MemoryRouter>
        <SupplierFootwearAnalyticsPage />
      </MemoryRouter>
    );

    expect(await screen.findByRole("alert")).toHaveTextContent("Pre/post nivelacija nije dostupna.");
    expect(within(screen.getByTestId("analytics-trust-summary")).getByText("Nije poznato")).toBeInTheDocument();
    expect(screen.queryByText("Sveže")).not.toBeInTheDocument();
  });

  it("shows schema readiness guidance and retries without rendering fallback zero KPIs", async () => {
    const baseResponse = await getVendorSalesNivelacija({});
    vi.mocked(getVendorSalesNivelacija).mockRejectedValueOnce(new AnalyticsMetaError(
      "Pre/post nivelacija nije dostupna.",
      {
        errorCode: "vendor_sales_nivelacija_contract_missing",
        correlationId: "vendor-readiness-test",
        meta: {
          success: false,
          errorCode: "vendor_sales_nivelacija_contract_missing",
          errorMessage: "Nedostaje očekivana relacija.",
          readinessId: "vendor-sales-nivelacija-schema",
          recoveryInstruction: "Administrator može pokrenuti read-only proveru relacije i SELECT prava.",
          requestedPeriodFromUtc: "2026-09-01T00:00:00Z",
          requestedPeriodToUtc: "2026-09-30T00:00:00Z",
          requestedDataScope: "all",
          effectiveDataScope: null,
          dataScopeSource: "not_applied",
          recommendationAllowed: false,
        },
      },
    ));

    render(
      <MemoryRouter>
        <SupplierFootwearAnalyticsPage />
      </MemoryRouter>
    );

    expect(await screen.findByText("ID provere: vendor-sales-nivelacija-schema")).toBeInTheDocument();
    expect(screen.getByText("Administrator može pokrenuti read-only proveru relacije i SELECT prava.")).toBeInTheDocument();
    expect(screen.getByText(/efektivni opseg: nije primenjen/)).toBeInTheDocument();
    expect(screen.queryByText("Ukupan promet")).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Pokušaj ponovo" }));
    expect(await screen.findByText("Post-prozor promet (uporediva kohorta)")).toBeInTheDocument();
    expect(baseResponse.meta?.success).toBe(true);
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
        dataSource: "Prodaja po dobavljaču — nivelacija po tipu obuće",
        dataQualityStatus: "good",
        dataFreshnessStatus: "fresh",
        recommendationAllowed: true,
      }));
    });

    expect(screen.queryAllByRole("heading", { level: 1 })).toHaveLength(0);
    expect(screen.getByRole("region", { name: "Asortiman dobavljača" })).toBeInTheDocument();
    expect(screen.queryByText("Kontrole asortimana")).not.toBeInTheDocument();
  });

  it("keeps period suggestions in the parent-owned embedded composition", async () => {
    vi.mocked(getVendorSalesNivelacija).mockResolvedValueOnce({
      ...(await getVendorSalesNivelacija({})),
      vendorStats: [],
      articleStats: [],
    });
    vi.mocked(getVendorSalesNivelacijaOptions).mockResolvedValueOnce([
      {
        eventDate: "2026-07-01T00:00:00Z",
        label: "Jul 2026",
        hasSalesWindow: true,
      },
    ]);

    const sharedFilters = {
      periodPreset: "30d" as const,
      fromDate: "2026-07-13",
      toDate: "2026-08-11",
      dataScope: "all",
      storeId: null,
      supplierId: null,
    };

    const { unmount } = render(
      <MemoryRouter>
        <SupplierFootwearAnalyticsPage sharedFilters={sharedFilters} />
      </MemoryRouter>,
    );

    expect(await screen.findByRole("button", { name: "Primeni predlog perioda" })).toBeInTheDocument();
    unmount();

    render(
      <MemoryRouter>
        <SupplierFootwearAnalyticsPage embedded sharedFilters={sharedFilters} onTrustMetadataChange={vi.fn()} />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(screen.queryByRole("button", { name: "Primeni predlog perioda" })).not.toBeInTheDocument();
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
        avgPriceChangePercent: null,
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

    const top5Article = (await screen.findByText("Udeo top 5 dobavljača (asortiman)")).closest("article");
    expect(top5Article).not.toBeNull();
    expect(within(top5Article!).getByText("Nije dostupno")).toBeInTheDocument();

    expect(screen.getByText("Nema podataka za grafikon tipova obuće.")).toBeInTheDocument();

    const tableSurface = await screen.findByTestId("supplier-footwear-analytics-data-table");
    const vendorRow = within(tableSurface).getByText("Dobavljač 1").closest("tr");
    expect(vendorRow).not.toBeNull();
    expect(within(vendorRow!).getAllByText("Nije dostupno").length).toBeGreaterThan(0);
    expect(within(vendorRow!).getByLabelText(/Udeo Nije dostupno/i)).toBeInTheDocument();

    within(vendorRow!).getByRole("button", { name: "Detalji" }).click();

    const reliabilityArticle = (await screen.findByText("Pouzdanost signala")).closest("article");
    const confidenceArticle = screen.getByText("Poverenje preporuke").closest("article");
    expect(reliabilityArticle).not.toBeNull();
    expect(confidenceArticle).not.toBeNull();
    expect(within(reliabilityArticle!).getByText("Nije dostupno")).toBeInTheDocument();
    expect(within(confidenceArticle!).getByText("Nije dostupno")).toBeInTheDocument();
  });

  it("keeps untrusted pre/post values unavailable in table, detail, and export payloads", () => {
    const row = {
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
      postRevenueSharePercent: 0,
      avgCoveragePre30: 0,
      avgCoveragePost30: null,
      articleCount: 1,
      activeArticlesCount: 0,
      increasedPriceArticlesCount: 0,
      decreasedPriceArticlesCount: 0,
      reliabilityPct: 50,
      hasComparableSalesWindow: false,
      recommendationAllowed: false,
      sharePct: null,
      trendPct: null,
      topFootwearType: "N/A",
      topFootwearTypeSharePct: null,
      avgElasticity: null,
      confidencePct: null,
      status: "insufficient_data" as const,
      statusReason: "Nema uporedivog prozora.",
    };

    const payload = resolveAnalyticsTablePayload({
      tableKey: "dobavljaci-tipovi-obuce",
      tableTitle: "Dobavljači i tipovi obuće",
      columns: decisionColumns,
      rows: [row],
    });
    const snapshot = buildAnalyticsDetailSnapshot({
      table: "dobavljaci-tipovi-obuce",
      recordId: "1",
      title: row.vendorName,
      columns: decisionColumns,
      row,
    });

    expect(payload.rows[0].postRevenue).toBeNull();
    expect(payload.rows[0].confidencePct).toBeNull();
    expect(snapshot.fields.find((field) => field.key === "postRevenue")?.value).toBe("Nije dostupno");
    expect(snapshot.fields.find((field) => field.key === "confidencePct")?.value).toBe("Nije dostupno");
  });

  it("keeps null-ID vendors with duplicate names distinct in table, detail, and snapshot IDs", async () => {
    const recommendation = {
      status: "maintain" as const,
      label: "Zadrži",
      summary: "Stabilan signal.",
      confidencePct: 70,
      reliabilityPct: 75,
      dataQualityStatus: "good" as const,
      recommendationAllowed: true,
      reasonCodes: ["stable_margin"],
    };

    const baseResponse = await getVendorSalesNivelacija({});
    vi.mocked(getVendorSalesNivelacija).mockResolvedValueOnce({
      ...baseResponse,
      vendorStats: [
        {
          vendorId: null,
          vendorName: "Nepoznat",
          preQty: 10,
          preRevenue: 1_000,
          postQty: 12,
          postRevenue: 1_200,
          changeQty: 2,
          changeRevenue: 200,
          changePercent: 20,
          absoluteChangeRevenue: 200,
          changeSharePercent: 40,
          postRevenueSharePercent: 40,
          avgCoveragePre30: 0.8,
          avgCoveragePost30: 0.7,
          articleCount: 1,
          activeArticlesCount: 1,
          increasedPriceArticlesCount: 0,
          decreasedPriceArticlesCount: 0,
          reliabilityPct: 75,
          hasComparableSalesWindow: true,
          recommendation,
        },
        {
          vendorId: null,
          vendorName: "NEPOZNAT",
          preQty: 5,
          preRevenue: 500,
          postQty: 6,
          postRevenue: 900,
          changeQty: 1,
          changeRevenue: 400,
          changePercent: 80,
          absoluteChangeRevenue: 400,
          changeSharePercent: 60,
          postRevenueSharePercent: 60,
          avgCoveragePre30: 0.8,
          avgCoveragePost30: 0.7,
          articleCount: 1,
          activeArticlesCount: 1,
          increasedPriceArticlesCount: 0,
          decreasedPriceArticlesCount: 0,
          reliabilityPct: 70,
          hasComparableSalesWindow: true,
          recommendation,
        },
      ],
      articleStats: [],
      totals: {
        ...baseResponse.totals,
        postRevenue: 2_100,
        vendorsCount: 2,
        hasComparableSalesWindow: true,
      },
    } as never);

    const saveSpy = vi.spyOn(analyticsTableState, "saveAnalyticsDetailSnapshot");

    render(
      <MemoryRouter>
        <SupplierFootwearAnalyticsPage />
      </MemoryRouter>,
    );

    const tableSurface = await screen.findByTestId("supplier-footwear-analytics-data-table");
    expect(within(tableSurface).getByText("1.200 RSD")).toBeInTheDocument();
    expect(within(tableSurface).getByText("900 RSD")).toBeInTheDocument();

    const secondRow = within(tableSurface).getByText("900 RSD").closest("tr");
    expect(secondRow).not.toBeNull();
    within(secondRow!).getByRole("button", { name: "Detalji" }).click();

    const detailSection = await screen.findByText("Detalj odluke: NEPOZNAT");
    const detailPanel = detailSection.closest("section");
    expect(detailPanel).not.toBeNull();
    expect(within(detailPanel!).getByText("900 RSD")).toBeInTheDocument();
    expect(within(detailPanel!).queryByText("1.200 RSD")).not.toBeInTheDocument();
    expect(within(detailPanel!).getByText(/Identitet dobavljača nije potvrđen/)).toBeInTheDocument();

    within(detailPanel!).getByRole("button", { name: "Otvori puni detalj" }).click();
    expect(saveSpy).toHaveBeenCalledWith(expect.objectContaining({
      recordId: "row:1",
    }));
    const snapshot = saveSpy.mock.calls.at(-1)?.[0];
    expect(snapshot?.fields).toEqual(expect.arrayContaining([
      expect.objectContaining({ key: "preRevenue", value: "500 RSD" }),
      expect.objectContaining({ key: "postRevenue", value: "900 RSD" }),
      expect.objectContaining({ key: "preQty", value: "5" }),
      expect.objectContaining({ key: "postQty", value: "6" }),
      expect.objectContaining({ key: "avgCoveragePre30", value: "80,00%" }),
      expect.objectContaining({ key: "hasComparableSalesWindow", value: "Da" }),
    ]));

    saveSpy.mockRestore();
  });

  it("keeps blank-name null-ID vendors distinct and does not merge their type insights", async () => {
    const recommendation = {
      status: "maintain" as const,
      label: "Zadrži",
      summary: "Stabilan signal.",
      confidencePct: 70,
      reliabilityPct: 75,
      dataQualityStatus: "good" as const,
      recommendationAllowed: true,
      reasonCodes: ["stable_margin"],
    };
    const comparableVendor = {
      vendorId: null,
      vendorName: "",
      preQty: 10,
      preRevenue: 1_000,
      postQty: 12,
      postRevenue: 1_200,
      changeQty: 2,
      changeRevenue: 200,
      changePercent: 20,
      absoluteChangeRevenue: 200,
      changeSharePercent: 50,
      postRevenueSharePercent: 50,
      avgCoveragePre30: 0.8,
      avgCoveragePost30: 0.7,
      articleCount: 1,
      activeArticlesCount: 1,
      increasedPriceArticlesCount: 0,
      decreasedPriceArticlesCount: 0,
      reliabilityPct: 75,
      hasComparableSalesWindow: true,
      recommendation,
    };

    const baseResponse = await getVendorSalesNivelacija({});
    vi.mocked(getVendorSalesNivelacija).mockResolvedValueOnce({
      ...baseResponse,
      vendorStats: [
        comparableVendor,
        { ...comparableVendor, postRevenue: 800, changePercent: 10 },
      ],
      articleStats: [
        {
          eventDate: "2026-08-01T00:00:00Z",
          vendorId: null,
          vendorName: "",
          sku: "SKU-A",
          articleName: "Patika A",
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
          hasComparableSalesWindow: true,
          priceChanged: true,
          priceChangePercent: 20,
        },
        {
          eventDate: "2026-08-01T00:00:00Z",
          vendorId: null,
          vendorName: "",
          sku: "SKU-B",
          articleName: "Čizma B",
          category: "Čizme",
          oldPrice: 80,
          newPrice: 90,
          preQty: 4,
          preRevenue: 400,
          postQty: 5,
          postRevenue: 800,
          changeQty: 1,
          changeRevenue: 400,
          changePercent: 100,
          coveragePre30: 0.8,
          coveragePost30: 0.7,
          hasSalesWindow: true,
          hasComparableSalesWindow: true,
          priceChanged: true,
          priceChangePercent: 12,
        },
      ],
      totals: {
        ...baseResponse.totals,
        postRevenue: 2_000,
        vendorsCount: 2,
        hasComparableSalesWindow: true,
      },
    } as never);

    render(
      <MemoryRouter>
        <SupplierFootwearAnalyticsPage />
      </MemoryRouter>,
    );

    const tableSurface = await screen.findByTestId("supplier-footwear-analytics-data-table");
    const unknownRows = within(tableSurface).getAllByText("Nepoznat dobavljač");
    expect(unknownRows).toHaveLength(2);
    expect(within(tableSurface).getAllByText("Nije dostupno").length).toBeGreaterThanOrEqual(2);

    within(unknownRows[0].closest("tr")!).getByRole("button", { name: "Detalji" }).click();
    expect(await screen.findByText("Detalj odluke: Nepoznat dobavljač")).toBeInTheDocument();
    expect(screen.getByText(/Identitet dobavljača nije potvrđen/)).toBeInTheDocument();
  });

  it("renders an unknown change percent as unavailable, not 0%, for a mature vendor without baseline", async () => {
    const baseResponse = await getVendorSalesNivelacija({});
    const [baseVendor] = baseResponse.vendorStats;
    vi.mocked(getVendorSalesNivelacija).mockResolvedValueOnce({
      ...baseResponse,
      vendorStats: [{
        ...baseVendor,
        preQty: 0,
        preRevenue: 0,
        changeQty: 12,
        changeRevenue: 1_200,
        changePercent: null,
        semanticChangePercentRevenue: null,
        hasRevenueBaseline: false,
        revenueBaselineReason: "no_pre_revenue_baseline_uplift",
        hasComparableSalesWindow: true,
        recommendation: {
          ...baseVendor.recommendation!,
          status: "insufficient_data",
          label: "Nedovoljno podataka",
          summary: "Nema validne prethodne baze prometa za procenu efekta posle promene cene.",
          reasonCodes: ["no_revenue_baseline"],
        },
      }],
    });

    render(
      <MemoryRouter>
        <SupplierFootwearAnalyticsPage />
      </MemoryRouter>,
    );

    const tableSurface = await screen.findByTestId("supplier-footwear-analytics-data-table");
    const vendorRow = within(tableSurface).getByText("Dobavljač 1").closest("tr");
    expect(vendorRow).not.toBeNull();
    const trendCell = within(vendorRow!).getAllByRole("cell")[4];
    expect(trendCell).toHaveTextContent("Nije dostupno");
    expect(trendCell.textContent).not.toMatch(/0[,.]00\s*%/);
    expect(trendCell).toHaveTextContent("Nova osnova; procenat promene nije uporediv");
  });

  it("renders assortment price-change effect labels instead of Supplier overview PoP copy", async () => {
    render(
      <MemoryRouter>
        <SupplierFootwearAnalyticsPage />
      </MemoryRouter>,
    );

    const tableSurface = await screen.findByTestId("supplier-footwear-analytics-data-table");
    expect(within(tableSurface).getByText("Efekat pozitivan")).toBeInTheDocument();
    expect(within(tableSurface).queryByText("Pojačaj fokus")).not.toBeInTheDocument();
    expect(within(tableSurface).queryByText("PoP trend")).not.toBeInTheDocument();
  });
});

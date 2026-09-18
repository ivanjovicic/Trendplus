import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import type { ReactNode } from "react";
import { MemoryRouter, Route, Routes, useNavigate, useParams } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import ProdajaPrePostNivelacijePage from "./ProdajaPrePostNivelacijePage";
import { getStores } from "../services/analyticsApi";
import * as analyticsTableState from "../services/analyticsTableState";
import { getAnalyticsDetailSnapshot } from "../services/analyticsTableState";
import { getDobavljaci } from "../services/dobavljaciApi";
import { getVendorSalesNivelacija } from "../services/vendorSalesNivelacijaApi";
import type {
  VendorSalesNivelacijaArticleStat,
  VendorSalesNivelacijaResponse,
  VendorSalesNivelacijaVendorStat,
} from "../services/vendorSalesNivelacijaApi";

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
  default: ({ title, dataSource }: { title?: string; dataSource?: string | null }) => (
    <div data-testid="analytics-trust-header">
      <h1>{title}</h1>
      <span>{dataSource}</span>
    </div>
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
    hasComparableSalesWindow: true,
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

function article(overrides: Partial<VendorSalesNivelacijaArticleStat> = {}): VendorSalesNivelacijaArticleStat {
  return {
    eventDate: "2026-06-01T00:00:00Z",
    vendorId: 10,
    vendorName: "Vendor A",
    sku: "SKU-1",
    articleName: "Patika 1",
    category: "Patike",
    oldPrice: 100,
    newPrice: 120,
    preQty: 10,
    preRevenue: 1000,
    postQty: 12,
    postRevenue: 1200,
    changeQty: 2,
    changeRevenue: 200,
    changePercent: 20,
    coveragePre30: 0.8,
    coveragePost30: 0.7,
    hasSalesWindow: true,
    hasComparableSalesWindow: true,
    priceChanged: true,
    priceChangePercent: 20,
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
      hasComparableSalesWindow: true,
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

function PrePostDetailRouteStub() {
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();

  return (
    <section data-testid="pre-post-detail-route">
      <p>Pre/Post detail route</p>
      <span data-testid="pre-post-detail-id">{id}</span>
      <button type="button" onClick={() => navigate(-1)}>Nazad na pre/post</button>
    </section>
  );
}

function renderPage(initialEntries = ["/analitika/nivelacije-pre-post"]) {
  return render(
    <MemoryRouter initialEntries={initialEntries}>
      <Routes>
        <Route path="/analitika/nivelacije-pre-post" element={<ProdajaPrePostNivelacijePage />} />
        <Route path="/analitika/nivelacije-pre-post/:id" element={<PrePostDetailRouteStub />} />
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

    const controlBar = await screen.findByTestId("analytics-control-bar");
    expect(within(controlBar).getByRole("heading", { name: "Kontrole i opseg" })).toBeInTheDocument();
    expect(within(controlBar).getByLabelText("Period")).toBeInTheDocument();
    expect(within(controlBar).getByLabelText("Od")).toBeInTheDocument();
    expect(within(controlBar).getByLabelText("Do")).toBeInTheDocument();
    expect(within(controlBar).getByLabelText("Dobavljač")).toBeInTheDocument();
    expect(within(controlBar).getByLabelText("Kategorija")).toBeInTheDocument();
    expect(within(controlBar).getByLabelText("Objekat")).toBeInTheDocument();
    expect(within(controlBar).getByRole("link", { name: "Kvalitet podataka" })).toHaveAttribute(
      "href",
      "/analytics/data-quality",
    );

    await waitFor(() => {
      expect(screen.getByTestId("prodaja-pre-post-nivelacije-data-table")).toBeInTheDocument();
    });

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

  it("keeps the trust header as the only page-level h1", async () => {
    renderPage();

    await screen.findByText("Prioritetna lista dobavljača");

    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    expect(screen.getByRole("heading", { level: 1, name: "Prodaja pre/posle nivelacije" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 2, name: "Prodaja pre/posle nivelacije" })).toBeInTheDocument();
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

  it("does not treat missing reliability as a weak Nisko signal", async () => {
    vi.mocked(getVendorSalesNivelacija).mockResolvedValue(
      response({
        vendorStats: [
          vendor({
            reliabilityPct: null as unknown as number,
            recommendation: {
              status: "review",
              label: "Pregled",
              summary: "Reliability signal nije dostupan.",
              confidencePct: 61,
              reliabilityPct: null as unknown as number,
              dataQualityStatus: "warning",
              reasonCodes: ["missing_cost"],
            },
          }),
        ],
      }),
    );

    renderPage();
    await screen.findByText("Prioritetna lista dobavljača");

    expect(await screen.findByText(/Nije dostupno/)).toBeInTheDocument();
    expect(screen.queryByText(/Nisko signal/)).not.toBeInTheDocument();
  });

  it("keeps null-ID vendors with duplicate names distinct in detail snapshots and routes", async () => {
    vi.mocked(getVendorSalesNivelacija).mockResolvedValue(
      response({
        vendorStats: [
          vendor({ vendorId: null, vendorName: "Nepoznat", postRevenue: 1200, changeRevenue: 200 }),
          vendor({ vendorId: null, vendorName: "NEPOZNAT", postRevenue: 900, changeRevenue: 400 }),
        ],
        totals: {
          ...response().totals,
          postRevenue: 2100,
          vendorsCount: 2,
        },
      }),
    );

    const saveSpy = vi.spyOn(analyticsTableState, "saveAnalyticsDetailSnapshot");
    renderPage();
    const table = await screen.findByTestId("prodaja-pre-post-nivelacije-data-table");

    const secondRow = within(table).getByText("900 RSD").closest("tr");
    expect(secondRow).not.toBeNull();
    fireEvent.click(within(secondRow!).getAllByRole("button", { name: "Detalji" })[0]);

    expect(await screen.findByText(/Identitet dobavljača nije potvrđen/i)).toBeInTheDocument();
    expect(within(screen.getByText(/Detalj odluke: NEPOZNAT/i).closest("section") as HTMLElement).getByText("900 RSD")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Otvori puni detalj" }));
    expect(await screen.findByText("Pre/Post detail route")).toBeInTheDocument();
    expect(saveSpy).toHaveBeenLastCalledWith(expect.objectContaining({ recordId: "row:1" }));
    expect(getAnalyticsDetailSnapshot("nivelacije-pre-post", "row:1")?.fields.find((field) => field.key === "postRevenue")?.value).toBe("900 RSD");

    saveSpy.mockRestore();
  });

  it("keeps blank-name null-ID vendors distinct with fallback labels and row keys", async () => {
    vi.mocked(getVendorSalesNivelacija).mockResolvedValue(
      response({
        vendorStats: [
          vendor({ vendorId: null, vendorName: "", postRevenue: 1200, changeRevenue: 200 }),
          vendor({ vendorId: null, vendorName: "   ", postRevenue: 900, changeRevenue: 400 }),
        ],
        totals: {
          ...response().totals,
          postRevenue: 2100,
          vendorsCount: 2,
        },
      }),
    );

    const saveSpy = vi.spyOn(analyticsTableState, "saveAnalyticsDetailSnapshot");
    renderPage();
    const table = await screen.findByTestId("prodaja-pre-post-nivelacije-data-table");

    expect(within(table).getAllByText("Nepoznat dobavljač")).toHaveLength(2);
    expect(within(table).getByText("1.200 RSD")).toBeInTheDocument();
    expect(within(table).getByText("900 RSD")).toBeInTheDocument();

    const secondRow = within(table).getByText("900 RSD").closest("tr");
    expect(secondRow).not.toBeNull();
    fireEvent.click(within(secondRow!).getAllByRole("button", { name: "Detalji" })[0]);

    expect(await screen.findByText(/Identitet dobavljača nije potvrđen/i)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Otvori puni detalj" }));
    expect(await screen.findByText("Pre/Post detail route")).toBeInTheDocument();
    expect(saveSpy).toHaveBeenLastCalledWith(expect.objectContaining({ recordId: "row:1" }));

    saveSpy.mockRestore();
  });

  it("does not collapse duplicate vendor IDs into one detail snapshot", async () => {
    vi.mocked(getVendorSalesNivelacija).mockResolvedValue(
      response({
        vendorStats: [
          vendor({ vendorId: 10, vendorName: "Vendor A", postRevenue: 100000, changeRevenue: 20000 }),
          vendor({ vendorId: 10, vendorName: "Vendor A magacin", postRevenue: 50000, changeRevenue: 10000 }),
        ],
        totals: {
          ...response().totals,
          postRevenue: 150000,
          vendorsCount: 2,
        },
      }),
    );

    const saveSpy = vi.spyOn(analyticsTableState, "saveAnalyticsDetailSnapshot");
    renderPage();
    const table = await screen.findByTestId("prodaja-pre-post-nivelacije-data-table");

    const secondRow = within(table).getByText("Vendor A magacin").closest("tr");
    expect(secondRow).not.toBeNull();
    fireEvent.click(within(secondRow!).getAllByRole("button", { name: "Detalji" })[0]);

    expect(await screen.findByText(/Identitet dobavljača nije potvrđen/i)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Otvori puni detalj" }));
    expect(await screen.findByText("Pre/Post detail route")).toBeInTheDocument();
    expect(saveSpy).toHaveBeenLastCalledWith(expect.objectContaining({ recordId: "row:1", title: "Vendor A magacin" }));

    saveSpy.mockRestore();
  });

  it("keeps special-character vendor names collision-safe for detail routes", async () => {
    vi.mocked(getVendorSalesNivelacija).mockResolvedValue(
      response({
        vendorStats: [
          vendor({ vendorId: null, vendorName: "A/B & Co.", postRevenue: 1200, changeRevenue: 200 }),
          vendor({ vendorId: null, vendorName: "A/B & Co", postRevenue: 900, changeRevenue: 400 }),
        ],
        totals: {
          ...response().totals,
          postRevenue: 2100,
          vendorsCount: 2,
        },
      }),
    );

    const saveSpy = vi.spyOn(analyticsTableState, "saveAnalyticsDetailSnapshot");
    renderPage();
    const table = await screen.findByTestId("prodaja-pre-post-nivelacije-data-table");

    const secondRow = within(table).getByText("A/B & Co").closest("tr");
    expect(secondRow).not.toBeNull();
    fireEvent.click(within(secondRow!).getAllByRole("button", { name: "Detalji" })[0]);
    fireEvent.click(screen.getByRole("button", { name: "Otvori puni detalj" }));

    expect(await screen.findByText("Pre/Post detail route")).toBeInTheDocument();
    expect(saveSpy).toHaveBeenLastCalledWith(expect.objectContaining({ recordId: "row:1", title: "A/B & Co" }));
    expect(getAnalyticsDetailSnapshot("nivelacije-pre-post", "row:1")?.title).toBe("A/B & Co");

    saveSpy.mockRestore();
  });

  it("shows shared filtered-out empty state when focus chips hide every vendor row", async () => {
    vi.mocked(getVendorSalesNivelacija).mockResolvedValue(
      response({
        vendorStats: [
          vendor({
            vendorId: 11,
            vendorName: "Vendor B",
            postRevenue: 50000,
            changeRevenue: 10000,
            recommendation: {
              status: "review",
              label: "Review",
              summary: "Signal za proveru.",
              confidencePct: 64,
              reliabilityPct: 61,
              dataQualityStatus: "warning",
              reasonCodes: ["review_signal"],
            },
          }),
        ],
        totals: {
          ...response().totals,
          postRevenue: 50000,
          vendorsCount: 1,
        },
      }),
    );

    renderPage();
    await screen.findByText("Prioritetna lista dobavljača");

    fireEvent.click(screen.getByRole("button", { name: /Pojacaj/i }));

    expect(await screen.findByRole("heading", { name: "Nema rezultata za trenutne filtere." })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Vrati prikaz svih dobavljača." })).toBeInTheDocument();
    expect(screen.queryByTestId("prodaja-pre-post-nivelacije-data-table")).not.toBeInTheDocument();
    expect(screen.queryByText("Vendor B")).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Vrati prikaz svih dobavljača." }));
    expect(await screen.findByTestId("prodaja-pre-post-nivelacije-data-table")).toBeInTheDocument();
    expect(screen.getByText("Vendor B")).toBeInTheDocument();
  });

  it("hides inline detail when the active focus filter excludes the selected row", async () => {
    vi.mocked(getVendorSalesNivelacija).mockResolvedValue(
      response({
        vendorStats: [
          vendor({ vendorId: 10, vendorName: "Vendor A", recommendation: { status: "increase_focus", label: "Increase focus", summary: "Jak signal.", confidencePct: 85, reliabilityPct: 80, dataQualityStatus: "good", reasonCodes: [] } }),
          vendor({ vendorId: 11, vendorName: "Vendor B", postRevenue: 50000, changeRevenue: 10000, recommendation: { status: "review", label: "Review", summary: "Signal za proveru.", confidencePct: 64, reliabilityPct: 61, dataQualityStatus: "warning", reasonCodes: ["review_signal"] } }),
        ],
        totals: {
          ...response().totals,
          postRevenue: 150000,
          vendorsCount: 2,
        },
      }),
    );

    renderPage();
    const table = await screen.findByTestId("prodaja-pre-post-nivelacije-data-table");
    const reviewRow = within(table).getByText("Vendor B").closest("tr");
    fireEvent.click(within(reviewRow!).getAllByRole("button", { name: "Detalji" })[0]);
    expect(await screen.findByText(/Detalj odluke: Vendor B/i)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /Pojacaj/i }));
    expect(screen.queryByText(/Detalj odluke: Vendor B/i)).not.toBeInTheDocument();
  });

  it("collapses inline detail when Sakrij is clicked", async () => {
    renderPage();
    await screen.findByText("Prioritetna lista dobavljača");

    fireEvent.click(screen.getAllByRole("button", { name: "Detalji" })[0]);
    expect(await screen.findByText(/Detalj odluke: Vendor A/i)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Sakrij" }));
    expect(screen.queryByText(/Detalj odluke: Vendor A/i)).not.toBeInTheDocument();
  });

  it("keeps detail navigation aligned with the production analitika route contract", async () => {
    renderPage();
    await screen.findByText("Prioritetna lista dobavljača");

    fireEvent.click(screen.getAllByRole("button", { name: "Detalji" })[0]);
    fireEvent.click(screen.getByRole("button", { name: "Otvori puni detalj" }));

    expect(await screen.findByText("Pre/Post detail route")).toBeInTheDocument();
    expect(getAnalyticsDetailSnapshot("nivelacije-pre-post", "10")).not.toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Nazad na pre/post" }));
    expect(await screen.findByText("Prioritetna lista dobavljača")).toBeInTheDocument();
  });

  it("matches the production detail path on direct navigation", () => {
    renderPage(["/analitika/nivelacije-pre-post/10"]);

    expect(screen.getByTestId("pre-post-detail-route")).toHaveTextContent("10");
  });

  it("labels absolute-change share explicitly in detail and export snapshot", async () => {
    renderPage();
    await screen.findByText("Prioritetna lista dobavljača");

    fireEvent.click(screen.getAllByRole("button", { name: "Detalji" })[0]);

    expect(screen.getByText(/Napomena o udelu promene/i)).toBeInTheDocument();
    expect(screen.getAllByText(/abs\(promena prometa\) \/ zbir apsolutnih promena prometa/i).length).toBeGreaterThanOrEqual(2);

    fireEvent.click(screen.getByRole("button", { name: "Otvori puni detalj" }));
    expect(await screen.findByText("Pre/Post detail route")).toBeInTheDocument();

    const snapshot = getAnalyticsDetailSnapshot("nivelacije-pre-post", "10");
    expect(snapshot).toEqual(expect.objectContaining({
      table: "nivelacije-pre-post",
      recordId: "10",
      title: "Vendor A",
    }));
    expect(snapshot?.fields.some((field) => field.key === "absoluteChangeSharePct" && field.label === "Udeo u apsolutnoj promeni %")).toBe(true);
    expect(snapshot?.metadata.some((meta) => meta.key === "absoluteChangeShareFormula" && meta.value.includes("apsolutnih promena"))).toBe(true);
  });

  it("does not reconstruct absolute-change share when the backend aggregate is unavailable", async () => {
    const base = response();
    vi.mocked(getVendorSalesNivelacija).mockResolvedValue(
      response({
        vendorStats: [vendor({ changeSharePercent: null as unknown as number })],
        totals: { ...base.totals, absoluteChangeRevenue: null as unknown as number },
      }),
    );

    renderPage();
    await screen.findByText("Prioritetna lista dobavljača");

    const table = await screen.findByTestId("prodaja-pre-post-nivelacije-data-table");
    const vendorRow = within(table).getByText("Vendor A").closest("tr");
    expect(vendorRow).not.toBeNull();
    expect(within(vendorRow!).getByText("Nije dostupno")).toBeInTheDocument();
    expect(within(vendorRow!).queryByText("100,00%")).not.toBeInTheDocument();
  });

  it("keeps non-finite backend aggregate and share values unavailable", async () => {
    const base = response();
    vi.mocked(getVendorSalesNivelacija).mockResolvedValue(
      response({
        vendorStats: [vendor({ changeSharePercent: Number.NaN as unknown as number })],
        totals: { ...base.totals, absoluteChangeRevenue: Number.NaN as unknown as number },
      }),
    );

    renderPage();
    await screen.findByText("Prioritetna lista dobavljača");

    const table = await screen.findByTestId("prodaja-pre-post-nivelacije-data-table");
    const vendorRow = within(table).getByText("Vendor A").closest("tr");
    expect(vendorRow).not.toBeNull();
    expect(within(vendorRow!).getByText("Nije dostupno")).toBeInTheDocument();
    expect(within(vendorRow!).queryByText("100,00%")).not.toBeInTheDocument();
    expect(screen.getByText("Top 5 udeo u promeni").parentElement).toHaveTextContent("N/A");
  });

  it("shows backend reliability percent instead of a local Visoko band", async () => {
    renderPage();
    await screen.findByText("Prioritetna lista dobavljača");

    expect(await screen.findByText(/80% signal/)).toBeInTheDocument();
    expect(screen.queryByText(/Visoko signal/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Srednje signal/)).not.toBeInTheDocument();
  });

  it("keeps missing toolbar metadata unavailable in detail snapshots instead of zero or OK", async () => {
    const base = response();
    vi.mocked(getVendorSalesNivelacija).mockResolvedValue(
      response({
        windowDays: undefined as unknown as number,
        metricsStatus: null,
        totals: {
          ...base.totals,
          vendorsCount: undefined as unknown as number,
          articlesCount: null as unknown as number,
        },
      }),
    );

    renderPage();
    await screen.findByText("Prioritetna lista dobavljača");
    expect(screen.getByText("Analiza poredjena po nivelacionom prozoru: prozor nije dostupan.")).toBeInTheDocument();

    fireEvent.click(screen.getAllByRole("button", { name: "Detalji" })[0]);
    fireEvent.click(screen.getByRole("button", { name: "Otvori puni detalj" }));

    const snapshot = getAnalyticsDetailSnapshot("nivelacije-pre-post", "10");
    expect(snapshot?.metadata).toEqual(expect.arrayContaining([
      expect.objectContaining({ key: "vendorsCount", label: "Dobavljača", value: "N/A" }),
      expect.objectContaining({ key: "articlesCount", label: "Artikala", value: "N/A" }),
      expect.objectContaining({ key: "windowDays", label: "Prozor analize", value: "N/A" }),
      expect.objectContaining({ key: "metricsStatus", label: "Status metrika", value: "N/A" }),
    ]));
  });

  it("preserves measured zero counts and authoritative OK status in detail snapshots", async () => {
    const base = response();
    vi.mocked(getVendorSalesNivelacija).mockResolvedValue(
      response({
        windowDays: 0,
        metricsStatus: "OK",
        totals: {
          ...base.totals,
          vendorsCount: 0,
          articlesCount: 0,
        },
      }),
    );

    renderPage();
    await screen.findByText("Prioritetna lista dobavljača");
    expect(screen.getByText("Analiza poredjena po nivelacionom prozoru od 0 dana.")).toBeInTheDocument();

    fireEvent.click(screen.getAllByRole("button", { name: "Detalji" })[0]);
    fireEvent.click(screen.getByRole("button", { name: "Otvori puni detalj" }));

    const snapshot = getAnalyticsDetailSnapshot("nivelacije-pre-post", "10");
    expect(snapshot?.metadata).toEqual(expect.arrayContaining([
      expect.objectContaining({ key: "vendorsCount", label: "Dobavljača", value: "0" }),
      expect.objectContaining({ key: "articlesCount", label: "Artikala", value: "0" }),
      expect.objectContaining({ key: "windowDays", label: "Prozor analize", value: "0" }),
      expect.objectContaining({ key: "metricsStatus", label: "Status metrika", value: "OK" }),
    ]));
  });

  it("uses the canonical unknown copy for missing quality metadata in snapshots", async () => {
    vi.mocked(getVendorSalesNivelacija).mockResolvedValue(
      response({
        dataQuality: null,
        meta: {
          success: true,
          dataQualityStatus: "warning",
          warningCode: "schema_fallback",
          isPartial: true,
        } as VendorSalesNivelacijaResponse["meta"],
      }),
    );

    renderPage();
    await screen.findByText("Prioritetna lista dobavljača");

    fireEvent.click(screen.getAllByRole("button", { name: "Detalji" })[0]);
    fireEvent.click(screen.getByRole("button", { name: "Otvori puni detalj" }));

    const snapshot = getAnalyticsDetailSnapshot("nivelacije-pre-post", "10");
    expect(snapshot?.metadata).toEqual(expect.arrayContaining([
      expect.objectContaining({ key: "dataTrust", label: "Poverenje", value: "Nepoznato" }),
      expect.objectContaining({ key: "analyzedShare", label: "Analizirani redovi", value: "Nije dostupno" }),
      expect.objectContaining({ key: "duplicateRowsRemoved", label: "Duplicati uklonjeni", value: "N/A" }),
      expect.objectContaining({ key: "inactiveRows", label: "Neaktivni redovi", value: "N/A" }),
    ]));
  });

  it("keeps a missing quality snapshot unknown across the trust surface", async () => {
    vi.mocked(getVendorSalesNivelacija).mockResolvedValue(
      response({
        dataQuality: null,
        meta: {
          success: true,
          dataQualityStatus: "warning",
          warningCode: "schema_fallback",
          isPartial: true,
        } as VendorSalesNivelacijaResponse["meta"],
      }),
    );

    renderPage();
    await screen.findByText("Prioritetna lista dobavljača");

    const trustButton = await screen.findByRole("button", { name: /Kvalitet signala: Nepoznato/i });
    expect(trustButton).toBeInTheDocument();
    fireEvent.click(trustButton);
    expect(screen.getByText("Kvalitet signala nije potvrđen jer snapshot kvaliteta nedostaje ili je delimičan.")).toBeInTheDocument();

    fireEvent.click(screen.getAllByRole("button", { name: "Detalji" })[0]);
    expect(screen.getByText("Kvalitet signala nije potvrđen jer snapshot kvaliteta nedostaje ili je delimičan.")).toBeInTheDocument();
  });

  it("keeps non-finite quality metadata unknown rather than healthy", async () => {
    const validDataQuality = response().dataQuality!;
    vi.mocked(getVendorSalesNivelacija).mockResolvedValue(
      response({
        dataQuality: {
          ...validDataQuality,
          analyzedSharePercent: Number.NaN,
        },
      }),
    );

    renderPage();
    await screen.findByText("Prioritetna lista dobavljača");

    expect(await screen.findByRole("button", { name: /Kvalitet signala: Nepoznato/i })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Kvalitet signala: Visoko poverenje/i })).not.toBeInTheDocument();

    fireEvent.click(screen.getAllByRole("button", { name: "Detalji" })[0]);
    fireEvent.click(screen.getByRole("button", { name: "Otvori puni detalj" }));
    const snapshot = getAnalyticsDetailSnapshot("nivelacije-pre-post", "10");
    expect(snapshot?.metadata).toEqual(expect.arrayContaining([
      expect.objectContaining({ key: "dataTrust", label: "Poverenje", value: "Nepoznato" }),
      expect.objectContaining({ key: "analyzedShare", label: "Analizirani redovi", value: "Nije dostupno" }),
      expect.objectContaining({ key: "duplicateRowsRemoved", label: "Duplicati uklonjeni", value: "N/A" }),
    ]));
  });

  it("does not render legacy zero placeholders when comparability evidence is missing", async () => {
    vi.mocked(getVendorSalesNivelacija).mockResolvedValue(
      response({
        vendorStats: [vendor({ hasComparableSalesWindow: undefined })],
        totals: { ...response().totals, hasComparableSalesWindow: undefined },
      }),
    );

    renderPage();
    await screen.findByText("Prioritetna lista dobavljača");

    const table = await screen.findByTestId("prodaja-pre-post-nivelacije-data-table");
    const vendorRow = within(table).getByText("Vendor A").closest("tr");
    expect(vendorRow).not.toBeNull();
    expect(within(vendorRow!).getAllByText("N/A").length).toBeGreaterThanOrEqual(2);
    expect(screen.queryByText("100.000 RSD")).not.toBeInTheDocument();
  });

  it("shows an error alert and hides KPI cards when the vendor sales load fails", async () => {
    vi.mocked(getVendorSalesNivelacija).mockRejectedValue(new Error("Vendor sales API timeout"));

    renderPage();

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Vendor sales API timeout");
    expect(document.querySelector(".ppn-decision-kpis")).toBeNull();
    expect(screen.queryByText(/Nisko signal/)).not.toBeInTheDocument();
    expect(screen.queryByText("Post-window promet posle nivelacije")).not.toBeInTheDocument();
  });

  it("shows unavailable driver revenue instead of fake zero RSD for non-finite change metrics", async () => {
    vi.mocked(getVendorSalesNivelacija).mockResolvedValue(
      response({
        articleStats: [
          article({
            changeRevenue: Number.NaN as unknown as number,
          }),
        ],
      }),
    );

    renderPage();
    await screen.findByText("Prioritetna lista dobavljača");
    fireEvent.click(screen.getAllByRole("button", { name: "Detalji" })[0]);

    expect(await screen.findByText("Top dobitnik SKU")).toBeInTheDocument();
    const driverGrid = screen.getByText("Top dobitnik SKU").closest(".ppn-driver-grid");
    expect(driverGrid).not.toBeNull();
    expect(within(driverGrid!).getAllByText("N/A").length).toBeGreaterThanOrEqual(2);
    expect(within(driverGrid!).queryByText("0 RSD")).not.toBeInTheDocument();
  });

  it("keeps measured zero revenue visible in driver summary when comparability is confirmed", async () => {
    vi.mocked(getVendorSalesNivelacija).mockResolvedValue(
      response({
        articleStats: [
          article({
            sku: "SKU-ZERO",
            articleName: "Patika nula",
            changeRevenue: 0,
          }),
        ],
      }),
    );

    renderPage();
    await screen.findByText("Prioritetna lista dobavljača");
    fireEvent.click(screen.getAllByRole("button", { name: "Detalji" })[0]);

    expect(await screen.findByText("Top dobitnik SKU")).toBeInTheDocument();
    const winnerCard = screen.getByText("Top dobitnik SKU").closest("article");
    expect(winnerCard).not.toBeNull();
    expect(within(winnerCard!).getByText("SKU-ZERO • Patika nula")).toBeInTheDocument();
    expect(within(winnerCard!).getByText("0 RSD")).toBeInTheDocument();
  });
});

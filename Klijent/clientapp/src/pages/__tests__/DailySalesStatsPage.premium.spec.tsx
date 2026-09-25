import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import type { ReactNode } from "react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import DailySalesStatsPage, { buildSupplierConcentration } from "../DailySalesStatsPage";
import { getStores } from "../../services/analyticsApi";
import { getDailySalesStats } from "../../services/dailySalesStatsApi";
import type { DailySalesTableResponse } from "../../services/dailySalesStatsApi";

vi.mock("recharts", () => ({
  Bar: () => null,
  BarChart: ({ children, data }: { children?: ReactNode; data?: Array<{ date?: string }> }) => (
    <div data-testid="bar-chart" data-order={data?.map((item) => item.date ?? "").join(",")}>{children}</div>
  ),
  CartesianGrid: () => null,
  ComposedChart: ({ children }: { children?: ReactNode }) => <div data-testid="composed-chart">{children}</div>,
  Legend: () => null,
  Line: () => null,
  LineChart: ({ children, data }: { children?: ReactNode; data?: Array<{ date?: string }> }) => (
    <div data-testid="line-chart" data-order={data?.map((item) => item.date ?? "").join(",")}>{children}</div>
  ),
  ResponsiveContainer: ({ children }: { children?: ReactNode }) => <div data-testid="responsive-container">{children}</div>,
  Tooltip: () => null,
  XAxis: () => null,
  YAxis: () => null,
}));

vi.mock("../../components/analytics/AnalyticsTrustHeader", () => ({
  default: ({
    title,
    lastRefreshAt,
    dataFreshnessStatus,
    dataQualityStatus,
    isPartial,
    emptyStateReason,
    dataSource,
  }: {
    title: string;
    lastRefreshAt?: string | null;
    dataFreshnessStatus?: string | null;
    dataQualityStatus?: string | null;
    isPartial?: boolean;
    emptyStateReason?: string | null;
    dataSource?: string | null;
  }) => (
    <div
      data-testid="analytics-trust-header"
      data-last-refresh-at={lastRefreshAt ?? ""}
      data-freshness={dataFreshnessStatus ?? ""}
      data-quality={dataQualityStatus ?? ""}
      data-partial={isPartial ? "true" : "false"}
      data-empty-reason={emptyStateReason ?? ""}
      data-source={dataSource ?? ""}
    >
      {title}
    </div>
  ),
}));

vi.mock("../../components/ui/InfoTip", () => ({
  default: ({ text }: { text: string }) => <span data-testid="info-tip">{text}</span>,
}));

vi.mock("../../services/analyticsApi", async () => {
  const actual = await vi.importActual<typeof import("../../services/analyticsApi")>("../../services/analyticsApi");
  return {
    ...actual,
    getStores: vi.fn(),
  };
});

vi.mock("../../services/dailySalesStatsApi", () => ({
  getDailySalesStats: vi.fn(),
}));

function LocationProbe() {
  const location = useLocation();
  return <output data-testid="location-search">{location.search}</output>;
}

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((nextResolve) => {
    resolve = nextResolve;
  });
  return { promise, resolve };
}

function response(overrides: Partial<DailySalesTableResponse> = {}): DailySalesTableResponse {
  return {
    requestedFrom: "2026-04-01",
    requestedTo: "2026-04-30",
    storeId: null,
    topN: 5,
    dataScope: "all",
    topSuppliers: [
      {
        supplierId: 1,
        supplierName: "Alfa",
        isUnknown: false,
        totalQty: 12,
        totalRevenue: 6000,
      },
    ],
    topSuppliersOrder: ["Alfa"],
    dateRows: [
      {
        date: "2026-04-01",
        firstShiftTotalItems: 10,
        secondShiftTotalItems: 8,
        totalRevenue: 9000,
        topSupplierCounts: [12],
        othersCount: 6,
        totalItemsSold: 18,
      },
    ],
    metadata: {
      totalDays: 30,
      uniqueSuppliersInRange: 1,
      unknownSupplierPct: 0,
      unknownSupplierItems: 0,
      offShiftItems: 0,
      offShiftRevenue: 0,
      totalItemsInRange: 18,
      duplicateReceiptGroupCount: 0,
      duplicateReceiptHeaderCount: 0,
      receiptAmountMismatchCount: 0,
      receiptAmountMismatchRevenue: 0,
      nonStandardReceiptCount: 0,
      nonStandardReceiptRevenue: 0,
      debtReceiptCount: 0,
      debtReceiptRevenue: 0,
      minAvailableDate: "2026-01-01",
      maxAvailableDate: "2026-04-30",
      warnings: [],
    },
    meta: {
      success: true,
      generatedAtUtc: "2026-07-01T08:00:00Z",
      lastRefreshAtUtc: "2026-07-01T08:00:00Z",
      dataQualityStatus: "good",
      isPartial: false,
    },
    ...overrides,
  };
}

describe("DailySalesStatsPage premium controls", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    vi.mocked(getStores).mockResolvedValue([]);
    vi.mocked(getDailySalesStats).mockResolvedValue(response());
  });

  it.each(["all", "existing", "imported"] as const)(
    "uses the %s scope for both initial Daily Sales period requests",
    async (scope) => {
      localStorage.setItem("trendplus:dataScope", scope);
      render(
        <MemoryRouter initialEntries={["/analytics/daily-sales"]}>
          <Routes>
            <Route path="/analytics/daily-sales" element={<DailySalesStatsPage />} />
          </Routes>
        </MemoryRouter>,
      );

      await waitFor(() => {
        expect(getDailySalesStats).toHaveBeenCalledTimes(2);
      });
      expect(vi.mocked(getDailySalesStats).mock.calls.map(([query]) => query.dataScope)).toEqual([scope, scope]);
    },
  );

  it("normalizes an invalid URL scope to all", async () => {
    render(
      <MemoryRouter initialEntries={["/analytics/daily-sales?dataScope=unexpected"]}>
        <Routes>
          <Route path="/analytics/daily-sales" element={<DailySalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(getDailySalesStats).toHaveBeenCalledTimes(2);
    });
    expect(vi.mocked(getDailySalesStats).mock.calls.map(([query]) => query.dataScope)).toEqual(["all", "all"]);
  });

  it("does not reload when the global scope event repeats the current scope", async () => {
    localStorage.setItem("trendplus:dataScope", "all");
    render(
      <MemoryRouter initialEntries={["/analytics/daily-sales"]}>
        <Routes>
          <Route path="/analytics/daily-sales" element={<DailySalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(getDailySalesStats).toHaveBeenCalledTimes(2);
    });

    act(() => {
      window.dispatchEvent(new Event("trendplus:data-scope-changed"));
    });

    expect(getDailySalesStats).toHaveBeenCalledTimes(2);
  });

  it("reloads both periods after a global scope change and ignores late old-scope responses", async () => {
    localStorage.setItem("trendplus:dataScope", "all");
    const oldCurrent = deferred<DailySalesTableResponse>();
    const oldPrevious = deferred<DailySalesTableResponse>();
    const nextCurrent = deferred<DailySalesTableResponse>();
    const nextPrevious = deferred<DailySalesTableResponse>();
    const pending = [oldCurrent, oldPrevious, nextCurrent, nextPrevious];
    let requestIndex = 0;

    vi.mocked(getDailySalesStats).mockImplementation(() => pending[requestIndex++].promise);

    render(
      <MemoryRouter initialEntries={["/analytics/daily-sales?fromDate=2026-04-01&toDate=2026-04-30"]}>
        <Routes>
          <Route
            path="/analytics/daily-sales"
            element={
              <>
                <DailySalesStatsPage />
                <LocationProbe />
              </>
            }
          />
        </Routes>
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(getDailySalesStats).toHaveBeenCalledTimes(2);
    });
    expect(vi.mocked(getDailySalesStats).mock.calls.slice(0, 2).map(([query]) => query.dataScope)).toEqual([
      "all",
      "all",
    ]);

    localStorage.setItem("trendplus:dataScope", "imported");
    act(() => {
      window.dispatchEvent(new Event("trendplus:data-scope-changed"));
    });

    await waitFor(() => {
      expect(getDailySalesStats).toHaveBeenCalledTimes(4);
    });
    expect(vi.mocked(getDailySalesStats).mock.calls.slice(-2).map(([query]) => query.dataScope)).toEqual([
      "imported",
      "imported",
    ]);

    nextCurrent.resolve(response({ dataScope: "imported", topSuppliersOrder: ["Novi scope"] }));
    nextPrevious.resolve(response({ dataScope: "imported", topSuppliersOrder: ["Novi scope"] }));

    await waitFor(() => {
      expect(screen.getByTestId("analytics-trust-header")).toHaveAttribute(
        "data-source",
        "Daily sales analytics (scope: imported)",
      );
      expect(screen.getByTestId("location-search")).toHaveTextContent(
        "fromDate=2026-04-01&toDate=2026-04-30&dataScope=imported",
      );
    });

    oldCurrent.resolve(response({ dataScope: "all", topSuppliersOrder: ["Stari scope"] }));
    oldPrevious.resolve(response({ dataScope: "all", topSuppliersOrder: ["Stari scope"] }));

    await waitFor(() => {
      expect(screen.getByTestId("analytics-trust-header")).toHaveAttribute(
        "data-source",
        "Daily sales analytics (scope: imported)",
      );
    });
  });

  it("uses shared trust header, control bar and analytics data table", async () => {
    render(
      <MemoryRouter initialEntries={["/analytics/daily-sales"]}>
        <Routes>
          <Route path="/analytics/daily-sales" element={<DailySalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(screen.getByTestId("analytics-trust-header")).toHaveTextContent("Prodaja po smeni");
    await waitFor(() => {
      expect(screen.getByTestId("analytics-trust-header")).toHaveAttribute("data-last-refresh-at", "2026-07-01T08:00:00Z");
      expect(screen.getByTestId("analytics-trust-header")).toHaveAttribute("data-quality", "good");
      expect(screen.getByTestId("analytics-trust-header")).toHaveAttribute("data-freshness", "fresh");
    });
    const controlBar = await screen.findByTestId("analytics-control-bar");
    expect(within(controlBar).getByRole("heading", { name: "Opseg i filteri" })).toBeInTheDocument();
    expect(within(controlBar).getByLabelText("Period")).toBeInTheDocument();
    expect(within(controlBar).getByLabelText("Objekat")).toBeInTheDocument();
    expect(within(controlBar).getByLabelText("Top dobavljača")).toBeInTheDocument();
    expect(within(controlBar).getByRole("link", { name: "Kvalitet podataka" })).toHaveAttribute(
      "href",
      "/analytics/data-quality",
    );

    await waitFor(() => {
      expect(screen.getByTestId("daily-sales-stats-data-table")).toBeInTheDocument();
    });
    expect(screen.getByText("Tabela po danima")).toBeInTheDocument();
  });

  it("localizes and exposes the daily supplier-total mismatch warning", async () => {
    vi.mocked(getDailySalesStats).mockResolvedValue(
      response({
        dateRows: [{
          ...response().dateRows[0],
          othersCount: 6,
          totalItemsSold: 17,
        }],
      }),
    );

    render(
      <MemoryRouter initialEntries={["/analytics/daily-sales"]}>
        <Routes>
          <Route path="/analytics/daily-sales" element={<DailySalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByText("Neusklađeno")).toHaveAttribute(
      "aria-label",
      "Red ima neusklađen ukupan broj komada",
    );
    expect(screen.getByTitle(/zbir najvećih dobavljača i ostalih ne odgovara/i)).toBeInTheDocument();
    expect(screen.getByText(/ima neusklađenost između ukupne kolone/i)).toBeInTheDocument();
  });

  it("does not report fresh data when the authoritative refresh timestamp is missing", async () => {
    vi.mocked(getDailySalesStats).mockResolvedValue(response({
      meta: {
        success: true,
        dataQualityStatus: "good",
        isPartial: false,
        lastRefreshAtUtc: null,
      },
    }));

    render(
      <MemoryRouter initialEntries={["/analytics/daily-sales"]}>
        <Routes>
          <Route path="/analytics/daily-sales" element={<DailySalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(screen.getByTestId("analytics-trust-header")).toHaveAttribute("data-freshness", "unknown");
    });
  });

  it("keeps trend and shift charts chronological when table sort changes", async () => {
    const baseRow = response().dateRows[0];
    vi.mocked(getDailySalesStats).mockResolvedValue(response({
      dateRows: [
        { ...baseRow, date: "2026-04-01", totalRevenue: 9000 },
        { ...baseRow, date: "2026-04-02", totalRevenue: 1000 },
      ],
    }));

    render(
      <MemoryRouter initialEntries={["/analytics/daily-sales"]}>
        <Routes>
          <Route path="/analytics/daily-sales" element={<DailySalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByTestId("line-chart")).toHaveAttribute(
      "data-order",
      "2026-04-01,2026-04-02",
    );

    const shiftMixPanel = screen.getByRole("heading", { name: /Smenski miks po danima/ }).closest("article");
    expect(shiftMixPanel).not.toBeNull();
    expect(within(shiftMixPanel as HTMLElement).getByTestId("bar-chart")).toHaveAttribute(
      "data-order",
      "2026-04-01,2026-04-02",
    );

    fireEvent.click(screen.getByRole("button", { name: /Prihod dana/ }));
    await waitFor(() => {
      expect(screen.getByTestId("line-chart")).toHaveAttribute(
        "data-order",
        "2026-04-01,2026-04-02",
      );
      expect(within(shiftMixPanel as HTMLElement).getByTestId("bar-chart")).toHaveAttribute(
        "data-order",
        "2026-04-01,2026-04-02",
      );
    });
  });

  it("shows shared error state instead of KPI zeros when daily sales fails", async () => {
    vi.mocked(getDailySalesStats).mockRejectedValue(new Error("backend down"));

    render(
      <MemoryRouter initialEntries={["/analytics/daily-sales"]}>
        <Routes>
          <Route path="/analytics/daily-sales" element={<DailySalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByRole("alert")).toHaveTextContent(/Dnevna prodaja trenutno nije dostupna/i);
    expect(screen.getByRole("alert")).toHaveTextContent("backend down");
    expect(screen.queryByText("Ukupan prihod")).not.toBeInTheDocument();
    expect(screen.queryByTestId("daily-sales-stats-data-table")).not.toBeInTheDocument();
  });

  it("shows shared empty state instead of only an in-table empty row", async () => {
    vi.mocked(getDailySalesStats).mockResolvedValue(
      response({
        dateRows: [],
        topSuppliers: [],
        topSuppliersOrder: [],
        meta: {
          success: true,
          generatedAtUtc: "2026-07-01T08:05:00Z",
          lastRefreshAtUtc: "2026-07-01T08:05:00Z",
          dataQualityStatus: "insufficient_data",
          emptyReason: "no_data_in_period",
          message: "Nema prodaje za izabrani period.",
          isPartial: false,
        },
        metadata: {
          totalDays: 30,
          uniqueSuppliersInRange: 0,
          unknownSupplierPct: 0,
          unknownSupplierItems: 0,
          offShiftItems: 0,
          offShiftRevenue: 0,
          totalItemsInRange: 0,
          duplicateReceiptGroupCount: 0,
          duplicateReceiptHeaderCount: 0,
          receiptAmountMismatchCount: 0,
          receiptAmountMismatchRevenue: 0,
          nonStandardReceiptCount: 0,
          nonStandardReceiptRevenue: 0,
          debtReceiptCount: 0,
          debtReceiptRevenue: 0,
          minAvailableDate: "2026-01-01",
          maxAvailableDate: "2026-03-31",
          warnings: [],
        },
      }),
    );

    render(
      <MemoryRouter initialEntries={["/analytics/daily-sales"]}>
        <Routes>
          <Route path="/analytics/daily-sales" element={<DailySalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByRole("heading", { name: /Nema podataka za izabrani period/i })).toBeInTheDocument();
    await waitFor(() => {
      expect(screen.getByTestId("analytics-trust-header")).toHaveAttribute("data-quality", "insufficient_data");
      expect(screen.getByTestId("analytics-trust-header")).toHaveAttribute(
        "data-empty-reason",
        "Nema prodaje za izabrani period.",
      );
    });
    expect(screen.getByRole("button", { name: "Prikaži dostupne podatke" })).toBeInTheDocument();
    expect(screen.getByText(/van dostupnog raspona prodaje/i)).toBeInTheDocument();
  });

  it("keeps a selected zero-sales store in the no-data state", async () => {
    vi.mocked(getDailySalesStats).mockResolvedValue(
      response({
        storeId: 7,
        dateRows: [],
        topSuppliers: [],
        topSuppliersOrder: [],
        meta: {
          success: true,
          dataQualityStatus: "insufficient_data",
          emptyReason: "no_data_in_period",
          message: "Nema prodaje za izabrani period.",
        },
        metadata: {
          ...response().metadata,
          totalDays: 0,
          totalItemsInRange: 0,
          minAvailableDate: null,
          maxAvailableDate: null,
        },
      }),
    );

    render(
      <MemoryRouter initialEntries={["/analytics/daily-sales?storeId=7"]}>
        <Routes>
          <Route path="/analytics/daily-sales" element={<DailySalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByRole("heading", { name: /Nema podataka za izabrani period/i })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: /Nema rezultata za trenutne filtere/i })).not.toBeInTheDocument();
  });

  it("uses backend no-data metadata even when the response contains calendar zero rows", async () => {
    vi.mocked(getDailySalesStats).mockResolvedValue(
      response({
        topSuppliers: [],
        topSuppliersOrder: [],
        dateRows: [
          { ...response().dateRows[0], totalRevenue: 0, totalItemsSold: 0, firstShiftTotalItems: 0, secondShiftTotalItems: 0 },
          { ...response().dateRows[0], date: "2026-04-02", totalRevenue: 0, totalItemsSold: 0, firstShiftTotalItems: 0, secondShiftTotalItems: 0 },
        ],
        meta: {
          success: true,
          dataQualityStatus: "insufficient_data",
          emptyReason: "no_data_in_period",
          message: "Nema prodaje za izabrani period.",
        },
        metadata: { ...response().metadata, totalDays: 30, totalItemsInRange: 0 },
      }),
    );

    render(
      <MemoryRouter initialEntries={["/analytics/daily-sales"]}>
        <Routes>
          <Route path="/analytics/daily-sales" element={<DailySalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByText(/Nema prodaje za izabrani period\./)).toBeInTheDocument();
    expect(screen.queryByText("Ukupan prihod")).not.toBeInTheDocument();
    expect(screen.queryByText("Stabilan pregled")).not.toBeInTheDocument();
    expect(screen.queryByTestId("line-chart")).not.toBeInTheDocument();
  });

  it("surfaces backend trust warnings from Daily Sales meta", async () => {
    vi.mocked(getDailySalesStats).mockResolvedValue(
      response({
        meta: {
          success: true,
          generatedAtUtc: "2026-07-01T08:15:00Z",
          lastRefreshAtUtc: "2026-07-01T08:15:00Z",
          dataQualityStatus: "warning",
          isPartial: true,
          warningCode: "DAILY_SALES_WARNINGS",
          warningMessage: "Dnevna prodaja ima upozorenja o kvalitetu podataka.",
          message: "Dnevna prodaja ima upozorenja o kvalitetu podataka.",
        },
        metadata: {
          totalDays: 30,
          uniqueSuppliersInRange: 1,
          unknownSupplierPct: 12,
          unknownSupplierItems: 4,
          offShiftItems: 2,
          offShiftRevenue: 1000,
          totalItemsInRange: 18,
          duplicateReceiptGroupCount: 0,
          duplicateReceiptHeaderCount: 0,
          receiptAmountMismatchCount: 0,
          receiptAmountMismatchRevenue: 0,
          nonStandardReceiptCount: 0,
          nonStandardReceiptRevenue: 0,
          debtReceiptCount: 0,
          debtReceiptRevenue: 0,
          minAvailableDate: "2026-01-01",
          maxAvailableDate: "2026-04-30",
          warnings: ["Veliki udeo prodaje ima nepoznatog dobavljača (20%+)."],
        },
      }),
    );

    render(
      <MemoryRouter initialEntries={["/analytics/daily-sales"]}>
        <Routes>
          <Route path="/analytics/daily-sales" element={<DailySalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(screen.getByTestId("analytics-trust-header")).toHaveAttribute("data-quality", "warning");
      expect(screen.getByTestId("analytics-trust-header")).toHaveAttribute("data-freshness", "stale");
      expect(screen.getByTestId("analytics-trust-header")).toHaveAttribute("data-partial", "true");
    });
  });

  it("marks a null-and-zero shift pair as incomplete without replacing the measured zero", async () => {
    vi.mocked(getDailySalesStats).mockResolvedValue(
      response({
        dateRows: [
          {
            ...response().dateRows[0],
            firstShiftTotalItems: 0,
            secondShiftTotalItems: 0,
            totalItemsSold: 18,
          },
          {
            ...response().dateRows[0],
            date: "2026-04-02",
            firstShiftTotalItems: null,
            secondShiftTotalItems: 0,
            totalItemsSold: 10,
          },
        ],
      }),
    );

    render(
      <MemoryRouter initialEntries={["/analytics/daily-sales"]}>
        <Routes>
          <Route path="/analytics/daily-sales" element={<DailySalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    const qualityToggle = await screen.findByTitle("Prikaži detalje kvaliteta");
    fireEvent.click(qualityToggle);

    const qualityPanel = screen.getByRole("heading", { name: /^Kvalitet podataka/ }).closest("article");
    expect(qualityPanel).not.toBeNull();
    const incompleteShiftCard = within(qualityPanel as HTMLElement)
      .getByText("Dani bez satnice")
      .closest("article");
    expect(incompleteShiftCard).not.toBeNull();
    expect(within(incompleteShiftCard as HTMLElement).getByText("1")).toBeInTheDocument();

    const incompleteCountCard = within(qualityPanel as HTMLElement)
      .getByText("Dani sa nepotpunom satnicom")
      .closest("article");
    expect(incompleteCountCard).not.toBeNull();
    expect(within(incompleteCountCard as HTMLElement).getByText("2")).toBeInTheDocument();

    const dayRow = screen.getAllByRole("cell", { name: "Nije dostupno" })[0]?.closest("tr");
    expect(dayRow).not.toBeNull();
    expect(within(dayRow as HTMLElement).getByText("Nije dostupno")).toBeInTheDocument();
    expect(within(dayRow as HTMLElement).getByText("0")).toBeInTheDocument();
  });

  it("does not reconcile contradictory supplier totals into trusted concentration shares", async () => {
    vi.mocked(getDailySalesStats).mockResolvedValue(
      response({
        topSuppliers: [{
          supplierId: 1,
          supplierName: "Alfa",
          isUnknown: false,
          totalQty: 25,
          totalRevenue: 12000,
        }],
        topSuppliersOrder: ["Alfa"],
        dateRows: [
          {
            ...response().dateRows[0],
            totalItemsSold: 18,
            totalRevenue: 9000,
          },
        ],
        metadata: { ...response().metadata, totalItemsInRange: 18 },
      }),
    );

    render(
      <MemoryRouter initialEntries={["/analytics/daily-sales"]}>
        <Routes>
          <Route path="/analytics/daily-sales" element={<DailySalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByTestId("supplier-concentration-warning")).toHaveTextContent(
      "Koncentracija dobavljača nije dostupna",
    );
    const concentrationPanel = screen
      .getByRole("heading", { name: /Koncentracija dobavljača/ })
      .closest("article");
    expect(concentrationPanel).not.toBeNull();
    expect(within(concentrationPanel as HTMLElement).getAllByText("N/A")).toHaveLength(2);
    expect(within(concentrationPanel as HTMLElement).getByText("Nije dostupno")).toBeInTheDocument();
  });

  it("shows concentration warning when supplier order metadata is missing", async () => {
    vi.mocked(getDailySalesStats).mockResolvedValue(
      response({
        topSuppliers: [{
          supplierId: 1,
          supplierName: "Alfa",
          isUnknown: false,
          totalQty: 12,
          totalRevenue: 6000,
        }],
        topSuppliersOrder: [],
      }),
    );

    render(
      <MemoryRouter initialEntries={["/analytics/daily-sales"]}>
        <Routes>
          <Route path="/analytics/daily-sales" element={<DailySalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByTestId("supplier-concentration-warning")).toHaveTextContent(
      "topSuppliersOrder",
    );
  });

  it("warns when previous-period request fails and does not label it as Nova baza", async () => {
    vi.mocked(getDailySalesStats)
      .mockResolvedValueOnce(response())
      .mockRejectedValueOnce(new Error("Previous period timeout"));

    render(
      <MemoryRouter initialEntries={["/analytics/daily-sales"]}>
        <Routes>
          <Route path="/analytics/daily-sales" element={<DailySalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    await screen.findByText("Poređenje sa prethodnim periodom");

    const warning = await screen.findByTestId("previous-comparison-warning");
    expect(warning).toHaveTextContent("Previous period timeout");
    expect(warning).toHaveTextContent("greške zahteva");
    expect(screen.getAllByText("Nedostupno").length).toBeGreaterThanOrEqual(4);
    expect(screen.queryByText("Nova baza")).not.toBeInTheDocument();
  });

  it("keeps a successful empty previous baseline distinct from a failed comparison", async () => {
    vi.mocked(getDailySalesStats)
      .mockResolvedValueOnce(response())
      .mockResolvedValueOnce(response({ dateRows: [] }));

    render(
      <MemoryRouter initialEntries={["/analytics/daily-sales"]}>
        <Routes>
          <Route path="/analytics/daily-sales" element={<DailySalesStatsPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByTestId("previous-comparison-empty-note")).toHaveTextContent(
      "Prethodni uporedivi period nema dovoljno podataka za poređenje.",
    );
    expect(screen.queryByTestId("previous-comparison-warning")).not.toBeInTheDocument();
  });

  it("marks concentration as unavailable when either denominator is missing", () => {
    const result = buildSupplierConcentration(
      response({
        topSuppliers: [{
          ...response().topSuppliers[0],
          totalQty: null,
          totalRevenue: null,
        }],
        metadata: { ...response().metadata, totalItemsInRange: null },
      }),
      null,
    );

    expect(result.warning).toContain("Nedostaje validan denominator količine");
    expect(result.warning).toContain("Nedostaje validan prihodovni denominator");
    expect(result.top3QtySharePct).toBeNull();
    expect(result.suppliersTo80Pct).toBeNull();
  });
});

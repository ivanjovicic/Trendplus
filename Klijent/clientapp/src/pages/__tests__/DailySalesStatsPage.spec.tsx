import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { rest } from "../../mocks/mswCompat";
import { describe, it, expect, beforeEach, vi } from "vitest";
import { server } from "../../mocks/server";
import DailySalesStatsPage from "../DailySalesStatsPage";
import { invalidateAnalyticsCache } from "../../services/analyticsApi";

// Mock the chart components
vi.mock("recharts", () => ({
  BarChart: ({ children }: any) => <div>{children}</div>,
  LineChart: ({ children }: any) => <div>{children}</div>,
  ComposedChart: ({ children }: any) => <div>{children}</div>,
  ResponsiveContainer: ({ children }: any) => <div>{children}</div>,
  CartesianGrid: () => <div />,
  XAxis: () => <div />,
  YAxis: () => <div />,
  Tooltip: () => <div />,
  Bar: () => <div />,
  Line: () => <div />,
  Legend: () => <div />,
}));

describe("DailySalesStatsPage (integration)", () => {
  function LocationProbe() {
    const location = useLocation();
    return <output data-testid="location-search">{location.pathname}{location.search}</output>;
  }

  const storesResponse = [
    { storeId: 1, storeName: "Store 1" },
    { storeId: 2, storeName: "Store 2" },
  ];

  const dailySalesResponse = {
    requestedFrom: "2026-04-01",
    requestedTo: "2026-04-30",
    storeId: null,
    topN: 5,
    dataScope: "all",
    topSuppliers: [],
    topSuppliersOrder: [],
    dateRows: [
      {
        date: "2026-04-01",
        firstShiftTotalItems: 10,
        secondShiftTotalItems: 8,
        totalRevenue: 9000,
        topSupplierCounts: [],
        othersCount: 2,
        totalItemsSold: 20,
      },
    ],
    metadata: {
      totalDays: 30,
      uniqueSuppliersInRange: 10,
      unknownSupplierPct: 5,
      unknownSupplierItems: 1,
      offShiftItems: 2,
      offShiftRevenue: 1000,
      totalItemsInRange: 20,
      duplicateReceiptGroupCount: 0,
      duplicateReceiptHeaderCount: 0,
      receiptAmountMismatchCount: 0,
      receiptAmountMismatchRevenue: 0,
      nonStandardReceiptCount: 0,
      nonStandardReceiptRevenue: 0,
      debtReceiptCount: 0,
      debtReceiptRevenue: 0,
      minAvailableDate: "2026-04-01",
      maxAvailableDate: "2026-04-30",
    },
    meta: {
      success: true,
      dataQualityStatus: "good",
    },
  };

  beforeEach(() => {
    invalidateAnalyticsCache();
    server.use(
      rest.get("/api/analytics/cached/filters/stores", (_req, res, ctx) =>
        res(ctx.status(200), ctx.json(storesResponse))
      ),
      rest.get("/api/analytics/daily-sales", (_req, res, ctx) =>
        res(ctx.status(200), ctx.json(dailySalesResponse))
      )
    );
  });

  it("renders page title", () => {
    render(
      <MemoryRouter initialEntries={["/analytics/daily-sales"]}>
        <Routes>
          <Route
            path="/analytics/daily-sales"
            element={<DailySalesStatsPage />}
          />
        </Routes>
      </MemoryRouter>
    );

    const title = screen.getByText(/Prodaja po smeni/i);
    expect(title).toBeInTheDocument();
  });

  it("renders filter controls", () => {
    render(
      <MemoryRouter initialEntries={["/analytics/daily-sales"]}>
        <Routes>
          <Route
            path="/analytics/daily-sales"
            element={<DailySalesStatsPage />}
          />
        </Routes>
      </MemoryRouter>
    );

    const controlBar = screen.getByTestId("analytics-control-bar");
    expect(within(controlBar).getByLabelText("Period")).toBeInTheDocument();
  });

  it("restores table sort from the URL and persists the next direction", async () => {
    render(
      <MemoryRouter initialEntries={["/analytics/daily-sales?sort=totalRevenue&dir=asc"]}>
        <Routes>
          <Route
            path="/analytics/daily-sales"
            element={
              <>
                <LocationProbe />
                <DailySalesStatsPage />
              </>
            }
          />
        </Routes>
      </MemoryRouter>,
    );

    const revenueHeader = await screen.findByRole("columnheader", { name: /Prihod dana/i });
    expect(revenueHeader).toHaveAttribute("aria-sort", "ascending");
    expect(screen.getByTestId("location-search")).toHaveTextContent("sort=totalRevenue");
    expect(screen.getByTestId("location-search")).toHaveTextContent("dir=asc");

    const revenueButton = within(revenueHeader).getByRole("button", { name: /Prihod dana/i });
    fireEvent.click(revenueButton);

    await waitFor(() => expect(revenueHeader).toHaveAttribute("aria-sort", "descending"));
    await waitFor(() => {
      expect(screen.getByTestId("location-search")).toHaveTextContent("sort=totalRevenue");
      expect(screen.getByTestId("location-search")).toHaveTextContent("dir=desc");
    });
  });

  it("falls back to the default date sort when the URL supplier column exceeds topN", async () => {
    render(
      <MemoryRouter initialEntries={["/analytics/daily-sales?topN=5&sort=supplier:7&dir=asc"]}>
        <Routes>
          <Route
            path="/analytics/daily-sales"
            element={
              <>
                <LocationProbe />
                <DailySalesStatsPage />
              </>
            }
          />
        </Routes>
      </MemoryRouter>,
    );

    await waitFor(() => {
      const search = screen.getByTestId("location-search").textContent ?? "";
      expect(search).not.toContain("sort=");
      expect(search).not.toContain("dir=");
      expect(search).toContain("topN=5");
    });
  });

  it("drops an unverified URL store filter when store discovery fails", async () => {
    const requestedStoreIds: Array<string | null> = [];

    server.use(
      rest.get("/api/analytics/cached/filters/stores", (_req, res, ctx) =>
        res(ctx.status(503), ctx.json({ message: "stores unavailable" }))
      ),
      rest.get("/api/analytics/daily-sales", (req, res, ctx) => {
        requestedStoreIds.push(req.url.searchParams.get("storeId"));
        return res(ctx.status(200), ctx.json(dailySalesResponse));
      }),
    );

    render(
      <MemoryRouter initialEntries={["/analytics/daily-sales?storeId=2"]}>
        <Routes>
          <Route
            path="/analytics/daily-sales"
            element={<DailySalesStatsPage />}
          />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByText("Filter prodavnice nije dostupan.")).toBeInTheDocument();

    await waitFor(() => {
      expect(requestedStoreIds.at(-1)).toBeNull();
    });

    expect(screen.getByDisplayValue("Svi objekti")).toBeDisabled();
  });
});

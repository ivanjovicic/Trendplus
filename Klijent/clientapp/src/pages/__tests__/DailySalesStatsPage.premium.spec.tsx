import { render, screen, waitFor, within } from "@testing-library/react";
import type { ReactNode } from "react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import DailySalesStatsPage from "../DailySalesStatsPage";
import { getStores } from "../../services/analyticsApi";
import { getDailySalesStats } from "../../services/dailySalesStatsApi";
import type { DailySalesTableResponse } from "../../services/dailySalesStatsApi";

vi.mock("recharts", () => ({
  Bar: () => null,
  BarChart: ({ children }: { children?: ReactNode }) => <div data-testid="bar-chart">{children}</div>,
  CartesianGrid: () => null,
  ComposedChart: ({ children }: { children?: ReactNode }) => <div data-testid="composed-chart">{children}</div>,
  Legend: () => null,
  Line: () => null,
  LineChart: ({ children }: { children?: ReactNode }) => <div data-testid="line-chart">{children}</div>,
  ResponsiveContainer: ({ children }: { children?: ReactNode }) => <div data-testid="responsive-container">{children}</div>,
  Tooltip: () => null,
  XAxis: () => null,
  YAxis: () => null,
}));

vi.mock("../../components/analytics/AnalyticsTrustHeader", () => ({
  default: ({ title }: { title: string }) => <div data-testid="analytics-trust-header">{title}</div>,
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
    ...overrides,
  };
}

describe("DailySalesStatsPage premium controls", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(getStores).mockResolvedValue([]);
    vi.mocked(getDailySalesStats).mockResolvedValue(response());
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
    expect(screen.getByRole("button", { name: "Prikaži dostupne podatke" })).toBeInTheDocument();
    expect(screen.getByText(/van dostupnog raspona prodaje/i)).toBeInTheDocument();
  });
});

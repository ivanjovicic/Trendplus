import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter } from "react-router-dom";
import type { DailySale } from "../../types/analytics";
import AnalyticsDetails, { calculateAnalyticsDetailsTrend } from "../AnalyticsDetails";

const apiMocks = vi.hoisted(() => ({
  checkAnalyticsHealth: vi.fn(),
  getSalesSummary: vi.fn(),
  getDailySales: vi.fn(),
  getInventoryStatus: vi.fn(),
  getTopProductsAdvanced: vi.fn(),
  getDashboardAdvanced: vi.fn(),
  getValidationCompleteness: vi.fn(),
  getValidationFreshness: vi.fn(),
  getValidationLostSales: vi.fn(),
  getValidationNegativeQty: vi.fn(),
}));

vi.mock("../../services/analyticsApi", () => apiMocks);

function dayAtOffset(endDate: string, offset: number): string {
  const [year, month, day] = endDate.split("-").map(Number);
  return new Date(Date.UTC(year, month - 1, day + offset)).toISOString().slice(0, 10);
}

function makeDailySales(endDate: string, count: number, missingOffsets: number[] = []): DailySale[] {
  return Array.from({ length: count }, (_, index) => ({
    date: dayAtOffset(endDate, index - count + 1),
    totalRevenue: 100 + index * 10,
    transactionCount: index + 1,
    totalUnits: 10 + index,
  })).filter((_, index) => !missingOffsets.includes(index - count + 1));
}

describe("AnalyticsDetails moving windows", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    apiMocks.checkAnalyticsHealth.mockResolvedValue({ tables: { salesFacts: 20, salesLineFacts: 20, productsDim: 20 } });
    apiMocks.getSalesSummary.mockResolvedValue({ totalRevenue: 1_000, totalTransactions: 20, totalUnits: 20 });
    apiMocks.getDailySales.mockImplementation((_fromDate: string, toDate: string) =>
      Promise.resolve(makeDailySales(toDate.slice(0, 10), 21, [-5])),
    );
    apiMocks.getInventoryStatus.mockResolvedValue({ totalSkuCount: 0, lowStockCount: 0, outOfStockCount: 0 });
    apiMocks.getTopProductsAdvanced.mockResolvedValue({ byRevenue: [], byUnits: [], byVelocity: [], byMarginImpact: [] });
    apiMocks.getDashboardAdvanced.mockResolvedValue({ cards: [], insights: [], actions: [], validations: [] });
    apiMocks.getValidationCompleteness.mockResolvedValue({ status: "unknown", score: null, affectedSku: null });
    apiMocks.getValidationFreshness.mockResolvedValue({ status: "unknown", freshnessHours: null });
    apiMocks.getValidationLostSales.mockResolvedValue({ status: "unknown", lostSalesEstimate: null });
    apiMocks.getValidationNegativeQty.mockResolvedValue({ status: "unknown", negativeQtyCount: null, totalRows: null });
  });

  it("renders weekly momentum unavailable when a calendar day is missing", async () => {
    render(
      <MemoryRouter>
        <AnalyticsDetails />
      </MemoryRouter>,
    );

    expect(await screen.findByText("Momentum: Nije dostupno")).toBeInTheDocument();
    expect(screen.getByText(/Tačke na grafikonu: 20/)).toHaveTextContent(
      "Nedostajući dani bez dnevnog reda nisu predstavljeni kao nule.",
    );
  });

  it("builds exact calendar-day moving averages for complete data", () => {
    const trend = calculateAnalyticsDetailsTrend(
      makeDailySales("2026-10-16", 30),
      "2026-09-17T00:00",
      "2026-10-16T23:59",
    );

    expect(trend).toHaveLength(30);
    expect(trend.at(-1)).toMatchObject({ ma7: 360, ma30: 245, status: "observed_sale" });
  });

  it("leaves gaps unknown and moving signals unavailable across them", () => {
    const trend = calculateAnalyticsDetailsTrend(
      makeDailySales("2026-10-16", 21, [-5]),
      "2026-09-26T00:00",
      "2026-10-16T23:59",
    );
    const missingDay = trend.find((day) => day.date === "2026-10-11");

    expect(missingDay).toMatchObject({ status: "unknown_day", revenue: null, totalRevenue: null });
    expect(trend.at(-1)?.ma7).toBeNull();
    expect(trend.at(-1)?.anomaly).toBeNull();
  });

  it("does not calculate moving signals for short or empty series", () => {
    const short = calculateAnalyticsDetailsTrend(
      makeDailySales("2026-10-16", 6),
      "2026-10-11",
      "2026-10-16",
    );
    const empty = calculateAnalyticsDetailsTrend([], "2026-10-10", "2026-10-16");

    expect(short.at(-1)).toMatchObject({ ma7: null, ma30: null, anomaly: null });
    expect(empty).toEqual([]);
  });
});

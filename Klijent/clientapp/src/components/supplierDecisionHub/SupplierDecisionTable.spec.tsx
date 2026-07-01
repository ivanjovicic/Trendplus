import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import SupplierDecisionTable from "./SupplierDecisionTable";
import type { RankingItem } from "../../services/supplierDecisionHubApi";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../../types/analyticsTable";

vi.mock("../analytics/AnalyticsTableToolbar", () => ({
  default: ({ tableKey, tableTitle, rows }: { tableKey: string; tableTitle: string; rows: RankingItem[] }) => (
    <div data-testid="analytics-toolbar">
      {tableKey} | {tableTitle} | rows: {rows.length}
    </div>
  ),
}));

const columns: AnalyticsTableColumn<RankingItem>[] = [
  { key: "supplierName", header: "Dobavljač", dataType: "text" },
  { key: "revenue", header: "Prihod", dataType: "currency" },
  { key: "confidenceScore", header: "Pouzdanost", dataType: "number" },
];

const filters: AnalyticsNamedValue[] = [{ key: "period", label: "Period", value: "90d" }];
const metadata: AnalyticsNamedValue[] = [{ key: "generatedAt", label: "Generisano", value: "2026-07-01" }];

function makeItem(overrides: Partial<RankingItem> = {}): RankingItem {
  return {
    supplierId: 7,
    supplierName: "Dobavljač Premium",
    revenue: 1250000,
    units: 420,
    fullPriceRevenueShare: 0.72,
    fullPriceSellthrough: 0.61,
    preMarkdownMarginPct: 0.38,
    markdownRevenueShare: 0.18,
    deadStockRate: 0.08,
    unsoldStockValue: 140000,
    repeatWinnerRate: 0.31,
    mlSupplierScore: 88,
    supplierQualityIndex: 82,
    recommendationCode: "EXPAND",
    confidenceScore: 0.91,
    reliabilityPct: 84,
    dataQualityStatus: "good",
    statusReason: "Strong full-price sellthrough.",
    reasonCodes: ["full_price_winner"],
    ...overrides,
  };
}

function renderTable(overrides: Partial<React.ComponentProps<typeof SupplierDecisionTable>> = {}) {
  const props: React.ComponentProps<typeof SupplierDecisionTable> = {
    items: [makeItem()],
    columns,
    analyticsFilters: filters,
    analyticsMetadata: metadata,
    loading: false,
    page: 1,
    pageSize: 25,
    totalCount: 1,
    sortBy: "revenue",
    sortDir: "desc",
    onPageChange: vi.fn(),
    onSortChange: vi.fn(),
    onSelectSupplier: vi.fn(),
    onOpenDetail: vi.fn(),
    ...overrides,
  };

  render(<SupplierDecisionTable {...props} />);
  return props;
}

describe("SupplierDecisionTable", () => {
  it("renders premium ranking context, export toolbar and formatted ranking rows", () => {
    renderTable({ totalCount: 17 });

    expect(screen.getByText("Supplier analytics")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Rang lista dobavljača" })).toBeInTheDocument();
    expect(screen.getByText(/Sortiranje i paginacija ostaju na backendu/i)).toBeInTheDocument();
    expect(screen.getByText("17")).toBeInTheDocument();
    expect(screen.getByTestId("analytics-toolbar")).toHaveTextContent("supplier-decision-hub");
    expect(screen.getByText("Dobavljač Premium")).toBeInTheDocument();
    expect(screen.getByText(/1.250.000/)).toBeInTheDocument();
    expect(screen.getByText("0,91")).toBeInTheDocument();
    expect(screen.getByText(/Širi saradnju|EXPAND/i)).toBeInTheDocument();
  });

  it("delegates backend sort requests without sorting locally", () => {
    const onSortChange = vi.fn();
    renderTable({ onSortChange, sortBy: "revenue", sortDir: "desc" });

    fireEvent.click(screen.getByRole("button", { name: /Dobavljač/i }));
    expect(onSortChange).toHaveBeenCalledWith("supplierName");

    fireEvent.click(screen.getByRole("button", { name: /Prihod/i }));
    expect(onSortChange).toHaveBeenCalledWith("revenue");
  });

  it("opens supplier detail and selection from the row click", () => {
    const item = makeItem({ supplierId: 12, supplierName: "Dobavljač Detalj" });
    const onSelectSupplier = vi.fn();
    const onOpenDetail = vi.fn();

    renderTable({ items: [item], onSelectSupplier, onOpenDetail });

    fireEvent.click(screen.getByText("Dobavljač Detalj"));

    expect(onSelectSupplier).toHaveBeenCalledWith(12);
    expect(onOpenDetail).toHaveBeenCalledWith(item);
  });

  it("renders loading skeleton and disables pagination actions while loading", () => {
    const onPageChange = vi.fn();
    renderTable({ items: [], loading: true, page: 2, totalCount: 75, onPageChange });

    expect(screen.getAllByText((_, element) => element?.className.includes("supplier-decision-skeleton-row") ?? false).length).toBeGreaterThan(0);
    expect(screen.getByRole("button", { name: "Prethodna" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Sledeća" })).toBeDisabled();
  });

  it("renders empty state and guards page boundaries", () => {
    const onPageChange = vi.fn();
    renderTable({ items: [], totalCount: 0, page: 1, onPageChange });

    expect(screen.getByText("Nema dobavljača za izabrane filtere.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Prethodna" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Sledeća" })).toBeDisabled();
    expect(onPageChange).not.toHaveBeenCalled();
  });

  it("uses pagination callbacks when more backend pages are available", () => {
    const onPageChange = vi.fn();
    renderTable({ page: 2, pageSize: 10, totalCount: 30, onPageChange });

    fireEvent.click(screen.getByRole("button", { name: "Prethodna" }));
    fireEvent.click(screen.getByRole("button", { name: "Sledeća" }));

    expect(onPageChange).toHaveBeenNthCalledWith(1, 1);
    expect(onPageChange).toHaveBeenNthCalledWith(2, 3);
  });
});

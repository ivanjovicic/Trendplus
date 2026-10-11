import { render, screen } from "@testing-library/react";
import IntelligenceSnapshotPanel from "./IntelligenceSnapshotPanel";
import type { DemandSignalItem, InventoryRiskSignalItem, TrendMomentumItem } from "../../services/analyticsIntelligenceApi";

const newDemand: DemandSignalItem = {
  articleId: 101,
  sku: "SKU-101",
  productName: "Nova cipela",
  category: "Obuća",
  supplierId: 1,
  supplierName: "Dobavljač",
  storeId: 1,
  storeName: "Beograd",
  storeCity: "Beograd",
  date: "2026-09-08",
  salesVelocity: 4,
  demandAcceleration: null,
  demandState: "NEW_DEMAND",
  daysSinceLastSale: 0,
  launchAgeDays: 3,
  storeCoverage: 1,
  sourceRows: 4,
};

const unknownInventory: InventoryRiskSignalItem = {
  articleId: 303,
  sku: "SKU-303",
  productName: "Artikal bez poznate zalihe",
  category: "Obuća",
  supplierId: null,
  supplierName: "Dobavljač",
  date: "2026-09-08",
  stockQty: null,
  avgDailySales30d: 0.033333,
  daysOfCover: null,
  stockTurn: null,
  stockoutDays: null,
  lowStockDays: null,
  deadStockRisk: null,
};

const trendItem = (trendEntropy: number | null): TrendMomentumItem => ({
  articleId: 707,
  sku: "SKU-707",
  productName: "Artikal trenda",
  category: "Obuća",
  supplierId: null,
  supplierName: "Dobavljač",
  signalDate: "2026-09-08",
  externalTrendScore: 12,
  localSalesAcceleration: 0,
  trendEntropy,
});

describe("IntelligenceSnapshotPanel demand state", () => {
  it("renders new demand distinctly from measured acceleration", () => {
    render(
      <IntelligenceSnapshotPanel
        demand={[newDemand]}
        inventory={[]}
        price={[]}
        trend={[]}
        asOfDate="2026-09-08"
        loading={false}
      />,
    );

    expect(screen.getAllByText("Novi signal")).toHaveLength(2);
    expect(screen.queryByText("NaN%")).not.toBeInTheDocument();
  });

  it("uses Serbian unavailable labels when a section has no rows", () => {
    render(
      <IntelligenceSnapshotPanel
        demand={[newDemand]}
        inventory={[]}
        price={[]}
        trend={[]}
        asOfDate="2026-09-08"
        loading={false}
      />,
    );

    expect(screen.getByText("Pregled signala")).toBeInTheDocument();
    expect(screen.getByText("Nema potvrđenog rizika zaliha")).toBeInTheDocument();
    expect(screen.getByText("Nema signala cene")).toBeInTheDocument();
    expect(screen.getByText("Nema trend signala")).toBeInTheDocument();
    expect(screen.queryByText("n/a")).not.toBeInTheDocument();
    expect(screen.queryByText("N/A")).not.toBeInTheDocument();
  });

  it("does not present an unknown stock baseline as a zero-risk result", () => {
    render(
      <IntelligenceSnapshotPanel
        demand={[]}
        inventory={[unknownInventory]}
        price={[]}
        trend={[]}
        asOfDate="2026-09-08"
        loading={false}
      />,
    );

    expect(screen.getByText("Nema potvrđenog rizika zaliha")).toBeInTheDocument();
    expect(screen.queryByText("Artikal bez poznate zalihe")).not.toBeInTheDocument();
    expect(screen.queryByText("0%")).not.toBeInTheDocument();
  });

  it("distinguishes unavailable entropy from a measured zero", () => {
    render(
      <IntelligenceSnapshotPanel
        demand={[]}
        inventory={[]}
        price={[]}
        trend={[trendItem(null), { ...trendItem(0), articleId: 708, sku: "SKU-708", productName: "Jednodnevna prodaja" }]}
        asOfDate="2026-09-08"
        loading={false}
      />,
    );

    expect(screen.getByText(/entropija Nije dostupno/)).toBeInTheDocument();
    expect(screen.getByText(/entropija 0\.00/)).toBeInTheDocument();
  });
});

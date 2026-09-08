import { render, screen } from "@testing-library/react";
import IntelligenceSnapshotPanel from "./IntelligenceSnapshotPanel";
import type { DemandSignalItem } from "../../services/analyticsIntelligenceApi";

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
});

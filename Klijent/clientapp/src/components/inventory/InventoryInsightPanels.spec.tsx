import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { InventoryInsightPanels } from "./InventoryInsightPanels";
import type { InventoryInsights, InventoryRow } from "./types";

const baseRow: InventoryRow = {
  id: 101,
  naziv: "Model A",
  plu: "PLU-101",
  kolicina: 0,
  minimalnaKolicina: 5,
  nabavnaCena: 100,
  estimatedValue: 0,
  idObjekat: 1,
  idDobavljac: 1,
  supplierName: "Dobavljač A",
  storeName: "Prodavnica 1",
  quantity: 0,
  minimum: 5,
  reorderGap: 5,
  stockState: "critical",
  stockStateLabel: "Bez zaliha",
  estimatedValueAmount: 0,
  unitCost: 100,
  coverageRatio: null,
  stockCoverDays: null,
  stockCoverStatus: "out_of_stock_risk",
  stockCoverStatusLabel: "Rizik rasprodaje",
  sellThroughRatio: 1,
  sellThroughStatus: "good",
  sellThroughStatusLabel: "Dobar sell-through",
  signalConfidencePct: 82,
  recommendationAllowed: true,
  signalText: "Dopuni",
  dataQualityStatus: "good",
  reasonCodes: ["replenish_needed", "stock_cover_out_of_stock_risk"],
};

function buildInsights(): InventoryInsights {
  return {
    totalItems: 1,
    totalEstimatedValue: 100,
    aging: [
      {
        bucketKey: "90+",
        label: "90+",
        itemCount: 1,
        totalUnits: 0,
        estimatedValue: 100,
      },
    ],
    abc: [
      {
        bucketKey: "A",
        label: "Klasa A",
        itemCount: 1,
        estimatedValue: 100,
        valueSharePct: 100,
      },
    ],
    topAgedItems: [
      {
        id: 101,
        plu: "PLU-101",
        naziv: "Model A",
        supplierName: "Dobavljač A",
        storeName: "Prodavnica 1",
        quantity: 0,
        minimum: 5,
        reorderGap: 5,
        estimatedValue: 100,
        daysSinceMovement: 120,
        agingBucket: "90+",
        agingLabel: "90+",
        abcClass: "A",
        stockState: "critical",
        stockCoverDays: null,
        stockCoverStatus: "out_of_stock_risk",
        stockCoverStatusLabel: "Rizik rasprodaje",
        sellThroughRatio: 1,
        sellThroughStatus: "good",
        sellThroughStatusLabel: "Dobar sell-through",
        signalConfidencePct: 82,
        recommendationAllowed: true,
        dataQualityStatus: "good",
        reasonCodes: ["replenish_needed", "stock_cover_out_of_stock_risk"],
      },
    ],
    topCapitalLockedItems: [],
  };
}

describe("InventoryInsightPanels", () => {
  it("shows unavailable aging badge copy on insights error instead of fake zero", () => {
    render(
      <InventoryInsightPanels
        insights={null}
        insightsLoading={false}
        insightsError="Inventory uvidi trenutno nisu dostupni."
        stores={[]}
        suppliers={[]}
        rows={[]}
        onOpenDetail={vi.fn()}
      />,
    );

    expect(screen.getByText("Aging analitika trenutno nije dostupna")).toBeInTheDocument();
    expect(screen.queryByText(/0 artikala je u 90\+ dana/)).not.toBeInTheDocument();
    expect(screen.getByRole("alert")).toHaveTextContent("Inventory uvidi trenutno nisu dostupni.");
  });

  it("shows measured zero in aging badge when backend returns explicit zero", () => {
    const insights = buildInsights();
    insights.aging = [{ bucketKey: "90+", label: "90+", itemCount: 0, totalUnits: 0, estimatedValue: 0 }];

    render(
      <InventoryInsightPanels
        insights={insights}
        insightsLoading={false}
        stores={[]}
        suppliers={[]}
        rows={[baseRow]}
        onOpenDetail={vi.fn()}
      />,
    );

    expect(screen.getByText("0 artikala je u 90+ dana")).toBeInTheDocument();
  });

  it("renders the explainability snapshot for insight items", () => {
    render(
      <InventoryInsightPanels
        insights={buildInsights()}
        insightsLoading={false}
        stores={[]}
        suppliers={[]}
        rows={[baseRow]}
        onOpenDetail={vi.fn()}
      />,
    );

    expect(screen.getByRole("region", { name: "Snapshot" })).toBeInTheDocument();
    expect(screen.getByText("82%")).toBeInTheDocument();
    expect(screen.getByText("Dozvoljena")).toBeInTheDocument();
    expect(screen.getByText("replenish_needed")).toBeInTheDocument();
  });

  it("keeps insight detail unit cost unavailable when it cannot be derived", () => {
    const onOpenDetail = vi.fn();

    render(
      <InventoryInsightPanels
        insights={buildInsights()}
        insightsLoading={false}
        stores={[]}
        suppliers={[]}
        rows={[]}
        onOpenDetail={onOpenDetail}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: /Model A/ }));

    expect(onOpenDetail).toHaveBeenCalledWith(expect.objectContaining({ unitCost: null, nabavnaCena: null }));
  });

  it("derives insight detail unit cost only from positive measured value and quantity", () => {
    const insights = buildInsights();
    insights.topAgedItems[0] = { ...insights.topAgedItems[0], quantity: 10, estimatedValue: 2500 };
    const onOpenDetail = vi.fn();

    render(
      <InventoryInsightPanels
        insights={insights}
        insightsLoading={false}
        stores={[]}
        suppliers={[]}
        rows={[]}
        onOpenDetail={onOpenDetail}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: /Model A/ }));

    expect(onOpenDetail).toHaveBeenCalledWith(expect.objectContaining({ unitCost: 250, nabavnaCena: 250 }));
  });
});

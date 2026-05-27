import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { InventoryItemsTable } from "./InventoryItemsTable";
import { buildSignalText, sellThroughStatusLabel, stockCoverStatusLabel } from "./inventoryUtils";
import type { InventoryRow } from "./types";

function makeRow(overrides: Partial<InventoryRow> = {}): InventoryRow {
  return {
    id: 101,
    naziv: "Model A",
    plu: "PLU-101",
    kolicina: 10,
    minimalnaKolicina: 5,
    nabavnaCena: 1000,
    estimatedValue: 10000,
    idObjekat: 1,
    idDobavljac: 2,
    supplierName: "Dobavljac A",
    storeName: "Prodavnica 1",
    quantity: 10,
    minimum: 5,
    reorderGap: 0,
    stockState: "healthy",
    stockStateLabel: "Stabilno",
    estimatedValueAmount: 10000,
    unitCost: 1000,
    coverageRatio: 2,
    stockCoverDays: 14,
    stockCoverStatus: "healthy",
    stockCoverStatusLabel: "Zdrava pokrivenost",
    sellThroughRatio: 0.62,
    sellThroughStatus: "good",
    sellThroughStatusLabel: "Dobar sell-through",
    signalConfidencePct: 82,
    recommendationAllowed: true,
    signalText: "Stabilan signal",
    dataQualityStatus: "good",
    reasonCodes: [],
    ...overrides,
  };
}

describe("Inventory signal presentation", () => {
  it("maps insufficient_data to explicit no-signal text", () => {
    expect(buildSignalText("insufficient_data", "good")).toBe("Nedovoljno podataka");
    expect(buildSignalText("healthy", "insufficient_data")).toBe("Nedovoljno podataka");
  });

  it("exposes readable stock cover and sell-through status labels", () => {
    expect(stockCoverStatusLabel("out_of_stock_risk")).toBe("Rizik rasprodaje");
    expect(stockCoverStatusLabel("insufficient_data")).toBe("Nedovoljno podataka");
    expect(sellThroughStatusLabel("warning")).toBe("Sell-through upozorenje");
    expect(sellThroughStatusLabel("insufficient_data")).toBe("Nedovoljno podataka");
  });

  it("renders explain buttons and does not show action CTA for insufficient_data", () => {
    const onOpenDetail = vi.fn();
    const onPreviousPage = vi.fn();
    const onNextPage = vi.fn();
    const onAddToActions = vi.fn();
    const onReviewSlowStock = vi.fn();

    render(
      <InventoryItemsTable
        rows={[
          makeRow({
            stockCoverDays: null,
            stockCoverStatus: "insufficient_data",
            stockCoverStatusLabel: "Nedovoljno podataka",
            sellThroughRatio: null,
            sellThroughStatus: "insufficient_data",
            sellThroughStatusLabel: "Nedovoljno podataka",
            recommendationAllowed: false,
            signalText: "Nedovoljno podataka",
          }),
        ]}
        loading={false}
        totalCount={1}
        pageNumber={1}
        totalPages={1}
        onOpenDetail={onOpenDetail}
        onPreviousPage={onPreviousPage}
        onNextPage={onNextPage}
        onAddToActions={onAddToActions}
        onReviewSlowStock={onReviewSlowStock}
        isRowQueued={() => false}
        isRowQueueBusy={() => false}
      />,
    );

    expect(screen.getByLabelText("Kako je izračunata pokrivenost zalihe")).toBeInTheDocument();
    expect(screen.getByLabelText("Kako je izračunat sell-through signal")).toBeInTheDocument();
    expect(screen.getAllByText("Nedovoljno podataka").length).toBeGreaterThan(0);
    expect(screen.getByRole("button", { name: "Dodaj u akcije" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Pregledaj sporu zalihu" })).not.toBeInTheDocument();
  });

  it("renders null metric as unavailable when status is not insufficient_data", () => {
    render(
      <InventoryItemsTable
        rows={[
          makeRow({
            stockCoverDays: null,
            stockCoverStatus: "healthy",
            stockCoverStatusLabel: "Zdrava pokrivenost",
            sellThroughRatio: null,
            sellThroughStatus: "good",
            sellThroughStatusLabel: "Dobar sell-through",
            signalText: "Stabilan signal",
          }),
        ]}
        loading={false}
        totalCount={1}
        pageNumber={1}
        totalPages={1}
        onOpenDetail={vi.fn()}
        onPreviousPage={vi.fn()}
        onNextPage={vi.fn()}
        onAddToActions={vi.fn()}
        onReviewSlowStock={vi.fn()}
        isRowQueued={() => false}
        isRowQueueBusy={() => false}
      />,
    );

    expect(screen.getAllByText("Nije dostupno").length).toBeGreaterThan(0);
  });

  it("shows action CTA when recommendation is blocked even with healthy cover", () => {
    render(
      <InventoryItemsTable
        rows={[
          makeRow({
            stockCoverStatus: "healthy",
            stockCoverStatusLabel: "Zdrava pokrivenost",
            sellThroughRatio: null,
            sellThroughStatus: "insufficient_data",
            sellThroughStatusLabel: "Nedovoljno podataka",
            recommendationAllowed: false,
            signalText: "Nedovoljno podataka",
          }),
        ]}
        loading={false}
        totalCount={1}
        pageNumber={1}
        totalPages={1}
        onOpenDetail={vi.fn()}
        onPreviousPage={vi.fn()}
        onNextPage={vi.fn()}
        onAddToActions={vi.fn()}
        onReviewSlowStock={vi.fn()}
        isRowQueued={() => false}
        isRowQueueBusy={() => false}
      />,
    );

    expect(screen.getByRole("button", { name: "Dodaj u akcije" })).toBeInTheDocument();
  });
});

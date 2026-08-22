import { fireEvent, render, screen } from "@testing-library/react";
import type { ComponentProps } from "react";
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
    supplierName: "Dobavljač A",
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

function renderTable(overrides: Partial<ComponentProps<typeof InventoryItemsTable>> = {}) {
  const props: ComponentProps<typeof InventoryItemsTable> = {
    rows: [makeRow()],
    loading: false,
    totalCount: 1,
    pageNumber: 1,
    totalPages: 1,
    onOpenDetail: vi.fn(),
    onPreviousPage: vi.fn(),
    onNextPage: vi.fn(),
    onAddToActions: vi.fn(),
    onReviewSlowStock: vi.fn(),
    isRowQueued: () => false,
    isRowQueueBusy: () => false,
    ...overrides,
  };

  render(<InventoryItemsTable {...props} />);
  return props;
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

  it("renders explain buttons, row count and unavailable signal text", () => {
    renderTable({
      rows: [
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
      ],
      totalCount: 7,
    });

    expect(screen.getByRole("heading", { name: "Tabela artikala" })).toBeInTheDocument();
    expect(screen.getByText("Inventory analytics")).toBeInTheDocument();
    expect(screen.getByText("7")).toBeInTheDocument();
    expect(screen.getByLabelText("Kako je izračunata pokrivenost zalihe")).toBeInTheDocument();
    expect(screen.getByLabelText("Kako je izračunat sell-through signal")).toBeInTheDocument();
    expect(screen.getAllByText("Nedovoljno podataka").length).toBeGreaterThan(0);
    expect(screen.getByRole("button", { name: "Dodaj u akcije" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Pregledaj sporu zalihu" })).not.toBeInTheDocument();
  });

  it("renders null metric as unavailable when status is not insufficient_data", () => {
    renderTable({
      rows: [
        makeRow({
          stockCoverDays: null,
          stockCoverStatus: "healthy",
          stockCoverStatusLabel: "Zdrava pokrivenost",
          sellThroughRatio: null,
          sellThroughStatus: "good",
          sellThroughStatusLabel: "Dobar sell-through",
          signalText: "Stabilan signal",
        }),
      ],
    });

    expect(screen.getAllByText("Nije dostupno").length).toBeGreaterThan(0);
  });

  it("keeps row click, keyboard open and inline action buttons separate", () => {
    const lowCoverRow = makeRow({
      id: 202,
      naziv: "Premium sandala",
      plu: "PLU-202",
      stockState: "critical",
      stockStateLabel: "Kritično",
      stockCoverStatus: "low_cover",
      stockCoverStatusLabel: "Niska pokrivenost",
      signalText: "Dopuni zalihu",
      quantity: 1,
      minimum: 4,
      reorderGap: 3,
    });
    const onOpenDetail = vi.fn();
    const onAddToActions = vi.fn();

    renderTable({
      rows: [lowCoverRow],
      onOpenDetail,
      onAddToActions,
      totalCount: 11,
    });

    const row = screen.getByRole("button", { name: /Otvori detalje za Premium sandala - Kritično/i });
    expect(row).toHaveClass("border-l-[var(--error)]");

    fireEvent.click(row);
    expect(onOpenDetail).toHaveBeenCalledWith(lowCoverRow);

    fireEvent.keyDown(row, { key: "Enter" });
    expect(onOpenDetail).toHaveBeenCalledTimes(2);

    fireEvent.click(screen.getByRole("button", { name: "Dodaj u akcije" }));
    expect(onAddToActions).toHaveBeenCalledWith(lowCoverRow);
    expect(onOpenDetail).toHaveBeenCalledTimes(2);
  });

  it("shows slow-stock review CTA and queued state without enabling duplicate queue action", () => {
    const slowRow = makeRow({
      stockCoverStatus: "slow_stock",
      stockCoverStatusLabel: "Spora zaliha",
      signalText: "Pregledaj sporu zalihu",
    });
    const onAddToActions = vi.fn();
    const onReviewSlowStock = vi.fn();

    renderTable({
      rows: [slowRow],
      onAddToActions,
      onReviewSlowStock,
      isRowQueued: () => true,
    });

    const queuedButton = screen.getByRole("button", { name: "U akcijama" });
    expect(queuedButton).toBeDisabled();

    fireEvent.click(screen.getByRole("button", { name: "Pregledaj sporu zalihu" }));
    expect(onReviewSlowStock).toHaveBeenCalledWith(slowRow);
    expect(onAddToActions).not.toHaveBeenCalled();
  });

  it("guards pagination controls at page boundaries", () => {
    const previous = vi.fn();
    const next = vi.fn();

    renderTable({
      pageNumber: 1,
      totalPages: 2,
      onPreviousPage: previous,
      onNextPage: next,
    });

    expect(screen.getByRole("button", { name: /Idi na prethodnu stranu tabele artikala/ })).toBeDisabled();
    fireEvent.click(screen.getByRole("button", { name: /Idi na sledeću stranu tabele artikala/ }));
    expect(next).toHaveBeenCalledTimes(1);
    expect(previous).not.toHaveBeenCalled();
  });
});

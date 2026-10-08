import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { SKUDetailModal } from "./SKUDetailModal";
import type { InventoryRow } from "./types";

function buildPlaceholderRow(overrides: Partial<InventoryRow> = {}): InventoryRow {
  return {
    id: 999,
    naziv: "SKU bez konteksta",
    plu: null,
    kolicina: 0,
    minimalnaKolicina: 0,
    nabavnaCena: 0,
    estimatedValue: 0,
    idObjekat: 1,
    idDobavljac: null,
    velicina: null,
    velicinaGroup: null,
    stockCoverDays: null,
    stockCoverStatus: "insufficient_data",
    stockCoverStatusLabel: "Nedovoljno podataka",
    sellThroughRatio: null,
    sellThroughStatus: "insufficient_data",
    sellThroughStatusLabel: "Nedovoljno podataka",
    signalConfidencePct: null,
    recommendationAllowed: null,
    reasonCodes: [],
    dataQualityStatus: "insufficient_data",
    supplierName: "Nerasporedjen",
    storeName: "Prodavnica 1",
    quantity: 0,
    minimum: 0,
    reorderGap: 0,
    stockState: "critical",
    stockStateLabel: "Bez zaliha",
    estimatedValueAmount: 0,
    unitCost: 0,
    coverageRatio: null,
    signalText: "Nedovoljno podataka",
    contextStatus: "loadingContext",
    ...overrides,
  };
}

describe("SKUDetailModal placeholder context", () => {
  it("renders loading context without a fake zero baseline", () => {
    render(
      <SKUDetailModal
        detailRow={buildPlaceholderRow()}
        detailData={null}
        detailLoading={false}
        detailError={null}
        detailTab="overview"
        detailSizeCurve={null}
        detailSizeCurveLoading={false}
        onClose={vi.fn()}
        onRetry={vi.fn()}
        onTabChange={vi.fn()}
      />,
    );

    expect(screen.getByText("Učitavam kontekst artikla...")).toBeInTheDocument();
    expect(screen.getAllByText("Nije dostupno").length).toBeGreaterThan(0);
    expect(screen.queryByText("Bez zaliha")).not.toBeInTheDocument();
  });

  it("renders missing context explicitly after detail fetch failure", () => {
    render(
      <SKUDetailModal
        detailRow={buildPlaceholderRow()}
        detailData={null}
        detailLoading={false}
        detailError="Artikal nije pronađen u detaljnom kontekstu."
        detailTab="overview"
        detailSizeCurve={null}
        detailSizeCurveLoading={false}
        onClose={vi.fn()}
        onRetry={vi.fn()}
        onTabChange={vi.fn()}
      />,
    );

    expect(screen.getByText("Kontekst artikla nije pronađen. Prikazuju se samo ograničeni podaci.")).toBeInTheDocument();
    expect(screen.getByText("Artikal nije pronađen u detaljnom kontekstu.")).toBeInTheDocument();
    expect(screen.getAllByText("Nije dostupno").length).toBeGreaterThan(0);
  });

  it("sanitizes technical detail errors while keeping retry available", () => {
    render(
      <SKUDetailModal
        detailRow={buildPlaceholderRow()}
        detailData={null}
        detailLoading={false}
        detailError="NpgsqlException: provider failure at SqlCommand.Execute"
        detailTab="overview"
        detailSizeCurve={null}
        detailSizeCurveLoading={false}
        onClose={vi.fn()}
        onRetry={vi.fn()}
        onTabChange={vi.fn()}
      />,
    );

    expect(screen.getByText("Detalj artikla trenutno nije dostupan.")).toBeInTheDocument();
    expect(screen.queryByText(/NpgsqlException|SqlCommand\.Execute/)).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Pokušaj ponovo učitavanje detalja artikla" })).toBeInTheDocument();
  });

  it("renders the inventory explainability snapshot when backend signal fields are present", () => {
    render(
      <SKUDetailModal
        detailRow={buildPlaceholderRow({
          contextStatus: null,
          stockCoverDays: null,
          stockCoverStatus: "out_of_stock_risk",
          stockCoverStatusLabel: "Rizik rasprodaje",
          sellThroughRatio: 1,
          sellThroughStatus: "good",
          sellThroughStatusLabel: "Dobar sell-through",
          signalConfidencePct: 82,
          recommendationAllowed: true,
          reasonCodes: ["replenish_needed", "stock_cover_out_of_stock_risk"],
          dataQualityStatus: "good",
        })}
        detailData={null}
        detailLoading={false}
        detailError={null}
        detailTab="overview"
        detailSizeCurve={null}
        detailSizeCurveLoading={false}
        onClose={vi.fn()}
        onRetry={vi.fn()}
        onTabChange={vi.fn()}
      />,
    );

    expect(screen.getByRole("region", { name: "Snapshot objašnjenja" })).toBeInTheDocument();
    expect(screen.getByText("82%")).toBeInTheDocument();
    expect(screen.getByText("Dozvoljena")).toBeInTheDocument();
    expect(screen.getByText("Rizik rasprodaje")).toBeInTheDocument();
    expect(screen.getByText("Potrebna dopuna")).toBeInTheDocument();
    expect(screen.queryByText("replenish_needed")).not.toBeInTheDocument();
  });
});

describe("SKUDetailModal size-curve tab", () => {
  function renderSizeCurveTab(detailSizeCurveError: string | null, onRetrySizeCurve = vi.fn()) {
    render(
      <SKUDetailModal
        detailRow={buildPlaceholderRow({ contextStatus: null })}
        detailData={null}
        detailLoading={false}
        detailError={null}
        detailTab="sizeCurve"
        detailSizeCurve={null}
        detailSizeCurveLoading={false}
        detailSizeCurveError={detailSizeCurveError}
        onClose={vi.fn()}
        onRetry={vi.fn()}
        onRetrySizeCurve={onRetrySizeCurve}
        onTabChange={vi.fn()}
      />,
    );
    return onRetrySizeCurve;
  }

  it("shows an explicit retryable error instead of the empty state when the size-curve fetch fails", () => {
    const onRetrySizeCurve = renderSizeCurveTab("Raspodela veličina za ovaj artikal trenutno nije dostupna.");

    expect(screen.getByRole("alert")).toHaveTextContent("Raspodela veličina za ovaj artikal trenutno nije dostupna.");
    expect(screen.queryByText("Nema podataka o raspodeli veličina za ovaj artikal.")).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Pokušaj ponovo učitavanje raspodele veličina" }));
    expect(onRetrySizeCurve).toHaveBeenCalledTimes(1);
  });

  it("sanitizes technical size-curve errors", () => {
    renderSizeCurveTab("NpgsqlException: provider failure at SqlCommand.Execute");

    expect(screen.getByRole("alert")).toHaveTextContent("Raspodela veličina za ovaj artikal trenutno nije dostupna.");
    expect(screen.queryByText(/NpgsqlException|SqlCommand\.Execute/)).not.toBeInTheDocument();
  });

  it("keeps the empty state for a successful fetch without size-curve data", () => {
    renderSizeCurveTab(null);

    expect(screen.getByText("Nema podataka o raspodeli veličina za ovaj artikal.")).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });
});

import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { RebalancingTable } from "./RebalancingTable";

describe("RebalancingTable scope labeling", () => {
  it("shows the selected store scope in the header and empty state", () => {
    render(
      <MemoryRouter>
      <RebalancingTable
        rebalance={{
          generatedAtUtc: "2026-08-10T10:00:00Z",
          totalCount: 0,
          returnedCount: 0,
          totalMatchingCount: 0,
          isTruncated: false,
          snapshotAvailable: true,
          items: [],
        }}
        rebalanceLoading={false}
        rebalanceError={null}
        rows={[]}
        stores={[]}
        displayCount={5}
        scopeLabel="za prodavnicu Prodavnica 1"
        onCompareStores={vi.fn()}
      />
      </MemoryRouter>,
    );

    expect(screen.getByText("Opseg: za prodavnicu Prodavnica 1")).toBeInTheDocument();
    expect(screen.getByText("Nema preporučenih redistribucija za prodavnicu Prodavnica 1.")).toBeInTheDocument();
  });

  it("labels expected sales as an estimate and links to manual transfers", () => {
    render(
      <MemoryRouter>
        <RebalancingTable
          rebalance={{
            generatedAtUtc: "2026-08-10T10:00:00Z",
            totalCount: 2,
            returnedCount: 2,
            totalMatchingCount: 2,
            isTruncated: false,
            snapshotAvailable: true,
            items: [
              { fromStoreId: 1, toStoreId: 2, skuId: 7, sizeCode: "40", recommendedQty: 2, urgency: "urgent", confidence: 0.8, reason: null, expectedSavedSales: 12000, expectedCapitalRelease: null },
              { fromStoreId: 1, toStoreId: 2, skuId: 8, sizeCode: "41", recommendedQty: 1, urgency: "optional", confidence: null, reason: null, expectedSavedSales: null, expectedCapitalRelease: null },
            ],
          }}
          rebalanceLoading={false}
          rebalanceError={null}
          rows={[]}
          stores={[{ storeId: 1, storeName: "Prodavnica 1" }, { storeId: 2, storeName: "Prodavnica 2" }]}
          displayCount={5}
          scopeLabel="za sve prodavnice"
          onCompareStores={vi.fn()}
        />
      </MemoryRouter>,
    );

    expect(screen.getByRole("heading", { name: "Predlozi redistribucije" })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Procena moguće prodaje" })).toBeInTheDocument();
    expect(screen.queryByText(/Sačuvana prodaja|Pametna/)).not.toBeInTheDocument();
    expect(screen.getByText(/nije ostvarena prodaja ni dokazana ušteda/)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Otvori prenose robe" })).toHaveAttribute("href", "/transfers");
    expect(screen.getByText("Nije dostupno")).toBeInTheDocument();
  });
});

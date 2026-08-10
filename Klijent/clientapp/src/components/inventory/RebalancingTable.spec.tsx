import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { RebalancingTable } from "./RebalancingTable";

describe("RebalancingTable scope labeling", () => {
  it("shows the selected store scope in the header and empty state", () => {
    render(
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
      />,
    );

    expect(screen.getByText("Opseg: za prodavnicu Prodavnica 1")).toBeInTheDocument();
    expect(screen.getByText("Nema preporučenih redistribucija za prodavnicu Prodavnica 1.")).toBeInTheDocument();
  });
});

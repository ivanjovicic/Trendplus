import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { InventoryAlertsFeed } from "../../components/inventory/InventoryAlertsFeed";
import { RebalancingTable } from "../../components/inventory/RebalancingTable";

const inventoryRow = {
  id: 101,
  naziv: "Artikal A",
  plu: "SKU-101",
  kolicina: 10,
  minimalnaKolicina: 3,
  nabavnaCena: 100,
  estimatedValue: 1000,
  idObjekat: 1,
  idDobavljac: 7,
  supplierName: "Dobavljac A",
  storeName: "Prodavnica 1",
  quantity: 10,
  minimum: 3,
  reorderGap: 0,
  stockState: "healthy" as const,
  stockStateLabel: "Stabilno",
  estimatedValueAmount: 1000,
  unitCost: 100,
  coverageRatio: 3.33,
  stockCoverDays: 5,
  stockCoverStatus: "healthy",
  stockCoverStatusLabel: "Zdrava pokrivenost",
  sellThroughRatio: 0.5,
  sellThroughStatus: "good",
  sellThroughStatusLabel: "Dobar sell-through",
  signalConfidencePct: 80,
  recommendationAllowed: true,
  signalText: "Stabilan signal",
  dataQualityStatus: "good",
  reasonCodes: [],
};

describe("Inventory snapshot row actionability", () => {
  it("blocks alert confidence and hides raw alert codes when backend denies recommendation", () => {
    render(
      <InventoryAlertsFeed
        alerts={{
          generatedAtUtc: "2026-08-10T10:00:00Z",
          totalCount: 1,
          returnedCount: 1,
          totalMatchingCount: 1,
          isTruncated: false,
          snapshotAvailable: true,
          items: [
            {
              alertType: "raw_internal_alert_code",
              skuId: 101,
              storeId: 7,
              severity: "warning",
              title: "Signal zalihe",
              message: "Provera je potrebna.",
              confidenceScore: 0.75,
              actionability: {
                status: "blocked",
                recommendationAllowed: false,
                dataQualityStatus: "warning",
                reasonLabel: "Nivo inventory signala nije dovoljno precizan za preporuku.",
              },
            },
          ],
        }}
        alertsLoading={false}
        alertsError={null}
        alertSeverityFilter=""
        onSeverityFilterChange={vi.fn()}
        displayCount={5}
        onOpenSizeCurve={vi.fn()}
        onOpenDetail={vi.fn()}
      />,
    );

    expect(screen.queryByText("raw_internal_alert_code")).not.toBeInTheDocument();
    expect(screen.queryByText("75,0%")).not.toBeInTheDocument();
    expect(screen.getByText(/Nivo inventory signala nije dovoljno precizan za preporuku/)).toBeInTheDocument();
    expect(screen.getByText(/Preporuka nije dozvoljena/)).toBeInTheDocument();
  });

  it("keeps a backend-approved true zero confidence visible", () => {
    render(
      <InventoryAlertsFeed
        alerts={{
          generatedAtUtc: "2026-08-10T10:00:00Z",
          totalCount: 1,
          returnedCount: 1,
          totalMatchingCount: 1,
          isTruncated: false,
          snapshotAvailable: true,
          items: [
            {
              alertType: "inventory_missing",
              skuId: 101,
              storeId: 7,
              severity: "critical",
              title: "Nedostatak zalihe",
              message: "Zaliha zahteva proveru.",
              confidenceScore: 0,
              actionability: {
                status: "actionable",
                recommendationAllowed: true,
                dataQualityStatus: "good",
                reasonLabel: "Nedostatak zalihe zahteva proveru.",
              },
            },
          ],
        }}
        alertsLoading={false}
        alertsError={null}
        alertSeverityFilter=""
        onSeverityFilterChange={vi.fn()}
        displayCount={5}
        onOpenSizeCurve={vi.fn()}
        onOpenDetail={vi.fn()}
      />,
    );

    expect(screen.getByText("0,0%")).toBeInTheDocument();
  });

  it("uses backend-safe rebalance reason text for blocked rows", () => {
    render(
      <RebalancingTable
        rebalance={{
          generatedAtUtc: "2026-08-10T10:00:00Z",
          totalCount: 1,
          returnedCount: 1,
          totalMatchingCount: 1,
          isTruncated: false,
          snapshotAvailable: true,
          items: [
            {
              fromStoreId: 1,
              toStoreId: 2,
              skuId: 101,
              sizeCode: "42",
              recommendedQty: 0,
              urgency: "recommended",
              confidence: 0.8,
              reason: "unknown_reason_code",
              expectedSavedSales: 0,
              expectedCapitalRelease: 0,
              actionability: {
                status: "blocked",
                recommendationAllowed: false,
                dataQualityStatus: "warning",
                reasonLabel: "Razlog redistribucije nije dovoljno precizan za preporuku.",
              },
            },
          ],
        }}
        rebalanceLoading={false}
        rebalanceError={null}
        rows={[inventoryRow]}
        stores={[
          { storeId: 1, storeName: "Prodavnica 1" },
          { storeId: 2, storeName: "Prodavnica 2" },
        ]}
        displayCount={5}
        scopeLabel="za sve prodavnice"
        onCompareStores={vi.fn()}
      />,
    );

    expect(screen.queryByText("unknown_reason_code")).not.toBeInTheDocument();
    expect(screen.getByText(/Razlog redistribucije nije dovoljno precizan za preporuku/)).toBeInTheDocument();
    expect(screen.getByText(/0\s*RSD/)).toBeInTheDocument();
  });
});

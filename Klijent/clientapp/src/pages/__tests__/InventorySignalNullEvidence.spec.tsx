import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { InventoryAlertsFeed } from "../../components/inventory/InventoryAlertsFeed";
import { RebalancingTable } from "../../components/inventory/RebalancingTable";
import { SizeCurveVisualization } from "../../components/inventory/SizeCurveVisualization";

describe("Inventory signal null evidence surfaces", () => {
  it("renders missing alert severity and confidence explicitly", () => {
    render(
      <InventoryAlertsFeed
        alerts={{
          generatedAtUtc: "2026-08-10T10:00:00Z",
          totalCount: 1,
          returnedCount: 1,
          totalMatchingCount: 2,
          isTruncated: true,
          snapshotAvailable: true,
          warning: "Inventory alert snapshot sadrzi redove sa nepotpunom signalnom evidencijom.",
          items: [
            {
              alertType: "inventory_missing",
              skuId: 101,
              storeId: 7,
              sizeCode: "42",
              severity: null,
              title: "Alert bez severity",
              message: "Severity nije dostupan.",
              confidenceScore: null,
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

    expect(screen.getByText("Nepoznato")).toBeInTheDocument();
    expect(screen.getByText("Prikazano 1 od 2 alerta")).toBeInTheDocument();
    expect(screen.getAllByText("N/A").length).toBeGreaterThan(0);
    expect(screen.getByText(/nepotpunom signalnom evidencijom/i)).toBeInTheDocument();
  });

  it("renders missing rebalance urgency and quantity explicitly", () => {
    render(
      <RebalancingTable
        rebalance={{
          generatedAtUtc: "2026-08-10T10:00:00Z",
          totalCount: 1,
          returnedCount: 1,
          totalMatchingCount: 2,
          isTruncated: true,
          snapshotAvailable: true,
          warning: "Rebalance snapshot sadrzi redove sa nepotpunom signalnom evidencijom.",
          items: [
            {
              fromStoreId: 1,
              toStoreId: 2,
              skuId: 101,
              sizeCode: "42",
              recommendedQty: null,
              urgency: null,
              confidence: null,
              reason: "Signal nije potpun.",
              expectedSavedSales: null,
              expectedCapitalRelease: null,
            },
          ],
        }}
        rebalanceLoading={false}
        rebalanceError={null}
        rows={[
          {
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
            stockState: "healthy",
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
          },
        ]}
        stores={[
          { storeId: 1, storeName: "Prodavnica 1" },
          { storeId: 2, storeName: "Prodavnica 2" },
        ]}
        displayCount={5}
        scopeLabel="za sve prodavnice"
        onCompareStores={vi.fn()}
      />,
    );

    expect(screen.getByText("Nepoznato")).toBeInTheDocument();
    expect(screen.getByText("Prikazano 1 od 2 predloga")).toBeInTheDocument();
    expect(screen.getAllByText("N/A").length).toBeGreaterThan(0);
    expect(screen.getByText(/nepotpunom signalnom evidencijom/i)).toBeInTheDocument();
  });

  it("renders missing size curve evidence explicitly", () => {
    const consoleWarnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});
    try {
      render(
        <SizeCurveVisualization
          items={[
            {
              skuId: 101,
              storeId: 7,
              sizeCode: "42",
              actualSizeShare: 0,
              idealSizeShare: null,
              deviationPct: 0,
              isCoreSizeMissing: null,
              isDeadSize: false,
              brokenRun: null,
              curveConfidence: 0,
              evidenceStatus: "missing",
              reasonCodes: ["missing_ideal"],
            },
          ]}
          cardLimit={4}
        />,
      );

      expect(screen.getAllByText("Evidencija: nedostaje").length).toBeGreaterThan(0);
      expect(screen.getAllByText("N/A").length).toBeGreaterThan(0);
      expect(screen.getAllByText("0pp").length).toBeGreaterThan(0);
      expect(screen.getAllByText("0,0%").length).toBeGreaterThan(0);
    } finally {
      consoleWarnSpy.mockRestore();
    }
  });
});

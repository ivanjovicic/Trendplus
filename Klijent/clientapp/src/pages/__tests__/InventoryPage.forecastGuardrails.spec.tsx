import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { DemandForecastPanel } from "../../components/inventory/DemandForecastPanel";

describe("DemandForecastPanel guardrails", () => {
  it("labels forecast replenishment as a signal-driven estimate", () => {
    const onSuggestRestock = vi.fn();

    render(
      <DemandForecastPanel
        forecast={{
          generatedAtUtc: "2026-06-22T10:00:00Z",
          totalCount: 1,
          snapshotAvailable: true,
          warning: null,
          items: [
            {
              skuId: 501,
              storeId: 1,
              sizeCode: "42",
              forecast7d: 3.2,
              forecast14d: 5.4,
              forecast28d: 9.8,
              probabilityOfOOSIn7d: 0.82,
              overstockRisk: 0.12,
              confidenceScore: 0.76,
              explanation: "Prodajni signal ukazuje na mogucu rasprodaju.",
            },
          ],
        }}
        forecastLoading={false}
        forecastError={null}
        rows={[
          {
            id: 501,
            naziv: "Model A",
            plu: "SKU-501",
            kolicina: 4,
            minimalnaKolicina: 2,
            nabavnaCena: 1000,
            estimatedValue: 4000,
            idObjekat: 1,
            idDobavljac: 7,
            supplierName: "Dobavljac A",
            storeName: "Prodavnica 1",
            quantity: 4,
            minimum: 2,
            reorderGap: 0,
            stockState: "healthy",
            stockStateLabel: "Stabilno",
            estimatedValueAmount: 4000,
            unitCost: 1000,
            coverageRatio: 2,
            stockCoverDays: 4,
            stockCoverStatus: "low_cover",
            stockCoverStatusLabel: "Niska pokrivenost",
            sellThroughRatio: 0.45,
            sellThroughStatus: "warning",
            sellThroughStatusLabel: "Sell-through upozorenje",
            signalConfidencePct: 76,
            recommendationAllowed: true,
            signalText: "Prati signal",
            dataQualityStatus: "good",
            reasonCodes: [],
          },
        ]}
        stores={[{ storeId: 1, storeName: "Prodavnica 1" }]}
        oosThreshold={0.25}
        overstockThreshold={0.5}
        oosDisplayCount={5}
        overstockDisplayCount={5}
        onSuggestRestock={onSuggestRestock}
      />,
    );

    expect(screen.getByRole("heading", { name: /Procena potra/i })).toBeInTheDocument();
    expect(screen.getByText(/signalni indikatori, ne automatski nalozi/i)).toBeInTheDocument();
    expect(screen.getByText(/Predlozi dopune su procene zasnovane na forecast signalu/i)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /SKU 501/i }));

    expect(onSuggestRestock).toHaveBeenCalledTimes(1);
  });
});

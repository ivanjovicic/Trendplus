import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { DemandForecastPanel } from "./DemandForecastPanel";

describe("DemandForecastPanel forecast qty copy", () => {
  it("labels forecast restock hints as demand qty instead of final order qty", () => {
    render(
      <DemandForecastPanel
        forecast={{
          generatedAtUtc: "2026-08-10T10:00:00Z",
          totalCount: 0,
          snapshotAvailable: true,
          provenanceStatus: "owner_unknown",
          materializerOwner: "none",
          isAuthoritativeForecast: false,
          items: [],
        }}
        forecastLoading={false}
        forecastError={null}
        rows={[]}
        stores={[]}
        oosThreshold={0.25}
        overstockThreshold={0.5}
        oosDisplayCount={5}
        overstockDisplayCount={5}
        onSuggestRestock={vi.fn()}
      />,
    );

    expect(screen.getByText("Predlozi dopune su procene zasnovane na signalu prognoze, ne finalna narudžbina. Potvrdite osnovu zalihe i operativni kontekst pre naručivanja.")).toBeInTheDocument();
    expect(screen.getByText(/Ograničeni signal: izvor podataka nije dokazan/i)).toBeInTheDocument();
  });

  it("labels missing relation as unavailable without claiming a materializer", () => {
    render(
      <DemandForecastPanel
        forecast={{
          generatedAtUtc: "2026-08-10T10:00:00Z",
          totalCount: 0,
          snapshotAvailable: false,
          provenanceStatus: "missing_relation",
          isAuthoritativeForecast: false,
          items: [],
        }}
        forecastLoading={false}
        forecastError={null}
        rows={[]}
        stores={[]}
        oosThreshold={0.25}
        overstockThreshold={0.5}
        oosDisplayCount={5}
        overstockDisplayCount={5}
        onSuggestRestock={vi.fn()}
      />,
    );

    expect(screen.getByText("Prognoza trenutno nije dostupna. Signal snapshot nije učitan ili nije dostupan.")).toBeInTheDocument();
    expect(screen.queryByText(/missing_relation/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/Snapshot tabela je prazna/i)).not.toBeInTheDocument();
  });

  it("does not rank missing risk evidence as stable or zero risk", () => {
    render(
      <DemandForecastPanel
        forecast={{
          generatedAtUtc: "2026-08-10T10:00:00Z",
          totalCount: 1,
          snapshotAvailable: true,
          meta: {
            success: true,
            warningCode: "inventory_signal_provenance_untrusted",
            warningMessage: "Signal snapshot nema dovoljnu evidenciju.",
            isPartial: true,
            dataQualityStatus: "warning",
          },
          items: [{
            skuId: 101,
            storeId: 7,
            sizeCode: "42",
            forecast7d: null,
            forecast14d: null,
            forecast28d: null,
            probabilityOfOOSIn7d: null,
            overstockRisk: null,
            confidenceScore: null,
            explanation: "Nedostaje signal.",
          }],
        }}
        forecastLoading={false}
        forecastError={null}
        rows={[]}
        stores={[]}
        oosThreshold={0.25}
        overstockThreshold={0.5}
        oosDisplayCount={5}
        overstockDisplayCount={5}
        onSuggestRestock={vi.fn()}
      />,
    );

    expect(screen.getByText(/rizik nestašice nije dostupan/i)).toBeInTheDocument();
    expect(screen.getByText(/rizik viška nije dostupan/i)).toBeInTheDocument();
    expect(screen.queryByText(/Status: stabilno/i)).not.toBeInTheDocument();
  });
});

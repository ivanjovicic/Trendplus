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

    expect(screen.getByText("Predlozi dopune su procene zasnovane na forecast signalu, ne finalna narudžbina. Potvrdite stock baseline i operativni kontekst pre naručivanja.")).toBeInTheDocument();
  });
});

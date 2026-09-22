import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import AnalyticsDetailView from "../AnalyticsDetailView";
import { getAnalyticsDetail } from "../../../services/analyticsDetailApi";

vi.mock("../../../services/analyticsDetailApi", () => ({
  getAnalyticsDetail: vi.fn(),
}));

vi.mock("../../../services/analyticsTableState", () => ({
  getAnalyticsDetailSnapshot: vi.fn(() => null),
}));

describe("AnalyticsDetailView decision trust projection", () => {
  beforeEach(() => {
    vi.mocked(getAnalyticsDetail).mockReset();
  });

  it("renders server recommendation, unavailable confidence and provenance without inventing zero", async () => {
    vi.mocked(getAnalyticsDetail).mockResolvedValue({
      table: "shoe-type-sales-stats",
      recordId: "unknown-nepoznato",
      title: "Nepoznato",
      subtitle: "Prodaja po tipu obuće",
      fields: [
        { key: "marginPct", label: "Marža (%)", value: null, dataType: "percent" },
        { key: "prePostNivelacijaRevenueImpactPct", label: "Uticaj nivelacije na promet (%)", value: null, dataType: "percent" },
      ],
      metadata: [{ key: "dataScope", label: "Opseg podataka", value: "Svi podaci" }],
      recommendation: {
        status: "do_not_trust",
        label: "Ne verovati preporuci",
        summary: "Identitet tipa obuće nije poznat; preporuka nije bezbedna za poslovnu odluku.",
        confidencePct: null,
        reliabilityPct: null,
        dataQualityStatus: "critical",
        recommendationAllowed: false,
        reasonCodes: ["unknown_entity"],
      },
      provenance: {
        requestedFromUtc: "2026-01-01T00:00:00Z",
        requestedToUtc: "2026-12-31T23:59:59Z",
        effectiveFromUtc: "2026-01-01T00:00:00Z",
        effectiveToUtc: "2026-12-31T23:59:59Z",
        season: null,
        storeId: null,
        dataScope: "all",
        generatedAtUtc: "2026-09-22T18:00:00Z",
        freshness: "fresh",
        dataQualityStatus: "critical",
        snapshotActive: false,
        snapshotGeneratedAtUtc: null,
        fallbackApplied: false,
        recommendationAllowed: false,
      },
    });

    render(<AnalyticsDetailView table="shoe-type-sales-stats" recordId="unknown-nepoznato" queryString="?dataScope=all" />);

    expect(await screen.findByText("Ne verovati preporuci")).toBeInTheDocument();
    expect(screen.getByText("Identitet tipa obuće nije poznat; preporuka nije bezbedna za poslovnu odluku.")).toBeInTheDocument();
    expect(screen.getAllByText("Nije dostupno")).toHaveLength(2);
    expect(screen.getByText("Nepoznat identitet")).toBeInTheDocument();
    expect(screen.getByText("Efektivni period")).toBeInTheDocument();
    expect(screen.getAllByText("Svi podaci")).toHaveLength(2);
  });
});

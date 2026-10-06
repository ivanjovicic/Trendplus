import { describe, expect, it } from "vitest";
import { buildSupplierOverviewHighlights, type SupplierOverviewHighlightRow } from "../supplierOverviewHighlights";

function row(name: string, revenue: number, pop: number | null, extra: Partial<SupplierOverviewHighlightRow> = {}): SupplierOverviewHighlightRow {
  return {
    dobavljacNaziv: name,
    isUnknown: false,
    ukupanPromet: revenue,
    popRevenueChangePct: pop,
    ...extra,
  };
}

describe("buildSupplierOverviewHighlights", () => {
  it("picks the largest known supplier and the largest backend PoP rise and fall", () => {
    const result = buildSupplierOverviewHighlights([
      row("Alfa", 500, 4),
      row("Beta", 900, -12),
      row("Gama", 300, 31),
      row("Delta", 200, -3),
    ]);
    expect(result.leader?.dobavljacNaziv).toBe("Beta");
    expect(result.topGrowth?.dobavljacNaziv).toBe("Gama");
    expect(result.topDecline?.dobavljacNaziv).toBe("Beta");
  });

  it("never lets an unknown supplier or a missing/non-finite value win", () => {
    const result = buildSupplierOverviewHighlights([
      row("Nepoznato", 5000, 99, { isUnknown: true }),
      row("Alfa", 400, null),
      row("Beta", Number.NaN, Number.POSITIVE_INFINITY),
      row("Gama", 100, Number.NaN),
    ]);
    expect(result.leader?.dobavljacNaziv).toBe("Alfa");
    expect(result.topGrowth).toBeNull();
    expect(result.topDecline).toBeNull();
  });

  it("does not treat zero PoP as growth or decline and ignores non-positive revenue for the leader", () => {
    const result = buildSupplierOverviewHighlights([row("Alfa", 0, 0), row("Beta", -50, 0)]);
    expect(result.leader).toBeNull();
    expect(result.topGrowth).toBeNull();
    expect(result.topDecline).toBeNull();
  });
});

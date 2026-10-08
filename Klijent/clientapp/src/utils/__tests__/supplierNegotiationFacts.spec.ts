import { describe, expect, it } from "vitest";
import { buildSupplierNegotiationFacts, type SupplierNegotiationFactInput } from "../supplierNegotiationFacts";

const known: SupplierNegotiationFactInput = {
  revenue: 1_200_000,
  units: 400,
  marginContribution: 300_000,
  marginPct: 25,
  marginCoveragePct: 80,
  revenueTrendPct: -12.5,
  inventoryValue: 900_000,
  inventoryUnits: 600,
  inventoryValueCoveragePct: 75,
  agedValue: 250_000,
  agedUnits: 120,
  agedValueCoveragePct: 50,
};

describe("buildSupplierNegotiationFacts", () => {
  it("formats up to five facts from known backend values with partial coverage stated", () => {
    const facts = buildSupplierNegotiationFacts(known);
    expect(facts).toHaveLength(5);
    expect(facts[1]).toMatch(/Maržni doprinos .*poznata za 80,0% prometa/);
    expect(facts[2]).toMatch(/Promet -12,5% u odnosu na prethodni period/);
    expect(facts[3]).toMatch(/delimično: pokrivenost 75,0%/);
    expect(facts[4]).toMatch(/starije od 90 dana .*delimično: pokrivenost 50,0%/);
  });

  it("omits unknown facts instead of presenting them as zero", () => {
    const facts = buildSupplierNegotiationFacts({
      ...known,
      marginCoveragePct: 0,
      revenueTrendPct: null,
      inventoryValue: null,
      agedValue: null,
    });
    expect(facts).toHaveLength(1);
    expect(facts.join(" ")).not.toMatch(/(^|\s)0 RSD|Nije dostupno/);
  });

  it("never derives an order recommendation", () => {
    expect(buildSupplierNegotiationFacts(known).join(" ")).not.toMatch(/poruč|naruč|BUY_/i);
  });
});

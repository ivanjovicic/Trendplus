import { describe, expect, it } from "vitest";
import {
  chunkProductDecisionActionStatusLookups,
  productDecisionActionStatusLookupSignature,
} from "../productDecisionActionStatus";

describe("product decision action-status batches", () => {
  it("keeps every live-sized lookup within the backend cap", () => {
    const items = Array.from({ length: 1200 }, (_, index) => ({
      sourceType: "product",
      sourceKey: `product:${index}`,
    }));

    const chunks = chunkProductDecisionActionStatusLookups(items);

    expect(chunks.map((chunk) => chunk.length)).toEqual([1000, 200]);
    expect(chunks.flat()).toEqual(items);
  });

  it("uses the same signature when only display order changes", () => {
    const first = [
      { sourceType: "product", sourceKey: "b" },
      { sourceType: "product", sourceKey: "a" },
    ];
    const second = [...first].reverse();

    expect(productDecisionActionStatusLookupSignature(first))
      .toBe(productDecisionActionStatusLookupSignature(second));
  });
});

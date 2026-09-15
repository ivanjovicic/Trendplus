import { describe, expect, it } from "vitest";
import { buildForecastRestockSuggestion, resolveForecastRestockDaysSinceMovement } from "../inventoryUtils";
import { buildInventoryRow } from "../inventoryUtils";

const row = buildInventoryRow({
  id: 999,
  naziv: "Artikal A",
  plu: "PLU-999",
  kolicina: 10,
  minimalnaKolicina: 3,
  nabavnaCena: 100,
  estimatedValue: 1000,
  idObjekat: 1,
  idDobavljac: null,
}, [{ storeId: 1, storeName: "Prodavnica 1" }], []);

const signal = {
  skuId: 999,
  storeId: 1,
  sizeCode: "42",
  forecast7d: 2.5,
  probabilityOfOOSIn7d: 0.82,
};

describe("forecast restock age evidence", () => {
  it("returns null when detail is not loaded for the sku", () => {
    expect(resolveForecastRestockDaysSinceMovement(999, null, null, false)).toBeNull();
  });

  it("returns null while detail is loading for the matching sku", () => {
    expect(resolveForecastRestockDaysSinceMovement(999, { id: 999 }, null, true)).toBeNull();
  });

  it("returns null when detail belongs to a different sku", () => {
    expect(resolveForecastRestockDaysSinceMovement(999, { id: 888 }, { id: 888, daysSinceMovement: 12 }, false)).toBeNull();
  });

  it("preserves measured zero days from matching detail", () => {
    expect(resolveForecastRestockDaysSinceMovement(999, { id: 999 }, { id: 999, daysSinceMovement: 0 }, false)).toBe(0);
  });

  it("preserves positive measured days from matching detail", () => {
    expect(resolveForecastRestockDaysSinceMovement(999, { id: 999 }, { id: 999, daysSinceMovement: 14 }, false)).toBe(14);
  });

  it("treats non-finite detail age as unavailable", () => {
    expect(resolveForecastRestockDaysSinceMovement(999, { id: 999 }, { id: 999, daysSinceMovement: Number.NaN }, false)).toBeNull();
  });

  it("does not fabricate zero days in workflow suggestion when age is missing", () => {
    const suggestion = buildForecastRestockSuggestion(row, signal, [{ storeId: 1, storeName: "Prodavnica 1" }], null);

    expect(suggestion.daysSinceMovement).toBeNull();
    expect(suggestion.note).toMatch(/nedostupno/i);
    expect(suggestion.note).not.toMatch(/Dana bez kretanja: 0 dana/);
  });

  it("keeps measured zero visible in workflow suggestion note", () => {
    const suggestion = buildForecastRestockSuggestion(row, signal, [{ storeId: 1, storeName: "Prodavnica 1" }], 0);

    expect(suggestion.daysSinceMovement).toBe(0);
    expect(suggestion.note).toMatch(/Dana bez kretanja: 0 dana/);
  });
});

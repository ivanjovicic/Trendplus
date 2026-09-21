import { describe, expect, it } from "vitest";
import { buildInventoryServerExportContractNote, inventoryDataScopeLabel } from "../inventoryUtils";

describe("inventory export contract helpers", () => {
  it("maps data scope labels for export metadata", () => {
    expect(inventoryDataScopeLabel("all")).toBe("Sve");
    expect(inventoryDataScopeLabel("existing")).toBe("Postojeći");
    expect(inventoryDataScopeLabel("imported")).toBe("Importovani");
  });

  it("documents current-stock snapshot semantics and signal-window difference", () => {
    const note = buildInventoryServerExportContractNote("existing");

    expect(note).toMatch(/snimak trenutnog stanja zaliha/i);
    expect(note).toMatch(/Postojeći/);
    expect(note).toMatch(/30-dnevni period prodaje/i);
    expect(note).toMatch(/nisu deo server dokumenta/i);
  });
});

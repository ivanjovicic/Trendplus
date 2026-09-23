import { describe, expect, it } from "vitest";
import { colorDisplayName, colorIdentityKey } from "../colorIdentity";

describe("color identity", () => {
  it("merges casing and surrounding whitespace without changing display text", () => {
    expect(colorIdentityKey(" Crna ")).toBe(colorIdentityKey("crna"));
    expect(colorDisplayName(" Crna ")).toBe("Crna");
  });

  it("canonicalizes equivalent Unicode forms while preserving diacritics", () => {
    expect(colorIdentityKey("Bež")).toBe(colorIdentityKey("Bež"));
    expect(colorDisplayName("Bež")).toBe("Bež");
    expect(encodeURIComponent(colorIdentityKey("Bež"))).toBe("BE%C5%BD");
  });

  it("uses one explicit unknown bucket", () => {
    expect(colorDisplayName("   ")).toBe("Nepoznato");
    expect(colorDisplayName("nepoznato")).toBe("Nepoznato");
    expect(colorIdentityKey(null)).toBe("NEPOZNATO");
  });

  it("does not erase meaningful internal spacing", () => {
    expect(colorIdentityKey("Svetlo plava")).not.toBe(colorIdentityKey("Svetlo  plava"));
  });
});

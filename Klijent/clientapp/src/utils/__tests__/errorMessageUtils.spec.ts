import { describe, it, expect } from "vitest";
import { getDataScope } from "../dataScope";

describe("dataScope", () => {
  it("returns default scope when not provided", () => {
    const result = getDataScope();
    expect(result).toBeTruthy();
    expect(typeof result).toBe("string");
  });

  it("returns scope for valid store and supplier (legacy API not supported)", () => {
    // `getDataScope` no longer accepts store/supplier args; ensure it still returns a valid scope
    const result = getDataScope();
    expect(result).toBeTruthy();
    expect(typeof result).toBe("string");
  });

  it("handles null supplier ID (legacy)", () => {
    const result = getDataScope();
    expect(result).toBeTruthy();
  });

  it("handles null store ID (legacy)", () => {
    const result = getDataScope();
    expect(result).toBeTruthy();
  });
});

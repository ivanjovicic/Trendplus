import { describe, it, expect } from "vitest";
import { getDataScope } from "../dataScope";

describe("dataScope", () => {
  it("returns default scope when not provided", () => {
    const result = getDataScope(undefined, undefined);
    expect(result).toBeTruthy();
    expect(typeof result).toBe("string");
  });

  it("returns scope for valid store and supplier", () => {
    const result = getDataScope(1, 2);
    expect(result).toBeTruthy();
    expect(typeof result).toBe("string");
  });

  it("handles null supplier ID", () => {
    const result = getDataScope(1, null);
    expect(result).toBeTruthy();
  });

  it("handles null store ID", () => {
    const result = getDataScope(null, 2);
    expect(result).toBeTruthy();
  });
});

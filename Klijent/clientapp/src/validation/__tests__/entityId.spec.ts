import { describe, expect, it } from "vitest";
import { entityId, formatEntityFallbackLabel, nullableEntityId, parseEntityIdParam } from "../entityId";

describe("entityId", () => {
  it.each([-2004188974, -598733481, -999999999, 0, 2082886995, -2147483648, 2147483647])("accepts Access ID %s", (value) => {
    expect(entityId.safeParse(value).success).toBe(true);
  });

  it("accepts null only in the nullable variant", () => {
    expect(nullableEntityId.safeParse(null).success).toBe(true);
    expect(entityId.safeParse(null).success).toBe(false);
  });

  it.each([1.5, Number.NaN, Number.POSITIVE_INFINITY, 2 ** 31, -(2 ** 31) - 1])("rejects %s", (value) => {
    expect(entityId.safeParse(value).success).toBe(false);
  });
});

describe("parseEntityIdParam", () => {
  it("keeps the sign of negative IDs", () => {
    expect(parseEntityIdParam("-2122024036")).toBe(-2122024036);
    expect(parseEntityIdParam(" 42 ")).toBe(42);
    expect(parseEntityIdParam("0")).toBe(0);
  });

  it.each(["abc", "--1", "", "12a", "1.5", "2147483648", null, undefined])("returns null for %s", (value) => {
    expect(parseEntityIdParam(value)).toBeNull();
  });
});

describe("formatEntityFallbackLabel", () => {
  it("includes the ID when known and omits it for null", () => {
    expect(formatEntityFallbackLabel("supplier", -2122024036)).toBe("Nepoznat dobavljač (ID -2122024036)");
    expect(formatEntityFallbackLabel("store", null)).toBe("Nepoznat objekat");
  });
});

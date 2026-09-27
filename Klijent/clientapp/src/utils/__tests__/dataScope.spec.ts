import { describe, expect, it } from "vitest";
import { dataScopeLabel } from "../dataScope";

describe("dataScopeLabel", () => {
  it.each([
    ["all", "Svi podaci"],
    ["existing", "Postojeći"],
    ["imported", "Uvezeni"],
  ] as const)("renders %s as %s", (scope, label) => {
    expect(dataScopeLabel(scope)).toBe(label);
  });
});

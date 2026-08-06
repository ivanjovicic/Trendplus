import { describe, expect, it } from "vitest";
import { getHeaderRouteCommands, resolveHeaderNavigation } from "../headerNavigation";

describe("headerNavigation", () => {
  it("builds dynamic breadcrumbs for detail routes", () => {
    const result = resolveHeaderNavigation("/analytics/products/123/edit");

    expect(result.group.label).toBe("Analitika");
    expect(result.item?.label).toBe("Odluke o proizvodima");
    expect(result.trail.map((entry) => entry.label)).toEqual([
      "Analitika",
      "Odluke o proizvodima",
      "123",
      "Edit",
    ]);
  });

  it("flattens route commands with sidebar group labels", () => {
    const commands = getHeaderRouteCommands();

    expect(commands.some((entry) => entry.label === "Pilot spremnost")).toBe(true);
    expect(commands.some((entry) => entry.to === "/analytics/decision-board")).toBe(true);
    expect(commands.find((entry) => entry.label === "Pilot spremnost")?.groupLabel).toBe("Executive");
  });
});

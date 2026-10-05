import { describe, expect, it } from "vitest";
import { CORE_ANALYTICS_ROUTE_DEFINITIONS } from "../analyticsRouteDefinitions";

describe("analyticsRouteDefinitions nivelacija labels", () => {
  it("uses canonical Serbian screen names for nivelacija analytics routes", () => {
    const prePost = CORE_ANALYTICS_ROUTE_DEFINITIONS.find((route) => route.path === "/analytics/nivelacije-pre-post");
    const prioriteti = CORE_ANALYTICS_ROUTE_DEFINITIONS.find((route) => route.path === "/analytics/pre-nivelacija-prioriteti");

    expect(prePost?.label).toBe("Pre/Posle nivelacije");
    expect(prioriteti?.label).toBe("Prioriteti nivelacije");
  });
});

import { describe, it, expect } from "vitest";
import { NAV_GROUPS } from "../navConfig";

describe("navConfig", () => {
  it("contains Pilot spremnost item in analytics group", () => {
    const analyticsGroup = NAV_GROUPS.find((group) => group.id === "analytics");
    expect(analyticsGroup).toBeDefined();

    const readinessItem = analyticsGroup?.items.find((item) => item.to === "/analytics/pilot-readiness");
    expect(readinessItem).toBeDefined();
    expect(readinessItem?.label).toBe("Pilot spremnost");
  });

  it("contains Izvršni board item in analytics group", () => {
    const analyticsGroup = NAV_GROUPS.find((group) => group.id === "analytics");
    expect(analyticsGroup).toBeDefined();

    const boardItem = analyticsGroup?.items.find((item) => item.to === "/analytics/decision-board");
    expect(boardItem).toBeDefined();
    expect(boardItem?.label).toBe("Izvršni board");
  });

  it("contains Konfiguracija item in admin group", () => {
    const adminGroup = NAV_GROUPS.find((group) => group.id === "admin");
    expect(adminGroup).toBeDefined();

    const configItem = adminGroup?.items.find((item) => item.to === "/admin/configuration");
    expect(configItem).toBeDefined();
    expect(configItem?.label).toBe("Konfiguracija");
  });
});

import { describe, it, expect } from "vitest";
import { NAV_GROUPS } from "../navConfig";

describe("navConfig", () => {
  it("contains Konfiguracija item in admin group", () => {
    const adminGroup = NAV_GROUPS.find((group) => group.id === "admin");
    expect(adminGroup).toBeDefined();

    const configItem = adminGroup?.items.find((item) => item.to === "/admin/configuration");
    expect(configItem).toBeDefined();
    expect(configItem?.label).toBe("Konfiguracija");
  });
});

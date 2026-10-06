import { describe, expect, it } from "vitest";
import { NAV_GROUPS } from "../navConfig";

function findGroup(id: string) {
  return NAV_GROUPS.find((group) => group.id === id);
}

describe("navConfig", () => {
  it("splits analytics navigation into IA groups", () => {
    expect(findGroup("analytics-executive")?.sidebarLabel).toBe("Executive");
    expect(findGroup("analytics-decisions")?.sidebarLabel).toBe("Odluke");
    expect(findGroup("analytics-operations")?.sidebarLabel).toBe("Operacije");
    expect(findGroup("analytics-data-quality")?.sidebarLabel).toBe("Kvalitet podataka");
    expect(findGroup("analytics-reports-legacy")?.sidebarLabel).toBe("Izveštaji / Legacy");
  });

  it("keeps legacy and support screens clearly labeled", () => {
    const reportsGroup = findGroup("analytics-reports-legacy");
    const adminGroup = findGroup("admin");

    expect(reportsGroup?.items.find((item) => item.to === "/analytics/supplier/report")?.badge?.label).toBe("Izveštaj");
    expect(reportsGroup?.items.some((item) => item.to === "/analytics/insight-studio")).toBe(false);
    expect(reportsGroup?.items.some((item) => item.to === "/analytics-details")).toBe(false);
    expect(adminGroup?.items.find((item) => item.to === "/admin/common-products")?.badge?.label).toBe("Support");
  });

  it("gives every Operacije entry a distinct icon", () => {
    const operationsItems = findGroup("analytics-operations")?.items ?? [];
    const icons = operationsItems.map((item) => item.icon);

    expect(operationsItems.length).toBeGreaterThan(1);
    expect(new Set(icons).size).toBe(icons.length);
  });

  it("groups canonical supplier sales with operational reports and keeps supplier decisions separate", () => {
    const operationsItems = findGroup("analytics-operations")?.items ?? [];
    const operationsRoutes = new Set(operationsItems.map((item) => item.to));
    const decisionItems = findGroup("analytics-decisions")?.items ?? [];
    const allItems = NAV_GROUPS.flatMap((group) => group.items);

    expect(operationsRoutes.has("/analytics/supplier-sales-stats")).toBe(false);
    expect(operationsRoutes.has("/analytics/dobavljaci-tipovi-obuce")).toBe(false);
    expect(allItems.filter((item) => item.to === "/analytics/supplier")).toHaveLength(1);
    expect(operationsRoutes.has("/analytics/supplier")).toBe(true);
    expect(decisionItems.some((item) => item.to === "/analytics/supplier")).toBe(false);
    expect(operationsItems.find((item) => item.to === "/analytics/supplier")?.label).toBe("Prodaja po dobavljačima");
    expect(operationsRoutes.has("/analytics/shoe-type-sales-stats")).toBe(true);
    expect(operationsRoutes.has("/analytics/color-sales-stats")).toBe(true);
    expect(operationsRoutes.has("/analytics/daily-sales")).toBe(true);
    expect(decisionItems.some((item) => item.to === "/analytics/supplier-decision-hub")).toBe(true);
    expect(findGroup("analytics-operations")?.items.find((item) => item.to === "/analytics/daily-sales")?.label).toBe("Prodaja po smenama");
    expect(findGroup("analytics-operations")?.items.findIndex((item) => item.to === "/analytics/supplier")).toBeLessThan(
      findGroup("analytics-operations")?.items.findIndex((item) => item.to === "/analytics/daily-sales") ?? -1,
    );
    expect(operationsRoutes.has("/analytics/nivelacije-pre-post")).toBe(true);
    expect(findGroup("analytics-operations")?.items.find((item) => item.to === "/analytics/color-sales-stats")?.badge).toMatchObject({
      label: "Analiza",
      tone: "info",
    });
  });

  it("preserves representative analytics and admin routes", () => {
    const routeSet = new Set(NAV_GROUPS.flatMap((group) => group.items.map((item) => item.to)));

    expect(routeSet.has("/analytics/pilot-readiness")).toBe(true);
    expect(routeSet.has("/analytics/decision-board")).toBe(true);
    expect(routeSet.has("/analytics/products")).toBe(true);
    expect(routeSet.has("/analytics/inventory")).toBe(true);
    expect(routeSet.has("/analytics/data-quality")).toBe(true);
    expect(routeSet.has("/analytics/supplier/report")).toBe(true);
    expect(routeSet.has("/admin/common-products")).toBe(true);
  });
});

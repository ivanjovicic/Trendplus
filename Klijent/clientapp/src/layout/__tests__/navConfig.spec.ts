import { describe, expect, it } from "vitest";
import appSource from "../../App.tsx?raw";
import { NAV_GROUPS } from "../navConfig";
import { CORE_ANALYTICS_ROUTE_DEFINITIONS } from "../../routes/analyticsRouteDefinitions";

function findGroup(id: string) {
  return NAV_GROUPS.find((group) => group.id === id);
}

const HIDDEN_EXPERIMENTAL_LINKS = [
  "/trend-dashboard",
  "/runtime-scoring",
  "/open-training",
  "/global-trends",
  "/scraper-hub",
  "/deichmann",
  "/aboutyou",
  "/humanic",
  "/amazon-shoes",
  "/ebay-shoes",
  "/google-shopping",
  "/image-upload-test",
];

describe("navConfig analytics-first scope (RQ659)", () => {
  const visibleLinks = NAV_GROUPS.flatMap((group) => group.items.map((item) => item.to));

  it("hides experimental, scraper and test links from primary navigation", () => {
    for (const path of HIDDEN_EXPERIMENTAL_LINKS) {
      expect(visibleLinks, `${path} must not be a navigation link`).not.toContain(path);
    }
    expect(findGroup("scrapers")).toBeUndefined();
    expect(NAV_GROUPS.flatMap((group) => [group.badge, ...group.items.map((item) => item.badge)])
      .some((badge) => badge?.label === "Test")).toBe(false);
  });

  it("keeps every group non-empty and the core retail analytics links visible", () => {
    for (const group of NAV_GROUPS) {
      expect(group.items.length, `${group.id} must not be empty`).toBeGreaterThan(0);
    }
    for (const path of [
      "/analytics",
      "/analytics/daily-sales",
      "/analytics/inventory",
      "/analytics/supplier",
      "/analytics/products",
      "/analytics/pre-nivelacija-prioriteti",
    ]) {
      expect(visibleLinks, `${path} must stay in navigation`).toContain(path);
    }
  });

  it("labels daily sales by what it shows, not by shift coverage", () => {
    const dailySales = NAV_GROUPS.flatMap((group) => group.items).find((item) => item.to === "/analytics/daily-sales");
    expect(dailySales?.label).toBe("Dnevna prodaja");
  });

  it("keeps deep-link routes for hidden links registered in the router", () => {
    for (const path of HIDDEN_EXPERIMENTAL_LINKS) {
      expect(appSource, `${path} route must stay registered`).toContain(`path="${path}"`);
    }
  });
});

describe("navConfig", () => {
  it("keeps the owner-approved eight primary analytics destinations and labels in order", () => {
    expect(findGroup("analytics-overview")?.sidebarLabel).toBe("Pregled");
    expect(findGroup("analytics-overview")?.items.map(({ to, label }) => [to, label])).toEqual([
      ["/analytics", "Pregled poslovanja"],
    ]);
    expect(findGroup("analytics-primary")?.sidebarLabel).toBe("Glavne odluke");
    expect(findGroup("analytics-primary")?.items.map(({ to, label }) => [to, label])).toEqual([
      ["/analytics/inventory", "Analitika zaliha"],
      ["/analytics/supplier", "Prodaja po dobavljačima"],
      ["/analytics/shoe-type-sales-stats", "Prodaja po tipu obuće"],
      ["/analytics/pre-nivelacija-prioriteti", "Prioriteti nivelacije"],
      ["/analytics/nivelacije-pre-post", "Pre/Posle nivelacije"],
      ["/analytics/actions", "Akcije i preporuke"],
      ["/analytics/data-quality", "Kvalitet podataka"],
    ]);
    expect((findGroup("analytics-overview")?.items.length ?? 0) + (findGroup("analytics-primary")?.items.length ?? 0)).toBe(8);
  });

  it("retains the nine approved secondary analyses in their own group", () => {
    expect(findGroup("analytics-additional")?.sidebarLabel).toBe("Dodatne analize");
    expect(findGroup("analytics-additional")?.items.map(({ to, label }) => [to, label])).toEqual([
      ["/analytics/pilot-readiness", "Pilot spremnost"],
      ["/analytics/decision-board", "Izvršni pregled odluka"],
      ["/analytics/products", "Odluke o proizvodima"],
      ["/analytics/decision-pulse", "Puls odluka"],
      ["/analytics/supplier?tab=scorecard", "Ocena dobavljača"],
      ["/analytics/daily-sales", "Dnevna prodaja"],
      ["/analytics/color-sales-stats", "Prodaja po boji artikla"],
      ["/analytics/supplier/report", "Izveštaj dobavljača"],
      ["/analytics/reports/pilot-intake", "Pilot izveštaj kvaliteta podataka"],
    ]);
  });

  it("uses distinct supplier labels and preserves the existing analysis badges", () => {
    const primarySupplier = findGroup("analytics-primary")?.items.find((item) => item.to === "/analytics/supplier");
    const scorecard = findGroup("analytics-additional")?.items.find((item) => item.to === "/analytics/supplier?tab=scorecard");
    const color = findGroup("analytics-additional")?.items.find((item) => item.to === "/analytics/color-sales-stats");
    const supplierReport = findGroup("analytics-additional")?.items.find((item) => item.to === "/analytics/supplier/report");
    const adminGroup = findGroup("admin");

    expect(primarySupplier?.label).toBe("Prodaja po dobavljačima");
    expect(scorecard?.label).toBe("Ocena dobavljača");
    expect(primarySupplier?.label).not.toBe(scorecard?.label);
    expect(color?.badge).toMatchObject({ label: "Analiza", tone: "info" });
    expect(supplierReport?.badge?.label).toBe("Izveštaj");
    expect(adminGroup?.items.find((item) => item.to === "/admin/common-products")?.badge?.label).toBe("Support");
  });

  it("keeps every navigation destination on a canonical route and avoids duplicate links", () => {
    const analyticsItems = NAV_GROUPS.filter((group) => group.id.startsWith("analytics-")).flatMap((group) => group.items);
    const canonicalPaths = new Set(
      CORE_ANALYTICS_ROUTE_DEFINITIONS.map((route) => route.path.split(/[?#]/)[0]),
    );

    expect(new Set(analyticsItems.map((item) => item.to)).size).toBe(17);
    for (const item of analyticsItems) {
      expect(canonicalPaths.has(item.to.split(/[?#]/)[0]), `${item.to} should target a canonical analytics route`).toBe(true);
    }
    expect(analyticsItems.some((item) => item.to === "/analytics/supplier-sales-stats")).toBe(false);
    expect(analyticsItems.some((item) => item.to === "/analytics/dobavljaci-tipovi-obuce")).toBe(false);
  });
});

import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

const pageSource = (name: string) => fs.readFileSync(
  path.resolve(process.cwd(), "src/pages", name),
  "utf8",
);

describe("RQ436 Operacije derived KPI cleanup", () => {
  it("removes always-unavailable Top 5 KPIs and the dead Shoe Type empty branch", () => {
    const shoe = pageSource("ShoeTypeSalesStatsPage.tsx");
    const color = pageSource("ColorSalesStatsPage.tsx");
    const prePost = pageSource("ProdajaPrePostNivelacijePage.tsx");

    expect(shoe).not.toContain("top5SharePct");
    expect(shoe).not.toContain('"filtered_out"');
    expect(color).not.toContain("top5SharePct");
    expect(prePost).not.toContain("top5SharePct");
    expect(prePost).not.toContain("periodGrowthPct");
  });

  it("keeps object metadata user-facing and cleans up Color detail scrolling", () => {
    const shoe = pageSource("ShoeTypeSalesStatsPage.tsx");
    const color = pageSource("ColorSalesStatsPage.tsx");
    const prePost = pageSource("ProdajaPrePostNivelacijePage.tsx");

    expect(shoe).toContain("Nepoznat objekat (ID");
    expect(color).toContain("Nepoznat objekat (ID");
    expect(prePost).toContain("Nepoznat objekat (ID");
    expect(color).toContain("return () => window.clearTimeout(timeoutId);");
  });
});

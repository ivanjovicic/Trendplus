import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { ANALYTICS_UNAVAILABLE_LABEL } from "../analyticsConstants";
import { formatTrendNumber } from "../../pages/GlobalTrendsPage";

const root = resolve(__dirname, "../..");

function readSrc(rel: string): string {
  return readFileSync(resolve(root, rel), "utf8");
}

describe("analytics presentation pass 2 residuals", () => {
  it("uses canonical unavailable label for trend number helper", () => {
    expect(ANALYTICS_UNAVAILABLE_LABEL).toBe("Nije dostupno");
    expect(formatTrendNumber(null, 2)).toBe(ANALYTICS_UNAVAILABLE_LABEL);
    expect(formatTrendNumber(Number.NaN, 1)).toBe(ANALYTICS_UNAVAILABLE_LABEL);
  });

  it("keeps AnalyticsDetails user-facing copy in Serbian without N/A", () => {
    const src = readSrc("pages/AnalyticsDetails.tsx");
    expect(src).toContain("Kvalitet podataka");
    expect(src).toContain("Najveći rast");
    expect(src).toContain("Preporučene akcije");
    expect(src).toContain("Greške pri učitavanju");
    expect(src).not.toMatch(/"N\/A"/);
    expect(src).not.toContain("Quick Insights");
    expect(src).not.toContain("Recommended actions");
  });

  it("keeps Configuration health fallbacks in Serbian", () => {
    const src = readSrc("pages/ConfigurationPage.tsx");
    expect(src).toContain("Nije dostupno");
    expect(src).not.toMatch(/health-status off">N\/A</);
    expect(src).toContain("Greška pri učitavanju pending batch-eva");
  });

  it("Serbianizes Inventory size-curve unavailable presentation", () => {
    const src = readSrc("components/inventory/SizeCurveVisualization.tsx");
    expect(src).toContain("ANALYTICS_UNAVAILABLE_LABEL");
    expect(src).not.toMatch(/"N\/A"/);
  });

  it("keeps Insight Studio trust helper on canonical unavailable label", () => {
    const src = readSrc("pages/insightStudioTrustPresentation.ts");
    expect(src).toContain("ANALYTICS_UNAVAILABLE_LABEL");
    expect(src).not.toMatch(/"N\/D"/);
    expect(src).not.toMatch(/"N\/A"/);
  });

  it("keeps IntelligenceSnapshotPanel unavailable copy Serbian", () => {
    const src = readSrc("components/dashboard/IntelligenceSnapshotPanel.tsx");
    expect(src).toContain("ANALYTICS_UNAVAILABLE_LABEL");
    expect(src).toContain("Pregled signala");
    expect(src).not.toMatch(/"n\/a"/);
    expect(src).not.toContain("Signals Snapshot");
    expect(src).not.toContain("No demand signal");
  });

  it("keeps AnalyticsActions outcome hint on canonical unavailable wording", () => {
    const src = readSrc("pages/AnalyticsActionsPage.tsx");
    expect(src).toContain("Nije dostupno");
    expect(src).toContain("ANALYTICS_UNAVAILABLE_LABEL");
    expect(src).not.toMatch(/„N\/A“/);
    expect(src).not.toMatch(/"N\/A"/);
  });

  it("keeps Insight Studio average-margin headers Serbian with diacritics", () => {
    const src = readSrc("pages/InsightStudioPage.tsx");
    expect(src).toContain("Prosečna marža %");
    expect(src).toContain("Prosečna marža");
    expect(src).not.toMatch(/Avg marza/);
    expect(src).not.toMatch(/>Avg marža</);
  });

  it("keeps Color PoP tip on canonical unavailable wording", () => {
    const src = readSrc("pages/ColorSalesStatsPage.tsx");
    expect(src).toContain("Nije dostupno ako prethodni period nije dostupan");
    expect(src).not.toMatch(/N\/A ako prethodni period/);
    expect(src).toContain("Brojilac neto udela");
    expect(src).toContain("Imenilac neto udela");
    expect(src).not.toMatch(/Neto udeo numerator/);
    expect(src).not.toMatch(/Neto udeo denominator/);
    expect(src).not.toMatch(/denominator nije pozitivan/);
  });

  it("keeps Amazon/eBay/Google Shopping trend unavailable and empty copy Serbian", () => {
    for (const rel of [
      "pages/EbayShoesTrendsPage.tsx",
      "pages/GoogleShoppingTrendsPage.tsx",
      "pages/AmazonShoesTrendsPage.tsx",
    ] as const) {
      const src = readSrc(rel);
      expect(src).not.toMatch(/Price N\/A/);
      expect(src).not.toContain("Loading…");
      expect(src).not.toContain("Run Sync");
      expect(src).not.toContain("No results for");
      expect(src).not.toMatch(/Cijena/);
      expect(src).toContain("Učitavanje…");
      expect(src).toContain("Pokreni sinhronizaciju");
      expect(src).toContain("Nema rezultata za");
    }
    const ebay = readSrc("pages/EbayShoesTrendsPage.tsx");
    const google = readSrc("pages/GoogleShoppingTrendsPage.tsx");
    expect(ebay).toContain("Nije dostupno");
    expect(google).toContain("Nije dostupno");
  });

});

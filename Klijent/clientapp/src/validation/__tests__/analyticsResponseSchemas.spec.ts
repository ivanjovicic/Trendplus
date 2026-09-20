import { describe, expect, it } from "vitest";
import {
  colorSalesStatsResponseSchema,
  dailySalesTableResponseSchema,
} from "../analyticsResponseSchemas";
import { AnalyticsResponseValidationError, validateAnalyticsResponse } from "../analyticsResponseValidation";

const colorRow = {
  boja: "Crna",
  preNivelacijePromet: 0,
  preNivelacijeKolicina: 0,
  posleNivelacijePromet: 10,
  posleNivelacijeKolicina: 1,
  ukupanPromet: 10,
  ukupnaKolicina: 1,
  previousPeriodRevenue: null,
  previousPeriodUnits: null,
  brojArtikalaSaNivelacijom: 0,
  brojArtikalaUkupno: 1,
  revenueWithCost: 10,
  estimatedCostRevenue: 0,
  marginContribution: 10,
  marginDataCoveragePct: null,
  fallbackCostCoveragePct: null,
  marginPct: 100,
  revenueWithNivelacijaSplit: 10,
  popRevenueChangePct: null,
  popUnitsChangePct: null,
  prePostNivelacijaRevenueImpactPct: null,
  prePostNivelacijaUnitsImpactPct: null,
  prePostNivelacijaRevenueCoveragePct: null,
  sharePct: 100,
  reliabilityPct: null,
};

const validColorResponse = {
  generatedAt: "2026-07-01T08:00:00Z",
  fromDate: "2026-06-01T00:00:00Z",
  toDate: "2026-07-01T00:00:00Z",
  dataWindowFrom: "2026-06-01T00:00:00Z",
  dataWindowTo: "2026-07-01T00:00:00Z",
  sezonaId: null,
  storeId: null,
  colors: [colorRow],
  totals: {
    ukupanPromet: 10,
    ukupanMarzniDoprinos: 10,
    prePromet: 0,
    poslePromet: 10,
    ukupnaKolicina: 1,
    preKolicina: 0,
    posleKolicina: 1,
    previousPeriodRevenue: null,
    previousPeriodUnits: null,
    popRevenueChangePct: null,
    popUnitsChangePct: null,
    prePostNivelacijaRevenueImpactPct: null,
    prePostNivelacijaUnitsImpactPct: null,
  },
  dataQuality: {
    missingCostRevenue: 0,
    missingCostRevenueSharePct: null,
    unknownColorRevenue: 0,
    unknownColorRevenueSharePct: null,
    revenueWithNivelacijaSplit: 10,
    revenueWithNivelacijaSplitSharePct: 100,
  },
  sezone: [],
};

describe("analytics response schemas", () => {
  it("accepts valid zero, positive, and nullable unknown values", () => {
    expect(colorSalesStatsResponseSchema.safeParse(validColorResponse).success).toBe(true);
    expect(dailySalesTableResponseSchema.safeParse({
      requestedFrom: "2026-06-01",
      requestedTo: "2026-07-01",
      storeId: null,
      topN: 0,
      dataScope: "all",
      topSuppliers: [],
      topSuppliersOrder: [],
      dateRows: [],
      metadata: {
        totalDays: 0,
        uniqueSuppliersInRange: 0,
        unknownSupplierPct: 0,
        unknownSupplierItems: 0,
        offShiftItems: 0,
        offShiftRevenue: 0,
        totalItemsInRange: 0,
        duplicateReceiptGroupCount: 0,
        duplicateReceiptHeaderCount: 0,
        receiptAmountMismatchCount: 0,
        receiptAmountMismatchRevenue: 0,
        nonStandardReceiptCount: 0,
        nonStandardReceiptRevenue: 0,
        debtReceiptCount: 0,
        debtReceiptRevenue: 0,
        minAvailableDate: null,
        maxAvailableDate: null,
      },
    }).success).toBe(true);
  });

  it.each([
    ["negative count", { ...colorRow, ukupnaKolicina: -1 }],
    ["percentage above 100", { ...colorRow, sharePct: 101 }],
    ["NaN", { ...colorRow, marginContribution: Number.NaN }],
    ["Infinity", { ...colorRow, marginContribution: Number.POSITIVE_INFINITY }],
  ])("rejects %s instead of repairing it", (_caseName, invalidRow) => {
    const result = colorSalesStatsResponseSchema.safeParse({
      ...validColorResponse,
      colors: [invalidRow],
    });
    expect(result.success).toBe(false);
  });

  it("rejects malformed dates and missing required response sections", () => {
    expect(colorSalesStatsResponseSchema.safeParse({
      ...validColorResponse,
      generatedAt: "not-a-date",
    }).success).toBe(false);
    expect(colorSalesStatsResponseSchema.safeParse({
      ...validColorResponse,
      totals: undefined,
    }).success).toBe(false);
  });

  it("raises a controlled validation error without producing fake zero values", () => {
    expect(() => validateAnalyticsResponse(
      { ...validColorResponse, colors: [{ ...colorRow, ukupnaKolicina: -1 }] },
      colorSalesStatsResponseSchema,
      "Color sales",
    )).toThrow(AnalyticsResponseValidationError);
  });
});

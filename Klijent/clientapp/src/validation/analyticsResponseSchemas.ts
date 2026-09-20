import { z } from "zod";

const finiteNumber = z.number().finite();
const nonNegativeNumber = finiteNumber.min(0);
const nonNegativeInteger = finiteNumber.int().min(0);
const percentage = finiteNumber.min(-100).max(100);
const nonNegativePercentage = finiteNumber.min(0).max(100);
const validDate = z.string().min(1).refine((value) => Number.isFinite(Date.parse(value)), "Must be a valid date.");
const nullableNumber = finiteNumber.nullable();
const nullableNonNegativeNumber = nonNegativeNumber.nullable();
const nullableNonNegativeInteger = nonNegativeInteger.nullable();
const nullablePercentage = percentage.nullable();
const nullableNonNegativePercentage = nonNegativePercentage.nullable();

const provenanceSchema = z.object({
  kind: z.enum([
    "authoritative_backend_aggregate",
    "observed_row_value",
    "frontend_display_derivation",
    "modeled_estimated",
    "unknown",
  ]),
  authority: z.enum(["authoritative", "observed", "derived", "modeled", "unknown"]),
  actionability: z.enum(["actionable", "informational", "blocked", "unknown"]),
  unit: z.string().nullable().optional(),
  denominator: z.string().nullable().optional(),
}).passthrough();

export const analyticsResponseMetaSchema = z.object({
  success: z.boolean(),
  generatedAtUtc: validDate.optional(),
  lastRefreshAtUtc: validDate.nullable().optional(),
  requestedPeriodFromUtc: validDate.nullable().optional(),
  requestedPeriodToUtc: validDate.nullable().optional(),
  effectivePeriodFromUtc: validDate.nullable().optional(),
  effectivePeriodToUtc: validDate.nullable().optional(),
  observedPeriodFromUtc: validDate.nullable().optional(),
  observedPeriodToUtc: validDate.nullable().optional(),
  dataQualityStatus: z.string().nullable().optional(),
  recommendationAllowed: z.boolean().nullable().optional(),
  isPartial: z.boolean().optional(),
  metricProvenance: z.record(z.string(), provenanceSchema).nullable().optional(),
}).passthrough();

const optionalMeta = analyticsResponseMetaSchema.nullable().optional();

const recommendationSchema = z.object({
  confidencePct: nullableNonNegativePercentage,
  reliabilityPct: nullableNonNegativePercentage,
  dataQualityStatus: z.string(),
  reasonCodes: z.array(z.string()),
}).passthrough();

const salesStatSchema = {
  preNivelacijePromet: nonNegativeNumber,
  preNivelacijeKolicina: nonNegativeNumber,
  posleNivelacijePromet: nonNegativeNumber,
  posleNivelacijeKolicina: nonNegativeNumber,
  ukupanPromet: nonNegativeNumber,
  ukupnaKolicina: nonNegativeNumber,
  previousPeriodRevenue: nullableNumber,
  previousPeriodUnits: nullableNumber,
  brojArtikalaSaNivelacijom: nonNegativeInteger,
  brojArtikalaUkupno: nonNegativeInteger,
  revenueWithCost: nonNegativeNumber,
  estimatedCostRevenue: nonNegativeNumber,
  marginContribution: finiteNumber,
  marginDataCoveragePct: nullableNonNegativePercentage,
  fallbackCostCoveragePct: nullableNonNegativePercentage,
  marginPct: percentage,
  revenueWithNivelacijaSplit: nonNegativeNumber,
  popRevenueChangePct: nullableNumber,
  popUnitsChangePct: nullableNumber,
  prePostNivelacijaRevenueImpactPct: nullableNumber,
  prePostNivelacijaUnitsImpactPct: nullableNumber,
  prePostNivelacijaRevenueCoveragePct: nullableNonNegativePercentage,
  sharePct: nullableNonNegativePercentage.optional(),
  reliabilityPct: nullableNonNegativePercentage.optional(),
  recommendation: recommendationSchema.optional(),
};

const costTotalsSchema = {
  ukupanPromet: nonNegativeNumber,
  ukupanMarzniDoprinos: finiteNumber,
  prePromet: nonNegativeNumber,
  poslePromet: nonNegativeNumber,
  ukupnaKolicina: nonNegativeNumber,
  preKolicina: nonNegativeNumber,
  posleKolicina: nonNegativeNumber,
  previousPeriodRevenue: nullableNumber,
  previousPeriodUnits: nullableNumber,
  popRevenueChangePct: nullableNumber,
  popUnitsChangePct: nullableNumber,
  prePostNivelacijaRevenueImpactPct: nullableNumber,
  prePostNivelacijaUnitsImpactPct: nullableNumber,
};

const colorDataQualitySchema = z.object({
  missingCostRevenue: nonNegativeNumber,
  missingCostRevenueSharePct: nullableNonNegativePercentage,
  unknownColorRevenue: nonNegativeNumber,
  unknownColorRevenueSharePct: nullableNonNegativePercentage,
  revenueWithNivelacijaSplit: nonNegativeNumber,
  revenueWithNivelacijaSplitSharePct: nullableNonNegativePercentage,
}).passthrough();

const shoeDataQualitySchema = colorDataQualitySchema
  .omit({ unknownColorRevenue: true, unknownColorRevenueSharePct: true })
  .extend({
    unknownTypeRevenue: nonNegativeNumber,
    unknownTypeRevenueSharePct: nullableNonNegativePercentage,
  });

const seasonSchema = z.object({
  id: nonNegativeInteger,
  naziv: z.string(),
  datumOd: validDate,
  datumDo: validDate,
}).passthrough();

export const colorSalesStatsResponseSchema = z.object({
  generatedAt: validDate,
  meta: optionalMeta,
  fromDate: validDate.nullable(),
  toDate: validDate.nullable(),
  dataWindowFrom: validDate.nullable(),
  dataWindowTo: validDate.nullable(),
  sezonaId: nonNegativeInteger.nullable(),
  storeId: nonNegativeInteger.nullable(),
  dataScope: z.string().nullable().optional(),
  colors: z.array(z.object({
    boja: z.string(),
    ...salesStatSchema,
  }).passthrough()),
  totals: z.object(costTotalsSchema).passthrough(),
  dataQuality: colorDataQualitySchema,
  sezone: z.array(seasonSchema),
}).passthrough();

export const shoeTypeSalesStatsResponseSchema = z.object({
  generatedAt: validDate,
  meta: optionalMeta,
  fromDate: validDate.nullable(),
  toDate: validDate.nullable(),
  dataWindowFrom: validDate.nullable(),
  dataWindowTo: validDate.nullable(),
  sezonaId: nonNegativeInteger.nullable(),
  storeId: nonNegativeInteger.nullable(),
  dataScope: z.string().nullable().optional(),
  shoeTypes: z.array(z.object({
    tipObuceId: nonNegativeInteger.nullable(),
    tipObuceNaziv: z.string(),
    ...salesStatSchema,
  }).passthrough()),
  totals: z.object({
    ...costTotalsSchema,
    brojTipovaObuce: nonNegativeInteger,
  }).passthrough(),
  dataQuality: shoeDataQualitySchema,
  sezone: z.array(seasonSchema),
}).passthrough();

const dailySalesRowSchema = z.object({
  date: validDate,
  firstShiftTotalItems: nullableNumber,
  secondShiftTotalItems: nullableNumber,
  totalRevenue: nullableNumber,
  topSupplierCounts: z.array(nullableNumber),
  othersCount: nullableNumber,
  totalItemsSold: nullableNumber,
}).passthrough();

const dailySalesMetadataSchema = z.object({
  totalDays: nullableNonNegativeInteger,
  uniqueSuppliersInRange: nullableNonNegativeInteger,
  unknownSupplierPct: nullableNonNegativePercentage,
  unknownSupplierItems: nullableNonNegativeNumber,
  offShiftItems: nullableNonNegativeNumber,
  offShiftRevenue: nullableNonNegativeNumber,
  totalItemsInRange: nullableNonNegativeNumber,
  duplicateReceiptGroupCount: nullableNonNegativeInteger,
  duplicateReceiptHeaderCount: nullableNonNegativeInteger,
  receiptAmountMismatchCount: nullableNonNegativeInteger,
  receiptAmountMismatchRevenue: nullableNonNegativeNumber,
  nonStandardReceiptCount: nullableNonNegativeInteger,
  nonStandardReceiptRevenue: nullableNonNegativeNumber,
  debtReceiptCount: nullableNonNegativeInteger,
  debtReceiptRevenue: nullableNonNegativeNumber,
  minAvailableDate: validDate.nullable(),
  maxAvailableDate: validDate.nullable(),
  warnings: z.array(z.string()).optional(),
}).passthrough();

export const dailySalesTableResponseSchema = z.object({
  requestedFrom: validDate,
  requestedTo: validDate,
  storeId: nonNegativeInteger.nullable(),
  topN: nonNegativeInteger,
  dataScope: z.string(),
  topSuppliers: z.array(z.object({
    supplierId: nonNegativeInteger.nullable(),
    supplierName: z.string(),
    isUnknown: z.boolean(),
    totalQty: nullableNumber,
    totalRevenue: nullableNumber,
  }).passthrough()),
  topSuppliersOrder: z.array(z.string()),
  dateRows: z.array(dailySalesRowSchema),
  metadata: dailySalesMetadataSchema,
  meta: optionalMeta,
}).passthrough();

const preNivelacijaScenarioSchema = z.object({
  expectedUnits30d: nonNegativeNumber,
  expectedRevenue30d: nonNegativeNumber,
  expectedMargin30d: finiteNumber,
  effectivePrice: nonNegativeNumber,
}).passthrough();

const preNivelacijaCandidateSchema = z.object({
  artikalId: nonNegativeInteger,
  sku: z.string(),
  stockUnits: nonNegativeNumber,
  units180: nonNegativeNumber,
  velocity180: nonNegativeNumber,
  daysSinceLastSale: nonNegativeNumber,
  markdownEvents: nonNegativeInteger,
  avgMarkdownPct: nonNegativePercentage,
  grossMarginPctEst: percentage,
  seasonRecencyBoost: finiteNumber,
  preNivelacijaScore: finiteNumber,
  scoreBreakdown: z.object({
    stockPressure: finiteNumber,
    velocityRisk: finiteNumber,
    recencyRisk: finiteNumber,
    markdownOpportunity: finiteNumber,
    marginPotential: finiteNumber,
    seasonRecencyBoost: finiteNumber,
  }).passthrough(),
  scenarioHighlightNow: preNivelacijaScenarioSchema,
  scenarioMarkdownNow: preNivelacijaScenarioSchema,
  marginDeltaHighlightVsMarkdown: finiteNumber,
  revenueDeltaHighlightVsMarkdown: finiteNumber,
  reliabilityPct: nullableNonNegativePercentage,
  decisionScore: finiteNumber,
  recommendation: z.object({
    confidencePct: nonNegativePercentage,
    reliabilityPct: nullableNonNegativePercentage,
    dataQualityStatus: z.string(),
    reasonCodes: z.array(z.string()),
  }).passthrough(),
}).passthrough();

export const preNivelacijaPriorityResponseSchema = z.object({
  generatedAtUtc: validDate,
  formulaVersion: z.string(),
  formulaDescription: z.string(),
  summary: z.object({
    supplierCount: nonNegativeInteger,
    candidatesCount: nonNegativeInteger,
    highPriorityCount: nonNegativeInteger,
    totalStockAtRisk: nonNegativeNumber,
    estimatedAvoidableMarkdownLoss: finiteNumber,
    expectedHighlightRevenueUplift: finiteNumber,
    averagePreNivelacijaScore: finiteNumber,
  }).passthrough(),
  supplierLeaderboard: z.array(z.object({
    highPrioritySkuCount: nonNegativeInteger,
    candidateSkuCount: nonNegativeInteger,
    stockUnitsAtRisk: nonNegativeNumber,
    estimatedAvoidableMarkdownLoss: finiteNumber,
    expectedHighlightRevenueUplift: finiteNumber,
    actionScore: finiteNumber,
    weekOverWeekRiskDeltaPct: nullableNumber,
  }).passthrough()),
  candidates: z.array(preNivelacijaCandidateSchema),
  queues: z.object({
    highlightNow: z.array(z.unknown()),
    monitor: z.array(z.unknown()),
    likelyMarkdownSoon: z.array(z.unknown()),
  }).passthrough(),
  alerts: z.array(z.unknown()),
  page: nonNegativeInteger,
  pageSize: nonNegativeInteger,
  totalCandidates: nonNegativeInteger,
  recommendationAllowed: z.boolean().nullable().optional(),
  meta: optionalMeta,
}).passthrough();

const inventoryMetaFields = {
  meta: optionalMeta,
};

const inventoryListItemSchema = z.object({
  id: nonNegativeInteger,
  naziv: z.string(),
  kolicina: nullableNumber.optional(),
  minimalnaKolicina: nullableNumber.optional(),
  estimatedValue: nullableNumber.optional(),
  stockCoverDays: nullableNumber.optional(),
  sellThroughRatio: nullableNonNegativePercentage.optional(),
  signalConfidencePct: nullableNonNegativePercentage.optional(),
}).passthrough();

export const inventoryBalanceResponseSchema = z.object({
  totalSku: nonNegativeInteger,
  totalOnHand: nonNegativeNumber,
  lowStockCount: nonNegativeInteger,
  outOfStockCount: nonNegativeInteger,
  estimatedInventoryValue: nullableNumber.optional(),
  ...inventoryMetaFields,
}).passthrough();

export const inventoryPagedResponseSchema = z.object({
  items: z.array(inventoryListItemSchema),
  totalCount: nonNegativeInteger,
  pageNumber: nonNegativeInteger,
  pageSize: nonNegativeInteger,
  ...inventoryMetaFields,
}).passthrough();

const inventoryInsightItemSchema = z.object({
  id: nonNegativeInteger,
  quantity: nonNegativeNumber,
  minimum: nonNegativeNumber,
  reorderGap: finiteNumber,
  estimatedValue: nullableNumber,
  daysSinceMovement: nonNegativeNumber,
  sellThroughRatio: nullableNonNegativePercentage.optional(),
  signalConfidencePct: nonNegativePercentage,
}).passthrough();

export const inventoryInsightsResponseSchema = z.object({
  totalItems: nonNegativeInteger,
  totalEstimatedValue: nonNegativeNumber,
  aging: z.array(z.object({
    itemCount: nonNegativeInteger,
    totalUnits: nonNegativeNumber,
    estimatedValue: nonNegativeNumber,
  }).passthrough()),
  abc: z.array(z.object({
    itemCount: nonNegativeInteger,
    estimatedValue: nonNegativeNumber,
    valueSharePct: nonNegativePercentage,
  }).passthrough()),
  topAgedItems: z.array(inventoryInsightItemSchema),
  topCapitalLockedItems: z.array(inventoryInsightItemSchema),
  ...inventoryMetaFields,
}).passthrough();

export const inventoryDetailResponseSchema = z.object({
  id: nonNegativeInteger,
  estimatedValue: nonNegativeNumber,
  updatedAt: validDate,
  movementCount: nonNegativeInteger,
  daysSinceMovement: nonNegativeNumber,
  stockCoverDays: nullableNumber.optional(),
  sellThroughRatio: nullableNonNegativePercentage.optional(),
  signalConfidencePct: nonNegativePercentage,
  recommendationAllowed: z.boolean(),
  history: z.array(z.unknown()),
}).passthrough();

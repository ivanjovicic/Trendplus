import type { AnalyticsResponseMeta } from "../types/analytics";
import { fetchAnalyticsJson } from "./analyticsHttp";
import { colorSalesStatsResponseSchema } from "../validation/analyticsResponseSchemas";

export interface AnalyticsRecommendation {
  status: "increase_focus" | "maintain" | "review" | "do_not_trust" | "insufficient_data";
  label: string;
  summary: string;
  confidencePct: number | null;
  reliabilityPct: number | null;
  dataQualityStatus: "good" | "warning" | "critical" | "insufficient_data";
  recommendationAllowed: boolean;
  reasonCodes: string[];
}

export interface ColorSalesStat {
  boja: string;
  preNivelacijePromet: number;
  preNivelacijeKolicina: number;
  posleNivelacijePromet: number;
  posleNivelacijeKolicina: number;
  ukupanPromet: number;
  ukupnaKolicina: number;
  previousPeriodRevenue: number | null;
  previousPeriodUnits: number | null;
  brojArtikalaSaNivelacijom: number;
  brojArtikalaUkupno: number;
  revenueWithCost: number;
  estimatedCostRevenue: number;
  marginContribution: number;
  marginDataCoveragePct: number | null;
  fallbackCostCoveragePct: number | null;
  marginPct: number | null;
  // TODO(backend-dto): keep ColorSalesStat aligned with the color-sales-stats endpoint quality payload.
  // Margin quality / cost coverage context must come from backend DTOs, not from frontend derivation.
  totalCost?: number;
  historicalCostRevenue?: number;
  historicalCostCoveragePct?: number | null;
  estimatedCostCoveragePct?: number | null;
  noCostRevenue?: number;
  noCostCoveragePct?: number | null;
  snapshotCostRevenue?: number;
  snapshotCostCoveragePct?: number;
  isEstimatedMargin?: boolean;
  marginQualityLabel?: string | null;
  marginQualityTier?: string | null;
  marginQualityShortLabel?: string | null;
  marginQualityTooltip?: string | null;
  revenueWithNivelacijaSplit: number;
  comparablePreRevenue: number;
  comparablePostRevenue: number;
  comparablePreQuantity: number;
  comparablePostQuantity: number;
  popRevenueChangePct: number | null;
  popUnitsChangePct: number | null;
  prePostNivelacijaRevenueImpactPct: number | null;
  prePostNivelacijaUnitsImpactPct: number | null;
  prePostNivelacijaRevenueCoveragePct: number | null;
  prePostSignalNote: string | null;
  prePostComparableArticleCount: number;
  sharePct?: number | null;
  decisionScore?: number | null;
  reliabilityPct?: number | null;
  isUnknown?: boolean;
  recommendation: AnalyticsRecommendation;
  // Legacy compatibility aliases (deprecated)
  promenaPrometa?: number | null;
  promenaKolicine?: number | null;
}

export interface ColorSalesTotals {
  ukupanPromet: number;
  ukupanMarzniDoprinos: number;
  ukupanTrosak?: number;
  weightedKnownMarginPct: number | null;
  weightedKnownMarginRevenue: number;
  prosecnaMarza?: number | null;
  historicalCostCoveragePct?: number | null;
  estimatedCostCoveragePct?: number | null;
  noCostCoveragePct?: number | null;
  snapshotCostRevenue?: number;
  snapshotCostCoveragePct?: number;
  isSnapshotActive?: boolean;
  snapshotGeneratedAtUtc?: string | null;
  isEstimatedMargin?: boolean;
  marginQualityLabel?: string | null;
  marginQualityTier?: string | null;
  marginQualityShortLabel?: string | null;
  marginQualityTooltip?: string | null;
  prePromet: number;
  poslePromet: number;
  ukupnaKolicina: number;
  preKolicina: number;
  posleKolicina: number;
  comparablePreRevenue: number;
  comparablePostRevenue: number;
  comparablePreQuantity: number;
  comparablePostQuantity: number;
  comparableArticleCount: number;
  comparableRevenueCoveragePct: number | null;
  prePostSignalNote: string | null;
  observedPreRevenue: number;
  observedPostRevenue: number;
  observedPreQuantity: number;
  observedPostQuantity: number;
  previousPeriodRevenue: number | null;
  previousPeriodUnits: number | null;
  brojBoja: number;
  popRevenueChangePct: number | null;
  popUnitsChangePct: number | null;
  prePostNivelacijaRevenueImpactPct: number | null;
  prePostNivelacijaUnitsImpactPct: number | null;
  recommendationSummary: {
    increaseFocus: number;
    maintain: number;
    review: number;
    doNotTrust: number;
    insufficientData: number;
  };
  // Legacy compatibility alias (deprecated)
  promenaPrometaPct?: number | null;
}

export interface ColorSalesDataQuality {
  missingCostRevenue: number;
  missingCostRevenueSharePct: number | null;
  estimatedCostRevenue?: number;
  estimatedCostRevenueSharePct?: number | null;
  unknownColorRevenue: number;
  unknownColorRevenueSharePct: number | null;
  revenueWithNivelacijaSplit: number;
  revenueWithNivelacijaSplitSharePct: number | null;
  observedRevenueWithNivelacijaSplit: number;
  observedRevenueWithNivelacijaSplitSharePct: number | null;
  nivelacijaEventCount?: number;
  nivelacijaEventArticleCount?: number;
  salesArticleCount?: number;
  salesArticlesWithMatchingNivelacija?: number;
  signedRevenuePolicy?: string;
  signedQuantityPolicy?: string;
  costQualityDenominatorStatus?: string;
  weightedKnownMarginPct: number | null;
  weightedKnownMarginRevenue: number;
}

export interface ColorSalesLineage {
  storeId: number | null;
  dataScope: string;
  sourceFamily?: string;
  sourceLabel?: string;
  sourceTables?: string;
  observedPopulation?: string;
  costPolicy?: string;
  prePostPolicy?: string;
  unknownPolicy?: string;
  eventCount: number;
  eventArticleCount: number;
  salesArticleCount: number;
  salesArticlesWithMatchingNivelacija: number;
  storePolicy: string;
  originPolicy: string;
}

export interface SezonaOption {
  id: number;
  naziv: string;
  datumOd: string;
  datumDo: string;
}

export interface ColorSalesStatsResponse {
  generatedAt: string;
  meta: AnalyticsResponseMeta;
  fromDate: string | null;
  toDate: string | null;
  dataWindowFrom: string | null;
  dataWindowTo: string | null;
  sezonaId: number | null;
  storeId: number | null;
  dataScope: "all" | "existing" | "imported";
  lineage: ColorSalesLineage;
  colors: ColorSalesStat[];
  totals: ColorSalesTotals;
  dataQuality: ColorSalesDataQuality;
  sezone: SezonaOption[];
}

export interface ColorSalesStatsQuery {
  sezonaId?: number | null;
  fromDate?: string | null;
  toDate?: string | null;
  storeId?: number | null;
  dataScope?: string | null;
  signal?: AbortSignal;
}

export async function getColorSalesStats(
  query: ColorSalesStatsQuery = {}
): Promise<ColorSalesStatsResponse> {
  const params = new URLSearchParams();
  if (query.sezonaId != null) params.set("sezonaId", String(query.sezonaId));
  if (query.fromDate) params.set("fromDate", query.fromDate);
  if (query.toDate) params.set("toDate", query.toDate);
  if (query.storeId != null) params.set("storeId", String(query.storeId));
  if (query.dataScope) params.set("dataScope", query.dataScope);

  return fetchAnalyticsJson<ColorSalesStatsResponse>(
    "/api/analytics/color-sales-stats",
    params,
    "Greska pri ucitavanju statistike boja artikala",
    { signal: query.signal, schema: colorSalesStatsResponseSchema }
  );
}

import type { AnalyticsResponseMeta } from "../types/analytics";
import { fetchAnalyticsJson } from "./analyticsHttp";

export interface AnalyticsRecommendation {
  status: "increase_focus" | "maintain" | "review" | "do_not_trust" | "insufficient_data";
  label: "Increase focus" | "Maintain" | "Review" | "Do not trust" | "Insufficient data";
  summary: string;
  confidencePct: number;
  reliabilityPct: number;
  dataQualityStatus: "good" | "warning" | "critical";
  reasonCodes: string[];
}

export interface SupplierFootwearBreakdown {
  tipObuceId: number | null;
  tipObuceNaziv: string;
  ukupanPromet: number;
  ukupnaKolicina: number;
  brojArtikala: number;
  totalCost: number;
  marginContribution: number;
  marginPct: number;
  shareOfSupplierRevenuePct: number;
  shareOfSupplierMarginContributionPct: number;
  previousPeriodRevenue: number | null;
  previousPeriodUnits: number | null;
  popRevenueChangePct: number | null;
  popUnitsChangePct: number | null;
  historicalCostRevenue?: number;
  historicalCostCoveragePct?: number | null;
  estimatedCostRevenue?: number;
  estimatedCostCoveragePct?: number | null;
  snapshotCostRevenue?: number;
  snapshotCostCoveragePct?: number | null;
  noCostRevenue?: number;
  noCostCoveragePct?: number | null;
  marginQualityLabel?: string | null;
  marginQualityTier?: string | null;
  marginQualityShortLabel?: string | null;
  marginQualityTooltip?: string | null;
}

export interface SupplierSalesStat {
  dobavljacId: number | null;
  dobavljacNaziv: string;
  isUnknown: boolean;
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
  marginPct: number;
  // Cost quality breakdown
  totalCost?: number;
  historicalCostRevenue?: number;
  historicalCostCoveragePct?: number;
  estimatedCostCoveragePct?: number;
  noCostRevenue?: number;
  noCostCoveragePct?: number;
  snapshotCostRevenue?: number;
  snapshotCostCoveragePct?: number;
  isEstimatedMargin?: boolean;
  marginQualityLabel?: string | null;
  marginQualityTier?: string | null;
  marginQualityShortLabel?: string | null;
  marginQualityTooltip?: string | null;
  popRevenueChangePct: number | null;
  popUnitsChangePct: number | null;
  prePostNivelacijaRevenueImpactPct: number | null;
  prePostNivelacijaUnitsImpactPct: number | null;
  prePostNivelacijaRevenueCoveragePct: number | null;
  prePostSignalNote?: string | null;
  prePostComparableArticleCount?: number;
  primaryFootwearType?: string | null;
  primaryFootwearTypeSharePct?: number;
  footwearTypeCount?: number;
  footwearBreakdown?: SupplierFootwearBreakdown[];
  sharePct?: number;
  shareOfMarginContribution?: number;
  /** @deprecated Use shareOfMarginContribution. */
  shareOfProfit?: number;
  shareOfUnits?: number;
  reliabilityPct?: number;
  recommendation?: AnalyticsRecommendation;
  // Legacy compatibility aliases (deprecated)
  promenaPrometa?: number | null;
  promenaKolicine?: number | null;
}

export interface SupplierSalesTotals {
  ukupanPromet: number;
  ukupanMarzniDoprinos: number;
  ukupanTrosak?: number;
  prosecnaMarza: number | null;
  historicalCostCoveragePct?: number;
  estimatedCostCoveragePct?: number;
  noCostCoveragePct?: number;
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
  previousPeriodRevenue: number | null;
  previousPeriodUnits: number | null;
  brojDobavljaca: number;
  brojDobavljacTipObuceKombinacija?: number;
  popRevenueChangePct: number | null;
  popUnitsChangePct: number | null;
  prePostNivelacijaRevenueImpactPct: number | null;
  prePostNivelacijaUnitsImpactPct: number | null;
  recommendationSummary?: {
    increaseFocus: number;
    maintain: number;
    review: number;
    doNotTrust: number;
    insufficientData: number;
  };
  // Legacy compatibility alias (deprecated)
  promenaPrometaPct?: number | null;
}

export interface SupplierSalesDataQuality {
  missingCostQty: number;
  missingCostRevenue: number;
  missingCostRevenueSharePct: number | null;
  estimatedCostRevenue?: number;
  estimatedCostRevenueSharePct?: number | null;
  unknownSupplierRevenue: number;
  unknownSupplierRevenueSharePct: number | null;
  revenueWithNivelacijaSplit: number;
  revenueWithNivelacijaSplitSharePct: number | null;
}

export interface SezonaOption {
  id: number;
  naziv: string;
  datumOd: string;
  datumDo: string;
}

export interface SupplierSalesStatsResponse {
  generatedAt: string;
  meta?: AnalyticsResponseMeta;
  fromDate: string | null;
  toDate: string | null;
  dataWindowFrom: string | null;
  dataWindowTo: string | null;
  sezonaId: number | null;
  storeId: number | null;
  dataScope?: string | null;
  provenanceBasis?: string | null;
  suppliers: SupplierSalesStat[];
  totals: SupplierSalesTotals;
  dataQuality: SupplierSalesDataQuality;
  sezone: SezonaOption[];
}

export interface SupplierSalesStatsQuery {
  sezonaId?: number | null;
  fromDate?: string | null;
  toDate?: string | null;
  storeId?: number | null;
  dataScope?: string | null;
  signal?: AbortSignal;
}

export async function getSupplierSalesStats(
  query: SupplierSalesStatsQuery = {}
): Promise<SupplierSalesStatsResponse> {
  const params = new URLSearchParams();
  if (query.sezonaId != null) params.set("sezonaId", String(query.sezonaId));
  if (query.fromDate) params.set("fromDate", query.fromDate);
  if (query.toDate) params.set("toDate", query.toDate);
  if (query.storeId != null) params.set("storeId", String(query.storeId));
  if (query.dataScope) params.set("dataScope", query.dataScope);

  return fetchAnalyticsJson<SupplierSalesStatsResponse>(
    "/api/analytics/supplier-sales-stats",
    params,
    "Greska pri ucitavanju statistike dobavljaca",
    { signal: query.signal }
  );
}

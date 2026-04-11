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

export interface ShoeTypeSalesStat {
  tipObuceId: number | null;
  tipObuceNaziv: string;
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
  revenueWithNivelacijaSplit: number;
  popRevenueChangePct: number | null;
  popUnitsChangePct: number | null;
  prePostNivelacijaRevenueImpactPct: number | null;
  prePostNivelacijaUnitsImpactPct: number | null;
  prePostNivelacijaRevenueCoveragePct: number | null;
  prePostSignalNote?: string | null;
  prePostComparableArticleCount?: number;
  sharePct?: number;
  reliabilityPct?: number;
  isUnknown?: boolean;
  recommendation?: AnalyticsRecommendation;
  // Legacy compatibility aliases (deprecated)
  promenaPrometa?: number | null;
  promenaKolicine?: number | null;
}

export interface ShoeTypeSalesTotals {
  ukupanPromet: number;
  ukupanMarzniDoprinos: number;
  prePromet: number;
  poslePromet: number;
  ukupnaKolicina: number;
  preKolicina: number;
  posleKolicina: number;
  previousPeriodRevenue: number | null;
  previousPeriodUnits: number | null;
  brojTipovaObuce: number;
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

export interface ShoeTypeSalesDataQuality {
  missingCostRevenue: number;
  missingCostRevenueSharePct: number | null;
  estimatedCostRevenue?: number;
  estimatedCostRevenueSharePct?: number | null;
  unknownTypeRevenue: number;
  unknownTypeRevenueSharePct: number | null;
  revenueWithNivelacijaSplit: number;
  revenueWithNivelacijaSplitSharePct: number | null;
}

export interface SezonaOption {
  id: number;
  naziv: string;
  datumOd: string;
  datumDo: string;
}

export interface ShoeTypeSalesStatsResponse {
  generatedAt: string;
  fromDate: string | null;
  toDate: string | null;
  dataWindowFrom: string | null;
  dataWindowTo: string | null;
  sezonaId: number | null;
  storeId: number | null;
  dataScope?: string | null;
  shoeTypes: ShoeTypeSalesStat[];
  totals: ShoeTypeSalesTotals;
  dataQuality: ShoeTypeSalesDataQuality;
  sezone: SezonaOption[];
}

export interface ShoeTypeSalesStatsQuery {
  sezonaId?: number | null;
  fromDate?: string | null;
  toDate?: string | null;
  storeId?: number | null;
  dataScope?: string | null;
  signal?: AbortSignal;
}

export async function getShoeTypeSalesStats(
  query: ShoeTypeSalesStatsQuery = {}
): Promise<ShoeTypeSalesStatsResponse> {
  const params = new URLSearchParams();
  if (query.sezonaId != null) params.set("sezonaId", String(query.sezonaId));
  if (query.fromDate) params.set("fromDate", query.fromDate);
  if (query.toDate) params.set("toDate", query.toDate);
  if (query.storeId != null) params.set("storeId", String(query.storeId));
  if (query.dataScope) params.set("dataScope", query.dataScope);

  return fetchAnalyticsJson<ShoeTypeSalesStatsResponse>(
    "/api/analytics/shoe-type-sales-stats",
    params,
    "Greska pri ucitavanju statistike tipova obuce",
    { signal: query.signal }
  );
}

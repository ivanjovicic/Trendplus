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
  marginContribution: number;
  marginDataCoveragePct: number | null;
  marginPct: number;
  revenueWithNivelacijaSplit: number;
  popRevenueChangePct: number | null;
  popUnitsChangePct: number | null;
  prePostNivelacijaRevenueImpactPct: number | null;
  prePostNivelacijaUnitsImpactPct: number | null;
  prePostNivelacijaRevenueCoveragePct: number | null;
  sharePct?: number;
  reliabilityPct?: number;
  isUnknown?: boolean;
  recommendation?: AnalyticsRecommendation;
  // Legacy compatibility aliases (deprecated)
  promenaPrometa?: number | null;
  promenaKolicine?: number | null;
}

export interface ColorSalesTotals {
  ukupanPromet: number;
  ukupanMarzniDoprinos: number;
  prePromet: number;
  poslePromet: number;
  ukupnaKolicina: number;
  preKolicina: number;
  posleKolicina: number;
  previousPeriodRevenue: number | null;
  previousPeriodUnits: number | null;
  brojBoja: number;
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

export interface ColorSalesDataQuality {
  missingCostRevenue: number;
  missingCostRevenueSharePct: number | null;
  unknownColorRevenue: number;
  unknownColorRevenueSharePct: number | null;
  revenueWithNivelacijaSplit: number;
  revenueWithNivelacijaSplitSharePct: number | null;
}

export interface SezonaOption {
  id: number;
  naziv: string;
  datumOd: string;
  datumDo: string;
}

export interface ColorSalesStatsResponse {
  generatedAt: string;
  fromDate: string | null;
  toDate: string | null;
  dataWindowFrom: string | null;
  dataWindowTo: string | null;
  sezonaId: number | null;
  storeId: number | null;
  dataScope?: string | null;
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
    { signal: query.signal }
  );
}

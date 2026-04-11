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
  marginContribution: number;
  marginDataCoveragePct: number | null;
  marginPct: number;
  popRevenueChangePct: number | null;
  popUnitsChangePct: number | null;
  prePostNivelacijaRevenueImpactPct: number | null;
  prePostNivelacijaUnitsImpactPct: number | null;
  prePostNivelacijaRevenueCoveragePct: number | null;
  sharePct?: number;
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
  prosecnaMarza: number;
  prePromet: number;
  poslePromet: number;
  ukupnaKolicina: number;
  preKolicina: number;
  posleKolicina: number;
  previousPeriodRevenue: number | null;
  previousPeriodUnits: number | null;
  brojDobavljaca: number;
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
  fromDate: string | null;
  toDate: string | null;
  dataWindowFrom: string | null;
  dataWindowTo: string | null;
  sezonaId: number | null;
  storeId: number | null;
  dataScope?: string | null;
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

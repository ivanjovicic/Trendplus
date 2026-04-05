import { makeUrl } from "./analyticsApi";

export interface ColorSalesStat {
  boja: string;
  preNivelacijePromet: number;
  preNivelacijeKolicina: number;
  posleNivelacijePromet: number;
  posleNivelacijeKolicina: number;
  ukupanPromet: number;
  ukupnaKolicina: number;
  brojArtikalaSaNivelacijom: number;
  brojArtikalaUkupno: number;
  revenueWithCost: number;
  marginContribution: number;
  marginDataCoveragePct: number | null;
  marginPct: number;
  revenueWithNivelacijaSplit: number;
  promenaPrometa: number | null;
  promenaKolicine: number | null;
}

export interface ColorSalesTotals {
  ukupanPromet: number;
  ukupanMarzniDoprinos: number;
  prePromet: number;
  poslePromet: number;
  ukupnaKolicina: number;
  preKolicina: number;
  posleKolicina: number;
  brojBoja: number;
  promenaPrometaPct: number | null;
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
}

export async function getColorSalesStats(
  query: ColorSalesStatsQuery = {}
): Promise<ColorSalesStatsResponse> {
  const params = new URLSearchParams();
  if (query.sezonaId != null) params.set("sezonaId", String(query.sezonaId));
  if (query.fromDate) params.set("fromDate", query.fromDate);
  if (query.toDate) params.set("toDate", query.toDate);
  if (query.storeId != null) params.set("storeId", String(query.storeId));

  const response = await fetch(makeUrl("/api/analytics/color-sales-stats", params));
  if (!response.ok) {
    const text = await response.text();
    throw new Error(`Greska pri ucitavanju statistike boja artikala: ${text}`);
  }

  return response.json() as Promise<ColorSalesStatsResponse>;
}

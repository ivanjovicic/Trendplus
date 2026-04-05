import { makeUrl } from "./analyticsApi";

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
  brojArtikalaSaNivelacijom: number;
  brojArtikalaUkupno: number;
  revenueWithCost: number;
  marginContribution: number;
  marginDataCoveragePct: number | null;
  marginPct: number;
  promenaPrometa: number | null;
  promenaKolicine: number | null;
}

export interface SupplierSalesTotals {
  ukupanPromet: number;
  ukupanMarzniDoprinos: number;
  prePromet: number;
  poslePromet: number;
  ukupnaKolicina: number;
  preKolicina: number;
  posleKolicina: number;
  brojDobavljaca: number;
  promenaPrometaPct: number | null;
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
}

export async function getSupplierSalesStats(
  query: SupplierSalesStatsQuery = {}
): Promise<SupplierSalesStatsResponse> {
  const params = new URLSearchParams();
  if (query.sezonaId != null) params.set("sezonaId", String(query.sezonaId));
  if (query.fromDate) params.set("fromDate", query.fromDate);
  if (query.toDate) params.set("toDate", query.toDate);
  if (query.storeId != null) params.set("storeId", String(query.storeId));

  const response = await fetch(makeUrl("/api/analytics/supplier-sales-stats", params));
  if (!response.ok) {
    const text = await response.text();
    throw new Error(`Greska pri ucitavanju statistike dobavljaca: ${text}`);
  }

  return response.json() as Promise<SupplierSalesStatsResponse>;
}

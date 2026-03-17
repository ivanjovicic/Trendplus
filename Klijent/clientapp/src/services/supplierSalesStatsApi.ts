const API = import.meta.env.VITE_API_BASE_URL;

export interface SupplierSalesStat {
  dobavljacId: number | null;
  dobavljacNaziv: string;
  preNivelacijePromet: number;
  preNivelacijeKolicina: number;
  posleNivelacijePromet: number;
  posleNivelacijeKolicina: number;
  ukupanPromet: number;
  ukupnaKolicina: number;
  brojArtikalaSaNivelacijom: number;
  brojArtikalaUkupno: number;
  marginPct: number;
  promenaPrometa: number | null;
  promenaKolicine: number | null;
}

export interface SupplierSalesTotals {
  ukupanPromet: number;
  prePromet: number;
  poslePromet: number;
  ukupnaKolicina: number;
  preKolicina: number;
  posleKolicina: number;
  brojDobavljaca: number;
  promenaPrometaPct: number | null;
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
  sezonaId: number | null;
  storeId: number | null;
  suppliers: SupplierSalesStat[];
  totals: SupplierSalesTotals;
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

  const qs = params.toString();
  const url = qs
    ? `${API}/api/analytics/supplier-sales-stats?${qs}`
    : `${API}/api/analytics/supplier-sales-stats`;

  const response = await fetch(url);
  if (!response.ok) {
    const text = await response.text();
    throw new Error(`Greska pri ucitavanju statistike dobavljaca: ${text}`);
  }

  return response.json() as Promise<SupplierSalesStatsResponse>;
}

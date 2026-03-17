const API = import.meta.env.VITE_API_BASE_URL;

export interface ShoeTypeSalesStat {
  tipObuceId: number | null;
  tipObuceNaziv: string;
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

export interface ShoeTypeSalesTotals {
  ukupanPromet: number;
  prePromet: number;
  poslePromet: number;
  ukupnaKolicina: number;
  preKolicina: number;
  posleKolicina: number;
  brojTipovaObuce: number;
  promenaPrometaPct: number | null;
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
  sezonaId: number | null;
  storeId: number | null;
  shoeTypes: ShoeTypeSalesStat[];
  totals: ShoeTypeSalesTotals;
  sezone: SezonaOption[];
}

export interface ShoeTypeSalesStatsQuery {
  sezonaId?: number | null;
  fromDate?: string | null;
  toDate?: string | null;
  storeId?: number | null;
}

export async function getShoeTypeSalesStats(
  query: ShoeTypeSalesStatsQuery = {}
): Promise<ShoeTypeSalesStatsResponse> {
  const params = new URLSearchParams();
  if (query.sezonaId != null) params.set("sezonaId", String(query.sezonaId));
  if (query.fromDate) params.set("fromDate", query.fromDate);
  if (query.toDate) params.set("toDate", query.toDate);
  if (query.storeId != null) params.set("storeId", String(query.storeId));

  const qs = params.toString();
  const url = qs
    ? `${API}/api/analytics/shoe-type-sales-stats?${qs}`
    : `${API}/api/analytics/shoe-type-sales-stats`;

  const response = await fetch(url);
  if (!response.ok) {
    const text = await response.text();
    throw new Error(`Greska pri ucitavanju statistike tipova obuce: ${text}`);
  }

  return response.json() as Promise<ShoeTypeSalesStatsResponse>;
}

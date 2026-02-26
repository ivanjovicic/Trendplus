import type { PreNivelacijaPriorityResponse } from "../types/preNivelacija";

const API = import.meta.env.VITE_API_BASE_URL;

export interface PreNivelacijaQuery {
  supplierId?: number;
  seasonId?: number;
  footwearTypeId?: number;
  stockMin?: number;
  stockMax?: number;
  noSaleDaysMin?: number;
  minScore?: number;
  marginFloor?: number;
  page?: number;
  pageSize?: number;
}

export async function getPreNivelacijaPrioriteti(query: PreNivelacijaQuery): Promise<PreNivelacijaPriorityResponse> {
  const params = new URLSearchParams();
  if (query.supplierId != null) params.set("supplierId", String(query.supplierId));
  if (query.seasonId != null) params.set("seasonId", String(query.seasonId));
  if (query.footwearTypeId != null) params.set("footwearTypeId", String(query.footwearTypeId));
  if (query.stockMin != null) params.set("stockMin", String(query.stockMin));
  if (query.stockMax != null) params.set("stockMax", String(query.stockMax));
  if (query.noSaleDaysMin != null) params.set("noSaleDaysMin", String(query.noSaleDaysMin));
  if (query.minScore != null) params.set("minScore", String(query.minScore));
  if (query.marginFloor != null) params.set("marginFloor", String(query.marginFloor));
  params.set("page", String(query.page ?? 1));
  params.set("pageSize", String(query.pageSize ?? 20));

  const url = `${API}/api/analytics/pre-nivelacija-prioriteti?${params.toString()}`;
  const res = await fetch(url);
  if (!res.ok) {
    const text = await res.text();
    throw new Error(`Neuspesno ucitavanje pre-nivelacija prioriteta: ${text}`);
  }
  return res.json() as Promise<PreNivelacijaPriorityResponse>;
}

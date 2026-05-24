import type { PreNivelacijaPriorityResponse } from "../types/preNivelacija";
import { assertAnalyticsMetaSuccess } from "../utils/analyticsResponseMeta";

import { makeUrl } from "./analyticsApi";

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

  const res = await fetch(makeUrl(`/api/analytics/pre-nivelacija-prioriteti`, params));
  if (!res.ok) {
    const text = await res.text();
    throw new Error(`Neuspesno ucitavanje pre-nivelacija prioriteta: ${text}`);
  }

  const payload = (await res.json()) as PreNivelacijaPriorityResponse;
  return assertAnalyticsMetaSuccess(
    payload,
    (response) => response.meta,
    "Pre-nivelacija prioriteti trenutno nisu dostupni."
  );
}

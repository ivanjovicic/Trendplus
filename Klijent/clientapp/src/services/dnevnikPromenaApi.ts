import type { DnevnikPromenaDetail, DnevnikPromenaResponse } from "../types/dnevnikPromena";
import { appendDataScopeToParams } from "../utils/dataScope";
import { apiUrl } from "../utils/apiUrl";

export async function getDnevnikPromena(
  pageNumber: number = 1,
  pageSize: number = 50,
  filters?: {
    tipPromene?: string;
    artikalId?: number;
    naziv?: string;
    brojRacuna?: string;
    fromDate?: string;
    toDate?: string;
    sortBy?: string;
    sortDir?: "asc" | "desc";
  }
): Promise<DnevnikPromenaResponse> {
  const params = new URLSearchParams({
    pageNumber: String(pageNumber),
    pageSize: String(pageSize),
  });

  if (filters?.tipPromene) params.append("tipPromene", filters.tipPromene);
  if (filters?.artikalId) params.append("artikalId", String(filters.artikalId));
  if (filters?.naziv) params.append("naziv", filters.naziv);
  if (filters?.brojRacuna) params.append("brojRacuna", filters.brojRacuna);
  if (filters?.fromDate) params.append("fromDate", filters.fromDate);
  if (filters?.toDate) params.append("toDate", filters.toDate);
  if (filters?.sortBy) params.append("sortBy", filters.sortBy);
  if (filters?.sortDir) params.append("sortDir", filters.sortDir);
  appendDataScopeToParams(params);

  const resp = await fetch(apiUrl(`/api/dnevnik-promena?${params.toString()}`));
  if (!resp.ok) {
    const body = await resp.json().catch(() => null);
    const message = body?.detail ?? body?.title ?? body?.error ?? `HTTP ${resp.status}`;
    throw new Error(message);
  }

  return resp.json();
}

export async function getDnevnikPromenaById(id: string | number): Promise<DnevnikPromenaDetail | null> {
  const resp = await fetch(apiUrl(`/api/dnevnik-promena/${id}`));

  if (resp.status === 404) {
    return null;
  }

  if (!resp.ok) {
    const body = await resp.json().catch(() => null);
    const message = body?.detail ?? body?.title ?? body?.error ?? `HTTP ${resp.status}`;
    throw new Error(message);
  }

  return resp.json();
}

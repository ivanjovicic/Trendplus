import type { DnevnikPromenaResponse } from "../types/dnevnikPromena";
import { appendDataScopeToParams } from "../utils/dataScope";

const API = import.meta.env.VITE_API_BASE_URL as string;

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

  const resp = await fetch(`${API}/api/dnevnik-promena?${params.toString()}`);
  if (!resp.ok) {
    const body = await resp.json().catch(() => null);
    const message = body?.detail ?? body?.title ?? body?.error ?? `HTTP ${resp.status}`;
    throw new Error(message);
  }

  return resp.json();
}

import type { AnalyticsDetailResponse } from "../types/analyticsTable";
import { appendDataScopeToParams } from "../utils/dataScope";
import { ApiHttpError, fetchAnalyticsJson } from "./analyticsHttp";

export async function getAnalyticsDetail(
  table: string,
  id: string,
  queryString = "",
  signal?: AbortSignal
): Promise<AnalyticsDetailResponse | null> {
  const params = new URLSearchParams(queryString.startsWith("?") ? queryString.slice(1) : queryString);
  if (!params.has("dataScope")) {
    appendDataScopeToParams(params);
  }
  try {
    return await fetchAnalyticsJson<AnalyticsDetailResponse>(
      `/api/analitika/${encodeURIComponent(table)}/${encodeURIComponent(id)}`,
      params,
      "Greska pri ucitavanju analytics detalja",
      { signal, dedupe: false }
    );
  } catch (error) {
    if (error instanceof ApiHttpError && error.status === 404) {
      return null;
    }

    throw error;
  }
}

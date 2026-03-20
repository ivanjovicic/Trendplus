import type { AnalyticsDetailResponse } from "../types/analyticsTable";

const API = import.meta.env.VITE_API_BASE_URL as string;

export async function getAnalyticsDetail(
  table: string,
  id: string,
  queryString = ""
): Promise<AnalyticsDetailResponse | null> {
  const suffix = queryString ? (queryString.startsWith("?") ? queryString : `?${queryString}`) : "";
  const response = await fetch(`${API}/api/analitika/${encodeURIComponent(table)}/${encodeURIComponent(id)}${suffix}`);

  if (response.status === 404) {
    return null;
  }

  if (!response.ok) {
    const body = await response.json().catch(() => null);
    const message = body?.detail ?? body?.title ?? body?.message ?? `HTTP ${response.status}`;
    throw new Error(message);
  }

  return response.json() as Promise<AnalyticsDetailResponse>;
}

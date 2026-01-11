import type { InventoryStatus, SalesSummary, TopProductsResult } from "../types/analytics";

const API = import.meta.env.VITE_API_BASE_URL;

function makeUrl(path: string, params?: URLSearchParams) {
  if (import.meta.env.DEV) {
    return params ? `${path}?${params.toString()}` : path;
  }
  return params ? `${API}${path}?${params.toString()}` : `${API}${path}`;
}

export async function checkAnalyticsHealth(): Promise<{
  status: string;
  tables: { salesFacts: number; salesLineFacts: number; productsDim: number };
  message: string;
}> {
  const res = await fetch(makeUrl("/api/analytics/health"));
  if (!res.ok) {
    const text = await res.text();
    throw new Error(`Provera zdravlja analytics baze nije uspela: ${text}`);
  }
  return res.json();
}

export async function getSalesSummary(
  fromDate?: string,
  toDate?: string,
  storeId?: number
): Promise<SalesSummary> {
  const params = new URLSearchParams();
  if (fromDate) params.append("fromDate", fromDate);
  if (toDate) params.append("toDate", toDate);
  if (storeId != null) params.append("storeId", String(storeId));

  const res = await fetch(makeUrl("/api/analytics/sales/summary", params));
  if (!res.ok) throw new Error("Greška pri u?itavanju sažetka prodaje");
  return res.json();
}

export async function getTopProducts(
  top: number = 20,
  fromDate?: string,
  toDate?: string,
  storeId?: number
): Promise<TopProductsResult> {
  const params = new URLSearchParams({ top: String(top) });
  if (fromDate) params.append("fromDate", fromDate);
  if (toDate) params.append("toDate", toDate);
  if (storeId != null) params.append("storeId", String(storeId));

  const res = await fetch(makeUrl("/api/analytics/sales/top-products", params));
  if (!res.ok) throw new Error("Greška pri u?itavanju top proizvoda");
  return res.json();
}

export async function getInventoryStatus(lowStockThreshold: number = 2): Promise<InventoryStatus> {
  const params = new URLSearchParams({ lowStockThreshold: String(lowStockThreshold) });
  const res = await fetch(makeUrl("/api/analytics/inventory/status", params));
  if (!res.ok) throw new Error("Greška pri u?itavanju statusa zaliha");
  return res.json();
}

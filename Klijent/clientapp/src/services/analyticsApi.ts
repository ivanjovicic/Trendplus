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

// eslint-disable-next-line @typescript-eslint/no-unused-vars
export async function getSalesSummary(
  fromDate?: string,
  toDate?: string,
  _useCached: boolean = false
): Promise<SalesSummary> {
  void _useCached;
  const params = new URLSearchParams();
  if (fromDate) params.append("fromDate", fromDate);
  if (toDate) params.append("toDate", toDate);

  const endpoint = "/api/analytics/sales/summary";
  const res = await fetch(makeUrl(endpoint, params));
  if (!res.ok) throw new Error("Greška pri učitavanju sažetka prodaje");
  return res.json();
}

// eslint-disable-next-line @typescript-eslint/no-unused-vars
export async function getTopProducts(
  top: number = 20,
  fromDate?: string,
  toDate?: string,
  _useCached: boolean = false
): Promise<TopProductsResult> {
  void _useCached;
  const params = new URLSearchParams({ top: String(top) });
  if (fromDate) params.append("fromDate", fromDate);
  if (toDate) params.append("toDate", toDate);

  const endpoint = "/api/analytics/sales/top-products";
  const res = await fetch(makeUrl(endpoint, params));
  if (!res.ok) throw new Error("Greška pri učitavanju top proizvoda");
  return res.json();
}

// eslint-disable-next-line @typescript-eslint/no-unused-vars
export async function getInventoryStatus(
  lowStockThreshold: number = 2,
  _useCached: boolean = false
): Promise<InventoryStatus> {
  void _useCached;
  const params = new URLSearchParams({ lowStockThreshold: String(lowStockThreshold) });

  const endpoint = "/api/analytics/inventory/status";
  const res = await fetch(makeUrl(endpoint, params));
  if (!res.ok) throw new Error("Greška pri učitavanju statusa zaliha");
  return res.json();
}

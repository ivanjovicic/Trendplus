import type {
  DailySale,
  DashboardAdvancedSnapshot,
  DashboardValidationEndpoint,
  InventoryStatus,
  SalesSummary,
  TopProductsAdvancedResult,
  TopProductsResult,
} from "../types/analytics";

const API = import.meta.env.VITE_API_BASE_URL;

export function makeUrl(path: string, params?: URLSearchParams) {
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
  useCached = true
): Promise<SalesSummary> {
  const params = new URLSearchParams();
  if (fromDate) params.append("fromDate", fromDate);
  if (toDate) params.append("toDate", toDate);

  const endpoint = useCached
    ? "/api/analytics/cached/sales/summary"
    : "/api/analytics/sales/summary";

  const res = await fetch(makeUrl(endpoint, params));
  if (!res.ok) throw new Error("Greska pri ucitavanju sazetka prodaje");
  return res.json();
}

export async function getTopProducts(
  top = 20,
  fromDate?: string,
  toDate?: string,
  useCached = true
): Promise<TopProductsResult> {
  const params = new URLSearchParams({ top: String(top) });
  if (fromDate) params.append("fromDate", fromDate);
  if (toDate) params.append("toDate", toDate);

  const endpoint = useCached
    ? "/api/analytics/cached/sales/top-products"
    : "/api/analytics/sales/top-products";

  const res = await fetch(makeUrl(endpoint, params));
  if (!res.ok) throw new Error("Greska pri ucitavanju top proizvoda");
  return res.json();
}

export async function getTopProductsAdvanced(
  top = 10,
  fromDate?: string,
  toDate?: string,
  useCached = true
): Promise<TopProductsAdvancedResult> {
  const params = new URLSearchParams({ top: String(top) });
  if (fromDate) params.append("fromDate", fromDate);
  if (toDate) params.append("toDate", toDate);

  const endpoint = useCached
    ? "/api/analytics/cached/sales/top-products-advanced"
    : "/api/analytics/sales/top-products-advanced";

  const res = await fetch(makeUrl(endpoint, params));
  if (!res.ok) throw new Error("Greska pri ucitavanju naprednih top proizvoda");
  return res.json();
}

export async function getInventoryStatus(
  lowStockThreshold = 2,
  useCached = true
): Promise<InventoryStatus> {
  const params = new URLSearchParams({ lowStockThreshold: String(lowStockThreshold) });

  const endpoint = useCached
    ? "/api/analytics/cached/inventory/status"
    : "/api/analytics/inventory/status";

  const res = await fetch(makeUrl(endpoint, params));
  if (!res.ok) throw new Error("Greska pri ucitavanju statusa zaliha");
  return res.json();
}

export async function getDailySales(
  fromDate?: string,
  toDate?: string,
  useCached = true
): Promise<DailySale[]> {
  const params = new URLSearchParams();
  if (fromDate) params.append("fromDate", fromDate);
  if (toDate) params.append("toDate", toDate);

  const endpoint = useCached
    ? "/api/analytics/cached/sales/daily"
    : "/api/analytics/sales/daily";

  const res = await fetch(makeUrl(endpoint, params));
  if (!res.ok) throw new Error("Greska pri ucitavanju dnevne prodaje");
  return res.json();
}

export async function getDashboardAdvanced(
  fromDate?: string,
  toDate?: string,
  useCached = true
): Promise<DashboardAdvancedSnapshot> {
  const params = new URLSearchParams();
  if (fromDate) params.append("fromDate", fromDate);
  if (toDate) params.append("toDate", toDate);

  const endpoint = useCached
    ? "/api/analytics/cached/dashboard/advanced"
    : "/api/analytics/dashboard/advanced";

  const res = await fetch(makeUrl(endpoint, params));
  if (!res.ok) throw new Error("Greska pri ucitavanju advanced dashboard metrika");
  return res.json();
}

export async function getValidationCompleteness(
  useCached = true
): Promise<DashboardValidationEndpoint> {
  const endpoint = useCached
    ? "/api/analytics/cached/validation/completeness"
    : "/api/analytics/validation/completeness";

  const res = await fetch(makeUrl(endpoint));
  if (!res.ok) throw new Error("Greska pri ucitavanju completeness validacije");
  return res.json();
}

export async function getValidationFreshness(
  useCached = true
): Promise<DashboardValidationEndpoint> {
  const endpoint = useCached
    ? "/api/analytics/cached/validation/freshness"
    : "/api/analytics/validation/freshness";

  const res = await fetch(makeUrl(endpoint));
  if (!res.ok) throw new Error("Greska pri ucitavanju freshness validacije");
  return res.json();
}

export async function getValidationLostSales(
  useCached = true
): Promise<DashboardValidationEndpoint> {
  const endpoint = useCached
    ? "/api/analytics/cached/validation/lost-sales"
    : "/api/analytics/validation/lost-sales";

  const res = await fetch(makeUrl(endpoint));
  if (!res.ok) throw new Error("Greska pri ucitavanju lost-sales validacije");
  return res.json();
}

export async function getValidationNegativeQty(
  fromDate?: string,
  toDate?: string,
  useCached = true
): Promise<DashboardValidationEndpoint> {
  const params = new URLSearchParams();
  if (fromDate) params.append("fromDate", fromDate);
  if (toDate) params.append("toDate", toDate);

  const endpoint = useCached
    ? "/api/analytics/cached/validation/negative-qty"
    : "/api/analytics/validation/negative-qty";

  const res = await fetch(makeUrl(endpoint, params));
  if (!res.ok) throw new Error("Greska pri ucitavanju negative-qty validacije");
  return res.json();
}

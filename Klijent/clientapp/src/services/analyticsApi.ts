import type {
  CategoryData,
  CategoryTrendPoint,
  DailySale,
  DashboardAdvancedSnapshot,
  DashboardValidationEndpoint,
  GenderData,
  HourData,
  InventoryStatus,
  PaymentData,
  QuickInsights,
  ReorderSuggestion,
  SalesSummary,
  StoreOption,
  SupplierData,
  TopProductsAdvancedResult,
  TopProductsResult,
  TransactionStats,
  WeekdayData,
} from "../types/analytics";

const API = import.meta.env.VITE_API_BASE_URL;

export function makeUrl(path: string, params?: URLSearchParams) {
  return params ? `${API}${path}?${params.toString()}` : `${API}${path}`;
}

function appendFilterParams(
  params: URLSearchParams,
  fromDate?: string,
  toDate?: string,
  storeId?: number | null,
  supplierId?: number | null
) {
  if (fromDate) params.append("fromDate", fromDate);
  if (toDate) params.append("toDate", toDate);
  if (storeId != null) params.append("storeId", String(storeId));
  if (supplierId != null) params.append("supplierId", String(supplierId));
}

async function fetchJson<T>(path: string, params?: URLSearchParams, errorMessage?: string): Promise<T> {
  const res = await fetch(makeUrl(path, params));
  if (!res.ok) {
    const text = await res.text();
    throw new Error(errorMessage ?? text);
  }
  return res.json() as Promise<T>;
}

export async function checkAnalyticsHealth(): Promise<{
  status: string;
  tables: { salesFacts: number; salesLineFacts: number; productsDim: number };
  message: string;
}> {
  return fetchJson("/api/analytics/health", undefined, "Provera zdravlja analytics baze nije uspela.");
}

export async function getSalesSummary(
  fromDate?: string,
  toDate?: string,
  useCached = true,
  storeId?: number | null,
  supplierId?: number | null
): Promise<SalesSummary> {
  const params = new URLSearchParams();
  appendFilterParams(params, fromDate, toDate, storeId, supplierId);

  return fetchJson(
    useCached ? "/api/analytics/cached/sales/summary" : "/api/analytics/sales/summary",
    params,
    "Greska pri ucitavanju sazetka prodaje"
  );
}

export async function getTopProducts(
  top = 20,
  fromDate?: string,
  toDate?: string,
  useCached = true,
  storeId?: number | null,
  supplierId?: number | null
): Promise<TopProductsResult> {
  const params = new URLSearchParams({ top: String(top) });
  appendFilterParams(params, fromDate, toDate, storeId, supplierId);

  return fetchJson(
    useCached ? "/api/analytics/cached/sales/top-products" : "/api/analytics/sales/top-products",
    params,
    "Greska pri ucitavanju top proizvoda"
  );
}

export async function getTopProductsAdvanced(
  top = 10,
  fromDate?: string,
  toDate?: string,
  useCached = true,
  storeId?: number | null,
  supplierId?: number | null
): Promise<TopProductsAdvancedResult> {
  const params = new URLSearchParams({ top: String(top) });
  appendFilterParams(params, fromDate, toDate, storeId, supplierId);

  return fetchJson(
    useCached ? "/api/analytics/cached/sales/top-products-advanced" : "/api/analytics/sales/top-products-advanced",
    params,
    "Greska pri ucitavanju naprednih top proizvoda"
  );
}

export async function getInventoryStatus(
  lowStockThreshold = 2,
  useCached = true
): Promise<InventoryStatus> {
  const params = new URLSearchParams({ lowStockThreshold: String(lowStockThreshold) });

  return fetchJson(
    useCached ? "/api/analytics/cached/inventory/status" : "/api/analytics/inventory/status",
    params,
    "Greska pri ucitavanju statusa zaliha"
  );
}

export async function getDailySales(
  fromDate?: string,
  toDate?: string,
  useCached = true,
  storeId?: number | null,
  supplierId?: number | null
): Promise<DailySale[]> {
  const params = new URLSearchParams();
  appendFilterParams(params, fromDate, toDate, storeId, supplierId);

  return fetchJson(
    useCached ? "/api/analytics/cached/sales/daily" : "/api/analytics/sales/daily",
    params,
    "Greska pri ucitavanju dnevne prodaje"
  );
}

export async function getByCategory(
  fromDate?: string,
  toDate?: string,
  useCached = true,
  storeId?: number | null,
  supplierId?: number | null
): Promise<CategoryData[]> {
  const params = new URLSearchParams();
  appendFilterParams(params, fromDate, toDate, storeId, supplierId);

  return fetchJson(
    useCached ? "/api/analytics/cached/sales/by-category" : "/api/analytics/sales/by-category",
    params,
    "Greska pri ucitavanju prodaje po kategorijama"
  );
}

export async function getByGender(
  fromDate?: string,
  toDate?: string,
  useCached = true,
  storeId?: number | null,
  supplierId?: number | null
): Promise<GenderData[]> {
  const params = new URLSearchParams();
  appendFilterParams(params, fromDate, toDate, storeId, supplierId);

  return fetchJson(
    useCached ? "/api/analytics/cached/sales/by-gender" : "/api/analytics/sales/by-gender",
    params,
    "Greska pri ucitavanju prodaje po polu"
  );
}

export async function getBySupplier(
  fromDate?: string,
  toDate?: string,
  useCached = true,
  storeId?: number | null,
  supplierId?: number | null
): Promise<SupplierData[]> {
  const params = new URLSearchParams();
  appendFilterParams(params, fromDate, toDate, storeId, supplierId);

  return fetchJson(
    useCached ? "/api/analytics/cached/sales/by-supplier" : "/api/analytics/sales/by-supplier",
    params,
    "Greska pri ucitavanju prodaje po dobavljacima"
  );
}

export async function getByWeekday(
  fromDate?: string,
  toDate?: string,
  useCached = true,
  storeId?: number | null,
  supplierId?: number | null
): Promise<WeekdayData[]> {
  const params = new URLSearchParams();
  appendFilterParams(params, fromDate, toDate, storeId, supplierId);

  return fetchJson(
    useCached ? "/api/analytics/cached/sales/by-weekday" : "/api/analytics/sales/by-weekday",
    params,
    "Greska pri ucitavanju prodaje po danima"
  );
}

export async function getByHour(
  fromDate?: string,
  toDate?: string,
  useCached = true,
  storeId?: number | null,
  supplierId?: number | null
): Promise<HourData[]> {
  const params = new URLSearchParams();
  appendFilterParams(params, fromDate, toDate, storeId, supplierId);

  return fetchJson(
    useCached ? "/api/analytics/cached/sales/by-hour" : "/api/analytics/sales/by-hour",
    params,
    "Greska pri ucitavanju prodaje po satima"
  );
}

export async function getByPayment(
  fromDate?: string,
  toDate?: string,
  useCached = true,
  storeId?: number | null,
  supplierId?: number | null
): Promise<PaymentData[]> {
  const params = new URLSearchParams();
  appendFilterParams(params, fromDate, toDate, storeId, supplierId);

  return fetchJson(
    useCached ? "/api/analytics/cached/sales/by-payment" : "/api/analytics/sales/by-payment",
    params,
    "Greska pri ucitavanju prodaje po nacinu placanja"
  );
}

export async function getCategoryTrends(
  fromDate?: string,
  toDate?: string,
  useCached = true,
  storeId?: number | null,
  supplierId?: number | null
): Promise<CategoryTrendPoint[]> {
  const params = new URLSearchParams();
  appendFilterParams(params, fromDate, toDate, storeId, supplierId);

  return fetchJson(
    useCached ? "/api/analytics/cached/sales/category-trends" : "/api/analytics/sales/category-trends",
    params,
    "Greska pri ucitavanju trendova kategorija"
  );
}

export async function getTransactionStats(
  fromDate?: string,
  toDate?: string,
  useCached = true,
  storeId?: number | null,
  supplierId?: number | null
): Promise<TransactionStats> {
  const params = new URLSearchParams();
  appendFilterParams(params, fromDate, toDate, storeId, supplierId);

  return fetchJson(
    useCached ? "/api/analytics/cached/sales/transaction-stats" : "/api/analytics/sales/transaction-stats",
    params,
    "Greska pri ucitavanju statistike transakcija"
  );
}

export async function getQuickInsights(
  fromDate?: string,
  toDate?: string,
  useCached = true,
  storeId?: number | null,
  supplierId?: number | null
): Promise<QuickInsights> {
  const params = new URLSearchParams();
  appendFilterParams(params, fromDate, toDate, storeId, supplierId);

  return fetchJson(
    useCached ? "/api/analytics/cached/quick-insights" : "/api/analytics/quick-insights",
    params,
    "Greska pri ucitavanju brzih uvida"
  );
}

export async function getReorderSuggestions(
  useCached = true,
  supplierId?: number | null
): Promise<ReorderSuggestion[]> {
  const params = new URLSearchParams();
  if (supplierId != null) params.append("supplierId", String(supplierId));

  return fetchJson(
    useCached ? "/api/analytics/cached/reorder-suggestions" : "/api/analytics/reorder-suggestions",
    params,
    "Greska pri ucitavanju predloga za dopunu zaliha"
  );
}

export async function getDashboardAdvanced(
  fromDate?: string,
  toDate?: string,
  useCached = true,
  storeId?: number | null,
  supplierId?: number | null
): Promise<DashboardAdvancedSnapshot> {
  const params = new URLSearchParams();
  appendFilterParams(params, fromDate, toDate, storeId, supplierId);

  return fetchJson(
    useCached ? "/api/analytics/cached/dashboard/advanced" : "/api/analytics/dashboard/advanced",
    params,
    "Greska pri ucitavanju advanced dashboard metrika"
  );
}

export async function getStores(useCached = true): Promise<StoreOption[]> {
  return fetchJson(
    useCached ? "/api/analytics/cached/filters/stores" : "/api/analytics/filters/stores",
    undefined,
    "Greska pri ucitavanju prodavnica"
  );
}

export async function getValidationCompleteness(
  useCached = true
): Promise<DashboardValidationEndpoint> {
  return fetchJson(
    useCached ? "/api/analytics/cached/validation/completeness" : "/api/analytics/validation/completeness",
    undefined,
    "Greska pri ucitavanju completeness validacije"
  );
}

export async function getValidationFreshness(
  useCached = true
): Promise<DashboardValidationEndpoint> {
  return fetchJson(
    useCached ? "/api/analytics/cached/validation/freshness" : "/api/analytics/validation/freshness",
    undefined,
    "Greska pri ucitavanju freshness validacije"
  );
}

export async function getValidationLostSales(
  useCached = true
): Promise<DashboardValidationEndpoint> {
  return fetchJson(
    useCached ? "/api/analytics/cached/validation/lost-sales" : "/api/analytics/validation/lost-sales",
    undefined,
    "Greska pri ucitavanju lost-sales validacije"
  );
}

export async function getValidationNegativeQty(
  fromDate?: string,
  toDate?: string,
  useCached = true
): Promise<DashboardValidationEndpoint> {
  const params = new URLSearchParams();
  if (fromDate) params.append("fromDate", fromDate);
  if (toDate) params.append("toDate", toDate);

  return fetchJson(
    useCached ? "/api/analytics/cached/validation/negative-qty" : "/api/analytics/validation/negative-qty",
    params,
    "Greska pri ucitavanju negative-qty validacije"
  );
}

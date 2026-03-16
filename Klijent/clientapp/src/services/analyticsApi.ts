import type {
  AnalyticsDashboardBootstrap,
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
  SupplierFilterOption,
  SupplierData,
  TopProductsAdvancedResult,
  TopProductsResult,
  TransactionStats,
  WeekdayData,
} from "../types/analytics";

const API = import.meta.env.VITE_API_BASE_URL;
const DEFAULT_CLIENT_CACHE_TTL_MS = 15_000;
const responseCache = new Map<string, { expiresAt: number; value: unknown }>();
const inFlightRequests = new Map<string, Promise<unknown>>();

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
  const url = makeUrl(path, params);
  const cacheTtlMs = resolveClientCacheTtl(path);

  if (cacheTtlMs > 0) {
    const cached = responseCache.get(url);
    if (cached && cached.expiresAt > Date.now()) {
      return cached.value as T;
    }

    const existingRequest = inFlightRequests.get(url);
    if (existingRequest) {
      return existingRequest as Promise<T>;
    }
  }

  const request = (async () => {
    const res = await fetch(url);
    if (!res.ok) {
      throw new Error(await parseApiError(res, errorMessage));
    }

    const data = (await res.json()) as T;
    if (cacheTtlMs > 0) {
      responseCache.set(url, { expiresAt: Date.now() + cacheTtlMs, value: data });
    }

    return data;
  })();

  if (cacheTtlMs > 0) {
    inFlightRequests.set(url, request as Promise<unknown>);
  }

  try {
    return await request;
  } finally {
    if (cacheTtlMs > 0) {
      inFlightRequests.delete(url);
    }
  }
}

function resolveClientCacheTtl(path: string): number {
  if (path.includes("/api/analytics/cached/filters/stores")) return 5 * 60_000;
  if (path.includes("/api/analytics/cached/filters/suppliers")) return 60_000;
  if (path.includes("/api/analytics/cached/dashboard/bootstrap")) return 30_000;
  if (path.includes("/api/analytics/cached/")) return DEFAULT_CLIENT_CACHE_TTL_MS;
  return 0;
}

async function parseApiError(res: Response, fallbackMessage?: string): Promise<string> {
  const contentType = res.headers.get("content-type") ?? "";

  if (contentType.includes("application/json")) {
    const payload = (await res.json().catch(() => null)) as
      | { detail?: string; title?: string; message?: string }
      | null;
    const detail = payload?.detail ?? payload?.message ?? payload?.title;
    if (detail && fallbackMessage) {
      return detail.startsWith(fallbackMessage) ? detail : `${fallbackMessage}: ${detail}`;
    }
    if (detail) return detail;
  }

  const text = (await res.text()).trim();
  if (text && fallbackMessage) {
    return text.startsWith(fallbackMessage) ? text : `${fallbackMessage}: ${text}`;
  }

  if (text) return text;
  return fallbackMessage ?? `HTTP ${res.status}`;
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

export async function getDashboardBootstrap(
  fromDate?: string,
  toDate?: string,
  _useCached = true,
  storeId?: number | null,
  supplierId?: number | null
): Promise<AnalyticsDashboardBootstrap> {
  const params = new URLSearchParams();
  appendFilterParams(params, fromDate, toDate, storeId, supplierId);

  return fetchJson(
    "/api/analytics/cached/dashboard/bootstrap",
    params,
    "Greska pri ucitavanju analytics dashboard bootstrapa"
  );
}

export async function getStores(useCached = true): Promise<StoreOption[]> {
  return fetchJson(
    useCached ? "/api/analytics/cached/filters/stores" : "/api/analytics/filters/stores",
    undefined,
    "Greska pri ucitavanju prodavnica"
  );
}

export async function getSupplierFilters(
  fromDate?: string,
  toDate?: string,
  _useCached = true,
  storeId?: number | null
): Promise<SupplierFilterOption[]> {
  const params = new URLSearchParams();
  if (fromDate) params.append("fromDate", fromDate);
  if (toDate) params.append("toDate", toDate);
  if (storeId != null) params.append("storeId", String(storeId));

  return fetchJson(
    "/api/analytics/cached/filters/suppliers",
    params,
    "Greska pri ucitavanju filtera dobavljaca"
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

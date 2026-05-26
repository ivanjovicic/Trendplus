import type {
  AnalyticsDashboardBootstrap,
  AnalyticsRefreshStatus,
  AnalyticsCacheStatus,
  AnalyticsCacheInvalidateResponse,
  AnalyticsDataQualityHealth,
  CategoryData,
  CategoryTrendPoint,
  DataQualityIssueListResult,
  DataQualityIssueType,
  DataQualitySortBy,
  DataQualitySortDir,
  DataQualityTopOffendersResult,
  DataQualityTrendResult,
  PilotDataQualityIntakeReport,
  DailySale,
  DashboardAdvancedSnapshot,
  DashboardValidationEndpoint,
  ForecastDto,
  GenderData,
  HourData,
  InventoryAlertListDto,
  InventoryStatus,
  InventoryInsights,
  InventoryItemDetail,
  PaymentData,
  QuickInsights,
  ProductDecisionCenterResponse,
  RebalanceListDto,
  ReorderSuggestion,
  InventoryStoreComparison,
  InventoryActionWorkflow,
  InventoryActionDecisionInput,
  InventoryReportSchedule,
  InventoryReportScheduleInput,
  InventoryScheduleRunResponse,
  SalesSummary,
  SizeCurveDto,
  StoreOption,
  SupplierFilterOption,
  SupplierData,
  TopProductsAdvancedResult,
  TopProductsResult,
  TransactionStats,
  WeekdayData,
  AnalyticsActionItem,
  AnalyticsActionListResponse,
  AnalyticsActionCounts,
  AnalyticsActionUpsertInput,
  AnalyticsActionStatusUpdateInput,
  AnalyticsActionFilters,
  AnalyticsResponseMeta,
  SupplierDecisionDurableReport,
  PilotIntakeDurableReport,
} from "../types/analytics";
import type { DocumentOperationResponse } from "./exportApi";
import { apiUrl } from "../utils/apiUrl";
import { appendDataScopeToParams } from "../utils/dataScope";
import {
  API_FAILOVER_TIMEOUT_MS_OPTION,
  type ApiFailoverRequestInit,
} from "../utils/apiFailover";
import { fetchWithTimeout, FetchTimeoutError } from "../utils/fetchWithTimeout";
import { API_COLD_START_TIMEOUT_MS, getRetryTimeouts } from "../utils/apiTimeouts";
import {
  AnalyticsMetaError,
  assertAnalyticsMetaSuccess as assertAnalyticsMetaSuccessShared,
} from "../utils/analyticsResponseMeta";

const DEFAULT_CLIENT_CACHE_TTL_MS = 15_000;
const DEFAULT_ANALYTICS_GET_TIMEOUT_MS = API_COLD_START_TIMEOUT_MS;
const responseCache = new Map<string, { expiresAt: number; value: unknown }>();
const inFlightRequests = new Map<string, Promise<unknown>>();

type FailoverAwareWindow = Window & {
  __trendplusFailoverInstalled?: boolean;
};

export { AnalyticsMetaError };

export function makeUrl(path: string, params?: URLSearchParams) {
  const baseUrl = apiUrl(path);
  const finalParams = params ? new URLSearchParams(params.toString()) : new URLSearchParams();
  if (path.startsWith("/api/analytics")) {
    appendDataScopeToParams(finalParams);
  }

  return finalParams && Array.from(finalParams.keys()).length > 0
    ? `${baseUrl}?${finalParams.toString()}`
    : baseUrl;
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

function isApiFailoverLayerActive(): boolean {
  if (typeof window === "undefined") return false;
  return Boolean((window as FailoverAwareWindow).__trendplusFailoverInstalled);
}

async function fetchAnalyticsResponse(
  url: string,
  init: RequestInit | undefined,
  timeoutMs: number
): Promise<Response> {
  if (isApiFailoverLayerActive()) {
    const failoverInit: ApiFailoverRequestInit = {
      ...init,
      [API_FAILOVER_TIMEOUT_MS_OPTION]: timeoutMs,
    };
    return fetch(url, failoverInit);
  }

  return fetchWithTimeout(url, init, timeoutMs);
}

/**
 * Fetch with retry on timeout (for cold-start backends).
 * Tries with shorter timeout first, then retries with longer timeout if first attempt times out.
 */
async function fetchJsonWithRetry<T>(url: string, timeoutMs: number, errorMessage?: string): Promise<T> {
  if (isApiFailoverLayerActive()) {
    const res = await fetchAnalyticsResponse(url, undefined, timeoutMs);
    if (!res.ok) {
      throw new Error(await parseApiError(res, errorMessage));
    }
    const payload = (await res.json()) as T;
    return assertAnalyticsMetaSuccess(payload, errorMessage);
  }

  const { firstAttemptTimeoutMs, totalTimeoutMs } = getRetryTimeouts(timeoutMs);
  
  try {
    const res = await fetchAnalyticsResponse(url, undefined, firstAttemptTimeoutMs);
    if (!res.ok) {
      throw new Error(await parseApiError(res, errorMessage));
    }
    const payload = (await res.json()) as T;
    return assertAnalyticsMetaSuccess(payload, errorMessage);
  } catch (error) {
    // Don't retry on non-timeout errors
    if (!(error instanceof FetchTimeoutError)) {
      throw error;
    }

    // First attempt timed out - retry with longer timeout
  const res = await fetchAnalyticsResponse(url, undefined, totalTimeoutMs);
    if (!res.ok) {
      throw new Error(await parseApiError(res, errorMessage));
    }
    const payload = (await res.json()) as T;
    return assertAnalyticsMetaSuccess(payload, errorMessage);
  }
}

async function fetchJson<T>(path: string, params?: URLSearchParams, errorMessage?: string): Promise<T> {
  const finalParams = params ? new URLSearchParams(params.toString()) : undefined;
  const url = makeUrl(path, finalParams);
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
    const data = await fetchJsonWithRetry<T>(url, DEFAULT_ANALYTICS_GET_TIMEOUT_MS, errorMessage);
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

async function fetchJsonWithCachedFallback<T>(
  cachedPath: string,
  fallbackPath: string,
  params?: URLSearchParams,
  errorMessage?: string
): Promise<T> {
  try {
    return await fetchJson<T>(cachedPath, params, errorMessage);
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    const normalized = message.toLowerCase();
    if (!normalized.includes("404") && !normalized.includes("not found")) {
      throw error;
    }

    return fetchJson<T>(fallbackPath, params, errorMessage);
  }
}

async function postJson<T>(path: string, body: unknown, errorMessage?: string): Promise<T> {
  const timeoutMs = DEFAULT_ANALYTICS_GET_TIMEOUT_MS;
  const init: RequestInit = {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(body),
  };

  if (isApiFailoverLayerActive()) {
    const response = await fetchAnalyticsResponse(makeUrl(path), init, timeoutMs);
    if (!response.ok) {
      throw new Error(await parseApiError(response, errorMessage));
    }

    const payload = (await response.json()) as T;
    return assertAnalyticsMetaSuccess(payload, errorMessage);
  }

  const { firstAttemptTimeoutMs, totalTimeoutMs } = getRetryTimeouts(timeoutMs);
  
  try {
    const response = await fetchAnalyticsResponse(makeUrl(path), init, firstAttemptTimeoutMs);

    if (!response.ok) {
      throw new Error(await parseApiError(response, errorMessage));
    }

    const payload = (await response.json()) as T;
    return assertAnalyticsMetaSuccess(payload, errorMessage);
  } catch (error) {
    // Don't retry on non-timeout errors
    if (!(error instanceof FetchTimeoutError)) {
      throw error;
    }

    // First attempt timed out - retry with longer timeout
    const response = await fetchAnalyticsResponse(makeUrl(path), init, totalTimeoutMs);

    if (!response.ok) {
      throw new Error(await parseApiError(response, errorMessage));
    }

    const payload = (await response.json()) as T;
    return assertAnalyticsMetaSuccess(payload, errorMessage);
  }
}

async function patchJson<T>(path: string, body: unknown, errorMessage?: string): Promise<T> {
  const timeoutMs = DEFAULT_ANALYTICS_GET_TIMEOUT_MS;
  const init: RequestInit = {
    method: "PATCH",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(body),
  };

  if (isApiFailoverLayerActive()) {
    const response = await fetchAnalyticsResponse(makeUrl(path), init, timeoutMs);
    if (!response.ok) {
      throw new Error(await parseApiError(response, errorMessage));
    }

    const payload = (await response.json()) as T;
    return assertAnalyticsMetaSuccess(payload, errorMessage);
  }

  const { firstAttemptTimeoutMs, totalTimeoutMs } = getRetryTimeouts(timeoutMs);
  
  try {
    const response = await fetchAnalyticsResponse(makeUrl(path), init, firstAttemptTimeoutMs);

    if (!response.ok) {
      throw new Error(await parseApiError(response, errorMessage));
    }

    const payload = (await response.json()) as T;
    return assertAnalyticsMetaSuccess(payload, errorMessage);
  } catch (error) {
    // Don't retry on non-timeout errors
    if (!(error instanceof FetchTimeoutError)) {
      throw error;
    }

    // First attempt timed out - retry with longer timeout
    const response = await fetchAnalyticsResponse(makeUrl(path), init, totalTimeoutMs);

    if (!response.ok) {
      throw new Error(await parseApiError(response, errorMessage));
    }

    const payload = (await response.json()) as T;
    return assertAnalyticsMetaSuccess(payload, errorMessage);
  }
}

function resolveClientCacheTtl(path: string): number {
  if (path.includes("/api/analytics/cached/filters/stores")) return 5 * 60_000;
  if (path.includes("/api/analytics/cached/filters/suppliers")) return 60_000;
  if (path.includes("/api/analytics/cached/dashboard/bootstrap")) return 30_000;
  if (path.includes("/api/analytics/cached/inventory/forecast")) return 5 * 60_000;
  if (path.includes("/api/analytics/cached/inventory/size-curve")) return 5 * 60_000;
  if (path.includes("/api/analytics/cached/inventory/rebalance-suggestions")) return 2 * 60_000;
  if (path.includes("/api/analytics/cached/inventory/alerts")) return 60_000;
  if (path.includes("/api/analytics/cached/")) return DEFAULT_CLIENT_CACHE_TTL_MS;
  return 0;
}

function assertAnalyticsMetaSuccess<T>(payload: T, fallbackMessage?: string): T {
  return assertAnalyticsMetaSuccessShared(
    payload,
    (candidate) => {
      if (!candidate || typeof candidate !== "object") {
        return null;
      }

      const meta = (candidate as { meta?: unknown }).meta;
      return meta && typeof meta === "object"
        ? (meta as AnalyticsResponseMeta)
        : null;
    },
    fallbackMessage ?? "Podaci trenutno nisu dostupni."
  );
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

export async function getAnalyticsRefreshStatus(): Promise<AnalyticsRefreshStatus> {
  return fetchJson(
    "/api/analytics/refresh-status",
    undefined,
    "Greska pri ucitavanju statusa osvezavanja analitike"
  );
}

export async function getAnalyticsCacheStatus(): Promise<AnalyticsCacheStatus> {
  return fetchJson(
    "/api/analytics/cached/cache/status",
    undefined,
    "Greska pri ucitavanju statusa analytics cache-a"
  );
}

export async function clearAnalyticsCache(family = "all"): Promise<AnalyticsCacheInvalidateResponse> {
  const params = new URLSearchParams();
  if (family.trim()) params.set("family", family.trim());
  const response = await fetchAnalyticsResponse(
    makeUrl("/api/analytics/cached/cache/invalidate", params),
    { method: "POST" },
    DEFAULT_ANALYTICS_GET_TIMEOUT_MS
  );

  if (!response.ok) {
    throw new Error(await parseApiError(response, "Greska pri ciscenju analytics cache-a"));
  }

  return (await response.json()) as AnalyticsCacheInvalidateResponse;
}

export async function getProductDecisionCenter(options?: {
  fromDate?: string;
  toDate?: string;
  storeId?: number | null;
  supplierId?: number | null;
  top?: number;
  dataScope?: string | null;
}): Promise<ProductDecisionCenterResponse> {
  const params = new URLSearchParams();
  if (options?.fromDate) params.append("fromDate", options.fromDate);
  if (options?.toDate) params.append("toDate", options.toDate);
  if (options?.storeId != null) params.append("storeId", String(options.storeId));
  if (options?.supplierId != null) params.append("supplierId", String(options.supplierId));
  if (options?.top != null) params.append("top", String(options.top));
  if (options?.dataScope) params.append("dataScope", options.dataScope);

  return fetchJson(
    "/api/analytics/cached/products/decision-center",
    params,
    "Greska pri ucitavanju Product Decision Center pregleda"
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

export async function getDataQualityIssues(paramsInput: {
  type: DataQualityIssueType;
  page?: number;
  pageSize?: number;
  q?: string;
  sortBy?: DataQualitySortBy;
  sortDir?: DataQualitySortDir;
  dataScope?: string | null;
}): Promise<DataQualityIssueListResult> {
  const params = new URLSearchParams();
  params.set("type", paramsInput.type);
  params.set("page", String(paramsInput.page ?? 1));
  params.set("pageSize", String(paramsInput.pageSize ?? 25));
  if (paramsInput.q?.trim()) params.set("q", paramsInput.q.trim());
  if (paramsInput.sortBy) params.set("sortBy", paramsInput.sortBy);
  if (paramsInput.sortDir) params.set("sortDir", paramsInput.sortDir);
  if (paramsInput.dataScope) params.set("dataScope", paramsInput.dataScope);

  return fetchJson(
    "/api/analytics/data-quality/list",
    params,
    "Greska pri ucitavanju data quality problema"
  );
}

export async function getAnalyticsDataQualityHealth(
  lookbackDays?: number,
  dataScope?: string | null
): Promise<AnalyticsDataQualityHealth> {
  const params = new URLSearchParams();
  if (lookbackDays != null) params.set("lookbackDays", String(lookbackDays));
  if (dataScope) params.set("dataScope", dataScope);

  return fetchJson(
    "/api/analytics/data-quality/health",
    params,
    "Greska pri ucitavanju data quality health pregleda"
  );
}

export async function getDataQualityTopOffenders(
  issueType: DataQualityIssueType,
  limit = 10,
  dataScope?: string | null
): Promise<DataQualityTopOffendersResult> {
  const params = new URLSearchParams();
  params.set("issueType", issueType);
  params.set("limit", String(limit));
  if (dataScope) params.set("dataScope", dataScope);

  return fetchJson(
    "/api/analytics/data-quality/top-offenders",
    params,
    "Greska pri ucitavanju top data quality problema"
  );
}

export async function getAnalyticsDataQualityTrend(
  days = 7,
  dataScope?: string | null
): Promise<DataQualityTrendResult> {
  const params = new URLSearchParams();
  params.set("days", String(days));
  if (dataScope) params.set("dataScope", dataScope);

  return fetchJson(
    "/api/analytics/data-quality/trend",
    params,
    "Greska pri ucitavanju data quality trenda"
  );
}

export async function getPilotDataQualityIntakeReport(paramsInput: {
  fromDate?: string | null;
  toDate?: string | null;
  storeId?: number | null;
  supplierId?: number | null;
  dataScope?: string | null;
}): Promise<PilotDataQualityIntakeReport> {
  const params = new URLSearchParams();
  if (paramsInput.fromDate) params.set("fromDate", paramsInput.fromDate);
  if (paramsInput.toDate) params.set("toDate", paramsInput.toDate);
  if (paramsInput.storeId != null) params.set("storeId", String(paramsInput.storeId));
  if (paramsInput.supplierId != null) params.set("supplierId", String(paramsInput.supplierId));
  if (paramsInput.dataScope) params.set("dataScope", paramsInput.dataScope);

  return fetchJson(
    "/api/analytics/data-quality/intake-report",
    params,
    "Greska pri ucitavanju pilot intake report-a"
  );
}

export async function getSupplierDecisionDurableReport(paramsInput: {
  fromDate?: string | null;
  toDate?: string | null;
  storeId?: number | null;
  supplierId?: number | null;
  scope?: string | null;
  dataScope?: string | null;
  category?: string | null;
  gender?: string | null;
  seasonId?: number | null;
  minRevenue?: number | null;
  onlyHighConfidence?: boolean | null;
  excludeOosBeforeMarkdown?: boolean | null;
  section?: string | null;
}): Promise<SupplierDecisionDurableReport> {
  const params = new URLSearchParams();
  if (paramsInput.fromDate) params.set("fromDate", paramsInput.fromDate);
  if (paramsInput.toDate) params.set("toDate", paramsInput.toDate);
  if (paramsInput.storeId != null) params.set("storeId", String(paramsInput.storeId));
  if (paramsInput.supplierId != null) params.set("supplierId", String(paramsInput.supplierId));
  if (paramsInput.scope) params.set("scope", paramsInput.scope);
  if (paramsInput.dataScope) params.set("dataScope", paramsInput.dataScope);
  if (paramsInput.category) params.set("category", paramsInput.category);
  if (paramsInput.gender) params.set("gender", paramsInput.gender);
  if (paramsInput.seasonId != null) params.set("seasonId", String(paramsInput.seasonId));
  if (paramsInput.minRevenue != null) params.set("minRevenue", String(paramsInput.minRevenue));
  if (paramsInput.onlyHighConfidence != null) params.set("onlyHighConfidence", String(paramsInput.onlyHighConfidence));
  if (paramsInput.excludeOosBeforeMarkdown != null) params.set("excludeOosBeforeMarkdown", String(paramsInput.excludeOosBeforeMarkdown));
  if (paramsInput.section) params.set("section", paramsInput.section);

  return fetchJson(
    "/api/analytics/reports/supplier-decision",
    params,
    "Greska pri ucitavanju trajnog supplier report-a"
  );
}

export async function getPilotIntakeDurableReport(paramsInput: {
  fromDate?: string | null;
  toDate?: string | null;
  storeId?: number | null;
  supplierId?: number | null;
  scope?: string | null;
  dataScope?: string | null;
}): Promise<PilotIntakeDurableReport> {
  const params = new URLSearchParams();
  if (paramsInput.fromDate) params.set("fromDate", paramsInput.fromDate);
  if (paramsInput.toDate) params.set("toDate", paramsInput.toDate);
  if (paramsInput.storeId != null) params.set("storeId", String(paramsInput.storeId));
  if (paramsInput.supplierId != null) params.set("supplierId", String(paramsInput.supplierId));
  if (paramsInput.scope) params.set("scope", paramsInput.scope);
  if (paramsInput.dataScope) params.set("dataScope", paramsInput.dataScope);

  return fetchJson(
    "/api/analytics/reports/pilot-intake",
    params,
    "Greska pri ucitavanju trajnog pilot intake report-a"
  );
}

export async function getInventoryBalance(
  useCached = true,
  storeId?: number | null,
  supplierId?: number | null
): Promise<import("../types/analytics").InventoryBalance> {
  const params = new URLSearchParams();
  if (storeId != null) params.append("storeId", String(storeId));
  if (supplierId != null) params.append("supplierId", String(supplierId));

  return fetchJson(
    useCached ? "/api/analytics/cached/inventory/balance" : "/api/analytics/inventory/balance",
    params,
    "Greska pri ucitavanju bilansa zaliha"
  );
}

export async function getInventoryList(
  options?: {
    pageNumber?: number;
    pageSize?: number;
    search?: string;
    storeId?: number | null;
    supplierId?: number | null;
    sortBy?: string | null;
  }
): Promise<import("../types/analytics").InventoryPagedResponse> {
  const pageNumber = options?.pageNumber ?? 1;
  const pageSize = options?.pageSize ?? 50;
  const params = new URLSearchParams({ page: String(pageNumber), pageSize: String(pageSize) });
  if (options?.search) params.append("search", options.search);
  if (options?.storeId != null) params.append("storeId", String(options.storeId));
  if (options?.supplierId != null) params.append("supplierId", String(options.supplierId));
  if (options?.sortBy) params.append("sortBy", options.sortBy);

  return fetchJson(
    "/api/analytics/cached/inventory/list",
    params,
    "Greska pri ucitavanju liste zaliha"
  );
}

export async function getInventoryInsights(options?: {
  search?: string;
  storeId?: number | null;
  supplierId?: number | null;
  sortBy?: string | null;
}): Promise<InventoryInsights> {
  const params = new URLSearchParams();
  if (options?.search) params.append("search", options.search);
  if (options?.storeId != null) params.append("storeId", String(options.storeId));
  if (options?.supplierId != null) params.append("supplierId", String(options.supplierId));
  if (options?.sortBy) params.append("sortBy", options.sortBy);

  return fetchJsonWithCachedFallback(
    "/api/analytics/cached/inventory/insights",
    "/api/analytics/inventory/insights",
    params,
    "Greska pri ucitavanju inventory uvida"
  );
}

export async function getInventoryItemDetail(id: number): Promise<InventoryItemDetail> {
  return fetchJson(
    `/api/analytics/inventory/${id}/detail`,
    undefined,
    "Greska pri ucitavanju detalja artikla"
  );
}

export async function exportInventoryReport(options: {
  format: "pdf" | "xlsx" | "csv";
  orientation?: "portrait" | "landscape";
  includeFiltersAndMetadata?: boolean;
  forceAsync?: boolean;
  search?: string;
  storeId?: number | null;
  supplierId?: number | null;
  sortBy?: string | null;
}): Promise<DocumentOperationResponse> {
  return postJson(
    "/api/analytics/inventory/export",
    {
      format: options.format,
      orientation: options.orientation ?? "landscape",
      includeFiltersAndMetadata: options.includeFiltersAndMetadata ?? true,
      forceAsync: options.forceAsync ?? false,
      search: options.search,
      storeId: options.storeId,
      supplierId: options.supplierId,
      sortBy: options.sortBy,
    },
    "Greska pri server-side eksportu bilansa"
  );
}

export async function previewInventoryReport(options?: {
  orientation?: "portrait" | "landscape";
  includeFiltersAndMetadata?: boolean;
  search?: string;
  storeId?: number | null;
  supplierId?: number | null;
  sortBy?: string | null;
}): Promise<DocumentOperationResponse> {
  return postJson(
    "/api/analytics/inventory/print-preview",
    {
      orientation: options?.orientation ?? "landscape",
      includeFiltersAndMetadata: options?.includeFiltersAndMetadata ?? true,
      search: options?.search,
      storeId: options?.storeId,
      supplierId: options?.supplierId,
      sortBy: options?.sortBy,
    },
    "Greska pri pripremi print preview-a"
  );
}

export async function printBlankInventoryForm(options?: {
  orientation?: "portrait" | "landscape";
}): Promise<DocumentOperationResponse> {
  const params = new URLSearchParams();
  if (options?.orientation) params.set("orientation", options.orientation);
  const qs = params.size > 0 ? `?${params.toString()}` : "";
  return postJson(
    `/api/analytics/inventory/print-blank${qs}`,
    {},
    "Greska pri pripremi praznog obrasca za stampu"
  );
}

export async function getInventoryStoreComparison(options?: {
  compareStoreIds?: number[];
  supplierId?: number | null;
  search?: string;
}): Promise<InventoryStoreComparison> {
  const params = new URLSearchParams();
  for (const storeId of options?.compareStoreIds ?? []) {
    params.append("compareStoreIds", String(storeId));
  }
  if (options?.supplierId != null) params.append("supplierId", String(options.supplierId));
  if (options?.search) params.append("search", options.search);

  return fetchJsonWithCachedFallback(
    "/api/analytics/cached/inventory/store-comparison",
    "/api/analytics/inventory/store-comparison",
    params,
    "Greska pri ucitavanju poredenja prodavnica"
  );
}

export async function getInventoryActionSuggestions(options?: {
  storeId?: number | null;
  supplierId?: number | null;
  search?: string;
}): Promise<InventoryActionWorkflow> {
  const params = new URLSearchParams();
  if (options?.storeId != null) params.append("storeId", String(options.storeId));
  if (options?.supplierId != null) params.append("supplierId", String(options.supplierId));
  if (options?.search) params.append("search", options.search);

  return fetchJson(
    "/api/analytics/inventory/action-suggestions",
    params,
    "Greska pri ucitavanju predloga akcije"
  );
}

export async function saveInventoryActionDecision(
  suggestionKey: string,
  input: InventoryActionDecisionInput
): Promise<{
  suggestionKey: string;
  actionType: string;
  status: string;
  note?: string | null;
  updatedAtUtc: string;
  updatedByUserName: string;
}> {
  return postJson(
    `/api/analytics/inventory/action-suggestions/${encodeURIComponent(suggestionKey)}/decision`,
    input,
    "Greska pri cuvanju odluke za predlog akcije"
  );
}

export async function getInventoryReportSchedules(): Promise<InventoryReportSchedule[]> {
  return fetchJson(
    "/api/analytics/inventory/report-schedules",
    undefined,
    "Greska pri ucitavanju rasporeda za mail izvestaje"
  );
}

// ── Analytics Action Queue ─────────────────────────────────────────────────

export async function getAnalyticsActions(
  filters?: AnalyticsActionFilters
): Promise<AnalyticsActionListResponse> {
  const params = new URLSearchParams();
  if (filters?.status) params.append("status", filters.status);
  if (filters?.priority) params.append("priority", filters.priority);
  if (filters?.sourceType) params.append("sourceType", filters.sourceType);
  if (filters?.dataQualityStatus) params.append("dataQualityStatus", filters.dataQualityStatus);
  if (filters?.search) params.append("search", filters.search);
  if (filters?.page != null) params.append("page", String(filters.page));
  if (filters?.pageSize != null) params.append("pageSize", String(filters.pageSize));
  return fetchJson<AnalyticsActionListResponse>(
    "/api/analytics/actions",
    params,
    "Greška pri učitavanju liste akcija"
  );
}

export async function getAnalyticsActionCounts(): Promise<AnalyticsActionCounts> {
  return fetchJson<AnalyticsActionCounts>(
    "/api/analytics/actions/counts",
    undefined,
    "Greška pri učitavanju brojača akcija"
  );
}

export async function getAnalyticsActionById(id: number): Promise<AnalyticsActionItem> {
  return fetchJson<AnalyticsActionItem>(
    `/api/analytics/actions/${id}`,
    undefined,
    "Greška pri učitavanju akcije"
  );
}

type AnalyticsActionUpsertResponse = {
  item: AnalyticsActionItem;
  created: boolean;
  existing: boolean;
  status: string;
  sourceKey: string;
};

type AnalyticsActionSourceStatusInput = {
  sourceType: string;
  sourceKeys: string[];
};

type AnalyticsActionSourceStatusResponse = {
  items: Array<{
    sourceKey: string;
    exists: boolean;
    status?: string | null;
    actionId?: number | null;
  }>;
};

export async function upsertAnalyticsAction(
  input: AnalyticsActionUpsertInput
): Promise<AnalyticsActionItem> {
  const response = await upsertAnalyticsActionWithResult(input);
  return response.item;
}

export async function upsertAnalyticsActionWithResult(
  input: AnalyticsActionUpsertInput
): Promise<AnalyticsActionUpsertResponse> {
  return postJson<AnalyticsActionUpsertResponse>(
    "/api/analytics/actions",
    input,
    "Greška pri dodavanju akcije"
  );
}

export async function getAnalyticsActionSourceStatuses(
  input: AnalyticsActionSourceStatusInput
): Promise<AnalyticsActionSourceStatusResponse> {
  return postJson<AnalyticsActionSourceStatusResponse>(
    "/api/analytics/actions/status",
    input,
    "Greška pri proveri statusa akcija"
  );
}

export async function updateAnalyticsActionStatus(
  id: number,
  input: AnalyticsActionStatusUpdateInput
): Promise<AnalyticsActionItem> {
  return patchJson<AnalyticsActionItem>(
    `/api/analytics/actions/${id}/status`,
    input,
    "Greška pri ažuriranju statusa akcije"
  );
}

export async function createInventoryReportSchedule(input: InventoryReportScheduleInput): Promise<InventoryReportSchedule> {
  return postJson(
    "/api/analytics/inventory/report-schedules",
    input,
    "Greska pri kreiranju rasporeda izvestaja"
  );
}

export async function updateInventoryReportSchedule(id: number, input: InventoryReportScheduleInput): Promise<InventoryReportSchedule> {
  const response = await fetchAnalyticsResponse(makeUrl(`/api/analytics/inventory/report-schedules/${id}`), {
    method: "PUT",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(input),
  }, 60_000);

  if (!response.ok) {
    throw new Error(await parseApiError(response, "Greska pri azuriranju rasporeda izvestaja"));
  }

  return (await response.json()) as InventoryReportSchedule;
}

export async function runInventoryReportScheduleNow(id: number): Promise<InventoryScheduleRunResponse> {
  return postJson(
    `/api/analytics/inventory/report-schedules/${id}/run-now`,
    {},
    "Greska pri rucnom pokretanju rasporeda"
  );
}

// ── Demand Forecasting ──────────────────────────────────────────────────────────

export async function getForecast(options?: {
  storeId?: number | null;
  supplierId?: number | null;
  skuId?: number | null;
  sizeCode?: string;
  top?: number;
}): Promise<ForecastDto> {
  const params = new URLSearchParams();
  if (options?.storeId != null) params.append("storeId", String(options.storeId));
  if (options?.supplierId != null) params.append("supplierId", String(options.supplierId));
  if (options?.skuId != null) params.append("skuId", String(options.skuId));
  if (options?.sizeCode) params.append("sizeCode", options.sizeCode);
  if (options?.top != null) params.append("top", String(options.top));
  return fetchJson(
    "/api/analytics/cached/inventory/forecast",
    params,
    "Greska pri ucitavanju forecast podataka"
  );
}

// ── Size Curve Intelligence ─────────────────────────────────────────────────

export async function getSizeCurve(options?: {
  storeId?: number | null;
  supplierId?: number | null;
  skuId?: number | null;
  top?: number;
}): Promise<SizeCurveDto> {
  const params = new URLSearchParams();
  if (options?.storeId != null) params.append("storeId", String(options.storeId));
  if (options?.supplierId != null) params.append("supplierId", String(options.supplierId));
  if (options?.skuId != null) params.append("skuId", String(options.skuId));
  if (options?.top != null) params.append("top", String(options.top));
  return fetchJson(
    "/api/analytics/cached/inventory/size-curve",
    params,
    "Greska pri ucitavanju size curve"
  );
}

// ── Smart Rebalancing ─────────────────────────────────────────────────────────────

export async function getRebalanceSuggestions(options?: {
  fromStoreId?: number | null;
  toStoreId?: number | null;
  supplierId?: number | null;
  urgency?: string;
  top?: number;
}): Promise<RebalanceListDto> {
  const params = new URLSearchParams();
  if (options?.fromStoreId != null) params.append("fromStoreId", String(options.fromStoreId));
  if (options?.toStoreId != null) params.append("toStoreId", String(options.toStoreId));
  if (options?.supplierId != null) params.append("supplierId", String(options.supplierId));
  if (options?.urgency) params.append("urgency", options.urgency);
  if (options?.top != null) params.append("top", String(options.top));
  return fetchJson(
    "/api/analytics/cached/inventory/rebalance-suggestions",
    params,
    "Greska pri ucitavanju predloga za redistribuciju"
  );
}

// ── Inventory Alerts ────────────────────────────────────────────────────────────

export async function getInventoryAlerts(options?: {
  storeId?: number | null;
  supplierId?: number | null;
  severity?: string;
  top?: number;
}): Promise<InventoryAlertListDto> {
  const params = new URLSearchParams();
  if (options?.storeId != null) params.append("storeId", String(options.storeId));
  if (options?.supplierId != null) params.append("supplierId", String(options.supplierId));
  if (options?.severity) params.append("severity", options.severity);
  if (options?.top != null) params.append("top", String(options.top));
  return fetchJson(
    "/api/analytics/cached/inventory/alerts",
    params,
    "Greska pri ucitavanju inventory alertova"
  );
}


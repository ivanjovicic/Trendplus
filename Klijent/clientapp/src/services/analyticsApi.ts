import type {
  AnalyticsDashboardBootstrap,
  AnalyticsDataQualityHealth,
  CategoryData,
  CategoryTrendPoint,
  DataQualityIssueListResult,
  DataQualityIssueType,
  DataQualitySortBy,
  DataQualitySortDir,
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
} from "../types/analytics";
import type { DocumentOperationResponse } from "./exportApi";
import { apiUrl } from "../utils/apiUrl";
import { appendDataScopeToParams } from "../utils/dataScope";
import { fetchWithTimeout } from "../utils/fetchWithTimeout";

const DEFAULT_CLIENT_CACHE_TTL_MS = 15_000;
const DEFAULT_ANALYTICS_GET_TIMEOUT_MS = 60_000;
const responseCache = new Map<string, { expiresAt: number; value: unknown }>();
const inFlightRequests = new Map<string, Promise<unknown>>();

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

async function fetchJson<T>(path: string, params?: URLSearchParams, errorMessage?: string): Promise<T> {
  // Ensure analytics endpoints include the global dataScope param so the header select affects charts/tables
  const finalParams = params ? new URLSearchParams(params.toString()) : new URLSearchParams();
  if (path.startsWith("/api/analytics")) {
    appendDataScopeToParams(finalParams);
  }

  const hasParams = Array.from(finalParams.keys()).length > 0;
  const url = makeUrl(path, hasParams ? finalParams : undefined);
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
    const res = await fetchWithTimeout(url, undefined, DEFAULT_ANALYTICS_GET_TIMEOUT_MS);
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

async function postJson<T>(path: string, body: unknown, errorMessage?: string): Promise<T> {
  const response = await fetchWithTimeout(makeUrl(path), {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(body),
  }, 60_000);

  if (!response.ok) {
    throw new Error(await parseApiError(response, errorMessage));
  }

  return (await response.json()) as T;
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

export async function getDataQualityIssues(paramsInput: {
  type: DataQualityIssueType;
  page?: number;
  pageSize?: number;
  q?: string;
  sortBy?: DataQualitySortBy;
  sortDir?: DataQualitySortDir;
}): Promise<DataQualityIssueListResult> {
  const params = new URLSearchParams();
  params.set("type", paramsInput.type);
  params.set("page", String(paramsInput.page ?? 1));
  params.set("pageSize", String(paramsInput.pageSize ?? 25));
  if (paramsInput.q?.trim()) params.set("q", paramsInput.q.trim());
  if (paramsInput.sortBy) params.set("sortBy", paramsInput.sortBy);
  if (paramsInput.sortDir) params.set("sortDir", paramsInput.sortDir);

  return fetchJson(
    "/api/analytics/data-quality/list",
    params,
    "Greska pri ucitavanju data quality problema"
  );
}

export async function getAnalyticsDataQualityHealth(lookbackDays?: number): Promise<AnalyticsDataQualityHealth> {
  const params = new URLSearchParams();
  if (lookbackDays != null) params.set("lookbackDays", String(lookbackDays));

  return fetchJson(
    "/api/analytics/data-quality/health",
    params,
    "Greska pri ucitavanju data quality health pregleda"
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

  return fetchJson(
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

  return fetchJson(
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

export async function createInventoryReportSchedule(input: InventoryReportScheduleInput): Promise<InventoryReportSchedule> {
  return postJson(
    "/api/analytics/inventory/report-schedules",
    input,
    "Greska pri kreiranju rasporeda izvestaja"
  );
}

export async function updateInventoryReportSchedule(id: number, input: InventoryReportScheduleInput): Promise<InventoryReportSchedule> {
  const response = await fetchWithTimeout(makeUrl(`/api/analytics/inventory/report-schedules/${id}`), {
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

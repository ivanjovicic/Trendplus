import { makeUrl } from "./analyticsApi";

export interface IntelligencePageResponse<T> {
  asOfDate: string | null;
  page: number;
  pageSize: number;
  totalCount: number;
  items: T[];
}

export interface DemandSignalItem {
  articleId: number;
  sku: string;
  productName: string;
  category: string;
  supplierId: number | null;
  supplierName: string;
  storeId: number;
  storeName: string;
  storeCity: string | null;
  date: string;
  salesVelocity: number;
  demandAcceleration: number;
  daysSinceLastSale: number | null;
  launchAgeDays: number;
  storeCoverage: number;
  sourceRows: number;
}

export interface InventoryRiskSignalItem {
  articleId: number;
  sku: string;
  productName: string;
  category: string;
  supplierId: number | null;
  supplierName: string;
  date: string;
  stockQty: number;
  avgDailySales30d: number;
  daysOfCover: number | null;
  stockTurn: number | null;
  stockoutDays: number;
  lowStockDays: number;
  deadStockRisk: number;
}

export interface PriceIntelligenceItem {
  articleId: number;
  sku: string;
  productName: string;
  category: string;
  brandKey: string;
  supplierId: number | null;
  supplierName: string;
  priceDate: string;
  netPrice: number;
  listPrice: number;
  cost: number;
  priceIndexVsCategory: number | null;
  priceIndexVsBrand: number | null;
  discountDepth: number;
  marginPct: number | null;
}

export interface TrendMomentumItem {
  articleId: number;
  sku: string;
  productName: string;
  category: string;
  supplierId: number | null;
  supplierName: string;
  signalDate: string;
  externalTrendScore: number;
  localSalesAcceleration: number;
  trendEntropy: number;
}

export interface DemandSignalsQuery {
  date?: string;
  historyDays?: number;
  articleId?: number;
  storeId?: number;
  supplierId?: number;
  category?: string;
  minSalesVelocity?: number;
  minDemandAcceleration?: number;
  page?: number;
  pageSize?: number;
  sortBy?:
    | "productName"
    | "date"
    | "salesVelocity"
    | "daysSinceLastSale"
    | "launchAgeDays"
    | "storeCoverage"
    | "sourceRows"
    | "demandAcceleration";
  sortDir?: "asc" | "desc";
}

export interface InventoryRiskSignalsQuery {
  date?: string;
  historyDays?: number;
  articleId?: number;
  supplierId?: number;
  category?: string;
  minDeadStockRisk?: number;
  onlyAtRisk?: boolean;
  page?: number;
  pageSize?: number;
  sortBy?:
    | "productName"
    | "date"
    | "stockQty"
    | "daysOfCover"
    | "stockTurn"
    | "stockoutDays"
    | "lowStockDays"
    | "deadStockRisk";
  sortDir?: "asc" | "desc";
}

export interface PriceIntelligenceQuery {
  articleId?: number;
  supplierId?: number;
  category?: string;
  brandKey?: string;
  minDiscountDepth?: number;
  minMarginPct?: number;
  page?: number;
  pageSize?: number;
  sortBy?:
    | "productName"
    | "priceDate"
    | "netPrice"
    | "discountDepth"
    | "priceIndexVsCategory"
    | "priceIndexVsBrand"
    | "marginPct";
  sortDir?: "asc" | "desc";
}

export interface TrendMomentumQuery {
  articleId?: number;
  supplierId?: number;
  category?: string;
  minExternalTrendScore?: number;
  minLocalSalesAcceleration?: number;
  page?: number;
  pageSize?: number;
  sortBy?: "productName" | "signalDate" | "localSalesAcceleration" | "trendEntropy" | "externalTrendScore";
  sortDir?: "asc" | "desc";
}

function buildParams<T extends object>(query: T): URLSearchParams {
  const params = new URLSearchParams();

  Object.entries(query as Record<string, string | number | boolean | undefined | null>).forEach(([key, value]) => {
    if (value == null) return;
    if (typeof value === "string" && value.trim() === "") return;
    if (typeof value === "boolean") {
      if (value) params.append(key, "true");
      return;
    }

    params.append(key, String(value));
  });

  return params;
}

async function fetchJson<T>(path: string, params?: URLSearchParams, errorMessage?: string): Promise<T> {
  const res = await fetch(makeUrl(path, params));
  if (!res.ok) {
    const text = await res.text();
    throw new Error(errorMessage ?? text);
  }

  return res.json() as Promise<T>;
}

export async function getDemandSignals(
  query: DemandSignalsQuery = {}
): Promise<IntelligencePageResponse<DemandSignalItem>> {
  return fetchJson(
    "/api/analytics/intelligence/demand-signals",
    buildParams(query),
    "Greska pri ucitavanju demand signals."
  );
}

export async function getInventoryRiskSignals(
  query: InventoryRiskSignalsQuery = {}
): Promise<IntelligencePageResponse<InventoryRiskSignalItem>> {
  return fetchJson(
    "/api/analytics/intelligence/inventory-risk",
    buildParams(query),
    "Greska pri ucitavanju inventory risk signals."
  );
}

export async function getPriceIntelligence(
  query: PriceIntelligenceQuery = {}
): Promise<IntelligencePageResponse<PriceIntelligenceItem>> {
  return fetchJson(
    "/api/analytics/intelligence/price-intelligence",
    buildParams(query),
    "Greska pri ucitavanju price intelligence."
  );
}

export async function getTrendMomentum(
  query: TrendMomentumQuery = {}
): Promise<IntelligencePageResponse<TrendMomentumItem>> {
  return fetchJson(
    "/api/analytics/intelligence/trend-momentum",
    buildParams(query),
    "Greska pri ucitavanju trend momentum signals."
  );
}

async function collectPagedSignals<TItem, TQuery extends object>(
  fetchPage: (query: TQuery) => Promise<IntelligencePageResponse<TItem>>,
  query: TQuery,
  maxPages = 2
): Promise<IntelligencePageResponse<TItem>> {
  const responses = await Promise.all(
    Array.from({ length: maxPages }, (_, index) =>
      fetchPage({
        ...query,
        page: index + 1,
        pageSize: 100,
      } as TQuery)
    )
  );

  const first = responses[0] ?? {
    asOfDate: null,
    page: 1,
    pageSize: 100,
    totalCount: 0,
    items: [] as TItem[],
  };

  const totalCount = first.totalCount;
  const items = responses
    .flatMap((response) => response.items)
    .slice(0, totalCount || undefined);

  return {
    asOfDate: first.asOfDate,
    page: 1,
    pageSize: items.length,
    totalCount,
    items,
  };
}

export function getDemandSignalsSample(
  query: Omit<DemandSignalsQuery, "page" | "pageSize"> = {},
  maxPages = 2
) {
  return collectPagedSignals(getDemandSignals, query, maxPages);
}

export function getInventoryRiskSignalsSample(
  query: Omit<InventoryRiskSignalsQuery, "page" | "pageSize"> = {},
  maxPages = 2
) {
  return collectPagedSignals(getInventoryRiskSignals, query, maxPages);
}

export function getPriceIntelligenceSample(
  query: Omit<PriceIntelligenceQuery, "page" | "pageSize"> = {},
  maxPages = 2
) {
  return collectPagedSignals(getPriceIntelligence, query, maxPages);
}

export function getTrendMomentumSample(
  query: Omit<TrendMomentumQuery, "page" | "pageSize"> = {},
  maxPages = 2
) {
  return collectPagedSignals(getTrendMomentum, query, maxPages);
}

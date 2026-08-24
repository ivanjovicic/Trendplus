import { makeUrl } from "./analyticsApi";
import type { AnalyticsResponseMeta } from "../types/analytics";
import { AnalyticsMetaError, assertAnalyticsMetaSuccess } from "../utils/analyticsResponseMeta";

export type RecommendationCode =
  | "EXPAND"
  | "EXPAND_SELECTIVELY"
  | "HOLD"
  | "PRICE_NEGOTIATE"
  | "ASSORTMENT_REDUCE"
  | "OOS_FALSE_NEGATIVE"
  | "REVIEW_QUALITY";

export type SupplierDecisionHubFilters = {
  fromDate?: string;
  toDate?: string;
  category?: string;
  gender?: string;
  seasonId?: number;
  minRevenue?: number;
  onlyHighConfidence?: boolean;
  excludeOosBeforeMarkdown?: boolean;
  supplierId?: number;
  storeId?: number | null;
  dataScope?: string | null;
};

export type SupplierDecisionHubSortField =
  | "supplierName"
  | "revenue"
  | "units"
  | "fullPriceRevenueShare"
  | "fullPriceSellthrough"
  | "preMarkdownMarginPct"
  | "markdownRevenueShare"
  | "deadStockRate"
  | "unsoldStockValue"
  | "repeatWinnerRate"
  | "mlSupplierScore"
  | "supplierQualityIndex"
  | "confidenceScore";

export type SupplierDecisionRankingQuery = {
  page?: number;
  pageSize?: number;
  sortBy?: SupplierDecisionHubSortField;
  sortDir?: "asc" | "desc";
};

export type SummarySupplierItem = {
  supplierId: number;
  supplierName: string;
  revenue: number;
  mlSupplierScore: number;
  supplierQualityIndex: number;
  recommendationCode: RecommendationCode;
  confidenceScore: number;
};

export type KeyInsightItem = {
  title: string;
  value: string;
  details: string;
  tone: string;
};

export type ScorecardTrustMetadata = {
  requestedFrom?: string | null;
  requestedTo?: string | null;
  requestedPeriodFrom?: string | null;
  requestedPeriodTo?: string | null;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  requestedDataset?: string | null;
  effectiveDataset?: string | null;
  effectivePeriodLabel?: string | null;
  provenanceBasis?: string | null;
  dataCoverageStatus?: string | null;
  dataScope?: string | null;
  coverage?: string | null;
  usedFallback?: boolean;
  fallbackReason?: string | null;
  fallbackReasonCode?: string | null;
  rowCount?: number;
  ignoredRowCount?: number;
  zeroRevenueRowsExcludedCount?: number;
  missingSupplierNameCount?: number;
  recommendationAllowed?: boolean;
  hasData?: boolean;
  hasExplicitDateRange?: boolean;
  noSilentFallback?: boolean;
  windowDays?: number;
  dataNote?: string | null;
  lastRefreshAtUtc?: string | null;
};

export type SummaryResponse = {
  from: string;
  to: string;
  supplierCount: number;
  fullPriceRevenueShare: number;
  fullPriceSellthrough: number;
  markdownRevenueShare: number;
  preMarkdownMarginPct: number;
  capitalAtRisk: number;
  topGrowSuppliers: SummarySupplierItem[];
  topRiskSuppliers: SummarySupplierItem[];
  keyInsights: KeyInsightItem[];
  dataNote?: string | null;
  trustMetadata?: ScorecardTrustMetadata | null;
  meta?: AnalyticsResponseMeta | null;
};

export type QuadrantItem = {
  supplierId: number;
  supplierName: string;
  revenue: number;
  markdownDependency: number;
  fullPriceSellthrough: number;
  preMarkdownMarginPct: number;
  supplierQualityIndex: number;
  recommendationCode: RecommendationCode;
  confidenceScore: number;
};

export type QuadrantResponse = {
  items: QuadrantItem[];
};

export type RankingItem = {
  supplierId: number;
  supplierName: string;
  revenue: number;
  units: number;
  fullPriceRevenueShare: number;
  fullPriceSellthrough: number;
  preMarkdownMarginPct: number;
  markdownRevenueShare: number;
  deadStockRate: number;
  unsoldStockValue: number;
  repeatWinnerRate: number;
  mlSupplierScore: number;
  supplierQualityIndex: number;
  recommendationCode: RecommendationCode;
  confidenceScore: number;
  reliabilityPct?: number | null;
  dataQualityStatus?: string | null;
  statusReason?: string | null;
  reasonCodes?: string[] | null;
};

export type RankingResponse = {
  page: number;
  pageSize: number;
  totalCount: number;
  items: RankingItem[];
  dataNote?: string | null;
  trustMetadata?: ScorecardTrustMetadata | null;
  meta?: AnalyticsResponseMeta | null;
};

export type SupplierHeaderDto = {
  supplierId: number;
  supplierName: string;
  periodFrom: string;
  periodTo: string;
  mlSupplierScore: number;
  aiExplanation: string;
  topFeature1: string;
  topFeature2: string;
  topFeature3: string;
  supplierQualityIndex: number;
  recommendationCode: RecommendationCode;
  confidenceScore: number;
};

export type SupplierKpisDto = {
  revenue: number;
  units: number;
  fullPriceRevenueShare: number;
  fullPriceSellthrough: number;
  markdownRevenueShare: number;
  preMarkdownMarginPct: number;
  deadStockRate: number;
  unsoldStockValue: number;
  repeatWinnerRate: number;
  capitalAtRisk: number;
};

export type CategoryBreakdownItem = {
  category: string;
  revenue: number;
  units: number;
  fullPriceRevenueShare: number;
  fullPriceSellthrough: number;
  markdownRevenueShare: number;
  deadStockRate: number;
  unsoldStockValue: number;
  repeatWinnerRate: number;
};

export type ArticleDecisionItem = {
  articleId: number;
  sku: string;
  articleName: string;
  category: string;
  firstMarkdownDate: string;
  preRevenue30d: number;
  postRevenue30d: number;
  preSellthrough30d: number;
  preMargin30d: number;
  markdownRevenueShare: number;
  stockBeforeMarkdown: number;
  stockoutBeforeMarkdownFlag: boolean;
  signalQualityFlag: string;
  signalQualityReason: string;
};

export type RecommendationHistoryItem = {
  periodStart: string;
  revenue: number;
  fullPriceRevenueShare: number;
  markdownRevenueShare: number;
  fullPriceSellthrough: number;
  preMarkdownMarginPct: number;
  recommendationCode: RecommendationCode;
  recommendationTitle: string;
  recommendationReason: string;
};

export type SupplierDecisionDetailsResponse = {
  supplierHeader: SupplierHeaderDto;
  kpis: SupplierKpisDto;
  categoryBreakdown: CategoryBreakdownItem[];
  winningArticles: ArticleDecisionItem[];
  markdownDependentArticles: ArticleDecisionItem[];
  blockedByOosArticles: ArticleDecisionItem[];
  recommendationHistory: RecommendationHistoryItem[];
};

export class SupplierDecisionApiError extends Error {
  readonly status: number;
  readonly errorCode: string | null;
  readonly correlationId: string | null;

  constructor(message: string, status: number, errorCode?: string | null, correlationId?: string | null) {
    super(message);
    this.name = "SupplierDecisionApiError";
    this.status = status;
    this.errorCode = errorCode ?? null;
    this.correlationId = correlationId ?? null;
  }
}

function normalizeDatasetName(value: string | null | undefined): string | null {
  const normalized = (value ?? "").trim().toLowerCase();
  if (!normalized) return null;
  if (normalized === "all_history" || normalized === "all-time") return "all_time";
  return normalized;
}

function normalizeTrustMetadata(raw: unknown): ScorecardTrustMetadata | null {
  if (!raw || typeof raw !== "object") return null;
  const value = raw as Record<string, unknown>;

  const requestedFrom = typeof value.requestedFrom === "string"
    ? value.requestedFrom
    : typeof value.requestedPeriodFrom === "string"
      ? value.requestedPeriodFrom
      : null;

  const requestedTo = typeof value.requestedTo === "string"
    ? value.requestedTo
    : typeof value.requestedPeriodTo === "string"
      ? value.requestedPeriodTo
      : null;

  return {
    ...value,
    requestedFrom,
    requestedTo,
    requestedDataset: normalizeDatasetName(typeof value.requestedDataset === "string" ? value.requestedDataset : null),
    effectiveDataset: normalizeDatasetName(typeof value.effectiveDataset === "string" ? value.effectiveDataset : null),
  } as ScorecardTrustMetadata;
}

function appendFilterParams(params: URLSearchParams, filters: SupplierDecisionHubFilters) {
  if (filters.fromDate) params.append("fromDate", filters.fromDate);
  if (filters.toDate) params.append("toDate", filters.toDate);

  const category = filters.category?.trim();
  if (category) params.append("category", category);

  const gender = filters.gender?.trim();
  if (gender) params.append("gender", gender);

  if (filters.seasonId != null) params.append("seasonId", String(filters.seasonId));
  if (filters.minRevenue != null) params.append("minRevenue", String(filters.minRevenue));
  if (filters.onlyHighConfidence) params.append("onlyHighConfidence", "true");
  if (filters.excludeOosBeforeMarkdown) params.append("excludeOosBeforeMarkdown", "true");
  if (filters.supplierId != null) params.append("supplierId", String(filters.supplierId));
  if (filters.storeId != null) params.append("storeId", String(filters.storeId));
  if (filters.dataScope) params.append("dataScope", filters.dataScope);
}

async function fetchJson<T>(path: string, params: URLSearchParams, errorMessage: string): Promise<T> {
  const response = await fetch(makeUrl(path, params));
  if (!response.ok) {
    let message = errorMessage;
    let errorCode: string | null = null;
    let correlationId: string | null = null;

    try {
      const body = (await response.json()) as Record<string, unknown>;
      if (typeof body.title === "string" && body.title.trim().length > 0) {
        message = body.title;
      } else if (typeof body.message === "string" && body.message.trim().length > 0) {
        message = body.message;
      }
      if (typeof body.errorCode === "string") errorCode = body.errorCode;
      if (typeof body.correlationId === "string") correlationId = body.correlationId;
    } catch {
      // Ignore parse errors and keep fallback message.
    }

    throw new SupplierDecisionApiError(message, response.status, errorCode, correlationId);
  }

  const parsed = (await response.json()) as T;
  if (parsed && typeof parsed === "object") {
    const withTrust = parsed as { trustMetadata?: unknown };
    if (withTrust.trustMetadata !== undefined) {
      withTrust.trustMetadata = normalizeTrustMetadata(withTrust.trustMetadata);
    }
  }

  try {
    return assertAnalyticsMetaSuccess(
      parsed,
      (candidate) => {
        if (!candidate || typeof candidate !== "object") return null;
        return (candidate as { meta?: AnalyticsResponseMeta | null }).meta ?? null;
      },
      errorMessage
    );
  } catch (reason) {
    if (reason instanceof AnalyticsMetaError) {
      throw new SupplierDecisionApiError(
        reason.message,
        response.status,
        reason.errorCode,
        reason.correlationId
      );
    }
    throw reason;
  }
}

export async function getSupplierDecisionSummary(
  filters: SupplierDecisionHubFilters
): Promise<SummaryResponse> {
  const params = new URLSearchParams();
  appendFilterParams(params, filters);
  return fetchJson<SummaryResponse>(
    "/api/analytics/suppliers/decision-hub/summary",
    params,
    "Ne mogu da učitam sažetak dobavljača."
  );
}

export async function getSupplierDecisionQuadrant(
  filters: SupplierDecisionHubFilters
): Promise<QuadrantResponse> {
  const params = new URLSearchParams();
  appendFilterParams(params, filters);
  return fetchJson<QuadrantResponse>(
    "/api/analytics/suppliers/decision-hub/quadrant",
    params,
    "Ne mogu da učitam kvadrant dobavljača."
  );
}

export async function getSupplierDecisionRanking(
  filters: SupplierDecisionHubFilters,
  query: SupplierDecisionRankingQuery = {}
): Promise<RankingResponse> {
  const params = new URLSearchParams();
  appendFilterParams(params, filters);

  if (query.page != null) params.append("page", String(query.page));
  if (query.pageSize != null) params.append("pageSize", String(query.pageSize));
  if (query.sortBy) params.append("sortBy", query.sortBy);
  if (query.sortDir) params.append("sortDir", query.sortDir);

  return fetchJson<RankingResponse>(
    "/api/analytics/suppliers/decision-hub/ranking",
    params,
    "Ne mogu da učitam rang listu dobavljača."
  );
}

export async function getAllSupplierDecisionRanking(
  filters: SupplierDecisionHubFilters,
  query: SupplierDecisionRankingQuery = {}
): Promise<RankingResponse> {
  const requestedPageSize = Math.max(1, Math.min(query.pageSize ?? 100, 100));
  const firstPage = await getSupplierDecisionRanking(filters, {
    ...query,
    page: 1,
    pageSize: requestedPageSize,
  });

  if (firstPage.items.length >= firstPage.totalCount) {
    return firstPage;
  }

  const totalPages = Math.ceil(firstPage.totalCount / firstPage.pageSize);
  const remainingPages = await Promise.all(
    Array.from({ length: totalPages - 1 }, (_, index) =>
      getSupplierDecisionRanking(filters, {
        ...query,
        page: index + 2,
        pageSize: firstPage.pageSize,
      })
    )
  );

  return {
    ...firstPage,
    page: 1,
    items: [
      ...firstPage.items,
      ...remainingPages.flatMap((page) => page.items),
    ],
  };
}

export async function getSupplierDecisionDetails(
  supplierId: number,
  filters: SupplierDecisionHubFilters
): Promise<SupplierDecisionDetailsResponse> {
  const params = new URLSearchParams();
  appendFilterParams(params, filters);

  return fetchJson<SupplierDecisionDetailsResponse>(
    `/api/analytics/suppliers/decision-hub/${supplierId}/details`,
    params,
    "Ne mogu da učitam detalje dobavljača."
  );
}

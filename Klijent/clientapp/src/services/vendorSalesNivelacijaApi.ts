import { fetchAnalyticsJson } from "./analyticsHttp";
import type { AnalyticsResponseMeta } from "../types/analytics";
import {
    vendorSalesNivelacijaOptionsSchema,
    vendorSalesNivelacijaResponseSchema,
} from "../validation/analyticsResponseSchemas";

const REQUEST_TIMEOUT_MS = 60_000;

export interface VendorSalesNivelacijaRecommendation {
    status: "effective" | "neutral" | "ineffective" | "immature" | "insufficient_data";
    label: string;
    summary: string;
    confidencePct: number | null;
    reliabilityPct: number | null;
    dataQualityStatus: string;
    recommendationAllowed?: boolean | null;
    reasonCodes: string[];
}

export interface VendorSalesNivelacijaDriverMetricSummary {
    mean: number | null;
    median: number | null;
    sampleCount: number;
    meanWeighting: "unweighted" | "post_revenue_weighted";
}

export interface VendorSalesNivelacijaDriverSummary {
    momentumRevenue: VendorSalesNivelacijaDriverMetricSummary;
    elasticity: VendorSalesNivelacijaDriverMetricSummary;
    didRevenue: VendorSalesNivelacijaDriverMetricSummary;
    lostSalesOOS: VendorSalesNivelacijaDriverMetricSummary;
}

export interface VendorSalesNivelacijaVendorStat {
    vendorId: number | null;
    vendorName: string;
    preQty: number;
    preRevenue: number;
    postQty: number;
    postRevenue: number;
    changeQty: number;
    changeRevenue: number;
    changePercent: number | null;
    absoluteChangeRevenue: number;
    changeSharePercent: number;
    postRevenueSharePercent: number;
    avgCoveragePre30: number | null;
    avgCoveragePost30: number | null;
    avgSalesActivityRatePre30Pct?: number | null;
    avgSalesActivityRatePost30Pct?: number | null;
    avgSalesActiveDaysPre30?: number | null;
    avgSalesActiveDaysPost30?: number | null;
    hasComparableSalesWindow?: boolean;
    semanticChangePercentRevenue?: number | null;
    semanticChangePercentQty?: number | null;
    articleCount: number;
    activeArticlesCount: number;
    increasedPriceArticlesCount: number;
    decreasedPriceArticlesCount: number;
    reliabilityPct: number | null;
    recommendation?: VendorSalesNivelacijaRecommendation | null;
    comparableArticleCount?: number;
    primaryFootwearType?: string | null;
    primaryFootwearTypeSharePercent?: number | null;
    primaryFootwearTypeAvgElasticity?: number | null;
    avgElasticity?: number | null;
    driverMetrics?: VendorSalesNivelacijaDriverSummary | null;
    typeInsightsAuthoritative?: boolean;
}

export interface VendorSalesNivelacijaArticleStat {
    eventDate: string;
    vendorId: number | null;
    vendorName: string;
    sku: string;
    articleName: string;
    category: string;
    oldPrice: number | null;
    newPrice: number | null;
    preQty: number;
    preRevenue: number;
    postQty: number;
    postRevenue: number;
    changeQty: number;
    changeRevenue: number;
    changePercent: number | null;
    coveragePre30: number | null;
    coveragePost30: number | null;
    salesActivityRatePre30Pct?: number | null;
    salesActivityRatePost30Pct?: number | null;
    salesActiveDaysPre30?: number | null;
    salesActiveDaysPost30?: number | null;
    hasSalesWindow: boolean;
    hasPreSalesEvidence?: boolean;
    hasPostSalesEvidence?: boolean;
    hasComparableSalesWindow?: boolean;
    hasQtyBaseline?: boolean;
    qtyBaselineReason?: string | null;
    hasRevenueBaseline?: boolean;
    revenueBaselineReason?: string | null;
    semanticChangePercentRevenue?: number | null;
    semanticChangePercentQty?: number | null;
    priceChanged: boolean;
    priceChangePercent: number | null;
    rolling7dPreRevenue?: number | null;
    rolling7dPostRevenue?: number | null;
    momentumRevenue?: number | null;
    priceElasticity?: number | null;
    didRevenue?: number | null;
    didQty?: number | null;
    lostSalesOOS?: number | null;
    oosRate?: number | null;
    metricReason?: string | null;
}

export interface VendorSalesNivelacijaTotals {
    preQty: number;
    preRevenue: number;
    postQty: number;
    postRevenue: number;
    changeQty: number;
    changeRevenue: number;
    changePercent: number | null;
    vendorsCount: number;
    articlesCount: number;
    activeArticlesCount: number;
    avgRevenuePerArticlePre: number;
    avgRevenuePerArticlePost: number;
    avgPriceChangePercent: number | null;
    absoluteChangeRevenue: number;
    avgCoveragePre30: number | null;
    avgCoveragePost30: number | null;
    avgSalesActivityRatePre30Pct?: number | null;
    avgSalesActivityRatePost30Pct?: number | null;
    avgSalesActiveDaysPre30?: number | null;
    avgSalesActiveDaysPost30?: number | null;
    hasComparableSalesWindow?: boolean;
    comparableRows?: number;
    comparableArticlesCount?: number;
    comparableVendorsCount?: number;
}

export interface VendorSalesNivelacijaDataQuality {
    rawRows: number | null;
    deduplicatedRows: number | null;
    duplicateRowsRemoved: number | null;
    cohortRows?: number | null;
    cohortRowsExcluded?: number | null;
    returnedRows?: number | null;
    truncatedRows?: number | null;
    comparableRows?: number | null;
    comparableSharePercent?: number | null;
    isDetailTruncated?: boolean | null;
    cohortPolicy?: string | null;
    inactiveRows: number | null;
    unchangedPriceRows: number | null;
    analyzedRows: number | null;
    analyzedSharePercent: number | null;
    lowPostCoverageRows: number | null;
    avgCoveragePre30: number | null;
    avgCoveragePost30: number | null;
    lowPostActivityRows?: number | null;
    avgSalesActivityRatePre30Pct?: number | null;
    avgSalesActivityRatePost30Pct?: number | null;
    avgSalesActiveDaysPre30?: number | null;
    avgSalesActiveDaysPost30?: number | null;
}

export interface VendorSalesNivelacijaCategoryStat {
    category: string;
    articlesCount: number;
    vendorsCount: number;
    preQty: number;
    preRevenue: number;
    postQty: number;
    postRevenue: number;
    changeQty: number;
    changeRevenue: number;
    changePercent: number | null;
    hasComparableSalesWindow?: boolean;
    comparableArticleCount?: number;
    postRevenueSharePercent?: number | null;
    avgElasticity?: number | null;
}

export interface VendorSalesNivelacijaPriceDirectionStat {
    segment: string;
    articlesCount: number;
    vendorsCount: number;
    avgPriceChangePercent: number | null;
    changeRevenue: number;
    changePercent: number | null;
    hasComparableSalesWindow?: boolean;
    comparableArticleCount?: number;
}

export interface VendorSalesNivelacijaInsight {
    title: string;
    value: string;
    details: string;
    tone: "positive" | "negative" | "neutral" | "warning" | string;
}

export interface VendorSalesNivelacijaResponse {
    generatedAt: string;
    windowDays: number | null;
    vendorId: number | null;
    eventDate: string | null;
    from: string | null;
    to: string | null;
    category: string | null;
    includeInactive: boolean;
    storeId: number | null;
    dataScope: "all" | "existing" | "imported" | string;
    scopeApplied: boolean;
    categories: string[];
    vendorStats: VendorSalesNivelacijaVendorStat[];
    articleStats: VendorSalesNivelacijaArticleStat[];
    totals: VendorSalesNivelacijaTotals;
    dataQuality?: VendorSalesNivelacijaDataQuality | null;
    categoryStats: VendorSalesNivelacijaCategoryStat[];
    typeInsightsAuthoritative?: boolean;
    typeInsightsSource?: string | null;
    typeInsightsDenominator?: string | null;
    typeInsightsElasticityWeighting?: string | null;
    priceDirectionStats: VendorSalesNivelacijaPriceDirectionStat[];
    insights: VendorSalesNivelacijaInsight[];
    avgMomentumRevenue?: number | null;
    avgElasticity?: number | null;
    avgDidRevenue?: number | null;
    avgLostSalesOOS?: number | null;
    driverMetrics?: VendorSalesNivelacijaDriverSummary | null;
    oosRate?: number | null;
    metricsStatus?: string | null;
    recommendationAllowed?: boolean | null;
    dataCoverageStatus?: "complete" | "partial" | "empty" | "unavailable" | string;
    dataCoverageReason?: string;
    meta?: AnalyticsResponseMeta | null;
}

export interface VendorSalesNivelacijaQuery {
    vendorId?: number | null;
    eventDate?: string | null;
    from?: string | null;
    to?: string | null;
    category?: string | null;
    includeInactive?: boolean;
    maxRows?: number;
    storeId?: number | null;
    dataScope?: string | null;
    signal?: AbortSignal;
}

export interface VendorSalesNivelacijaOption {
    eventDate: string;
    eventsCount: number;
    vendorsCount: number | null;
    articlesCount: number | null;
    activeArticlesCount: number | null;
    hasSalesWindow: boolean;
    label: string;
}

export interface VendorSalesNivelacijaPrePostPairResponse {
    current: VendorSalesNivelacijaResponse;
    previous?: VendorSalesNivelacijaResponse | null;
    previousError?: string | null;
    outcomeLedger?: VendorSalesNivelacijaOutcomeLedger | null;
    outcomeLedgerError?: string | null;
}

export interface VendorSalesNivelacijaOutcomeLedger {
    population: string;
    periodBasis: string;
    stockEvidenceReason: string;
    eventCount: number;
    matureComparableCount: number;
    isTruncated: boolean;
    eventLimit: number;
    events: VendorSalesNivelacijaOutcomeEvent[];
    aggregates: VendorSalesNivelacijaOutcomeAggregate[];
}

export interface VendorSalesNivelacijaOutcomeEvent {
    eventId: number;
    eventDate: string;
    storeId: number | null;
    articleId: number;
    articleName: string;
    supplierId: number | null;
    supplierName: string;
    shoeTypeId: number | null;
    shoeType: string;
    discountDepthPct: number | null;
    depthBand: string;
    preUnits: number | null;
    postUnits: number | null;
    preRevenue: number | null;
    postRevenue: number | null;
    hasComparableWindows: boolean;
    preAveragePrice: number | null;
    postAveragePrice: number | null;
    preMarginContribution: number | null;
    postMarginContribution: number | null;
    preCostCoveragePct: number | null;
    postCostCoveragePct: number | null;
    costEvidenceReason: string;
    stockAtEvent: number | null;
    sellThroughPct: number | null;
    daysToClear: number | null;
    stockEvidenceReason: string;
}

export interface VendorSalesNivelacijaOutcomeAggregate {
    supplierId: number | null;
    supplierName: string;
    shoeTypeId: number | null;
    shoeType: string;
    depthBand: string;
    eventCount: number;
    matureComparableCount: number;
    medianRevenueDelta: number | null;
    medianUnitsDelta: number | null;
    medianMarginDelta: number | null;
    costCoveragePct: number | null;
}

export interface VendorSalesNivelacijaPrePostPairQuery extends VendorSalesNivelacijaQuery {
    previousFrom: string;
    previousTo: string;
}

export interface VendorSalesNivelacijaOptionsQuery {
    vendorId?: number | null;
    category?: string | null;
    take?: number;
    storeId?: number | null;
    dataScope?: string | null;
}

function normalizeDataScope(dataScope: string | null | undefined): "all" | "existing" | "imported" {
    const normalized = (dataScope ?? "all").trim().toLowerCase();
    return normalized === "existing" || normalized === "imported" ? normalized : "all";
}

export async function getVendorSalesNivelacija(
    query: VendorSalesNivelacijaQuery
): Promise<VendorSalesNivelacijaResponse> {
    const params = new URLSearchParams();
    if (query.vendorId != null) params.set("vendorId", String(query.vendorId));
    if (query.eventDate) params.set("eventDate", query.eventDate);
    if (query.from) params.set("from", query.from);
    if (query.to) params.set("to", query.to);
    if (query.category) params.set("category", query.category);
    if (query.includeInactive != null) params.set("includeInactive", String(query.includeInactive));
    if (query.maxRows != null) params.set("maxRows", String(query.maxRows));
    if (query.storeId != null) params.set("storeId", String(query.storeId));
    if (query.dataScope) params.set("dataScope", query.dataScope);

    const result = await fetchAnalyticsJson<VendorSalesNivelacijaResponse>(
        "/api/analytics/vendor-sales-nivelacija",
        params,
        "Pre/post nivelacija podaci trenutno nisu dostupni.",
        {
            signal: query.signal,
            timeoutMs: REQUEST_TIMEOUT_MS,
            schema: vendorSalesNivelacijaResponseSchema,
        },
    );

    const expectedStoreId = query.storeId ?? null;
    const expectedDataScope = normalizeDataScope(query.dataScope);
    if (result.scopeApplied !== true
        || result.storeId !== expectedStoreId
        || result.dataScope !== expectedDataScope) {
        throw new Error("Pre/post nivelacija nije potvrdila traženi objekat i opseg podataka.");
    }

    return result;
}

export async function getVendorSalesNivelacijaPrePostPair(
    query: VendorSalesNivelacijaPrePostPairQuery,
): Promise<VendorSalesNivelacijaPrePostPairResponse> {
    const params = new URLSearchParams();
    if (query.vendorId != null) params.set("vendorId", String(query.vendorId));
    if (query.eventDate) params.set("eventDate", query.eventDate);
    if (query.from) params.set("from", query.from);
    if (query.to) params.set("to", query.to);
    params.set("previousFrom", query.previousFrom);
    params.set("previousTo", query.previousTo);
    if (query.category) params.set("category", query.category);
    if (query.includeInactive != null) params.set("includeInactive", String(query.includeInactive));
    if (query.maxRows != null) params.set("maxRows", String(query.maxRows));
    if (query.storeId != null) params.set("storeId", String(query.storeId));
    if (query.dataScope) params.set("dataScope", query.dataScope);

    const result = await fetchAnalyticsJson<VendorSalesNivelacijaPrePostPairResponse>(
        "/api/analytics/vendor-sales-nivelacija/pre-post-pair",
        params,
        "Pre/post nivelacija podaci trenutno nisu dostupni.",
        {
            signal: query.signal,
            timeoutMs: REQUEST_TIMEOUT_MS,
        },
    );

    const expectedStoreId = query.storeId ?? null;
    const expectedDataScope = normalizeDataScope(query.dataScope);
    if (result.current.scopeApplied !== true
        || result.current.storeId !== expectedStoreId
        || result.current.dataScope !== expectedDataScope) {
        throw new Error("Pre/post nivelacija nije potvrdila traženi objekat i opseg podataka.");
    }

    return result;
}

export async function getVendorSalesNivelacijaOptions(
    query: VendorSalesNivelacijaOptionsQuery = {}
): Promise<VendorSalesNivelacijaOption[]> {
    const params = new URLSearchParams();
    if (query.vendorId != null) params.set("vendorId", String(query.vendorId));
    if (query.category) params.set("category", query.category);
    if (query.take != null) params.set("take", String(query.take));
    if (query.storeId != null) params.set("storeId", String(query.storeId));
    if (query.dataScope) params.set("dataScope", query.dataScope);

    return fetchAnalyticsJson<VendorSalesNivelacijaOption[]>(
        "/api/analytics/vendor-sales-nivelacija/options",
        params,
        "Opcije pre/post nivelacija trenutno nisu dostupne.",
        {
            timeoutMs: REQUEST_TIMEOUT_MS,
            schema: vendorSalesNivelacijaOptionsSchema,
        },
    );
}

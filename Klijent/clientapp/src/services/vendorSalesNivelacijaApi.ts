import { fetchWithTimeout } from "../utils/fetchWithTimeout";
import { apiUrl } from "../utils/apiUrl";
import type { AnalyticsResponseMeta } from "../types/analytics";
import { assertAnalyticsMetaSuccess } from "../utils/analyticsResponseMeta";

const REQUEST_TIMEOUT_MS = 60_000;

export interface VendorSalesNivelacijaRecommendation {
    status: "increase_focus" | "maintain" | "review" | "do_not_trust" | "insufficient_data";
    label: string;
    summary: string;
    confidencePct: number | null;
    reliabilityPct: number | null;
    dataQualityStatus: string;
    recommendationAllowed?: boolean | null;
    reasonCodes: string[];
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
    changePercent: number;
    absoluteChangeRevenue: number;
    changeSharePercent: number;
    postRevenueSharePercent: number;
    avgCoveragePre30: number | null;
    avgCoveragePost30: number | null;
    hasComparableSalesWindow?: boolean;
    semanticChangePercentRevenue?: number | null;
    semanticChangePercentQty?: number | null;
    articleCount: number;
    activeArticlesCount: number;
    increasedPriceArticlesCount: number;
    decreasedPriceArticlesCount: number;
    reliabilityPct: number | null;
    recommendation?: VendorSalesNivelacijaRecommendation | null;
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
    changePercent: number;
    vendorsCount: number;
    articlesCount: number;
    activeArticlesCount: number;
    avgRevenuePerArticlePre: number;
    avgRevenuePerArticlePost: number;
    avgPriceChangePercent: number;
    absoluteChangeRevenue: number;
    avgCoveragePre30: number | null;
    avgCoveragePost30: number | null;
    hasComparableSalesWindow?: boolean;
}

export interface VendorSalesNivelacijaDataQuality {
    rawRows: number | null;
    deduplicatedRows: number | null;
    duplicateRowsRemoved: number | null;
    inactiveRows: number | null;
    unchangedPriceRows: number | null;
    analyzedRows: number | null;
    analyzedSharePercent: number | null;
    lowPostCoverageRows: number | null;
    avgCoveragePre30: number | null;
    avgCoveragePost30: number | null;
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
    changePercent: number;
    hasComparableSalesWindow?: boolean;
}

export interface VendorSalesNivelacijaPriceDirectionStat {
    segment: string;
    articlesCount: number;
    vendorsCount: number;
    avgPriceChangePercent: number;
    changeRevenue: number;
    changePercent: number;
    hasComparableSalesWindow?: boolean;
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
    priceDirectionStats: VendorSalesNivelacijaPriceDirectionStat[];
    insights: VendorSalesNivelacijaInsight[];
    avgMomentumRevenue?: number | null;
    avgElasticity?: number | null;
    avgDidRevenue?: number | null;
    avgLostSalesOOS?: number | null;
    oosRate?: number | null;
    metricsStatus?: string | null;
    recommendationAllowed?: boolean | null;
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

    const baseUrl = apiUrl("/api/analytics/vendor-sales-nivelacija");
    const url = params.toString()
        ? `${baseUrl}?${params.toString()}`
        : baseUrl;

    const response = await fetchWithTimeout(url, { signal: query.signal }, REQUEST_TIMEOUT_MS);
    if (!response.ok) {
        const text = await response.text();
        throw new Error(`Neuspesno ucitavanje pre/post nivelacija analitike: ${text}`);
    }

    const payload = (await response.json()) as VendorSalesNivelacijaResponse;
    const result = assertAnalyticsMetaSuccess(
        payload,
        (result) => result.meta,
        "Pre/post nivelacija podaci trenutno nisu dostupni."
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

export async function getVendorSalesNivelacijaOptions(
    query: VendorSalesNivelacijaOptionsQuery = {}
): Promise<VendorSalesNivelacijaOption[]> {
    const params = new URLSearchParams();
    if (query.vendorId != null) params.set("vendorId", String(query.vendorId));
    if (query.category) params.set("category", query.category);
    if (query.take != null) params.set("take", String(query.take));
    if (query.storeId != null) params.set("storeId", String(query.storeId));
    if (query.dataScope) params.set("dataScope", query.dataScope);

    const baseUrl = apiUrl("/api/analytics/vendor-sales-nivelacija/options");
    const url = params.toString()
        ? `${baseUrl}?${params.toString()}`
        : baseUrl;

    const response = await fetchWithTimeout(url, undefined, REQUEST_TIMEOUT_MS);
    if (!response.ok) {
        const text = await response.text();
        throw new Error(`Neuspesno ucitavanje nivo opcija: ${text}`);
    }

    return response.json() as Promise<VendorSalesNivelacijaOption[]>;
}

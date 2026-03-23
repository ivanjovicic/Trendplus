import { fetchWithTimeout } from "../utils/fetchWithTimeout";
import { apiUrl } from "../utils/apiUrl";

const REQUEST_TIMEOUT_MS = 60_000;

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
    articleCount: number;
    activeArticlesCount: number;
    increasedPriceArticlesCount: number;
    decreasedPriceArticlesCount: number;
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
    changePercent: number;
    hasSalesWindow: boolean;
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
}

export interface VendorSalesNivelacijaDataQuality {
    rawRows: number;
    deduplicatedRows: number;
    duplicateRowsRemoved: number;
    inactiveRows: number;
    unchangedPriceRows: number;
    analyzedRows: number;
    analyzedSharePercent: number;
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
}

export interface VendorSalesNivelacijaPriceDirectionStat {
    segment: string;
    articlesCount: number;
    vendorsCount: number;
    avgPriceChangePercent: number;
    changeRevenue: number;
    changePercent: number;
}

export interface VendorSalesNivelacijaInsight {
    title: string;
    value: string;
    details: string;
    tone: "positive" | "negative" | "neutral" | "warning" | string;
}

export interface VendorSalesNivelacijaResponse {
    generatedAt: string;
    windowDays: number;
    vendorId: number | null;
    eventDate: string | null;
    from: string | null;
    to: string | null;
    category: string | null;
    includeInactive: boolean;
    categories: string[];
    vendorStats: VendorSalesNivelacijaVendorStat[];
    articleStats: VendorSalesNivelacijaArticleStat[];
    totals: VendorSalesNivelacijaTotals;
    dataQuality: VendorSalesNivelacijaDataQuality;
    categoryStats: VendorSalesNivelacijaCategoryStat[];
    priceDirectionStats: VendorSalesNivelacijaPriceDirectionStat[];
    insights: VendorSalesNivelacijaInsight[];
    avgMomentumRevenue?: number | null;
    avgElasticity?: number | null;
    avgDidRevenue?: number | null;
    avgLostSalesOOS?: number | null;
    oosRate?: number | null;
    metricsStatus?: string | null;
}

export interface VendorSalesNivelacijaQuery {
    vendorId?: number | null;
    eventDate?: string | null;
    from?: string | null;
    to?: string | null;
    category?: string | null;
    includeInactive?: boolean;
}

export interface VendorSalesNivelacijaOption {
    eventDate: string;
    eventsCount: number;
    vendorsCount: number;
    articlesCount: number;
    activeArticlesCount: number;
    hasSalesWindow: boolean;
    label: string;
}

export interface VendorSalesNivelacijaOptionsQuery {
    vendorId?: number | null;
    category?: string | null;
    take?: number;
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

    const baseUrl = apiUrl("/api/analytics/vendor-sales-nivelacija");
    const url = params.toString()
        ? `${baseUrl}?${params.toString()}`
        : baseUrl;

    const response = await fetchWithTimeout(url, undefined, REQUEST_TIMEOUT_MS);
    if (!response.ok) {
        const text = await response.text();
        throw new Error(`Neuspesno ucitavanje pre/post nivelacija analitike: ${text}`);
    }

    return response.json() as Promise<VendorSalesNivelacijaResponse>;
}

export async function getVendorSalesNivelacijaOptions(
    query: VendorSalesNivelacijaOptionsQuery = {}
): Promise<VendorSalesNivelacijaOption[]> {
    const params = new URLSearchParams();
    if (query.vendorId != null) params.set("vendorId", String(query.vendorId));
    if (query.category) params.set("category", query.category);
    if (query.take != null) params.set("take", String(query.take));

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

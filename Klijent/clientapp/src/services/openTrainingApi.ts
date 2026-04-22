import { apiUrl } from "../utils/apiUrl";

// -- Types ---------------------------------------------------------------------

export interface OpenTrainingStats {
    datasetCount:          number;
    productCount:          number;
    labelCount:            number;
    popularityLabelCount:  number;
    dealLabelCount:        number;
    splitCount:            number;
    splits:                { split: string; count: number }[];
}

export interface OpenTrainingDataset {
    id:           number;
    name:         string;
    sourceType:   string;
    description:  string | null;
    license:      string | null;
    rawLocation:  string | null;
    createdAt:    string;
    productCount: number;
}

export interface TopLabel {
    productId: number;
    title:     string;
    brand:     string | null;
    shoeType:  string | null;
    price:     number | null;
    currency:  string | null;
    imageUrl:  string | null;
    labelType: string;
    score:     number;
    createdAt: string;
}

export interface ShoeTypeCount {
    shoeType: string;
    productCount: number;
}

export interface BrandCount {
    brand: string;
    productCount: number;
}

export interface DiagnosticsHistogramBucket {
    rangeLabel: string;
    lo: number;
    hi: number;
    count: number;
}

export interface DiagnosticsQuality {
    total: number;
    withRating: number;
    withReviews: number;
    withPrice: number;
    withBrand: number;
    withShoeType: number;
    withRatingAndReviews: number;
}

export interface DiagnosticsTopGroup {
    brand: string;
    shoeType: string;
    productCount: number;
    withRating: number;
}

export interface DiagnosticsScoreStats {
    count: number;
    min: number;
    max: number;
    avg: number;
    median: number;
    p25: number;
    p75: number;
}

export interface Diagnostics {
    histogram: DiagnosticsHistogramBucket[];
    quality: DiagnosticsQuality | null;
    topGroups: DiagnosticsTopGroup[];
    scoreStats: DiagnosticsScoreStats | null;
}

export interface RecomputeLabelsRequest {
    datasetNames?:       string[];
    minProductsPerGroup?: number;
}

export interface RecomputeLabelsResult {
    datasetCount:    number;
    candidateProducts: number;
    scoredProducts:  number;
    groupCount:      number;
    removedLabels:   number;
    insertedLabels:  number;
    computedAtUtc:   string;
}

// -- API calls -----------------------------------------------------------------

export async function fetchOpenTrainingStats(): Promise<OpenTrainingStats> {
    const res = await fetch(apiUrl("/api/open-training/stats"));
    if (!res.ok) throw new Error(`Stats fetch failed: ${res.status}`);
    return res.json();
}

export async function fetchOpenTrainingDatasets(): Promise<OpenTrainingDataset[]> {
    const res = await fetch(apiUrl("/api/open-training/datasets"));
    if (!res.ok) throw new Error(`Datasets fetch failed: ${res.status}`);
    return res.json();
}

export async function fetchTopLabels(
    labelType: "popularity_prior" | "deal_score" = "popularity_prior",
    take: number = 20,
    shoeType?: string,
    brand?: string,
): Promise<TopLabel[]> {
    const params = new URLSearchParams({ labelType, take: String(take) });
    if (shoeType) params.set("shoeType", shoeType);
    if (brand) params.set("brand", brand);

    const res = await fetch(apiUrl(`/api/open-training/labels/top?${params}`));
    if (!res.ok) throw new Error(`Top labels fetch failed: ${res.status}`);
    return res.json();
}

export async function fetchShoeTypes(): Promise<ShoeTypeCount[]> {
    const res = await fetch(apiUrl("/api/open-training/shoe-types"));
    if (!res.ok) throw new Error(`Shoe types fetch failed: ${res.status}`);
    return res.json();
}

export async function fetchBrands(shoeType?: string): Promise<BrandCount[]> {
    const params = new URLSearchParams();
    if (shoeType) params.set("shoeType", shoeType);
    const suffix = params.size > 0 ? `?${params}` : "";

    const res = await fetch(apiUrl(`/api/open-training/brands${suffix}`));
    if (!res.ok) throw new Error(`Brands fetch failed: ${res.status}`);
    return res.json();
}

export async function fetchDiagnostics(
    labelType: "popularity_prior" | "deal_score" = "popularity_prior",
): Promise<Diagnostics> {
    const params = new URLSearchParams({ labelType });
    const res = await fetch(apiUrl(`/api/open-training/diagnostics?${params}`));
    if (!res.ok) throw new Error(`Diagnostics fetch failed: ${res.status}`);
    return res.json();
}

export async function recomputeLabels(
    body: RecomputeLabelsRequest,
): Promise<RecomputeLabelsResult> {
    const res = await fetch(apiUrl("/api/open-training/recompute-labels"), {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
    });
    if (!res.ok) {
        const text = await res.text();
        throw new Error(text || `HTTP ${res.status}`);
    }
    return res.json();
}


const BASE = import.meta.env.VITE_API_URL ?? "";

// ── Types ─────────────────────────────────────────────────────────────────────

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

// ── API calls ─────────────────────────────────────────────────────────────────

export async function fetchOpenTrainingStats(): Promise<OpenTrainingStats> {
    const res = await fetch(`${BASE}/api/open-training/stats`);
    if (!res.ok) throw new Error(`Stats fetch failed: ${res.status}`);
    return res.json();
}

export async function fetchOpenTrainingDatasets(): Promise<OpenTrainingDataset[]> {
    const res = await fetch(`${BASE}/api/open-training/datasets`);
    if (!res.ok) throw new Error(`Datasets fetch failed: ${res.status}`);
    return res.json();
}

export async function fetchTopLabels(
    labelType: "popularity_prior" | "deal_score" = "popularity_prior",
    take: number = 20,
): Promise<TopLabel[]> {
    const params = new URLSearchParams({ labelType, take: String(take) });
    const res = await fetch(`${BASE}/api/open-training/labels/top?${params}`);
    if (!res.ok) throw new Error(`Top labels fetch failed: ${res.status}`);
    return res.json();
}

export async function recomputeLabels(
    body: RecomputeLabelsRequest,
): Promise<RecomputeLabelsResult> {
    const res = await fetch(`${BASE}/api/open-training/recompute-labels`, {
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

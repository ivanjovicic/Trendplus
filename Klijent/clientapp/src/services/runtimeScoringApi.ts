const BASE = import.meta.env.VITE_API_URL ?? import.meta.env.VITE_API_BASE_URL ?? "";

export interface RuntimeScoringEvaluateRequest {
    imageFile: File;
    cost?: number;
    targetPrice?: number;
    brand?: string;
    category?: string;
    market?: string;
    // Local-data scoring inputs
    dobavljacId?: number;
    tipObuceId?: number;
    sezonaId?: number;
    velicina?: string;
    boja?: string;
    materijal?: string;
}

export interface RuntimeScoringSimilarProduct {
    productId: number;
    productName: string;
    similarity: number;
    imageFileName: string | null;
    brand: string | null;
    shoeType: string | null;
}

export interface RuntimeScoringEvaluateResponse {
    finalScore: number;
    sellProbabilityRS: number;
    priceFitScore: number;
    popularityScore: number;
    dealScore: number;
    marginScore: number;
    trendMomentum: number;
    recommendedPriceRange: string;
    marketDemandScore: number;
    imageSimilarityScore: number;
    sourceCoverageScore: number;
    sourceCoverageCount: number;
    // Local-data scores
    supplierScore: number;
    shoeTypeScore: number;
    seasonalScore: number;
    sizeColorScore: number;
    materialScore: number;
    localDemandScore: number;
    hasTrainingSignal: boolean;
    usedPythonModel: boolean;
    market: string;
    currency: string | null;
    typicalPrice: number | null;
    verdict: string;
    verdictColor: "green" | "blue" | "amber" | "orange" | "red" | "gray";
    scoreLabel: string;
    confidence: number;
    pricePositioning: string;
    insights: string[];
    similarProducts: RuntimeScoringSimilarProduct[];
}

function isFiniteNumber(value: number | undefined): value is number {
    return typeof value === "number" && Number.isFinite(value);
}

export async function evaluateRuntimeScoring(
    request: RuntimeScoringEvaluateRequest,
): Promise<RuntimeScoringEvaluateResponse> {
    const formData = new FormData();
    formData.append("image", request.imageFile);

    if (isFiniteNumber(request.cost)) formData.append("cost", String(request.cost));
    if (isFiniteNumber(request.targetPrice)) formData.append("targetPrice", String(request.targetPrice));
    if (request.brand?.trim()) formData.append("brand", request.brand.trim());
    if (request.category?.trim()) formData.append("category", request.category.trim());
    if (request.market?.trim()) formData.append("market", request.market.trim().toUpperCase());
    if (isFiniteNumber(request.dobavljacId)) formData.append("dobavljacId", String(request.dobavljacId));
    if (isFiniteNumber(request.tipObuceId)) formData.append("tipObuceId", String(request.tipObuceId));
    if (isFiniteNumber(request.sezonaId)) formData.append("sezonaId", String(request.sezonaId));
    if (request.velicina?.trim()) formData.append("velicina", request.velicina.trim());
    if (request.boja?.trim()) formData.append("boja", request.boja.trim());
    if (request.materijal?.trim()) formData.append("materijal", request.materijal.trim());

    const res = await fetch(`${BASE}/api/v1/scoring/evaluate`, {
        method: "POST",
        body: formData,
    });

    if (!res.ok) {
        let message = `Runtime scoring failed (${res.status})`;
        try {
            const err = await res.json();
            if (typeof err?.detail === "string" && err.detail.length > 0) {
                message = err.detail;
            } else if (typeof err?.error === "string" && err.error.length > 0) {
                message = err.error;
            } else if (typeof err?.title === "string" && err.title.length > 0) {
                message = err.title;
            }
        } catch {
            // keep generic message
        }
        throw new Error(message);
    }

    return res.json();
}

export function getRuntimeProductImageUrl(imageFileName: string | null | undefined): string | null {
    if (!imageFileName) return null;
    return `${BASE}/product-images/${encodeURIComponent(imageFileName)}`;
}

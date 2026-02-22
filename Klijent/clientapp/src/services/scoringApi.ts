const PYTHON_BASE = (import.meta.env.VITE_PYTHON_API_URL ?? "").replace(/\/+$/, "");

// ── Types ────────────────────────────────────────────────────────────────────

export type DashboardRun = {
    runId: number;
    startedAt: string;
    finishedAt: string | null;
    status: string;
    totalItems: number;
};

export type DashboardItem = {
    rank: number;
    itemId: number;
    brand: string | null;
    name: string | null;
    category: string | null;
    imageUrl: string | null;
    finalScore: number;
    baseScore: number | null;
    momentumRaw: number | null;
    momentumNormalized: number | null;
    appearanceCount: number;
    sourceCount: number;
    marketCount: number;
    scoreComponents: Record<string, number> | null;
    markets: string[] | null;
    sources: string[] | null;
    minPrice: number | null;
    maxPrice: number | null;
    prevFinalScore: number | null;
    totalRunAppearances: number;
    canonicalKey: string | null;
};

export type DashboardData = {
    run: DashboardRun | null;
    items: DashboardItem[];
    message?: string;
};

// ── API calls ────────────────────────────────────────────────────────────────

export async function fetchGlobalTop10(
    items: any[],
    shoeType?: string | null,
    topN = 10,
): Promise<any[]> {
    const url = PYTHON_BASE ? `${PYTHON_BASE}/api/global-top10` : "/api/global-top10";
    const resp = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ items, shoeType: shoeType ?? null, topN }),
    });
    if (!resp.ok) throw new Error(`Scorer ${resp.status}`);
    const data = await resp.json();
    return data.top10 ?? [];
}

export async function fetchDashboard(limit = 20): Promise<DashboardData> {
    const url = PYTHON_BASE
        ? `${PYTHON_BASE}/api/dashboard/latest?limit=${limit}`
        : `/api/dashboard/latest?limit=${limit}`;
    const resp = await fetch(url);
    if (!resp.ok) throw new Error(`Dashboard ${resp.status}`);
    const data = await resp.json();
    // snake_case → camelCase mapping for items
    const items: DashboardItem[] = (data.items ?? []).map((r: any) => ({
        rank:                 r.rank,
        itemId:              r.item_id,
        brand:               r.brand,
        name:                r.name,
        category:            r.category,
        imageUrl:            r.image_url,
        finalScore:          r.final_score,
        baseScore:           r.base_score,
        momentumRaw:         r.momentum_raw,
        momentumNormalized:  r.momentum_normalized,
        appearanceCount:     r.appearance_count,
        sourceCount:         r.source_count,
        marketCount:         r.market_count,
        scoreComponents:     r.score_components,
        markets:             r.markets,
        sources:             r.sources,
        minPrice:            r.min_price,
        maxPrice:            r.max_price,
        prevFinalScore:      r.prev_final_score,
        totalRunAppearances: r.total_run_appearances,
        canonicalKey:        r.canonical_key,
    }));
    return { run: data.run, items, message: data.message };
}

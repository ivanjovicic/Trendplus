// ── Types ────────────────────────────────────────────────────────────────────

export type AmazonShoeProduct = {
    id: number;
    asin: string;
    name: string | null;
    brand: string | null;
    price: number | null;
    originalPrice: number | null;
    currency: string | null;
    rating: number;
    reviewCount: number;
    trendScore: number;
    imageUrl: string | null;
    productUrl: string | null;
    category: string | null;
    gender: string | null;
    domain: string | null;
    lastSynced: string;
    createdAt: string;
};

export type PagedResult<T> = {
    items: T[];
    total: number;
    page: number;
    pageSize: number;
    pages: number;
};

export type SyncResult = {
    total: number;
    inserted: number;
    updated: number;
    type: string;
};

export type CategorySummary = {
    category: string | null;
    count: number;
    avgRating: number;
    avgPrice: number | null;
    lastSynced: string;
};

// ── API base ─────────────────────────────────────────────────────────────────

const API_BASE = (import.meta.env.VITE_API_URL ?? "").replace(/\/+$/, "");

async function get<T>(path: string): Promise<T> {
    const resp = await fetch(`${API_BASE}${path}`);
    if (!resp.ok) {
        const txt = await resp.text().catch(() => resp.statusText);
        throw new Error(`${resp.status}: ${txt}`);
    }
    return resp.json();
}

// ── Calls ────────────────────────────────────────────────────────────────────

export async function syncAmazonShoes(
    type: string,
    gender?: string | null,
    minPrice?: number | null,
    maxPrice?: number | null,
): Promise<SyncResult> {
    let url = `/api/shoes/sync?type=${encodeURIComponent(type)}`;
    if (gender && gender !== "all") url += `&gender=${encodeURIComponent(gender)}`;
    if (minPrice != null) url += `&minPrice=${minPrice}`;
    if (maxPrice != null) url += `&maxPrice=${maxPrice}`;
    return get<SyncResult>(url);
}

export async function getAmazonShoesByType(
    type: string,
    gender?: string | null,
    sortBy = "rating",
    page = 1,
    pageSize = 20,
): Promise<PagedResult<AmazonShoeProduct>> {
    let url = `/api/shoes?type=${encodeURIComponent(type)}&page=${page}&pageSize=${pageSize}&sortBy=${sortBy}`;
    if (gender && gender !== "all") url += `&gender=${encodeURIComponent(gender)}`;
    return get(url);
}

export async function getAllAmazonShoes(
    page = 1,
    pageSize = 50,
): Promise<PagedResult<AmazonShoeProduct>> {
    return get(`/api/shoes/all?page=${page}&pageSize=${pageSize}`);
}

export async function getAmazonShoeCategories(): Promise<CategorySummary[]> {
    return get(`/api/shoes/categories`);
}

export async function deleteAmazonShoeCategory(category: string): Promise<{ deleted: number }> {
    const resp = await fetch(
        `${API_BASE}/api/shoes/category/${encodeURIComponent(category)}`,
        { method: "DELETE" },
    );
    if (!resp.ok) throw new Error(`DELETE ${resp.status}`);
    return resp.json();
}

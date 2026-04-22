import { apiUrl } from "../utils/apiUrl";

// ── Types ─────────────────────────────────────────────────────────────────────

export interface GoogleShoppingProduct {
    id:          number;
    productId:   string | null;
    title:       string | null;
    brand:       string | null;
    price:       number | null;
    currency:    string | null;
    rating:      number;
    reviewCount: number;
    position:    number;
    imageUrl:    string | null;
    productUrl:  string | null;
    category:    string | null;
    gender:      string | null;
    domain:      string | null;
    trendScore:  number;
    lastSynced:  string;
    createdAt:   string;
}

export interface GooglePagedResult<T> {
    items:    T[];
    total:    number;
    page:     number;
    pageSize: number;
    pages:    number;
}

export interface GoogleSyncResult {
    total:    number;
    inserted: number;
    updated:  number;
    type:     string;
}

export interface GoogleCategorySummary {
    category:   string | null;
    count:      number;
    avgRating:  number;
    avgPrice:   number | null;
    lastSynced: string;
}

// ── API calls ─────────────────────────────────────────────────────────────────

export async function syncGoogleShopping(
    type:      string,
    gender?:   string | null,
    minPrice?: number | null,
    maxPrice?: number | null,
): Promise<GoogleSyncResult> {
    const params = new URLSearchParams({ type });
    if (gender && gender !== "all") params.append("gender", gender);
    if (minPrice != null) params.append("minPrice", String(minPrice));
    if (maxPrice != null) params.append("maxPrice", String(maxPrice));
    const res = await fetch(apiUrl(`/api/google/shopping/sync?${params}`));
    if (!res.ok) throw new Error(`Sync failed: ${res.status} ${await res.text()}`);
    return res.json();
}

export async function getGoogleShoppingByType(
    type:     string,
    gender?:  string | null,
    sortBy  = "score",
    page     = 1,
    pageSize = 20,
): Promise<GooglePagedResult<GoogleShoppingProduct>> {
    const params = new URLSearchParams({ type, page: String(page), pageSize: String(pageSize), sortBy });
    if (gender && gender !== "all") params.append("gender", gender);
    const res = await fetch(apiUrl(`/api/google/shopping?${params}`));
    if (!res.ok) throw new Error(`Fetch failed: ${res.status}`);
    return res.json();
}

export async function getAllGoogleShopping(
    page     = 1,
    pageSize = 50,
): Promise<GooglePagedResult<GoogleShoppingProduct>> {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    const res = await fetch(apiUrl(`/api/google/shopping/all?${params}`));
    if (!res.ok) throw new Error(`Fetch failed: ${res.status}`);
    return res.json();
}

export async function getGoogleShoppingCategories(): Promise<GoogleCategorySummary[]> {
    const res = await fetch(apiUrl("/api/google/shopping/categories"));
    if (!res.ok) throw new Error(`Fetch failed: ${res.status}`);
    return res.json();
}

export async function deleteGoogleShoppingCategory(category: string): Promise<{ deleted: number; category: string }> {
    const res = await fetch(apiUrl(`/api/google/shopping/category/${encodeURIComponent(category)}`), { method: "DELETE" });
    if (!res.ok) throw new Error(`Delete failed: ${res.status}`);
    return res.json();
}

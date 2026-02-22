const BASE = import.meta.env.VITE_API_URL ?? "";

// ── Types ─────────────────────────────────────────────────────────────────────

export interface EbayShoeProduct {
    id:          number;
    ebayItemId:  string;
    name:        string | null;
    brand:       string | null;
    condition:   string | null;
    price:       number | null;
    currency:    string | null;
    rating:      number;
    reviewCount: number;
    imageUrl:    string | null;
    productUrl:  string | null;
    category:    string | null;
    marketplace: string | null;
    lastSynced:  string;
    createdAt:   string;
}

export interface EbayPagedResult<T> {
    items:    T[];
    total:    number;
    page:     number;
    pageSize: number;
    pages:    number;
}

export interface EbaySyncResult {
    total:    number;
    inserted: number;
    updated:  number;
    type:     string;
}

export interface EbayCategorySummary {
    category:   string | null;
    count:      number;
    avgRating:  number;
    avgPrice:   number | null;
    lastSynced: string;
}

// ── API calls ─────────────────────────────────────────────────────────────────

export async function syncEbayShoes(
    type:      string,
    minPrice?: number | null,
    maxPrice?: number | null,
): Promise<EbaySyncResult> {
    const params = new URLSearchParams({ type });
    if (minPrice != null) params.append("minPrice", String(minPrice));
    if (maxPrice != null) params.append("maxPrice", String(maxPrice));
    const res = await fetch(`${BASE}/api/ebay/shoes/sync?${params}`);
    if (!res.ok) throw new Error(`Sync failed: ${res.status} ${await res.text()}`);
    return res.json();
}

export async function getEbayShoesByType(
    type:     string,
    page     = 1,
    pageSize = 20,
): Promise<EbayPagedResult<EbayShoeProduct>> {
    const params = new URLSearchParams({ type, page: String(page), pageSize: String(pageSize) });
    const res = await fetch(`${BASE}/api/ebay/shoes?${params}`);
    if (!res.ok) throw new Error(`Fetch failed: ${res.status}`);
    return res.json();
}

export async function getAllEbayShoes(
    page     = 1,
    pageSize = 50,
): Promise<EbayPagedResult<EbayShoeProduct>> {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    const res = await fetch(`${BASE}/api/ebay/shoes/all?${params}`);
    if (!res.ok) throw new Error(`Fetch failed: ${res.status}`);
    return res.json();
}

export async function getEbayShoeCategories(): Promise<EbayCategorySummary[]> {
    const res = await fetch(`${BASE}/api/ebay/shoes/categories`);
    if (!res.ok) throw new Error(`Fetch failed: ${res.status}`);
    return res.json();
}

export async function deleteEbayShoeCategory(category: string): Promise<{ deleted: number; category: string }> {
    const res = await fetch(`${BASE}/api/ebay/shoes/category/${encodeURIComponent(category)}`, { method: "DELETE" });
    if (!res.ok) throw new Error(`Delete failed: ${res.status}`);
    return res.json();
}

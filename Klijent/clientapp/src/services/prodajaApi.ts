import { appendDataScopeToParams } from "../utils/dataScope";
import { apiUrl } from "../utils/apiUrl";

const LOOKUP_CACHE_TTL_MS = 15 * 1000;

export type ProdajaArtikalLookupDto = {
    id: number;
    naziv: string;
    cena: number;
    kolicina: number;
};

const lookupCache = new Map<string, { expiresAt: number; data: ProdajaArtikalLookupDto[] }>();
const lookupInFlight = new Map<string, Promise<ProdajaArtikalLookupDto[]>>();

export async function fetchProdajaArtikliLookup(
    query?: string,
    take: number = 30,
    includeZeroStock: boolean = false
): Promise<ProdajaArtikalLookupDto[]> {
    const params = new URLSearchParams();
    if (query?.trim()) params.set("q", query.trim());
    params.set("take", String(Math.max(1, Math.min(200, take))));
    params.set("includeZeroStock", String(includeZeroStock));
    appendDataScopeToParams(params);

    const key = params.toString();
    const now = Date.now();
    const cached = lookupCache.get(key);
    if (cached && now < cached.expiresAt) {
        return cached.data;
    }

    const pending = lookupInFlight.get(key);
    if (pending) {
        return pending;
    }

    const request = (async () => {
        const res = await fetch(apiUrl(`/api/artikli/lookup?${params.toString()}`));
        if (!res.ok) {
            const body = await res.json().catch(() => null);
            const message = body?.detail ?? body?.title ?? body?.error ?? `HTTP ${res.status}`;
            throw new Error(message);
        }

        const data = await res.json() as ProdajaArtikalLookupDto[];
        lookupCache.set(key, { expiresAt: Date.now() + LOOKUP_CACHE_TTL_MS, data });
        return data;
    })();

    lookupInFlight.set(key, request);
    try {
        return await request;
    } finally {
        lookupInFlight.delete(key);
    }
}

export function clearProdajaLookupCache(): void {
    lookupCache.clear();
    lookupInFlight.clear();
}

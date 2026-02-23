import type { Sezona } from "../types/Sezona";
import { getDataScope } from "../utils/dataScope";

const API = import.meta.env.VITE_API_BASE_URL as string;
const SEZONE_CACHE_TTL_MS = 5 * 60 * 1000;

const sezoneCache = new Map<string, { data: Sezona[]; expiresAt: number }>();
const sezoneInFlight = new Map<string, Promise<Sezona[]>>();

export async function getSezone(): Promise<Sezona[]> {
    const scope = getDataScope();
    const cacheKey = `sezone:${scope}`;
    const now = Date.now();
    const cached = sezoneCache.get(cacheKey);
    if (cached && now < cached.expiresAt) {
        return cached.data;
    }

    const inflight = sezoneInFlight.get(cacheKey);
    if (inflight) {
        return inflight;
    }

    const request = (async () => {
        const res = await fetch(`${API}/api/sezone?dataScope=${encodeURIComponent(scope)}`);
        if (!res.ok) throw new Error("Ne mogu da dohvatim sezone");
        const data = await res.json() as Sezona[];
        sezoneCache.set(cacheKey, {
            data,
            expiresAt: Date.now() + SEZONE_CACHE_TTL_MS,
        });
        return data;
    })();

    sezoneInFlight.set(cacheKey, request);

    try {
        return await request;
    } finally {
        sezoneInFlight.delete(cacheKey);
    }
}

export async function createSezona(naziv: string, datumOd: string, datumDo: string): Promise<number> {
    // Convert local date strings to UTC ISO strings to avoid timezone issues
    const odDate = new Date(datumOd);
    const doDate = new Date(datumDo);
    
    const res = await fetch(`${API}/api/sezone`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
            Naziv: naziv,
            DatumOd: odDate.toISOString(), // Convert to UTC
            DatumDo: doDate.toISOString()  // Convert to UTC
        }),
    });

    if (!res.ok) {
        const body = await res.json().catch(() => null);
        const message = body?.detail ?? body?.message ?? body?.error ?? `HTTP ${res.status}`;
        throw new Error(message);
    }

    const data = await res.json();
    return data.id;
}

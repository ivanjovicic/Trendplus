import type { Sezona } from "../types/Sezona";

const API = import.meta.env.VITE_API_BASE_URL as string;
const SEZONE_CACHE_TTL_MS = 5 * 60 * 1000;

let sezoneCache: Sezona[] | null = null;
let sezoneCacheExpiresAt = 0;
let sezoneInFlight: Promise<Sezona[]> | null = null;

export async function getSezone(): Promise<Sezona[]> {
    const now = Date.now();
    if (sezoneCache && now < sezoneCacheExpiresAt) {
        return sezoneCache;
    }

    if (sezoneInFlight) {
        return sezoneInFlight;
    }

    sezoneInFlight = (async () => {
        const res = await fetch(`${API}/api/sezone`);
        if (!res.ok) throw new Error("Ne mogu da dohvatim sezone");
        const data = await res.json() as Sezona[];
        sezoneCache = data;
        sezoneCacheExpiresAt = Date.now() + SEZONE_CACHE_TTL_MS;
        return data;
    })();

    try {
        return await sezoneInFlight;
    } finally {
        sezoneInFlight = null;
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

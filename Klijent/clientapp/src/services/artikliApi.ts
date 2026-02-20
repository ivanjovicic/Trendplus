import { type Artikal } from "../types/Artikal";
import { ArtikalFormData } from "../types/artikalformdata";
import { NivelacijeResponse } from "../types/nivelacije";

export type CreateArtikalDto = {
    PLU?: string | null;
    Naziv: string;
    ProdajnaCena: number;
    NabavnaCena?: number | null;
    NabavnaCenaDin?: number | null;
    PrvaProdajnaCena?: number | null;
    Kolicina?: number | null;
    Komentar?: string | null;
    // client-friendly camelCase keys also accepted by server mapping
    tipObuceId?: number | null;
    dobavljacId?: number | null;
    idObjekat?: number | null;
    idSezona?: number | null;
};
const API = import.meta.env.VITE_API_BASE_URL;
const ARTIKLI_PAGED_CACHE_TTL_MS = 30 * 1000;
const artikliPagedCache = new Map<string, { expiresAt: number; data: ArtikliPagedResponse<any> }>();
const artikliPagedInFlight = new Map<string, Promise<ArtikliPagedResponse<any>>>();

export type ArtikliPagedResponse<T> = {
    items: T[];
    totalCount: number;
    pageNumber: number;
    pageSize: number;
};

export async function createArtikal(payload: CreateArtikalDto): Promise<number> {
    const res = await fetch(`${API}/artikli`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
    });

    if (!res.ok) {
        let body: any = null;
        try {
            body = await res.json();
        } catch {
            try {
                body = { message: await res.text() };
            } catch {
                body = null;
            }
        }

        const message = body?.detail ?? body?.message ?? body?.error ?? `HTTP ${res.status}`;
        throw new Error(message);
    }

    const data = await res.json().catch(() => null);
    if (!data || typeof data.id === "undefined") {
        throw new Error("Unexpected server response when creating artikal.");
    }

    return data.id;
}

export async function getArtikal(id: number): Promise<Artikal> {
    const res = await fetch(`${API}/artikli/${id}`);
    if (!res.ok) throw new Error("Artikal ne postoji");
    return res.json();
}

export async function updateArtikal(id: number, data: ArtikalFormData): Promise<void> {
    const dto = {
        naziv: data.naziv,
        prodajnaCena: data.prodajnaCena,
        nabavnaCena: data.nabavnaCena ?? null,
        nabavnaCenaDin: data.nabavnaCenaDin ?? null,
        prvaProdajnaCena: data.prvaProdajnaCena ?? null,
        kolicina: data.kolicina ?? null,
        komentar: data.komentar ?? null,
        tipObuceId: data.tipObuceId ?? null,
        dobavljacId: data.dobavljacId ?? null,
        idObjekat: null,
        idSezona: data.idSezona ?? null,
    };

    const resp = await fetch(`${API}/artikli/${id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(dto),
    });

    if (!resp.ok) {
        // u catch granama, ako body ima correlationId:
        const body = await resp.json().catch(() => null);
        const correlationId = body?.correlationId;
        const message = body?.detail ?? body?.title ?? `HTTP ${resp.status}`;
        throw new Error(
          correlationId ? `${message} (CorrelationId: ${correlationId})` : message
        );
    }
}

export async function getArtikli(): Promise<Artikal[]> {
    const res = await fetch(`${API}/artikli`);
    if (!res.ok) {
        throw new Error(`Greška pri učitavanju artikala: ${res.status}`);
    }
    return res.json();
}

export async function getArtikliPaged<T = any>(
    pageNumber: number = 1,
    pageSize: number = 50,
    filters?: {
        naziv?: string;
        sezonaId?: number | "";
        minCena?: number;
        maxCena?: number;
        minKolicina?: number;
        maxKolicina?: number;
        sortBy?: "naziv" | "prodajnaCena" | "nabavnaCena" | "kolicina" | "id";
        sortDir?: "asc" | "desc";
    }
): Promise<ArtikliPagedResponse<T>> {
    const params = new URLSearchParams({
        pageNumber: String(pageNumber),
        pageSize: String(pageSize),
    });

    if (filters?.naziv) params.append("naziv", filters.naziv);
    if (filters?.sezonaId !== undefined && filters.sezonaId !== "") params.append("sezonaId", String(filters.sezonaId));
    if (filters?.minCena !== undefined) params.append("minCena", String(filters.minCena));
    if (filters?.maxCena !== undefined) params.append("maxCena", String(filters.maxCena));
    if (filters?.minKolicina !== undefined) params.append("minKolicina", String(filters.minKolicina));
    if (filters?.maxKolicina !== undefined) params.append("maxKolicina", String(filters.maxKolicina));
    if (filters?.sortBy) params.append("sortBy", filters.sortBy);
    if (filters?.sortDir) params.append("sortDir", filters.sortDir);

    const cacheKey = params.toString();
    const now = Date.now();
    const cached = artikliPagedCache.get(cacheKey);
    if (cached && now < cached.expiresAt) {
        return cached.data as ArtikliPagedResponse<T>;
    }

    const existingInFlight = artikliPagedInFlight.get(cacheKey);
    if (existingInFlight) {
        return existingInFlight as Promise<ArtikliPagedResponse<T>>;
    }

    const request = (async () => {
        const res = await fetch(`${API}/api/artikli?${params.toString()}`);
        if (!res.ok) {
            const body = await res.json().catch(() => null);
            const message = body?.detail ?? body?.title ?? body?.error ?? `HTTP ${res.status}`;
            throw new Error(message);
        }

        const data = await res.json() as ArtikliPagedResponse<T>;
        artikliPagedCache.set(cacheKey, {
            expiresAt: Date.now() + ARTIKLI_PAGED_CACHE_TTL_MS,
            data: data as ArtikliPagedResponse<any>,
        });
        return data;
    })();

    artikliPagedInFlight.set(cacheKey, request as Promise<ArtikliPagedResponse<any>>);

    try {
        return await request;
    } finally {
        artikliPagedInFlight.delete(cacheKey);
    }
}

export async function nivelacijaCena(artikalId: number, novaProdajnaCena: number, komentar?: string): Promise<void> {
    const resp = await fetch(`${API}/api/nivelacija`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ artikalId, novaProdajnaCena, komentar: komentar ?? null }),
    });

    if (!resp.ok) {
        const body = await resp.json().catch(() => null);
        const correlationId = body?.correlationId;
        const message = body?.detail ?? body?.title ?? body?.error ?? `HTTP ${resp.status}`;
        throw new Error(correlationId ? `${message} (CorrelationId: ${correlationId})` : message);
    }
}

export async function getNivelacije(
    pageNumber: number = 1,
    pageSize: number = 50,
    filters?: {
        artikalId?: number;
        naziv?: string;
        fromDate?: string;
        toDate?: string;
        sortBy?: string;
        sortDir?: "asc" | "desc";
    }
): Promise<NivelacijeResponse> {
    const params = new URLSearchParams({
        pageNumber: String(pageNumber),
        pageSize: String(pageSize),
    });

    if (filters?.artikalId) params.append("artikalId", String(filters.artikalId));
    if (filters?.naziv) params.append("naziv", filters.naziv);
    if (filters?.fromDate) params.append("fromDate", filters.fromDate);
    if (filters?.toDate) params.append("toDate", filters.toDate);
    if (filters?.sortBy) params.append("sortBy", filters.sortBy);
    if (filters?.sortDir) params.append("sortDir", filters.sortDir);

    const resp = await fetch(`${API}/api/nivelacije?${params.toString()}`);
    if (!resp.ok) {
        const body = await resp.json().catch(() => null);
        const message = body?.detail ?? body?.title ?? body?.error ?? `HTTP ${resp.status}`;
        throw new Error(message);
    }

    return resp.json();
}

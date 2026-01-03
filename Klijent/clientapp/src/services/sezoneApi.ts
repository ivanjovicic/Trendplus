import type { Sezona } from "../types/Sezona";

const API = import.meta.env.VITE_API_BASE_URL as string;

export async function getSezone(): Promise<Sezona[]> {
    const res = await fetch(`${API}/api/sezone`);
    if (!res.ok) throw new Error("Ne mogu da dohvatim sezone");
    return res.json();
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

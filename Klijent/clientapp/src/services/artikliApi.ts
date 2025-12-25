import { type Artikal } from "../types/Artikal";

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
    const res = await fetch(`${ API }/artikli/${id}`);
    if (!res.ok) throw new Error("Artikal ne postoji");
    return res.json();
}

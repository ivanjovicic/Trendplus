import { type Artikal } from "../types/Artikal";
import { ArtikalFormData } from "../types/artikalformdata";

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
        idSezona: null,
    };

    const resp = await fetch(`${API}/artikli/${id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(dto),
    });

    if (!resp.ok) {
        throw new Error(`UpdateArtikal failed: ${resp.status}`);
    }
}

export async function getArtikli(): Promise<Artikal[]> {
    const res = await fetch(`${API}/artikli`);
    if (!res.ok) {
        throw new Error(`Greška pri učitavanju artikala: ${res.status}`);
    }
    return res.json();
}

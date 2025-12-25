import type { TipObuce } from "../types/TipObuce";

export async function getTipoviObuce(): Promise<TipObuce[]> {
    const res = await fetch(`${API}/tipovi-obuce`);
    if (!res.ok) throw new Error("Ne mogu da dohvatim tipove obuće");
    return res.json();
}
const API = import.meta.env.VITE_API_BASE_URL;

export async function createTipObuce(naziv: string) {
    const res = await fetch(`${API}/tipovi-obuce`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ Naziv: naziv })
    });

    if (!res.ok) throw new Error('Ne mogu da kreiram tip');

    const data = await res.json();
    return data.id as number;
}

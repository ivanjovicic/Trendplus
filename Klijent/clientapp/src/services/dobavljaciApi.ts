import type { Dobavljac } from "../types/Dobavljaci";

export async function getDobavljaci(): Promise<Dobavljac[]> {
    const res = await fetch("/dobavljaci"); // backend endpoint za dobavljače
    if (!res.ok) throw new Error("Ne mogu da dohvatim dobavljače");
    return res.json();
}

export async function createDobavljac(naziv: string) {
    const res = await fetch('/dobavljaci', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ Naziv: naziv })
    });

    if (!res.ok) throw new Error('Ne mogu da kreiram dobavljača');

    const data = await res.json();
    return data.id as number;
}

import type { Dobavljac } from "../types/Dobavljaci";
import { getDataScope } from "../utils/dataScope";
import { apiUrl } from "../utils/apiUrl";

export async function getDobavljaci(): Promise<Dobavljac[]> {
    const scope = getDataScope();
    const res = await fetch(apiUrl(`/api/dobavljaci?dataScope=${encodeURIComponent(scope)}`));
    if (!res.ok) throw new Error("Ne mogu da dohvatim dobavljače");
    return res.json();
}

export async function createDobavljac(
    naziv: string,
    adresa?: string,
    telefon?: string,
    napomena?: string
): Promise<number> {
    const res = await fetch(apiUrl("/api/dobavljaci"), {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
            Naziv: naziv,
            Adresa: adresa || null,
            Telefon: telefon || null,
            Napomena: napomena || null
        })
    });

    if (!res.ok) throw new Error("Ne mogu da kreiram dobavljača");

    const data = await res.json();
    return data.id as number;
}

import { useState } from "react";
import { KreirajProdajuDto } from "../../types/prodaja/prodaja";

interface CreateProdajaFormProps {
    artikli: { id: number; naziv: string; cena: number }[];
    onSubmit: (data: KreirajProdajuDto) => Promise<void>;
}

export default function CreateProdajaForm({ artikli, onSubmit }: CreateProdajaFormProps) {
    const [brojRacuna, setBrojRacuna] = useState("");
    const [stavke, setStavke] = useState<{ idArtikal: number; kolicina: number; cena: number }[]>(
        [
            { idArtikal: artikli[0]?.id ?? 0, kolicina: 1, cena: artikli[0]?.cena ?? 0 },
        ]
    );
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const addStavka = () =>
        setStavke((s) => [
            ...s,
            { idArtikal: artikli[0]?.id ?? 0, kolicina: 1, cena: artikli[0]?.cena ?? 0 },
        ]);

    const removeStavka = (index: number) => setStavke((s) => s.filter((_, i) => i !== index));

    const updateStavka = (
        index: number,
        patch: Partial<{ idArtikal: number; kolicina: number; cena: number }>
    ) =>
        setStavke((s) => {
            const copy = [...s];
            copy[index] = { ...copy[index], ...patch };
            return copy;
        });

    const handleSubmit = async () => {
        setError(null);

        if (!stavke.length) {
            setError("Dodajte bar jednu stavku.");
            return;
        }

        const payload: KreirajProdajuDto = {
            brojRacuna,
            idObjekat: 1,
            nacinPlacanja: "Gotovina",
            stavke,
        };

        setIsSubmitting(true);
        try {
            await onSubmit(payload);
            // reset
            setBrojRacuna("");
            setStavke([{ idArtikal: artikli[0]?.id ?? 0, kolicina: 1, cena: artikli[0]?.cena ?? 0 }]);
            alert("Prodaja uspešna ✔️");
        } catch (err: any) {
            console.error(err);
            setError(err?.message ?? "Greška pri kreiranju prodaje");
        } finally {
            setIsSubmitting(false);
        }
    };

    return (
        <div>
            <div style={{ marginBottom: 12 }}>
                <input
                    placeholder="Broj računa"
                    value={brojRacuna}
                    onChange={(e) => setBrojRacuna(e.target.value)}
                    className="input-big"
                />
            </div>

            {stavke.map((s, i) => (
                <div key={i} style={{ display: "flex", gap: 8, alignItems: "center", marginBottom: 8 }}>
                    <select
                        value={s.idArtikal}
                        onChange={(e) => {
                            const id = Number(e.target.value);
                            const art = artikli.find((a) => a.id === id);
                            updateStavka(i, { idArtikal: id, cena: art?.cena ?? s.cena });
                        }}
                        style={{ minWidth: 220 }}
                        className="input-big"
                        aria-label={`Artikal ${i + 1}`}
                    >
                        {artikli.map((a) => (
                            <option key={a.id} value={a.id}>
                                {a.naziv} — {a.cena}
                            </option>
                        ))}
                    </select>

                    <input
                        type="number"
                        min={1}
                        value={s.kolicina}
                        onChange={(e) => updateStavka(i, { kolicina: Number(e.target.value) })}
                        style={{ width: 100 }}
                        className="input-big"
                        aria-label={`Količina ${i + 1}`}
                    />

                    <input
                        type="number"
                        min={0}
                        value={s.cena}
                        onChange={(e) => updateStavka(i, { cena: Number(e.target.value) })}
                        style={{ width: 140 }}
                        className="input-big"
                        aria-label={`Cena ${i + 1}`}
                    />

                    <button
                        type="button"
                        className="button-big"
                        onClick={() => removeStavka(i)}
                        style={{ height: 40 }}
                    >
                        Ukloni
                    </button>
                </div>
            ))}

            <div style={{ display: "flex", gap: 8, marginTop: 8 }}>
                <button type="button" className="button-big" onClick={addStavka}>
                    Dodaj stavku
                </button>

                <button type="button" className="button-big" onClick={handleSubmit} disabled={isSubmitting}>
                    {isSubmitting ? "Kreiram..." : "Sačuvaj prodaju"}
                </button>
            </div>

            {error && <p className="mt-4 text-lg font-medium error-msg">{error}</p>}
        </div>
    );
}

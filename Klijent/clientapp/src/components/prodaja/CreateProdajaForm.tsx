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

    const ukupno = stavke.reduce((sum, s) => sum + s.kolicina * s.cena, 0);

    return (
        <div className="card">
            <h2 className="text-2xl font-semibold mb-6">🛒 Nova prodaja</h2>

            <div style={{ marginBottom: '1.5rem' }}>
                <label className="field-label">Broj računa</label>
                <input
                    placeholder="Broj računa"
                    value={brojRacuna}
                    onChange={(e) => setBrojRacuna(e.target.value)}
                    className="input-big"
                />
            </div>

            <div style={{ marginBottom: '1.5rem' }}>
                <h3 className="text-lg font-semibold mb-4">Stavke</h3>
                
                {stavke.map((s, i) => (
                    <div key={i} style={{ display: 'flex', gap: 12, marginBottom: 12, alignItems: 'flex-start', flexWrap: 'wrap' }}>
                        <div style={{ flex: '1 1 200px', minWidth: '200px' }}>
                            <label className="field-label" style={{ fontSize: '0.875rem' }}>Artikal</label>
                            <select
                                value={s.idArtikal}
                                onChange={(e) => {
                                    const id = Number(e.target.value);
                                    const art = artikli.find((a) => a.id === id);
                                    updateStavka(i, { idArtikal: id, cena: art?.cena ?? s.cena });
                                }}
                                className="input-big"
                                style={{ marginTop: '0.25rem', marginBottom: 0 }}
                                aria-label={`Artikal ${i + 1}`}
                            >
                                {artikli.map((a) => (
                                    <option key={a.id} value={a.id}>
                                        {a.naziv} — {a.cena} RSD
                                    </option>
                                ))}
                            </select>
                        </div>

                        <div style={{ flex: '0 1 100px' }}>
                            <label className="field-label" style={{ fontSize: '0.875rem' }}>Količina</label>
                            <input
                                type="number"
                                min={1}
                                value={s.kolicina}
                                onChange={(e) => updateStavka(i, { kolicina: Number(e.target.value) })}
                                className="input-big"
                                style={{ marginTop: '0.25rem', marginBottom: 0 }}
                                aria-label={`Količina ${i + 1}`}
                            />
                        </div>

                        <div style={{ flex: '0 1 120px' }}>
                            <label className="field-label" style={{ fontSize: '0.875rem' }}>Cena (RSD)</label>
                            <input
                                type="number"
                                min={0}
                                value={s.cena}
                                onChange={(e) => updateStavka(i, { cena: Number(e.target.value) })}
                                className="input-big"
                                style={{ marginTop: '0.25rem', marginBottom: 0 }}
                                aria-label={`Cena ${i + 1}`}
                            />
                        </div>

                        <div style={{ flex: '0 0 auto', paddingTop: '1.75rem' }}>
                            <button
                                type="button"
                                className="button-big"
                                onClick={() => removeStavka(i)}
                                style={{ 
                                    background: '#dc2626', 
                                    width: 'auto', 
                                    padding: '10px 16px',
                                    marginTop: 0
                                }}
                            >
                                Ukloni
                            </button>
                        </div>
                    </div>
                ))}

                <button 
                    type="button" 
                    className="button-big" 
                    onClick={addStavka}
                    style={{ 
                        background: '#059669', 
                        maxWidth: '200px',
                        marginTop: '1rem'
                    }}
                >
                    + Dodaj stavku
                </button>
            </div>

            <div style={{ 
                borderTop: '2px solid #e5e7eb', 
                paddingTop: '1rem', 
                marginBottom: '1rem',
                fontSize: '1.25rem',
                fontWeight: 600
            }}>
                Ukupno: {ukupno.toFixed(2)} RSD
            </div>

            <button 
                type="button" 
                className="button-big" 
                onClick={handleSubmit} 
                disabled={isSubmitting}
                style={{ maxWidth: '300px' }}
            >
                {isSubmitting ? "Kreiram..." : "💰 Sačuvaj prodaju"}
            </button>

            {error && <p className="error-msg" style={{ marginTop: '1rem' }}>{error}</p>}
        </div>
    );
}
